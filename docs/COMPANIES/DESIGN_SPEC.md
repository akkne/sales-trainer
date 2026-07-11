# Companies (Компании) — Design Spec

Implementation-ready UI spec for the new **Companies** tab. Harmonizes with
`docs/REDESIGN_V2/DESIGN_SPEC.md` (violet `#6C5BD9`, Hanken Grotesk, 72px sticker
rail, card patterns) and reuses the CSS tokens/classes already in
`src/frontend/app/globals.css`.

> Production UI language is **Russian**. All user-facing strings in this spec are
> Russian. Code identifiers / class names stay Latin. New CSS classes are prefixed
> `co-`.

---

## 0. Design decisions (rationale)

- **A private CRM-lite, not a gamified surface.** No XP, no streaks, no difficulty
  tiers. This tab is the user's real-world prospect ledger. Tone: calm, clerical,
  trustworthy — closer to Profile/Settings than to Practice.
- **List page = compact list, not a card grid.** Companies are personal records
  the user scans by name (like a contact list), not a catalog to browse. A dense
  row list with search reads faster and scales to dozens of entries. (Bundle-style
  card grid is reserved for content the product curates.)
- **Company page = single centered column** (max-width `860px`, mirroring Profile),
  not a 3-column app-screen. A prospect record is a document the user reads/edits
  top-to-bottom, so a document-width column with stacked sections is the right
  altitude. Desktop does **not** split into 3 columns; see §7.
- **Practice call reuses the existing full-screen voice UI verbatim.** We only
  design (a) the entry CTA on the company page and (b) a lightweight **pre-call
  goal step** (inline panel, not a separate screen). The goal + company description
  are handed to a dedicated company-voice route that renders the *same*
  `voice/page.tsx` experience.
- **Combined chronological timeline** on the company page mixes practice calls and
  real-call logs into one reverse-chronological feed, with a segmented filter
  (Все / Тренировки / Реальные) so the user can narrow it. This is the "story of
  the account" — the single most valuable view. (Requirement #6 → adopted.)
- **Accent language:** practice = violet (`--primary`); real-world log = success
  green (`--success`); this color split lets the user parse the timeline at a glance.

---

## 1. Navigation

### 1.1 Route
`/companies` (list) and `/companies/[id]` (company page). New pre-call goal step is
**inline** on the company page (no dedicated route). The handoff to the call uses a
new route `/companies/[id]/call/voice` (and optionally `/companies/[id]/call/chat`),
see §6.

### 1.2 Rail icon — `briefcase` (add to `IconName` union in `icon.tsx`)

20×20 within the 24×24 viewBox, `stroke:currentColor`, `stroke-width:1.5`, round
caps/joins (matches every other rail icon). Briefcase silhouette:

```jsx
case "briefcase":
    return (
        <svg {...svgProps}>
            {/* body */}
            <rect x="3" y="7" width="18" height="13" rx="2" />
            {/* handle */}
            <path d="M8 7V5a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
            {/* latch line across the body */}
            <path d="M3 12h18" />
        </svg>
    );
```
Add `briefcase: "briefcase"` to the `ICON_NAMES` map for consistency.

### 1.3 Rail placement (`nav-rail.tsx` → `RAIL_ITEMS`)

Insert **after Practice, before Guidebook** — practice → companies is the natural
"train, then go do it for real" adjacency.

```ts
const RAIL_ITEMS: RailItem[] = [
    { href: "/tree",      icon: "compass",   label: "Путь" },
    { href: "/dialog",    icon: "message",   label: "Практика" },
    { href: "/companies", icon: "briefcase", label: "Компании" },   // NEW
    { href: "/guidebook", icon: "book",      label: "Справочник" },
    { href: "/friends",   icon: "users",     label: "Друзья" },
    { href: "/discuss",   icon: "forum",     label: "Обсуждения" },
];
```
Active state uses existing `.rail-item.active` (dark `#1C1C20` pill). No badge.

### 1.4 Mobile bottom nav (`bottom-nav.tsx` → `NAV_ITEMS`)

The bottom bar shows max 5 items and currently holds Path / Practice / Guidebook /
Friends / Profile. **Replace Guidebook with Companies** on mobile (Guidebook stays
reachable from Path/rail; Companies is higher-value for the on-the-go seller):

```ts
const NAV_ITEMS = [
    { href: "/tree",      icon: "compass",   label: "Путь" },
    { href: "/dialog",    icon: "message",   label: "Практика" },
    { href: "/companies", icon: "briefcase", label: "Компании" },   // NEW (replaces guidebook)
    { href: "/friends",   icon: "users",     label: "Друзья" },
    { href: "/profile",   icon: "user",      label: "Профиль" },
];
```
Uses existing `.bottom-nav a` / `.bottom-nav a.active` styling.

---

## 2. Screen — `/companies` (companies list)

### 2.1 Layout
Single column inside the standard `.page > .container` wrapper (same as Practice).
Reuse `.practice-header` for the title block.

