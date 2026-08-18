# Voice Roleplay Feature — Technical Specification

## Overview

Voice-based sales conversation practice in the existing Dialog tab. Stack:
- **VAD**: @ricky0123/vad-web (browser-based voice activity detection)
- **STT**: Deepgram Nova-3 WebSocket (streaming transcription)
- **LLM**: GPT-4.1 (conversation logic + character response)
- **TTS**: ElevenLabs Flash v2.5 streaming (voice synthesis)

**Target latency**: End of user speech → start of character audio ≤ 700ms

## User Flow

```
/dialog/[bundleId]/[modeId]
    ├── Text mode (existing) — keyboard input
    └── Voice mode (new) — microphone input
        ├── User speaks → VAD detects end (~1200ms silence)
        ├── Deepgram transcript ready (streamed parallel)
        ├── GPT evaluates + generates character response
        ├── ElevenLabs streams audio → playback starts
        └── Mic indicator shows green ring while speaking
```

## Telephone Call Mode (Phase 36)

Full-screen call simulator at `/dialog/[bundleId]/[modeId]/voice` («Позвонить»
CTA on the mode card). Continuous VAD — no push-to-talk.

### Call state machine

```
 idle ──«Позвонить»──▶ dialing ──session ready──▶ connected ──hangup/endCall──▶ ended
  ▲                       │                          │ call timer, live         │ /complete,
  └──«Позвонить ещё раз»──┘                          │ subtitles, no barge-in   ▼ feedback modal
                                                     │ vibrate on connect
```

- **Silent by design**: no ringback and no busy tones. The synthesized Web Audio
  tones were removed — they added nothing to a training call and only made the
  page noisy. The only remaining state cue is haptic.
- **Vibration** (`features/voice/services/call-haptics.ts`): `navigator.vibrate(80)`
  on `dialing → connected` (mobile).
- **The call connects on "session ready"** (`useVoice.onSessionReady`), which fires
  both for a freshly created session and for one handed in from outside (a custom
  scenario pre-started on another page). Reused sessions used to skip the callback,
  leaving the call on «Соединение…» for its whole duration.
- **Every call gets its own session**: `endSession()` drops the session id inside
  `useVoice`, so «Позвонить снова» always creates a fresh one. (`stopVoice()` only
  stops listening and keeps the session — the chat mic button toggles voice input
  off and on inside one dialog; the call pages call both on hang-up.)
  Reusing a completed session made the backend reject every turn and left the page
  hanging on «Соединение…» → «Готовим разбор…» with nothing in flight.
- **A refused turn is never silent.** `POST .../voice/stream` answers `409` when the
  session is finished/missing/not voice-enabled (it used to set `200` before calling
  the service, so a rejected turn arrived as an empty body — the persona just never
  spoke). The client turns `409` into «Этот звонок уже завершён», and a stream that
  yields zero frames for any other reason raises «Собеседник не ответил».
- **A pre-started scenario session is single-use.** `/dialog/[bundleId]/[modeId]/voice?session=…`
  checks the session's status before dialling; once it is played out the CTA becomes
  «К сценариям» instead of «Позвонить снова» — the page cannot recreate the scenario
  text, and a session started here without it is rejected by the backend (400).
- **No barge-in (persona finishes first)**: the microphone is paused for the whole
  AI turn (paused before the `/voice/stream` request, resumed only when playback
  ends). We deliberately do **not** listen while the persona is speaking — barge-in
  used to fire on the slightest noise and cut the AI off mid-sentence. The persona
  finishes, then the frontend starts listening. (The legacy `interrupted`/«· перебито»
  subtitle state is kept but is now unreachable.)
- **Live subtitles**: interim recognizer text shown italic/dashed; committed
  phrases become user bubbles; AI reply streams chunk-by-chunk into one bubble.
- **End-of-speech detection** (`features/voice/services/speech-endpointer.ts`):
  the silence timer is armed on *interim* recognition results too
  (`vadSilenceMs + 250ms` grace), so an utterance is committed without waiting
  for the browser to finalize it (Web Speech finalization adds 0.5–1.5s).
  A final result arms the plain `vadSilenceMs` timer; recognition results that
  arrive while a turn is already processing are dropped to avoid duplicates.
