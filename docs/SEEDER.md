# Content Seeders

Administrators can bulk-import content via the admin panel using three seeder endpoints: skills, topics, and lessons. All seeders accept **JSON only** and perform idempotent upsert operations.

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

file: <JSON file>
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

file: <JSON file>
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

### API endpoint

```
POST /admin/seeder/lessons
Authorization: Bearer <adminToken>
Content-Type: multipart/form-data

file: <JSON file>
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
| 400 | No file, unparseable JSON, missing required fields at lesson level |
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

file: <JSON file>
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
| 400 | No file, non-`.json` file, unparseable JSON, or root not an object/array |
| 401 | Missing/expired token |
| 403 | User is not Admin or SuperAdmin |

---

## Exercise Content Schemas

For the complete content schema and validation rules for each of the 10 exercise types, see [NEW_EXERCISE_TYPES.md](NEW_EXERCISE_TYPES.md). The canonical schemas are:

- `choose_option`, `fill_blank` — binary-choice types with options and correct index
- `reorder`, `match_pairs`, `categorize` — structured arrangement types
- `spot_mistake`, `rewrite`, `ai_dialogue`, `evaluate_call`, `free_text` — AI-evaluated types with `ai_prompt` field

Each type is validated on import; exercises with invalid `content` are skipped and reported in the response `errors` array.
