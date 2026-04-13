# New Exercise Types — Architecture Spec

> This document specifies 8 new exercise types for the SalesTrainer platform.

## Overview

| # | Type Key | Name (RU) | AI Required | Description |
|---|----------|-----------|-------------|-------------|
| 1 | `ordering` | Расставь по порядку | No | Drag-drop items into correct sequence |
| 2 | `matching` | Соедини пары | No | Connect items from two columns |
| 3 | `categorizing` | Разложи по категориям | No | Sort items into 2-3 buckets |
| 4 | `find_error` | Найди ошибку | Yes | Find mistake in dialog/text, optionally explain |
| 5 | `rewrite_better` | Перепиши лучше | Yes | Improve weak text, AI evaluates improvement |
| 6 | `ai_dialog` | Текстовый диалог | Yes | Multi-turn text chat with AI customer |
| 7 | `rate_call` | Оцени звонок | Yes | Evaluate call transcript, compare with AI |
| 8 | `written_answer` | Написать ответ | Yes | Free-form answer to prompt, AI evaluates |

---

## Common AI Configuration

For AI-powered types (4-8), prompts are configured at two levels:

### Global Type Prompts

Stored in new PostgreSQL table `ExerciseTypePrompts`:

| Column | Type | Notes |
|--------|------|-------|
| `Id` | uuid | PK |
| `ExerciseType` | text | Type key (e.g., `find_error`) |
| `SystemPrompt` | text | Base prompt for all exercises of this type |
| `UpdatedAt` | timestamp | Last modification |

**Seeded defaults provided for each AI type.**

### Per-Exercise Prompts

Stored in `Exercise.SerializedContent` alongside exercise data:

```json
{
  "aiPrompt": "Additional evaluation criteria specific to this exercise...",
  // ... other type-specific fields
}
```

### Combined Prompt Construction

```
[Global Type System Prompt]

[Per-Exercise aiPrompt, if present]

Формат ответа: {JSON schema specific to type}
```

### AI Response Format

All AI-powered types return:

```json
{
  "passed": true|false,
  "rating": 0-10,
  "feedback": "Detailed feedback text..."
}
```

- `passed` = main success indicator
- `rating` = numeric score for progress tracking
- `feedback` = explanation to show user

---

## Type 1: Ordering (`ordering`)

### Purpose
User arranges 4-6 shuffled items into correct sequence. Tests process understanding (e.g., SPIN question order, cold call stages).

### Content Schema

```json
{
  "situation": "Клиент только что отказал вам по телефону. Расставьте этапы работы с возражением в правильном порядке.",
  "items": [
    {"id": "a", "text": "Выслушать возражение полностью"},
    {"id": "b", "text": "Согласиться с чувствами клиента"},
    {"id": "c", "text": "Задать уточняющий вопрос"},
    {"id": "d", "text": "Предложить решение"},
    {"id": "e", "text": "Подтвердить согласие клиента"}
  ],
  "correctOrder": ["a", "b", "c", "d", "e"],
  "explanation": "Важно сначала выслушать, потом показать эмпатию, затем уточнить причину, и только потом предлагать решение."
}
```

### Answer Schema

```json
{
  "order": ["a", "c", "b", "d", "e"]
}
```

### Evaluation Logic (Backend)

```csharp
public class OrderingEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "ordering";
    
    // IsCorrect: userOrder exactly matches correctOrder
    // Score: 100 if correct, 0 if wrong (no partial credit)
    // Explanation: from content
}
```

### Frontend Component

- Drag-and-drop list (react-beautiful-dnd or native HTML5 DnD)
- Items start shuffled (shuffle on mount, seeded by exerciseId for consistency)
- Drop zones with visual reordering
- Mobile: tap-to-select + tap-to-place alternative
- Submit shows correct order with green/red highlighting per item

---

## Type 2: Matching (`matching`)

### Purpose
Connect items from left column to right column. Tests associations (objection→technique, signal→meaning, persona→approach).

### Content Schema

