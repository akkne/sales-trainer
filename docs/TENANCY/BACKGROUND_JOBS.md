# Background jobs — the tenancy registry

Every `BackgroundService`, `IHostedService`, Hangfire job and Kafka consumer in `src/backend`,
with the tenant mode each one runs in and the line of code that declares it.

Produced by the **Phase 40.14** audit. The rule this document exists to enforce is one sentence
from [TENANCY.md](TENANCY.md) §1.6:

> **An unset tenant is an exception, never a licence.**

A background worker has no HTTP request, so nothing populates `ITenantContext` for it. Left alone
it therefore starts every scope with an *empty* context — and an empty context is not "no data",
it is "whatever the database role can see". For a `BYPASSRLS` role that is every customer's rows.
The whole point of the audit is that no worker may acquire that reach by accident: each one below
names its mode out loud, in code, at a cited line.

Companion docs: [TENANCY.md](TENANCY.md) (the design), [docs/TESTING/TENANCY.md](../TESTING/TENANCY.md)
(how to verify it), [docs/DONT_FORGET.md](../DONT_FORGET.md) (what the operator still owes).

---

## 1. The three legal modes

| Mode | How it is declared | What it means | Who may use it |
|------|--------------------|---------------|----------------|
| **Per-organization iteration** | `TenantContext.SetOrganization(id)` on a fresh scope per organization — via `TenantJobScope.ForOrganization` in gamification, inline elsewhere | The job enumerates organizations, then does its real work one tenant at a time, each in its own DI scope | Anything that produces user-visible output |
| **System** | `TenantContext.EnterSystemMode()` | Deliberately cross-tenant. Emits no `SET LOCAL app.organization_id`, so it sees rows only under a role that bypasses RLS | Platform plumbing with no tenant to attribute work to |
| **Tenant from the event envelope** | `KafkaConsumerBackgroundService.RequiresOrganization` (default `true`) | The organization travels inside `EventEnvelope.OrganizationId` and is applied before the handler runs; an envelope without one is rejected, retried, then dead-lettered | Kafka consumers handling tenant data |

The modes are mutually exclusive and `TenantContext` enforces that: entering system mode with an
organization already set throws, and so does the reverse ([TenantContext.cs:52](../../src/backend/building-blocks/BuildingBlocks/Tenancy/TenantContext.cs)).
`SetOrganization` also refuses to be re-pointed at a second organization within one scope — that
guard is what turns "the loop forgot to reset the tenant" from a silent cross-tenant write into an
exception, and it is why every per-organization loop below opens a **new** scope per iteration
rather than reusing one.

A fourth mode, **platform-wide** (`IsPlatformWide`), exists for validated Sellevate staff and is
deliberately unavailable to background work: it is entitlement by a human principal, and a job has
no principal. `EnterPlatformMode` and `EnterSystemMode` throw at each other.

---

## 2. The registry

`BYPASSRLS` column: does this worker return zero rows the day the service is moved off the owning
superuser onto the `NOBYPASSRLS` role `sellevate_app`
([create_sellevate_app_role.sql](sql/create_sellevate_app_role.sql))? "Yes" means it goes quiet
**without erroring**, which is why every "yes" also has an entry in `docs/DONT_FORGET.md`.

### 2.1 Workers that touch a tenant-scoped database

