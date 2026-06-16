# Скилы, темы, уроки и упражнения — справочник по контенту

> Этот документ описывает **модель контента** SalesTrainer и то, как наполнять её
> данными. Если вы пишете контент или импортируете его через seeder/admin API —
> читайте этот файл. Подробные JSON-схемы каждого типа упражнения — в
> [NEW_EXERCISE_TYPES.md](NEW_EXERCISE_TYPES.md).

## Иерархия контента

Контент образует дерево из **четырёх** уровней (а не двух):

```
Skill (скил)
└── Topic (тема)
    └── Lesson (урок)
        └── Exercise (упражнение)
```

> ⚠️ Важно: уроки принадлежат **темам**, а не скилам напрямую.
> `Lesson.TopicId → Topic`, `Topic.SkillId → Skill`. Эндпоинт
> `GET /skills/{slug}/lessons` под капотом находит скил, берёт его темы и собирает
> их уроки.

---

## Часть 1 — Скил (Skill)

Скил — это верхнеуровневый раздел обучения (например, «Холодные звонки»).

Сущность: `src/backend/api/Features/SkillTree/Models/Skill.cs`

### Поля скила

| Поле | Тип | Описание |
|------|-----|----------|
| `Id` | GUID | Первичный ключ |
| `IconicName` | строка | Уникальный slug-идентификатор. Используется как «адрес» скила в URL: `GET /skills/{IconicName}/lessons`. Должен быть уникальным и не меняться после создания |
| `Title` | строка | Название, которое видит пользователь. Например: `"Холодные звонки"` |
| `Description` | строка \| null | Необязательное описание |
| `OrderInTree` | число | Порядок отображения скила в дереве. Меньшее — выше |
| `Stage` | строка | `key` этапа воронки для группировки на `/tree`. По умолчанию `"general"`. Этапы хранятся в БД (`SkillStages`) и редактируются в админке (`/admin/skill-stages`); встроенные ключи: `preparation`, `discovery`, `engagement`, `closing`, `retention`. |

> Полей `slug`, `iconName`, `sortOrder`, `prerequisiteSkillId`,
> `applicableSalesTypes` в коде **нет** — это устаревшие имена из старой версии
> документа. Идентификатор называется `IconicName`, порядок — `OrderInTree`.

---

## Часть 2 — Тема (Topic)

Тема группирует уроки внутри скила.

Сущность: `src/backend/api/Features/SkillTree/Models/Topic.cs`

### Поля темы

| Поле | Тип | Описание |
|------|-----|----------|
| `Id` | GUID | Первичный ключ |
| `SkillId` | GUID | К какому скилу относится тема |
| `IconicName` | строка | Уникальный slug темы |
| `Title` | строка | Название темы |
| `OrderInSkill` | число | Порядок темы внутри скила |

---

## Часть 3 — Урок (Lesson)

Урок — контейнер для упражнений.

Сущность: `src/backend/api/Features/Lessons/Models/Lesson.cs`

### Поля урока

| Поле | Тип | Описание |
|------|-----|----------|
| `Id` | GUID | Первичный ключ |
| `TopicId` | GUID | К какой теме относится урок |
| `Title` | строка | Название урока |
| `OrderInTopic` | число | Порядок урока внутри темы |

> Поля `xpReward` у урока **нет** — XP начисляется логикой прогресса, а не из поля.

---

## Часть 4 — Упражнение (Exercise)

Упражнение — задание внутри урока.

Сущность: `src/backend/api/Features/Lessons/Models/Exercise.cs`

### Поля упражнения

| Поле | Тип | Описание |
|------|-----|----------|
| `Id` | GUID | Первичный ключ |
| `LessonId` | GUID | К какому уроку относится |
| `Type` | строка | Тип упражнения (см. [NEW_EXERCISE_TYPES.md](NEW_EXERCISE_TYPES.md)) |
| `OrderInLesson` | число | Порядок упражнения внутри урока |
| `SerializedContent` | строка (JSON) | Содержимое упражнения. Структура зависит от типа. По умолчанию `"{}"` |
| `CustomAiPrompt` | строка \| null | Доп. AI-промпт на уровне конкретного упражнения. Альтернативно можно положить `ai_prompt` прямо в `SerializedContent` |
| `CreatedAt` / `UpdatedAt` | дата | Аудит |

