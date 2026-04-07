# Skill Enrollment System

## Overview

Users choose which skills they want to study. This is different from the old model where skills were unlocked sequentially through prerequisites.

**Key concepts:**
- **Enrolled** = skill the user actively wants to study (status ≠ `locked`)
- **Active** = skill currently being viewed in `/tree` (stored in localStorage via `selectedSkillStore`)
- `sales-basics` is always enrolled and cannot be removed

---

## Onboarding flow

**Step 1** — Sales type  
**Step 2** — Experience level  
**Step 3 (new)** — Skill selection (replaces Goal)

The step 3 screen shows all skills as toggles:
- `sales-basics` is pre-selected, locked on, shown with a dimmed-grey toggle  
- Other skills can be toggled on/off  
- Submitting calls `POST /onboarding` with `{ salesType, experienceLevel, selectedSkillSlugs: [...] }`

**Backend** (`OnboardingService.CompleteOnboardingForUserAsync`):
- Creates `UserSkillProgress` with `status = "available"` for each selected slug  
- `sales-basics` is always added regardless of the payload  
- No sequential locking — ALL selected skills start as `available`

---

## Profile — managing enrolled skills

`/profile` → "Мои навыки" section.

Each skill shows a toggle switch:
- **Grey toggle (on, dimmed)** = `sales-basics` — always enrolled, cannot be removed  
- **Green toggle (on)** = enrolled skill — click to unenroll  
- **Grey toggle (off)** = non-enrolled skill — click to enroll  

On toggle, the frontend calls `PUT /skills/enrolled` with the full updated slug list.

**Backend** (`SkillTreeService.UpdateEnrolledSkillsAsync`):
- Sets `status = "available"` for newly added skills (or restores if previously locked)  
- Sets `status = "locked"` for removed skills (progress is preserved, not deleted)  
- `sales-basics` is always kept enrolled

---

## /tree — skill selector sidebar

Left sidebar lists all enrolled skills (status ≠ `locked`).

- Clicking a skill sets it as the "active" skill (stored in `localStorage` via `selectedSkillStore`)  
- The center panel shows lessons for the active skill  
- On first load, the first enrolled skill is auto-selected  
- Each skill shows a mini progress bar

---

## Lesson unlock within a skill

When a skill is first accessed (enrolled), lessons are seeded lazily by `EnsureSkillLessonsSeededAsync`:
- Lesson with the lowest `sortOrder` → `available`  
- All others → `locked`  

After a lesson is completed, `UnlockNextLessonInSkillAsync` sets the next lesson to `available`.

This means: first lesson is always available, each subsequent lesson unlocks after the previous one is completed.

---

## API reference

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/skills` | All skills with user's progress (locked if not enrolled) |
| `PUT` | `/skills/enrolled` | Replace enrolled skill set. Body: `{ skillSlugs: string[] }` |
| `POST` | `/onboarding` | Complete onboarding. Body: `{ salesType, experienceLevel, selectedSkillSlugs }` |

---

## Data model

```
UserSkillProgress
  UserId         Guid
  SkillId        Guid
  Status         "locked" | "available" | "in_progress" | "completed"
  CompletedLessonCount  int
  TotalLessonCount      int
```

Enrollment = `Status != "locked"`.  
Not enrolled = `Status == "locked"` OR no row exists.