| Service | Class | What it does | Mode | Declared at | Needs `BYPASSRLS` |
|---------|-------|--------------|------|-------------|-------------------|
| company | `FollowUpReminderBackgroundService` | Polls prospects whose follow-up date has arrived, publishes `company.followup.due` | **Per-organization iteration**, over an enumeration step that is **system** | `SetOrganization` at [:69](../../src/backend/company-service/Company/Features/Companies/FollowUpReminders/FollowUpReminderBackgroundService.cs); `EnterSystemMode` at [:132](../../src/backend/company-service/Company/Features/Companies/FollowUpReminders/FollowUpReminderBackgroundService.cs) | **Yes** — the enumeration only |
| gamification | `StreakResetJob` (Hangfire, daily cron) | Zeroes streaks nobody kept alive | **Per-organization iteration** over a **system** enumeration | `TenantJobScope.ForOrganization` at [:44](../../src/backend/gamification-service/Gamification/Features/Gamification/StreakResetJob.cs); enumeration at [:27](../../src/backend/gamification-service/Gamification/Features/Gamification/StreakResetJob.cs) | **Yes** — the enumeration only |
| gamification | `WeeklyLeagueClosureJob` (Hangfire, weekly cron) | Closes the week's leagues and opens the next period | **Per-organization iteration** over a **system** enumeration | `TenantJobScope.ForOrganization` at [:52](../../src/backend/gamification-service/Gamification/Features/League/WeeklyLeagueClosureJob.cs); enumeration at [:37](../../src/backend/gamification-service/Gamification/Features/League/WeeklyLeagueClosureJob.cs) | **Yes** — the enumeration only |
| notification | `DelayedChatEmailDispatcherService` | Sends "you have an unread message" emails after a grace period | **Per-organization iteration**, driven by the claimed batch rather than by an enumeration | `SetOrganization(pending.OrganizationId)` at [:90](../../src/backend/notification-service/Notification/Features/Notifications/Emails/Delayed/DelayedChatEmailDispatcherService.cs) | No — the queue is Redis, the organization rides inside each item |
| identity | `ExpiredRefreshTokenCleanupService` | Deletes expired/revoked refresh tokens | **System** | `EnterSystemMode` at [:56](../../src/backend/identity-service/Identity/Features/Auth/ExpiredRefreshTokenCleanupService.cs) | No — `RefreshTokens` is platform-global, not tenant-scoped |
| identity | `ExpiredEmailVerificationCleanupService` | Deletes expired verification tokens | **System** | `EnterSystemMode` at [:50](../../src/backend/identity-service/Identity/Features/Auth/ExpiredEmailVerificationCleanupService.cs) | No — same, platform-global table |
| identity, learning, gamification | `OutboxRelayBackgroundService` | Reads pending outbox rows of every tenant and forwards them to Kafka | **System** — the one legitimate cross-tenant reader in the system | `EnterSystemMode` at [:56](../../src/backend/building-blocks/BuildingBlocks/Outbox/OutboxRelayBackgroundService.cs) | No — `OutboxMessages` carries no RLS policy (it is plumbing, §3 below) |
| learning | `LessonVersionBackfill` (startup, once) | Mints a published "version 1" for lessons that have never been published, so 40.16's progress backfill has something to bind to | **System** | `EnterSystemMode` in the startup scope of [Program.cs](../../src/backend/learning-service/Learning/Program.cs), the same shape gamification's seeders use | No — it sees the **global** library only, and the content RLS policy admits `OrganizationId IS NULL` rows with no session variable set. An organization's own lessons (40.18) are deliberately out of its reach: their first version comes from their own admin or their own learners, inside that organization's context |

### 2.2 Kafka consumers — tenant from the envelope

Every one of these extends `KafkaConsumerBackgroundService`, which applies
`EventEnvelope.OrganizationId` to the scope's `TenantContext` **before** the handler runs and
throws when the envelope has none ([KafkaConsumerBackgroundService.cs:167](../../src/backend/building-blocks/BuildingBlocks/Messaging/KafkaConsumerBackgroundService.cs)).
The throw is not a crash: it travels the ordinary handler-failure path, so the message is retried
and then dead-lettered. A message is never handled without a decided tenant.

| Service | Consumer | Topics | `RequiresOrganization` | Why |
|---------|----------|--------|------------------------|-----|
| gamification | `LearningEventsConsumer` | `exercise.completed`, `lesson.completed`, `skill.completed` | `true` (inherited) | Grants XP into tenant-scoped tables |
| gamification | `DialogEvaluatedConsumer` | `dialog.evaluated` | `true` (inherited) | Same |
| notification | `NotificationEventConsumer` | eight topics — achievements, friends, chat, follow-ups | `true` (inherited, [deliberately documented at :48](../../src/backend/notification-service/Notification/Eventing/NotificationEventConsumer.cs)) | Writes into `org:{orgId}:` Redis inboxes; a notification with no organization has no inbox to land in |
| identity | `OrganizationReplicaConsumer` | `organization.created` / `updated` / `suspended` | **`false`**, [:41](../../src/backend/identity-service/Identity/Eventing/OrganizationReplicaConsumer.cs) | The tenant *registry* projection: these events are **about** organizations, they are not **inside** one |
| ai, learning, gamification, notification, social | `UserReplicaConsumer` (five copies) | `user.registered` / `updated` / `deleted` / `avatar.changed` | **`false`** in all five | `UserReplicas` is deliberately platform-global — a user is a cross-organization identity ([TENANCY.md](TENANCY.md) §4.2) |
| analytics | `FunnelEventsConsumer` | `user.registered`, `exercise.completed`, `xp.granted` | **`false`**, [:58](../../src/backend/analytics-service/Analytics/Features/Funnels/Eventing/FunnelEventsConsumer.cs) | `user.registered` fires before the user has an organization at all |
| ai | `GamificationDialogWeightsConsumer` | `gamification.dialog-weights.updated` | **`false`** (made explicit in 40.14) | Mirrors `GamificationSettings`, a single platform-global row, into an in-memory singleton — see §4 |
| learning | `OrganizationProfileConsumer` (40.19) | `organization.profile.updated` | **`true`** (inherited, [declared by omission](../../src/backend/learning-service/Learning/Eventing/OrganizationProfileConsumer.cs)) | Writes `OrganizationProfileReplicas`, strict tenant data. Unlike identity's `OrganizationReplicaConsumer` two rows up, this event is **inside** a tenant, not about one: the profile belongs to the organization the way its lessons do. An envelope with no organization is dead-lettered rather than guessed at |
| ai | `OrganizationProfileConsumer` (40.19) | `organization.profile.updated` | **`true`** (inherited) | Same table, same reason, second copy — and here the stakes are higher: a guessed tenant would apply one customer's `banned_claims` to another customer's practice calls |

