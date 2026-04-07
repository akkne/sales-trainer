# Admin Reference Material CRUD — Testing Checklist

## Phase 15

### Backend endpoints

- [ ] `GET /admin/reference` returns all materials across all skills (admin token required)
- [ ] `GET /admin/reference?skillId=<id>` filters by skill
- [ ] `GET /admin/reference?category=objections` filters by category
- [ ] `GET /admin/reference?search=rapport` searches title and content
- [ ] `GET /admin/reference/categories` returns distinct non-null categories
- [ ] `POST /admin/skills/:skillId/reference` creates material with category + tags
- [ ] `PUT /admin/reference/:id` updates all fields including category/tags
- [ ] `DELETE /admin/reference/:id` removes material → 204
- [ ] Non-admin token → 403 on all `/admin/reference` routes

### Frontend — `/admin/reference` page

- [ ] Page loads and shows all reference materials table
- [ ] Skill filter narrows results
- [ ] Category filter narrows results
- [ ] Search input filters in real-time (debounced)
- [ ] "+ New material" button opens create form
- [ ] Create form: requires skill selection, title; category/tags optional
- [ ] After create, new material appears in list
- [ ] "Edit" opens inline edit with all fields pre-filled (incl. category/tags)
- [ ] "Save" updates and refreshes list
- [ ] "Delete" shows confirm → confirmed → material removed
- [ ] Empty state shown when no results

### Frontend — `/admin/skills/[id]/reference` page

- [ ] Existing page still works for per-skill reference management
- [ ] Create form includes category and tags fields
- [ ] Edit form includes category and tags fields
- [ ] Category and tags displayed in list view

### Nav

- [ ] "Reference" link appears in admin sidebar
- [ ] Active state highlights correctly when on `/admin/reference`
