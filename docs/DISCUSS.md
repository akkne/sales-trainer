# Discuss (Community Forum)

A community Q&A forum where salespeople create threads, post replies, upvote, and tag
topics. Mirrors the design in `.design/new-design/js/screens-discuss.jsx`. Implemented as a
vertical slice under `src/backend/api/Features/Discuss` and `src/frontend/features/discuss`.

## Concept
- **Threads** — a title + body authored by a user, with one or more tags.
- **Replies** — answers to a thread. The thread author (or an admin) can mark one reply as the
  accepted answer, which flags the thread as **Решено / solved**.
- **Votes** — upvote-only on both threads and replies. One vote per user per target
  (unique index prevents double-voting); the vote row's existence means "upvoted".
- **Tags (hybrid)** — an admin-managed curated catalog plus user free-form tags created on the
  fly. "Popular tags" counts are computed dynamically from thread-tag links.
- **Moderation** — admins delete/pin any thread, delete replies, and toggle the "Горячее / hot"
  flag. Pinned threads always sort first under the default "hot" sort.

## Data model (PostgreSQL)
| Table | Purpose |
|-------|---------|
| `DiscussThreads` | thread + denormalized `UpvoteCount`/`ReplyCount`/`ViewCount`, `AcceptedReplyId` (soft pointer), `IsPinned`, `IsHot`, `LastActivityAt` |
| `DiscussReplies` | replies, FK→thread (cascade), `IsAccepted` mirror |
| `DiscussTags` | `Slug` (unique), `Name`, `IsCurated` |
| `DiscussThreadTags` | join thread↔tag, unique `(ThreadId, TagId)` |
| `DiscussVotes` | polymorphic upvote, unique `(UserId, TargetType, TargetId)` |
| `DiscussPhotos` | polymorphic photo attachment, `(OwnerType, OwnerId, OrderIndex)` index — see [Photos](#photos) |

Migration: `Infrastructure/Data/Migrations/*_AddDiscussTables.cs` (auto-applied on startup).
Photos added later by `20260612201243_AddDiscussPhotos`.

## Sorting
- **hot** (default): pinned threads first, then a time-decayed score
  `(upvotes*4 + replies*2 + log10(views+1)) / sqrt(hoursSinceActivity + 2)`, with a large boost
  when an admin sets `IsHot`. Scored in memory over a bounded candidate window.
- **new**: by `LastActivityAt` desc.
- **unanswered**: zero-reply threads by `CreatedAt` desc.

## Photos
Threads and replies can carry up to **10 image attachments** each (PNG/JPEG/WEBP, magic-byte
validated, ≤5 MB per file). Images are stored in S3/MinIO (shared `salestrainer-avatars` bucket
under the `discuss/` prefix) with metadata rows in the `DiscussPhotos` table (polymorphic owner,
mirroring `DiscussVotes`).

- **Two-step upload**: create the thread/reply via the existing JSON endpoints, then `POST` the
  images to its photo sub-resource (`/discuss/threads/{id}/photos` or `/discuss/replies/{id}/photos`).
- **Author-only management**: only the author may upload or delete a post's photos.
- **Cleanup on delete**: deleting a thread or reply (including admin delete) removes its photo rows
  and best-effort-deletes the S3 objects.
- Photo `url` is the relative content path `/discuss/photos/{id}/content`.

Endpoints and DTO fields: see [API_CONTRACTS.md](API_CONTRACTS.md#photos).

## Stats
`GET /discuss/stats` returns total threads, total replies, and top authors of the week — the sum
of upvotes received on each author's threads + replies in the last 7 days. No "online" counter.

## Key files
- Backend: `Features/Discuss/{Models,Configurations,Services,DiscussController.cs,AdminDiscussController.cs}`
- Frontend: `features/discuss/{hooks,components,lib}`, pages `app/(main)/discuss/`,
  admin page `app/(admin)/admin/discuss/page.tsx`.
- Endpoints: see [API_CONTRACTS.md](API_CONTRACTS.md#discuss-community-forum).
- Tests: see [TESTING/DISCUSS.md](TESTING/DISCUSS.md).
