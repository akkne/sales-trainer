# Decisions

Non-trivial engineering decisions with their alternatives and rationale. Newest first.

---

## Phase 40.24 — automatic repeats (2026-08-18)

Eleven forks, decided during an unattended run under the rules in `docs/DONT_FORGET.md` (no
questions, no new tests, nothing executed against any database).

The frame: an internal training's effect decays in two to three weeks, and a one-shot assignment
reproduces precisely that failure. `repeat_schedule` has existed since 40.21, stored and deliberately
uninterpreted. This block interprets it. The test every fork below is answered against is: **does the
second wave produce a number you can put next to the first wave's and learn something from?** A
repeat that cannot be compared to the thing it repeats is a second assignment, not a repeat.

### A repeat is a new `Assignments` row that points at its origin

**Chosen: one new row per wave**, carrying `RepeatOfAssignmentId` (always the assignment a human
created, never another repeat) and `RepeatWaveIndex` (1-based position in the schedule). The 40.21
freeze trigger already said this in its own comment — "the answer to 'we want that practice again' is
a new assignment, which is also exactly what 40.24's repeats will create" — and `Assignment.CreatedBy`
was made nullable in that block for this row and no other.

**Rejected: a second round inside the same assignment.** It looks cheaper and it is the variant that
keeps "one assignment, one funnel" literally true on 40.25's dashboard. It has nowhere to put the
result. `AssignmentProgressRecords` carries exactly one `BestScore` and one `AttemptCount` per person,
and that single pair is deliberate — 40.22 spent a block arguing that two numbers are what the РОП
acts on. A second round therefore either overwrites the first round's score, which destroys the only
evidence anybody had that the training decayed, or adds a second pair of columns, at which point the
third wave needs a third pair. Widening the row into a rounds table is the same fan-out as a new
assignment row, with a new table, a new RLS policy and no reuse of anything.

**Rejected: new progress rows against the same assignment, discriminated by a round column.** Same
idea one layer down, and it breaks the unique index `(OrganizationId, AssignmentId, UserId)` that
40.21 built as a correctness constraint and 40.23's fan-out relies on to be re-runnable. Everything
that reads progress — the manager's strip, the threshold evaluator, the deadline sweep, the funnel —
would have to learn what "current round" means, and each of them would be a place to get it wrong.

**The cost is real and is paid rather than hidden.** A series shows up as two or three rows in the
РОП's assignment list. That is what `RepeatOfAssignmentId` and `RepeatWaveIndex` are on
`AssignmentSummaryDto` for: 40.25 can group the waves back into one series and show the comparison,
which is a presentation problem with a foreign key behind it. The squashed alternative is a data-loss
problem with nothing behind it.

### +7 and +21 are measured from the origin's issue moment, and the whole cohort moves together

**Chosen: the anchor is the origin's `ActivatedAt`**, and every offset is absolute from it. Wave 2 is
anchor + 21 days whether or not wave 1 fired on time.

**Rejected: anchoring per person, at the moment each of them cleared the threshold.** This is the
textbook spaced-repetition answer and it is the one a learning app would pick. It cannot be built on
this schema without making a repeat *per person*, because a repeat is an assignment and an assignment
has one deadline: forty people who passed on six different days need six waves, which is six
assignments, six funnels, and a dashboard that has lost the ability to say anything about the team.
It is also the wrong product. The unit the РОП acts on is a team meeting — «на планёрке в понедельник»
is the roadmap's own phrase for what the dashboard is for — and a cohort that drifts apart by a week
has no Monday.

**Rejected: anchoring on the deadline.** An assignment may have none, and then the feature silently
does not exist for it. Worse, the deadline is editable while the assignment is active, so extending it
by two days would move a wave that may already have gone out.

**Rejected: chaining each wave off the previous one.** A wave that fires late — a service restart, an
organization skipped for a tick — would push every later wave later with it, so the spacing that is
the entire content of "spaced repetition" would drift for exactly the installations that had trouble.

### The repeat goes to the origin's recipients, not to a fresh resolution of its audience rule

**Chosen: the cohort is the origin's `AssignmentProgressRecords` rows, intersected with the live
roster**, and the repeat stores that as its own audience — `{"kind":"users","userIds":[…]}`.

**Rejected: re-resolving the stored rule.** `whole_team` re-resolved three weeks later includes
everybody hired since, and hands them a *shortened refresher* of a training session they never
attended: the theory has been stripped out, so they get the practice without the material it is
practice for. It also changes the denominator between waves, which is the one comparison the series
exists to support — "26 of 40 completed" next to "31 of 46 completed" answers nothing.

**Retained from 40.23: the live roster is still consulted**, because a progress row outlives
employment on purpose (it is the record that somebody was asked) and mailing an ex-employee their
former employer's homework is the failure that check was added for.

### Everybody who was asked is asked again, including the people who failed and the people who never started

**Chosen: outcome does not filter the cohort.**

**Rejected: repeating only to those who completed** — the reading that follows from "spaced repetition
is for knowledge you acquired". It inverts 40.22's central argument. That block separated
`failed_threshold` from `not_started` precisely so the person who tried four times and stayed under
the bar would be visible, calling them "the most valuable row on the screen"; a repeat that skips them
means the product stops asking exactly the person who most needs to practise, and stops asking them
*silently*.

**Rejected: repeating only to those who failed**, i.e. treating the wave as remediation. Then the
repeat measures a self-selected group and the series comparison is meaningless — a completion rate
computed over the people who already failed once cannot be read next to one computed over everybody.
It would also make the repeat a punishment with a name on it, which is a different product.

The honest cost: somebody still working through the original at +7 receives a second, shorter
assignment. That is tolerable because the shortened wave is genuinely smaller work, and the
alternative — suppressing the wave for anybody mid-flight — makes the cohort outcome-dependent again.

### Idempotency is the existence of the row, and there is no "wave sent" flag anywhere

**Chosen: a wave has been issued exactly when a row with `(RepeatOfAssignmentId, RepeatWaveIndex)`
exists**, guarded by a partial unique index. The sweep derives what is due from the schedule, the
anchor and the rows that exist; nothing is incremented and nothing is stamped.

This is 40.22's rule applied to a different table, and the reason it matters here is specific rather
than stylistic: **the obvious alternative is impossible.** A `LastRepeatWaveIssued` column on the
origin would have to be written by the sweep — and the origin may be `closed`, which the 40.21 trigger
freezes *whole*, refusing any update at all. A stamp-based design would therefore have thrown on
every closed origin, and the natural "fix" for that (skip closed origins) is the next fork, which is
worse.

**Rejected: a Redis marker keyed by assignment and wave.** The dedupe stores in this system have
TTLs, so a marker either outlives the assignment or expires before it — and an expired marker means
the whole team gets the +7 refresher a second time. Idempotency that depends on a clock is not
idempotency.

### A closed assignment still repeats

**Chosen: `closed` origins generate their remaining waves.** The sweep skips only `draft` — an
assignment nobody was ever issued has no cohort and no anchor.

**Rejected: closing suppresses repeats**, which reads naturally from `closed` meaning "this is history
now". It makes the feature fail in the worst possible direction: a five-day assignment is *supposed*
to be closed by day 7, so tidying up would silently cancel the +7 and +21 waves. The РОП configured
repeats once, on purpose, and would find out that they never came by not noticing anything at all.

**The cost, recorded rather than papered over:** the only way to cancel a series is to edit
`repeat_schedule` while the assignment is still `active` (which 40.21 deliberately left editable — it
is not in the freeze set, and 40.24 keeps it out). Once closed, the schedule is frozen with everything
else and the remaining waves will fire. There is no "cancel repeats" button, because there is no
admin panel yet at all (40.20); it is in `docs/DONT_FORGET.md`.

**Consequence worth stating:** removing the schedule from an active assignment cancels every wave that
has not gone out, and shortening `offsetDays` from `[7,21]` to `[7]` cancels only the second. Both work
because the wave ordinal, not the day, is the identity — see the next fork.

### The wave is identified by its ordinal, not by its day

**Chosen: `RepeatWaveIndex` is the 1-based position in `offsetDays` at the moment the wave is
generated.**

**Rejected: keying on the offset value.** The schedule stays editable on an active assignment. Moving
the first wave from +7 to +5, a day after it went out, would leave no row with offset 5 — so the sweep
would compute a wave due two days ago and fire it, sending the same refresher to the same people
twice. With ordinals, editing a schedule can only ever change waves that have not happened.

### "Shortened" means less repetition and less theory — never a lower bar

**Chosen:** the repeat drops `reference_material` items (keeping them only when they are all the
assignment has), and halves `dialog_score.requiredCount`, rounded up, minimum one.
`minimumScore` and `exercise_accuracy.minimumAccuracyPercent` are copied untouched.

**Rejected: lowering the quality bar to make the repeat easy.** It would turn the refresher into the
four-minute completion 40.22 exists to make unreachable, and — more quietly fatal — it would make the
two waves' scores incomparable, destroying the only thing the series is for.

**Rejected: trimming the exercise set or the conversation.** The completion rule measures exactly
those, and an issued assignment's rule and content are frozen together for the reason 40.21 gives.
Theory is the one part that is safe to drop automatically: it was read a fortnight ago by everybody
who did the original, and re-reading it is the part of a refresher nobody does.

### The repeat is created already `active`

**Chosen: the sweep inserts the row with `Status = active`, `ActivatedAt = now`, and fans out in the
same transaction** — the same pair of writes a human pressing "issue" performs, through the same
extracted helper.

**Rejected: creating it as a draft for the РОП to approve.** "Настраивается один раз, потом
автоматически" is the roadmap's requirement in six words, and a draft waiting for a press is a to-do
item — which is precisely what the roadmap says internal trainings die of. The approval the РОП gives
is configuring the schedule; asking for it twice is asking for it at the moment they have stopped
thinking about that training.

The deadline is the origin's *duration* re-based on now (floor of one day), not its absolute date,
which would arrive already overdue. An origin with no deadline repeats with none.

### A wave more than three days late is dropped, not delivered

**Chosen: `RepeatCatchUpDays` (default 3).** A wave whose moment has passed by more than that is
skipped permanently and logged.

**Rejected: firing every overdue wave on the next tick**, the usual catch-up behaviour. Two things go
wrong at once. A service down over a long weekend would deliver a "+7 day" refresher at +16, which is
not the feature arriving late — the entire value of spaced repetition is the spacing. And on the day
40.24 first deploys, every assignment already carrying a schedule would fire *both* waves within the
same hour, because both their moments are in the past.

**Rejected: recording skipped waves.** The skip is recomputed from the clock every tick and stays
stable, so a row saying "skipped" would add a second source of truth and no information.

### `fixed_offsets` is the entire vocabulary, and no new notification type was added

**Chosen: one kind**, `{"kind":"fixed_offsets","offsetDays":[7,21]}`, with `offsetDays` optional and
defaulting to the roadmap's two values; 1–4 offsets, each 1–180 days, strictly ascending. Unknown
kinds are refused on write and unreadable on read, exactly as 40.22 did for `completion_rule` — a
schedule nobody can parse means an assignment that silently never repeats, which is indistinguishable
on every screen from an assignment nobody configured repeats for.

**Rejected: a cron expression.** It can express "every second Tuesday", which is a rhythm the thing
being scheduled does not have: what is being tracked is the decay curve of one training session, which
starts on the day it was issued and has no weekly alignment. A vocabulary that can say more than the
domain means is a vocabulary of ways to be wrong.

