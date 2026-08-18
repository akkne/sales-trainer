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
| learning | `AssignmentDeadlineSweepService` (40.23, digest 40.26) | Warns everybody who has not finished an assignment whose deadline is inside the lead window, tells the organization's administrators who has not **started** it, then stamps the assignment as announced | **Per-organization iteration** over a **system** enumeration | `SetOrganization` at [:83](../../src/backend/learning-service/Learning/Features/Assignments/AssignmentDeadlineSweepService.cs); `EnterSystemMode` at [:121](../../src/backend/learning-service/Learning/Features/Assignments/AssignmentDeadlineSweepService.cs) | **Yes** — the enumeration only |
| learning | `AssignmentRepeatSweepService` (40.24) | Re-issues a shortened version of an assignment at the offsets its `repeat_schedule` names (+7 and +21 days by default), as a new assignment linked to its origin | **Per-organization iteration** over a **system** enumeration | `SetOrganization` at [:87](../../src/backend/learning-service/Learning/Features/Assignments/AssignmentRepeatSweepService.cs); `EnterSystemMode` at [:134](../../src/backend/learning-service/Learning/Features/Assignments/AssignmentRepeatSweepService.cs) | **Yes** — the enumeration only |
| learning | `ContentGenerationSweepService` (40.27) | Advances the admin content pipeline one step per run: the structuring call, then — only after a human approved the structure — the generation call that writes a lesson | **Per-organization iteration** over a **system** enumeration | `SetOrganization` at [:86](../../src/backend/learning-service/Learning/Features/ContentGeneration/ContentGenerationSweepService.cs); `EnterSystemMode` at [:125](../../src/backend/learning-service/Learning/Features/ContentGeneration/ContentGenerationSweepService.cs) | **Yes** — the enumeration only |
| learning | `ContentAdaptationSweepService` (40.32) | Answers a few items of a batch tone rewrite or AI content review per tick — one LLM call per exercise — and writes a **proposal** onto the item. Applies nothing | **Per-organization iteration** over a **system** enumeration | `SetOrganization` at [:86](../../src/backend/learning-service/Learning/Features/ContentAdaptation/ContentAdaptationSweepService.cs); `EnterSystemMode` at [:122](../../src/backend/learning-service/Learning/Features/ContentAdaptation/ContentAdaptationSweepService.cs) | **Yes** — the enumeration only |
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
| notification | `NotificationEventConsumer` | fifteen topics — achievements, friends, chat, follow-ups, the four `assignment.*` (three in 40.23, the digest in 40.26) and the three `dialog.review.*` (two in 40.25, `disputed` in 40.26) | `true` (inherited, [deliberately documented at :48](../../src/backend/notification-service/Notification/Eventing/NotificationEventConsumer.cs)) | Writes into `org:{orgId}:` Redis inboxes; a notification with no organization has no inbox to land in |
| identity | `OrganizationReplicaConsumer` | `organization.created` / `updated` / `suspended` | **`false`**, [:41](../../src/backend/identity-service/Identity/Eventing/OrganizationReplicaConsumer.cs) | The tenant *registry* projection: these events are **about** organizations, they are not **inside** one |
| ai, learning, gamification, notification, social | `UserReplicaConsumer` (five copies) | `user.registered` / `updated` / `deleted` / `avatar.changed` | **`false`** in all five | `UserReplicas` is deliberately platform-global — a user is a cross-organization identity ([TENANCY.md](TENANCY.md) §4.2) |
| analytics | `FunnelEventsConsumer` | `user.registered`, `exercise.completed`, `xp.granted`, and since 40.25 `assignment.issued`, `assignment.progress.changed` | **`false`**, [:58](../../src/backend/analytics-service/Analytics/Features/Funnels/Eventing/FunnelEventsConsumer.cs) | `user.registered` fires before the user has an organization at all |
| ai | `GamificationDialogWeightsConsumer` | `gamification.dialog-weights.updated` | **`false`** (made explicit in 40.14) | Mirrors `GamificationSettings`, a single platform-global row, into an in-memory singleton — see §4 |
| learning | `OrganizationProfileConsumer` (40.19) | `organization.profile.updated` | **`true`** (inherited, [declared by omission](../../src/backend/learning-service/Learning/Eventing/OrganizationProfileConsumer.cs)) | Writes `OrganizationProfileReplicas`, strict tenant data. Unlike identity's `OrganizationReplicaConsumer` two rows up, this event is **inside** a tenant, not about one: the profile belongs to the organization the way its lessons do. An envelope with no organization is dead-lettered rather than guessed at |
| ai | `OrganizationProfileConsumer` (40.19) | `organization.profile.updated` | **`true`** (inherited) | Same table, same reason, second copy — and here the stakes are higher: a guessed tenant would apply one customer's `banned_claims` to another customer's practice calls |
| learning | `AssignmentThresholdConsumer` (40.22) | `dialog.evaluated`, `exercise.completed` | **`true`** (inherited, [declared by omission](../../src/backend/learning-service/Learning/Eventing/AssignmentThresholdConsumer.cs)) | Writes `UserDialogScores` and updates `AssignmentProgressRecords`, both strict tenant data under plain-equality policies. See §4d — this is also the first consumer in the system that subscribes to a topic its **own** service produces |

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

