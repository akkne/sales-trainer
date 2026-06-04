# Testing — Phase 37 Night Polish Pass

Manual checklist for the 2026-06-05 overnight polish (see ROADMAP Phase 37).

## 37.1–37.3, 37.7 — April palette migration

- [ ] `/dialog/[bundleId]/[modeId]` chat: header, sidebar, messages, input — no broken/transparent
      surfaces in **dark theme** (previously MD3 classes rendered as no-ops)
- [ ] Session history sidebar: active session highlighted indigo, delete confirm modal styled
- [ ] `/login`, `/register`: inputs on `--surface`, focus ring indigo, primary button ink/bg
- [ ] Landing `/`: no Duolingo green, CTA = ink button, feature cards on `--surface`
- [ ] `/skill/[id]` and `/skill/[id]/map`: progress bars olive/indigo, no `#58CC02`
- [ ] Admin panel (all pages incl. exercise editors): readable in dark theme, indigo accents
- [ ] Notification bell + panel + cards: April palette, unread tint
- [ ] grep sweep is clean:
      `grep -rnE "on-surface|surface-container|outline-variant|tonal-transition|font-headline|#58CC02" src/frontend/app src/frontend/components`

## 37.4 — Voice call polish

- [ ] «Позвонить» → ringback tone (425 Hz, 1s/4s loop) plays while «Соединение...»
- [ ] First AI reply → tone stops, on mobile a short vibration fires
- [ ] «Положить трубку» / AI ends call → triple busy beep
- [ ] Interrupt AI mid-reply by speaking → its subtitle fades to 60% with «· прервано» label,
      dashed border; new user phrase recognized
- [ ] Leaving the page mid-call stops all tones

## 37.5 — Voice usage surfacing

- [ ] `/profile`: «Голосовые звонки» card visible when limits configured
      (`Voice:DailyLimitMinutes` / `MonthlyLimitMinutes` > 0)
- [ ] Bars: olive < 80%, warn ≥ 80%, red + «Лимит исчерпан» when over
- [ ] `/admin/voice/usage` (Admin/SuperAdmin): table sorted by monthly spend,
      over-limit numbers red, «Обновить» refetches
- [ ] `GET /admin/voice/usage` returns 403 for plain users

## 37.6 — Skeletons & states

- [ ] `/dialog` while loading: hero + 4 card skeletons (no full-screen spinner)
- [ ] `/guidebook` while loading: 6 card skeletons
- [ ] `/league` while loading: header + 8 row skeletons; API error → ErrorState with retry;
      no league → «Лига ещё не сформирована» empty state
- [ ] `/tree` API error → ErrorState with «Повторить» that refetches

## Automated

- Frontend: `npx vitest run` — 47 tests (incl. `callSounds.test.ts`, updated
  `LessonPath.test.tsx`, new `ChooseOptionExercise.test.tsx`)
- Backend: `dotnet test` — 122 tests
- `npx tsc --noEmit` and `npx next build` pass
