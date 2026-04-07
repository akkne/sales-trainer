# Keyboard Controls — Manual Test Checklist

## Feature
Keyboard shortcuts for exercise session: digit keys select options, Enter/Space submits or continues.

## Scope
- `MultipleChoiceExercise` — digits + Enter/Space
- `FillBlankExercise` — digits + Enter/Space
- `FreeTextExercise` — Enter to continue after answer shown (no digit hijacking)

---

## Setup
Open any lesson session via `/session/[lessonId]` on desktop (keyboard available).

---

## Test Cases

### Multiple Choice & Fill-Blank exercises

| # | Action | Expected |
|---|--------|----------|
| 1 | Press `1` before answering | First option becomes selected (blue highlight) |
| 2 | Press `2` | Second option selected |
| 3 | Press `3` / `4` | Third/fourth option selected (if they exist) |
| 4 | Press a digit > option count (e.g., `9`) | Nothing happens |
| 5 | Press `Enter` with no option selected | Nothing happens (submit button still disabled) |
| 6 | Press `1` then `Enter` | Answer submitted, result feedback shown |
| 7 | Press `Space` with option selected | Answer submitted |
| 8 | Press `Enter` after result shown | Moves to next exercise ("Продолжить" triggered) |
| 9 | Press `Space` after result shown | Same as above |
| 10 | Change selection with digit, then submit with Enter | Correct index sent |

### Free Text exercise

| # | Action | Expected |
|---|--------|----------|
| 11 | Type in textarea, press Enter | Newline added in textarea (NOT submitted) |
| 12 | Press Space in textarea | Space character typed (NOT submitted) |
| 13 | After AI evaluation shown, press Enter | Moves to next exercise |
| 14 | Digit keys while textarea focused | Characters typed normally, no option selection |

### Keyboard hint

| # | Check | Expected |
|---|-------|----------|
| 15 | Desktop browser (non-touch) | Small gray hint below action button: "1–4 выбрать · Enter — проверить" or "Enter — продолжить" |
| 16 | Touch device (mobile) | Hint hidden (controlled by `pointer-fine` media query) |

### Edge cases

| # | Scenario | Expected |
|---|----------|----------|
| 17 | Rapidly press Enter twice | Only advances once (second press handled after state update) |
| 18 | Press digit while submitting (isSubmitting=true) | No re-selection (disabled) |
| 19 | Navigate to completion screen, press Enter | No crash, page remains on completion screen |

---

## Unit tests
Run: `npm test -- __tests__/useKeyboardControls` inside `src/frontend/`

All 11 tests should pass.
