# Admin Panel — Design & Decisions

## Roles

| Role | Value | Capabilities |
|---|---|---|
| `User` | 0 | Regular player — existing experience unchanged |
| `Admin` | 1 | Manage content (skills, lessons, exercises, reference), view player list |
| `SuperAdmin` | 2 | All admin functions + manage admin roles (promote/demote users) |

Role is stored as an integer column on the `User` table and emitted as a `role` claim in the JWT access token.

---

## Authorization policies (backend)

| Policy | Required role | Applied to |
|---|---|---|
| `RequireAdmin` | Admin OR SuperAdmin | All `/admin/*` endpoints except user management |
| `RequireSuperAdmin` | SuperAdmin only | `PUT /admin/users/:id/role` |

Policies are registered in `Program.cs`. Controllers use `[Authorize(Policy = "RequireAdmin")]`.

---

## Seeding the first SuperAdmin

On application startup (`Program.cs`), a default superadmin is upserted if no superadmin exists:
- Email: from env var `SUPERADMIN_EMAIL` (default `admin@sallevate.local`)
- Password: from env var `SUPERADMIN_PASSWORD` (default `Admin123!`)

Change these in production via environment variables.

---

## API endpoints (admin namespace)

All routes prefixed `/admin`. Require `RequireAdmin` unless noted.

### Skills
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/skills | — | `AdminSkillDto[]` |
| POST | /admin/skills | `{title, slug, iconName, sortOrder, applicableSalesTypes[]}` | `AdminSkillDto` |
| PUT | /admin/skills/:id | same fields | `AdminSkillDto` |
| DELETE | /admin/skills/:id | — | 204 |

### Lessons
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/skills/:skillId/lessons | — | `AdminLessonDto[]` |
| POST | /admin/skills/:skillId/lessons | `{title, sortOrder}` | `AdminLessonDto` |
| PUT | /admin/lessons/:id | `{title, sortOrder}` | `AdminLessonDto` |
| DELETE | /admin/lessons/:id | — | 204 |

### Exercises
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/lessons/:lessonId/exercises | — | `AdminExerciseDto[]` |
| POST | /admin/lessons/:lessonId/exercises | `{type, sortOrder, content: <jsonb>}` | `AdminExerciseDto` |
| PUT | /admin/exercises/:id | same | `AdminExerciseDto` |
| DELETE | /admin/exercises/:id | — | 204 |

### Reference Materials
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/skills/:skillId/reference | — | `AdminReferenceMaterialDto[]` |
| POST | /admin/skills/:skillId/reference | `{title, markdownContent, sortOrder}` | `AdminReferenceMaterialDto` |
| PUT | /admin/reference/:id | same | `AdminReferenceMaterialDto` |
| DELETE | /admin/reference/:id | — | 204 |

### Users (SuperAdmin only)
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/users | — | `AdminUserDto[]` |
| PUT | /admin/users/:id/role | `{role: "User"\|"Admin"\|"SuperAdmin"}` | `AdminUserDto` |

### JSON Import (Seeder)
| Method | Path | Body | Response |
|---|---|---|---|
| POST | /admin/seeder/skills | `multipart/form-data` with JSON file | `SkillsImportResultDto` |
| POST | /admin/seeder/lessons | `multipart/form-data` with JSON file | `LessonsImportResultDto` |

---

## Frontend routes

All routes under `/admin` are protected — non-admins are redirected to `/tree`.

```
app/(admin)/
  layout.tsx           ← sidebar nav + auth guard
  admin/
    page.tsx           ← redirect to /admin/skills
    skills/
      page.tsx         ← skill list + JSON import
      [id]/
        page.tsx       ← edit skill + lessons list
        lessons/
          [lessonId]/
            page.tsx       ← lesson edit + exercises list
            exercises/
              page.tsx     ← visual exercise editor (all 11 types)
        reference/
          page.tsx     ← reference materials list + editor
    lessons/
      page.tsx         ← all lessons view + JSON import
    users/
      page.tsx         ← user list + role management (superadmin only)
```

---

## UI principles

- Minimal, functional, monochrome color scheme
- Standard HTML-like forms via Tailwind utility classes
- Tables for list views
- Inline delete confirmation (no separate modal — just a button state change to "Confirm?")
- JSON import sections collapsible on each entity page

---

## JSON Import Formats

### Skills Template
```json
[
  {
    "slug": "cold-calling",
    "title": "Cold Calling",
    "iconName": "phone",
    "sortOrder": 1,
    "applicableSalesTypes": ["b2b_saas", "b2c"],
    "prerequisiteSkillIcon": null
  }
]
```

### Lessons Template (with all 11 exercise types)
```json
[
  {
    "skillIcons": ["cold-calls"],
    "title": "Opening the Call",
    "sortOrder": 1,
    "difficultyLevel": 1,
    "xpReward": 50,
    "exercises": [
      { "type": "multiple_choice", "sortOrder": 1, "content": {...} },
      { "type": "fill_blank", "sortOrder": 2, "content": {...} },
      { "type": "open_question", "sortOrder": 3, "content": {...} },
      { "type": "ordering", "sortOrder": 4, "content": {...} },
      { "type": "matching", "sortOrder": 5, "content": {...} },
      { "type": "categorizing", "sortOrder": 6, "content": {...} },
      { "type": "find_error", "sortOrder": 7, "content": {...} },
      { "type": "rewrite_better", "sortOrder": 8, "content": {...} },
      { "type": "ai_dialog", "sortOrder": 9, "content": {...} },
      { "type": "rate_call", "sortOrder": 10, "content": {...} },
      { "type": "written_answer", "sortOrder": 11, "content": {...} }
    ]
  }
]
```

---

## Exercise Types (11 total)

| Type | Description | Key Content Fields |
|------|-------------|-------------------|
| `multiple_choice` | Quiz with 4 options | situation, question, options[], correctOptionIndex, explanation |
| `fill_blank` | Dialog completion | characterName, characterLine (with ___), options[], correctOptionIndex |
| `open_question` | Free-form AI-evaluated answer | question, aiPrompt |
| `ordering` | Arrange items in sequence | instruction, items[], correctOrder[], explanation |
| `matching` | Connect left/right columns | instruction, leftItems[], rightItems[], correctPairs[] |
| `categorizing` | Sort items into buckets | instruction, categories[], items[], correctMapping{} |
| `find_error` | Identify mistake in dialog | instruction, dialogLines[], errorLineId, suggestedFixes[] |
| `rewrite_better` | Improve given text | originalText, context, minLength, maxLength, aiPrompt |
| `ai_dialog` | Practice with AI persona | scenario, persona{}, systemPrompt, minTurnsForCompletion, aiPrompt |
| `rate_call` | Evaluate transcript quality | transcript[], criteria[], aiPrompt |
| `written_answer` | Write based on prompt | prompt, context, minLength, maxLength, aiPrompt |

See `src/frontend/components/admin/exercise-editors/types.ts` for full TypeScript interfaces.
