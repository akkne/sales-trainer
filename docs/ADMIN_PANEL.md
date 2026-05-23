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
| POST | /admin/skills | `{iconicName, title, description?, orderInTree, stage?}` | `AdminSkillDto` |
| PUT | /admin/skills/:id | `{iconicName?, title?, description?, orderInTree?, stage?}` | `AdminSkillDto` |
| DELETE | /admin/skills/:id | — | 204 |

### Lessons
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/lessons | — | `AdminLessonWithSkillDto[]` (all lessons) |
| GET | /admin/skills/:skillId/lessons | — | `AdminLessonDto[]` |
| POST | /admin/skills/:skillId/lessons | `{title, sortOrder, difficultyLevel, xpReward}` | `AdminLessonDto` |
| PUT | /admin/lessons/:id | same | `AdminLessonDto` |
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
| GET | /admin/reference | query: `?skillId=&category=&search=` | `AdminReferenceMaterialDto[]` |
| GET | /admin/reference/categories | — | `string[]` |
| GET | /admin/skills/:skillId/reference | — | `AdminReferenceMaterialDto[]` |
| POST | /admin/skills/:skillId/reference | `{title, markdownContent, sortOrder, category?, tags?}` | `AdminReferenceMaterialDto` |
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
      page.tsx         ← skill list + inline JSON import
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
      page.tsx         ← all lessons view + inline JSON import
    reference/
      page.tsx         ← global reference materials view
    dialog/
      page.tsx         ← dialog bundles management
    open-question/
      page.tsx         ← AI prompts management
    users/
      page.tsx         ← user list + role management (superadmin only)
