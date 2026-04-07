# Lesson Execution Flow

> Feature spec — sequential lesson progress with "Start" popover and full-screen exercise session.
> **Status: planned, not implemented.**

---

## 1. Goal

A user opens `/tree`, sees the ordered lesson path, taps a lesson node — a small "Приступить к прохождению" popover appears above the node. Tapping the CTA opens the lesson in a new browser tab as a full-screen exercise session.

---

## 2. Progress Model (already in DB)

```
UserLessonProgress
  userId     Guid
  lessonId   Guid
  status     "locked" | "available" | "in_progress" | "completed"
  bestScore  int
  completedAt DateTime?
```

**Unlock rule:** the first lesson of each skill starts as `available`; every subsequent lesson is `locked` until the previous one reaches `completed`. When a lesson is completed `ExerciseService.UpdateLessonProgressAsync` already sets the _skill_ progress, but **does NOT auto-unlock the next lesson** — this needs to be added (see §4).

---

## 3. UX Flow

```
/tree
  └─ LessonPath (ordered by sortOrder)
       └─ tap any non-locked node
            └─ small popover appears above node
                 title:  lesson.title
                 sub:    "Урок N из Total"
                 button: "Приступить к прохождению"  →  opens /session/[lessonId] in new tab
```

**Popover behaviour:**
- Tapping outside the popover (or another node) closes it.
- Only one popover is open at a time.
- Locked nodes are not tappable (cursor-not-allowed, no popover).
- Active/available node already has a persistent popover in the current design — replace it with this tap-to-open behaviour for consistency.

---

## 4. Backend changes needed

### 4.1 Auto-unlock next lesson on completion

In `ExerciseService.UpdateLessonProgressAsync`, after marking a lesson `completed`, find the lesson with `sortOrder = completedLesson.sortOrder + 1` **within the same skill** and set its `UserLessonProgress.status = "available"`.

For cross-skill unlock the existing `UnlockNextSkillAsync` is sufficient — it unlocks the first lesson of the next skill (no change needed there, but make sure `UserLessonProgress` rows exist for all lessons of the newly unlocked skill with `status = "locked"` except the first one which becomes `"available"`).

### 4.2 Seed lesson progress rows

When a skill is unlocked, `UserLessonProgressRecords` rows must be created for **all** its lessons:
- `status = "locked"` for all except the first (`sortOrder` lowest)
- first lesson → `status = "available"`

Currently the seeder or onboarding may not create these rows. Verify and fix.

---

## 5. Frontend changes needed

### 5.1 Tap-to-open popover (replace always-on popover)

In `LessonPath` / `LessonNode`:
- Remove `isActive` always-open popover logic.
- Add local state `openNodeId: string | null`.
- Clicking an available/in_progress/completed node toggles the popover.
- Click-outside handler closes it.

Popover card content:
```
┌──────────────────────────┐
│  Урок N из Total         │  ← small gray caption
│  [lesson title]          │  ← bold
│  ┌────────────────────┐  │
│  │ Приступить к       │  │  ← green button, btn-3d style
│  │ прохождению        │  │
│  └────────────────────┘  │
└──────────────────────────┘
```

### 5.2 `/session/[lessonId]` — full-screen exercise tab

New route outside `(main)` layout (no BottomNav, no sidebar).

**Screen structure (see attached design images):**
```
┌─────────────────────────────┐
│ [✕]  ██████████░░░░  ♥♥♥♥  │  ← header: close, progress bar, hearts
├─────────────────────────────┤
│                             │
│  [character portrait]       │
│  ┌─────────────────────┐   │
│  │  situation text     │   │  ← speech bubble
│  └─────────────────────┘   │
│                             │
│  [1]  Option A              │  ← numbered 3D choice buttons
│  [2]  Option B              │
│  [3]  Option C              │
│                             │
└─────────────────────────────┘
│  [ПРОВЕРИТЬ]                │  ← sticky bottom CTA
└─────────────────────────────┘
```

- `✕` button closes the tab (`window.close()`) or navigates back.
- Progress bar advances per exercise within the lesson.
- Hearts: 4 hearts, lose one per wrong answer; 0 hearts → session ends with failure screen.
- After last exercise → completion screen (XP earned, streak update, "Вернуться к пути" button).

---

## 6. API contracts (new / changed)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/lessons/{id}/session` | Returns lesson metadata + exercises in order for the session screen. Reuses existing `/lessons/{id}/exercises`. |
| `POST` | `/exercises/{id}/submit` | Already exists. Returns `isCorrect`, `xpEarned`, `explanation`. |

No new endpoints required — the session screen can use existing endpoints.

---

## 7. Implementation order

1. Fix `UpdateLessonProgressAsync` — auto-unlock next lesson in same skill.
2. Ensure all `UserLessonProgressRecords` are seeded on skill unlock/onboarding.
3. Frontend: swap always-on active popover → tap-to-open popover in `LessonNode`.
4. Frontend: create `/session/[lessonId]` route with the full-screen exercise UI.
5. Wire "Приступить" button → `window.open('/session/'+lessonId, '_blank')`.
6. Test full flow: tree → popover → session tab → completion → return.
