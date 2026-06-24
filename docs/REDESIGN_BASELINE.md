# Redesign Baseline — Feature List Only

> **Purpose.** This is the brief for a redesign agent. It lists **only what the
> product can do** — its features and capabilities. Nothing else.
>
> It deliberately says **nothing** about: screens, pages, routes, layout, where
> anything is positioned, what is centered, what sits next to what, panels, bars,
> grids, menus, navigation structure, colors, fonts, spacing, components, or any
> arrangement of elements. **None of that is given to you, and none of it is a
> constraint.**
>
> You decide, from scratch and with your own judgment, how the product is
> structured, navigated, and laid out. The list below is the *only* fixed input:
> the features must exist. How a user reaches them, how they're grouped, and how
> any of it looks is entirely yours to invent.

Last updated: 2026-06-24.

---

## What the product is

SalesTrainer is a sales-training web app. Users learn sales skills, practice
realistic conversations with an AI client (by text and by voice), compete with
others, add friends, study a reference of sales techniques, and track their
progress through gamification. The UI language is Russian.

---

## Features

### Learning
- A library of sales **skills** to learn.
- Skills are organized by sales-funnel stage (preparation, discovery, engagement, closing, retention).
- Each skill contains **lessons**; each lesson is a sequence of **exercises**.
- A skill progresses through states as its lessons are completed (available → in progress → completed). All skills are reachable; there is no prerequisite locking.
- **10 exercise types**, each a distinct interaction:
  - *Choose the best option* — pick the best response from choices (auto-graded).
  - *Fill the blank* — complete a missing phrase in a dialog line (auto-graded).
  - *Reorder* — put steps or lines into the correct order (auto-graded).
  - *Match pairs* — connect items into correct pairs (auto-graded, partial credit).
  - *Categorize* — sort items into categories (auto-graded, partial credit).
  - *Spot the mistake* — find and explain a mistake in a line (AI-graded).
  - *Rewrite* — improve a weak message (AI-graded).
  - *In-lesson AI dialogue* — hold a short conversation with an AI client inside the lesson, by text or voice (AI-graded).
  - *Evaluate a call* — read a transcript and assess it (AI-graded).
  - *Free text* — answer an open question in your own words (AI-graded).
- Solving exercises one at a time, with a correctness result after each.
- Written **AI feedback** on AI-graded exercises.
- A limited pool of **hearts** per lesson, spent on mistakes.
- **XP** awarded for correct answers.
- A lesson **completion** result summarizing XP earned, accuracy, time, and hearts remaining.

### AI practice conversations
- **Dialog bundles**: themed practice packs tied to a skill.
- **Modes** inside a bundle: specific scenarios / AI personas, each with its own character and grading rules.
- **Text chat** practice: a live conversation with the AI client.
- **Voice call** practice: a real-time spoken conversation with the AI client.
- Live voice call states: ready, dialing, connected, listening, user speaking, AI processing, AI speaking, error, call ended.
- Live **transcript** of both sides, including in-progress (interim) speech.
- Ability to **interrupt** the AI mid-sentence; interrupted AI replies are marked.
- A live **call timer**.
- Audio/haptic cues for ringing, connecting, and hanging up; an unsupported-browser fallback.
- The AI **ends the call itself** (logical end, or abruptly on bad user behavior).
- **Session history**: past practice sessions grouped by date, each showing its mode, bundle, message count and XP; reopenable.
- A working/typing indicator while the AI thinks.
- Post-conversation **feedback**: an honest written critique citing the conversation, plus an XP reward (0–100, composed from confidence/tone, argument structure, objection handling, and goal achievement). A conversation with no user messages is abandoned — no feedback, no XP.
- A featured **NPC mentor** persona that challenges the user.

