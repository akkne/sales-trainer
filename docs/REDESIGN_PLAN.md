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

## Phase 1: Design System Foundation

### 1.1 Tailwind Config Update
**File:** `src/frontend/tailwind.config.ts`

Changes:
- [ ] Add new color palette (primary, secondary, tertiary, surface hierarchy)
- [ ] Add font families: Plus Jakarta Sans, Manrope
- [ ] Update border radius defaults (DEFAULT: 1rem, lg: 2rem, xl: 3rem, full: 9999px)
- [ ] Add spacing scale adjustments

### 1.2 Global Styles & Fonts
**File:** `src/frontend/app/layout.tsx`, `src/frontend/app/globals.css`

Changes:
- [ ] Import Google Fonts: Plus Jakarta Sans, Manrope
- [ ] Import Material Symbols Outlined icon font
- [ ] Set `font-family: 'Manrope'` as body default
- [ ] Set background color to `#f7f6f4` (surface)
- [ ] Add `.font-headline` utility class for Plus Jakarta Sans
- [ ] Remove old Duolingo-style green (`#58CC02`) references

### 1.3 Material Symbols Icons
**New File:** `src/frontend/components/ui/Icon.tsx`

- [ ] Create Icon component wrapper for Material Symbols Outlined
- [ ] Support filled/outlined variants via `style` prop
- [ ] Default settings: `'FILL' 0, 'wght' 400, 'GRAD' 0, 'opsz' 24`

---

## Phase 2: Layout Components

### 2.1 Bottom Navigation Bar
**File:** `src/frontend/components/layout/BottomNav.tsx`

Current: Emoji icons, Duolingo green, Russian labels
New Design:
- [ ] Replace emojis with Material Symbols icons:
  - `school` → Mastery/Путь
  - `trophy` → Leagues/Лига  
  - `menu_book` → Guidebook/Справочник
  - `forum` → Dialog/Диалог
  - `person` → Profile/Профиль
- [ ] Update active color from `#58CC02` to `#006a2d` (primary)
- [ ] Add `bg-surface` background with `border-t border-outline-variant`
- [ ] Glassmorphism: `backdrop-filter: blur(20px)` + `rgba(255,255,255,0.8)`

### 2.2 Main Layout
**File:** `src/frontend/app/(main)/layout.tsx`

Current: White background, padding for bottom nav
New Design:
- [ ] Change background to `#f7f6f4` (surface)
- [ ] Add desktop sidebar (hidden on mobile)
- [ ] Add top navigation bar for desktop
- [ ] Keep mobile bottom nav

### 2.3 Desktop Sidebar Navigation
**New File:** `src/frontend/components/layout/Sidebar.tsx`

From design "Mastery Dashboard":
- [ ] User profile block with avatar, name, level badge
- [ ] Navigation links with icons: Mastery, Leagues, Library, Analytics, Settings
- [ ] Active state: `bg-primary-container text-primary`
- [ ] Current path widget at bottom with "Continue Learning" CTA
- [ ] `w-64` width, `bg-surface-container-low` background

### 2.4 Top App Bar (Desktop)
**New File:** `src/frontend/components/layout/TopAppBar.tsx`

From design "Course Map":
- [ ] Brand name "SalesMastery" left-aligned
- [ ] Navigation links (hidden on mobile)
- [ ] Right side: notification bell with red dot, achievements icon, user avatar
- [ ] Sticky positioning, glassmorphism effect

---

## Phase 3: Authentication & Onboarding

### 3.1 Onboarding Page
**File:** `src/frontend/app/(auth)/onboarding/page.tsx`

Current: 4 steps, emoji icons, Duolingo green buttons
New Design (from "Onboarding: Sales Type"):
- [ ] Progress bar: 4px height, primary fill on `surface-container-highest` track
- [ ] Step indicator: "Step X of Y" label above progress
- [ ] Page header: `font-headline text-3xl font-bold` + description paragraph
- [ ] Selection cards: Bento grid layout (`grid-cols-auto-fit minmax(280px, 1fr)`)
- [ ] Card states:
  - Default: `bg-surface-container` no border
  - Hover: `border-outline-variant bg-surface-container-high`
  - Selected: `border-2 border-primary bg-primary-container`
- [ ] Material icons instead of emojis (cloud_done, home_work, shopping_bag, etc.)
- [ ] "Most Popular" badge: pill shape, `bg-primary text-on-primary`
- [ ] Info hint row with `info` icon
- [ ] CTA button: `rounded-full bg-primary` with arrow icon suffix

**OUR categories to KEEP (not from design):**
- B2B SaaS, Розница, Недвижимость, Финансы, B2C
- Personas: SDR, Account Executive, Account Manager, Основатель, Другое
- Experience: Новичок, Опытный, Руководитель

