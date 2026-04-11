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

---

## Frontend routes

All routes under `/admin` are protected — non-admins are redirected to `/tree`.

```
app/(admin)/
  layout.tsx           ← sidebar nav + auth guard
  admin/
    page.tsx           ← redirect to /admin/skills
    skills/
      page.tsx         ← skill list
      [id]/
        page.tsx       ← edit skill + lessons list
        lessons/
          [lessonId]/
            page.tsx   ← exercises list + editor
        reference/
          page.tsx     ← reference materials list + editor
    users/
      page.tsx         ← user list + role management (superadmin only)
```

---

## UI principles

- Minimal, functional, no animations
- Standard HTML-like forms via Tailwind utility classes
- Tables for list views, simple modals for create/edit
- Inline delete confirmation (no separate modal — just a button state change to "Confirm?")
- Role badge on user list (color-coded: gray=User, blue=Admin, purple=SuperAdmin)