### 2.3 Workers that touch no tenant data at all

Listed so the registry is provably complete rather than merely long. None of these opens a DI
scope, resolves a `DbContext`, or reads a tenant-scoped Redis key, so none of them has a tenant
mode to declare — and that "no mode" is itself the audited answer, not an omission.

| Service | Class | What it does | Why it has no tenant |
|---------|-------|--------------|----------------------|
| all | `KafkaTopicProvisioner` | Creates topics and their `.dlt` companions at startup | Talks to the Kafka admin API only; topics are platform infrastructure |
| ai | `UpstreamConnectionWarmupService` | Re-opens idle sockets to Deepgram/ElevenLabs/OpenAI every four minutes | HTTP connection pooling; reads and writes nothing |
| analytics | `PresenceGaugeUpdaterService` | Refreshes the `app_users_online` Prometheus gauge | Deliberately the **sum across organizations** — an operational metric, never served to a customer. The cross-org read is named in the method itself (`CountOnlineAcrossAllOrganizationsAsync`), so it is explicit rather than ambient. Per-organization counts require `CountOnlineAsync(organizationId)`, which needs a tenant to call. An organization id as a Prometheus label would put customer identities and unbounded cardinality into the metrics store |

---

## 3. Why `OutboxRelayBackgroundService` is the only legitimate system reader

The relay reads every tenant's pending rows and forwards them to Kafka. It has to: it is the
component that exists precisely because the producer's transaction has already committed and gone.

That is exactly why the tenant lives **inside** `OutboxMessage.Payload` — in the serialized
`EventEnvelope` — and is not re-derived at publish time. The relay never has to know whose row it
is holding. `OutboxMessage.OrganizationId` exists as a mirrored column, but it is informational:
the relay forwards `Payload` verbatim and does not filter or branch on it. Consumers take the
tenant from the envelope and assert it (§2.2), which is where the boundary is actually enforced.

Until the 40.14 audit this was the one component whose licence was never written down. It opened a
scope, left `TenantContext` blank, and got cross-tenant reach as a side effect of emptiness —
indistinguishable, at a glance, from a job that had simply forgotten to set a tenant. The
`EnterSystemMode()` call added in 40.14 changes no behaviour whatsoever; it changes the code from
*inferring* the licence to *stating* it, and it makes a scope accidentally handed to the relay with
a tenant already on it fail loudly instead of quietly narrowing the relay's reach to one customer.

`OutboxMessages` carries no RLS policy, by the same reasoning: it is plumbing, in the third
category of [TENANCY.md](TENANCY.md) §1.2 alongside `default_avatars` and `exercise_type_prompts`.

---

## 4. The one finding the audit could not fix in code

`GamificationSettings` — which holds the dialog scoring weights and XP multiplier — is a **single
platform-global row**. It has no `OrganizationId`. So the "gamification settings" screen in one
customer's admin panel silently retunes dialog scoring for every other customer.

40.14 found this while classifying `GamificationDialogWeightsConsumer`, whose inherited
`RequiresOrganization = true` was wrong in both directions at once:

- a settings change saved by **Sellevate staff** carries no `org_id` claim, hence no organization
  on the envelope, so the consumer rejected it, retried it and dead-lettered it — the weights
  silently never propagated;
- a settings change saved by **one customer's administrator** was accepted and then applied to
  every customer anyway, because the destination `IDialogScoringWeightsProvider` is a process-wide
  singleton in ai-service.

The audit made the mode explicit (`RequiresOrganization => false`), which fixes the first symptom
and makes the second honest. Making the *settings themselves* per-organization is a product
decision with a schema migration behind it, not a tenancy repair an autonomous run should invent —
it is recorded for the owner in [docs/DONT_FORGET.md](../DONT_FORGET.md) and
[docs/DECISIONS.md](../DECISIONS.md) (2026-08-16).

---

## 4a. Phase 40.18 added no background job, and that was a decision

