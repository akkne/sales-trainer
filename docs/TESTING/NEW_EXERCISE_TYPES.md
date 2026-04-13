# Testing: New Exercise Types

Manual test checklist for 8 new exercise types.

## Prerequisites

- [ ] Backend running with migrations applied (includes `ExerciseTypePrompts` table)
- [ ] OpenAI API key configured (for AI-powered types)
- [ ] Test exercises created via admin panel or seeder

---

## Type 1: Ordering (`ordering`)

### Setup
Create exercise with content:
```json
{
  "situation": "Расставьте этапы холодного звонка в правильном порядке.",
  "items": [
    {"id": "a", "text": "Приветствие"},
    {"id": "b", "text": "Выявление потребности"},
    {"id": "c", "text": "Презентация"},
    {"id": "d", "text": "Работа с возражениями"},
    {"id": "e", "text": "Закрытие"}
  ],
  "correctOrder": ["a", "b", "c", "d", "e"],
  "explanation": "Важно соблюдать последовательность этапов."
}
```

### Tests
- [ ] Items display shuffled on load
- [ ] Drag-and-drop reorders items
- [ ] Up/down buttons move items
- [ ] Submit with correct order → `isCorrect: true, score: 100`
- [ ] Submit with wrong order → `isCorrect: false, score: 0`
- [ ] Explanation shows after submit
- [ ] Continue button advances to next exercise
- [ ] Skip button works

---

## Type 2: Matching (`matching`)

### Setup
Create exercise with content:
```json
{
  "situation": "Соедините возражение с техникой ответа.",
  "leftColumn": [
    {"id": "l1", "text": "Слишком дорого"},
    {"id": "l2", "text": "Мне нужно подумать"}
  ],
  "rightColumn": [
    {"id": "r1", "text": "Техника сравнения"},
    {"id": "r2", "text": "Техника изоляции"}
  ],
  "correctPairs": [
    {"left": "l1", "right": "r1"},
    {"left": "l2", "right": "r2"}
  ],
  "explanation": "Каждая техника соответствует типу возражения."
}
```

### Tests
- [ ] Left column items clickable, highlight on select
- [ ] Right column items clickable after left selected
- [ ] Connection created between items
- [ ] Can remove connection by clicking X
- [ ] "Сбросить все связи" clears all
- [ ] Submit enabled only when all items connected
- [ ] All correct → `isCorrect: true, score: 100`
- [ ] Partial correct → `isCorrect: false, score: 50` (1 of 2)
- [ ] Color feedback shows correct/incorrect pairs

---

## Type 3: Categorizing (`categorizing`)

### Setup
Create exercise with content:
```json
{
  "situation": "Распределите вопросы: хорошие или плохие?",
  "items": [
    {"id": "i1", "text": "Какие у вас цели?"},
    {"id": "i2", "text": "Хотите скидку?"}
  ],
  "categories": [
    {"id": "good", "title": "Хороший", "color": "#58CC02"},
    {"id": "bad", "title": "Плохой", "color": "#FF4B4B"}
  ],
  "correctMapping": {"i1": "good", "i2": "bad"},
  "explanation": "Открытые вопросы лучше закрытых."
}
```

### Tests
- [ ] Items show in center pool
- [ ] Drag item to category bucket
- [ ] Click item cycles through categories
- [ ] Can remove item from category back to pool
- [ ] Submit enabled when all items placed
- [ ] All correct → score: 100
- [ ] Partial → proportional score
- [ ] Visual feedback shows correct/incorrect placement

---

## Type 4: Find Error (`find_error`)

### Setup
Create exercise with content:
```json
{
  "situation": "Найдите ошибку в диалоге.",
  "dialogLines": [
    {"id": "line1", "speaker": "Продавец", "text": "Здравствуйте!"},
    {"id": "line2", "speaker": "Продавец", "text": "Наш продукт лучший!"},
    {"id": "line3", "speaker": "Клиент", "text": "Спасибо, не интересует."}
  ],
  "errorLineId": "line2",
  "requireExplanation": true,
  "aiPrompt": "Оцени, понял ли пользователь что питч без выявления потребности — ошибка."
}
```

### Tests
- [ ] Dialog lines displayed with speaker labels
- [ ] Lines clickable, selection highlights
- [ ] If `requireExplanation`: textarea appears after selection
- [ ] If `suggestedFixes`: fix options appear
- [ ] Correct line selected → 50 points base
- [ ] Good explanation → up to +25 points (AI)
- [ ] Correct fix → +25 points
- [ ] Wrong line → score: 0
- [ ] AI feedback shown

---

## Type 5: Rewrite Better (`rewrite_better`)

### Setup
Create exercise with content:
```json
{
  "situation": "Перепишите эту тему письма лучше.",
  "originalText": "Предложение о сотрудничестве",
  "context": "B2B SaaS, целевая аудитория: IT-директора",
  "aiPrompt": "Оцени: персонализация, интрига, краткость.",
  "minLength": 10,
  "maxLength": 80
}
```

