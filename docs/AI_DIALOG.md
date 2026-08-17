# AI Dialog Feature — Technical Specification

## Overview

New tab "Диалог" (left of Profile) where users practice sales skills via AI-powered chat conversations.

## User Flow

```
/dialog (tab)
    └── Skill bundles grid (e.g., "Холодные звонки")
            └── Mode selection (e.g., "Обход секретаря")
                    └── Chat screen with GPT-4.1-mini + history sidebar
                            └── Conversation ends → GPT-4.1 feedback popup (no points shown)
```

## Data Model

### PostgreSQL Tables

**DialogBundles** (linked to Skills)
```sql
CREATE TABLE "DialogBundles" (
    "Id" uuid PRIMARY KEY,
    "OrganizationId" uuid NULL,             -- 40.11: NULL = global library, else org-authored
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
    "OrganizationId" uuid NULL,             -- 40.11: NULL = global library, else org-authored
    "BundleId" uuid NOT NULL REFERENCES "DialogBundles"("Id") ON DELETE CASCADE,
    "Key" varchar(100) NOT NULL,
    "Title" varchar(200) NOT NULL,
    "Description" varchar(1000) NOT NULL,
    "ChatSystemPrompt" text NOT NULL,       -- AI role for conversation
    "FeedbackSystemPrompt" text NOT NULL,   -- AI evaluation instructions
    "SortOrder" int NOT NULL,
    "IsActive" bool NOT NULL DEFAULT true,
    "CreatedAt" timestamp NOT NULL,
    "UpdatedAt" timestamp NOT NULL
);

-- 40.11: the mode key is unique per organization, not per installation. Two indexes, because
-- Postgres treats NULLs in a composite unique index as distinct, so the composite one alone
-- would not stop two global modes sharing a key.
CREATE UNIQUE INDEX "IX_DialogModes_OrganizationId_BundleId_Key"
    ON "DialogModes" ("OrganizationId", "BundleId", "Key") WHERE "OrganizationId" IS NOT NULL;
CREATE UNIQUE INDEX "IX_DialogModes_BundleId_Key_Global"
    ON "DialogModes" ("BundleId", "Key") WHERE "OrganizationId" IS NULL;
```

Both tables are **content** in the tenancy model (`docs/TENANCY/CONTENT_MODEL.md`): `NULL` means
the global library every organization sees, a non-null value means one organization authored the
row. The EF query filter reads `OrganizationId IS NULL OR = current` — never plain equality, which
would hand a new customer an empty practice page — and Postgres row-level security enforces the
same comparison underneath (migration `20260815154837_AddOrganizationId`). The two seeded hidden
bundles, `company-call` and `custom-scenario`, stay global on purpose: the frontend looks them up
by key and every organization needs them.

### MongoDB Collection

**dialog_sessions** (user conversations — kept in MongoDB for flexible message array)
```json
{
  "_id": ObjectId,
  "organizationId": "guid",   // 40.11: owning organization, never absent
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
    "summary": "string",
    "content": "string",
    "score": number,          // 0–10 balanced grade (carrot-and-stick)
    "generatedAt": ISODate
  },
  "xpEarned": number,
  "createdAt": ISODate,
  "completedAt": ISODate
}
```

#### The tenant boundary in Mongo (40.11)

Mongo has no row-level security. For every Postgres table in this codebase the EF query filter is
convenience and the RLS policy is the boundary; here **there is no second layer** — the application
is the boundary, and a boundary spread across call sites is one that gets forgotten on the next
one. So all session access goes through a single class:

| | |
|---|---|
| Class | `Features/Dialog/Services/Implementation/DialogSessionRepository.cs` |
| Interface | `IDialogSessionRepository` — no method takes an organization, none returns "all organizations" |
| Construction | requires `ITenantContext`; the collection handle is created here and nowhere else |
| Unset tenant | throws `InvalidOperationException("Organization context is not set.")` — never an empty list, which would hide a misconfigured gateway behind an empty history screen |
| System bypass | none. Nothing in ai-service reads sessions outside a request; a future background reader must add an explicitly reviewed method (roadmap 40.14) |

`MongoDbContext` no longer exposes the collection at all, and a unit test
(`AiTenancyModelTests.Only_the_repository_reaches_the_dialog_sessions_collection`) fails the build
if a second file ever names `GetCollection<DialogSession>`.

