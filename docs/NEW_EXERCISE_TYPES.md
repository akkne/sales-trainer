# Exercise Types — Reference

> This document describes all 11 exercise types in the SalesTrainer platform.
> For the content model (Skill → Topic → Lesson → Exercise), admin/seeder API, and
> progress rules, see [SKILLS_AND_EXERCISES.md](SKILLS_AND_EXERCISES.md).

## Overview

| # | Type Key | Name (RU) | AI Required | Description |
|---|----------|-----------|-------------|-------------|
| 1 | `choose_option` | Выбери лучший ответ | No | Select best answer from 2-4 options |
| 2 | `fill_blank` | Заполни пропуск | No | Fill gap in dialogue context |
| 3 | `reorder` | Расставь по порядку | No | Drag-drop items into correct sequence |
| 4 | `match_pairs` | Соедини пары | No | Connect items from two columns |
| 5 | `categorize` | Разложи по категориям | No | Sort items into 2-3 buckets |
| 6 | `spot_mistake` | Найди ошибку | Yes | Find mistake in dialog, explain |
| 7 | `rewrite` | Перепиши лучше | Yes | Improve weak text, AI evaluates |
| 8 | `ai_dialogue` | Текстовый диалог | Yes | Multi-turn text chat with AI customer |
| 9 | `evaluate_call` | Оцени звонок | Yes | Evaluate call transcript, compare with AI |
| 10 | `free_text` | Напиши ответ | Yes | Free-form answer, AI evaluates |
| 11 | `theory_card` | Карточка теории | No | Non-graded "story" card the learner swipes through |

> **Theory lessons.** A lesson made up entirely of `theory_card` exercises is a
> *theory lesson*: the learner swipes through the cards like stories (no answer, no
> grading), and reaching the last card marks the lesson complete. Theory lessons
> count toward `completedLessons`/`totalLessons` exactly like practice lessons and
> do **not** block later lessons. See Type 11 below.

---

## Constants

### Backend (C#)
```csharp
// src/backend/api/Features/Exercises/ExerciseTypes.cs
public static class ExerciseTypes
{
    public const string ChooseOption = "choose_option";
    public const string FillBlank = "fill_blank";
    public const string Reorder = "reorder";
    public const string MatchPairs = "match_pairs";
    public const string Categorize = "categorize";
    public const string SpotMistake = "spot_mistake";
    public const string Rewrite = "rewrite";
    public const string AiDialogue = "ai_dialogue";
    public const string EvaluateCall = "evaluate_call";
    public const string FreeText = "free_text";
    public const string TheoryCard = "theory_card";
}
```

### Frontend (TypeScript)
```typescript
// src/frontend/lib/exerciseTypes.ts
export const ExerciseTypes = {
    ChooseOption: 'choose_option',
    FillBlank: 'fill_blank',
    Reorder: 'reorder',
    MatchPairs: 'match_pairs',
    Categorize: 'categorize',
    SpotMistake: 'spot_mistake',
    Rewrite: 'rewrite',
    AiDialogue: 'ai_dialogue',
    EvaluateCall: 'evaluate_call',
    FreeText: 'free_text',
    TheoryCard: 'theory_card',
} as const;
```

---

## Type 1: Choose Option (`choose_option`)

### Purpose
User selects the best answer from 2-4 options. Tests knowledge and basic intuition.

### Content Schema
```json
{
    "situation": "Клиент говорит: 'Это слишком дорого'",
    "options": [
        { "text": "Да, понимаю. Могу предложить скидку.", "is_correct": false },
        { "text": "Скажите, дорого относительно чего?", "is_correct": true },
        { "text": "Это лучшая цена на рынке.", "is_correct": false }
    ],
    "explanation": "Лучше уточнить причину возражения, чем сразу снижать цену."
}
```

### Answer Schema
```json
{ "selectedOptionIndex": 1 }
```

---

## Type 2: Fill Blank (`fill_blank`)

### Purpose
User fills a gap in dialogue, seeing context before and after. Tests contextual understanding.

### Content Schema
```json
{
    "before": "Клиент: У нас уже есть поставщик.",
    "after": "Клиент: Ну, в целом да, можно обсудить.",
    "options": [
        { "text": "Понял, но мы лучше!", "is_correct": false },
        { "text": "А что если я покажу, как можно сэкономить 20%?", "is_correct": true },
        { "text": "Жаль, до свидания.", "is_correct": false }
    ]
}
```