```json
{
  "situation": "Соедините возражение клиента с лучшей техникой ответа.",
  "leftColumn": [
    {"id": "l1", "text": "Слишком дорого"},
    {"id": "l2", "text": "Нам ничего не нужно"},
    {"id": "l3", "text": "Отправьте на почту"},
    {"id": "l4", "text": "Я подумаю"}
  ],
  "rightColumn": [
    {"id": "r1", "text": "Техника 'Бумеранг'"},
    {"id": "r2", "text": "Техника 'Сравнение'"},
    {"id": "r3", "text": "Техника 'Мост'"},
    {"id": "r4", "text": "Техника 'Изоляция'"}
  ],
  "correctPairs": [
    {"left": "l1", "right": "r2"},
    {"left": "l2", "right": "r1"},
    {"left": "l3", "right": "r3"},
    {"left": "l4", "right": "r4"}
  ],
  "explanation": "Техника 'Сравнение' помогает при возражении 'дорого', показывая ценность относительно стоимости."
}
```

### Answer Schema

```json
{
  "pairs": [
    {"left": "l1", "right": "r2"},
    {"left": "l2", "right": "r3"},
    {"left": "l3", "right": "r1"},
    {"left": "l4", "right": "r4"}
  ]
}
```

### Evaluation Logic

```csharp
public class MatchingEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "matching";
    
    // IsCorrect: all pairs match exactly
    // Score: (correctPairs / totalPairs) * 100
    // Partial credit allowed
}
```

### Frontend Component

- Two columns side by side
- Click left item → click right item to connect (or drag line)
- Lines drawn between connected pairs (SVG)
- Color-coded feedback on submit
- "Сбросить" button to clear all connections

---

## Type 3: Categorizing (`categorizing`)

### Purpose
Sort 6-8 items into 2-3 buckets/categories. Tests judgment (qualify vs disqualify lead, opener/body/CTA, good vs bad question).

### Content Schema

```json
{
  "situation": "Распределите вопросы по категориям: хороший discovery-вопрос или плохой.",
  "items": [
    {"id": "i1", "text": "Сколько у вас сотрудников?"},
    {"id": "i2", "text": "Вам нравится наш продукт?"},
    {"id": "i3", "text": "Какие цели вы ставите на этот квартал?"},
    {"id": "i4", "text": "Почему вы ещё не купили?"},
    {"id": "i5", "text": "Что мешает достичь этих целей?"},
    {"id": "i6", "text": "Хотите скидку?"}
  ],
  "categories": [
    {"id": "good", "title": "Хороший вопрос", "color": "#58CC02"},
    {"id": "bad", "title": "Плохой вопрос", "color": "#FF4B4B"}
  ],
  "correctMapping": {
    "i1": "good",
    "i2": "bad",
    "i3": "good",
    "i4": "bad",
    "i5": "good",
    "i6": "bad"
  },
  "explanation": "Хорошие discovery-вопросы открытые и направлены на понимание ситуации клиента, а не на продажу."
}
```

### Answer Schema

```json
{
  "mapping": {
    "i1": "good",
    "i2": "bad",
    "i3": "good",
    "i4": "bad",
    "i5": "bad",
    "i6": "good"
  }
}
```

### Evaluation Logic

```csharp
public class CategorizingEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "categorizing";
    
    // IsCorrect: all items in correct categories
    // Score: (correctItems / totalItems) * 100
    // Partial credit allowed
}
```

### Frontend Component

- Items in center pool (draggable cards)
- Category buckets as drop zones (colored borders)
- Items move into bucket on drop
- Item can be moved between buckets or back to pool
- Submit shows correct placement with checkmarks/X marks

---

## Type 4: Find Error (`find_error`)

### Purpose
Read dialog/email, click on line containing error. Optionally explain why or select fix. Builds analytical skill.

### Content Schema

```json
{
  "situation": "Найдите ошибку в этом холодном звонке.",
  "dialogLines": [
    {"id": "line1", "speaker": "Продавец", "text": "Добрый день! Меня зовут Алексей, компания Рост."},
    {"id": "line2", "speaker": "Клиент", "text": "Добрый день."},
    {"id": "line3", "speaker": "Продавец", "text": "Мы предлагаем лучшее CRM-решение на рынке!"},
    {"id": "line4", "speaker": "Клиент", "text": "Нам ничего не нужно."},
    {"id": "line5", "speaker": "Продавец", "text": "Понял, до свидания."}
  ],
  "errorLineId": "line3",
  "aiPrompt": "Оцени объяснение пользователя почему эта строка — ошибка. Критерии: 1) Понял ли что проблема в питче вместо вопроса 2) Упомянул ли важность discovery",
  "requireExplanation": true,
  "suggestedFixes": [
    {"id": "fix1", "text": "Скажите, вы сейчас используете какую-то CRM?"},
    {"id": "fix2", "text": "Наш продукт дешевле конкурентов!"},
    {"id": "fix3", "text": "Можно узнать, как сейчас ведёте клиентскую базу?"}
  ],
  "correctFixIds": ["fix1", "fix3"]
}
```

