е# Frontend Redesign Plan

## Overview

Full frontend redesign based on the **Sales Training Platform** design from Stitch (Project ID: 7882200878184662893).

### Design System Summary

**Palette — "Warm Precision":**
- Primary: `#006a2d` (deep green)
- Primary Container: `#5dfd89` (bright green)
- Secondary: `#006942` / Container: `#7afbb7`
- Tertiary: `#006573` (teal) / Container: `#00e0fd`
- Surface: `#f7f6f4` (warm off-white)
- Surface Container: `#e8e8e6`
- On-Surface: `#2e2f2e`
- Error: `#b02500`

**Typography:**
- Headlines: Plus Jakarta Sans (font-headline)
- Body/Labels: Manrope (font-body)

**Corners:**
- Cards: `1rem` (rounded-DEFAULT)
- Large elements: `2rem` (rounded-lg)
- Pills/badges: `9999px` (rounded-full)

**"No-Line" Rule:** NO 1px borders for sectioning — use background color shifts only.

---

## Phase 1: Design System Foundation ✅

### 1.1 Tailwind Config Update
**File:** `src/frontend/app/globals.css` (using CSS custom properties with Tailwind v4)

Changes:
- [x] Add new color palette (primary, secondary, tertiary, surface hierarchy)
- [x] Add font families: Plus Jakarta Sans, Manrope
- [x] Update border radius defaults (DEFAULT: 1rem, lg: 2rem, xl: 3rem, full: 9999px)
- [x] Add spacing scale adjustments

### 1.2 Global Styles & Fonts
**File:** `src/frontend/app/layout.tsx`, `src/frontend/app/globals.css`

Changes:
- [x] Import Google Fonts: Plus Jakarta Sans, Manrope
- [x] Import Material Symbols Outlined icon font
- [x] Set `font-family: 'Manrope'` as body default
- [x] Set background color to `#f7f6f4` (surface)
- [x] Add `.font-headline` utility class for Plus Jakarta Sans
- [x] Remove old Duolingo-style green (`#58CC02`) references

### 1.3 Material Symbols Icons
**File:** `src/frontend/components/ui/Icon.tsx`

- [x] Create Icon component wrapper for Material Symbols Outlined
- [x] Support filled/outlined variants via `style` prop
- [x] Default settings: `'FILL' 0, 'wght' 400, 'GRAD' 0, 'opsz' 24`

---

## Phase 2: Layout Components ✅

### 2.1 Bottom Navigation Bar
**File:** `src/frontend/components/layout/BottomNav.tsx`

Current: Emoji icons, Duolingo green, Russian labels
New Design:
- [x] Replace emojis with Material Symbols icons:
  - `school` → Mastery/Путь
  - `trophy` → Leagues/Лига  
  - `menu_book` → Guidebook/Справочник
  - `forum` → Dialog/Диалог
  - `person` → Profile/Профиль
- [x] Update active color from `#58CC02` to `#006a2d` (primary)
- [x] Add `bg-surface` background with `border-t border-outline-variant`
- [x] Glassmorphism: `backdrop-filter: blur(20px)` + `rgba(255,255,255,0.8)`

### 2.2 Main Layout
**File:** `src/frontend/app/(main)/layout.tsx`

Current: White background, padding for bottom nav
New Design:
- [x] Change background to `#f7f6f4` (surface)
- [x] Add desktop sidebar (hidden on mobile)
- [x] Add top navigation bar for desktop
- [x] Keep mobile bottom nav

### 2.3 Desktop Sidebar Navigation
**File:** `src/frontend/components/layout/Sidebar.tsx`

From design "Mastery Dashboard":
- [x] User profile block with avatar, name, level badge
- [x] Navigation links with icons: Mastery, Leagues, Library, Analytics, Settings
- [x] Active state: `bg-primary-container text-primary`
- [x] Current path widget at bottom with "Continue Learning" CTA
- [x] `w-64` width, `bg-surface-container-low` background

### 2.4 Top App Bar (Desktop)
**File:** `src/frontend/components/layout/TopAppBar.tsx`

From design "Course Map":
- [x] Brand name "SalesMastery" left-aligned
- [x] Navigation links (hidden on mobile)
- [x] Right side: notification bell with red dot, achievements icon, user avatar
- [x] Sticky positioning, glassmorphism effect

---

## Phase 3: Authentication & Onboarding ✅

### 3.1 Onboarding Page
**File:** `src/frontend/app/(auth)/onboarding/page.tsx`

