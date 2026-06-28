# Frontend — English UI & Toast Notifications

Implemented 2026-06-28. Two related frontend changes shipped together.

## 1. Product language switched to English

The REDESIGN_V2 direction made English the product's primary language, but the
frontend still shipped hardcoded Russian strings. All user-facing Cyrillic text
and Russian code comments across `src/frontend/` were translated to English:

- **Scope:** ~96 files — `app/`, `features/`, `shared/`, and `__tests__/`.
- **Approach:** straight inline replacement. There is **no i18n framework**
  (no next-intl / i18next) — the app is English-only. Strings remain inline,
  matching how the redesign mocks were authored.
- `<html lang="ru">` → `<html lang="en">` (`app/layout.tsx`).
- All `toLocaleString`/`toLocale*String` calls moved from `ru`/`ru-RU` to
  `en`/`en-GB` (24-hour time, short date) for consistency with the English UI.
- Plural/time-ago helpers (`features/discuss/lib/format.ts`,
  `friend-activity-feed`, session/league countdowns, `pluralizeRu`) were rewritten
  with English singular/plural logic. `pluralizeRu` keeps its old signature for
  call-site compatibility but now returns English forms.
- A glossary kept terminology consistent (Path / Practice / Guidebook / Friends /
  Profile / Discussions; Streak; Skill tree; Counterpart/Prospect; Breakdown;
  Feedback; persona "Skeptical Sam"; etc.).
- One intentional exception: `app/(main)/dialog/page.tsx` still matches the
  substring `"голос"` inside `sessionKind()` — an internal heuristic against
  backend `modeId` values, not user-facing text.

### Fonts & spacing
Verified against `docs/REDESIGN_V2/DESIGN_SPEC.md` — **already compliant**, no
changes needed: Hanken Grotesk (400–800) loaded in `app/layout.tsx`, with the
radius (`--r-*`), spacing (`--s-*`, 4px base), and type-scale tokens in
`app/globals.css` matching the spec.

## 2. Toast / snackbar system (new)

Previously there was **no transient notification system** — chat errors used raw
`window.alert()`, and other errors were inline-only. Added a lightweight in-house
toast system (no new npm dependency — built on Zustand + Framer Motion, both
already deps).

- `features/notifications/store/toast-store.ts` — Zustand store + `toast` helper.
  - API: `toast.success(msg)`, `toast.error(msg)`, `toast.info(msg)`,
    `toast.push(msg, variant?)`. Also `useToastStore()` for `push`/`dismiss`.
  - Auto-dismiss: success/info 4s, error 6s. Manual dismiss + stacking supported.
- `features/notifications/components/toaster.tsx` — `<Toaster />` portal
  (`document.body`, high z-index, `pointer-events` pass-through), styled to the
  design system (card radius, surface/line/shadow tokens, `--success`/`--bad`/
  `--primary` accent bars), animated with Framer Motion. Accessible:
  `role="alert"`/`aria-live="assertive"` for errors, `role="status"`/`polite`
  otherwise.
- Mounted once in `app/providers.tsx` so it covers every route (auth + main).
- `features/friends/hooks/use-chat.ts` — the two `window.alert()` calls replaced
  with `toast.error(...)` (English messages).

### Notification center fixes
- `features/notifications/` translated to English; `aria-live="polite"` added to
  the panel; mobile panel `rounded-none` → standard card radius token; unread
  badge moved from `bg-bad` (danger red) to `bg-primary` (violet = attention);
  hardcoded `text-amber-500`/`text-white` replaced with palette tokens.

## Verification
- `rg '[А-Яа-яЁё]'` over `app/ features/ shared/ __tests__/`: zero user-facing
  Cyrillic (only the internal `modeId` heuristic noted above).
- `npx tsc --noEmit`: source clean (only a stale `.next/dev/types` cache error for
  a removed route, regenerated on next build).
- `npx vitest run`: 78 tests pass (13 files); test assertions updated to the new
  English strings.
