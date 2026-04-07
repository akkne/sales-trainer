# Testing — Lesson Course Map (Phase 13)

## What was added
- `/skill/[id]/map` page — structured course map per skill
- `LessonSummary` TS interface extended with `description` and `estimatedMinutes`
- SkillNode active-state popover gets a "Посмотреть карту курса →" link
- `/skill/[id]` page header gets a "Карта курса →" shortcut link

## Manual checklist

### Map page rendering
- [ ] Navigate to `/skill/sales-basics/map` (or any slug with lessons)
- [ ] Header shows: skill icon, skill title, "X из N уроков пройдено", completion %
- [ ] Green progress bar fills proportionally
- [ ] Lesson cards render in sortOrder sequence

### Lesson card states
- [ ] **Completed** lesson: green circle with ✓, light-green card background, no CTA button
- [ ] **Active** (available/in_progress) lesson: green circle with number, white card, green border, "Начать урок" button
- [ ] **Locked** lesson: grey circle with lock icon, dimmed card (opacity-60), "Пройди предыдущий урок" hint
- [ ] Each card shows XP badge (`+N XP`) and estimated time (`~N мин`) when available
- [ ] Description excerpt shown if lesson has a description (truncated to 2 lines)

### Navigation
- [ ] "Начать урок" button on active lesson navigates to `/session/[lessonId]`
- [ ] "← Назад" link returns to `/skill/[id]`
- [ ] SkillNode popover (active state on `/tree`) shows "Посмотреть карту курса →" link → navigates to map
- [ ] `/skill/[id]` page shows "Карта курса →" link in progress header → navigates to map

### Edge cases
- [ ] Skill with 0 lessons → empty state "Уроки ещё не добавлены" shown
- [ ] Skill with all lessons locked → all cards dimmed, 0% completion
- [ ] Skill with all lessons completed → 100% header, all cards green ✓

### Backend (no changes needed — `description` and `estimatedMinutes` already in LessonSummaryDto)
- [ ] `GET /skills/sales-basics/lessons` response includes `description` (nullable) and `estimatedMinutes` (int)
