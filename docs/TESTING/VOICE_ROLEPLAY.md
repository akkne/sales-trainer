# Voice Roleplay Testing

## Prerequisites

1. Configure keys in `appsettings.json`:
   - `Deepgram:ApiKey` — Deepgram API key
   - `ElevenLabs:ApiKey` — ElevenLabs API key
   - `ElevenLabs:VoiceId` — default voice ID

2. Enable voice mode in admin:
   - Go to `/admin/dialog`
   - Select a bundle → mode
   - Check "Voice Enabled"
   - Optionally set custom Voice ID

## Manual Test Checklist

### Configuration
- [ ] `/dialog/voice/config` returns `enabled: false` when keys missing
- [ ] `/dialog/voice/config` returns `enabled: true` when all configured
- [ ] Admin panel shows voice toggle per mode
- [ ] Voice button hidden when mode has `voiceEnabled: false`

### Voice Pipeline
- [ ] Click mic button → browser asks for microphone permission
- [ ] Mic button shows green ring when speaking
- [ ] Speech is transcribed while speaking (interim results)
- [ ] Final transcript shown when speech ends
- [ ] AI response audio plays automatically
- [ ] Mic disabled while AI is responding
- [ ] Mic re-enabled after playback ends

### States
- [ ] Idle → gray mic icon, "Нажмите для голоса"
- [ ] Listening → green pulsing, "Слушаю..."
- [ ] Speaking → green ping animation, "Говорите..."
- [ ] Processing → spinner, "Обработка..."
- [ ] Playing → speaker icon, "Отвечает..."
- [ ] Error → red text, recovers gracefully

### Session Integration
- [ ] Voice messages saved to MongoDB session
- [ ] Text and voice can be mixed in same session
- [ ] Stop signal triggers auto-complete
- [ ] Feedback modal shows after completion

### Error Handling
- [ ] Denied microphone → error toast, text input still works
- [ ] Deepgram connection lost → attempts reconnect
- [ ] ElevenLabs error → error shown, can retry
- [ ] Network timeout → error message, transcript preserved

### Admin Panel
- [ ] Create mode with `voiceEnabled: true`
- [ ] Update mode voice settings
- [ ] Voice column shows in modes table
- [ ] Voice ID field disabled when voice not enabled

## Integration Test Outline

```csharp
[Fact]
public async Task VoiceEndpoint_ReturnsAudio_WhenConfigured()
{
    // Arrange: create session with voice-enabled mode
    // Act: POST /dialog/sessions/{id}/voice with transcript
    // Assert: response is audio/mpeg, messages saved to session
}

[Fact]
public async Task VoiceEndpoint_Returns503_WhenNotConfigured()
{
    // Arrange: remove ElevenLabs config
    // Act: POST /dialog/sessions/{id}/voice
    // Assert: 503 Service Unavailable
}

[Fact]
public async Task VoiceConfig_ReturnsEnabled_WhenAllKeysSet()
{
    // Arrange: set all voice-related keys
    // Act: GET /dialog/voice/config
    // Assert: enabled: true, deepgram.configured: true
}
```

## Browser Compatibility

- [x] Chrome (recommended)
- [x] Edge
- [ ] Firefox (may require flags for AudioWorklet)
- [ ] Safari (limited Web Audio API support)

## Performance Notes

- VAD detection latency: ~100ms
- Deepgram interim results: ~200ms
- ElevenLabs TTS first byte: ~300ms
- Total expected latency: 600-800ms
