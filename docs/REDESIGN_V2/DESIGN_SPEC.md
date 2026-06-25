# Sellevate / SalesTrainer — Redesign V2 Design Spec

Extracted from the Claude-Design canvas at
`.design/Project redesign for SalesTrainer/SalesTrainer.dc.html`
(markup uses `<x-dc>`; style objects live in the inline `<script type="text/x-dc">`
block at the bottom of that file — `support.js` is only the generic dc-runtime,
it contains none of the app tokens). All hex/px values below are quoted verbatim
from the source.

> The canvas copy is English placeholder. The production app is Russian — capture
> **structure / layout / components / tokens**, not the English strings.

---

## 1. Design Tokens

### 1.1 Color palette

**Backgrounds / canvas**
| Token | Hex | Usage |
|---|---|---|
| Page backdrop (outside app frame) | `#E3E3EA` | body, behind the rounded app shell |
| App shell surface | `#FFFFFF` | the main 1380px rounded frame |
| Content canvas (scroll areas) | `#FBFBFC` | every screen's scrolling body, profile/settings bg |
| Sticker-rail bg | `#FCFCFD` | left nav column |
| Subtle fill / search field / chip bg | `#F4F4F7` | inputs, idle chips, segmented buttons |
| Muted fill (counts, progress track) | `#F1F1F4` / `#EFEFF2` | count badges; progress bar tracks |
| Hover wash (nav, list rows) | `#F1F1F4`, `#F6F6F9`, `#FAFAFC` | nav hover, group hover, row hover |
| Message-in bubble bg | `#F1F1F4` | incoming chat bubble |

**Surfaces / cards**
| Token | Hex | Usage |
|---|---|---|
| Card surface | `#FFFFFF` | all cards, panels, list containers |
| Card border | `#ECECEF` | default card/border |
| Divider (lighter) | `#F0F0F3` / `#F4F4F6` / `#F2F2F4` | column rules, header bottoms, inner separators |
| Frame border | `#E9E9EE` | outer app frame border |
| Input border | `#ECECEF` | search & text inputs |

**Ink / text hierarchy**
| Token | Hex | Usage |
|---|---|---|
| Base body text | `#1C1C1E` | `body` color |
| Heading ink (strong) | `#26262B` | card titles, lesson titles |
| Heading ink (alt) | `#2A2A30` | input text, sub-titles |
| Mid ink | `#3A3A40` | section labels, list names |
| Secondary text | `#52525A` / `#56565E` | learn items, meta values |
| Muted body | `#6A6A72` / `#86868E` / `#8A8A93` | descriptions, paragraphs |
| Placeholder / faint | `#9A9AA2`, `#A0A0A8`, `#B0B0B8`, `#B4B4BC` | labels, timestamps, input placeholder (`#B4B4BC`) |
| Idle nav icon | `#A2A2AA` | inactive rail icons |

**Accent — violet (primary)**
| Token | Hex | Usage |
|---|---|---|
| Primary accent | `#6C5BD9` | primary buttons, active marks, links, progress |
| Primary accent (hover/pressed) | `#5C4CCB` | button hover, link text |
| Accent deep (text on tint) | `#3A2E8C` | active skill-row label text |
| Accent tint surface | `#EFEAFE` / `#EDEAFB` / `#EAE5FB` | violet pill/badge fills |
| Accent tint card bg | `#F5F4FB` / `#F7F6FD` / `#FBFAFE` | chat-button bg, coach quote box, related hover |
| Accent tint border | `#ECE9F9` / `#EEEAFB` / `#E2DCF7` / `#D9D2F7` / `#C9BEF2` | violet borders, focus/hover ring on cards |
| Accent gradient | `linear-gradient(135deg,#6C5BD9,#9B8CF0)` | avatars, profile hero badge |
| Accent gradient (bars) | `linear-gradient(90deg,#6C5BD9,#9B8CF0)` | in-progress progress bars |

