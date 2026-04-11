# Voice Dialogs - Bug Report

**Date**: 2026-04-12  
**Feature**: Voice Dialogs (Voice Practice)  
**Status**: QA Analysis Complete

---

## Critical Issues

### BUG-001: Voice recognition does not resume after AI response

**Severity**: Critical  
**Files**: `src/frontend/lib/hooks/useVoice.ts:193-194`, `src/frontend/lib/voice/webSpeechClient.ts:193-200`

**Description**:  
After the AI finishes playing audio, `resume()` is called on `WebSpeechClient`, but it checks `this.state === "idle"` to decide if resuming is allowed. However, after `pause()` is called (line 186-191), `shouldRestart` is set to `false`. When `resume()` tries to start again, it sets `shouldRestart = true` but the `recognition` object may be `null` because `pause()` only calls `stop()` without nullifying.

**Problem**: In `resume()` method:
```typescript
resume(): void {
    if (this.recognition && this.state === "idle") {  // state is "idle" after pause
        this.shouldRestart = true;
        try {
            this.recognition.start();  // May throw "recognition has already started"
        } catch {
            // Ignored silently
        }
    }
}
```

The recognition may have been restarted automatically by `onend` handler before `resume()` is called, causing `start()` to throw.

**Expected**: After AI response plays, microphone should automatically start listening again.  
**Actual**: Microphone may not resume, requiring user to stop and restart voice mode.

---

### BUG-002: Voice mode continues recording on completed sessions

**Severity**: Critical  
**Files**: `src/frontend/app/dialog/[bundleId]/[modeId]/page.tsx:569-577`

**Description**:  
When a session is ended (manually or via stop signal), the voice mode UI (`VoiceMicButton`) is still rendered and functional. There's no check for `isEnded` or session status in voice mode rendering.

**Evidence from logs**:
```
System.InvalidOperationException: Session 69d369d6e910abaa70df4841 is not active
```

The user was able to click the microphone button on a completed session, triggering backend errors.

**Reproduction**:
1. Start voice dialog
2. Complete the session (AI sends stop signal or user ends manually)
3. Click microphone button again
4. Observe 400 error from backend

**Fix needed**: Add `isEnded` check to voice mode rendering:
```tsx
{isVoiceMode && !isEnded && !feedback && (
    <VoiceMicButton ... />
)}
```

---

### BUG-003: No new session creation in voice mode

**Severity**: Critical  
**Files**: `src/frontend/app/dialog/[bundleId]/[modeId]/page.tsx:140-160`, `src/frontend/lib/hooks/useVoice.ts:140-142`

**Description**:  
In text mode, `handleSendMessage` creates a new session if `sessionId` is null. However, in voice mode, `startVoice` just returns early if `sessionId` is null:

```typescript
const startVoice = useCallback(async () => {
    if (!isVoiceAvailable || !sessionId) {
        return;  // Silent return - no new session created
    }
    ...
}, [isVoiceAvailable, sessionId, ...]);
```

**Expected**: Clicking microphone button without active session should create a new session.  
**Actual**: Nothing happens, user is stuck without feedback.

---

## High Priority Issues

### BUG-004: Web Speech API not supported in Firefox/Safari Private Mode

**Severity**: High  
**Files**: `src/frontend/lib/voice/webSpeechClient.ts:65-68`

**Description**:  
The `isWebSpeechSupported()` function only checks for API existence:
```typescript
export function isWebSpeechSupported(): boolean {
    return typeof window !== "undefined" &&
           !!(window.SpeechRecognition || window.webkitSpeechRecognition);
}
```

However:
- Firefox does not support Web Speech API speech recognition
- Safari Private Browsing mode blocks getUserMedia
- Some Chrome-based browsers may have it disabled by policy

**No UI indication** is shown when speech recognition fails on these browsers.

**Recommendation**: Add browser compatibility message and fallback to Deepgram (already implemented but not used).

---

### BUG-005: Deepgram client is prepared but never used

**Severity**: High  
**Files**: `src/frontend/lib/voice/deepgramClient.ts`, `src/frontend/lib/voice/vadManager.ts`

