# API_CONTRACTS.md

Base URL: `http://localhost:5000` (dev) | `http://backend:8080` (docker internal)

All endpoints except those marked `[public]` require `Authorization: Bearer <accessToken>`.

> **Microservices migration:** `/auth/*`, `/demo/*`, `/profile/*`, `/onboarding/*` and
> `/avatars/*` are now served by the extracted **Identity service** (gateway base URL
> `http://localhost:5000`), not the monolith. Paths and request/response shapes are
> unchanged. One caveat, still true: `GET /profile` returns the activity-consistency /
> progress-points / completed-skill / average-score aggregates as **0** — `ProfileService.cs:32`
> hard-codes `CompletedSkillCount: 0` and its neighbours. The reason this document used to give for
> it ("Gamification/Learning not extracted yet, roadmap phases 7 & 8") is no longer the reason: both
> services were extracted long ago. The zeros survive because identity-service was never wired to
> compose them — it would have to call gamification and learning on the profile read path, and
> nobody has decided whether that is worth the fan-out. The identity fields (displayName, email,
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
| POST | /auth/register `[public]` | `{email, password, displayName}` | `AuthTokenResponseDto` + cookie `refreshToken`, or `202 {email, requiresEmailVerification}` |
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

> **`POST /auth/register` is back (Phase 40.37),** after 40.7 had deleted it — see
> [TENANCY/TENANCY.md](TENANCY/TENANCY.md) section 4.1a. It creates an account with **no
> membership** and never creates one, so registering buys an identity and no access to anybody's
> data: with no active membership the JWT carries no `org_id`, and the client shows the
> "waiting for an invitation" screen instead of the app. Joining an organization is still
> exclusively `POST /auth/invites/{token}/accept`.
>
> `409` when the address is taken — sign-up is the one route that cannot be evasive about that, as a
> form refusing to say so could not create the account either. `202` instead of `200` when
> `EmailVerification:Enabled` is on: the account exists, a code has been mailed, and no session is
> issued until `POST /auth/verify-email` consumes it. The flag is **off** by default, in which case
> the address is marked verified on the spot and the response is a normal `200` with a session.
>
> `POST /auth/google` provisions on the same terms — an unknown Google identity becomes an account
> with no membership, since sign-up "через email или Google" is what `RAW.md` asks for. Its one
> remaining `401` is an address already held by an *unverified* local row, which can be neither
> signed into nor duplicated. The membership check it used to apply is gone: a member-less account
> now has a screen to land on, and refusing it here while `/auth/login` admitted it was an
> inconsistency the platform-staff carve-out existed to paper over.

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
two fields (`orgId`, `orgRole`) alongside `id`/`email`/`displayName`/`role`/`isOnboardingCompleted`,
and adds **`orgName`** (Phase 40.20).

`orgName` is the display name of the caller's own organization and is deliberately **not** a token
claim: it is read from identity's local registry projection on every call, so a rename takes effect
immediately instead of after everyone signs in again, and the read costs nothing on the
authentication path because the projection is local. It is `null` for a caller with no organization,
and also `null` when the projection has not yet consumed `organization.created` for a real one —
the panel shows a neutral label rather than blocking. It exists because nothing else told a member
the name of their own organization: the claim carries only the id, and `GET /organizations/{id}` is
platform-staff only.

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
| POST | /admin/platform/organizations/bootstrap-admin | `{organizationId, email, role?}` | `BootstrapOrganizationAdminResponseDto`, `400` role is not `TenancyAdmin`/`TenancySuperAdmin`, `404`, `409` already has an admin, `403` suspended |

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
email, same one-time token — in a scope pinned to the target organization. Until 2026-08-20 the
role was always `TenancySuperAdmin` and was never taken from the request at all, because a role
taken from the request would let a platform-only endpoint mint any organization role anywhere — a
far larger blast radius than the one thing it exists to do. The owner asked for the choice back, so
the request now carries an optional `role`, parsed by the exact rules `InviteService.ParseRole`
already applies to an ordinary invite (including the retired `OrgAdmin` name check), and then
narrowed to `TenancyAdmin` or `TenancySuperAdmin` only — `Manager` and anything unrecognized are a
`400`. Omitted or blank still defaults to `TenancySuperAdmin`, so every caller that predates this
field keeps working unchanged. The endpoint still cannot mint an arbitrary organization role; it can
only pick which rank of administrator it bootstraps. It answers `409` if the organization already
has an active `TenancyAdmin` or `TenancySuperAdmin` membership, or a pending invite for either, so
it cannot be used as a back door into a running customer's organization — see DECISIONS.md
(2026-08-20).

`404` also covers "organization-service created it seconds ago and identity-service has not
consumed `organization.created` yet"; the message says so and the operation is safe to retry.

### Internal, un-gatewayed (`X-Internal-Service-Secret`) — demo-request provisioning

| Method | Path | Body → Response |
|---|---|---|
| POST | /internal/organizations/{organizationId:guid}/bootstrap-admin | `{organizationName, organizationSlug, email, role?, actorUserId}` → `{inviteId, email, expiresAt}` |

Called only by organization-service's `POST /admin/demo-requests/{id}/provision` (above). Not
`[Authorize]` and not `[TenantScoped]` — the caller has no JWT and no membership in the
organization it names by route segment, the same carve-out `PlatformAdminController` relies on for
its request body (`scripts/tenancy-boundary-lint.py` allow-lists this controller by exact path for
the route-segment case).

In order: **upserts `OrganizationReplica` from the payload** — authoritative here, not fed from
Kafka, because the caller *is* the registry owner and just committed the organization row in the
same request that triggers this call, which would otherwise race its own Kafka consumer on every
provision (unlike the `404` two paragraphs up, which tolerates exactly that race for a human
retrying a form); **re-checks `actorUserId` is a platform `SuperAdmin`** in identity-db (`403` — the
shared secret authorizes organization-service's channel, this check authorizes the actor, and
skipping it would let a plain `Admin`'s provisioning click be laundered into a superadmin act);
`409` if an active `TenancyAdmin`/`TenancySuperAdmin` membership already exists; **`200` returning
the existing invite, not `409`, if one is already pending** — required for convergent retry, since
`InviteService.CreateAsync` sends its email after commit and outside any try/catch, so a mail
failure can leave a committed invite behind a thrown exception; otherwise mints through
`IInviteService` with `InvitedBy = actorUserId`, role validated/narrowed by the same rule
`PlatformAdminService.ResolveBootstrapRole` applies (`Manager`/unknown → `400`, omitted →
`TenancySuperAdmin`), and the mail send is wrapped so a committed invite never surfaces as a `500`.
Full narrative: docs/DEMO_REQUEST.md.

Excluded from the gateway routing table and from `RouteParity.Tests`'
`Every_public_controller_route_is_reachable_through_the_gateway` by the same `internal/` prefix
convention every other service-to-service route here uses.

---

## Invites & memberships `[tenant-scoped]`

| Method | Path | Body | Response | Gate |
|---|---|---|---|---|
| GET | /memberships?status=active\|deactivated\|all | — | `MembershipDto[]` | `RequireOrgAdmin` |
| GET | /invites?status=pending\|all | — | `InviteSummaryDto[]` | `RequireOrgAdmin` |
| POST | /invites | `{email?, emails?, role}` | `CreateInvitesResponseDto` | `RequireOrgSuperAdmin` |
| DELETE | /invites/{inviteId} | — | 204 / 404 | `RequireOrgSuperAdmin` |
| DELETE | /memberships/{userId} | — | 204 / 404 | `RequireOrgSuperAdmin` |

The three writes add or remove a user, which after the 2026-08-16 role split is the one privilege
reserved for a superadmin (`org_role = TenancySuperAdmin`, or a platform `role = SuperAdmin`). The
two reads are not that privilege and were added in Phase 40.20, because a `TenancyAdmin` hands out
assignments to these people and cannot do it blind: both controllers are gated `RequireOrgAdmin` at
the class level with `RequireOrgSuperAdmin` on each write action. Authorize attributes are ANDed and
every superadmin satisfies the admin policy, so the looser class gate cannot widen a write.

Everything here needs the gateway-injected `X-Organization-Id` header — all of it is
`[TenantScoped]`, so a request without that header gets `403` before the action runs.

`MembershipDto`: `{userId, email, displayName, role, status, joinedAt, deactivatedAt}` — `role` and
`status` are **names**, not the enum's numbers (identity-service registers no
`JsonStringEnumConverter`). Ordered by display name. The query starts from `Memberships` and joins
outward to `Users`; a user with no membership row is invisible here, which is the point — `Users` is
platform-global and has no organization column of its own.

`InviteSummaryDto`: `{id, email, role, status, invitedBy, createdAt, expiresAt}`. Default
`status=pending`, because an accepted invite is a membership now and belongs on the roster.
`status` is derived — `pending` / `accepted` / `revoked` / `expired` — with recorded facts
outranking the clock, so an accepted invite whose expiry has passed still reads `accepted`. There is
**no `token` field and never will be**: the raw token exists once, in the creation response and the
invitee's mailbox, and a listing that returned it would turn any admin read into account takeover of
a pending invitee.

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
| GET | /skills/progress-summary | — | `LearningProgressSummaryDto` — the caller's own headline numbers |
| GET | /skills/:skillId/topics | — | `TopicDto[]` — `{topicId, skillId, title, orderInSkill}`, the topics of one skill by **id** (not slug), ordered |
| PUT | /skills/enrolled | `{skillSlugs: string[]}` | 204 |

`PUT /skills/enrolled` — replaces the user's enrolled skill set.  
Skills in the list that are not yet enrolled are set to `available`.  
Skills currently enrolled but absent from the list are set to `locked` (progress preserved).  
`sales-basics` is always kept enrolled.

`LearningProgressSummaryDto`: `{completedSkillCount, totalSkillCount, completedLessonCount, averageExerciseScore}`

`GET /skills/progress-summary` — the profile screen's headline numbers, from the service that owns
the rows. `averageExerciseScore` is the mean `bestScore` over **completed** lessons (the same
definition the skill tree uses for its per-skill accuracy, so the screens agree) and is `null`, not
`0`, when nothing has been completed — "no data" and "scored zero" are different answers. Both skill
counts cover only the skills the learner is **enrolled** in, so `completedSkillCount /
totalSkillCount` means the same thing here as on the tree.

> **Do not read progress from `GET /profile`.** Identity-service still carries
> `averageExerciseScore`, `completedSkillCount`, `totalSkillCount`, `totalXpAmount` and the streak
> fields on `UserProfileStatsDto`, but they are hard-coded zeros: identity stopped owning learning
> data at the microservices split and gamification was removed from the product. The fields survive
> only so the response shape stays stable. The same applies to `averageExerciseScore` on
> social-service's `PublicProfileDto`.

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
| GET | /skills/:slug/lessons | — | `LessonSummaryDto[]` (200 `[]` for a real skill with no lessons yet; 404 only when `:slug` names no skill at all) |
| GET | /lessons | — | `LessonSummaryDto[]` (all skills) |
| GET | /topics/:topicId/lessons | — | `LessonSummaryDto[]` (one topic, with the caller's per-lesson status) |
| GET | /lessons/:lessonId/exercises | — | `ExerciseDto[]` (200 `[]` for a real lesson with no exercises yet; 404 only when `:lessonId` names no lesson at all — docs/AUDIT_PROD.md X-5) |
| POST | /exercises/:exerciseId/submit | `{answer: <jsonb>, skipped?: boolean}` | `ExerciseSubmissionResultDto` |
| POST | /exercises/:exerciseId/chat | `{message: string}` | `ExerciseChatResponseDto` |
| POST | /exercises/:exerciseId/voice/stream | `{message: string}` | `application/octet-stream` — length-prefixed frames |

`LessonSummaryDto`: `{lessonId, title, orderInTopic, topicOrder, status, bestScore, kind}` where `kind` is `"theory"` (every exercise is a `theory_card`) or `"practice"`. Theory lessons are played as swipeable cards; the client submits the last card once to complete them. Across a skill, lessons are ordered by `topicOrder` (the topic's `OrderInSkill`) first, then by `orderInTopic` — so topics stay grouped instead of interleaving; the client sorts by `(topicOrder, orderInTopic)`.

**`ExerciseSubmissionResultDto`'s `correctAnswer` field (docs/AUDIT_PROD.md X-3/X-6/X-8):** the
pre-submission `GET /lessons/:lessonId/exercises` deliberately strips every answer-key field
(`is_correct`, `correct_position`, `is_mistake`, `ai_prompt` — `ExerciseService.StripAnswerKeyFields`)
so the client never has the answer before the learner submits one. Three exercise types' feedback UI
was reading one of those stripped fields anyway and so always found it absent: `reorder`'s per-row
correct/wrong marking (`correct_position`), `choose_option`/`fill_blank`'s "which option was right"
highlight after a wrong answer (`is_correct`), and `spot_mistake`'s "which line was the real mistake"
highlight (`is_mistake`). The fix is one contract addition rather than three: `POST
/exercises/:exerciseId/submit`'s response gained `correctAnswer: ExerciseCorrectAnswerDto | null`,
populated by the same evaluation strategy that already needed the answer to grade the submission —
so nothing is revealed before the learner has answered, only in the result of having done so.
`ExerciseCorrectAnswerDto`: `{correctOptionIndex: number | null, order: number[] | null,
correctLineIndex: number | null}` — each exercise type sets only the field it needs and leaves the
rest `null`: `choose_option`/`fill_blank` set `correctOptionIndex` (index into `options`); `reorder`
sets `order` (the `items` indices in correct order, same shape as the `{order: number[]}` the learner
submits, so the client diffs its own submission against it); `spot_mistake` sets `correctLineIndex`
(index into `dialogue`). Free-form/AI-graded types with nothing to reveal in this shape
(`free_text`, `rewrite`, `ai_dialogue`, `evaluate_call`) and a skipped submission leave the whole
object `null`.

**`POST /exercises/:exerciseId/submit`'s `skipped` field (docs/AUDIT_PROD.md X-4):** the client's "Skip"
button sends `{answer: {}, skipped: true}` instead of calling nothing. A skipped submission records a
real `UserExerciseAttempt` (always `isCorrect: false`, `score: 0`, no AI call and no grading strategy
invoked) so it counts toward the lesson's every-exercise-attempted completion gate the same as a wrong
answer would — before this, a lesson finished entirely by skipping showed the learner "Урок завершён"
for a lesson the backend never actually closed. Omitting `skipped` (or sending `false`) behaves exactly
as before.

**AI Dialog Chat Endpoint:**
`POST /exercises/:exerciseId/chat` — for `ai_dialog` type exercises only. Handles multi-turn conversation.
`ExerciseChatResponseDto`: `{response: string, isComplete: boolean, turnNumber: number, maxTurns: number}`
The **user speaks first** — an empty `message` returns an empty turn (no AI greeting); the AI only replies after the user's opening line.

**AI Dialog Voice Endpoint:**
`POST /exercises/:exerciseId/voice/stream` — voice mode for `ai_dialog` exercises. Streams the same length-prefixed `[flags u32][textLen u32][text][audioLen u32][audioMp3]` frames as the live-call voice stream (`flags` bit0 = isFinal, bit1 = isStopSignal/endCall). Shares chat history with `/chat`, so text and voice turns interleave. Uses the same TTS pipeline as calls.

**`GET /skills/:slug/lessons` 404 vs. empty array:** 404 means `:slug` matches no `Skill` row at all. A
skill that exists but has no topics, or has topics but no lessons, is a normal state (a skill just
added to the tree, or mid-authoring) and returns 200 with `[]`, not 404 — the client's "no lessons yet"
empty state, not its error state, is what should render for it.

**Lesson unlock behavior:**
- First call to `GET /skills/:slug/lessons` lazy-seeds `UserLessonProgress` rows: lesson 1 → `available`, rest → `locked`.
- Submitting an answer that attempts a lesson's last remaining exercise marks the lesson `completed` and eagerly writes the next lesson (by order) to `available`.
- `locked`/`available` in the `GET /skills/:slug/lessons` response is re-derived on every read from completed-lesson facts, not trusted from the stored row alone (docs/AUDIT_PROD.md X-11): any lesson immediately following a `completed` one is reported `available` even if its own row is still `locked` or missing, so an account whose eager unlock write never fired self-heals on the next read. See `docs/LEARNING_SERVICE.md#lesson-progression--unlocking` for full details.

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

---

## Reference

| Method | Path | Response |
|---|---|---|
| GET | /skills/:skillIdentifier/reference | `ReferenceMaterialDto[]` — the materials of one skill. The segment is resolved as **either the skill's GUID or its slug** (`IconicName`), matching the sibling `GET /skills/:skillSlug/lessons`'s dual acceptance (docs/AUDIT_PROD.md finding A-4). An identifier that resolves to no skill returns `200 []`, not a 404 — the endpoint has always returned an empty list for an unmatched GUID, and unmatched slugs now follow the same rule instead of routing-404ing (the old `{skillId:guid}` constraint rejected any slug before the handler ran) |
| GET | /reference?category=&search= | `ReferenceMaterialDto[]` — the whole library, both filters optional and independent |
| GET | /reference/categories | `string[]` — the distinct non-empty categories, for the filter control |

`ReferenceMaterialDto`: `{materialId, title, markdownContent, sortOrder, category, tags: string[], skillId}` — `skillId` is what a caller must resolve first (e.g. via `/reference`) if all it has is a `materialId`, since there is no `GET /reference/:materialId` route.

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

**`TechniqueDialogTurnDto.Annotations`, `TechniqueDialogAnnotationDto.Label` and
`TechniqueCoachChallengeDto.Label` are all declared non-nullable but come from author-supplied
`Technique.DialogJson`/`Coach.ChallengesJson`, not a DB column** — `System.Text.Json` does not
enforce nullable annotations, so a turn missing the `annotations` key (or holding
`"annotations": null`), a literal `null` element inside `annotations`/`challenges`, or an
annotation/challenge missing `label` would otherwise deserialize with the field `null` despite
the declared type. `TechniqueService` normalizes all of this once, server-side, in
`DeserializeDialogTurns`/`NormalizeAnnotations`/`DeserializeChallenges`: a `null` `annotations`
array becomes `[]`, a `null` array element (turn, annotation, or challenge) is dropped, and a
`null` `label` becomes `""`. Clients may treat every one of these fields as always present and
never `null` — but the frontend guards `turn.annotations` at the render site anyway
(`app/(main)/guidebook/page.tsx`), since this DTO's non-nullability is a service-level guarantee,
not something the JSON contract itself enforces.

`TechniqueMetaDto`: `{skills: [{iconicName, title, techniqueCount}], totalCount, userCounts: {mastered, master, unseen}}`. A technique's skill(s) are `PrimarySkillId` *and* `AdditionalSkills` combined (same union `GET /techniques/:slug` already uses for `skillIconicNames`) — `skills` and the `?skill=` filter on `GET /techniques` both resolve a skill facet against either field, so a technique tagged only via `AdditionalSkills` (the common case in practice) still shows up under its skill's chip and its count. Only skills that have at least one technique (by that union) appear in `skills`.

`userCounts` fields are **nested, not a partition** — `mastered` (level ≥ Practitioner) is a superset of `master` (level ≥ Master), by design (docs comment on `TechniqueLevels`), so they are never meant to be added together. `unseen` is `totalCount` minus every technique the user has *any* progress row for, including ones seen but still below `mastered`'s threshold. That is why `mastered + master + unseen` can come in under `totalCount`: the techniques a learner has looked at but not yet reached Practitioner on are counted in neither exposed bucket. This is intentional under the current three-field contract, not a miscount; a caller that needs an exhaustive breakdown needs a fourth "seen, unmastered" bucket, which does not exist yet (no consumer currently needs it — the guidebook only reads `totalCount` and `userCounts.mastered`).

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

## Admin (the gate is **per controller** — read this before assuming)

All routes prefixed `/admin`. Unauthorized → 403.

> **There is no single policy for `/admin/*` any more.** This section used to be headed "requires
> `RequirePlatformAdmin`" and to claim that "every `/admin/*` content endpoint below is open to
> Sellevate staff at either rank", with `RequireOrgAdmin` declared in every service but holding
> **zero call sites**. Blocks 40.15 onward falsified all of that: `RequireOrgAdmin` now has **20
> call sites across 18 controllers**, and the two gates split the section roughly in half.
>
> **Platform-only (`RequirePlatformAdmin`, `role` ∈ {`Admin`, `SuperAdmin`}).** The shared library
> and the platform's own knobs — nobody's customer edits these: skills, skill stages, topics,
> exercise-type prompts, daily quotes, the seeder, dialog bundles/modes, voice usage, leagues,
> gamification settings, and discuss moderation.
>
> **Organization-scoped (`RequireOrgAdmin`).** Everything a customer administers inside their own
> tenant: lessons, lesson versions, lesson metrics/accuracy, programme, assignments, exercises,
> reference, techniques, content overrides, content generation, content adaptation, team skill
> gaps, team insights, dialog reviews, admin dialog sessions, dialog overrides, AI quota usage, and
> the organization profile. The policy **also admits platform staff** carrying no organization role
> at all, so a Sellevate administrator can still reach these screens — with the RLS `USING` clause
> widening their reads and `WITH CHECK` refusing their writes into a tenant they did not name.
>
> Individual subsections below state their own gate; where one does, it is authoritative. A handful
> of controllers mix the two — `AdminLessonsController` is org-scoped except for `Create`, which is
> platform-only because creating a base lesson adds to the shared library.
>
> `RequireSuperAdmin` is unchanged and still guards only the routes that add or remove a user: the
> `/admin/users` mutations and all of `/admin/platform/*`. The organization roles (`TenancyAdmin`,
> `TenancySuperAdmin`) live on `membership`, not on `user`; their own admin screen is roadmap block
> 40.20. See [IDENTITY_SERVICE.md](IDENTITY_SERVICE.md), [ADMIN_PANEL.md](ADMIN_PANEL.md) and
> `docs/DECISIONS.md` (2026-08-16) for the route audit that started this split.

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
| PUT | /admin/skills/:skillIconicName/topics/:topicIconicName | `{iconicName?, title?, orderInSkill?}` | `AdminTopicDto` |
| PUT | /admin/topics/:id | `{iconicName?, title?, orderInSkill?}` | `AdminTopicDto` |
| DELETE | /admin/topics/:id | — | 204 |

`AdminTopicDto`: `{id, skillId, iconicName, title, orderInSkill}`
`AdminTopicWithSkillDto`: `{id, skillId, skillIconicName, skillTitle, iconicName, title, orderInSkill}`

The two `PUT`s are the same update reached two ways: by iconic name (the pair the admin UI has in
hand when it is browsing a skill) or by primary key. Both take the same partial body — every field
optional, only the supplied ones are written. The iconic-name form ignores `skillIconicName` when
locating the row; the topic's own iconic name is already unique.

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

### Assignments (Phase 40.21, thresholds 40.22, issuing and the manager's screen 40.23, automatic repeats 40.24, the РОП's push 40.26)

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
| POST | /admin/assignments/:assignmentId/remind?scope=unfinished\|not_started | — | `AssignmentReminderResultDto` (409 if it is not active or the scope is unknown; **503** when the roster cannot be read) |

Learner-facing (`[Authorize]`, no admin gate, takes no user id — the caller is the token):

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /assignments/active | — | `ActiveAssignmentDto[]`, soonest deadline first, `[]` when there are none |

Service-to-service (no JWT; `X-Internal-Service-Secret` + `[TenantScoped]`, **not routed through the
gateway**):

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /internal/memberships/active *(identity-service)* | — | `{userIds: uuid[], administratorUserIds: uuid[]}` — active memberships of the organization in the header, and (40.26) the subset who administer it |
| GET | /internal/assignments/practice-context?userId=&modeKey= *(learning-service)* | — | `AssignmentPracticeContextDto`, or 204 when this person owes no conversation on that mode |

`CreateAssignmentRequestDto` / `UpdateAssignmentRequestDto`:
`{title, goal?, sourceType, sourceRef?, content?: AssignmentContentItemDto[], audience?, opensAt?, deadline?, completionRule, repeatSchedule?}`
`CreateAssignmentRequestDto` also takes `contentGenerationJobId?` (40.31). **When it is present,
`sourceType` and `sourceRef` in the body are ignored and derived from the run instead** — `gap_detected`
+ the run's `skill-gap:<stage>@<date>` for a run the dashboard started, `training` +
`lesson-version:<uuid>` for one somebody started by pasting material. The caller therefore cannot label
hand-written work as detected by the dashboard, and cannot lose the link by forgetting to label
generated work. `content` left empty defaults to the run's own frozen lesson version, so the ordinary
case is one field; sending a `content` list keeps it, which is how the exercises get paired with a
`dialog_scenario` and a reading. A run that has not reached `completed` with a `producedLessonVersionId`
is a 400 — an assignment pointing at a lesson that does not exist yet would be issued the moment
somebody pressed activate, and 40.21 froze `content` at that moment on purpose
`AssignmentContentItemDto`: `{kind, reference, orderIndex, persona?}` — `persona` (40.23) is `{name?, position?, personality?, difficulty?}`, meaningful only on a `dialog_scenario` item and silently dropped from every other kind
`AssignmentAudienceDto`: `{kind, userIds?, groupId?}`
`AssignmentDto`: `{id, title, goal, sourceType, sourceRef, content[], audience, opensAt, deadline, completionRule, repeatSchedule, repeatOfAssignmentId, repeatWaveIndex, status, createdBy, createdAt, updatedAt, activatedAt, closedAt}`
`AssignmentSummaryDto`: `{id, title, sourceType, status, audienceKind, opensAt, deadline, hasRepeatSchedule, repeatOfAssignmentId, repeatWaveIndex, contentItemCount, assignedCount, startedCount, completedCount, failedThresholdCount, createdBy, createdAt, updatedAt}`
`repeatOfAssignmentId` / `repeatWaveIndex` (40.24) are set only on a generated repeat and say which wave of which origin it is; both null on anything a human created. There is **no route that creates a repeat** — the sweep does, on its own — and `createdBy` is null on every one of them
`AssignmentProgressDto`: `{userId, status, bestScore, attemptCount, firstOpenedAt, completedAt}`
`AssignmentReminderResultDto`: `{notifiedCount, scope}`

**`scope` on `POST /remind` (40.26), and the two failure modes it inherited.** `unfinished` is the
default and 40.23's behaviour — everybody who has not completed it, including the people under the
threshold. `not_started` is the set the day-before digest names, and it exists because that digest
carries the remind button: a notice listing five names whose button then messages twelve people is
the product doing something other than what it just said. The route now also **consults the live
roster before nudging anybody**, so it can answer 503 the way `POST /activate` does — a progress row
outlives employment on purpose, and reminding without checking means mailing an ex-employee their
former employer's homework. `notifiedCount` therefore counts people who both owe the work and still
work there.
`ActiveAssignmentDto`: `{id, title, goal, opensAt, deadline, completionRule, content: ActiveAssignmentItemDto[], status, bestScore, attemptCount, firstOpenedAt, completedAt}`
`ActiveAssignmentItemDto`: `{kind, reference, orderIndex, title, lessonId}` — `title` is null when the referenced content was archived after the assignment was issued, `lessonId` is set only for `lesson_version`. **No persona is ever in this DTO** — see below.
`AssignmentPracticeContextDto`: `{assignmentId, title, goal, personaName, personaPosition, personaPersonality, personaDifficulty}`

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

**`repeatSchedule` is optional and, since 40.24, checked against a closed vocabulary of exactly one
kind.** It is what turns one training into recurring practice; without it the product's central claim
is a slogan (`docs/TENANCY/ASSIGNMENTS.md` §2.1).

| `kind` | Shape | Means |
|---|---|---|
| `fixed_offsets` | `{"kind":"fixed_offsets","offsetDays":[7,21]}` | A shortened re-issue this many days after **the origin was issued** (`activatedAt`), once per offset. `offsetDays` may be omitted and then means exactly `[7, 21]` |

Anything else is a **400**: an unknown `kind`, a list that is empty, longer than 4, not ascending, or
holding an offset outside 1–180 days. A cron expression is deliberately not expressible — what is
being scheduled is the decay curve of one training session, which has no weekly rhythm to align to.

What a wave actually is, because none of it is a route:

- **A new assignment row**, created already `active` and linked to its origin by
  `repeatOfAssignmentId` + `repeatWaveIndex`. Configured once, then automatic — a draft awaiting a
  press would be a to-do item, which is what internal trainings die of. A repeat never carries a
  schedule of its own (the database refuses it), so a series is one level deep.
- **Issued to the origin's recipients**, intersected with the live roster — not to a fresh resolution
  of the audience rule, which three weeks later would hand a shortened refresher to everybody hired
  since and change the denominator between waves. Its own stored `audience` is therefore the resolved
  `{"kind":"users","userIds":[…]}`. Outcome does not filter it: whoever was asked is asked again,
  `failed_threshold` and `not_started` included.
- **Shortened, never easier.** `reference_material` items are dropped (kept when they are all the
  assignment has) and `dialog_score.requiredCount` is halved, rounded up, minimum one. The score bars
  are copied untouched — lowering one would make the two waves incomparable, which is the only thing
  the series is for. The deadline is the origin's *duration* re-based on issue time (floor of one
  day); an origin with no deadline repeats with none.
- **A closed origin still repeats.** The only way to cancel a series is to clear or shorten
  `repeatSchedule` with a `PUT` **while the assignment is still active** — it is deliberately not in
  the freeze set. Once closed, everything on the row is frozen and the remaining waves will fire.
- **A wave more than three days late is dropped**, not delivered: the value of spaced repetition is
  the spacing.

No new notification family: a repeat stages `assignment.issued` per recipient, exactly as a
human-pressed issue does.

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
employee list lives in identity-service, so the column stores the rule and never a copy of it.

**Issuing resolves the rule (40.23).** `POST /activate` asks identity-service for the organization's
active member ids, turns the rule into named people, writes one `not_started` progress row per
recipient and stages one `assignment.issued` notice per recipient — in one transaction, so "was
asked" and "was told" cannot diverge. From this block on, `GET /admin/assignments/:id/progress`
returns real rows and the summary's funnel counts are real numbers.

Three consequences worth knowing before calling these routes:

- **A named `userIds` list is intersected with the live roster.** Somebody who has left is dropped
  (with a log line, not a refusal — one leaver must not break every assignment that mentioned them),
  and a user id belonging to another organization is dropped outright. An audience that resolves to
  **nobody** is a `400`: a silently empty issue produces an active assignment whose funnel reads zero
  of zero, which on the screen is indistinguishable from a team that has not started.
- **`{"kind":"group"}` is a `400`**, not a silent widening to the whole team. Nothing in the platform
  defines a group yet; the kind is accepted structurally by the schema so a later block needs no
  migration.
- **`PUT` on an *active* assignment re-resolves the audience and tops up**, adding rows and notices
  for anybody new and never removing anybody. That is how a person hired after the issue joins work
  already running — nothing back-dates them automatically. A recipient who has since left keeps their
  row (it is the record that they were asked) but is not contacted again.

Both routes answer **`503`** when identity-service cannot be reached, with nothing written. That is
deliberately not a `500`: nothing is wrong with the request or the assignment, and the honest
instruction is "press it again".

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

**The learner's own screen (40.23).** `GET /assignments/active` returns the caller's unfinished
assignments — the query is over *their progress rows*, so an assignment nobody issued to them cannot
appear however active it is, and no second authorization gate is needed to make that true. Completed
ones drop off ("пока не выполнено"); `failed_threshold` stays, because the work is finished and the
bar was not met and hiding it would leave the person who most needs another attempt with no way back.
An assignment whose `opensAt` has not arrived is absent. `completionRule` comes back verbatim so the
screen can name the bar instead of a status word. **An empty array is the normal answer** and the
client must render nothing for it — the assignment strip is an addition to the home screen, never a
replacement for the skill tree.

**Nothing on the learner path writes.** There is no "mark as opened" route, deliberately: it would
make the read path a second writer of columns `AssignmentThresholdConsumer` owns, with a different
idea of what "started" means (a screen opened rather than graded work done).

**The practice conversation and its persona (40.23).** A `dialog_scenario` content item names an
ai-service dialog mode key, and the assignment's practice dialogue is an ordinary `POST
/dialog/sessions` on that mode. ai-service then calls
`GET /internal/assignments/practice-context` itself and injects the assignment's framing and persona
into the prompt through `AssignmentPracticePromptBuilder`. **The persona is never in a response the
browser sees and never accepted in a request body**: the client starting the session belongs to the
person being graded against that persona, and a persona they can send is one they can rewrite. The
lookup degrades to "no assignment" on any failure, so a practice screen never fails to open because
learning-service is down.

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

### The manager dashboard (Phase 40.25)

The screen a РОП actually opens: one assignment's funnel with named people behind it, the team's
skill heat map, the team's graded conversations, and the two-way review loop over a disputed grade.
Design: [TENANCY/ASSIGNMENTS.md](TENANCY/ASSIGNMENTS.md) §4 and §4.1.

**Gateway routing.** Before this block, `/assignments/*` and `/admin/assignments/*` had no gateway
route at all — the manager strip shipped in 40.23 could not reach learning-service through the
gateway. 40.25 adds `learning-assignments` (`/assignments/{**catch-all}`),
`learning-admin-assignments` / `learning-admin-assignments-root` (`/admin/assignments/{**catch-all}`
and the bare `/admin/assignments`), `learning-admin-team` (`/admin/team/{**catch-all}`),
`learning-admin-dialog-reviews` / `-root` (`/admin/dialog-reviews/{**catch-all}` and the bare path),
`learning-dialog-reviews` / `-root` (`/dialog-reviews/{**catch-all}` and the bare path), and
`ai-admin-dialog-sessions` / `-root` (`/admin/dialog-sessions/{**catch-all}` and the bare path).

#### Assignment dashboard (learning-service)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/assignments/:assignmentId/dashboard | — | `AssignmentDashboardDto`, 404 if the assignment does not exist |

`RequireOrgAdmin`, same gate as every other assignment route. It supersedes nothing —
`GET /admin/assignments/:assignmentId/progress` stays, because it is the raw, name-free list and the
only one of the two that cannot be affected by identity-service being down.

`AssignmentDashboardDto`: `{assignment: AssignmentSummaryDto, funnel: AssignmentFunnelDto, rows: AssignmentDashboardRowDto[], series: AssignmentWaveDto[], rosterKnown}`
`AssignmentFunnelDto`: `{assignedCount, notStartedCount, startedCount, completedCount, failedThresholdCount, leftOrganizationCount, assignedActiveCount}`
`AssignmentDashboardRowDto`: `{userId, displayName, status, bestScore, attemptCount, firstOpenedAt, completedAt, isActiveMember}`
`AssignmentWaveDto`: `{assignmentId, waveIndex, status, activatedAt, deadline, funnel: AssignmentFunnelDto}`

**The funnel is five counts, not four.** `failedThresholdCount` is not a subset of `completedCount` —
it is people who finished the measured work and stayed under the bar, the row the roadmap calls the
most valuable on the screen. `startedCount` is `in_progress` + `completed` + `failed_threshold`
together: a person who tried and failed has started.

**The roster counts are nullable, not zero, when identity-service could not be asked who still works
here.** `rosterKnown: false` means every `isActiveMember` and both `leftOrganizationCount` /
`assignedActiveCount` are `null`; the screen should say "could not check" rather than draw a zero. A
person who left keeps their progress row (40.23) and would otherwise read as `not_started` forever —
`isActiveMember` is how the screen stops counting them against a team that has not failed at
anything.

**`series` always has at least one entry — the assignment itself.** A single-shot assignment yields
exactly one wave (itself) rather than an empty list, so the screen has one shape instead of two.
`waveIndex` is `0` for the origin and the 1-based `repeatWaveIndex` for each repeat, so "wave 2" on the
screen matches the offset ordinal the РОП configured.

`displayName` on a row comes from `UserReplicas` and is nullable — learning-service does not own
identities, and someone who has never triggered a `user.updated` has no replica row yet. A missing
name is returned as `null`, never as an invented placeholder.

#### Team skill heat map (learning-service)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/team/skill-map?days= | — | `TeamSkillMapDto` |

`RequireOrgAdmin`. `days` (query, default the service's own window) bounds how far back attempts are
counted; team readiness is a statement about now, not about somebody's whole history.

`TeamSkillMapDto`: `{windowStart, stages: TeamSkillMapStageDto[], skills: TeamSkillMapSkillDto[], members: TeamSkillMapMemberDto[], unattributedAttemptCount, minimumAttemptsForAccuracy, rosterKnown}`
`TeamSkillMapStageDto`: `{key, label, accent, order, attemptCount, accuracyPercent}`
`TeamSkillMapSkillDto`: `{skillId, title, stageKey, orderInTree, attemptCount, accuracyPercent}`
`TeamSkillMapMemberDto`: `{userId, displayName, isActiveMember, attemptCount, accuracyPercent, weakestStageKey, weakestSkillId, dialogCount, dialogAverageScore, stages: TeamSkillMapCellDto[], skills: TeamSkillMapCellDto[]}`
`TeamSkillMapCellDto`: `{key, attemptCount, accuracyPercent}`

**`displayName` and `isActiveMember` on `TeamSkillMapMemberDto` are both nullable, same reason as the
assignment dashboard row above** — read from `UserReplicas`/the roster read, either of which can be
missing for one person without the whole response failing. `displayName` is `null`, never an invented
placeholder, for anyone without a replica row yet; the client types this field `string | null` and
resolves the fallback label itself (`useTeamMemberNames` in `use-team-directory.ts`) rather than
sorting or rendering the raw value — a client that treats it as always-a-string will crash the first
time replication lags behind a fresh signup. `isActiveMember` is `null` for **every** member at once
whenever `TryReadRosterAsync` fails to reach identity-service (`TeamSkillMapService.cs`) — it is not
one person's field going missing, it is the whole roster read failing — so the client types it
`boolean | null` too (`TeamSkillMapMember`/`TeamMemberName` in `use-team-directory.ts`) and every
consumer must check `=== false`, never `!isActiveMember`, and gate the "departed" label on
`rosterKnown`/`isRosterKnown` (`audience-picker.tsx`, mirroring `org-team/utils/team-roster.ts`).

**One endpoint, not two, because "per manager: where they sag, by funnel stage" and "per team: a
skill heat map" are the same matrix read along its two axes.** Splitting them would run the
aggregation twice and let the two screens disagree about the same window.

**`dialogCount`/`dialogAverageScore` count every `UserDialogScores` row for the person in the
window, unconditionally — a `Score` of `0` is a real grade ("a conversation the manager wrecked",
`DialogScoreScale.Minimum`'s own doc comment) and is never excluded from either number.** There is
no nullable "no score" state at this layer: the only way a conversation is absent from the count is
that no row exists for it at all (2026-08-20 audit, org heat map O-3 — `AssignmentThresholdConsumer`
used to silently skip writing the row for a `dialog.evaluated` event with no `ModeKey`, which starved
this endpoint of every mode-key-less conversation regardless of its score; it now writes the row with
an empty `DialogModeKey` instead, which `AssignmentThresholdEvaluator`'s exact-key matching still
correctly ignores).

**`accuracyPercent` is `null`, never `0`, below `minimumAttemptsForAccuracy`.** Two right answers out
of two is 100% and means nothing about anybody — the same call 40.22 made for withholding an accuracy
until every exercise in a set has been attempted. `weakestStageKey`/`weakestSkillId` are the
lowest-scoring cell that has enough attempts to report a number at all, computed server-side so every
consumer answers "where do they sag" the same way; both are `null` for somebody with no qualifying
cell, which is a different statement from "weak everywhere" and must not be drawn as one.
`unattributedAttemptCount` buckets attempts whose exercise no longer exists — folded nowhere, the same
call `unversionedAttempts` makes elsewhere in this document. The stage vocabulary is `Skill.Stage` /
`SkillStages`, the platform's existing one, not a second one invented for this screen.

#### From the heat map to content — the loop of Phase 40.31 (learning-service)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/team/skill-gaps?days= | — | `TeamSkillGapsDto` |
| POST | /admin/team/skill-gaps/:stageKey/content | — | `ContentGenerationJobDto`, 409 if the stage is not failing |
| POST | /admin/team/skill-gaps/:stageKey/dismiss | `{note?}` | `TeamSkillGapsDto` (recomputed), 409 if the stage is not failing |
| DELETE | /admin/team/skill-gaps/:stageKey/dismiss | — | 204, 404 if there was no refusal to take back |

`RequireOrgAdmin`, `[TenantTransaction]`. `days` is the same window parameter `skill-map` takes, and
the suggestion is derived from that very call — a red cell with no suggestion, or a suggestion for a
cell that is not red, would both be bugs the screen could not explain.

`TeamSkillGapsDto`: `{windowStart, minimumAttemptsForGap, maximumAccuracyPercentForGap, minimumStrugglingManagers, gaps: TeamSkillGapDto[], suppressed: SuppressedTeamSkillGapDto[], rosterKnown}`
`TeamSkillGapDto`: `{stageKey, stageLabel, sourceRef, attemptCount, accuracyPercent, strugglingManagerCount, measuredManagerCount, weakestSkills: TeamSkillGapSkillDto[], proposedTitle, proposedGoal}`
`TeamSkillGapSkillDto`: `{skillId, title, attemptCount, accuracyPercent}`
`SuppressedTeamSkillGapDto`: `{stageKey, stageLabel, attemptCount, accuracyPercent, reason, suppressedUntil, contentGenerationJobId}`

`reason` is one of `dismissed`, `run_in_progress`, `recently_addressed`.

**A gap is three conditions, and all three thresholds are echoed back.** At least
`minimumAttemptsForGap` attempts on the stage (20), accuracy at or below
`maximumAccuracyPercentForGap` (60), and at least `minimumStrugglingManagers` managers below that bar
(2). Echoed for the same reason `minimumAttemptsForAccuracy` is: a screen that must explain why the
reddest cell produced no suggestion needs the numbers that decided it, and a client that hard-codes
them a second time is how the two eventually disagree.

**`suppressed` is returned rather than filtered away.** A panel that silently shows nothing cannot be
told apart from a broken one, and «почему мне ничего не предлагают» is the question that gets a
feature switched off. `run_in_progress` carries the run's id, so the answer is a link.

**`sourceRef` is `skill-gap:<stage>@<yyyy-MM-dd>` and is assembled server-side.** It is what an
assignment born from this gap will carry, so it is written by the code that measured the gap and
never by the caller that asks for one. The observed numbers are not in it — they are in
`proposedGoal`, which becomes the assignment's `goal`.

**`POST …/content` is idempotent while a run for that stage is alive**: it returns that run instead
of starting a second one. A dismissal or a recent run suppresses the *offer* but does not forbid the
*act* — pressing the button on a dismissed gap clears the dismissal and proceeds, because suppression
governs what the panel proposes and not what the administrator may do. A stage that is not failing at
all is a 409 at any price. The run it starts is an ordinary 40.27 run: same checkpoint, same
sufficiency threshold, and the lesson it eventually produces arrives archived.

**The material is composed, not uploaded.** There is no textarea behind this button, so the run's
`sourceMaterial` is written deterministically from the measurement and the organization profile — the
same seven fields 40.19 renders and 40.29 interviews for. An organization with an empty profile gets a
run in `insufficient` with 40.28's own codes, which is the honest answer rather than a bug: we do not
know enough about that company to write exercises for them.

#### The team's graded conversations (ai-service)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/dialog-sessions?userId=&modeId=&maxScore=&limit= | — | `AdminDialogSessionSummaryDto[]`, newest first |
| GET | /admin/dialog-sessions/:sessionId | — | `AdminDialogTranscriptDto`, 404 if unknown |

`RequireOrgAdmin`. `limit` defaults to 25. `maxScore` is the parameter that makes the list useful —
"show me conversations scored 4 and below" is a list a РОП can act on, "show me every conversation" is
not.

`AdminDialogSessionSummaryDto`: `{id, userId, bundleId, modeId, modeKey, modeTitle, status, messageCount, score, feedbackSummary, assignmentId, createdAt, completedAt}`
`AdminDialogTranscriptDto`: `{id, userId, bundleId, modeId, modeKey, modeTitle, status, score, feedback, assignmentId, createdAt, completedAt, messages: AdminDialogTranscriptMessageDto[]}`
`AdminDialogTranscriptMessageDto`: `{index, role, content, timestamp}`

**This lives in ai-service, not on the dashboard response, because the conversations are Mongo
documents `IDialogSessionRepository` alone holds** — Mongo has no row-level security, so a filter
spread over two services is a filter that will be forgotten in one of them. The screen asks
learning-service for the funnel and ai-service for the words; neither service reads the other's
store. `message.Index` is part of the contract (not left implicit in array order) because a coaching
note quotes a session id **and** a message index, and the same line said twice must still be citable.
Names are absent from both DTOs — ai-service holds no user replica, and the screen already has the
team's names from the heat map.

#### The review loop (learning-service)

The РОП selects a fragment of a graded conversation and comments on it; the manager reads it and may
dispute the grade. One table, `DialogReviewNotes` — see [DB_SCHEMA.md](DB_SCHEMA.md) — for both
directions, because a coaching note and a score dispute share every field except who may close the row
and with which word.

Admin side (`RequireOrgAdmin`):

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/dialog-reviews?kind=&status=&sessionId= | — | `DialogReviewNoteDto[]`, 400 on a malformed filter |
| POST | /admin/dialog-reviews | `CreateCoachingNoteRequestDto` | `DialogReviewNoteDto`, 400 on a validation failure |
| POST | /admin/dialog-reviews/:noteId/resolve | `ResolveScoreDisputeRequestDto` | `DialogReviewNoteDto`, 400 on a validation failure, 404 if unknown |

Manager side (`[Authorize]`, no admin gate, takes no user id — the caller is the token):

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /dialog-reviews | — | `DialogReviewNoteDto[]`, newest first — coaching notes addressed to the caller and disputes they filed, in one list |
| POST | /dialog-reviews/disputes | `CreateScoreDisputeRequestDto` | `DialogReviewNoteDto`, 400 on a validation failure |
| POST | /dialog-reviews/:noteId/acknowledge | — | `DialogReviewNoteDto`, 404 if unknown or not the caller's |

`DialogReviewNoteDto`: `{id, kind, status, sessionId, dialogModeKey, subjectUserId, subjectDisplayName, authorUserId, authorDisplayName, quotedFromMessageIndex, quotedToMessageIndex, quotedText, comment, disputedScore, resolution, adjustedScore, resolvedBy, resolvedAt, createdAt, updatedAt}`
`CreateCoachingNoteRequestDto`: `{sessionId, quotedFromMessageIndex?, quotedToMessageIndex?, quotedText, comment}` — `sessionId`, `quotedText` and `comment` are required; a coaching note whose whole content is "messages 4 to 6" cannot be re-read next month
`CreateScoreDisputeRequestDto`: `{sessionId, quotedFromMessageIndex?, quotedToMessageIndex?, quotedText?, comment}` — `sessionId` and `comment` are required, the quote is not: the manager is usually arguing about the conversation as a whole
`ResolveScoreDisputeRequestDto`: `{outcome, resolution?, adjustedScore?}` — `outcome` is `upheld` or `rejected` and required; `resolution` is required when `outcome` is `rejected` (closing a complaint in silence turns the mechanism into a rubber stamp); `adjustedScore` (0–100) is accepted only when `outcome` is `upheld`

**The organization, the manager, the scenario and the grade are never taken from the request body.**
`CreateCoachingNoteRequestDto`/`CreateScoreDisputeRequestDto` name only a `sessionId`; the subject,
`dialogModeKey` and `disputedScore` are read off that session's `UserDialogScores` row inside the
caller's organization, so a hand-written body cannot address a note at somebody else's employee. A
session with no recorded score cannot be annotated at all (400) — an ungraded conversation has no
grade to dispute and is not on the screen the fragment was selected from.

**`adjustedScore` is recorded, never applied.** It does not change `UserDialogScores` or any
assignment verdict — 40.22 made every progress number derived from attempt rows and recomputed on
every event, and a hand-edited score would both be overwritten by the next redelivery and make the
completion threshold negotiable by the person being measured. Retro-scoring is a decision the owner
has not made (docs/DONT_FORGET.md).

**At most one open dispute per conversation.** A second `POST /dialog-reviews/disputes` on a session
that already has an open dispute is refused (400) by the service before it ever inserts, backed by the
`UX_DialogReviewNotes_OpenDisputePerSession` partial unique index at the database as the final
guarantee. A session may be disputed again after a verdict, because a person told "the grade stands"
who later finds new evidence is not spamming the queue.

Both admin write routes 403 (`Forbid()`) when the caller satisfies `RequireOrgAdmin` without holding
an organization in context (platform staff impersonating nobody) — same shape as
`AdminAssignmentsController`: a review row belongs to one organization, and with none in context the
save guard would otherwise throw and surface a 500 describing an internal invariant.

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

`AdminReferenceMaterialDto`: `{id, skillId, skillTitle, title, markdownContent, sortOrder, category, tags: string[]}` — there is no `skillSlug`; the record carries the skill's title, not its slug.

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

`skill` query param filters by `Skills.IconicName` (same convention as the public route), and matches only `PrimarySkillId` — unlike the public `GET /techniques`/`GET /techniques/meta`, it does **not** also match `AdditionalSkills` (docs/DECISIONS.md, AD-3).
`GET /admin/techniques/export` returns every technique (ignores `skill`/`search` filters) shaped exactly like the `import` request body, so an export file feeds straight back into `POST /admin/techniques/import`. UI: "Export JSON" button on `/admin/techniques`.

**AD-3 skill linking (2026-08-21):** no new endpoint. `/admin/techniques`' per-row skill quick-editor and its "select rows → assign primary skill to N" bulk toolbar both reuse `PUT /admin/techniques/:id` — the quick-editor sends the technique's current full write body with only `primarySkillId` overridden; the bulk toolbar does the same, once per selected technique, via `Promise.allSettled` (`useBulkAssignTechniqueSkill` in `features/admin/hooks/use-admin.ts`) so one failing row is reported as a failure rather than hidden by the others' success. `additionalSkillIds` still has no UI control anywhere (docs/DECISIONS.md).

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
> shapes are unchanged. `AdminUserDetailDto` used to also carry activity stats
> (streaks/XP/skills/score) sourced from the same identity-service method that hard-codes them to
> `0` for `GET /profile` — so the admin "Activity" card reported "Skills 0/0" for every user even
> though the system has real skills and completions (2026-08-21 admin audit, AD-1). Consistent with
> the `GET /profile` fix (see below), those fields were dropped from the response rather than wired
> to invented data; the admin "Activity" card was already removed from the UI for the same reason.
> Lists/manages users platform-wide (not scoped to one
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
`AdminUserDetailDto`: `AdminUserDto` + `{persona}`

Reading the roster and a user's detail is open to both platform staff roles. Renaming, avatar moderation and role changes all mutate a user and are `RequireSuperAdmin`-only, so a platform `Admin` sees the modal read-only. `DELETE /admin/users/:id/avatar` reuses the avatar reset flow (deletes the uploaded S3 object and falls back to the default avatar).

### Daily Quotes

Platform-only (`RequirePlatformAdmin`) — the quote of the day is one shared editorial calendar, not
per-organization content. The learner-facing read is `GET /daily-quote` (any authenticated role,
documented above).

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/daily-quotes?from=&to= | — | `AdminDailyQuoteDto[]`, ordered by date; both bounds optional and inclusive |
| POST | /admin/daily-quotes | `{date, text, author?}` | `AdminDailyQuoteDto` |
| PUT | /admin/daily-quotes/:id | `{date, text, author?}` | `AdminDailyQuoteDto` (404 if missing) |
| DELETE | /admin/daily-quotes/:id | — | 204 (404 if missing) |

`AdminDailyQuoteDto`: `{id, date, text, author, createdAt, updatedAt}`. `date` is a plain calendar
date (`DateOnly`), not a timestamp. `author` is optional on write and stored as `""` when omitted,
never null.

Both writes validate the same two things: blank `text` → `400`, and a **second quote for a date that
already has one → `409 Conflict`**. One date carries at most one quote, which is what lets the
learner-facing read fall back to "the most recent quote at or before today" without having to choose
between candidates.

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
> by Learning to grade AI exercise types. `Skills` are owned by Learning, so
> `DialogBundleDto.skillSlug`/`skillTitle` are resolved by calling learning-service's
> `GET /internal/skills/lookup` (shares `InternalServiceAuthFilter` with the other internal
> endpoints; degrades to an empty slug/title per bundle, never fails the bundle list, if that
> call is unreachable — docs/AUDIT_CONTRACTS.md finding C-3).
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

Service-to-service (no JWT; `X-Internal-Service-Secret`, **not routed through the gateway**,
no `[TenantScoped]` — skills are global content):

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /internal/skills/lookup *(learning-service)* | — | `SkillLookupDto[]` — `{id, iconicName, title}` for every skill; called by ai-service to resolve `DialogBundleDto.skillSlug`/`skillTitle` (docs/AUDIT_CONTRACTS.md finding C-3) |

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
| DELETE | /dialog/sessions/:sessionId | — | `204`; `404` when the session does not exist or does not belong to the caller |

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
| GET | /admin/voice/usage | — | `AdminVoiceUsageDto` (`RequirePlatformAdmin` — revised 2026-08-16). **Phase 40.11: scoped to the caller's organization**, not the whole installation — a platform superadmin sees another organization's numbers by impersonating into it (40.9). **Phase 40.33 added four fields** — `organizationDailyLimitSeconds`, `organizationMonthlyLimitSeconds`, `organizationUsedSecondsToday`, `organizationUsedSecondsThisMonth` — because the per-user rows answered «кто много говорит» and never answered «сколько осталось у компании». Existing fields unchanged. |

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
**and** `ReadinessNoFeedbackUntil` on the company so the next `GET /companies/{id}/readiness`
regenerates from the fresh session list instead of being held back by a stale negative cache. There
is no other path in company-service that marks a practice call complete.

> There is no standalone `GET /readiness` endpoint anywhere in this API — every reference to
> "readiness" above is short for `GET /companies/{id}/readiness`. The bare path `/readiness` is not
> routed by the gateway, has no controller, and is not called by the frontend; it is easy to
> misread the shorthand above as a distinct route, which is why this note exists (found during the
> 2026-08-20 production audit, docs/TESTING/PROD_AUDIT_2026_08_20.md). The `/readyz` health-check
> endpoint (per-service dependency readiness for infra probes, not exposed through the gateway) is
> unrelated and documented in docs/MONITORING.md, not here.

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

**All six routes on this controller** are `[TenantScoped]` — the two above plus the four Phase 40.29
interview routes documented further down (`GET /organizations/profile/gaps`, `PATCH`,
`POST …/draft`, `POST …/draft/apply`). `TenantContextMiddleware` rejects the request with `403` if
the gateway-validated `X-Organization-Id` header is absent — there is no organization id anywhere in
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

### Organization profile as an interview (Phase 40.29, tenant-scoped)

Four routes on the same row, and they exist because nobody fills in a thirty-field form. An empty
profile is not a cosmetic problem — it is the state in which 40.19's substitution does nothing at all,
so every lesson in the product reads as the neutral fallback. See
[ORGANIZATION_SERVICE.md](ORGANIZATION_SERVICE.md#the-profile-as-an-interview-phase-4029).

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /organizations/profile/gaps?limit=3 | — | `OrganizationProfileGapsDto` |
| PATCH | /organizations/profile | `PatchOrganizationProfileRequestDto` | `OrganizationProfileDto` |
| POST | /organizations/profile/draft | `ExtractedProfileDraftDto` | `OrganizationProfileDraftPreviewDto` |
| POST | /organizations/profile/draft/apply | `ApplyOrganizationProfileDraftRequestDto` | `OrganizationProfileDraftAppliedDto` |

**Authorization.** `GET …/gaps` and `POST …/draft` are `[Authorize]` only, like `GET`: neither writes
anything. `PATCH` and `POST …/draft/apply` additionally require `RequireOrgAdmin` — they are the two
routes on this controller that can change what every lesson in the organization says. **The plain
`PUT` carries `RequireOrgAdmin` too** — 40.34 closed the hole this paragraph used to describe as
open, because a member who could replace all seven columns could empty `banned_claims` and make the
organization's AI coach its reps into the exact promises compliance forbade. Verified in
`OrganizationProfileController`: `GET` and `GET …/gaps` and `POST …/draft` are `[Authorize]` only;
`PUT`, `PATCH` and `POST …/draft/apply` are `RequireOrgAdmin`.

- `OrganizationProfileGapsDto`: `{questions: [{code, question, priority}], totalGapCount, blockingGapCount, isReadyForParameterization}`.
  Codes: `product`, `icp`, `objections`, `script_stages`, `tone`, `banned_claims`, `glossary` — a
  closed list, in asking order. `priority` is `blocking` / `important` / `optional`; **`blocking` means
  `{{organization.*}}` renders the fallback until it is answered**, and `isReadyForParameterization`
  is exactly `blockingGapCount == 0`. `questions` is capped (default 3, maximum 7) and
  `totalGapCount` is not, so a screen can show three and still say «осталось ещё 4». A gap is
  reported for fewer than 3 objections and fewer than 3 script stages, not merely for zero.
  **Never 404**: an organization that has never saved a profile gets all seven questions.
- `PatchOrganizationProfileRequestDto`: the `PUT` body with every field optional. **An omitted field
  keeps its stored value.** There is no way to *clear* a field here — `null` already means «не
  отвечал» — so clearing stays on the whole-row `PUT`.
- `ExtractedProfileDraftDto`: `{product?, icp?, tone?, objections?: [{text, bestResponse?}], scriptStages?: string[], glossary?: {[term]: string}, bannedClaims?: string[]}` —
  learning-service's `ContentStructureDto`, field for field, redeclared rather than shared (same rule
  as `MaterialGapCodes`). **There is no `jobId`:** the caller reads the structure off
  `GET /admin/content-generation/{jobId}` and posts it here, so organization-service stays the only
  writer of the profile. That adds no authority — the same administrator can already `PUT` an
  arbitrary structure onto the run and an arbitrary profile onto this row.
- `OrganizationProfileDraftPreviewDto`: `{fields: [{field, decision, currentValue?, suggestedValue?, addedItemCount}], conflictCount, gapsAfterApply}`.
  Writes nothing. `decision` is `unchanged` / `fill` / `conflict` / `extend`. The preview is planned
  with **every** overwritable field accepted, because its job is to show the most the draft could do.
- `ApplyOrganizationProfileDraftRequestDto`: `{draft, acceptedFields?: string[]}`. `acceptedFields`
  accepts only `product`, `icp`, `tone`, `script_stages`; anything else is dropped silently. Omitting
  it is the safe default and the expected case. `400` if `draft` is missing.
- `OrganizationProfileDraftAppliedDto`: `{profile, appliedFields, gaps}` — the write and the next
  round of the interview in one response, so the screen does not need a second round trip between
  «ИИ заполнил профиль» and «остался один вопрос».

**The merge policy, which is the contract that matters:**

| Field | What apply does | Consent needed |
|---|---|---|
| `product`, `icp`, `tone`, `scriptStages` | fills if empty; **keeps the existing value** if not | yes — name it in `acceptedFields` |
| `objections` | union by text (case-insensitive); an existing entry wins, keeping its `frequency` | no — nothing is lost |
| `glossary` | adds unknown terms; an existing term keeps its definition | no |
| `bannedClaims` | union, add-only | **impossible** — no `acceptedFields` value can delete one |

`POST …/draft/apply` publishes `organization.profile.updated` like every other save, so the 40.19
replicas learn about a promoted draft the same way they learn about a form submission.

### Demo requests (lead capture — NOT tenant-scoped)

> Owned by **organization-service**, alongside the tenant registry rather than in identity-service:
> a lead has no user, no organization and no membership, so it cannot be `ITenantScoped` and its
> bounded context is "prospective tenant", not "user". Rationale and the anti-spam design:
> [DEMO_REQUEST.md](DEMO_REQUEST.md).

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /demo-requests `[public]` | `CreateDemoRequestRequestDto` | `202 DemoRequestAcceptedDto`, `400` validation, `429` `{message, retryAfterSeconds}` + `Retry-After` |
| GET | /admin/demo-requests `[RequirePlatformAdmin]` | — | `DemoRequestDto[]`, newest first |
| PATCH | /admin/demo-requests/{id}/status `[RequirePlatformAdmin]` | `{status}` | `DemoRequestDto` or `404` |
| POST | /admin/demo-requests/{id}/provision `[RequireSuperAdmin]` | `ProvisionDemoRequestRequestDto` (all optional) | see "Provisioning" below |

`CreateDemoRequestRequestDto`: `{fullName, workEmail, phone, companyName, jobTitle?, salesTeamSize, comment?, consentGiven, marketingConsentGiven, website?}`
`DemoRequestAcceptedDto`: `{id, submittedAt}`
`DemoRequestDto`: `{id, fullName, workEmail, phone, companyName, jobTitle?, salesTeamSize, comment?, status, consentGivenAt, marketingConsentGivenAt?, createdAt, updatedAt, organizationId?, organizationName?, organizationSlug?, provisioningState, bootstrapInviteId?, bootstrapAdminEmail?, provisionedAt?}` — the trailing fields report provisioning progress (below); `provisioningState` is `NotProvisioned | OrganizationCreated | AdminInvited`.

`organizationName` / `organizationSlug` are resolved by a join inside organization-service, which owns
both the lead and the registry — they are null until the lead is provisioned. The **invite's expiry is
deliberately not here**: `Invite` belongs to identity-service, so putting it on a list endpoint would
mean either a cross-service read per row or a replica of another service's table. It is returned once,
by the provision call that creates it.

**`phone` is required** (owner decision, 2026-08-20 — was optional). Not needed to *reply* —
`workEmail` already covers that — but required because the sales motion this form feeds is
phone-first: a business decision, not a technical one. See docs/DECISIONS.md.

`salesTeamSize` is `UpToFive | SixToTwenty | TwentyOneToFifty | FiftyOneToTwoHundred | MoreThanTwoHundred`
and `status` is `New | Contacted | Approved | Declined` (`Approved`, not `Qualified` — renamed
2026-08-20, docs/DECISIONS.md) — both are enum **names** on the wire and are stored as those names,
so the Russian labels live only in the frontend (docs/LOCALIZATION.md).

**`salesTeamSize` is required and nullable in the DTO on purpose.** `[Required]` on a non-nullable
enum has nothing to reject, so an omitted field would bind to the zero member and record `UpToFive` as
though somebody had chosen it. Omitting it is a `400`.

**Two consents, not one.** `consentGiven` (data processing) is required and must be `true`;
`marketingConsentGiven` is a separate optional field, and `false` is a perfectly valid answer.
152-ФЗ/GDPR treat them as distinct purposes, and one bundled checkbox would force a visitor to accept
marketing email to get a demo. Both are stored as timestamps (`consentGivenAt`,
`marketingConsentGivenAt`), never as booleans — a boolean records that a box was ticked, a timestamp
records when.

**`website` is a honeypot.** It is a hidden field no human fills. A non-empty value persists nothing
and sends no email, but still answers `202` with a freshly minted id — the response is deliberately
indistinguishable from a real submission, because a bot that can tell the difference simply stops
filling the field.

`429` is a **per-email** cooldown (`DemoRequests:SubmissionCooldownSeconds`, default 300), not a
general rate limit — there is no rate-limiting middleware in this backend and none was added for one
marketing form.

**The submitter now receives email too (2026-08-20, reversing the original "never mail the
submitter" decision — docs/DECISIONS.md).** A `202` sends the unchanged internal notification plus
a «Спасибо, что выбрали Sellevate» acknowledgement to `workEmail`. A `PATCH …/status` that actually
transitions a lead into `Approved` — never a re-patch of an already-`Approved` lead — sends a
«Заявку одобрили» notification to `workEmail`. That email carries **no link** (changed alongside
provisioning, below): it used to point at `{Frontend:Url}/register`, which stranded the recipient
on the awaiting-organization gate, since `/register` creates an identity with no membership. It now
only says the request is approved and a workspace invitation will follow. All three sends are
wrapped and logged, never surfaced as an error: an unconfigured internal inbox or a MailerSend
failure on any of them still returns the normal response with the lead (or status change) persisted.
The honeypot and the per-email cooldown are now the only two things limiting how often this
anonymous endpoint can be made to mail a third party.

### Provisioning — `POST /admin/demo-requests/{id}/provision`

`RequireSuperAdmin`, not `RequirePlatformAdmin` — provisioning creates a membership, which
`AuthorizationPolicies` reserves for a superadmin at either the platform or the organization level.
Creates the organization and sends the bootstrap invite to its first administrator, in one call.
Full design and the write-order safety property: docs/DEMO_REQUEST.md, "How provisioning is
actually written". Allowed from any status; sets `Status = Approved` itself.

`ProvisionDemoRequestRequestDto`: `{organizationName?, slug?, adminEmail?, role?}` — every field
optional, defaulting respectively to `CompanyName`, a normalized form of the name, `WorkEmail`, and
`TenancySuperAdmin`.

| Code | Body |
|---|---|
| `200` | `{demoRequestId, status, provisioningState, organization: {id, name, slug}, inviteId, inviteEmail, inviteExpiresAt, alreadyProvisioned}` |
| `404` | lead not found |
| `409` | `{code: "slug-taken", slug, message}` |
| `409` | `{code: "organization-has-admin", organizationId}` |
| `503` | `{code: "invite-failed", organizationId, provisioningState: "OrganizationCreated"}` |
| `400` | `{message}` — bad role or bad email |

**`200` — never a fresh `409` — on a lead that is already fully provisioned** (`alreadyProvisioned:
true`), because this is a UI button and a double-click must not look like an error. On that path
`inviteExpiresAt` is `null`: that timestamp is never stored on `DemoRequest`, and re-asking
identity-service to answer a call defined to have no side effects would defeat the point of the
fast path.

`organization.created` is published exactly once across a first attempt, a retry after a `503`, and
an already-provisioned call — see docs/DEMO_REQUEST.md for exactly which step publishes it.

---

## Admin content pipeline (Phases 40.27–40.28, learning-service)

The РОП's «структурировать → **остановиться** → сгенерировать» run. Every route is
`RequireOrgAdmin` and carries `[TenantTransaction]`; the organization comes from the
gateway-validated header and appears in no route, query string or body
([TENANCY.md §1.3](TENANCY/TENANCY.md)). Gateway cluster `learning`, routes
`/admin/content-generation` and `/admin/content-generation/{**catch-all}`.

Full description of the pipeline and why the stop is the whole feature:
[CONTENT_PIPELINE.md](CONTENT_PIPELINE.md).

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/content-generation?status= | — | `ContentGenerationJobSummaryDto[]`, newest first |
| GET | /admin/content-generation/{jobId} | — | `ContentGenerationJobDto` or `404` |
| POST | /admin/content-generation | `{title, material}` | `201` + `ContentGenerationJobDto` (status `structuring`, or **`insufficient`** — 40.28) |
| PUT | /admin/content-generation/{jobId}/structure | `ContentStructureDto` | `ContentGenerationJobDto`, `409` outside `awaiting_review` / `insufficient` |
| POST | /admin/content-generation/{jobId}/material | `{material}` | **40.28** — `ContentGenerationJobDto`, `409` unless the run is `insufficient` |
| POST | /admin/content-generation/{jobId}/approve | — | `ContentGenerationJobDto` (status `generating`), `409` + `insufficiency` if the structure is too thin |
| POST | /admin/content-generation/{jobId}/retry | — | `ContentGenerationJobDto`, `409` unless the run failed |

```jsonc
// ContentStructureDto — the artifact at the checkpoint. Field for field the organization
// profile of CONTENT_MODEL.md §3, and deliberately a separate draft (DECISIONS.md, 2026-08-18).
{
  "product": "…",                                    // or null
  "icp": "…",                                        // or null
  "tone": "…",                                       // or null
  "objections":   [{ "text": "Дорого", "bestResponse": "…" }],   // ≤ 10
  "scriptStages": ["Приветствие", "Выявление потребности"],      // ≤ 12
  "glossary":     { "СДЭК": "…" },                               // ≤ 30 terms
  "bannedClaims": ["гарантированная доходность"]                 // ≤ 20
}

// ContentGenerationJobDto
{
  "id": "…", "title": "…", "status": "awaiting_review",
  "gapSourceRef": null,                              // 40.31 — "skill-gap:<stage>@<yyyy-MM-dd>" when
                                                     // the dashboard started this run, else null
  "sourceMaterial": "…",
  "structure": { /* ContentStructureDto */ },        // null until structuring returns
  "insufficiency": null,                             // 40.28 — non-null iff status is "insufficient"
  "structuredAt": "…", "approvedAt": null,
  "producedLessonId": null, "producedLessonVersionId": null,
  "producedExerciseCount": 0, "generatedAt": null,
  "failureReason": null,
  "createdAt": "…", "updatedAt": "…"
}

