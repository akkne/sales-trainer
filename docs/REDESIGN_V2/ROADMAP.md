# Redesign V2 — Roadmap

> **Source of truth:** `.design/Project redesign for SalesTrainer/` (Claude-Design canvas).
> Full spec extracted to [`DESIGN_SPEC.md`](./DESIGN_SPEC.md).
> This roadmap **supersedes** the earlier blue/Manrope redesign in `docs/REDESIGN_ROADMAP.md`.

Frontend-only. **Backend is NOT touched** in this effort.

## Goal

Re-skin the entire user-facing frontend to the new violet / Hanken Grotesk /
left-nav-rail design system, **without changing functionality**, while removing
leagues + achievements from the UI (per product decision).

## Locked product decisions

- **New shell:** left vertical "sticker rail" nav (72px) replaces the TopAppBar +
  BottomNav. Rail items: avatar→`/profile`, Learning Path→`/tree`, Practice→`/dialog`,
  Reference→`/guidebook`, Friends→`/friends`, Discuss→`/discuss`, Settings→`/settings` (new route).
  **No League item.**
- **Palette:** primary violet `#6C5BD9` (hover `#5C4CCB`), dark emphasis `#1C1C20`,
  canvas `#FBFBFC`, surface `#FFF`. Font: **Hanken Grotesk** (400–800). Frame radius 22px.
- **Remove from UI — Leagues:** drop from nav + remove `StatsWidget` league mini-card +
  remove league analytics page-view/event. **Keep `/league` route/page working** (reachable
  by direct URL only) — do not delete it, do not redesign it.
- **Remove from UI — Achievements:** remove the profile achievements grid, the in-session
  `AchievementToast`, the friend-card achievement count, the `earned_achievement` activity
  type, and the public-profile achievement StatTile. Delete now-dead `AchievementToast`
  component + test. Backend-facing data fields may stay (ignored).
- **Hearts:** **no-op.** There is no hearts/lives mechanic in the frontend — `--heart` is
  only an error-color token; retries are attempt-based (max 2). Nothing to remove.
- **Keep (per new design):** accuracy %, "Best record · N days" (streak), skills/lessons
  done, per-skill progress, **voice-minute quotas**, **levels** & **mastery rings**
  (NOT in removal scope). XP is not surfaced in the new shell (design choice) but stays in
  backend + completion stats.
- **Out of scope (not redesigned):** Landing `/`, the `/league` page, the Admin area `/admin/*`.

## Execution model

Sequential phases (shared `globals.css` forbids parallel edits). After **every** phase:
a **verifier/tester** agent runs `tsc`, `vitest`, `eslint`, greps for removed-feature
leakage, and reviews the diff. Commit after each green phase (`feat:`/`refactor:`/`fix:`).
A final full-QA pass closes the effort. App is **not** auto-run (>60s rule) — visual
verification is collected into a checklist for the user.

---

## Phases

- [x] **P1 — Tokens + fonts + base components** ✅ verified (vitest 83/83; tsc clean except pre-existing stale `.next` validator noise for nonexistent admin/open-question route)
  - `app/globals.css` `:root` + `[data-theme="dark"]`: replace palette with V2 tokens
    (violet primary, new ink/surface/border scale, radii 22/16/14/11/9, new shadows).
  - `app/layout.tsx`: swap Google Fonts to Hanken Grotesk 400–800; remove Manrope/Unbounded.
  - Base + reusable component classes: buttons (primary/dark/soft-violet/outline/light/text-link),
    cards (resting + violet-lift hover), chips/badges (difficulty, status, hashtag, NEW, count),
    inputs/search, toggle switch, stat tile, progress bar, segmented control, avatar gradients.

- [ ] **P2 — Screen-level CSS classes** (all into `globals.css`, before touching screens)
  - `.rail*` nav rail; `.path*` 3-col + lessons timeline + FAB; `.practice*`/bundle/mentor/recent;
    `.ref*` grid + 392px detail panel + example bubbles + metric tiles + coach; `.friends*` grid +
    online dot + 330px chat/activity rail; `.discuss*` feed + upvote + segmented + sidebar;
    `.profile*` cover/identity/stat tiles/enrolled/voice-quota; `.settings*` cards/toggle/danger;
    plus session/dialog/voice/auth restyle helpers. Responsive blocks.

- [ ] **P3 — App shell: left nav rail**
  - Replace `top-app-bar.tsx` + `bottom-nav.tsx` usage with a new `nav-rail.tsx`
    (`features/layout/components/`). Update `(main)/layout.tsx` to the rail + framed shell.
    Friend-request unread dot on Friends item. **No League nav item.** Mobile variant.

- [ ] **P4 — Gamification removal (leagues + achievements UI)**
  - League: remove `StatsWidget` league mini-card (`stats-widget.tsx`); remove league entries
    from `analytics/track.ts` + `use-page-view-tracker.ts`. (`/league` page stays.)
  - Achievements: remove grid in `profile/page.tsx`; remove toast mount + capture in
    `session/[lessonId]/page.tsx`; remove count in `friend-card.tsx` + public profile;
    remove `earned_achievement` in `friend-activity-feed.tsx`; delete `achievement-toast.tsx`
    + `__tests__/AchievementToast.test.tsx`; clean dead imports/hooks.

- [ ] **P5 — Screen restyles** (each sub-phase = its own commit + verifier)
  - [ ] 5.1 Path / `tree`
  - [ ] 5.2 Practice (`dialog` list + `dialog/[bundleId]` mode select)
  - [ ] 5.3 Reference (`guidebook` + `reference/[id]`)
  - [ ] 5.4 Friends (`friends` + chat + `friends/[userId]` public profile)
  - [ ] 5.5 Discuss (`discuss` + `discuss/[threadId]`)
  - [ ] 5.6 Profile + **new `/settings` route** (split settings out of profile; preserve theme/logout/admin/email/password)
  - [ ] 5.7 Session / Exercise player (`session/[lessonId]` + exercise components)
  - [ ] 5.8 Dialog text chat + Voice call (`dialog/[bundleId]/[modeId]` + voice)
  - [ ] 5.9 Auth: login / register / onboarding / verify-email

- [ ] **P6 — Final QA pass**
  - Full tester: `tsc` + `vitest` + `eslint` clean; grep no league/achievement leakage in
    user shell; route + nav audit; build check; assemble manual-visual checklist for the user.

---

## Status log

- 2026-06-25 — Phase 0 done: DESIGN_SPEC.md extracted; current-frontend map produced;
  product decisions locked; roadmap created.