### Tests
- [ ] Original text displayed in gray box
- [ ] Context shows if present
- [ ] Textarea with character counter
- [ ] Submit disabled if < minLength
- [ ] Error shown if > maxLength
- [ ] AI rating badge (0-10) shown after submit
- [ ] AI feedback text shown
- [ ] `isCorrect` based on rating >= 8

---

## Type 6: AI Dialog (`ai_dialog`)

### Setup
Create exercise with content:
```json
{
  "situation": "Проведите discovery-звонок.",
  "persona": {
    "name": "Скептик Сергей",
    "role": "IT-директор",
    "description": "Скептически настроен, отвечает коротко"
  },
  "chatSystemPrompt": "Ты Скептик Сергей. Отвечай коротко и скептически.",
  "aiPrompt": "Оцени качество вопросов и работу со скептицизмом.",
  "maxTurns": 8,
  "minTurnsForCompletion": 3
}
```

### Tests
- [ ] Persona header shows name and role
- [ ] AI sends first message on load
- [ ] User can type and send messages
- [ ] AI responds to each message
- [ ] "Завершить диалог" button appears after minTurns
- [ ] Auto-complete if AI sends `[DIALOG_END]`
- [ ] Final rating and feedback shown after completion
- [ ] Messages scroll to bottom
- [ ] Typing indicator while AI responds

---

## Type 7: Rate Call (`rate_call`)

### Setup
Create exercise with content:
```json
{
  "situation": "Оцените этот звонок.",
  "transcript": [
    {"speaker": "Продавец", "text": "Здравствуйте, чем могу помочь?"},
    {"speaker": "Клиент", "text": "Ищу CRM."},
    {"speaker": "Продавец", "text": "Отлично, давайте запланируем демо!"}
  ],
  "criteria": [
    {"id": "c1", "name": "Квалификация", "description": "Была ли квалификация?"},
    {"id": "c2", "name": "Открытые вопросы", "description": "Использовались ли?"}
  ],
  "ratingScale": {"min": 1, "max": 5},
  "aiPrompt": "Сравни оценки пользователя с объективным анализом."
}
```

### Tests
- [ ] Transcript collapsible/expandable
- [ ] All criteria displayed with rating buttons
- [ ] Rating buttons 1-5 clickable
- [ ] Overall comment textarea optional
- [ ] Submit enabled only when all criteria rated
- [ ] AI shows its own ratings + comparison
- [ ] Feedback explains where user was right/wrong

---

## Type 8: Written Answer (`written_answer`)

### Setup
Create exercise with content:
```json
{
  "prompt": "Напишите ответ на возражение 'Слишком дорого'.",
  "context": "B2B SaaS, $500/месяц",
  "aiPrompt": "Оцени: не снижает цену сразу, выясняет причину, предлагает альтернативы.",
  "minLength": 30,
  "maxLength": 300
}
```

### Tests
- [ ] Prompt displayed prominently
- [ ] Context shows if present
- [ ] Textarea with character counter
- [ ] Submit disabled if < minLength
- [ ] Warning if > maxLength
- [ ] AI rating (0-10) shown
- [ ] AI feedback with improvements
- [ ] `isCorrect` based on rating >= 8

---

## Integration Tests

- [ ] Mixed lesson (old + new types) completes correctly
- [ ] Hearts deducted on wrong answer for new types
- [ ] XP awarded on correct answer
- [ ] Achievement triggers work with new types
- [ ] Session stats (accuracy, time) calculated correctly
- [ ] Skip button works for all types
- [ ] Mobile responsive for all components

---

## Admin Panel Tests

- [ ] Can create exercise of each new type
- [ ] JSON content validated on save
- [ ] Exercise preview works
- [ ] Edit existing exercise works
- [ ] Delete exercise works

---

## API Tests

### POST /exercises/{id}/submit

Test each type with valid and invalid answers:

```bash
# Ordering - correct
curl -X POST /exercises/{id}/submit \
  -d '{"answer": {"order": ["a", "b", "c"]}}'

# Matching - partial
curl -X POST /exercises/{id}/submit \
  -d '{"answer": {"pairs": [{"left": "l1", "right": "r2"}]}}'

# AI types - text answer
curl -X POST /exercises/{id}/submit \
  -d '{"answer": {"text": "User response..."}}'
```

### POST /exercises/{id}/chat (ai_dialog only)

```bash
curl -X POST /exercises/{id}/chat \
  -d '{"message": "Здравствуйте, меня зовут Алексей."}'
```

Expected response:
```json
{
  "response": "Да, слушаю. Что вы хотели?",
  "isComplete": false,
  "turnNumber": 1,
  "maxTurns": 10
}
```