// ContentInsufficiencyDto (Phase 40.28) — the refusal, as a list the screen can render as
// bullets. `code` is what the UI keys off; `message` is what the РОП reads.
{
  "stage": "structure",                              // or "material" — see below
  "gaps": [
    { "code": "no_objections",
      "message": "В материале нет ни одного возражения клиента. Добавьте примеры возражений, которые менеджеры слышат чаще всего, или запись звонка, где они звучат." }
  ],
  "note": "…"                                        // the model's own reasoning, diagnostic only
}
```

`code` is one of a closed vocabulary — `off_topic`, `too_short`, `no_product`, `no_icp`,
`no_objections`, `no_script`, `no_examples` — and a code outside it is dropped rather than shown. The
sentence per code is fixed and written on the server, never by the model: a model-authored refusal
is a different sentence every run, is untranslatable, and occasionally demands something the product
cannot accept.

`ContentGenerationJobSummaryDto` carries `insufficiency` too, unlike the material and the structure:
it is the reason the run is sitting there, and a list that shows `insufficient` without saying what
is missing sends the administrator into a detail screen for every refused run. It also carries
`gapSourceRef` (40.31), so a list of runs distinguishes the ones a person started from the ones the
dashboard proposed.

**There is a second door into this pipeline since 40.31**, and it is
`POST /admin/team/skill-gaps/{stageKey}/content`. It creates an ordinary run — same six states, same
worker, same checkpoint, same sufficiency threshold — with two differences: its `sourceMaterial` is
composed from the measurement and the organization profile rather than pasted, and it carries
`gapSourceRef`. Nothing in the pipeline branches on that column; it is read by the suggestion panel
(to stop offering a stage somebody is already working on) and copied by `POST /admin/assignments`
(to give the resulting assignment its provenance).

Behaviour a caller can observe, and each of these is a decision:

- **The two long halves are asynchronous.** `POST` returns immediately with status `structuring`;
  a background sweep makes the LLM call and moves the run to `awaiting_review`. The screen polls
  `GET /admin/content-generation/{jobId}`. Same for `approve` → `generating` → `completed`.
- **`approve` is idempotent by state.** Approving a run that is already `generating` or `completed`
  returns it unchanged rather than re-queueing it — a double-clicked button must not buy two lessons.
  Approving a `structuring` or `failed` run is `409`.
- **`400` on start** only when `material` is empty or over 60 000 characters, or `title` is blank.
  **Thin material is not a `400` (40.28)** — it is a run in the `insufficient` state carrying
  `insufficiency`. A `400` would make the РОП start over and re-pay for structuring the deck they
  already uploaded, and «добавьте примеры возражений или запись звонка» is worth more to them than
  the error was.
- **The threshold has two stages** (40.28). `stage: "material"` means it was decided from the text
  itself, before anything was sent to a model — under ~400 characters or 60 words, or not a single
  word in the whole document that belongs to selling. `stage: "structure"` means the material was
  read properly and what came back was too thin to build four good exercises from: no objections
  *and* no script stages, or no product *and* no ICP. The model's own verdict rides the same
  structuring call and can **add** a refusal (it is the only judge that recognises a recipe
  mentioning a price) but never lift one.
- **A refusal is arguable, and arguing with it is cheap.** `POST …/material` appends text and puts
  the run back to `structuring`; the next call reads only what was added, alongside the structure
  already extracted. `PUT …/structure` is also open on a refused run, so somebody who knows the four
  objections may simply type them — the edited structure is re-inspected, and an edit that leaves it
  just as empty leaves the run refused.
- **`409` on approve** when the structure is too thin, with `{message, insufficiency}`. The run is
  moved to `insufficient` *before* the error is returned, so a screen that polls sees the same list
  without having caught anything.
- **Every value in a structure is bounded** at 2000 characters and every list is capped, on write and
  on read, matching the 40.19 render path's caps.
- **The produced lesson arrives archived.** `producedLessonId` names a real `Lesson` with real
  `Exercise` rows and a published `LessonVersion`, owned by the caller's organization, invisible to
  learners until `PUT /admin/lessons/{id}` sends `isArchived: false`. Per-item accept/reject of
  generated exercises is roadmap 40.32.

**`PUT /admin/lessons/{id}` gained an optional `isArchived` in this block.** Omitted, it leaves the
flag alone, so existing callers are unaffected. It exists because archiving previously had no reverse
and a generated lesson would otherwise be stranded.

### Internal (ai-service, not exposed through the gateway)

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /ai/content/structure | `{material, knownStructure?}` | `ExtractedContentStructureDto` |
| POST | /ai/content/generate | `{structure, focus?, maximumExerciseCount}` | `{title, exercises: [{type, content}]}` |
| POST | /ai/content/rewrite | **40.32** — `{exerciseType, content, profile?}` | `{content, summary}` — `content` is **null** when nothing needed changing |
| POST | /ai/content/review | **40.32** — `{exerciseType, content, profile?}` | `{findings: [{code, detail}]}` — an empty list is the expected answer |

Both are guarded by `InternalServiceAuthFilter` (`X-Internal-Service-Secret`) exactly like
`POST /ai/evaluate`, and both are stateless — no organization, no database, no job. `503` on any
provider failure, `400` on a missing or oversized `material`.

**The material is deliberately absent from `/ai/content/generate`.** That is the token saving the
roadmap asks for, and it is what makes the human's edit at the checkpoint binding rather than
advisory: a model that could still see the source would keep re-finding the objection the reviewer
deleted.


---

## Batch content adaptation and AI content review (Phase 40.32, learning-service)

«Перепиши все упражнения этапа "закрытие" под наш продукт и тон» → a background batch → a queue of
proposals → **accept or reject one at a time**. The same routes serve the block's second half with
`mode: "quality_review"`: a per-exercise report of what is methodically wrong with content the РОП
wrote by hand.

Every route is `RequireOrgAdmin` and carries `[TenantTransaction]`; the organization comes from the
gateway-validated header and appears in no route, query string or body
([TENANCY.md §1.3](TENANCY/TENANCY.md)). Gateway cluster `learning`, route
`/admin/content/{**catch-all}` — **added in this block, and it is also what finally makes 40.18's
`/admin/content/overrides` reachable through the gateway** (DECISIONS.md, 2026-08-18).

Full description: [CONTENT_PIPELINE.md](CONTENT_PIPELINE.md) §6a.

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/content/adaptations?mode=&status= | — | `ContentAdaptationJobSummaryDto[]`, newest first |
| GET | /admin/content/adaptations/{jobId} | — | `{summary, items[]}` or `404` |
| GET | /admin/content/adaptations/{jobId}/items/{itemId} | — | `ContentAdaptationItemDto` or `404` |
| POST | /admin/content/adaptations | `{mode?, stageKey}` | `201` + `{summary, items[]}`; `400` on an unknown mode, an empty stage or a stage above the per-batch ceiling; `409` when a live batch for that stage and mode already exists |
| POST | /admin/content/adaptations/{jobId}/items/{itemId}/accept | — | `ContentAdaptationItemDto`; `409` if the item is not `proposed`, if the exercise changed since the proposal, or if the batch is a **quality review** |
| POST | /admin/content/adaptations/{jobId}/items/{itemId}/reject | — | `ContentAdaptationItemDto`; `409` unless the item is `proposed` |
| POST | /admin/content/adaptations/{jobId}/retry | — | `{summary, items[]}`; `409` when nothing failed |

**There is no bulk verb, and that is the block.** Accept and reject each take an item id, so the
smallest thing that can be applied is one exercise and applying it took a human click. «Применить
всё» is auto-apply with a person's name attached; if sixty decisions is too many, the answer is a
narrower stage, which is what the `MaximumItemsPerJob` ceiling (60) exists to force.

```jsonc
// ContentAdaptationJobSummaryDto
{
  "id": "…",
  "mode": "tone_rewrite",          // or "quality_review"
  "stageKey": "closing",           // a Skill.Stage value
  "status": "awaiting_review",     // preparing | awaiting_review | completed | failed
                                   // derived from the items, never counted
  "itemCount": 23,                 // frozen at creation — the denominator of «сделано N из M»
  "pendingCount": 0,               // still owed an AI call; the part that still costs money
  "awaitingReviewCount": 9,        // still owed a HUMAN answer — the number the screen is about
  "acceptedCount": 11, "rejectedCount": 2, "unchangedCount": 1, "failedCount": 0,
  "failureReason": null,
  "createdAt": "…", "updatedAt": "…", "completedAt": null
}

