# Testing — Telephone Call Mode (Phase 36)

Route: `/dialog/[bundleId]/[modeId]/voice`. Requires a voice-enabled mode and
Chrome/Edge desktop (Web Speech API) or configured Deepgram.

## Connect

- [ ] Mode card on `/dialog/[bundleId]` shows «Позвонить» next to «Чат»
- [ ] «Позвонить» → status pill «Соединение...», **no sound at all** (calls are silent — no ringback, no busy tone)
- [ ] Session ready: status «На связи», call timer starts (mm:ss mono)
- [ ] Voice practice of a custom scenario (`?session=…`): the timer starts too — the call does not stay on «Соединение…»
- [ ] Mobile: short vibration on connect
- [ ] Header shows `X/Y МИН СЕГОДНЯ` when daily limit configured

## Conversation

- [ ] Speaking → interim subtitle (italic, dashed border) updates live
- [ ] Pause (~silence timeout) → phrase commits to a user bubble, AI starts replying
      (commits from interim text — no extra wait for browser finalization;
      unit tests: `__tests__/speechEndpointer.test.ts`)
- [ ] AI reply streams into a single assistant bubble chunk-by-chunk; audio plays sentence-by-sentence
- [ ] Subtitles auto-scroll to the newest line
- [ ] **Mobile mic continuity:** after the AI's reply finishes, the mic re-activates and
      keeps working for the 2nd, 3rd … user turn (regression: it used to die after the
      first reply because the recognition instance was reused). `WebSpeechClient` now
      spins up a fresh `SpeechRecognition` on every start/resume/auto-end.
      Unit tests: `features/voice/services/__tests__/web-speech-client.test.ts`

## Barge-in

- [ ] Speak while AI audio is playing → playback stops immediately
- [ ] The cut-off AI bubble fades (60%), dashed border, label «· прервано»
- [ ] Your new phrase is recognized and the dialog continues cleanly (no double replies)

## Hangup & feedback

- [ ] «Положить трубку» → status «Звонок завершён», «Готовим разбор...» (silently)
- [ ] Feedback modal opens with the score and the analysis; «Закрыть разбор» resets to idle
- [ ] **No XP anywhere for a call**: no «+N XP получено» in the modal, no `+N` badge in the session
      history — even though the backend still returns `xpEarned`
- [ ] AI-initiated end (endCall=true) triggers the same completion flow and releases the mic
- [ ] Empty call (no phrases) completes without a feedback modal, hint reads «Разбирать нечего…» — never a stuck «Готовим разбор…»
- [ ] **Second call**: «Позвонить снова» connects and can be hung up again (a new session each time)
- [ ] Custom scenario (`?session=…`): after the call the CTA is «К сценариям», not «Позвонить снова»;
      re-opening the page on a played-out session refuses to dial with «Этот сценарий уже отыгран»
- [ ] A turn the backend refuses (409) or that returns nothing shows an error — the persona is never
      silently mute
- [ ] Analysis failure/timeout → error badge + «Повторить разбор»; the retry produces the feedback
- [ ] Leaving the page mid-call: session completed in background

## Limits

- [ ] With `Voice:DailyLimitMinutes` exceeded → call refused with toast/error (429 `{period, usedSeconds, limitSeconds}`)
- [ ] `/profile` quota bars: olive < 80%, warn ≥ 80%, red + «Лимит исчерпан» when over
- [ ] `/admin/voice/usage`: user rows sorted by monthly spend; over-limit values red; 403 for non-admin

## Fallback / errors

- [ ] Unsupported browser → «Голосовой режим недоступен» card with «Назад»
- [ ] Mic permission denied → error toast, call returns to idle
- [ ] Network drop mid-stream → error shown, can retry «Позвонить ещё раз»
