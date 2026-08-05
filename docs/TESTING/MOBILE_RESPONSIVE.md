# Mobile Responsiveness — Manual Test Checklist

The frontend is adapted for phones (target 360–414px wide). Responsive layout
lives almost entirely in `src/frontend/app/globals.css` (shared grid/layout
classes) plus per-page Tailwind modifiers; the admin panel has a dedicated
mobile drawer in `src/frontend/app/(admin)/layout.tsx`.

Mobile shell (redesign V2): the desktop left nav rail is hidden ≤767px and
replaced by two bars rendered from `src/frontend/app/(main)/layout.tsx`:
- **`MobileTopbar`** (`features/layout/components/mobile-topbar.tsx`) — sticky
  top bar with the wordmark plus the rail-only destinations that have no slot
  in the bottom nav: Справочник, Обсуждения, Настройки, and the notification
  bell (panel opens full-width under the bar).
- **`BottomNav`** — fixed bottom tab bar: Путь, Практика, Компании, Друзья,
  Профиль (safe-area aware; content reserves space via `.has-bottom-nav`).

Full-screen flows (session player, AI chat/voice, company calls, auth,
onboarding) size themselves with `100dvh` (with a `100vh` fallback) so the
mobile browser address bar doesn't crush the layout.

## Breakpoints in use
- `767.98px` / `768px` (Tailwind `md`) — desktop/mobile boundary; top nav ↔ bottom nav + hamburger.
  The mobile side is written as `max-width: 767.98px` rather than `767px` on purpose: at a
  fractional viewport width (Android devices with a non-integer `devicePixelRatio`, Windows
  display scaling) `767px` and `min-width: 768px` *both* failed to match, so the rail and the
  bottom nav rendered at the same time and the nav covered content with no reserved padding.
- `1000px` — multi-column grids collapse to a single column (tree, league, friends, discuss, profile, guidebook).
- `768–1000px` (tablet band, expressed as a non-overlapping range) — the tree FAB becomes
  `position: fixed`. It must not be folded into `max-width: 1000px`, or it would override the
  phone offset that clears the bottom nav.
- `640px` — phone refinements block in `globals.css`: tighter gutters/paddings, single-column dialog grids, shrunk countdown, wider chat bubbles, smaller lesson-path nodes, near-fullscreen modals.
- `560px` — milestones grid → 3 cols, landing features → 1 col.
- `400px` — friend-request Accept/Decline buttons move to their own full-width row.
- **`max-height: 520px` (with `max-width: 1023.98px`)** — the only *height* breakpoint.
  Landscape phones report ≥768px wide but only 375–430px tall, so they were served the desktop
  shell; the 72px rail needs ~516px of vertical space and its last items (notification bell,
  settings) fell below the fold unreachable. This tier forces mobile chrome instead. The rail
  additionally sizes its padding/gap/items with `clamp(..., vh, ...)` so it compresses rather
  than overflowing; above ~820px tall every clamp sits at its max and desktop is unchanged.

## Sizing rules to preserve
- **Never size a layout box with `100vh` alone.** `vh` resolves against the *large* viewport
  (toolbars retracted), so on mobile browsers the box is taller than what is visible and its
  bottom — usually where a CTA lives — is permanently off-screen. Always ship the
  `height: 100vh; height: 100dvh;` fallback pair. This applies to `body`, `.shell`,
  `.shell-content`, `.rail`, `.landing`, `.modal` and every full-screen flow.
- **Every bottom-anchored control needs `env(safe-area-inset-bottom)`.** `viewportFit: "cover"`
  is set in `app/layout.tsx`, so the page really does render under the home indicator (34px on
  notched iPhones, ~24px on Android gesture nav). Use
  `padding-bottom: max(<base>, env(safe-area-inset-bottom))`.
- **A flex/grid child that holds text needs `min-width: 0`** (and `minmax(0, 1fr)` for grid
  tracks), otherwise it floors at its min-content width and pushes siblings off-screen.
- **A row of unshrinkable buttons needs `flex-wrap: wrap`** on its parent — without it the last
  button is pushed past the edge and clipped.

## How to test
Open Chrome DevTools → device toolbar (iPhone SE 375px and Pixel 414px), or resize the window. Check both light and dark theme.

