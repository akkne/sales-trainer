# Lesson Execution Redesign — Duolingo Green Scheme

## Source

Design taken from Google Stitch project **"Игровая классика (Gamified Sales) - PRD"**  
(`projects/5546133593140033209`)

Screens used:
- `854f31d4a31c455bb111e9b0846bffe7` — Skill Tree (Home)
- `25f72cf9a5e34fb0828f1b9eb36f77e2` — Skill Path
- `717fe6255b5c444b87da88eb1cc7499a` — Exercise

---

## Design System (Green Scheme)

### Colors

| Token | Value | Usage |
|---|---|---|
| `primary` | `#59c705` / `#58CC02` | Buttons, active nodes, progress |
| `primary-shadow` | `#58A700` / `#4a9e04` | Button 3D shadow |
| `accent-yellow` | `#FFC800` | Completed nodes, streak |
| `accent-yellow-shadow` | `#E5B400` | Yellow button shadow |
| `accent-blue` | `#1CB0F6` | XP, selected option highlight |
| `accent-blue-shadow` | `#1899D6` | Blue button shadow |
| `accent-red` | `#FF4B4B` | Hearts, incorrect answer |
| `accent-red-shadow` | `#EA2B2B` | Red button shadow |
| `surface` | `#F7F7F7` | Card backgrounds, locked nodes |
| `text-main` | `#4B4B4B` | Body text |
| `muted` | `#AFAFAF` | Disabled text, icons |
| `border-color` | `#E5E5E5` | Card borders |

### Typography

- **Display / Headings**: Manrope (bold 700–800)
- **Body**: Manrope (regular 500–600)
- Replace current Space Grotesk + Lexend → Manrope everywhere

### Button Style (3D Press Effect)

```css
/* Normal state */
border-b-4 border-[shadow-color] rounded-full font-extrabold uppercase tracking-wider

/* Active/press */
active:translate-y-1 active:border-b-0

/* Hover */
hover:brightness-110
```

### Node Types

| State | Visual |
|---|---|
| Completed | Yellow circle `#FFC800` + gold medal badge |
| Active | Green circle `#59c705` + pulsing ring `animate-ping` + popover card |
| Locked | Gray circle `#F7F7F7` + `🔒` icon + `cursor-not-allowed` |
| Boss/Chest | Rounded-2xl rotated 45° gray square + trophy icon |

---

## Screen 1: Skill Tree (`/tree`)

### Current vs Target

**Current**: Simple vertical list of SkillNode components.  
**Target**: Duolingo-style path with section header cards, zigzag nodes, active popover.

### Layout

```
┌──────────────────────────────────────────────────────┐
│  Header: logo + nav icons (guidebook, league, profile) │
└──────────────────────────────────────────────────────┘
┌─────────────────────────────┐  ┌────────────────────┐
│ Section 1 banner (green)     │  │ Sidebar:           │
│ "Основы продаж"  1/5        │  │ 🔥 Streak card     │
│                              │  │ ⚡ XP card         │
│  [zigzag path with nodes]    │  │ 🏆 League card     │
│  ● completed (yellow)        │  │ Mascot + tip       │
│    ↓ active (green+pulse)    │  │                    │
│      ↓ locked (gray)         │  │                    │
│        ↓ locked              │  │                    │
│  [chest node]                │  │                    │
│                              │  │                    │
│ Section 2 banner (locked)    │  │                    │
└─────────────────────────────┘  └────────────────────┘
```

### Component Changes

**`/app/(main)/tree/page.tsx`**  
Refactor to group skills by section. Each section renders:
1. Section header card (`bg-primary` if unlocked, `bg-surface border` if locked)
2. Zigzag node path with `relative` container + absolute vertical line

**`/components/ui/SkillNode.tsx`**  
Extend with:
- `variant: 'completed' | 'active' | 'locked'`
- Popover card on active node (absolute positioned, shows above node)
- Pulsing ring (`animate-ping`) for active state
- 3D shadow styles for each variant
- Position offset: `node-center | node-left | node-right` (margin-based zigzag)

**`/components/layout/StatsWidget.tsx`**  
Redesign into 3 separate cards (streak / XP / league) with border-2 + hover effects.  
Add mascot card below with motivational text.

### Active Node Popover

```tsx
// Shown by default for the current active node
<div className="absolute -top-32 w-64 bg-white border-2 border-border-color rounded-2xl p-4 shadow-lg">
  <h3>{lesson.title}</h3>
  <ProgressBar value={progress} max={total} />
  <span>{progress}/{total}</span>
  <Button href={`/skill/${skillId}`}>Старт</Button>
</div>
```

---

## Screen 2: Skill Path (`/skill/[id]`)

### Current vs Target

**Current**: Plain list of lesson cards with lock/checkmark states.  
**Target**: Visual path with SVG curved connector lines and individual lesson nodes.

### Layout

```
┌─────────────────────────────────────┐ ┌──────────────┐
│ Header: ← back | skill title         │ │ Stats row    │
│                                       │ │              │
│ [SVG curved path background]          │ │ Skill card:  │
│                                       │ │ - description│
│   ●  completed (yellow)               │ │ - progress   │
│   |  SVG path line                    │ │   bar        │
│   ●  active (green+pulse+popover)     │ │              │
│   |                                   │ │ 📖 Guidebook │
│   🔒 locked                           │ │    link      │
│   |                                   │              │
│   🔒 locked                           │              │
│                                       │
│   🏆 boss node (rotated square)       │
└───────────────────────────────────────┘
```

### Component Changes

