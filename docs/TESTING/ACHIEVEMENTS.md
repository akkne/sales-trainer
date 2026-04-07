# Achievements & Badges — Testing Checklist

## Endpoint

`GET /profile/achievements` — requires Bearer token  
Response: `AchievementDto[]` with `achievementId`, `key`, `title`, `description`, `iconEmoji`, `isUnlocked`, `unlockedAt`

## Manual Test Cases

### 1. Seed on startup
- Restart the backend container
- Call `GET /profile/achievements` with any valid token
- Expect: 10 achievements returned, all `isUnlocked: false` for new user

### 2. Unlock "first_lesson" on first correct answer
- Complete any exercise correctly
- Call `GET /profile/achievements`
- Expect: achievement with `key: "first_lesson"` has `isUnlocked: true`

### 3. Unlock XP threshold achievements
- Earn 100+ XP total
- Call `GET /profile/achievements`
- Expect: `xp_100` is `isUnlocked: true`

### 4. Profile page badges grid
- Open `/profile`
- Expect: "Достижения" section shows a 5-column grid of badge icons
- Locked badges should be grayscale/opacity-40
- Unlocked badges should have green border and bright color
- Footer shows "X из 10 разблокировано"

### 5. Submit exercise → unlocked keys in response
- Submit a correct answer for the first time
- Expect: `POST /exercises/:id/submit` response includes `newlyUnlockedAchievementKeys: ["first_lesson"]`

## Achievement Conditions

| Key | Condition | Threshold |
|---|---|---|
| `first_lesson` | first_lesson | — |
| `lessons_5` | lesson_count | 5 |
| `lessons_20` | lesson_count | 20 |
| `lessons_50` | lesson_count | 50 |
| `xp_100` | xp_total | 100 |
| `xp_500` | xp_total | 500 |
| `xp_1000` | xp_total | 1000 |
| `streak_7` | streak_days | 7 |
| `streak_30` | streak_days | 30 |
| `skill_completed` | skill_completed | — |
