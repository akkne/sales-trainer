# Production audit — sellevate.site, 2026-08-20

A browser-driven sweep of the deployed app as superadmin: every route under `(main)`, `(org)` and
`(admin)` was loaded and every request it made was checked for a non-2xx status, then each failure
was reproduced against the API before being called a bug.

## How the sweep was run

The Chrome extension's network log turned out to keep stale entries across navigations, which
produced one phantom failure (see "Not bugs" below). The reliable probe is the page's own
Performance API, read after the page settles:

```js
performance.getEntriesByType('resource')
  .filter(e => e.name.includes('api.sellevate.site') && e.responseStatus >= 400)
  .map(e => e.name.replace('https://api.sellevate.site', '') + ' -> ' + e.responseStatus)
```

Run that in the page console (or via `javascript_tool`) after each navigation. `responseStatus` is
the real status the browser saw, including for cross-origin responses.

---

## Fixed

### 1. `GET /admin/program/enrollments` answered 500 — `/org/program`

**Symptom.** The org panel's Programme screen showed "Никто не зачислен" (nobody enrolled). That was
not an empty list: the request behind it failed with 500 and the screen rendered its empty state over
the error.

**Cause.** The query ordered by a member of a constructor-projected record:

```csharp
.Join(..., (enrollment, version) => new ProgramEnrollmentDto(...))
.OrderBy(enrollment => enrollment.EnrolledAt)   // untranslatable
```

Npgsql cannot translate that, so it threw `InvalidOperationException` at execution. The in-memory
provider the unit tests use translates every LINQ shape, which is why no test caught it.

**Fix.** `ProgramEnrollmentService.BuildEnrollmentListQuery` orders on the entity, ahead of the
projection.

**Regression guard.** `Learning.Tests/Unit/ProgramEnrollmentQueryTranslationTests.cs` builds the real
service query against an Npgsql-configured context and calls `ToQueryString()` — this needs no
database, because translation fails before any connection is opened. One test asserts the shipped
shape still fails, so "the ordering reads better at the end" cannot quietly bring the 500 back.

**Verify in the app.** Open `/org/program` as an org admin and confirm the enrollments panel loads
without a 500 in the Performance-API probe above.

### 2. Profile screen reported 0% accuracy and 0 skills

**Symptom.** `/profile` showed "Точность 0%" and "Навыки 0" for an account whose completed lessons
averaged 94% (the figure `/tree` shows for the same person). `/friends/:userId` showed "Средний балл
0%" for everybody, and the admin user modal showed 0 XP / 0-day streaks / 0-0 skills / avg score 0.

**Cause.** `GET /profile` is identity-service, which hard-codes all learning aggregates to `0`:
identity stopped owning learning data at the microservices split, and nothing replaced the read. The
same is true of `averageExerciseScore` on social-service's `PublicProfileDto`. The screens read the
fields as if they were real.

**Fix.**

- New `GET /skills/progress-summary` in learning-service — the service that owns the rows — returning
  `{completedSkillCount, totalSkillCount, completedLessonCount, averageExerciseScore}`. Accuracy is
  the mean `bestScore` over completed lessons, matching the tree's per-skill definition, and is
  `null` (rendered "—") when nothing is completed. Both skill counts cover enrolled skills only, so
  the fraction means the same thing as on the tree.
- `/profile` reads accuracy and skills from that endpoint.
- The fabricated tiles are gone from the friend profile and the admin user modal rather than wired to
  fake data. Publishing another learner's real score is a product decision nobody has taken.
- `ProfileService` and `docs/API_CONTRACTS.md` now say in writing that those DTO fields are zeros and
  must not be rendered.

**Regression guard.** `Learning.Tests/Unit/SkillTreeProgressSummaryTests.cs` — the average ignores
in-progress lessons, "nothing completed" yields `null` rather than `0`, and the skill counts exclude
skills the learner is not enrolled in.

**Verify in the app.** `/profile` "Точность" must equal what `/tree` shows for the same account
(both are the mean best score over completed lessons); a brand-new account must show "—", not "0%".

---

## Not bugs (checked, ruled out)

- **`POST /tracking/events` → 503.** Seen on every page load in the first pass, then never again:
  the extension's network log had cached one stale entry. Direct probes returned 204 across every
  whitelisted page value, concurrently and serially, and the Performance API reported 204 on fresh
  loads. Transient at worst; analytics is best-effort and swallows failures by design.
- **`GET /organizations/profile` → 404 on `/org/profile`.** The organization has not filled its
  profile in yet; the screen renders the questionnaire, which is the correct state.
- **Expired access token.** A 401 on every request is followed by `POST /auth/refresh` → 200 and a
  retry of each call. The refresh flow works; no user-visible interruption.
- **`/admin/bulk-lessons` redirects to `/admin/lessons`.** Deliberate consolidation, not a dead route.
