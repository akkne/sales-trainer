# Exercise Types — Reference

> This document describes all 10 exercise types in the SalesTrainer platform.
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

## Scoring

All types return `score` on a **0–100** scale. `isCorrect` rules differ:

| Type | Scoring | `isCorrect` |
|------|---------|-------------|
| `choose_option`, `fill_blank`, `reorder` | Binary: 100 or 0 | score == 100 |
| `match_pairs`, `categorize` | Partial: `(correct / total) × 100` | all items correct |
| `spot_mistake` | 50 pts for the right line + 0–50 from AI on the explanation | score ≥ 75 |
| `rewrite`, `free_text`, `evaluate_call` | AI: `score = rating × 10` | `passed \|\| rating ≥ 8` |
| `ai_dialogue` | AI, but score 30 / fail if user turns < `max_turns / 2` | `passed \|\| rating ≥ 8` |

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
