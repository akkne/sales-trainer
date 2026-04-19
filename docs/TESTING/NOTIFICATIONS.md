# Testing: Notifications

## Manual Test Checklist

### Bell & Unread Badge
- [ ] Bell icon visible in top app bar (desktop)
- [ ] No badge shown when unread count is 0
- [ ] Badge shows exact count when 1–9 unread
- [ ] Badge shows `9+` when more than 9 unread
- [ ] Unread count refreshes automatically (within ~20 seconds)

### Panel Behavior
- [ ] Click bell → panel opens with recent notifications
- [ ] Click outside panel → panel closes
- [ ] Click a notification → marks it read, closes panel, navigates to `actionUrl` if present
- [ ] "Прочитать всё" button is disabled when there are no unread notifications
- [ ] "Прочитать всё" marks all notifications as read and updates badge immediately
- [ ] Empty state shown when there are no notifications
- [ ] Loading state shown on first open
- [ ] Error state shown if request fails
- [ ] Mobile: panel takes full-width below top bar with overlay behind

### Notification Triggers

**Friend request received**
- [ ] User A sends friend request to user B
- [ ] B sees new notification with type `FriendRequestReceived`
- [ ] Clicking notification navigates to `/friends?tab=requests`

**Friend request accepted**
- [ ] B accepts A's request
- [ ] A sees new notification with type `FriendRequestAccepted`
- [ ] Clicking notification navigates to B's public profile

**Chat message received**
- [ ] A sends a chat message to B
- [ ] B sees new notification with type `ChatMessageReceived`
- [ ] Body preview is truncated to 160 chars with `…` for long messages
- [ ] Clicking notification navigates to the chat conversation

**Achievement unlocked**
- [ ] User triggers an achievement (e.g., completes first lesson)
- [ ] Notification with type `AchievementUnlocked` appears
- [ ] Title includes the achievement emoji + name
- [ ] Clicking notification navigates to `/profile`

**Streak milestone**
- [ ] User hits a streak milestone (3, 7, 14, 30, 60, 90, 180, 365)
- [ ] Notification with type `StreakMilestone` appears
- [ ] Title shows the current streak day count
- [ ] Clicking notification navigates to `/profile`

### Read State
- [ ] Unread notifications have tinted background and primary-color dot
- [ ] Read notifications have plain surface background, no dot
- [ ] Marking one as read updates its visual state without reordering
- [ ] Marking one as read decrements the unread badge

### Cleanup Job
- [ ] Hangfire dashboard shows `notification-cleanup` recurring job
- [ ] Scheduled for `30 0 * * *` (daily at 00:30 UTC)
- [ ] Manually triggering the job deletes read notifications older than 30 days
- [ ] Unread notifications older than 30 days are NOT deleted
- [ ] Read notifications younger than 30 days are NOT deleted

### API Contract
- [ ] `GET /notifications?limit=20&includeRead=true` returns paginated list ordered newest first
- [ ] `GET /notifications/unread-count` returns `{count}`
- [ ] `PUT /notifications/{id}/read` returns 204 and is idempotent
- [ ] `PUT /notifications/read-all` returns 204
- [ ] All endpoints require authentication; unauthenticated requests return 401
- [ ] Cross-user read attempts return 404 (notification not found for this recipient)
