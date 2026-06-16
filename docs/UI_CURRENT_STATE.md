# UI Current State — Baseline for Redesign

> Purpose: an exhaustive, factual snapshot of the existing frontend (screens, layout, elements, visual system) for Claude Design to use as the starting point of a redesign. This document describes **what exists today** — it intentionally contains no redesign direction.

Last updated: 2026-06-07.

---

## 1. Product in one paragraph

SalesTrainer (brand name in UI: **Sellevate**) is a Duolingo-style sales-training web app. Users learn sales skills through a skill tree of lessons with 10+ exercise types, practice realistic conversations with AI in text chat and **real-time voice calls**, compete in weekly leagues, add friends and chat, browse a guidebook of sales techniques, and track streaks/XP/achievements. UI language is **Russian**. There is also an admin panel for content management.

---

## 2. Tech stack (what the redesign must work within)

| Layer | Tech |
|---|---|
| Framework | Next.js 16 (App Router), React 19, TypeScript |
| Styling | Tailwind CSS 4 + custom CSS design tokens in `src/frontend/app/globals.css` |
| Animation | Framer Motion (voice call), CSS keyframes (confetti, toasts, pulse) |
| Icons | Hand-coded SVG set in `src/frontend/shared/components/icon.tsx` (~40 icons, no external library) |
| Fonts | Geist Sans / Geist Mono (Inter / JetBrains Mono fallbacks) |
| State | Zustand (`auth-store`, `selected-skill-store`) + React Query for server state |
| Avatars | Procedural `GeoAvatar` generated from seed string (no photo uploads) |
| Theming | Light/dark via `html[data-theme="dark"]`; density via `data-density="compact"` — all tokens are CSS custom properties |

**Key implication for redesign:** the entire visual system lives in `globals.css` as CSS variables (`--bg`, `--indigo`, `--s-4`, `--r-md`, `--sh-1`, `--t-xl`…). Changing tokens re-skins the whole app; components reference tokens, not raw values. The voice call page is an exception — it uses inline styles.

---

## 3. Current visual system

### 3.1 Color palette (light mode)

Warm paper-like neutrals + muted earthy accents:

| Token | Value | Role |
|---|---|---|
| `--bg` | `#F4F2ED` | page background (warm off-white) |
| `--bg-2` | `#EAE7E0` | hover states, secondary bg |
| `--surface` | `#FFFFFF` | cards, panels |
| `--surface-2` | `#FBF9F5` | sidebars, elevated surfaces |
| `--line` / `--line-2` | `#E2DED5` / `#CDC8BD` | borders, dividers |
| `--ink` … `--ink-4` | `#1C1B18` → `#9A9689` | 4-step text hierarchy |
| `--indigo` | `oklch(0.50 0.18 270)` | primary accent: links, progress, primary actions |
| `--olive` | `oklch(0.58 0.10 140)` | success, active voice call |
| `--rust` | `oklch(0.62 0.14 42)` | streaks, highlights, AI speaking |
| `--clay` | `oklch(0.72 0.10 55)` | neutral accent, "processing" state |
| `--good` / `--bad` / `--warn` | green / red / amber oklch | semantic states |

Each accent has a `-soft` background variant and (some) a `-ink` text variant. Dark mode overrides everything (`--bg: #15140F`, `--surface: #1F1D18`, `--ink: #EFECE3`, darkened soft tones).

### 3.2 Scales

- **Spacing:** 4px base — `--s-1`(4) … `--s-10`(72).
- **Radius:** `--r-xs`(4) / `--r-sm`(8) / `--r-md`(12) / `--r-lg`(16) / `--r-xl`(22) / `--r-2xl`(28); pills use 999px.
- **Shadows:** `--sh-1` (subtle card), `--sh-2` (hover/buttons), `--sh-3` (modals), `--sh-inner` (pressed).
- **Type sizes:** 12 / 13 / 15 / 17 / 21 / 28 / 36 / 48 / 64px. Body = 15px.
- **Typography character:** headings with tight negative letter-spacing (-0.8…-1.5px); section labels in **uppercase mono with +1–2px letter-spacing** (e.g. "НАВЫКИ", "СТАТИСТИКА", "AI ДИАЛОГ · 4 МОДУЛЯ") — this mono-label pattern is used everywhere as a visual signature.
- **Animations:** pulse, ping, slide-up (toasts/modals), confetti, typing dots, spin, shimmer on achievements. Transitions 0.08–0.2s.