### Answer Schema
```json
{ "selectedOptionIndex": 1 }
```

---

## Type 3: Reorder (`reorder`)

### Purpose
User arranges 4-6 items in correct sequence. Tests process understanding.

### Content Schema
```json
{
    "instruction": "Расставьте этапы холодного звонка в правильном порядке",
    "items": [
        { "text": "Приветствие", "correct_position": 1 },
        { "text": "Выявление потребности", "correct_position": 2 },
        { "text": "Презентация", "correct_position": 3 },
        { "text": "Работа с возражениями", "correct_position": 4 },
        { "text": "Закрытие", "correct_position": 5 }
    ],
    "explanation": "Важно сначала понять потребность, потом предлагать решение."
}
```

### Answer Schema
```json
{ "order": [0, 1, 2, 3, 4] }
```

---

## Type 4: Match Pairs (`match_pairs`)

### Purpose
User connects items from left column to right column. Tests associations.

### Content Schema
```json
{
    "instruction": "Соедините возражение с лучшей техникой ответа",
    "pairs": [
        { "left": "Слишком дорого", "right": "Техника сравнения ценности" },
        { "left": "Нам ничего не нужно", "right": "Техника бумеранга" },
        { "left": "Отправьте на почту", "right": "Техника моста" }
    ],
    "explanation": "Каждое возражение требует своего подхода."
}
```

### Answer Schema
```json
{
    "pairs": [
        { "left": "Слишком дорого", "right": "Техника сравнения ценности" },
        { "left": "Нам ничего не нужно", "right": "Техника бумеранга" },
        { "left": "Отправьте на почту", "right": "Техника моста" }
    ]
}
```

---

## Type 5: Categorize (`categorize`)

### Purpose
User sorts 4-8 items into 2-3 categories. Tests judgment.

### Content Schema
```json
{
    "instruction": "Распределите вопросы по категориям",
    "categories": ["Хороший вопрос", "Плохой вопрос"],
    "items": [
        { "text": "Сколько у вас сотрудников?", "category": "Хороший вопрос" },
        { "text": "Вам нравится наш продукт?", "category": "Плохой вопрос" },
        { "text": "Какие цели на этот квартал?", "category": "Хороший вопрос" },
        { "text": "Хотите скидку?", "category": "Плохой вопрос" }
    ],
    "explanation": "Хорошие discovery-вопросы открытые и направлены на понимание."
}
```

### Answer Schema
```json
{ "mapping": { "0": "Хороший вопрос", "1": "Плохой вопрос", "2": "Хороший вопрос", "3": "Плохой вопрос" } }
```

---

## Type 6: Spot Mistake (`spot_mistake`)

### Purpose
User identifies mistake in dialog and optionally explains why. AI evaluates explanation.

### Content Schema
```json
{
    "dialogue": [
        { "speaker": "seller", "text": "Добрый день! Меня зовут Алексей.", "is_mistake": false },
        { "speaker": "client", "text": "Добрый день.", "is_mistake": false },
        { "speaker": "seller", "text": "Мы лучшая CRM на рынке!", "is_mistake": true },
        { "speaker": "client", "text": "Нам ничего не нужно.", "is_mistake": false }
    ],
    "explanation": "Продавец сразу начал питчить вместо вопроса о потребности.",
    "ai_prompt": "Оцени, понял ли пользователь, что проблема в питче вместо discovery."
}
```

### Answer Schema
```json
{ "selectedLineIndex": 2, "explanation": "Продавец сразу питчит вместо вопроса" }
```

---

## Type 7: Rewrite (`rewrite`)

### Purpose
User improves weak text. AI evaluates improvement against criteria.

### Content Schema
```json
{
    "instruction": "Перепишите тему холодного письма более цепляюще",
    "original": "Предложение о сотрудничестве",
    "evaluation_criteria": [
        "Персонализация",
        "Интрига без кликбейта",
        "Краткость (до 50 символов)"
    ],
    "ai_prompt": "Оцени улучшенную тему письма по критериям."
}
```

### Answer Schema
```json
{ "rewrittenText": "Сергей, 3 способа сократить цикл продаж в IT" }
```

---

## Type 8: AI Dialogue (`ai_dialogue`)

### Purpose
Multi-turn cold-call simulation where the AI plays the customer. Tests conversation skills.

