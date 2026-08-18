# Content Seeders

Administrators can bulk-import content via the admin panel using three seeder endpoints: skills, topics, and lessons. All seeders accept **JSON only** and perform idempotent upsert operations.

---

## 0. The seeder writes the GLOBAL library, and only the global library (Phase 40.19)

Read this before anything else on this page. It is the one thing about the seeder that changed when
organizations became able to own content.

Every import below writes rows with `organization_id IS NULL` — the base library every customer
reads. **It cannot be pointed at a customer, and that is a hard rule rather than a convention.**

- Every request must carry an explicit `target=global` form field. Anything else — a missing field, a
  different value, an organization id — is rejected with `400`. The field exists so that seeding the
  shared library is a stated intention rather than a default, and so that the day somebody wants a
  per-organization import they have to answer the question this endpoint currently refuses to be
  asked.
- `target` is **not** an organization id and must never become one. The tenant is read from
  `ITenantContext` (the gateway-validated header), never from a request body
  ([TENANCY.md §1.3](TENANCY/TENANCY.md)). `scripts/tenancy-boundary-lint.py` enforces this.
- Every read inside the seeder is narrowed to `organization_id IS NULL` too, which is the half that
  fixed a real bug. Reads used to go through the tenancy query filter, which admits *"global or
  mine"*. A platform administrator who is also a member of an organization would load that
  organization's **override** lessons alongside the base ones — and lessons upsert on
  `(topicId, title)`, so re-running a bundle import would silently overwrite a customer's edited
  lesson and its exercises with the base text. Nothing in the response said so; the customer would
  simply find their edits gone. The `*/export` endpoints had the mirror bug: an export taken by that
  administrator carried the overrides out into a file that re-imported as if it were the shared
  library.
- The narrowing is done by query rather than by "run as platform staff", so it holds no matter what
  tenant header the request happened to carry.

**How an organization customizes content, then, since not through the seeder:**

| Want | Mechanism | Cost |
|---|---|---|
| The lesson to name their product, ICP, objections, tone, terminology | Fill in the profile form (`PUT /organizations/profile`) — the base lesson's `{{organization.*}}` placeholders resolve from it. See [CONTENT_PARAMETERIZATION.md](CONTENT_PARAMETERIZATION.md) | Cheap. One base lesson still serves everybody, and they keep receiving base improvements |
| A genuinely different lesson body | `POST /admin/content/overrides/lesson/{baseId}` — copy-on-write (Phase 40.18, [CONTENT_MODEL.md §2.6](TENANCY/CONTENT_MODEL.md)) | Expensive. That lesson leaves the base library's improvement path and joins the staleness review queue |
| A different curriculum *order* | Programme versioning (Phase 40.17) — reordering touches no lesson | Cheap |

`.claude/local-seed/seed.py` sends `target=global` on every bundle import and deliberately grew no
flag to point itself anywhere else. If per-organization seeding is ever needed, that is a change to
the API contract and to this document, not a command-line switch.

### The customer's own path is a different pipeline (Phase 40.27)

A customer who wants content **out of their own material** does not get a seeder flag. They get the
admin content pipeline — [CONTENT_PIPELINE.md](CONTENT_PIPELINE.md) — which is the product version of
what `seed.py` does offline, with one difference that is the whole point: it **stops in the middle**
and shows the extracted structure for confirmation before generating anything.

The two paths are deliberately separate, and the table above gains a fourth row because of it:

| Want | Mechanism | Cost |
|---|---|---|
| A lesson built from their own deck, script or training notes | `POST /admin/content-generation` → review the structure → approve (Phase 40.27) | One structuring call and one generation call. The lesson is theirs, `organization_id` set, archived until reviewed. It never joins the shared library and never reaches another customer |

Three properties keep this from becoming a back door into the shared library:

- **It writes `organization_id`, always.** There is no target field and no way to ask for a global
  write. The seeder remains the one authoring path for `organization_id IS NULL`, and that has not
  changed.
- **It goes through the ordinary content tables**, so a generated lesson is versioned, overridable and
  substitutable exactly like a seeded one — but it is *the organization's* lesson, outside the base
  library's improvement path by construction rather than by an override marker.
- **`seed.py` itself is untouched by 40.27.** It still seeds the global library, still sends
  `target=global`, still has no per-organization flag.

---

## 1. Skills Seeder — `/admin/seeder/skills`

Imports skills only.

### JSON format

```json
[
  {
    "iconicName": "cold-calling",
    "title": "Cold Calling",
    "description": null,
    "orderInTree": 1,
    "stage": "preparation"
  }
]
```