### 3.2 Login Page
**File:** `src/frontend/app/(auth)/login/page.tsx`

- [ ] Apply new surface background
- [ ] Update Google login button styling
- [ ] Add brand header with icon

### 3.3 Register Page
**File:** `src/frontend/app/(auth)/register/page.tsx`

- [ ] Match login page styling
- [ ] Consistent form input styling

---

## Phase 4: Main Pages

### 4.1 Skill Tree Page (Mastery)
**File:** `src/frontend/app/(main)/tree/page.tsx`

Current: Left sidebar with skills, center lesson path, right stats
New Design (from "Mastery Dashboard"):
- [ ] **Left sidebar:** Skill selector with mini progress bars
- [ ] **Center content:**
  - Current module hero card: `bg-primary` with white text
  - Mastery level progress: `Level X/5` + percentage + progress bar
  - "Continue Lesson" CTA with gradient background
  - Secondary modules row: 2-column grid
- [ ] **Right sidebar:** StatsWidget + Next Milestones panel
- [ ] **Top stats row:** 3 cards (Daily Progress, Total XP, Streak)

### 4.2 Stats Widget
**File:** `src/frontend/components/layout/StatsWidget.tsx`

Current: Simple stats display
New Design:
- [ ] Grid of stat cards: Daily Progress bar, Total XP (primary-container bg), Streak with bolt icon
- [ ] `bg-surface-container rounded-2xl` containers
- [ ] XP card: `bg-primary-container` highlight
- [ ] Streak icon: Material Symbol `bolt`

### 4.3 Lesson Path Component
**File:** `src/frontend/components/ui/LessonPath.tsx`

Current: Circular nodes with lock/check icons
New Design:
- [ ] Keep existing path structure but update styling
- [ ] Progress ring with SVG stroke-dasharray for circular indicator
- [ ] Update colors to new palette

---

## Phase 5: Course Map Page

### 5.1 Skill Map Page
**File:** `src/frontend/app/(main)/skill/[id]/map/page.tsx`

Current: Linear lesson list with status badges
New Design (from "Course Map: Cold Calling"):
- [ ] **Header card:** `bg-primary-container` with circular progress ring (SVG)
- [ ] Progress text: "X out of Y lessons completed", "+N XP earned"
- [ ] **Lesson cards list:** Vertical timeline layout
  - Completed: `bg-surface-container`, green checkmark badge
  - Up Next: `bg-secondary-container ring-2 ring-secondary`, "Up Next" pill, bolt icon on Start button
  - Locked: `bg-surface-container-low opacity-60`, lock icon badge, unlock hint text
- [ ] XP badges: `bg-primary-container text-primary rounded-full`
- [ ] Duration badges: `schedule` icon + "X min"
- [ ] CTA buttons: "Review" (outline), "Start" (filled secondary), "Locked" (disabled)

---

## Phase 6: League Page

### 6.1 League Page
**File:** `src/frontend/app/(main)/league/page.tsx`

Current: Tier emoji, countdown, participant list
New Design (from "The Arena: Refined Leaderboard"):
- [ ] **League header card:** `bg-secondary-container`
  - `military_tech` icon + "The Arena" label
  - League name: `font-headline text-3xl`
  - Countdown timer with separate blocks (Days:Hours:Mins)
- [ ] **Stat cards row:** 2-column grid
  - Your Rank card: `#N` with trend arrow
  - Status card: "Near Promotion" + XP to top 10 + next reward info
- [ ] **Leaderboard table:**
  - Grid layout: `[3rem_1fr_auto]`
  - Top 3: Gold/Silver/Bronze rank badges
  - Promotion zone rows: `bg-[rgba(93,253,137,0.10)]`
  - Your row: `bg-[rgba(0,106,45,0.08)] border-left-3 border-primary`
  - Relegation zone rows: `bg-[rgba(176,37,0,0.07)]` with error text
  - Collapsed sections: "Participants 04-11" clickable rows
- [ ] **Boost CTA banner:** `bg-tertiary-container` with module suggestion
- [ ] Avatar rings for current user

---

## Phase 7: Guidebook Page

### 7.1 Guidebook Page
**File:** `src/frontend/app/(main)/guidebook/page.tsx`

Current: Category chips, expandable cards, markdown content
New Design (from "Guidebook"):
- [ ] **Top nav integration:** Active state for "Guidebook" link
- [ ] **Sub nav row:** Skills / Users secondary links
- [ ] **Search bar:** Rounded-full, `search` icon prefix, focus border
- [ ] **Filter chips:** Category pills with icons (fire, trophy)
  - Active: `bg-primary text-on-primary`
  - Inactive: `bg-surface-container text-on-surface-variant`