### 10 типов упражнений

Полный список и JSON-схема content/answer каждого типа — в
[NEW_EXERCISE_TYPES.md](NEW_EXERCISE_TYPES.md). Кратко:

| Тип | AI? | Кратко |
|-----|-----|--------|
| `choose_option` | нет | Выбор лучшего ответа |
| `fill_blank` | нет | Заполнить пропуск в диалоге |
| `reorder` | нет | Расставить по порядку |
| `match_pairs` | нет | Соединить пары (частичный зачёт) |
| `categorize` | нет | Разложить по категориям (частичный зачёт) |
| `spot_mistake` | да | Найти ошибку + объяснить |
| `rewrite` | да | Переписать текст лучше |
| `ai_dialogue` | да | Многоходовый диалог с AI-клиентом (текст/голос) |
| `evaluate_call` | да | Оценить транскрипт звонка |
| `free_text` | да | Свободный ответ |

---

## Оценивание

### Шкала

Все типы возвращают `score` в диапазоне **0–100**.

- **Детерминированные** (`choose_option`, `fill_blank`, `reorder`) — бинарно: 100 при верном ответе, 0 при неверном.
- **С частичным зачётом** (`match_pairs`, `categorize`) — `score = доля верных × 100`; `isCorrect = true` только если верны **все** элементы.
- **AI-оцениваемые** (`spot_mistake`, `rewrite`, `ai_dialogue`, `evaluate_call`, `free_text`) — модель возвращает `rating` 1–10, итог `score = rating × 10`.

### AI-модель и формат ответа

- Модель: `gpt-4.1-nano` (production) / `gpt-4.1-mini` (development).
  Ключ конфигурации: `OpenAI:OpenQuestionModel`.
- AI возвращает JSON: `{ "passed": bool, "rating": 1-10, "feedback": "текст" }`.
- Правило прохождения для AI-типов: `isCorrect = passed || rating >= 8`
  (порог `8` зашит в коде, см. `AiEvaluationStrategyBase`).

> ⚠️ Конфиг `ExerciseEvaluation:PassingScoreThreshold` (по умолчанию `7`)
> присутствует в `appsettings.json`, но в текущей логике оценивания **не
> используется** — AI-порог зашит как `rating >= 8`. Не полагайтесь на этот ключ
> при настройке контента.

### Промпты для AI-типов

Итоговый системный промпт собирается из двух уровней:

1. **Глобальный промпт типа** — таблица `ExerciseTypePrompts` (поле `SystemPrompt`),
   управляется через admin API (см. ниже).
2. **Промпт упражнения** — поле `ai_prompt` **внутри `SerializedContent` JSON**
   (каноническое место; колонка `CustomAiPrompt` — не используется оценщиком).

Они объединяются как: `[глобальный]` + `Дополнительные критерии:` + `[упражнения]`
+ инструкция о формате ответа.

**Валидация контента:** При создании/обновлении упражнения через admin API (`POST /admin/lessons/{id}/exercises`, `PUT /admin/exercises/{id}`) и при импорте (`POST /admin/lessons/{id}/exercises/import`) поле `content` валидируется серверной стороной per-type. Одиночные операции возвращают 400 с объединёнными ошибками; импорт валидирует per-item, пропускает плохие элементы и возвращает ошибки в response.

---

## Прогресс и статусы

### Статусы скила (вычисляются на лету)

Статус считается из количества пройденных уроков скила
(`SkillTreeService`), а **не** из цепочки prerequisite:

```csharp
status = completedLessons == 0          ? "available"
       : completedLessons >= totalLessons ? "completed"
       : "in_progress";
```

