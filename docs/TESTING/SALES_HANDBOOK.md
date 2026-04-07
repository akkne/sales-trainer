# Testing — Sales Handbook (Phase 14)

## What was added
- `Category` and `Tags` columns on `ReferenceMaterial` entity + migration
- `GET /reference?category=&search=` — global cross-skill reference endpoint
- `GET /reference/categories` — distinct categories endpoint
- `/guidebook` page: search, category chips, expand-on-tap cards
- "📖 Справочник" tab added to BottomNav

## Manual checklist

### Guidebook page (`/guidebook`)
- [ ] Page loads and shows "📖 Справочник" header
- [ ] Search input visible; typing filters results in real-time (debounced via useDeferredValue)
- [ ] Clear (×) button appears when search has text; clears input on click
- [ ] Empty state shown when search yields no results

### Category chips
- [ ] "Все" chip selected by default (green)
- [ ] Chips only appear when at least one reference material has a category set
- [ ] Clicking a category chip filters to that category only
- [ ] Clicking the same chip again deselects it (shows all)
- [ ] Multiple filters work together: category + search

### Technique cards
- [ ] Card shows: category badge (colored), tags (grey pills), title, excerpt
- [ ] Clicking card expands it: full markdown rendered, excerpt hidden
- [ ] Clicking again collapses the card
- [ ] Only one card expanded at a time
- [ ] Expanded card shows "📚 Связанный навык →" link → navigates to `/skill/[slug]`

### Existing per-skill reference page (`/reference/[slug]`)
- [ ] Still works correctly after DTO change (category + tags + skillSlug added)

### Backend API
- [ ] `GET /reference` returns all materials (200)
- [ ] `GET /reference?category=objections` returns only objections items
- [ ] `GET /reference?search=холодные` returns items with that text
- [ ] `GET /reference/categories` returns array of distinct category strings
- [ ] `GET /skills/sales-basics/reference` still works (per-skill endpoint unchanged)
- [ ] Migration applied: `ReferenceMaterials` table has `Category` and `Tags` columns
- [ ] Migration applied: `Lessons` table has `Description` and `EstimatedMinutes` columns

### Admin panel — reference editor
- [ ] Existing admin reference editor still loads/saves correctly (category/tags fields are nullable)
