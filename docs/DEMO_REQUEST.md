# Demo Request

The landing page's way in for a company that has not signed up yet. A visitor presses
«Запросить демо», fills one form, and gets told that somebody will contact them — the lead lands
in `organization-db`, an email goes to the sales inbox, and (as of 2026-08-20) the visitor's own
address gets an acknowledgement, with a further email once the lead is approved.

Tests: [docs/TESTING/DEMO_REQUEST.md](TESTING/DEMO_REQUEST.md).
Endpoints: [docs/API_CONTRACTS.md](API_CONTRACTS.md) → Organization service → Demo requests.

---

## Flow

```
/ (лендинг) → «Запросить демо»
      │
      ▼
 /demo — форма: имя, рабочий email, телефон, компания, должность?,
         размер отдела продаж, комментарий?, согласие на обработку ПДн
         + отдельное необязательное согласие на рассылку
      │
      ▼
 POST /demo-requests                      ← anonymous, no tenant
      │
      ├── honeypot filled ──► 202, ничего не сохранено, писем нет
      ├── same email again  ──► 429 + Retry-After
      ▼
 202 { id, submittedAt }
      │  ├─► строка в organization-db.DemoRequests (Status = New)
      │  ├─► письмо на внутренний адрес продаж
      │  └─► «Спасибо, что выбрали Sellevate» на WorkEmail заявителя
      ▼
 «Отлично, мы с вами свяжемся» — форма заменяется панелью успеха, без навигации
```

The success panel replaces the form in place rather than routing anywhere. A visitor who just
handed over their contact details should not be moved to a different screen — the confirmation
has to appear where they were looking.

---

## Which service owns it, and why it is the odd one out

`organization-service`, alongside the tenant registry — **not** identity-service and not a new
service.

A demo request is a prospective tenant. It has no user, no organization, and no membership: it
precedes all three. That rules out every tenant-scoped service in the platform, because
`DemoRequest` cannot satisfy `ITenantScoped` — there is no organization to scope it to. The one
existing precedent for exactly this shape is `Organization` itself, which is deliberately not
tenant-scoped because it *is* the registry (docs/TENANCY/TENANCY.md §1.2, §1.9). A lead sits one
step earlier on the same axis, so it sits in the same service.

identity-service was the alternative, since it already hosts the anonymous pre-auth endpoints and
already wires `IEmailSender`. It was rejected on bounded context: identity answers "who is this
user", and a marketing lead is not a user.

`DemoRequest` therefore carries **no query filter** and no row-level security, same as
`Organizations`. Reads are gated by `RequirePlatformAdministrator` instead — see below.

---

## Anti-spam, and what it deliberately does not do

The endpoint is anonymous and unauthenticated, which is the whole point of it and also its only
real risk.

**The submitter now receives email — this reverses the original decision.** Until 2026-08-20 no
email was ever sent to the address a visitor typed in, on the reasoning recorded below and still
worth reading, because it names the exact risk this endpoint now carries:

> A public unauthenticated form that emails whatever address was typed into it is a mail relay
> aimed at arbitrary third parties, and the abuse costs the platform its sending reputation rather
> than costing the abuser anything. The visitor was told on screen instead.

