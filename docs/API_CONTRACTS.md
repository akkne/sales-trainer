# API_CONTRACTS.md

Base URL: `http://localhost:5000` (dev) | `http://backend:8080` (docker internal)

All endpoints except those marked `[public]` require `Authorization: Bearer <accessToken>`.

> **Microservices migration:** `/auth/*`, `/demo/*`, `/profile/*`, `/onboarding/*` and
> `/avatars/*` are now served by the extracted **Identity service** (gateway base URL
> `http://localhost:5000`), not the monolith. Paths and request/response shapes are
> unchanged. One transitional caveat: `GET /profile` returns the activity-consistency / progress-points / completed-
> skill / average-score aggregates as **0** because those are owned by Gamification/Learning
> (not extracted yet, roadmap phases 7 & 8); the identity fields (displayName, email,
> persona, avatarUrl) are real. See [IDENTITY_SERVICE.md](IDENTITY_SERVICE.md).

---

## Gateway-injected headers

The gateway validates the JWT once and injects trusted identity headers into every downstream
request; client-supplied copies of these headers are always stripped first, so a caller cannot
spoof them. See `src/backend/gateway/Gateway/IdentityForwarding.cs` and
`src/backend/building-blocks/BuildingBlocks/Identity/IdentityHeaders.cs`.

| Header | Set from | Notes |
|---|---|---|
| `X-User-Id` | JWT `sub` (falls back to the `NameIdentifier` claim) | present on any authenticated request |
| `X-User-Role` | JWT `role` claim | present when the token carries a role |
| `X-Organization-Id` | JWT `org_id` claim | present once `identity-service` issues `org_id` (Phase 40.6); absent on tokens without it |

`org_role` (Phase 40.6) is **not** forwarded as a gateway header — unlike `X-User-Id`/
`X-User-Role`/`X-Organization-Id`, every service already validates the JWT itself
(`AddJwtBearer`, shared signing key), so all four policies (`RequirePlatformAdmin`,
`RequireSuperAdmin`, `RequireOrgAdmin`, `RequireOrgSuperAdmin`) read the `org_role`/`role` claims
straight off the validated token; no header round-trip needed.

`X-Organization-Id` populates `Sellevate.BuildingBlocks.Tenancy.ITenantContext` via
`TenantContextMiddleware`. A route marked `[TenantScoped]` (or built with
`.RequireTenantScope()`) returns `403 Forbidden` when the header is missing or malformed — the
caller is a validated identity that lacks organization context, not an unauthenticated one, so a
403 (not a 401) is returned. The organization is **never** read from the request body, query
string, or route — enforced by `scripts/tenancy-boundary-lint.py` (CI: `tenancy-boundary`
workflow). See [docs/TENANCY/TENANCY.md](TENANCY/TENANCY.md) section 1.3.