### Interaction model
- **User speaks first.** The salesperson (user) opens the call — there is no AI greeting.
  Backend returns an empty turn for an empty message; the AI only replies after the opening line.
- **Two modes, chosen by the user before the dialog starts:**
  - **Text** — type replies, posted to `POST /exercises/:id/chat`.
  - **Voice** — speak aloud (`useExerciseVoice` hook), streamed from
    `POST /exercises/:id/voice/stream`. Reuses the exact STT/VAD/TTS pipeline as live
    calls (WebSpeech STT → exercise voice stream → MP3 playback). The AI can hang up
    (`endCall`/isStopSignal) to end the dialog naturally.
  - Both modes share the same chat history and feed the same completion submission.

### Content Schema
```json
{
    "persona": "Скептик Сергей",
    "scenario": "Discovery-звонок с IT-директором",
    "context": "Клиент скептически настроен, отвечает коротко, торопится",
    "max_turns": 6,
    "success_criteria": [
        "Качество вопросов",
        "Работа со скептицизмом",
        "Достижение следующего шага"
    ],
    "ai_prompt": "Оцени диалог продавца по критериям."
}
```

### Answer Schema
```json
{
    "messages": [
        { "role": "assistant", "content": "Да, слушаю, быстро." },
        { "role": "user", "content": "Скажите, какую CRM используете?" },
        { "role": "assistant", "content": "Никакую. Всё в Excel." }
    ],
    "completedNaturally": true
}
```

---

## Type 9: Evaluate Call (`evaluate_call`)

### Purpose
User reads call transcript and rates by criteria. AI compares with its own analysis.

### Content Schema
```json
{
    "transcript": [
        { "speaker": "seller", "text": "Здравствуйте, это Алексей из компании Рост." },
        { "speaker": "client", "text": "Добрый день." },
        { "speaker": "seller", "text": "Скажите, вы рассматриваете новые решения для продаж?" }
    ],
    "evaluation_axes": [
        { "name": "Квалификация", "description": "Была ли проведена квалификация?" },
        { "name": "Открытые вопросы", "description": "Использовались ли открытые вопросы?" },
        { "name": "Следующий шаг", "description": "Был ли согласован следующий шаг?" }
    ],
    "ai_prompt": "Сравни оценку пользователя с объективным анализом звонка."
}
```

### Answer Schema
```json
{
    "ratings": { "Квалификация": 3, "Открытые вопросы": 4, "Следующий шаг": 5 },
    "overallComment": "Хороший звонок, но квалификация поверхностная."
}
```

---

## Type 10: Free Text (`free_text`)

### Purpose
User writes free-form response to prompt. AI evaluates against criteria.

### Content Schema
```json
{
    "situation": "Клиент говорит: 'Это слишком дорого для нас'",
    "instruction": "Напишите ответ на это возражение",
    "evaluation_criteria": [
        "Не снижает цену сразу",
        "Выясняет причину возражения",
        "Профессиональный тон"
    ],
    "ai_prompt": "Оцени ответ на возражение 'дорого' по критериям."
}
```

### Answer Schema
```json
{ "text": "Понимаю вашу позицию. Скажите, когда вы говорите 'дорого' — это относительно бюджета или ценности?" }
```

---

## Type 11: Theory Card (`theory_card`)

### Purpose
A non-graded "story" card the learner swipes through *before* practice. There is no
answer and no AI evaluation — it exists to deliver knowledge. A lesson built entirely
of `theory_card` exercises is a **theory lesson**: cards are shown stories-style with a
progress indicator on top; the last card's button becomes "Завершить", and reaching it
marks the lesson complete (same completion path as practice). A theory lesson can hold
many cards (8, 12, or more) — the player switches from segmented progress bars to a
compact `N / M` counter when there are too many segments to read.

### Interaction model
- The learner taps/clicks "Далее" (or the right side of the card) to advance, and the
  left side / back arrow to go back. No answer is collected.
- On the **last card**, "Далее" becomes "Завершить". Finishing submits **once** (the
  last card) to `POST /exercises/:id/submit` with an empty answer `{}` — this is the
  only graded-shaped call and it always succeeds, so the lesson is completed and the
  fixed theory XP is awarded a single time (it does not multiply per card).
- XP is the base reward for `theory_card` in the `ExerciseTypeRewards` table
  (seeded at **5**, admin-editable) — intentionally smaller than a practice exercise.