Current: 4 steps, emoji icons, Duolingo green buttons
New Design (from "Onboarding: Sales Type"):
- [x] Progress bar: 4px height, primary fill on `surface-container-highest` track
- [x] Step indicator: "Step X of Y" label above progress
- [x] Page header: `font-headline text-3xl font-bold` + description paragraph
- [x] Selection cards: Bento grid layout (`grid-cols-auto-fit minmax(280px, 1fr)`)
- [x] Card states:
  - Default: `bg-surface-container` no border
  - Hover: `border-outline-variant bg-surface-container-high`
  - Selected: `border-2 border-primary bg-primary-container`
- [x] Material icons instead of emojis (cloud_done, home_work, shopping_bag, etc.)
- [x] "Most Popular" badge: pill shape, `bg-primary text-on-primary`
- [x] Info hint row with `info` icon
- [x] CTA button: `rounded-full bg-primary` with arrow icon suffix

**OUR categories to KEEP (not from design):**
- B2B SaaS, Розница, Недвижимость, Финансы, B2C
- Personas: SDR, Account Executive, Account Manager, Основатель, Другое
- Experience: Новичок, Опытный, Руководитель

### 3.2 Login Page
**File:** `src/frontend/app/(auth)/login/page.tsx`

- [x] Apply new surface background
- [x] Update Google login button styling
- [x] Add brand header with icon

### 3.3 Register Page
**File:** `src/frontend/app/(auth)/register/page.tsx`

- [x] Match login page styling
- [x] Consistent form input styling

---

## Phase 4: Main Pages ✅

### 4.1 Skill Tree Page (Mastery)
**File:** `src/frontend/app/(main)/tree/page.tsx`

Current: Left sidebar with skills, center lesson path, right stats
New Design (from "Mastery Dashboard"):
- [x] **Left sidebar:** Skill selector with mini progress bars
- [x] **Center content:**
  - Current module hero card: `bg-primary` with white text
  - Mastery level progress: `Level X/5` + percentage + progress bar
  - "Continue Lesson" CTA with gradient background
  - Secondary modules row: 2-column grid
- [x] **Right sidebar:** StatsWidget + Next Milestones panel
- [x] **Top stats row:** 3 cards (Daily Progress, Total XP, Streak)

### 4.2 Stats Widget
**File:** `src/frontend/components/layout/StatsWidget.tsx`

Current: Simple stats display
New Design:
- [x] Grid of stat cards: Daily Progress bar, Total XP (primary-container bg), Streak with bolt icon
- [x] `bg-surface-container rounded-2xl` containers
- [x] XP card: `bg-primary-container` highlight
- [x] Streak icon: Material Symbol `bolt`

### 4.3 Lesson Path Component
**File:** `src/frontend/components/ui/LessonPath.tsx`

Current: Circular nodes with lock/check icons
New Design:
- [x] Keep existing path structure but update styling
- [x] Progress ring with SVG stroke-dasharray for circular indicator
- [x] Update colors to new palette

---

## Phase 5: Course Map Page ✅

### 5.1 Skill Map Page
**File:** `src/frontend/app/(main)/skill/[id]/map/page.tsx`

Current: Linear lesson list with status badges
New Design (from "Course Map: Cold Calling"):
- [x] **Header card:** `bg-primary-container` with circular progress ring (SVG)
- [x] Progress text: "X out of Y lessons completed", "+N XP earned"
- [x] **Lesson cards list:** Vertical timeline layout
  - Completed: `bg-surface-container`, green checkmark badge
  - Up Next: `bg-secondary-container ring-2 ring-secondary`, "Up Next" pill, bolt icon on Start button
  - Locked: `bg-surface-container-low opacity-60`, lock icon badge, unlock hint text
- [x] XP badges: `bg-primary-container text-primary rounded-full`
- [x] Duration badges: `schedule` icon + "X min"
- [x] CTA buttons: "Review" (outline), "Start" (filled secondary), "Locked" (disabled)

---

## Phase 6: League Page ✅

### 6.1 League Page
**File:** `src/frontend/app/(main)/league/page.tsx`

Current: Tier emoji, countdown, participant list
New Design (from "The Arena: Refined Leaderboard"):
- [x] **League header card:** `bg-secondary-container`
  - `military_tech` icon + "The Arena" label
  - League name: `font-headline text-3xl`
  - Countdown timer with separate blocks (Days:Hours:Mins)
- [x] **Stat cards row:** 2-column grid
  - Your Rank card: `#N` with trend arrow
  - Status card: "Near Promotion" + XP to top 10 + next reward info
