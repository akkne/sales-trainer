# Next Lesson Button — Testing Checklist

## Phase 16

### Backend — `GET /lessons/:lessonId/next`

- [ ] Returns `NextLessonDto` when a next lesson exists and is `available` in the same skill
- [ ] Returns 204 when the current lesson is the last in the skill
- [ ] Returns 204 when the next lesson is still `locked` (not yet unlocked)
- [ ] Correctly identifies next lesson by `sortOrder` (ascending)
- [ ] Does not cross skill boundaries (only same skill)
- [ ] Requires authentication — 401 without token

### Frontend — session completion screen

- [ ] After completing a lesson with a next available lesson: "Следующий урок →" button appears
- [ ] Button shows the next lesson title below it
- [ ] "Вернуться к пути" is shown as a plain text link (not green button) when next lesson available
- [ ] Clicking "Следующий урок →" navigates to `/session/[nextLessonId]` (replaces history)
- [ ] After completing the last lesson in a skill: "Все уроки пройдены! 🎉" message shown
- [ ] "Вернуться к пути" is green button when no next lesson
- [ ] While next lesson status is loading: neither button nor message shown (loading state)
- [ ] Failure screen is unaffected (no next lesson button on failure)