Recorded here rather than left as an absence, because "the staleness queue must be a sweep" is the
obvious design and the next person will wonder why there is no entry for it.

40.18's roadmap text says overrides "are marked stale and fall into a review queue". A worker that
walks every override and sets a flag would have belonged in §2.1 above, in explicit system mode, with
the same `BYPASSRLS` footnote the five existing per-organization jobs carry. It was rejected twice
over:

- **It can only restate a comparison two columns already answer.** An override's fork marker and its
  base's current state are both readable at query time; the queue is `GET
  /admin/content/overrides?staleOnly=true` and computes the answer per request. A stored flag adds a
  second source of truth and no information.
- **While it lags, it is wrong in the dangerous direction.** Between the base publishing and the
  sweep running, a stored flag says an override is current when it is not — which is precisely the
  claim a review queue exists to prevent an organization from believing.

The alternative that *cannot* work at all is worth stating too, because it looks cheapest: marking
synchronously inside the publish transaction. That means writing rows into organizations the
publisher is not in, and the RLS `WITH CHECK` clause — the one clause the 2026-08-16 role split
deliberately did not widen for platform staff — refuses it. Making it possible would mean a bypass on
the publish path, a far larger hole than the problem.

Full reasoning and the rejected alternatives: `docs/DECISIONS.md` (2026-08-18).

---

## 4b. Phase 40.19 added two consumers and no worker, and the shape is worth naming

The two new rows in §2.2 are the first Kafka consumers in this system that project **strict tenant
data** rather than a platform-global directory. Every previous replica projection — `UserReplicas`
five times over, `OrganizationReplicas` once — opted out of `RequiresOrganization`, because what it
copied was cross-organization by nature. `OrganizationProfileReplicas` is the opposite: the row is
one tenant's, its RLS policy is plain equality, and the write therefore has to happen with that
tenant in context. The base class already does exactly that from the envelope, so the correct
declaration here is **no declaration at all** — the default `true` is the right answer, and the two
consumers are notable precisely because they do not override it.

The thing that would have been wrong is the alternative that looks simpler: `RequiresOrganization =
false` plus reading the organization out of the payload. The envelope would then say "no tenant"
while the handler wrote into one, `TenantSaveChangesInterceptor` would see system mode, and the
write would land only because the service happens to run under a `BYPASSRLS` role today. It would
break silently on the day the role split lands — the same trap §2.1 documents for the five
per-organization jobs, arrived at from a direction where it was avoidable.

No polling worker was added. A profile is small, changes rarely and is republished in full on every
save, so a periodic reconciler would spend its life confirming that nothing changed. The one case it
would repair — an event lost while a consumer was down — is repaired by the next save, and until then
the reader falls back to the neutral base wording rather than to wrong text. What that costs is
recorded in `docs/DONT_FORGET.md`: a profile saved **before** this phase shipped has never been
published, so it must be re-saved once by hand.

---

## 5. How to keep this registry true

The registry rots the moment someone adds a hosted service. Three cheap checks:

1. **Find every worker.** The registration list is authoritative, not the class names:

   ```bash
   grep -rn --include=*.cs "AddHostedService" src/backend | grep -v /obj/ | grep -v /bin/
   grep -rn --include=*.cs "AddOrUpdate\|RecurringJob" src/backend/gamification-service | grep -v /obj/
   ```

   Every hit must appear in §2 of this document.

2. **Every worker that opens a scope must decide a mode in it.** A scope that resolves a
   `DbContext` without a preceding `SetOrganization` / `EnterSystemMode` is the bug this document
   exists to prevent:

   ```bash
   grep -rn --include=*.cs -B4 -A8 "CreateScope()" src/backend | grep -v /obj/ | grep -v Tests
   ```

3. **`IgnoreQueryFilters()` in production code must be an organization enumeration and nothing
   else.** As of 40.14 there are exactly three call sites outside tests — the enumeration steps of
   `FollowUpReminderBackgroundService`, `StreakResetJob` and `WeeklyLeagueClosureJob`. All three sit
   inside an explicit system-mode scope and `Select` the organization id column only; not one of
   them reads row content.

   ```bash
   grep -rn --include=*.cs "IgnoreQueryFilters" src/backend | grep -v /obj/ | grep -v Tests
   ```

   A fourth call site is a finding until proven otherwise.

There is no automated gate for any of this yet — `scripts/tenancy-boundary-lint.py` guards the HTTP
boundary (no `organizationId` in DTOs, routes or query strings) and `scripts/tenancy-pool-lint.py`
guards `AddDbContextPool`, but neither knows what a background job is. Turning check 2 or 3 into a
lint is the natural next step and is deliberately not in 40.14's scope.
