# Localization

## Current state
The **user-facing frontend is in Russian**. This covers everything a regular user
sees: auth (login/register/onboarding/verify-email), the main app shell and
navigation, the learning path (skill tree, skills, topics, lessons, exercises,
theory, guidebook, reference), dialog practice + voice, the social area (friends,
chat, discussions, team/cohort progress), profile, settings, notifications, and the
companies module.

The **platform admin panel** (`app/(admin)/**`, `features/admin/**`) is intentionally
left in **English** — it is only seen by internal staff.

The **organization panel** (`app/(org)/**`, `features/org-*/**`, block 40.20) is
**Russian**, and in the **«вы»-form** rather than the app's «ты». It is not an admin
panel in the sense of the rule above: the customer's РОП opens it every week, so it is
a product surface. The «вы» is deliberate — the app coaches one person, this panel is
one company talking to another. Backend values (`not_started`, `score_dispute`,
`failed_threshold`, …) are translated **once per feature** in
`features/<feature>/constants/`, never as a literal in JSX; the fixed dictionary is
[TENANCY/ADMIN_UI_DESIGN.md §1.4](TENANCY/ADMIN_UI_DESIGN.md).

## Approach
- **No i18n library.** Strings are translated **in place** in the components
  (direct replacement of English literals with Russian). The app is single-language.
- Only **user-visible display text** was translated: JSX text, visible props
  (`label`/`title`/`placeholder`/`aria-label`/`alt`/button/heading/tooltip), toasts,
  and user-facing error/empty-state messages.
- **Not translated** (kept English on purpose): code identifiers, object keys,
  `className`/`data-*`, icon names, routes/API paths, query keys, enum/union string
  values compared in logic or sent to the backend, `console`/logger diagnostics,
  code comments, and test IDs.
- **Kept as-is by glossary:** brand name `Sellevate`, `XP`, `Email`, persona role
  labels `SDR`/`Account Executive`/`Account Manager`.

## If you add new UI
Write user-facing copy directly in Russian (informal ты-форма). Keep backend-facing
values and logic strings in English. If a rendered string has a corresponding test
assertion, update the test to the Russian text.

## Reverting to English / going multi-language later
Because translation is in-place, restoring English or supporting multiple locales
would require introducing a proper i18n layer (e.g. `next-intl`) and extracting
strings into dictionaries. That was explicitly out of scope for this pass.
