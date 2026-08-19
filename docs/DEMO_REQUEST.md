# Demo Request

The landing page's way in for a company that has not signed up yet. A visitor presses
«Запросить демо», fills one form, and gets told that somebody will contact them — the lead lands
in `organization-db` and an email goes to the sales inbox.

Tests: [docs/TESTING/DEMO_REQUEST.md](TESTING/DEMO_REQUEST.md).
Endpoints: [docs/API_CONTRACTS.md](API_CONTRACTS.md) → Organization service → Demo requests.

---

## Flow

```
/ (лендинг) → «Запросить демо»
      │
      ▼
 /demo — форма: имя, рабочий email, телефон?, компания, должность?,
         размер отдела продаж, комментарий?, согласие на обработку ПДн
         + отдельное необязательное согласие на рассылку
      │
      ▼
 POST /demo-requests                      ← anonymous, no tenant
      │
      ├── honeypot filled ──► 202, ничего не сохранено, письма нет
      ├── same email again  ──► 429 + Retry-After
      ▼
 202 { id, submittedAt }
      │  ├─► строка в organization-db.DemoRequests (Status = New)
      │  └─► письмо на внутренний адрес продаж
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
real risk. Three measures, in order of how much they matter:

**1. No email is ever sent to the submitter.** The one notification goes to a fixed,
configured internal address. Sending a confirmation to whatever address was typed into a public
form turns the endpoint into a mail relay aimed at arbitrary third parties, and the abuse costs
the platform its sending reputation rather than costing the abuser anything. The visitor is told
on screen instead — that is what the success panel is for.

**2. Honeypot.** A `website` text input, visually hidden and `tabIndex={-1}`, that no person ever
sees. If it comes back non-empty the request persists nothing and sends nothing, but still
returns a normal `202` with a freshly minted id. The response must be indistinguishable from a
real one — a bot that can tell the difference just stops filling the field.

It is positioned off-screen rather than `display: none`, because the field being reachable at all
is what makes naive form-fillers fill it.

**3. Per-email cooldown.** A second submission from the same normalized address inside
`DemoRequests:SubmissionCooldownSeconds` (default 300) gets `429` with `Retry-After`, modelled on
identity-service's resend-code cooldown.

There is **no CAPTCHA and no per-IP limit.** No rate-limiting middleware exists anywhere in this
backend, and introducing one for a single low-traffic form would be the wrong place to introduce
it. The cooldown is keyed on email, so it stops the accidental double-submit and the lazy script,
not a determined attacker with a wordlist. If leads start arriving as junk, per-IP limiting at the
gateway is the next step, not a bigger form.

---

## Why these fields

Eight fields, six of them one line. Long B2B demo forms trade completion rate for qualification
data, and at this stage the platform has no sales team large enough to need the data more than it
needs the lead.

| Field | Required | Why |
|---|---|---|
| `fullName` | yes | Somebody has to be addressed by name in the reply |
| `workEmail` | yes | The reply channel, and the cooldown key |
| `phone` | no | Faster for some buyers, a dealbreaker to demand from others |
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

`Status` moves `New → Contacted → Qualified | Declined`. Nothing enforces the order — it is a
label for a human working a list, not a state machine, and pretending otherwise would only
produce a validation error on the day somebody marks a lead qualified before logging the call.

---

## Configuration

| Key | Environment variable | Default | Meaning |
|---|---|---|---|
| `DemoRequests:NotificationEmail` | `DemoRequests__NotificationEmail` | *(empty)* | Where the lead notification is sent |
| `DemoRequests:NotificationRecipientName` | `DemoRequests__NotificationRecipientName` | `Sellevate` | Display name on that email |
| `DemoRequests:SubmissionCooldownSeconds` | `DemoRequests__SubmissionCooldownSeconds` | `300` | Per-email resubmission cooldown |

An unset `NotificationEmail` logs a warning and skips the send — **the lead is still persisted**.
The same is true when MailerSend itself fails: sending is wrapped and the failure never turns into
an error the visitor sees. A lead saved with nobody notified is recoverable from the admin list; a
lead refused because the mail provider was down is gone.