// ContentAdaptationItemDto — one item, with everything needed to answer yes or no
{
  "summary": {
    "id": "…", "exerciseId": "…", "lessonId": "…", "lessonTitle": "Работа с ценой",
    "exerciseType": "choose_option", "orderInLesson": 3,
    "status": "proposed",          // pending | proposed | unchanged | accepted | rejected | failed
    "changeSummary": "Заменил абстрактную выгоду на ваш срок внедрения и тон на «вы».",
    "findingCount": 0,             // review mode
    "hasBlockingFinding": false,   // review mode — a defect that makes the exercise harmful, not weak
    "changedFieldCount": 4,
    "failureReason": null, "resolvedAt": null
  },
  "currentContent":  { /* the exercise body as it stands right now */ },
  "proposedContent": { /* the rewritten body; null in review mode */ },
  "changes": [                     // the machine-readable half of the diff — an enumeration, not a merge
    { "path": "situation",       "before": "Клиент говорит…", "after": "Клиент говорит…" },
    { "path": "options[1].text", "before": "…",               "after": "…" }
  ],
  "findings": [                    // review mode; empty in rewrite mode
    { "code": "unmeasurable_criteria", "severity": "blocking",
      "message": "Критерии оценки свободного ответа нельзя проверить…",
      "detail": "\"ответил вежливо\"" }
  ],
  "isStale": false                 // the exercise has been edited since the proposal was computed;
                                   // computed on read, never stored. A stale item cannot be accepted
}
```

**Nothing is merged, deliberately.** Both documents travel whole and the server enumerates which JSON
leaves differ; it never produces a third document. 40.18 ruled out three-way merging of prose and
grading criteria on the grounds that it produces plausible nonsense which then grades a living
salesperson, and that ruling holds one level down.

**Accepting a rewrite of a global-library exercise forks the lesson first** (40.18 copy-on-write) and
writes the body into the organization's own copy — the response's `summary.exerciseId` is what was
proposed against, and the applied row is recorded on the item. No `LessonVersion` is published:
accepting edits the draft exactly as `PUT /admin/exercises/{id}` does, and publishing stays a
separate human act on the existing 40.15 route.

**The seven review codes** (`ambiguous_correct_answer`, `multiple_correct_answers`,
`obvious_distractors`, `answer_given_away`, `unmeasurable_criteria`, `missing_explanation`,
`banned_claim_rewarded`) are a closed vocabulary. ai-service returns codes and a quoted fragment;
the Russian sentence and the `blocking`/`advisory` severity are resolved by learning-service, so two
runs over the same exercise produce the same complaint and «сколько упражнений с этим дефектом» is a
query. Codes learning-service does not know are dropped, never rendered blank.

---

## AI quotas and spend (Phase 40.33)

Full description: [AI_QUOTAS.md](AI_QUOTAS.md). Two gatewayed admin routes and four internal ones.

### `GET /admin/ai-usage` — `RequireOrgAdmin`

This month's spend for the caller's organization. **Organization administrators, not platform staff
only**: the person who has to know their content pipeline is about to stop is the РОП whose pipeline
it is, and telling them a month later through a support ticket is the situation the roadmap bullet
«расход виден в дашборде раньше, чем в счёте от провайдера» exists to prevent.

```jsonc
{
  "periodKey": "2026-08",            // the UTC month
  "currency": "RUB",
  "quotaState": "ok",                // ok | warning | batch_paused | exhausted
  "llmPromptTokens": 812340,
  "llmCompletionTokens": 214905,
  "llmTotalTokens": 1027245,
  "llmMonthlyTokenLimit": 20000000,
  "llmCallCount": 1841,
  "llmEstimatedCallCount": 1602,     // counted from characters, not from a reported usage block
  "speechCharacters": 418220,
  "voiceUsedMinutesToday": 37,
  "voiceDailyLimitMinutes": 600,
  "voiceUsedMinutesThisMonth": 612,
  "voiceMonthlyLimitMinutes": 6000,
  "estimatedCost": null,             // derived, never stored. null whenever ANY line is unpriced
  "hasUnpricedModels": true,         // at least one model used this month has no price configured
  "models": [
    { "model": "gpt-4o", "kind": "llm", "promptTokens": 612340, "completionTokens": 180905,
      "callCount": 1610, "speechCharacters": 0, "estimatedCost": null },
    { "model": "yandex-tts", "kind": "tts", "promptTokens": 0, "completionTokens": 0,
      "callCount": 231, "speechCharacters": 418220, "estimatedCost": 543.68 }
  ]
}
```

- **`quotaState` has four values and the third is the interesting one.** `batch_paused` means the
  organization is past its batch ceiling: background pipelines have stopped, conversations have not.
  A report that only said `ok`/`exhausted` would leave an administrator wondering why their content
  pipeline went quiet in a month they can still hold calls in.
- **`estimatedCost` is derived, never stored**, and `null` for a model with no configured price —
  reported as unpriced rather than as free, because zero reads as "this model costs nothing".
  **The top-level total follows the same rule, and follows it strictly: it is `null` as soon as
  *any* line is unpriced, not only when every line is** (corrected in 40.34 — it used to sum the
  priced lines and skip the rest). That matters more than it sounds, because the shipped price table
  configures speech and no LLM model at all: the old behaviour reported the speech bill alone as a
  confident number while the entire LLM cost — the dominant one — contributed nothing to it. A
  partial sum presented as a total is worse than no total, and `hasUnpricedModels` sitting beside it
  was not enough of a warning. Fill in `AiQuotas:PricePerMillionTokens` and the figure appears.
- **`llmEstimatedCallCount` is high by design**: only streamed dialog turns are estimated, and they
  are the most numerous and the cheapest calls in the product. Everything expensive carries the
  provider's own token count.
- A **platform administrator with no organization header** reads the installation-wide total
  (`IsPlatformWide` widening, 40.16). Safe in a way `/admin/voice/usage` was not before 40.11: it
  returns per-model token counts and no identities at all.

### `GET` / `PUT /admin/ai-quota` — `RequirePlatformAdmin`, `[TenantScoped]`

```jsonc
// PUT body — every field optional; omitting one CLEARS it back to the platform default
{ "voiceDailyLimitMinutes": 1200, "voiceMonthlyLimitMinutes": null,
  "llmMonthlyTokenLimit": 50000000, "batchReservePercent": 20,
  "note": "Contract 2026-Q3, raised after the onboarding batch" }

