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

# frontend — the form, the success panel, the landing CTA
cd src/frontend && npx vitest run __tests__/DemoRequestPage.test.tsx __tests__/LandingPage.test.tsx
```

| Suite | Covers |
|---|---|
| `Organization.Tests/Unit/DemoRequestServiceTests.cs` | persisting a lead with the email normalized (trimmed, lowercased); exactly one notification email to the configured inbox; no send at all when `NotificationEmail` is blank; the submission still succeeding when the mail provider throws; the honeypot persisting nothing, sending nothing, and still returning an id; a second submission inside the cooldown throwing `DemoRequestCooldownException` with a sensible `RetryAfterSeconds`; a submission after the cooldown elapses succeeding; marketing consent stamping `MarketingConsentGivenAt` when true and leaving it null when false; the required `ConsentGivenAt` being stamped either way; both the HTML and plain-text bodies reporting the marketing answer correctly |
| `Organization.Tests/Unit/DemoRequestControllerTests.cs` | `202` with the accepted DTO; `429` carrying both the `Retry-After` header and `retryAfterSeconds` in the body |
| `Organization.Tests/Unit/AdminDemoRequestControllerTests.cs` | the list coming back newest-first; the status patch updating and returning the DTO; `404` for an unknown id |
| `Organization.Tests/Unit/CreateDemoRequestRequestValidationTests.cs` | `salesTeamSize` being a **required nullable** enum; `consentGiven` being pinned to `true` rather than merely present; `marketingConsentGiven` carrying no constraint at all |
| `Organization.Tests/Unit/AuthorizationPolicyContractTests.cs` | `DemoRequestController` carrying `[AllowAnonymous]`; `AdminDemoRequestController` carrying `RequirePlatformAdministrator` |
| `RouteParity.Tests` | the four `/demo-requests` + `/admin/demo-requests` gateway routes matching the two controllers' declared templates |
| `__tests__/DemoRequestPage.test.tsx` | every field rendering; the exact payload shape posted to `/demo-requests`, including `salesTeamSize` as the English enum name and `website: ""`; the success heading «Отлично, мы с вами свяжемся» after a resolved `202`; the cooldown message on a rejected `429`; the submit button disabled while pending; marketing consent sending `false` unticked and `true` ticked; the required data-processing consent blocking submission entirely when unticked |
| `__tests__/LandingPage.test.tsx` | the «Запросить демо» CTA existing and pointing at `/demo` |

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

1. **Happy path.** Open `/`, press «Запросить демо» in the header or the hero. Fill the form, tick
   only the required consent, submit. Expect: the card is replaced in place — no navigation — by
   «Отлично, мы с вами свяжемся» quoting the address you typed.
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
     -d '{"fullName":"Bot","workEmail":"bot@example.com","companyName":"Bot Co",
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
   updated DTO. Nothing enforces the order of statuses by design.
8. **Mobile.** Open `/demo` at 375px. Expect the two columns to stack, every field full-width, both
   consent rows readable as two distinct questions rather than one block of small print.