**Dark / inverse**
| Token | Hex | Usage |
|---|---|---|
| Dark surface (buttons, active nav, FAB) | `#1C1C20` | active nav pill, "Call" button, floating action bar, Hot/active segment, Light-theme chip |
| Dark hover | `#000` | dark button hover |
| Dark hero gradient | `linear-gradient(120deg,#201F2C,#2E2A4A)` | featured-mentor banner |
| Profile cover gradient | `linear-gradient(120deg,#201F2C,#3A3358)` | profile header band |
| Mentor avatar gradient | `linear-gradient(135deg,#7C5CFC,#B79BFF)` | featured-mentor avatar |
| FAB muted label | `#8E8E98` | "start new lesson" eyebrow on FAB |

**Semantic colors**
| Meaning | Text | Fill | Usage |
|---|---|---|---|
| Success / done / online | `#1F9E5A` (also `#34A85A`, `#34C36B`) | `#EAF7EF` / `#E9F7EF` | completed pills, "SOLVED", online dot (`#34C36B`), 100% bars (`#34C36B`), check icons. `#34C36B` is the brighter "node/dot" green; `#1F9E5A` the text green. |
| Warning / medium difficulty | `#B5840F` / `#C79212` / `#9A7B2E` | `#FFF8E6` / `#F4F0E6` | "Medium" badge, best-record stat |
| Danger / hard / logout | `#D9503E` (also `#D9722E` orange) | `#FDECEA` / `#FCEEEA` / `#FFF1E8` | "Hard" badge, logout button, "PINNED" (orange `#D9722E` on `#FFF1E8`) |
| Info / blue | `#2F6FE0` / `#4C8DF6` / `#4658D6` | `#EAF2FF` / `#EEF0FE` | blue chips, lessons-done stat, "Preparation" group dot (`#4C8DF6`) |

**Chip color map** (lesson-task chips, `chipMap()`):
`choice` `#EAF2FF`/`#2F6FE0` · `blank` `#E9F7EF`/`#1F9E5A` · `reorder` `#FFF1E8`/`#D9722E` ·
`match` `#F1ECFB`/`#6C5BD9` · `categorize` `#FDEBF3`/`#C44E8A` · `spot` `#FDECEA`/`#D9503E` ·
`rewrite` `#EAF6F8`/`#1E8AA0` · `dialogue` `#EEF0FE`/`#4658D6` · `evaluate` `#F4F0E6`/`#9A7B2E` ·
`free` `#EFEFF2`/`#6A6A72`.

**Avatar gradient palette** (`ava()`, seeded hash → one of 7 pairs):
`[#6C5BD9,#9B8CF0]`, `[#4C8DF6,#7FB0FA]`, `[#E16BA0,#F09BC2]`, `[#2FB36F,#73D6A0]`,
`[#F0863C,#F7B07A]`, `[#1E9FB0,#6FCBD6]`, `[#8A5BD9,#B79BFF]`. Text always `#fff`.

**Funnel-stage dot colors** (Learning Path groups):
Preparation `#4C8DF6` · Discovery `#6C5BD9` · Engagement `#E16BA0` · Closing `#F0863C` · Retention `#1E9FB0`.

### 1.2 Typography
- **Family:** `'Hanken Grotesk', system-ui, -apple-system, sans-serif` (Google Fonts).
- **Weights loaded:** 400, 500, 600, 700, 800.
- **Smoothing:** `-webkit-font-smoothing: antialiased; text-rendering: optimizeLegibility`.
- **Size scale (observed):**
  | Role | Size | Weight | Letter-spacing |
  |---|---|---|---|
  | Profile / Settings display | 22px | 800 | -.02em |
  | Skill header title | 21px | 800 | -.02em |
  | Screen H1 (Practice/Reference/Friends/Discuss) | 20px | 800 | -.02em |
  | Mentor name / panel title | 18px | 800 | — |
  | Stat tile value | 23px | 800 | -.02em |
  | Skill stat value | 17px | 700 | -.01em |
  | Card title (bundles/techniques/threads) | 15.5px | 700 | -.01em |
  | Section header | 14–15px | 700 | — |
  | Lesson title | 15px | 700 | -.01em |
  | Body / description | 13–13.5px | 400–500 | — |
  | Meta / list secondary | 12–12.5px | 500–600 | — |
  | Eyebrow / uppercase label | 11–12.5px | 700 | +.03em / +.04em, `text-transform:uppercase` |
  | Chip / pill | 10.5–11.5px | 600–800 | up to +.04em |
  | Smallest tags (`LESSON n`, "NEW") | 10.5–11px | 700–800 | +.04em |