- **Usage limits**: header shows `X/Y МИН СЕГОДНЯ` (from `GET /dialog/voice/usage`);
  backend returns 429 when `Voice:DailyLimitMinutes` / `MonthlyLimitMinutes`
  exceeded. Per-user spend report for admins: `GET /admin/voice/usage` +
  `/admin/voice/usage` page. Quota bars also shown on `/profile`.
  **Since Phase 40.33 there is a second, organization-wide window under the per-user one** —
  see "Per-organization voice limits" below.
- Leaving the page mid-call completes the session (fire-and-forget) so minutes
  and history are recorded.
- **The analysis never spins forever**: `POST /dialog/sessions/{id}/complete` is
  capped at 120s client-side (`RequestTimeoutError`; the backend's own upstream
  budget is 90s), a failure shows the reason plus a «Повторить разбор» button, and
  a call that ended with no session says so («Разбирать нечего…») instead of
  claiming «Готовим разбор…». A retry on a session the backend already completed
  returns the stored feedback instead of an error.

Manual checklist: [TESTING/VOICE_CALL.md](TESTING/VOICE_CALL.md)

## UI Design

- Round microphone button (centered or bottom)
- Green ring animation when user is speaking
- Mic disabled while AI is responding
- No avatar for now, no progress-points system changes

## Architecture

### Frontend Voice Pipeline

```
Microphone → Web Speech API (browser STT, ru-RU, interim results → live subtitles)
                                          ↓
                       Silence timeout commits the phrase
                                          ↓
                  POST /dialog/sessions/:id/voice/stream  { transcript }
                                          ↓
        LLM streams {"reply", "endCall"} → text frames pushed immediately (per sentence)
                                          ↓
          each sentence → realtime TTS (pipelined with LLM stream) → audio frame
                                          ↓
   Length-prefixed frames (text / mp3) → Web Audio queued playback + streamed AI subtitles
```

No barge-in: the mic stays paused for the entire AI turn and only resumes once
playback ends, so the persona always finishes speaking before the frontend listens.

### Backend Voice Endpoint

```
POST /dialog/sessions/{sessionId}/voice/stream
Body: { transcript: string }
Response: application/octet-stream — length-prefixed frames
          (uint32 flags | uint32 textLen | text | uint32 audioLen | mp3),
          flag bit 0 = isFinal sentinel, bit 1 = isStopSignal (endCall)
Side effect: saves user + assistant messages to the session
```

The chat model answers with structured JSON `{"reply": string, "endCall": bool}`;
`StreamingChatReplyParser` extracts the reply incrementally. **Text frames are
yielded immediately** (audio length 0) so live subtitles appear within seconds.
Both providers (Yandex SpeechKit, Google) are synchronous realtime APIs
answering in well under a second. Synthesis is **pipelined with the LLM
stream**: as soon as a sentence is extracted, its TTS request starts in the
background while the next sentence is still streaming from the LLM; audio
frames are flushed in reply order as they complete, so speech starts almost
immediately and flows sentence-by-sentence with no stalls between sentences.

Sentence extraction lives in `SentenceChunker` (unit-tested separately). The
**first** chunk also splits on clause delimiters (`, ; : — –` followed by
whitespace, so `1,5` stays intact) with a lower minimum length (12 chars vs
20), so the very first TTS request — and the first audible audio — starts as
early as possible; subsequent chunks split on sentence enders (`. ! ? \n`)
only, keeping natural prosody.

`TtsRouter` is the single source of truth for provider selection
(Yandex TTS is the sole supported provider) and for the
"is voice configured" checks in both controllers. It is wrapped by
`CachingTtsRouter`: audio for short phrases (≤80 chars) is cached in-process
(`TtsAudioCache`, 32 MB size-bounded, 24h TTL), so repeated greetings and
confirmations skip the provider round-trip entirely.