**Rejected: a new `assignment.repeat.issued` event family.** 40.23 justified three notification types
by how differently the recipient reads them; a repeat reads exactly like an issue ("you have new
practice"), so it reuses `assignment.issued`. The dedupe key is the assignment id, and a repeat *is* a
new assignment id, so notification-service needed no change at all.

### The unique index does not lead with `OrganizationId`, and there is no long rebuild

**Chosen: `IX_Assignments_RepeatOfAssignmentId_RepeatWaveIndex`, partial on
`RepeatOfAssignmentId IS NOT NULL`** — the second deliberate exception to the tenant-leading
convention inside this feature, after `IX_AssignmentProgressRecords_AssignmentId_Status` in 40.21, and
for the same two reasons. It is the only index covering the new self-referencing foreign key, so
without it Postgres scans the whole table on every attempt to delete an assignment; and an origin id
is globally unique already, so putting the organization in front would weaken the uniqueness rather
than scope it. Isolation is decided by the row-level-security policy, never by an index.

**No `40.24_*_indexes_concurrently.sql`, and that is a decision rather than an omission.**
`Assignments` is empty in every deployed database — nothing could create one before 40.21, and the
РОП's admin panel is still 40.20 — so both columns and the index land over zero rows and there is no
long rebuild to schedule. The index is also a *correctness* constraint, and deferring one of those to
a script somebody has to remember to run is how a unique column ends up not being unique. What ships
instead is the read-only `docs/TENANCY/sql/40.24_assignment_repeats_verify.sql`, never executed by
this run.

---

## Phase 40.23 — issuing, the manager's screen, and three notices (2026-08-18)

Seven forks, decided during an unattended run under the rules in `docs/DONT_FORGET.md` (no
questions, no new tests, nothing executed against any database).

The frame: 40.21 built the table, 40.22 built what updates a row, and neither built what **creates**
one. A progress row means "this person was asked", which is a fact about the moment a human pressed
"issue" — so this block is where an audience rule stops being a rule. Everything below is answerable
by "does this make the number on the РОП's screen describe what actually happened?"

### The roster comes from identity-service over HTTP, not from a Kafka replica in learning-db

**Chosen: a synchronous internal call — `GET /internal/memberships/active`** — made once, at the
moment somebody issues an assignment or edits a running one's audience, guarded by the shared-secret
filter ai-service already uses on its internal routes and by `[TenantScoped]`. It returns **user ids
and nothing else**.

**Rejected: a `membership.*` event family and an `OrganizationMemberReplicas` table in learning-db**,
which is what every other cross-service read in this system does and is the choice that would have
looked consistent in review. Three things killed it, and only the third is decisive:

- Identity publishes no membership events at all today, so it needed a new topic family, a new
  publisher at three write sites, a new tenant-scoped table with its own RLS policy, a new consumer,
  and — because Kafka carries changes rather than state — **a backfill of every existing membership**
  that a human would have to run. Until they did, every assignment in the installation would issue to
  nobody.
- A replica of memberships is tenant data, while `UserReplicas` next to it is deliberately
  platform-global (TENANCY.md §4.2). Two projections of "people" in one database with opposite
  tenancy rules is a trap for the next person, not a convenience.
- **The failure mode is silent and the failure is the product.** A replica that is lagging, or
  partially backfilled, resolves `whole_team` to nine people out of forty. Nothing errors. The
  assignment goes `active`, the funnel says nine, and nobody finds out until the РОП wonders why
  thirty-one people are missing from a screen that has no way to show absence. The synchronous call
  fails the other way: identity is unreachable, the issue is refused with a 503, and the human who
  just pressed the button is told to press it again.

The cost is real and is recorded rather than hidden: issuing an assignment now depends on
identity-service being up, and the fan-out is a network call in the path of an admin action. It is
one call on a rare route, not a hot path — and 40.24's repeat job will make the same call from a
background worker, where a failure simply retries on the next tick.

**Rejected: reading `Memberships` directly from learning-service's connection.** Databases are
per-service in this architecture and that is not negotiable; the point of the boundary is that
identity can change its membership schema without breaking learning.

### Explicit user lists are filtered through the live roster too

**Chosen: every audience kind, including `{"kind":"users","userIds":[…]}`, is intersected with the
active roster.** 40.21 stored those ids unchecked and said plainly that it could not check them.

**Rejected: honouring the list as given**, which is the literal reading of the stored rule. Between
choosing names in the admin panel and pressing "issue", somebody can have left — and a row issued to
them reads "not started" on the РОП's screen forever and mails homework to an ex-employee. The more
serious case is a hand-written request body naming a user id from **another organization**: the
progress row would be written under this organization's id and stay isolated, but the person would be
notified about work at a company they do not work for.

People named but no longer employed are **dropped with a log line rather than refusing the whole
issue**: failing an assignment because one person left last week would make offboarding break every
assignment that ever mentioned them. An audience that resolves to *nobody* is refused outright, since
a silently empty issue produces an `active` assignment whose funnel reads zero of zero — which on the
screen is indistinguishable from a team that has not started.

**Rejected: resolving `{"kind":"group"}` to the whole team.** No group exists in the platform yet.
"I sent it to the new hires" quietly becoming "I sent it to everybody" is the kind of surprise that
costs a customer's trust in every other number the product shows. It is a 400 with a sentence saying
so.

### Rows are created in one batch at issue time, and the audience edit tops up

**Chosen: one transaction per issue** that writes a `not_started` row and stages one
`assignment.issued` outbox event per recipient, bounded at 2000 recipients.

This is not a new decision so much as the one 40.21 and 40.22 both already made, now carried out:
lazy creation on first open inverts the table's meaning, and "who has not started" — the single
question `ASSIGNMENTS.md` §5 and roadmap 40.26 are built on — becomes an inference from absent rows
instead of a query over present ones.

**The ceiling is a ceiling, not paging.** Two thousand rows and two thousand outbox rows commit
comfortably; twenty thousand holds locks long enough to notice. An organization with twenty thousand
people on one five-day assignment has a product problem rather than a database one, and refusing says
so while an administrator is present to read it.

**Editing a running assignment's audience re-resolves and adds, never removes.** 40.21 deliberately
left the audience editable after issue ("adding three people to a running assignment is an ordinary
act"), so the update path has to fan out or the permission is a lie. Removal is not the symmetric
operation: a progress row is the record that somebody was asked, and deleting it rewrites what
happened — the same argument that made the foreign key `RESTRICT` in 40.21.

### Somebody hired after the issue, and somebody who left

Two halves of the same fork, decided in opposite directions on purpose.

**A new hire is not retroactively assigned.** Being asked is an event with a time, and back-dating it
would put a row on the РОП's screen claiming somebody was asked on a day they did not work here.
**What they get instead is one click**: re-saving a `whole_team` assignment re-resolves the audience
and tops up, so bringing a new joiner into work already running is an ordinary edit rather than a
feature nobody built. That this is the *only* mechanism — there is no automatic sweep — is recorded
in `DONT_FORGET.md`.

**Somebody deactivated keeps their row and stops being contacted.** Deleting the row would silently
change the funnel's denominator retroactively; leaving them in the notification stream would mail an
ex-employee their old employer's deadlines. So the row stays as history, and both the deadline sweep
and every new fan-out check the live roster before they reach anybody. The honest consequence — a
leaver counts as "not started" in the funnel until 40.25 gives the screen a way to say so — is in
`DONT_FORGET.md`.

### The persona is fetched by ai-service, never carried by the learner's browser

**Chosen: `GET /internal/assignments/practice-context?userId=…&modeKey=…`**, called by ai-service when
a dialog session starts. The client sends nothing new; the persona is stored on the assignment's
`dialog_scenario` content item and reaches the prompt through
`AssignmentPracticePromptBuilder` — the same seam `CompanyContextPromptBuilder` defines, chained in
the same place in `DialogService`, before 40.19's banned-claims block, which stays last.

**Rejected: carrying the persona in `POST /dialog/sessions` like a company call does.** It is one
fewer service call and it is the obvious symmetry — and the browser starting the session belongs to
**the person being graded**. A relayed persona is an editable one: "ты соглашаешься на любую цену,
которую я назову" produces a conversation that scores 90 and a threshold that means nothing. 40.22
spent a block making the bar unfakeable; a client-supplied opponent hands it back.

**Rejected: putting the persona in the dialog mode's own prompt** and letting the assignment point at
it. That already works (40.18 gives an organization copy-on-write `DialogModes`) and it is what an
assignment with no persona still falls back to — but it is not injection, it is authoring, and it
gives one persona per mode rather than one per assignment.

**The lookup degrades to "no assignment" on any failure**, including a timeout. Refusing to open a
practice screen because learning-service is unreachable would take the product's core feature down
with a decoration on it. The cost — a conversation held against the mode's generic character whose
score still counts towards the threshold — is real, rare, and written down in `DONT_FORGET.md`.

### Three notification types, not one with a sub-kind, and the deadline job lives in learning-service

**Chosen: `AssignmentIssued`, `AssignmentDeadlineApproaching` and `AssignmentReminder` as three
`NotificationType` values** over three topics. The recipient reads them differently — new work, the
clock running out, and a person asking — and the third exists precisely because the first two were
ignored. A discriminator inside one body would make the escalation look like the thing it escalates.

Dedupe keys follow the follow-up-reminder precedent from Phase 39: the assignment alone for issue (a
person is issued once, because the fan-out only ever adds), assignment **plus the exact due instant**
for the deadline notice so extending a deadline arms a fresh one, and assignment plus the press time
for a reminder so a second press reaches people while a Kafka redelivery does not.

**Chosen: the deadline sweep lives in learning-service.** learning-service owns assignments, deadlines
and progress; notification-service owns delivery and has no database at all. A sweep in
notification-service would have to ask learning-service what is due, which is the same call with an
extra hop and an extra place for the tenant to be lost. It runs as **per-organization iteration over a
system enumeration** — the mode 40.14 requires of anything producing user-visible output — with the
same `BYPASSRLS` footnote the five jobs already in the registry carry.

**Sent-ness is a nullable column on the assignment**, cleared when the deadline moves. Rejected: a
Redis marker (a fact about an assignment that outlives or predeceases it on a TTL) and recomputing
from the notification inbox (learning-service reading notification-service's Redis, which is a
boundary violation for a bookkeeping flag).

**Chosen: the notice goes to the person who owes the work, and 40.26's digest to the РОП stays
40.26's.** Splitting them keeps this block's three families about the manager and leaves the
"notification the day before the deadline listing who has not started" — a different recipient, a
different payload, a button — where the roadmap put it.

### No index for the sweep's enumeration, and no `_indexes_concurrently.sql` or backfill

**Chosen: one nullable column and nothing else.** The sweep's enumeration is the one query in this
service that filters without leading on `OrganizationId` — it asks which organizations have an
unannounced deadline across all of them — so an index for it would have to be a partial index on
`(Deadline)`, the exact shape the convention since 40.10 exists to prevent. Not worth the exception
over a table that grows at the rate a human writes assignments; the tenant-leading index 40.21 built
already serves every per-organization query that follows the enumeration.

Stated explicitly because 40.10–40.13 each shipped a concurrent-index script and a backfill: neither
exists here for the same reason it did not in 40.21 and 40.22. `Assignments` is empty in every
deployed database — nothing could create one before 40.21, and 40.21 shipped without a screen — so the
column is added over zero rows and no existing row changes meaning. What ships instead is
`docs/TENANCY/sql/40.23_assignment_fanout_verify.sql`: read-only, never executed.

---

## Phase 40.22 — completion is a quality threshold (2026-08-18)

Six forks, decided during an unattended run under the rules in `docs/DONT_FORGET.md` (no questions,
no new tests, nothing executed against any database).

The frame: 40.21 shipped `completion_rule` as "a required object naming a kind" and deliberately
refused to say more, so that this block could define the meaning without inheriting a guess. What is
being decided here is not a data format — it is **what the number on the РОП's dashboard is allowed
to mean**. Every fork below is answerable by "can a manager reach this state in four minutes without
getting better at anything?"

### The vocabulary is exactly the roadmap's two kinds, and a third was rejected

**Chosen: `dialog_score` (`minimumScore`, `requiredCount`) and `exercise_accuracy`
(`minimumAccuracyPercent`)**, an unknown kind refused with a 400 at create and update time, a bar
outside 1–100 refused, and a bar of **zero refused explicitly** — "score at least 0" is a threshold
every click clears, which is the failure mode wearing a discriminator.

**Rejected: a composite `all_of` kind** combining several rules, which is the obvious extension and
looked necessary because an assignment may carry both exercises and a conversation. It was dropped
because `AssignmentProgressRecords` stores a single `BestScore`, and a composite rule has no natural
single score: the honest options are "the minimum of the components", which is meaningless across
different bars, and "normalized attainment", which replaces the number a РОП understands (68 points
on a call) with an index nobody does. The consequence is real and is recorded in `DONT_FORGET.md`: an
assignment with both an exercise set and a dialogue is judged on one of the two, and the creator has
to decide which is the bar. That is a smaller lie than a score whose scale changes per assignment.

**Rejected: tolerating an unknown kind** and treating it as "no threshold yet". A rule nothing can
evaluate completes nobody, and on the dashboard "nobody can finish this" is indistinguishable from
"nobody tried" — the same silent failure 40.21 refused to give a resting place in the schema,
arriving through the front door instead.

**Rejected: a `CHECK` constraint listing the kinds.** The vocabulary is a product decision that will
grow (40.24, 40.25), and a database constraint enumerating it means a migration per addition and a
frozen `completion_rule` on issued rows that no longer satisfies the newest constraint. The check
stays "an object with a kind"; the vocabulary lives in the service, and
`docs/TENANCY/sql/40.22_completion_threshold_verify.sql` asserts it by query.

### Accuracy counts submissions, and the score is withheld until the set has been attempted

This is the pair of details where "not a click" actually lives, so both were chosen against easier
alternatives.

**Rejected: accuracy as "exercises eventually answered correctly ÷ exercises in the set".** It is the
kinder reading and it is brute-forceable: retry each exercise until it is green and everybody reaches
100%. **Chosen: correct submissions ÷ all submissions**, which is the definition
`LessonAccuracyService` already reports to the admin panel — reused rather than restated — and under
which guessing lowers the number.

**Rejected: reporting accuracy from the first submission onward.** One lucky answer out of twenty is
100% accuracy, and it would complete an eighty-percent assignment outright. **Chosen: the score stays
`null` until every exercise in the pinned set has been attempted at least once**, which is also the
completion rule `UpdateLessonProgressAsync` already uses for lessons. `null` is deliberately not
zero: "we do not know yet" and "they scored nothing" are different rows on the screen.

### Attempts are matched by exercise id, not by the pinned lesson version id

**Rejected: filtering attempts on `UserExerciseAttempt.LessonVersionId == the pinned version`,**
which is the consistent-looking choice given that 40.16 exists precisely to bind attempts to
versions. It breaks in a way nobody would find quickly: the learner's submit path binds an attempt to
whatever version is published on the day they answer, so republishing the lesson while an assignment
is running would make that assignment permanently unreachable, silently, for everybody still working
on it.

**Chosen: the pinned snapshot decides *which exercises* the threshold covers** — read out of
`LessonVersion.Content` by the new `LessonSnapshotSerializer.ReadExerciseIds` — **and attempts are
matched by exercise id.** The version still does the job it was pinned for: it says what was asked,
immutably. Counting an attempt on a slightly reworded version of the same exercise is a much smaller
error than an assignment nobody can finish.

### `dialog.evaluated` had to be widened, because it carried no grade

The roadmap says threshold evaluation reuses the existing scoring. For exercises it does, exactly.
For conversations it could not: the event carried `rawScore`, which **despite the name is
`FeedbackResult.XpReward`** — the pre-multiplier XP, bounded by the sum of four configurable weights
rather than by 100 — while the 0–10 grade the learner is actually shown never left ai-service.

**Rejected: interpreting `rawScore` as a quality signal.** Its scale is a runtime setting that a
gamification-settings screen can change, so the same conversation would clear a 70 bar before the
change and fail it after. That is worse than no threshold, because it is a threshold that moves.

**Rejected: an out-of-band lookup** from learning-service into ai-service for the grade and the mode
key, on every evaluated conversation. It is a synchronous cross-service dependency on an event path,
which is the shape `MICROSERVICES.md` exists to avoid.

**Chosen: two additive fields on the event** — `qualityScore` (the grade, normalized to 0–100 by the
producer so no consumer needs to know ai-service's internal scale) and `modeKey` (because an
assignment's `dialog_scenario` item addresses a mode by key, the way ai-service's own API does, and
40.18's override keeps its parent's key while getting a new id). `rawScore` was left alone rather
than renamed: gamification reads it, and renaming a field on a live topic buys nothing. The existing
wire-shape test was extended to pin both.

### A row per graded conversation, not a counter — which is also the idempotency answer

**Rejected: incrementing `AttemptCount` on each event and keeping a running best.** It is the obvious
implementation and it cannot express the rule: "three conversations scoring at least 70" is a
question about a set, and a single counter cannot answer "how many cleared the bar". It also drifts:
the shared Redis dedupe store has a TTL, so a replayed topic or a delayed redelivery inflates the
count with no practice behind it — and "tried 4 times and did not reach the bar" is precisely the
number the РОП uses to decide who needs coaching.

**Chosen: `UserDialogScores`, one row per graded conversation, unique on
`(OrganizationId, UserId, SessionId)`**, and both progress numbers **recomputed** from attempt rows
on every evaluation. Reprocessing an event then writes the same rows and derives the same numbers,
by construction rather than by a dedupe window. The table is deliberately **not** keyed to an
assignment: one conversation may satisfy two assignments referencing the same scenario, and the row
records what happened to a person rather than what it counted towards.

### One consumer over both topics, rather than an inline call on the exercise path

**Rejected: calling the evaluator at the end of `ExerciseService.SubmitExerciseAnswerAsync`.** It is
the shorter path and it is only half a solution: a conversation is graded in **ai-service**, so its
half must be event-driven regardless. The result would be two writers of the same two columns with
two failure modes, two idempotency stories and two places to forget the tenant — and it would put
work the learner is not waiting for inside their submit request.

**Chosen: `AssignmentThresholdConsumer`, subscribing to `dialog.evaluated` and
`exercise.completed`.** learning-service consuming a topic it also publishes is new in this system
and is not a loop: the handler publishes nothing, and `exercise.completed` is used purely as a "this
person did something" trigger — every number it might have carried is already in learning-db. The
cost is that a threshold is met a moment after the work rather than in the same response, which is
recorded in `DONT_FORGET.md`. `RequiresOrganization` stays at its inherited `true`, like 40.19's two
profile consumers: both tables it writes are strict tenant data under plain-equality policies.

### The evaluator updates progress rows and never creates one

**Rejected: creating a progress row on first scored activity**, which would have made the block
demonstrable instead of dormant. It reintroduces exactly what 40.21 rejected, one level along: a row
would then mean "this person did something a referenced lesson contains", so somebody who practised
that lesson for their own reasons would appear on the РОП's screen as though they had been assigned
it, and "who has not started" would stay an inference from absent rows.

**Chosen: the evaluator updates rows that exist and does nothing when there is none.** The honest
consequence, stated in `ROADMAP.md`, `ASSIGNMENTS.md` and `DONT_FORGET.md` rather than buried: until
40.23's fan-out ships, 40.22 runs over an empty set and changes nothing a human can see. An
unobservable-but-correct block is better than a number produced by a shortcut 40.23 would have to
unwind.

The one thing 40.22 *did* add on the write path, because it is the last moment it can be added:
`POST /activate` refuses an assignment whose rule measures content it does not carry — `dialog_score`
with no `dialog_scenario` item, `exercise_accuracy` with no `lesson_version` item. Issuing freezes
both the rule and the content, so after that moment the mismatch is permanent and the assignment is
unfinishable forever.

---

## Phase 40.21 — the Assignment entity (2026-08-18)

Eight forks the roadmap left to the agent, decided during an unattended run under the rules in
`docs/DONT_FORGET.md` (no questions, no new tests, nothing executed against any database).

The frame for all eight: Stage E is the first block whose product claim is about *behaviour of a
team*, not about data isolation. The claim is that a РОП can turn one internal training into targeted
practice in minutes and then see who actually did it. Every decision below is answerable by "does this
keep that claim true a year later, when the content has moved on and the people have changed?"

### The entity is separate, and the shape of the argument matters more than the conclusion

The roadmap already says "отдельная сущность, а не переиспользованный learning path", so the
conclusion was given. What was not given is *why the obvious reuse fails*, and that reason constrains
everything else.

**Rejected: `ProgramVersion` with a deadline and an audience column.** The two objects differ on every
axis that has a column. A programme is walked over months and its central mechanic is the pin — a
learner stays on the snapshot they started, and 40.17 built a whole freeze-and-diff apparatus so that
nobody's curriculum moves under them. An assignment is finished or abandoned within days, and nobody
is pinned to it; its central mechanic is a deadline and a threshold, neither of which a programme has
any meaning for. Sharing the table means every programme row carries two null columns it will never
use, every assignment row carries a version number nobody increments, and the freeze trigger has to
learn two different notions of "frozen".

**Chosen: two new tables in learning-db**, `Assignments` and `AssignmentProgressRecords`, alongside
the programme tables rather than inside them. learning-service already owns progress and grading
(roadmap 40.21), so threshold evaluation in 40.22 reuses existing scoring without a cross-service
call, and "does this pinned lesson version still exist" stays a question one database can answer.

**Rejected: a new assignment-service.** Two tables, no independent scaling story, no separate
lifecycle, and an immediate synchronous dependency on learning-service for every read — the
distributed monolith the microservices doc warns about, and the same rejection 40.17 recorded for a
programme-service.

### `content` holds references to a frozen lesson version, not exercise bodies and not exercise ids

This is the decision that makes the roadmap's "новых рендереров нет" (40.23) literally true, so it
was made in 40.21 rather than left to the block that would discover the problem.

**Rejected: inline exercise content in the jsonb column.** It is the shortest path from "AI generated
five exercises" to "the assignment has five exercises", and it forks the platform in half. There would
be two homes for an exercise body — `Exercises.SerializedContent` and this column — and therefore two
grading paths, two override stories (40.18 resolves overrides for the first home only), and two places
for 40.19's `{{organization.*}}` substitution to be applied or forgotten. The second home would get
the second-class treatment on every one of those, and the divergence would be discovered by a customer.

**Rejected: a list of `Exercises.Id` values.** Better, but it points at the *mutable* working copy the
admin panel edits. An assignment issued in March and scored in April against an exercise edited in
between produces a `BestScore` describing a question nobody can reconstruct — which is precisely the
defect 40.16 spent a block removing from `UserExerciseAttempts`. Repeating it one level up, in the
table whose numbers a РОП uses to decide who needs coaching, would be worse than the original.

**Chosen: `{"items":[{"kind","reference","orderIndex"}]}` over three kinds** — `lesson_version`
(a `LessonVersions.Id`: the frozen, ordered exercise set), `dialog_scenario` (an ai-service dialog mode
key, deliberately a string because that is how ai-service addresses modes, not a uuid), and
`reference_material` (ungraded theory). Every kind names something that either cannot change or is
resolved through the existing read path. The assignment's exercises are ordinary exercises inside an
ordinary lesson version, so the eleven existing renderers, the existing grading and 40.18/40.19's
resolution all apply with no new code — which is what "no new renderer" has to mean if it is a claim
rather than a hope.

The generation pipeline of Stage F therefore has a defined output: it writes ordinary lesson and
exercise rows, publishes a lesson version, and the assignment references it. That is one more step
than "put the JSON in the assignment", and it is the step that keeps everything downstream working.

### `source_ref` is a namespaced string, and when it names content it names a version

**Rejected: a bare `uuid` column.** Two of the three source types do not have one. A `gap_detected`
assignment references a metric — a skill plus a funnel stage, or a named counter — which is not a row
id; a `training` assignment created before Stage F's upload entity exists has nothing to point at yet.
A uuid column would have been null in the cases that matter and would have forced a second column the
day the first non-uuid source arrived.

**Rejected: `lesson_id` when the source is a lesson.** Same argument as the content column, and the
verify script asserts against it explicitly (`SourceRef LIKE 'lesson:%'` must find zero rows). A
reference to a mutable lesson answers "what was this practice about" with whatever the lesson has since
become.

**Chosen: `varchar(200)`, read according to `source_type`, written as `<kind>:<identifier>`** —
`lesson-version:<uuid>` for library content, and whatever Stage F and 40.25 settle on for uploads and
metrics. Plus a check constraint tying the two columns together: `source_ref` is null exactly when
`source_type = 'manual'`. A manual assignment carrying a dangling reference is a row nobody can
interpret a year later, and that is a cheap constraint to add now and an expensive one to add later.

### `audience` stores the rule; the resolved people are the progress rows

The forcing constraint is not a preference: **the list of an organization's employees lives in
identity-service** (`Memberships`), and learning-db holds only `UserReplicas`, which is
platform-global and says nothing about who belongs where. learning-service therefore *cannot* resolve
"the whole team" into names on its own.

**Rejected: an `AssignmentAudienceMembers` join table filled at create time.** It would be a copy of
somebody else's data, stale the moment anybody is hired or leaves, and it would answer the wrong
question: the РОП needs to know who was *asked*, which is a fact about issue time, and that fact is
already the existence of a progress row.

**Rejected: three columns — `audience_kind`, `audience_user_ids uuid[]`, `audience_group_id`.** Two of
the three are null in every row, and the array column commits the schema to "a list of ids" as the only
extensible case. Rules like "everybody who scored under 60 last month" — which §3.4's gap-detected flow
points straight at — do not fit it.

**Chosen: `audience jsonb`** holding `{"kind":"whole_team"}`, `{"kind":"users","userIds":[…]}` or
`{"kind":"group","groupId":…}`, with a check constraint requiring an object naming its kind. The
`group` kind is in the vocabulary although nothing in the platform defines a group yet, so 40.23 needs
no migration to use it; the shape is validated, the meaning is not. And learning-service deliberately
does **not** validate the user ids against membership — it cannot, and a check against the
platform-global `UserReplicas` would look like a membership check while proving nothing, which is worse
than no check at all.

### `completion_rule` is required, has no default, and 40.21 asserts only that it names a kind

This is the block's load-bearing decision, and it is a decision about what the schema makes
*impossible* rather than about what it stores.

**Rejected: nullable, or defaulting to `{}`.** Either one gives "completion means opening everything" a
resting place. `ASSIGNMENTS.md` §1.1 is explicit about the consequence: managers click through in four
minutes, the dashboard reads 100%, and the number is a lie the РОП eventually catches — the difference
between a training tool and a compliance-theatre tool. A default that expresses the failure mode
guarantees somebody ships it, because the path of least resistance is to omit the field.

**Rejected: a typed C# record with a closed set of rule kinds, validated in 40.21.** That is 40.22's
vocabulary. Inventing it a block early means 40.22 either inherits guesses or breaks them, and the
instruction for this run was explicitly not to build something the next block must break.

**Chosen: `NOT NULL`, no default, no way for the API to omit it, and one constraint —
`jsonb_typeof = 'object' AND jsonb_exists(…, 'kind')`.** A discriminator is the one field every
discriminated rule shape will have whatever 40.22 decides, so requiring it constrains nothing and
catches a scalar or an array. The documented examples (`{"kind":"dialog_score","minimumScore":70,
"requiredCount":3}`, `{"kind":"exercise_accuracy","minimumAccuracyPercent":80}`) are documentation, not
schema. `repeat_schedule` gets the same treatment, nullable because one-shot is a legitimate answer.

### `status`: three values, one-way, and the freeze is narrower than 40.17's on purpose

**Rejected: a fourth value `scheduled`** for an assignment whose `opens_at` is in the future. Whether
an assignment is visible yet is a question about `opens_at`; a second place to store the same fact is a
second place for it to be wrong, and the two would disagree the first time somebody edits the opening
time.

**Rejected: freezing an issued assignment whole**, the way 40.17 freezes a published programme version.
It is the consistent-looking choice and it is wrong here. A programme version is frozen because
somebody is *pinned* to it; an assignment has no pin. Meanwhile adding three people to a running
assignment and extending a deadline by two days are ordinary acts of running a team — the roadmap's own
40.23 and 40.26 both assume them. A whole-row freeze would be a trigger those blocks have to break.

**Rejected: no database enforcement, just service checks.** Same answer 40.15 and 40.17 gave: "the
service currently refuses" is not the guarantee "it cannot be written".

**Chosen: `draft → active → closed`, one-way, with a trigger that freezes exactly the four fields every
recorded score was measured against** — `source_type`, `source_ref`, `content`, `completion_rule` (plus
`organization_id` and `activated_at`) — and leaves title, goal, audience, `opens_at`, `deadline` and
`repeat_schedule` writable. A closed assignment is frozen whole, because at that point it is history and
the answer to "we want that practice again" is a new assignment — which is also exactly what 40.24's
repeats will create. The service refuses the frozen fields first, with a message naming them, rather
than silently dropping them: an administrator who believes they moved a threshold and did not is worse
off than one who is told they cannot.

### `assignment_progress` ships with no writer, and the alternative was worse

40.21 creates the table and nothing fills it: fan-out is 40.23, threshold evaluation is 40.22. The
temptation was to add one small write path so the table is not dead — "mark it in progress when the
learner first opens it".

**Rejected: creating a progress row lazily on first open.** It requires resolving the audience to know
whose assignment it is, which is 40.23's headline; and it inverts the table's meaning. If a row exists
only once somebody has started, then "who has not started" — the single most actionable question in
`ASSIGNMENTS.md` §5, and the entire subject of roadmap 40.26 — becomes an inference from absent rows
rather than a query over present ones. The row's existence has to mean "this person was asked", and
that fact is written at issue time or not at all.

**Chosen: the table exists, the funnel counts read zero, and both facts are stated in the API docs and
in `DONT_FORGET.md`.** An honest zero is better than a number produced by a shortcut that 40.23 would
then have to unwind.

### One index deliberately does not lead with the organization

Every tenant-scoped index since 40.10 leads with `OrganizationId`, because the query filter and the RLS
policy put it in front of every predicate. `IX_AssignmentProgressRecords_AssignmentId_Status` does not.

It serves two things that both need `AssignmentId` first: 40.25's per-assignment funnel, and the
`ON DELETE RESTRICT` check on the foreign key. The unique index
`(OrganizationId, AssignmentId, UserId)` cannot cover the latter — `AssignmentId` is not its leading
column — so without this index Postgres scans the whole progress table on every attempt to delete an
assignment. That is the exact trap 40.12 documented when company-service's child indexes stopped
covering their foreign key, arrived at from the other direction.

`RESTRICT` rather than `CASCADE` on that key is the same argument as the status decision: a progress row
is the record that somebody was asked to do something, and deleting an assignment must not erase it.
Drafts have no progress rows, so the one deletion the service permits still works, and the constraint
is a second guarantee behind that rule.

### No `_indexes_concurrently.sql`, and no backfill — stated as a decision because the neighbours have both

40.10–40.13 each shipped a concurrent-index script and a backfill, and each documented a maintenance
window in which user data was invisible. Nothing of that shape exists here: both tables are created
empty by the migration, so every index is built over zero rows and the ACCESS EXCLUSIVE lock costs
nothing; nothing filters on the new tables, so no existing row anywhere changes meaning. The unique
index is a correctness constraint, and deferring a correctness constraint to a script somebody has to
remember to run is the worse trade — the same call 40.15, 40.17 and 40.18 made.

What ships instead is `docs/TENANCY/sql/40.21_assignments_verify.sql`: read-only, never executed,
checking the schema, asserting that neither RLS policy carries an `IS NULL` branch, and querying what
the deliberately-absent foreign keys would have checked.

---

## Phase 40.19 — the organization profile and content parameterization (2026-08-18)

Seven forks the roadmap left open, decided during an unattended run under the rules in
`docs/DONT_FORGET.md` (no questions, no new tests, nothing executed against any database).

The frame for all seven: 40.18 made forking content possible but expensive — an override leaves the
base library's improvement path and joins a review queue. This block's job is to make the *cheap* path
strong enough that few people take the expensive one. Every decision below is answerable by "does this
make substitution good enough to replace a fork?"

### Substitution is resolved on read, and this is the one non-negotiable decision in the block

**Chosen: the stored row keeps the template; only the HTTP response and the outgoing AI prompt carry
substituted text.**

**Rejected: resolve at write time** — when the seeder imports, or when an admin saves. It is the
cheaper implementation (one pass, no hot-path cost, no provider, no replica) and it is fatal. 40.15
freezes `Exercise.SerializedContent` and the lesson title into `LessonVersion.Content` and hashes the
result into `ContentHash`. Render before the write and publishing the same base lesson in two
organizations produces two different hashes and two different snapshot rows — the shared library
silently becomes one library per customer, which is exactly §1's fork with none of §2.6's guard rails
and no queue telling anybody it happened. The same argument applies one table over: a rendered
`DialogMode.ChatSystemPrompt` would give every organization a different `BaseContentHash`, and 40.18's
staleness queue would report every override as stale forever.

**Rejected: resolve at write time but hash the template separately.** This works, and it costs a second
canonical serialization of every content document plus a rule that the next person has to know: "hash
this field pre-render, that one post-render". The invariant "the row is the template" is one sentence
and cannot be got wrong by accident.

Consequence accepted: the read path pays for it. Mitigated by short-circuiting on the absence of `{{`
(the overwhelming majority of content), by memoizing the profile per request, and by keeping the whole
thing off the network (see the replica decision below).

### An unfilled field renders as neutral prose, not as a blank and not as the placeholder

The roadmap explicitly asks what happens to an unfilled field. Three answers, and the third is right
for a reason that is about the product, not the code.

**Rejected: leave `{{organization.icp}}` visible.** It is the most honest option for a developer and the
worst for the customer: a salesperson mid-lesson reads curly braces and concludes the product is broken.
Worse, it makes the failure *loud in the wrong place* — the person who sees it cannot fix it.

**Rejected: the empty string.** «Расскажите, чем  помогает » is not a lesson with a missing word, it is a
lesson that looks like a bug. And it is silent: nobody reports "there was a double space".

**Chosen: the phrase the base library was already written in** — «ваш продукт», «ваш клиент»,
«типичные возражения ваших клиентов», «ваш скрипт звонка». A trial account on day one, before the РОП has
opened the form, reads *exactly* as the library read before 40.19 existed. Substitution becomes strictly
additive: filling the form can only improve the text, never repair it.

An unknown key — `{{organization.produkt}}`, a typo — is removed from the output and logged as a warning
by the rendering service. The two failure modes were weighed explicitly: displaying it is loud to the
wrong audience, and throwing would fail a lesson a learner is in the middle of over a cosmetic defect.
A log line is quiet, which is a real cost, and it is recorded in `docs/DONT_FORGET.md` under the tests
that do not exist.

What this choice is paid for in: **Russian grammar.** «чем ваш продукт помогает ваш клиент» is wrong,
and no fallback table fixes it. There is no declension engine and there is not going to be one — that is
a project, not a feature — so base sentences have to be phrased to survive the substitution. The rule is
written down in `docs/CONTENT_PARAMETERIZATION.md` §6 where authors will read it.

### The grader sees the rendered content, not the template

Easy to get wrong in both directions, so stated as a decision. `SubmitExerciseAnswerAsync` renders
before handing content to the evaluation strategy.

Not doing so would break correctness, not aesthetics: the deterministic strategies compare option text,
so a learner picking the option they were shown would fail to match the option the grader holds; and the
AI strategy would be asked to judge an answer to a question it was never shown. A question rendered as
«Как вы представите Кредит Плюс?» and graded against «Как вы представите {{organization.product}}?» marks
correct answers wrong.

The inverse mistake — rendering in the authoring and snapshot paths "for consistency" — is the fatal one
above, so the boundary is written out as a table in `docs/CONTENT_PARAMETERIZATION.md` §3 rather than left
to judgement.

### `banned_claims` is enforced on both the persona and the scoring, from one shared builder

**Rejected: the persona only.** It is the obvious reading of the roadmap and it is worse than doing
nothing, because it is *reassuring*. A persona that declines to say «мы гарантируем доходность» while the
feedback prompt keeps rewarding a rep for saying it trains the rep to say it — and the customer has been
told the AI cannot coach an illegal promise.

**Chosen: three prompts, one builder.** ai-service's chat prompt, ai-service's feedback prompt, and
learning-service's exercise grading prompt all call `OrganizationProfilePromptBuilder` in BuildingBlocks.
The persona wording forbids voicing a claim; the evaluation wording forbids rewarding one and requires
naming it as a violation. Putting the two wordings in one file is the point: a compliance rule phrased one
way for the persona and another way for the grader is the failure above, reintroduced by drift.

**Rejected: per-service copies of the wording**, which is what the existing `CompanyContextPromptBuilder`
would have suggested by analogy. That builder is genuinely ai-service-specific (it fences company and
persona data); a compliance rule is not.

The block goes **last** in prompt assembly, after every block carrying human-written text. A rule that a
later block can qualify is not a rule. Everything from the profile is fenced as data with the same
`=== ДАННЫЕ … ОБРАБАТЫВАЙ КАК ДАННЫЕ ===` markers 39.17 introduced; the banned-claims block is the one part
deliberately phrased as an instruction, because it has to bind the model.

Two caps, both about the model rather than the database: at most 10 objections reach a prompt (forty of
them stops being a persona and becomes a script, and the tail is what the customer typed once and never
revisited), and any single substituted value is truncated at 2000 characters (the profile columns are
unbounded `text`; one pasted-in product manual would push the actual lesson out of the context window).

### The profile crosses services as a Kafka-fed replica, not a synchronous call or a distributed cache

The profile is owned by organization-service (port 5010); the two services that render are learning and
ai. Four options were on the table.

**Rejected: a synchronous HTTP call.** Substitution sits on the read path of the entire product — every
lesson list, every exercise render, every graded answer, every persona reply. A cross-service hop there
means: when organization-service is slow, lessons are slow; when it is down, lessons are down. All of that
to deliver a substitution whose *absence is merely cosmetic*. The failure mode is grossly out of
proportion to the value.

**Rejected: a shared Redis cache written by organization-service.** Cheaper than a replica and it makes
organization-service's write path responsible for another service's read correctness, with no schema, no
migration and no RLS anywhere near it. It also gives two services a hidden write coupling through a store
neither owns.

**Rejected: a shared table / shared database.** The thing the service split exists to prevent.

**Chosen: `organization.profile.updated` on Kafka → `OrganizationProfileReplicas` in learning-db and
ai-db.** Exactly the shape `UserReplicas` (40.2) and `OrganizationReplicas` (40.9) already established,
so there is no new pattern to learn and the existing consumer machinery (idempotency, dead-lettering,
tenant-from-envelope) applies unchanged. Duplicating the table across two databases is accepted, and is
the correct trade against every alternative above.

Four sub-decisions inside it:

- **The payload is the whole profile, never a delta.** The consumers are last-writer-wins replicas; a
  delta would make a dropped message permanent, a snapshot makes the next save repair it.
- **The jsonb columns travel as raw JSON text**, parsed once by `OrganizationProfileSnapshot` in
  BuildingBlocks. Three services' worth of DTOs would give each service its own chance to disagree about
  the shape, and the visible symptom would be a lesson and a persona prompt rendering the same profile
  differently — which is precisely what "one base lesson serves everybody" is supposed to rule out.
- **Published after the commit, not inside it.** A replica that learned about a profile the transaction
  rolled back would render a lesson containing text no organization ever saved. The other direction — a
  committed save whose event is lost — is the one the snapshot payload is designed for.
- **`RequiresOrganization` stays at its inherited `true`.** These are the first Kafka consumers in the
  system that project strict tenant data, and every earlier replica projection opted out. The tempting
  copy-paste — opt out, then read the organization from the payload — would have the envelope say "no
  tenant" while the handler writes into one: `TenantSaveChangesInterceptor` sees system mode, and the write
  lands only because the services currently run under a `BYPASSRLS` role. It would break silently on the
  day the `sellevate_app` role split lands. Recorded in `docs/TENANCY/BACKGROUND_JOBS.md` §4b.

**No reconciliation job.** A profile is small, changes rarely, and is republished in full on every save,
so a periodic reconciler would spend its life confirming nothing changed. The one case it repairs — an
event lost while a consumer was down — is repaired by the next save. What that leaves is one genuine gap:
a profile saved *before* this phase shipped was never published, so its replicas do not exist. That is a
one-time manual re-save, in `docs/DONT_FORGET.md`, not a permanent worker.

**No republish endpoint** either, for the same reason: one more platform-only route to authorize, for a
problem that occurs exactly once in the system's life.

### Platform-wide callers get the empty profile

In platform mode the tenancy query filter admits every organization's rows at once, so "the caller's
profile" is not a well-defined thing. Returning the first row would render Sellevate staff a lesson with
some customer's product name in it, and — worse in ai-service — run a staff practice call under some
customer's `banned_claims`.

**Chosen: `OrganizationProfileSnapshot.Empty`**, i.e. staff read the library as it is written. This is the
same rule `ContentOverrideResolution` chose in 40.18 for the same reason, and stating it identically in both
places is deliberate: the next person adding a per-organization read path should find one answer, not two.

### The seeder's target parameter is a literal, not an organization id — and the reads were the real bug

The roadmap asks for "an explicit target parameter, no pointing at a customer". The interesting part is that
the parameter is the *smaller* half of the fix.

**The bug.** Every read in `AdminSeederController` went through the tenancy query filter, which admits
"global **or mine**". A platform administrator is normally also a member of an organization, so their
requests carry `X-Organization-Id`. Their seeder reads therefore loaded that organization's **override**
lessons alongside the base ones — and `UpsertLesson` matches on `(TopicId, Title)`, which an override
inherits from its base by construction. Re-running a bundle import overwrote the customer's edited lesson
and its exercises with the base text. Nothing in the response said so, no log line named it, and the
customer's only symptom was that their edits were gone. `Skills.ToDictionaryAsync(IconicName)` had a
louder variant of the same flaw: a duplicate key exception the moment two organizations shared a name.
The `*/export` endpoints had the mirror bug — an export carried the overrides out into a file that
re-imported as if it were the shared library.

**Chosen: narrow by query, not by role.** Every read is `Where(x => x.OrganizationId == null)`. Rejected
alternative: run the controller in platform mode, or drop the tenant header. Both work today and both
depend on ambient state a future middleware change can alter; a `WHERE` clause holds whatever header the
request carried.

**Chosen: `target=global`, a required literal.** Rejected: making it optional with `global` as the default,
which is the ergonomic choice and defeats the purpose — the point is that seeding the shared library is a
*stated* intention, and that the day somebody wants per-organization seeding they must answer the question
this endpoint currently refuses to be asked. Also rejected: naming it `organizationId` and accepting `null`
for global. That would put a tenant identifier in a request body, which is the one thing Stage C forbids
outright (`docs/TENANCY/TENANCY.md` §1.3) and `scripts/tenancy-boundary-lint.py` catches. `target` is a
mode selector with one legal value and must never become an id.

`seed.py` sends the field and deliberately grew no flag to point itself anywhere else. If per-organization
seeding is ever wanted, that is a change to the API contract and to `docs/SEEDER.md`, not a command-line
switch somebody can pass by accident at 2am.

---

## Phase 40.18 — copy-on-write overrides and the staleness queue (2026-08-18)

Nine forks the roadmap left open, decided during an unattended run under the rules in
`docs/DONT_FORGET.md` (no questions, no new tests, nothing executed against any database).

### An override is a row in the same table, not a link table and not a column on the version

The roadmap asks where an override lives. Three shapes were possible.

**Rejected: a link table** `content_override (organization_id, base_id, override_id, kind)`. It makes
"is this row an override?" a join, on the learner's hot path, for a fact the row itself could carry;
and it makes the invariant that matters — *a global row is never somebody's override* — inexpressible
as a constraint, because the ownership lives in one table and the parenthood in another.

**Rejected: `organization_id` on `lesson_version` alone**, leaving `Lessons` global. The unit of
override would then be a snapshot rather than a lesson, so an organization's copy would have no
identity between publishes, nothing for progress or a programme pin to point at, and no slug.

**Chosen: the parent pointer lives on the content row.** 40.15 had already built exactly this for
lessons (`Lessons.ParentLessonId`, `LessonVersions.BaseVersionId`), so the block's schema work was
bringing `Techniques` and `ReferenceMaterials` — the two the roadmap warns are easy to forget — to
the same shape, and adding the CHECK that says an override always has an owner. That CHECK is the
one thing 40.15 left unstated, and it is now on `Lessons` too.

### Staleness is derived on read; there is no `stale` flag anywhere

The roadmap says overrides are "marked stale and fall into a review queue". The obvious reading is a
column somebody sets.

**Rejected: mark synchronously inside the publish transaction.** It cannot work, and the reason is
structural rather than a matter of taste. Publishing a global lesson is done by platform staff with
no organization set, or by one organization's administrator; marking every *other* organization's
override means writing rows into tenants the writer is not in, and the RLS `WITH CHECK` clause — the
one clause the 2026-08-16 role split deliberately did **not** widen for platform staff — refuses
exactly that. Making it possible would mean giving the publish path a bypass, which is a much larger
hole than the problem it solves.

**Rejected: a background sweep** that walks overrides and sets a flag. It works, and it would have
gone into `docs/TENANCY/BACKGROUND_JOBS.md` with a declared mode as 40.14 requires. But it buys
nothing: it can only ever restate a comparison that two columns already answer, it can lag, and while
it lags the queue says an override is current when its base has already moved — which is the one
error a review queue must not make. It would also be the sixth job to inherit the `BYPASSRLS`
dependency the registry already lists five times.

**Chosen: the queue is a query.** `GET /admin/content/overrides?staleOnly=true` compares, per
override, the fork marker against the base as it stands right now. A flag that is computed cannot
disagree with the facts, and the three review actions resolve staleness by changing the facts the
query reads rather than by clearing a flag: **accept base** retires the override, **keep override**
re-points the fork marker, **edit and publish** re-points it as a side effect (40.15's
`ResolveBaseVersionIdAsync` already reads the parent's latest published version).

The cost is honest and small: the queue is O(overrides of this organization), which is single digits
to low tens, not O(library).

### Lessons compare version ids; techniques and reference materials compare a content fingerprint

40.15 froze every published lesson, so a lesson's fork point can be a pointer at a snapshot that
still exists — and the review screen can therefore show *what upstream said before* as well as what
it says now. `Technique` and `ReferenceMaterial` have no version table.

**Rejected: build two more version tables.** That is 40.15 done twice, including the freeze trigger,
the partial unique draft index, the canonical serializer and the publish endpoint, for two families
whose content is one row each. It would have doubled the block.

**Chosen: `BaseContentHash`** — the SHA-256 of the base's canonical content at fork time, using the
same `CanonicalJsonWriter` 40.15 built. It answers the only question the queue asks ("has upstream
moved?") exactly as well as a version id. What it gives up is the before-image: the review screen
shows the organization's text beside the base's *current* text and cannot show the base's previous
text, because nothing stored it. That is the honest trade and it is written into the DTO
(`BaseAtFork` is null for those two kinds) rather than hidden.

Identity, ownership and `UpdatedAt` are deliberately outside the hash. A base re-saved unchanged has
not changed, and a queue that cries wolf on `UpdatedAt` teaches the person reading it to click
through without looking — which is the failure mode that ends with a stale grading criterion scoring
a real salesperson.

### No auto-merge, and no server-side diff either

The roadmap is categorical about the merge. The block extends the same reasoning one step: the
review endpoint returns three whole documents and computes no diff. A textual diff of prose is the
first half of a merge, and once the API produces one, the pressure to "just apply the
non-conflicting hunks" becomes a product conversation rather than an architectural one. The screen
may diff for display; the server states facts.

### Read resolution is an explicit call on the learner-facing paths, not a query filter

The tenancy query filter admits "mine or global", so an organization with three overrides sees three
lessons twice. Resolution — hide a global row when the caller's own organization has a live override
of it — is the missing half of copy-on-write.

**Rejected: fold it into `HasQueryFilter`.** A filter that references its own `DbSet` is applied
recursively to the subquery inside it, and EF offers no way to say "the anti-join, but unfiltered".

**Rejected anyway, and this is the stronger reason: the authoring paths must see both sides.** The
review screen's entire job is showing the base next to the override; a filter that hid the base would
make the queue unbuildable from the same context that serves it. So the rule is explicit: learner
reads resolve, authoring reads do not, and platform-wide callers do not either — in platform mode the
filter admits every organization at once, so "somebody's override exists" would hide a global lesson
from Sellevate staff because one customer edited it.

Cost is one `NOT EXISTS` against an index that exists for this and nothing else.

### The write boundary between "my override" and "the shared library" is in C#, not in RLS

This is the sharpest thing the block found, and it was true before the block: the content RLS policy
is `OrganizationId IS NULL OR OrganizationId = current` in the `WITH CHECK` clause as well as the
`USING` clause, because a customer must be able to read the global library. Read as a write rule that
says *any organization may write a row with a null owner* — that is, may edit every other customer's
curriculum. The database cannot tell the two cases apart, because "global" is a null and not a
tenant.

**Rejected: change the content `WITH CHECK` to plain equality.** It would be correct in isolation and
would break the seeder, the bundle importer and every platform-staff authoring path, all of which
legitimately write null-owner rows.

**Chosen: `ContentAuthoringGuard`**, one rule stated once and called from every mutating content
route: a row with an owner may be written by an administrator of that organization (RLS has already
proved they are inside it); a row without one needs platform rights. Creating brand-new content from
nothing stays platform-only — an organization customizes what exists, and originating an original
curriculum is 40.19/40.20's question.

### The four content admin controllers were opened to organization administrators

`AdminLessonsController`, `AdminExercisesController`, `AdminTechniquesController` and
`AdminReferenceController` were all `RequirePlatformAdmin`. Left that way, copy-on-write would have
produced a copy that nobody but Sellevate could edit — the third of the review screen's three
actions would have had no route at all, and the block would have shipped a mechanism with no use.

They now carry `RequireOrgAdmin` plus the per-row guard above. Two real bugs surfaced while doing it,
both of which would have been silent: an exercise created inside an override lesson inherited no
organization and would have landed in the shared library (appearing inside that lesson for every
other customer), and the technique slug-clash check spanned "global or mine", so an override — which
carries its base's slug on purpose, to keep the URL stable — could never be saved.

The same widening also forced `[TenantTransaction]` onto those controllers, closing the gap
`TenantTransactionScope` had been documenting about itself since 40.10: they opened no transaction,
so `SET LOCAL app.organization_id` never ran, and the moment an organization owned a row they would
have stopped being able to see it. Fail-closed, and invisible in the logs.

### Retiring an override archives it; it is never deleted

"Take the new base" has to make the override stop shadowing its parent. Deleting the row is the
obvious implementation and the wrong one: `UserExerciseAttempt`, `UserLessonProgress` and
`UserTechniqueProgress` point at these rows without a foreign key (40.16's decision, for reasons that
still hold), and Mongo dialog sessions carry `ModeId` the same way. Deleting to tidy a review queue
would orphan the history that the whole of 40.15/40.16 exists to protect.

So `IsArchived` was added to `Techniques` and `ReferenceMaterials`, matching the column 40.15 already
put on `Lessons`, and resolution ignores archived overrides — which is exactly "the base is visible
again". In ai-service the same job is done by the existing `IsActive` flag rather than a new column,
because the mode list already filters on it and a second retirement flag beside it would be two
things that can disagree.

A retired override is **revived** rather than duplicated if the organization presses "edit" again:
`UNIQUE (OrganizationId, Slug)` and `UNIQUE (OrganizationId, BundleId, Key)` make a second copy
impossible anyway, and the organization had already discarded its text when it accepted the base, so
the revived row is re-derived from the current base rather than recovered.

### ai-service: modes are override-able, bundles are not, and no Kafka event crosses services

The roadmap names `DialogMode`/`DialogBundle` together. Only the first carries a prompt: a bundle has
a title, a description, an emoji and a sort order.

**Rejected: copy-on-write for bundles.** A copied bundle is an empty folder — the global modes still
belong to the original — so it needs a second resolution layer answering "which modes does this copy
contain", and the natural answer ("all of the parent's, plus mine, minus the shadowed ones") is the
whole-library fork of CONTENT_MODEL §1 reproduced one level down. An organization that wants its own
folder creates one, which 40.11 already allows.

**Chosen:** the override is a `DialogMode` row keeping its parent's `BundleId` and `Key`, which the
40.11 unique indexes already permit (the composite one is filtered to non-global rows). The overridden
prompt therefore appears in the same bundle in the same position with no extra machinery.

The seeded hidden modes (`company-call`, `custom-scenario`) stay global and the service **refuses** to
override them, rather than merely not offering it. Their prompts are half code: the service completes
them at run time from placeholders (the company being called, the scenario the learner typed), and a
per-organization copy would drift away from the code that feeds it until it silently stopped matching.

**Rejected: a Kafka event between learning-service and ai-service** to propagate staleness. There is
nothing to propagate. An override and the base it forked from are always the same content family in
the same database, so staleness is an intra-database comparison everywhere it is asked. An event would
add a delivery guarantee, an ordering question and a dead-letter path to a query that cannot be wrong.

### No `40.18_*_indexes_concurrently.sql`, stated as a decision rather than left absent

Every operation in both migrations is cheap on Postgres 11+: nullable columns and a NOT NULL boolean
with a constant default are catalog changes; the three new indexes are built over tables holding tens
to hundreds of rows; the CHECK constraints scan the same tens. 40.10–40.13 needed concurrent scripts
because they rebuilt indexes on tables that were already large and already live, and nothing here has
that shape. What exists instead is a read-only
`docs/TENANCY/sql/40.18_content_overrides_verify.sql`.

---

## Phase 40.17 — programme versioning and enrollment (2026-08-17)

Twelve forks the roadmap left to the agent, decided during an unattended run under the rules in
`docs/DONT_FORGET.md` (no questions, no new tests, nothing executed against any database).

### The tables live in learning-db, beside the lessons they reference

`program_item` pins a `lesson_version_id` and names a `skill_id`. Both live in learning-db, and a
programme is meaningless without them.

**Rejected: organization-service.** It is the tenant registry — organizations, their auth config,
their profile — and it has never held a row that references content. Putting the curriculum there
makes every programme read a cross-service call and makes "does this pinned version still exist" a
question no database can answer.

**Rejected: a new programme-service.** Three tables, no independent scaling story, no separate
lifecycle, and an immediate synchronous dependency on learning-service for every read. That is the
distributed monolith the microservices doc already warns about.

**Chosen: learning-db.** The verify script can then check by query what a foreign key would have
checked (that every pinned version exists, that the denormalized `LessonId` agrees with its
snapshot), which is precisely what the cross-service options give up.

### All three tables are strict tenant data, and this is the first place the content flavour is wrong

Everything Stage D added to learning-db so far — `Lessons`, `LessonVersions`, `Skills`, `Topics` —
is a *content* table: `OrganizationId` is nullable, `NULL` means the global library, and the RLS
policy is `IS NULL OR = current`. The obvious move is to keep going.

**Rejected: nullable `organization_id` with a global default programme.** A `NULL` owner here would
mean "everybody's programme", and the moment one exists, publishing a new version of it rearranges
every customer's curriculum at once — the fork-avoidance argument of §1 inverted into the worst
possible blast radius. A curriculum is not content anybody shares; it is a decision one organization
made about its own people.

**Chosen:** `NOT NULL`, `ITenantScoped`, `EnableTenantRls` with plain equality on all three tables.
The verify script asserts explicitly that neither `qual` nor `with_check` contains `IS NULL`, because
"copy what the neighbouring table does" is exactly how that mistake would arrive later.

### `program_item` carries `lesson_id` as well as `lesson_version_id`

The roadmap names four columns: `program_version_id, skill_id, order_index, lesson_version_id`. A
fifth looks like padding.

It is the one that makes the block's central question expressible. With only the version id, the
unique constraint can say "this snapshot appears once" but not "this lesson appears once" — so a
curriculum could list the same lesson at versions 3 and 5 simultaneously, which is the same material
with two different answer keys inside one programme. And the diff's largest bucket, "same lesson, now
pinned to a newer snapshot", needs lesson identity to exist at all.

It cannot drift: a published `LessonVersion`'s `LessonId` is frozen by 40.15's trigger.

**Rejected: derive it by joining `LessonVersions` on every read.** The join works and is cheap; what
it cannot do is be a unique index.

**Also rejected: a separate `skill_order_index` beside `order_index`.** Reordering skills is
expressible as a permutation of one dense running order, and two ordering columns are two chances for
them to disagree about what comes first.

### No foreign key on the three content references; a real one on the enrollment

`program_item.skill_id`, `.lesson_id` and `.lesson_version_id` get no foreign key, for exactly the
reason 40.16 gave for `UserExerciseAttempt.LessonVersionId`: those are content tables under an
`IS NULL OR = current` policy while `ProgramItems` is strict tenant data under plain equality, and a
constraint spanning the two is validated with the writer's privileges. It would either leak the
existence of rows the writer may not read or refuse writes it should allow.

`ProgramEnrollments.ProgramVersionId` **does** get one, `ON DELETE RESTRICT`, because both sides are
strict tenant data under the same policy and always in the same organization — the objection above
simply does not apply. `RESTRICT` rather than `CASCADE`: a programme version somebody is standing on
is not something to delete, and refusing the delete is a far better answer than silently unpinning a
learner mid-course.

### No "programme version 1", and nobody is enrolled by the migration

This is the decision most likely to be re-litigated, because 40.16 made the opposite call one block
earlier: it *did* mint a "version 1" for every lesson that had never been published.

The two cases are not alike. A lesson's version 1 is a faithful snapshot of something that already
exists — the body is right there in the `Exercises` rows, and the only thing missing was the act of
freezing it. A programme version is not a snapshot of anything; it is a curriculum decision, and
nobody has made it yet. The live skill tree is not a curriculum, it is whatever the seeder happened
to load.

**Rejected: mint a programme version 1 from the live tree and enroll every existing user.** It reads
as the tidy, symmetric move and is the worse one. Everybody currently learning would be pinned,
silently, to a snapshot nobody authored — and pinning means they stop receiving content improvements
until they notice a switch prompt for a programme they never opted into. Today's behaviour (always
the newest content) would become tomorrow's frozen copy, invisibly, on deploy day.

**Chosen:** the migration creates three empty tables and nothing else. The first programme version
exists when an administrator calls `POST /admin/program/versions/draft` and then `/publish`.

### Enrollment does not gate access to lessons — fail-open, deliberately, and against the house style

Every tenancy decision in Phase 40 has been fail-closed: an unset organization yields zero rows, a
write with no tenant throws. The consistent-looking move is to require an enrollment before a learner
may open a lesson.

**Rejected: fail-closed.** There is no screen anywhere that builds a programme (that is 40.20), so on
the day this deploys, every organization has zero published programme versions and zero enrollments.
Fail-closed would mean nobody in the product can open a lesson until a РОП uses an API by hand. That
is not a security posture, it is an outage.

The deeper reason is that fail-closed is the right default for *data* and the wrong one for a
*curriculum*. Fail-closed exists so one customer never sees another's rows; an unenrolled learner
reading the global library sees nothing that is not already theirs to see — the content RLS policy is
unchanged and still decides that. The question here is "which subset, in what order", and the honest
answer when no programme exists is "all of it, in tree order", which is exactly what the product did
yesterday.

**Chosen:** absent an enrollment, `GET /program` answers `isEnrolled: false` and the existing read
paths behave exactly as before. Enrollment narrows and freezes; it does not authorize.

### Enrollment is asymmetric: an administrator may create a pin and may never move one

The roadmap sentence is "new enrollments go to the new version; existing learners get an explicit
switch with a diff". Two operations, and the temptation is to write one that does both.

**Rejected: one `PUT /admin/program/enrollments/{userId}` that sets the version.** It makes "the
manager on lesson 8 of 21 is not rearranged" a matter of which button the UI draws. The first support
request of the form "can you just move everyone onto v4" gets satisfied by an endpoint that exists.

**Chosen:** `POST /admin/program/enrollments` is idempotent and returns an existing pin *unchanged*,
so re-running "enroll everybody" after a publish enrolls the newcomers and moves nobody.
`POST /program/switch` acts on the caller's own pin, takes no user id, and there is no third route.
The property is then structural rather than procedural.

**The switch names its target rather than meaning "whatever is newest".** The learner was shown a
diff against a specific version; if another is published in between, the id they send no longer
matches and the call is refused with 409 instead of landing them on a programme nobody showed them.
The race is small and the refusal is cheap, and the entire block exists because of what a silent
programme change does to somebody mid-course.

### Freezing is a database trigger — and the one on `program_item` is the important half

Same shape as 40.15, sharper reason. A frozen lesson snapshot that can be edited corrupts a metric; a
frozen *programme* that can be edited rearranges the curriculum under a person who is on lesson 8 of
21, which is the failure the block is named for.

The structure lives in the item rows, so `ProgramItems_reject_frozen_change` fires
`BEFORE INSERT OR UPDATE OR DELETE` — `DELETE` included, because removing a lesson from a frozen
programme is the same edit seen from the other side.

The subtle part is how a legitimate cascade gets through. The trigger looks up its owning programme
version's status; when the lookup finds nothing it allows the row, because Postgres runs
`ON DELETE CASCADE` *after* the parent row is deleted, so "no parent" means the version itself is
being dropped. That branch is not a hole for a mis-tenanted write: an `INSERT`/`UPDATE` whose
organization does not match the session GUC is refused by the table's own RLS `WITH CHECK` before the
lookup could be fooled by RLS hiding the parent, and a session with no GUC cannot write these tables
at all.

**Rejected: enforce the freeze in `ProgramVersionService` only.** In a repository whose entire
tenancy argument is that application-layer filters are not enough (TENANCY.md §1.5), a code-only
guarantee about immutability is the weaker claim.

### There is no content hash; the no-op publish check compares reference tuples

40.15 stops a no-op publish with a SHA-256 over the snapshot. A programme has no body to hash — it is
references — so the equivalent is comparing the draft's `(lesson_id, lesson_version_id, skill_id,
order_index)` tuples, in order, with the last published version's.

**Rejected: hash the tuple list and store it.** A stored hash is a third representation of the same
facts that can drift from them, and it buys nothing: the comparison reads two small item lists that
are already being loaded.

It matters more here than it does for lessons. A programme version that changed nothing would still
tell every enrolled learner that a new programme is waiting and then show them an empty diff — which
is precisely how a switch notice stops being read, and the notice is the whole mechanism by which a
breaking change reaches a human.

### `is_breaking` in the diff reads the interval between the pins, not the target version's flag

The shortcut is to report the target `LessonVersion.IsBreaking`. It is wrong whenever a programme
skips more than one lesson version, which it usually will: a changed correct answer in version 4
would be hidden behind a cosmetic version 5, and the learner would be told the switch is safe.

**Chosen:** ask whether *any* published version of that lesson strictly after the lower of the two
version numbers and up to and including the higher declared itself breaking. Expressed with min/max
so a deliberate move back to an older programme is reported just as loudly — the learner crosses the
same edit either way. A pin whose snapshot is missing or invisible counts as breaking: "the content
changed and nobody can say how" is a breaking change.

### The draft is re-derived from the live tree, and titles come out of the snapshot

Two smaller calls that mirror 40.15 rather than inventing something.

`POST /admin/program/versions/draft` re-walks skills → topics → lessons on every call and rebuilds
the draft's items, exactly as 40.15's lesson draft is re-serialized from the live rows. The
alternative — a draft the admin edits directly — means a second authoring surface, a sync problem
with the tree, and a screen that does not exist yet.

Each item is pinned through `ILessonVersionService.EnsurePublishedVersionIdAsync`, the same resolver
an exercise submission goes through, so a programme and the progress recorded against it can never
disagree about which snapshot a lesson currently is. It is called *before* the write scope opens, for
the reason its own remarks give: minting can lose a unique-index race, and a unique violation aborts
the whole transaction it happens in. One resolver call per lesson is more round trips than a single
query, and that is the accepted price of not writing a second resolver.

`lessonTitle` in every DTO is parsed out of the pinned snapshot rather than read off the live
`Lessons` row. Showing the current title beside an old pin is the retroactive substitution this whole
phase exists to stop; `null` (snapshot gone or invisible) is a truer answer than the new title. The
cost is loading `Content` — the largest column in `LessonVersions` — for every pinned version, which
is acceptable at programme size and worth a generated column if a curriculum ever gets big enough to
feel it.

### `/skill-tree` is not rewired onto the programme; `GET /program` is a new route

The pin has to be readable somewhere. The maximal move is to make `SkillTreeService` serve the pinned
programme whenever an enrollment exists.

**Rejected, for now.** `/skill-tree` is the read path the entire product depends on, it has existing
unit tests, and Rule №3 forbids writing tests for the change. More to the point, it would change
nothing for anybody: on deploy day there are no enrollments, so the branch would be dead code with
the blast radius of the main read path.

**Chosen:** the pinned programme is `GET /program`, complete with items, upgrade availability and the
pending diff, so wiring the learner's screens onto it is a frontend change rather than a backend one.
The honest cost — until that wiring happens, the pin does not change what a learner sees on the
existing screens — is recorded in `docs/DONT_FORGET.md` and in the roadmap, not glossed.

### A separate `ProgramVersionStatuses`, and no `_indexes_concurrently.sql`

`ProgramVersionStatuses` duplicates `LessonVersionStatuses` value for value today. Sharing one
constant would couple two check constraints that are frozen by different triggers for different
reasons — and 40.18's likely `stale` state for lessons would silently widen the programme's
vocabulary too.

And, as in 40.15, there is no companion concurrent-index script: all three tables are created empty
by the migration, so every index is built over zero rows, and two of them (one draft per
organization, one pin per learner) are correctness constraints that must not wait for a script
somebody has to remember to run. What exists instead is
`docs/TENANCY/sql/40.17_program_versioning_verify.sql` — read-only, safe with the service up, and
never executed against anything.

---

## Phase 40.16 — progress bound to a lesson version (2026-08-17)

Seven forks the roadmap left to the agent, decided during an unattended run under the rules in
`docs/DONT_FORGET.md` (no questions, no new tests, nothing executed against any database).

### The attempt keeps `ExerciseId` and gains `LessonVersionId`; there is no third column

The roadmap asks for "`lesson_version_id` + the exercise's identity **inside** the version, not the
mutable `ExerciseId`", which reads at first like two new columns and a retired one.

It is one new column, because 40.15 already put `exerciseId` **inside** the snapshot and therefore
inside its hash. The identity of an exercise within a version is that key; the pair
`(LessonVersionId, ExerciseId)` locates the exact question, options and answer key the learner saw.
`ExerciseId` is not retired — it changes meaning rather than shape, from "a pointer at an editable
row" to "a key into a frozen document", and the code and schema comments say so where somebody will
read them.

**Rejected: a separate `ExerciseIndexInVersion` (ordinal position).** Positions move when an admin
reorders exercises, so the ordinal is exactly the mutable thing being escaped from. **Rejected:
dropping `ExerciseId`.** Every existing read path uses it, and the "did the learner attempt every
exercise in this lesson" gate is a question about the *current* lesson, where the live row is the
right answer.

### Both new columns are nullable, and neither is a foreign key

**Nullable**, because attempts recorded before this phase have nothing to point at until the
backfill runs, and because a lesson can legitimately have no published version at the moment its
column is added. `NULL` means "unversioned" and is reported as its own bucket by the accuracy
endpoint. The alternative — `NOT NULL` with a placeholder version id — is the all-zeros-organization
trick of 40.10 applied to a case that does not need it: nothing filters on this column, so a `NULL`
hides no row and creates no fail-closed window.

**No foreign key**, because `LessonVersions` is a content table under an `IS NULL OR = current` RLS
policy while `UserExerciseAttempts` is strict tenant data under plain equality. A foreign key is
validated with the referencing statement's privileges, so on the day the service runs as the
`NOBYPASSRLS` role `sellevate_app`, a perfectly valid reference could be rejected because the
checking statement cannot see the row. `ExerciseId` has never carried one either, for the same
family of reasons.

### "Version 1" for existing lessons is minted in C# at startup, not by the migration and not by SQL

This was the fork the task named explicitly, and the deciding argument is not about migrations at
all — it is about the hash.

`LessonVersion.ContentHash` is a SHA-256 over the exact bytes `LessonSnapshotSerializer` emits, with
object keys in **ordinal** order. Postgres stores `jsonb` with its own key ordering (length, then
bytes) and its own whitespace. A version minted by `jsonb_build_object` in a migration or in a
`psql` script would therefore carry a hash the service never reproduces — and the very next publish
would see a mismatch and mint a second, byte-identical version. That defeats the single thing
`content_hash` exists for, and it would put a spurious break in the metric series this phase is
being written to protect. Whatever mints these snapshots has to be the same code that hashes them.

**Chosen: `LessonVersionBackfill`, resolved in learning-service's startup scope after
`Database.Migrate()`, in system mode** — the shape gamification-service already uses for its
seeders. Idempotent, a no-op on every start after the first (one indexed query returning zero rows),
and one transaction per lesson so a single lesson with unparseable exercise content cannot stop the
service from starting.

**Rejected: raw SQL inside the EF migration** — the hash problem above, and it would also have to
run before the versions it needs exist. **Rejected: a human-run `.sql` file for the whole job** —
same hash problem. **Rejected: lazily, on the first attempt after deployment** — that binds *new*
attempts fine (and is in fact what `EnsurePublishedVersionIdAsync` does) but leaves every
*historical* attempt unbound forever, which is the half of the block the roadmap actually asked for.

The second half — pointing the existing attempt and progress rows at that version — **is** plain SQL
and is in `docs/TENANCY/sql/40.16_progress_version_backfill.sql`, batched, run by a human. It is a
full-table update on tables that grow with usage, which is not something to do inside a startup path
that a readiness probe is waiting on.

### An attempt on a never-published lesson mints version 1; an attempt on a *drifted* lesson does not

`EnsurePublishedVersionIdAsync` resolves the newest published version and mints one only when the
lesson has none at all. It deliberately does **not** compare the live rows' hash against the last
published snapshot and mint on a mismatch.

**Why the minting branch exists at all.** Publishing is an administrator's act, and at the moment
40.16 ships nobody has ever performed it — 40.15 created the table and left every lesson with zero
versions, and there is still no admin screen for it (40.20). An attempt on such a lesson would have
nothing to point at, which is the bug this phase closes.

**Why it stops there.** Minting on drift would stamp every unpublished administrator edit as an
unattributed content change. Since nothing can tell such a change from a typo, it would have to be
recorded as breaking, and the accuracy series would then split every time someone fixed a comma —
the precise failure `is_breaking` exists to prevent, arrived at from the other side. It also races
the explicit publish endpoint: a learner submitting between the edit and the publish would mint a
breaking version and the administrator's `isBreaking: false` would be swallowed by the
identical-hash branch.

**The accepted gap, stated plainly:** an administrator who edits an exercise and does not publish
has learners answering new content bound to the previous snapshot. Publishing is what makes an edit
historically visible, and 40.20's admin screen has to make publishing the natural end of editing.
Recorded in `docs/DONT_FORGET.md` rather than half-fixed here.

### The version is resolved *before* the submission's write transaction, not inside it

Minting can lose a unique-index race on `(LessonId, VersionNumber)` to another learner answering the
first exercise of the same never-published lesson. In Postgres a unique violation aborts the entire
transaction it happens in, so a recovery read placed inside that transaction fails with "current
transaction is aborted" — and inside `SubmitExerciseAnswerAsync`'s write scope it would take the
learner's answer down with it. The resolve therefore runs first, in its own transactions, and the
loser of the race adopts the row the winner created. The snapshot is immutable once published, so
reading it a moment earlier costs nothing.

### `UserLessonProgress.LessonVersionId` is refreshed only when the row advances

Set when the row is created; updated afterwards only on a new best score or the transition to
completed.

**Rejected: refresh on every submission.** The row records two facts — the best score and whether
the lesson is finished — and both belong to the version they were earned on. Restamping on every
answer would relabel "completed version 1" as "completed version 3" the first time a learner opened
the lesson again after a breaking edit they never saw, which is the retroactive rewrite this phase
exists to stop, reached from the progress side instead of the attempt side.

### The accuracy series lives in learning-service; analytics-service gets documentation only

The roadmap says "the dashboard joins cosmetic versions and splits semantic ones", and there is no
dashboard. Where the joining logic should live was a real fork.

**analytics-service cannot host it.** It is Redis-only by design, stores no attempts, no scores and
no lesson ids, and its `exercise.completed` consumer increments one platform-wide Prometheus counter
with no lesson, no version and no organization in it — deliberately, because a customer id as a
Prometheus label puts identities and unbounded cardinality into the monitoring store. Making it
compute accuracy means giving it a database and a copy of the attempts, i.e. building a second
system of record for the number whose trustworthiness is the entire point.

**Chosen: `GET /admin/lessons/{lessonId}/accuracy` in learning-service**, which owns the data, and a
new section in `docs/ANALYTICS_SERVICE.md` stating the rule for anyone who later draws the chart:
aggregate per version, join across cosmetic publishes, split at breaking ones, and never fold the
unversioned bucket into version 1. That last one is not a formatting preference — merging attempts
whose content nobody can identify into a version's series is the same unprovable claim the phase
removes, told by the fix instead of by the bug.

The endpoint carries `RequireOrgAdmin` with no second platform-level gate, unlike the publish routes
of 40.15. It writes nothing and counts only the caller's own organization's attempts, so an
organization administrator asking about a global lesson gets their own team's numbers — exactly the
question a РОП is entitled to ask about content they did not write.

### There *is* a `40.16_*_indexes_concurrently.sql`, and that reverses 40.15's call

40.15 put its indexes in the migration and said the absence of a concurrent-index script was a
decision. 40.16 puts them in a script, and the difference is which tables are involved.
`LessonVersions` was created empty and `Lessons` is a few hundred content rows; `UserExerciseAttempts`
and `UserLessonProgressRecords` grow with every answered exercise, and 40.10 already moved every
index on those two tables out of the migration for exactly this reason. Adding the columns stays in
the migration — both are nullable, so Postgres 11+ treats it as a catalogue-only change with no
rewrite and no long lock.

The index names in the script are the exact ones EF Core generates, including the `~` that marks
EF's truncation at Postgres's 63-byte identifier limit. Renaming them for readability would make the
next `dotnet ef migrations add` decide the indexes are missing and emit a table-locking
`CreateIndex` into a startup path — which is the thing the script exists to avoid.

---

## Phase 40.15 — immutable lesson versioning (2026-08-17)

Six forks the roadmap left to the agent, decided during an unattended run under the rules in
`docs/DONT_FORGET.md` (no questions, no new tests, nothing executed against any database).

### `Lessons` is extended into the lifeline; `lesson_version` is the only new table

The roadmap names two tables, `lesson` and `lesson_version`, and the existing schema already has a
`Lessons` table. Two readings were possible.

**Rejected: create a new `lesson` table beside `Lessons` and migrate.** It produces two rows that
both claim to be "the lesson", and every downstream block then has to say which one it means —
40.16's attempts, 40.17's `program_item`, 40.18's overrides, plus every existing read path, the
seeder and the admin panel. The migration would be a rename dressed up as a new concept.

**Chosen: `Lessons` *is* the `lesson` table** and gains the three columns that make it a lifeline
rather than a leaf — `ParentLessonId`, `Slug`, `IsArchived`. `LessonVersions` is genuinely new,
because nothing like it existed. This is also what CONTENT_MODEL.md §2.1 describes when it calls
`lesson` "the identity/lifeline of a lesson": an identity the existing table already was.

**Room left for 40.16 without doing its work.** 40.16 has to attach historical attempts to a
"version 1". Nothing here creates a version for an existing lesson — the first version appears when
an admin opens a draft or publishes — so 40.16 is free to decide whether version 1 is minted by a
backfill script or lazily on first read. What it gets for free: a lesson's version numbering starts
at 1 by construction, and the snapshot carries `exerciseId`, which is the join key an attempt needs.
Deciding that for it would have been guessing at a migration whose shape depends on how many
historical attempts exist.

### The snapshot is denormalized JSON of the whole lesson, and `exerciseId` is inside the hash

`{ "exercises": [{ "content", "customAiPrompt", "exerciseId", "orderInLesson", "type" }],
"schemaVersion", "title" }`. The alternative — version each `Exercise` row and reconstruct a lesson
from N version rows — makes every historical question ("what did this learner actually answer?") an
archaeology exercise. A lesson is kilobytes; copy the whole thing.

`exerciseId` is in the document and therefore in the hash, which is a real trade and was taken
knowingly: deleting an exercise row and recreating it with identical content yields a new hash and
so a new version. Keeping identity *out* of the hash while storing it *in* the content would be
worse than either option — two versions could then have the same hash and different content, which
destroys the hash's meaning entirely. And the identity really did change; a version that pretends
otherwise lies to 40.16.

### The hash is over a canonical form, and canonicalization is not optional

Object keys sorted ordinal, array order preserved (it is meaningful in exercise content), the whole
document then SHA-256'd as UTF-8 and stored as lowercase hex.

Without sorting, an admin panel that re-serialized an exercise's content with its keys in a
different order would look like a content change on every save, and "publish with no changes" would
mint a version every time — which is exactly and only what `content_hash` exists to prevent. The
stored `Content` **is** the canonical form, so the hash is a function of what is stored rather than
of a parallel representation that could drift from it.

Two limits, accepted and written down rather than hidden. Numbers are passed through unchanged, so
`1` and `1.0` hash differently; normalizing them means choosing a numeric model (double? decimal?)
and silently rewriting customer content to fit it. And the column is `jsonb`, so Postgres
re-normalizes on write and a `SELECT` does not return the hashed bytes — anyone recomputing the hash
from a query result will get a mismatch and must go through `LessonSnapshotSerializer` instead.

### The draft is re-derived from the live rows, not edited in place

CONTENT_MODEL.md says two things that look like they conflict: the draft row is mutable and is what
editing happens on (§2.1), and `Exercise` rows are the working representation the admin panel edits
(§2.2). They reconcile if the draft is a mirror: `LessonVersionService` re-serializes it from the
live `Lesson` + `Exercise` rows on every call.

**Rejected: make the draft's JSON the thing admins edit.** It would mean a second editor for the
same content, a sync problem between the two, and a rewrite of the existing admin panel — for a
block whose job is to add history, not to move the authoring surface.

The consequence is that the draft row is close to a cache, and the honest question is whether it
earns its place. It does, for what live rows cannot carry: who started editing, when, which base
version was forked, and the plain fact that this lesson has unpublished changes — the last being
what 40.18's stale-override queue reads. And it is the row the one-draft-per-lesson index
constrains, which is a guarantee that has nowhere else to live.

### Freezing is a database trigger, not a service convention

"Publishing freezes the row forever" is the property the whole table exists for, so it is enforced
where it cannot be bypassed. `LessonVersions_reject_frozen_change` refuses any change to `Content`,
`ContentHash`, `VersionNumber`, `LessonId`, `OrganizationId`, `IsBreaking` or `PublishedAt` once the
row has left `draft`, and refuses `published → draft` and any exit from `archived`.

A code-only guarantee would be the weaker claim in a repository whose entire tenancy argument is
that application-layer filters are not enough (TENANCY.md §1.5). The failure it prevents is silent:
a snapshot edited after the fact re-scores every historical attempt against it, which is precisely
the corruption 40.16 is being written to fix — arrived at from inside the fix.

`BaseVersionId` and `Status` are deliberately left writable on a frozen row. 40.18's review screen
offers "keep the override, re-point its base" as one of its three actions, and archiving a version
is a lifecycle move rather than a rewrite. The transition check is what stops that second door being
used to walk a version back to draft and edit it.

### Slugs are machine-generated; nothing transliterates Russian titles

`UNIQUE (OrganizationId, Slug)` needs a value for every existing lesson, and lesson titles are
Russian prose.

**Rejected: transliterate the title.** A transliteration table is a long-lived guess about how
«Работа с возражениями» should read in latin that nobody asked for, and it collides (two lessons,
one slug) exactly where the constraint is supposed to help.

**Chosen:** `lesson-<32 hex of the row's own id>`, unique by construction and needing no retry loop,
with an optional explicit `slug` on create and update that is *validated* rather than rewritten — an
admin who typed a slug meant that slug. This is cheap because nothing routes by the slug yet: making
them readable later is a rename, and a rename is safe for exactly that reason. The generated form is
duplicated in the migration's SQL and in `LessonSlugGenerator`; they must move together.

Two indexes, not one, because Postgres treats NULLs in a composite unique index as distinct — the
same trap paid for in 40.10 for `Skill.IconicName`, `Topic.IconicName` and `Technique.Slug`.

### Publishing global content requires platform rights; the policy alone would be a hole

The roadmap asks who may publish. `RequireOrgAdmin` on the controller admits an organization's own
administrator as well as any platform administrator — correct for an organization's own lessons, and
a hole for the global library, since a lesson with `OrganizationId IS NULL` is read by every
customer. So each write additionally resolves the lesson's owner and demands platform rights when it
is global.

Only one direction needs checking. The reverse — an organization administrator reaching another
organization's lesson — was already impossible before the request arrived, through the query filter
and the RLS policy. Re-checking it here would be a second implementation of the boundary, and a
second implementation is a second thing to get wrong.

### There is no `40.15_*_indexes_concurrently.sql`, and that is the decision

40.10–40.13 each shipped one, because each rebuilt indexes on tables that were already large and
already live: a transactional build takes `ACCESS EXCLUSIVE`, and those migrations run from
`Database.Migrate()` at startup where a long build stalls readiness and races the replicas.

Nothing in 40.15 has that shape. `LessonVersions` is created empty by the migration, so its indexes
are built over zero rows; `Lessons` is a content table of a few hundred rows. And the new index on
it enforces slug uniqueness, which is correctness — the same reasoning 40.13 used to put four unique
constraints in the migration rather than the script. Deferring a correctness constraint to a script
somebody has to remember to run is the worse trade.

The same applies to the slug backfill: it is in the migration because its value comes from each
row's own primary key, so unlike 40.9–40.13 there is no ordering requirement between two steps and
no window in which data is invisible. What was written instead is
`docs/TENANCY/sql/40.15_lesson_versioning_verify.sql` — read-only, safe with the service up, and
also never executed. If some future installation grows a `Lessons` table large enough to make this
the wrong call, the fix is to move three `CREATE INDEX` calls into a concurrent script, not to relax
the constraint.

---

## 2026-08-16 — `Admin`/`SuperAdmin` become platform roles, every tenancy gets its own pair

Requested directly by the project owner tonight, ahead of the remaining Phase 40 blocks, and
revising the split Phase 40.6 made. Verbatim:

> «admin и superadmin это чисто роли наши, которые не должны ограничиваться tenancy, они должны
> показывать все. а вот у каждой tenancy должны быть роли tenancy-admin и tenancy-superadmin. пока
> что различием сделай только то что только суперадмины могут добавлять/удалять пользователей.
> остальное у них одно и то же. админка у админов и админов tenancy будет разная в разном месте.
> это пока можно не продумывать, я сам потом пришлю дизайн.»

### The model

Two independent axes, both already in the JWT, now both fully populated.

**Platform roles** — `User.Role`, `role` claim. Sellevate's own staff, deliberately not bounded by
tenancy.

| Value | Role | May |
|---|---|---|
| 0 | `User` | nothing administrative |
| 1 | `Admin` | all platform content administration |
| 2 | `SuperAdmin` | everything `Admin` may, **plus adding and removing users** |

**Organization roles** — `Membership.Role` / `Invite.Role`, `org_role` claim.

| Value | Role | May |
|---|---|---|
| 0 | `Manager` | nothing administrative |
| 1 | `TenancyAdmin` | administer their own organization |
| 2 | `TenancySuperAdmin` | everything `TenancyAdmin` may, **plus adding and removing the organization's users** |

- **`Admin` is reinstated at value 1, and reusing the value is the safe move rather than the risky
  one.** Phase 40.6 removed `Admin` and deliberately left 1 unassigned so a legacy `Role = 1` row
  would fail loudly. But 1 meant exactly "global platform admin" before 40.6, which is precisely
  what it means again — any surviving row lands back on the meaning it already had. There is
  nothing to migrate and nothing to reinterpret. Allocating 3 instead would have left a permanent
  hole in the enum and a class of rows that still fail to deserialize for no benefit.
- **`OrgAdmin` is renamed to `TenancyAdmin` in place, at the same value 1.** Both columns are
  `integer` (`HasConversion<int>()` in `MembershipEntityConfiguration` and
  `InviteEntityConfiguration`), so the rename is source-level only: **no data migration and no EF
  migration**. `RoleEnumContractTests` pins every number and name so the next rename cannot silently
  re-label rows already in the database.
- **The retired `OrgAdmin` string is rejected, not mapped.** `InviteService.ParseRole` answers 400
  with a message naming both replacements. Silently mapping it to `TenancyAdmin` would let a stale
  client keep inviting people at a role it no longer means to, and the rename is exactly the moment
  that mistake is cheap to catch. Both role parsers also gained an `Enum.IsDefined` check —
  `Enum.TryParse` accepts bare numbers and would happily have persisted `(OrgRole)99`.

### The four policies

Declared identically in all six services by `AuthorizationPolicies.Register`, so a token means the
same thing wherever it lands.

| Policy | Satisfied by |
|---|---|
| `RequirePlatformAdmin` | `role` ∈ {`Admin`, `SuperAdmin`} |
| `RequireSuperAdmin` | `role` = `SuperAdmin` |
| `RequireOrgAdmin` | `org_role` ∈ {`TenancyAdmin`, `TenancySuperAdmin`} **or** `role` ∈ {`Admin`, `SuperAdmin`} |
| `RequireOrgSuperAdmin` | `org_role` = `TenancySuperAdmin` **or** `role` = `SuperAdmin` |

- **Platform staff satisfy the organization-scoped policies while holding no `org_role` claim at
  all.** That is the point of "не должны ограничиваться tenancy": a Sellevate administrator normally
  has no membership anywhere. Token issuance is unchanged — absent membership still yields no
  `org_id`/`org_role`, never an implied one — so the platform role alone has to carry them.
- **Making the *data* they then see span every organization is a separate concern** living in the
  tenancy layer (`ITenantContext`, query filters, RLS), not in these policies. This block
  deliberately stops at the seam.
- **Each service keeps its own `AuthorizationPolicies.cs` rather than sharing one from
  BuildingBlocks.** The file is a handful of constants and one registration method; a shared
  version would couple every service's authorization to a building-block release, and the
  duplication is pinned by an `AuthorizationPolicyContractTests` in each service's test project that
  asserts the wire-level names and the two asymmetries. *Alternative rejected — a
  `SellevateAuthorizationPolicies` extension in BuildingBlocks.* Less text, but it makes the six
  `Program.cs` files silently inherit policy changes from a dependency bump, which is the opposite
  of what an authorization decision wants.

### Route audit — every gated route, before and after

The rule applied: platform **content** administration is ordinary admin work and moves to
`RequirePlatformAdmin`; anything that creates, invites, deactivates or re-roles a **user or
membership** is superadmin-exclusive; impersonation stays superadmin-exclusive on its own merits.

| Service | Route(s) | Before | After |
|---|---|---|---|
| identity | `GET /admin/users`, `GET /admin/users/{id}` | `RequireSuperAdmin` | **`RequirePlatformAdmin`** |
| identity | `PUT /admin/users/{id}` | `RequireSuperAdmin` | `RequireSuperAdmin` |
| identity | `DELETE /admin/users/{id}/avatar` | `RequireSuperAdmin` | `RequireSuperAdmin` |
| identity | `PUT /admin/users/{id}/role` | `RequireSuperAdmin` | `RequireSuperAdmin` |
| identity | `POST /invites`, `DELETE /invites/{id}` | `RequireOrgAdmin` | **`RequireOrgSuperAdmin`** |
| identity | `DELETE /memberships/{userId}` | `RequireOrgAdmin` | **`RequireOrgSuperAdmin`** |
| identity | `POST /admin/platform/impersonation` | `RequireSuperAdmin` | `RequireSuperAdmin` |
| identity | `GET /admin/platform/impersonation` | `RequireSuperAdmin` | `RequireSuperAdmin` |
| identity | `POST /admin/platform/organizations/bootstrap-admin` | `RequireSuperAdmin` | `RequireSuperAdmin` |
| organization | `/organizations` (create, list, read, update, suspend, reactivate) | `RequireSuperAdmin` | **`RequirePlatformAdmin`** |
| organization | `/organizations/profile` | `[Authorize]` | `[Authorize]` |
| learning | `admin/skills`, `admin/skill-stages` | `RequireSuperAdmin` | **`RequirePlatformAdmin`** |
| learning | `admin/topics`, `admin/lessons` | `RequireSuperAdmin` | **`RequirePlatformAdmin`** |
| learning | `admin/exercises`, `admin/exercise-type-prompts` | `RequireSuperAdmin` | **`RequirePlatformAdmin`** |
| learning | `admin/techniques`, `admin/reference` | `RequireSuperAdmin` | **`RequirePlatformAdmin`** |
| learning | `admin/daily-quotes`, `admin/seeder` | `RequireSuperAdmin` | **`RequirePlatformAdmin`** |
| ai | `admin/dialog` | `RequireSuperAdmin` | **`RequirePlatformAdmin`** |
| ai | `admin/voice` | `RequireSuperAdmin` | **`RequirePlatformAdmin`** |
| gamification | `admin/gamification` | `RequireSuperAdmin` | **`RequirePlatformAdmin`** |
| gamification | `admin/leagues` | `RequireSuperAdmin` | **`RequirePlatformAdmin`** |
| social | `admin/discuss` | `RequireSuperAdmin` | **`RequirePlatformAdmin`** |

`RequireOrgAdmin` now has no call site at all: both of its former routes turned out to be
add/remove-a-user. It stays declared because it is the correct gate for the organization admin
panel that block 40.20 will build, and because leaving it out would invite the next screen to reach
for `RequireOrgSuperAdmin` by default.

### Three consequences worth stating

- **Bootstrapping an organization now creates a `TenancySuperAdmin`, not a `TenancyAdmin`.** Only a
  superadmin can invite, so a first admin without that rank would leave the organization unable to
  add anybody — a dead end on day one. The "already bootstrapped" guard follows the same value, which
  also gives an organization that somehow ends up with no superadmin a legitimate way back.
- **An impersonation token is granted `org_role: TenancyAdmin`, deliberately one rank below.** The
  impersonator borrows an organization to see what its people see; adding or removing that
  organization's users is not part of looking around, and the token already downgrades the platform
  role to `User` for the same reason.
- **Google login no longer refuses platform staff who hold no membership.** The membership check
  exists because an ordinary account with no organization has nothing to sign in to; a platform
  `Admin`/`SuperAdmin` has the whole platform admin panel. Without the carve-out the password path
  would admit them and the Google path would lock them out. They still receive no `org_id`/`org_role`.

### Not in scope

The visual split between the platform admin panel and the organization admin panel is **roadmap
block 40.20** and is waiting on the owner's design («админка у админов и админов tenancy будет
разная в разном месте... я сам потом пришлю дизайн»). Nothing here builds a new screen; the existing
`app/(admin)` panel simply admits `Admin` alongside `SuperAdmin` and hides the add/remove-user
affordances from `Admin`.

### The data half: «они должны показывать все»

The policies above only decide who may call a route. Passing them changed nothing about what came
back: every read was still filtered to an organization platform staff usually do not belong to, so
the platform admin screens would have rendered an empty page. The second half of the change widens
the reads.

**A third tenant mode rather than reuse of the one that existed.** `ITenantContext` gains
`IsPlatformWide` alongside `IsSystem`. The tempting shortcut — "platform staff are just system mode
with a face" — is wrong in a way that would not show up until it mattered: system mode exists
because there is *nobody* to attribute the work to and relies on a `BYPASSRLS` role, while
platform-wide mode exists because there *is* somebody and they are entitled. Collapsing them would
let any background job inherit a human's privileges, or let a request inherit a job's connection
role. The two are mutually exclusive and entering the second from the first throws.

**The claim is the only door.** `TenantContextMiddleware` enters platform mode from the `role` claim
of the principal the service itself authenticated — never a header, body, query or route value. This
is the same rule tenancy has had since 40.2 ("the organization is never read from the request"),
applied to the privilege as well as to the tenant. Tests pin that a forged `X-User-Role`, an
invented `X-Platform-Mode` header, and an unauthenticated principal carrying the role claim all
fail to open it. Impersonation tokens carry `role: User` by design (40.9), so borrowing an
organization confers nothing.

**Reads widen in three places, writes in none.** Query filters gain the branch, RLS policies gain
`app.platform_mode` in `USING`, and the two Mongo repositories drop the organization from their
filter because Mongo has no policy to carry it. `WITH CHECK` deliberately does **not** get the
branch and `TenantSaveChangesInterceptor` still demands an explicit organization on insert. A
platform administrator sees every customer and can still only write into one they named. If that
makes some future platform write path fail, the correct fix is to name the organization, not to
loosen the policy.

**Platform mode coexists with an organization** instead of replacing it — an administrator who also
belongs to a tenant reads across all of them and writes into theirs — which is why the connection
interceptor emits both `SET LOCAL` statements rather than choosing between them.

**Why a GUC and not a second `BYPASSRLS` role.** A role-based bypass would need a second connection
string per service, a second pool, and a decision at connection time about which one a request gets
— privilege escalation would then be a connection-selection bug, invisible in the policy. With
`app.platform_mode` the policy itself states who may read what, in one place, reviewable in
`\d+ tablename` on any environment. It also keeps the "one application role, no `BYPASSRLS`"
property that 40.4 established. The GUC is set only by `TenantConnectionInterceptor`, only from
`IsPlatformWide`, only via `SET LOCAL`, so it cannot survive its transaction on a pooled connection.

**Redis is deliberately excluded.** Notification inboxes and analytics presence are namespaced by
key prefix, so a cross-organization read means scanning every prefix. No platform screen asks for
one today, and building the scan now would add an unbounded `KEYS`-shaped operation to serve a
feature nobody requested. Platform staff therefore see per-organization Redis state only when acting
inside one organization. Written here so that nobody later reads "platform staff see everything" and
assumes live presence across customers is included — it is not.

**One code path for the policy SQL.** `EnableTenantRls` now emits `DROP POLICY IF EXISTS` before
`CREATE POLICY`, which makes it re-appliable; the seven `RefreshTenantPoliciesForPlatformStaff`
migrations therefore call the helper again instead of carrying a copy of the policy text that would
drift the next time the helper changes. Their `Down` passes `admitPlatformStaff: false` through that
same helper, so the rollback regenerates the exact pre-change policy rather than an approximation
written from memory.

---

## 2026-08-16 — the remaining services get `organization_id` (40.13, Stage C closes)

- **`DiscussTags` is social-db's one content-flavour table, and the stamping is explicit rather than
  automatic.** A tag is a word, not somebody's content: `OrganizationId` is nullable, `NULL` is the
  curated vocabulary every organization shares, and the filter is `== null || == current` — plain
  equality would open Discuss to a new customer with no tags at all, the same trap learning's
  `Skill.IconicName` and ai's seeded dialog modes named in 40.10/40.11. The stamping cannot be
  automatic the way `ITenantScoped` writes are: the write guard (`TenantSaveChangesInterceptor`)
  only recognizes `ITenantScoped`, whose `OrganizationId` is a non-nullable `Guid`, so `DiscussTag`
  cannot implement it without losing the nullable column that makes the content flavour work at all.
  `ResolveOrCreateTagsAsync` stamps a user-typed tag by hand; the SuperAdmin curated-tag endpoint
  leaves the column unset on purpose. `UNIQUE(Slug)` becomes `UNIQUE(OrganizationId, Slug)` plus a
  partial unique index over the global rows (Postgres treats `NULL`s in a composite unique index as
  distinct, so the composite alone would allow the curated tag "objections" to exist twice at the
  global level) — the same pair learning-service needed for `Skill.IconicName`.
  - *Alternative rejected — a separate `CuratedTags` table, kept entirely apart from per-organization
    tags.* Would avoid the nullable column and the explicit-stamping asymmetry, but every read path
    that resolves a thread's tags (list, search, autocomplete) would need to union two tables instead
    of filtering one, and the frontend tag picker gains no benefit from the split — it already treats
    curated and custom tags identically. Not worth doubling the read surface to avoid one nullable
    column.

- **`UserReplicas` stays platform-global in both gamification-service and social-service, the same
  call learning (40.10) and ai (40.11) made.** It projects identity-service's cross-organization user
  directory (TENANCY.md §4.2): a user's `DisplayName`/`Email`/`AvatarKey` are not organization-scoped
  facts, they are the same three fields regardless of which organization is asking, and giving the
  table an `OrganizationId` would just duplicate one row per organization a person belongs to for no
  isolation gain — Identity is the source of truth for who somebody is, not for who employs them.
  **What this leaves open, stated plainly:** social-service's `FriendService.SearchUsersAsync` still
  searches `UserReplicas` platform-wide, so it can surface a person from another organization by name
  or email in a friend-search result. That is not a new leak 40.13 introduced — the same platform-wide
  search existed before this block — and the boundary that actually matters is enforced one step
  later: a friend request toward that person is refused by the `Friendships` RLS policy the moment
  either party tries to accept it, so the two people can never actually become friends or open a chat
  across the organization boundary. Narrowing the search itself to organization-mates only was
  considered and deliberately left to a later block, because it needs a join through Identity's
  membership table that no 40.13 service currently has a read path for; see `docs/DONT_FORGET.md`.

- **Redis-only services get key prefixing, not separate Redis databases, for notification-service and
  analytics-service.** Both services have no relational database and therefore no RLS to fall back
  on — the Redis key name *is* the tenant boundary, the same shape 40.11 used for ai-service's
  verdict cache and voice-quota counters. Every organization's data gets `org:{orgId}:` prepended to
  its existing key (`notifications:inbox:{userId}` → `org:{orgId}:notifications:inbox:{userId}`,
  `presence:online` → `org:{orgId}:presence:online`); the key builders raise on an empty organization
  rather than building `org:00000000-...:...`, which would be one shared bucket collecting every
  caller whose context was missing — worse than the un-prefixed key it replaced, because it would
  *look* correctly namespaced.
  - *Alternative rejected — a separate logical Redis database (`SELECT n`) per organization.* Redis
    databases are a fixed, small, per-connection-pool resource (16 by default), not a per-tenant
    primitive, and StackExchange.Redis multiplexers are shared singletons in both services — routing
    per request would mean either one connection per organization (defeating the point of a shared
    multiplexer) or a runtime `SELECT` race between requests sharing a connection.
  - *Alternative rejected — a separate Redis instance per organization.* The operationally correct
    shape at very large scale, and explicitly out of scope for this block: it would need per-tenant
    infrastructure provisioning, which Phase 40 has not built for any store yet (Postgres and Mongo
    both stay single-instance, RLS/application-filter-scoped).
  - Old un-prefixed keys are not migrated or flushed. notification-service's inbox/counter keys carry
    a TTL and expire on their own; analytics-service's `presence:online` is a TTL-less sorted set and
    needs one manual `DEL` — recorded in `docs/DONT_FORGET.md` because it is the one key in this block
    that will not disappear by itself.

- **Four unique-index swaps live inside the `AddOrganizationId` migration for gamification-service,
  and two do for social-service — not the concurrent-rebuild script 40.10–40.12 used for every
  index.** The pattern through 40.12 was "no `CREATE INDEX` in the migration at all," because
  `Database.Migrate()` runs on the startup path and a long index build there stalls readiness. That
  reasoning does not apply to a small, fixed set of constraints on tables that hold at most a row per
  user (or a handful of rows per week): `Leagues.(WeekStartDate,Tier)`, `UserStreaks.(UserId)`,
  `UserAchievements.(UserId,AchievementId)`, `UserLearningProgress`'s primary key,
  `DiscussTags.(Slug)`, and `Friendships.(RequesterId,AddresseeId)` (+ its canonical-pair index) were
  all **correctness-load-bearing in the deploy-to-script window**, not performance work: memberships
  (40.6) let one person belong to two customers, and every one of these old, platform-wide constraints
  would have refused that person's second organization a row — a league, a streak, an achievement, a
  friendship, or (for `DiscussTags`) an entire second organization's ability to create the tag
  somebody is typing. Leaving them for the concurrent-rebuild script would have meant the service was
  broken for the second organization from the moment of deploy until a human ran a separate script.
  The read indexes on the two tables that actually grow without bound in each service —
  `UserXpRecords`/`LeagueMemberships` in gamification-db, everything else in social-db — stay in the
  concurrent-rebuild scripts, unchanged from the 40.10–40.12 pattern.

- **Chat isolation is application-side in social-service, because Mongo has no row-level security.**
  `chat_conversations` gets an `organizationId` field, but there is no database-side policy that can
  enforce it the way Postgres's RLS does for the other six tables — the entire boundary is
  `ChatConversationRepository`, the only class permitted to call `GetCollection<ChatConversation>`.
  `MongoDbContext` was cut down to expose the database handle alone (it used to expose
  `ChatConversations` as a property), and a unit test walks the source tree and fails the build if a
  second file names `GetCollection<ChatConversation>` — the same structural move ai-service made for
  `dialog_sessions` in 40.11. Every repository method takes the tenant from `ITenantContext` and
  raises on an unset one; there is no system-mode bypass, because there is no legitimate
  platform-wide read of somebody's private messages.
  - *Alternative rejected — a `organizationId` filter added ad hoc at each of `ChatService`'s five
    call sites, with no repository.* Cheaper to write, but it is exactly the shape that lets a future
    call site forget the filter with nothing else in the codebase able to catch it — Mongo will not
    reject an unfiltered query the way Postgres rejects a write with no `SET LOCAL` under `FORCE ROW
    LEVEL SECURITY`. Centralizing behind one interface turns "did every caller remember" into "does
    the interface exist and is it the only path," which is checkable by a test that does not need to
    enumerate call sites.
  - The structural half of the boundary — friendship and chat cannot cross the organization — comes
    from `ChatService.GetOrCreateConversationAsync` refusing to open a conversation between people who
    are not `Accepted` friends, combined with `Friendships` being RLS-protected tenant data: a
    friendship that cannot cross the boundary is also a conversation that cannot.

- **`LeagueSettings` becomes tenant data with `UNIQUE(OrganizationId)`, not configuration, and the
  startup seeder stops creating it.** Every other singleton settings row in gamification-db
  (`GamificationSettings`) is genuinely installation-wide and stayed platform-global. `LeagueSettings`
  looked like the same shape — one row, admin-edited — but `CurrentPeriodStartDate`/
  `CurrentPeriodEndsAt` are the state of a *running competition*, not a knob: shared, the first
  organization to roll over advanced the period for everybody, and every other organization's weekly
  rollover then found the period already advanced, bailed out, and left its leagues open forever; one
  customer's admin pressing "close the league now" did that to every other customer too. Startup has
  no tenant, so the seeder cannot create a correctly-scoped row for every future organization up
  front — it stopped creating this row at all, and `LeagueService.GetSettingsAsync` returns a correct
  unsaved default until an organization's own admin first saves league settings, which is when the row
  is actually created.

---

## 2026-08-15 — learning-service gets `organization_id` (40.10, first Stage-C service)

- **Content is nullable-owner, tenant data is not, and they use different RLS helpers.** Progress
  (`UserSkillProgressRecords`, `UserLessonProgressRecords`, `UserExerciseAttempts`,
  `UserTechniqueProgress`) is `ITenantScoped` with a `NOT NULL` owner and `EnableTenantRls`. Content
  (`Skills`, `Topics`, `Lessons`, `Exercises`, `Techniques`, `ReferenceMaterials`) gets a nullable
  column where `NULL` means "global library, shared by everyone" and `EnableTenantRlsForContent`, so
  the policy is `IS NULL OR = current`. Content therefore cannot implement `ITenantScoped`, whose
  `OrganizationId` is a non-nullable `Guid` — that is a consequence of the design, not an oversight.
- **Every entity declares its own query filter, and a test enforces it by walking the model.** EF
  does not inherit filters through navigations and every read path here composes
  `Skill → Topic → Lesson → Exercise`. Listing the entities again in a test would repeat whichever
  omission the test is supposed to catch, so `Every_entity_with_an_organization_id_has_its_own_query_filter`
  asks the model instead: anything with an `OrganizationId` and no filter fails the build.
  `OutboxMessage` is the single asserted exception (platform-global, read only by the relay).
- **Unique content slugs need two indexes, not one.** `UNIQUE (OrganizationId, IconicName)` is what
  lets a second customer have its own `objections` skill, but Postgres treats NULLs in a composite
  unique index as *distinct*, so that index alone would permit two global `objections` skills — a
  silent weakening of a constraint that holds today. The fix is a second, partial unique index over
  `IconicName WHERE "OrganizationId" IS NULL`. Applied to `Skill.IconicName`, `Topic.IconicName` and
  `Technique.Slug`; the roadmap only named the first, but all three are the same defect.
- **The EF migration contains no `CREATE INDEX` and no backfill.** learning-service runs
  `Database.Migrate()` during startup, so anything slow in a migration stalls the readiness probe
  and races the replicas — which is exactly what the roadmap means by "long index rebuilds are a
  separate operational step, not `DatabaseBootstrapper`". Indexes move to
  `docs/TENANCY/sql/40.10_learning_organization_indexes_concurrently.sql` (`CREATE INDEX
  CONCURRENTLY`, `pg_index.indisvalid` checked *before* anything is dropped, old index dropped only
  after its replacement is valid), and the backfill to
  `40.10_learning_organization_backfill.sql`, because learning-db has no tenant registry to look the
  default organization up in. **Consequence accepted and documented:** the EF model snapshot declares
  indexes that no migration creates, so a fresh database has the columns and the RLS but not the
  indexes until the operational script runs. That is the one place in this service where the
  snapshot deliberately runs ahead of the migrations.
  - *Alternative rejected — `migrationBuilder.Sql(..., suppressTransaction: true)` with
    `CREATE INDEX CONCURRENTLY IF NOT EXISTS` inside the migration.* It is the shape the roadmap's
    checklist literally describes, and it would keep the snapshot honest, but it puts the long build
    back on the startup path, which is the thing the same roadmap line forbids. The two halves of
    that bullet contradict each other for a service that migrates at startup; the "operational step"
    half wins because it is the one about production behaviour.
- **The backfill refuses to run as a role that cannot bypass RLS.** The migration enables `FORCE ROW
  LEVEL SECURITY` before the backfill runs, so a connection without `BYPASSRLS` cannot see the
  placeholder rows: the `UPDATE`s would touch zero rows and the assertions would then "pass" because
  they cannot see the rows either. A silent no-op that reports success is worse than a hard failure,
  so the script checks `pg_roles.rolsuper OR rolbypassrls` up front and aborts.
- **Placeholder organization instead of a nullable-then-tightened column.** Adding `NOT NULL` to a
  populated table needs a default; existing rows get the all-zeros guid and the column default is
  dropped immediately afterwards, so a later insert that forgets the organization fails loudly rather
  than landing in a phantom tenant. Between the migration and the backfill those rows are invisible
  (fail-closed, TENANCY.md §1.5) — correct, but user-visible as "my progress vanished", so the two
  steps belong in one maintenance window. Written up in Russian in `docs/DONT_FORGET.md`.
- **One transaction pattern for the whole service: `TenantTransactionScope`.** `SET LOCAL` needs a
  transaction, EF only opens one for `SaveChanges`, and a bare `SELECT` under RLS silently returns
  nothing. Rather than sprinkling `BeginTransactionAsync` per call site, learning-service has one
  re-entrant helper with two entry points — `BeginReadAsync` (rolls back on dispose; it exists to
  make rows visible, not to persist) and `BeginWriteAsync` + explicit `CommitAsync`.
  - *Alternative rejected — a global MVC action filter opening a transaction for the whole request.*
    It would need no call-site changes at all, but `SubmitExerciseAnswerAsync` calls the AI evaluator
    mid-request and `/exercises/{id}/voice/stream` writes TTS audio to the response body from inside
    the action. A request-wide transaction would hold a Postgres connection open across both — idle
    in transaction for the length of an LLM call or an entire audio stream. Placing the scopes by
    hand costs ~20 one-line edits and keeps the transaction around the database work only.
  - *Alternative rejected — `Database.UseTransaction` for the life of the connection.* Already
    rejected in the 40.4 entry below for making `SaveChanges` externally owned; nothing changed.
  - The helper no-ops when `Database.IsRelational()` is false, so the in-memory unit tests are
    unaffected.
- **Content authoring stays superadmin-only, so no content write-stamping is needed yet.** Every
  content endpoint in learning-service is `RequireSuperAdministrator`, so content is authored only by
  platform staff and `OrganizationId` stays `NULL` — the seeder produces a global library without any
  special system mode. Note the gap this leaves for later: the content RLS policy's `WITH CHECK`
  admits `NULL`, so it would **not** stop an org-scoped writer from creating global content. When
  40.18 adds organization-authored content, the write side needs an application-level guard
  (a content equivalent of `TenantSaveChangesInterceptor`); RLS alone will not cover it.
- **`DisableTenantRls` added to BuildingBlocks.** identity's first RLS table (40.7) was created by the
  same migration that enabled RLS, so `Down` could just drop the table. A service adding
  `OrganizationId` to tables that already exist needs to hand them back unprotected, so the rollback
  helper now exists alongside the two enable helpers.
- **Admin controllers are the one documented place with no tenant scope.** They read and write
  content directly, and the content policy admits global rows even with the session variable unset,
  so they work as-is for the whole of 40.10. Recorded as a known gap rather than papered over,
  because 40.18 must revisit it.

---

## 2026-08-15 — Platform superadmin, impersonation, and the live-data migration (40.9)

- **Which service hosts what.** Organization CRUD stays in organization-service and impersonation
  goes to identity-service, split along the line of *which database the operation needs*.
  organization-service owns the tenant registry, so create / list / suspend / reactivate belong
  there and nowhere else. Impersonation mints a JWT, and only identity-service holds the signing
  key and the membership table the claims are built from. The one genuinely ambiguous case is
  inviting the first `OrgAdmin`: the invite row lives in identity-db, so the endpoint lives in
  identity-service (`POST /admin/platform/organizations/bootstrap-admin`) even though the action
  reads like organization administration. *Alternative rejected:* organization-service calling
  identity-service over HTTP to create the invite — that would put a synchronous cross-service
  call on a path that already has a database it can write to, and would need a second trust
  mechanism between the two services.

- **The superadmin panel does not get its own invite path.** `bootstrap-admin` opens a DI scope,
  points that scope's `TenantContext` at the target organization and calls the ordinary Phase 40.7
  `IInviteService` — the same code, the same tenant guards, the same email. This is the
  "open a scope per unit of work" shape [TENANCY.md §1.6](TENANCY/TENANCY.md) prescribes for
  background jobs. *Alternative rejected:* writing an `Invite` row directly from the platform
  service. It is four lines shorter and immediately becomes a second definition of what a valid
  invite is, which then drifts.

- **The endpoint refuses an organization that already has an admin.** Without that check,
  `bootstrap-admin` is a permanent platform-side back door into any running customer's
  organization. With it, it can only do the one thing it exists for. The check covers both an
  active `OrgAdmin` membership and a pending, unexpired `OrgAdmin` invite.

- **The impersonation token is deliberately weaker than the token that asked for it.** It carries
  `role: User` — not `SuperAdmin` — plus `org_id` and `org_role: OrgAdmin` for the target
  organization, the marker claims `imp` / `imp_id` / `imp_actor`, a short lifetime
  (`Impersonation:TokenLifetimeMinutes`, default 15) and **no refresh token**. Dropping the
  platform role is what stops an impersonation session reaching `RequireSuperAdmin` routes,
  including the impersonation route itself; the explicit `IsAlreadyImpersonating` check in the
  service is belt-and-braces for the day someone edits that policy. `sub` stays the superadmin's
  own user id: the impersonator borrows an organization, never an identity, so anything the token
  writes is attributable to a real person. *Alternative rejected:* reusing
  `AuthenticationService`'s token path with a flag — the differences here *are* the security
  properties, and hiding them behind a boolean makes them easy to lose.

- **The audit row is written before the token is returned**, in the same database, so a token that
  exists always has a record behind it. It records actor id and email, organization id and name
  (copied, so the row still reads correctly after a rename), a mandatory free-text reason, and the
  issue/expiry times. A crossing nobody can justify afterwards is exactly the one nobody can
  review. *Alternative rejected:* a log line — Loki retention is not an audit policy.

- **Suspension is enforced at token issuance, not at the gateway.** The check sits in
  `AuthenticationService.IssueTokensForUserAsync`, the single point password login, Google
  sign-in, invite acceptance and refresh all converge on. Enforcing it per-controller would leave
  whichever route is added next unguarded; enforcing it only at login would let an already-issued
  refresh token keep working for its full 30 days. Consequence to accept: a suspended
  organization's users keep their current access token until it expires (≤15 minutes), because
  JWTs are not revocable without a session store.

- **identity-service gets an `OrganizationReplica` table rather than calling organization-service.**
  DB-per-service means identity cannot join the registry, and asking over HTTP would put a second
  service on the authentication hot path and make identity unable to sign anyone in whenever
  organization-service is down. The replica is fed by the `organization.*` topics that already
  existed, unused, since 40.5 — the same `UserReplica` pattern
  ([TENANCY.md §1.1](TENANCY/TENANCY.md)). **A missing replica row reads as active, never as
  suspended:** the projection is eventually consistent, and a consumer that is briefly behind must
  not lock a paying customer out of their own product. Suspension is a deliberate recorded act; its
  absence is what "no row" means. *Accepted cost:* an organization created seconds ago may not be
  in the replica yet, so `bootstrap-admin` and `impersonation` answer `404` with a message saying
  exactly that, and the operator retries.

- **`OrganizationReplicas` and `ImpersonationAuditEntries` are outside RLS.** The first is read
  while deciding whether a token may be issued at all — before there is a tenant context to filter
  by, the same reason `OrganizationAuthConfigurations` skipped RLS in 40.8. The second exists to
  record crossings *between* tenants and is read by platform staff, not by the organization named
  in it. A table whose main access path would have to bypass RLS on every call should not pretend
  to have it.

- **`scripts/tenancy-boundary-lint.py` gained a two-file allow-list instead of being worked
  around.** [TENANCY.md §1.3](TENANCY/TENANCY.md) states the rule and its single exception in the
  same breath: the organization is never read from body/query/route, *except* through an explicit
  superadmin impersonation endpoint. The two request DTOs that name an organization are listed by
  exact path, with a stale-entry check so an exception cannot outlive the code it was granted for.
  *Alternative rejected:* naming the files so the regex misses them — that is the same exception,
  minus the review. Response DTOs use a nested `OrganizationReferenceDto(Id, Name)` — mirroring
  organization-service's own `OrganizationSummaryDto` — so the outbound case needs no exception at
  all.

- **The live-data migration is SQL files plus a bash driver, not a dotnet tool.** It follows the
  shape the repo already has for one-shot data moves (`scripts/migrate-monolith-to-services.sh`):
  dry-run by default, connection resolved from `.env`, destructive SQL printed before it runs. A
  DBA can read the four files in `docs/TENANCY/sql/` without a .NET toolchain, which matters
  because the person running them at 2am is not necessarily the person who wrote them. It is
  explicitly **not** an EF migration: EF migrations are schema, run automatically on service start,
  and must never carry a one-shot data backfill that has to be rehearsed on a copy of production
  first.

- **The rollback is verifiable because the forward run leaves evidence.** `tenancy_backfill_40_9`
  records which organization was created and when; `tenancy_backfill_40_9_demoted_users` records
  the platform role of every account whose removed global `Admin` value was cleared. The rollback
  deletes only memberships created at or before that timestamp and **refuses outright** if anyone
  has joined since — deleting a real post-migration membership is worse than a failed rollback. It
  never deletes a user: offboarding is deactivation ([TENANCY.md §4.3](TENANCY/TENANCY.md)) and
  that does not stop applying because a migration went wrong.
  `scripts/tenancy-default-organization-verify.sh` proves all of it against two throwaway
  databases built from the services' own EF migrations, and drops them again.

- **The migration is honest about how little there is to backfill.** `Invites` — the only
  `ITenantScoped` table anywhere today — has had `OrganizationId NOT NULL` since it was created in
  40.7, so there is nothing to fill in; the script asserts that rather than assuming it.
  `OutboxMessages.OrganizationId` is left null where it is null, because a platform-global event
  legitimately has no tenant and stamping one on retroactively would be inventing history. Every
  other service database has no organization column yet — that is Stage C (40.10+), and the file
  ends with the extension template for it instead of pretending to cover it.

- **Role mapping in the backfill.** A user holding the removed global `Admin` value (1) or the
  platform `SuperAdmin` role (2) becomes `OrgAdmin` of the default organization; everyone else
  becomes `Manager`. The platform role on `Users` is a separate axis and is not touched by the
  mapping — a `SuperAdmin` stays a `SuperAdmin` and additionally gains a membership, because
  without one they cannot use the ordinary product at all.

---

## 2026-08-15 — The SSO seam (40.8): why a three-step login exists before there is a second provider

- **Why build a seam for something explicitly not being built.** SSO is deferred
  ([TENANCY.md §4.5](TENANCY/TENANCY.md)), but the *shape* of login is not deferrable. The
  request, when it comes, is not "add SSO" — it is a 200-seat customer requiring Azure AD, with
  their deadline and the deal as leverage. Retrofitting that into a single hardcoded
  email+password branch means rewriting login, session issuance, the invite mechanic and user
  provisioning simultaneously, under exactly the conditions where you least want to touch
  authentication. Building the seam first is roughly a day: one table, one interface with one
  implementation, and a login screen with one extra step. Doing it later is weeks of that rewrite.
  The cheap half of the work is done now precisely because it is cheap now.
- **What was deliberately *not* built:** no OIDC provider, no SAML provider, no
  `jit_provisioning` behaviour (the column is stored and never read), no per-organization session
  TTL applied to token issuance, no admin UI to edit the configuration (that is 40.20's split of
  the admin panel), and no endpoint that writes an `OrganizationAuthConfigurations` row —
  organizations get one when 40.9's superadmin panel creates them. An organization with no row
  logs in with a password, the same as the platform default.
- **Which service owns `organization_auth_config` — identity-service, not organization-service.**
  The deciding constraint is *when* the row is read: on `POST /auth/login/start`, before
  authentication, with no JWT, no `X-Organization-Id` and no tenant context. Putting the table in
  organization-service would mean identity-service making an unauthenticated cross-service HTTP
  call on the single most availability-sensitive path in the product — nobody can log in if
  organization-service is down — and would force organization-service to expose an anonymous
  "which organization owns this email domain" endpoint, which is a far better enumeration oracle
  than the thing this design is trying to avoid. There is also precedent: `Membership` (40.6) and
  `Invite` (40.7) both live in identity-service despite being about organizations. The boundary
  that holds is **identity-service owns access, organization-service owns the registry and the
  business/content profile** — how you get in is access.
- **The table is deliberately not `ITenantScoped` and has no RLS policy** — unlike `Invites`,
  which 40.7 put behind `EnableTenantRls`. Its primary read is a cross-tenant question by nature
  ("which organization claims this domain") asked at a moment when no tenant context can exist. A
  table whose main access path has to bypass RLS on every single login is not protected by RLS,
  it is decorated with it — and worse, `TenantConnectionInterceptor`'s system mode relies on a
  `BYPASSRLS` role that does not exist yet on real servers, so the "correct" version would work
  locally and silently stop resolving organizations the day `sellevate_app` is rolled out. This
  is the same reasoning that kept the `Organizations` registry in 40.5 out of RLS. Consequence to
  respect when a write path is added: the organization must be taken from `ITenantContext`
  explicitly in the query, because neither the query filter nor the database will do it.
- **`POST /auth/login/start` answers `200 {"method":"password"}` for every syntactically valid
  address, known or not.** It never returns the organization id or name, and there is no "this
  address is known" flag. This continues 40.7's choice for Google sign-in (one identical `401`
  for "no such account" and "account without a membership"): the first step of a login screen is
  the most reachable endpoint in the product, and any variation in its answer turns it into a
  free customer-list oracle.
  - **The one leak we accept and name:** an organization that configures OIDC/SAML *will* get a
    different answer for its domain, because the browser has to be sent somewhere. That is
    inherent to SSO, not to this design, and it is opt-in by the customer who configured it. It
    also reveals only "this domain uses SSO with us", never which individual addresses exist.
  - **Rejected:** returning `404` for an unknown address (a perfect enumeration oracle), and
    returning the organization name so the screen could greet the user by company (the same
    oracle with better branding).
