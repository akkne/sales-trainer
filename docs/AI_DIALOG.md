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
| POST | `/dialog/sessions/{sessionId}/complete` | End session, generate feedback, award XP. Returns `204` (session abandoned, no feedback) when the user never sent a message |

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
| POST | `/admin/dialog/import` | Bulk import bundles + nested modes from one JSON file |

### Bulk import

`POST /admin/dialog/import` (`multipart/form-data`, `file=<JSON>`, ≤20 MB) loads
whole dialog bundles with their modes in one file — the same import/template UX as
content (Download Template + paste/upload on the `/admin/dialog` page). Bundles
reference their skill by `skillIconicName`. Upsert is idempotent: bundles by
`(skillId, title)`, modes by `(bundleId, key)`. Bad items (unknown skill, empty
key/title) are skipped into `errors[]`; everything else is still written. See the
import-shape contract in [API_CONTRACTS.md](API_CONTRACTS.md). Tested by
`tests/Integration/AdminDialogImportTests.cs` (create, idempotent re-import,
unknown-skill error, 403 for non-admin — requires Docker).

## AI Integration

### Configuration (appsettings.json)

```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "BaseUrl": "https://api.openai.com",
    "ChatCompletionsPath": "/v1/chat/completions",
    "ChatModel": "gpt-4.1-nano",
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

**ChatSystemPrompt** — AI role for the conversation. Backend appends a structured-output
instruction: the model must answer ONLY with a JSON object

```json
{"reply": "<реплика персонажа>", "endCall": true|false}
```

enforced via `response_format` (json_schema; flat OpenRouter shape for the f5ai proxy,
nested `json_schema` shape for OpenAI). `reply` always comes first so the voice pipeline
can stream it to TTS while the model is still generating
(`StreamingChatReplyParser` extracts it incrementally and tolerates plain-text fallback).

**FeedbackSystemPrompt** — AI evaluation instructions. Backend appends honest-evaluation
rules (cite the dialog verbatim, no invented praise), the `[DETAILED]` two-block format
and the `[XP:число]` tag requirement (see XP Rewards below).

### Call Termination (endCall)

The persona model returns `endCall: true` when it hangs up:
- Immediately on critical user mistakes — swearing, rudeness, rambling nonsense,
  begging, weak openers without specifics, repeating a rejected argument, lying.
- Normally when the conversation reached its logical end.

`endCall` maps to `isStopSignal` on the stored message, the stream frame flags and the
chat DTOs (wire/storage names unchanged). The frontend ends the call and requests feedback.

### XP Rewards

AI generates XP (sum 0-100), each criterion counted only if it actually occurred:
- Confidence and tone: up to 25 XP
- Argument structure and substance: up to 25 XP
- Objection handling (if there were objections): up to 25 XP
- Achieving the call goal (passed secretary, scheduled meeting): up to 25 XP

Calibration: 0-20 fail (client hung up due to user mistakes), 21-45 weak,
46-70 normal, 71-85 good, 86-100 exceptional (rare).

Hard rules enforced in code:
- A response without an `[XP:N]` tag awards **0 XP** (no silent defaults).
- A session with **no user messages** is marked `abandoned` without calling the
  feedback model at all — `/complete` returns `204 No Content`, no XP, no feedback modal.

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
Infrastructure/Mongo/
  MongoDbContext.cs               — DialogSessions collection
```

### Seeded content

No default bundles or modes are seeded except for the special **company-call** mode
described below. All other dialog content comes from the database — create it via
the admin CRUD (`AdminDialogController` / admin panel).

> Troubleshooting: if `/dialog` shows «Практика диалогов пока недоступна»,
> either `OpenAI:ApiKey` is not configured (bundles endpoint intentionally
> returns `[]`) or no bundles have been created yet in the admin panel.

## Company-Context Sessions

### Purpose

When the `company-service` creates a practice call for a specific prospect company,
it injects structured company facts into the AI session so the model plays a realistic
employee of that company rather than a generic persona.

### Seeded template

`CompanyCallModeSeeder` runs at ai-service startup (idempotent — skips if key
`company-call` already exists). It creates:

- A `DialogBundle` with `IsHidden = true` — excluded from `GET /dialog/bundles` but
  accessible via admin CRUD and used internally.
- A `DialogMode` with `Key = "company-call"`, `VoiceEnabled = true`, admin-editable
  prompts in Russian describing: AI plays an employee/decision-maker at the prospect
  company; behave realistically for a cold sales call; use the injected company facts.

The fixed `{bundleId, modeId}` for this mode is available via
`GET /dialog/company-call-mode`.

### Request contract

`POST /dialog/sessions` accepts an optional `companyContext` object:

```json
{
  "bundleId": "<company-call bundle id>",
  "modeId": "<company-call mode id>",
  "companyContext": {
    "companyName": "ООО Рога и Копыта",
    "companyDescription": "Поставщик офисных принадлежностей для малого бизнеса",
    "callGoal": "Записать встречу с директором"
  }
}
```

`callGoal` is optional. When omitted, the goal line is not appended to the prompt.

### Prompt composition

When `companyContext` is present the service appends a structured block to both
`ChatSystemPrompt` and `FeedbackSystemPrompt` at runtime:

```
---
Компания: <companyName>
Описание: <companyDescription>
Цель звонка пользователя: <callGoal>   ← omitted if callGoal is blank
```

The base prompts are stored in PostgreSQL and remain unchanged. The appended block
is built in `DialogService.BuildChatSystemPrompt` / `BuildFeedbackSystemPrompt`
(and equivalently in `VoiceDialogService.BuildChatSystemPrompt` for voice streaming).

### Persistence

`companyContext` is stored in the MongoDB `DialogSession` document as
`companyCallContext: {companyName, companyDescription, callGoal?}`. All subsequent
turns (`SendMessageAsync`, `CompleteSessionAsync`) and the voice path
(`VoiceDialogService.StreamVoiceMessageAsync`) read it from the session, so the
context is consistent across the entire conversation without re-sending it on every
request.

### Hidden bundle invariant

`GetActiveBundlesAsync` filters `IsHidden = true` bundles, so the company-call bundle
never appears in the user-facing `/dialog/bundles` listing. Admin CRUD (`/admin/dialog/bundles`)
shows all bundles including hidden ones and allows editing the prompts.

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