### 3.3 Recurring page pattern

Most main pages share a "hero header" pattern:
1. Mono uppercase label with counter ("СПРАВОЧНИК · 24 ТЕХНИКИ")
2. Big tight-tracked title, often ending with a period ("Коллекция.", "Друзья.", "Мастерство разговора.")
3. One-line subtitle in `--ink-3`
4. Right-aligned row of `StatTile` components (icon badge + big number + mono label)

---

## 4. Route map

### Public / auth (centered-card layout)
| Route | Screen |
|---|---|
| `/` | Landing |
| `/login`, `/register` | Auth forms |
| `/onboarding` | First-run setup |

### Main app (TopAppBar layout, auth-guarded)
| Route | Screen |
|---|---|
| `/tree` | Skill tree — main hub |
| `/skill/[id]`, `/skill/[id]/map` | Skill detail / progress map |
| `/session/[lessonId]` | Exercise session (own full-screen layout, no app bar) |
| `/dialog` | Dialog bundles list |
| `/dialog/[bundleId]` | Mode selection within a bundle |
| `/dialog/[bundleId]/[modeId]` | Text chat with AI |
| `/dialog/[bundleId]/[modeId]/voice` | **Voice call** (own full-screen layout) |
| `/guidebook` | Technique reference |
| `/reference/[id]` | Single technique page |
| `/league` | Weekly league |
| `/friends` (+ `/friends/[userId]`, `/friends/chat/[conversationId]`) | Social hub, profiles, DMs |
| `/profile` | Profile & settings |

### Admin (sidebar layout, role-guarded)
`/admin` + subpages: import (bundle), skills, topics, lessons, reference, techniques, dialog, prompts (AI), voice/usage, users.

Redirects: unauthenticated → `/login`; authenticated visiting `/` → `/tree`; non-admin on `/admin/*` → `/tree`.

---

## 5. Shared layout

### Page width
Wide, near-fullscreen layout: `.container` is fluid `width: 100%; max-width: 1840px; padding: 0 clamp(24px, 3.5vw, 72px)`; `.appbar-inner` matches. Page grids stretch with it (`.tree-grid-a` minmax columns, `.friends-grid` fluid left column, `.gb-grid` auto-fit ≥420px → 3 columns on wide screens). Reading-style pages cap narrower: skill/reference/friend-profile `max-w-4xl`, session exercise 860–900px, dialog chat thread 980px.

### TopAppBar (sticky, 60px, all main pages) — `features/layout/components/top-app-bar.tsx`
- Left: wordmark "SalesTrainer".
- Center (desktop): 6 nav items with icons — Tree, League, Guidebook, Dialog, Friends, Profile; active item gets `--bg-2` highlight.
- Right: streak pill (flame + count, rust-soft) → NotificationBell with unread badge → profile chip (GeoAvatar 28px + "Level N") → hamburger on mobile.
- Mobile: hamburger opens full-screen stacked nav overlay; there is also a fixed glass-blur BottomNav on mobile.

### Shared components — `shared/components/`
| Component | Notes |
|---|---|
| `Button` | variants: primary / accent / secondary / ghost / outline / destructive; sizes sm(30px)–xl(56px); loading spinner; active = translateY(1px) |
| `Card`, `Chip`, `Input`, `Progress`, `Skeleton`, `StatTile`, `ErrorState` | token-driven, consistent |
| `GeoAvatar` | procedural avatar from name seed; sizes 28–168px |
| `Icon` | custom SVG set: flame, bolt, trophy, heart, book, mic, phone, search, chevrons, etc. |
| `AchievementToast` | queue of slide-up toasts bottom-right with shine animation |
| `LessonPath` | vertical lesson chain for the tree |
| `ThemeToggle`, `Wordmark`, `GoogleLoginButton`, `NotificationBell` | |

