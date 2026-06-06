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

## Verification (Playwright, 2026-06-07)
- /tree desktop+mobile, /session (choose-option + result), /league, /profile, /dialog, /guidebook — all render with new palette
- vitest: 53/53 pass; tsc clean
- Pre-existing console errors unrelated to redesign: Google OAuth origin 403, 404 lessons on empty skill, negative SVG circle radius in CircularProgress