Connections to OpenAI / Yandex TTS are kept warm:
the named HttpClients use `SocketsHttpHandler` with a 10-minute pooled idle
timeout, and `UpstreamConnectionWarmupService` HEAD-pings each configured
upstream every 4 minutes, so a dialog turn after an idle period does not pay
the TCP+TLS handshake (~100–300ms). Yandex synthesizes raw LPCM
which the service wraps in a WAV header (v1 REST API has no mp3; OggOpus is not
decodable in Safari). A TTS failure is logged and swallowed — the user still
gets the reply as text, and the stream finishes normally with the final sentinel.

### Per-organization voice limits (Phase 40.33)

Until 40.33 the only voice limit was **per user**: 30 minutes a day, 300 a month, the same numbers
for every customer, from `Voice:DailyLimitMinutes` / `MonthlyLimitMinutes`. A customer's *total*
voice spend was therefore however many users they had times that allowance — adding seats added
budget, and nothing anywhere capped the organization. That is the roadmap's «один клиент, гоняющий
голос сутками».

40.33 adds an organization-wide window **under** the per-user one. Both apply on every turn:

```
org:{orgId}:voice:{userId}:day:{y}:{m}:{d}     ← per user   (since the feature shipped)
org:{orgId}:voice:{userId}:month:{y}:{m}
org:{orgId}:voice:org:day:{y}:{m}:{d}          ← per organization (40.33)
org:{orgId}:voice:org:month:{y}:{m}
```

Both are kept deliberately. The per-user limit stops one person burning the whole organization's day,
which the organization limit alone cannot; the organization limit stops the customer the roadmap
names, which the per-user limit alone could not.

- **The organization's numbers come from `OrganizationQuotas` in ai-db**, per organization, set by
  platform staff through `PUT /admin/ai-quota`. An organization with no row is metered against the
  platform defaults in `AiQuotas:DefaultVoice*LimitMinutes` (600/day, 6000/month) — never unmetered.
  Full reasoning: [AI_QUOTAS.md](AI_QUOTAS.md) §2.
- **The reservation order is per-user first, organization second**, and an organization refusal rolls
  both per-user reservations back before it throws, so a blocked call leaves no phantom reservation.
- **The client contract is unchanged**: still `429`, still `{error, period, usedSeconds,
  limitSeconds}`. The `period` reads `organization day` / `organization month` when it is the
  organization window that closed.
- **`GET /admin/voice/usage` grew four fields** — `organizationDailyLimitSeconds`,
  `organizationMonthlyLimitSeconds`, `organizationUsedSecondsToday`,
  `organizationUsedSecondsThisMonth`. The per-user rows answer «кто много говорит»; these answer
  «сколько осталось у компании», which is the number that decides whether the next call connects.
- **Durable accounting is unchanged**: actual seconds still land in Mongo `dialog_sessions` during
  the refund step. Redis holds the reservation window only.

### Synthesis is now metered too, and the exercise path stopped bypassing it (Phase 40.33)

`YandexTtsService` records every synthesized character against the calling organization
(`AiUsageRecords`, kind `tts`) — charged at the provider call, so a `CachingTtsRouter` hit costs
nothing and is recorded as nothing.

This is also where a real hole closed. The interactive `ai_dialogue` exercise
(`POST /exercises/{id}/voice/stream`) was served by **learning-service's own copy** of `TtsRouter` and
`YandexTtsService`, against a `YandexTts:ApiKey` that service held itself. Every sentence it spoke was
synthesized outside the voice meter entirely. learning-service now calls `POST /ai/tts` and holds no
speech key at all. `scripts/ai-provider-lint.py` keeps it that way.

### Configuration (appsettings.json)

```json
{
  "Deepgram": {
    "ApiKey": "REPLACE_WITH_DEEPGRAM_API_KEY",
    "Model": "nova-3",
    "Language": "ru",
    "SmartFormat": true,
    "Punctuate": true
  },
  "YandexTts": {
    "ApiKey": "REPLACE_WITH_YANDEX_API_KEY",
    "BaseUrl": "https://tts.api.cloud.yandex.net",
    "Voice": "marina",
    "Lang": "ru-RU",
    "Speed": "1.2"
  },
  "Voice": {
    "Enabled": true,
    "TtsProvider": "yandex",
    "VadSilenceMs": 1200,
    "MaxRecordingSeconds": 60,
    "DailyLimitMinutes": 30,
    "MonthlyLimitMinutes": 300
  },
  "AiQuotas": {
    "DefaultVoiceDailyLimitMinutes": 600,
    "DefaultVoiceMonthlyLimitMinutes": 6000,
    "DefaultLlmMonthlyTokenLimit": 20000000,
    "DefaultBatchReservePercent": 10
  }
}
```

