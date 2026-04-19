# Sellevate — Full Interface Redesign (Claude Design Prompt)

> Ready-to-paste prompt for Claude Design / Google Stitch.
> Goal: redesign the entire user interface of Sellevate while preserving every existing feature. Handbook and AI Dialogs may be reimagined more freely (see section 5).

---

## 1. Product overview

Sellevate is a Duolingo-for-sales web app: a gamified B2B sales-skills trainer used primarily in Russian by SDRs, Account Executives, Account Managers and founders. Users complete bite-size exercises, practice live conversations with AI, and compete in weekly leagues. The whole UI is in Russian — keep Russian labels verbatim. Target platforms: responsive web (mobile-first + desktop shell), Next.js 15 App Router, Tailwind.

Current look is a Duolingo clone (green #58CC02, 3D buttons with bottom shadow, cartoon mascot, Manrope). **Throw that away.** You have full brand freedom — new palette, type, motion language, illustration style. The ask is a fresh, premium, distinctive look while keeping the gamified, playful spirit of daily-streak learning. Surprise me.

## 2. Required screens (design every one)

### Auth & onboarding
1. **Landing `/`** — hero, value props, two CTAs ("Начать бесплатно", "Попробовать без регистрации"), feature grid (realistic scenarios, AI evaluation, streaks & leagues, handbook).
2. **Login `/login`** — email+password, Google OAuth, link to register.
3. **Register `/register`** — symmetric to login.
4. **Onboarding (4 steps)** — persona picker (SDR / AE / Account Manager / Founder / Other) → sales type → experience → skill selection. Progress dots + "Пропустить".

### Main shell (authenticated)
Design both shells:
- **Mobile:** bottom nav with 6 items — `Путь`, `Лига`, `Справочник`, `Диалог`, `Друзья`, `Профиль`. Notification bell + profile chip in a compact top area.
- **Desktop:** top app bar with brand lock-up, horizontal nav (same 6 items), streak counter, notification bell with unread badge, profile chip showing "Уровень N" and avatar initial.

### Core learning flow
5. **Home `/tree`** — main hub. Two modes:
   - No skill selected → full all-skills "lesson path" (a scrollable vertical path of lesson nodes in zigzag layout).
   - Skill selected → same path scoped to one skill, with "Сменить навык →" link.
   Each node has 3 states: **completed** (medal badge), **active** (prominent, pulsing ring, tap opens popover), **locked** (subdued, lock icon). Active-node popover: lesson title, "Урок X из N", primary CTA "Приступить к прохождению", secondary link "Посмотреть карту курса →". Side widget with: 🔥 streak, ⚡ weekly XP, 🏆 total XP, plus a short motivational tip.
6. **Skill course map `/skill/[id]/map`** — rich per-skill overview. Header with skill title, icon, completion %, circular progress. Grid/list of lesson cards (step number/lock/check badge, title, 1-line description, estimated minutes, XP reward, status chip like "Далее"). CTAs: "Начать", "Продолжить", "Повторить", or locked state with "Пройди предыдущий урок".
7. **Lesson session `/session/[lessonId]`** — full-screen, no global nav. Header: ✕ close, animated progress bar, ❤️ hearts counter (starts at 4, lose one per wrong answer). Body: character speech bubble for context, then one of 11 exercise types (see section 4). Footer: "ПРОПУСТИТЬ" + primary action ("ПРОВЕРИТЬ" / "ПРОДОЛЖИТЬ" / "ОТПРАВИТЬ ОЦЕНКУ" / "ЗАВЕРШИТЬ ДИАЛОГ"), plus keyboard hint ("1–4 выбрать · Enter — проверить"), hidden on touch. Result banner slides up from bottom — green for correct, red for wrong, with explanation and XP earned. Completion screen: big celebration, 2×2 stat grid (XP earned / accuracy % / time / hearts remaining), primary "Следующий урок →" button when available, secondary "Вернуться к пути". Hearts=0 → failure screen with "Попробовать снова". Achievement unlock toasts slide in during the session.

### Competition & social
8. **League `/league`** — weekly leaderboard. Top banner with tier name (Бронза / Серебро / Золото / Алмаз) and tier artwork. Countdown "До конца недели Xд Xч". Top-3 podium (crown for 1st, medals for 2nd/3rd). Rank list with clearly marked **promotion zone** (top 10, positive highlight) and **demotion zone** (bottom 5, warning tint). Current user's row is pinned-prominent regardless of rank. Promotion/demotion outcome banner at top when a week just closed.
9. **Friends `/friends`** — tabbed: **Друзья** (search bar, activity feed "Активность друзей", friend cards with "Написать"), **Запросы** (incoming + outgoing, unread badge on tab), **Рейтинг** (friend-only XP leaderboard), **Чаты** (conversation list).
10. **Public profile `/friends/[userId]`** — avatar, display name, persona badge, stats grid (streak, XP, achievements count, avg score), action buttons (add friend / cancel / remove / message).
11. **Chat `/friends/chat` and `/friends/chat/[conversationId]`** — 1-to-1 chat. Conversation list on the left (last message preview, unread badge, avatar), chat window on the right (message bubbles user-right / friend-left, timestamps, input with send button). On mobile: list and chat are two screens.
12. **Notifications dropdown** — anchored to bell. Header "Уведомления" + "Прочитать всё". Notification cards with type icon, title, body, relative time, unread tint. Types: friend request received, friend request accepted, chat message received, achievement unlocked, streak milestone. On mobile render as a full-screen sheet.

### Practice (AI)
13. **Dialog bundles `/dialog`** — grid of bundle cards (each card: icon, title, description, mode count). Empty state if OpenAI not configured.
14. **Dialog modes `/dialog/[bundleId]`** — mode cards for the chosen bundle. Each mode may support text, voice, or both — show badges.
15. **Dialog chat `/dialog/[bundleId]/[modeId]`** — REIMAGINE heavily (see section 5). Current baseline: collapsible history sidebar grouped by date, chat bubbles, text input, "Завершить диалог" button, feedback modal with XP + feedback text + "Новый диалог".
16. **Voice dialog** — same screen, but a big mic button with states: idle / listening (pulse) / processing (spinner) / AI speaking / error. Status text below ("Нажмите для голоса", "Слушаю…", "AI отвечает…").

### Reference
17. **Handbook `/guidebook`** — REIMAGINE (see section 5). Baseline: category chips (Возражения / Холодные звонки / Закрытие / Квалификация / Rapport / Переговоры) with "Все" default, search input, expandable technique cards (category badge, tags, title, excerpt → full markdown + "Связанный навык →" link).

### Profile
18. **Profile `/profile`** — avatar + display name + email + persona badge. 2×2 or 4-up stat grid: 🔥 current streak, ⚡ total XP, 🏆 longest streak, 🎯 avg score %. "Навыки пройдено" progress bar. **Achievements grid** (5-col on desktop, 3-col on mobile): locked = grayscale + opacity, unlocked = branded color; counter "X из 10 разблокировано". **Мои навыки** enrollment section: per-skill card with toggle — base skill labelled "Базовый — всегда включён", others toggleable. Admin panel link visible only to admins. Logout button.

## 3. Features that MUST stay functional (don't redesign them away)

- 11 exercise types — every interaction pattern below must have a clear UI
- XP, daily streaks with reset, 10 achievements with unlock toasts, leagues with promotion/demotion
- Sequential lesson unlock, retry queue (failed exercises re-queued at end, max 2 attempts)
- Hearts system (4 hearts per session)
- Skill enrollment (subscribe / unsubscribe to skills)
- Skip button in exercises
- Keyboard shortcuts (1–4 select, Enter submit) — show hints on non-touch
- Post-session stats (XP, accuracy, time, hearts remaining)
- Voice roleplay states (idle / listening / processing / playing)
- Notifications bell with unread badge and 5 notification types
- Friend request flow with pending badge on nav
- 1-to-1 chat with polling (keep a live-feeling chat UX)
- Demo mode (no registration) — mention in landing and auth screens
- Admin role indicator on profile (link to admin panel — don't need to redesign admin itself)

## 4. The 11 exercise types — design each as a distinct UI pattern

1. **multiple_choice** — numbered option buttons (1-4), single select, explanation on result
2. **fill_blank** — sentence with `???` placeholder + numbered option buttons that fill the blank
3. **free_text** — textarea with character counter ("Минимум 20 символов"), mic button for voice-to-text, AI score 0-10
4. **ordering** — shuffled list, reorderable via drag OR up/down arrow buttons
5. **matching** — two columns, tap-to-connect pairs, connection visual, reset button
6. **categorizing** — items pool at top, 2–3 category buckets below, drag-or-tap to assign
7. **find_error** — a dialogue with clickable lines, user taps the wrong line and optionally types explanation (10+ chars)
8. **rewrite_better** — read-only original sales phrase + textarea for improved version + criteria list
9. **ai_dialog** — mini chat with a customer persona (avatar + scenario), user types replies, minimum N turns before "Завершить диалог", AI score at end
10. **rate_call** — collapsible call transcript + 1–5 star rating on each evaluation axis + optional comment textarea
11. **written_answer** — prompt bubble + long-form textarea, AI-evaluated

All exercises share: character speech bubble for context/situation, result banner (green/red), explanation card, "Продолжить" action.

## 5. What to REIMAGINE — make these screens feel premium

These parts can break from the current UX. Go further than the baseline:

### Handbook `/guidebook` — gamify it
- **Mastery level per technique** (Novice → Practitioner → Expert → Master), shown as a progress ring or level badge on each card.
- **"Techniques mastered" hero stat** at the top.
- **Inside each expanded card:** a **Sample Dialog** section with alternating prospect/rep chat bubbles (scripted example), a short **Case study** snippet, and 2–3 micro-prompts ("Practice this now →" deep-linking into a matching exercise).
- A "Coach" sidecar in expanded cards: a mentor persona (e.g. "Skeptic Sergey") giving a one-paragraph insight on this technique.
- Filter chips stay, but treat categories as visually distinct "collections" — almost like Pokédex-style sets, not flat filters.

### AI Dialog `/dialog/[bundleId]/[modeId]` — make it a scene, not a chat
- **Customer avatar + scene frame** at the top: large character illustration/portrait, name, job title, company, mood indicator. Background hints at setting (office, phone call, video call).
- **Live in-session feedback rail** on the side (or collapsible): as the user types, a coach panel shows tips, spotted mistakes, and technique flags ("You used open-ended question ✓", "Missed the objection hook ⚠️"). Feels like live commentary.
- **Turn timer** and **objective chips** at the top ("Цель: выявить потребность", "Обойти возражение о цене").
- **End-of-session scorecard:** axes like Rapport / Discovery / Objection-handling / Close with radar/bar chart + XP + transcript replay with highlighted moments.
- History sidebar becomes a **"Case files"** tray — past dialogs as labelled case folders with outcomes.

### New mode: **Live Call with NPC Mentor**
- Add a dedicated entry on `/dialog` (distinct from regular bundles): a rotating NPC mentor card ("Skeptic Sergey", "Mentor Marcus", etc.) with a motivational line + "CHALLENGE" CTA.
- Opens a stylized "call" screen: ringing phone transition, avatar answers, live voice roleplay, post-call review with mentor's verdict.

### Voice mic button
- Make it the hero of the voice screen — a big, tactile, alive control with waveform/particle feedback while listening and a distinct "AI speaking" state.

## 6. Interaction & motion

- Path/tree screens deserve **real motion**: animated dashed path between active and next node, ping/pulse on the active node, node unlock micro-celebration.
- Exercise result: slide-up banner with spring easing, subtle confetti on correct streaks.
- Achievement toast: cinematic slide-in, auto-dismiss after ~4s.
- League tier transitions and promotion outcome banner: make them feel like a reward moment.
- Respect `prefers-reduced-motion`.

## 7. Constraints

- **Languages in UI:** Russian (keep labels verbatim where quoted above).
- **Responsive:** mobile-first, but produce explicit mobile AND desktop layouts for every screen. Bottom nav on mobile, top nav on desktop.
- **Accessibility:** WCAG AA contrast, visible focus rings, don't rely on color alone for correct/wrong, keyboard hints on exercises.
- **Safe areas:** respect iOS safe-area-inset on bottom nav and session footer.
- **Empty states:** design empty states for friends, chats, notifications, leaderboard, no-lessons tree.
- **Loading states:** skeleton layouts, not spinners.
- **Error states:** OpenAI not configured (dialog/voice), network errors, etc.

## 8. Deliverables

For each screen listed in section 2, produce:
- Mobile layout
- Desktop layout (where applicable — session and auth screens can be single-layout)
- Key interactive states (hover/focus/active/loading/empty/error)
- Component tokens (color, type, spacing, radius, shadow) consolidated into a single design system page

Deliver the design system page first (brand, typography, color system, core components: buttons, cards, chips, inputs, modals, toasts, progress, badges, avatars, path nodes, chat bubbles, bottom/top nav) — then the screens. All exported as interactive, clickable prototypes where the main flows (onboarding → tree → session → completion → league) are navigable end-to-end.