## 4c. Phase 40.21 added no background job, and 40.24 added exactly the one it predicted

Recorded here rather than left as an absence, because 40.21 ships two columns — `repeat_schedule` and
`deadline` — that both read like an invitation to write a sweep, and the next person will look for the
entry.

- **`repeat_schedule` is stored and not interpreted.** The job that re-issues an assignment at +7 and
  +21 days is 40.24, and when it arrives it belongs in §2.1: a worker touching a tenant-scoped table,
  in explicit system mode, iterating organizations one at a time, carrying the same `BYPASSRLS`
  footnote as the five jobs already listed there. Writing it in 40.21 would have meant inventing the
  schedule vocabulary before the block that owns it. *(It arrived as
  `AssignmentRepeatSweepService`, in exactly that shape — §4f.)*
- **`deadline` likewise.** "Notify the РОП the day before the deadline with the list of who has not
  started" is 40.26, and it cannot be written before 40.23 exists, because until an audience is
  resolved into `AssignmentProgressRecords` rows there is no list to send.
- **Nothing in 40.21 needs a sweep to be correct.** Every question the block answers — which
  assignments exist, what they ask for, who is where — is answered by a query at request time. The
  status column moves only on an explicit act by a person (`activate`, `close`), so there is no state
  that decays while nothing runs.

The consequence to be honest about: an assignment whose deadline has passed stays `active` until
somebody closes it. That is the correct behaviour for 40.21 — closing on a timer means a background
job, and the job that would do it also has to notify people, which is 40.26's whole subject.

---

## 4d. Phase 40.22 added one consumer, no worker, and the shape has two things worth naming

`AssignmentThresholdConsumer` is the writer `AssignmentProgressRecords` was missing. It listens to
`dialog.evaluated` and `exercise.completed`, mirrors a graded conversation into `UserDialogScores`,
and re-judges every open assignment row belonging to that person against its completion rule.

**It subscribes to a topic its own service publishes**, which nothing in the system did before. That
looks like a loop and is not one: `exercise.completed` goes out through learning-service's outbox
under the topic's own consumer group, and the handler publishes nothing. The alternative — calling
the evaluator inline at the end of `ExerciseService.SubmitExerciseAnswerAsync` — was rejected because
half the evidence arrives from **another service**: a conversation is graded in ai-service and
learning-service only ever learns the result on `dialog.evaluated`. An inline exercise path plus an
event-driven dialogue path would mean two writers of the same two columns, with two failure modes and
two idempotency stories. One consumer over both topics keeps a single writer, and it keeps the
learner's submit request free of work they are not waiting for.

**Idempotency does not rest on the dedupe store.** The Redis `IIdempotencyStore` every consumer
inherits has a TTL, so a topic replayed after it expires would be handled twice. That is harmless
here by construction rather than by luck: nothing in the handler increments anything.
`dialog.evaluated` writes at most one row, guarded by a unique index on
`(OrganizationId, UserId, SessionId)`; `exercise.completed` writes nothing at all and is used purely
as a "this person did something" trigger. `AttemptCount` and `BestScore` are then recomputed from the
attempt rows that already exist. A counter bumped per event would have inflated silently — and
"tried 4 times and did not reach the bar" is exactly the number a РОП acts on.