### Answer Schema

```json
{
  "selectedLineId": "line3",
  "explanation": "Продавец сразу начал питчить вместо того чтобы задать вопрос и узнать потребность.",
  "selectedFixId": "fix1"
}
```

### Evaluation Logic

```csharp
public class FindErrorEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "find_error";
    
    // Step 1: Check if correct line selected (50 points max)
    // Step 2: If requireExplanation, call AI to evaluate explanation (25 points max)
    // Step 3: If suggestedFixes present, check fix selection (25 points max)
    // IsCorrect: line correct AND (AI passed OR no explanation required) AND (fix correct OR no fixes)
}
```

### AI Request (for explanation)

```
[Global find_error system prompt]

[Per-exercise aiPrompt]

Контекст ошибки: [error line text]
Объяснение пользователя: [user explanation]

Формат ответа: {"passed": true/false, "rating": 0-10, "feedback": "..."}
```

### Frontend Component

- Dialog displayed as chat bubbles (speaker labels)
- Lines are clickable (highlight on hover)
- Selected line gets red border
- If `requireExplanation`: textarea appears after line selection
- If `suggestedFixes`: show fix options as buttons after explanation
- Multi-step flow: select line → explain → choose fix → submit

---

## Type 5: Rewrite Better (`rewrite_better`)

### Purpose
See weak version of text, rewrite it better. AI evaluates improvement. Tests practical application.

### Content Schema

```json
{
  "situation": "Перепишите эту тему холодного письма, чтобы она была более цепляющей.",
  "originalText": "Предложение о сотрудничестве",
  "context": "Вы пишете письмо IT-директору средней компании, предлагая облачную CRM.",
  "aiPrompt": "Оцени улучшенную тему письма. Критерии: 1) Персонализация 2) Интрига без кликбейта 3) Краткость (до 50 символов) 4) Релевантность контексту",
  "minLength": 10,
  "maxLength": 100
}
```

### Answer Schema

```json
{
  "rewrittenText": "Сергей, 3 способа сократить цикл продаж в IT"
}
```

### Evaluation Logic

```csharp
public class RewriteBetterEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "rewrite_better";
    
    // Validate length constraints
    // Call AI with original + rewrite + context
    // IsCorrect: AI.passed
    // Score: AI.rating * 10
}
```

### AI Request

```
[Global rewrite_better system prompt]

[Per-exercise aiPrompt]

Оригинал: [originalText]
Контекст: [context]
Переписанный вариант: [user rewrittenText]

Формат ответа: {"passed": true/false, "rating": 0-10, "feedback": "..."}
```

### Frontend Component

- Show original text in gray box with "Оригинал:" label
- Context displayed above
- Textarea for rewrite with character counter
- Submit button disabled until min length met
- Result shows AI rating badge + feedback

---

## Type 6: AI Dialog (`ai_dialog`)

### Purpose
Multi-turn text chat where AI plays customer. Intermediate between single response and voice roleplay. Uses existing persona infrastructure.

### Content Schema

```json
{
  "situation": "Вы звоните потенциальному клиенту для discovery-звонка.",
  "persona": {
    "name": "Скептик Сергей",
    "role": "IT-директор",
    "description": "Скептически настроен, отвечает коротко, торопится"
  },
  "chatSystemPrompt": "Ты Скептик Сергей, IT-директор. Отвечай коротко и скептически. Торопись. Через 3-4 реплики начни смягчаться если продавец показывает понимание.",
  "aiPrompt": "Оцени диалог продавца. Критерии: 1) Качество вопросов 2) Работа со скептицизмом 3) Достижение следующего шага",
  "maxTurns": 10,
  "minTurnsForCompletion": 4
}
```