- Body line-height for paragraphs: `1.5`–`1.6`; chip/bubble text `1.45`.

### 1.3 Radii
| Element | Radius |
|---|---|
| Outer app frame | `22px` |
| Large hero/banner (mentor, profile badge) | `18px` / `22px` |
| Big cards (skill header, profile cards, settings cards) | `16px` |
| Standard cards (bundles, techniques, lessons, list containers) | `14px`–`15px` |
| Inner panels / detail boxes | `13px` |
| Inputs / search | `11px` |
| Buttons | `9px`–`11px` |
| Nav pill (rail item) | `12px` |
| Avatar button (rail) | `14px` outer / `11px` inner |
| Avatar (list, 34px) | `10px`; small (20–30px) `6px`–`9px` |
| Chips / pills / small badges | `6px`–`8px` |
| Count badge | `5px` |
| Progress bars / track | `7px`–`8px` |
| Toggle switch | `24px` (pill) |
| Chat bubble | `14px 14px 4px 14px` (me) / `14px 14px 14px 4px` (them); example bubbles use `13px…` |

### 1.4 Shadows
| Use | Value |
|---|---|
| App frame | `0 14px 54px rgba(28,28,58,.11)` |
| Card resting | `0 1px 2px rgba(20,20,40,.04)` (also `.03`) |
| Card hover (violet lift) | `0 6px 20px rgba(108,91,217,.10)` (lessons `0 4px 16px rgba(108,91,217,.10)`; technique selected `0 6px 20px rgba(108,91,217,.12)`) |
| Floating action bar (FAB) | `0 12px 34px rgba(20,20,40,.28)` |
| Profile hero badge | `0 6px 18px rgba(108,91,217,.3)` |
| In-progress lesson node glow | `0 0 0 4px rgba(108,91,217,.16)` (ring) |
| Active skill row | `0 1px 2px rgba(108,91,217,.08)` |

### 1.5 Spacing & layout rhythm
- Outer page padding `26px`; app frame `max-width:1380px`, `min-width:1120px`,
  height `calc(100vh - 52px)` clamped `640px`–`880px`.
- Screen header padding ~`20px 26-30px`; scroll body padding ~`22-24px 26-30px`,
  Path body adds `padding-bottom:96px` to clear the FAB.
- Card internal padding `16-22px`. Gaps between cards/grid items `10-16px`.
- Stat-tile grids: 4-up `grid-template-columns:repeat(4,1fr)`.
- Auto-fill card grids: `repeat(auto-fill,minmax(280-300px,1fr))` (Practice/Reference),
  friends `minmax(230px,1fr)`.

### 1.6 Scrollbar
```
::-webkit-scrollbar { width:9px; height:9px; }
::-webkit-scrollbar-thumb { background:#D7D7DF; border-radius:9px; border:2px solid transparent; background-clip:padding-box; }
::-webkit-scrollbar-thumb:hover { background:#C2C2CC; }
::-webkit-scrollbar-track { background:transparent; }
```
`input::placeholder { color:#B4B4BC; }`

---

## 2. App Shell — Left "Sticker Rail" Nav

- **Container:** `width:72px; flex:none; border-right:1px solid #F0F0F3; background:#FCFCFD;`
  `display:flex; flex-direction:column; align-items:center; padding:16px 0 18px; gap:9px;`
- **Order (top → bottom):** Avatar button → 24px divider line (`#ECECEF`, `margin:6px 0 5px`) →
  Learning Path → Practice → Reference → Friends → Discuss → (spacer) → **Settings pinned at bottom**
  (Settings item gets `marginTop:auto`).
- **No "League" item. No leaderboard nav.** Profile is reached only via the avatar
  button at the top (no dedicated profile nav icon).

**Avatar button** (`avatarBtn` / `avatarInner`):
`44×44px`, `border-radius:14px`, `padding:2px`, transparent bg; border is
`2px solid #6C5BD9` when on Profile screen else `2px solid transparent`.
Inner: `border-radius:11px`, gradient `linear-gradient(135deg,#6C5BD9,#9B8CF0)`,
white initials ("AK"), `font-weight:800; font-size:14px`.