**Description**:  
The codebase has complete implementations for:
- `DeepgramClient` - WebSocket-based real-time transcription
- `VadManager` - Voice Activity Detection using @ricky0123/vad-web

But `useVoice.ts` uses only `WebSpeechClient` which has browser compatibility issues. The Deepgram client would work across all browsers.

**Documentation mismatch**: `docs/VOICE_ROLEPLAY.md` describes Deepgram as the primary STT solution, but implementation uses Web Speech API.

---

### BUG-006: DailyLimitMinutes and MonthlyLimitMinutes not enforced

**Severity**: High  
**Files**: `src/backend/api/Features/Voice/VoiceConfigController.cs:28-29`, `src/backend/api/Features/Voice/VoiceDialogService.cs`

**Description**:  
Configuration includes:
```json
"DailyLimitMinutes": 30,
"MonthlyLimitMinutes": 300
```

These values are returned in `/dialog/voice/config` response but **never checked or enforced** in:
- `VoiceDialogService.ProcessVoiceMessageAsync()` - no usage tracking
- Frontend `useVoice.ts` - no limit checking

**Expected**: Users should have voice usage limits enforced.  
**Actual**: Unlimited voice usage possible.

---

### BUG-007: Race condition in silence timeout and speech processing

**Severity**: High  
**Files**: `src/frontend/lib/hooks/useVoice.ts:152-170`

**Description**:  
The silence timeout logic has a race condition:

```typescript
onResult: (transcript: string, isFinal: boolean) => {
    if (silenceTimeoutRef.current) {
        clearTimeout(silenceTimeoutRef.current);
        silenceTimeoutRef.current = null;
    }

    if (isFinal) {
        transcriptBufferRef.current += ... + transcript;
        
        silenceTimeoutRef.current = setTimeout(() => {
            const finalTranscript = transcriptBufferRef.current.trim();
            if (finalTranscript) {
                processSpeech(finalTranscript);  // May still be processing previous
            }
        }, vadSilenceMs);
    }
}
```

If `processSpeech` is still running (network delay), a new `processSpeech` call can be triggered, causing:
- Duplicate messages
- Inconsistent state
- Backend errors

---

## Medium Priority Issues

### BUG-008: AudioPlayer doesn't handle codec errors

**Severity**: Medium  
**Files**: `src/frontend/lib/voice/audioPlayer.ts:79`

**Description**:  
```typescript
const audioBuffer = await audioContext.decodeAudioData(arrayBuffer);
```

If the TTS service returns corrupted audio or unsupported codec, `decodeAudioData` throws. The error is caught but user gets generic "Playback failed" without option to retry with text fallback.

---

### BUG-009: Memory leak in AudioPlayer

**Severity**: Medium  
**Files**: `src/frontend/lib/voice/audioPlayer.ts:19-24`

**Description**:  
`AudioContext` is created but only closed in `destroy()`:
```typescript
private getAudioContext(): AudioContext {
    if (!this.audioContext) {
        this.audioContext = new AudioContext();
    }
    return this.audioContext;
}
```

If multiple voice sessions happen without `destroy()` being called (e.g., navigating between pages), AudioContexts accumulate. Browsers limit concurrent AudioContexts.

---

### BUG-010: Stale state reference in onSpeechEnd callback

**Severity**: Medium  
**Files**: `src/frontend/lib/hooks/useVoice.ts:189-192`

**Description**:  
```typescript
onSpeechEnd: () => {
    if (state !== "processing" && state !== "playing") {
        setState("listening");
    }
}
```

The `state` variable is captured in closure at callback creation time. It won't reflect current state, potentially causing incorrect state transitions.

**Fix**: Use `setState` callback form or `useRef` for state checking.

---

### BUG-011: Error messages in Russian hard-coded in webSpeechClient

**Severity**: Medium  
**Files**: `src/frontend/lib/voice/webSpeechClient.ts:83, 93, 100, 152, 172`

**Description**:  
Error messages are hard-coded in Russian:
- "Web Speech API не поддерживается в этом браузере"
- "Нет доступа к микрофону"
- "Ошибка распознавания: ..."

No i18n support. App should use consistent localization.

---

### BUG-012: VoicerTts service polling is inefficient

**Severity**: Medium  
**Files**: `src/backend/api/Features/Voice/VoicerTtsService.cs:148-204`