| Field | Type | Notes |
|---|---|---|
| `iconicName` | string | Unique identifier — upsert key |
| `title` | string | Display title |
| `description` | string \| null | Optional description |
| `orderInTree` | number | Position in skill tree (lower = higher) |
| `stage` | string | Skill stage `key`. Built-in: `preparation`, `discovery`, `engagement`, `closing`, `retention`; `general` is the default when omitted. Stages are DB-driven and admin-editable (`/admin/skill-stages`) — any configured `key` is valid; unknown keys render in a generic "Другое" bucket on `/tree`. |

### API endpoint

```
POST /admin/seeder/skills
Authorization: Bearer <adminToken>
Content-Type: multipart/form-data

file:   <JSON file>
target: global          # required — see §0. Any other value returns 400
```

### Response `200 OK`

```json
{
  "skillsCreated": 2,
  "skillsUpdated": 1,
  "errors": []
}
```

---

## 2. Topics Seeder — `/admin/seeder/topics`

Imports topics (groups of lessons within a skill).

### JSON format

```json
[
  {
    "skillIconicName": "cold-calling",
    "iconicName": "opening-techniques",
    "title": "Opening Techniques",
    "orderInSkill": 1
  }
]
```

| Field | Type | Notes |
|---|---|---|
| `skillIconicName` | string | Parent skill's `iconicName` |
| `iconicName` | string | Unique topic identifier within the skill — upsert key (combined with skill) |
| `title` | string | Display title |
| `orderInSkill` | number | Position within the skill (lower = higher) |

### API endpoint

```
POST /admin/seeder/topics
Authorization: Bearer <adminToken>
Content-Type: multipart/form-data

file:   <JSON file>
target: global          # required — see §0. Any other value returns 400
```

### Response `200 OK`

```json
{
  "topicsCreated": 3,
  "topicsUpdated": 1,
  "errors": []
}
```

---

## 3. Lessons Seeder — `/admin/seeder/lessons`

Imports lessons and their nested exercises in one operation. Exercises are validated per type; bad exercises are skipped and reported in errors.

### JSON format

```json
[
  {
    "topicIconicName": "opening-techniques",
    "title": "Opening the Call",
    "orderInTopic": 1,
    "exercises": [
      {
        "type": "choose_option",
        "orderInLesson": 1,
        "content": {
          "situation": "Client says: 'Too expensive'",
          "options": [
            { "text": "I can offer a discount.", "is_correct": false },
            { "text": "Expensive relative to what?", "is_correct": true }
          ],
          "explanation": "Better to ask why than to cut price."
        }
      },
      {
        "type": "free_text",
        "orderInLesson": 2,
        "content": {
          "situation": "Client: 'We already have a vendor'",
          "instruction": "Write your response",
          "evaluation_criteria": ["Doesn't lower price", "Asks about pain"],
          "ai_prompt": "Evaluate the response."
        },
        "customAiPrompt": null
      }
    ]
  }
]
```

| Field | Type | Notes |
|---|---|---|
| `topicIconicName` | string | Parent topic's `iconicName` |
| `title` | string | Lesson title — upsert key (combined with topic) |
| `orderInTopic` | number | Position within the topic |
| `exercises` | array | Nested exercises (see below) |

**Exercise object** (in the nested `exercises` array):

| Field | Type | Notes |
|---|---|---|
| `type` | string | Exercise type (see [NEW_EXERCISE_TYPES.md](NEW_EXERCISE_TYPES.md)) |
| `orderInLesson` | number | Position within the lesson — upsert key for exercises |
| `content` | object | JSON content per type. **Validated server-side per type.** Invalid content returns 400 on single create/update; per-item errors on import (bad items skipped, reported in response) |
| `customAiPrompt` | string \| null | Optional per-exercise AI prompt (legacy; admin UI always sends null now — use `content.ai_prompt` instead) |

> **Theory lessons.** Theory cards are seeded exactly like any other exercise —
> nested under a lesson with `"type": "theory_card"` and a `content` object whose
> `layout` selects the card template (`text`/`dialogue`/`bullets`/`quote`, validated
> server-side). A lesson whose exercises are **all** `theory_card` is treated as a
> theory lesson by the app. Example:
> ```json
> { "type": "theory_card", "orderInLesson": 1,
>   "content": { "layout": "text", "title": "Зачем теория", "body": "Короткий ввод." } }
> ```

### API endpoint

```
POST /admin/seeder/lessons
Authorization: Bearer <adminToken>
Content-Type: multipart/form-data

file:   <JSON file>
target: global          # required — see §0. Any other value returns 400
```

### Response `200 OK`

```json
{
  "lessonsCreated": 3,
  "lessonsUpdated": 1,
  "exercisesCreated": 10,
  "exercisesUpdated": 2,
  "errors": ["Exercise 2 in 'Opening the Call': missing required field 'options'"]
}
```

### Error responses

