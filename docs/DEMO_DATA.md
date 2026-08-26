# Demo Data (local development)

`scripts/seed-demo-data.py` fills a local stack with believable *lived* data so that
every screen renders something instead of an empty state.

It is a local-development tool only. It talks to the `docker-compose.infra.yml`
containers by name and has no notion of a remote connection string.

---

## Why it exists

The content seeder ([SEEDER.md](SEEDER.md)) imports the **library** — skills, topics,
lessons, exercises. A freshly started stack therefore has plenty to practise against
and nobody who has practised. Every screen that summarises activity falls back to its
empty state:

| Screen | What it showed before |
|---|---|
| `/org` | «Тепловая карта появится после первых попыток» |
| `/org/assignments` | «Заданий пока нет» |
| `/org/dialogs` | no sessions to review |
| `/companies` | «Пока нет ни одной компании» |
| `/discuss` | «Пока нет обсуждений», «Пока нет тегов», «Пока нет данных» |
| `/profile`, `/tree`, `/skill/[id]` | «Нет данных» in every accuracy tile |
| notification bell | «Пока нет уведомлений» |
| dialog history sidebar | «Истории диалогов пока нет» |

That is correct behaviour and useless for screenshots, design review, or eyeballing a
layout under realistic load. This script writes the missing layer.

---

## Running it

```bash
scripts/dev-infra.sh                    # the containers must be up
python3 scripts/seed-demo-data.py       # seeds the 'local-dev' organization
```

| Flag | Meaning |
|---|---|
| `--organization <uuid>` | Seed some other organization instead of the one with slug `local-dev` |
| `--owner-email <email>` | The existing account the data hangs off. Default `admin.local@sellevate.dev` |
| `--dry-run` | Print what would be written and touch nothing |

The script prints its plan before writing and refuses to start when the content library
is empty, when the organization does not exist, or when the owner account has no
password hash.

---

## What it writes

| Store | Rows |
|---|---|
| `postgres/identity` | 12 demo teammates, their onboarding profiles, their memberships |
| `postgres/learning` | the five skill stages, ~880 exercise attempts, lesson and skill progress, ~100 dialog scores, technique mastery, 30 daily quotes, 9 reference materials |
| `postgres/learning` | 6 assignments (3 active, 1 draft, 2 closed) with a progress row per person, plus coaching notes and score disputes |
| `postgres/company` | 16 companies with contacts, call log, personas, practice calls, cached briefings and readiness scores |
| `postgres/social` | 8 tags, 18 discuss threads with replies and votes, friendships |
| `mongo/dialog_sessions` | ~100 full conversations with AI feedback |
| `redis` | one notification inbox per person |

### Sign-in

The demo accounts are created with **the owner account's own password hash**, so they
share its password. There is no new secret to store anywhere, and no bcrypt dependency
in the script.

```
alina.kovaleva@sellevate.dev      Алина Ковалёва       РОП (TenancyAdmin)
olga.terenteva@sellevate.dev      Ольга Терентьева     РОП (TenancyAdmin)
dmitry.sokolov@sellevate.dev      Дмитрий Соколов      менеджер
… nine more, all @sellevate.dev
```

---

## The two properties that matter

**It is idempotent.** Every generated row's primary key is a `uuid5` derived from a
fixed namespace and the row's logical identity, and every insert is
`ON CONFLICT DO NOTHING`. Running the script twice produces exactly the same database
as running it once — verified by re-running and comparing counts. Nothing is ever
deleted from Postgres or Mongo.

The one exception is the Redis inbox, which is `DEL`'d and rewritten: a Redis list has
no primary key to collide with, so appending would double every notification on a
second run. Only the seeded organization's own inbox keys are touched.

**No cell is left blank.** The team skill map withholds an accuracy below
`MinimumAttemptsForAccuracy` (5) attempts, so the generator guarantees each person at
least 7 attempts in every skill. Without that the screen this data mostly exists to
fill would still be covered in «нет данных» markers.

---

## Shaping the data

Two constants at the top of the script control volume:

```python
HISTORY_WINDOW_DAYS = 88          # just inside the heat map's 90-day default window
MINIMUM_ATTEMPTS_PER_SKILL = 7    # above the heat map's accuracy threshold of 5
MAXIMUM_ATTEMPTS_PER_SKILL = 16
```

`RANDOM_SEED` is fixed, so the same stack always gets the same numbers. Change it to
get a different-looking but equally valid dataset.

Each demo person carries a tuple of skills they are good at; attempts in those skills
land at 74–93 % accuracy and everything else at 38–66 %. That is what makes the heat
map show a readable pattern instead of uniform noise, and what gives the skill-gap
panel something real to detect.

---

## What it deliberately does not do

- **It does not seed content.** Skills, lessons and exercises come from
  `.claude/local-seed/seed.py` ([SEEDER.md](SEEDER.md)). This script reads them and
  fails if they are missing.
- **It does not create organizations.** The tenant must already exist.
- **It writes no global-library rows.** Reference materials are the one piece of
  content it adds, and they attach to existing global skills the same way the seeder's
  do.
- **It never runs against anything but the local containers.** There is no
  connection-string flag and adding one would be a mistake — see the safety rules in
  `.claude/CLAUDE.md`.

---

## Related

- [SEEDER.md](SEEDER.md) — the content library import this one depends on
- [LOCAL_DEV.md](LOCAL_DEV.md) — starting the stack
- [DB_SCHEMA.md](DB_SCHEMA.md) — the tables written here
- [TENANCY/TENANCY.md](TENANCY/TENANCY.md) — why every row carries an `OrganizationId`