### Standard page states
- Loading: centered spinner or `Skeleton` rows.
- Error: `ErrorState` (icon circle + title + message + retry).
- Empty: centered icon circle (72–80px) + title + hint + optional CTA.
- Modals: fixed overlay `rgba(0,0,0,0.45)` + backdrop blur, surface card `--r-2xl` `--sh-3`, max-height 82vh, close on ESC/outside-click.

---

## 6. Screen-by-screen inventory

### 6.1 Landing `/` — `app/page.tsx`
- Header: wordmark + login link.
- Hero: 🚀 emoji, title "Прокачай продажи за 5 минут в день" (accent phrase in rust), subtitle, single CTA — primary dark "Начать бесплатно" (→ register).
- 4 feature cards (emoji + title + description): real scenarios, AI grading, streaks/leagues, guidebook.

### 6.2 Login / Register `(auth)/login`, `(auth)/register`
- Centered card max-width 420px on `--surface`.
- Sparkle icon + "Sellevate" brand, email + password inputs (rounded-2xl, indigo focus ring), inline error text, dark submit button, "или" divider, Google OAuth button, cross-link to the other form.

### 6.3 Skill tree `/tree` — main hub
3-column desktop grid `300px | 1fr | 320px`, stacked on mobile.
- **Left — skills sidebar:** "НАВЫКИ" mono label; collapsible stage groups (chevron + colored dot + mono stage name + completion ratio); inside — skill rows (icon + title, highlight on selection).
- **Center — lesson path:** header band with "НАВЫК · X/Y УРОКОВ", skill title (32px), compact indigo progress bar with % and remaining count; below — scrollable area with a **dotted-grid background** (1px dots, 20px pitch) where `LessonPath` renders the vertical lesson chain; empty state if no lessons.
- **Right — stats sidebar:** "СТАТИСТИКА" label, `StatsWidget`: current streak (flame), total XP (bolt), weekly XP.

### 6.4 Exercise session `/session/[lessonId]` — full-screen, no app bar
- **Header:** close (X) left, full-width progress bar (current/total, indigo fill), 4 hearts right (lost ones faded gray).
- **Body:** one exercise at a time, centered max 820px. 10 exercise components: ChooseOption, FillBlank, Reorder, MatchPairs, Categorize, SpotMistake, Rewrite, AiDialogue, EvaluateCall, FreeText. Shared props contract (`content`, `onSubmit`, `onContinue`, `submittedResult`).
- **Result overlay:** correctness banner + expandable AI feedback + continue button.
- **Completion screen:** confetti (36 CSS particles), pulsing olive check circle (120px), "УРОК ЗАВЕРШЁН" mono label, "Отличная работа!" (48px), 2×2 stats grid (XP / accuracy / duration / hearts), "Вернуться к пути" CTA.

### 6.5 Dialog list `/dialog`
- Hero header: "AI ДИАЛОГ · N МОДУЛЕЙ", title "Мастерство разговора." (48px), stat tiles (completed dialogs, avg score).
- Bundle cards grid (auto-fill 320px): emoji icon 56px in indigo-soft rounded square, title, description, indigo arrow; hover lifts shadow + indigo border.
- Bottom **NPC mentor card**: dark `--ink` background, GeoAvatar, name "Skeptic Sergey", mono role "VP · ВОЗРАЖЕНИЯ", quote, "CHALLENGE" button with phone icon.

