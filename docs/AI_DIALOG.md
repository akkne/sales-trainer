# AI Dialog Feature — Technical Specification

## Overview

New tab "Диалог" (left of Profile) where users practice sales skills via AI-powered chat conversations.

## User Flow

```
/dialog (tab)
    └── Skill bundles grid (e.g., "Холодные звонки")
            └── Mode selection (e.g., "Обход секретаря")
                    └── Chat screen with GPT-4.1-mini + history sidebar
                            └── Conversation ends → GPT-4.1 feedback popup + XP reward
```

## Data Model

### PostgreSQL Tables

**DialogBundles** (linked to Skills)
```sql
CREATE TABLE "DialogBundles" (
    "Id" uuid PRIMARY KEY,
    "SkillId" uuid NOT NULL REFERENCES "Skills"("Id") ON DELETE CASCADE,
    "Title" varchar(200) NOT NULL,
    "Description" varchar(1000) NOT NULL,
    "IconEmoji" varchar(10) NOT NULL,
    "SortOrder" int NOT NULL,
    "IsActive" bool NOT NULL DEFAULT true,
    "CreatedAt" timestamp NOT NULL,
    "UpdatedAt" timestamp NOT NULL
);
```

**DialogModes** (exercises inside a bundle)
```sql
CREATE TABLE "DialogModes" (
    "Id" uuid PRIMARY KEY,
    "BundleId" uuid NOT NULL REFERENCES "DialogBundles"("Id") ON DELETE CASCADE,
    "Key" varchar(100) NOT NULL,
    "Title" varchar(200) NOT NULL,
    "Description" varchar(1000) NOT NULL,
    "ChatSystemPrompt" text NOT NULL,       -- AI role for conversation
    "FeedbackSystemPrompt" text NOT NULL,   -- AI evaluation instructions
    "SortOrder" int NOT NULL,
    "IsActive" bool NOT NULL DEFAULT true,
    "CreatedAt" timestamp NOT NULL,
    "UpdatedAt" timestamp NOT NULL,
    UNIQUE ("BundleId", "Key")
);
```

### MongoDB Collection

**dialog_sessions** (user conversations — kept in MongoDB for flexible message array)
```json
{
  "_id": ObjectId,
  "userId": "guid",
  "bundleId": "guid",
  "modeId": "guid",
  "status": "active" | "completed" | "abandoned",
  "messages": [
    {
      "role": "assistant" | "user",
      "content": "string",
      "timestamp": ISODate,
      "isStopSignal": boolean
    }
  ],
  "feedback": {
    "content": "string",
    "generatedAt": ISODate
  },
  "xpEarned": number,
  "createdAt": ISODate,
  "completedAt": ISODate
}
```

## API Endpoints

### Bundles & Modes (public)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/dialog/bundles` | List active bundles with skill info |
| GET | `/dialog/bundles/{bundleId}/modes` | List active modes for bundle |
| GET | `/dialog/sessions` | List user's session history |

### Chat Session

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/dialog/sessions` | Start new session (body: `{bundleId, modeId}`) |
| GET | `/dialog/sessions/{sessionId}` | Get session with messages |
| POST | `/dialog/sessions/{sessionId}/messages` | Send user message, get AI response |
| POST | `/dialog/sessions/{sessionId}/complete` | End session, generate feedback, award XP |

### Admin CRUD

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/admin/dialog/bundles` | List all bundles (incl. inactive) |
| POST | `/admin/dialog/bundles` | Create bundle (requires `skillId`) |
| PUT | `/admin/dialog/bundles/{id}` | Update bundle |
| DELETE | `/admin/dialog/bundles/{id}` | Delete bundle (cascades to modes) |
| GET | `/admin/dialog/bundles/{bundleId}/modes` | List all modes with prompts |
| POST | `/admin/dialog/bundles/{bundleId}/modes` | Create mode |
| PUT | `/admin/dialog/modes/{id}` | Update mode (incl. system prompts) |
| DELETE | `/admin/dialog/modes/{id}` | Delete mode |

## AI Integration

### Configuration (appsettings.json)

```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "BaseUrl": "https://api.openai.com",
    "ChatCompletionsPath": "/v1/chat/completions",
    "ChatModel": "gpt-4.1-mini",
    "FeedbackModel": "gpt-4.1",
    "MaxTokensChat": 500,
    "MaxTokensFeedback": 1500
  }
}
```

### Buying API access from Russia (RUB-friendly proxy gateways)

`OpenAI:BaseUrl` can be pointed at an OpenAI-compatible reseller that accepts
СБП / Russian cards instead of `api.openai.com`. The wire format is identical, so
no code changes are needed — only the config.

Tested / supported gateways:

| Gateway | `BaseUrl` | `ChatCompletionsPath` | Notes |
|---------|-----------|-----------------------|-------|
| **ProxyAPI** | `https://api.proxyapi.ru/openai` | `/v1/chat/completions` | СБП, карты МИР, оплата криптой |
| **VseGPT** | `https://api.vsegpt.ru` | `/v1/chat/completions` | Поддерживает GPT-4.1 + Claude + LLaMA из одной точки |
| **BotHub** | `https://bothub.chat/api/v2/openai` | `/v1/chat/completions` | Подписочная модель + оплата за токены |
| **GPTunnel** | `https://gptunnel.ru/v1` | `/chat/completions` | Самый дешёвый по гпт-4.1-mini на текущий момент |
| `f5ai.*` | (auth via `X-Auth-Token` — обработано в коде) | — | Legacy, оставлен для совместимости |
| Original OpenAI | `https://api.openai.com` | `/v1/chat/completions` | Только иностранные карты |

После смены `BaseUrl` ключ покупается в личном кабинете шлюза, вставляется в
`OpenAI:ApiKey` — рестарт backend, готово.

### System Prompts (stored in PostgreSQL, editable via admin)

**ChatSystemPrompt** — AI role for the conversation. Backend appends:
```
ВАЖНО: Когда разговор подошёл к логическому завершению, добавь в КОНЕЦ тег:
[DIALOG_END]
```

**FeedbackSystemPrompt** — AI evaluation instructions. Backend appends:
```
В КОНЦЕ ответа укажи количество XP в формате: [XP:число]
Критерии (0-100): уверенность (30), работа с возражениями (30), результат (40).
```

### Stop Detection

AI explicitly signals stop via `[DIALOG_END]` tag in response.
- Tag is parsed and removed from displayed content
- `isStopSignal: true` set on message
- "Завершить диалог" button shown to user

### XP Rewards

AI generates XP (0-100) based on:
- Confidence and professionalism: up to 30 XP
- Objection handling: up to 30 XP
- Achieving goal (passed secretary, scheduled meeting): up to 40 XP

XP is saved to `UserXpRecords` with source `"dialog"`.

### Graceful Degradation

If `OpenAI:ApiKey` is not configured:
- `/dialog/bundles` returns empty array
- `/dialog/sessions/*` returns 503 Service Unavailable
- Admin CRUD still works (catalog management)

## Frontend Features

### Session History Sidebar

Left sidebar in chat screen showing:
- "Новый диалог" button at top
- Sessions grouped by date (Сегодня, Вчера, X дн. назад)
- Each session shows: mode title, bundle title, message count, XP earned
- Click session → load its messages
- Click "К выбору навыка" → return to `/dialog`

### Chat Screen

- Toggle sidebar button (☰)
- Close button (✕) → return to `/dialog`
- Header: mode title + bundle name
- Messages: green (user, right), gray (AI, left)
- Typing indicator while waiting
- "Завершить диалог" button when `isStopSignal: true`
- Feedback modal with XP badge

## File Structure

### Backend
```
Features/Dialog/
  DialogBundle.cs                 — EF entity
  DialogMode.cs                   — EF entity  
  DialogSession.cs                — MongoDB entity
  DialogEntityConfigurations.cs   — EF configs
  DialogBundleDto.cs
  DialogModeDto.cs
  DialogSessionDto.cs
  DialogRequestDtos.cs
  IOpenAiChatService.cs           — interface + result types
  OpenAiChatService.cs            — GPT API calls
  DialogService.cs                — business logic
  DialogController.cs             — public endpoints
  AdminDialogController.cs        — admin CRUD
  DialogSeeder.cs                 — seed default bundles/modes (runs on startup)
Infrastructure/Mongo/
  MongoDbContext.cs               — DialogSessions collection
```

### Seeded content (DialogSeeder)

Runs on every startup, idempotent (skips when any bundle already exists).
Registered in `DialogServiceCollectionExtensions` and invoked from `Program.cs`
after `AchievementSeeder`. If the target skill `iconicName` is missing, a
lightweight fallback Skill is created so the seed never blocks startup.

| Bundle | Skill iconicName | Modes (all `voiceEnabled=true`) |
|--------|------------------|--------------------------------|
| 📞 Холодные звонки | `cold-calls` | Обход секретаря; Опеннер на ЛПР |
| 🛡️ Работа с возражениями | `objection-handling` | Возражение «Дорого» |

> Troubleshooting: if `/dialog` shows «Практика диалогов пока недоступна»,
> either `OpenAI:ApiKey` is not configured (bundles endpoint intentionally
> returns `[]`) or the seeder hasn't run yet — check backend startup logs for
> `Dialog seed:` lines.

### Frontend
```
app/(main)/dialog/
  page.tsx                        — bundles grid
  [bundleId]/
    page.tsx                      — modes grid
    [modeId]/
      page.tsx                    — chat screen with sidebar
app/(admin)/admin/dialog/
  page.tsx                        — bundles CRUD
  [bundleId]/
    page.tsx                      — modes CRUD with prompt editors
components/dialog/
  BundleCard.tsx
  ModeCard.tsx
  ChatMessage.tsx
  ChatInput.tsx
  FeedbackModal.tsx
  SessionHistorySidebar.tsx
lib/hooks/
  useDialog.ts                    — public hooks
  useAdminDialog.ts               — admin hooks
```

## Testing

See `docs/TESTING/AI_DIALOG.md` for:
- Manual test checklist
- Integration test outline