### Content Schema
The `layout` field selects the visual template and the fields under it. Four layouts:

**`text`** — heading + body (one paragraph per `\n`):
```json
{ "layout": "text", "title": "Что такое СПИН", "body": "СПИН — методика вопросов.\nS — ситуационные." }
```

**`dialogue`** — short client/seller exchange, rendered with the **same** chat bubbles
as the Guidebook (Справочник). `side: "me"` = salesperson (right bubble),
`side: "them"` = client (left bubble); `annotations` are optional tags shown after the text:
```json
{
    "layout": "dialogue",
    "title": "Уточняющий вопрос вместо скидки",
    "turns": [
        { "side": "them", "text": "Это слишком дорого." },
        { "side": "me", "text": "Подскажите, дорого относительно чего?", "annotations": ["уточнение"] },
        { "side": "them", "text": "Ну, у конкурентов дешевле." }
    ]
}
```

**`bullets`** — heading + list of points:
```json
{ "layout": "bullets", "title": "Правила первого звонка", "items": ["Слушай клиента", "Задавай открытые вопросы"] }
```

**`quote`** — a large quote/principle with an optional source:
```json
{ "layout": "quote", "text": "Люди покупают у тех, кому доверяют.", "author": "Зиг Зиглар" }
```

### Validation rules
- `layout` is required and must be one of `text`, `dialogue`, `bullets`, `quote`.
- `text`: `body` is a required non-empty string; `title` optional.
- `dialogue`: `turns` is a non-empty array; each turn needs `side` ∈ {`me`,`them`} and a non-empty `text`.
- `bullets`: `items` is a non-empty array of non-empty strings.
- `quote`: `text` is a required non-empty string; `author` optional.

### Answer Schema
No answer. The completion submission sends `{}`; the evaluator always returns
`{ isCorrect: true, score: 100 }` and never calls AI.

---

## Scoring

All types return `score` on a **0–100** scale. `isCorrect` rules differ:

| Type | Scoring | `isCorrect` |
|------|---------|-------------|
| `choose_option`, `fill_blank`, `reorder` | Binary: 100 or 0 | score == 100 |
| `match_pairs`, `categorize` | Partial: `(correct / total) × 100` | all items correct |
| `spot_mistake` | 50 pts for the right line + 0–50 from AI on the explanation | score ≥ 75 |
| `rewrite`, `free_text`, `evaluate_call` | AI: `score = rating × 10` | `passed \|\| rating ≥ 8` |
| `ai_dialogue` | AI, but score 30 / fail if user turns < `max_turns / 2` | `passed \|\| rating ≥ 8` |
| `theory_card` | Not graded — always 100 | always `true` (completion only) |

> The config key `ExerciseEvaluation:PassingScoreThreshold` (default `7`) exists in
> `appsettings.json` but is **not** used by the current evaluation logic — the AI
> pass threshold is hardcoded as `rating >= 8` in `AiEvaluationStrategyBase`.

## AI Configuration

For AI-powered types (6-10), prompts are configured at two levels:

1. **Global Type Prompts** — stored in the `ExerciseTypePrompts` table
   (managed via `PUT admin/exercise-type-prompts/{exerciseType}`).
2. **Per-Exercise Prompts** — the `ai_prompt` field inside the exercise content
   JSON, or the `Exercise.CustomAiPrompt` column.

Combined prompt = Global prompt + `Дополнительные критерии:` + per-exercise prompt
+ response-format instruction.

### Model
AI evaluation uses `OpenAI:OpenQuestionModel`:
- **Production:** `gpt-4.1-nano`
- **Development:** `gpt-4.1-mini`

(The legacy `FreeTextModel` / `gpt-4o-mini` key is not used by the current
evaluation path.)

### AI Response Format
All AI-powered types expect the model to return:
```json
{
    "passed": true,
    "rating": 8,
    "feedback": "Detailed feedback text..."
}
```
`rating` is 1–10 (defaults to 5 if missing); the platform computes
`score = rating × 10`.

> **Microservices (Phase 8):** the content described here is now owned and served by the extracted **[learning-service](LEARNING_SERVICE.md)** through the gateway (Postgres `learning` DB). Paths, schemas and behaviour are unchanged. AI-graded exercise types are scored by the learning-service calling the ai-service `POST /ai/evaluate` (the learning-service still owns the `ExerciseTypePrompt` text and passes it in).
