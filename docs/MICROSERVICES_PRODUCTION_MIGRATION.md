# Production Migration: Monolith → Microservices (single server)

> Deployment runbook for cutting the live monolith over to the microservices
> stack **on one server**. Pair it with [DEPLOYMENT.md](DEPLOYMENT.md) (deploy
> shapes), [MICROSERVICES.md](MICROSERVICES.md) (target architecture) and
> `SERVER_SETUP.md` (host provisioning).
>
> Single-server trade-offs (no fault isolation, shared blast radius, one box to
> max out) are accepted on purpose — this is a pet project. This doc does not
> re-argue that.

---

## 0. What actually changes

| | Monolith (today) | Microservices (target) |
|---|---|---|
| Backend processes | 1 (`api`) | 8 (`gateway` + 7 services) |
| Public entry | `api.DOMAIN` → monolith | `api.DOMAIN` → **gateway** → services |
| Postgres | 1 DB (`sallevate`) | 5 logical DBs (`identity`/`learning`/`gamification`/`ai`/`social`) on the **same** instance |
| Mongo | `sallevate` (2 collections) | same `sallevate` DB, split by collection (ai: `dialog_sessions`, social: `chat_conversations`) |
| Redis | 1 instance | 2 instances (`redis` + `analytics-redis`) |
| New infra | — | **Kafka** (KRaft) + kafka-ui + kafka-exporter |
| Frontend | Vercel | Vercel (unchanged — not on this server) |

The frontend stays on Vercel. The only thing the frontend needs is that
`api.DOMAIN` keeps answering — and it does, because the gateway now owns that host.

---

## 1. Resource impact (how much bigger the box must be)

**There is no GPU anywhere and that does not change.** All AI is external HTTP
(OpenAI-compatible LLM, Whisper STT, Yandex TTS). GPU need = **0 → 0**.

The cost is **RAM**, then disk, then a bit of CPU. Going from 1 .NET process to
8, plus adding a JVM (Kafka) and a second Redis, is the real jump.

### Steady-state RAM (rough working set, idle-to-light load)

| Component | Monolith | Microservices |
|---|---|---|
| .NET backend(s) | 1 × ~250 MB | 8 × ~120–180 MB ≈ **1.2–1.5 GB** |
| Postgres | ~150 MB | ~250 MB (5 DBs, more connections) |
| Mongo | ~150 MB | ~150 MB |
| Redis | ~30 MB | 2 × ~30 MB |
| **Kafka (JVM)** | — | **~600 MB–1 GB** |
| kafka-ui (JVM) | — | ~300–400 MB |
| kafka-exporter | — | ~20 MB |
| Loki + Prometheus + Grafana | ~500 MB | ~600 MB (more series/log volume) |
| MinIO | ~150 MB | ~150 MB |
| **Total working set** | **~1.5–2 GB** | **~4.5–6 GB** |

### Build-time RAM (the real spike)

`docker compose up --build` compiles **8 .NET images** instead of 1. Each
`dotnet publish` is memory-hungry; building several in parallel can momentarily
need **3–5 GB extra**. The 4 GB swap from `SERVER_SETUP.md` keeps this from OOM-ing
but builds get slow if you rely on swap. Mitigations: build images in CI / a
registry and `pull` on the server, or build services one at a time.

### CPU

- **Idle:** low. 8 .NET services idle are cheap; Kafka idles at a few % of one core.
- **Under load:** roughly additive to the monolith; the new tax is Kafka
  broker work + JSON (de)serialization of events between services. Budget **+1
  vCPU** over what the monolith used.
- **Build:** the spike — wants all cores. Same mitigation as RAM (build off-box).

### Disk

| | Monolith | Microservices |
|---|---|---|
| Images | 1 backend + 1 frontend + infra | **8 backend** + frontend + Kafka/kafka-ui images |
| Volumes | pg, mongo, redis, loki, prom, grafana, minio | + `analytics_redis_data`, **`kafka_data`** (grows with retention), bigger Loki/Prom |

Budget **+10–20 GB** for images and Kafka/observability data; watch Kafka log
retention and Loki/Prometheus retention. `deploy-prod.sh` already prunes dangling
images and build cache >72 h.

### Bottom line — server sizing

| | Was enough for monolith | Recommended for microservices |
|---|---|---|
| vCPU | 2 | **4** |
| RAM | 4 GB | **8 GB** (6 GB is the floor and will swap during builds) |
| Disk | 40 GB | **60–80 GB** SSD |
| GPU | none | none |

On Timeweb (Amsterdam), that's about one tier up from the monolith box. 8 GB RAM
is the line that matters most — at 4 GB the stack will not hold all services +
Kafka + builds.

