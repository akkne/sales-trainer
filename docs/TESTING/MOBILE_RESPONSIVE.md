# Mobile Responsiveness — Manual Test Checklist

The frontend is adapted for phones (target 360–414px wide). Responsive layout
lives almost entirely in `src/frontend/app/globals.css` (shared grid/layout
classes) plus per-page Tailwind modifiers; the admin panel has a dedicated
mobile drawer in `src/frontend/app/(admin)/layout.tsx`.

## Breakpoints in use
- `768px` (Tailwind `md`) — desktop/mobile boundary; top nav ↔ bottom nav + hamburger.
- `1000px` — multi-column grids collapse to a single column (tree, league, friends, discuss, profile, guidebook).
- `760px` — AI text-chat sidebar hides.
- `640px` — phone refinements block in `globals.css`: tighter gutters/paddings, single-column dialog grids, shrunk countdown, wider chat bubbles, smaller lesson-path nodes, near-fullscreen modals.
- `560px` — achievements grid → 3 cols, landing features → 1 col.

## How to test
Open Chrome DevTools → device toolbar (iPhone SE 375px and Pixel 414px), or resize the window. Check both light and dark theme.

## User-facing checklist (no horizontal page scroll anywhere)
- [ ] **Top bar / nav** — desktop nav hidden, hamburger + bottom nav visible; profile chip / streak pill hidden in appbar.
- [ ] **Tree** — sidebar, lesson path, stats stack into one column; lesson-path nodes fit without overflow.
- [ ] **League** — countdown digits fit on one row; leaderboard rows don't overflow.
- [ ] **Dialog list** — bundle/mode cards are one per row (no clipped 300px cards); mentor card padding sane.
- [ ] **AI text chat** — conversation sidebar hidden ≤760px; bubbles ~85% width; input row fits.
- [ ] **Voice** — avatar shrinks; CTA is full-width.
- [ ] **Session/exercise** — options, footer buttons fit; reduced top/body padding.
- [ ] **Friends / chat** — list/window stack; message bubbles ~88% width.
- [ ] **Discuss** — tag sidebar stacks below threads; thread rows fit.
- [ ] **Profile** — header wraps; stats 2-up; achievements 3-up.
- [ ] **Guidebook** — cards single column; expanded card readable.
- [ ] **Landing / auth** — hero scales (clamp); features single column; auth card fits.
- [ ] **Modals** — near-fullscreen, scroll internally, never overflow viewport.

## Admin checklist
- [ ] Mobile top bar with hamburger appears (<768px); tapping it opens the drawer over a dimmed backdrop.
- [ ] Drawer closes on backdrop tap, on the X button, and automatically after navigating to a section.
- [ ] On desktop (≥768px) the sidebar is static as before — no drawer behavior.
- [ ] Every admin **table** scrolls horizontally inside its own region (`overflow-x-auto`) instead of breaking the page layout.
- [ ] Edit/create **forms** collapse multi-column grids to a single column; no fixed-width input overflows.
- [ ] Page header action buttons wrap below the title on narrow screens.