**Description**:  
TTS synthesis uses polling:
```csharp
for (var attempt = 0; attempt < maxAttempts; attempt++) {
    await Task.Delay(pollIntervalMs, ct);  // 500ms delay
    // Check status
}
```

With 500ms interval and max 120 attempts, this can block for up to 60 seconds. A single slow TTS task blocks the entire request.

**Better approach**: Use WebSocket or long-polling with server-sent events.

---

## Low Priority Issues

### BUG-013: VoiceMicButton shows mic icon for both idle and active states

**Severity**: Low  
**Files**: `src/frontend/components/dialog/VoiceMicButton.tsx:79-80`

**Description**:  
```tsx
<Icon
    name={isActive ? "mic" : "mic"}  // Same icon for both
    ...
/>
```

The icon name is "mic" in both states - should be different for better UX (e.g., "mic_off" when idle).

---

### BUG-014: Voice error toast auto-dismisses too quickly

**Severity**: Low  
**Files**: `src/frontend/app/dialog/[bundleId]/[modeId]/page.tsx:316-318`

**Description**:  
```typescript
const handleVoiceError = useCallback((error: Error) => {
    setVoiceError(error.message);
    setTimeout(() => setVoiceError(null), 5000);
}, []);
```

5 seconds may not be enough for user to read and understand microphone permission errors.

---

### BUG-015: No MaxRecordingSeconds enforcement

**Severity**: Low  
**Files**: `src/backend/api/Features/Voice/VoiceConfigController.cs:27`, `src/frontend/lib/hooks/useVoice.ts`

**Description**:  
`MaxRecordingSeconds: 60` is configured and returned but:
- Frontend doesn't track recording duration
- No timeout to force stop recording
- Backend doesn't validate transcript duration

---

## Architectural Issues

### ARCH-001: Documentation/Implementation mismatch

**Files**: `docs/VOICE_ROLEPLAY.md`

The documentation describes:
- Deepgram Nova-3 WebSocket for STT
- ElevenLabs Flash v2.5 for TTS
- Target latency ≤ 700ms

Actual implementation:
- Web Speech API for STT (browser-native, not Deepgram)
- VoicerTts service (custom, not ElevenLabs direct)
- No latency measurement or optimization

---

### ARCH-002: VAD feature implemented but unused

**Files**: `src/frontend/lib/voice/vadManager.ts`

The `VadManager` class with @ricky0123/vad-web is fully implemented but not used. The Web Speech API has its own (less configurable) speech detection. Using `VadManager` would provide:
- More precise speech end detection
- Configurable thresholds
- Works in all browsers (not just Chrome)

---

### ARCH-003: Inconsistent voice mode detection

**Files**: `src/frontend/app/dialog/[bundleId]/[modeId]/page.tsx:39, 329, 335`

Voice mode is determined by multiple factors:
```typescript
const chatMode = searchParams.get("mode") || "text";  // URL param

modeVoiceEnabled: chatMode === "voice" && (currentMode?.voiceEnabled ?? false),  // Hook param

const isVoiceMode = chatMode === "voice" && currentMode?.voiceEnabled && isVoiceAvailable;  // Render decision
```

These could diverge if URL is manually edited or backend config changes.

---

## Recommendations

1. **Critical**: Fix session state checking for voice mode (BUG-002, BUG-003)
2. **Critical**: Fix speech recognition resume logic (BUG-001)
3. **High**: Implement Deepgram fallback for non-Chrome browsers (BUG-004, BUG-005)
4. **High**: Add rate limiting/usage tracking (BUG-006)
5. **Medium**: Use useRef for state in callbacks to avoid stale closures (BUG-010)
6. **Low**: Update documentation to match implementation (ARCH-001)

---

## Test Cases to Add

See `docs/TESTING/VOICE_ROLEPLAY.md` for test plan. Additional cases needed:

1. Voice mode on completed session - should show text input only
2. Voice mode session creation - clicking mic without session
3. Speech recognition resume after playback
4. Browser compatibility (Firefox, Safari, Edge)
5. Usage limits enforcement
6. Concurrent speech processing prevention
7. Long recording timeout
8. Audio codec error handling
