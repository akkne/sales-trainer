# Testing — Discuss (Community Forum)

## Automated

### Backend (NUnit + FluentAssertions, Testcontainers Postgres)
`src/backend/tests/Integration/DiscussTests.cs` and `AdminDiscussTests.cs`.
Run (requires a docker daemon for the Postgres testcontainer):
```
cd src/backend
dotnet test tests/Sellevate.Tests.csproj --filter "FullyQualifiedName~Discuss"
```
Coverage: create thread with curated + free-form tags, validation, sort (new/unanswered/hot,
pinned-first), tag filter, search, thread detail + viewer upvote flag, reply increments count,
idempotent upvote + unvote (thread & reply), author/admin/non-author accepted-reply (403),
clear-solved, weekly top-author stats window, popular tags ordering, 401 when anonymous;
admin: 403 for non-admins, delete thread (cascade replies/tags/votes), delete accepted reply
clears flag + decrements count, pin (appears first), hot toggle, tag CRUD incl. duplicate-slug
409 + cascade, paged admin listing.

### Frontend (Vitest)
`src/frontend/__tests__/DiscussVoteButton.test.tsx` — VoteButton render/toggle/disabled/aria,
plus `formatTimeAgo` / `pluralizeRu` helpers.
```
cd src/frontend && npx vitest run __tests__/DiscussVoteButton.test.tsx
```

## Manual checklist
1. Nav: "Обсуждения" appears in top + bottom nav and routes to `/discuss`.
2. List: hero shows real thread/reply counts; hot/new/unanswered toggles change ordering;
   tag chips filter; search box filters by text.
3. Create: "Новая тема" opens modal; pick curated tags + add a free tag; publish → thread opens.
4. Thread: upvote toggles the count and highlights; post a reply → appears, count rises.
5. Accepted answer: as the thread author (or admin) mark a reply → "Решено" + "Лучший ответ"
   badges; non-authors don't see the control.
6. Sidebars: popular tags show counts; top authors of week list ranks.
7. Admin `/admin/discuss`: pin/unpin (pinned floats to top of user list), hot toggle, delete
   thread/reply, create/edit/delete curated tags; duplicate slug shows an error.