### Answer Schema

The answer is the full message history, stored progressively:

```json
{
  "messages": [
    {"role": "assistant", "content": "Да, слушаю, только быстро."},
    {"role": "user", "content": "Добрый день! Скажите, какую CRM вы сейчас используете?"},
    {"role": "assistant", "content": "Никакую. Всё в Excel. Зачем звоните?"},
    {"role": "user", "content": "Понял. А сколько примерно клиентов ведёте в Excel?"}
  ],
  "completedNaturally": true
}
```

### Evaluation Logic

```csharp
public class AiDialogEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "ai_dialog";
    
    // Called when user completes dialog (clicks "Завершить" or AI signals end)
    // Send full conversation to AI for evaluation
    // IsCorrect: AI.passed
    // Score: AI.rating * 10
}
```

### AI Request (for chat turns)

Uses `chatSystemPrompt` as system message. Appends `[DIALOG_END]` instruction like existing dialog feature.

### AI Request (for final evaluation)

```
[Global ai_dialog system prompt]

[Per-exercise aiPrompt]

Полный диалог:
[formatted message history]

Формат ответа: {"passed": true/false, "rating": 0-10, "feedback": "..."}
```

### Frontend Component

- Chat interface similar to `/dialog` but inline in session
- Messages appear one by one
- AI response shows typing indicator
- "Завершить диалог" button (enabled after minTurns)
- Auto-end if AI sends `[DIALOG_END]` signal
- Final screen shows rating + feedback
- Message history preserved if user returns

---

## Type 7: Rate Call (`rate_call`)

### Purpose
Read call transcript, rate by criteria, compare with AI analysis. Teaches analytical skills, prepares for call analysis feature.

### Content Schema

```json
{
  "situation": "Прочитайте транскрипт звонка и оцените его по критериям.",
  "transcript": [
    {"speaker": "Продавец", "text": "Здравствуйте, это Алексей из компании Рост."},
    {"speaker": "Клиент", "text": "Добрый день."},
    {"speaker": "Продавец", "text": "Скажите, вы рассматриваете новые решения для управления продажами?"},
    {"speaker": "Клиент", "text": "Ну, в целом да, но пока не приоритет."},
    {"speaker": "Продавец", "text": "Понял. А что сейчас является приоритетом?"},
    {"speaker": "Клиент", "text": "Автоматизация отчётности."},
    {"speaker": "Продавец", "text": "Интересно! Давайте я покажу, как наш продукт это решает. Когда удобно созвониться?"},
    {"speaker": "Клиент", "text": "В четверг после обеда."},
    {"speaker": "Продавец", "text": "Отлично, я отправлю приглашение. Спасибо!"}
  ],
  "criteria": [
    {"id": "c1", "name": "Квалификация", "description": "Была ли проведена квалификация клиента?"},
    {"id": "c2", "name": "Открытые вопросы", "description": "Использовались ли открытые вопросы?"},
    {"id": "c3", "name": "Следующий шаг", "description": "Был ли согласован чёткий следующий шаг?"},
    {"id": "c4", "name": "Активное слушание", "description": "Демонстрировал ли продавец понимание?"}
  ],
  "ratingScale": {"min": 1, "max": 5},
  "aiPrompt": "Оцени анализ пользователя. Сравни его оценки с объективным анализом звонка. Укажи что пользователь оценил верно и где ошибся."
}
```

### Answer Schema

```json
{
  "ratings": {
    "c1": 3,
    "c2": 4,
    "c3": 5,
    "c4": 3
  },
  "overallComment": "Хороший звонок, но квалификация поверхностная — не выяснил бюджет и сроки."
}
```

### Evaluation Logic

```csharp
public class RateCallEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "rate_call";
    
    // Send transcript + user ratings + user comment to AI
    // AI provides its own analysis and compares
    // IsCorrect: AI.passed (based on quality of user analysis)
    // Score: AI.rating * 10
    // Feedback: AI's analysis + comparison
}
```

### AI Request

