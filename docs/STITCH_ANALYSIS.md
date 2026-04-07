# Stitch Design Analysis — Duolingo Green Interface

## Source

Project ID: `5546133593140033209` ("Игровая классика (Gamified Sales)")  
Design system: Emerald Orbit (Bouncy Brutalism)

## Screens analyzed

| Screen | Stitch ID |
|---|---|
| Skill Tree (Home) | `854f31d4a31c455bb111e9b0846bffe7` |
| Skill Path | `25f72cf9a5e34fb0828f1b9eb36f77e2` |
| Exercise | `717fe6255b5c444b87da88eb1cc7499a` |
| League | `bbddd177746c4480ab478c3a142f61da` |
| Profile | `3c59101cf5d542bca809d1a90bf4382f` |
| Guidebook | `f0025d25b7174eb0b3b5eaa5e7689750` |
| Onboarding | `607c94ae210f45fd9ae2aad3dbb7c761` |

## Design tokens (from Stitch)

```
Primary green:     #58CC02  (shadow: #58A700)
Accent blue:       #1CB0F6
Gold/streak:       #FFC800  (shadow: #E0A800)
Error/hearts red:  #FF4B4B  (shadow: #CC3333)
Background:        #FFFFFF
Surface:           #F7F7F7
Border:            #E5E5E5
Text muted:        #AFAFAF

Font: Manrope (display + body)
Button radius: 16px (1rem)
3D shadow: 0 4px 0 [darker shade]
```

## What was already implemented (Phase 8)

- SkillNode with completed/active/locked states + zigzag offsets
- StatsWidget with streak/XP/total cards
- Exercise page: ✕ + progress bar + hearts
- MultipleChoice: numbered 3D buttons, blue selected state
- ExerciseResultBanner: slide-up animation, correct/incorrect panels
- LessonPath with vertical node path, path line overlay

## What was missing and implemented in this session

### 1. Full-screen session page (`/session/[lessonId]`)
**File:** `src/frontend/app/session/[lessonId]/page.tsx`

The exercise page was in `(main)` layout (BottomNav visible). Created a dedicated full-screen session outside the layout:
- Header: ✕ · progress bar · hearts
- All 3 exercise types with skip support
- **Completion screen**: animated 🎉, XP earned counter, total exercises, remaining hearts, "Вернуться к пути" button
- **Failure screen**: 💔, "Попробовать снова" (resets session state via `restartKey`), "Вернуться к пути"
- Restart implemented without navigation — `SessionFlow` component keyed on `restartKey`

### 2. Tap-to-open popover on lesson nodes
**File:** `src/frontend/components/ui/LessonPath.tsx`

Previous: always-on `pointer-events-none` popover above active node.  
Now: tap-to-toggle, click-outside closes, only one popover open at a time.  
Popover "Приступить к прохождению" button links to `/session/[lessonId]`.

### 3. Skip button (ПРОПУСТИТЬ) in exercises
**Files:** `MultipleChoiceExercise.tsx`, `FillBlankExercise.tsx`, `FreeTextExercise.tsx`

Added optional `onSkip?: () => void` prop. When provided, renders a secondary outlined "Пропустить" button alongside "Проверить". Skip advances to next exercise without penalty.

### 4. League countdown timer
**File:** `src/frontend/app/(main)/league/page.tsx`

Stitch design shows "До конца недели 2д 14ч" above the leaderboard.  
Implemented `useCountdown(weekEndDate)` hook — computes days/hours/minutes from `weekEndDate` ISO string, updates every 60s.

### 5. Animated dashed path line
**File:** `src/frontend/components/ui/LessonPath.tsx`, `globals.css`

Stitch design uses a dashed animated SVG stroke for the active node segment.  
Replaced solid green div with an SVG `<line>` using `stroke-dasharray: 10 10` and `@keyframes dash-scroll` animation (1.2s loop).

## Still missing (deferred to Phase 9 backend work)

- **Sequential lesson unlock** — `UpdateLessonProgressAsync` backend method (auto-unlock next lesson on completion)
- **Achievements/badges section on Profile** — needs backend `Achievement` entity + API