Indexes all lead with `organizationId`, which is also the designated prefix of any future shard
key — a shard key that did not start with the tenant would scatter one customer's sessions across
every shard and make the cross-tenant scan the cheap operation. They are created by
`docs/TENANCY/mongo/40.11_dialog_sessions_organization_backfill.js`, which also backfills existing
documents. Until that script runs, pre-40.11 sessions match no organization's filter and are
invisible — fail-closed, the same shape RLS gives Postgres.

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
| POST | `/dialog/sessions/{sessionId}/complete` | End session, generate feedback, award progress points. Returns `204` (session abandoned, no feedback) when the user never sent a message |

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
content (Download Template + paste/upload on the `/admin/dialog` page). In the
JSON, bundles reference their skill by the friendly `skillIconicName`; the admin
page resolves it to a `skillId` (GUID) client-side before upload — the endpoint
itself keys bundles by `skillId` because the ai-service does not own the `Skills`
table. A bundle that already carries a `skillId` (e.g. a re-imported export)
passes through untouched. Upsert is idempotent: bundles by
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
    "DialogModel": "gpt-4o",
    "OpenQuestionModel": "gpt-4.1",
    "MaximumDialogTokenCount": 500,
    "MaximumFeedbackTokenCount": 1500
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
{"reply": "<реплика персонажа>", "endCall": true|false, "endCallReason": "<причина или null>"}
```

enforced via `response_format` (json_schema; flat OpenRouter shape for the f5ai proxy,
nested `json_schema` shape for OpenAI). `reply` always comes first so the voice pipeline
can stream it to TTS while the model is still generating
(`StreamingChatReplyParser` extracts it incrementally and tolerates plain-text fallback).

**FeedbackSystemPrompt** — AI evaluation instructions. Backend appends honest-evaluation
rules (cite the dialog verbatim, no invented praise), the `[DETAILED]` two-block format,
the balanced `[SCORE:число]` grade (see Overall Score below) and the `[XP:число]` tag
requirement (see Progress Point Rewards below).

### Organization profile and `banned_claims` (Phase 40.19)

Both stored prompts may contain `{{organization.product}}`, `{{organization.icp}}`,
`{{organization.tone}}`, `{{organization.objections}}`, `{{organization.script}}` and
`{{organization.glossary.<term>}}`. They resolve from the caller's organization profile
(`PUT /organizations/profile`) **on the way to the model**, never in the stored row — so one base
persona serves every customer and `DialogMode.BaseContentHash` (40.18) stays identical across
organizations. An unfilled field renders as neutral prose («ваш продукт», «ваш клиент»), never as a
blank and never as visible curly braces. Full syntax:
[CONTENT_PARAMETERIZATION.md](CONTENT_PARAMETERIZATION.md).

Placeholders **outside** the `organization.` namespace pass through untouched, which is what keeps
the seeded hidden modes (`company-call`, `custom-scenario`) working — their prompts are completed at
run time from placeholders the code supplies.

Assembly order for both prompts, and it is deliberate:

1. `{{organization.*}}` resolved in the stored prompt;
2. company-call / custom-scenario blocks appended (unchanged from before 40.19);
3. the organization context block, then the banned-claims block — **last**.

A compliance rule that a later block can qualify is not a rule.

**`banned_claims` binds both sides of the call.** The persona never voices or confirms a listed
claim, even if the user provokes it or says it first — it deflects or asks again rather than
repeating it — and the rule is stated as outranking the role, the character and every instruction
above it. The feedback prompt gets the mirror rule: never reward a banned claim, lower the score, and
name the violation in the feedback text. Enforcing only the persona side would be worse than nothing,
because a grader that keeps rewarding «мы гарантируем доходность» teaches the rep to say it anyway.

At most 10 objections from the profile reach a prompt, and any single substituted value is capped at
2000 characters.

### Conversation style

The appended instruction tells the persona to behave like a real person: greet back,
answer in full natural sentences (not curt one-liners), react to what the caller says,
ask follow-ups, and warm up when the caller is polite and on-topic. Nervousness, pauses
or a weak opener are explicitly **not** grounds to be rude or hang up — the persona gives
the caller a chance to recover. Toughness scales with the persona difficulty.

### Call Termination (endCall / endCallReason)

The persona model decides on its own when to hang up and returns `endCall: true`:
- When genuinely disrespected — swearing, insults, threats, aggression.
- When the caller lies/manipulates or the talk turns into nonsense that doesn't recover
  after one clarification.
- Normally, when the conversation reached its logical end (agreed / final refusal).

When `endCall: true`, the model fills `endCallReason` with a short tag
(`оскорбления`, `манипуляция`, `договорились`, `отказ`, …); otherwise it returns `null`.
`endCallReason` is parsed by `StreamingChatReplyParser` and logged server-side (chat and
voice paths).

**Farewell safety net:** models sometimes voice a goodbye in `reply` but leave
`endCall: false`, leaving the call hanging. `StreamingChatReplyParser` therefore forces
`endCall: true` (reason `farewell`) whenever the reply contains a clear terminal farewell
(«всего доброго», «до свидания», «кладу трубку», …). A persona goodbye always ends the call. `endCall` still maps to `isStopSignal` on the stored message, the stream
frame flags and the chat DTOs (wire/storage names unchanged); the frontend ends the call
and requests feedback.

### Overall Score (0–10)

Every completed session (voice **call** and **text** practice alike) gets a single
0–10 grade shown at the top of the feedback modal. It is deliberately **carrot-and-stick**:
the prompt requires the model to name at least one genuine strength when one exists (pryanik)
and to point out key mistakes firmly but without condescension (knut). Calibration:
`0–2` fail, `3–4` weak, `5–6` normal (a working result, not a punishment), `7–8` good,
`9–10` excellent (rare); a one-to-two-line dialog caps at `4`.

The model emits a `[SCORE:N]` tag on its own line; the backend parses and clamps it to
`0–10` (`OpenAiChatService.ExtractScore`), stores it on `DialogFeedback.Score`, and returns
it via `/complete` and session DTOs. A missing tag defaults to `0`. The frontend
`FeedbackModal` renders it as a colored badge (bad / warn / success) with a label.

### Progress Point Rewards

> **Not shown to the user (2026-08-14).** A call awards no visible experience: the analysis is the
> reward. `xpEarned` stays on the `/complete` response and on `dialog.evaluated` — the model still
> scores the session and the score drives the criteria below — but neither the feedback modal nor
> the session history renders it. Same pattern as the skill tree, which still returns its
> gamification fields without displaying them.

AI generates progress points (sum 0-100), each criterion counted only if it actually occurred:
- Confidence and tone: up to 25 points
- Argument structure and substance: up to 25 points
- Objection handling (if there were objections): up to 25 points
- Achieving the call goal (passed secretary, scheduled meeting): up to 25 points

Calibration: 0-20 fail (client hung up due to user mistakes), 21-45 weak,
46-70 normal, 71-85 good, 86-100 exceptional (rare).

Hard rules enforced in code:
- A response without an `[XP:N]` tag awards **0 points** (no silent defaults).
- A session with **no user messages** is marked `abandoned` without calling the
  feedback model at all — `/complete` returns `204 No Content`, no progress points, no feedback modal.

Progress points are saved to `UserXpRecords` with source `"dialog"`.

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
- Each session shows: mode title, bundle title, message count, points earned
- Click session → load its messages
- Click "К выбору навыка" → return to `/dialog`

### Chat Screen

- Toggle sidebar button (☰)
- Close button (✕) → return to `/dialog`
- Header: mode title + bundle name
- Messages: green (user, right), gray (AI, left)
- Typing indicator while waiting
- "Завершить диалог" button when `isStopSignal: true`
- Feedback modal showing points earned

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

No default bundles, modes, or skills are seeded. All dialog content and skills
come exclusively from the database — create them via the admin CRUD
(`AdminDialogController` / admin panel). There is no startup seeder for dialog
content (the former `DialogSeeder` and its hardcoded `practice`-stage skills
were removed).

> Troubleshooting: if `/dialog` shows «Практика диалогов пока недоступна»,
> either `OpenAI:ApiKey` is not configured (bundles endpoint intentionally
> returns `[]`) or no bundles have been created yet in the admin panel.

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