## User-facing checklist (no horizontal page scroll anywhere)
- [ ] **Top bar / nav** — nav rail hidden; mobile top bar (wordmark + справочник/обсуждения/настройки/уведомления) and bottom nav visible; notification panel opens full-width under the top bar and doesn't cover the bottom nav.
- [ ] **Tree (phone ≤767px)** — dedicated layout: desktop sidebar/overview hidden; sticky skill-picker bar under the topbar opens a bottom sheet with the stage/skill accordion; picking a skill closes the sheet. Header card shows 2 stat cells (placeholder "Точность/Время" hidden); timeline nodes 30px; "Начать/Повторить" buttons enlarged; FAB fixed above the bottom nav, full-width.
- [ ] **Exercises on touch** — match-pairs columns stack vertically (tap left item, then right); categorize works without drag-and-drop: tap a phrase → highlighted, tap a category → placed (letter shortcut buttons hidden on touch); reorder up/down arrows are comfortably tappable; submit footer has 16px side padding.
- [ ] **League** — countdown digits fit on one row; team progress rows don't overflow.
- [ ] **Dialog list** — bundle/mode cards are one per row (no clipped 300px cards); mentor card padding sane.
- [ ] **AI text chat** — conversation sidebar hidden ≤767.98px and opens as an overlay drawer when the header toggle is tapped; tapping the scrim closes it; bubbles ~85% width; input row fits.
- [ ] **AI text chat header (≤360px)** — the ✕ close button stays on screen (the header row wraps rather than pushing it off the right edge).
- [ ] **Voice** — avatar shrinks; CTA is full-width.
- [ ] **Session/exercise** — options, footer buttons fit; reduced top/body padding.
- [ ] **Friends / chat** — list/window stack; message bubbles ~88% width.
- [ ] **Discuss** — tag sidebar stacks below threads; thread rows fit.
- [ ] **Profile** — header wraps; stats 2-up; milestones 3-up.
- [ ] **Guidebook** — cards single column; expanded card readable.
- [ ] **Landing / auth** — hero scales (clamp); features single column; auth card fits.
- [ ] **Modals** — near-fullscreen, scroll internally, never overflow viewport.
- [ ] **Companies list** — header stacks, "Добавить компанию" full-width; search takes the full row; status filter chips wrap.
- [ ] **Company page** — header wraps (smaller avatar, name + status badge can break to a new line); status dropdown stays inside the viewport; readiness ring stacks above its details; timeline card top rows wrap instead of overflowing; call-log/contact form buttons stack full-width.
- [ ] **Company practice call (chat/voice)** — same as AI chat/voice: no 100vh clipping, input reachable with the keyboard open.
- [ ] **Theory (stories) player** — reading area uses tighter phone padding.
- [ ] **Tree FAB** — floating "continue" bar shrinks below 320px viewports instead of overflowing.

## Landscape / short-viewport checklist
Rotate a phone to landscape (or use DevTools at 812×375 and 932×430) — this is the case that
used to hide the notification bell and settings gear entirely.
- [ ] Mobile top bar + bottom nav appear (not the desktop rail), fully styled.
- [ ] No content sits under the bottom nav.
- [ ] On a short *desktop* window (e.g. 1400×500) the rail stays, but compresses to fit — the
      settings gear at the bottom is still visible and clickable.

## Safe-area checklist (notched iPhone / Android gesture nav)
- [ ] Theory ("stories") player — the Далее/Завершить button clears the home indicator.
- [ ] Voice call — the Завершить звонок button clears the home indicator.
- [ ] Onboarding — the Далее button clears the home indicator.
- [ ] Bottom nav — the last row of page content is fully visible above it.
- [ ] Modals with a footer (post-call feedback, delete confirm) — the confirm button is
      reachable with the browser toolbar showing; the overlay scrolls if the modal is tall.

## Admin checklist
- [ ] Mobile top bar with hamburger appears (<768px); tapping it opens the drawer over a dimmed backdrop.
- [ ] On a short viewport (e.g. iPhone SE, 375×667) the drawer's nav list **scrolls** — every section including Users and "Back to app" is reachable.
- [ ] Drawer closes on backdrop tap, on the X button, and automatically after navigating to a section.
- [ ] On desktop (≥768px) the sidebar is static as before — no drawer behavior.
- [ ] Every admin **table** scrolls horizontally inside its own region (`overflow-x-auto`) instead of breaking the page layout.
- [ ] Edit/create **forms** collapse multi-column grids to a single column; no fixed-width input overflows.
- [ ] Page header action buttons wrap below the title on narrow screens.