**No worker was added, and 40.26 still owes one.** Nothing decays while nothing runs: a threshold is
re-evaluated when the evidence arrives, and an assignment nobody touches keeps the status it had. The
one case that needs a clock is the deadline — "at the deadline, everybody still `in_progress` is a
person who did not finish" — and that belongs with the notification 40.26 has to send anyway. Until
then a passed deadline changes no status, the same honest gap §4c recorded for closing.

---

## 4e. Phase 40.23 added one worker, no consumer, and two service-to-service calls

`AssignmentDeadlineSweepService` is the job §4c said 40.26 would owe and 40.23 turned out to need
first: the "your deadline is close" notice has to reach the person who owes the work, and that is a
clock, not an event. It is the sixth entry in §2.1 and carries the same shape and the same
footnote as the five above it — per-organization iteration over a system enumeration, `BYPASSRLS`
required for the enumeration only.

**The enumeration is the one query in learning-service that does not lead with `OrganizationId`.**
It asks "which organizations have an unannounced deadline coming" across all of them, reads a single
column, and never touches row content; everything after it runs in a fresh scope with a concrete
organization set. The list comes from `Assignments` rather than from a replicated tenant registry,
for the reason 40.12 recorded for company-service: a registry-driven loop visits every customer who
has never written an assignment and silently skips one whose registry row has not replicated yet,
turning replication lag into a dropped notice.

**Sent-ness is a column, and that is what makes the sweep idempotent.**
`Assignments."DeadlineNoticeSentAt"` is stamped in the same transaction as the notices it describes,
so a crash mid-tick re-announces rather than losing, and a successful tick never re-announces.
Moving the deadline clears the stamp — the notice names a date, and an extended deadline that
announced itself to nobody would be worse than no notice at all. The assignment is stamped **even
when nobody needed warning**, otherwise an assignment everybody had finished would be re-examined
every half hour until its deadline passed.

**The roster is consulted before anybody is warned, and a failure to read it skips the
organization.** A progress row outlives the person's employment on purpose — it is the record that
they were asked — so "still has work outstanding" and "should hear about it" are different sets, and
skipping the check would mail an ex-employee their former employer's homework deadline. When
identity-service cannot be reached the tick marks nothing and warns nobody for that organization;
the next tick picks it up, because sent-ness was never stamped.

**Two synchronous service-to-service calls entered the system in this block**, which is worth naming
because this registry is otherwise a registry of Kafka and clocks:

- **learning → identity**, `GET /internal/memberships/active`, from the fan-out and from this sweep.
  Chosen over a membership replica specifically because a lagging replica issues an assignment to a
  subset of the team and reports success ([DECISIONS.md](../DECISIONS.md), 2026-08-18).
- **ai → learning**, `GET /internal/assignments/practice-context`, when a dialog session starts, so
  the assignment's persona reaches the prompt without passing through the browser of the person
  being graded. It **degrades to "no assignment"** on any failure rather than blocking practice.

Neither is a background job, and neither belongs in §2.1 — but both are places where an organization
travels between services as a header rather than inside a Kafka envelope, and that is the fact this
registry exists to keep visible.

**No consumer was added.** The three notification topics are produced here and consumed by
notification-service's existing `NotificationEventConsumer` (§2.2), which keeps its inherited
`RequiresOrganization = true`: a notification with no organization has no inbox to land in.

---

## 4f. Phase 40.24 added one worker, no consumer, and the one job whose silence nobody would notice

`AssignmentRepeatSweepService` is the seventh entry in §2.1 and the job §4c predicted three blocks
ago, in the shape §4c predicted: per-organization iteration over a system enumeration, `BYPASSRLS`
required for the enumeration only. What it does is turn one training into recurring practice — a
shortened re-issue at +7 and +21 days — which is the mechanism `docs/TENANCY/ASSIGNMENTS.md` §2.1
calls the difference between the product's central claim and a slogan.

**This is the job whose `BYPASSRLS` footnote matters most, and the reason is about visibility rather
than about severity.** Every job in §2.1 goes quiet without erroring the day learning-service moves
onto the `NOBYPASSRLS` role `sellevate_app`, because a system-mode enumeration emits no
`SET LOCAL app.organization_id` and comes back empty. For the deadline sweep somebody eventually
notices: the notices stop and people miss deadlines they were warned about last month. For this one
there is nothing to notice — an assignment that was never created leaves no row, no notification and
no gap in anything a person looks at. The product would simply stop having the feature. It is in
`docs/DONT_FORGET.md` alongside the other six.