- **A method with no provider is refused, not downgraded to a password.** If an organization is
  configured for `oidc`, `POST /auth/login` returns `401` even for the correct password. The
  alternative — falling back to the password provider when no provider matches — would mean that
  switching a customer to their directory silently leaves password login working alongside it,
  which is the opposite of what the customer bought. This is the only behaviour the seam actually
  changes today, and it is the one worth testing.
- **`method` is stored as `text` with a `CHECK` constraint, not as an int enum.** The rest of the
  codebase converts enums with `HasConversion<int>()`, but this value is also the wire value in
  `LoginStartResponseDto` and the key `IAuthProvider.Method` is matched on, and keeping one
  spelling across the database, the interface and the JSON removes a mapping layer that exists
  only to be gotten wrong. The `CHECK ("Method" IN ('password','oidc','saml'))` is a stricter
  guarantee than an int enum, which happily stores `47`.
- **Noted, not fixed: `scripts/codestyle-lint.py` and the identity-service codebase disagree about
  comments.** CODESTYLE §9 forbids comments outright; the linter implements that literally,
  flagging `///` XML documentation too. identity-service was already at **527** violations before
  40.8 (40.7's own merged `Features/Invites/` accounts for 101 of them), because the house style
  in this service is to record *why* a security decision was made next to the code that makes it —
  which is exactly what a reviewer of an authentication seam needs. 40.8 follows that style and
  leaves the count at 585. This is a real contradiction between the written rule and the practice
  every recent block has followed, and resolving it (relax the rule to allow `///`, or strip the
  documentation) is a repo-wide call, not a thing to settle inside one sub-phase. The
  `codestyle` CI workflow is consequently red for identity-service and was already red on arrival.

---

## 2026-08-15 — organization-service scaffold (40.5): registry vs profile tenancy scope, deferred authorization

- **Which table is tenant-scoped, and why the answer isn't "both":** `Organizations` (the tenant
  registry, one row per customer) does **not** implement `ITenantScoped` and never gets
  `EnableTenantRls`. `OrganizationProfiles` (product/ICP/objections/script/tone/glossary/banned
  claims, [CONTENT_MODEL.md §3](TENANCY/CONTENT_MODEL.md)) does both. The registry can't be
  tenant-scoped by its own `organization_id` — addressing "which organizations exist" is
  inherently a cross-tenant, platform-level query, the same reason `40.5`'s registry endpoints are
  not `[TenantScoped]`. The profile is exactly the shape RLS exists for: one row that belongs to
  exactly one organization, reachable only through `ITenantContext`.