**Nav item** (`navStyle(id)`): `42×42px`, `border-radius:12px`, no border, centered,
`cursor:pointer`, `transition:background .15s,color .15s`.
- **Active:** `background:#1C1C20; color:#FFFFFF` (dark pill).
- **Idle:** `background:transparent; color:#A2A2AA`.
- **Hover (idle):** `background:#F1F1F4` (via `style-hover`).
Each icon is a 20×20 (Settings 19×19) inline SVG, `stroke:currentColor`,
`stroke-width:1.7`, round caps/joins.

**Icons (briefly):**
- *Learning Path* — two small circles (top-left, bottom-right) joined by an S-curved path (a winding route/node graph).
- *Practice* — a speech/chat bubble with a small clock hand inside (conversation + time).
- *Reference* — an open book / bookmark spine (left edge folded), a library tome.
- *Friends* — two people: one head+shoulders circle in front, a partial second person behind (group).
- *Discuss* — a rounded speech bubble with a tail and two text lines inside (forum/comment).
- *Settings* — classic gear/cog (circle center + notched outer ring).

---

## 3. Per-Screen Layout Specs

Screen routing is single-state (`state.screen`): `path | practice | ref | friends | discuss | profile | settings`.
Main area is `flex:1` to the right of the rail.

### 3.1 PATH (Learning Path) — 3-column
`[256px categories] | [center skill detail, flex:1] | [300px overview]`

**Left categories column** (`width:256px`, right border `#F0F0F3`):
- Header row: title "Learning Path" (15.5px/700) + a small pill showing the active
  funnel stage (`#F4F4F7` bg, `#ECECEF` border, ring dot in `#6C5BD9`).
- Scrollable accordion of **funnel stage groups** (Preparation, Discovery, Engagement,
  Closing, Retention). Each group: colored 8px square dot + UPPERCASE stage name
  (`#9A9AA2`, 11.5px/700, +.04em) + count badge (`#F1F1F4`, 10.5px/700) + chevron
  (rotates 180° when open).
- Expanded group lists **skills** as rows: 17px circular **status node** +
  name + optional "now" tag. Node states: done = `#34C36B` filled with ✓; in-progress
  = `#6C5BD9` filled; available = white with `2px solid #DEDEE4` border. Active row:
  `background:#F5F3FD`, `border:1px solid #E2DCF7`, label `#3A2E8C` 700, soft violet shadow.

**Center column** (flex:1, `position:relative`, canvas `#FBFBFC`):
- Breadcrumb header: stage name → chevron → violet skill-name pill (`#EDEAFB`/`#6C5BD9`)
  + right-aligned "lessons completed" meta pill.
- **Skill header card** (`#FFF`, `border-radius:16px`): title 21px/800 + summary;
  status badge ("Mastered/In progress/Available", violet tint); a progress bar
  (track `#EFEFF2`, fill violet gradient) + % label; a **4-cell stat grid** (1px-gap
  hairline grid over `#EFEFF2`) showing **Lessons / Completed / Accuracy / Time spent**.