```
[Global rate_call system prompt]

[Per-exercise aiPrompt]

Транскрипт:
[formatted transcript]

Критерии оценки: [list criteria]

Оценки пользователя:
[formatted user ratings]

Комментарий пользователя: [overallComment]

Формат ответа: {
  "passed": true/false,
  "rating": 0-10,
  "aiRatings": {"c1": 4, "c2": 5, "c3": 5, "c4": 2},
  "feedback": "Анализ AI: ... Сравнение с вашим: ..."
}
```

### Frontend Component

- Transcript displayed as scrollable chat view
- Criteria cards below with 1-5 star/number selectors
- Overall comment textarea
- Submit → show AI comparison:
  - Side-by-side ratings (User | AI)
  - Feedback text highlighting matches/differences

---

## Type 8: Written Answer (`written_answer`)

### Purpose
Simple prompt → free text answer → AI evaluation. The foundational AI-evaluated text type.

### Content Schema

```json
{
  "prompt": "Напишите ответ на возражение клиента: 'Это слишком дорого для нас'",
  "context": "B2B SaaS продукт, стоимость $500/месяц, клиент — маленький стартап",
  "aiPrompt": "Оцени ответ на возражение 'дорого'. Критерии: 1) Не оправдывается и не снижает цену сразу 2) Выясняет причину возражения 3) Предлагает альтернативы (рассрочка, меньший пакет) 4) Профессиональный тон",
  "minLength": 50,
  "maxLength": 500
}
```

### Answer Schema

```json
{
  "text": "Понимаю вашу позицию. Скажите, когда вы говорите 'дорого' — это относительно бюджета или относительно ценности, которую вы пока видите? Если дело в бюджете, у нас есть стартовый пакет за $200 и помесячная оплата."
}
```

### Evaluation Logic

```csharp
public class WrittenAnswerEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "written_answer";
    
    // Validate length
    // Call AI with prompt + context + answer
    // IsCorrect: AI.passed
    // Score: AI.rating * 10
}
```

### AI Request

```
[Global written_answer system prompt]

[Per-exercise aiPrompt]

Задание: [prompt]
Контекст: [context]
Ответ пользователя: [user text]

Формат ответа: {"passed": true/false, "rating": 0-10, "feedback": "..."}
```

### Frontend Component

- Prompt displayed prominently
- Context in gray info box (if present)
- Textarea with character counter
- Rating badge + feedback on submit
- Similar to existing `open_question` but with new styling

---

## Database Changes

### New Table: `ExerciseTypePrompts`

```sql
CREATE TABLE "ExerciseTypePrompts" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "ExerciseType" text NOT NULL UNIQUE,
    "SystemPrompt" text NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);

-- Seed default prompts
INSERT INTO "ExerciseTypePrompts" ("Id", "ExerciseType", "SystemPrompt", "UpdatedAt") VALUES
(gen_random_uuid(), 'find_error', 'Ты эксперт по продажам. Оцениваешь, правильно ли пользователь определил ошибку в диалоге и понял её причину.', now()),
(gen_random_uuid(), 'rewrite_better', 'Ты эксперт по копирайтингу в продажах. Оцениваешь улучшение текста по критериям качества продающего текста.', now()),
(gen_random_uuid(), 'ai_dialog', 'Ты эксперт по переговорам и продажам. Оцениваешь качество ведения диалога продавцом.', now()),
(gen_random_uuid(), 'rate_call', 'Ты эксперт по анализу звонков продаж. Проводишь объективный анализ и сравниваешь с оценкой пользователя.', now()),
(gen_random_uuid(), 'written_answer', 'Ты эксперт по продажам. Оцениваешь качество письменных ответов по критериям профессионализма и эффективности.', now());
```

### Migration

Create migration `AddNewExerciseTypes`:
1. Add `ExerciseTypePrompts` table
2. Seed default prompts
3. No changes to `Exercises` table (SerializedContent already flexible)

---

## API Changes

### Existing Endpoints (no change needed)

- `POST /exercises/{exerciseId}/submit` — already accepts generic `JsonElement` answer
- `GET /lessons/{lessonId}/exercises` — already returns `ExerciseDto` with `type` and `content`
- Admin CRUD — already flexible

### New Endpoints

#### AI Dialog Chat (within exercise)

```
POST /exercises/{exerciseId}/chat
Body: { "message": "string" }
Response: { 
  "response": "string", 
  "isComplete": boolean,
  "turnNumber": number,
  "maxTurns": number
}
```

