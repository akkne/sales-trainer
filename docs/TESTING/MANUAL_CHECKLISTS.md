# Manual Test Checklists

Consolidated manual testing checklists for major features.

---

## AI Dialog Testing

### Prerequisites
- Backend running with valid `OpenAI:ApiKey`
- MongoDB running
- Test user logged in

### Bundle & Mode Display
- [ ] Navigate to `/dialog` tab
- [ ] Bundles grid displays
- [ ] Click bundle → modes page
- [ ] Click mode → chat page

### Chat Session
- [ ] Session starts with AI first message
- [ ] User can send messages
- [ ] AI responds, typing indicator shows
- [ ] Messages display correctly (user=right, AI=left)
- [ ] "Завершить диалог" appears after stop signal
- [ ] Feedback modal shows with XP

### Error Handling
- [ ] Close button returns to `/dialog`
- [ ] Network errors handled gracefully
- [ ] No API key → "Практика диалогов пока недоступна"

### Admin Panel
- [ ] Create/edit/delete bundles
- [ ] Create/edit/delete modes with prompts
- [ ] Skill association works

---

## Voice Roleplay Testing

### Prerequisites
- OpenAI-compatible key (`OpenAI:ApiKey`) + Yandex TTS key (`YandexTts:ApiKey`) configured
- Voice enabled on mode in admin
- Chrome/Edge desktop (Web Speech API)

### Configuration
- [ ] `/dialog/voice/config` returns correct enabled status
- [ ] Voice page shows "недоступен" screen when mode doesn't support voice

### Voice Pipeline
- [ ] «Позвонить» requests mic permission, status goes Соединение → На связи
- [ ] Live subtitles: own words appear while speaking (dashed interim line),
      become a committed entry on pause
- [ ] AI reply streams into subtitles chunk by chunk and audio plays once
      (phrase NOT repeated several times)
- [ ] Mic works again after AI playback — second phrase is recognized
      (regression: pause/resume race used to kill the mic after turn 1)
- [ ] Barge-in: talking over the AI stops playback and processes the new phrase

### Call Termination (endCall)
- [ ] Swearing / rudeness → persona says a closing line and hangs up,
      feedback modal opens automatically
- [ ] Feedback cites actual quotes from the call, low XP for a failed call
- [ ] Pick up and hang up without saying anything → NO feedback modal,
      no XP, session marked `abandoned` (backend returns 204)

### Error Handling
- [ ] Denied mic → error message shown
- [ ] Daily/monthly limit exceeded → 429 message with limit minutes
- [ ] Leaving the page mid-call completes the session

---

## New Exercise Types Testing

### Type: Ordering
- [ ] Items shuffled on load
- [ ] Drag-and-drop works
- [ ] Up/down buttons work
- [ ] Correct order → score 100
- [ ] Wrong order → score 0

### Type: Matching
- [ ] Click left → click right to connect
- [ ] Lines drawn between pairs
- [ ] Can remove connections
- [ ] Partial credit for partial correct

### Type: Categorizing
- [ ] Items in center pool
- [ ] Drag to category buckets
- [ ] Click cycles categories
- [ ] Partial credit scoring

### Type: Find Error
- [ ] Dialog lines clickable
- [ ] Selection highlights
- [ ] Explanation textarea if required
- [ ] Fix options if provided
- [ ] AI evaluates explanation

### Type: Rewrite Better
- [ ] Original text in gray box
- [ ] Character counter
- [ ] Length validation
- [ ] AI rating + feedback

### Type: AI Dialog (exercise)
- [ ] Persona header shows
- [ ] Multi-turn conversation
- [ ] "Завершить" after minTurns
- [ ] Final rating shown

### Type: Rate Call
- [ ] Transcript displayed
- [ ] Rating buttons for criteria
- [ ] Comment optional
- [ ] AI comparison shown

### Type: Written Answer
- [ ] Prompt displayed
- [ ] Length validation
- [ ] AI rating + feedback

### Integration
- [ ] Mixed lessons work
- [ ] Hearts deducted on wrong
- [ ] XP awarded on correct
- [ ] Skip works for all types
- [ ] Mobile responsive

### Result Banner — AI-evaluated exercises (Free Text, Rewrite, Spot Mistake, Rate Call, AI Dialog)
- [ ] Banner expands when `aiFeedback` is present
- [ ] Rating badge shows `N/10` in the accent color
- [ ] Title colors switch: pass → good, 40–79 score → warn ("Почти"), below 40 → bad ("Не совсем")
- [ ] Long feedback scrolls within the card; bottom fade appears only when overflow
- [ ] Exercise content has enough bottom padding and isn't obscured by the taller banner
- [ ] Compact banner (no `aiFeedback`) still shows explanation or fallback text on one line

---

## Seeder & Import Testing

### Skills Import
- [ ] JSON upload parses correctly
- [ ] CSV upload parses correctly
- [ ] Upsert by slug works
- [ ] Error messages clear

### Lessons Import
- [ ] Skill selection dropdown works
- [ ] Exercises nested correctly
- [ ] All 11 types supported
- [ ] Upsert by title works

---

## Session & Exercise Testing

### Session Flow
- [ ] Progress bar updates
- [ ] Hearts decrease on wrong
- [ ] 0 hearts → failure screen
- [ ] Completion screen shows stats
- [ ] "Next lesson" button works

### Keyboard Controls
- [ ] 1-4 select options
- [ ] Enter submits/continues
- [ ] Space submits/continues
- [ ] Works in fill-blank too

### Achievement Notifications
- [ ] Toast appears on unlock
- [ ] Auto-dismisses after 4s
- [ ] Queue drains correctly
- [ ] Click dismisses early

---

## Handbook ("Коллекция") Testing

### Prerequisites
- Backend running with `AddTechniques` migration applied
- Logged-in user

### List view
- [ ] Navigate to `/guidebook`
- [ ] Header counter shows `СПРАВОЧНИК · N ТЕХНИК` matching seed count (≥4)
- [ ] StatTiles `Освоено / Мастер / Новых` sum to total
- [ ] Category pills render with correct colors from `/techniques/meta`
- [ ] "Все" is selected by default
- [ ] Clicking a category filters the grid
- [ ] Search box filters by name/tag/body
- [ ] Empty result state renders when nothing matches

### Mastery + "Новое" chip
- [ ] Card ring shows `L1` and 0% stroke for freshly-seeded user
- [ ] `Новое` chip appears on cards with `isNew: true`
- [ ] After expanding a new card, calling `/techniques/:slug/seen` removes the chip on next fetch
- [ ] `userCounts.unseen` decreases after mark-seen

### Expanded detail
- [ ] Body markdown renders
- [ ] Dialog turns render with correct side bubbles (me=right/indigo, them=left/grey)
- [ ] Annotations (e.g. `[S]`, `[P → I]`) appear inline on turns that have them
- [ ] Case blocks render `title · body`
- [ ] Coach sidecar renders when present (GeoAvatar, name, role, quote, challenges)
- [ ] "Связанный навык →" links to `/skill/:iconicName` only when `primarySkillIconicName` is set
- [ ] Re-clicking the card collapses it

### Admin CRUD (role=Admin or SuperAdmin)
- [ ] `GET /admin/techniques` returns the full list
- [ ] `POST /admin/techniques` with duplicate slug → 409
- [ ] `POST` with unknown `categorySlug` → 400
- [ ] `PUT` replaces nested dialog/cases/coach atomically
- [ ] `DELETE` cascades to dialog/cases/coach/skills rows