### 6.6 Mode selection `/dialog/[bundleId]`
- Back link "Назад к диалогам", bundle icon 72px, "ВЫБЕРИ РЕЖИМ" label, bundle title (36px), description.
- Mode cards (auto-fill 300px): title + description + two action buttons — **Chat** (surface-2, message icon) and **Voice** (olive, phone icon). Empty state if no modes.

### 6.7 Voice call `/dialog/[bundleId]/[modeId]/voice` ⭐ most complex screen
Full-height, own layout, **inline styles** (not token classes).

**Header bar** (`--surface-2`, bottom border):
- Left: back "К сценариям".
- Center: 8px status dot + mono uppercase status — "ОЖИДАНИЕ" / "ВЫЗОВ" / live `MM:SS` timer / "ЗАВЕРШЁН".
- Right: voice quota "MM/LL МИН СЕГОДНЯ" (mono 11px, turns red when exceeded).

**Center stage:**
- GeoAvatar **168px** with a 3px state-colored ring + optional ping/pulse animation. Ring color encodes the pipeline state:
  - idle → gray `--line` · dialing → indigo pulsing · listening → olive solid · user speaking → olive pulsing · processing → clay pulsing · AI speaking → rust pulsing · error → `--bad`.
- Bundle name (mono 12px) + mode title (32px) + mode description.
- **Live transcript** (max-width 560px, max-height 26vh, scrollable): user bubbles float right (surface-2, solid border), AI bubbles float left (surface); mono 10px role labels above ("Вы" / mode name); interim speech = dashed border + italic + ink-3; interrupted AI replies = dashed border, 0.6 opacity, clay "(прервано)" mark.
- **State pill**: colored dot + label ("Готов к звонку", "Соединение...", "На связи · слушаю вас", "Слышу вас", "Думаю над ответом...", "Собеседник говорит", "Помехи на линии", "Звонок завершён").
- Contextual hint below (13px ink-3): e.g. "Говорите свободно — я отвечу, как только сделаете паузу" / "Прерывайте, когда захотите ответить".
- Error alert (bad-soft, auto-dismiss 5s); "Готовим разбор..." spinner while completing.

**Footer:** single large pill button — olive "Позвонить" (idle), red "Положить трубку" with rotated phone icon (connected), olive "Позвонить ещё раз" (ended), indigo "Закрыть разбор" (feedback open).

**Feedback modal** (after call): blurred overlay; book icon + "Обратная связь" title + XP badge (indigo-soft pill with bolt); scrollable summary with "Подробнее" markdown expansion; footer "Новый диалог" button.

**Extras:** ringing sound while dialing, vibration on connect, hang-up beep; unsupported-browser state (bad-soft mic icon circle + "Голосовой режим недоступен").

### 6.8 Guidebook `/guidebook`
- Hero: "СПРАВОЧНИК · N ТЕХНИК", title "Коллекция.", stat tiles (mastered / level / new).
- Search input (360px, icon + clear), active tag pills (`#tag` + remove), skill filter pills with counts ("Все" + per skill; active = dark bg).
- Technique cards (2-col grid): **MasteryRing** (56px SVG progress ring with "L1"/"—"), chips (skill, tags, "New"), title 22px, summary, difficulty badge, expand chevron.
- Expanded card spans full width: markdown body, example dialogue as alternating chat bubbles with `[annotation]` labels, case-study box (bg-2, metrics in mono), right 300px **coach sidebar** (GeoAvatar 44px, name, mono role, quote, challenge pills).

### 6.9 League `/league`
- Tier header (medal emoji + tier name color-coded + description).
- Dismissible promotion/demotion outcome banner (olive-soft / bad-soft).
- Countdown timer "ДД : ЧЧ : ММ" (3xl tabular numbers).
- Stats: current rank badge + weekly XP (tone follows zone).
- Leaderboard table: sticky "# / УЧАСТНИК / XP" header; rank badges (1st rust, 2nd gray, 3rd clay); rows with GeoAvatar + name (+"(ты)") + mono XP; zone backgrounds (indigo-soft = you with 3px left border, olive-soft = promotion, bad-soft = demotion); zone divider lines "БЕЗОПАСНАЯ ЗОНА" / "ЗОНА ВЫЛЕТА".
- CTA banner "Ускорь продвижение" → `/tree`.