**Exception since 2026-08-16 (the owner's role split):** a caller whose validated `role` claim is
`Admin` or `SuperAdmin` — Sellevate's own staff — passes a `[TenantScoped]` route **without** the
header, and their reads span every organization. They normally hold no membership, so requiring the
header would lock the platform admin panel out of its own screens. The privilege comes from the
claim on the token this service authenticated, never from a header a client could send; the
organization header, when present, still only names an organization and still grants nothing. Their
writes are unchanged and still require an explicit organization. See TENANCY.md §1.6a.

---

## Auth `[public]`

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /auth/invites/{token}/accept `[public]` | `{displayName?, password?}` | `AuthTokenResponseDto` + cookie `refreshToken` |
| POST | /auth/verify-email | `{email, code}` | `AuthTokenResponseDto` + cookie `refreshToken` |
| POST | /auth/resend-code | `{email}` | 204 |
| POST | /auth/login/start | `{email}` | `{method}` |
| POST | /auth/login | `{email, password}` | `AuthTokenResponseDto` + cookie |
| POST | /auth/google | `{idToken}` | `AuthTokenResponseDto` + cookie |
| POST | /auth/refresh `[public]` | — (reads cookie) | `AuthTokenResponseDto` + new cookie |
| POST | /auth/logout | — | 204 |
| POST | /demo/token `[public]` | — | `{accessToken, expiresInSeconds}` |

`AuthTokenResponseDto`: `{accessToken, userId, displayName, isOnboardingCompleted, role, orgId, orgRole}`

> **There is no `POST /auth/register` (Phase 40.7).** The route is deleted, not guarded — see
> [TENANCY/TENANCY.md](TENANCY/TENANCY.md) section 4.1. The only way an account is created is
> `POST /auth/invites/{token}/accept`. `POST /auth/google` is a login method only: it rejects an
> unknown Google identity, and one whose account has no **active** membership, with `401` and a
> single identical message for both cases (it must not reveal which addresses belong to a
> customer).

> **Suspended organizations (Phase 40.9).** `/auth/login`, `/auth/google`, `/auth/refresh` and
> `/auth/invites/{token}/accept` all answer `403` (`"Organization suspended"`) when the caller's
> active membership belongs to a suspended organization. The check lives at the single point every
> one of those routes converges on — token issuance — so a route added later cannot miss it, and a
> refresh token cannot outlive the suspension by its 30-day lifetime. Already-issued access tokens
> still work until they expire (≤15 min): a JWT is not revocable without a session store. An
> organization identity-service has not heard of yet reads as **active**, never as suspended — the
> registry projection is eventually consistent and a lagging consumer must not lock a customer out.

**Three-step login (Phase 40.8).** The login method is a per-organization setting
(`organization_auth_config`, owned by identity-service), so the client asks first and sends a
credential second:

1. `POST /auth/login/start` `{email}` → `200 {"method": "password" | "oidc" | "saml"}`
2. the client renders the form for that method
3. `POST /auth/login` `{email, password}` — the server re-resolves the organization and
   dispatches to the `IAuthProvider` registered for its method

`/auth/login/start` is **pre-authentication and not tenant-scoped**: the caller has no token and
no `X-Organization-Id` yet — resolving that is what the step is for.

It answers `200` for **every syntactically valid address, known or not** (`400` only for a
malformed one), and never returns the organization id or name. This is the same anti-enumeration
choice `POST /auth/google` makes with its single identical `401`: the first step of a login screen
is the most reachable endpoint in the product, so its answer must not vary with whether the
address belongs to a customer. The organization is resolved from an **active membership** first
(the invite path) and from `allowed_email_domains` second; anything unmatched resolves to
`password`, exactly like a known address in a password organization.

`"oidc"`/`"saml"` are declared but **not implemented**. An organization configured for one has
password login refused — `POST /auth/login` returns `401` even for the correct password — rather
than silently downgraded. Only a customer who configures SSO makes their own domain's answer
differ; see [DECISIONS.md](DECISIONS.md) (2026-08-15, 40.8).

`orgId`/`orgRole` (Phase 40.6) are `null` unless the user has an active `membership` row —
absent membership is never implicit organization access. `GET /auth/me` mirrors the same
two fields (`orgId`, `orgRole`) alongside `id`/`email`/`displayName`/`role`/`isOnboardingCompleted`.

**Email verification flow.** Since 40.7 the invite replaces this flow for anyone arriving by
invite: possession of the token already proves control of the address, so an accepted invite
creates the user with `IsEmailVerified = true` and no code is ever sent. The code endpoints stay
for accounts that predate invites. `/auth/verify-email` takes a code and returns tokens.
`/auth/resend-code`
re-issues a code (silent 204 for unknown/already-verified emails to avoid account enumeration;
`429` with `Retry-After` + `{retryAfterSeconds}` while a resend cooldown is active).
`/auth/verify-email` returns `401` on an invalid/expired/exhausted code.
`/auth/login` returns `403 {message, requiresEmailVerification: true, email}` when the address
is not yet verified. Google sign-in is auto-verified. See [EMAIL_VERIFICATION.md](EMAIL_VERIFICATION.md).

---

## Platform superadmin `[SuperAdmin]` `[NOT tenant-scoped]` (Phase 40.9 — unchanged by the 2026-08-16 role split: impersonation and bootstrapping an organization's first admin are both superadmin-exclusive)

> Served by **identity-service** under `/admin/platform/*` (gateway route `identity-admin-platform`).
> Organization CRUD lives in organization-service; what lands here is everything that needs
> identity-db — minting a token and creating an invite. See [DECISIONS.md](DECISIONS.md) (2026-08-15).

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /admin/platform/impersonation | `{organizationId, reason}` | `ImpersonationTokenDto`, `404` unknown org, `403` suspended / already impersonating |
| GET | /admin/platform/impersonation | — | `ImpersonationAuditEntryDto[]`, newest first, max 100 |
| POST | /admin/platform/organizations/bootstrap-admin | `{organizationId, email}` | `BootstrapOrganizationAdminResponseDto`, `404`, `409` already has an admin, `403` suspended |

`ImpersonationTokenDto`: `{accessToken, expiresAt, impersonationId, organization: {id, name}}`
`ImpersonationAuditEntryDto`: `{id, actorUserId, actorEmail, organization: {id, name}, reason, issuedAt, expiresAt}`
`BootstrapOrganizationAdminResponseDto`: `{inviteId, organization: {id, name}, email, expiresAt, token}`

These are the **only** routes in the backend where an organization identifier arrives in a request
body. TENANCY.md §1.3 states the rule and this exception in the same breath: a superadmin crossing
a tenant boundary does so through an explicit endpoint that mints a new token, never through a
parameter on an ordinary route. `scripts/tenancy-boundary-lint.py` allow-lists the two request DTOs
by exact path and nothing else.

**The impersonation token is deliberately weaker than the one that requested it:**

| Property | Value | Why |
|---|---|---|
| `role` | `User` — **never** `SuperAdmin` | it cannot reach any `RequireSuperAdmin` route, which is what stops a second impersonation |
| `sub` | the superadmin's own user id | the impersonator borrows an organization, never an identity |
| `org_id` / `org_role` | target organization / `TenancyAdmin` | what it is for — deliberately one rank below `TenancySuperAdmin`, so an impersonator cannot add or remove the borrowed organization's users |
| `imp`, `imp_id`, `imp_actor` | marker claims | it is recognisable as an impersonation token wherever it turns up |
| lifetime | `Impersonation:TokenLifetimeMinutes`, default **15** | short |
| refresh token | **none** | the session cannot be silently renewed; extending it means asking again and writing another audit row |

The audit row is written and committed *before* the token is returned, so a token that exists
always has a record behind it. `reason` is required (3–500 chars).

`bootstrap-admin` reuses the Phase 40.7 invite machinery verbatim — same `IInviteService`, same
email, same one-time token — in a scope pinned to the target organization. The role is always
`TenancySuperAdmin` and is not taken from the request: only a superadmin can invite, so a first
admin one rank lower would leave the organization unable to add anybody. It answers `409` if the
organization already has an active `TenancySuperAdmin` membership or a pending `TenancySuperAdmin`
invite, so it cannot be used as a back door into a running customer's organization.

`404` also covers "organization-service created it seconds ago and identity-service has not
consumed `organization.created` yet"; the message says so and the operation is safe to retry.

---

## Invites & memberships `[OrgSuperAdmin]` `[tenant-scoped]`

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /invites | `{email?, emails?, role}` | `CreateInvitesResponseDto` |
| DELETE | /invites/{inviteId} | — | 204 / 404 |
| DELETE | /memberships/{userId} | — | 204 / 404 |

All three add or remove a user, which after the 2026-08-16 role split is the one privilege reserved
for a superadmin — so all three require `RequireOrgSuperAdmin` (`org_role = TenancySuperAdmin`, or a
platform `role = SuperAdmin`) **and** the gateway-injected `X-Organization-Id` header. They are
`[TenantScoped]`, so a request without that header gets `403` before the action runs. A
`TenancyAdmin` gets `403` here and nowhere else.

`CreateInvitesResponseDto`: `{created: [{id, email, role, expiresAt, token}], rejected: [{email, reason}]}`

- `email` and `emails` are both accepted and merged — one address or a pasted bulk list, so a
  РОП onboarding forty managers does not click forty times. A bulk request is **partially
  successful**: bad addresses land in `rejected` with a reason
  (`invalid-email`, `duplicate-in-request`, `already-a-member`, `invite-already-pending`)
  while the rest are created.
- `role` is an `OrgRole` (`Manager` / `TenancyAdmin` / `TenancySuperAdmin`); an unknown value is
  `400`. The retired name `OrgAdmin` is **rejected**, not mapped — the `400` message names both
  replacements so a stale client can fix itself (`docs/DECISIONS.md`, 2026-08-16). Bare numbers
  outside the enum are rejected too.
- `token` is the raw single-use token and is returned **only here, once** — the database keeps
  only its SHA-256 hash. It is also mailed to the invitee (MailerSend).
- The route deliberately carries **no organization segment**. The roadmap sketched
  `/organizations/{id}/invites`, but the organization must come from the header, never the
  route (section 1.3 of TENANCY.md), and `/organizations/*` already belongs to
  organization-service at the gateway. See [DECISIONS.md](DECISIONS.md) (2026-08-15).
- `DELETE /invites/{inviteId}` revokes a pending invite; an already-accepted invite and an
  invite belonging to another organization are both `404`.
- `DELETE /memberships/{userId}` is **offboarding, not deletion**: it sets
  `status = deactivated` + `deactivatedAt` and keeps the row. There is no endpoint that deletes
  a membership — the manager's history belongs to the organization.

`POST /auth/invites/{token}/accept` is the public counterpart and is *not* tenant-scoped: the
caller has no organization yet, so the organization is recovered from the token's own HMAC
signature. Responses:

| Status | Meaning |
|---|---|
| 200 | accepted — `AuthTokenResponseDto` + `refreshToken` cookie, `orgId`/`orgRole` already set |
| 400 | the address has no account yet and no `password` was supplied |
| 404 | unknown, malformed, tampered-with, or another organization's token (deliberately indistinguishable) |
| 409 | the invite was already used |
| 410 | the invite expired or was revoked |

Accepting an invite for an address that **already has an account** adds a membership to that
account; it never creates a second user.

## Onboarding

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /onboarding | `{salesType, experienceLevel, selectedSkillSlugs, persona?}` | 204 |

`salesType`: `b2b_saas` / `retail` / `real_estate` / `finance` / `b2c`  
`experienceLevel`: `beginner` / `experienced` / `manager`  
`selectedSkillSlugs`: array of skill slugs the user wants to enroll in (e.g. `["sales-basics","cold-calls"]`).
`sales-basics` is always included by the backend regardless of the payload.

---

## Skill Tree

> **Microservices (Phase 8):** all learner + admin content routes below — `/skills/*`,
> `/skill-tree`, `/lessons/*`, `/topics/*`, `/exercises/*`, `/reference/*`,
> `/techniques/*`, `/daily-quote`, and the content `/admin/*` routes — are served by the
> extracted **[learning-service](LEARNING_SERVICE.md)** through the gateway. Paths and
> shapes are unchanged. Two shape-preserving notes: the exercise-submission DTO returns
> `xpEarned: 0` and an empty `newlyUnlockedAchievementKeys` (progress points/milestones now belong
> to gamification, granted asynchronously from the `exercise.completed` event), and
> `/skill-tree` returns the activity-consistency/progress-points/goal aggregate fields as `0` (owned by
> gamification). AI-graded exercise types are scored by the learning-service calling the
> ai-service `POST /ai/evaluate`.

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /skill-tree | — | `SkillTreeResponseDto` |
| GET | /skills | — | `SkillTreeNodeDto[]` (all skills, `locked` if not enrolled) |
| GET | /skills/stages | — | `SkillStageDto[]` (admin-configured funnel stages, ordered) |
| PUT | /skills/enrolled | `{skillSlugs: string[]}` | 204 |

`PUT /skills/enrolled` — replaces the user's enrolled skill set.  
Skills in the list that are not yet enrolled are set to `available`.  
Skills currently enrolled but absent from the list are set to `locked` (progress preserved).  
`sales-basics` is always kept enrolled.

`SkillTreeResponseDto`: `{skillNodes[], currentStreakDayCount, totalXpAmount, weeklyXpAmount, dailyXpAmount, dailyXpGoal, weeklyXpGoal}`  
`dailyXpAmount`/`weeklyXpAmount` = progress points earned today / this week (UTC); `dailyXpGoal`/`weeklyXpGoal` = targets from the admin-editable `GamificationSettings` table (defaults 100 / 500), not hardcoded config.  
`SkillTreeNodeDto`: `{skillId, slug, title, iconName, sortOrder, status, completedLessonCount, totalLessonCount, isLocked, stage}`. `stage` is the funnel-stage bucket the skill belongs to — see `Skills.Stage` in [DB_SCHEMA](DB_SCHEMA.md).  
`SkillStageDto`: `{key, label, accent, order}` — the admin-editable display metadata for a funnel stage (label, CSS accent color, sort order). The frontend groups `/tree` by `stage` and resolves each bucket's label/color via this list, falling back to built-in defaults while it loads. Stages are managed via the admin endpoints below; `general` is the implicit fallback bucket for unassigned skills and is not a stored row.

---

## Programme (Phase 40.17)

The learner's own view of the frozen curriculum they are pinned to, and the only route in the system
that moves a pin. Design: [TENANCY/CONTENT_MODEL.md](TENANCY/CONTENT_MODEL.md) §2.5.

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /program | — | `MyProgramDto` |
| POST | /program/switch | `{targetProgramVersionId}` | `MyProgramDto` (409 if the target cannot be switched to) |

`MyProgramDto`: `{isEnrolled, programVersionId, programVersionNumber, enrolledAt, switchedAt, items[], latestPublishedProgramVersionId, latestPublishedProgramVersionNumber, switchAvailable, pendingDiff}`
`ProgramItemDto`: `{id, skillId, lessonId, lessonVersionId, lessonVersionNumber, lessonTitle, orderIndex}`

`lessonTitle` is read out of the **pinned snapshot**, not off the live `Lessons` row. Showing the
current title next to an old pin is exactly the retroactive substitution the phase exists to stop;
`null` means the snapshot is no longer visible, which is a truer answer than the new title.

`isEnrolled: false` is a normal answer, not an error. An organization that has published no
programme version has no pins, and its people go on reading the live skill tree as they always have
— enrollment narrows and freezes, it does not gate (see [DECISIONS.md](DECISIONS.md), 2026-08-17).

`pendingDiff` is present only when `switchAvailable` is true, and is the same `ProgramDiffDto` the
admin diff route returns. **Nothing about it is applied until the learner calls `/program/switch`.**

`POST /program/switch` acts on the caller's own pin and takes no user id; there is deliberately no
route by which anyone moves anybody else's. The target version is **named** rather than implied, so
that a version published between showing the diff and accepting it cannot become the one the learner
lands on. 409 covers all three refusals — not enrolled, target is not a published version of this
organization, target is the version they are already on — because each of them means "there is
nothing here to switch to" and none should resolve into a move nobody asked for.

---

## Lessons & Exercises

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /skills/:slug/lessons | — | `LessonSummaryDto[]` |
| GET | /lessons | — | `LessonSummaryDto[]` (all skills) |
| GET | /lessons/:lessonId/exercises | — | `ExerciseDto[]` |
| GET | /lessons/:lessonId/next | — | `NextLessonDto` or 204 if no next lesson |
| POST | /exercises/:exerciseId/submit | `{answer: <jsonb>}` | `ExerciseSubmissionResultDto` |
| POST | /exercises/:exerciseId/chat | `{message: string}` | `ExerciseChatResponseDto` |
| POST | /exercises/:exerciseId/voice/stream | `{message: string}` | `application/octet-stream` — length-prefixed frames |

`LessonSummaryDto`: `{lessonId, title, orderInTopic, topicOrder, status, bestScore, kind}` where `kind` is `"theory"` (every exercise is a `theory_card`) or `"practice"`. Theory lessons are played as swipeable cards; the client submits the last card once to complete them. Across a skill, lessons are ordered by `topicOrder` (the topic's `OrderInSkill`) first, then by `orderInTopic` — so topics stay grouped instead of interleaving; the client sorts by `(topicOrder, orderInTopic)`.

**AI Dialog Chat Endpoint:**
`POST /exercises/:exerciseId/chat` — for `ai_dialog` type exercises only. Handles multi-turn conversation.
`ExerciseChatResponseDto`: `{response: string, isComplete: boolean, turnNumber: number, maxTurns: number}`
The **user speaks first** — an empty `message` returns an empty turn (no AI greeting); the AI only replies after the user's opening line.

**AI Dialog Voice Endpoint:**
`POST /exercises/:exerciseId/voice/stream` — voice mode for `ai_dialog` exercises. Streams the same length-prefixed `[flags u32][textLen u32][text][audioLen u32][audioMp3]` frames as the live-call voice stream (`flags` bit0 = isFinal, bit1 = isStopSignal/endCall). Shares chat history with `/chat`, so text and voice turns interleave. Uses the same TTS pipeline as calls.

**Lesson unlock behavior:**
- First call to `GET /skills/:slug/lessons` lazy-seeds `UserLessonProgress` rows: lesson 1 → `available`, rest → `locked`.
- Submitting a correct answer marks the lesson `completed` and sets the next lesson (by `sortOrder`) to `available`.
- See [LESSON_UNLOCK.md](LESSON_UNLOCK.md) for full details.

`ExerciseDto.content` shape by type:

**multiple_choice**: `{situation, question, options[], correctOptionIndex, explanation?}`
**fill_blank**: `{characterName, characterLine, options[], correctOptionIndex, explanation?}`
**free_text**: `{situation, prompt, evaluationCriteria}`
**ordering**: `{situation, items[{id, text}], correctOrder[], explanation?}`
**matching**: `{situation, leftColumn[{id, text}], rightColumn[{id, text}], correctPairs[{left, right}], explanation?}`
**categorizing**: `{situation, items[{id, text}], categories[{id, title, color}], correctMapping{itemId: categoryId}, explanation?}`
**find_error**: `{situation, dialogLines[{id, speaker, text}], errorLineId, aiPrompt?, requireExplanation?, suggestedFixes?[{id, text}], correctFixIds?[]}`
**rewrite_better**: `{situation, originalText, context?, aiPrompt, minLength?, maxLength?}`
**ai_dialog**: `{situation, persona{name, role, description}, chatSystemPrompt, aiPrompt, maxTurns?, minTurnsForCompletion?}`
**rate_call**: `{situation, transcript[{speaker, text}], criteria[{id, name, description}], ratingScale{min, max}, aiPrompt}`
**written_answer**: `{prompt, context?, aiPrompt, minLength?, maxLength?}`

`answer` shape by type:

**multiple_choice / fill_blank**: `{selectedOptionIndex: number}`
**free_text**: `{text: string}`
**ordering**: `{order: string[]}` — item IDs in user's order
**matching**: `{pairs: [{left, right}]}`
**categorizing**: `{mapping: {itemId: categoryId}}`
**find_error**: `{selectedLineId, explanation?, selectedFixId?}`
**rewrite_better**: `{rewrittenText: string}`
**ai_dialog**: `{messages: [{role, content}], completedNaturally: boolean}`
**rate_call**: `{ratings: {criterionId: number}, overallComment?: string}`
**written_answer**: `{text: string}`

`NextLessonDto`: `{lessonId, title, xpReward}` — next lesson in same skill with status `available` or `in_progress`. Returns 204 when no next lesson exists.

---

## Reference

| Method | Path | Response |
|---|---|---|
| GET | /skills/:slug/reference | `ReferenceMaterialDto[]` |

`ReferenceMaterialDto`: `{materialId, title, markdownContent, sortOrder}`

---

## Techniques (Handbook / "Коллекция")

All routes require auth. Card response includes per-user mastery state; `/meta` aggregates per-user counts. See [HANDBOOK_REDESIGN.md](HANDBOOK_REDESIGN.md).

| Method | Path | Query / Body | Response |
|---|---|---|---|
| GET | /techniques | `?skill=&search=&tag=` (repeatable) | `TechniqueCardDto[]` |
| GET | /techniques/meta | — | `TechniqueMetaDto` |
| GET | /techniques/:slug | — | `TechniqueDetailDto` |
| POST | /techniques/:slug/seen | `{}` | 204 (sets `FirstSeenAt`, clears `isNew`) |

`skill` filter matches `Skills.IconicName` (not id) so URLs stay human-readable. `tag` can be repeated (`?tag=objection&tag=discovery`) — AND semantics. `search` matches (case-insensitive) on `Name`, `Summary`, `Body`, and `Tags`.

`TechniqueCardDto`: `{id, slug, name, summary, tags: string[], primarySkillIconicName?, primarySkillTitle?, difficulty, difficultyName, sortOrder, masteryLevel, masteryPercent, hasDialog, hasCase, hasCoach, isNew}`

`difficulty`: 1=Novice, 2=Practitioner, 3=Expert, 4=Master — static per-technique property. `difficultyName` is its display form. `masteryLevel` / `masteryPercent` are per-user. `hasDialog` / `hasCase` / `hasCoach` let the card show the right tabs without the detail round-trip.

`TechniqueDetailDto`: `{card: TechniqueCardDto, body, skillIconicNames: string[], dialogTurns: TechniqueDialogTurnDto[], case?: TechniqueCaseDto, coach?: TechniqueCoachDto}`

`TechniqueDialogTurnDto`: `{orderIndex, side: "me"|"them", text, annotations: [{label, tone?}]}`
`TechniqueCaseDto`: `{title, body, metrics?}` — `metrics` is a free JSON object (e.g. `{deal: "$124k", cycleDays: 41}`). At most one case per technique.
`TechniqueCoachDto`: `{avatarSeed, name, role, quote, challenges: [{label, kind?, targetSlug?}]}`

`TechniqueMetaDto`: `{skills: [{iconicName, title, techniqueCount}], totalCount, userCounts: {mastered, master, unseen}}`. Only skills that have at least one technique appear in `skills`.

---

## Profile

> **Microservices (Phase 7):** `/profile/achievements` is served by the extracted
> **[gamification-service](GAMIFICATION_SERVICE.md)** through the gateway (more specific
> than Identity's `/profile/*`); the response shape is unchanged. The activity-consistency/progress-points/skill
> aggregates inside `GET /profile` (Identity) are composed from gamification's
> `GET /gamification/progress` once Identity consumes it (Phase 2 caveat).

| Method | Path | Response |
|---|---|---|
| GET | /profile | `UserProfileStatsDto` |
| GET | /profile/achievements | `AchievementDto[]` |
| PUT | /profile/persona | `{persona: string}` → 204 |
| PUT | /profile | `{displayName: string (1–100, required), persona?: string}` → 204 |

> `PUT /profile` updates the user's display name (and, when `persona` is provided and
> valid, upserts the persona in one call). `displayName` is trimmed; empty → `400`,
> `>100` chars → `400`, `persona` outside the allow-list (`sdr`, `account_executive`,
> `account_manager`, `founder`, `other`) → `400`, unknown user → `404`. A successful
> update publishes `UserUpdatedEvent` so replica-holding services (ai, notification, …)
> refresh their cached display name.

### Progress tracking data (Phase 7)

Served by the gamification-service through the gateway.

| Method | Path | Response |
|---|---|---|
| GET | /gamification/progress | `GamificationProgressDto` |

`GamificationProgressDto`: `{currentStreakDayCount, longestStreakDayCount, totalXpAmount, dailyXpAmount, weeklyXpAmount, dailyXpGoal, weeklyXpGoal}`

`UserProfileStatsDto`: `{displayName, email, currentStreakDayCount, longestStreakDayCount, totalXpAmount, completedSkillCount, totalSkillCount, averageExerciseScore, persona?, avatarUrl}`

`persona` values: `sdr` | `account_executive` | `account_manager` | `founder` | `other`

`AchievementDto`: `{achievementId, key, title, description, iconEmoji, isUnlocked, unlockedAt}`

Achievement condition types: `first_lesson` | `lesson_count` | `xp_total` | `streak_days` | `skill_completed`

`ExerciseSubmissionResultDto` now includes `newlyUnlockedAchievementKeys: string[]` — keys of milestones unlocked in this submit.

---

## League / Team Progress

> **Microservices (Phase 7):** `/league` (and `/admin/leagues/*`, `/admin/gamification/*`)
> are served by the extracted **[gamification-service](GAMIFICATION_SERVICE.md)** through
> the gateway — paths and DTO shapes unchanged. League data is DB-backed on the
> `gamification` Postgres database; participant display names/avatars come from a local
> `UserReplica` (no join into Identity). The weekly closure job runs on the
> gamification service's own Hangfire schema.

| Method | Path | Response |
|---|---|---|
| GET | /league | `CurrentLeagueResponseDto` |

`CurrentLeagueResponseDto`: `{leagueId, tier, tierName, tierColor, weekStartDate, weekEndDate, periodEndsAt, participantsByRank[], currentUserRank, previousWeekOutcome: "promoted"|"demoted"|null, promotionZoneSize, demotionZoneSize, maximumLeagueParticipantCount}`

- `promotionZoneSize`/`demotionZoneSize`/`maximumLeagueParticipantCount`: live from `LeagueSettings` (admin-configurable). The user team-progress page must render zones from these, not hardcoded constants.
- `tierName`/`tierColor`: presentation for `tier`, resolved from the admin-editable `LeagueTiers` table (fall back to the tier key + neutral color if the tier was deleted).
- `periodEndsAt` (ISO-8601 instant): exact moment the current period closes. The team-progress-tab countdown MUST target this, not the day-start of `weekEndDate`.
`LeagueParticipantDto`: `{userId, displayName, weeklyXpAmount, rank, isCurrentUser, avatarUrl}`

Tiers: configurable via the `LeagueTiers` table (admin CRUD below). Default ladder `bronze → silver → gold → diamond`; the promotion ladder follows `Order` ascending (entry tier = lowest order).
- Top N per tier promoted to next tier next period, bottom M demoted (cannot drop below the lowest-order tier) — zone sizes come from the `LeagueSettings` table (defaults: promotion 10, demotion 5, max participants 30), editable via `/admin/leagues/settings`
- Period scheduling: the current period start/end live in `LeagueSettings` (`CurrentPeriodStartDate`, `CurrentPeriodEndsAt`, `PeriodLengthDays`). A recurring job (every 15 min) closes the period and creates the next only once `CurrentPeriodEndsAt` has passed, so an admin-set end date drives the schedule.
- `previousWeekOutcome`: shown only if user had a membership last week; use for in-app banner

---

## Daily Quote

| Method | Path | Response |
|---|---|---|
| GET | /daily-quote?date=YYYY-MM-DD | `DailyQuoteDto` or 204 if no quotes exist yet |

`DailyQuoteDto`: `{text, author, date}`. `date` query param is optional (defaults to UTC today; the frontend passes the client's local date). Returns the quote for the requested date, falling back to the most recent quote at or before it — so the widget keeps showing the last scheduled quote on days without a dedicated one. Requires auth (any role); managed via `/admin/daily-quotes` (see ADMIN_PANEL.md).

---

## Auth — updated response

`AuthTokenResponseDto` now includes `role: "User" | "Admin" | "SuperAdmin"` (the global `Admin` role
was removed in Phase 40.6 — see below) plus the nullable `orgId`/`orgRole` pair.

---

## Admin (requires `RequirePlatformAdmin` policy — revised 2026-08-16)

All routes prefixed `/admin`. Unauthorized → 403.

> **2026-08-16 — platform content administration is `RequirePlatformAdmin`.** Every `/admin/*`
> content endpoint below is open to Sellevate staff at either rank (`role` ∈ {`Admin`,
> `SuperAdmin`}). The only routes that stay `RequireSuperAdmin` are the ones that add or remove a
> user — the `/admin/users` mutations and all of `/admin/platform/*`. The organization roles
> (`TenancyAdmin`, `TenancySuperAdmin`) live on `membership`, not on `user`, and their own admin
> screen is roadmap block 40.20, waiting on the owner's design; `RequireOrgAdmin` is declared in
> every service for it but has zero call sites today. See
> [IDENTITY_SERVICE.md](IDENTITY_SERVICE.md), [ADMIN_PANEL.md](ADMIN_PANEL.md) and
> `docs/DECISIONS.md` (2026-08-16) for the full route audit.

### Skills
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/skills | — | `AdminSkillDto[]` |
| POST | /admin/skills | `{iconicName, title, description?, orderInTree, stage?}` | `AdminSkillDto` |
| PUT | /admin/skills/:id | `{iconicName?, title?, description?, orderInTree?, stage?}` | `AdminSkillDto` |
| DELETE | /admin/skills/:id | — | 204 |

`AdminSkillDto`: `{id, iconicName, title, description, orderInTree, stage}`. `stage` is the `key` of a configured Skill Stage (see below) — built-in keys are `preparation`, `discovery`, `engagement`, `closing`, `retention`; `general` is the fallback default. Drives the grouped sidebar on `/tree`.

### Skill Stages
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/skill-stages | — | `AdminSkillStageDto[]` |
| POST | /admin/skill-stages | `{key, label, accent, order}` | `AdminSkillStageDto` |
| PUT | /admin/skill-stages/:id | `{label, accent, order}` | `AdminSkillStageDto` |
| DELETE | /admin/skill-stages/:id | — | 204 |

`AdminSkillStageDto`: `{id, key, label, accent, order}`. The funnel stages used to group skills on `/tree`, replacing the previously frontend-hardcoded list. `key` is the immutable slug stored on `Skills.Stage`; only `label`, `accent` (CSS color, e.g. `#7C3AED` or `var(--indigo)`), and `order` are editable. `key` is lowercased and must be unique. **Create** rejects a duplicate key (`400`); **Update** ignores any key change. **Delete** is blocked (`400`) while any skill is still assigned to the stage — reassign those skills first. Seeded defaults match the original 5 stages; `general` is the implicit fallback and is intentionally not a stored row.

### Topics
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/topics | — | `AdminTopicWithSkillDto[]` |
| GET | /admin/skills/:skillIconicName/topics | — | `AdminTopicDto[]` |
| POST | /admin/skills/:skillIconicName/topics | `{iconicName, title, orderInSkill}` | `AdminTopicDto` |
| PUT | /admin/topics/:id | `{iconicName?, title?, orderInSkill?}` | `AdminTopicDto` |
| DELETE | /admin/topics/:id | — | 204 |

`AdminTopicDto`: `{id, skillId, iconicName, title, orderInSkill}`
`AdminTopicWithSkillDto`: `{id, skillId, skillIconicName, skillTitle, iconicName, title, orderInSkill}`

### Lessons
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/lessons | — | `AdminLessonWithTopicDto[]` |
| GET | /admin/topics/:topicIconicName/lessons | — | `AdminLessonDto[]` |
| POST | /admin/topics/:topicIconicName/lessons | `{title, orderInTopic, slug?}` | `AdminLessonDto` (400 if `slug` is malformed) |
| PUT | /admin/lessons/:id | `{title, orderInTopic, slug?}` | `AdminLessonDto` (400 if `slug` is malformed) |
| DELETE | /admin/lessons/:id | — | 204 |

`AdminLessonDto`: `{id, topicId, title, orderInTopic, slug, isArchived}`
`AdminLessonWithTopicDto`: `{id, topicId, topicIconicName, topicTitle, title, orderInTopic}`

`slug` (Phase 40.15) is optional in both directions. Omitted on create, the lesson gets a
collision-free machine slug derived from its own id; omitted on update, the existing slug is left
alone rather than regenerated — regenerating would change the lesson's stable identifier on every
title edit, which is the one thing a slug must not do. Supplied, it is validated (lowercase latin
letters, digits, single hyphens) and rejected with 400 rather than silently rewritten.

### Lesson versions (Phase 40.15)

Immutable snapshots of a lesson together with its full ordered exercise set. Design:
[TENANCY/CONTENT_MODEL.md](TENANCY/CONTENT_MODEL.md) §2.

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/lessons/:lessonId/versions | — | `LessonVersionSummaryDto[]`, newest first (404 if no such lesson) |
| GET | /admin/lessons/:lessonId/versions/:versionId | — | `LessonVersionDto` |
| POST | /admin/lessons/:lessonId/versions/draft | — | `LessonVersionDto` (the lesson's single draft, created if absent) |
| POST | /admin/lessons/:lessonId/versions/publish | `{isBreaking?}` (default `false`) | `PublishLessonVersionResultDto` |

`LessonVersionSummaryDto`: `{id, lessonId, versionNumber, status, contentHash, baseVersionId, isBreaking, createdBy, createdAt, publishedAt}`
`LessonVersionDto`: the same plus `content` — the snapshot, as a JSON object rather than a string
`PublishLessonVersionResultDto`: `{version, createdNewVersion}`

`createdNewVersion: false` means the content hash matched the last published version: nothing
changed, so nothing was frozen and the existing version comes back unchanged. The caller should say
"no changes to publish" rather than show a version number that did not move.

`isBreaking` is the one thing publishing cannot infer. A typo fix and a changed correct answer look
identical to a diff, and the accuracy series joins across the first and splits across the second
(Phase 40.16, below) — so the publisher declares which it was.

**Authorization is two-part.** The controller carries `RequireOrgAdmin`, which admits an
organization's own administrator as well as any platform administrator. That alone is not enough:
a lesson with `OrganizationId IS NULL` is the global library every customer reads, so both write
routes additionally require platform administrator rights when the lesson is global, answering 403
otherwise. The reverse direction needs no check — another organization's lessons were already
invisible before the request arrived, through the query filter and the RLS policy. As everywhere,
the organization comes from the gateway-validated `X-Organization-Id` header via `ITenantContext`,
never from the body, the query string or the route.

### Lesson accuracy by version (Phase 40.16)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/lessons/:lessonId/accuracy | — | `LessonAccuracySeriesDto` (404 if no such lesson) |

`LessonAccuracySeriesDto`: `{lessonId, segments[], unversionedAttempts}`
`LessonAccuracySegmentDto`: `{startVersionNumber, endVersionNumber, versionNumbers[], versionIds[], startsAtBreakingChange, statistics}`
`LessonAttemptStatisticsDto`: `{attemptCount, correctAttemptCount, accuracy, averageScore, firstAttemptAt, lastAttemptAt}`

`accuracy` is a fraction in `0..1`, not a percentage, and is `0` when `attemptCount` is `0` — a
segment with no attempts is returned rather than omitted, because "nobody has answered this version
yet" and "this version does not exist" are different answers.

**A segment is a run of versions the chart may draw as one line.** It starts at the lesson's first
published version and at every version published with `isBreaking: true`; cosmetic versions extend
the segment they follow. Draft versions are excluded (no learner ever saw one); archived versions are
kept (they were live once, and dropping them would erase the history of everyone who studied then).

`unversionedAttempts` counts attempts with no `lessonVersionId` at all — everything recorded before
40.16, until `docs/TENANCY/sql/40.16_progress_version_backfill.sql` is run. They are a separate
bucket rather than part of version 1 on purpose: nobody can prove which content those answers were
scored against, and folding them in would be the same retroactive claim the phase exists to stop.

`RequireOrgAdmin`, and — unlike the publish routes above — with no second platform-level gate. This
endpoint writes nothing and counts only the caller's own organization's attempts: the RLS policy on
`UserExerciseAttempts` is plain equality, so an organization administrator asking about a global
lesson gets their own team's numbers and nobody else's, which is exactly what a РОП is entitled to
ask.

### Programme versions and enrollment (Phase 40.17)

The РОП's curriculum: an ordered list of references, frozen on publish, and who is standing on which
frozen copy. Design: [TENANCY/CONTENT_MODEL.md](TENANCY/CONTENT_MODEL.md) §2.5.

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/program/versions | — | `ProgramVersionSummaryDto[]`, newest first |
| GET | /admin/program/versions/:programVersionId | — | `ProgramVersionDto` |
| GET | /admin/program/versions/:programVersionId/diff/:baselineProgramVersionId | — | `ProgramDiffDto` (from baseline → version) |
| POST | /admin/program/versions/draft | — | `ProgramVersionDto` (the organization's single draft, created if absent) |
| POST | /admin/program/versions/publish | — | `PublishProgramVersionResultDto` (409 if there is no draft) |
| GET | /admin/program/enrollments | — | `ProgramEnrollmentDto[]` |
| POST | /admin/program/enrollments | `{userId}` | `ProgramEnrollmentDto` (409 if nothing is published yet) |

`ProgramVersionSummaryDto`: `{id, versionNumber, status, itemCount, enrollmentCount, createdBy, createdAt, publishedAt}`
`ProgramVersionDto`: `{id, versionNumber, status, createdBy, createdAt, publishedAt, items[]}`
`PublishProgramVersionResultDto`: `{version, createdNewVersion}`
`ProgramEnrollmentDto`: `{userId, programVersionId, programVersionNumber, previousProgramVersionId, enrolledAt, switchedAt}`

`POST .../draft` re-derives the draft's items from the live skill tree every time it is called:
skills by their position in the tree, topics by theirs, lessons by theirs, archived lessons left out.
Each item is pinned to the lesson's newest published version, minting a version 1 for a lesson that
has never been published — through the same resolver an exercise attempt goes through, so a
programme and the progress recorded against it can never disagree about which snapshot a lesson
currently is.

`createdNewVersion: false` means the draft's items were identical to the last published version's:
nothing was frozen, the draft was discarded, and the existing version comes back. This matters more
than the lesson equivalent — a version that changed nothing would still tell every enrolled learner
that a new programme is waiting and then show them an empty diff, which is how a switch notice stops
being read.

`ProgramDiffDto`: `{fromProgramVersionId, fromVersionNumber, toProgramVersionId, toVersionNumber, addedLessons[], removedLessons[], changedLessons[], movedLessons[], hasBreakingChanges}`
`ProgramDiffLessonDto` (added/removed): `{lessonId, skillId, lessonVersionId, lessonVersionNumber, lessonTitle, orderIndex}`
`ProgramDiffVersionChangeDto` (changed): `{lessonId, skillId, lessonTitle, fromLessonVersionId, fromLessonVersionNumber, toLessonVersionId, toLessonVersionNumber, isBreaking}`
`ProgramDiffMoveDto` (moved): `{lessonId, lessonTitle, fromSkillId, toSkillId, fromOrderIndex, toOrderIndex}`

Four buckets rather than one list, because they mean four different things to whoever decides.
`movedLessons` is the whole content of a "reorder the skills" edit and is the proof that such an edit
touched no lesson — same lesson, same pinned snapshot, different place.

`isBreaking` on a changed lesson is **not** read off the target version's own flag. A programme can
skip several lesson versions at once, so the answer is "did any published version of this lesson
between the two pins declare itself breaking" — every version strictly after the lower of the two
version numbers and up to and including the higher, so that a move back to an older programme is
reported just as loudly. A pin whose snapshot is missing or invisible counts as breaking: "the
content changed and nobody can say how" is a breaking change.

**Enrollment is asymmetric, and that asymmetry is the block.** `POST /admin/program/enrollments`
puts a learner with no pin on the newest published version and is idempotent — a learner who already
has a pin comes back **unchanged**, not moved. An administrator re-running it after publishing
therefore enrolls the newcomers and leaves everybody mid-course exactly where they were. Moving an
existing pin is the learner's own act (`POST /program/switch`), and no admin route does it.

**Authorization is one-part, unlike the lesson-version routes above.** `RequireOrgAdmin` and nothing
else: there is no such thing as a global programme — `ProgramVersions.OrganizationId` is `NOT NULL`
and the RLS policy is plain equality — so the "is this the global library?" question that forced a
second gate onto lesson publishing has no analogue here. The write routes do refuse a caller with no
organization in context at all (403): platform staff satisfy `RequireOrgAdmin` without holding a
membership, and a programme with no owner is not a thing that can be written. As everywhere, the
organization comes from the gateway-validated `X-Organization-Id` header via `ITenantContext`.

### Assignments (Phase 40.21, thresholds 40.22)

The РОП's targeted practice: what the team is asked to do after an internal training, who it is for,
what counts as done, and who is where on it. Design:
[TENANCY/ASSIGNMENTS.md](TENANCY/ASSIGNMENTS.md) §1.

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/assignments?status=draft\|active\|closed | — | `AssignmentSummaryDto[]`, newest first (400 on an unknown status) |
| GET | /admin/assignments/:assignmentId | — | `AssignmentDto` |
| GET | /admin/assignments/:assignmentId/progress | — | `AssignmentProgressDto[]` |
| POST | /admin/assignments | `CreateAssignmentRequestDto` | `AssignmentDto` (a draft) |
| PUT | /admin/assignments/:assignmentId | `UpdateAssignmentRequestDto` | `AssignmentDto` (409 when the status forbids the edit) |
| POST | /admin/assignments/:assignmentId/activate | — | `AssignmentDto` (409 if it is not a draft, or has no content) |
| POST | /admin/assignments/:assignmentId/close | — | `AssignmentDto` (409 if it is not active) |
| DELETE | /admin/assignments/:assignmentId | — | 204 (409 for anything that has been issued) |

`CreateAssignmentRequestDto` / `UpdateAssignmentRequestDto`:
`{title, goal?, sourceType, sourceRef?, content?: AssignmentContentItemDto[], audience?, opensAt?, deadline?, completionRule, repeatSchedule?}`
`AssignmentContentItemDto`: `{kind, reference, orderIndex}`
`AssignmentAudienceDto`: `{kind, userIds?, groupId?}`
`AssignmentDto`: `{id, title, goal, sourceType, sourceRef, content[], audience, opensAt, deadline, completionRule, repeatSchedule, status, createdBy, createdAt, updatedAt, activatedAt, closedAt}`
`AssignmentSummaryDto`: `{id, title, sourceType, status, audienceKind, opensAt, deadline, hasRepeatSchedule, contentItemCount, assignedCount, startedCount, completedCount, failedThresholdCount, createdBy, createdAt, updatedAt}`
`AssignmentProgressDto`: `{userId, status, bestScore, attemptCount, firstOpenedAt, completedAt}`

**`completionRule` is required, has no default, and since 40.22 is checked against a closed
vocabulary.** If completion could mean "opened everything", managers would click through in four
minutes, the dashboard would read 100%, and the number would be a lie the РОП eventually catches — so
the API has no way to express it. Two kinds, both from the roadmap:

| `kind` | Shape | One attempt is | Met when | `bestScore` on the progress row |
|---|---|---|---|---|
| `dialog_score` | `{"kind":"dialog_score","minimumScore":70,"requiredCount":3}` | one graded practice conversation on one of the assignment's `dialog_scenario` items | `requiredCount` conversations have each scored at least `minimumScore` | the best single conversation score so far |
| `exercise_accuracy` | `{"kind":"exercise_accuracy","minimumAccuracyPercent":80}` | one exercise submission against the assignment's pinned `lesson_version` | every exercise in the pinned set has been attempted **and** correct submissions ÷ all submissions ≥ `minimumAccuracyPercent` | `null` until the whole set has been attempted, then the accuracy percent |

Anything else is a **400**: an unknown `kind`, a missing number, a bar outside 1–100, a
`requiredCount` outside 1–20. A bar of **zero is refused explicitly** — "score at least 0" is a
threshold every click clears, which is the failure mode wearing a discriminator. Counting
conversations rather than averaging them is deliberate (an average lets one strong call carry two
weak ones), and so is counting exercise *submissions* rather than exercises-eventually-correct
(brute-forcing a set lowers accuracy instead of raising it).

`POST /activate` additionally **409s when the rule measures content the assignment does not carry** —
`dialog_score` with no `dialog_scenario` item, `exercise_accuracy` with no `lesson_version` item.
Issuing freezes both the rule and the content, so this is the last moment they can be reconciled, and
an assignment nobody can ever finish is indistinguishable on the dashboard from a team that has not
started.

`repeatSchedule` (optional) is still checked only for being an object naming a `kind`; its vocabulary
is 40.24's.

Work done **before** the assignment was issued never counts towards it: the measurement window opens
at the later of `activatedAt` and `opensAt`.

`content` is a list of **references**, never exercise bodies. Three kinds:

| `kind` | `reference` | Why |
|---|---|---|
| `lesson_version` | a `LessonVersions.Id` | The assignment's exercise set. A frozen snapshot, so a recorded score always describes content somebody can still read; pointing at a mutable `Exercise` id would repeat the defect 40.16 removed from progress. The eleven existing exercise types render it with **no new code** |
| `dialog_scenario` | an ai-service dialog mode **key** | The practice conversation. A key, not a uuid — that is how ai-service addresses modes. 40.23 turns it into an ordinary `DialogSession` with an injected persona |
| `reference_material` | a `ReferenceMaterials.Id` | Ungraded theory, resolved through the same override and substitution path as any other read |

Duplicate `(kind, reference)` pairs are a 400, `orderIndex` is re-derived densely from the order the
items arrive in, and a `lesson_version` or `reference_material` reference that is not a uuid is a 400.

`audience` is the **rule**, not the resolved people: `{"kind":"whole_team"}`,
`{"kind":"users","userIds":[…]}` or `{"kind":"group","groupId":…}`, defaulting to `whole_team`. The
employee list lives in identity-service, so learning-service deliberately does not check the ids
against membership — it cannot, and a stale copy would be worse than none. Resolving the rule into
people and notifying them is 40.23; until then **nothing creates a progress row**, so
`GET /admin/assignments/:id/progress` returns `[]` and every funnel count on the summary reads zero.
40.22 wrote the *updater* — what moves a row between the four statuses — not the creator: a row's
existence means "this person was asked", which is a fact about issue time.
The `group` kind is accepted structurally so 40.23 needs no migration, but nothing in the platform
defines a group yet.

`sourceType` is `training`, `manual` or `gap_detected`, and `sourceRef` is read according to it: a
`manual` assignment with a source reference is a 400. When the reference names library content it must
name a **frozen version** (`lesson-version:<uuid>`), never a lesson.

**Status transitions are one-way: `draft → active → closed`.** A draft is fully editable and
deletable. An issued assignment refuses edits to `sourceType`, `sourceRef`, `content` and
`completionRule` with a 409 naming the fields — refused rather than silently ignored, because an
administrator who believes they moved a threshold and did not is worse off than one who is told they
cannot. Title, goal, audience, opening time, deadline and repeat schedule stay editable, because adding
three people to a running assignment and extending a deadline are ordinary acts. A closed assignment is
frozen whole and cannot be reopened; the answer to "we want that practice again" is a new assignment,
which is also what 40.24's repeats will create. The database enforces all of this with a trigger, not
only the service.

**Authorization is one-part, like the programme routes above.** `RequireOrgAdmin` and nothing else:
there is no global assignment — `Assignments.OrganizationId` is `NOT NULL` and the RLS policy is plain
equality — so the "is this the global library?" question that forces a second gate onto lesson
publishing has no analogue. The write routes refuse a caller with no organization in context at all
(403): platform staff satisfy `RequireOrgAdmin` without holding a membership, and an assignment with no
owner is not a thing that can be written. As everywhere, the organization comes from the
gateway-validated `X-Organization-Id` header via `ITenantContext`.

**No learner-facing routes yet.** The manager's own screen (`GET /assignments`, "the active assignment
sits at the top until done") is 40.23, together with the audience resolution it depends on.

**Progress moves on events, not on requests (40.22).** `AssignmentProgressDto.status` is written by
`AssignmentThresholdConsumer`, which listens to `dialog.evaluated` and `exercise.completed` and
re-judges that person's open assignments. Nothing about it is synchronous with a learner's submit, so
a progress row updates a moment after the work rather than in the same response —
`POST /exercises/:id/submit` returns exactly what it always did. The four statuses:

| `status` | Means |
|---|---|
| `not_started` | issued to this person, no work recorded since it was issued |
| `in_progress` | started, and the work the rule measures is not finished yet |
| `failed_threshold` | the work **is** finished and the result is under the bar — "started, tried 4 times, did not reach it" |
| `completed` | the bar was met. Terminal: a later weaker attempt is practice, not a demotion, so `bestScore` and `completedAt` stand |

`attemptCount` and `bestScore` are **recomputed** from the recorded attempts on every evaluation, never
incremented, so reprocessing an event cannot inflate them.

### Content overrides and the staleness queue (Phase 40.18)

Copy-on-write: an organization customizes the shared library one row at a time instead of forking it.
Design: [TENANCY/CONTENT_MODEL.md](TENANCY/CONTENT_MODEL.md) §1 and §2.6.

`{kind}` is one of `lessons`, `techniques`, `reference-materials` — in the path, never in a body, so
an action cannot be aimed at a row of a different family by editing a payload.

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/content/overrides?staleOnly=false | — | `ContentOverrideDto[]` |
| GET | /admin/content/overrides/:kind/:overrideId | — | `ContentOverrideReviewDto` |
| POST | /admin/content/overrides/:kind/:baseId | — | `ContentOverrideDto` (409 if the row is already somebody's copy; 400 if the caller has no organization) |
| POST | /admin/content/overrides/:kind/:overrideId/accept-base | — | 204 |
| POST | /admin/content/overrides/:kind/:overrideId/keep-override | — | 204 |

`ContentOverrideDto`: `{kind, overrideId, baseId, title, isStale, forkedFrom, baseCurrent}`
`ContentOverrideReviewDto`: `{summary, override, baseAtFork, baseCurrent}` — three whole documents.

**`POST /admin/content/overrides/:kind/:baseId` is the only route in the platform that creates a
copy of anything**, and it is reachable only from a person pressing "edit". Nothing runs at
onboarding: an organization handed a private fork of the library on day one stops receiving every
later improvement to it, permanently, and nobody notices until the content roadmap has stopped
existing. It is idempotent — pressing it twice returns the existing copy untouched — and it refuses a
row that is already an organization's own copy (409). For a lesson it clones the lesson row and every
exercise in it and opens the draft version, so the administrator lands in something editable.

**`isStale` is computed on every read, not stored.** For a lesson, `forkedFrom` and `baseCurrent` are
`LessonVersion` ids and staleness is "they differ, and the base has something published". For a
technique or a reference material they are content hashes, because neither family has a version
table. A null `forkedFrom` counts as stale — "unknown base, needs review" is a state 40.15 left
expressible on purpose.

**The review payload contains no diff, by design.** `baseAtFork` is populated only for lessons (40.15
froze the snapshot it points at); for the other two kinds the base's previous text was overwritten in
place and nothing stored it, so the field is null and the screen compares the override against the
base's current text. Nothing is merged anywhere: a lesson is prose and grading criteria, and a
three-way merge of those produces text that reads as if a person wrote it and then scores a real
salesperson against a rule nobody chose.

**Three actions, and the third one is elsewhere.** `accept-base` archives the override so read
resolution stops shadowing the global row — archived, not deleted, because progress rows point at it
without a foreign key. `keep-override` re-points the fork marker at the base as it stands now and
touches no content: it records "we looked, ours still stands". **Edit** is the ordinary authoring
routes (`PUT /admin/lessons/:id`, the `/admin/exercises` routes, `PUT /admin/techniques/:id`,
`PUT /admin/reference/:id`, and `POST /admin/lessons/:id/versions/publish`), which 40.18 opened to
organization administrators for exactly this purpose; publishing a new override version re-points the
fork marker as a side effect, so editing clears the queue entry on its own.

**Authorization: `RequireOrgAdmin`, and the organization comes from `ITenantContext`.** A caller with
no organization at all (platform staff, or a request that reached the service without the gateway
header) gets 400 on create and an empty queue on read — there is nobody for a copy to belong to.

### Authoring the library vs authoring an override (Phase 40.18)

`AdminLessonsController`, `AdminExercisesController`, `AdminTechniquesController` and
`AdminReferenceController` were `RequirePlatformAdmin` until 40.18 and are now `RequireOrgAdmin`
**plus a per-row ownership check**:

- a row with an `OrganizationId` belongs to that organization, and RLS has already proved the caller
  is inside it → an organization administrator may write it;
- a row with a null `OrganizationId` is the global library → platform administrator rights required
  (403 otherwise);
- **creating** content from nothing (`POST /admin/topics/:iconicName/lessons`,
  `POST /admin/techniques`, `POST /admin/techniques/import`, `POST /admin/skills/:skillId/reference`)
  stays platform-only. An organization customizes what exists; originating an original curriculum is
  40.19/40.20's question.

The check is in application code and not in row-level security, and that is worth stating plainly
because it looks like a gap: the content RLS policy is `OrganizationId IS NULL OR = current` in the
`WITH CHECK` clause as well as the `USING` clause, since a customer must be able to read the shared
library. Read as a write rule, that says any organization may write a row with a null owner — that
is, edit every other customer's curriculum. The database cannot tell those two cases apart, because
"global" is a null and not a tenant.

One consequence for existing callers: creating an exercise under a lesson now stamps the exercise
with the lesson's organization. Before 40.18 it left it null, which was correct while every lesson
was global and would have put an override's exercises into the shared library the moment one existed.

### Exercises
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/lessons/:lessonId/exercises | — | `AdminExerciseDto[]` |
| POST | /admin/lessons/:lessonId/exercises | `{type, orderInLesson, content: <jsonb>, customAiPrompt?}` | `AdminExerciseDto` (400 if content invalid per type) |
| POST | /admin/lessons/:lessonId/exercises/import | `[{type, orderInLesson, content, customAiPrompt?}, …]` (array) | `ExercisesImportResultDto` (per-item validation; bad items skipped, reported in errors) |
| PUT | /admin/exercises/:id | same | `AdminExerciseDto` (400 if content invalid per type) |
| DELETE | /admin/exercises/:id | — | 204 |

**Content validation:** The `content` field is validated server-side per exercise type. Single create/update return 400 with joined error messages on invalid content. Import validates each exercise; bad ones are skipped and reported in the `errors` array with per-item messages. See [NEW_EXERCISE_TYPES.md](NEW_EXERCISE_TYPES.md) for per-type content schema.

`ExercisesImportResultDto`: `{exercisesCreated, exercisesUpdated, errors[]}`. Bulk upsert by `orderInLesson` within the lesson; empty array → 400, unknown lesson → 404. The admin exercises page exports the lesson's exercises in exactly this array shape (re-importable).

`AdminExerciseDto`: `{id, lessonId, type, orderInLesson, content, customAiPrompt}`

### Exercise Type Prompts
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/exercise-type-prompts | — | `ExerciseTypePromptDto[]` |
| GET | /admin/exercise-type-prompts/:exerciseType | — | `ExerciseTypePromptDto` |
| PUT | /admin/exercise-type-prompts/:exerciseType | `{systemPrompt}` | `ExerciseTypePromptDto` |

`ExerciseTypePromptDto`: `{id, exerciseType, systemPrompt, updatedAt}`

### Progress & Recognition (Progress Points)
All progress-point economy knobs are DB-driven and admin-editable (no hardcoded constants).

> **Microservices (Phase 7):** `/admin/gamification/*` is served by the
> **[gamification-service](GAMIFICATION_SERVICE.md)** through the gateway — shapes
> unchanged. On `PUT /admin/gamification/settings` the service additionally emits a
> `gamification.dialog-weights.updated` Kafka event so the ai-service refreshes its
> cached dialog scoring weights (replacing the old in-process read).

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/gamification/settings | — | `GamificationSettingsDto` |
| PUT | /admin/gamification/settings | `UpdateGamificationSettingsRequestDto` | `GamificationSettingsDto` |
| GET | /admin/gamification/exercise-rewards | — | `ExerciseTypeRewardDto[]` |
| PUT | /admin/gamification/exercise-rewards/:exerciseType | `{baseXpReward}` | `ExerciseTypeRewardDto` (upsert) |
| GET | /admin/gamification/streak-milestones | — | `StreakMilestoneDto[]` |
| POST | /admin/gamification/streak-milestones | `{dayCount, xpReward}` | `StreakMilestoneDto` (400 on duplicate `dayCount`) |
| PUT | /admin/gamification/streak-milestones/:id | `{dayCount, xpReward}` | `StreakMilestoneDto` |
| DELETE | /admin/gamification/streak-milestones/:id | — | 204 |

`GamificationSettingsDto` / `UpdateGamificationSettingsRequestDto`: `{dailyXpGoal, weeklyXpGoal, dialogXpMultiplier, dialogWeightConfidence, dialogWeightStructure, dialogWeightObjection, dialogWeightGoal}`
- `dailyXpGoal`/`weeklyXpGoal` must be positive; `dialogXpMultiplier` must be positive; criterion weights are non-negative and must sum to > 0.
- **Dialog progress points**: the AI scores a completed dialog on four criteria, each capped at its weight (raw score range `0..Σweights`). Earned progress points = `round(rawScore × dialogXpMultiplier)`. The criterion maximums are injected into the feedback prompt, so editing weights re-shapes how the AI distributes points.
- **Exercise progress points**: `baseXpReward` per exercise type is awarded on a correct/passed answer (historic flat value 10; seeded for all 10 types). Unknown/unseeded types fall back to 10.
- **Activity consistency milestones**: a one-off bonus when the daily activity streak first reaches `dayCount`. When the table is non-empty it is authoritative; when empty the historic ladder (7→50, 30→200) applies.

`ExerciseTypeRewardDto`: `{id, exerciseType, baseXpReward}`  
`StreakMilestoneDto`: `{id, dayCount, xpReward}`

### Reference Materials
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/reference | — | `AdminReferenceMaterialDto[]` (all, with optional `?skillId=&category=&search=`) |
| GET | /admin/reference/categories | — | `string[]` |
| GET | /admin/skills/:skillId/reference | — | `AdminReferenceMaterialDto[]` |
| POST | /admin/skills/:skillId/reference | `{title, markdownContent, sortOrder, category?, tags?}` | `AdminReferenceMaterialDto` |
| PUT | /admin/reference/:id | `{title, markdownContent, sortOrder, category?, tags?}` | `AdminReferenceMaterialDto` |
| DELETE | /admin/reference/:id | — | 204 |

`AdminReferenceMaterialDto`: `{id, skillId, skillTitle, skillSlug, title, markdownContent, sortOrder, category, tags: string[]}`

### Techniques
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/techniques | — (`?skill=&search=`) | `AdminTechniqueDto[]` |
| GET | /admin/techniques/:id | — | `AdminTechniqueDto` |
| POST | /admin/techniques | `AdminTechniqueWriteRequestDto` | `AdminTechniqueDto` (409 on slug conflict, 400 on unknown `primarySkillId` or out-of-range `difficulty`) |
| PUT | /admin/techniques/:id | `AdminTechniqueWriteRequestDto` | `AdminTechniqueDto` (additional skills + coach are synced to the payload) |
| DELETE | /admin/techniques/:id | — | 204 |
| POST | /admin/techniques/import | `AdminTechniqueWriteRequestDto[]` | `AdminTechniqueImportResultDto` — upserts by `slug` |
| GET | /admin/techniques/export | — | `AdminTechniqueWriteRequestDto[]` — all techniques, re-importable verbatim |

`skill` query param filters by `Skills.IconicName` (same convention as the public route).
`GET /admin/techniques/export` returns every technique (ignores `skill`/`search` filters) shaped exactly like the `import` request body, so an export file feeds straight back into `POST /admin/techniques/import`. UI: "Export JSON" button on `/admin/techniques`.

On update and on re-import the child rows (`TechniqueSkills`, `TechniqueCoaches`) are **synced in place**: links missing from the payload are deleted, new ones inserted, and an existing coach row is updated rather than replaced. Deleting and re-inserting them inside one `SaveChanges` used to fail with an EF concurrency error (`expected to affect 1 row(s), but actually affected 0 row(s)`) for every technique that had a coach. `AdminTechniqueImportResultDto` counters are incremented only after the row is persisted, so a failed item is never counted as both updated and failed.

`dialog` and `case` are deserialized strictly, and a shape mismatch is swallowed — the block simply disappears from the technique. `dialog[].annotations` must be `{label, tone?}` objects (not bare strings) and `case.metrics` must be an object (`{"Reply rate": "+38%"}`), not an array.

`AdminTechniqueDto`: `{id, slug, name, summary, body, tags: string[], primarySkillId?, primarySkillIconicName?, primarySkillTitle?, additionalSkillIds: Guid[], difficulty, difficultyName, sortOrder, createdAt, updatedAt, dialog?: JsonNode, case?: JsonNode, coach?: AdminTechniqueCoachDto}`

`AdminTechniqueCoachDto`: `{avatarSeed, name, role, quote, challenges?: JsonNode}`

`AdminTechniqueWriteRequestDto`: same shape minus `id`/timestamps and server-derived fields. `dialog`, `case`, and `coach.challenges` accept any JSON value — the server persists them to the `DialogJson` / `CaseJson` / `ChallengesJson` columns verbatim. `difficulty` must be 1..4.

`AdminTechniqueImportResultDto`: `{createdCount, updatedCount, failedCount, errors: string[]}` — import upserts each entry by `slug`, validates it, and rolls through the list, returning per-slug errors instead of aborting the whole batch.

### Leagues / Team Progress
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/leagues | — (`?weekStart=YYYY-MM-DD&tier=gold`) | `AdminLeagueListItemDto[]` |
| GET | /admin/leagues/weeks | — | `string[]` (distinct week start dates, desc) |
| GET | /admin/leagues/:id | — | `AdminLeagueDetailDto` |
| POST | /admin/leagues/close-current | — | 204 — manually runs the weekly closure job |
| POST | /admin/leagues/:id/resync | — | `AdminLeagueDetailDto` — recomputes weekly XP from `UserXpRecords` |
| PUT | /admin/leagues/memberships/:membershipId/tier | `{tier}` | `AdminLeagueDetailDto` of the target league (same week; created if missing) |
| PUT | /admin/leagues/memberships/:membershipId/xp | `{delta}` (non-zero int, may be negative) | `AdminLeagueDetailDto` |
| DELETE | /admin/leagues/memberships/:membershipId | — | 204 |
| GET | /admin/leagues/settings | — | `LeagueSettingsDto` (initializes the period on first access) |
| PUT | /admin/leagues/settings | `UpdateLeagueSettingsRequestDto` | `LeagueSettingsDto` (400 if values non-positive, zones exceed max, or period length ≤ 0) |
| GET | /admin/leagues/tiers | — | `AdminLeagueTierDto[]` (ordered by `order`) |
| POST | /admin/leagues/tiers | `{key, name, color, order}` | `AdminLeagueTierDto` (400 on blank fields or duplicate key) |
| PUT | /admin/leagues/tiers/:id | `{name, color, order}` | `AdminLeagueTierDto` (key is immutable; 404 if missing) |
| DELETE | /admin/leagues/tiers/:id | — | 204 (400 if it is the last tier or has existing leagues) |

`AdminLeagueListItemDto`: `{id, tier, weekStartDate, weekEndDate, memberCount}`
`AdminLeagueDetailDto`: `{id, tier, weekStartDate, weekEndDate, members: AdminLeagueMemberDto[]}`
`AdminLeagueMemberDto`: `{membershipId, userId, displayName, email, weeklyXpAmount, rank, promotionOutcome}`
`AdminLeagueTierDto`: `{id, key, name, color, order}`
`LeagueSettingsDto`: `{maximumLeagueParticipantCount, promotionZoneSize, demotionZoneSize, currentPeriodEndsAt, periodLengthDays}`
`UpdateLeagueSettingsRequestDto`: same as above but `currentPeriodEndsAt`/`periodLengthDays` are optional — when omitted the period is left unchanged, so zones can be edited alone. Setting `currentPeriodEndsAt` also realigns the active period's leagues' `WeekEndDate` so the progress-point window tracks the new end.

Progress-point adjustment is recorded as a `UserXpRecords` row with `Source = "admin_correction"` and `EarnedAt` stamped at the team progress period's week start — a direct `WeeklyXpAmount` write would be erased by the next progress-point sync, while a correction record survives every re-sync and stays auditable.

### Users (read: `RequirePlatformAdmin`; every mutation: `RequireSuperAdmin`)

> Owned by the extracted **[identity-service](IDENTITY_SERVICE.md)** (it owns
> Users/Roles). The gateway flips `/admin/users/*` to the identity cluster; paths and
> shapes are unchanged. The `AdminUserDetailDto` activity stats (activity-consistency/progress-points/skills/score)
> are owned by gamification/learning, so identity returns them as `0` for now — same
> caveat as `GET /profile`. Lists/manages users platform-wide (not scoped to one
> organization), so the controller is Sellevate-staff-only throughout: reading is
> `RequirePlatformAdmin`, every mutation is `RequireSuperAdmin`. Role changes move between the
> three platform roles (`User`/`Admin`/`SuperAdmin`) — organization roles are a different axis and
> are never assignable here.

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/users | — | `AdminUserDto[]` |
| GET | /admin/users/:id | — | `AdminUserDetailDto` (404 if missing) |
| PUT | /admin/users/:id | `{displayName}` | `AdminUserDto` (400 if name not 2–50 chars, 404 if missing) — moderation rename |
| DELETE | /admin/users/:id/avatar | — | 204 (404 if missing) — moderation: reset uploaded photo to default |
| PUT | /admin/users/:id/role | `{role: "User"\|"Admin"\|"SuperAdmin"}` | `AdminUserDto` (SuperAdmin only) |

`AdminUserDto`: `{id, email, displayName, role, createdAt, isEmailVerified, authProvider ("Google"|"Password"), hasCustomAvatar, avatarUrl}`
`AdminUserDetailDto`: `AdminUserDto` + `{currentStreakDayCount, longestStreakDayCount, totalXpAmount, completedSkillCount, totalSkillCount, averageExerciseScore, persona}`

Reading the roster and a user's detail is open to both platform staff roles. Renaming, avatar moderation and role changes all mutate a user and are `RequireSuperAdmin`-only, so a platform `Admin` sees the modal read-only. `DELETE /admin/users/:id/avatar` reuses the avatar reset flow (deletes the uploaded S3 object and falls back to the default avatar).

### Seeder
| Method | Path | Body | Response |
|---|---|---|---|
| POST | /admin/seeder/skills | `multipart/form-data; file=<JSON>, target=global` | `SkillsImportResultDto` |
| POST | /admin/seeder/topics | `multipart/form-data; file=<JSON>, target=global` | `TopicsImportResultDto` |
| POST | /admin/seeder/lessons | `multipart/form-data; file=<JSON>, target=global` | `LessonsImportResultDto` |
| POST | /admin/seeder/bundle | `multipart/form-data; file=<JSON>, target=global` (≤20 MB) | `BundleImportResultDto` |
| GET | /admin/seeder/skills/export | — | `SkillExportDto[]` — re-importable via POST /admin/seeder/skills |
| GET | /admin/seeder/topics/export | — | `TopicExportDto[]` — re-importable via POST /admin/seeder/topics |
| GET | /admin/seeder/lessons/export | — | `LessonExportDto[]` (with nested exercises) — re-importable via POST /admin/seeder/lessons |
| GET | /admin/seeder/bundle/export | — | `BundleExportDto` (`{ skills: [...] }`) — re-importable via POST /admin/seeder/bundle |

**`target` is required on all four imports and must be the literal `global`** (Phase 40.19). Anything
else — a missing field, a different word, an organization id — is `400`. It states which library is
being written, and it is **not** an organization id and must never become one: the tenant is read from
`ITenantContext`, never from a body (docs/TENANCY/TENANCY.md §1.3, enforced by
`scripts/tenancy-boundary-lint.py`). Since 40.19 every read inside these endpoints is also narrowed to
`OrganizationId IS NULL`, which fixed a silent bug — the tenancy query filter admits "global or mine",
and lessons upsert on `(topicId, title)`, so a re-run could overwrite a customer's override with the
base text. See [SEEDER.md](SEEDER.md) §0.

Each `GET …/export` returns the full **global** content set shaped exactly like the matching import
body, so an export file feeds straight back into its import (exercise `content` is emitted as a JSON
object, not a string). They take no `target`: there is only one thing to export, and since 40.19 they
are narrowed to `OrganizationId IS NULL` for the mirror of the reason above — an export carrying one
customer's overrides would re-import as if those were everybody's content. Ordered by the relevant
order field; skill/topic icon names are resolved from ids. UI: "Export JSON" buttons on
`/admin/skills`, `/admin/topics`, `/admin/lessons`; "Export tree" on `/admin/import`.

Seeded lesson titles and exercise content may carry `{{organization.*}}` placeholders. They are stored
verbatim and resolved per organization at render time, which is why the same seeded bundle produces the
same `ContentHash` for every customer — see [CONTENT_PARAMETERIZATION.md](CONTENT_PARAMETERIZATION.md).

**Skills JSON:** `[{ iconicName, title, description?, orderInTree, stage? }]`
**Topics JSON:** `[{ skillIconicName, iconicName, title, orderInSkill }]`
**Lessons JSON:** `[{ topicIconicName, title, orderInTopic, exercises: [{ type, orderInLesson, content, customAiPrompt? }] }]`
**Bundle JSON:** `{ skills: [{ iconicName, title, description?, orderInTree, stage?, topics: [{ iconicName, title, orderInSkill, lessons: [{ title, orderInTopic, exercises: [{ type, orderInLesson, content, customAiPrompt? }] }] }] }] }` (a bare skills array is also accepted). Whole skill tree in one file; idempotent upsert (skills/topics by `iconicName`, lessons by `(topicId, title)`, exercises by `(lessonId, orderInLesson)`); per-type content validation; invalid exercises are skipped into `errors[]`. UI: `/admin/import`.
`BundleImportResultDto = { skillsCreated, skillsUpdated, topicsCreated, topicsUpdated, lessonsCreated, lessonsUpdated, exercisesCreated, exercisesUpdated, errors[] }`

`SkillsImportResultDto`: `{skillsCreated, skillsUpdated, errors: string[]}`
`TopicsImportResultDto`: `{topicsCreated, topicsUpdated, errors: string[]}`
`LessonsImportResultDto`: `{lessonsCreated, lessonsUpdated, exercisesCreated, exercisesUpdated, errors: string[]}`

Max file size: 10 MB.


---

## Transcription

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /transcription/transcribe | `multipart/form-data; file=<audio>` | `TranscriptionResponseDto` |

`TranscriptionResponseDto`: `{text: string, language: string|null}`

**Supported formats:** mp3, mp4, m4a, mpeg, mpga, wav, webm, ogg  
**Max file size:** configurable via `Whisper:MaxFileSizeMb` (default 25 MB)  
**Model:** configurable via `Whisper:Model` (default `whisper-1`)  
**Language:** configurable via `Whisper:Language` (default `ru`)

Errors:
- `400` — file missing, too large, or unsupported format
- `502` — Whisper API returned an error

**Reused by Companies (Phase 39.15):** the call-log form's voice memo recorder
(`features/companies/hooks/use-voice-memo-recorder.ts`) posts the recorded `webm` blob to this
same endpoint via the existing `useTranscribeAudio` hook — no new endpoint, no company-service
involvement.

---

## Dialog (AI-powered conversation practice)

> **Microservices (Phase 6):** `/dialog/*`, `/transcription/*`, `/admin/dialog/*` and
> `/admin/voice/*` are served by the extracted **[ai-service](AI_SERVICE.md)** through the
> YARP gateway — paths unchanged. On `/complete` the service now emits a `dialog.evaluated`
> Kafka event (the gamification-service grants the progress points) instead of writing `UserXpRecords` directly.
> Internal-only (not via the gateway): `POST /ai/evaluate` `{exerciseType, systemPrompt?,
> exerciseContent, userAnswer}` → `{isCorrect, score, explanation?, aiFeedback?}`, called
> by Learning to grade AI exercise types. `DialogBundleDto.skillTitle` is now empty
> (`Skills` are owned by Learning; only `skillId` is kept).
> Internal-only (Phase 39.12): `POST /ai/companies/briefing`
> `{companyDescription, goal?, recentCalls: [{contactName?, subject, outcome, occurredAt}],
> feedbackSummaries: string[]}` → `{content, generatedAt}` (`content` is markdown), called by
> company-service to generate the pre-call cheat sheet. Stateless — reads nothing from Mongo or
> Postgres itself, just composes a Russian system prompt from the request body and asks the
> configured LLM. `503` if OpenAI isn't configured or the provider call fails.
> Internal-only (Phase 39.13): `POST /ai/companies/parse-log` `{rawText}` (max 16000 chars, `400`
> if exceeded) → `{contactName?, subject, outcome, occurredAt?}`, called by company-service to
> extract a structured call-log draft from pasted notes/transcript. Stateless — composes a Russian
> system prompt instructing the model to return strict JSON, then parses it. `subject`/`outcome`
> default to an empty string if the model omits them; `contactName`/`occurredAt` are `null` if not
> mentioned or unparseable (never fails the whole parse just for a missing date). `503` if OpenAI
> isn't configured, the provider call fails, or the AI response isn't valid JSON. Both internal
> endpoints share `InternalServiceAuthFilter` (`X-Internal-Service-Secret` header, checked against
> `InternalAuth:ServiceSecret`; left open when that config key is unset, i.e. dev/single-service
> mode).
> Internal-only (Phase 39.14): `POST /ai/companies/persona` `{companyDescription (max 16000 chars,
> `400` if exceeded), contactName?, contactPosition?, difficulty: "Easy"|"Medium"|"Hard"}` →
> `{name, position, personality}`, called by company-service to invent a buyer persona for a
> practice call. Stateless — composes a Russian system prompt (difficulty tunes how
> tough/skeptical the persona is; `contactName`/`contactPosition` are an optional seed, not copied
> verbatim) instructing strict JSON output, then parses it. `503` if OpenAI isn't configured, the
> provider call fails, or any of `name`/`position`/`personality` is missing/empty in the response
> (unlike parse-log, personas have no valid "N/A" field — an incomplete persona is treated as a
> parse failure). Shares `InternalServiceAuthFilter` with the other internal endpoints.
> Internal-only (Phase 39.16): `POST /ai/companies/readiness` `{userId, goal?, sessionIds: string[]}`
> (max 50 session ids, `400` if exceeded) → `{score (0-100), strengths: string[], gaps: string[],
> recommendation}`, called by company-service to score a user's readiness for a real call. Unlike
> the other internal endpoints, this one **does** read from ai-service's own Mongo store: for each
> `sessionIds` entry it loads the `DialogSession` **scoped to `userId`** (via
> `IDialogService.GetSessionForUserAsync`, so ai-service independently verifies the caller-supplied
> ids belong to that user — defense in depth beyond `InternalServiceAuthFilter` + company-service's
> ownership check) and pulls `Feedback.Summary`, skipping sessions with no feedback yet (abandoned/incomplete calls).
> If zero sessions have usable feedback, returns **`204 No Content`** without calling the LLM — the
> "no data yet" signal company-service turns into its own `204`. Otherwise composes a Russian
> system prompt from the collected summaries (+ optional `goal`) instructing strict JSON output;
> `score` is clamped to `[0, 100]` after parsing (tolerates a numeric string too). `503` if OpenAI
> isn't configured, the provider call fails, or the response is unparseable/missing
> `score`/`recommendation` (`strengths`/`gaps` default to `[]` if omitted). Shares
> `InternalServiceAuthFilter` with the other internal endpoints.

### Public endpoints

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /dialog/bundles | — | `DialogBundleDto[]` (hidden bundles excluded) |
| GET | /dialog/bundles/:bundleId/modes | — | `DialogModeDto[]` |
| GET | /dialog/company-call-mode | — | `{bundleId, modeId}` — IDs of the seeded company-call mode; `404` if not yet seeded |
| GET | /dialog/sessions | — | `DialogSessionSummaryDto[]` (user's history) |
| GET | /dialog/custom-scenario-mode | — | `{bundleId, modeId}` — IDs of the seeded custom-scenario mode; `404` if not yet seeded |
| POST | /dialog/scenario/validate | `{scenario: string}` | `{isValid: bool, rejectionReason: string\|null}`; `503` when the check itself is unavailable |
| POST | /dialog/sessions | `{bundleId, modeId, companyContext?, customScenario?}` | `DialogSessionDto`; `422` when `customScenario` is not about sales |
| GET | /dialog/sessions/:sessionId | — | `DialogSessionDto` |
| POST | /dialog/sessions/:sessionId/messages | `{content: string}` | `DialogMessageDto` |
| POST | /dialog/sessions/:sessionId/complete | — | `{summary, content, generatedAt, xpEarned}`; `204 No Content` when the session has no user messages (marked `abandoned`, no feedback generated) |

**Company context:** `companyContext` is optional on `POST /dialog/sessions`. Shape: `{companyName: string (required, ≤200), companyDescription: string (required, ≤8000), callGoal?: string (≤500), personaName?: string (≤200), personaPosition?: string (≤200), personaPersonality?: string (≤4000), personaDifficulty?: string (≤16, "Easy"|"Medium"|"Hard")}`. When present, the service appends a structured block to the mode's `ChatSystemPrompt` and `FeedbackSystemPrompt` at runtime (not stored in PostgreSQL — only persisted in the MongoDB `DialogSession` document as `companyCallContext`). The `GET /dialog/company-call-mode` endpoint returns the fixed `{bundleId, modeId}` that callers must pass when starting a company-practice session. **Constraint:** `companyContext` may only be used with the seeded company-call mode (key `company-call`); passing it with any other mode returns `400 Bad Request`.


**Custom scenario:** `customScenario` is an optional free-text role-play brief on `POST /dialog/sessions` (20–1500 characters after trimming). It is spliced into the mode's prompts at runtime the same way `companyContext` is, fenced in BEGIN/END markers and labelled as data, and persisted only on the MongoDB `DialogSession` document as `customScenarioContext`. **Constraints:** it may only be used with the seeded custom-scenario mode (key `custom-scenario`, IDs from `GET /dialog/custom-scenario-mode`), that mode *requires* it, and it must pass the sales-relevance check — a rejected scenario returns `422 Unprocessable Entity` with the reason in `message`, and no session is created.

`POST /dialog/scenario/validate` runs the same check on its own so the compose dialog can reject off-topic text before starting anything. It is advisory: session start re-validates server-side, so the client's verdict is never trusted. Verdicts are cached in Redis under a SHA-256 of the whitespace/case-normalized scenario (approvals 30d, rejections 7d), which makes the re-check a cache hit. A check that cannot produce a verdict at all is never cached and never treated as approval — both endpoints answer `503`.
**Persona role-play (Phase 39.14):** the four `persona*` fields are all optional/nullable — a call may have no persona (e.g. `personaName` absent or blank), in which case the prompt output is byte-for-byte identical to pre-39.14 behavior. When `personaName` is non-blank, `CompanyContextPromptBuilder` appends a second block to the chat system prompt instructing the model to **role-play as** that persona (name, position, personality, and a difficulty-derived toughness description), and a related but distinctly-worded "grade with this persona in mind" block to the feedback system prompt. See [AI_DIALOG.md](AI_DIALOG.md) for the full prompt shapes.

**DTOs:**
- `DialogBundleDto`: `{id, skillId, skillSlug, skillTitle, title, description, iconEmoji, sortOrder, isActive}`
- `DialogModeDto`: `{id, bundleId, key, title, description, sortOrder, isActive}`
- `DialogSessionDto`: `{id, bundleId, modeId, status, messages[], feedback?, xpEarned, createdAt, completedAt}`
- `DialogSessionSummaryDto`: `{id, bundleId, modeId, modeTitle, bundleTitle, status, messageCount, xpEarned, createdAt, completedAt}`
- `DialogMessageDto`: `{role: "assistant"|"user", content, timestamp, isStopSignal}`
- `DialogFeedbackDto`: `{content, generatedAt, xpEarned}`

**Session status:** `active` | `completed` | `abandoned`

**Stop signal:** AI adds `[DIALOG_END]` tag when conversation should end. Tag is parsed and `isStopSignal: true` set on message.

**Progress-point reward:** AI generates progress points (0-100) via the `[XP:number]` tag in feedback. Saved to `UserXpRecords` with source `"dialog"`.

**Graceful degradation:**
- If `OpenAI:ApiKey` is not configured, `GET /dialog/bundles` returns `[]`
- Session endpoints return `503 Service Unavailable` if OpenAI not configured

### Admin endpoints (`RequirePlatformAdmin` — revised 2026-08-16)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/dialog/bundles | — | `DialogBundleDto[]` |
| GET | /admin/dialog/bundles/:bundleId | — | `DialogBundleDto` |
| POST | /admin/dialog/bundles | `CreateBundleRequestDto` | `DialogBundleDto` |
| PUT | /admin/dialog/bundles/:bundleId | `UpdateBundleRequestDto` | `DialogBundleDto` |
| DELETE | /admin/dialog/bundles/:bundleId | — | 204 |
| GET | /admin/dialog/bundles/:bundleId/modes | — | `AdminDialogModeDto[]` |
| GET | /admin/dialog/modes/:modeId | — | `AdminDialogModeDto` |
| POST | /admin/dialog/bundles/:bundleId/modes | `CreateModeRequestDto` | `AdminDialogModeDto` |
| PUT | /admin/dialog/modes/:modeId | `UpdateModeRequestDto` | `AdminDialogModeDto` |
| DELETE | /admin/dialog/modes/:modeId | — | 204 |
| POST | /admin/dialog/import | `multipart/form-data; file=<JSON>` (≤20 MB) | `DialogImportResultDto` |
| GET | /admin/dialog/export | — | `DialogExportDto` — all bundles with nested modes, re-importable verbatim |

#### Prompt overrides (Phase 40.18)

Per-organization prompt customization, the ai-service half of copy-on-write. Design:
[TENANCY/CONTENT_MODEL.md](TENANCY/CONTENT_MODEL.md) §2.6 and §4.

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/dialog/overrides/modes?staleOnly=false | — | `DialogModeOverrideDto[]` |
| GET | /admin/dialog/overrides/modes/:overrideId | — | `DialogModeOverrideReviewDto` |
| POST | /admin/dialog/overrides/modes/:baseModeId | — | `DialogModeOverrideDto` (409 if already a copy, or if the mode is in a seeded hidden bundle) |
| PUT | /admin/dialog/overrides/modes/:overrideId | `UpdateModeRequestDto` | `AdminDialogModeDto` (404 if not this organization's override) |
| POST | /admin/dialog/overrides/modes/:overrideId/accept-base | — | 204 |
| POST | /admin/dialog/overrides/modes/:overrideId/keep-override | — | 204 |

`DialogModeOverrideDto`: `{overrideId, baseModeId, bundleId, key, title, isStale, forkedFromHash, baseCurrentHash}`
`DialogModeOverrideReviewDto`: `{summary, overrideChatSystemPrompt, overrideFeedbackSystemPrompt, baseChatSystemPrompt, baseFeedbackSystemPrompt}`

`RequireOrgAdmin`; the organization comes from `ITenantContext`, never from the route or the body.

The override keeps its parent's `bundleId` and `key`, so the customized prompt appears in the same
bundle in the same position — the 40.11 unique indexes already allow it, because the composite one is
filtered to non-global rows. `GET /dialog/bundles/:id/modes` resolves: an organization with an
override sees its own prompt and not the base, and an organization without one sees the base.

**`DialogBundle` is not override-able**, and this is the one place the implementation is narrower
than the roadmap sentence. A bundle carries no prompt at all — title, description, emoji, sort order
— while a copied bundle would be an empty folder needing a second resolution layer for "which modes
are in it". An organization that wants its own bundle creates one, which 40.11 already allows.

**The seeded hidden modes (`company-call`, `custom-scenario`) cannot be overridden — 409, not a
silent no-op.** Their prompts are half code: the service completes them at run time from placeholders
it supplies, and a per-organization copy would drift away from the code that feeds it until it
quietly stopped matching.

`PUT /admin/dialog/overrides/modes/:overrideId` is the "edit" action, and it lives on this controller
rather than as a widened route on `AdminDialogController` for a concrete reason: stacking a second
`[Authorize]` on one action of a platform-only controller **ANDs** the two policies rather than ORing
them, so the code would read as though organization administrators were admitted and they would still
be refused. A separate controller makes the weaker gate impossible to misread.
`AdminDialogController` stays platform-only in full.

`key` and `bundleId` are not editable on an override: they are its link to the row it shadows, and
changing the key would make the copy stop resolving over its base and start appearing beside it —
the one outcome copy-on-write exists to prevent.

**Dialog export JSON:** `GET /admin/dialog/export` returns `{ bundles: [{ skillId, title, description, iconEmoji, sortOrder, isActive, modes: [{ key, title, description, chatSystemPrompt, feedbackSystemPrompt, sortOrder, isActive, voiceEnabled, voiceId }] }] }` — exactly the shape `POST /admin/dialog/import` accepts, so an export file re-imports verbatim. UI: "Export JSON" button on `/admin/dialog`.

**Dialog import JSON:** `{ bundles: [{ skillId, title, description?, iconEmoji?, sortOrder?, isActive?, modes: [{ key, title, description?, chatSystemPrompt?, feedbackSystemPrompt?, sortOrder?, isActive?, voiceEnabled?, voiceId? }] }] }` (a bare bundles array is also accepted). The endpoint keys bundles by `skillId` (a `Guid`) because the ai-service does not own the `Skills` table. Humans, however, paste `skillIconicName`: the `/admin/dialog` import panel resolves `skillIconicName → skillId` client-side (from `/admin/skills`) before upload, and a bundle that already has a `skillId` (e.g. a re-imported export) is left as-is — so both shapes work. Unknown `skillIconicName` is rejected client-side with nothing uploaded. Idempotent upsert: bundles by `(skillId, title)`, modes by `(bundleId, key)`; bundles with a missing/invalid `skillId` and modes with empty key/title are skipped into `errors[]`. UI: import panel on `/admin/dialog`.
`DialogImportResultDto = { bundlesCreated, bundlesUpdated, modesCreated, modesUpdated, errors[] }`

**Admin DTOs:**
- `AdminDialogModeDto`: extends `DialogModeDto` with `chatSystemPrompt, feedbackSystemPrompt`
- `CreateBundleRequestDto`: `{skillId, title, description, iconEmoji, sortOrder, isActive}`
- `UpdateBundleRequestDto`: all fields optional
- `CreateModeRequestDto`: `{key, title, description, chatSystemPrompt, feedbackSystemPrompt, sortOrder, isActive}`
- `UpdateModeRequestDto`: all fields optional

**Storage:**
- Bundles & Modes: PostgreSQL (`DialogBundles`, `DialogModes` tables, linked to `Skills`)
- Sessions: MongoDB (`dialog_sessions` collection)

**AI models:**
- Chat: `gpt-4.1-nano` (configurable via `OpenAI:ChatModel`)
- Feedback: `gpt-4.1` (configurable via `OpenAI:FeedbackModel`)

---

## Voice (Voice Roleplay)

### Public endpoints

| Method | Path | Body | Response |
|--------|------|------|----------|
| GET | /dialog/voice/config | — | `VoiceConfigDto` |
| GET | /dialog/voice/usage | — | `{dailyUsedSeconds, dailyLimitSeconds, dailyExceeded, monthlyUsedSeconds, monthlyLimitSeconds, monthlyExceeded}` |
| POST | /dialog/sessions/{sessionId}/voice/stream | `{transcript}` | `application/octet-stream` — length-prefixed frames (see below) |

`voice/stream` errors before the first frame are real status codes, not an empty 200 body:
`400` empty transcript, `401` no user, `429` voice limit reached, `503` voice not configured, and
**`409 {error}`** when the session is missing, already completed, or its mode has voice disabled —
without it the client received a 200 with zero frames and the persona simply stayed silent.

### Admin endpoints

| Method | Path | Body | Response |
|--------|------|------|----------|
| GET | /admin/voice/usage | — | `AdminVoiceUsageDto` (`RequirePlatformAdmin` — revised 2026-08-16). **Phase 40.11: scoped to the caller's organization**, not the whole installation — a platform superadmin sees another organization's numbers by impersonating into it (40.9). Response shape unchanged. |

```jsonc
// AdminVoiceUsageDto
{
  "dailyLimitSeconds": 600,
  "monthlyLimitSeconds": 7200,
  "users": [
    {
      "userId": "guid",
      "email": "user@example.com",
      "displayName": "User",
      "dailyUsedSeconds": 120,
      "monthlyUsedSeconds": 1800,
      "totalSeconds": 5400,
      "sessionCount": 12,
      "lastCallAt": "2026-06-05T10:00:00Z"
    }
  ]
}
// Sorted by monthlyUsedSeconds desc. Aggregated from MongoDB dialog sessions (voiceSeconds > 0).
```

**Voice stream frame format** (big-endian):
```
uint32 flags        // bit 0 = isFinal (sentinel, end of stream), bit 1 = isStopSignal (endCall)
uint32 textLength
byte[] text         // utf-8 sentence of the AI reply
uint32 audioLength
byte[] audio        // mp3 for that sentence
```
The final sentinel frame has empty text/audio and carries the `isStopSignal` flag.
`429` with `{period, usedSeconds, limitSeconds}` when the daily/monthly voice limit is exceeded.

> Removed (legacy, unused by frontend): `POST /dialog/sessions/{sessionId}/voice`,
> `GET /dialog/sessions/{sessionId}/voice/response`, Deepgram endpoints.

**VoiceConfigDto:**
```json
{
  "enabled": true,
  "vadSilenceMs": 600,
  "maxRecordingSeconds": 60,
  "deepgram": {
    "configured": true,
    "model": "nova-3",
    "language": "ru",
    "smartFormat": true,
    "punctuate": true
  }
}
```

**VoiceResponseDto:** `{content, isStopSignal, timestamp}`

**Voice endpoint behavior:**
- Accepts user transcript
- Generates AI response via GPT
- Synthesizes audio via ElevenLabs
- Returns audio stream (mp3)
- Saves both messages to MongoDB session

**Graceful degradation:**
- Returns 503 if Deepgram or ElevenLabs not configured
- `/dialog/voice/config` returns `enabled: false` if keys missing

### Admin voice fields

`AdminDialogModeDto` extended with:
- `voiceEnabled: boolean` — whether voice mode available for this mode
- `voiceId: string | null` — ElevenLabs voice ID override

`CreateModeRequestDto` / `UpdateModeRequestDto` accept:
- `voiceEnabled?: boolean`
- `voiceId?: string | null`

---

## Friends

> Served by the **social-service** (Phase 5) — the gateway flips `/friends/*` and
> `/chat/*` to the `social` cluster. Paths and DTO shapes are unchanged. The
> progress-list/profile/activity progress-point-and-milestone aggregate fields currently return
> `0`/empty until Gamification/Learning are extracted (see [SOCIAL_SERVICE.md](SOCIAL_SERVICE.md)).

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /friends | — | `FriendDto[]` |
| GET | /friends/requests | — | `FriendRequestDto[]` |
| POST | /friends/requests | `{addresseeId}` | 201 `{friendshipId}` |
| PUT | /friends/requests/{friendshipId}/accept | — | 204 |
| PUT | /friends/requests/{friendshipId}/decline | — | 204 |
| DELETE | /friends/requests/{friendshipId} | — | 204 (requester cancels own pending request) |
| DELETE | /friends/{friendUserId} | — | 204 |
| GET | /friends/search?query={q} | — | `UserSearchResultDto[]` |
| GET | /friends/leaderboard | — | `FriendLeaderboardEntryDto[]` |
| GET | /friends/activity | — | `FriendActivityDto[]` (returns `[]` until Gamification/Learning emit activity events) |
| GET | /friends/profile/{userId} | — | `PublicProfileDto` |

`FriendDto`: `{userId, displayName, persona?, totalXpAmount, currentStreakDayCount, achievementCount, avatarUrl}`

`FriendRequestDto`: `{friendshipId, userId, displayName, persona?, direction, createdAt}`
- `direction`: `"incoming"` | `"outgoing"`

Request lifecycle: only the **addressee** may `accept`/`decline`; only the **requester** may `DELETE /friends/requests/{friendshipId}` to cancel a still-pending request. Decline keeps a `Declined` row (so the requester can later revive it by re-sending); cancel hard-deletes the row, returning the pair to the `none` state. Both `accept`/`decline`/`cancel` return `400` if the request is no longer pending and `404` if it does not exist; `cancel` returns `400` if the caller is not the requester. No event is emitted on decline or cancel.

`PublicProfileDto`: `{userId, displayName, persona?, totalXpAmount, currentStreakDayCount, achievementCount, averageExerciseScore, friendshipStatus, avatarUrl, friendshipId?}`
- `friendshipStatus`: `"none"` | `"pending_outgoing"` | `"pending_incoming"` | `"friends"`
- `friendshipId`: the underlying friendship row id when one exists (`null` for `"none"` / self). Lets the UI cancel an outgoing request directly from the "request sent" button without first fetching `/friends/requests`.

`UserSearchResultDto`: `{userId, displayName, persona?, friendshipStatus, avatarUrl, friendshipId?}` (`friendshipId` as above)

`FriendLeaderboardEntryDto`: `{userId, displayName, totalXpAmount, rank, isCurrentUser, avatarUrl}`

`FriendActivityDto`: `{userId, displayName, activityType, description, occurredAt}`
- `activityType`: `"earned_achievement"` | `"earned_xp"` | `"completed_lesson"` | `"streak_milestone"`

---

## Chat

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /chat/conversations | — | `ChatConversationSummaryDto[]` |
| POST | /chat/conversations | `{friendUserId}` | `ChatConversationSummaryDto` |
| GET | /chat/conversations/{id}/messages?limit=50&before={msgId} | — | `ChatMessageDto[]` |
| POST | /chat/conversations/{id}/messages | `{content}` | `ChatMessageDto` |
| POST | /chat/conversations/{id}/read | — | `204 No Content` |

`POST /chat/conversations/{id}/read` records the caller's read watermark on the conversation
(`lastReadAt[userId]`) and publishes `chat.message.read`, which cancels any pending
unread-message email for that conversation (see [EMAIL_NOTIFICATIONS.md](EMAIL_NOTIFICATIONS.md)).

`ChatConversationSummaryDto`: `{conversationId, friendUserId, friendDisplayName, lastMessagePreview?, lastMessageAt?}`

`ChatMessageDto`: `{id, senderId, content, sentAt, isOwn}`

**Business rules:**
- Chat only available between accepted friends
- Creating a conversation validates active friendship
- Messages are stored in MongoDB `chat_conversations` collection
- Participant IDs are always sorted for canonical document identity

---

## Notifications

> **Served by `notification-service` (Phase 4)** through the gateway — the paths and
> contracts below are unchanged so the frontend is unaffected. Storage is Redis
> (per-user capped list + unread counter), not PostgreSQL. See
> [NOTIFICATION_SERVICE.md](NOTIFICATION_SERVICE.md).

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /notifications?limit=20&includeRead=true | — | `NotificationDto[]` |
| GET | /notifications/unread-count | — | `UnreadNotificationCountDto` |
| PUT | /notifications/{notificationId}/read | — | 204 |
| PUT | /notifications/read-all | — | 204 |

`NotificationDto`: `{id, notificationType, title, body, actionUrl?, relatedEntityId?, isRead, createdAt, readAt?}`
- `notificationType`: `"FriendRequestReceived"` | `"FriendRequestAccepted"` | `"ChatMessageReceived"` | `"AchievementUnlocked"` | `"StreakMilestone"`

`UnreadNotificationCountDto`: `{count}`

**Business rules:**
- Notifications are scoped to the authenticated recipient
- `actionUrl` is a relative frontend route (e.g. `/friends?tab=requests`, `/friends/chat/{conversationId}`, `/profile`)
- `relatedEntityId` stores source-entity id as string (friendship id, conversation id, achievement key, etc.)
- Marking a single notification as read is idempotent; already-read notifications return 204
- Retention is a **30-day Redis TTL** per inbox (replaces the monolith's `notification-cleanup` Hangfire job); inboxes are capped (default 100 per user)
- Triggers arrive as Kafka events the service consumes (`achievement.unlocked`, `streak.milestone`, `friend.request.received`, `friend.request.accepted`, `chat.message.sent`)

---

## Discuss (community forum)

> Served by the **social-service** (Phase 5) — the gateway flips `/discuss/*` and
> `/admin/discuss/*` to the `social` cluster. Paths and DTO shapes are unchanged; the
> tables move to the `social` Postgres database and photos stay on S3/MinIO
> (see [SOCIAL_SERVICE.md](SOCIAL_SERVICE.md)).

All endpoints require auth. Threads, replies and votes are PostgreSQL; votes are upvote-only
(a row's existence = upvoted), de-duplicated by a unique `(userId, targetType, targetId)` index.

### User endpoints

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /discuss/threads?sort=&search=&tag=&page=&pageSize= | — | `PagedResult<DiscussThreadSummaryDto>` |
| GET | /discuss/threads/{threadId} | — | `DiscussThreadDetailDto` (also increments view count) |
| POST | /discuss/threads | `{title, body, tags: string[]}` | `DiscussThreadDetailDto` (201) |
| POST | /discuss/threads/{threadId}/replies | `{body}` | `DiscussReplyDto` (201) |
| POST | /discuss/threads/{threadId}/upvote | — | `VoteResultDto` |
| DELETE | /discuss/threads/{threadId}/upvote | — | `VoteResultDto` |
| POST | /discuss/replies/{replyId}/upvote | — | `VoteResultDto` |
| DELETE | /discuss/replies/{replyId}/upvote | — | `VoteResultDto` |
| POST | /discuss/threads/{threadId}/accepted-reply | `{replyId}` | `DiscussThreadDetailDto` (author or admin; else 403) |
| DELETE | /discuss/threads/{threadId}/accepted-reply | — | `DiscussThreadDetailDto` (clears solved) |
| GET | /discuss/tags?curatedOnly= | — | `DiscussTagDto[]` |
| GET | /discuss/tags/popular?limit= | — | `PopularTagDto[]` |
| GET | /discuss/stats | — | `DiscussStatsDto` |

`sort`: `hot` (default; pinned first, then time-decayed score, manual `isHot` boosts) | `new` (by lastActivityAt) | `unanswered` (zero-reply only).

- `DiscussThreadSummaryDto`: `{id, title, bodyPreview, authorId, authorName, authorAvatarUrl, upvoteCount, replyCount, viewCount, isPinned, isHot, isSolved, tags: [{slug, name}], createdAt, lastActivityAt, viewerHasUpvoted}`
- `DiscussThreadDetailDto`: summary fields + `{body, acceptedReplyId, replies: DiscussReplyDto[]}`
- `DiscussReplyDto`: `{id, threadId, authorId, authorName, authorAvatarUrl, body, upvoteCount, isAccepted, createdAt, viewerHasUpvoted}`
- `DiscussTagDto`: `{id, slug, name, isCurated}`
- `PopularTagDto`: `{slug, name, threadCount}`
- `DiscussStatsDto`: `{totalThreads, totalReplies, topAuthorsOfWeek: [{authorId, authorName, authorAvatarUrl, upvotesReceived}]}` (upvotes received on the author's threads+replies in the last 7 days)
- `VoteResultDto`: `{upvoteCount, hasUpvoted}`
- `PagedResult<T>`: `{items: T[], page, pageSize, totalCount}`

Tags are hybrid: a thread's `tags` array mixes existing curated/free slugs and brand-new labels;
unknown labels are created on the fly as non-curated tags (slug = lowercased, whitespace→`-`).

### Admin endpoints (`RequirePlatformAdmin` — revised 2026-08-16)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/discuss/threads?search=&page=&pageSize= | — | `PagedResult<DiscussThreadSummaryDto>` |
| DELETE | /admin/discuss/threads/{threadId} | — | 204 (cascades replies, thread-tags, votes) |
| POST | /admin/discuss/threads/{threadId}/pin | `{isPinned}` | `DiscussThreadSummaryDto` |
| POST | /admin/discuss/threads/{threadId}/hot | `{isHot}` | `DiscussThreadSummaryDto` |
| DELETE | /admin/discuss/replies/{replyId} | — | 204 (clears accepted-reply if it pointed here, decrements count) |
| GET | /admin/discuss/tags | — | `DiscussTagDto[]` |
| POST | /admin/discuss/tags | `{name, slug?}` | `DiscussTagDto` (201; 409 on duplicate slug) |
| PUT | /admin/discuss/tags/{tagId} | `{name?, slug?}` | `DiscussTagDto` (409 on duplicate slug) |
| DELETE | /admin/discuss/tags/{tagId} | — | 204 (cascades thread-tags) |

### Photos

Photos (up to 10) attach to a thread or a reply via a two-step flow: create the thread/reply
with the existing JSON endpoints above, then upload images to its photo sub-resource. Stored in
S3/MinIO (key prefix `discuss/`) + the `DiscussPhotos` table. Bucket: `salestrainer-avatars` in the monolith, `sellevate-social` in the extracted social-service.
All require auth except the content GET.

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /discuss/threads/{threadId}/photos | `multipart/form-data`, field `files` (1..N images) | `200 DiscussPhotoListDto` |
| POST | /discuss/replies/{replyId}/photos | `multipart/form-data`, field `files` (1..N images) | `200 DiscussPhotoListDto` |
| DELETE | /discuss/photos/{photoId} | — | 204 (author only; else 403) |
| GET | /discuss/photos/{photoId}/content `[public]` | — | `200` image bytes |

- Upload errors: `400` (no files / >10 total / unsupported type / file >5 MB), `403` (not the author), `404` (thread/reply missing).
- `GET /discuss/photos/{photoId}/content` returns the image bytes with `Content-Type` from the stored value, `Cache-Control: public, max-age=60`, and `X-Content-Type-Options: nosniff`; `404` if missing.
- Allowed types: PNG / JPEG / WEBP (magic-byte validated). Per-file max 5 MB. Max 10 photos per owner (service-enforced).
- Photo `url` is the relative path `/discuss/photos/{id}/content`.
- Deleting a thread or reply (including admin delete) removes its photo rows and best-effort-deletes the S3 objects.

- `DiscussPhotoListDto`: `{photos: DiscussPhotoDto[]}`
- `DiscussPhotoDto`: `{id, url, orderIndex}`

DTO additions on the Discuss user endpoints above:
- `DiscussThreadDetailDto` gains `photos: DiscussPhotoDto[]`
- `DiscussReplyDto` gains `photos: DiscussPhotoDto[]`
- `DiscussThreadSummaryDto` gains `photoCount: number` and `firstPhotoUrl: string | null`

---

## Avatars

| Method | Path | Auth | Body | Response |
|---|---|---|---|---|
| POST | /avatars | Bearer | `multipart/form-data` with `file` field (PNG/JPG/JPEG/WEBP, max 5 MB) | `200 { "avatarUrl": "/avatars/{userId}" }` |
| DELETE | /avatars | Bearer | — | 204 |
| GET | /avatars/{userId:guid} `[public]` | — | — | `200` image bytes with `Content-Type: image/png\|jpeg\|webp`; `404` if user or avatar object not found |

- `POST /avatars` stores the image in S3 under `users/{userId}/avatar{ext}` and sets `AvatarType = Uploaded` on the user row.
- The S3/MinIO bucket (`Storage:S3:Bucket`) is created at startup via `IObjectStorage.EnsureBucketExistsAsync()` (best-effort, before default-avatar seeding). Without this the bucket never exists in fresh MinIO and `POST /avatars` fails with HTTP 500 (`NoSuchBucket`).
- `DELETE /avatars` best-effort deletes the uploaded object from S3, then resets `AvatarType = Default`, `AvatarKey = null`.
- `GET /avatars/{userId}` returns the uploaded object if `AvatarType == Uploaded`, otherwise the `DefaultAvatars` row matching `user.DefaultAvatarIndex`. Returns `404` if the user/avatar object cannot be resolved (so the client falls back to the generated avatar instead of a 500).
- `GET /avatars/{userId}` uses **validation-based caching**: it returns the object's `ETag` and `Cache-Control: public, no-cache` (clients cache but must revalidate every load). A matching `If-None-Match` yields `304 Not Modified` with no body. This makes a freshly uploaded avatar appear immediately after a page refresh (and in the nav bar) while unchanged images cost only a 304 round-trip. Do **not** restore a long `max-age` here — it reintroduces the stale-avatar-after-refresh bug.
- Subtask 5 will expose `avatarUrl` (value: `/avatars/{userId}`) on profile/user DTOs throughout the API.

---

## Company service (Phase 39)

> **New microservice `company-service`** (host port **5009**). Routes `/companies/*` via YARP gateway cluster `company` (wired in Phase 39.4). All endpoints require Bearer auth; `userId` extracted from `ClaimTypes.NameIdentifier`. Every query is scoped to the authenticated user — foreign/unknown ids return `404`.

### Companies

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /companies | `?search=` (optional) | `CompanySummaryDto[]` sorted newest-updated first |
| POST | /companies | `{name, description?}` | `201 CompanyDetailDto` |
| GET | /companies/{id} | — | `CompanyDetailDto` or `404` |
| PUT | /companies/{id} | `{name, description}` | `CompanyDetailDto` or `404` |
| PUT | /companies/{id}/status | `{status}` | `CompanyDetailDto` or `404` |
| PUT | /companies/{id}/follow-up | `{nextActionAt, nextActionNote?}` | `CompanyDetailDto` or `404` |
| POST | /companies/{id}/briefing | — | `CompanyBriefingDto`, `404`, or `503` if ai-service is unavailable |
| GET | /companies/{id}/briefing | — | `CompanyBriefingDto` or `204` if never generated, or `404` |
| GET | /companies/{id}/readiness | — | `CompanyReadinessDto`, `204` if no data yet, `404`, or `503` if ai-service is unavailable |
| DELETE | /companies/{id} | — | `204` or `404` (cascade-deletes logs + practice calls + contacts + personas) |

`CompanySummaryDto`: `{id, name, descriptionExcerpt (≤160 chars), status, callLogCount, practiceCallCount, contactCount, nextActionAt, createdAt, updatedAt}`
`CompanyDetailDto`: `{id, name, description, status, callLogCount, practiceCallCount, contactCount, nextActionAt, nextActionNote, followUpNotifiedAt, createdAt, updatedAt}`

Validation: `name` required, max 200; `description` max 8000.

`status` (Phase 39.10) is one of `Lead | Contacted | MeetingScheduled | DealWon | DealLost`
(string enum), defaulting to `Lead` on creation. `PUT /companies/{id}/status` sets it directly —
no server-side transition constraints, any status may be set from any other. `404` on missing
company or wrong owner, same ownership pattern as every other company endpoint.

`nextActionAt`/`nextActionNote`/`followUpNotifiedAt` (Phase 39.11 — follow-up reminders):
`PUT /companies/{id}/follow-up` with a non-null `nextActionAt` (re)schedules the follow-up
(`nextActionNote` optional, max 2000 chars, defaults to empty) and **resets
`followUpNotifiedAt` to `null`**, so a rescheduled due date is eligible to notify again even if
the previous one already fired. A request with `nextActionAt: null` **clears** the follow-up —
`nextActionNote` and `followUpNotifiedAt` are cleared with it. `followUpNotifiedAt` is
read-only/server-managed (set by the reminder background service, see below) and is exposed on
`CompanyDetailDto` for observability, not on the list DTO. `nextActionAt` is included on
`CompanySummaryDto` so the `/companies` list can render a due/overdue badge per row without an
extra request per company.

**Follow-up reminder background service (Kafka producer):** `company-service` runs a hosted
background service (`FollowUpReminderBackgroundService`) that polls every
`FollowUpReminder:PollIntervalMinutes` (default 5) for companies where `NextActionAt <= now AND
FollowUpNotifiedAt IS NULL`, claims them (sets `FollowUpNotifiedAt`, commits), and publishes one
`company.followup.due` Kafka event per claimed company — see `docs/MICROSERVICES.md §4.1` for the
topic/payload and `docs/ARCHITECTURE.md` for the claim-before-publish trade-off. Since Phase 40.12
the poll runs **once per organization** with a scoped tenant context (an unset tenant raises rather
than meaning "every organization"), and the event carries `organizationId` in the **envelope** —
not in the payload, whose fields are unchanged. Consumed by
notification-service → `NotificationType.CompanyFollowUpDue`, an in-app-only notification (no
email) titled *«Пора связаться с {companyName}»*, `actionUrl` `/companies/{id}`.

**Pre-call briefing / "Шпаргалка" (Phase 39.12):** `POST /companies/{id}/briefing` gathers
context — the company's `description`, the most recent non-empty `PracticeCall.Goal` (single
newest, not the last-5-distinct list used by `/recent-goals`), and the last 5 `CallLogEntry` rows
(newest first) — and forwards it to ai-service's internal `POST /ai/companies/briefing` (see
below), which returns a markdown cheat sheet. company-service caches the result on
`Company.BriefingContent`/`BriefingGeneratedAt` and returns it. `GET /companies/{id}/briefing`
returns the cached value without calling ai-service; `204` if the company exists but a briefing
has never been generated. Both endpoints follow the same ownership/`404` pattern as every other
company endpoint; `POST` returns `503` if ai-service is unreachable or misconfigured (mirrors the
Evaluation feature's error handling). **Feedback summaries are not included** — company-service
has no cross-service read into ai-service's Mongo feedback store (out of scope for 39.12), so the
`feedbackSummaries` list sent to ai-service is always empty; the briefing prompt degrades
gracefully (skips that section's data) when empty.

`CompanyBriefingDto`: `{content, generatedAt}` — both `null` when never generated.

**Readiness score (Phase 39.16):** `GET /companies/{id}/readiness` **self-generates and caches** —
unlike briefing, there is no separate `POST`. On a cache miss it gathers the company's practice-call
`DialogSessionId`s (newest first, capped to 50 — mirrors ai-service's own cap) and the single most
recent non-empty `PracticeCall.Goal`, and forwards them to ai-service's internal
`POST /ai/companies/readiness` (see above). The result is cached on
`Company.ReadinessJson`/`ReadinessGeneratedAt` and returned; a subsequent `GET` returns the cache
without calling ai-service again. Two distinct "no data" cases both collapse to `204`, with all
`CompanyReadinessDto` fields `null`, but they are **not** treated identically for caching: (1) the
company has no practice calls yet — the ai-service call is skipped entirely and **nothing is
cached** (every `GET` re-checks cheaply, since there's no fan-out to avoid); (2) the company has
practice calls but ai-service signalled `204` (none of them have usable feedback yet, e.g. sessions
still in progress or abandoned) — this **is negative-cached** on `Company.ReadinessNoFeedbackUntil`
for a short TTL (2 minutes) so repeated requests within the TTL short-circuit to the empty result
instead of re-running the fan-out (up to 50 sequential `DialogSessionId` lookups against
ai-service/Mongo) on every request (PR #26 review fast-follow, 39.17). `404` on missing
company/wrong owner; `503` if ai-service is unreachable, misconfigured, or returns an
unparseable/incomplete response (same pattern as briefing/persona) — a failure of this kind is
**never** cached (positive or negative), so the next `GET` retries against ai-service instead of
being stuck behind a stale/incorrect cache entry.

**Cache invalidation:** creating a practice call (`POST /companies/{id}/practice-calls`) is this
codebase's practice-completion signal (dialog-session completion itself is tracked only in
ai-service's Mongo, not in company-service) — it clears `ReadinessJson`/`ReadinessGeneratedAt`
**and** `ReadinessNoFeedbackUntil` on the company so the next `GET /readiness` regenerates from the
fresh session list instead of being held back by a stale negative cache. There is no other path in
company-service that marks a practice call complete.

`CompanyReadinessDto`: `{score, strengths, gaps, recommendation, generatedAt}` — all fields `null`
when there's no data yet (see above).

### Call Log

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /companies/{id}/logs | — | `CallLogEntryDto[]` sorted by `occurredAt DESC` |
| POST | /companies/{id}/logs | `{contactName, subject, outcome, occurredAt, contactId?}` | `201 CallLogEntryDto`, `404` if company not found, or `400` if `contactId` does not belong to the company |
| PUT | /companies/{id}/logs/{logId} | `{contactName, subject, outcome, occurredAt, contactId?}` | `CallLogEntryDto`, `404`, or `400` if `contactId` does not belong to the company |
| DELETE | /companies/{id}/logs/{logId} | — | `204` or `404` |
| POST | /companies/{id}/logs/parse | `{rawText}` | `ParsedCallLogDto`, `404`, or `503` if ai-service is unavailable |

**AI log parsing / "Вставить заметки" (Phase 39.13):** `POST /companies/{id}/logs/parse` proxies
`rawText` (pasted notes/transcript) to ai-service's internal `POST /ai/companies/parse-log` (see
above) and returns the extracted draft **without persisting anything** — the client prefills the
existing log-create form for the user to review/edit, then saves it through the normal
`POST /companies/{id}/logs`. Same ownership/`404` pattern as every other company endpoint; `503`
if ai-service is unreachable, misconfigured, or returns an unparseable response.

`ParsedCallLogDto`: `{contactName: string|null, subject, outcome, occurredAt: DateTime|null}`.

`CallLogEntryDto`: `{id, companyId, contactName, subject, outcome, occurredAt, createdAt, updatedAt, contactId}`

Validation: `contactName` required, max 200; `subject`, `outcome` optional (empty string allowed), max 4000. `contactId` is optional; when present it must reference a `CompanyContact` belonging to the same company (otherwise `400`). The free-text `contactName` is always stored regardless of `contactId`, so the log keeps a readable label even after the linked contact is deleted (see Contacts below).

**`400` on a bad `contactId` (39.17 hardening):** company-service raises a typed
`ContactNotFoundInCompanyException` — not a generic `InvalidOperationException` — both when the
ownership check fails up front and when a concurrently-deleted contact trips the `ContactId`
foreign key at `SaveChangesAsync` time (the check-then-act race between the ownership check and the
save; the FK-violation `DbUpdateException` is only translated when it's specifically a Postgres
`23503` on the `FK_CallLogEntries_CompanyContacts_ContactId` constraint — any other
`DbUpdateException` propagates unchanged as a `500`). Both cases map to the same
`400 { code: "CONTACT_NOT_FOUND", message }` response, where `code` is a machine-readable
discriminator distinguishing this from other `400`s on the same endpoints (e.g. ASP.NET
model-validation failures on `contactName`/`subject`/`outcome` length, which have no `code` field).
The frontend only clears the stale `contactId` from the call-log form when it sees
`code === "CONTACT_NOT_FOUND"`, so retrying resubmits as free text instead of repeating the same
failing request, while other `400`s leave the form untouched.

### Practice Calls

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /companies/{id}/practice-calls | `{dialogSessionId, goal?}` | `201 PracticeCallDto` or `404` |
| GET | /companies/{id}/practice-calls | — | `PracticeCallDto[]` sorted by `createdAt DESC` |
| GET | /companies/{id}/recent-goals | — | `string[]` — last 5 distinct non-empty goals, newest first |

`PracticeCallDto`: `{id, companyId, dialogSessionId, goal, createdAt}`

`goal` is **optional** (`≤1000`); when omitted/empty it is stored as `""` and excluded from
recent-goals. The client records the practice call only once the session **completes and
feedback is formed** (on hang-up / stop-signal), not at call start — so an abandoned session
leaves no practice-call record.

Validation: `goal` max 1000; `dialogSessionId` required.

### Contacts (Phase 39.9 — mini-CRM)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /companies/{id}/contacts | — | `CompanyContactDto[]` sorted by `createdAt DESC` |
| POST | /companies/{id}/contacts | `{name, position?, notes?}` | `201 CompanyContactDto` or `404` if company not found |
| PUT | /companies/{id}/contacts/{contactId} | `{name, position?, notes?}` | `CompanyContactDto` or `404` |
| DELETE | /companies/{id}/contacts/{contactId} | — | `204` or `404`. Any `CallLogEntry.ContactId` referencing this contact is set to `null`; the log's free-text `ContactName` is preserved. |

`CompanyContactDto`: `{id, companyId, name, position, notes, createdAt, updatedAt}`

Validation: `name` required, max 200; `position` optional (nullable, defaults to empty), max 200; `notes` optional (nullable, defaults to empty), max 2000. Create and Update use the same nullability for `position`/`notes` (39.17 hardening — they previously diverged: Update declared them as non-nullable with an empty-string default instead of nullable).

### Personas (Phase 39.14 — AI persona generation for practice calls)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /companies/{id}/personas | — | `CompanyPersonaDto[]` sorted by `createdAt DESC` |
| POST | /companies/{id}/personas | `{name, position, personality, difficulty}` | `201 CompanyPersonaDto` or `404` |
| DELETE | /companies/{id}/personas/{personaId} | — | `204` or `404` |
| POST | /companies/{id}/personas/generate | `{contactName?, contactPosition?, difficulty}` | `GeneratedCompanyPersonaDto`, `404`, or `503` if ai-service is unavailable |

`CompanyPersonaDto`: `{id, companyId, name, position, personality, difficulty, createdAt}`
`GeneratedCompanyPersonaDto`: `{name, position, personality}` — not persisted.

Validation: `name` required, max 200; `position` required, max 200; `personality` required, max 4000; `difficulty` one of `Easy | Medium | Hard` (string enum, same conversion pattern as `Company.Status`).

**Generate-then-save flow:** `POST /companies/{id}/personas/generate` gathers the company's
`description` and forwards it — plus the optional `contactName`/`contactPosition` seed and the
requested `difficulty` — to ai-service's internal `POST /ai/companies/persona` (see above), and
returns the draft `{name, position, personality}` **without persisting anything**, so the caller
can regenerate before committing. Saving is a separate `POST /companies/{id}/personas` call with
the (possibly edited) draft plus the chosen `difficulty`. The roadmap's "seeded from an existing
contact" note is purely a frontend UX affordance (prefilling `contactName`/`contactPosition` from
a contact the user picks) — there is no backend coupling between `CompanyContact` and
`CompanyPersona`. Same ownership/`404` pattern as every other company endpoint; `503` if
ai-service is unreachable, misconfigured, or returns an unparseable/incomplete response.

**Injection into practice calls:** the frontend's persona selector (chips + «Без персоны» +
generate) lets the caller pick a saved `CompanyPersona` (or none) before starting a voice/chat
practice call; the selected persona's `name`/`position`/`personality`/`difficulty` are sent as the
`persona*` fields of `companyContext` on `POST /dialog/sessions` (see the Dialog section above).

---

## Tracking / Usage Metrics

> **Served by the analytics-service** (Phase 1). The gateway routes `/tracking/*` to the
> analytics cluster; the monolith's `MetricsController` is left in place as reference but no
> longer receives this traffic. Frontend paths are unchanged. See
> [ANALYTICS_SERVICE.md](ANALYTICS_SERVICE.md).

| Method | Path | Auth | Body | Response |
|---|---|---|---|---|
| POST | /tracking/events | Bearer | `{event, page}` | `204` on success; `400` on unknown event/page; `401` if unauthenticated |
| POST | /tracking/presence/ping | Bearer | _(none)_ | `204` on success; `401` if no resolvable user identity |

- `/tracking/events` feeds the Prometheus counters `app_page_views_total` / `app_events_total`. `event="page_view"` is recorded as a page view (uses only `page`); any other event uses both labels.
- `event` and `page` are validated against a **server-side whitelist** (`analytics-service/Analytics/Features/Tracking/Constants/TrackedEvents.cs`) to cap label cardinality — unknown values are rejected with `400`, never silently accepted.
- `/tracking/presence/ping` marks the caller present (Redis sorted set) and bumps `app_authenticated_requests_total`. Identity is taken from the gateway-injected `X-User-Id` header, falling back to the validated JWT subject.
- All product metrics are scraped from the `/metrics` endpoint (jobs `sallevate-backend` + `sellevate-analytics`); there is no read API for them — query them in Prometheus/Grafana. See [MONITORING.md](MONITORING.md).

---

## Organization service (Phase 40.5)

> **New microservice `organization-service`** (host port **5010**). Routes `/organizations/*` via
> YARP gateway cluster `organization`. All endpoints require Bearer auth. See
> [ORGANIZATION_SERVICE.md](ORGANIZATION_SERVICE.md) and
> [docs/TENANCY/TENANCY.md](TENANCY/TENANCY.md).

### Organizations (tenant registry — not tenant-scoped)

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /organizations | `{name, slug?}` | `201 OrganizationDetailDto`, `400` blank name, `409` slug taken |
| GET | /organizations | — | `OrganizationSummaryDto[]` newest-created first |
| GET | /organizations/{id} | — | `OrganizationDetailDto` or `404` |
| PUT | /organizations/{id} | `{name, slug?}` | `OrganizationDetailDto`, `404`, `400`, or `409` slug taken |
| POST | /organizations/{id}/suspend | — | `OrganizationDetailDto` or `404` |
| POST | /organizations/{id}/reactivate | — | `OrganizationDetailDto` or `404` |

`OrganizationSummaryDto`: `{id, name, slug, status, createdAt}`
`OrganizationDetailDto`: `{id, name, slug, status, createdAt, updatedAt}`

`slug` is normalized (lowercased, non-alphanumerics collapsed to single hyphens) and globally
unique; omitted → derived from `name`. `status` is `Active | Suspended`. These routes are not
`[TenantScoped]` — they administer the registry itself, not one organization's own data (see
docs/TENANCY/TENANCY.md §1.2).

**Phase 40.9, revised 2026-08-16:** the whole controller requires `RequirePlatformAdmin` — running the tenant registry is ordinary platform administration, and only adding/removing users is superadmin-exclusive. Any other caller gets `403`.
That gate is what makes addressing an organization by a route id legitimate here and nowhere else
(docs/TENANCY/TENANCY.md §1.3).

Publishes `organization.created` on create, `organization.updated` on rename/reactivate,
`organization.suspended` on suspend. **Consumed since 40.9** by identity-service, which keeps an
`OrganizationReplicas` projection so a suspended organization stops producing tokens.

### Organization profile (tenant-scoped)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /organizations/profile | — | `OrganizationProfileDto` or `404` if not set up yet |
| PUT | /organizations/profile | `UpdateOrganizationProfileRequestDto` | `OrganizationProfileDto` |

Both routes are `[TenantScoped]`: `TenantContextMiddleware` rejects the request with `403` if the
gateway-validated `X-Organization-Id` header is absent — there is no organization id anywhere in
the route or body (docs/TENANCY/TENANCY.md §1.3). The target organization is resolved solely from
the header.

`OrganizationProfileDto` / `UpdateOrganizationProfileRequestDto`: `{product?, icp?, objections[] ({text, frequency?, bestResponse?}), scriptStages: string[], tone?, glossary: {[key]: string}, bannedClaims: string[], createdAt, updatedAt}` — shape per [CONTENT_MODEL.md §3](TENANCY/CONTENT_MODEL.md#3-the-organization-profile--the-part-that-removes-most-forks). `PUT` upserts: the first call for an organization creates the row.

**Since Phase 40.19 this `PUT` is the substitution surface of the whole product.** A successful save
publishes `organization.profile.updated` (whole profile, after the commit), which learning-service and
ai-service project into local replicas; from then on `{{organization.product}}` and its siblings
resolve out of it in lesson text, exercise content, grading prompts and persona prompts, and
`bannedClaims` binds both the AI persona and the scoring. Two consequences a caller can observe:

- **It is eventually consistent.** A save takes a moment to reach a lesson or a live call. The response
  is authoritative for organization-db; the rendered lesson is not, for a second or so.
- **An empty profile is not an error state.** Unfilled fields render as the neutral base wording
  («ваш продукт», «ваш клиент»), never as blanks and never as visible `{{…}}`.

Syntax, fallbacks and the render-on-read rule: [CONTENT_PARAMETERIZATION.md](CONTENT_PARAMETERIZATION.md).