- **The registry's `{id:guid}` route parameter does not violate the "organization id never in the
  route" boundary rule.** `scripts/tenancy-boundary-lint.py` forbids a route/query/body parameter
  literally named `organizationId` (the value that would answer "which tenant is making this
  request"). `OrganizationController`'s `{id:guid}` answers a different question — "which registry
  row should the platform operator act on" — the same category of thing `CompanyController`'s
  `{companyId:guid}` already does for a different resource. Naming the parameter `id` rather than
  `organizationId` keeps this distinction visible to the linter and to a future reader.
- **`OrganizationController` has no role restriction yet beyond `[Authorize]`.** Any authenticated
  user can create/list/update/suspend/reactivate an organization today. This is deliberately
  deferred, not an oversight: `RequireSuperAdmin` in its post-40.6 shape (platform role, split from
  the current global `Admin`) does not exist until 40.6/40.9. Locking this controller down is
  explicit 40.9 scope ("платформенная суперадминка"). Recorded here so it is not mistaken for a
  security gap in 40.5's own review — it is a known, roadmapped gap.
- **Reactivation publishes `organization.updated`, not a fourth topic.** The roadmap names exactly
  three: `organization.created` / `organization.updated` / `organization.suspended`. Treating
  "status flipped back to Active" as a case of "the registry row changed" rather than minting
  `organization.reactivated` keeps the contract at the size the roadmap specified; a consumer that
  cares about the transition reads `status` off the `organization.updated` payload.
- **RLS on `OrganizationProfiles` is wired but not yet the layer doing the isolating locally.** The
  service connects with the same Postgres superuser every other service uses
  (`ConnectionStrings:Postgres`), not the restricted `sellevate_app` role — that role's real-server
  rollout is still pending a human (`docs/DONT_FORGET.md`), and superusers bypass RLS regardless of
  `FORCE`. Locally, the EF query filter and `TenantSaveChangesInterceptor` are what actually
  prevent cross-tenant reads/writes today; the migration-level `EnableTenantRls` call is correct
  and ready for when a service starts connecting as `sellevate_app` (Stage C territory, 40.10+,
  though nothing in the roadmap currently schedules organization-service itself for that switch).
- **`jsonb` columns stored as plain `string` properties**, matching the existing
  `Exercise.SerializedContent` convention (`learning-service`) rather than typed EF-owned JSON
  columns — the codebase already has this pattern in exactly the place a new service should copy
  it from, and it keeps `OrganizationDbContext` free of a JSON-column-mapping abstraction that
  nothing else in the service needs.

## 2026-08-15 — RLS infrastructure: `SET LOCAL` vs `SET`, and the read-transaction requirement (40.4)

- **Problem the roadmap names directly:** `SET LOCAL` is transaction-scoped — safe against
  connection-pool leaks by construction, since Postgres reverts it when the transaction ends
  regardless of what happens to the pooled connection afterward. A bare `SET` is session-scoped and
  leaks the previous request's tenant onto the next one that borrows the same pooled physical
  connection — TENANCY.md §1.5 calls this the single highest-risk detail of the whole design. But
  `SET LOCAL` only has an effect *inside* an active transaction, and EF Core read paths (plain LINQ
  queries) run with no implicit transaction, so the two halves of the requirement pull against each
  other.
- **Decision:** never use a bare `SET`. `TenantConnectionInterceptor`
  (`BuildingBlocks/Tenancy/TenantConnectionInterceptor.cs`) hooks EF Core's
  `IDbTransactionInterceptor.TransactionStarted` / `TransactionStartedAsync` — which fires for
  **every** transaction, not only ones the interceptor itself opens — and issues
  `SET LOCAL app.organization_id = '<guid>'` as the first statement inside it. EF Core already
  wraps every `SaveChangesAsync` call in an implicit transaction by default, so every **write**
  is covered automatically, at zero extra plumbing. Tenant-scoped **reads** have no implicit
  transaction and must open one explicitly
  (`await using var transaction = await context.Database.BeginTransactionAsync();`) to get the
  same protection — this is a requirement carried forward to whichever service rolls tenant-scoped
  reads out (Stage C, 40.10+), not something this building block can retrofit onto a bare `SELECT`
  from the outside.
- **Alternative rejected — set the GUC on connection open with a bare `SET`, reset it on
  `ConnectionClosing`.** This is the shape TENANCY.md §1.5 gestures at as the pragmatic fallback
  ("sets the GUC on connection open"). Rejected because the reset only fires on the *ordinary* close
  path; any code path that disposes a connection without going through EF's close event (a crashed
  request, a connection returned to the pool by a lower-level ADO.NET failure) leaves the previous
  tenant's value live for the next borrower. `SET LOCAL` cannot leak this way even in a crash,
  because Postgres — not application code — is what undoes it, at the transaction boundary.
