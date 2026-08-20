# Testing — Demo Request

Feature: [docs/DEMO_REQUEST.md](../DEMO_REQUEST.md).
Endpoints: [docs/API_CONTRACTS.md](../API_CONTRACTS.md) → Organization service → Demo requests.

## Automated

```bash
# backend — service behaviour, both controllers, DTO validation shape, authorization policies
dotnet test src/backend/organization-service/Organization.Tests/Sellevate.Organization.Tests.csproj \
  --filter "TestCategory!=Integration"

# backend — the four new gateway routes actually reach the two new controllers
dotnet test src/backend/route-parity/RouteParity.Tests/Sellevate.RouteParity.Tests.csproj

# frontend — the form, the success panel, the landing CTA, and the platform-admin pipeline screen
cd src/frontend && npx vitest run __tests__/DemoRequestPage.test.tsx __tests__/LandingPage.test.tsx \
  __tests__/AdminDemoRequestsPage.test.tsx
```

| Suite | Covers |
|---|---|
| `Organization.Tests/Unit/DemoRequestServiceTests.cs` | persisting a lead with the email normalized (trimmed, lowercased); exactly one internal notification email to the configured inbox; only the acknowledgement sending (not the internal notification) when `NotificationEmail` is blank; the submission still succeeding when the mail provider throws; the honeypot persisting nothing, sending nothing, and still returning an id; a second submission inside the cooldown throwing `DemoRequestCooldownException` with a sensible `RetryAfterSeconds`; a submission after the cooldown elapses succeeding; marketing consent stamping `MarketingConsentGivenAt` when true and leaving it null when false; the required `ConsentGivenAt` being stamped either way; both the HTML and plain-text bodies of the internal notification reporting the marketing answer correctly; a submission acknowledgement sending to the submitter's own `workEmail`; the lead still persisting and the internal notification still sending when the acknowledgement send throws; the approval email sending only on an actual transition into `Approved`; nothing sending when an already-`Approved` lead is re-patched to `Approved`; nothing sending on `New → Declined`; the status change still being recorded when the approval email throws |
| `Organization.Tests/Unit/DemoRequestControllerTests.cs` | `202` with the accepted DTO; `429` carrying both the `Retry-After` header and `retryAfterSeconds` in the body |
| `Organization.Tests/Unit/AdminDemoRequestControllerTests.cs` | the list coming back newest-first; the status patch updating and returning the DTO; `404` for an unknown id |
| `Organization.Tests/Unit/FrontendConfigurationTests.cs` | `PrimaryUrl` taking the first origin out of the comma-separated CORS allow-list `Frontend:Url` actually holds, so the approval email’s registration link is not `http://localhost:3000,https://sellevate.vercel.app/register` |
| `Organization.Tests/Unit/CreateDemoRequestRequestValidationTests.cs` | `phone` being required (owner decision, 2026-08-20); `salesTeamSize` being a **required nullable** enum; `consentGiven` being pinned to `true` rather than merely present; `marketingConsentGiven` carrying no constraint at all |
| `Organization.Tests/Unit/AuthorizationPolicyContractTests.cs` | `DemoRequestController` carrying `[AllowAnonymous]`; `AdminDemoRequestController` carrying `RequirePlatformAdministrator` |
| `RouteParity.Tests` | the four `/demo-requests` + `/admin/demo-requests` gateway routes matching the two controllers' declared templates |
| `__tests__/DemoRequestPage.test.tsx` | every field rendering; the exact payload shape posted to `/demo-requests`, including `salesTeamSize` as the English enum name and `website: ""`; the success heading «Отлично, мы с вами свяжемся» after a resolved `202`; the cooldown message on a rejected `429`; the submit button disabled while pending; marketing consent sending `false` unticked and `true` ticked; the required data-processing consent blocking submission entirely when unticked; phone now also blocking submission when left empty (owner decision, 2026-08-20) |
| `__tests__/LandingPage.test.tsx` | the «Запросить демо» CTA existing and pointing at `/demo` |
| `__tests__/AdminDemoRequestsPage.test.tsx` | the list rendering every at-a-glance field; the status rendering as a colour-coded badge; a non-`Approved` status change calling `PATCH` with `{status}` and no confirmation step; the `Approved` transition requiring an inline "Confirm approval" click before any `PATCH` fires, and being cancellable with no `PATCH` sent either way; `marketingConsentGivenAt` surfaced as a Yes/No indicator; the "No demo requests yet." empty state |

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
