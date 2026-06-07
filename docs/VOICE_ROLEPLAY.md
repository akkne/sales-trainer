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
        ├── User speaks → VAD detects end (~450ms silence)
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
 idle ──«Позвонить»──▶ dialing ──first AI reply──▶ connected ──hangup/endCall──▶ ended
  ▲                       │ ringback tone 425Hz        │ call timer, live          │ busy beeps,
  └──«Позвонить ещё раз»──┘ (1s on / 4s off)           │ subtitles, barge-in       ▼ feedback modal
                                                       │ vibrate on connect     /complete
```

- **Sounds** (`lib/voice/callSounds.ts`): ringback while `dialing`, triple busy
  beep on `ended` — synthesized with Web Audio oscillators, no binary assets.
- **Vibration**: `navigator.vibrate(80)` on `dialing → connected` (mobile).
- **Barge-in**: user speech during playback stops audio, aborts the in-flight
  `/voice/stream` fetch; the cut-off AI subtitle fades to 60% opacity with a
  «· прервано» label and dashed border.
- **Live subtitles**: interim recognizer text shown italic/dashed; committed
  phrases become user bubbles; AI reply streams chunk-by-chunk into one bubble.
- **Usage limits**: header shows `X/Y МИН СЕГОДНЯ` (from `GET /dialog/voice/usage`);
  backend returns 429 when `Voice:DailyLimitMinutes` / `MonthlyLimitMinutes`
  exceeded. Per-user spend report for admins: `GET /admin/voice/usage` +
  `/admin/voice/usage` page. Quota bars also shown on `/profile`.
- Leaving the page mid-call completes the session (fire-and-forget) so minutes
  and history are recorded; all tones stop on unmount.

Manual checklist: [TESTING/VOICE_CALL.md](TESTING/VOICE_CALL.md)

## UI Design (Duolingo-style)

- Round microphone button (centered or bottom)
- Green ring animation when user is speaking
- Mic disabled while AI is responding
- No avatar for now, no XP system changes

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

Barge-in: if the user starts talking during playback, audio stops and the
in-flight stream is aborted.

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

`TtsRouter` is the single source of truth for provider selection
(`Voice:TtsProvider`, fallback order yandex → google) and for the
"is voice configured" checks in both controllers. Yandex synthesizes raw LPCM
which the service wraps in a WAV header (v1 REST API has no mp3; OggOpus is not
decodable in Safari). A TTS failure is logged and swallowed — the user still
gets the reply as text, and the stream finishes normally with the final sentinel.

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
    "VadSilenceMs": 450,
    "MaxRecordingSeconds": 60,
    "DailyLimitMinutes": 30,
    "MonthlyLimitMinutes": 300
  }
}
```

### Buying voice API access from Russia

| Layer | Provider | Where to buy (RUB-friendly) | Config keys |
|-------|----------|------------------------------|-------------|
| **STT** | Deepgram | Через ProxyAPI / VseGPT (есть deepgram-compatible бридж) или напрямую с зарубежной картой | `Deepgram:ApiKey` |
| **STT (fallback)** | Web Speech API (браузер) | Бесплатно, не требует ключа | — |
| **TTS (основной)** | Yandex SpeechKit v1 | Yandex Cloud, рубли (карта/счёт). Latency <1 c — реалистичный звонок | `YandexTts:ApiKey`, `Voice:TtsProvider=yandex` |
| **TTS (alt)** | Google Cloud TTS | Google Cloud, иностранная карта | `GoogleTts:ApiKey`, `Voice:TtsProvider=google` |
| **TTS (alt)** | SaluteSpeech (Сбер) | СБП, но минимум 15 000 ₽/мес для юрлиц | отклонено |
| **LLM** | См. [AI_DIALOG.md](AI_DIALOG.md#buying-api-access-from-russia-rub-friendly-proxy-gateways) | — | `OpenAI:BaseUrl` |

`Voice:TtsProvider` явно выбирает провайдер; если выбранный не сконфигурирован,
`TtsRouter` фолбэчится в порядке yandex → google.

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
2. **Short VAD silence**: 450ms end-of-speech detection
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