This endpoint handles the real-time chat within an `ai_dialog` exercise. State stored server-side (Redis or in-memory).

### Response Changes

`ExerciseSubmissionResultDto` already has:
- `isCorrect: boolean`
- `score: number`
- `explanation: string | null`
- `aiFeedback: string | null`

No changes needed — new types use same structure.

---

## Admin Panel Changes

### Exercise Editor Forms

Add type-specific editors for each new type:

1. **Ordering**: Sortable item list + correct order setter
2. **Matching**: Two-column item editor + pair connector
3. **Categorizing**: Item list + category definitions + mapping editor
4. **Find Error**: Dialog line editor + error line selector + optional fix options
5. **Rewrite Better**: Original text + context + length limits
6. **AI Dialog**: Persona editor + system prompt + evaluation prompt
7. **Rate Call**: Transcript editor + criteria builder
8. **Written Answer**: Prompt + context + length limits

### Type Prompt Management

New admin page: `/admin/exercise-prompts`
- List all exercise types with global prompts
- Edit system prompt per type
- Preview combined prompt

---

## Frontend Implementation

### Component Files

```
src/frontend/components/exercise/
├── OrderingExercise.tsx
├── MatchingExercise.tsx
├── CategorizingExercise.tsx
├── FindErrorExercise.tsx
├── RewriteBetterExercise.tsx
├── AiDialogExercise.tsx
├── RateCallExercise.tsx
└── WrittenAnswerExercise.tsx
```

### Type Definitions

```typescript
// lib/hooks/useLesson.ts
export type ExerciseType = 
  | 'multiple_choice'
  | 'fill_blank'
  | 'open_question'
  | 'ordering'
  | 'matching'
  | 'categorizing'
  | 'find_error'
  | 'rewrite_better'
  | 'ai_dialog'
  | 'rate_call'
  | 'written_answer';
```

### Exercise Page Dispatcher

```typescript
// app/session/[lessonId]/page.tsx
function renderExercise(exercise: ExerciseData) {
  switch (exercise.type) {
    case 'multiple_choice': return <MultipleChoiceExercise {...} />;
    case 'fill_blank': return <FillBlankExercise {...} />;
    case 'open_question': return <OpenQuestionExercise {...} />;
    case 'ordering': return <OrderingExercise {...} />;
    case 'matching': return <MatchingExercise {...} />;
    case 'categorizing': return <CategorizingExercise {...} />;
    case 'find_error': return <FindErrorExercise {...} />;
    case 'rewrite_better': return <RewriteBetterExercise {...} />;
    case 'ai_dialog': return <AiDialogExercise {...} />;
    case 'rate_call': return <RateCallExercise {...} />;
    case 'written_answer': return <WrittenAnswerExercise {...} />;
    default: return <UnsupportedExercise type={exercise.type} />;
  }
}
```

---

## Implementation Order

1. **Database**: Migration + seed prompts
2. **Backend Strategies**: Implement 8 new `IExerciseEvaluationStrategy` classes
3. **Backend DI**: Register strategies in `ExerciseServiceCollectionExtensions`
4. **Backend Chat Endpoint**: Add `/exercises/{id}/chat` for `ai_dialog`
5. **Frontend Components**: Build 8 new exercise components
6. **Frontend Integration**: Update session page dispatcher
7. **Admin Editors**: Add type-specific content editors
8. **Admin Prompts Page**: Global prompt management
9. **Testing**: Manual + automated tests per type
10. **Docs**: Update API_CONTRACTS.md, TESTING/

---

## Testing Checklist

### Per Type

- [ ] Content schema validates correctly
- [ ] Evaluation returns expected IsCorrect/Score
- [ ] AI types handle API unavailable gracefully
- [ ] Frontend renders content properly
- [ ] User can interact and submit answer
- [ ] Result banner shows correct/incorrect + feedback
- [ ] XP awarded on correct answer
- [ ] Progress saved and displayed

### Integration

- [ ] Mixed lesson (old + new types) works
- [ ] Admin can create/edit exercises of each type
- [ ] Seeder imports new types from CSV
- [ ] Mobile UI works for drag-drop types