**`/app/(main)/skill/[id]/page.tsx`**  
Replace lesson list with visual node path:
- `<svg>` overlay with curved `<path>` connectors between nodes
- Each lesson = positioned node with title label below
- Alternating left/right offsets for zigzag effect
- Active node shows popover with "Старт" button

---

## Screen 3: Exercise (`/exercise/[id]`)

### Current vs Target

**Current**: Has exercise types, ExerciseResultBanner, but plain header.  
**Target**: Duolingo-exact header + character speech bubble + numbered options + slide-up banners.

### Header (replace current)

```tsx
<header className="w-full max-w-[800px] px-4 py-6 flex items-center gap-4 sticky top-0 bg-white">
  {/* Close button */}
  <button><span>close</span></button>
  
  {/* Progress bar */}
  <div className="flex-grow h-4 bg-surface rounded-full border-2 border-border-color">
    <div className="h-full bg-primary rounded-full" style={{ width: `${progress}%` }} />
  </div>
  
  {/* Hearts */}
  <div className="flex items-center gap-1 text-accent-red font-bold">
    <span>❤️</span>
    <span>{hearts}</span>
  </div>
</header>
```

### Character Speech Bubble (for MultipleChoice / FillBlank)

When exercise has a `situation` field, show:
```tsx
<div className="flex items-end gap-4 mb-8">
  {/* Character portrait */}
  <div className="w-24 h-24 rounded-2xl overflow-hidden border-2 border-border-color hidden md:block">
    <img src={characterImage} />
  </div>
  
  {/* Speech bubble */}
  <div className="relative bg-surface p-4 rounded-2xl border-2 border-border-color flex-grow">
    <div className="absolute -left-3 bottom-6 w-4 h-4 bg-surface border-l-2 border-b-2 border-border-color rotate-45" />
    <p>{situation}</p>
  </div>
</div>
```

### Multiple Choice Options (numbered badges)

```tsx
<button className="w-full text-left p-4 rounded-2xl border-2 border-border-color bg-white
                   shadow-[0_4px_0_0_#E5E5E5] flex items-center gap-3
                   [&.selected]:border-accent-blue [&.selected]:bg-[#e8f7fe]">
  <div className="w-8 h-8 rounded-lg border-2 border-border-color flex items-center justify-center font-bold">
    {index + 1}
  </div>
  <span>{option.text}</span>
</button>
```

### Validation Banners (slide-up)

Replace current `ExerciseResultBanner` with two variants:

**Correct** — `bg-[#d7ffb8] border-[#b5f283]`:
```tsx
<div className="fixed bottom-0 left-0 w-full p-4 slide-up">
  <div className="flex justify-between items-center max-w-[800px] mx-auto">
    <div className="flex items-center gap-4 text-[#46a004]">
      <div className="w-16 h-16 bg-white rounded-full flex items-center justify-center">
        <span>✓ check_circle</span>
      </div>
      <div>
        <h3 className="text-2xl font-extrabold">Отлично!</h3>
        <p>{explanation}</p>
      </div>
    </div>
    <Button onClick={onContinue}>ПРОДОЛЖИТЬ</Button>
  </div>
</div>
```

**Incorrect** — `bg-[#ffdfe0] border-[#ffc1c3]`:
```tsx
// Same structure but red colors + "Не совсем так" + correct answer shown
```

---

## Font Migration

Add Manrope to `layout.tsx`:

```tsx
import { Manrope } from 'next/font/google'
const manrope = Manrope({ subsets: ['latin', 'cyrillic'], variable: '--font-manrope' })
```

Update `globals.css`:
```css
body { font-family: var(--font-manrope), sans-serif; }
```

Keep Space Grotesk + Lexend loading to avoid breaking existing pages until full migration.

---

## Implementation Order

1. **Design tokens** — add Manrope, update CSS variables, define token constants
2. **SkillNode redesign** — completed/active/locked variants, popover, zigzag offsets
3. **StatsWidget redesign** — 3 separate cards + mascot block
4. **Skill Tree page** — section banners, vertical path line, compose new SkillNode
5. **Skill Path page** — SVG path, lesson nodes, sidebar
6. **Exercise header** — replace header component with progress bar + hearts
7. **Character speech bubble** — add to MultipleChoice and FillBlank
8. **Numbered option buttons** — update option button style
9. **Validation banners** — replace ExerciseResultBanner with new slide-up design
10. **E2E smoke test** — play through a lesson start to finish

---

## Files to Create/Modify

| File | Action |
|---|---|
| `app/layout.tsx` | Add Manrope font |
| `app/globals.css` | Add font-family, green token variables |
| `app/(main)/tree/page.tsx` | Full redesign — section banners + zigzag path |
| `app/(main)/skill/[id]/page.tsx` | Full redesign — SVG path + lesson nodes |
| `app/(main)/exercise/[id]/page.tsx` | Replace header, add hearts state |
| `components/ui/SkillNode.tsx` | Extended variants + popover + 3D styles |
| `components/layout/StatsWidget.tsx` | 3-card design + mascot |
| `components/exercise/MultipleChoiceExercise.tsx` | Speech bubble + numbered options |
| `components/exercise/FillBlankExercise.tsx` | Speech bubble + numbered options |
| `components/exercise/ExerciseResultBanner.tsx` | Slide-up Duolingo banners |

---

## Notes

- Hearts (lives) are **visual only** in v1 — no penalty logic, always start at 4
- Character portrait for speech bubble: use a placeholder image from public/ or a generic avatar
- SVG curved paths on Skill Path page: use `C` (cubic bezier) curves matching Stitch design
- Mascot image on sidebar: use existing owl image URL from Stitch or a local asset
- Zigzag positions: implement as CSS utility classes (`node-center`, `node-left`, `node-right`)
