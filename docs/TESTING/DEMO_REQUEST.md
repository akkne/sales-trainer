# Testing — Demo Request

Feature: [docs/DEMO_REQUEST.md](../DEMO_REQUEST.md).
Endpoints: [docs/API_CONTRACTS.md](../API_CONTRACTS.md) → Organization service → Demo requests.

## Automated

```bash
# backend (organization-service) — service behaviour, both controllers, provisioning, DTO
# validation shape, authorization policies
dotnet test src/backend/organization-service/Organization.Tests/Sellevate.Organization.Tests.csproj \
  --filter "TestCategory!=Integration"

# backend (identity-service) — the internal bootstrap-admin route's attribute contract; the
# behavioural cases (actor checks, replica upsert, pending-invite convergence, mail-failure
# recovery) are Integration-tagged and need Testcontainers, see below
dotnet test src/backend/identity-service/Identity.Tests/Sellevate.Identity.Tests.csproj \
  --filter "TestCategory!=Integration"

# backend — the gateway routes actually reach the right controllers, and internal/* routes stay
# out of the routing table entirely
dotnet test src/backend/route-parity/RouteParity.Tests/Sellevate.RouteParity.Tests.csproj

# frontend — the form, the success panel, the landing CTA, and the platform-admin pipeline screen
cd src/frontend && npx vitest run __tests__/DemoRequestPage.test.tsx __tests__/LandingPage.test.tsx \
  __tests__/AdminDemoRequestsPage.test.tsx
```

Provisioning's behavioural identity-service tests
(`Identity.Tests/Integration/InternalOrganizationBootstrapFlowTests.cs`) run against a real Postgres
via Testcontainers, the same as every other `[Category("Integration")]` fixture in this service —
`dotnet test … --filter "TestCategory=Integration"` once Docker is available locally.

