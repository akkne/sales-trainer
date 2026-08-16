# Frontend Testing

## Tooling

| Tool | Purpose |
|---|---|
| Vitest 3 | Test runner |
| React Testing Library 16 | Component rendering + queries |
| @testing-library/jest-dom | Custom DOM matchers |
| jsdom | Browser environment emulation |

## Setup

Vitest is configured in `src/frontend/vitest.config.mts`.
Setup file: `src/frontend/vitest.setup.ts` (imports jest-dom matchers).

## How to run

```bash
# From src/frontend/
npm test          # one-off run
npm run test:watch  # watch mode
```

## Test location

All tests live in `src/frontend/__tests__/`.

## Coverage

| Test file | What it covers |
|---|---|
| `countdown.test.ts` | Pure logic: `computeCountdown()` — days/hours/minutes formatting, edge cases |
| `MultipleChoiceExercise.test.tsx` | Render, option selection, disabled state, onSubmit call, skip button presence/click |
| `LessonPath.test.tsx` | Node rendering, tap-to-open popover, `/session/[lessonId]` link, click-outside, only-one-open invariant |
| `CompaniesFormat.test.ts` | `pluralizeRu`/`pluralizeCompanies`/`companiesCountLabel`/`formatDateRu`/`relativeTimeRu` (Phase 39.5) |
| `useCompanies.test.tsx` | `useCompanies` list+client filter, `useCreateCompany`/`useDeleteCompany` invalidation (Phase 39.5) |
| `CompaniesPage.test.tsx` | `/companies` list page: rows, empty/loading/error states, search, create-modal flow (Phase 39.5) |
| `CompaniesTimeline.test.ts` | `mergeTimeline`/`filterTimeline` — chronological merge of practice calls + logs, segmented filter (Phase 39.6) |
| `CallLogForm.test.tsx` | Add/edit real-call log form: required-field validation, trimmed submit payload, edit pre-fill (Phase 39.6) |
| `useCompanyLogs.test.tsx` | `useCompanyLogs`/`useAddCallLog`/`useUpdateCallLog`/`useDeleteCallLog` — endpoints + cache invalidation (Phase 39.6) |
| `usePracticeCalls.test.tsx` | `useCompanyPracticeCalls`/`useRecentGoals` — endpoints, disabled-when-no-id (Phase 39.6) |
| `CompanyPage.test.tsx` | `/companies/[id]` page: loading/404/error states, description, pre-call CTA handoff, delete-company confirm (Phase 39.6) |
| `LoginPage.test.tsx` | Two-stage `/login` (Phase 40.8): stage 1 shows no password field and posts `/auth/login/start`; the password form appears only after the server answers `password`; an `oidc` answer shows the "SSO not connected" notice and **no** password field; "Изменить" returns to stage 1 |
| `roleGating.test.ts` | The 2026-08-16 role split's display gates (`isPlatformStaff`, `canManagePlatformUsers`): both Sellevate staff roles reach the platform admin panel, only `SuperAdmin` may add/remove users, no organization role ever reaches the platform panel, and the retired `OrgAdmin` name is gone from both role vocabularies |

## What NOT to test (yet)

- Full session page state machine (requires API mock setup for `useExercisesForLesson`)
- League page (countdown hook wired to component — test the pure function instead)
- Admin pages (low value, rapidly changing)

## Adding tests

Follow the pattern in existing test files. Mock Next.js `Link` with a plain `<a>`:

```ts
vi.mock("next/link", () => ({
    default: ({ href, children }: { href: string; children: React.ReactNode }) => (
        <a href={href}>{children}</a>
    ),
}));
```

Mock hooks with `vi.mock("@/lib/hooks/useXxx", () => ({ useXxx: () => ({...}) }))`.

## sessionStats — Post-Session Statistics

**File:** `__tests__/sessionStats.test.ts`

Tests the `formatSessionDuration(totalSeconds)` pure utility used in the session completion screen.

| Case | Input | Expected |
|---|---|---|
| Under a minute | 45 | "45 сек" |
| Zero duration | 0 | "0 сек" |
| Exactly one minute | 60 | "1 мин 0 сек" |
| 90 seconds | 90 | "1 мин 30 сек" |
| 185 seconds | 185 | "3 мин 5 сек" |
| 10 minutes | 600 | "10 мин 0 сек" |

## AiDialogueExercise — Cold-call dialog (text/voice)

**File:** `__tests__/AiDialogueExercise.test.tsx`

Covers the reworked cold-call exercise: user-first flow and the text/voice mode choice.

| Case | Expectation |
|---|---|
| Mode choice on mount | Renders "Текст" / "Голос"; no chat request fired (user speaks first) |
| Text mode | Selecting "Текст" reveals the reply input and "Напишите первую реплику" hint |
| First message | Posts the user's opening line to `/exercises/:id/chat` |
| Voice unavailable | "Голос" button disabled when `useExerciseVoice().isVoiceAvailable` is false |

The voice pipeline (`useExerciseVoice`) is mocked — it reuses the live-call STT/VAD/TTS
services (`features/voice/services/*`) but streams from `/exercises/:id/voice/stream`.
Manual voice checks follow the live-call checklist in [VOICE_CALL.md](VOICE_CALL.md).

## FreeTextExercise — Voice dictation into the answer field

**File:** `__tests__/FreeTextExercise.test.tsx`

Covers the "Голос" button on free-text (свободный ответ) exercises. Previously the
button was a dead stub with no `onClick` — voice input never worked. It now drives
`useSpeechDictation` (plain STT that appends finalized speech into the textarea, with
no AI streaming, unlike `useExerciseVoice`).

| Case | Expectation |
|---|---|
| Button rendered | "Голос" shown when `useSpeechDictation().isAvailable` is true |
| Button wired | Clicking "Голос" calls `dictation.toggle()` |
| Dictation appends | Final transcript fragments are appended to the answer field (space-separated) |
| Voice unavailable | "Голос" button hidden when dictation is unavailable |

`useSpeechDictation` is mocked in the test. It gates on `voiceConfig.enabled &&
isWebSpeechSupported()`, and stops automatically once the answer is submitted.

## Enter key — primary action shortcut in the session flow

**File:** `__tests__/EnterKeyActions.test.tsx`

Enter presses the primary action of the current session screen. One shared hook —
`features/exercise/hooks/use-enter-action.ts` (`useEnterAction`) — is wired into
`ExerciseActionFooter` ("Проверить"/"Отправить"), `ExerciseResultBanner` ("Далее"),
`TheoryLessonPlayer` ("Далее"/"Завершить"), and the session gate/completion screens
("Начать работу над ошибками", "Вернуться к пути"), so every exercise gets it for free.
Rewrite and free-text additionally submit on plain Enter typed inside their answer
textarea (Shift+Enter inserts a newline — same convention as the AI dialogue composer).

| Case | Expectation |
|---|---|
| Footer submit | Enter calls `onSubmit` when `canSubmit && !isSubmitting` |
| Footer gated | Enter is a no-op when submission is disabled or in flight |
| Key repeat | Held-Enter repeats are ignored (one press = one action, can't blast through screens) |
| Textarea guard | Plain Enter inside a textarea is ignored by the global hook; Ctrl/Cmd+Enter submits |
| Focused button | Enter on a focused button is left to the browser's native click (no double-fire) |
| Result banner | Enter calls `onContinue` ("Далее") |
| Theory next/finish | Enter advances the card; on the last card calls `onComplete`; inert while completing |
| Theory arrows | ←/→ page between cards but never trigger completion (safe to hold) |
