# Notifications

In-app notification system with a bell dropdown in the top app bar. Notifications are stored in PostgreSQL and polled from the frontend.

## Triggers

| Type                    | Trigger source                                      | actionUrl                                 |
|-------------------------|-----------------------------------------------------|-------------------------------------------|
| `FriendRequestReceived` | `FriendService.SendFriendRequestAsync` (new + reactivated) | `/friends?tab=requests`           |
| `FriendRequestAccepted` | `FriendService.AcceptFriendRequestAsync`            | `/friends/{requesterId}`                  |
| `ChatMessageReceived`   | `ChatService.SendMessageAsync`                      | `/friends/chat/{conversationId}`          |
| `AchievementUnlocked`   | `AchievementService.EvaluateAchievementsForUserAsync` | `/profile`                              |
| `StreakMilestone`       | `ExerciseService.AwardStreakBonusExperiencePointsIfMilestoneAsync` | `/profile`                  |

Streak milestones fire on 3, 7, 14, 30, 60, 90, 180, 365 days.

Chat notifications truncate the body preview to 160 characters.

## Data Model

See [DB_SCHEMA.md](DB_SCHEMA.md#notifications) for the `Notifications` table definition.

Domain enum values (stored as integers):
- `1` — FriendRequestReceived
- `2` — FriendRequestAccepted
- `3` — ChatMessageReceived
- `4` — AchievementUnlocked
- `5` — StreakMilestone

## Backend

Feature folder: `src/backend/api/Features/Notifications/`

- `Models/Notification.cs` — entity
- `Models/NotificationType.cs` — enum
- `Models/NotificationDto.cs` — API response record
- `Models/UnreadNotificationCountDto.cs`
- `NotificationEntityConfiguration.cs` — EF Core mapping (indexes, max lengths)
- `Services/Abstract/INotificationService.cs`
- `Services/Implementation/NotificationService.cs`
- `NotificationCleanupJob.cs` — Hangfire recurring job
- `NotificationController.cs`
- `NotificationFeatureServiceCollectionExtensions.cs` — DI registration

Trigger integrations inject `INotificationService`:
- `FriendService` — creates FriendRequestReceived / FriendRequestAccepted
- `ChatService` — creates ChatMessageReceived
- `AchievementService` — creates AchievementUnlocked per newly-unlocked achievement
- `ExerciseService` — creates StreakMilestone when the streak crosses a milestone threshold

Notification creation happens after the primary operation succeeds; if the notification write fails, the user-facing action is not rolled back (logged as a warning, not an error).

## Cleanup

Hangfire recurring job `notification-cleanup` registered in `Program.cs`:
- Cron: `30 0 * * *` (daily at 00:30 UTC)
- Retention: 30 days
- Deletes rows where `IsRead = true AND CreatedAt < now - 30 days`

## Frontend

Components: `src/frontend/components/notifications/`

- `NotificationBell.tsx` — button with unread-count badge, owns the open/close state and outside-click handling
- `NotificationPanel.tsx` — dropdown (full-screen on mobile, `md:w-96` card on desktop) with list + "Mark all as read"
- `NotificationCard.tsx` — single row with type icon, title, body preview, relative timestamp, unread indicator
- `notificationMeta.ts` — per-type icon/color map and relative-time formatter

Hook: `src/frontend/lib/hooks/useNotifications.ts`

- `useNotifications(enabled)` — list query (polls every 30s while panel is open)
- `useUnreadNotificationCount()` — badge count (polls every 20s)
- `useMarkNotificationAsRead()`
- `useMarkAllNotificationsAsRead()`

The bell is mounted in `components/layout/TopAppBar.tsx` (replaces the previous placeholder with always-on dot).

## API

See [API_CONTRACTS.md](API_CONTRACTS.md#notifications).

## Testing

See [TESTING/NOTIFICATIONS.md](TESTING/NOTIFICATIONS.md).