**The idempotency story is a unique index and nothing else, and that is not a stylistic choice.** A
wave has been issued exactly when an `Assignments` row with `(RepeatOfAssignmentId, RepeatWaveIndex)`
exists; the sweep derives what is due from the schedule, the origin's `ActivatedAt` and the rows that
exist. The alternative every other sweep in this registry uses — a sent-ness column on the row being
swept, as `DeadlineNoticeSentAt` is for §4e — **cannot be built here**: the origin may be `closed`,
and the 40.21 freeze trigger refuses *any* update to a closed assignment. A stamp-based design would
have thrown on every closed origin, and the natural repair for that (skip closed origins) would mean
that tidying up a finished five-day assignment silently cancels its refreshers.

**The enumeration is coarser than §4e's, deliberately.** It asks "which organizations have an
assignment that repeats and was issued recently enough for a wave to still be pending", not "which
organizations have a wave due right now": the offsets live inside a jsonb document, and computing
them in SQL would put the schedule vocabulary in two places, one of which nobody would remember to
update. The date bound is the catch-up window plus the longest offset the vocabulary allows, so an
organization drops out of the enumeration once none of its assignments can have a wave left. The
per-organization step then computes the exact answer, usually to nothing.

**A late wave is dropped, not delivered.** Past `Assignments__RepeatCatchUpDays` (default 3) a wave is
skipped permanently and logged, because the value of spaced repetition is the spacing — a "+7 day"
refresher arriving at +16 is not the feature arriving late. It is also what stops the first tick after
a deploy from firing every historical wave at once. The skip is recomputed from the clock rather than
recorded, so there is no row to find afterwards; the log line is "too long ago to issue now".

**Two synchronous calls, both inherited.** The sweep asks identity-service for the live roster
(`GET /internal/memberships/active`, §4e) before issuing anything, because a progress row outlives
employment and an ex-employee must not be mailed their former employer's homework. A failure to read
it skips the organization for the tick and nothing is recorded, so the next tick retries — which is
the background-worker version of the trade 40.23 had to defend on an admin route. The repeat's own
practice conversation reaches ai-service through the same `GET /internal/assignments/practice-context`
seam, unchanged.

**No consumer was added and no new notification topic.** A repeat stages `assignment.issued` per
recipient, exactly as a human-pressed issue does; notification-service's dedupe key is the assignment
id and a repeat *is* a new assignment id, so nothing there needed to change.

---

## 4g. Phase 40.26 added no worker at all, and grew the one §4e built

The РОП's day-before digest looks like a new job — it has a clock, a lead window and a per-tenant
list of recipients. It is not one, and the reason is worth stating because the next person will look
for the entry: **it is the same notice about the same date.** `AssignmentDeadlineSweepService` already
walks the organizations with an unannounced deadline coming, already opens a fresh scope per
organization, and already reads the roster there. Adding a second sweep would have meant two clocks
that can disagree about when "a day before" is, two roster reads per organization per tick, and two
sent-ness stories for one fact.

So §2.1's sixth row keeps its mode, its declaration line and its `BYPASSRLS` footnote unchanged, and
`AssignmentDeadlineNoticeService` — the per-organization half — publishes both families in one
transaction. Four properties came out of that and are the ones to preserve:

- **One timestamp still answers "has this deadline been announced".** `DeadlineNoticeSentAt` covers
  the manager notices and the РОП digest together, because they go out in the same pass and describe
  the same date. Extending the deadline still clears it and re-arms both.
- **A tick that cannot address the administrators does nothing at all** — it does not send the
  manager notices alone. That case is a rolling deploy in which identity-service is still older than
  40.26 and answers without `administratorUserIds`; stamping then would mark the deadline announced
  and lose the digest with nothing left behind to notice. Skipping costs one tick, and the next one
  picks the organization up because nothing was stamped. Both halves of the tick therefore fail
  together, which is also true of the older failure mode: an unreadable roster already skipped the
  organization in 40.23.
- **Zero non-starters means no digest and a stamp anyway.** «Все молодцы» is the message that teaches
  a РОП to ignore the channel, and re-examining a finished assignment every half hour until its
  deadline is the cost §4e already refused to pay.