The owner reversed this: a submission now also sends a «Спасибо, что выбрали Sellevate»
acknowledgement to `workEmail`, and an approval sends a «Заявку одобрили» notification pointing the
submitter at registration (both docs/DECISIONS.md — 2026-08-20, and the "Two new emails to the
submitter" section below). The mail relay risk the original decision was written to avoid did not
go away; it was accepted deliberately. What is left to limit it is exactly the two measures below —
**the honeypot and the per-email cooldown are now the only things standing between this endpoint
and being used to mail a third party on demand.** Neither was designed with that job in mind: the
honeypot only catches a bot that fills a hidden field, and the cooldown only slows one email address
down to once per `SubmissionCooldownSeconds`. A human filling in someone else's real address by hand
once is not stopped by either.

**1. Honeypot.** A `website` text input, visually hidden and `tabIndex={-1}`, that no person ever
sees. If it comes back non-empty the request persists nothing and sends nothing, but still
returns a normal `202` with a freshly minted id. The response must be indistinguishable from a
real one — a bot that can tell the difference just stops filling the field.

It is positioned off-screen rather than `display: none`, because the field being reachable at all
is what makes naive form-fillers fill it.

**2. Per-email cooldown.** A second submission from the same normalized address inside
`DemoRequests:SubmissionCooldownSeconds` (default 300) gets `429` with `Retry-After`, modelled on
identity-service's resend-code cooldown. Since a submission now mails that address, this cooldown is
also the only throttle on how often the acknowledgement email itself can be sent to a given address.

There is **no CAPTCHA and no per-IP limit.** No rate-limiting middleware exists anywhere in this
backend, and introducing one for a single low-traffic form would be the wrong place to introduce
it. The cooldown is keyed on email, so it stops the accidental double-submit and the lazy script,
not a determined attacker with a wordlist. If leads start arriving as junk, or the endpoint gets used
to spam a third party, per-IP limiting at the gateway is the next step, not a bigger form.

### Two new emails to the submitter

**On submission**, in addition to the unchanged internal sales-inbox notification, `workEmail`
receives a «Спасибо, что выбрали Sellevate» acknowledgement (formal «вы», matching this flow's
register): confirms the request arrived, sets the expectation that a manager will reach out within
one business day, and briefly restates what the demo covers.

**On approval** (`Status` moving into `Approved`, see below), `workEmail` receives a «Заявку
одобрили» notification. This fires only on an actual transition into `Approved` — re-patching an
already-`Approved` lead (an admin refreshing, double-clicking, or retrying) sends nothing, so the
submitter is never mailed twice for one approval.

**This email stopped linking to `/register` once provisioning shipped (2026-08-20).** Until then
`/register` was the only thing it could point at, and that link strands the recipient: `/register`
creates a global identity with no membership, landing the recipient on the awaiting-organization
gate having "registered" into nothing. The link that actually works — `/invite/{token}`, 7-day
expiry — only exists once a platform superadmin provisions the lead (below), a separate act that
may happen minutes or days later. This email now only says the request is approved and that a
workspace invitation will follow, rather than promise a link that may not exist yet. See
docs/DECISIONS.md.

Both follow the same failure semantics as the internal notification: wrapped in try/catch, logged,
never surfaced to the caller. A lead is still persisted when the acknowledgement fails to send, and
a status update is still recorded when the approval email fails to send.

---

## Why these fields

Nine visible fields — six one-line inputs, one textarea and two checkboxes — plus the hidden
honeypot. Long B2B demo forms trade completion rate for qualification data, and at this stage the
platform has no sales team large enough to need the data more than it needs the lead. Five of the
nine are required.

| Field | Required | Why |
|---|---|---|
| `fullName` | yes | Somebody has to be addressed by name in the reply |
| `workEmail` | yes | The reply channel, and the cooldown key |
| `phone` | **yes** (owner decision, 2026-08-20 — was optional) | Not strictly needed to *reply* — `workEmail` already covers that — but required because the sales motion this form feeds is phone-first: both Russian vendors whose live forms could actually be read (Talent Rocks, Эквио) require it, and CIS B2B sales moves over a call faster than over email. A business decision about how sales works here, not a technical requirement of the endpoint. |
| `companyName` | yes | The unit actually being sold to |
| `jobTitle` | no | Separates a РОП from a curious sales rep, but not worth blocking on |
| `salesTeamSize` | yes | The single strongest qualifier for this product, and it is one tap |
| `comment` | no | Where a real buyer volunteers the thing no field asked about |
| `consentGiven` | yes | Consent to processing personal data, stored as `ConsentGivenAt` |
| `marketingConsentGiven` | no | Consent to marketing outreach, stored as `MarketingConsentGivenAt` |

`salesTeamSize` is a select whose **wire values are English enum names** (`UpToFive`,
`SixToTwenty`, `TwentyOneToFifty`, `FiftyOneToTwoHundred`, `MoreThanTwoHundred`) with Russian
labels, per docs/LOCALIZATION.md — enum values crossing the wire stay English.

It is also **required and nullable in the request DTO**, which looks like an oversight beside a
non-nullable column and is not one. `[Required]` on a non-nullable enum has nothing to reject: a body
that omits the field binds to the zero member, and `UpToFive` gets stored as though a visitor had
picked the smallest bucket. Nullability is what gives the attribute something to fail on. A test
pins this so it cannot be tidied away.

### Two consents, not one

The required data-processing consent and the optional marketing consent are **separate checkboxes**,
and the form is laid out so they read as two distinct questions rather than one block of small print.
152-ФЗ and GDPR guidance both treat them as distinct purposes, and bundling them would force a
visitor to accept marketing email in order to ask for a demo. Declining the marketing one is a
completely valid submission — the field carries no validation constraint at all.

This is also what the two Russian vendors whose live forms could actually be read (Talent Rocks,
Эквио) do, which is worth more than the pattern any US-market SaaS uses here.

Both are stored as timestamps (`ConsentGivenAt`, `MarketingConsentGivenAt`, the latter null when not
given), not booleans. A boolean records that a box was ticked; a timestamp records when, which is the
part that matters if it is ever asked about.

---

## Language register

This flow uses the formal **«вы»**, unlike the learner-facing UI, which uses «ты»
(docs/LOCALIZATION.md). It addresses a company decision-maker who has not bought anything yet —
the same register as the org panel under `app/(org)/`, and the opposite of the tone the product
uses once somebody is inside it training.

---

## Admin side

`GET /admin/demo-requests` and `PATCH /admin/demo-requests/{id}/status`, both
`RequirePlatformAdministrator`. Leads are platform-wide, never an organization's data, so the org
panel never sees them.

`Status` moves `New → Contacted → Approved | Declined` (`Approved`, not `Qualified` — renamed
2026-08-20). Nothing enforces the order — it is a label for a human working a list, not a state
machine, and pretending otherwise would only produce a validation error on the day somebody marks
a lead approved before logging the call.

Moving a lead to `Approved` sends the customer an email saying their request was approved, so the
screen below treats that one transition as the expensive one — every other move is silent.

The screen is `/admin/demo-requests` (`app/(admin)/admin/demo-requests/page.tsx`), platform-panel
English per docs/LOCALIZATION.md, raw Tailwind per docs/ADMIN_PANEL.md. It lists every lead newest
first with an inline status control per row, gates the `Approved` transition behind an inline
confirmation (not `window.confirm`), shows `marketingConsentGivenAt` as a Yes/No indicator column,
and tucks `jobTitle`/`comment` behind a per-row "Details" toggle. Data hooks:
`features/admin/hooks/use-demo-requests.ts`; the sales-team-size label map and status list:
`features/admin/lib/demo-request-format.ts`. Tests:
`__tests__/AdminDemoRequestsPage.test.tsx`.

### Provisioning: turning an approved lead into a tenant

`POST /admin/demo-requests/{id}/provision` (`SuperAdmin` only) is the one-click version of what
the "Organizations" screen otherwise does in two separate steps — create a tenant, then bootstrap
its first admin. Body is `{organizationName?, slug?, adminEmail?, role?}`, all optional; the
server defaults the name from the lead's `companyName`, the slug from a normalized name, the
admin email from the lead's `workEmail`, and the role to `TenancySuperAdmin`. `DemoRequestDto`
carries the outcome as `organizationId?`, `organizationName?`, `organizationSlug?`,
`provisioningState` (`NotProvisioned | OrganizationCreated | AdminInvited`), `bootstrapInviteId?`,
`bootstrapAdminEmail?`, `provisionedAt?`.

**`OrganizationCreated` is a deliberate, surfaced middle state, not a glitch.** The organization
write and the invite send are two operations, and the second can fail (`503 invite-failed`) after
the first has already committed. The screen shows this as a warning-toned "Organization created,
invite not sent" indicator with a "Finish provisioning" retry that calls the same endpoint — never
as either "done" or "failed", both of which would be lying about what actually happened.

**The Provision action is SuperAdmin-only**, reusing `canManagePlatformUsers` — the same predicate
that gates first-admin bootstrap and impersonation on the organizations screen — rather than a new
check. A plain `Admin` sees the lead list and provisioning state exactly as a `SuperAdmin` does,
just without the button.

**The confirmation is inline** (never `window.confirm`) and lets the admin override the slug and
the invited email before submitting — the slug is the one field that can collide with an existing
organization (`409 slug-taken`). The client only sends a field when the admin actually edited it
away from the previewed default; an untouched field is omitted from the request body entirely, so
the server's own name/slug/email derivation stays the single source of truth for what "default"
means. `409 organization-has-admin` (the tenant exists and already has an administrator — no
invite is sent) and a plain `400` are rendered as their own distinct messages inline; a `503`
message says the organization was created and that pressing again finishes the job, then the list
is refetched so the row does not keep showing `NotProvisioned` after the tenant already exists.

**The organization's name and slug are on `DemoRequestDto`; the invite's expiry is not**, and that
asymmetry follows ownership rather than convenience. organization-service owns both the lead and the
registry, so it resolves the name and slug in one join and they survive a page reload. `Invite`
belongs to identity-service, so reporting its expiry on a list endpoint would mean a cross-service
read per row or a replica of somebody else's table — it is returned once, by the provision call that
creates it, and the screen caches that for the rest of the session (`provisionedDetailsById`). After
a reload the row shows the real organization and says the expiry is unknown, rather than guessing.

This was originally built the other way — nothing but `organizationId` on the DTO, everything else
from the cached provision response — and the result was that every lead provisioned before the
current page load rendered the lead's own `companyName` and the literal text "slug unknown", which in
normal operation is most of the list. Two tests pin the join now.

### How provisioning is actually written — the safety property, not the screen

`DemoRequestProvisioningService.ProvisionAsync` (organization-service) is the whole feature; the
screen above is a client of it. The order matters and is deliberate:

1. **Lock the lead row** (`SELECT … FOR UPDATE`, skipped on the in-memory test provider) inside a
   transaction, so two concurrent provisions for the same lead cannot both read
   `OrganizationId == null` and both try to create an organization. A **partial unique index on
   `DemoRequests.OrganizationId` where not null** is the second, database-level line of defense for
   the same property.
2. **`AdminInvited` already? Commit and return, no side effects at all.** This is the fast path a
   double-click hits, and it is a `200`, never a fresh `409` — a UI button being pressed twice must
   not look like a failure.
3. **Not yet provisioned:** resolve and check the slug *before* writing anything (reusing
   `OrganizationService`'s own slug logic, promoted to `internal` rather than duplicated — the same
   move `InviteService.ParseRole` made for `PlatformAdminService`). A collision throws before either
   row is touched, so the transaction's rollback leaves nothing to clean up. Otherwise: insert the
   `Organization`, flip the lead to `OrganizationCreated` and `Status = Approved`, resolve and store
   `BootstrapAdminEmail` — **one `SaveChangesAsync`, one commit, both rows together** — then publish
   `organization.created`. A retry that finds `OrganizationId` already set skips straight past this
   step, which is what keeps the event published exactly once.
4. **Call identity-service, outside any transaction.** `POST internal/organizations/{organizationId}
   /bootstrap-admin` — see below. Its failure is the one this design treats as ordinary: the lead
   stays at `OrganizationCreated`, and the same call again always converges.
5. **A second, separate transaction** records `BootstrapInviteId`, flips to `AdminInvited`, and
   stamps `ProvisionedAt` — only once identity-service has actually answered.

`BootstrapAdminEmail` is resolved and stored exactly once, at step 3 — a retry's own `adminEmail`
override (if any) is ignored once the organization exists, because by then the invite it would name
may already be committed. `role`, by contrast, is **not** persisted anywhere and is re-read from
each call's request body, so a first attempt rejected for a bad role converges on a retry that
sends a valid one.

### The call into identity-service

`POST internal/organizations/{organizationId:guid}/bootstrap-admin` (identity-service), guarded by
`InternalServiceAuthFilter` — the shared secret, not a JWT — and deliberately not `[TenantScoped]`:
the caller is organization-service itself, with no membership in the organization to carry an
`X-Organization-Id` header for. Body: `{organizationName, organizationSlug, email, role?,
actorUserId}`.

In order: **upsert `OrganizationReplica` from the payload** (not from Kafka — see the class summary
on `OrganizationBootstrapService` for why this call would otherwise race its own consumer on every
single provision); **re-check `actorUserId` is a platform `SuperAdmin`** in identity-db (`403` if
not — the shared secret authorizes the channel, this check authorizes the actor, and skipping it
would let a plain `Admin`'s click be laundered into a superadmin act); an active administrator
already existing is `409`; **a pending admin invite already existing is `200` returning that
invite**, not a fresh `409` — the same convergent-retry property `/provision` needs, because
`InviteService.CreateAsync` sends its email after commit and outside any try/catch, so a mail
failure can leave a committed invite behind a thrown exception. The role is validated and narrowed
to `TenancyAdmin`/`TenancySuperAdmin` by the exact rule `PlatformAdminService.ResolveBootstrapRole`
already applies (promoted to `internal` rather than duplicated), defaulting to `TenancySuperAdmin`.
Full contract: docs/API_CONTRACTS.md. The reversal of the Phase 40.9 decision against exactly this
shape of call, and why it is narrow rather than a precedent, is recorded in docs/DECISIONS.md.

---

## Configuration

| Key | Environment variable | Default | Meaning |
|---|---|---|---|
| `DemoRequests:NotificationEmail` | `DemoRequests__NotificationEmail` | *(empty)* | Where the lead notification is sent |
| `DemoRequests:NotificationRecipientName` | `DemoRequests__NotificationRecipientName` | `Sellevate` | Display name on that email |
| `DemoRequests:SubmissionCooldownSeconds` | `DemoRequests__SubmissionCooldownSeconds` | `300` | Per-email resubmission cooldown |
| `IdentityService:BaseUrl` | `IdentityService__BaseUrl` | `http://identity:8080` | Where provisioning's bootstrap-admin call goes |
| `IdentityService:TimeoutSeconds` | *(not set — in-code default)* | `10` | How long that call may take before provisioning gives up and leaves the lead at `OrganizationCreated` |
| `InternalAuth:ServiceSecret` | `INTERNAL_SERVICE_SECRET` | *(unset)* | The header identity-service's `InternalServiceAuthFilter` checks; see docs/CONFIGURATION.md and docs/DONT_FORGET.md for the fact that this is not actually provisioned in any real environment yet |

**`Frontend:Url`/`FrontendConfiguration` is no longer read anywhere in this service.** The approval
email used to build a `/register` link from it (`FrontendConfiguration.PrimaryUrl` — the first
origin out of the comma-separated CORS allow-list `Frontend:Url` holds, since the raw value would
have produced `http://localhost:3000,https://sellevate.site/register`). Now that the email links
nowhere (see above), the binding in `DemoRequestFeatureServiceCollectionExtensions` and the class
itself are dead configuration, left in place rather than torn out mid-feature — see
docs/DONT_FORGET.md. `FrontendConfigurationTests.cs` still pins the class's own behaviour in
isolation, independent of whether anything in this service currently reads it.

An unset `NotificationEmail` logs a warning and skips the internal sales notification only — **the
lead is still persisted, and the submitter's own acknowledgement still sends.** The same is true
when MailerSend itself fails on any of the three emails: every send is wrapped and the failure never
turns into an error the visitor sees. A lead saved with nobody notified is recoverable from the
admin list; a lead refused because the mail provider was down is gone.
