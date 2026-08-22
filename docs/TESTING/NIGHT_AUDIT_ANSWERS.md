# Testing — the four things built from the owner's night-audit answers (2026-08-22)

Covers what shipped for **Q-4, Q-5, Q-6 and Q-8** of
[NIGHT_AUDIT_QUESTIONS.md](../NIGHT_AUDIT_QUESTIONS.md). The other eight answered questions were
decisions **not** to build (Q-3, Q-7, Q-10, Q-11), work for a person rather than code (Q-1, Q-9,
Q-12), or already closed by earlier code (Q-13) — nothing to test there beyond what already exists.

Decision record: [DECISIONS.md](../DECISIONS.md), entry "2026-08-22 — owner's answers".

---

## Automated

```bash
# Frontend — all of it (995 tests), or just the new files
cd src/frontend
npx tsc --noEmit
npx vitest run
npx vitest run __tests__/platformNavigation.test.ts        # Q-5
npx vitest run __tests__/onboardingSkillSelection.test.ts  # Q-6
npx vitest run __tests__/notificationPreferences.test.tsx  # Q-4

# Backend
cd src/backend/notification-service
dotnet test Notification.Tests/Sellevate.Notification.Tests.csproj    # Q-4, 65 tests
cd src/backend/learning-service
dotnet test Learning.Tests/Sellevate.Learning.Tests.csproj \
  --filter "FullyQualifiedName~AdminExercisesReorderTests"            # Q-8, 10 tests

# Tenancy tripwire — the reorder route and the preferences route both touch it
python3 scripts/tenancy-boundary-lint.py
```

| File | Covers |
|---|---|
| `__tests__/platformNavigation.test.ts` | Q-5. The platform nav offers no route into `/admin/leagues` or `/admin/gamification`, every entry is unique and uses a real icon. The last test is the point: it fails if someone re-adds either entry in a later diff. |
| `__tests__/onboardingSkillSelection.test.ts` | Q-6. `submitOnboarding`'s asymmetry (the onboarding write may sink onboarding, the enrollment write may not, but its failure comes back as a value) and the flag's `localStorage` round trip — the half a refactor can silently drop, turning the banner back into a silence. |
| `__tests__/notificationPreferences.test.tsx` | Q-4. The one-shot migration of the old browser values: uploads a legacy choice, fills only the switch the browser held, **never** overwrites a preference already saved (the `isDefault` guard), keeps the keys on failure so the next visit retries, waits for the server's answer, runs once across re-renders. |
| `Notification.Tests/Unit/NotificationPreferencesTests.cs` | Q-4 backend. The defaults are reminders-on/updates-off; an unset preference reads as default; a `GET` writes nothing; saving the defaults still flips `isDefault` to false; one person's preference is not another's; a token with no subject is refused rather than served defaults; the key carries no `org:` prefix. |
| `Learning.Tests/Unit/AdminExercisesReorderTests.cs` | Q-8. A move persists and is echoed back; every way a request could produce duplicated or missing positions is refused **before** any write (subset, repeated id, shared position, foreign id, empty, unknown lesson); non-contiguous positions are accepted; the response carries each exercise's content. |

---

## Manual

Needs the app running (`scripts/dev-up.sh`) and a superadmin login.

### Q-5 — the gamification screens are unreachable from the panel
1. Open `/admin`. The sidebar has **no** "Leagues" and no "Gamification" entry.
2. Type `/admin/leagues` directly. It still loads — the routes were deliberately not deleted, only
   unlinked. This is the expected outcome, not a leftover.

### Q-6 — the honest line on `/tree`
The failure is hard to provoke naturally, so force it:
1. In DevTools, before finishing onboarding, block the request:
   `const f = window.fetch; window.fetch = (u, o) => String(u).includes("/skills/enrolled") ? Promise.resolve(new Response("{}", {status: 500})) : f(u, o);`
2. Finish onboarding. You should land on `/tree` (**not** be held on the onboarding screen) and see
   one amber line: «Не удалось сохранить выбор навыков из онбординга…» with a link to the profile.
3. Reload `/tree`. The line is still there — that is the `localStorage` half doing its job.
4. Go to the profile and enroll any skill. Return to `/tree`: the line is gone for good.
5. Dismissing it with the × also clears it permanently.

### Q-4 — preferences follow the user, and the migration runs once
1. **Migration.** In DevTools on `/settings`, set the old keys and reload:
   `localStorage.setItem("notif.productUpdates","true")` → reload → the "Обновления продукта" switch
   reads **on**, a `PUT /notifications/preferences` fires, and both `notif.*` keys are gone.
2. **Cross-device.** Toggle both switches. Log in from another browser (or a private window) and open
   `/settings` — the switches match. This is what localStorage could never do.
3. **A saved choice wins over a stale local key.** After step 1, set `notif.productUpdates` to the
   opposite value and reload. Nothing is uploaded and the key is merely cleaned up: the server's
   stored answer stands.
4. **Not yet enforced.** Switching "Обновления продукта" off changes no mail, because no
   product-update mailer exists yet. That is the documented state, not a bug — see
   [NOTIFICATION_SERVICE.md](../NOTIFICATION_SERVICE.md).

### Q-8 — a reorder lands whole or not at all
1. Open `/admin/lessons/<lessonId>/exercises` on a lesson with three or more exercises.
2. Press ▲/▼ on a row. It moves, one `PUT …/exercises/reorder` goes out, and the order survives a
   reload.
3. **Failure path.** Stub the reorder to 500:
   `const f = window.fetch; window.fetch = (u, o) => String(u).includes("/exercises/reorder") ? Promise.resolve(new Response("{}", {status: 500})) : f(u, o);`
   Press ▲. A toast appears **and the list snaps back** to its previous order — with an atomic
   request nothing moved on the server, so the rollback is the truth rather than an invented state.
   Reload to confirm the server agrees.
4. Repeat 1–3 on `/admin/skills/<id>/topics/<topicId>/lessons/<lessonId>/exercises` and on the org
   editor `/org/content/lessons/<lessonId>` — all three send the same single request.
