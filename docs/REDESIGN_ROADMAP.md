# Redesign Roadmap — New Design System

Source: `.design/new-design/`

## What changes

From the earthy/sage palette (rust, olive, indigo, oklch) to an electric blue + violet system on a warm off-white canvas. Fonts change from Geist to Manrope (UI) + Unbounded (display) + JetBrains Mono.

---

## Phase 1 — Design tokens [globals.css]
- Replace all CSS variables with tokens from `.design/new-design/css/tokens.css`
- New palette: `--primary: #2f5bff`, `--violet: #7c5cff`, `--bg: #f3f1ec`
- Update shadows, border radii (bigger: xs=8, sm=12, md=16, lg=20, xl=26), spacing
- Dark mode tokens

## Phase 2 — Fonts
- Add Google Fonts link in layout.tsx `<head>`: Manrope, Unbounded, JetBrains Mono
- Update `--font-ui`, `--font-display`, `--font-mono` variables
- Remove Geist font imports

## Phase 3 — Base component styles [globals.css]
- Buttons: new `.btn` system (btn-primary with `--sh-primary` shadow, btn-dark, btn-soft, btn-outline, btn-ghost, btn-success, btn-danger, size variants)
- Cards: white surface + `--sh-1` shadow
- Glass cards: `backdrop-filter` blur
- Chips & badges
- Progress bar: gradient blue→violet
- Inputs: updated border radius + focus ring
- Toggle switch
- Stat tiles
- App backdrop: radial gradients + grid

## Phase 4 — AppBar (TopAppBar)
- Glass effect (backdrop-filter + semi-transparent bg)
- Height: 60px → 66px
- Gradient logo mark (blue→violet gradient square with icon)
- Wordmark with ".sellevate" + primary dot
- Pill-shaped nav links, active = blue soft bg
- Streak pill (flame color, rounded)
- Icon buttons (notifications)
- Profile chip (avatar + level display)
- Mobile: keep burger but update style

## Phase 5 — BottomNav
- Glass effect
- Active state: primary color

## Phase 6 — StatsWidget
- 2x2 grid stat tiles with new tints
- Daily goal card with progress
- Tip card
- League mini card

## Phase 7 — Skill Tree page
- Update layout colors, sidebar styles
- Skill row active state → primary
- Stage group styles

## Phase 8 — Exercise screens
- Option buttons (`.opt` style) — white bg + border, selected = blue soft
- Session footer: ok = success-soft, bad = heart-soft
- Complete screen

## Phase 9 — Other screens
- League, Profile, Dialog screens
- Verify with Playwright MCP

---

## Status
- [x] Phase 1: Design tokens — globals.css rewritten; legacy token aliases (`--indigo`, `--rust`, `--olive`, `--clay`, `--sage`, `--good`, `--bad`, `--warn`) kept so untouched screens inherit the palette
- [x] Phase 2: Fonts — Manrope/Unbounded/JetBrains Mono via Google Fonts; Geist removed
- [x] Phase 3: Base styles — btn/card/chip/prog/stat/field/switch/appbar/bottom-nav CSS classes
- [x] Phase 4: AppBar — glass, gradient mark, pill nav, streak pill, profile chip
- [x] Phase 5: BottomNav — glass + primary active
- [x] Phase 6: StatsWidget — new tones, eyebrow labels, gradient progress
- [x] Phase 7: Skill Tree — inherits via aliases; fixed mobile bug (inline display:grid overrode `hidden`, now `.tree-desktop-grid` media query)
- [x] Phase 8: Exercises — verified in Playwright: options, success states, result banner OK
- [~] Phase 9: Other screens — League/Profile/Dialog/Guidebook verified visually; deeper polish optional
- [x] Mobile BottomNav rendered in MainLayout; CSS layer conflicts fixed (unlayered component classes beat Tailwind layered `hidden` utilities — resolved via media queries in globals.css)
- [x] Dark theme verified (data-theme="dark" tokens)

## Verification (Playwright, 2026-06-07)
- /tree desktop+mobile, /session (choose-option + result), /league, /profile, /dialog, /guidebook — all render with new palette
- vitest: 53/53 pass; tsc clean
- Pre-existing console errors unrelated to redesign: Google OAuth origin 403, 404 lessons on empty skill, negative SVG circle radius in CircularProgress

---

# Stage 2 — Screen-level redesign (mockup `.design/new-design/js/*.jsx` + `css/screens.css`, `css/tree.css`)

Stage 1 only ported tokens/fonts/base components. The mockup also defines full screen layouts
that the app does not follow yet (screens still use old inline-style markup). Stage 2 ports them.

