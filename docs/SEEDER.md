# Content Seeders

Administrators can bulk-import content via the admin panel using two separate seeders.

---

## 1. Skills Seeder — `/admin/seeder`

Imports skills only. Accepts **CSV** or **JSON**.

### CSV format

| Column | Type | Notes |
|---|---|---|
| `slug` | string | Unique identifier — upsert key |
| `title` | string | Display title |
| `icon_name` | string | Icon name (e.g. `phone`, `handshake`) |
| `sort_order` | integer | Position in the skill tree |
| `sales_types` | string | Pipe-separated list: `b2b_saas\|retail\|b2c` |

```csv
slug,title,icon_name,sort_order,sales_types
cold-calling,Cold Calling,phone,1,b2b_saas|b2c
```

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

`stage` is optional. Known values: `preparation`, `discovery`, `engagement`, `closing`, `retention`. Defaults to `general` when omitted on create; preserved when omitted on update.

### API endpoint

```
POST /admin/seeder/skills
Authorization: Bearer <adminToken>
Content-Type: multipart/form-data

file: <CSV or JSON file>
```

### Success response `200 OK`

```json
{
  "skillsCreated": 2,
  "skillsUpdated": 0,
  "errors": []
}
```

---

## 2. Content Import — `/admin/content`

Imports lessons and exercises for a **selected skill**. Accepts **CSV** or **JSON**.

The skill is selected via dropdown in the admin panel — no skill metadata is included in the import file.

### CSV format

One row = one exercise. Lessons are auto-created or updated by title.

| Column | Type | Notes |
|---|---|---|
| `lesson_title` | string | Upsert key within the skill |
| `lesson_sort_order` | integer | Order within the skill |
| `lesson_difficulty` | integer | 1 = easy, 2 = medium, 3 = hard |
| `lesson_xp` | integer | XP awarded on lesson completion |
| `exercise_type` | string | `multiple_choice` / `fill_blank` / `free_text` |
| `exercise_sort_order` | integer | Order within the lesson — upsert key for exercises |
| `exercise_content_json` | JSON | Must be valid JSON; quote the field in CSV |

```csv
lesson_title,lesson_sort_order,lesson_difficulty,lesson_xp,exercise_type,exercise_sort_order,exercise_content_json
Opening the Call,1,1,50,multiple_choice,1,"{""situation"":""You just dialed."",""question"":""Best opener?"",""options"":[""Hi boss"",""Hi I'm Alex"",""Buy something?""],""correctOptionIndex"":1}"
```

### JSON format

Lessons contain nested exercises — no need for repeated lesson fields per row.

```json
[
  {
    "title": "Opening the Call",
    "sortOrder": 1,
    "difficultyLevel": 1,
    "xpReward": 50,
    "exercises": [
      {
        "type": "multiple_choice",
        "sortOrder": 1,
        "content": {
          "situation": "You just dialed a prospect.",
          "question": "Best opener?",
          "options": ["Hi boss", "Hi I'm Alex from Acme", "Buy something?"],
          "correctOptionIndex": 1,
          "explanation": "A friendly opener sets the tone."
        }
      },
      {
        "type": "fill_blank",
        "sortOrder": 2,
        "content": {
          "characterName": "Prospect",
          "characterLine": "Who is this?",
          "options": ["Nobody.", "I'm Alex from Acme.", "Please don't hang up!"],
          "correctOptionIndex": 1
        }
      }
    ]
  }
]
```

### API endpoint

```
POST /admin/seeder/lessons?skillId={guid}
Authorization: Bearer <adminToken>
Content-Type: multipart/form-data

file: <CSV or JSON file>
```

### Success response `200 OK`

```json
{
  "lessonsCreated": 3,
  "lessonsUpdated": 1,
  "exercisesCreated": 10,
  "exercisesUpdated": 2,
  "errors": []
}
```

### Error responses

| Status | When |
|---|---|
| 400 | No file, wrong extension, missing skillId, unparseable file, missing required columns |
| 401 | Missing/expired token |
| 403 | User is not Admin or SuperAdmin |
| 404 | Skill not found for given skillId |

---

## exercise_content_json shapes

**multiple_choice**
```json
{
  "situation": "...",
  "question": "...",
  "options": ["A", "B", "C"],
  "correctOptionIndex": 1,
  "explanation": "optional"
}
```

**fill_blank**
```json
{
  "characterName": "Prospect",
  "characterLine": "...",
  "options": ["A", "B", "C"],
  "correctOptionIndex": 0,
  "explanation": "optional"
}
```

**free_text**
```json
{
  "situation": "...",
  "prompt": "...",
  "evaluationCriteria": "..."
}
```
