# Redesign Baseline — What Exists (Function, Not Form)

> **Purpose.** This document is the brief you hand to a redesign agent. It describes
> **what the product does and what lives on every screen** — the features, the
> exercises, the dialog internals, the routes and what each page contains — so the
> designer fully understands the product surface.
>
> It deliberately says **nothing about the current implementation of the look**:
> no colors, tokens, fonts, spacing scales, shadows, layout coordinates, "where
> things are positioned", CSS, or component internals. The old visual system is
> **not** a constraint. You are expected to bring a fresh, bold design direction.
>
> If you need the factual snapshot of the *current* visuals (only to know what
> you're replacing), that lives in `docs/UI_CURRENT_STATE.md` — but treat it as the
> past, not a brief.

Last updated: 2026-06-22.

---

## 1. Product in one paragraph

SalesTrainer (UI brand: **Sellevate**) is a Duolingo-style sales-training web app.
Users learn sales skills through a tree of lessons built from 10 exercise types,
practice realistic conversations with an AI client in **text chat** and **real-time
voice calls**, compete in **weekly leagues**, add **friends** and chat with them,
browse a **guidebook** of sales techniques, and track **streaks / XP / achievements**.
UI language is **Russian**. There is also an **admin panel** for managing all content.

Everything below is the menu of surfaces a redesign has to cover. How any of it
*looks* is entirely open.

---

## 2. The core loops (what the user is actually doing)

1. **Learn loop** — pick a skill → open a lesson → solve a sequence of exercises one
   at a time → get graded (instantly or by AI) → earn XP → lesson complete → next lesson.
2. **Practice loop** — pick a dialog bundle → pick a mode (a scenario/persona) →
   converse with the AI client by text or voice → AI ends the call → receive written
   feedback + XP.
3. **Compete loop** — weekly XP places you on a league leaderboard; at week's end you
   are promoted / kept / demoted between tiers.
4. **Social loop** — find people, send friend requests, see friends' activity,
   compare on a friends leaderboard, direct-message them.
5. **Reference loop** — browse/search a guidebook of sales techniques, each with
   examples and a "coach" persona; track mastery per technique.
6. **Identity loop** — profile shows your stats, achievements, skill enrollment, voice
   quota, and settings.

A redesign should make these six loops feel coherent and motivating; the gamification
signals (§7) are the connective tissue and must remain legible everywhere.

---

## 3. Route map (every screen that must exist)

### Public / auth
| Route | Screen | What it's for |
|---|---|---|
| `/` | Landing | Pitch + single CTA to register |
| `/login`, `/register` | Auth forms | Email+password and Google OAuth |
| `/onboarding` | First-run setup | Initial persona/skill setup for new users |

### Main app (auth-guarded)
| Route | Screen | What it contains |
|---|---|---|
| `/tree` | Skill tree — main hub | Browse skills grouped by funnel stage; pick a skill; see its lesson path; see your headline stats |
| `/skill/[id]`, `/skill/[id]/map` | Skill detail / progress map | A single skill's lessons & progress |
| `/session/[lessonId]` | Exercise session | The actual lesson player — one exercise at a time, hearts, progress, completion screen |
| `/dialog` | Dialog bundles list | Catalog of practice bundles + a featured NPC mentor |
| `/dialog/[bundleId]` | Mode selection | The scenarios/personas inside a bundle; choose Chat or Voice |
| `/dialog/[bundleId]/[modeId]` | Text chat with AI | Conversation player + session history |
| `/dialog/[bundleId]/[modeId]/voice` | **Voice call** | Real-time spoken conversation player |
| `/guidebook` | Technique reference | Searchable/filterable library of techniques + mastery |
| `/reference/[id]` | Single technique page | One technique in full |
| `/league` | Weekly league | Tier, countdown, your rank, leaderboard, promotion zones |
| `/friends` | Social hub | Friends, requests, friends leaderboard, chats |
| `/friends/[userId]` | Public profile | Another user's profile |
| `/friends/chat/[conversationId]` | Direct messages | Full DM thread |
| `/profile` | Profile & settings | Your stats, achievements, enrollment, quota, settings |

### Admin (role-guarded)
`/admin` dashboard + CRUD pages: bundle/content import, skills, skill-stages, topics,
lessons, reference, techniques, dialog scenarios, AI prompts, voice usage analytics,
users. Functional content-management surface — likely lower design priority, but it
exists and uses the same product.

Redirects: unauthenticated → `/login`; authenticated visiting `/` → `/tree`;
non-admin on `/admin/*` → `/tree`.

---

## 4. Content model (so screens make sense)

Content is a **four-level tree**:

```
Skill   (e.g. "Холодные звонки")  — grouped under a funnel Stage
└── Topic  (e.g. "Основы")
    └── Lesson  (e.g. "Открытие звонка")
        └── Exercise  (one of 10 types)
```

- **Skills** belong to a **Stage** (funnel phase) — built-in stages: `preparation`,
  `discovery`, `engagement`, `closing`, `retention` (editable in admin). The tree
  groups skills by stage.
- A skill's status is derived from lessons completed: `available` → `in_progress`
  → `completed`. (There is **no** prerequisite-locking today; every skill is
  reachable. A redesign may *visualize* progression however it likes.)
- **Lessons** are ordered containers of **exercises**. XP is awarded by progress
  logic on correct answers.

---

## 5. The 10 exercise types (the heart of the learn loop)

Each lesson is a sequence of these. A redesign needs a distinct, clear treatment for
each interaction. AI-graded types return written feedback.

| Type | Graded by | What the user does |
|---|---|---|
| `choose_option` | rule | Pick the best response from options |
| `fill_blank` | rule | Fill the missing phrase in a dialog line |
| `reorder` | rule | Put steps/lines into the correct order |
| `match_pairs` | rule (partial credit) | Connect items into correct pairs |
| `categorize` | rule (partial credit) | Sort items into categories |
| `spot_mistake` | **AI** | Find the mistake in a line and explain it |
| `rewrite` | **AI** | Rewrite a weak message into a better one |
| `ai_dialogue` | **AI** | Hold a short multi-turn conversation with an AI client (text or voice) inside the lesson |
| `evaluate_call` | **AI** | Read a call transcript and assess it |
| `free_text` | **AI** | Answer an open question in your own words |

**Session player essentials** (function, not layout):
- One exercise on screen at a time; you submit, see a correctness result, optionally
  read AI feedback, then continue.
- **Hearts**: a small pool per session, spent on mistakes — a fail/tension mechanic.
- A running **progress** indicator across the lesson.
- A **completion** moment: celebration + a summary of XP earned, accuracy, time, and
  hearts left, then a way back to the path.

These moments (the per-exercise feedback and the completion celebration) are the
emotional peaks of the app — strong candidates for the redesign to elevate.

---

## 6. The AI Dialog feature (the practice loop, in detail)

This is the product's signature feature. Two depths exist on the same content.

**Structure:**
- **Bundle** = a themed pack tied to a skill (e.g. "Холодные звонки"). Has a title,
  description, an icon, and contains modes.
- **Mode** = a specific scenario / AI persona within a bundle (e.g. "Обход секретаря",
  "Skeptic Sergey — VP, objections"). Each mode is the AI's character + the rules for
  how the conversation is judged.
- **Session** = one user's run through a mode: the message history, a status
  (active / completed / abandoned), the final feedback, and XP earned.

**Text chat mode** contains:
- The live conversation between user and AI client.
- A **session-history** panel: a "new dialog" action, past sessions grouped by date
  (today / yesterday / N days ago), each showing the mode, bundle, message count and
  XP; clicking one reloads it.
- A typing/working indicator while the AI thinks.
- An end-of-call **feedback** view: written critique + an XP reward.

**Voice call mode** — the richest, most dynamic screen in the app. It's a live phone
call with the AI. The redesign must express a **pipeline of call states** clearly and
beautifully (whatever the visual metaphor):
- States the user moves through: *idle / ready* → *dialing* → *connected: listening* →
  *user speaking* → *processing (AI thinking)* → *AI speaking* → optionally *error* →
  *call ended*.
- A **live transcript** of both sides, including in-progress (interim) speech and the
  ability for the user to **interrupt** the AI mid-sentence (interrupted AI replies are
  marked as such).
- A live **call timer** and a **voice-minute quota** indicator (daily/monthly minutes —
  there's a usage cap).
- Call affordances: start the call, hang up, call again, and open the post-call review.
- Audio/haptic cues exist (ringing, connect buzz, hang-up tone) and an
  unsupported-browser fallback.
- A post-call **feedback** review with the written critique and XP.
- A featured **NPC mentor** concept exists on the dialog list (a named persona that
  challenges the user) — a hook the redesign can lean into.

**Feedback & XP behavior worth knowing:** the AI ends the call itself (logical end, or
abruptly on bad user behavior). Feedback is honest and cites the conversation. XP (0–100)
is composed from confidence/tone, argument structure, objection handling, and goal
achievement. A call with no user messages is abandoned — no feedback, no XP.

---

## 7. Gamification signals (must stay legible across the whole app)

These recur everywhere and are the motivational spine. A redesign can reinvent how
they look but must keep them present and readable:

- **Streak** — consecutive-day flame/counter. Surfaces in the top bar, profile, tree.
- **XP** — earned per exercise and per dialog; weekly XP feeds the league.
- **Hearts** — limited mistakes per lesson session.
- **Levels** — a user level shown on the profile chip.
- **Achievements** — unlockable badges; locked vs unlocked states; celebratory toasts
  when earned.
- **Leagues** — tiered weekly competition (Bronze / Silver / Gold / Diamond), with
  promotion and demotion.
- **Mastery rings / levels per technique** — progress on each guidebook technique.
- **Voice-minute quotas** — daily and monthly caps on voice practice.

---

## 8. Screen contents (what lives on each — not how it's arranged)

### Landing `/`
A short pitch (headline + supporting line), a few feature highlights (real scenarios,
AI grading, streaks & leagues, guidebook), and one primary call-to-action to start.

### Login / Register
Email + password, inline validation errors, Google OAuth, and a link between the two
forms. The Sellevate brand identity shows here.

### Onboarding `/onboarding`
First-run setup that establishes the user's persona and which skills they're enrolled in.

### Skill tree `/tree` (main hub)
- A browsable list of **skills grouped by funnel stage**, each stage showing a
  completion ratio and collapsible.
- The selected skill's **lesson path** — the ordered chain of lessons with progress
  and a clear "what's next".
- The user's **headline stats** (current streak, total XP, weekly XP).
This is the home screen and the most-visited surface — the best place to set the tone.

### Exercise session `/session/[lessonId]`
The lesson player (see §5): exercise sequence, hearts, progress, per-exercise result &
AI feedback, and the completion celebration with its stats summary.

### Dialog list `/dialog`
The catalog of **bundles** (each with icon, title, description, and progress signals
like completed dialogs / average score), plus the featured **NPC mentor** call-to-action.

### Mode selection `/dialog/[bundleId]`
The bundle's **modes**, each offering a **Chat** and a **Voice** entry point, with a
description of the scenario/persona. Empty state when a bundle has no modes yet.

### Text chat / Voice call
See §6 in full.

### Guidebook `/guidebook`
- A **searchable, filterable** library of sales **techniques** (search box, tag pills,
  filter by skill with counts).
- Each technique card shows its **mastery** (a level / ring), associated skill & tags,
  a "new" marker, a difficulty signal, a summary, and expands to: full explanation, an
  **example dialogue** with annotations, a **case study** with metrics, and a **coach**
  persona (named character with a quote and challenge prompts).

### Single technique `/reference/[id]`
A full standalone page for one technique (same content as the expanded card).

### League `/league`
- The current **tier** (with its identity) and a description.
- A dismissible **promotion/demotion outcome** banner.
- A **countdown** to week's end.
- The user's **current rank** and weekly XP.
- A **leaderboard**: ranked participants with avatars, names ("you" marked), and XP;
  **zones** marking the promotion band, the safe band, and the relegation band.
- A CTA nudging the user back into lessons to climb.

### Friends `/friends`
Four areas:
- **Друзья** — friend list + a user **search**, plus a **friend activity feed**.
- **Заявки** — incoming/outgoing friend **requests** with accept/decline (and a count badge).
- **Лидерборд** — a **friends-only leaderboard**.
- **Чаты** — conversation list + thread + composer (direct messaging).
Sub-pages: a friend's **public profile** and a full **DM thread** view.

### Profile `/profile`
- Identity header: avatar, name, email, persona.
- A **stats** set: streak, total XP, personal record, accuracy.
- **Skills-completed** progress.
- **Voice minutes**: daily + monthly quota usage.
- **Achievements**: a grid of badges with locked/unlocked states and tooltips.
- **Skill enrollment**: searchable list with per-skill toggles.
- **Settings**: theme toggle, admin entry (if admin), logout.

### Admin `/admin/*`
A content-management area: dashboard plus CRUD for skills, stages, topics, lessons,
reference techniques, dialog bundles/modes (incl. their AI prompts), bulk import of
whole content/dialog trees, voice-usage analytics, and user management.

---

## 9. Fixed product facts the redesign must respect

- **UI language is Russian.** All user-facing copy is in Russian (examples above are
  the real strings). Keep it Russian unless told otherwise.
- **Avatars are procedural**, generated from a seed — there are no uploaded photos.
  A redesign can change the avatar *style* but should assume "generated identity mark",
  not "user photo".
- **Light and dark** appearance both exist and are expected.
- **Two brand names currently coexist** ("SalesTrainer" in-app, "Sellevate" on auth) —
  this is debt; a redesign is a good moment to unify the brand.
- **Two AI depths** (in-lesson `ai_dialogue` and the standalone dialog feature) and
  **two AI modalities** (text + voice) all need first-class treatment.
- The six core loops (§2) and all gamification signals (§7) must survive any redesign.

---

## 10. What this document intentionally omits

By design, you will **not** find here: the current color palette, design tokens,
typography scale, spacing/radius/shadow systems, animation specifics, component
internals, grid/column layouts, where any element sits on the page, the tech-stack
styling choices, or file/CSS structure. None of that is a constraint on the new design.

Bring a new, strong visual direction. The only fixed inputs are: **the features, the
content model, the screen contents, the gamification signals, and the Russian-language,
procedural-avatar, light/dark product facts** described above.
