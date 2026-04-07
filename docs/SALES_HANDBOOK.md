# Sales Handbook (Phase 14)

## Overview
A searchable, filterable reference page at `/guidebook` showing sales technique cards grouped by category.

## Entry point
BottomNav → "📖 Справочник"

## Page structure

### Search bar
Real-time search (via React `useDeferredValue`) filtering by title and markdown content.

### Category chips
Dynamically loaded from `GET /reference/categories`. Chips: "Все" + one per distinct category.  
Known categories: `objections`, `cold-calls`, `closing`, `discovery`, `rapport`, `negotiation`.

### Technique cards
- **Collapsed**: category badge, tag pills, title, 2-line excerpt
- **Expanded**: full markdown (ReactMarkdown), "📚 Связанный навык →" link to `/skill/[slug]`
- Only one card expanded at a time

### Empty state
Shown when no materials match the current search/category combination.

## API

| Method | Route | Description |
|---|---|---|
| GET | `/reference` | All reference materials (optional `?category=&search=`) |
| GET | `/reference/categories` | Distinct category list |
| GET | `/skills/{slug}/reference` | Per-skill reference (unchanged) |

## ReferenceMaterial entity fields
| Field | Type | Notes |
|---|---|---|
| Category | `string?` | Slug, e.g. `"objections"` |
| Tags | `string?` | Comma-separated, e.g. `"rapport,discovery"` |

## Frontend files
- `src/frontend/app/(main)/guidebook/page.tsx` — handbook page
- `src/frontend/lib/hooks/useReference.ts` — `useHandbook()`, `useHandbookCategories()` hooks
- `src/frontend/components/layout/BottomNav.tsx` — "📖 Справочник" tab added

## Backend files
- `Features/Reference/ReferenceMaterial.cs` — `Category`, `Tags` fields added
- `Features/Reference/ReferenceMaterialDto.cs` — `category`, `tags`, `skillSlug` added
- `Features/Reference/ReferenceService.cs` — `GetAllReferenceMaterialsAsync`, `GetAllCategoriesAsync`
- `Features/Reference/ReferenceController.cs` — `GET /reference`, `GET /reference/categories`
- `Migrations/20260405180000_AddCategoryTagsToReference` — DB migration
