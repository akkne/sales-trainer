# Assignments: the РОП → manager loop, and AI inside the admin panel

**Status:** §1 is **built** (Phase 40.21 — the entity, its progress table and the РОП's CRUD), so is
§1.1 (Phase 40.22 — the completion-rule vocabulary and its evaluation), so is §1.3 (Phase 40.23 —
issuing, the manager's screen and the three notices), so is §2.1 (Phase 40.24 — automatic repeats),
so is §4 and §4.1 (Phase 40.25 — the funnel, the heat map, the quotes and the two-way loop), and so
is **§5's second mine (Phase 40.26 — the day-before digest to the РОП, the one-click reminder behind
it, and the dispute push 40.25 could not send)**, as API and as data. Everything else on this page is
still design: §2/§3 the AI pipeline is Stage F (40.27+), and §5's *first* mine — the sufficiency gate
on thin input — is 40.28.

**One thing to read §4 with:** 40.25 shipped the whole of it as endpoints and one new table, and the
РОП has no screen to see any of it on. The admin panel split is 40.20 and it is waiting on the
owner's design — the same reason 40.15–40.24 shipped without a frontend. What did ship on the
manager's side is the half that is theirs: the dispute button and the review inbox.

Parent doc: [TENANCY.md](TENANCY.md). Sibling: [CONTENT_MODEL.md](CONTENT_MODEL.md).
Schema as built: [DB_SCHEMA.md](../DB_SCHEMA.md#assignments-assignmentprogressrecords).
Routes as built: [API_CONTRACTS.md](../API_CONTRACTS.md#assignments-phase-4021).

The driving scenario: **a sales team has just had a training session** (their own, internal, not
Sellevate's). The knowledge decays in two weeks unless it is practised. The РОП must be able to
turn that training into targeted practice for their team in minutes, and see who actually did it.

---

## 1. The `Assignment` is a new entity, not a repurposed learning path

The existing skill tree is a **long, sequential, self-paced curriculum**. What is needed after a
training is **short, targeted, group-wide, with a deadline**. These are different objects and
forcing the second into the first produces a bad version of both.

```
assignment
  id, organization_id, created_by
  title, goal
  source_type          -- training | manual | gap_detected
  source_ref           -- which training upload, or which metric triggered it
                       --   training     → lesson-version:<uuid>   (frozen, never lesson:)
                       --   manual       → NULL, by CHECK constraint
                       --   gap_detected → skill-gap:<stage>@<yyyy-MM-dd>   (40.31, §3.4)
  content jsonb        -- [exercise refs, dialog scenario refs, theory refs]
  audience             -- user_ids | group_id | whole_team
  opens_at, deadline
  completion_rule jsonb
  repeat_schedule jsonb
  status               -- draft | active | closed

assignment_progress
  assignment_id, user_id
  status               -- not_started | in_progress | completed | failed_threshold
  best_score, attempt_count, first_opened_at, completed_at
```

### 1.1 Completion is a quality threshold, not a click

`completion_rule` is the design's load-bearing detail. Examples:
`3 dialogues scoring ≥ 70`, or `exercise accuracy ≥ 80% across the set`.

If completion means "opened everything", managers will click through in four minutes, the
dashboard will read 100%, and the number will be a lie the РОП eventually catches. That single
choice is the difference between a training tool and a compliance-theatre tool.

Corollary: a failed threshold is a **normal, visible state** (`failed_threshold`), not a hidden
retry. The РОП needs to see "started, tried 4 times, still under threshold" — that person needs
coaching, and it is the most valuable row on the screen.

#### What 40.22 built, and the four forks it had to settle

The two examples above became the whole vocabulary — `dialog_score` and `exercise_accuracy` — and
nothing else is accepted. Rejected alternatives are in [DECISIONS.md](../DECISIONS.md) (2026-08-18);
the parts that change how the rest of this page should be read:

- **A rule says what one attempt is and what bar it clears**, which is what lets one progress row
  carry the whole verdict in two numbers. `dialog_score` counts conversations that each cleared the
  bar rather than averaging them, because an average lets one strong call carry two weak ones and the
  skill being trained is doing it right repeatedly. `exercise_accuracy` counts *submissions*, so
  brute-forcing a set until everything is green lowers the number instead of raising it, and it
  withholds a score until every exercise has been attempted, because one lucky answer out of twenty
  is otherwise 100%. Those two details are where "not a click" actually lives; the rest is bookkeeping.
- **The evaluation reuses the existing scoring, and one contract had to be widened for it to.**
  Exercise correctness comes from the same `UserExerciseAttempt` rows the ordinary submit path
  writes, and accuracy is the definition `LessonAccuracyService` already reports. Dialogues were the
  gap: `dialog.evaluated` carried `rawScore`, which despite the name is the pre-multiplier XP reward
  and says nothing about how the conversation went, while the 0–10 grade the learner sees never left
  ai-service. 40.22 added `qualityScore` (normalized 0–100) and `modeKey` to that event.
- **`in_progress` and `failed_threshold` are separated by whether the *work* is finished**, not by
  whether the bar was cleared. Somebody who has done two of three required conversations is
  unfinished; somebody who has done four and cleared none is the row §4 is about. Collapsing the two
  would hide the person who needs coaching among the people who have not started, which is the exact
  failure this section exists to prevent, arrived at from the other side.
- **Everything is recomputed from attempt rows, never incremented.** A graded conversation is stored
  once (`UserDialogScores`, unique on organization + user + session) and the two numbers on the
  progress row are derived from what exists. That is what makes an at-least-once Kafka redelivery
  harmless — and an attempt count that inflates while nobody practises would poison precisely the row
  the РОП is supposed to act on.

The thing that carried into §1.3: **`AssignmentProgressRecords` had no row *creator*.** 40.22 wrote
the updater — what moves a row between the four states — but a row's existence means "this person was
asked", and that is 40.23's fan-out. It has since shipped, so the funnel counts below are real and
threshold evaluation runs over a non-empty set.

### 1.3 What 40.23 built: the moment a rule becomes people

Three things at once, because they are one act seen from three sides — issuing, being told, and
having somewhere to see it.

**The audience is resolved by asking identity-service, once, at issue time.** learning-db holds
`UserReplicas`, which is platform-global and says nothing about who belongs where, so it cannot
answer "who works here" on its own. `GET /internal/memberships/active` returns the calling
organization's active member ids — ids only, guarded by the shared-secret filter ai-service already
uses on its internal routes and by `[TenantScoped]`. The whole-team rule becomes that list; a named
list is **intersected** with it; a group is refused, because no group exists in the platform yet and
"the new hires" quietly becoming "everybody" is not a surprise a product survives.

The alternative — a `membership.*` Kafka family and a replica table in learning-db — is what every
other cross-service read here does, and it was rejected on its failure mode rather than its shape: a
replica that lags or has never been backfilled resolves the whole team to nine people out of forty,
issues to nine, and reports success. Nothing errors, and nobody finds out. The synchronous call fails
loudly instead — a 503 on the button that was just pressed. Full argument in
[DECISIONS.md](../DECISIONS.md) (2026-08-18).

**One `not_started` row and one `assignment.issued` event per recipient, in one transaction.** The
outbox is what makes "was asked" and "was told" atomic. Editing a running assignment's audience
re-resolves and **tops up**, never removes: 40.21 left the audience editable on purpose, and a
progress row is the record that somebody was asked, which deleting would rewrite. That top-up is also
the answer to a person hired after the issue — re-saving the assignment brings them in, and nothing
back-dates them.

Somebody who leaves keeps their row (history) and stops being contacted (the deadline sweep and every
new fan-out check the live roster first). The honest cost is that a leaver counts as "not started" in
the funnel until §4's screen has a way to say so — recorded in
[DONT_FORGET.md](../DONT_FORGET.md).

**The manager's screen is a strip above the learning path**, `GET /assignments/active`: their own
unfinished assignments, soonest deadline first, naming the *bar* rather than a status word — a manager
who cannot see the threshold cannot aim at it. `failed_threshold` is tinted rather than hidden, for
the reason §1.1 gives. With no assignments the strip renders nothing at all and the home screen is
byte-for-byte what it was, because the roadmap's requirement is that an assignment take the top of the
screen, not that it replace it.

**Three notification families** — issued, deadline approaching, reminder — as three types rather than
one with a discriminator, since the recipient reads them differently and the third exists precisely
because the first two were ignored. The deadline sweep lives in learning-service (which owns
assignments, deadlines and progress; notification-service has no database) and runs as
per-organization iteration over a system enumeration, per
[BACKGROUND_JOBS.md](BACKGROUND_JOBS.md) §4e. The notice goes to the person who owes the work; the
РОП's "who has not started" digest arrived in 40.26 as the second half of the same tick (§5.1).

### 1.2 What 40.21 built, and the five forks it had to settle

The sketch above named eleven columns; turning it into a schema forced five choices the sketch left
open. All five are recorded with their rejected alternatives in [DECISIONS.md](../DECISIONS.md)
(2026-08-18); the short version, because it changes how the rest of this page should be read:

- **`content` holds references, never exercise bodies** — specifically a pinned `LessonVersion` for
  the exercise set, an ai-service dialog mode key for the conversation, and a `ReferenceMaterials.Id`
  for theory. That is what makes §6's "no new renderer needed" literally true rather than aspirational:
  an assignment's exercises *are* ordinary exercises, played by the existing screens and graded by the
  existing scoring. It is also why a recorded score always describes content somebody can still read.
- **`audience` stores the rule, not the resolved people** (`whole_team` / `users` / `group`). The
  employee list lives in identity-service; a resolved list in learning-db would be a stale copy of it.
  The resolution is 40.23's, and its output — the progress rows — is the authoritative record of who
  was actually asked.
- **`source_ref` names a frozen version, never a lesson.** `lesson-version:<uuid>`, because a
  `lesson_id` silently re-points at whatever the lesson has become — the defect 40.16 spent a block
  removing from progress.
- **`completion_rule` is required and has no default.** §1.1 above is the reason: a default would have
  to mean "no threshold", and that is the compliance-theatre failure with a resting place in the
  schema. What 40.21 asserts is only that a rule names its `kind`; the vocabulary is 40.22's.
- **`status` is `draft → active → closed`, one-way, enforced by a database trigger**, and issuing
  freezes what the assignment asks for while leaving who, when and what-it-is-called editable. Adding
  three people to a running assignment and extending a deadline are ordinary acts; rewriting the
  threshold under people who already have scores is not.

The one thing to know when reading §2–§5 below: **`assignment_progress` had no row creator until
40.23.** 40.21 built the table, 40.22 wrote what updates a row, and 40.23 (§1.3 above) writes the
rows at issue time — so the funnel counts in §4 are now real numbers rather than honest zeroes.

---

## 2. The post-training flow

1. РОП uploads the training materials — deck, notes, a recording, or just pasted text.
2. AI **structures** it and stops: *"This training is about price objection handling. Techniques:
   A, B, C. Objections covered: …"*
3. РОП corrects two lines and presses **create assignment**.
4. Generation produces exercises + a dialogue scenario with a persona configured for exactly those
   objections + the grading criteria.
5. Assign to the team, deadline 5 days.
6. Managers are notified; the assignment sits at the top of their home screen until done.
7. РОП gets the assignment dashboard.

This reuses the pipeline that already exists in `.claude/local-seed/seed.py` — *structure the raw
text, then generate content from the structure* — and moves it from a developer script into the
product, with a human checkpoint inserted between the two phases (§3.1).

### 2.1 Repetition is the whole point

Training effect decays in two to three weeks. That decay is the actual reason internal trainings
don't stick, and a one-shot assignment reproduces the failure.

`repeat_schedule` lets an assignment automatically re-issue a shortened version at +7 and +21 days
— configured once, then automatic. This is the mechanism that turns one-off trainings into
recurring practice; without it, the claim is a slogan.

#### What 40.24 built, and the four things that change how the rest of this page reads

The vocabulary is one kind — `{"kind":"fixed_offsets","offsetDays":[7,21]}`, with the list optional
and defaulting to exactly those two numbers — and a background sweep (`AssignmentRepeatSweepService`,
[BACKGROUND_JOBS.md](BACKGROUND_JOBS.md) §4f) that acts on it. Eleven forks are recorded with their
rejected alternatives in [DECISIONS.md](../DECISIONS.md) (2026-08-18); these four are the ones that
change what the paragraphs above and below mean:

- **A wave is a new `Assignment` row, not a second round inside the old one.** The squashed variant is
  the one that keeps "one assignment, one funnel" literally true on §4's dashboard, and it is
  unbuildable on this schema for a reason that is the whole point of §1.1: `assignment_progress`
  carries exactly one `best_score` per person, deliberately, so a second wave's result would have to
  overwrite the first's — destroying the only evidence anybody had that the training decayed, which is
  the fact this section exists to surface. The link is `repeat_of_assignment_id` plus a 1-based
  `repeat_wave_index`, so §4 can group the waves into one series and show the comparison. A repeat
  never points at another repeat: the series is one level deep and the database refuses a repeat that
  carries a schedule of its own.
- **The offsets are measured from the origin's issue moment, and the cohort moves together.** Anchoring
  per person — at the moment each of them cleared the threshold, which is what a spaced-repetition
  textbook would say — needs one assignment per person, and forty people who passed on six different
  days become six funnels the РОП cannot read. The unit they act on is a team meeting; «планёрка в
  понедельник» in §4 is the same observation from the other side.
- **The repeat goes to the people the origin was issued to**, intersected with the live roster —
  not to a fresh resolution of the audience rule. `whole_team` re-resolved three weeks later hands a
  *shortened* refresher to everybody hired since, i.e. the practice with the theory already stripped
  out of it, and it changes the denominator between waves, which is exactly the comparison the series
  exists to make. Outcome does not filter it either: the person who tried four times and stayed under
  the bar is in §1.1's words the most valuable row on the screen, and a repeat that skipped them would
  be the product silently giving up on the one person who needs it.
- **"Shortened" means less repetition and less theory, never a lower bar.** The `reference_material`
  items are dropped (kept only when they are all the assignment has) and `dialog_score.required_count`
  is halved, rounded up; the score bars are copied untouched. Lowering a bar to make the repeat easy
  would make the two waves' numbers incomparable, which costs the series its only purpose — and it
  would put the four-minute completion §1.1 is written against back within reach.

Two consequences worth stating plainly, because both look like bugs from the outside. **A closed
assignment still repeats** — a five-day assignment is supposed to be closed by day 7, and a repeat
that died when the РОП tidied up would be a feature that only works for people who never close
anything. The way to cancel a series is therefore to clear or shorten `repeat_schedule` **while the
assignment is still active**; once closed it is frozen with everything else, and the remaining waves
will fire. And **a wave more than three days late is dropped rather than delivered**, because the value
of spaced repetition is the spacing: a "+7 day" refresher arriving at +16 is not the feature arriving
late. Both are in [DONT_FORGET.md](../DONT_FORGET.md).

---

## 3. AI inside the admin panel

The point is not generation — that already works. The point is that **the РОП must never see an
empty editor.** Every content-creation screen should start at "upload your material" and end at
"review and correct".

### 3.1 A checkpoint between structuring and generation — highest value, lowest cost

Show the intermediate structure before generating anything:

> Extracted: product — X. 7 objections: «дорого», «уже есть поставщик», … 4 script stages.
> Tone: business-formal.
> **Correct?** What should be removed, what is missing?

A correction here costs 30 seconds. The same correction after generation is a rewrite of 15
exercises. It is also cheaper in tokens — nothing is generated that will be thrown away.

### 3.2 The organization profile as an interview, not a form

Thirty empty fields ([CONTENT_MODEL.md §3](CONTENT_MODEL.md#3-the-organization-profile--the-part-that-removes-most-forks))
is an hour of work nobody will spend, so the profile stays empty and every lesson stays generic.

Instead: the РОП uploads a product deck and their call script; AI fills in what it can and asks
only about the gaps. Five minutes, and the profile is populated — which is what makes parameterized
base content work at all.

### 3.3 Real call recordings → an objection library

The customer already has recordings sitting in their telephony system. Run them through and
extract: which objections actually occur and how often, how the top performers handle them, where
everyone fails.

The output feeds both the content and the persona configuration for the simulator. Commercially
this is the strongest of these ideas: the training content is assembled from the customer's own
reality rather than being a generic sales course. It also makes the objection frequencies real
instead of guessed.

(Consent and recording-retention terms have to be settled before this ships — see the data
questions in [TENANCY.md §1.8](TENANCY.md#18-mongo-dialog-sessions-and-redis).)

### 3.4 Closing the loop from metric to content

The dashboard sees the team failing on price. The admin panel itself offers:
*"Generate 5 exercises on this + a dialogue with a persona who pushes hard for a discount?"* —
one button, `source_type = gap_detected`, `source_ref` = the metric.

This is what turns the dashboard from a report into a tool. A report gets opened quarterly; a tool
that proposes the next action gets opened weekly.

#### What 40.31 built for §3.4, and the five things that change how the rest of this page reads

Four routes, one table, one column. Six forks with their rejected alternatives are in
[DECISIONS.md](../DECISIONS.md) (2026-08-18); these are the ones that change what the paragraphs
above and below mean.

- **«Провал команды» is three conditions, not one number.** A stage of the funnel qualifies when it
  has at least **20 attempts** inside the window, team accuracy **at or below 60%**, and **at least
  two managers** with a reportable cell at or below that bar. All three are the agent's product
  decisions, calibrated against numbers the product already states: 40.25's five-attempt floor for a
  single cell (20 is four of those), 40.22's own example of a passing bar (80%, so 60 is twenty
  points below "needs practice" and reads as "the team cannot do this"), and the difference between
  a coaching conversation and a content decision (one weak manager is already named by
  `TeamSkillMapMemberDto.WeakestStageKey`).
- **The suggestion is computed, never stored.** It is derived from the *same* call that draws the
  heat map, so the panel and the matrix cannot disagree about the same window, and a gap that closes
  stops being offered without anything having to extinguish a row. The only thing 40.31 stores is a
  **refusal** — `TeamSkillGapDismissals`, one live row per stage — because "a person said no" is the
  one fact the attempt rows do not imply. Same call 40.18 made for staleness and 40.25 for the funnel.
- **`source_ref` for a metric is `skill-gap:<stage>@<yyyy-MM-dd>`.** The stage half is the identity
  — a second observation of the same weak stage next week is the same gap — and the date half is the
  evidence. The numbers themselves are **not** in the reference: they are written into
  `Assignment.Goal` at creation, which is what keeps a year-old row readable when the window that
  produced it has long since rolled past.
- **The button starts a 40.27 run, not an assignment.** There is no unreviewed model output in the
  team's live tree at any point: the run stops at the same checkpoint every run stops at, and the
  lesson it produces arrives archived. The assignment appears at the end, from
  `POST /admin/assignments` with `contentGenerationJobId` — and that route **derives** `source_type`
  and `source_ref` from the run rather than believing the body. A client cannot label hand-written
  work as detected by the dashboard, and a client that forgets to label generated work cannot lose
  the link. It also gave `training` its first writer: a pasted-material run produces
  `source_type = training`, `source_ref = lesson-version:<uuid>`, which is what §1 said that value
  meant and what nothing had yet written.
- **A suggestion that was refused comes back, and a suggestion being worked on does not repeat.**
  A dismissal lasts **90 days** — the heat map's own default window, so a refusal lives exactly as
  long as the measurement that provoked it could still be the same measurement — and is broken early
  if the number falls **10 points** below what it was when the refusal was recorded. A live run
  holding a stage's reference suppresses that stage outright, and pressing the button anyway
  returns **that run** rather than buying a second lesson. Every suppressed gap is reported with its
  reason and its expiry, because a panel that merely shows nothing is indistinguishable from a
  broken one.

**What 40.31 did not build:** the persona half of the roadmap's own sentence. «Диалог с персоной,
которая давит на скидку» would be a generated `DialogMode`, and dialog modes live in ai-service with
no generation path of any kind — building one is a second 40.27, not a corner of this block. The
suggestion therefore proposes exercises; the dialogue is an ordinary `dialog_scenario` content item
the РОП adds to the same assignment from the modes that already exist. Recorded in
[DONT_FORGET.md](../DONT_FORGET.md).

### 3.5 Batch tone adaptation

*"Rewrite every exercise in the «закрытие» stage for our product and tone"* → background job →
a list of diffs → accept/reject one by one. Never auto-applied. The РОП owns what their team reads.

### 3.6 AI review of human-written content

When the РОП writes an exercise by hand, check it: is the correct answer actually unambiguous, are
the distractors too obvious, are the free-text grading criteria measurable?

This is quality control that costs Sellevate no staff time — and without it, the customer's weak
content becomes Sellevate's perceived quality.

---

## 4. What the РОП sees

- **Per assignment:** the funnel — assigned → started → completed → met threshold
- **Per manager:** where exactly they fail, mapped to the sales-funnel stage
- **Per team:** a skill heat map
- **Quotes from the actual dialogues**, not only numbers

The last one is underrated. «68 баллов» is not actionable to a РОП. Three lines where a manager
gave away the price *is* — it is ready-made material for Monday's team meeting. That is the feature
that makes them open the product every week rather than every quarter.

#### What 40.25 built for §4, and the six things that change how the rest of this page reads

Three endpoints and one table. Nine forks are recorded with their rejected alternatives in
[DECISIONS.md](../DECISIONS.md) (2026-08-18); these are the ones that change what the paragraphs
above and below mean.

- **The funnel has five stages, not four, and the fifth is the whole point.** `GET
  /admin/assignments/{id}/dashboard` reports assigned, not started, started, completed *and*
  `failedThresholdCount` separately. §1.1 argues that «начал, пробовал 4 раза, не дотянул» is the
  most valuable row on the screen; a four-stage funnel ending at "completed" puts those people back
  among the people who never started, which is the exact collapse 40.22 separated the two states to
  prevent — reintroduced by the screen that exists to show it. The rows come back named and ordered
  worst-standing first, so the screen opens on the people something needs doing about.
- **A leaver is marked rather than deleted or counted, which closes the cost §1.3 recorded.** The
  dashboard asks identity-service who still holds an active membership and annotates each row, then
  reports `leftOrganizationCount` and `assignedActiveCount` next to the raw counts. The row itself
  still stays — it is the record that somebody was asked — but the arithmetic no longer punishes a
  team for somebody's departure. Filtering leavers out entirely was rejected: a five-person team
  where two people left is a different situation from a three-person team, and the РОП is the one
  who knows which they are looking at. **This roster read is fail-open**, unlike the one at issue
  time: `null` means "we could not check", which is not zero, and the funnel is still true without
  it.
- **The wave series is on the same response.** A repeat is its own row with its own funnel (§2.1),
  and the dashboard returns every wave of the series with its own five counts. That comparison is
  the entire reason 40.24 built waves as separate rows; making it a second request would make it a
  comparison nobody performs.
- **«Этап воронки продаж» is `Skill.Stage`, the vocabulary the skill tree already has.** `GET
  /admin/team/skill-map` returns one matrix read along both axes the roadmap asks for: per team,
  accuracy per skill; per manager, the stage they sag on. company-service's `CompanyStatus` pipeline
  (39.10) was rejected for it — that is a *deal* pipeline, where a company sits in a rep's CRM, and
  the question here is about a *conversation*. A cell below five attempts reports no percentage at
  all, because two right answers out of two is 100% and is a fact about nobody.
- **The quotes come from ai-service and learning-service never reads Mongo.** `GET
  /admin/dialog-sessions` lists the team's *graded* conversations filtered by manager, scenario and
  an upper bound on the grade — «покажи разговоры на 4 и ниже» is a list somebody takes to a
  meeting — and `GET /admin/dialog-sessions/{id}` returns the transcript with per-message indexes a
  quote can point at. `IDialogSessionRepository` stays the single holder of the collection (§1.6 of
  [TENANCY.md](TENANCY.md)); the screen asks each service for what it owns.
- **`GET /admin/assignments/{id}/progress` is unchanged and stays.** It is the raw, name-free list,
  and the only one of the two that cannot be affected by identity-service being unavailable.

**«Метрики воронки заданий — в `analytics-service`»** resolved to two platform-wide Prometheus
counters — `app_assignments_issued_total` and `app_assignment_progress_total{state}` — fed by
`assignment.issued` and a new `assignment.progress.changed`. It is a counter and not a projection
because [ANALYTICS_SERVICE.md](../ANALYTICS_SERVICE.md) settled the governing rule in 40.16:
analytics is Redis-only, stores no attempts and carries no organization label, since a customer id
as a Prometheus label puts identities and unbounded cardinality into the monitoring store. Analytics
therefore answers *is anybody doing assignments at all*, and the funnel with names stays in
learning-service where the rows are.

### 4.1 Two-way feedback

- The РОП can select a fragment of a dialogue, comment on it, and send it to the manager.
- The manager can **dispute an AI score**, which routes to the РОП for review.

Without the second mechanism, AI grading is a black box, and the first genuinely disputed score
destroys the team's trust in every number the product shows. A dispute path also generates exactly
the labelled data needed to tune the grading prompts.

#### What 40.25 built for §4.1, and the four choices that carry it

**One table, `DialogReviewNotes`, with a `Kind`** — `coaching_note` (РОП → manager, closed by being
read) and `score_dispute` (manager → РОП, closed by a verdict). They are the same object seen from
either end: an annotation on a fragment of one conversation, written by one party and closed by the
other, sharing a session, a quoted fragment, a comment, an author, a subject and a resolution. Two
tables would have duplicated all six and given the tenant column, the policy, the frozen-quote copy
and the freeze rules two places each to be got right. What genuinely differs is which words close a
row, and that is a check constraint rather than a second schema.

**It lives in learning-service, and nothing in it reads ai-service.** The conversation is a Mongo
document one service away, so the obvious home looks like ai-service; the disputed *number* is a
`UserDialogScores` row, which is here and is the value that actually drives an assignment's
threshold. Every write starts from that row: the session id, the manager, the scenario and the grade
are read out of learning-db rather than taken from the request. That is what makes "the РОП cannot
address a note at somebody else's employee" a property of the query — a session belonging to another
organization does not exist to the code that would write the row — and it is why an *ungraded*
conversation cannot be annotated at all, which is the right refusal rather than a limitation.

**The quoted fragment is copied into the row, not referenced.** The whole use for it is three lines
on Monday morning; a note that renders empty because ai-service is slow, or because retention
eventually trims old sessions, is a note that failed at the only moment it mattered. The message
indexes are stored alongside so the fragment can still be found in context while the session exists.

**An upheld dispute records a corrected score and does not apply it.** 40.22 made every progress
number derived from attempt rows and recomputed on every event, so a hand-edited score would be
overwritten by the next redelivery — and, worse, a threshold that can be argued down by the person
being measured is the four-minute completion §1.1 exists to make unreachable, reached by another
route. Whether an upheld dispute should eventually move a grade is a product decision with money and
trust on both sides, and it is in [DONT_FORGET.md](../DONT_FORGET.md) rather than guessed at. The
labelled data the roadmap wants is extracted by
[`docs/TENANCY/sql/40.25_dialog_reviews_verify.sql`](sql/40.25_dialog_reviews_verify.sql) §6.

Two smaller rules with the same shape. **Rejecting a dispute requires a reason and upholding one does
not** — "the grade stands, because" is the sentence that keeps the mechanism from being a rubber
stamp, and agreement needs no defence; both outcomes notify the manager, because a dispute closed in
silence recreates the black box. And **one open dispute per conversation**, by partial unique index:
a queue that can be flooded with duplicates of one complaint is a queue the РОП stops opening. It is
partial, so the same call may be disputed again after a verdict.

Two things 40.25 shipped without, both stated plainly because both looked like bugs from the outside.
**The РОП got no push when a dispute arrived** — notifications are addressed to a user id and nothing
in the platform could enumerate an organization's administrators. **40.26 closed that**: widening
`GET /internal/memberships/active` with an `administratorUserIds` subset (§5.1) gave the platform the
address, and filing a dispute now publishes `dialog.review.disputed` to each administrator except its
own author, carrying the manager's name, the grade they contest and their own sentence. That read is
**fail-open**: identity-service being unreachable costs the notice, never the dispute, because the
row is already written and already in the queue the dashboard reads. And **the РОП still has no
screen** for any of §4 or §4.1; the manager's half — the dispute button in the feedback modal and the
`/dialog-reviews` inbox — did ship. The screen is in [DONT_FORGET.md](../DONT_FORGET.md).

---

## 5. Two mines

**Generation quality equals input quality.** A РОП will upload a three-slide deck and get six
mediocre exercises — and will judge the product, not their input. There must be an explicit
sufficiency gate: the AI **refuses** to generate on thin input and says specifically what is
missing («добавьте примеры возражений или запись звонка»). Four good exercises beat fifteen
watery ones.

**An assignment is coercion.** The РОП assigns it, nobody does it, the product looks dead, the
customer churns. So non-completion must be visible *and* immediately actionable: not a report the
РОП might open, but a notification the day before the deadline listing who has not started, with a
one-click reminder. Adoption does not fail on content quality — it fails on whether the РОП pushes
their team. Design for that.

### 5.1 What 40.26 built for the second mine, and the six choices that carry it

No new table, no new column, no migration, and no new background job — the whole block is one new
capability in identity-service, two notification families, and a scope on a button that already
existed. Eight forks are recorded with their rejected alternatives in
[DECISIONS.md](../DECISIONS.md) (2026-08-18); these six change how the rest of this page reads.

- **The platform can now name an organization's administrators, and that is the block's real
  unlock.** Every РОП-facing notice was blocked on the same missing fact, which is why 40.25's
  dispute push was deferred *here* rather than fixed there. `GET /internal/memberships/active` gained
  `administratorUserIds` — a **subset of the ids it already returned**, not a role per member.
  Returning roles would have answered the question by publishing the organization's role directory to
  every service holding the shared secret, and nothing in learning-service ever asks "what is this
  person"; it asks "who should hear about this". Both tenancy administrator roles qualify: they
  differ only in who may hire and fire, which has nothing to do with who should be told the team is
  missing a deadline.
- **It goes to every administrator, not to `created_by`.** Addressing the author is cheaper and
  quieter and it fails three ways: `created_by` is null on every automatic repeat (40.24), so a
  wave's digest would have nobody to go to; the author may have left, and 40.23 spent a block making
  sure the product does not mail ex-employees; and a nudge aimed at one named person is a nudge that
  dies when they are on holiday, which is precisely the week nobody does the training. The cost is
  real and worth naming: in an organization with five administrators, five people read the same
  digest and four of them assume the fifth will act. That is a diffusion the screen can fix (40.20)
  and the addressing cannot.
- **Nobody has failed to start ⇒ no notification at all.** The alternative — a digest that says «все
  молодцы» — is not neutral, it is the message that teaches a РОП the channel is filler, and a
  channel they have learned to skip is exactly the failure this section is written against. The
  assignment is still stamped as announced, so the silence costs one tick's work rather than a sweep
  that re-examines it every half hour.
- **Only `not_started`, although the sweep knows who is under the threshold.** §1.1 calls «начал,
  пробовал 4 раза, не дотянул» the most valuable row on the screen, and it is — for coaching. A push
  telling that person "you have not finished" is the product being obtuse at somebody who knows
  better than it does. They are on 40.25's dashboard, ordered first, and deliberately not in this
  notice. The roadmap asks for «список тех, кто не начал» and means it.
- **The one-click reminder is a deep link with a scope, and the endpoint behind it works today.**
  `actionUrl` is `/admin/assignments/{id}?action=remind&scope=not_started`, and
  `POST /admin/assignments/{id}/remind?scope=not_started` answers. Two things follow. The link opens
  a screen rather than acting by itself, because a URL in an email that messages a team the moment it
  is opened is a URL a mail scanner can fire. And the **scope had to exist**: 40.23's remind nudged
  everybody unfinished, which was right while the only way to press it was to be looking at the
  assignment — a notice naming five people whose button then messages twelve is the product doing
  something other than what it just said. The screen does not exist yet (40.20 waits on the owner's
  design), which is the reason the parameters are decided here: whoever draws it reads the action out
  of the link instead of inventing one.
- **Two hazards this block created and closed in the same breath.** Putting the button in front of
  every administrator made five presses of the same reminder possible within a minute, so the
  reminder's dedupe key coarsened from the exact instant to the hour — presses on different days
  still reach people, a chorus in one meeting does not. And the reminder now **consults the live
  roster**, fail-closed like issuing: it was the last path in the feature that could still mail an
  ex-employee their former employer's homework.

**Idempotency needed nothing new**, and that is the shape to preserve. `DeadlineNoticeSentAt` — the
column 40.23 added — already answers "have I announced this deadline", the digest describes the same
date as the notices beside it, and moving the deadline already clears the stamp and re-arms both. A
second column would have been a second answer to one question. The one case that forced a decision is
a tick that can read the roster but not the administrators, which means an identity-service older
than this block: it **skips the organization entirely** rather than sending the manager notices and
stamping, because a stamp is permanent and the digest would be gone with nothing left to notice.
Details in [BACKGROUND_JOBS.md](BACKGROUND_JOBS.md) §4g.

**What this block did not do, deliberately.** An assignment whose deadline passes still stays
`active` until somebody closes it — the gap §4c and §4d both recorded as "40.26 owes one". It is not
in this block's three lines, and closing on a timer takes away the ordinary act of extending a
deadline that 40.21 kept editable on purpose. It is in [DONT_FORGET.md](../DONT_FORGET.md) as a
product decision rather than left as a silent omission.

---

## 6. Fit with what exists

| Existing | Reuse |
|----------|-------|
| `notification-service` | assignment issued / deadline approaching / reminder — **done in 40.23**: three topics, three `NotificationType` values, mapped in `NotificationEventMapper` onto the existing `org:{orgId}:` inbox and the generic email template. **40.26 added the two РОП-facing ones** (`assignment.deadline.digest`, `dialog.review.disputed`) the same way — no template, no schema, no new service |
| ai-service dialog modes | the assignment's practice dialogue is a `DialogSession` with an injected persona — **done in 40.23**: `AssignmentPracticePromptBuilder`, the same seam `CompanyContextPromptBuilder` defines. The persona is stored on the `dialog_scenario` content item and **fetched by ai-service** rather than carried by the learner's browser, because the browser belongs to the person being graded against it |
| `analytics-service` | assignment funnel metrics |
| exercise types ([NEW_EXERCISE_TYPES.md](../NEW_EXERCISE_TYPES.md)) | the 11 existing types are the assignment's content vocabulary — **confirmed in 40.23**: a `lesson_version` item links to `/session/:lessonId`, the ordinary lesson screen, and no renderer was added |
| `learning-service` grading | threshold evaluation reuses existing scoring — **done in 40.22**: `UserExerciseAttempt` rows for accuracy, ai-service's own feedback grade for conversations |
| the 40.23 fan-out | a repeat's issue is the same two writes a human-pressed issue performs — **done in 40.24**: one extracted `AssignmentFanOut`, so "asked" and "told" stay atomic on both paths and there is one idempotency story rather than two |

Nothing here needs a new service. `Assignment` most naturally belongs to `learning-service`
(it owns progress and grading), with the AI generation calls going to `ai-service` the same way
learning already calls `/ai/evaluate`.