```

---

## UI principles

- Minimal, functional, monochrome color scheme
- Standard HTML-like forms via Tailwind utility classes
- Tables for list views
- Inline delete confirmation (no separate modal — just a button state change to "Confirm?")
- JSON import sections collapsible on each entity page (Skills, Lessons)

---

## JSON Import Workflow

JSON import is available inline on Skills and Lessons pages:

1. Click "Import JSON" button
2. Download template for reference (shows all supported fields)
3. Upload your JSON file
4. View import results (created/updated counts, errors)


### Lessons Template (with all 10 exercise types)
```json
[
  {
    "topicIconicName": "cold-calls",
    "title": "Opening the Call",
    "orderInTopic": 1,
    "exercises": [
      {
        "type": "choose_option",
        "orderInLesson": 1,
        "content": {
          "situation": "Клиент говорит: 'Это слишком дорого'",
          "options": [
            { "text": "Да, понимаю. Могу предложить скидку.", "is_correct": false },
            { "text": "Скажите, дорого относительно чего?", "is_correct": true },
            { "text": "Это лучшая цена на рынке.", "is_correct": false }
          ],
          "explanation": "Лучше уточнить причину возражения."
        }
      },
      {
        "type": "fill_blank",
        "orderInLesson": 2,
        "content": {
          "before": "Клиент: У нас уже есть поставщик.",
          "after": "Клиент: Ну, в целом да, можно обсудить.",
          "options": [
            { "text": "Понял, но мы лучше!", "is_correct": false },
            { "text": "А что если я покажу, как сэкономить 20%?", "is_correct": true },
            { "text": "Жаль, до свидания.", "is_correct": false }
          ]
        }
      },
      {
        "type": "reorder",
        "orderInLesson": 3,
        "content": {
          "instruction": "Расставьте этапы холодного звонка",
          "items": [
            { "text": "Приветствие", "correct_position": 1 },
            { "text": "Выявление потребности", "correct_position": 2 },
            { "text": "Презентация", "correct_position": 3 },
            { "text": "Работа с возражениями", "correct_position": 4 }
          ],
          "explanation": "Сначала понять потребность, потом предлагать."
        }
      },
      {
        "type": "match_pairs",
        "orderInLesson": 4,
        "content": {
          "instruction": "Соедините возражение с техникой",
          "pairs": [
            { "left": "Слишком дорого", "right": "Сравнение ценности" },
            { "left": "Нам ничего не нужно", "right": "Техника бумеранга" },
            { "left": "Отправьте на почту", "right": "Техника моста" }
          ],
          "explanation": "Каждое возражение требует своего подхода."
        }
      },
      {
        "type": "categorize",
        "orderInLesson": 5,
        "content": {
          "instruction": "Распределите вопросы по категориям",
          "categories": ["Хороший вопрос", "Плохой вопрос"],
          "items": [
            { "text": "Какие цели на квартал?", "category": "Хороший вопрос" },
            { "text": "Вам нравится наш продукт?", "category": "Плохой вопрос" }
          ],
          "explanation": "Хорошие вопросы открытые и про понимание."
        }
      },
      {
        "type": "spot_mistake",
        "orderInLesson": 6,
        "content": {
          "dialogue": [
            { "speaker": "seller", "text": "Добрый день!", "is_mistake": false },
            { "speaker": "seller", "text": "Мы лучшая CRM!", "is_mistake": true },
            { "speaker": "client", "text": "Нам ничего не нужно.", "is_mistake": false }
          ],
          "explanation": "Питч вместо discovery — ошибка.",
          "ai_prompt": "Оцени понимание проблемы питча."
        }
      },
      {
        "type": "rewrite",
        "orderInLesson": 7,
        "content": {
          "instruction": "Перепишите тему письма цепляюще",
          "original": "Предложение о сотрудничестве",
          "evaluation_criteria": ["Персонализация", "Интрига", "Краткость"],
          "ai_prompt": "Оцени улучшение темы письма."
        }
      },
      {
        "type": "ai_dialogue",
        "orderInLesson": 8,
        "content": {
          "persona": "Скептик Сергей",
          "scenario": "Discovery-звонок",
          "context": "IT-директор, скептичен, торопится",
          "max_turns": 6,
          "success_criteria": ["Качество вопросов", "Работа со скептицизмом"],
          "ai_prompt": "Оцени диалог продавца."
        }
      },
      {
        "type": "evaluate_call",
        "orderInLesson": 9,
        "content": {
          "transcript": [
            { "speaker": "seller", "text": "Здравствуйте, это Алексей." },
            { "speaker": "client", "text": "Добрый день." },
            { "speaker": "seller", "text": "Рассматриваете новые решения?" }
          ],
          "evaluation_axes": [
            { "name": "Квалификация", "description": "Была ли квалификация?" },
            { "name": "Открытые вопросы", "description": "Использовались ли?" }
          ],
          "ai_prompt": "Сравни оценку с анализом звонка."
        }
      },
      {
        "type": "free_text",
        "orderInLesson": 10,
        "content": {
          "situation": "Клиент: 'Это слишком дорого'",
          "instruction": "Напишите ответ на возражение",
          "evaluation_criteria": ["Не снижает цену", "Выясняет причину"],
          "ai_prompt": "Оцени ответ на возражение."
        }
      }
    ]
  }
]
```

---

## Exercise Types (10 total)

| Type | Description | Key Content Fields |
|------|-------------|-------------------|
| `choose_option` | Select best answer from options | situation, options: [{text, is_correct}], explanation |
| `fill_blank` | Fill gap in dialogue | before, after, options: [{text, is_correct}] |
| `reorder` | Arrange items in sequence | instruction, items: [{text, correct_position}], explanation |
| `match_pairs` | Connect left/right columns | instruction, pairs: [{left, right}], explanation |
| `categorize` | Sort items into buckets | instruction, categories[], items: [{text, category}], explanation |
| `spot_mistake` | Identify mistake in dialog | dialogue: [{speaker, text, is_mistake}], explanation, ai_prompt |
| `rewrite` | Improve given text | instruction, original, evaluation_criteria[], ai_prompt |
| `ai_dialogue` | Practice with AI persona | persona, scenario, context, max_turns, success_criteria[], ai_prompt |
| `evaluate_call` | Evaluate transcript quality | transcript: [{speaker, text}], evaluation_axes: [{name, description}], ai_prompt |
| `free_text` | Write based on prompt | situation, instruction, evaluation_criteria[], ai_prompt |

See `src/frontend/lib/exerciseTypes.ts` for TypeScript constants.
See `src/backend/api/Features/Exercises/ExerciseTypes.cs` for C# constants.

---

## Visual Exercise Editor

The admin panel provides a visual editor for all 10 exercise types at:
`/admin/skills/[skillId]/topics/[topicId]/lessons/[lessonId]/exercises`

Features:
- Type-specific form fields for each exercise type
- Drag reordering with up/down arrows
- Inline preview of content
- Add/edit/delete exercises without raw JSON editing
- Auto-assigns orderInLesson based on position

Each exercise type has a dedicated editor component in `src/frontend/components/admin/exercise-editors/`:
- ChooseOptionEditor.tsx
- FillBlankEditor.tsx
- ReorderEditor.tsx
- MatchPairsEditor.tsx
- CategorizeEditor.tsx
- SpotMistakeEditor.tsx
- RewriteEditor.tsx
- AiDialogueEditor.tsx
- EvaluateCallEditor.tsx
- FreeTextEditor.tsx

---

## Ordering Rules

- **Lessons** have `sortOrder` by their position within a skill
- **Exercises** have `sortOrder` by their position within a lesson
- Backend queries always `OrderBy(x => x.SortOrder)` to ensure consistent ordering
- Visual editor allows reordering via up/down arrows