| Status | When |
|---|---|
| 400 | `target` missing or not `global` (§0), no file, unparseable JSON, missing required fields at lesson level |
| 401 | Missing/expired token |
| 403 | User is not Admin or SuperAdmin |
| 404 | Skill or topic not found |

---

## 4. Bundle Seeder — `/admin/seeder/bundle`

Imports an **entire content tree in one file**: skill → topics → lessons →
exercises. This is the convenient "one file, whole skill" path and is exposed in
the admin UI under **Bundle Import** (`/admin/import`), with a Download Template
button. Steps 1–3 above (skills / topics / lessons) remain available for partial,
level-by-level imports.

### JSON format

A `{ "skills": [...] }` object (a bare skills array is also accepted):

```json
{
  "skills": [
    {
      "iconicName": "cold-calling",
      "title": "Cold Calling",
      "description": "Mastering outbound cold calls",
      "orderInTree": 1,
      "stage": "preparation",
      "topics": [
        {
          "iconicName": "cold-calling-basics",
          "title": "Basics",
          "orderInSkill": 1,
          "lessons": [
            {
              "title": "Opening the call",
              "orderInTopic": 1,
              "exercises": [
                { "type": "choose_option", "orderInLesson": 1, "content": { } },
                { "type": "free_text", "orderInLesson": 2, "content": { }, "customAiPrompt": null }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

`topics`, `lessons`, and `exercises` are all optional at their level — you can
import just skills, or skills + topics, etc.

### API endpoint

```
POST /admin/seeder/bundle
Authorization: Bearer <adminToken>
Content-Type: multipart/form-data   (max 20 MB)

file:   <JSON file>
target: global          # required — see §0. Any other value returns 400
```

### Behavior

- **Idempotent upsert** — skills/topics by `iconicName`, lessons by
  `(topicId, title)`, exercises by `(lessonId, orderInLesson)`. Re-importing the
  same file is safe.
- **Per-type content validation** runs before each exercise is written; invalid
  exercises are skipped and reported in `errors[]` with a path prefix
  (`Lesson '...', exercise N (type): ...`), while the rest of the tree is still
  created.

### Response `200 OK`

```json
{
  "skillsCreated": 1, "skillsUpdated": 0,
  "topicsCreated": 1, "topicsUpdated": 0,
  "lessonsCreated": 2, "lessonsUpdated": 0,
  "exercisesCreated": 4, "exercisesUpdated": 0,
  "errors": []
}
```

### Error responses

| Status | When |
|---|---|
| 400 | `target` missing or not `global` (§0), no file, non-`.json` file, unparseable JSON, or root not an object/array |
| 401 | Missing/expired token |
| 403 | User is not Admin or SuperAdmin |

---

## Exercise Content Schemas

For the complete content schema and validation rules for each of the 10 exercise types, see [NEW_EXERCISE_TYPES.md](NEW_EXERCISE_TYPES.md). The canonical schemas are:

- `choose_option`, `fill_blank` — binary-choice types with options and correct index
- `reorder`, `match_pairs`, `categorize` — structured arrangement types
- `spot_mistake`, `rewrite`, `ai_dialogue`, `evaluate_call`, `free_text` — AI-evaluated types with `ai_prompt` field

Each type is validated on import; exercises with invalid `content` are skipped and reported in the response `errors` array.

## Export endpoints

`GET /admin/seeder/{skills,topics,lessons,bundle}/export` return the global library in exactly the
shape the matching import accepts, so an export round-trips. They take no `target`: there is only one
thing to export, and as of 40.19 they are narrowed to `organization_id IS NULL` for the reason given
in §0 — an export that carried one customer's overrides would re-import as if those were everybody's
content.

## Placeholders in seeded content

Seeded lesson text and exercise content may contain `{{organization.product}}`,
`{{organization.icp}}`, `{{organization.tone}}`, `{{organization.objections}}`,
`{{organization.script}}` and `{{organization.glossary.<term>}}`. They are stored verbatim and
resolved per organization at render time, never at import time — which is why the same seeded bundle
produces the same `ContentHash` for every customer. Full syntax, fallback behaviour and authoring
guidance: **[CONTENT_PARAMETERIZATION.md](CONTENT_PARAMETERIZATION.md)**.

Two things not to do in a seed file, both from that document: do not put a placeholder in an answer
key, and write each sentence so that it still reads correctly with the neutral fallback («ваш
продукт», «ваш клиент») substituted in, because there is no Russian declension engine behind this.

---

> **Microservices (Phase 8):** the content described here is now owned and served by the extracted **[learning-service](LEARNING_SERVICE.md)** through the gateway (Postgres `learning` DB). Paths, schemas and behaviour are unchanged. AI-graded exercise types are scored by the learning-service calling the ai-service `POST /ai/evaluate` (the learning-service still owns the `ExerciseTypePrompt` text and passes it in).