---

## 2. Prerequisites (once)

- [ ] Server sized per §1 (4 vCPU / 8 GB / 60–80 GB). Keep the 4 GB swap from `SERVER_SETUP.md`.
- [ ] DNS unchanged: `DOMAIN`, `*.DOMAIN` → server IP. `api.DOMAIN` already used; the gateway takes it over.
- [ ] Root `.env` filled (see [.env.example](../.env.example)) — same secrets as the monolith **plus** nothing new is mandatory; the Kafka/Redis hostnames are internal defaults. Review the optional **OPERATIONAL TUNABLES** block if you want to change LLM model / token limits / voices / token lifetimes from `.env`.
- [ ] **Switch `ASPNETCORE_ENVIRONMENT` to `Production`** for a real prod deploy (the compose file ships `Development`, which exposes Swagger). Either edit the env lines or export `ASPNETCORE_ENVIRONMENT=Production` and templatize them.
- [ ] **Full backup taken** (see §3). Do not skip — this is the rollback.

---

## 3. Backup (the rollback safety net)

Run **before** touching anything. The migration is read-only on the monolith DB,
but back up regardless.

```bash
# On the server, from the repo root. .env vars are NOT in your shell, so read them
# from .env first (cut -f2- keeps values that contain '='):
cd ~/sellevate
PG_USER=$(grep -E '^POSTGRES_USER=' .env | cut -d= -f2-)
PG_DB=$(grep -E '^POSTGRES_DB=' .env | cut -d= -f2-)
PG_CID=$(docker compose ps -q postgres 2>/dev/null)
MONGO_CID=$(docker compose ps -q mongo 2>/dev/null)
TS=$(date +%Y%m%d-%H%M%S); mkdir -p ~/backups/$TS

# Postgres (the monolith DB). Plain `docker exec` avoids compose var-warnings.
docker exec "$PG_CID" pg_dump -U "$PG_USER" -d "$PG_DB" \
  | gzip > ~/backups/$TS/monolith-pg.sql.gz

# Mongo
docker exec "$MONGO_CID" mongodump --archive --db=sallevate \
  | gzip > ~/backups/$TS/mongo-sallevate.archive.gz

# Snapshot the whole VM too if Timeweb offers it (one click = cheapest rollback).
```

---

## 4. Cutover order

The DB-per-service split needs the empty schemas to exist *before* data is copied,
because the services own their migrations. So the order is: **bring services up
once (they self-create their DBs+tables) → stop them → copy data → start again.**

### Step 1 — Deploy code, let services bootstrap their schemas

```bash
cd ~/sellevate            # repo root on the server
git pull --ff-only
docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build -d
```

On first start each relational service runs `DatabaseBootstrapper.EnsureDatabaseExistsAsync()`
(creates its DB if missing) then `Database.Migrate()` (creates its tables). After a
minute the databases `identity`, `learning`, `gamification`, `ai`, `social` exist —
**empty** — next to the still-present monolith `sallevate` DB on the same Postgres.

