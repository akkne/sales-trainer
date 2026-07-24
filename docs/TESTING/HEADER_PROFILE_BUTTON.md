# Testing: Header Profile Button Cleanup

> Scope: Phase 32 in [ROADMAP.md](../ROADMAP.md). Verifies that the trophy
> "milestones" button is gone from the desktop header and that the profile
> chip uses a first-letter avatar instead of the medal icon.

## Manual Test Checklist

### Desktop header (≥ md breakpoint)

**Right-side cluster**
- [ ] Order of items is: activity-consistency indicator (if streak > 0) → notification bell → profile chip
- [ ] There is NO trophy / milestones button (`emoji_events`) between the bell and the profile chip
- [ ] Profile chip uses `bg-primary-container` with rounded-full shape
- [ ] Profile chip shows a circular avatar filled with `bg-primary` on the left
- [ ] Avatar displays the UPPERCASE first letter of the authenticated user's display name
- [ ] Chip label on the right reads `Уровень {level}` where `level = floor(totalXp / 1000) + 1`
- [ ] There is NO medal icon (`military_tech`) inside the chip
- [ ] Chip has `aria-label="Профиль (<displayName>)"` for screen readers
- [ ] Click chip → navigates to `/profile`
- [ ] Click bell → notification panel opens (unchanged)

**Edge cases**
- [ ] When `displayName` is missing, avatar falls back to `?`
- [ ] When `totalXp = 0`, chip still shows `Уровень 1`
- [ ] When streak = 0, only bell and chip appear to the right of the nav
- [ ] Hover on chip → opacity reduces (no layout shift)

### Navigation (untouched)

- [ ] `Лиги` link (`/league`) still present in the main nav with its trophy icon
- [ ] `/league` page loads normally with team progress list and promotion/demotion zones
- [ ] `Мастерство`, `Лиги`, `Библиотека`, `Диалоги`, `Друзья` order unchanged

### Mobile (< md breakpoint)

- [ ] Top app bar is hidden on mobile (no regression)
- [ ] Bottom navigation behaviour unchanged

### Regression

- [ ] `/profile` page still reachable from bottom nav
- [ ] Friend request badge still rendered on the `Друзья` nav item when there are incoming requests
- [ ] No console warnings or missing-key errors in browser devtools
