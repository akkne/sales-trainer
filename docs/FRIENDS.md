# Friends & Chat

Social layer for Sellevate: friendships, public profiles, user search, friend leaderboard, activity feed, and 1-to-1 chat.

> **Microservices migration (Phase 5):** Friends + Chat were extracted into the
> `social-service` — see [SOCIAL_SERVICE.md](SOCIAL_SERVICE.md). The gateway flips
> `/friends/*` and `/chat/*` to that service (the monolith slice below stays as
> reference). In the service, display names/avatars come from the local `UserReplica`
> (fed by `user.*` events) instead of joining the `Users` table, and the
> friend-request notifications are now the `friend.request.received` /
> `friend.request.accepted` / `chat.message.sent` Kafka events consumed by the
> notification-service. Leaderboard/profile/activity XP-and-achievement aggregates are
> owned by Gamification/Learning (not yet extracted) and currently return `0`/empty.

---

## Database Schema

### PostgreSQL: `Friendships` Table

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | uuid | PK | |
| RequesterId | uuid | FK → Users | Who sent the request |
| AddresseeId | uuid | FK → Users | Who received the request |
| Status | int | | 0 = Pending, 1 = Accepted, 2 = Declined |
| CreatedAt | timestamptz | | When request was sent |
| AcceptedAt | timestamptz | yes | When request was accepted |

**Indexes:**
- Unique composite: `(RequesterId, AddresseeId)` — prevents duplicate requests
- Individual: `RequesterId`, `AddresseeId` — fast lookups

**Constraints:**
- Check: `RequesterId != AddresseeId` — cannot friend yourself

### MongoDB: `chat_conversations` Collection

```json
{
  "_id": "ObjectId",
  "participantIds": ["guid", "guid"],
  "messages": [
    {
      "id": "ObjectId",
      "senderId": "guid",
      "content": "string",
      "sentAt": "datetime"
    }
  ],
  "lastMessageAt": "datetime",
  "createdAt": "datetime"
}
```

**Index:** `participantIds` — for efficient conversation lookup by user.

`participantIds` is always sorted to ensure one canonical document per pair.

---

## API Endpoints

### Friend Endpoints (route prefix: `/friends`)

All endpoints require `Authorization: Bearer` token.

| Method | Route | Request Body | Response |
|--------|-------|-------------|----------|
| GET | `/friends` | — | `FriendDto[]` |
| GET | `/friends/requests` | — | `FriendRequestDto[]` |
| POST | `/friends/requests` | `{ addresseeId: guid }` | `201` with friendship ID |
| PUT | `/friends/requests/{friendshipId}/accept` | — | `204` |
| PUT | `/friends/requests/{friendshipId}/decline` | — | `204` |
| DELETE | `/friends/{friendUserId}` | — | `204` |
| GET | `/friends/search?query={q}` | — | `UserSearchResultDto[]` |
| GET | `/friends/leaderboard` | — | `FriendLeaderboardEntryDto[]` |
| GET | `/friends/activity` | — | `FriendActivityDto[]` |
| GET | `/friends/profile/{userId}` | — | `PublicProfileDto` |

### Chat Endpoints (route prefix: `/chat`)

| Method | Route | Request Body | Response |
|--------|-------|-------------|----------|
| GET | `/chat/conversations` | — | `ChatConversationSummaryDto[]` |
| POST | `/chat/conversations` | `{ friendUserId: guid }` | `ChatConversationSummaryDto` |
| GET | `/chat/conversations/{id}/messages?limit=50&before={msgId}` | — | `ChatMessageDto[]` |
| POST | `/chat/conversations/{id}/messages` | `{ content: string }` | `ChatMessageDto` |

---

## DTOs

### FriendDto
```json
{
  "userId": "guid",
  "displayName": "string",
  "persona": "string?",
  "totalXpAmount": 0,
  "currentStreakDayCount": 0,
  "achievementCount": 0
}
```

### FriendRequestDto
```json
{
  "friendshipId": "guid",
  "userId": "guid",
  "displayName": "string",
  "persona": "string?",
  "direction": "incoming | outgoing",
  "createdAt": "datetime"
}
```

### PublicProfileDto
```json
{
  "userId": "guid",
  "displayName": "string",
  "persona": "string?",
  "totalXpAmount": 0,
  "currentStreakDayCount": 0,
  "achievementCount": 0,
  "averageExerciseScore": 0.0,
  "friendshipStatus": "none | pending_outgoing | pending_incoming | friends"
}
```

### UserSearchResultDto
```json
{
  "userId": "guid",
  "displayName": "string",
  "persona": "string?",
  "friendshipStatus": "none | pending_outgoing | pending_incoming | friends"
}
```

### FriendLeaderboardEntryDto
```json
{
  "userId": "guid",
  "displayName": "string",
  "totalXpAmount": 0,
  "rank": 1,
  "isCurrentUser": false
}
```

### FriendActivityDto
```json
{
  "userId": "guid",
  "displayName": "string",
  "activityType": "completed_lesson | earned_achievement | streak_milestone",
  "description": "string",
  "occurredAt": "datetime"
}
```

### ChatConversationSummaryDto
```json
{
  "conversationId": "string",
  "friendUserId": "guid",
  "friendDisplayName": "string",
  "lastMessagePreview": "string?",
  "lastMessageAt": "datetime?"
}
```

### ChatMessageDto
```json
{
  "id": "string",
  "senderId": "guid",
  "content": "string",
  "sentAt": "datetime",
  "isOwn": false
}
```

---

## Frontend Routes

| Route | Page |
|-------|------|
| `/friends` | Main tabbed page: Друзья / Запросы / Рейтинг / Чаты |
| `/friends?tab=chats` | Чаты tab — messenger-style list + window |
| `/friends?tab=chats&conv={id}` | Чаты tab with conversation selected |
| `/friends/[userId]` | Public profile of another user |
| `/friends/chat` | Deprecated, redirects to `/friends?tab=chats` |
| `/friends/chat/[conversationId]` | Deprecated, redirects to `/friends?tab=chats&conv={id}` |

---

## UI Components

### Friends Tab (`/friends`)
- Pill-style tab bar: "Друзья" / "Запросы" (badge) / "Рейтинг" / "Чаты"
- Active tab and selected conversation synced to URL (`?tab=...&conv=...`)
- Friends tab: search bar, activity feed, friend cards with avatar, stats, Chat/Profile buttons
- Requests tab: incoming + outgoing friend requests
- Leaderboard tab: friend XP ranking
- Chats tab: two-pane messenger (list left, chat right on desktop; stacked on mobile)
- Empty state: "Найди первого напарника!"

### Public Profile (`/friends/[userId]`)
- Avatar, displayName, persona badge
- Stats grid: streak, XP, achievements, avg score
- Friendship button (contextual)
- "Написать" button if friends

### Chat
- Conversation list with last message preview
- Chat bubbles: own = right/green, friend = left/gray
- Text input + send button
- Polling every 5s for new messages

---

## Business Rules

1. Cannot send friend request to yourself
2. Cannot send duplicate request (unique index on RequesterID + AddresseeId)
3. Before sending, check both directions (A->B and B->A)
4. Declining a request allows re-sending later (status changes to Declined)
5. Removing a friend deletes the Friendship row entirely
6. Chat only available between accepted friends
7. Creating a conversation validates active friendship
8. User search returns max 20 results, minimum 2 chars query
9. Activity feed limited to 20 most recent items