| Статус | Значение |
|--------|----------|
| `available` | Доступен (ещё нет пройденных уроков) |
| `in_progress` | Есть хотя бы один пройденный урок |
| `completed` | Все уроки пройдены |

> ⚠️ Механики **блокировки скилов prerequisite-цепочкой нет**. Поля
> `UserSkillProgress.Status` / `UserLessonProgress.Status` со значением по
> умолчанию `"locked"` существуют, но в расчёте дерева скилов сейчас не
> участвуют — все скилы стартуют как `available`. Если потребуется реальная
> блокировка, это нужно реализовать.

### Что происходит при верном ответе

1. Записывается попытка `UserExerciseAttempt` (UserId, ExerciseId, ответ, IsCorrect, Score, AI-фидбек, AttemptedAt).
2. Обновляется прогресс урока, начисляется XP, проверяются достижения.
3. Пересчитывается статус скила по формуле выше.

> ⚠️ **Очереди повтора упражнений нет.** Старая версия документа описывала
> систему «ошибся → упражнение в конец урока, до 3 попыток» — в коде такой
> механики **не существует**. Каждый сабмит просто создаёт новую запись
> `UserExerciseAttempt`; ограничения на число попыток нет, пользователь может
> отправлять ответ сколько угодно раз.

---

## API — справочник

### Learner (для обучающегося)

| Метод | URL | Что делает |
|-------|-----|------------|
| `GET` | `/skills` | Все скилы (с прогрессом, если авторизован) |
| `GET` | `/skills/{skillSlug}/lessons` | Уроки скила по `IconicName` (через темы) |
| `GET` | `/skills/{skillId}/topics` | Темы скила по GUID |
| `GET` | `/topics/{topicId}/lessons` | Уроки темы |
| `GET` | `/lessons` | Все уроки пользователя |
| `GET` | `/lessons/{lessonId}/exercises` | Упражнения урока |
| `GET` | `/skill-tree` | Полное дерево скилов пользователя (XP, streak) |
| `POST` | `/exercises/{exerciseId}/submit` | Отправить ответ на упражнение |
| `POST` | `/exercises/{exerciseId}/chat` | Сообщение в `ai_dialogue` (текст) |
| `POST` | `/exercises/{exerciseId}/voice/stream` | Голосовой стрим `ai_dialogue` (текст + MP3) |

### Admin — управление контентом

Все под префиксом `admin/`, требуют роль администратора.

**Скилы** (`AdminSkillsController`)

| Метод | URL |
|-------|-----|
| `GET` | `admin/skills` |
| `POST` | `admin/skills` |
| `PUT` | `admin/skills/{id}` |
| `DELETE` | `admin/skills/{id}` |

**Темы** (`AdminTopicsController`)

| Метод | URL |
|-------|-----|
| `GET` | `admin/topics` |
| `GET` | `admin/skills/{skillIconicName}/topics` |
| `POST` | `admin/skills/{skillIconicName}/topics` |
| `PUT` | `admin/skills/{skillIconicName}/topics/{topicIconicName}` |
| `PUT` | `admin/topics/{id}` |
| `DELETE` | `admin/topics/{id}` |

**Уроки** (`AdminLessonsController`)

| Метод | URL |
|-------|-----|
| `GET` | `admin/lessons` |
| `GET` | `admin/topics/{topicIconicName}/lessons` |
| `POST` | `admin/topics/{topicIconicName}/lessons` |
| `PUT` | `admin/lessons/{id}` |
| `DELETE` | `admin/lessons/{id}` |

**Упражнения** (`AdminExercisesController`)

| Метод | URL | Что делает |
|-------|-----|------------|
| `GET` | `admin/lessons/{lessonId}/exercises` | Упражнения урока |
| `POST` | `admin/lessons/{lessonId}/exercises` | Создать упражнение |
| `POST` | `admin/lessons/{lessonId}/exercises/import` | Массовый импорт (JSON-массив) |
| `PUT` | `admin/exercises/{id}` | Изменить |
| `DELETE` | `admin/exercises/{id}` | Удалить |

