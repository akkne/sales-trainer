# Handbook ("Справочник") — Architecture Redesign Proposal

Status: **implemented** (backend + frontend + migration, 2026-04-21)
Last updated: 2026-04-21
Related: [`src/frontend/app/(main)/guidebook/page.tsx`](../src/frontend/app/(main)/guidebook/page.tsx), [`src/backend/api/Features/Reference`](../src/backend/api/Features/Reference), [DB_SCHEMA](DB_SCHEMA.md#referencematerials), [API_CONTRACTS](API_CONTRACTS.md#reference), [`.design/redesign/components/screens.jsx`](../.design/redesign/components/screens.jsx) (`Handbook`, `TechniqueCard`, `MasteryRing`).

---

## 1. What the redesign demands

From `.design/redesign` screens and the "Коллекция" screenshot, every handbook entry is now a **technique**, not a markdown article. Each card shows:

| Block | Data it needs |
|---|---|
| Hero header | total count of techniques, per-user counts: *Освоено* / *Мастер* / *Новых* |
| Category chip row | fixed set of categories with colors |
| Mastery ring (left of card) | `level` (1–4) + `masteryPercent` (0–100), **per user** |
| Title + excerpt | `name`, short `summary` |
| Chips | one primary `category`, N free `tags` |
| Level label (right of card) | `levelName` = {Novice, Practitioner, Expert, Master} + maybe suffix (e.g. `Novice+`) |
| Expanded: sample dialog | ordered list of turns `{side: me|them, text, annotations?: [{label, tone}]}` (e.g. `[S]`, `[P → I]`) |
| Expanded: case study | structured block: `title` (e.g. "Mid-market CRM, 2024"), `body`, optional metrics (`deal`, `cycleDays`) |
| Expanded: coach sidecar (NPC mentor) | `avatarSeed`, `name`, `role`, `quote`, array of 3 `practiceChallenges` (each links to a session) |
| Expanded actions | "Практиковать сейчас" (starts a session tied to the technique), "Открыть диалог с AI" (starts AI dialog), "Связанный навык →" |

Key semantic shift: the entry is no longer a **page of markdown text tied to one skill**. It is a first-class **Technique** that lives across skills, has structured sub-entities, and tracks **per-user mastery**.

---

## 2. Where we are today

### 2.1 Data model

One flat table, one markdown blob:

```
ReferenceMaterials(
  Id uuid PK,
  SkillId uuid FK -> Skills,       -- each material belongs to exactly one skill
  Title text,
  MarkdownContent text,            -- everything lives here as freeform MD
  SortOrder int,
  Category text NULL,              -- slug: "objections" | "closing" | ...
  Tags text NULL                   -- comma-separated
)
```

No per-user state, no structured dialog/case/coach sub-entities, 1:1 to Skill.

### 2.2 API
- `GET /skills/:slug/reference` → materials for one skill
- `GET /reference?category=&search=` → all materials, filtered
- `GET /reference/categories` → distinct categories
- Admin CRUD at `/admin/reference/*`

### 2.3 Frontend
`guidebook/page.tsx` already renders the redesigned UI shell — **but with fake/hardcoded data** for every new block:
- `MasteryRing level={1} mastered={Math.random() * 100}` → bogus
- Level label `"Novice"` → hardcoded
- Sample dialog bubbles → two hardcoded strings
- Coach sidecar "Skeptic Sergey" quote → hardcoded for every card
- Header stats (`Освоено / Мастер / Новых`) → "—" / `materials.length`

So the UI is already aligned; the gap is purely in the data layer.

---

## 3. Proposed target architecture

### 3.1 New entities (PostgreSQL)

Rename the user-facing concept to **Technique**. Keep the `ReferenceMaterials` table in the DB for one migration cycle, then drop — see §5.

```
Techniques                         -- replaces ReferenceMaterials as the primary entity
  Id               uuid PK
  Slug             text UNIQUE     -- "spin-questions", "feel-felt-found" (for URLs + seeding)
  Name             text            -- display name ("SPIN-вопросы")
  Summary          text            -- 1–2 sentence excerpt
  Body             text            -- long-form markdown (optional, legacy path)
  Category         text            -- FK-ish slug: "objections"|"closing"|"discovery"|...
  Tags             text[]          -- real array, not CSV
  PrimarySkillId   uuid NULL FK -> Skills   -- "Связанный навык"; NULLABLE because techniques can be skill-agnostic
  SortOrder        int
  CreatedAt        timestamptz
  UpdatedAt        timestamptz

TechniqueSkills                    -- M:N (a technique can be referenced by multiple skills)
  TechniqueId uuid FK
  SkillId     uuid FK
  PK (TechniqueId, SkillId)

TechniqueDialogTurns               -- ordered example-dialog turns
  Id           uuid PK
  TechniqueId  uuid FK -> Techniques ON DELETE CASCADE
  OrderIndex   int
  Side         text    -- 'me' | 'them'
  Text         text
  Annotations  jsonb NULL           -- [{label:"S", tone:"rust"}, {label:"P → I", tone:"rust"}]

TechniqueCases                     -- one or more real-world cases per technique
  Id           uuid PK
  TechniqueId  uuid FK ON DELETE CASCADE
  Title        text     -- "Mid-market CRM, 2024"
  Body         text
  Metrics      jsonb NULL   -- { dealSize: "4.2M ₽", cycleDays: 11 }
  OrderIndex   int

TechniqueCoaches                   -- NPC mentor block (may be 0 or 1 per technique, or reuse across)
  Id             uuid PK
  TechniqueId    uuid FK ON DELETE CASCADE
  AvatarSeed     text         -- "sergey" — feeds GeoAvatar
  Name           text         -- "Skeptic Sergey"
  Role           text         -- "Коуч · возражения"
  Quote          text
  Challenges     jsonb        -- [{label:"Задать 1 S-вопрос", sessionKind:"...", payload:{...}}, ...]
```

Alternative (simpler, less queryable): collapse `Dialog / Cases / Coach` into a single `TechniqueContent jsonb` column on `Techniques`. Trade-off in §4.

### 3.2 Per-user mastery

The redesign shows a mastery ring, level, and "Освоено / Мастер / Новых" aggregates — none of which exist today.

```
UserTechniqueProgress
  Id                uuid PK
  UserId            uuid FK -> Users ON DELETE CASCADE
  TechniqueId       uuid FK -> Techniques ON DELETE CASCADE
  Level             int       -- 1..4 (Novice / Practitioner / Expert / Master)
  MasteryPercent    int       -- 0..100, drives the ring fill
  PracticeCount     int       -- # of sessions credited to this technique
  LastPracticedAt   timestamptz NULL
  FirstSeenAt       timestamptz -- used to compute "Новых" (unseen/new for this user)
  UNIQUE (UserId, TechniqueId)
```

Updates are driven from:
- dialog/session completion events — when a session is tagged with `TechniqueId`, increment `PracticeCount`, bump `MasteryPercent` by a formula (e.g. evaluation score mapped to delta), promote `Level` at thresholds.
- `FirstSeenAt` is set when the user first opens the technique card (expand event) — keeps the "Новых" count honest.

### 3.3 Linking sessions back to techniques

Today a session (`Exercise`, `DialogMode`) has no link to a reference material. The redesign "Практиковать сейчас" button assumes a technique → practice relation. Options:
- add `TechniqueId` nullable FK on `Exercises` and `DialogModes`, OR
- add a join table `TechniquePractice(TechniqueId, Kind, RefId)` where `Kind ∈ {exercise, dialog_mode, lesson}`.

The join table scales better: one technique can have many practice surfaces.

### 3.4 API surface

Public:
```
GET  /techniques?category=&search=&tag=
      -> TechniqueCardDto[]   -- card-level data (no dialog turns / cases)
GET  /techniques/:slug
      -> TechniqueDetailDto   -- full, with dialog turns, cases, coach, user progress
GET  /techniques/meta
      -> { categories: [{slug,label,color}], totalCount, userCounts: {mastered, master, unseen} }
POST /techniques/:slug/seen
      -> 204  (flips FirstSeenAt; used when card is first expanded)
```

`TechniqueCardDto`:
```
{ id, slug, name, summary, category, tags[], primarySkillSlug?,
  userProgress: { level, levelName, masteryPercent, isNew } }
```

Admin CRUD mirrors current `/admin/reference/*` but rooted at `/admin/techniques/*` with nested endpoints for dialog turns, cases, coach.

### 3.5 Frontend changes (after data layer lands)

- `useReference.ts` → `useTechniques.ts`: `useTechniqueList(filters)`, `useTechnique(slug)`, `useTechniquesMeta()`.
- `guidebook/page.tsx`: replace hardcoded level/ring/dialog/coach with real fields; plug header stats into `useTechniquesMeta()`.
- Remove `Math.random()` on the ring.
- Category pill labels become driven by `/techniques/meta` instead of being hardcoded in the component.

---

## 4. Trade-offs — decisions to make before coding

| Decision | Option A | Option B | Recommendation |
|---|---|---|---|
| Sub-entity storage | Normalized tables (`TechniqueDialogTurns`, `TechniqueCases`, `TechniqueCoaches`) | Single `Content jsonb` blob | **A** — admin UI and search need row-level access; jsonb blocks indexing/ordering |
| Skill relation | Keep 1:1 (reuse `Techniques.PrimarySkillId`) | Add M:N via `TechniqueSkills` | **B** — SPIN applies to Discovery *and* Qualification; M:N matches reality |
| Rename on the wire | Rename `ReferenceMaterial` → `Technique` everywhere | Keep the API name, change DB only | **Rename** — the UI and product calls it "техника"; "reference" is legacy vocabulary |
| Mastery formula | Store raw `MasteryPercent` + derive `Level` | Store both | **Store both** — derivation is cheap but `Level` is shown without the %, and promotion should be an event (triggers XP / unlocks) |
| Category | Free text (today) | Enum table `Categories(slug,label,color,sortOrder)` | **Enum table** — the redesign assigns a color per category and we already hardcode labels in three places (frontend, seeder, admin) |

---

## 5. Migration plan (no code yet)

1. **Add** `Techniques`, `TechniqueSkills`, `TechniqueDialogTurns`, `TechniqueCases`, `TechniqueCoaches`, `UserTechniqueProgress`, `Categories` tables. Existing `ReferenceMaterials` stays.
2. **Backfill**: for every `ReferenceMaterial` row, create a matching `Technique` with the same `Id` (so external URLs survive), copy `Title → Name`, `MarkdownContent → Body`, `SortOrder`, `Category`, split `Tags` CSV into `text[]`. `PrimarySkillId = ReferenceMaterials.SkillId`. Dialog turns / cases / coach start empty — editors fill them in via admin UI.
3. **Ship** new public `/techniques*` endpoints **alongside** the old `/reference*` (deprecated, kept 1 release).
4. **Flip** `guidebook/page.tsx` to the new hook. Delete random/hardcoded blocks.
5. **Add** session → technique linkage (§3.3) + progress update hooks in session-completion flow.
6. **Drop** old `/reference*` endpoints and the `ReferenceMaterials` table in a later migration.

---

## 6. Open questions

- Do we need per-user notes / bookmarks on a technique? (Not in screenshots, but is a natural extension of "Коллекция".)
- Is "Новых" = "never-seen by this user" or "added in the last N days globally"? Affects whether `FirstSeenAt` is per-user or we need a global `PublishedAt`.
- Coach/NPC — is this shared across techniques (a roster of mentors) or per-technique embedded? If shared, `TechniqueCoaches` becomes `Coaches` + `TechniqueCoaches(TechniqueId, CoachId)` M:N.
- Practice challenges — pre-authored strings (as shown) or dynamic mini-exercises? If dynamic, each challenge should reference a real `Exercise` or `DialogMode`.
- Localization — current field names are bilingual (`Title` in Russian). If we need EN/RU split, sub-entities have to be locale-keyed.

---

## 7. Body formatting (fix)

Technique `Body` text is authored/stored with literal escape sequences (`\n`, `\t`)
rather than real whitespace, and CommonMark collapses single newlines into spaces.
`guidebook/page.tsx` runs `normalizeMarkdown()` before `<ReactMarkdown>`:

- literal `\t` / real tab → 4 non-breaking spaces (NBSP avoids markdown indented-code-block parsing)
- literal `\n` / `\r\n` and real newlines → CommonMark hard break (`"  \n"`)

This keeps inline markdown (bold, links) working while rendering authored line breaks
and indentation correctly. `remark-breaks` is intentionally not used (not installed).