- [x] **Leaderboard table:**
  - Grid layout: `[3rem_1fr_auto]`
  - Top 3: Gold/Silver/Bronze rank badges
  - Promotion zone rows: `bg-[rgba(93,253,137,0.10)]`
  - Your row: `bg-[rgba(0,106,45,0.08)] border-left-3 border-primary`
  - Relegation zone rows: `bg-[rgba(176,37,0,0.07)]` with error text
  - Collapsed sections: "Participants 04-11" clickable rows
- [x] **Boost CTA banner:** `bg-tertiary-container` with module suggestion
- [x] Avatar rings for current user

---

## Phase 7: Guidebook Page ✅

### 7.1 Guidebook Page
**File:** `src/frontend/app/(main)/guidebook/page.tsx`

Current: Category chips, expandable cards, markdown content
New Design (from "Guidebook"):
- [x] **Top nav integration:** Active state for "Guidebook" link
- [x] **Sub nav row:** Skills / Users secondary links (header section)
- [x] **Search bar:** Rounded-full, `search` icon prefix, focus border
- [x] **Filter chips:** Category pills with icons (fire, trophy)
  - Active: `bg-primary text-on-primary`
  - Inactive: `bg-surface-container text-on-surface-variant`
- [x] **User progress row:** Avatar with ring + "Your Progress" label (in TopAppBar)
- [x] **Featured card:** Full-width with category badge, title, body, key steps list, tag pills
- [x] **Card grid:** Expandable cards with category label, title, description, tags, arrow icon
- [x] **Editorial banner:** Category badges with colored backgrounds
- [ ] **FAB:** Floating "Add Note" button bottom-right (not implemented - no notes feature yet)

---

## Phase 8: Dialog Pages ✅

### 8.1 Dialog Selection Page
**File:** `src/frontend/app/(main)/dialog/page.tsx`

Current: Simple bundle cards grid
New Design (from "Dialog Selection"):
- [x] **Page header:** "Skill Dialogs" label + "Master the Art of the Conversation" headline
- [x] **Filter tabs:** All / Unlocked / In Progress pills (simplified - showing all)
- [x] **Dialog cards:** Vertical list layout
  - Icon badge: 48x48 circle with module icon
  - Status pill: Active (primary), Unlocked (secondary), Locked (neutral)
  - Progress bar for in-progress cards
  - Social proof avatars: "-space-x-2" stack + "1.2k practiced"
  - CTA buttons: Continue/Start/Resume/Locked
  - Module count badge
- [x] **Challenge banner:** `bg-primary` full-width with image

### 8.2 Bundle Card Component
**File:** `src/frontend/components/dialog/BundleCard.tsx`

- [x] Redesign with icon badge, status pill, progress bar, CTA

### 8.3 Voice Practice Page
**File:** `src/frontend/app/dialog/[bundleId]/[modeId]/page.tsx`

New Design (from "Voice Practice: Balanced Controls"):
- [ ] Timer display top-center
- [ ] Persona card with avatar, name, title, difficulty badge
- [ ] Status bar: "Listening to your response..."
- [ ] Control buttons: mic, close, end session, hint, script
- [ ] Objectives sidebar panel: Session objectives list with completion states

---

## Phase 9: Profile Page ✅

### 9.1 Profile Page
**File:** `src/frontend/app/(main)/profile/page.tsx`

Current: Stats grid, achievements, skill toggles
New Design:
- [x] **User header:** Avatar with ring, name, email, persona badge
- [x] **Stats grid:** 2x2 with emoji icons, values, labels
  - Use new surface-container backgrounds
  - Update XP color to primary palette
- [x] **Skills progress bar:** New styling with rounded-2xl container
- [x] **Achievements grid:** 5-column, unlocked vs locked states
- [x] **Skills enrollment:** Toggle switches with new colors
- [x] **Logout button:** Neutral styling

---

## Phase 10: Session/Exercise Pages ✅

### 10.1 Session Page
**File:** `src/frontend/app/session/[lessonId]/page.tsx`

Current: Hearts, progress bar, exercise content
Changes:
- [x] Update progress bar colors (primary fill)
- [x] Update hearts styling
- [x] Completion screen: New XP/accuracy card styling
- [x] Failure screen: Updated colors

### 10.2 Exercise Components
**Files:**
- `src/frontend/components/exercise/MultipleChoiceExercise.tsx`
- `src/frontend/components/exercise/FillBlankExercise.tsx`
- `src/frontend/components/exercise/OpenQuestionExercise.tsx`
- `src/frontend/components/exercise/ExerciseResultBanner.tsx`