`Voice:*LimitMinutes` remain the **per-user** allowance and stay platform-wide. `AiQuotas:Default*`
are the **per-organization** defaults, overridden per customer in `OrganizationQuotas`. Both sections
live in ai-service only — learning-service no longer reads either.

### Buying voice API access from Russia

| Layer | Provider | Where to buy (RUB-friendly) | Config keys |
|-------|----------|------------------------------|-------------|
| **STT** | Deepgram | Через ProxyAPI / VseGPT (есть deepgram-compatible бридж) или напрямую с зарубежной картой | `Deepgram:ApiKey` |
| **STT (fallback)** | Web Speech API (браузер) | Бесплатно, не требует ключа | — |
| **TTS** | Yandex SpeechKit v1 | Yandex Cloud, рубли (карта/счёт). Latency <1 c — реалистичный звонок | `YandexTts:ApiKey` |
| **TTS (alt)** | SaluteSpeech (Сбер) | СБП, но минимум 15 000 ₽/мес для юрлиц | отклонено |
| **LLM** | См. [AI_DIALOG.md](AI_DIALOG.md#buying-api-access-from-russia-rub-friendly-proxy-gateways) | — | `OpenAI:BaseUrl` |

Единственный поддерживаемый TTS-провайдер — Yandex SpeechKit. `TtsRouter` проверяет, что `YandexTts:ApiKey` задан; если нет — запрос завершается ошибкой.

#### Как получить ключ Yandex SpeechKit

1. Зарегистрироваться / войти в [Yandex Cloud](https://console.yandex.cloud), создать платёжный аккаунт (карта РФ).
2. Создать каталог (folder), в нём — **сервисный аккаунт** с ролью `ai.speechkit-tts.user`.
3. У сервисного аккаунта создать **API-ключ** (не IAM-токен — API-ключ бессрочный).
4. Вставить ключ в `appsettings.Development.json` → `YandexTts:ApiKey` и перезапустить backend.
   `folderId` при авторизации API-ключом сервисного аккаунта не нужен.

Цены (2026): ~1 300 ₽/млн символов, новым аккаунтам даётся стартовый грант.
Голоса: `marina` (по умолчанию), `alexander`, `lera`, `masha`, `dasha`, `julia`,
`alena`, `filipp` — меняются через `YandexTts:Voice` без пересборки.

## Database Changes

### PostgreSQL — DialogMode extension

Add to `DialogModes` table:
- `VoiceEnabled` (bool, default false) — whether voice mode is available for this mode
- `VoiceId` (string, nullable) — ElevenLabs voice ID override (uses default if null)

### Admin Panel

- Toggle voice enabled per mode
- Voice ID override field

## API Endpoints

### Voice Session Endpoints

| Method | Path | Body | Response |
|--------|------|------|----------|
| POST | /dialog/sessions/{sessionId}/voice/stream | `{transcript}` | length-prefixed text+mp3 frames (see Architecture) |
| GET | /dialog/voice/config | — | `{enabled, vadSilenceMs, ...}` |
| GET | /dialog/voice/usage | — | daily/monthly usage and limits |

> Legacy non-streaming endpoints (`POST .../voice`, `GET .../voice/response`) were removed.

### Admin Endpoints

| Method | Path | Body | Response |
|--------|------|------|----------|
| PUT | /admin/dialog/modes/{id} | `{voiceEnabled?, voiceId?}` | `AdminDialogModeDto` |

## Services

### IDeepgramService

```csharp
public interface IDeepgramService
{
    bool IsConfigured { get; }
    Task<string> TranscribeStreamAsync(Stream audioStream, CancellationToken ct);
}
```

Note: Deepgram WebSocket runs in browser, backend only needs to validate config.

### IElevenLabsService

```csharp
public interface IElevenLabsService
{
    bool IsConfigured { get; }
    Task<Stream> SynthesizeSpeechAsync(string text, string? voiceId, CancellationToken ct);
}
```

### IVoiceDialogService

```csharp
public interface IVoiceDialogService
{
    Task<Stream> ProcessVoiceMessageAsync(
        string sessionId, 
        string userId, 
        string transcript, 
        CancellationToken ct);
}
```

## Frontend Components

### New Files

```
lib/voice/
  vadManager.ts         — @ricky0123/vad-web wrapper
  deepgramClient.ts     — Deepgram WebSocket client
  audioPlayer.ts        — Web Audio API streaming playback
components/dialog/
  VoiceMicButton.tsx    — Duolingo-style mic with green ring
  VoiceChat.tsx         — Voice mode wrapper for chat page
lib/hooks/
  useVoice.ts           — Voice pipeline orchestration hook
```

### VoiceMicButton States

1. **Idle**: Gray mic icon, tap to start
2. **Listening**: Green ring pulsing, VAD active
3. **Processing**: Loading indicator, waiting for AI
4. **Playing**: Speaker icon, AI audio playing
5. **Disabled**: Grayed out (AI responding or not configured)

## Graceful Degradation

If any service is not configured:
- `GET /dialog/voice/config` returns `{enabled: false}`
- Voice button hidden in UI
- Text mode works as before
- Admin can still configure modes for future use

## Latency Optimization

1. **Parallel STT**: Deepgram streams transcription while user speaks
2. **VAD silence window**: 1200ms end-of-speech detection — wide enough that the
   speaker can pause mid-thought without being cut off before finishing
3. **Pipelined TTS**: sentence N is synthesized concurrently with LLM streaming
   of sentence N+1; audio frames flush in reply order as soon as they are ready
4. **WebSocket reuse**: Keep Deepgram connection open during session
5. **Audio prefetch**: Start playback immediately on first chunk

## Error Handling

- Microphone permission denied → show toast, fall back to text
- Deepgram connection lost → reconnect or fall back to text
- ElevenLabs error → show error, user can retry
- Network timeout → show error, preserve transcript in input

## Testing Checklist

See `docs/TESTING/VOICE_ROLEPLAY.md`

---

## QUESTIONS (RESOLVED)

1. ~~**Deepgram API key source**~~ → User provides API key in config
2. ~~**ElevenLabs voices**~~ → Default voice ID `21m00Tcm4TlvDq8ikWAM` (Rachel)
3. ~~**Voice mode toggle**~~ → No toggle. Mode is either text-only or voice-only based on `voiceEnabled` flag
4. ~~**Mobile support**~~ → Yes, supported
5. ~~**Session continuity**~~ → Text and voice are separate modes. User cannot switch mid-session
6. ~~**Rate limiting**~~ → Configurable limits in appsettings: `DailyLimitMinutes`, `MonthlyLimitMinutes`

---

## Implementation Status

### Completed

**Backend:**
- [x] Config sections in `appsettings.json` (Deepgram, ElevenLabs, Voice)
- [x] `IElevenLabsService` + `ElevenLabsService` (streaming TTS)
- [x] `IVoiceDialogService` + `VoiceDialogService` (orchestrates GPT + TTS)
- [x] `VoiceConfigController` — `/dialog/voice/config`, `/dialog/voice/deepgram-key`
- [x] `VoiceDialogController` — `POST /dialog/sessions/{sessionId}/voice`
- [x] Migration: `VoiceEnabled`, `VoiceId` fields on `DialogModes`
- [x] DTOs updated with voice fields

**Frontend:**
- [x] `@ricky0123/vad-web` installed
- [x] `lib/voice/vadManager.ts` — VAD wrapper
- [x] `lib/voice/deepgramClient.ts` — WebSocket client
- [x] `lib/voice/audioPlayer.ts` — Web Audio playback
- [x] `useVoice.ts` hook — full voice pipeline
- [x] `VoiceMicButton.tsx` — Duolingo-style mic with states
- [x] Chat page integration with voice mode
- [x] Admin panel: voice toggle + voice ID per mode

### Not Implemented

- [ ] Unit tests for `ElevenLabsService`
- [ ] Integration tests for voice endpoint
- [ ] Frontend component tests for `VoiceMicButton`