| Suite | Covers |
|---|---|
| `Organization.Tests/Unit/DemoRequestServiceTests.cs` | persisting a lead with the email normalized (trimmed, lowercased); exactly one internal notification email to the configured inbox; only the acknowledgement sending (not the internal notification) when `NotificationEmail` is blank; the submission still succeeding when the mail provider throws; the honeypot persisting nothing, sending nothing, and still returning an id; a second submission inside the cooldown throwing `DemoRequestCooldownException` with a sensible `RetryAfterSeconds`; a submission after the cooldown elapses succeeding; marketing consent stamping `MarketingConsentGivenAt` when true and leaving it null when false; the required `ConsentGivenAt` being stamped either way; both the HTML and plain-text bodies of the internal notification reporting the marketing answer correctly; a submission acknowledgement sending to the submitter's own `workEmail`; the lead still persisting and the internal notification still sending when the acknowledgement send throws; the approval email sending only on an actual transition into `Approved`; nothing sending when an already-`Approved` lead is re-patched to `Approved`; nothing sending on `New → Declined`; the status change still being recorded when the approval email throws |
| `Organization.Tests/Unit/DemoRequestControllerTests.cs` | `202` with the accepted DTO; `429` carrying both the `Retry-After` header and `retryAfterSeconds` in the body |
| `Organization.Tests/Unit/AdminDemoRequestControllerTests.cs` | the list coming back newest-first; the status patch updating and returning the DTO; `404` for an unknown id; `Provision` returning the provisioning result on success, `404`/`409 slug-taken`/`503 invite-failed`/`400` mapped from the service's exceptions; `AdminDemoRequestController` carrying `RequirePlatformAdministrator` and its `ProvisionDemoRequest` action carrying `RequireSuperAdministrator` while `UpdateDemoRequestStatus` carries no action-level override |
| `Organization.Tests/Unit/DemoRequestProvisioningTests.cs` | first provision creating the organization and the invite end to end; `organization.created` published exactly once, including across a retry; a second call on an already-`AdminInvited` lead returning `alreadyProvisioned: true` with no second organization and no identity-service call; a slug collision throwing with zero writes; an explicit slug avoiding the collision; the identity client throwing leaving the lead at `OrganizationCreated` with no 500; a retry after that failure converging to `AdminInvited` without a second organization; identity reporting an already-pending invite surfacing as success with that invite's id; `adminEmail` defaulting to `workEmail` and an explicit override taking precedence; `404`-shaped `null` for an unknown lead; the service's constructor taking no dependency capable of sending the plain-approval email |
| `Identity.Tests/Unit/InternalOrganizationBootstrapControllerContractTests.cs` | the route stays under `internal/`; `[ServiceFilter(typeof(InternalServiceAuthFilter))]` is present; no `[Authorize]`; no `[TenantScoped]` |
| `Identity.Tests/Integration/InternalOrganizationBootstrapFlowTests.cs` (Testcontainers — not run in every environment, see "Automated" above) | missing/wrong secret → `403`; **no `OrganizationReplica` row yet still succeeds** (the race the replica-upsert-from-payload exists to close); actor is a platform `Admin` (not `SuperAdmin`) → `403` with no invite row written; unknown actor → `403`; an active administrator already existing → `200` with a second invite still minted (an organization may have any number of administrators, 2026-08-27); a pending invite **for the same address** → `200` returning that invite, while a different address gets its own; every invite read going through a tenant-scoped transaction, without which row-level security answers "no invites"; `Manager` role → `400`; an omitted role defaulting to `TenancySuperAdmin`; the invite's `InvitedBy` equal to the actor; a throwing mail sender never turning a committed invite into a `500` |
| `Organization.Tests/Unit/FrontendConfigurationTests.cs` | `PrimaryUrl` taking the first origin out of the comma-separated CORS allow-list `Frontend:Url` actually holds, so the approval email’s registration link is not `http://localhost:3000,https://sellevate.site/register` |
| `Organization.Tests/Unit/CreateDemoRequestRequestValidationTests.cs` | `phone` being required (owner decision, 2026-08-20); `salesTeamSize` being a **required nullable** enum; `consentGiven` being pinned to `true` rather than merely present; `marketingConsentGiven` carrying no constraint at all |
| `Organization.Tests/Unit/AuthorizationPolicyContractTests.cs` | `DemoRequestController` carrying `[AllowAnonymous]`; `AdminDemoRequestController` carrying `RequirePlatformAdministrator` |
| `RouteParity.Tests` | the four `/demo-requests` + `/admin/demo-requests` gateway routes matching the two controllers' declared templates |
| `__tests__/DemoRequestPage.test.tsx` | every field rendering; the exact payload shape posted to `/demo-requests`, including `salesTeamSize` as the English enum name and `website: ""`; the success heading «Отлично, мы с вами свяжемся» after a resolved `202`; the cooldown message on a rejected `429`; the submit button disabled while pending; marketing consent sending `false` unticked and `true` ticked; the required data-processing consent blocking submission entirely when unticked; phone now also blocking submission when left empty (owner decision, 2026-08-20) |
| `__tests__/LandingPage.test.tsx` | the «Запросить демо» CTA existing and pointing at `/demo`; `/landing` staying put for a signed-in visitor; `/` forking to `/tree` when signed in and to `/landing` otherwise |
| `__tests__/AdminDemoRequestsPage.test.tsx` | the list rendering every at-a-glance field; the status rendering as a colour-coded badge; a non-`Approved` status change calling `PATCH` with `{status}` and no confirmation step; the `Approved` transition requiring an inline "Confirm approval" click before any `PATCH` fires, and being cancellable with no `PATCH` sent either way; `marketingConsentGivenAt` surfaced as a Yes/No indicator; the "No demo requests yet." empty state; the emails note rendering unconditionally; the **Provision** button hidden from a plain `Admin` and shown to a `SuperAdmin`; confirming `POST`s to `/admin/demo-requests/{id}/provision` with an edited slug or edited email sent and the untouched default omitted (never both sent); cancelling the confirmation sending no request; `409 slug-taken` rendering inline and keeping the entered slug; `503 invite-failed` rendering the "created, invite failed, press again" message and refetching the list; a `400` rendering the server's own message; `OrganizationCreated` rendering "Finish provisioning" instead of "Provision"; `AdminInvited` rendering the organization/invite details with no button |

### Why `CreateDemoRequestRequestValidationTests` inspects the constructor

On a positional record an attribute with no explicit target binds to the constructor **parameter**,
not the generated property. ASP.NET Core reads those parameter attributes when it builds
`ModelMetadata`, so `[ApiController]` enforces them on a real request — but
`System.ComponentModel`'s `Validator.TryValidateObject` only ever looks at properties and will
happily report a completely invalid payload as valid. A test written against `Validator` passes
while proving nothing, so that fixture reflects over the constructor instead. Do not "simplify" it
back.

## Manual

Needs organization-service and the gateway up — `scripts/dev-up.sh` covers both. MailerSend does
not need to be configured; with `DemoRequests:NotificationEmail` empty the send is skipped with a
logged warning and **the lead is still stored**, which is itself worth confirming once.

1. **Happy path.** Open `/`, press «Запросить демо» in the header or the hero. Fill the form —
   phone included, it is required as of 2026-08-20 — tick only the required consent, submit.
   Expect: the card is replaced in place — no navigation — by «Отлично, мы с вами свяжемся» quoting
   the address you typed.