Changes:
- [x] Update selection colors (primary instead of Duolingo green)
- [x] Update correct/incorrect feedback colors
- [x] Apply new typography scale

### 10.3 Achievement Toast
**File:** `src/frontend/components/ui/AchievementToast.tsx`

Current: Green border, emoji icon
New Design (from "Achievement Notification"):
- [x] Updated border color to primary
- [x] Trophy icon `emoji_events`
- [x] New typography styling

---

## Phase 11: Admin Pages ✅

### 11.1 Admin Layout
**File:** `src/frontend/app/(admin)/layout.tsx`

- [x] Apply new surface colors
- [x] Update navigation styling

### 11.2 Admin Skills Page
**File:** `src/frontend/app/(admin)/admin/skills/page.tsx`

From design "Admin: Skills Management":
- [x] Skill cards with icon, title, description, stats
- [x] Edit/Delete actions
- [x] Add new skill form modal

### 11.3 Other Admin Pages
**Files:**
- `src/frontend/app/(admin)/admin/lessons/page.tsx`
- `src/frontend/app/(admin)/admin/dialog/page.tsx`
- `src/frontend/app/(admin)/admin/reference/page.tsx`
- etc.

- [x] Apply consistent admin styling
- [x] Update form inputs and buttons

---

## Phase 12: Shared Components

### 12.1 Buttons
Created: `src/frontend/components/ui/Button.tsx`
- [x] Primary: `bg-primary text-on-primary rounded-full`
- [x] Secondary: `bg-secondary-container` for secondary actions
- [x] Tertiary: `bg-surface-container-high text-on-surface`
- [x] Ghost: Transparent with `text-primary`
- [x] Error: `bg-error` for destructive actions
- [x] Disabled: `opacity-60 cursor-not-allowed`
- [x] IconButton variant for icon-only buttons
- [x] Loading state with spinner

### 12.2 Form Inputs
Created: `src/frontend/components/ui/Input.tsx`
- [x] TextInput: `bg-surface-container-low` with focus ring
- [x] SearchInput: Rounded-full with search icon
- [x] Textarea: Multi-line input
- [x] Select: Dropdown with custom chevron
- [x] Toggle: Switch component
- [x] Checkbox: Checkbox with custom styling
- [x] InputWrapper: Label, error, hint support

### 12.3 Progress Bars
Created: `src/frontend/components/ui/Progress.tsx`
- [x] ProgressBar: Linear progress with variants
- [x] CircularProgress: SVG-based circular indicator
- [x] StepProgress: Multi-step form progress
- [x] ProgressSkeleton: Loading state

### 12.4 Cards
Created: `src/frontend/components/ui/Card.tsx`
- [x] Card: Base component with surface variants
- [x] CardHeader: Title, subtitle, icon, action
- [x] CardContent: Content area
- [x] CardFooter: Actions area
- [x] StatCard: Metrics display
- [x] CardSkeleton: Loading state

### 12.5 Common Components
Created: `src/frontend/components/ui/Common.tsx`
- [x] Badge: Status indicators and labels
- [x] StatusBadge: Badge with icon
- [x] NotificationDot: Indicator dot
- [x] Avatar: Profile images with initials fallback
- [x] AvatarGroup: Stacked avatars
- [x] Divider: Visual separator
- [x] Chip: Filter pills and tags

---

## Implementation Order

### Wave 1: Foundation (Required First)
1. Tailwind config + global styles
2. Font imports
3. Icon component

### Wave 2: Core Layout
1. BottomNav redesign
2. MainLayout with sidebar support
3. Sidebar component
4. TopAppBar component

### Wave 3: Auth Flow
1. Onboarding page
2. Login/Register pages

### Wave 4: Primary User Pages
1. Skill Tree (Mastery)
2. Course Map
3. League
4. Guidebook
5. Dialog Selection

### Wave 5: Session & Exercises
1. Session page
2. Exercise components
3. Achievement toast

### Wave 6: Profile & Admin
1. Profile page
2. Admin pages

---

## Notes

- **Keep Russian text** — only change styling, not copy
- **Keep existing data models** — backend unchanged
- **Keep existing routes** — no URL changes
- **Our categories vs design:** Use OUR onboarding options (B2B SaaS, etc.), not the design's placeholder categories
- **Mobile-first:** Bottom nav for mobile, sidebar for desktop (md+)
- **Accessibility:** Maintain ARIA roles, keyboard support