### 6.10 Friends `/friends`
- Hero: "СООБЩЕСТВО · N ДРУЗЕЙ", title "Друзья.", stat tiles (friends / requests / chats).
- 4-tab bar (active = dark `--ink` bg): **Друзья** (UserSearchBar + friend cards + right `FriendActivityFeed` sidebar), **Заявки** (badge count; incoming/outgoing `FriendRequestCard` with accept/decline), **Лидерборд** (`FriendLeaderboard`), **Чаты** (`ChatsPane`: conversation list + thread + message input, 5s polling).
- Sub-pages: `/friends/[userId]` public profile, `/friends/chat/[conversationId]` full DM view.

### 6.11 Profile `/profile`
Two-column desktop layout (`.profile-grid`, collapses to one column ≤1000px) inside a 1320px container; header + stats span full width:
- Header card: GeoAvatar 80px square (initials on indigo), name, email, persona pill (e.g. "SDR").
- Stats grid 4×: streak / XP / record / accuracy (icon circles in rust/indigo/olive/clay).
- "Навыки пройдено" progress bar.
- Voice minutes section: mic badge, daily + monthly quota bars (red when exceeded).
- Achievements: 5-col badge grid; locked = grayscale 0.4 opacity, unlocked = indigo-soft; hover tooltip.
- Skills enrollment list: search + rows with toggle switches (indigo when on; one always-on skill disabled).
- Settings: theme toggle, admin link (if Admin), red logout.

### 6.12 Admin `/admin/*`
Separate sidebar-nav layout. Dashboard + CRUD pages for skills/topics/lessons/reference/techniques/dialog scenarios/AI prompts/voice usage analytics/users. Functional tables-and-forms style; uses the same token system but minimal styling effort. (Likely out of redesign scope, but exists.)

---

## 7. Gamification elements present across screens

These recur everywhere and must survive any redesign:
- **Streak** (flame icon, rust) — app bar pill, profile, tree stats.
- **XP** (bolt icon, indigo) — earned per lesson/dialog, weekly XP drives league.
- **Hearts** (4 per lesson session) — lost on mistakes.
- **Levels** ("Level N" in profile chip), **achievements** (badges + slide-up toasts), **leagues** (Bronze/Silver/Gold/Diamond tiers), **mastery rings** (L1+ per technique), **voice minute quotas** (daily/monthly).

---

## 8. Where things live (for the implementing agent)

```
src/frontend/
  app/                      # Next.js App Router pages (routes above)
    globals.css             # ALL design tokens + keyframes — single source of visual truth
    (auth)/ (main)/ (admin)/ # layout groups
  features/                 # per-feature components (layout/, exercises/, dialog/, friends/, …)
  shared/
    components/             # Button, Card, Icon, StatTile, GeoAvatar, …
    stores/                 # Zustand
    api/                    # api-client + React Query hooks
```

Existing redesign-related material: `docs/REDESIGN_PROMPT.md` (earlier brief), `docs/HANDBOOK_REDESIGN.md` (implemented guidebook redesign), `.design/redesign/` (jsx canvases). `.design/DESIGN.md` is the sacred original design doc — do not modify.

---

## 9. Known visual inconsistencies / debt (useful context, not direction)

- Voice call page uses **inline styles** instead of shared token classes/components — restyling it means editing the page, not tokens.
- Two brand names coexist: "SalesTrainer" (app bar, landing header) vs "Sellevate" (login card).
- Landing and exercise/voice screens each have bespoke layouts outside the shared `(main)` layout.
- Emoji are used as icons in some places (bundle icons, landing hero, league medals) while the rest of the app uses the custom SVG icon set.
- Admin panel is visually much plainer than the user-facing app.