// Response (both verbs)
{ "voiceDailyLimitMinutes": 1200, "voiceMonthlyLimitMinutes": null,
  "llmMonthlyTokenLimit": 50000000, "batchReservePercent": 20,
  "note": "…",
  "isOrganizationSpecific": true,          // false when no row exists and everything below is a default
  "effectiveVoiceDailyLimitMinutes": 1200,
  "effectiveVoiceMonthlyLimitMinutes": 6000,   // ← the platform default showing through the null above
  "effectiveLlmMonthlyTokenLimit": 50000000,
  "effectiveBatchReservePercent": 20,
  "updatedAt": "2026-08-18T04:11:00Z" }
```

**Platform staff only, and that is a commercial boundary rather than a technical one.** A quota is
what the customer bought; an organization administrator raising their own is not an administrative
action, it is a purchase. `PUT` always writes the caller's own `X-Organization-Id` — documented as
"the one they impersonated into (40.9)", though as of 2026-08-21 (AD-5) no impersonation token can
actually reach this controller (`RequirePlatformAdmin` requires `role: Admin`/`SuperAdmin`;
impersonation mints `role: User` on purpose) — so there is no organization id in the route or body
for the write (`scripts/tenancy-boundary-lint.py`), and today there is no way for platform staff to
write a quota for any organization but their own. Tracked as Q-10 in `docs/NIGHT_AUDIT_QUESTIONS.md`.

`batchReservePercent` is clamped to 0–90; a negative limit is read as null.

### `GET /admin/ai-quota/{organizationId}` — `RequirePlatformAdmin`

Read-only counterpart added for AD-5 (2026-08-21 admin audit): reads the **named** organization's
quota directly, bypassing the caller's own `X-Organization-Id`. Same response shape as `GET
/admin/ai-quota` above. Safe as a read because every caller here is already platform-wide
(`RequirePlatformAdmin` ⇒ `role: Admin`/`SuperAdmin` ⇒ `TenantContext.IsPlatformWide`), and
`OrganizationQuota`'s own EF query filter already lets a platform-wide caller read every
organization's row — this endpoint only narrows that already-cross-tenant-readable query to one
organization instead of leaving it defaulted to the caller's own. Allow-listed by exact path in
`scripts/tenancy-boundary-lint.py`. `PUT /admin/ai-quota` has no matching `{organizationId}` form and
must not gain one — see the write-path caveat above.

The platform panel's `/admin/organizations/{id}/quota` screen reads through this endpoint
(`usePlatformOrganizationQuotaSettings`) so the numbers shown always belong to the organization named
in the URL, not to the session's own. `useOrganizationQuotaSettings()` (no id) still exists for "my
own organization's quota" but the quota screen no longer uses it for display.

### A quota refusal, on any metered route

```jsonc
// 429
{ "error": "Organization AI quota reached",
  "resource": "llm_tokens",          // llm_tokens | voice_minutes
  "period": "month_batch_reserve",   // day | month | month_batch_reserve
  "used": 18000412, "limit": 18000000 }