### Reference / guidebook
- A **library of sales techniques**.
- **Search** and **filter** techniques (by text, by tag, by skill).
- Each technique provides: a full explanation, an annotated example dialogue, a case study with metrics, associated skill and tags, a difficulty signal, a "new" marker, and a **coach persona** (a named character with a quote and challenge prompts).
- **Mastery** tracking per technique (a level that progresses).

### Competition
- **Weekly leagues** with tiers (Bronze, Silver, Gold, Diamond).
- A **leaderboard** of participants with their weekly XP.
- The user's current **rank** and weekly XP.
- **Promotion / safe / relegation** bands.
- A **countdown** to the end of the week.
- End-of-week **promotion / demotion** outcome.

### Social
- A **friends** list.
- **Search** for other users.
- Friend **requests** (incoming and outgoing) with accept / decline.
- A **friend activity feed**.
- A **friends-only leaderboard**.
- **Direct messaging** with friends (conversations, threads, sending messages).
- Viewing another user's **public profile**.

### Discuss (community forum)
- A community **Q&A forum** where users ask and answer questions.
- **Threads**: a title + body authored by a user, with one or more tags.
- **Replies** to a thread.
- The thread author (or an admin) can mark one reply as the **accepted answer**, which flags the thread as **solved** (Решено).
- **Upvotes** on both threads and replies (upvote-only; one vote per user per item).
- **Tags**: a curated, admin-managed catalog plus user free-form tags created on the fly, and a sense of **popular tags**.
- **Image attachments**: up to 10 images per thread or per reply (author-managed).
- **Sorting** of threads: *hot* (default — a time-decayed engagement score), *new*, and *unanswered*.
- **Pinned** and **hot** threads (admin-flagged) surface ahead of others.
- Forum **stats**: total threads, total replies, and top authors of the week.
- **Moderation** (admin): delete or pin any thread, delete replies, toggle the "hot" flag.

### Profile & identity
- A user profile with name, email, avatar, and persona.
- Personal **stats**: streak, total XP, personal record, accuracy.
- **Skills-completed** progress.
- **Voice-minute quota** usage (daily and monthly caps on voice practice).
- **Achievements**: unlockable badges with locked and unlocked states, and celebratory notifications when earned.
- **Skill enrollment**: choosing which skills to be enrolled in.
- **Settings**: light/dark appearance toggle, logout, and (for admins) an entry to the admin area.

### Gamification signals (recur throughout)
- **Streak** — consecutive-day counter.
- **XP** — earned from exercises and conversations; weekly XP feeds the leagues.
- **Hearts** — limited mistakes per lesson.
- **Levels** — a user level.
- **Achievements** — unlockable badges.
- **Leagues** — tiered weekly competition.
- **Mastery** — per-technique progress.
- **Voice-minute quotas** — daily and monthly voice-practice caps.

### Accounts & onboarding
- **Register** and **log in** with email + password.
- **Google OAuth** sign-in.
- A **first-run onboarding** that establishes the user's persona and initial skill enrollment.

### Admin
- A content-management area for managing all of the above content: skills, funnel stages, topics, lessons, reference techniques, dialog bundles and modes (including their AI prompts), bulk import of whole content/dialog trees, voice-usage analytics, and user management.

---

## Fixed product facts to respect

- **UI language is Russian.** All user-facing copy is Russian.
- **Avatars are procedural**, generated from a seed — no uploaded photos. Assume "generated identity mark", not "user photo". The style is open.
- **Light and dark** appearance both exist and are expected.
- **Two AI modalities** (text and voice) and **two AI depths** (in-lesson dialogue and standalone practice) all need first-class treatment.
- All features listed above must survive the redesign.

---

## What this document intentionally omits

Everything about form. There is deliberately **no** route map, no list of screens,
no description of what lives on any page, no navigation model, no layout, no
positioning, no "this is centered / this is in the top bar / this is a panel", no
colors, tokens, typography, spacing, shadows, animation, components, or file
structure. The redesign agent is expected to invent all of it from the feature
list alone.