## Phase S1 — Missing CSS [globals.css]
Port from mockup css not yet in globals.css:
- Typography: `.display .h1 .h2 .h3 .h4 .lead .body .small .muted .num`
- Helpers: `.page .row .col .gap-* .wrap .grow .center .between .hr .itile.* .hero-head .hero-stats .back-link .field-wrap .nav`
- Tree (tree.css): `.stat-2x2 .tree-grid-a .tree-center .lp-scroll .stage-head/.stage-dot/.stage-name/.stage-ratio/.stage-skills .skill-row/.skill-ic/.skill-name .skill-band .lesson-path .lp-* .lp-trophy .goal-card .tip-card .league-mini`
- Session/complete (screens.css): `.session* .opt* .exercise .complete* .check-circle .confetti .cs`
- Dialog: `.bundle-grid/.bundle-card .mentor-card/.mentor-tex .mode-grid/.mode-card`
- Voice: `.voice* .transcript .tr-bubble .state-pill .vdot/.pdot`
- Text chat: `.chat-screen .dc-*`
- Modal/feedback: `.modal* .fb-* .more-btn`
- League: `.league-grid .countdown .cd-* .cta-row .lb-* .rank-badge .you-tag`
- Tabs/friends: `.tabbar .tab .friends-grid .friend-row .activity .chat-wrap .chat-* .msg`
- Guidebook: `.gb-* .mastery-ring .dlg-example .dx .case-box .case-metrics`
- Profile: `.profile-head .profile-stats .ach* .theme-* .logout-row .quota`
- Landing/auth: `.landing .land-* .auth .auth-card .auth-or`
- Responsive blocks (1000px/760px/560px)

## Phase S2 — Skill Tree page → Variant A (3-column cards)
- Container layout `.tree-grid-a`: sidebar card / center card / stats rail (not full-bleed grid)
- Sidebar: `.stage-head` + `.stage-skills` + `.skill-row` (active = primary-soft)
- Center: `.skill-band` header + `.lp-scroll.dotted` serpentine `.lesson-path` (lp-node 72px, lp-pulse, lp-meta, trophy)
- Right: StatsWidget on `.stat-2x2` + goal/tip/league-mini cards
- Mobile: stacked, same components

## Phase S3 — Dialog list + Mode select
- `/dialog`: HeroHead + `.bundle-grid` of `.bundle-card` (itile, chip, "Открыть →") + `.mentor-card`
- `/dialog/[bundleId]`: `.back-link`, itile header, `.mode-grid` of `.mode-card` (Чат / Позвонить buttons)

## Phase S4 — Text chat (AI dialog)
- `.chat-screen` 2-col: `.dc-side` history sidebar + `.dc-main` (dc-head, dc-thread, dc-msg/dc-bubble, typing dots, dc-input)

## Phase S5 — Voice roleplay
- `.voice` layout: voice-top (back, status dot+timer, quota), voice-stage (voice-avatar + va-ring pulse, transcript tr-bubbles, state-pill, hint), voice-foot CTA
- Feedback → `.modal` with `.fb-list good/bad`

## Phase S6 — Guidebook
- HeroHead + `.gb-tools` (search + chip filters) + `.gb-grid` of expandable `.gb-card` with `.mastery-ring`, `.dlg-example`/`.dx`, `.case-box`

## Phase S7 — League / Friends / Profile / Session polish
- League: `.league-grid` + `.countdown`/`.cd-*` + `.cta-row` + `.lb-card` rows with zones
- Friends: `.tabbar` + `.friends-grid` + activity rail + `.chat-wrap` chats
- Profile: `.profile-head`/`.profile-stats`/`.ach-grid`/`.theme-grid`/`.logout-row`
- Session: switch inline styles to `.session*`/`.opt*` classes; complete screen → `.complete` + confetti

## Phase S8 — Landing + Auth
- Landing: `.land-top` wordmark, `.land-hero` display title with grad-text, `.land-features`
- Auth: centered `.auth-card` with wordmark, fields, dark CTA, `.auth-or`, Google button

## Status (Stage 2)
- [x] S1 CSS — all mockup screen classes ported to globals.css
- [x] S2 Tree — variant A card layout, serpentine lesson path, verified in Playwright (desktop+mobile)
- [x] S3 Dialog list + mode — HeroHead, bundle-grid, mentor-card, mode-grid; verified
- [x] S4 Text chat — chat-screen/dc-* layout, history sidebar, bubbles, typing dots; verified
- [x] S5 Voice — voice-stage, pulsing avatar ring, tr-bubbles, state-pill; verified
- [x] S6 Guidebook — hero-head, gb-tools search/filters, expandable gb-cards with mastery ring; verified
- [x] S7 League/Friends/Profile/Session — countdown/lb-card with promo/demote zones, tabbar/friends-grid/activity rail, profile-head/ach-grid/theme-grid/logout-row; Session: shell→`.session*`, options→`.opt*` (А/Б/В keys, sel/correct/wrong/dim), shared `ExerciseActionFooter`, result banner→`.session-foot.ok/.bad` with `.btn-success`/`.btn-danger` Дальше, complete screen→`.complete`/`.confetti`; all 10 exercise types migrated off legacy vars; verified
- [x] S8 Landing/Auth — land-hero with grad-text display title, land-features, centered auth-card with wordmark/auth-or/Google; verified

After each phase: rebuild `docker compose up --build -d frontend`, verify via Playwright MCP, commit.

## Verification (Playwright, 2026-06-07, stage 2)
- Verified against the running app: /tree (variant A + serpentine + mobile + dark theme), /dialog, /dialog/[id], AI text chat (bubbles, typing dots, history sidebar), voice call, /guidebook, /league, /friends, /profile, landing (logged out), /login, /session (option select → green/red verdict bar)
- vitest 53/53, tsc clean (tests updated: button labels «Проверить»/«Пропустить», non-colliding lesson titles in LessonPath test)
- Fixed long-standing GeoAvatar bug: negative seed hash produced ink-on-ink avatars and the negative-SVG-radius console error; avatars now render the new palette
- Remaining known console noise: Google OAuth origin 403 (env config), 404 lessons for skills without content