- [ ] **User progress row:** Avatar with ring + "Your Progress" label
- [ ] **Featured card:** Full-width with category badge, title, body, key steps list, tag pills
- [ ] **Card grid:** 2-column, each with category label, title, description, tags, arrow icon
- [ ] **Editorial banner:** Dark overlay on image, "Editor's Pick" badge, "Listen Now" CTA
- [ ] **FAB:** Floating "Add Note" button bottom-right

---

## Phase 8: Dialog Pages

### 8.1 Dialog Selection Page
**File:** `src/frontend/app/(main)/dialog/page.tsx`

Current: Simple bundle cards grid
New Design (from "Dialog Selection"):
- [ ] **Page header:** "Skill Dialogs" label + "Master the Art of the Conversation" headline
- [ ] **Filter tabs:** All / Unlocked / In Progress pills
- [ ] **Dialog cards:** Vertical list layout
  - Icon badge: 48x48 circle with module icon
  - Status pill: Active (primary), Unlocked (secondary), Locked (neutral)
  - Progress bar for in-progress cards
  - Social proof avatars: "-space-x-2" stack + "1.2k practiced"
  - CTA buttons: Continue/Start/Resume/Locked
  - Module count badge
- [ ] **Challenge banner:** `bg-primary` full-width with image

### 8.2 Bundle Card Component
**File:** `src/frontend/components/dialog/BundleCard.tsx`

- [ ] Redesign with icon badge, status pill, progress bar, CTA

### 8.3 Voice Practice Page
**File:** `src/frontend/app/dialog/[bundleId]/[modeId]/page.tsx`

New Design (from "Voice Practice: Balanced Controls"):
- [ ] Timer display top-center
- [ ] Persona card with avatar, name, title, difficulty badge
- [ ] Status bar: "Listening to your response..."
- [ ] Control buttons: mic, close, end session, hint, script
- [ ] Objectives sidebar panel: Session objectives list with completion states

---

## Phase 9: Profile Page

### 9.1 Profile Page
**File:** `src/frontend/app/(main)/profile/page.tsx`

Current: Stats grid, achievements, skill toggles
New Design:
- [ ] **User header:** Avatar with ring, name, email, persona badge
- [ ] **Stats grid:** 2x2 with emoji icons, values, labels
  - Use new surface-container backgrounds
  - Update XP color to `#1CB0F6` → keep or change to primary
- [ ] **Skills progress bar:** New styling with rounded-2xl container
- [ ] **Achievements grid:** 5-column, unlocked vs locked states
- [ ] **Skills enrollment:** Toggle switches with new colors
- [ ] **Logout button:** Neutral styling

---

## Phase 10: Session/Exercise Pages

### 10.1 Session Page
**File:** `src/frontend/app/session/[lessonId]/page.tsx`

Current: Hearts, progress bar, exercise content
Changes:
- [ ] Update progress bar colors (primary fill)
- [ ] Update hearts styling
- [ ] Completion screen: New XP/accuracy card styling
- [ ] Failure screen: Updated colors

### 10.2 Exercise Components
**Files:**
- `src/frontend/components/exercise/MultipleChoiceExercise.tsx`
- `src/frontend/components/exercise/FillBlankExercise.tsx`
- `src/frontend/components/exercise/OpenQuestionExercise.tsx`
- `src/frontend/components/exercise/ExerciseResultBanner.tsx`

Changes:
- [ ] Update selection colors (primary instead of Duolingo green)
- [ ] Update correct/incorrect feedback colors
- [ ] Apply new typography scale

### 10.3 Achievement Toast
**File:** `src/frontend/components/ui/AchievementToast.tsx`

Current: Green border, emoji icon
New Design (from "Achievement Notification"):
- [ ] Updated border color to primary
- [ ] Trophy icon `emoji_events`
- [ ] New typography styling

---

## Phase 11: Admin Pages

### 11.1 Admin Layout
**File:** `src/frontend/app/(admin)/layout.tsx`

- [ ] Apply new surface colors
- [ ] Update navigation styling

### 11.2 Admin Skills Page
**File:** `src/frontend/app/(admin)/admin/skills/page.tsx`

From design "Admin: Skills Management":
- [ ] Skill cards with icon, title, description, stats
- [ ] Edit/Delete actions
- [ ] Add new skill form modal

### 11.3 Other Admin Pages
**Files:**
- `src/frontend/app/(admin)/admin/lessons/page.tsx`
- `src/frontend/app/(admin)/admin/dialog/page.tsx`
- `src/frontend/app/(admin)/admin/reference/page.tsx`
- etc.

- [ ] Apply consistent admin styling
- [ ] Update form inputs and buttons

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
