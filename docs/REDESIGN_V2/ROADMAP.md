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

> **CSS note:** there is no separate "all screen CSS up front" pass. Each shell/screen
> phase below **owns the CSS classes it needs** (added to `globals.css` alongside its
> markup). Phases run sequentially, so there's no `globals.css` contention. P1 already
> shipped the shared token + base-component layer everything builds on.

- [x] **P2 — App shell: left nav rail** ✅ verified (tsc no new errors, vitest 83/83). NavRail (desktop) + restyled BottomNav (mobile, no League); NotificationBell kept in rail; minimal `/settings` route created; `top-app-bar.tsx` now dead code (delete later). No literal device-frame (full-viewport adaptation).
  - Add a new `nav-rail.tsx` (`features/layout/components/`) + its `.rail*` CSS; replace
    `top-app-bar.tsx` + `bottom-nav.tsx` usage in `(main)/layout.tsx` with the rail + framed
    shell. Avatar→`/profile`, Path→`/tree`, Practice→`/dialog`, Reference→`/guidebook`,
    Friends→`/friends`, Discuss→`/discuss`, Settings (bottom)→`/settings`.
    Friend-request unread dot on Friends item. **No League nav item.** Mobile variant.

- [x] **P3 — Gamification removal (leagues + achievements UI)** ✅ verified (tsc no new errors, vitest 78/78 after dropping 5 AchievementToast tests). League mini-card + analytics removed (page kept); achievements grid/toast/friend-count/activity removed; `achievement-toast.tsx`, its test, and `use-achievements.ts` deleted; notification-meta achievement entry kept for graceful backend notifications.
  - League: remove `StatsWidget` league mini-card (`stats-widget.tsx`); remove league entries
    from `analytics/track.ts` + `use-page-view-tracker.ts`. (`/league` page stays.)
  - Achievements: remove grid in `profile/page.tsx`; remove toast mount + capture in
    `session/[lessonId]/page.tsx`; remove count in `friend-card.tsx` + public profile;
    remove `earned_achievement` in `friend-activity-feed.tsx`; delete `achievement-toast.tsx`
    + `__tests__/AchievementToast.test.tsx`; clean dead imports/hooks.

- [ ] **P4 — Screen restyles** (each sub-phase = its own CSS + commit + verifier)
  - [x] 4.1 Path / `tree` ✅ 3-col accordion + timeline + FAB + overview; tsc clean, vitest 78/78. Omitted "what you'll learn"/related-techniques + accuracy/time stats (no backend data).
  - [x] 4.2 Practice ✅ mentor banner + bundle grid (Chat/Call) + recent sessions + mode select; tsc clean, vitest 78/78. Mode-count number + dialog stats omitted (no data); difficulty inferred from sortOrder.
  - [x] 4.3 Reference ✅ card grid + 392px slide-in detail panel (example bubbles, metric tiles, coach); MasteryRing kept (sm on card, full in panel); tsc clean, vitest 78/78. Hardcoded quick-tags + old stat-tiles omitted (data contract).
  - [x] 4.4 Friends ✅ main list (requests + friend grid) + 330px Activity/Chat rail; public profile restyled. Leaderboard tab UNMOUNTED (component kept) per design+gamification removal; online-dot omitted (no backend presence); chat polling + deep-links preserved. tsc clean, vitest 78/78.
  - [x] 4.5 Discuss ✅ feed + 44px upvote col + segmented sort + 264px sidebar (community/tags/top-authors); thread detail restyled, all mutations (vote/new/reply/accept/pin/images) preserved. "% решённых"=— (not in API). 1 test updated. tsc clean, vitest 78/78.
  - [x] 4.6 Profile + /settings ✅ profile: dark cover + 4 stat tiles + enrolled-skills (enrollment toggles kept, base always-on) + voice quota. Settings: Appearance/Account(email read-only, admin)/Logout. Theme+logout+admin moved off profile. Omitted edit-profile/password-change/notif-prefs (no backend). tsc clean, vitest 78/78.
  - [x] 4.7 Session/Exercise ✅ V2 shell (violet gradient progress, no hearts indicator), all 10 exercise types get chipMap chips + V2 bubbles, result banner (ok/warn/bad tones), completion stats XP/accuracy/time. Confirmed hearts never existed in completion props. tsc clean, vitest 78/78, no tests touched.
  - [x] 4.8 Dialog chat + Voice ✅ V2 chat (history sidebar, asymmetric bubbles, typing dots, feedback modal) + voice (state-ring colors per pipeline state, mic hero, transcript, quota, all states). State machine untouched (restyle only); fixed interrupted-bubble class; removed dead code. tsc clean, vitest 78/78.
  - [ ] 4.9 Auth: login / register / onboarding / verify-email

- [ ] **P5 — Final QA pass**
  - Full tester: `tsc` + `vitest` + `eslint` clean; grep no league/achievement leakage in
    user shell; route + nav audit; build check; assemble manual-visual checklist for the user.

---

## Status log

- 2026-06-25 — Phase 0 done: DESIGN_SPEC.md extracted; current-frontend map produced;
  product decisions locked; roadmap created.