```
.page
  .container
    header (.practice-header)         → title + subtitle + right-aligned "+ Добавить"
    toolbar (.co-list-toolbar)        → search field (left) + count (right)
    list   (.co-list)                 → rows (.co-row) OR empty/loading/error
```

### 2.2 Header
- Title: `.practice-title` → **«Компании»**
- Subtitle: `.practice-subtitle` → **«Ваши реальные клиенты: описание, тренировки перед звонком и журнал встреч»**
- Right-aligned primary button (`.btn .btn-primary`) with plus icon → **«Добавить компанию»**
  opens the Create modal (§5.1). On mobile the button drops below the subtitle,
  full-width (`.btn-block`).

Header row structure:
```jsx
<div className="practice-header co-list-head">
  <div>
    <h1 className="practice-title">Компании</h1>
    <p className="practice-subtitle">Ваши реальные клиенты: описание, тренировки перед звонком и журнал встреч</p>
  </div>
  <button className="btn btn-primary" onClick={openCreate}>
    <Icon name="plus" size={16} /> Добавить компанию
  </button>
</div>
```

### 2.3 Toolbar
- Search: reuse `.field-search` wrapper (magnifier absolutely positioned, same as
  Guidebook/Reference). Placeholder **«Поиск по названию…»**. `max-width: 420px`.
  Client-side filter on company name (case-insensitive substring).
- Right side, muted count: **«N компаний»** in `.co-list-count` (see plural note §8).

### 2.4 Row (`.co-row`) — one company
Left→right: avatar square · name + meta · trailing chevron. Entire row is a
`<Link href={/companies/[id]}>`. Reuses the seeded-gradient avatar (`ava()` +
`initials()` copied from `dialog/page.tsx`, seed = company `id`).

```
[ 40px avatar ]  Название компании                         12 → chevron-right
                 3 тренировки · 2 звонка · обновлено 2 д назад
```
- **Avatar:** 40px, `border-radius: 11px`, gradient `linear-gradient(135deg,from,to)`,
  white initials 700/14px. (Same recipe as `.session-icon-sq`.)
- **Name:** 15.5px/700, `-0.01em`, `--ink-heading`. Truncate with ellipsis.
- **Meta line:** 12.5px/500, `--ink-4`. Composed of present facts only, `·`-joined:
  practice count, real-call-log count, relative "обновлено …". Omit zero counts.
- **Trailing:** `Icon name="chevron-right" size={18}` in `--ink-4`.
- Row hover = violet lift on the container (`.co-list` is the bordered card; rows
  separated by hairlines, same pattern as `.sessions-card` / `.session-row`).

### 2.5 The list container
Reuse the `.sessions-card` visual (white card, `--line` border, `--r-md`, `--sh-1`)
renamed `.co-list`; each `.co-row` mirrors `.session-row` (flex, gap 14px, padding
`14px 18px`, bottom hairline `--line-2`, hover `--surface-2`, last-child no border).

### 2.6 States
- **Loading:** `.co-list` card containing 5 skeleton rows: each row = `Skeleton 40×40
  rounded 11` + two stacked line skeletons (`160×13`, `220×11`). Header skeletons
  as in Practice.
- **Error:** reuse `<ErrorState title="Не удалось загрузить" message={err.message}
  onRetry={refetch} />` inside `.page` with `padding:60px 24px`.
- **Empty (no companies yet):** reuse `.empty` block:
  - icon: `.empty .ic` with `Icon name="briefcase" size="lg"`
  - `.h3` → **«Пока нет ни одной компании»**
  - `.small` → **«Добавьте компанию, которой планируете позвонить — и потренируйтесь перед реальным разговором»**
  - primary button (`.btn .btn-primary`, centered, `margin-top:20px`) → **«Добавить первую компанию»** (opens Create modal).