Verify (read the DB user from .env — it's not in your shell):
```bash
docker exec "$(docker compose ps -q postgres 2>/dev/null)" \
  psql -U "$(grep -E '^POSTGRES_USER=' .env | cut -d= -f2-)" -d postgres -c '\l'
# expect: sallevate (monolith) + identity, learning, gamification, ai, social
```

### Step 2 — Stop the services that write Postgres (freeze the copy window)

Leave infra (postgres/mongo/redis/kafka/minio) running; stop the app services so
nothing writes mid-copy:

```bash
docker compose stop gateway identity learning gamification ai social analytics notification
```

(Brief downtime starts here. For a pet project this is fine; total window is the
copy time, usually seconds-to-minutes depending on data size.)

### Step 3 — Run the data split

`scripts/migrate-monolith-to-services.sh` copies each owned table from the
monolith DB into the right service DB and seeds the `UserReplicas` projections.
It is **read-only on the monolith** and refuses to overwrite non-empty targets
unless `--force`.

> **No `psql`/`pg_dump` needed on the host.** The script auto-detects the running
> `postgres` container and runs the Postgres tools *inside* it (so the client
> version always matches the server — an older host `pg_dump` would refuse to dump
> a newer server). Force the mode with `PG_MODE=docker` / `PG_MODE=host` if needed.
> `--dry-run` also prints a **column-parity report**: if any monolith column is
> missing in a service table, it's flagged there instead of failing mid-load.

```bash
# Dry run first — shows the table→service plan, row counts, and column parity; writes nothing:
./scripts/migrate-monolith-to-services.sh --dry-run

# Real run (PG host/port/creds + monolith DB name are read from .env):
./scripts/migrate-monolith-to-services.sh
```

What it does, exactly:
- **identity** ← `Users`, `UserProfiles`, `RefreshTokens`, `EmailVerificationCodes`, `DefaultAvatars`
- **learning** ← all content (`Skills`, `Lessons`, `Exercises`, `Techniques`, …) + user progress; renames `UserTechniqueProgressRecords` → `UserTechniqueProgress`
- **ai** ← `DialogBundles`, `DialogModes`
- **gamification** ← `UserXpRecords`, `UserStreaks`, `Achievements`, `UserAchievements`, `Leagues`/tiers/memberships/settings, rewards, milestones
- **social** ← `Friendships`, all `Discuss*`
- **UserReplicas** ← seeded in ai/gamification/social/learning from monolith `Users` (UserId, Email, DisplayName, AvatarKey, UpdatedAt). Email + DisplayName are NOT NULL in every service's `UserReplicas`, so both come from the monolith row.

Not copied on purpose: `Notifications` (now Redis), `OpenQuestionGlobalContexts`
(not in any service schema), and content the services re-seed on startup
(`GamificationSettings`, `LeagueSettings`, `Achievements` definitions, `DefaultAvatars`,
SuperAdmin). **Mongo is not touched** — it already shares the `sallevate` DB,
split by collection.

### Step 4 — Start everything and verify

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

Health gate — wait for every service to be ready:
```bash
for s in identity learning gamification ai social analytics notification gateway; do
  printf '%s: ' "$s"
  docker compose exec -T "$s" sh -c 'wget -qO- http://localhost:8080/readyz || echo DOWN'
  echo
done
```

Then functional smoke test (through the public gateway):
- [ ] `https://DOMAIN` loads (Vercel frontend) and talks to `api.DOMAIN`
- [ ] Log in (Google OAuth + super-admin) — exercises identity + JWT
- [ ] Open a skill/lesson — learning content present (counts match the monolith)
- [ ] Profile shows real progress points / activity consistency — gamification replica + data intact
- [ ] Friends / discuss list loads — social data intact
- [ ] Start a roleplay dialog — ai-service + Mongo `dialog_sessions`
- [ ] Notifications panel loads (Redis)
- [ ] Grafana (`grafana.DOMAIN`): Kafka consumer-lag dashboard healthy, no DLQ growth

### Step 5 — Decommission the monolith DB (later, not now)

Keep the `sallevate` monolith DB **read-only and untouched** for a few days as a
hot rollback. Once you trust the new stack, you can drop it — but **show the SQL
and take a fresh backup first** (project safety rule). Don't rush this.

---

## 5. Rollback

Because the migration only *reads* the monolith DB and *adds* new service DBs,
rollback is clean:

1. `docker compose stop` the microservice app containers.
2. Redeploy the monolith (the `monolith-legacy` branch / tag — see below) and
   point `api.DOMAIN` back at it.
3. The monolith `sallevate` DB was never modified, so it resumes exactly as before.

If a copy went wrong but you want to retry the split (not roll back), re-run the
script with `--force` — it truncates the target tables and reloads.

---

## 6. Branch layout (monolith kept as a fallback)

- `main` — microservices only (the monolith reference code under `src/backend/api`
  is removed here once the cutover is trusted).
- `monolith-legacy` — the last fully-working pure-monolith state
  (commit `171c34e`, before any microservice code), kept so the monolith can be
  redeployed for rollback or comparison.

---

## 7. Раскатка тенантов — turning a single-tenant install into tenant #1 (Phase 40.9)

> This section is a **separate, later** maintenance window from the monolith→microservices
> cutover above. Do not combine them: one is a schema-and-topology change with a clean rollback
> (the monolith DB is never modified), the other rewrites live rows in two databases.

### 7.1 What the migration does

Everything already in production belongs to nobody in particular — there is no `organization`
anywhere. The migration puts it all into one organization so that the tenancy work from 40.4–40.8
has something to be true about:

| Database | Change |
|---|---|
| `organization` | one row in `Organizations` — the default organization |
| `identity` | one `Memberships` row per existing user; one `OrganizationAuthConfigurations` row (`method = password`); one `OrganizationReplicas` row; the removed global `Admin` role (value 1) cleared |

Nothing else. `Invites` has had `OrganizationId NOT NULL` since 40.7 so there is nothing to fill
in — the script asserts this rather than assuming it. `OutboxMessages.OrganizationId` stays null
where it is null: a platform-global event legitimately has no tenant. **Every other service
database (`learning`, `ai`, `company`, `gamification`, `social`, `notification`) has no
`organization_id` column at all yet** — that is Stage C, roadmap 40.10+, and each service gets its
own window with its own backfill file.

### 7.2 Prerequisites

- Both services have already started once on the new code, so their EF migrations have run.
  `40.9_default_organization_backfill_identity_db.sql` writes into `OrganizationReplicas`, which
  the `AddOrganizationReplicaAndImpersonationAudit` migration creates. The driver refuses up front
  if a required table is missing.
- A **restored copy of production** to rehearse on. This is not optional and is the one step the
  agent cannot do for you.
- A fresh backup of `identity` and `organization`, taken the same way as §3.

### 7.3 The run

```bash
# 1. Rehearse on the restored copy. Point the script at it, never at production yet.
IDENTITY_DB=identity_restored ORGANIZATION_DB=organization_restored \
  ./scripts/tenancy-default-organization-backfill.sh --dry-run

IDENTITY_DB=identity_restored ORGANIZATION_DB=organization_restored \
  ./scripts/tenancy-default-organization-backfill.sh --apply

# 2. Rehearse the rollback on the same copy, so you have seen it work before you need it.
IDENTITY_DB=identity_restored ORGANIZATION_DB=organization_restored \
  ./scripts/tenancy-default-organization-backfill.sh --rollback --i-have-a-backup

# 3. Only now, production. Stop the app containers first — the migration writes rows the
#    running services also write.
docker compose stop identity organization gateway
./scripts/tenancy-default-organization-backfill.sh --dry-run
./scripts/tenancy-default-organization-backfill.sh --apply
docker compose start identity organization gateway
```

The SQL itself lives in [`docs/TENANCY/sql/40.9_*.sql`](TENANCY/sql/) — read those four files, they
*are* the migration. The script only chooses the connection, passes one organization id to both
databases and prints what is about to happen.

`ORGANIZATION_ID` defaults to `00000000-0000-4000-8000-000000000001`. It is a fixed, recognisable
constant rather than a fresh uuid because the same value has to land in two databases that have no
foreign key between them, and nothing will complain if the two runs disagree.

### 7.4 Verifying afterwards

```bash
# Every user has exactly one membership, all in the default organization.
docker exec -i postgres psql -U "$POSTGRES_USER" -d identity -c \
  'SELECT "OrganizationId", count(*) FROM "Memberships" GROUP BY 1;'

# Everyone can still sign in: the login flow now needs the auth-config row and the replica row.
curl -sS -X POST https://api.DOMAIN/auth/login/start -H 'content-type: application/json' \
  -d '{"email":"someone@customer.com"}'
```

Then sign in as a real user in a browser. A `403` with "This organization is suspended" means the
`OrganizationReplicas` row is missing or has `Status = 1`; a `401` means the membership did not
land.

### 7.5 Rollback

`./scripts/tenancy-default-organization-backfill.sh --rollback --i-have-a-backup`.

It prints the full destructive SQL first (project safety rule) and refuses without the flag. The
SQL itself refuses to run if anyone has joined the default organization since the backfill —
accepting an invite, being bootstrapped as the first `OrgAdmin` — because deleting a real
post-migration membership is worse than a failed rollback. In that situation the rollback is a
manual job, and the backup from §7.2 is the real safety net.

The rollback restores the demoted platform roles exactly (the forward run recorded them) and never
deletes a user. It was verified end-to-end against throwaway databases built from the services' own
EF migrations — `./scripts/tenancy-default-organization-verify.sh`, which is safe to run on any
machine with a local Postgres and creates and drops its own databases.

### 7.6 After the migration

- The platform superadmin screen at `/admin/organizations` is how every *subsequent* organization
  is created. The migration is a one-off for the installation that predates tenancy.
- The default organization's `AllowedEmailDomains` is deliberately empty. Filling it in would route
  every address at that domain to this organization, which is wrong the moment a second customer
  shares a mail provider.
- Stage C (40.10+) adds `organization_id` service by service. Each one is another window, another
  backfill file next to these, and the same rehearse-then-apply discipline.

---

## 8. Known caveats on a single box

- **No isolation:** one service OOM/looping can starve the others. Add per-service
  `mem_limit` / `cpus` in compose if one misbehaves.
- **Kafka is a JVM and the heaviest single newcomer** — it dominates the RAM jump.
- **Build on the box uses swap** — prefer building images off-box and pulling.
- **One Postgres instance** holds all 5 DBs (one process, one backup target). Fine
  for one server; horizontal scale would mean separate instances later.
