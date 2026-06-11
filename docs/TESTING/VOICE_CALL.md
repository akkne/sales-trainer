# Testing — Telephone Call Mode (Phase 36)

Route: `/dialog/[bundleId]/[modeId]/voice`. Requires a voice-enabled mode and
Chrome/Edge desktop (Web Speech API) or configured Deepgram.

## Connect

- [ ] Mode card on `/dialog/[bundleId]` shows «Позвонить» next to «Чат»
- [ ] «Позвонить» → status pill «Соединение...», ringback tone loops (1s beep / 4s pause)
- [ ] First AI reply: tone stops, status «На связи», call timer starts (mm:ss mono)
- [ ] Mobile: short vibration on connect
- [ ] Header shows `X/Y МИН СЕГОДНЯ` when daily limit configured

## Conversation

- [ ] Speaking → interim subtitle (italic, dashed border) updates live
- [ ] Pause (~silence timeout) → phrase commits to a user bubble, AI starts replying
      (commits from interim text — no extra wait for browser finalization;
      unit tests: `__tests__/speechEndpointer.test.ts`)
- [ ] AI reply streams into a single assistant bubble chunk-by-chunk; audio plays sentence-by-sentence
- [ ] Subtitles auto-scroll to the newest line

## Barge-in

- [ ] Speak while AI audio is playing → playback stops immediately
- [ ] The cut-off AI bubble fades (60%), dashed border, label «· прервано»
- [ ] Your new phrase is recognized and the dialog continues cleanly (no double replies)

## Hangup & feedback

- [ ] «Положить трубку» → triple busy beep, status «Звонок завершён», «Готовим разбор...»
- [ ] Feedback modal opens with score/XP; «Закрыть разбор» resets to idle
- [ ] AI-initiated end (endCall=true) triggers the same completion flow
- [ ] Empty call (no phrases) completes without a feedback modal
- [ ] Leaving the page mid-call: session completed in background, no stuck tones

## Limits

- [ ] With `Voice:DailyLimitMinutes` exceeded → call refused with toast/error (429 `{period, usedSeconds, limitSeconds}`)
- [ ] `/profile` quota bars: olive < 80%, warn ≥ 80%, red + «Лимит исчерпан» when over
- [ ] `/admin/voice/usage`: user rows sorted by monthly spend; over-limit values red; 403 for non-admin

## Fallback / errors

- [ ] Unsupported browser → «Голосовой режим недоступен» card with «Назад»
- [ ] Mic permission denied → error toast, call returns to idle
- [ ] Network drop mid-stream → error shown, can retry «Позвонить ещё раз»