- **No new `IgnoreQueryFilters()` call site.** The count in §5 check 3 stays at **five**. The digest
  is computed inside the per-organization scope from rows the query filter and the RLS policy already
  constrain; the enumeration is the one §4e wrote and is untouched.

**One synchronous call left the background world and entered a user-facing write in this block**,
which this registry should name even though it is not a job. `DialogReviewService` now asks
identity-service for the administrators when a manager files a score dispute, so the notice 40.25
could not address goes out. It is **fail-open** — the dispute is written and queued whatever
identity-service says — which is the opposite of the fan-out's contract and the same as the
dashboard's, for the reason 40.25 gave: a read that decides *who is asked to do work* must fail
loudly, and a read that decides *who is told about a row that already exists* must not take the row
away with it.

`POST /admin/assignments/{id}/remind` also reads the roster now, and that one is **fail-closed**, like
issuing: the alternative is mailing an ex-employee their former employer's homework, which 40.23
refused in the sweep and this was the last path that still could.

**No new consumer and no new `IHostedService`.** The two new topics are produced by learning-service's
outbox and consumed by notification-service's existing `NotificationEventConsumer` (§2.2), whose
inherited `RequiresOrganization = true` stays correct: a digest addressed to a РОП still lands in one
organization's `org:{orgId}:` inbox.

---

## 4h. Phase 40.27 added the eighth worker, and it is the first one a person is waiting on

`ContentGenerationSweepService` advances the admin content pipeline
([CONTENT_PIPELINE.md](../CONTENT_PIPELINE.md)): the structuring call, then — only after a human has
approved what structuring produced — the generation call that writes a lesson. It is §2.1's eighth
row, in the shape the seven above it established: per-organization iteration over a system
enumeration, `BYPASSRLS` required for the enumeration only.

Four things about it differ from every job already in this registry, and all four are consequences of
the same fact — **somebody is watching this one.**

- **The tick is seconds, not minutes.** Twenty by default, against thirty minutes for the deadline
  sweep and sixty for the repeat sweep. Those two are about dates days away; this one is about a
  spinner on an administrator's screen.
- **Its `BYPASSRLS` footnote fails loudly rather than quietly.** §4f said the repeat sweep is the job
  whose silence nobody would notice. This is the opposite end: under `sellevate_app` the enumeration
  returns nothing, every run sits at «структурируем…» forever, and the first administrator to try it
  files a bug within the hour. It is still in `docs/DONT_FORGET.md` with the other seven, because
  "somebody will notice" is not a design.
- **The enumeration reads one column of a table whose rows are customer documents.** Every system-mode
  enumeration in this registry selects only `OrganizationId`; here the rule stops being hygiene, since
  the row content is an uploaded product deck and a compliance list read out of it.
- **The claim is a conditional `UPDATE`, not a read-then-write, and it commits before the LLM call.**
  Two instances that both read a free lease would both stamp it and both pay for the same generation.
  The predicate travels inside the `UPDATE`, so exactly one tick wins. Committing first is what lets a
  five-minute call happen without an idle-in-transaction connection behind it — and a rollback would
  not un-bill the provider anyway. The lease (10 minutes) is deliberately longer than the HTTP timeout
  (300 seconds), because a lease expiring mid-call would hand the run to a second worker and buy the
  lesson twice.

**Phase 40.28 changed what the worker does at the end of the structuring half, and changed nothing
about the registry.** No new job, no new consumer, no new system-mode enumeration. The sweep still
claims only `structuring` and `generating` runs, and the state 40.28 added — `insufficient` — is
deliberately **not** worker-owned: a refused run waits for a person and costs nothing while it waits.
The one operational consequence worth knowing is that the structuring step can now end in a state
that is neither the checkpoint nor a failure, and it is logged at `Information`, because refusing
thin material is the feature working ([LLM_FAILURE_HANDLING.md](../LLM_FAILURE_HANDLING.md)). The
other is that a resumed run sends only the part of the material that has not been read before —
`StructuredMaterialLength` — so arguing with a refusal does not re-bill the customer for the deck.

**Idempotency does not rest on the dedupe store or on a counter.** A run has produced a lesson exactly
when `ProducedLessonId` is non-null, and `CK_ContentGenerationJobs_Produced` refuses to let that column
exist outside the `completed` state. The worker re-reads it after the call and before the write, which
is where a worker whose lease expired mid-call finds out it lost. Same rule as 40.22 and 40.24: derive
from state, never increment — and here the counter would have been denominated in money.