- **Alternative rejected — keep a transaction open for the life of every connection
  (`Database.UseTransaction`, committed at end of request).** This would make reads "just work"
  without call-site changes, but it means every `DbContext` write becomes an *externally owned*
  transaction that EF no longer auto-commits on `SaveChanges` — a correctness footgun that would
  silently stop persisting data the first time this interceptor is wired into a service that isn't
  expecting it. Not worth the risk for a building block nothing consumes yet.
- **Verified against a real (local, throwaway) Postgres instance — and it changed the SQL:** the
  first version of `TenantRlsMigrationBuilderExtensions` compared
  `organization_id = current_setting('app.organization_id', true)::uuid`, following TENANCY.md's
  literal text ("missing_ok... yields zero rows"). The integration test
  (`TenantRowLevelSecurityIntegrationTests`) caught that this is insufficient: once a *pooled
  physical connection* has run `SET LOCAL app.organization_id = ...` even once, Postgres reverts
  the GUC to `''` (empty string) — not `NULL` — when that transaction ends, because the first
  `SET LOCAL` on an unrecognized custom GUC registers a placeholder whose "previous" value is `''`,
  not "undefined." `''::uuid` then raises a hard Postgres error (`22P02`) on every subsequent query
  against an RLS-protected table on that connection, instead of filtering to zero rows — the
  opposite of fail-closed. Fixed by wrapping the setting in `NULLIF(..., '')` before the cast:
  `NULLIF(current_setting('app.organization_id', true), '')::uuid`. Since Npgsql (and every
  ADO.NET provider) pools physical connections, essentially *every* long-lived pooled connection in
  a running service will eventually hit this once it has served one tenant request — this is not an
  edge case, it is the steady-state behavior, and `NULLIF` is required, not optional.
