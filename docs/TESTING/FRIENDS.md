# Testing: Friends & Chat

## Manual Test Checklist

### Friend Requests
- [ ] Send friend request from user A to user B
- [ ] Verify request appears as "outgoing" for A and "incoming" for B
- [ ] Accept friend request from B
- [ ] Verify both users now appear in each other's friend lists
- [ ] Send another request — verify "already friends" error
- [ ] Decline a friend request — verify it disappears from pending
- [ ] After decline, resend request — verify it works (status resets to Pending)
- [ ] Cancel an outgoing request from sender A — "Отменить" button on the outgoing row,
      request disappears from A's outgoing list and B's incoming list
- [ ] After cancel, A can send a fresh request to B (returns to none state)
- [ ] Addressee B cannot cancel an incoming request (only A may); A cannot cancel after B accepts

### Friend Management
- [ ] View friends list with stats (XP, streak, achievements)
- [ ] Remove a friend — verify they disappear from both friend lists
- [ ] After removal, can send new friend request

### User Search
- [ ] Search by display name — verify results
- [ ] Search by email — verify results
- [ ] Query < 2 chars — no results returned
- [ ] Self not included in results
- [ ] Results show correct friendship status for each user

### Public Profile
- [ ] View profile of non-friend — shows "Добавить" button
- [ ] View profile after sending request — shows "Запрос отправлен"
- [ ] View profile of incoming request — shows "Принять" button
- [ ] View profile of friend — shows "Уже друзья" + "Написать" button
- [ ] Stats displayed correctly (streak, XP, achievements, avg score)

### Friend Leaderboard
- [ ] Shows current user + all friends ranked by total XP
- [ ] Current user row highlighted
- [ ] Top 3 have trophy icons
- [ ] Empty state when no friends

### Activity Feed
- [ ] Shows recent friend achievements
- [ ] Shows recent friend XP earnings
- [ ] Sorted by most recent first
- [ ] Limited to 20 items
- [ ] Empty when no friends

### Chat
- [ ] Create conversation with a friend
- [ ] Send text message — appears on right side (green)
- [ ] Friend's message appears on left side (gray)
- [ ] Messages poll every 5 seconds
- [ ] Conversation list shows last message preview and timestamp
- [ ] Multiple conversations — sorted by most recent
- [ ] Enter key sends message, Shift+Enter creates new line

### Edge Cases
- [ ] Cannot send friend request to self (400 error)
- [ ] Cannot accept request meant for another user (400 error)
- [ ] Cannot chat with non-friend (400 error)
- [ ] Cannot create duplicate friend request (400 error)
- [ ] Cancel a non-pending or non-owned request returns 400; cancel a missing request returns 404
- [ ] Navigation badge shows incoming request count

### Navigation
- [ ] "Друзья" tab visible in BottomNav (mobile)
- [ ] "Друзья" link visible in TopAppBar (desktop)
- [ ] Pending request badge appears on desktop nav
- [ ] Tab routing works: /friends, /friends/{userId}, /friends/chat, /friends/chat/{id}

### Visual Parity (April redesign — Phase 35)
- [ ] Palette: no Material Design 3 tokens anywhere under `/friends`
      (no `on-surface`, `primary-container`, `outline-variant`, `tonal-transition`,
      `font-headline`, `bg-surface-container*`, `*-container`)
- [ ] Typography: all headings + body use Geist (`var(--f-sans)`), numbers/timestamps
      use `font-mono` (`var(--f-mono)`)
- [ ] Avatars: every user avatar in friends list, requests, search, chat header,
      conversation rows, and public profile renders via `GeoAvatar` (not a letter in
      a colored square/circle)
- [ ] Public profile stats: 4× `StatTile` in rust (streak) / indigo (XP) / olive
      (achievements) / neutral (avg score); persona shown as `Chip`
- [ ] Active conversation row: dark `bg-ink` with `text-bg` text (same pattern as
      the active tab in the Friends hub)
- [ ] Shadows: cards use `var(--sh-1)`; elevated/active states use `var(--sh-2)`
- [ ] Dark theme: toggle theme in profile settings, then walk every Friends screen
      (friends / requests / leaderboard / chats / public profile). No white
      fragments, all text legible, no hard-coded hex colors visible
