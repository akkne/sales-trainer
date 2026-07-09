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
