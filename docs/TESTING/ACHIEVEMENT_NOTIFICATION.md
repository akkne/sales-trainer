# Achievement Unlock Notification — Manual Test Checklist

## Feature
When a user unlocks an achievement during an exercise session, a toast notification slides in from the top showing the achievement icon, title, and description.

---

## Setup
1. Have at least one achievement close to unlocking (e.g., "first_blood" = answer 1 exercise correctly)
2. Open a lesson session via `/session/[lessonId]`
3. Submit a correct answer

---

## Test Cases

### Toast appearance

| # | Action | Expected |
|---|--------|----------|
| 1 | Submit a correct answer that unlocks an achievement | Toast slides in from top with green border |
| 2 | Toast content | Shows achievement icon emoji, "Достижение разблокировано!" label, title, and description |
| 3 | Wait 4 seconds | Toast slides out automatically |
| 4 | Click the toast before auto-dismiss | Toast dismisses immediately |
| 5 | Submit wrong answer that would have unlocked (if answered correctly) | No toast shown |

### Queue behavior

| # | Action | Expected |
|---|--------|----------|
| 6 | Submit answer that unlocks 2 achievements simultaneously | First toast shows; after it dismisses, second appears |
| 7 | Multiple correct answers each unlocking a different achievement | Toasts queue and show one at a time with no overlap |

### Positioning

| # | Device | Expected |
|---|--------|----------|
| 8 | Desktop | Toast appears top-center, max-width 384px |
| 9 | Mobile | Toast appears top-center, full width minus 16px padding per side |

### No disruption

| # | Check | Expected |
|---|-------|----------|
| 10 | Toast visible, press Enter | "Продолжить" still works (keyboard controls unaffected) |
| 11 | Toast visible, select next option | Session continues normally underneath toast |
| 12 | Navigate to completion screen | Any pending toast is still shown if not dismissed |

---

## Unit tests
Run: `npm test -- __tests__/AchievementToast` inside `src/frontend/`

All 5 tests should pass.