**Промпты типов упражнений** (`AdminExerciseTypePromptsController`)

| Метод | URL | Что делает |
|-------|-----|------------|
| `GET` | `admin/exercise-type-prompts` | Все глобальные промпты |
| `GET` | `admin/exercise-type-prompts/{exerciseType}` | Промпт типа |
| `PUT` | `admin/exercise-type-prompts/{exerciseType}` | Создать/обновить (upsert) |

### Seeder — массовое наполнение из JSON

`AdminSeederController`. Файл загружается через `multipart/form-data`
(лимит 10 МБ). Идемпотентный **upsert**, возвращает `{ created, updated, errors[] }`.

| Метод | URL | Ключ совпадения (upsert) |
|-------|-----|--------------------------|
| `POST` | `admin/seeder/bundle` | целое дерево за один файл (см. ниже) |
| `POST` | `admin/seeder/skills` | по `IconicName` |
| `POST` | `admin/seeder/topics` | по `IconicName` |
| `POST` | `admin/seeder/lessons` | уроки по `(TopicId, Title)`, вложенные упражнения по `(LessonId, OrderInLesson)` |

> Эндпоинт `admin/seeder/lessons` принимает уроки с **вложенным массивом
> упражнений** — то есть поддерево «уроки + упражнения» можно залить одним файлом.

#### Bundle import — всё дерево одним файлом

`POST admin/seeder/bundle` (лимит 20 МБ) принимает **весь контент-граф разом**:
скил → темы → уроки → упражнения. Это самый удобный путь «посадить админа и
залить контент». В UI — вкладка **Bundle Import** (`/admin/import`) с кнопкой
Download Template.

Формат файла — объект `{ "skills": [...] }` (или просто массив скилов):

```json
{
  "skills": [
    {
      "iconicName": "cold-calling",
      "title": "Холодные звонки",
      "description": "…",
      "orderInTree": 1,
      "stage": "preparation",
      "topics": [
        {
          "iconicName": "cold-calling-basics",
          "title": "Основы",
          "orderInSkill": 1,
          "lessons": [
            {
              "title": "Открытие звонка",
              "orderInTopic": 1,
              "exercises": [
                { "type": "choose_option", "orderInLesson": 1, "content": { /* по схеме типа */ } },
                { "type": "free_text", "orderInLesson": 2, "content": { /* … */ }, "customAiPrompt": null }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

Upsert-ключи те же, что у пошаговых сидеров (скилы/темы по `iconicName`, уроки по
`(TopicId, Title)`, упражнения по `(LessonId, OrderInLesson)`) — **повторный
импорт того же файла безопасен**. Контент каждого упражнения валидируется по типу
до записи; невалидные упражнения пропускаются и попадают в `errors` с указанием
пути (`Lesson '…', exercise N (type): …`), остальное дерево создаётся.

Ответ — `BundleImportResultDto`: счётчики `*Created`/`*Updated` по каждому уровню
плюс `errors[]`.

---

## Чеклист для автора контента

1. Создать скил (`admin/skills` или `seeder/skills`) — задать `IconicName`, `Title`, `OrderInTree`, `Stage`.
2. Создать темы скила (`seeder/topics`) — `IconicName`, `Title`, `OrderInSkill`, `SkillId`.
3. Создать уроки + упражнения (`seeder/lessons`) — урок: `Title`, `OrderInTopic`; упражнения: `Type`, `OrderInLesson`, `Content` (JSON по схеме типа), опц. `CustomAiPrompt`.
4. Для AI-типов при необходимости задать глобальный промпт типа (`admin/exercise-type-prompts/{type}`).
5. Проверить `Content` каждого упражнения по схеме из [NEW_EXERCISE_TYPES.md](NEW_EXERCISE_TYPES.md).