- **Empty (search returns nothing):** `.empty` with `.small` → **«Ничего не найдено по запросу «{q}»»** (no button, no big icon — mirror Reference's empty search).

---

## 3. Screen — `/companies/[id]` (company page)

### 3.1 Layout — centered single column, `max-width: 860px`

Wrap the scroll body in `.co-page` (`max-width:860px; margin:0 auto; padding:22px
clamp(16px,3.5vw,30px) 64px`). Vertical section stack, gap `20px`:

```
back link (.back-link)                → «← К списку»
identity header (.co-header)          → avatar 64 · name (editable) · meta · actions
── practice CTA panel (.co-cta) ──     → goal input + "Позвонить" (see §4)
── description card (.co-card) ──      → free-form editor (§3.3)
── timeline card (.co-card) ──         → segmented filter + feed (§3.4)
```
Real-call "add log" lives as a compact form at the top of the timeline card
(§3.5). Practice history and real logs are unified in the timeline (§3.4); a
filter narrows to one kind.

### 3.2 Identity header (`.co-header`)
```
[ 64px avatar ]  Название компании  ✎                 [ ⋯ menu ]
                 3 тренировки · 2 реальных звонка
```
- Avatar: 64px, `border-radius:16px`, seeded gradient, initials 800/22px.
- Name: `.h3` (21px/800). A small ghost edit affordance: `Icon name="edit" size={15}`
  in `--ink-4`, clickable → opens Edit-name inline (input replaces the `<h1>`; Enter
  saves, Esc cancels) OR the Edit modal (§5.1, reused as edit mode). Prefer inline
  rename for name; the `⋯` menu covers destructive/edit-all.
- Meta line under name: 12.5px/500 `--ink-4`, present-facts only.
- Right: `.icon-btn` with a vertical-dots affordance. Since `icon.tsx` has no
  "dots" icon, use `Icon name="settings"` is wrong here — instead render a **⋯**
  as three 3px dots via a tiny inline element, or reuse `Icon name="edit"`.
  **Decision:** use two buttons instead of a menu to avoid a new icon:
  - `.icon-btn` `Icon name="edit"` → Edit company modal (§5.1), `aria-label="Редактировать компанию"`
  - `.icon-btn` `Icon name="delete"` (`--heart` on hover) → Delete confirm (§5.3), `aria-label="Удалить компанию"`

### 3.3 Description card (`.co-card` + `.co-desc`)
The free-form context that is injected into the AI roleplay prompt.

- Card header row: eyebrow (`.eyebrow`) **«ОПИСАНИЕ КОМПАНИИ»** on the left; on the
  right a `.btn-link` toggling read ↔ edit (**«Изменить»** / while editing:
  **«Сохранить»** + a `.btn-link` **«Отмена»**).
- **Read mode:** paragraph, 13.5px/1.6, `--ink-secondary`, `white-space:pre-wrap`.
  If empty → dashed placeholder box (`.co-desc-empty`): 1px dashed `--line`,
  `--r-sm`, padding 16px, centered `--ink-4` text **«Добавьте описание: кто эта
  компания, что продаёт, кто ЛПР, боли и контекст. Это описание получит ИИ‑собеседник
  во время тренировки.»** + a `.btn-soft` **«Добавить описание»**.
- **Edit mode:** `<textarea class="field co-textarea">` (min-height 160px,
  `padding:12px 14px`, `line-height:1.6`, resize vertical), placeholder same helper
  text as above. Below it a helper caption (11.5px `--ink-4`): **«Это описание ИИ
  использует как роль собеседника на тренировочных звонках.»**
- Autosave-on-blur is acceptable; explicit «Сохранить» is the primary path.

### 3.4 Timeline card (`.co-card` + `.co-timeline`)
Header row: eyebrow **«ИСТОРИЯ»** left; right a **segmented control**
(`.co-seg`, styled per §8.2 / DESIGN_SPEC segmented control):
**Все · Тренировки · Звонки** (active = dark `#1C1C20`).

Below the header, when the "Звонки"/"Все" filter is active, the **add-log form**
(§3.5) sits collapsed as a single «+ Записать звонок» trigger button
(`.btn-soft`, full width, dashed variant `.co-add-log`) that expands into the
3-field form. (When filter = "Тренировки", the add-log trigger is hidden.)

**Feed** = reverse-chronological list of items, one `.co-tl-item` per entry,
connected by a 2px vertical rail (`--line`, absolutely positioned at left 19px),
mirroring the lesson-timeline connector language but simpler.

Two item kinds:

**(a) Practice entry** — `.co-tl-item.practice`
- Node: 14px dot, `--primary` fill, white ring (`box-shadow:0 0 0 3px var(--surface)`).
- Row body (a bordered `.co-tl-card`, hover = violet lift):
  - top: `pill-inprogress`-style violet pill **«Тренировка»** + goal text (if the
    call had a goal) as a `.chip` + right-aligned relative timestamp (12px `--ink-4`).
  - title 14px/700 `--ink-heading`: the goal, else **«Тренировочный звонок»**.
  - meta 12px `--ink-4`: **«Голосовой звонок · N реплик · MM:SS»** (present facts only).
  - if feedback exists: a 1-line score/summary + `.btn-link` **«Разбор →»** opening
    the existing `FeedbackModal` in read-only mode (reuse `features/dialog/components/feedback-modal.tsx`).

**(b) Real-call log entry** — `.co-tl-item.reallog`
- Node: 14px dot, `--success` fill, white ring.
- Row body (`.co-tl-card`):
  - top: green pill **«Реальный звонок»** (`.co-pill-real`, `--success-soft`/`--success`)
    + right-aligned date (the user-entered date, formatted `d MMM yyyy`) + hover-reveal
    `.icon-btn` edit + delete (`aria-label` «Редактировать запись» / «Удалить запись»).
  - three labeled mini-blocks stacked (`.co-log-field`):
    - **«С кем говорил»** (eyebrow-mini) → value 13.5px `--ink-secondary`
    - **«О чём был разговор»** → value
    - **«К чему пришли»** → value (rendered with a subtle left accent bar
      `2px solid --success` since it's the outcome — the money line)
  - Empty individual fields are omitted (only render blocks with content), but
    "с кем" is required on creation (§3.5).

### 3.5 Add / edit real-call log form (`.co-log-form`)
Inline expandable panel (not a modal on desktop — keeps the user in context; on
mobile it may present as the same fields in a full-width modal, see §7). Fields:

1. **«С кем говорил»** — `<input class="field">`, required, placeholder
   **«Имя и должность, напр. Иван, руководитель отдела закупок»**.
2. **«О чём был разговор»** — `<textarea class="field co-textarea">` (min-height 88px),
   placeholder **«Кратко о ходе разговора»**.
3. **«К чему пришли»** — `<textarea class="field co-textarea">` (min-height 72px),
   placeholder **«Договорённости, следующий шаг»**.
4. **«Дата»** — `<input type="date" class="field co-date">`, defaults to today.

Footer: `.btn-primary` **«Сохранить запись»** + `.btn-ghost` **«Отмена»**.
Each field preceded by a `.co-field-label` (12px/600 `--ink-2`, `margin-bottom:6px`).
Edit reuses the same form pre-filled (opened from the entry's edit `.icon-btn`);
in edit context it is presented via the modal shell (§5.2) so it overlays the entry
being edited.

### 3.6 States (company page)
- **Loading:** back link + header skeleton (avatar `64×64 r16`, name `200×20`,
  meta `160×13`) + two `.co-card` skeleton blocks (`Skeleton height 140/220 r14`).
- **Error / not found:** `.empty` with `Icon name="warning"` (heart-soft ic),
  `.h3` **«Компания не найдена»**, `.small` **«Возможно, она была удалена»**, and a
  `.btn-ghost` **«← К списку»**.
- **Empty timeline:** inside the timeline card, `.empty` (compact, `padding:32px 20px`):
  `.small` **«Здесь появятся ваши тренировки и записи о реальных звонках»**.

---

## 4. Pre-call goal step (`.co-cta` panel) + handoff

Inline panel directly under the identity header — the primary action of the page.

### 4.1 Visual
`.co-cta` = a slightly emphasized card: white surface, `1px solid
var(--primary-tint-border-2)`, `--r-lg`, padding `18px 20px`, subtle violet tint
background `var(--primary-softer)`. Layout:

```
eyebrow  «ТРЕНИРОВКА ПЕРЕД ЗВОНКОМ»
input row:  [ goal input ...................... ]  [ 📞 Позвонить ]  [ 💬 Чат ]
recent goals:  chip  chip  chip   (reuse tappable)
helper caption
```
- **Goal input:** `<input class="field co-goal-input">`, placeholder
  **«Цель звонка: напр. договориться о встрече с ЛПР»**. Optional — call works with
  empty goal.
- **Primary CTA:** `.btn .btn-dark` (dark `#1C1C20`, matches "Call" language across
  the app) with `Icon name="phone"` → **«Позвонить»**. Full-height match to input.
- **Secondary CTA:** `.btn .btn-soft` with `Icon name="message"` → **«Чат»**
  (text-practice variant; only if text company-practice is enabled — otherwise omit).
- **Recent goals:** up to 5 previously used goals for THIS company rendered as
  `.chip-tag` (clickable) that fill the input when tapped. Label above them
  (11.5px `--ink-4`): **«Недавние цели»**. Hidden if none.
- **Helper caption** (11.5px `--ink-4`): **«ИИ сыграет сотрудника этой компании,
  используя описание выше и указанную цель.»**
- **Guard:** if description is empty, the caption swaps to a gentle nudge
  **«Совет: заполните описание компании — звонок станет реалистичнее.»** (still
  allow the call).

### 4.2 Handoff to the existing full-screen call UI
Do **not** redesign the call. On "Позвонить":
1. Persist the goal (append to this company's recent-goals list; POST when the
   session is created).
2. Navigate to **`/companies/[id]/call/voice`**.

This route renders the **same** voice experience as
`app/dialog/[bundleId]/[modeId]/voice/page.tsx`. Recommended implementation:
extract the voice screen into a shared component and mount it from both routes,
OR have the company route build the equivalent props. The company variant differs
only in **where the scenario/system-prompt comes from**:
- `bundleId`/`modeId` are replaced by a company context: the backend builds the
  roleplay system prompt from `company.description` + the pre-call `goal`.
- The `useVoice` session-create call (`POST /dialog/sessions`) becomes a company
  variant (e.g. `POST /companies/[id]/practice-sessions` with `{ goal }`), returning
  a `sessionId` the voice pipeline uses unchanged.
- The in-call header shows the **company name** (via `GeoAvatar`/seeded avatar) and
  the goal as the subtitle where the mode title normally sits. Everything else
  (states, subtitles, timer, `FeedbackModal`, sounds) is untouched.
- On end, `completeDialogSession` (or company equivalent) writes the practice entry
  that then appears in the company timeline (§3.4a). Back navigation returns to
  `/companies/[id]` (not `/dialog`).

The **chat** variant (`/companies/[id]/call/chat`) analogously reuses the existing
text-dialog screen with the same company-context session. Optional for v1.

> This keeps requirement #3 literal: "EXACTLY the existing voice-call experience,
> but the AI plays a person at THIS company using the company description," plus the
> goal injection.

**39.6 implementation note:** the company page persists the goal to
`sessionStorage` (key `company-call-goal:{companyId}`) immediately before
navigating, and also passes it as a `?goal=` query param on the same URL. The
`sessionStorage` value is the **authoritative** goal source for the call route —
the query param exists only as a fallback for a deep-link or a hard refresh
where `sessionStorage` may already be cleared or was never set on this tab.
39.7 must read `sessionStorage` first and fall back to the query param.

---

## 5. Modals (reuse `.modal-overlay` / `.modal` / `.modal-head/body/foot`)

All modals use the existing shell classes (see globals.css §modal). `max-width:560px`
default; the delete confirms use `max-width:440px`. Standard: `Icon name="close"`
`.icon-btn` in `.modal-head`, ESC + overlay-click to close, focus-trap, initial
focus on first field / on the safe (Cancel) button for destructive dialogs.

### 5.1 Create / Edit company modal
- Head title: **«Новая компания»** (create) / **«Редактировать компанию»** (edit).
- Body:
  - `.co-field-label` **«Название»** → `<input class="field">` required, autofocus,
    placeholder **«Напр. ООО «Ромашка»»**. Max ~120 chars.
  - `.co-field-label` **«Описание»** (optional in create) → `<textarea class="field
    co-textarea">` min-height 120px, placeholder = the description helper text (§3.3).
    Sublabel 11.5px `--ink-4`: **«Можно заполнить позже на странице компании.»**
- Foot: `.btn-ghost` **«Отмена»** (left) + `.btn-primary` **«Создать»** /
  **«Сохранить»** (right, disabled while name is blank).
- On create success → navigate to the new `/companies/[id]`.

### 5.2 Add / edit real-call log modal (mobile + edit context)
On desktop, adding a log is the inline panel (§3.5). **Editing** an existing log,
and adding on mobile, use this modal with the identical 3 fields + date (§3.5).
- Head: **«Запись о звонке»** (add) / **«Изменить запись»** (edit).
- Foot: `.btn-ghost` **«Отмена»** + `.btn-primary` **«Сохранить»**.

### 5.3 Delete confirms
Small modal (`max-width:440px`), no body scroll.
- **Delete company:** head **«Удалить компанию?»**; body `.small` **«Компания
  «{name}», её описание, тренировки и журнал звонков будут удалены безвозвратно.»**;
  foot `.btn-ghost` **«Отмена»** + `.btn-danger` **«Удалить»**.
- **Delete log entry:** head **«Удалить запись?»**; body **«Запись о звонке будет
  удалена.»**; foot `.btn-ghost` **«Отмена»** + `.btn-danger` **«Удалить»**.

---

## 6. Component inventory

### 6.1 Reused (no changes needed)
| Component | From | Use |
|---|---|---|
| `Icon` | `shared/components/icon` | all icons (add `briefcase`) |
| `Skeleton` | `shared/components` | loading states |
| `ErrorState` | `shared/components` | list/page error |
| `GeoAvatar` | `shared/components/geo-avatar` | in-call company avatar (§4.2); optional on rows |
| `FeedbackModal` | `features/dialog/components/feedback-modal` | practice entry «Разбор →» |
| voice screen | `app/dialog/[bundleId]/[modeId]/voice/page.tsx` | extract → shared, reuse for company call (§4.2) |
| `useVoice` | `features/voice/hooks/use-voice` | unchanged pipeline; company session-create variant |
| CSS: `.page .container .practice-header .practice-title .practice-subtitle .field .field-search .btn .btn-primary .btn-dark .btn-soft .btn-ghost .btn-danger .btn-link .icon-btn .chip .chip-tag .eyebrow .empty .back-link .modal* .sessions-card .session-row .pill-inprogress .h3 .h4 .small` | globals.css | reuse verbatim |

### 6.2 New components — `features/companies/` (mirror `features/dialog/` structure)
```
features/companies/
  hooks/
    use-companies.ts            → useCompanies(), useCompany(id), useCompanyTimeline(id),
                                   createCompany, updateCompany, deleteCompany,
                                   addCallLog, updateCallLog, deleteCallLog,
                                   createCompanyPracticeSession({id, goal}),
                                   useRecentGoals(id)
  components/
    company-row.tsx             → list row (§2.4)
    company-list-toolbar.tsx    → search + count (§2.3)
    company-header.tsx          → identity header + edit/delete actions (§3.2)
    company-description-card.tsx→ read/edit description (§3.3)
    precall-panel.tsx           → goal input + CTAs + recent goals (§4)
    company-timeline.tsx        → segmented filter + feed (§3.4)
    timeline-practice-item.tsx  → practice entry (§3.4a)
    timeline-reallog-item.tsx   → real-log entry (§3.4b)
    call-log-form.tsx           → 3-field + date form, used inline & in modal (§3.5)
    company-modal.tsx           → create/edit modal (§5.1)
    call-log-modal.tsx          → add/edit log modal (§5.2)
    confirm-delete-modal.tsx    → generic small confirm (§5.3) [or reuse an existing confirm if present]
```
Routes (App Router):
```
app/(main)/companies/page.tsx            → list
app/(main)/companies/[id]/page.tsx       → company page
app/companies/[id]/call/voice/page.tsx   → full-screen call (outside (main) shell, like existing voice route)
app/companies/[id]/call/chat/page.tsx    → optional text practice
```
> Note the existing voice route lives at `app/dialog/...` **outside** the `(main)`
> group (no rail, full-screen). Mirror that: put the company call route outside
> `(main)` so it renders full-screen exactly like today.

---

## 7. Responsive behavior

- **Desktop (≥768px):**
  - List: header row is title-block left / «Добавить» button right; `.co-list`
    full width of `.container`.
  - Company page: single centered column, `max-width:860px`. Pre-call panel input +
    CTAs sit on one row. Add-log form expands inline within the timeline card.
- **Mobile (<768px):**
  - Rail hidden, bottom-nav shows Companies (§1.4).
  - List: «Добавить компанию» button drops full-width (`.btn-block`) under the
    subtitle; search full-width; rows unchanged (already flexible).
  - Company page: `.co-page` padding shrinks to `16px`. Pre-call panel stacks:
    goal input full-width, then CTAs row (Позвонить / Чат) full-width `.btn-block`
    (2-up if both present, else single). Recent-goal chips wrap.
  - Add-log uses the **modal** (§5.2) instead of inline expansion (more room, avoids
    a tall inline form pushing the timeline down).
  - Modals: existing responsive `.modal` rules apply (max-height 92vh, tighter pads).
- The company call route is inherently full-screen and already responsive via the
  reused voice screen.

---

## 8. New CSS (append to globals.css, all values in V2 tokens)

```css
/* ── Companies: list ── */
.co-list-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; }
.co-list-toolbar { display: flex; align-items: center; gap: 14px; margin: 0 0 14px; }
.co-list-toolbar .co-search-wrap { position: relative; flex: 1; max-width: 420px; }
.co-list-count { margin-left: auto; font-size: 12.5px; font-weight: 600; color: var(--ink-4); white-space: nowrap; }

.co-list {
  background: var(--surface);
  border: 1px solid var(--line);
  border-radius: var(--r-md);
  box-shadow: var(--sh-1);
  overflow: hidden;
}
.co-row {
  display: flex; align-items: center; gap: 14px;
  padding: 14px 18px;
  border-bottom: 1px solid var(--line-2);
  text-decoration: none; color: inherit;
  transition: background var(--transition);
}
.co-row:last-child { border-bottom: none; }
.co-row:hover { background: var(--surface-2); }
.co-row-av {
  width: 40px; height: 40px; border-radius: 11px; flex: none;
  display: grid; place-items: center;
  color: #fff; font-weight: 700; font-size: 14px; letter-spacing: -0.01em;
}
.co-row-body { flex: 1; min-width: 0; }
.co-row-name {
  font-size: 15.5px; font-weight: 700; letter-spacing: -0.01em; color: var(--ink-heading);
  margin: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
}
.co-row-meta { font-size: 12.5px; font-weight: 500; color: var(--ink-4); margin: 2px 0 0; }
.co-row-chev { color: var(--ink-4); flex: none; }

/* ── Companies: page shell ── */
.co-page { max-width: 860px; margin: 0 auto; padding: 22px clamp(16px,3.5vw,30px) 64px; display: flex; flex-direction: column; gap: 20px; }
.co-header { display: flex; align-items: center; gap: 16px; }
.co-header-av { width: 64px; height: 64px; border-radius: 16px; flex: none; display: grid; place-items: center; color: #fff; font-weight: 800; font-size: 22px; letter-spacing: -0.02em; }
.co-header-body { flex: 1; min-width: 0; }
.co-header-name { display: inline-flex; align-items: center; gap: 8px; }
.co-header-meta { font-size: 12.5px; font-weight: 500; color: var(--ink-4); margin: 4px 0 0; }
.co-header-actions { display: flex; gap: 8px; flex: none; }

/* ── Card primitive (companies) ── */
.co-card { background: var(--surface); border: 1px solid var(--line); border-radius: var(--r-lg); box-shadow: var(--sh-1); padding: 18px 20px; }
.co-card-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 14px; }
.co-field-label { font-size: 12px; font-weight: 600; color: var(--ink-2); margin-bottom: 6px; display: block; }
.co-textarea { height: auto; min-height: 120px; padding: 12px 14px; line-height: 1.6; resize: vertical; }
.co-date { max-width: 200px; }

/* ── Pre-call panel ── */
.co-cta { background: var(--primary-softer); border: 1px solid var(--primary-tint-border-2); border-radius: var(--r-lg); padding: 18px 20px; }
.co-cta-row { display: flex; gap: 10px; margin: 12px 0 10px; }
.co-goal-input { flex: 1; }
.co-cta .btn { flex: none; }
.co-recent-label { font-size: 11.5px; font-weight: 600; letter-spacing: 0.02em; color: var(--ink-4); margin-bottom: 6px; }
.co-recent-goals { display: flex; flex-wrap: wrap; gap: 8px; }
.co-cta-help { font-size: 11.5px; color: var(--ink-4); margin: 10px 0 0; line-height: 1.5; }

/* ── Description ── */
.co-desc { font-size: 13.5px; line-height: 1.6; color: var(--ink-secondary); white-space: pre-wrap; margin: 0; }
.co-desc-empty { border: 1px dashed var(--line); border-radius: var(--r-sm); padding: 16px; text-align: center; color: var(--ink-4); font-size: 13px; line-height: 1.5; display: flex; flex-direction: column; align-items: center; gap: 12px; }

/* ── Timeline ── */
.co-seg { display: inline-flex; gap: 4px; background: var(--surface-3); border: 1px solid var(--line); border-radius: 9px; padding: 3px; }
.co-seg button { border: none; background: transparent; color: var(--ink-3); font-size: 12.5px; font-weight: 700; padding: 6px 13px; border-radius: 7px; cursor: pointer; transition: background var(--transition), color var(--transition); }
.co-seg button.active { background: var(--dark-surface); color: #fff; }

.co-add-log { width: 100%; border: 1px dashed var(--primary-tint-border-2); background: var(--primary-softer); color: var(--primary-strong); justify-content: center; margin-bottom: 16px; }

.co-timeline { position: relative; }
.co-tl-item { position: relative; padding: 0 0 16px 40px; }
.co-tl-item:last-child { padding-bottom: 0; }
.co-tl-item::before { content: ""; position: absolute; left: 19px; top: 18px; bottom: 0; width: 2px; background: var(--line); }
.co-tl-item:last-child::before { display: none; }
.co-tl-node { position: absolute; left: 13px; top: 6px; width: 14px; height: 14px; border-radius: 50%; box-shadow: 0 0 0 3px var(--surface); }
.co-tl-item.practice .co-tl-node { background: var(--primary); }
.co-tl-item.reallog  .co-tl-node { background: var(--success); }
.co-tl-card { border: 1px solid var(--line); border-radius: 13px; padding: 12px 14px; transition: border-color var(--transition), box-shadow var(--transition); }
.co-tl-card:hover { border-color: var(--primary-tint-border); box-shadow: var(--sh-2); }
.co-tl-top { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; }
.co-tl-time { margin-left: auto; font-size: 12px; color: var(--ink-4); white-space: nowrap; }
.co-tl-title { font-size: 14px; font-weight: 700; color: var(--ink-heading); margin: 0 0 2px; }
.co-tl-meta { font-size: 12px; color: var(--ink-4); margin: 0; }

.co-pill-real { display: inline-flex; align-items: center; padding: 3px 8px; border-radius: 6px; font-size: 10.5px; font-weight: 700; background: var(--success-soft); color: var(--success); }

/* real-log fields */
.co-log-field { margin-top: 10px; }
.co-log-field-label { font-size: 10.5px; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase; color: var(--ink-4); margin-bottom: 3px; }
.co-log-field-value { font-size: 13.5px; line-height: 1.5; color: var(--ink-secondary); white-space: pre-wrap; }
.co-log-field.outcome .co-log-field-value { border-left: 2px solid var(--success); padding-left: 10px; }

/* inline log form */
.co-log-form { border: 1px solid var(--line); border-radius: 13px; padding: 16px; margin-bottom: 16px; background: var(--surface-2); }
.co-log-form .co-field-label + * { margin-bottom: 14px; }
.co-log-form-foot { display: flex; gap: 10px; justify-content: flex-end; }

/* ── Responsive ── */
@media (max-width: 767px) {
  .co-list-head { flex-direction: column; }
  .co-list-head .btn-primary { width: 100%; }
  .co-cta-row { flex-direction: column; }
  .co-cta-row .btn { width: 100%; }
  .co-date { max-width: 100%; }
}
```

### 8.1 Notes
- `.co-list`, `.co-row` deliberately clone `.sessions-card`/`.session-row` so the
  list reads as a first-class V2 surface. If the team prefers, apply `.sessions-card`
  + `.session-row` directly and skip `.co-list`/`.co-row`.
- The `.co-seg` segmented control matches the DESIGN_SPEC segmented-control token
  (active dark `#1C1C20`, idle `--surface-3`).
- Timeline connector reuses the lesson-timeline visual language (vertical rail +
  ringed nodes) but at a smaller 14px node scale suited to a document column.

---

## 9. Russian copy — master list

| Key | Russian |
|---|---|
| Rail / bottom-nav label | Компании |
| List title | Компании |
| List subtitle | Ваши реальные клиенты: описание, тренировки перед звонком и журнал встреч |
| Add company button | Добавить компанию |
| Add first company button | Добавить первую компанию |
| Search placeholder | Поиск по названию… |
| Count | {n} компаний / компания / компании (§8 plural) |
| Empty list title | Пока нет ни одной компании |
| Empty list body | Добавьте компанию, которой планируете позвонить — и потренируйтесь перед реальным разговором |
| Empty search | Ничего не найдено по запросу «{q}» |
| Loading error title | Не удалось загрузить |
| Back to list | ← К списку |
| Company not found title | Компания не найдена |
| Company not found body | Возможно, она была удалена |
| Edit company (aria) | Редактировать компанию |
| Delete company (aria) | Удалить компанию |
| Description eyebrow | ОПИСАНИЕ КОМПАНИИ |
| Description edit toggle | Изменить |
| Description save / cancel | Сохранить / Отмена |
| Description empty prompt | Добавьте описание: кто эта компания, что продаёт, кто ЛПР, боли и контекст. Это описание получит ИИ‑собеседник во время тренировки. |
| Description add button | Добавить описание |
| Description helper (edit) | Это описание ИИ использует как роль собеседника на тренировочных звонках. |
| Pre-call eyebrow | ТРЕНИРОВКА ПЕРЕД ЗВОНКОМ |
| Goal placeholder | Цель звонка: напр. договориться о встрече с ЛПР |
| Call button | Позвонить |
| Chat button | Чат |
| Recent goals label | Недавние цели |
| Pre-call helper | ИИ сыграет сотрудника этой компании, используя описание выше и указанную цель. |
| Pre-call nudge (no desc) | Совет: заполните описание компании — звонок станет реалистичнее. |
| History eyebrow | ИСТОРИЯ |
| Segmented: all/practice/real | Все · Тренировки · Звонки |
| Add-log trigger | + Записать звонок |
| Practice pill | Тренировка |
| Practice default title | Тренировочный звонок |
| Practice meta | Голосовой звонок · {n} реплик · {mm:ss} |
| Feedback link | Разбор → |
| Real-call pill | Реальный звонок |
| Log field: who (label) | С кем говорил |
| Log field: who (placeholder) | Имя и должность, напр. Иван, руководитель отдела закупок |
| Log field: topic (label) | О чём был разговор |
| Log field: topic (placeholder) | Кратко о ходе разговора |
| Log field: outcome (label) | К чему пришли |
| Log field: outcome (placeholder) | Договорённости, следующий шаг |
| Log field: date | Дата |
| Save log | Сохранить запись |
| Edit entry (aria) | Редактировать запись |
| Delete entry (aria) | Удалить запись |
| Empty timeline | Здесь появятся ваши тренировки и записи о реальных звонках |
| Create modal title | Новая компания |
| Edit modal title | Редактировать компанию |
| Field: name | Название |
| Name placeholder | Напр. ООО «Ромашка» |
| Field: description (optional) | Описание |
| Description sublabel (modal) | Можно заполнить позже на странице компании. |
| Create / Save button | Создать / Сохранить |
| Cancel | Отмена |
| Log modal title (add/edit) | Запись о звонке / Изменить запись |
| Delete company title | Удалить компанию? |
| Delete company body | Компания «{name}», её описание, тренировки и журнал звонков будут удалены безвозвратно. |
| Delete log title | Удалить запись? |
| Delete log body | Запись о звонке будет удалена. |
| Delete confirm button | Удалить |

### Plurals (companies count)
Use standard RU plural rule on `n`:
- ends 1 (not 11) → **компания**
- ends 2–4 (not 12–14) → **компании**
- else → **компаний**

### 8.2 Relative time (RU)
Provide a small RU relative-time helper (the existing `relativeTime` returns
English). Format: «только что», «{n} мин назад», «{n} ч назад», «{n} д назад»,
older → absolute «d MMM yyyy». Real-log dates always render absolute
(«9 июл 2026»).

---

## 10. Accessibility & gamification

- Every row/card link has a descriptive `aria-label`; icon-only buttons carry
  `aria-label` (listed in §9). Modals: `role="dialog" aria-modal="true"`, labelled
  by the head title, focus-trap, ESC to close, restore focus to the invoker.
- Segmented filter = `role="tablist"` with `aria-selected`; textareas/inputs have
  associated `<label>` via the `.co-field-label`.
- Timeline nodes/rails are `aria-hidden` decoration.
- Respect `prefers-reduced-motion` (already global) — hover lifts/transitions
  collapse automatically.
- **No gamification** in this tab: no XP, no streaks, no difficulty badges. The only
  score surfaced is inside the reused `FeedbackModal` for a practice call, opened on
  demand via «Разбор →». Counts on rows/headers are neutral facts, not points.
```