- **Lessons timeline:** vertical connector line (`#ECECEF`, 2px) with 36px circular
  nodes (completed green ✓ / in-progress violet with glow ring / available white #).
  Each lesson card: `LESSON n` eyebrow + status pill + title + **task-type chips**
  (from chipMap) + right-side action button (Review / Resume / Start). In-progress
  card highlighted with `#E2DCF7` border + violet shadow.
- **Floating action bar (FAB):** centered bottom, dark `#1C1C20`, rounded 14px,
  shadow `0 12px 34px rgba(20,20,40,.28)`; eyebrow "Start new lesson" + lesson name +
  violet "Start →" button.

**Right overview panel** (`width:300px`, left border):
- Header with clock icon + "Overview".
- "About this skill" paragraph; "What you'll learn" check-list (green `#EAF7EF` check
  chips); "Related techniques" rows (chevron, hover violet) linking to Reference.

**Gamification signals here:** progress %, accuracy stat (92%), time-spent, lesson
completion counts. **No XP, no streak on this screen, no hearts.**

### 3.2 PRACTICE (AI Practice) — single column
Header: "AI Practice" 20px/800 + subtitle. Scroll canvas `#FBFBFC`:
- **Featured-mentor banner:** dark gradient `linear-gradient(120deg,#201F2C,#2E2A4A)`,
  rounded 18px, white text; 64px gradient avatar ("DK"), "FEATURED MENTOR" eyebrow
  (translucent white pill), mentor name + blurb, white "Start voice call" button
  (mic icon).
- **Dialog bundles** — section label + auto-fill grid (`minmax(280px,1fr)`) of bundle
  cards: 42px tinted icon-square (abbrev initials) + difficulty badge (Easy/Medium/Hard);
  title + desc; a violet skill pill + "N modes"; footer with two buttons split by a
  top hairline — **Chat** (light violet `#F5F4FB`/`#5C4CCB`) and **Call** (dark `#1C1C20`).
- **Recent sessions** — a single bordered card containing rows: tinted 38px session icon,
  mode title, "bundle · N messages · Voice call/Text chat" meta, timestamp, and "Reopen →".

**Gamification:** difficulty tiers; session history with message counts and voice/chat
kind. Voice practice is surfaced here (and quota'd on Profile). No XP/streak/hearts.

### 3.3 REFERENCE (Technique Library) — main + slide-in detail
Left/main column:
- Header: "Technique Library" 20px/800 + subtitle; a **search field** (`#F4F4F7` bg,
  `#ECECEF` border, radius 11, magnifier icon, max-width 420px) + a row of quick **tag
  chips** (Discovery/Objections/Closing/Voice).
- **Technique cards grid** (`minmax(300px,1fr)`): difficulty badge + optional green
  "NEW" badge; name 15.5px/700; description; hashtag tag chips (`#F4F4F7`); footer with
  violet skill name + "Read →". Selected card: `#C9BEF2` border + violet shadow.
- Empty state: centered "No techniques match …".

**Right detail panel** (`width:392px`, conditionally rendered on selection):
- Header "TECHNIQUE" + close (×) button.
- Difficulty + NEW + skill pills; technique name 21px/800; hashtag chips.
- "How it works" paragraph.
- **Example dialogue**: chat-bubble transcript inside a `#FBFBFC`/`#EFEFF2` box —
  "You" bubbles violet tint (right-aligned), "Client" bubbles white/bordered (left),
  with optional violet "↳ note" annotation.
- "Case study" text + **two metric tiles** (violet `#F5F4FB`/`#ECE9F9`, big `#5C4CCB`
  value + label).
- **Coach** block: 40px gradient avatar + name; italic quote in violet box `#F7F6FD`;
  a challenge callout row (arrow icon); full-width dark "Practice this technique" button
  (routes to Practice).

### 3.4 FRIENDS — main list + 330px right rail (chat OR activity)
Left/main column (right border):
- Header "Friends" 20px/800 + search field "Find people by name…".
- **Requests** section (uppercase label): rows in white cards — gradient avatar,
  name + mutual line, violet **Accept** + grey **Decline** buttons.
- **All friends** section: auto-fill grid (`minmax(230px,1fr)`) of friend cards —
  avatar with **online/offline status dot** (`#34C36B` online / `#C8C8D0` offline,
  bottom-right, white ring), name + focus line, and a small message-icon button
  (opens chat).

Right rail (`width:330px`) shows **one of two** states:
- **Activity** (default, when no chat open): "Activity" header + feed rows
  (30px avatar, "**Name** did X", relative time).
- **Chat** (when a friend is opened): back button + avatar + name + green "Online";
  scrollable message thread (me = violet `#6C5BD9` bubble right; them = `#F1F1F4`
  bubble left); composer input (`#F7F7FA`) + violet send button (paper-plane icon).

**Gamification:** social only — activity feed references completing skills / practice /
questions. No leaderboard, no points shown on friends.

### 3.5 DISCUSS — main feed + 264px right sidebar
Left/main column (right border):
- Header: "Discuss" 20px/800 + subtitle, and a violet **New question** button (+ icon).
- **Sort segmented control** row: Hot / New / Unanswered (active = dark `#1C1C20`,
  idle = `#F4F4F7`/`#6A6A72`).
- **Thread list** rows (separated by `#F0F0F3` hairlines): left a vertical **upvote**
  control (up-chevron button + bold vote count, 44px wide); right the body — optional
  "PINNED" (orange) / "SOLVED" (green) badges, title 15.5px/700 (hover violet),
  one-line excerpt, then a meta row of hashtag chips (`#EFEAFE`/`#6C5BD9`), tiny author
  avatar + "author · time", and a right-aligned reply count with bubble icon.

Right sidebar (`width:264px`, bg `#FBFBFC`):
- **Community** stat card: Threads / Replies / Solved rate (green value).
- **Popular tags** wrap of hashtag chips.
- **Top authors this week** list: 28px avatar + name + violet points.

### 3.6 PROFILE — centered single column (max-width 860px)
- **Cover band:** 120px tall, gradient `linear-gradient(120deg,#201F2C,#3A3358)`.
- **Identity row** (pulled up `-42px`): 86px rounded-22 gradient avatar with 4px
  `#FBFBFC` ring + violet shadow; name 22px/800 + "role · email"; right "Edit profile"
  outline button.
- **Stat tiles** — 4-up grid of white cards, each: colored icon + grey label + big
  23px/800 value:
  - **Accuracy** 92% (green target icon)
  - **Best record** 21 days (amber trophy icon) — *this is the streak/days signal*
  - **Skills done** 3 (violet check icon)
  - **Lessons done** 14 (blue book icon)
- **Two-column row** (`1.4fr 1fr`):
  - **Enrolled skills** card: per-skill name + % + progress bar (100% = solid green
    `#34C36B`, else violet gradient). "Manage" links to Path.
  - **Voice minutes** card: **Today 12/30 min** (violet bar 40%) and
    **This month 186/500 min** (green bar 37%) + note that quota resets daily/monthly.

**Gamification present:** accuracy %, best streak ("21 days"), skills/lessons completed,
per-skill progress, **voice quota** (daily + monthly). **Confirmed ABSENT: no leagues/
leaderboard, no achievements/badges grid, no hearts/lives, no XP counter.**

### 3.7 SETTINGS — centered single column (max-width 640px)
- Header "Settings" 22px/800 + subtitle.
- **Appearance** card: a 2-segment Light / Dark selector (Light active = dark
  `#1C1C20` chip; Dark idle = `#F4F4F7`).
- **Notifications** card: two **toggle rows** (label + sub-label) with pill switches —
  ON = violet `#6C5BD9` with knob right; OFF = grey `#D8D8DF` with knob left
  (`42×24px` track, 20px white knob).
- **Account** card: rows for Email (Change), Password (Update), and **Admin area**
  ("Open →") — each a label + sub-label + violet text-link / link-with-arrow.
- **Log out** button: full-width danger tint `#FCEEEA` / text `#D9503E`,
  hover `#FAE2DC`.

---

## 4. Reusable Components

| Component | Spec |
|---|---|
| **Primary button** | violet `#6C5BD9`, white text, radius 10–11px, padding ~`9-12px 17-18px`, 13px/700, hover `#5C4CCB`. Often trailing arrow icon. |
| **Dark button** | `#1C1C20`/white, radius 9–11px, hover `#000` (Call, Practice-this-technique, FAB Start-on-dark). |
| **Light/secondary button** | `#F4F4F7` bg, `#6A6A72` text, `1px solid #ECECEF`, radius 9–11px, hover `#EDEDF0` (Decline, Dark theme chip). |
| **Soft-violet button** | `#F5F4FB` bg, `#5C4CCB` text, `1px solid #ECE9F9`, radius 9px, hover `#EDEAFB` (Chat). |
| **Outline button** | white, `1px solid #E4E4E9`, `#3A3A40` text, hover border/text `#6C5BD9` (Edit profile). |
| **Text link** | `#5C4CCB`, 12.5px/700, no bg (Manage, Change, Update). |
| **Icon button (small)** | `30×30px`, white, `1px solid #ECECEF`, radius 8px, `#9A9AA2` icon, hover `#F4F4F7`/darker (close, back, message). |
| **Difficulty badge** | Easy `#EAF7EF`/`#1F9E5A`, Medium `#FFF8E6`/`#B5840F`, Hard `#FDECEA`/`#D9503E`; 10.5px/800, radius 6px, padding `3px 8px`. |
| **Status pill (lessons)** | Completed `#EAF7EF`/`#1F9E5A`, In progress `#EFEAFE`/`#6C5BD9`, Not started `#F1F1F4`/`#9A9AA2`; 10.5px/700, radius 6px. |
| **Hashtag chip** | `#F4F4F7` (or `#EFEAFE` violet variant) bg, `#6A6A72`/`#6C5BD9` text, 11px/600, radius 6px, padding `3px 8px`, prefixed `#`. |
| **Quick-tag chip (clickable)** | `#F4F4F7`/`#ECECEF` border, 12px/600 `#6A6A72`, radius 8px, hover border `#D9D2F7` + text `#5C4CCB`. |
| **Count/number badge** | `#F1F1F4` bg, `#A8A8B0` text, 10.5px/700, radius 5px, padding `1px 6px`. |
| **"NEW" badge** | `#EAF7EF`/`#1F9E5A`, 10.5px/800, +.04em, radius 6px. |
| **Standard card** | `#FFF`, `1px solid #ECECEF`, radius 14–15px, shadow `0 1px 2px rgba(20,20,40,.04)`, hover border `#D9D2F7` + shadow `0 6px 20px rgba(108,91,217,.10)`. |
| **Stat tile** | white card, radius 14px, padding `16px 18px`: colored icon + grey label + 23px/800 value. |
| **List row** | flex, gap ~14px, padding ~`14px 18px`, bottom hairline `#F4F4F6`, hover `#FAFAFC`. |
| **Section header** | 14px/700 `#3A3A40`, or uppercase eyebrow 11.5–12.5px/700 `#9A9AA2` +.03em. |
| **Search input** | `#F4F4F7` bg, `1px solid #ECECEF`, radius 11px, padding `10px 14px`, magnifier icon, 13.5px text, placeholder `#B4B4BC`. |
| **Text input (chat)** | `1px solid #ECECEF`, `#F7F7FA` bg, radius 11px, padding `10px 13px`, 13px. |
| **Toggle switch** | `42×24px` track radius 24px; ON `#6C5BD9` knob-right, OFF `#D8D8DF` knob-left; 20px white circular knob, 2px inset. |
| **Avatar** | gradient (seeded 7-palette), 34px default radius 10px / 40px radius 12px (coach) / 20–30px small radius 6–9px; white initials 700–800. Profile hero 86px radius 22. |
| **Progress bar** | track `#EFEFF2` (or `#EFEFF2`/8px), fill violet gradient `linear-gradient(90deg,#6C5BD9,#9B8CF0)` or solid `#34C36B` at 100%, radius 7–8px. |
| **Chat bubble** | me = `#6C5BD9`/white, them = `#F1F1F4`/`#2A2A30`; asymmetric radius `14px 14px 4px 14px` / `14px 14px 14px 4px`; max-width 76%; 13px/1.45. |
| **Segmented control** | active = `#1C1C20`/white; idle = `#F4F4F7`/`#6A6A72` `1px solid #ECECEF`; radius 9px, padding `7px 16px`, 12.5px/700. |

---

## 5. Notes for implementation
- The whole app is a **fixed rounded "device frame"** centered on a grey backdrop —
  not a full-bleed page. Width clamps `1120–1380px`.
- Accent is consistently `#6C5BD9` (primary) / `#5C4CCB` (hover/link text). Dark
  `#1C1C20` is the secondary emphasis color (active states, high-contrast buttons, FAB).
- Hover affordance on cards is a **violet lift**: border → `#D9D2F7`, shadow → violet.
- Avatars, stage dots, and chip colors are **algorithmically/seeded**, not fixed per
  entity — reproduce the palettes and the hash in `ava()` / `chipMap()`.
- Confirmed gamification surface: **XP is NOT shown anywhere; streak is "Best record
  N days" on Profile; accuracy %, completion counts, and voice quota are present.**
  **No leagues, no achievements grid, no hearts/lives anywhere.**