- **Column naming departs from TENANCY.md's SQL prose on purpose.** TENANCY.md's examples write
  `organization_id` (snake_case). The actual schema convention already in this codebase (see
  `docs/DB_SCHEMA.md`, e.g. `Users.Email`, `Companies.Status`) is EF's default Npgsql naming: the
  C# property name verbatim, quoted, PascalCase. `EnableTenantRls`/`EnableTenantRlsForContent`
  default the column to `"OrganizationId"` to match what `ITenantScoped.OrganizationId` actually
  generates, with an override parameter for the rare table that needs something else. The Postgres
  GUC name itself, `app.organization_id`, is unrelated to table-column casing (it is a
  configuration-parameter string, not a SQL identifier) and stays exactly as TENANCY.md specifies.
- **The migration/owning role must itself tolerate `FORCE ROW LEVEL SECURITY`.** Postgres only
  exempts a table's *owner* from `FORCE` automatically when that owner is a superuser; a
  non-superuser owner needs `BYPASSRLS` granted explicitly, or `FORCE` starts filtering migrations
  too, not only the app role. TENANCY.md's "migrations then run as the owner (policies bypassed,
  which is what you want)" is only true given this precondition, which it does not spell out.
  Local dev (`scripts/dev-infra.sh`, role `st`) already satisfies it — `st` is the Postgres image's
  initial superuser, confirmed via `pg_roles.rolsuper` — but a real server's migration role may not
  be. Documented as a load-bearing precondition in
  `docs/TENANCY/sql/create_sellevate_app_role.sql`, and flagged again in `docs/DONT_FORGET.md` so
  whoever runs that script on a real server does not miss it.

---

## 2026-08-14 — `EventEnvelope.OrganizationId` is nullable on the wire, strict at the consumer (40.3)

- **Problem:** the roadmap demands the base consumer **fail** when an event carries no tenant, but
  at the time the envelope changes *nothing publishes an organization yet* (producers only learn
  their tenant in 40.6/40.9), and some events — Identity's user lifecycle stream, consumed to keep
  the `UserReplica` read model in sync — are genuinely cross-org and will never carry one. A field
  that is required on the wire would make every existing producer illegal on the day it lands.
- **Decision:** split "nullable on the wire" from "permissive at the consumer".
  `EventEnvelope.OrganizationId` is `Guid?`, but `KafkaConsumerBackgroundService` exposes
  `protected virtual bool RequiresOrganization => true` and **throws** on a missing organization by
  default; the throw travels the ordinary retry/dead-letter path. A platform-global consumer must
  override it to `false`, which puts the message into `ITenantContext.IsSystem`.
- **Why this preserves the actual rule:** TENANCY.md §1.6 says an unset tenant is *an exception,
  never a license*. What matters is that cross-org processing be an explicit, auditable, per-consumer
  opt-in — not that the field be non-nullable in JSON. A required field would have forced a fake
  sentinel organization id onto genuinely global events, which is strictly worse: it looks like a
  tenant, filters like a tenant, and silently scopes nothing.
- **Alternative rejected — bump the envelope twice** (nullable now, required after 40.9). The
  envelope is the frozen cross-service contract; each bump is a coordinated redeploy of every
  producer and consumer. The roadmap explicitly calls for doing this **once, before the first
  tenant-scoped consumer exists**. The strictness that a second bump would buy already exists in
  `RequiresOrganization`, at zero coordination cost.
- **`PartitionKey` stays the user id.** Moving it to `org:user` would reshuffle every existing
  partition assignment and buy no ordering guarantee the user id does not already provide —
  per-user ordering is the property consumers actually depend on.
- **`OutboxMessage.OrganizationId` is informational only.** The relay forwards `Payload` verbatim
  and never filters on the column. This is deliberate: the relay is the one legitimate system-wide
  reader of all outbox rows, which is exactly why the tenant is carried *inside* the serialized
  envelope rather than derived at publish time.

---

## 2026-08-14 — Multi-tenancy: the tenant is `Organization`, and isolation is enforced by Postgres

- **Status:** design recorded, **nothing implemented**. Full design in
  [docs/TENANCY/](TENANCY/TENANCY.md). Verified starting point: zero `tenant` identifiers exist
  anywhere in the codebase.
- **Naming (the decision with the widest blast radius):** the tenant is **`Organization`**, not
  `Company`. `company-service` + `docs/COMPANIES/` already own `Company` as *a prospect a
  salesperson practises calling* — a per-user private CRM. Reusing the name would make `CompanyId`
  mean two different things across services, in JWT claims and Kafka payloads, where a mix-up is a
  data leak rather than a compile error. Russian UI copy may still say «Компания».
- **Isolation:** the proposal's "one database, `tenant_id` everywhere" does not describe this
  system — DB-per-service is already the shape (7 Postgres DBs + Mongo + Redis). So
  `organization_id` is added per database, and the RLS/`SET LOCAL` plumbing lives in
  BuildingBlocks rather than being written seven times. Three layers: (1) the gateway injects
  `X-Organization-Id` from the validated JWT and strips client copies — reusing the existing
  `IdentityHeaders` contract, never reading the org from a query parameter; (2) EF global query
  filters, explicitly labelled convenience, not security; (3) Postgres RLS with `FORCE`, `WITH
  CHECK`, and an app role without `BYPASSRLS` — the only layer that survives
  `ExecuteUpdate`/Dapper/raw SQL.
- **Write guard:** a `SaveChangesInterceptor` in BuildingBlocks (not a base `DbContext` — there are
  seven contexts) that stamps `organization_id` on insert and rejects cross-tenant writes by
  comparing against `OriginalValues`, making the column immutable after creation. Both
  `SavingChanges` **and** `SavingChangesAsync` must be implemented — sync-only is a no-op in this
  codebase, which is async throughout.
- **Content:** per-customer curriculum forks are rejected outright — 15 customers would mean 15
  forks and no reachable content roadmap. Instead: global library (`organization_id IS NULL`) +
  copy-on-write overrides + immutable `lesson_version` snapshots, with progress pinned to a version.
  The existing schema has the bug this prevents: `UserExerciseAttempt.ExerciseId` points at mutable
  content, so editing a correct answer silently rewrites historical accuracy — the exact number
  sold to the РОП. Note the model adaptation: `Lesson` has no body today (only `Title`); all
  content is in `Exercise.SerializedContent`, so the versioned unit is the lesson **plus its
  ordered exercise set** as one JSON snapshot.
- **Access:** no public registration route at all (deleting `POST /auth/register`, not guarding
  it); `memberships (user_id, organization_id, role)` from day one even while the UI allows one org
  per user; the global `UserRole` enum splits into a platform role and a per-membership org role,
  so a РОП is an admin *of one organization*, never of the platform; offboarding deactivates,
  never deletes, because the manager's history belongs to the customer.
- **Deliberately deferred:** per-tenant subdomains (wildcard TLS, DNS, per-tenant CORS, OAuth
  callbacks — defer until someone pays for branding) and SSO itself. But the *seam* for SSO is
  built now — `organization_auth_config`, an `IAuthProvider` with a single password implementation,
  and a three-step login (email → resolve org → dispatch to provider) — because a 200-seat customer
  requiring Azure AD otherwise forces a simultaneous rewrite of login, sessions, invites and
  provisioning under their deadline.
- **The non-technical risk this design exists to defuse:** per-customer customization is a linear
  cost of delivery disguised as a feature. If Sellevate adapts the content, ~20 customers turns the
  company into a content agency. Hence the organization profile (product / ICP / objections /
  script / tone) with parameterized base content, and an explicit pilot measurement: if more than a
  third of adaptation needs hand-editing lesson text, the parameterization is wrong and must be
  fixed before the tenth customer.

---

## 2026-08-14 — Gamification is gone from the product, not from the backend

- **Context (user decision):** points, streaks and leagues are out. The removal had already started
  (the `/league` route was unlinked from the nav, the friends leaderboard was commented out, the
  skill tree stopped rendering its gamification fields), leaving the product half-way: a call still
  ended with «+N XP получено», the lesson path still promised «60 XP», the profile still showed
  «Лучшая серия», and `/league` was still reachable by URL.
- **Decision:** finish it in the **frontend only**. Removed from the user-facing app: XP on the
  lesson path, on the exercise result banner, in the call analysis and session history; the streak
  tiles on `/profile` and on a friend's profile; the `/league` route and its hook; the friends
  leaderboard (component + `/friends/leaderboard` query) and the dead `StatsWidget`; the landing
  page's "XP, серии и лиги" pitch; and the now-dead league/leaderboard CSS. Achievement and streak
  notifications are dropped on arrival — the notification service can still hold older ones, and an
  "achievement unlocked" toast in a product without achievements is pure confusion.
- **Kept deliberately:** every backend service, endpoint, event and DB table, plus the admin panel
  that configures them, and the DTO fields the API returns (`xpEarned`, `currentStreakDayCount`, …).
  The same pattern the skill tree already used. Reasons: the score still drives the AI feedback
  criteria; deleting `gamification-service` is a migration across four services' Kafka contracts and
  three databases, not a UI cleanup; and a reversal costs one commit this way instead of a rebuild.
- **Alternative rejected:** ripping out the service and its events now. It would put a large,
  irreversible backend migration behind a request that was about what the user sees.
- **Regression tests:** `FeedbackModal.test.tsx` (no XP even when the backend sends it); the
  remaining suites cover the touched exercise components.

---

## 2026-08-14 — A domain event must never hold a user request hostage

- **Context (user-reported):** «разбор не генерируется, бесконечная генерация», console showing a
  401 and `blocked by CORS policy`. The ai-service log tells the real story:
  `15:49:09 Calling OpenAI API` → `15:49:25 Extracted feedback summary … score: 6` →
  `15:50:49 ERR POST /dialog/sessions/{id}/complete responded 500 in 100010 ms`. The feedback was
  ready in 16s; the request then sat for another 100s and died. What happens after the feedback is
  saved is `PublishEvaluatedAsync` → `KafkaEventPublisher.ProduceAsync`, and the local Kafka
  container was not running (`repository-kafka-1` absent; the log is a wall of
  `localhost:9092 … Connection refused`). librdkafka's default `message.timeout.ms` is **5 minutes**,
  so the produce blocked until the 100s server/gateway timeout killed the request. The gateway then
  answered `504` — a response it writes itself, with no downstream CORS headers — so the browser
  reported "blocked by CORS" and the status code never reached the client. (The 401s were unrelated:
  an expired access token that the client refreshed.)
- **Decision:**
  1. `IEventPublisher.PublishAsync` no longer waits at all: the message is queued locally
     (`Produce` + delivery-report callback), so the request that produced it pays nothing whether
     the broker is healthy or absent, and a failed delivery is logged as an error rather than
     thrown. `Kafka:PublishTimeoutSeconds` (default 10) becomes librdkafka's `message.timeout.ms`,
     i.e. how long it keeps retrying in the background instead of the 5-minute default.
     `ForwardAsync` (outbox) and the dead-letter publisher still await and still throw — bounded by
     the same timeout — because their callers retry, and a silently "sent" outbox row would be lost
     forever. Ordering per partition is unaffected: the producer queues in call order.
  2. The gateway adds CORS headers to responses **it** generates (`GatewayErrorCorsMiddleware`),
     skipping anything that already carries them so proxied answers never get a duplicate
     `Access-Control-Allow-Origin` (browsers reject that outright).
  3. The client renders a `TypeError` from fetch as «Сервер не ответил…» rather than
     "Failed to fetch".
- **Alternative rejected:** an outbox for ai-service (write the event in the same transaction, let
  a relay retry it). That is the correct end state and `IOutboxEventForwarder` already exists — but
  ai-service has no outbox table and its session state lives in Mongo, so it is a migration, not a
  fix. The bounded publish is what stops a user-facing hang today; the outbox remains the follow-up.
- **Note for local dev:** Kafka *is* in `docker-compose.infra.yml`; that container simply was not
  up. With the bound in place a missing broker now costs a logged error, not a dead request.
- **Regression tests:** `KafkaEventPublisherTests` (an unreachable broker neither blocks nor throws;
  outbox forwarding still throws), `GatewayErrorCorsTests` (gateway-generated responses carry the
  headers for an allowed origin only).

---

## 2026-08-14 — A persona that says nothing is a bug in the contract, not in the LLM

- **Context (user-reported):** «собеседник вообще не отвечает». The ai-service log showed
  `POST /dialog/sessions/{id}/voice/stream` answering **200 in 11ms** with
  `WRN Voice stream aborted … Session … is not active`. `VoiceDialogController` sets
  `Response.StatusCode = 200` and the streaming content type *before* it asks the service for the
  first chunk, so every domain rejection (session completed, session missing, voice disabled)
  reached the browser as a 200 with an empty body. The frontend read zero frames, showed nothing,
  and the call looked alive with a mute persona. The same log also showed
  `POST /dialog/sessions → 400`: «Позвонить снова» on a custom-scenario page tried to create a
  session for the hidden `custom-scenario` mode without scenario text, which the backend rejects.
- **Decision:** three layers, because each one alone still leaves a silent failure.
  1. Backend: in the `InvalidOperationException` handler, if `!Response.HasStarted`, answer
     **409** with the message instead of an empty stream.
  2. Client: `409` → «Этот звонок уже завершён», and any stream that yields **zero frames** raises
     «Собеседник не ответил» rather than returning quietly.
  3. Page: a pre-started (`?session=`) scenario session is single-use — its status is checked
     before dialling, and once played out the CTA becomes «К сценариям», since this page never sees
     the scenario text and cannot legally create a replacement session.
- **Alternative rejected:** buffering the first chunk before committing the status code. It delays
  the first audible frame for every healthy call to improve the error path only, and the failure is
  always known before the first chunk anyway.
- **Regression tests:** `useVoice.test.tsx` (empty stream → error, 409 → error),
  `DialogVoiceCallPage.test.tsx` (a spent scenario refuses to dial and offers «К сценариям»).

---

## 2026-08-14 — Silent calls, and an analysis that cannot hang

### Call tones removed entirely

- **Context (user-reported):** the synthesized ringback (425 Hz, 1s/4s) and the triple busy beep
  were noise. A training call is not a phone call — the user already knows they pressed «Позвонить».
- **Decision:** delete `CallSoundsPlayer` and the Web Audio oscillators with it. The only state cue
  left is the connect vibration, now in `features/voice/services/call-haptics.ts` (`CallHaptics`).
- **Alternative rejected:** a volume/mute toggle. Nobody would have turned the tones back on, and it
  buys a settings row plus persistence for a feature with no demand.

### «Готовим разбор…» could never finish — three causes, all of them state, not the LLM

