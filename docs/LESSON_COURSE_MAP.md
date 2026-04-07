# Lesson Course Map (Phase 13)

## Overview
A detailed overview screen per skill showing all lessons as a structured course map.  
Route: `/skill/[slug]/map`

## Entry points
- SkillNode active-state popover on `/tree` → "Посмотреть карту курса →"
- `/skill/[id]` page header → "Карта курса →"

## Page structure

### Header card (green)
- Skill icon + title
- "X из N уроков пройдено" subtitle
- Completion % (large number, top-right)
- White progress bar

### Lesson list
One card per lesson, sorted by `sortOrder`.

| State | Circle | Card style | Extra |
|---|---|---|---|
| completed | Green ✓ | Light-green bg | — |
| available / in_progress | Green number | White, green border-2 | "Начать урок" CTA button |
| locked | Grey lock icon | Grey bg, opacity-60 | "Пройди предыдущий урок" hint |

Each card shows:
- Lesson number badge (Урок N)
- XP reward badge (`+N XP`)
- Estimated duration (`~N мин`) when > 0
- Lesson title (bold)
- Description excerpt (2-line clamp) if present
- Lock hint for locked lessons

## Data sources
- `GET /skills/[slug]/lessons` → `LessonSummary[]` (includes `description`, `estimatedMinutes`)
- `GET /skills` → `SkillTreeNode[]` (for skill title/icon in header)

## Frontend files
- `src/frontend/app/(main)/skill/[id]/map/page.tsx` — map page
- `src/frontend/lib/hooks/useLesson.ts` — `LessonSummary` extended with `description`, `estimatedMinutes`
- `src/frontend/components/ui/SkillNode.tsx` — popover link added
- `src/frontend/app/(main)/skill/[id]/page.tsx` — header shortcut added
