# Assignments: the РОП → manager loop, and AI inside the admin panel

**Status:** §1 is **built** (Phase 40.21 — the entity, its progress table and the РОП's CRUD), so is
§1.1 (Phase 40.22 — the completion-rule vocabulary and its evaluation), and so is §1.3 (Phase 40.23 —
issuing, the manager's screen and the three notices). Everything else on this page is still design:
§2.1 repeats are 40.24, §2/§3 the AI pipeline is Stage F (40.27+), §4 the dashboard is 40.25, §5's
non-completion push is 40.26.

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
РОП's "who has not started" digest is still §5's, i.e. 40.26's.

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

### 4.1 Two-way feedback

- The РОП can select a fragment of a dialogue, comment on it, and send it to the manager.
- The manager can **dispute an AI score**, which routes to the РОП for review.

Without the second mechanism, AI grading is a black box, and the first genuinely disputed score
destroys the team's trust in every number the product shows. A dispute path also generates exactly
the labelled data needed to tune the grading prompts.

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

---

## 6. Fit with what exists

| Existing | Reuse |
|----------|-------|
| `notification-service` | assignment issued / deadline approaching / reminder — **done in 40.23**: three topics, three `NotificationType` values, mapped in `NotificationEventMapper` onto the existing `org:{orgId}:` inbox and the generic email template |
| ai-service dialog modes | the assignment's practice dialogue is a `DialogSession` with an injected persona — **done in 40.23**: `AssignmentPracticePromptBuilder`, the same seam `CompanyContextPromptBuilder` defines. The persona is stored on the `dialog_scenario` content item and **fetched by ai-service** rather than carried by the learner's browser, because the browser belongs to the person being graded against it |
| `analytics-service` | assignment funnel metrics |
| exercise types ([NEW_EXERCISE_TYPES.md](../NEW_EXERCISE_TYPES.md)) | the 11 existing types are the assignment's content vocabulary — **confirmed in 40.23**: a `lesson_version` item links to `/session/:lessonId`, the ordinary lesson screen, and no renderer was added |
| `learning-service` grading | threshold evaluation reuses existing scoring — **done in 40.22**: `UserExerciseAttempt` rows for accuracy, ai-service's own feedback grade for conversations |

Nothing here needs a new service. `Assignment` most naturally belongs to `learning-service`
(it owns progress and grading), with the AI generation calls going to `ai-service` the same way
learning already calls `/ai/evaluate`.