**One synchronous call entered the system**, the second learning → ai seam after `POST /ai/evaluate`:
`POST /ai/content/structure` and `POST /ai/content/generate`, both internal, both behind
`InternalServiceAuthFilter`, neither exposed through the gateway. They are **fail-closed** in the sense
that matters here — a failure spends an attempt and, after three, hands the run back to a person with
the reason on the row. Nothing degrades to a partial lesson: a generation where every exercise failed
validation is recorded as a failure, because a `completed` run pointing at an empty lesson looks
finished and teaches nothing.

**No consumer was added and no Kafka topic.** The pipeline's state lives in one row that the screen
polls; there is nothing to tell another service about, and nothing that decays while nothing runs
except a lease, which is what the lease is for.

## 4i. Phase 40.32 added the ninth worker, and it is the first one that spends money per row

`ContentAdaptationSweepService` answers a batch's items — «перепиши все упражнения этапа "закрытие"»,
or the same sweep asking what is methodically wrong with them instead
([CONTENT_PIPELINE.md](../CONTENT_PIPELINE.md) §6a). It is §2.1's ninth row, in the shape the eight
above it established: per-organization iteration over a system enumeration, `BYPASSRLS` required for
the enumeration only, and the seventh `IgnoreQueryFilters()` call site in production code.

Everything §4h says about the eighth worker applies here — the seconds-long tick because somebody is
watching, the conditional-`UPDATE` claim committed before the call, the ten-minute lease deliberately
longer than the HTTP timeout. **Three things are genuinely new, and all three follow from one fact:
a run makes two calls, a batch makes up to sixty.**

- **The lease is on the batch and the idempotency is on the item.** 40.27 could put both on the run,
  because "has this run produced a lesson" is one column. Here the equivalent question is asked sixty
  times, so the item is the row that answers it: an item carrying a proposal is never queued again,
  whatever happens to the process holding the batch's lease. That is what makes an interrupted batch
  cost exactly one call rather than forty.
- **Each item is committed on its own, and the attempt budget is per item.** Sixty calls cannot be
  held inside one transaction, and batching the writes would discard answers already billed for when
  a tick dies at item four. The failure budget is per item for the mirror-image reason: one exercise
  the model chokes on must not exhaust a budget that is protecting fifty-nine good proposals.
  `POST …/retry` re-queues exactly the failed items.
- **The worker cannot reach an `Exercise`, and that is the block.** It writes
  `ContentAdaptationItems` and its batch's own status columns; the only code in the system that
  writes an exercise body from a proposal is `ContentAdaptationJobService.AcceptItemAsync`, which
  runs inside an organization administrator's request. The roadmap's «никогда не автоприменение» is
  therefore a fact about which types this worker may write, not a rule somebody has to remember. A
  future "apply the whole batch" verb would move that line, and moving it is the one change to this
  feature that should never be made quietly.

**The batch's `Status` is a projection, not a counter.** `ContentAdaptationStatusCalculator`
recomputes it from the items inside every writing transaction, so a tick that dies after answering
three items leaves nothing wrong — there is no counter to be behind. Section 5 of
[40.32_content_adaptation_verify.sql](sql/40.32_content_adaptation_verify.sql) checks the projection
against the items and expects zero disagreements.

**Two synchronous calls entered the system**, both internal, both behind `InternalServiceAuthFilter`,
neither exposed through the gateway: `POST /ai/content/rewrite` and `POST /ai/content/review`. They
share the `/ai/content` prefix, the client and the timeout with 40.27's two, and they are fail-closed
in the same sense — a failure spends the item's attempt and, after two, leaves that item failed with
the reason on the row while the rest of the batch continues.

**No consumer was added and no Kafka topic.** A batch's state lives in rows the screen polls; there
is nothing to tell another service about. Nothing decays while nothing runs except a lease, which is
what the lease is for.