2. **The lead landed.** As a platform admin:
   ```bash
   curl -s http://localhost:5001/admin/demo-requests -H "Authorization: Bearer $TOKEN" | jq '.[0]'
   ```
   Expect your submission first in the list, `status: "New"`, `workEmail` lowercased,
   `marketingConsentGivenAt: null` if you left that box unticked.
3. **Cooldown.** Submit the same email again straight away. Expect the inline error naming roughly
   when to retry, and no second row in the admin list. `Retry-After` should be on the response.
4. **Honeypot.** The `website` input is off-screen, so drive it directly:
   ```bash
   curl -i -X POST http://localhost:5001/demo-requests -H 'Content-Type: application/json' \
     -d '{"fullName":"Bot","workEmail":"bot@example.com","phone":"+70000000000","companyName":"Bot Co",
          "salesTeamSize":"UpToFive","consentGiven":true,"marketingConsentGiven":false,
          "website":"http://spam.example"}'
   ```
   Expect `202` with an id that looks exactly like a real one — and **no** new row in
   `GET /admin/demo-requests`. That indistinguishability is the point; if the response ever differs
   from a genuine one, the honeypot is worthless.
5. **Required qualifier.** Repeat the call above without `salesTeamSize`. Expect `400`, **not** a
   stored lead with `UpToFive` — that silent default is the bug the nullable DTO field prevents.
6. **Consent is two questions.** Submit with `consentGiven: false`. Expect `400`. Submit with
   `consentGiven: true, marketingConsentGiven: false`. Expect `202` — declining marketing must never
   block a demo request.
7. **Status flow.** `PATCH /admin/demo-requests/{id}/status` with `{"status":"Contacted"}`. Expect the
   updated DTO. Nothing enforces the order of statuses by design. Valid values are now
   `New | Contacted | Approved | Declined` — `"Qualified"` was renamed to `"Approved"` 2026-08-20 and
   is no longer accepted.
8. **Mobile.** Open `/demo` at 375px. Expect the two columns to stack, every field full-width, both
   consent rows readable as two distinct questions rather than one block of small print.
9. **The two submitter emails (2026-08-20).** Point MailerSend at a real inbox you control (or a
   catch-all) via `DemoRequests:NotificationEmail` and `MailerSend:*`, then:
   - Submit the form with your own address as `workEmail`. Expect **two** emails: the unchanged
     internal notification, and a «Спасибо, что выбрали Sellevate» acknowledgement addressed to you
     (formal «вы»), naming the one-business-day expectation and briefly restating what the demo
     covers.
   - `PATCH /admin/demo-requests/{id}/status` with `{"status":"Approved"}` for that lead. Expect a
     third email, «Заявку одобрили», linking to `{Frontend:Url}/register`.
   - `PATCH` the same lead to `{"status":"Approved"}` again (re-patch of an already-approved lead).
     Expect **no** new email — this is the no-double-send guarantee; only an actual transition into
     `Approved` mails the submitter.
   - `PATCH` a fresh `New` lead straight to `{"status":"Declined"}`. Expect no email to the
     submitter either — only the `Approved` transition triggers one.
10. **The admin pipeline screen.** As a platform admin, open `/admin/demo-requests`. Expect your
   submission listed first (created date, name, company, work email, phone, team size, status,
   and a Yes/No marketing-consent indicator), and "Details" reachable for job title / comment.
   Change its status via the row's select to `Contacted` — expect the `PATCH` to fire immediately
   with no confirmation. Change it to `Approved` — expect an inline "Confirm approval" prompt
   (never a browser `confirm()` dialog) before any request is sent; press Cancel and confirm no
   `PATCH` fired, then repeat and press "Confirm approval" and confirm it now does.
11. **Provisioning (needs `POST /admin/demo-requests/{id}/provision` deployed).** As a plain
    `Admin`, confirm the "Provisioning" column shows no button on a `NotProvisioned` lead — only a
    `SuperAdmin` sees "Provision". As a `SuperAdmin`, press "Provision": expect an inline panel
    (never a browser dialog) previewing the organization to create and the address to invite, with
    the slug and invite email editable. Leave both untouched and confirm — expect the organization
    to appear at `/admin/organizations` and the row to flip to "Provisioned" with the org name,
    slug, invited email and invite expiry. Repeat on a fresh lead, this time editing the slug to
    one that collides with an existing organization — expect the inline `slug-taken` message
    naming the slug, with the value you typed still in the box, and no navigation away. Simulate a
    `503` by stopping MailerSend/misconfiguring it before confirming — expect "the organization was
    created, but the invite failed to send"; refresh the page and confirm the row now shows
    "Organization created, invite not sent" with a "Finish provisioning" button in place of
    "Provision"; press it to confirm the invite goes out and the row moves to "Provisioned".
