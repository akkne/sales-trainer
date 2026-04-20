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
- Deepgram + ElevenLabs API keys configured
- Voice enabled on mode in admin

### Configuration
- [ ] `/dialog/voice/config` returns correct enabled status
- [ ] Voice button hidden when mode doesn't support voice

### Voice Pipeline
- [ ] Mic button requests permission
- [ ] Green ring while speaking
- [ ] Transcript shown on speech end
- [ ] AI audio plays automatically
- [ ] Mic re-enabled after playback

### Button States
- Idle → gray mic
- Listening → green pulsing
- Processing → spinner
- Playing → speaker icon
- Error → red text, recovers

### Error Handling
- [ ] Denied mic → toast, text still works
- [ ] Connection lost → reconnect attempt
- [ ] Network timeout → error shown, transcript preserved

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
