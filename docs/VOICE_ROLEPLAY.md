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
        ├── User speaks → VAD detects end (~600ms silence)
        ├── Deepgram transcript ready (streamed parallel)
        ├── GPT evaluates + generates character response
        ├── ElevenLabs streams audio → playback starts
        └── Mic indicator shows green ring while speaking
```

## UI Design (Duolingo-style)

- Round microphone button (centered or bottom)
- Green ring animation when user is speaking
- Mic disabled while AI is responding
- No avatar for now, no XP system changes

## Architecture

### Frontend Voice Pipeline

```
Microphone → VAD (detects speech) → Deepgram WS (streaming STT)
                                          ↓
                            Transcript ready on speech end
                                          ↓
                            POST /dialog/sessions/:id/voice
                                          ↓
                            GPT response + ElevenLabs TTS
                                          ↓
                            Audio stream → Web Audio playback
```

### Backend Voice Endpoint

```
POST /dialog/sessions/{sessionId}/voice
Body: { transcript: string }
Response: audio/mpeg stream (ElevenLabs)
Side effect: saves messages to session, calls GPT
```

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
  "ElevenLabs": {
    "ApiKey": "REPLACE_WITH_ELEVENLABS_API_KEY",
    "BaseUrl": "https://voiceapi.csv666.ru",
    "VoiceId": "REPLACE_WITH_VOICE_ID",
    "Model": "eleven_flash_v2_5",
    "OutputFormat": "mp3_44100_128"
  },
  "Voice": {
    "Enabled": true,
    "VadSilenceMs": 600,
    "MaxRecordingSeconds": 60
  }
}
```

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
| POST | /dialog/sessions/{sessionId}/voice | `{transcript}` | `audio/mpeg` stream |
| GET | /dialog/voice/config | — | `{enabled, vadSilenceMs}` |

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
2. **Short VAD silence**: 600ms end-of-speech detection
3. **Streaming TTS**: ElevenLabs starts audio before full response generated
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