```

**429, not 402.** 402 is reserved for the *provider* telling us **our** balance is empty
(`OpenAiPaymentRequiredException`); conflating them would make a customer's cap look like our outage.
Voice keeps its existing 429 shape (`{error, period, usedSeconds, limitSeconds}`) with `period`
reading `organization day` / `organization month`.

An **unattributed** metered call — one arriving with no `X-Organization-Id` — is `400`
`{ "error": "An organization is required for metered AI calls." }`. A caller mistake with a fixed
remedy, reported as such rather than as a server fault.

### Internal, un-gatewayed (`X-Internal-Service-Secret`, like `/ai/evaluate`)

| Method | Path | Body → Response |
|---|---|---|
| POST | /ai/chat | `{systemPrompt, messages:[{role, content}]}` → `{content, isStopSignal}` |
| POST | /ai/chat/stream | same body → `application/x-ndjson`, one `{"d":"…"}` per content delta |
| POST | /ai/tts | `{text, voiceId?}` → `audio/wav` |
| GET | /ai/quota/preflight?workload=batch\|interactive | — → `{allowed: bool}`. **Reads only** |

The first three are what learning-service's deleted in-process provider clients used to do. On a
provider failure they answer with a **named** code so the caller rebuilds the same exception it used
to throw itself — `payment_required` (402), `rate_limited` (429), `provider_auth` (503),
`provider_rejected` / `provider_failed` / `provider_unreachable` (503), with the upstream status
alongside. The provider's own body never travels: it is redacted and dropped inside ai-service, per
[LLM_FAILURE_HANDLING.md](LLM_FAILURE_HANDLING.md).

All four require `X-Organization-Id`, and all callers now send it. `X-Ai-Workload` (`batch` /
`interactive`, default `interactive`) declares whether a person is waiting.