**The counts after 40.32: 30 `AddHostedService` registrations, nine workers in §2.1, seven
`IgnoreQueryFilters()` call sites.**

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
   else.** As of 40.32 there are exactly **seven** call sites outside tests — the enumeration steps
   of `FollowUpReminderBackgroundService`, `StreakResetJob`, `WeeklyLeagueClosureJob`,
   `AssignmentDeadlineSweepService` (40.23), `AssignmentRepeatSweepService` (40.24),
   `ContentGenerationSweepService` (40.27) and `ContentAdaptationSweepService` (40.32). All seven sit
   inside an explicit system-mode scope and `Select` the organization id column only; not one of them
   reads row content — which matters more for the last two than for the five before them, because a
   row on `ContentGenerationJobs` holds a customer's uploaded product deck and a row on
   `ContentAdaptationItems` holds their exercises rewritten in their own voice.

   ```bash
   grep -rn --include=*.cs "IgnoreQueryFilters" src/backend | grep -v /obj/ | grep -v Tests
   ```

   An eighth call site is a finding until proven otherwise.

### 40.31 added no job, no consumer and no seventh `IgnoreQueryFilters()`, and it had a real reason to

Phase 40.31 (closing the loop from metric to content) is the block where the temptation was concrete
rather than theoretical, so the refusal is worth writing down.

The obvious shape for «дашборд сам предлагает» is a nightly sweep: enumerate organizations, recompute
each team's heat map, write the gaps it finds into a table, extinguish the ones that closed, expire
the dismissals. That is a ninth worker, a seventh `IgnoreQueryFilters()`, a second writer of a table
nobody reads between ticks, and a panel that is stale by up to a day.

**None of it was built, and the block is smaller for it.** The suggestions are computed inside the
administrator's own HTTP request, from the same `ITeamSkillMapService` call the heat map is drawn
from — a concrete tenant in context, an ordinary `TenantTransactionScope`, no enumeration and no
system mode anywhere. A gap that closes stops being offered because the matrix stops showing it, not
because a job noticed. The only stored fact is a refusal (`TeamSkillGapDismissals`), and it needs no
sweep either: its expiry is a `WHERE ExpiresAt > now()` on the read, and the rule that reopens it
early is a comparison against `AccuracyPercentAtDismissal` in the same query. An expired row costs one
index entry and is overwritten the next time somebody dismisses that stage.

The counts in §5 at the time stood at: **29 `AddHostedService` registrations, eight workers in §2.1,
six `IgnoreQueryFilters()` call sites.** 40.32 moved all three by one — see §4i — and an eighth
`IgnoreQueryFilters()` is still a finding.

What 40.31 *does* touch in worker territory is one column on a table an existing worker owns:
`ContentGenerationJobs.GapSourceRef`. `ContentGenerationSweepService` (§4h) neither reads nor writes
it — the column is set once at creation and read only by HTTP paths — so the worker's enumeration,
its lease and its claim `UPDATE` are byte-for-byte what 40.27 shipped.

### 40.25 added no job and no consumer, and that is worth stating

Phase 40.25 (the РОП's dashboard) is a read block. It adds **no** `IHostedService`, **no** new
`KafkaConsumerBackgroundService`, and **no** sixth `IgnoreQueryFilters()` call site — the count above
stays at five. What it does add is topics to two consumers that already exist, and one topic to
learning-service's outbox:

- `FunnelEventsConsumer` (analytics) gained `assignment.issued` and `assignment.progress.changed`.
  Its `RequiresOrganization => false` is unchanged and stays correct for the same reason it was
  correct before: every branch increments a process-local Prometheus counter and stores nothing, so
  there is no per-tenant state that could be written under the wrong organization. Both new topics
  do carry an organization in the envelope; the consumer simply does not need it.
- `NotificationEventConsumer` gained `dialog.review.commented` and `dialog.review.resolved`. Its
  inherited `RequiresOrganization => true` is unchanged and stays correct: both produce a
  notification in one organization's `org:{orgId}:` Redis inbox, so an envelope with no tenant has
  no destination.
- `assignment.progress.changed` is published by `AssignmentThresholdEvaluator` inside the ordinary
  request/consumer transaction that writes the progress row, through the outbox. It is not a job.

The dashboard's own roster read (`IOrganizationMemberDirectory`) happens inside an HTTP request with
a concrete tenant in context. It is **not** a system-mode read and does not belong in the table
above.

There is no automated gate for any of this yet — `scripts/tenancy-boundary-lint.py` guards the HTTP
boundary (no `organizationId` in DTOs, routes or query strings) and `scripts/tenancy-pool-lint.py`
guards `AddDbContextPool`, but neither knows what a background job is. Turning check 2 or 3 into a
lint is the natural next step and is deliberately not in 40.14's scope.