- **Context (user-reported):** after a call, the page sat on «Готовим разбор…» forever. The feedback
  request was not slow — in the reported flows it was **never sent at all**, and the hint lied.
  1. `useVoice` kept the finished session in `currentSessionIdRef`. The next «Позвонить снова»
     reused it (the page's `setSessionId(null)` lands a tick later, so the sync effect cannot win
     the race), the "session created" callback never fired, the call hung on «Соединение…», and its
     hang-up then had no session id to complete → an eternal «Готовим разбор…».
  2. The companies page latched `callEndedRef = true` on hang-up and never cleared it, so the *next*
     call's session was swallowed by the same guard — same dead end.
  3. `describePipeline` printed «Готовим разбор…» for *any* `ended` state, whether a request was in
     flight, had failed, or was never started.
- **Decision:** the session id is dropped by the new `endSession()` (the call pages call it on
  hang-up alongside `stopVoice`, which only stops listening — the chat mic button toggles voice
  input inside one dialog and must keep its session), so every call gets a fresh session; `callEndedRef` is reset on pick-up; the ended-state hint is derived from what
  is actually true (`describeEndedCall`: running / failed / ready / nothing to analyse); the
  in-flight guard is a ref, so a hang-up racing the persona's `endCall` cannot double-post.
  `POST /dialog/sessions/{id}/complete` is additionally capped at 120s client-side
  (`ApiRequestOptions.timeoutMs` → `RequestTimeoutError`; the backend's own upstream budget is 90s)
  and a failure offers «Повторить разбор». A retry against a session the backend already completed
  reads the stored feedback (`GET /dialog/sessions/{id}`) instead of failing on "not active".
- **Alternative rejected:** polling the session until feedback appears. It hides the failure instead
  of surfacing it, and the completion endpoint is synchronous — there is nothing to poll for.
- **Regression tests:** `__tests__/useVoice.test.tsx` (a fresh session per call, both end paths),
  `CompanyVoiceCallPage.test.tsx` (second call connects; no analysis promise without a session;
  retry after failure), `DialogVoiceCallPage.test.tsx` (a reused pre-started session connects).

---

## 2026-07-11 — AI backend hardening (39.17, PR #22 + PR #26 review fast-follows)

### `InternalAuth:ServiceSecret` — wire the missing header in learning-service, don't just document

- **Context:** PR #22 review flagged that `InternalAuth:ServiceSecret` (the shared secret behind
  ai-service's `InternalServiceAuthFilter`, guarding `EvaluationController` and the Companies AI
  controllers — briefing/readiness/parse-log/persona) is never provisioned in any `appsettings*.json`
  in this repo, and learning-service's `AiEvaluationClient` never sent the
  `X-Internal-Service-Secret` header (unlike company-service's four AI clients, which all already
  send it via their `*AiServiceCollectionExtensions`). Net effect today: the guard runs open in
  every environment (unset secret ⇒ `InternalServiceAuthFilter` skips the check), so
  `EvaluationController` is currently reachable by anyone who can route to ai-service directly.
- **Decision:** Wire the header in `AiEvaluationServiceCollectionExtensions.AddAiEvaluationClient`
  (learning-service), mirroring the exact pattern company-service's `BriefingAiServiceCollectionExtensions`
  / `ReadinessAiServiceCollectionExtensions` / `ParseLogAiServiceCollectionExtensions` /
  `PersonaAiServiceCollectionExtensions` already use: read `InternalAuth:ServiceSecret` from
  config, add the header to the typed `HttpClient` only if the secret is non-empty.
- **Why wiring instead of documenting:** the fix is a ~10-line, single-file, additive change
  (no behavior change while the secret stays unset — it's the same no-op the other four clients
  already have) that closes the actual gap, rather than leaving `EvaluationController` open and
  writing a paragraph explaining why. There was no risk/blast-radius reason to prefer
  documentation-only here — the change touches nothing else callers depend on.
- **Still true after this fix:** `InternalAuth:ServiceSecret` is *provisioned* nowhere (no
  `appsettings*.json`/deployment config sets it), so the guard still runs open by default in
  every environment today. Wiring the header only means that *if/when* ops sets the secret in
  ai-service **and** all three callers (company-service, learning-service, gateway if it ever
  calls ai-service directly), the guard will actually enforce it end-to-end. Provisioning the
  secret itself is an ops/deployment task, out of scope here — tracked as a gap, not silently
  assumed done.

### Negative-cache TTL for the "no usable feedback yet" readiness result

- **Context:** PR #26 review noted `GET /companies/{id}/readiness` re-fans-out (up to 50
  sequential `DialogSessionId` lookups via ai-service → Mongo) on *every* request while the
  company has practice sessions but ai-service keeps returning `204` (no feedback text landed
  yet) — the positive cache (`ReadinessJson`) only helps once there's a real result.
- **Decision:** Add `Company.ReadinessNoFeedbackUntil` (nullable timestamptz) — set to
  `now + 2 minutes` when ai-service returns `204` after a real fan-out; checked before the
  fan-out on subsequent `GET`s. Left untouched (`null`) for the *other* 204 case — zero practice
  calls — since that path already short-circuits before touching ai-service and has nothing
  expensive to avoid. Cleared by `CreatePracticeCallAsync` alongside the existing
  `ReadinessJson`/`ReadinessGeneratedAt` invalidation, and cleared again once a real result is
  cached, so a fresh practice call always gets a fresh readiness attempt.
- **Why 2 minutes:** short enough that a user who just finished a practice call and immediately
  reloads doesn't wait meaningfully longer than before for a fresh readiness attempt (the
  practice-call-created invalidation already covers the common case), long enough to absorb
  repeated polling/reloads from the frontend readiness card within the same short window.
- **Alternative considered:** cache the negative result indefinitely until the next practice
  call. Rejected — feedback can, in principle, land in Mongo asynchronously without a new practice
  call being created in company-service (out of scope to fully reason about here), so an
  unbounded negative cache risked being wrong for longer than necessary.

### Dedicated `BriefingModel`/`MaximumBriefingTokenCount` config in ai-service

- **Context:** PR #22 review noted the briefing feature (39.12) reused `OpenAiConfiguration`'s
  `OpenQuestionModel`/`MaximumFeedbackTokenCount` — config names that describe unrelated features
  (open-question exercises, dialog feedback), making it unclear/risky to retune either without
  affecting briefing too.
- **Decision:** Add `OpenAiConfiguration.BriefingModel` (default `"gpt-4.1"`, same as
  `OpenQuestionModel`'s default) and `MaximumBriefingTokenCount` (default `1500`, same as
  `MaximumFeedbackTokenCount`'s default) — unset config keeps today's behavior byte-for-byte.
  `IOpenAiChatService.GenerateTextAsync` gained optional `model`/`maxTokens` parameters (default
  `null` ⇒ falls back to `OpenQuestionModel`/`MaximumFeedbackTokenCount`, preserving the other
  three callers — `ParseLogService`, `ReadinessService`, `PersonaService` — unchanged); only
  `BriefingService` passes the new dedicated options explicitly.
- **Why not also split ParseLog/Readiness/Persona:** out of scope — the PR #22 review only
  flagged briefing by name, and those three weren't called out as piggybacking on unrelated
  config. Keeping the change scoped avoids touching three working features' behavior/config
  surface without a stated need.

---

## 2026-06-21 — Phase 3 (Shared User read-model replica) — resolved as satisfied/superseded

- **Context:** [MICROSERVICES_ROADMAP.md](MICROSERVICES_ROADMAP.md) Phase 3 ("Shared User
  read-model replica") was still `[ ]`, but the established database-per-service pattern had
  already realized it by the time the domain services were extracted (Phases 5–8). This entry
  records the per-task verdict so the roadmap reflects reality rather than leaving a phantom
  open phase.

### Per-task verdict

- **3.1 — UserReplica table + `user.*` consumer in BuildingBlocks, reusable by every service →
  Satisfied.** The shared `UserReplica` entity lives in BuildingBlocks since Phase 0.1
  ([src/backend/building-blocks/BuildingBlocks/Identity/UserReplica.cs](../src/backend/building-blocks/BuildingBlocks/Identity/UserReplica.cs)),
  alongside the `user.*` topic constants
  ([Eventing/Topics.cs](../src/backend/building-blocks/BuildingBlocks/Eventing/Topics.cs) lines 17–20)
  and the reusable idempotent consumer base `KafkaConsumerBackgroundService` (Phase 0.4).
  Every extracted domain service keeps **its own** replica table, fed by its own idempotent
  `user.*` consumer (dedupe on `eventId`) plus its own EF config:
  - gamification-service: [Identity/UserReplica.cs](../src/backend/gamification-service/Gamification/Identity/UserReplica.cs),
    [Eventing/UserReplicaConsumer.cs](../src/backend/gamification-service/Gamification/Eventing/UserReplicaConsumer.cs),
    [Infrastructure/Data/UserReplicaEntityConfiguration.cs](../src/backend/gamification-service/Gamification/Infrastructure/Data/UserReplicaEntityConfiguration.cs)
  - ai-service: [Identity/UserReplica.cs](../src/backend/ai-service/Ai/Identity/UserReplica.cs),
    [Eventing/UserReplicaConsumer.cs](../src/backend/ai-service/Ai/Eventing/UserReplicaConsumer.cs),
    [Infrastructure/Data/UserReplicaEntityConfiguration.cs](../src/backend/ai-service/Ai/Infrastructure/Data/UserReplicaEntityConfiguration.cs)
  - social-service: [Identity/UserReplica.cs](../src/backend/social-service/Social/Identity/UserReplica.cs),
    [Eventing/UserReplicaConsumer.cs](../src/backend/social-service/Social/Eventing/UserReplicaConsumer.cs),
    [Infrastructure/Data/UserReplicaEntityConfiguration.cs](../src/backend/social-service/Social/Infrastructure/Data/UserReplicaEntityConfiguration.cs)
  - learning-service: [Identity/UserReplica.cs](../src/backend/learning-service/Learning/Identity/UserReplica.cs),
    [Eventing/UserReplicaConsumer.cs](../src/backend/learning-service/Learning/Eventing/UserReplicaConsumer.cs),
    [Infrastructure/Data/UserReplicaEntityConfiguration.cs](../src/backend/learning-service/Learning/Infrastructure/Data/UserReplicaEntityConfiguration.cs)

  (notification-service and analytics-service are Redis-only with no relational store, so they
  consume `user.*`/funnel events directly and need no `UserReplica` table — consistent with the
  pattern.)

- **3.2 — Wire the replica into the still-monolithic remaining features so they stop joining
  Identity tables → Superseded by Phases 5–8 + Phase 9.** The strangler migration extracted
  **all** domain services, each owning a local replica seeded from `user.*` events, and the
  monolith is being retired in Phase 9 (kept only as reference). There are no remaining
  monolithic features left to "wire onto the replica," so this task is superseded by the actual
  extraction work rather than skipped arbitrarily.

- **3.3 — Tests: replica seed / update / delete → Satisfied per-service.** Each service's replica
  consumer is covered by that service's own test suite; the canonical explicit example is
  [src/backend/social-service/Social.Tests/Unit/UserReplicaConsumerTests.cs](../src/backend/social-service/Social.Tests/Unit/UserReplicaConsumerTests.cs)
  (seed on `user.registered`, idempotent re-seed, update on `user.updated`, delete on
  `user.deleted`).

### Alternative considered

- **A single central User replica service** that every other service queries over REST/gRPC,
  instead of each service holding its own copy. **Rejected:** it reintroduces a synchronous
  cross-service dependency on a shared store — the exact coupling database-per-service exists to
  remove (see [DATA_OWNERSHIP.md](DATA_OWNERSHIP.md)). Database-per-service + a local
  event-fed `UserReplica` per service is the locked decision.

### Reusable-extraction assessment

- Considered extracting a shared `UserReplicaConsumer` base / EF config into BuildingBlocks to
  remove the near-identical per-service consumers. **Not done:** each consumer is bound to its
  own `DbContext` type and its own per-service event DTOs
  (e.g. [gamification IncomingIntegrationEvents.cs](../src/backend/gamification-service/Gamification/Eventing/IncomingIntegrationEvents.cs)),
  so a shared base would require generics over `DbContext` plus shared event contracts, touching
  every service's migrations. That exceeds the "removes real duplication at low risk" bar, so
  this resolution is **documentation-only** (no code extracted).

---

## 2026-06-15 — Email verification by code

### MailerSend as the email provider

- **Decision:** Send the verification email through MailerSend.
- **Why:** EU-hosted (matches the European server), free tier covers low-volume verification
  mail, simple Bearer-token HTTP API, supports sending from a custom verified domain.
- **Alternatives:** Brevo (also EU/free), Amazon SES (cheapest at scale, more setup),
  self-hosted SMTP (rejected — new-IP deliverability is poor). The `IEmailSender` abstraction
  keeps the provider swappable.

### Store codes in Postgres, not Redis

- **Decision:** Persist `EmailVerificationCodes` in Postgres via EF, despite Redis being wired up.
- **Why:** Redis is registered but otherwise unused, with no established pattern; the codebase is
  EF-centric and the integration-test harness runs a real Postgres but only a stub Redis. Postgres
  gives a testable, well-trodden path plus expiry/attempt columns and a Hangfire cleanup job.
- **Trade-off:** Codes need a periodic cleanup job (added) instead of Redis TTL auto-expiry.

### Hash codes; one active code per email

- **Decision:** Store only the SHA-256 hash of the code, replace any prior code on each request,
  cap attempts, and rate-limit resends.
- **Why:** Limits blast radius of a DB read, and the attempt cap + short TTL make a 6-digit code
  safe against brute force. BCrypt was considered overkill for a short-lived single-use OTP.

### Register no longer returns tokens

- **Decision:** `/auth/register` returns `RegistrationResultDto` (verification required) instead of
  an `AuthTokenResponseDto`; tokens are issued by `/auth/verify-email`.
- **Why:** Tokens must not be granted before the address is proven. Google sign-in stays
  auto-verified. Existing users are grandfathered verified by the migration.

---

## 2026-06-12 — Discuss photo attachments

### Single polymorphic `DiscussPhotos` table

- **Decision:** Store thread and reply photos in one `DiscussPhotos` table with an
  `(OwnerType, OwnerId)` polymorphic owner, rather than two separate tables (e.g.
  `DiscussThreadPhotos` + `DiscussReplyPhotos`).
- **Why:** Mirrors the existing `DiscussVotes` shape (`TargetType, TargetId`), so the slice stays
  internally consistent and the upload/list/delete code path is shared.
- **Trade-off:** No DB-level FK to the owner row; orphan cleanup is handled in the service on
  thread/reply delete.

### Two-step create (JSON create + multipart photo sub-resource)

- **Decision:** Keep the existing JSON create endpoints for threads/replies unchanged and add a
  separate multipart photo sub-resource (`POST .../photos`). Alternative considered: switch the
  create endpoints themselves to `multipart/form-data`.
- **Why:** Lowest-risk change — the existing create endpoints and their callers stay untouched.
- **Trade-off:** A post can exist with a failed photo upload. The frontend surfaces this as a
  non-fatal, retryable error rather than discarding the created post.

### Service-level max-10 enforcement (no DB constraint)

- **Decision:** Enforce the 10-photos-per-owner cap in the service, not via a DB constraint.
- **Why:** Matches the slice's existing service-enforced validation style (the same approach
  used elsewhere in Discuss).

### Duplicated image magic-byte validator

- **Decision:** Accept a duplicated `ImageContentValidator` between the Avatars and Discuss slices
  rather than extracting a shared utility now.
- **Why:** Bounded scope; the two slices are independent. A future shared utility is possible if a
  third consumer appears.

### Style note: mirror the existing slice conventions

- **Decision:** New Discuss-photo files intentionally mirror the existing Discuss/Avatars slice
  conventions — `public class` EF configs, `{ get; set; }` + `= null!` entities, `ct` parameter
  name, and inline cache / `nosniff` headers like `AvatarsController` — rather than the strict
  letter of [CODESTYLE.md](CODESTYLE.md).
- **Why:** Keeps the slice internally consistent with the code it lives next to.

## Email notifications

### Shared email transport in BuildingBlocks (not duplicated per service)

- **Decision:** Move the MailerSend email stack (`IEmailSender`, `EmailMessage`,
  `MailerSendEmailSender`, `MailerSendConfiguration`) out of the identity service into
  `Sellevate.BuildingBlocks.Email`, exposed via `AddSellevateEmail()`. Alternative considered:
  copy the sender into the notification service.
- **Why:** Two services now send transactional email (identity verification codes, notification
  emails); one shared implementation avoids divergent MailerSend wiring and config drift.
- **Trade-off:** BuildingBlocks gains an HTTP/email concern, but it already references
  `Microsoft.AspNetCore.App` (so `IHttpClientFactory`/`AddHttpClient` are available).

### Redis user replica in the notification service (no database)

- **Decision:** Resolve a recipient's email/display name from a Redis-backed user replica
  (`notifications:user:{userId}`) fed by `UserReplicaConsumer`, rather than introducing EF/Postgres
  or a synchronous call to identity.
- **Why:** The notification service is deliberately Redis-only; a Redis projection keeps that
  property and matches the `UserReplica` pattern other services use (just without EF).
- **Trade-off:** Eventually consistent — a brand-new user with no replicated email yet is simply
  not emailed (logged, never throws).

### Delayed unread-chat email via a Redis sorted set + watermark

- **Decision:** Implement "email if a message is unread after 5 minutes" with a Redis sorted set
  of pending emails (scored by due time) plus a per-(recipient, conversation) read watermark, polled
  by a background dispatcher. A `chat.message.read` event updates the watermark; the dispatcher
  skips messages read before they came due. Alternative considered: Hangfire delayed jobs.
- **Why:** Keeps the service Redis-only (no Hangfire/DB), and a watermark is simpler and more
  replay-safe than scheduling + cancelling individual jobs.
- **Trade-off:** Delivery is approximate to within one poll interval (default 30s); acceptable for
  a "you missed a message" email.

### OOP email templates (template-method) over inline HTML strings

- **Decision:** Generate notification email HTML inside the notification service via a template
  hierarchy — `NotificationEmailTemplate` (abstract) + per-type subclasses + a shared
  `NotificationEmailLayout` and a `NotificationEmailRenderer` that selects by `NotificationType`.
- **Why:** Adding an email for a new type is one small subclass; the shared, client-safe chrome and
  HTML-encoding live in one place. Matches the request to "use OOP and separate helpers".
- **Trade-off:** More files than a single string builder, but each is small and isolated.

### Codestyle "no comments" rule (CODESTYLE.md §9) is aspirational, not a merge gate

- **Decision:** The companies feature (Phase 39) ships with XML `///` doc comments and the
  occasional inline rationale comment, the same convention the rest of the backend already uses.
  `scripts/codestyle-lint.py` flags ~490 such lines in the feature's touched files, but `main`
  already contains 909 `///` doc-comment lines across the backend, so the rule is not enforced
  repo-wide. Mass-stripping comments from only the companies files would make the feature
  *inconsistent* with the surrounding codebase for no functional gain.
- **Why:** Release gate is "no new lint/type/test regressions vs `main`", not "touched files must
  satisfy an unenforced style law". The `catch (Exception ex)` abbreviations the linter flagged in
  touched files are likewise pre-existing on `main` (the feature only touched the file), so they are
  out of scope for this branch.
- **Follow-up:** If the team wants CODESTYLE.md §9 enforced, do it as a dedicated repo-wide sweep
  with its own PR, not piecemeal per feature.

### Internal service-to-service auth secret ships inert in the docker/compose shape

- **Decision:** `InternalServiceAuthFilter` (ai-service) treats a missing `InternalAuth:ServiceSecret`
  as "allow" (dev convenience). Neither the `ai` nor `company` service sets that key in
  `docker-compose.yml`/`.env` today, so the `/ai/companies/*` guard is a no-op in the deploy shape.
- **Why acceptable for the companies release:** those internal AI routes are not gateway-exposed
  (verified: 0 `/ai` routes in the gateway config) — they are reachable only on the internal Docker
  network. The filter is defense-in-depth, not the primary boundary. A company-service appsettings
  stub (`"InternalAuth": { "ServiceSecret": "INJECTED_FROM_ENV" }`) was added for discoverability.
- **Follow-up (post-merge hardening):** inject a shared `InternalAuth__ServiceSecret` env on BOTH
  the `ai` and `company` services (and any k8s manifest) so the guard enforces in non-dev; provision
  symmetrically to avoid a one-sided 401.

### ai-service must accept string enum values on its JSON wire (persona 400 fix)

- **Bug:** `POST /companies/{id}/personas/generate` returned `AI persona service returned 400`.
  Root cause: company-service serializes the persona `Difficulty` enum as a **string** (via
  `enum.ToString()`, e.g. `"Medium"`), but ai-service registered plain `AddControllers()` with no
  `JsonStringEnumConverter`. System.Text.Json binds enums from **numbers** by default, so the string
  failed to deserialize and `[ApiController]` auto-returned **400** before `PersonaController` ran.
- **Decision:** Register `JsonStringEnumConverter` in ai-service's `AddControllers().AddJsonOptions(...)`,
  mirroring company-service's existing config. Cross-service enum payloads now bind by name on both hops.
- **Why the tests missed it:** `PersonaControllerTests`/`PersonaServiceTests` build the DTO in-process
  and never cross the JSON wire. Added `PersonaRequestWireContractTests` to lock the string-enum
  contract at the serialization boundary.

### F5Ai LLM provider must be selected via OpenAI__Provider (persona/dialog 500 → 401 fix)

- **Bug:** After the persona string-enum fix, `POST /ai/companies/persona` reached the LLM but
  returned **500** wrapping `OpenAiAuthenticationException` — the F5Ai gateway (`api.f5ai.ru`)
  answered **401**. This broke ALL LLM calls (persona, dialog, feedback), not just personas.
- **Root cause:** `OpenAiChatService` picks the auth header by `OpenAI:Provider`
  (`F5Ai` → `X-Auth-Token`, otherwise → `Authorization: Bearer`). After the "AI7c" refactor from
  URL-sniffing to an explicit provider enum, no deploy config set `OpenAI__Provider`, so it defaulted
  to `OpenAi` → Bearer → 401 against F5Ai. Verified live: same key returns **200** with
  `X-Auth-Token` and **401** with `Bearer`.
- **Fix:** Added `OpenAI__Provider=${OPENAI_PROVIDER:-OpenAi}` to both ai and learning service env
  blocks in `docker-compose.yml`; set `OPENAI_PROVIDER=F5Ai` in `.env` (documented default `OpenAi`
  in `.env.example`). No code change — the enum path was already correct, only unconfigured.
- **Recurrence in the Local Dev profile (custom-scenario validation 503):** the fix above only
  covered `docker-compose.yml`. The host scripts — `scripts/dev-ai.sh` and the
  `export_backend_env` / `export_learning_env` blocks in `scripts/lib-local-env.sh` — exported
  `OpenAI__ApiKey` / `BaseUrl` / `ChatCompletionsPath` but **not** `OpenAI__Provider`, so the
  default Local Dev profile still sent Bearer to F5Ai. Symptom: `POST /dialog/scenario/validate`
  → 401 `{"error":{"message":"API key is missing"}}` → `ScenarioValidationUnavailableException`
  → **503**, surfaced in the UI as «Не удалось проверить сценарий». The scenario text was never
  the cause. Fix: export `OpenAI__Provider` (plus the model/token tunables compose already passed)
  from the host scripts too. **Rule: any new `OpenAI__*` key added to `docker-compose.yml` must be
  mirrored into the host dev scripts, and vice versa** — the two profiles are the same config
  surface and drift between them is invisible until a live call fails.

### Frontend adaptivity: sizing rules over per-page breakpoints

- **Bug (user-reported):** on some devices buttons stopped being visible — with no zoom and no
  change of screen resolution. Three independent root causes, all the same underlying mistake:
  layout boxes were given **hard, absolute sizes** (`100vh`, hand-counted pixel constants,
  fixed-count grid tracks) instead of intrinsic sizes with floors and ceilings. Each is correct
  on the developer's monitor and drifts on every other device.
  1. **Landscape phones got the desktop shell.** The rail is `height: 100vh` with every child
     `flex-shrink: 0`, summing to ~516px. A landscape phone reports ≥768px wide but 375–430px
     tall, so it matched the desktop branch and the notification bell + settings gear rendered
     below the fold with no scroll affordance.
  2. **`/tree` FAB anchored to the timeline, not the viewport.** At ≤1000px `.path-grid` becomes
     `height: auto`, so `.path-center` grows to the full height of the lesson list — but the
     `position: absolute` FAB was only switched to `fixed` at ≤767px. In the 768–1000px band
     (every iPad in portrait) the "Начать" CTA sat ~2000px below the fold.
  3. **A 1px breakpoint dead zone.** `max-width: 767px` and `min-width: 768px` both fail to match
     at fractional widths (non-integer `devicePixelRatio`, Windows display scaling), so the rail
     and the bottom nav rendered simultaneously and the nav covered content.
- **Decision:** fix the *sizing rules*, not the individual pages. Codified in
  `docs/TESTING/MOBILE_RESPONSIVE.md`: always ship the `100vh`/`100dvh` fallback pair; every
  bottom-anchored control carries `env(safe-area-inset-bottom)` (`viewportFit: "cover"` is set,
  so the inset is real); text-bearing flex/grid children get `min-width: 0` / `minmax(0, 1fr)`;
  rows of unshrinkable buttons get `flex-wrap: wrap`. Added one **height** tier
  (`max-height: 520px`) — the axis the breakpoint system had no concept of.
- **Alternative rejected:** a full re-tier of all ~23 media queries onto Tailwind's scale. It is
  the right end state, but it touches every page layout at once and a regression could not be
  attributed. Deferred until there are screenshot tests; the `.98` suffix closes the dead zone
  in the meantime.
- **Why the tests missed it:** all 272 frontend tests are jsdom unit/hook tests, which have no
  layout engine — jsdom does not compute `vh`, `env()`, flex overflow, or media queries. This
  class of bug is only reachable through visual/viewport testing, so it is covered by the manual
  checklist rather than by assertions.

---

## Phase 40.6 — Identity: memberships and the role split (2026-08-15)

### The `RequireAdmin` audit — every call site, decided deliberately

The block's instruction was explicit: don't mechanically rename `RequireAdmin` to
`RequireSuperAdmin`, decide each call site. The deciding question for every one of them was
the same: **is this endpoint scoped to one organization, or is it still global/platform
content?** As of this branch, the organization-scoped admin screen (roadmap block 40.20,
"Разделение админки" — a separate `/admin` surface for a РОП's own program, overrides and
company profile) does not exist yet. Every current `/admin/*` endpoint manages a *global*
resource (the shared content library, platform-wide user list, platform-wide gamification
config, platform-wide discuss moderation) with **no** `organization_id` anywhere in its
query — confirmed by reading each controller, not assumed. So every single one resolved the
same way:

| Call site | Old policy | New policy | Why |
|---|---|---|---|
| `identity-service` `AdminUsersController` (whole controller, incl. `GET /admin/users`, `GET/PUT /admin/users/:id`, `DELETE .../avatar`) | `RequireAdmin` (class-level) | `RequireSuperAdmin` | Lists/manages users **platform-wide**, not scoped to one org. `PUT .../role` was already `RequireSuperAdmin`; now the whole controller is, so that per-method attribute was redundant and removed. |
| `identity-service` `AdminUsersController.ChangeRole` | `RequireSuperAdmin` (unchanged) | `RequireSuperAdmin` | Already correct — role changes only ever move between the two remaining platform roles (`User`/`SuperAdmin`). `Enum.TryParse<UserRole>("Admin", …)` now fails with 400, same as any other unknown role (locked in by `ChangeRole_RejectsRemovedAdminRole`). |
| `ai-service` `AdminDialogController` (dialog bundles/modes) | `RequireAdmin` | `RequireSuperAdmin` | Global dialog-content library; no `organizationId` in ai-service today. |
| `ai-service` `AdminVoiceUsageController` (`GET /admin/voice/usage`) | `RequireAdmin` | `RequireSuperAdmin` | Platform-wide voice usage/cost view across *all* users — a cost-control screen for Sellevate, not an org-scoped one. |
| `gamification-service` `AdminGamificationController` | `RequireAdmin` | `RequireSuperAdmin` | Global gamification config (XP sources, thresholds). |
| `gamification-service` `AdminLeaguesController` | `RequireAdmin` | `RequireSuperAdmin` | Team-progress/league administration is a single global ladder today, not per-organization. |
| `learning-service` — `AdminSkillsController`, `AdminSkillStagesController`, `AdminTopicsController`, `AdminLessonsController`, `AdminExercisesController`, `AdminExerciseTypePromptsController`, `AdminReferenceController`, `AdminTechniquesController`, `AdminDailyQuotesController`, `AdminSeederController` (10 controllers) | `RequireAdmin` | `RequireSuperAdmin` | All manage the shared, nullable-`organization_id` content library (skills/lessons/exercises/reference/techniques/quotes) — see TENANCY.md §1.2. Per-organization content overrides are 40.18 (copy-on-write), not shipped yet; until then editing this content is still Sellevate-staff work. |
| `social-service` `AdminDiscussController` | `RequireAdmin` | `RequireSuperAdmin` | Platform-wide discuss moderation; no org scoping in social-service. |
| `social-service` `DiscussController.IsAdmin()` (private helper — **not** a named policy, a raw `User.IsInRole("Admin") \|\| User.IsInRole("SuperAdmin")` check gating "delete any thread/reply, not just your own") | n/a (inline role check) | `User.IsInRole("SuperAdmin")` | Same underlying question (platform moderator vs. thread author) — caught by grep for `IsInRole("Admin")`, not by the `RequireAdmin` policy-name search, so called out explicitly here. |

**Net effect:** every existing admin surface is Sellevate-staff-only now. `RequireOrgAdmin`
is registered in every service that had `RequireAdmin` (identity, ai, gamification,
learning, social) as pure infrastructure — `policy.RequireAssertion(... HasClaim("org_role",
"OrgAdmin") ...)` — with **zero call sites** in this block. It exists for 40.7 (an OrgAdmin
inviting their own organization's managers) and 40.20 (the org admin screen). Leaving it
unused-but-present, rather than adding it only when 40.7 needs it, means the claim shape and
policy name are locked in now and 40.7 doesn't have to touch every service's `Program.cs`
again.

### `Admin` role value: left unassigned, not reused

`UserRole` was `{User = 0, Admin = 1, SuperAdmin = 2}`. Removing `Admin` leaves value `1`
unassigned rather than renumbering `SuperAdmin` down to `1` or reusing `1` for something new.
**Why:** a pre-existing row with `Role = 1` (if any exist in a real database before 40.9's
migration runs) fails to deserialize loudly (`InvalidOperationException` from EF's enum
conversion) instead of silently becoming whatever the next thing assigned to `1` happens to
be. Loud failure on stale data is the correct default until 40.9 explicitly decides what a
former `Admin` user becomes (most likely: an `OrgAdmin` membership in whatever organization
40.9 backfills them into).

### JWT `org_id`/`org_role`: looked up at token-issue time, not decoded from a header

`AuthenticationService.IssueTokensForUserAsync` queries the user's memberships
(`Status == Active`, ordered by `JoinedAt`, first-or-default) and adds `org_id`/`org_role`
claims to the JWT only when one exists. **Why not push this into the gateway or a header
instead:** every downstream service already validates the JWT directly via its own
`AddJwtBearer` (shared HMAC signing key) — `org_role` doesn't need a header round-trip the
way `X-Organization-Id` does for `ITenantContext`, because the authorization policies read
`HttpContext.User.Claims` off the already-validated token. Only `identity-service` needed
code changes to *produce* the claims; every other service only needed the new
`RequireOrgAdmin` policy definition, not a new header consumer.

**Ordering by `JoinedAt` when multiple active memberships could theoretically exist:** the
schema (composite PK `(UserId, OrganizationId)`) does not prevent a user from having two
active memberships even though nothing in the product creates that today ("even while the UI
only allows one organization per user" per the spec). Picking deterministically now avoids a
silent behavior change if that assumption is relaxed later without anyone revisiting token
issuance.

### `Membership` is a plain EF entity — not `ITenantScoped`, no RLS, in this block

`Membership.OrganizationId` is the tenant-defining column for `membership` conceptually, but
the entity does **not** implement `ITenantScoped` and no RLS policy was added to it in 40.6.
**Why:** `ITenantScoped`/RLS assume `ITenantContext.OrganizationId` is already known when the
row is written — but establishing membership (accepting an invite, an OrgAdmin inviting a
manager) is exactly the moment that context doesn't exist yet for the invitee, and superadmin
actions (creating the *first* membership in a brand-new organization, 40.9) are inherently
cross-tenant. This mirrors `organization-service`'s own `Organization` registry entity, which
is likewise not tenant-scoped — both are cross-organization registries by nature, not
per-tenant data. No membership CRUD endpoints ship in this block (that's 40.7/40.9), so this
is a forward-looking note, not yet a tested boundary.

**Follow-up for 40.7/40.9:** when membership *write* endpoints ship, guard them with
application-level checks (actor's `org_id`/`org_role` claim against the target
`organization_id`), not RLS — and revisit this decision if a read-heavy membership-listing
endpoint would benefit from RLS instead of an app-level filter.

### `Membership.UserId` gets a real FK; `OrganizationId` does not

`UserId` references `Users.Id` in the **same** database (identity-service owns both tables),
so a normal FK with `ON DELETE CASCADE` applies — unlike `OrganizationId`, which is a
cross-service reference under DB-per-service and per the block's explicit instruction stays a
bare `uuid` with no FK. Cascade (not `Restrict`) was chosen because there is no
delete-account endpoint yet (per IDENTITY_SERVICE.md) and an orphaned membership row pointing
at a deleted user would be meaningless; revisit if/when account deletion ships and offboarding
semantics (`membership.status = deactivated`, never delete) need to apply to the user row too.

### Frontend: dropping `Admin` collapses the admin-panel gate to `SuperAdmin`-only

`shared/stores/auth-store.ts`'s `UserRole` type drops `"Admin"`; every frontend call site that
checked `role === "Admin" || role === "SuperAdmin"` (`app/(admin)/layout.tsx` — both the
redirect guard and the nav-item list, `app/(admin)/admin/users/page.tsx`,
`app/(main)/settings/page.tsx`'s admin-area visibility, `app/(main)/discuss/[threadId]/page.tsx`'s
moderation check, `features/admin/components/user-detail-modal.tsx`'s role dropdown) now
checks `role === "SuperAdmin"` alone — a mechanical consequence of the backend audit above,
not a separate decision, since every guarded surface maps to a `RequireSuperAdmin` endpoint.
Added `orgId`/`orgRole` (optional) to `AuthenticatedUser` and threaded them through
`useHandleSuccessfulAuth`/`useInitAuth` so the org claims introduced this block are actually
reachable from the frontend once 40.20 needs them — leaving JWT claims with no client-visible
counterpart would have been a dangling reference in the other direction.

---

## Phase 40.12 — company-service, where the scope is double (2026-08-15)

### Company-service is scoped by organization **and** by user, and the two are enforced differently

Every other Stage-C service has one axis: a row belongs to an organization. company-service has
two — a row belongs to one salesperson *inside* one organization, because this is a personal CRM,
the list of companies one person is calling. Getting either half wrong is a bug, but in opposite
directions: the organization half leaks between paying customers, the user half hands a
salesperson a colleague's private pipeline inside one customer.

The two halves are therefore enforced in two different places, on purpose:

- **Organization** — never written at a call site. It comes from `ITenantContext` through the EF
  query filter and, authoritatively, from the RLS policy on all five tables. Nothing in
  `CompanyService` mentions it.
- **User** — always an explicit `UserId == userId` predicate, on the parent row *and* on every
  sub-resource query. A query filter cannot express it (the user is a method argument, not ambient
  state), and hiding it in the model would make "which rows can this caller see" impossible to read
  off a call site.

Before 40.12, sub-resource queries filtered on `CompanyId` alone and leaned on the ownership check
performed on the parent two statements earlier. That was sound — a company id is unique to one
user — but it left half of a two-part rule resting on an argument rather than on a predicate. All
24 of them now carry the user themselves. The two deliberate exceptions are the navigation-property
counts in `ListCompaniesAsync`/`GetCompanyAsync`, which count children of a company row already
matched by both halves.

The isolation tests assert both halves separately, and the user half is asserted **through the
service layer**, because the database deliberately admits both colleagues' rows — the predicate is
the only thing between them.

### Strict RLS on all five tables, with no content flavour anywhere

learning-db and ai-db needed `EnableTenantRlsForContent` (`organization_id IS NULL OR = current`)
because they hold a global library shared by every tenant. company-db holds nothing of the sort:
`Companies`, `CallLogEntries`, `PracticeCalls`, `CompanyContacts` and `CompanyPersonas` are all one
salesperson's own working data. Every policy is therefore `EnableTenantRls`, plain equality, and a
row with no organization is invisible rather than shared. That also makes the "unset tenant" test
here stronger than in the other two services: an unset tenant sees literally nothing, not "only the
global library".

### `Company` is not deduplicated across users or organizations, and stays that way

Two salespeople at the same customer who both call Acme already produce two `Companies` rows today
— there is no unique constraint on `(UserId, Name)`, and none is added. Once the table is
tenant-scoped that becomes up to three rows for the same real-world company: one per salesperson
per organization.

That is the correct answer for what this table actually stores. `Description` is a free-form brief
written for one person's call, `Contacts`/`Personas`/`CallLogEntries` are that person's notes and
that person's practice history, and `NextActionAt` is that person's calendar. Merging two
salespeople onto one row would mean one of them editing the other's brief and seeing the other's
log. A shared *account* record — one row per organization, with per-user pipelines hanging off it —
is a different product feature (a real CRM's account/opportunity split), not a deduplication tweak,
and nothing in Phase 40 asks for it.

### Where company-service's per-organization poll gets its organizations

`FollowUpReminderBackgroundService` is the job TENANCY.md §1.6 names as "scans due follow-ups
across **all** orgs". It now iterates organizations with a scoped context per organization, which
raises the question of where the list comes from. Three options were on the table:

1. **A replicated tenant registry** (`OrganizationReplicas`, the way 40.9 gave identity one). It
   would need a Kafka consumer, and company-service is deliberately producer-only — `Program.cs`
   documents why — so wiring `AddSellevateEventing` would add a Redis dependency this service has
   never had. Worse for correctness: an organization whose registry row had not replicated yet
   would be skipped, turning replication lag into a silently dropped reminder.
2. **Ask organization-service synchronously.** Puts a second service in the path of a background
   job and fails the whole tick when it is down.
3. **`SELECT DISTINCT "OrganizationId" FROM "Companies" WHERE <due and unnotified>`** — chosen. The
   question the job actually asks is "which organizations have a follow-up due right now", and that
   is a fact of company-db. An organization with no companies is not worth a loop iteration, and no
   organization with due rows can be missed.

The enumeration is the one place in company-service that enters system mode: one method, one
column, no row content, and everything downstream runs with a concrete organization set — which is
the "explicit, auditable opt-in" §1.6 asks for rather than an ambient fallback.

**The cost, stated plainly:** system mode issues no `SET LOCAL`, so this query returns rows only
for a role that bypasses RLS. That holds today (the service connects as the owning superuser) and
is exactly the trap `DB_SCHEMA.md` already flags about `OrganizationAuthConfiguration` — a
`NOBYPASSRLS` `sellevate_app` would make the enumeration return an empty list and the poll would go
quiet without erroring. Recorded in `docs/DONT_FORGET.md` as a prerequisite of that rollout: either
grant the background connection `BYPASSRLS` or give company-service a second connection string for
system mode.

### `IEventPublisher` takes the organization as a parameter, not from ambient context

`company.followup.due` had to start filling 40.3's envelope `organizationId`. The publisher is a
singleton and `ITenantContext` is request-scoped, so it cannot read the tenant itself — and it
should not: the producers that matter here are background jobs, where the tenant is a property of
the unit of work rather than of the caller. `PublishAsync` therefore grew an optional
`organizationId` parameter, defaulting to `null` for genuinely platform-global events, which also
keeps every pre-40.12 call site compiling and behaving identically.

### The migration does no index work at all

learning's and ai's 40.10/40.11 migrations omitted `CREATE INDEX` and left it to a concurrent
script. company-service goes one step further and omits the `DROP INDEX` too. The old child-table
indexes were `("CompanyId", <time>)` and doubled as the index the cascade-delete foreign key needs;
their organization-first replacements do not serve that FK. Had the migration dropped them,
deleting a company between the deploy and the index script would have sequential-scanned four
tables. The script creates the replacements *and* a plain `("CompanyId")` index per child table,
verifies `pg_index.indisvalid`, and only then drops.

## Phase 40.11 — ai-service across three stores (2026-08-15)

### Mongo sessions get a repository, not a convention

`dialog_sessions` had four readers (`DialogService`, `VoiceDialogService`, `VoiceUsageService`, and
an unused `GetSessionByIdAsync`), each building its own `Builders<DialogSession>.Filter`. Adding an
`organizationId` clause to each would have been the smaller diff and the wrong shape: Mongo has no
row-level security, so unlike every Postgres table in this codebase there is no second layer to
catch the call site that forgets. **Decision:** one `DialogSessionRepository`, taking
`ITenantContext` in its constructor, holding the only `GetCollection<DialogSession>` in the
service, exposing no method that accepts an organization or returns "all organizations".
`MongoDbContext` stopped exposing the collection entirely, and a unit test asserts against the
source tree that exactly one file names it — because nothing in C# enforces "sole holder".

**Alternative considered and rejected:** a convention plus a code-review checklist. The block's own
requirement was "one place to audit"; a convention is auditable only by reading every file that
might have violated it.

### An unset tenant on a session read raises; on the verdict cache it degrades

Both are "fail closed", but they fail closed differently, and the difference is deliberate.

A session read with no organization **throws** `InvalidOperationException("Organization context is
not set.")` — the same wording as `TenantSaveChangesInterceptor`, so operators grep once. Returning
an empty list would also be safe, and that is exactly the problem: a misconfigured gateway would
present as "my history disappeared" rather than as an error, and would survive to production.

The custom-scenario verdict cache with no organization is **skipped**, read and write. The file
already documents Redis there as an optimization and never a dependency (a `RedisException` falls
through to the model), so an unset tenant degrades the same way an unreachable Redis does — one
extra model call. The data at stake is a verdict about the caller's own text, not something another
organization put there. What must never happen — reading a key with no owner — does not.

### No system-mode bypass on the session repository

`ITenantContext.IsSystem` exists and `TenantSaveChangesInterceptor` honours it. The repository does
not. Nothing in ai-service reads sessions outside a request today — both Kafka consumers touch
Postgres only — so a system hatch would be an escape hatch with no user, in the one class whose
entire purpose is to not have one. A future background reader must add an explicitly reviewed
method; roadmap 40.14 (the background-job registry) is where that argument belongs.

### ai-service `UserReplica` stays platform-global in 40.11

`UserReplicas` is a projection of identity's users, maintained by `UserReplicaConsumer` — a Kafka
consumer with no request and therefore no ambient tenant. Giving it an `OrganizationId` means
deciding a tenant per message, which is the identity/consumer audit in 40.13, not this block.
learning-db made the same call in 40.10 and consistency between the two replicas is worth more than
a partial fix here. Its only cross-organization reader is the SuperAdmin voice-usage screen, whose
*rows* are now organization-scoped, so no user outside the caller's organization appears in it.

### The SuperAdmin voice-usage report becomes organization-scoped

`GET /admin/voice/usage` used to aggregate every session in the installation. Under the repository
it aggregates the caller's organization. This is a visible behaviour change to a staff-only screen,
accepted rather than special-cased: a cross-tenant total is precisely the leak this block exists to
close, and 40.9 already ships the legitimate way to see another organization's numbers
(impersonation, which mints a token carrying that organization). The org-scoped admin surface is
40.20.

### Old Redis keys expire, they are not flushed

Two options for keys written before the `org:` prefix: delete them, or let them age out. **Decision:
let them age out.** The requirement is that a stale un-prefixed key is never *read* again, and the
new key shape guarantees that on its own — no code path can produce the old shape. A flush would
buy nothing for correctness and would cost real behaviour: `FLUSHDB` on a shared Redis takes out
every other service's keys, and a targeted `SCAN`+`DEL` is an operation on live infrastructure for
data that is already unreachable.

Two consequences are accepted explicitly. Voice quota counters restart for the current day/month
window, so a user can reserve up to one extra window's quota once — bounded, self-healing, and
identical under either option. And `RedisIdempotencyStore` keys change shape *only for events that
carry an organization*: a redelivery of an already-handled org event can be processed a second time
within one TTL of the deploy. Every handler in this codebase is idempotent by construction.
Platform-global events keep the historical `idem:{group}:{eventId}` key precisely so their dedupe
survives untouched.

### The idempotency organization comes from the envelope, not from `ITenantContext`

`RedisIdempotencyStore` is a singleton and `ITenantContext` is scoped, so injecting the context was
never available — but the reason to prefer the envelope stands on its own: a consumer's tenant is a
property of the message it is holding, and reading it from ambient state would be a second source
of truth that can disagree with the payload. The organization is therefore an optional parameter on
`IIdempotencyStore`, passed by `EventMessageProcessor` from `EventEnvelope.OrganizationId`.

### `40.11` index rebuilds and the Mongo backfill are operational steps

Same reasoning as 40.10, and the same split: the EF migration adds columns and RLS policies only.
`CREATE INDEX CONCURRENTLY` cannot run in a transaction and a transactional build would take an
`ACCESS EXCLUSIVE` lock during `Database.Migrate()` at startup. The Mongo backfill is separate for
a different reason — it is the user-visible step. Between the deploy and the script, pre-40.11
sessions match no organization's filter and a user sees an empty history; that window must be
closed by a human who is watching, not by a startup path that might race replicas.

---

## Phase 40.14 — the background-job audit and the isolation acceptance (2026-08-16)

### The audit's own rule: a mode that is inferred is a mode that is absent

40.14 walked every `BackgroundService`, `IHostedService`, Hangfire job and Kafka consumer in
`src/backend` and asked one question of each: *where does this say which side of the tenant boundary
it is on?* Two could not answer, and both were "working" — which is the point. A background worker
has no HTTP request, so nothing populates `ITenantContext` for it; left alone it starts every scope
with an empty context, and an empty context is not "no data", it is "everything the database role
can see". Under the owning superuser that is every customer's rows.

`OutboxRelayBackgroundService` had exactly that shape, and it is the one component the design
genuinely licenses to read across tenants. It opened a scope, left the context blank, and got its
cross-tenant reach as a side effect of emptiness — indistinguishable, reading the code, from a job
that had simply forgotten. The fix changes no behaviour at all: `EnterSystemMode()` on each tick's
scope. **The value is entirely in the declaration.** A licence that is inferred cannot be reviewed,
cannot be grepped, and cannot be distinguished from a bug; and a scope handed to the relay with a
tenant already on it now throws instead of quietly narrowing the relay to one customer.

The corollary is the registry itself ([docs/TENANCY/BACKGROUND_JOBS.md](TENANCY/BACKGROUND_JOBS.md)),
including its third table — the workers that touch no tenant data at all. Listing only the
interesting jobs produces a document whose completeness cannot be checked: a reader has no way to
distinguish an audited "needs no tenant" from a worker somebody missed. The connection warmer, the
topic provisioner and the Prometheus presence gauge are in the registry with their reasons for
exactly that reason.

### `GamificationSettings` is platform-global, and that is a product bug the agent did not invent a fix for

Classifying `GamificationDialogWeightsConsumer` turned up a real defect. It inherited
`RequiresOrganization = true` although its payload mirrors `GamificationSettings` — a single row
with no `OrganizationId` — into a **process-wide singleton** in ai-service. That was wrong in both
directions simultaneously: weights saved by Sellevate staff carry no `org_id` claim, so the envelope
had no organization and every message was rejected, retried and dead-lettered (the setting silently
never propagated); weights saved by one customer's administrator were accepted and then applied to
every customer's dialog scoring anyway.

**Decision: declare the mode, do not redesign the feature.** `RequiresOrganization => false` is the
honest description of what the code does — platform-global configuration, no database access — and
it fixes the first symptom outright. Making the settings per-organization is a schema migration plus
a product call about who owns scoring weights, and inventing that at 3 a.m. in an audit block would
be the agent choosing a product direction. It is written down for the owner instead
(`docs/DONT_FORGET.md`).

### Reads widen for platform staff; writes widen nowhere — now expressed in code, not in prose

The 2026-08-16 platform-mode work stated the asymmetry plainly: `USING` gets the `app.platform_mode`
branch, `WITH CHECK` does not. Postgres therefore enforces it for free. **Mongo has no policies**, and
both chokepoint repositories had a single `TenantFilter()` that returned `Filter.Empty` under
platform mode and then fed `Find`, `UpdateOne` and `DeleteOne` alike — so a validated administrator
could mutate a document in an organization they never named.

Splitting it into `TenantReadFilter()` and `TenantWriteFilter()` was chosen over adding a boolean
parameter or a comment. The boundary is a security property, so it should be visible at the call
site: `SessionOfUserForWriteFilter` reads as a different thing from `SessionOfUserForReadFilter`,
and the next method added to either class has to pick one. A comment saying "remember not to widen
writes" is a rule the compiler cannot hold.

### The system-mode write guard: refuse `Guid.Empty`, allow an explicit organization

`TenantSaveChangesInterceptor` returned immediately in system mode, so a tenant-scoped entity could
be created carrying the default `Guid.Empty` — a row owned by no organization, visible to none, and
unattributable forever. No path reaches it today.

It was added anyway because of the shape of the `dialog.evaluated` bug found in the same review: a
consumer throwing "carries no organization" has one very cheap wrong fix, `RequiresOrganization =>
false`, which resolves the exception by moving the handler into system mode. That turns a loud,
correct failure into silent zero-organization data. The guard makes the shortcut fail too. It
deliberately does **not** require an ambient organization in system mode — that would defeat the
mode's reason for existing — only that a *created* tenant-scoped row name its organization
explicitly, which is precisely the auditable act being asked for.

### What the security review found and what was deliberately left alone

The `security-reviewer` pass returned zero critical findings; five were fixed in commit `af7ff0e`.
Three were left, and the reasoning is the point:

- **Cross-checking `X-Organization-Id` against the JWT `org_id` claim** (defence in depth). The
  gateway already strips and re-adds the header from the validated token, so a client cannot forge
  it through the front door; the finding is about reaching a service port directly. The fix is a few
  lines, but it changes behaviour at the authentication boundary for callers the agent cannot
  exercise — platform staff hold no `org_id` claim, and Rule №3 forbids writing the tests that would
  prove the change safe. Recorded rather than merged.
- **ai-service has RLS-protected tables and no `TenantTransactionScope`.** Four services ship that
  helper so bare reads get a transaction for `SET LOCAL` to scope to; ai-service ships neither, so
  the day it connects as `sellevate_app` an organization's own dialog modes go invisible. Real, and
  fail-closed. It is a multi-site change to 40.11's service with no test coverage available, and it
  cannot bite before the role rollout — which is a human step that is already gated. Recorded as a
  prerequisite of that rollout.
- **`POST /demo/token` mints a valid signed JWT and every compose file sets
  `ASPNETCORE_ENVIRONMENT=Development`.** Serious, and outside the tenant boundary: the demo token
  carries no `role`, no `org_id` and no `org_role`, so `TenantContext` stays unset and every filter
  yields zero rows — fail-closed exactly as designed. The fix is a deployment-configuration decision
  whose blast radius (error pages, logging, CORS) the agent cannot validate without running the
  stack. Recorded as the top item for the owner.

### The end-to-end two-organization acceptance test was not written

The roadmap's own wording for 40.14 asked for it. Rule №3 (`docs/DONT_FORGET.md`, introduced by the
owner on 2026-08-16) forbids writing new tests of any kind. The rule wins: the block's item is marked
`[~]` rather than quietly satisfied by something weaker, and the gap is listed under "Тесты,
которых нет". Documentation is not tests, so the acceptance *checklist* in
`docs/TESTING/TENANCY.md` was written and is the deliverable that shipped in its place.
