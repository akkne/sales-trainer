# EMAIL_NOTIFICATIONS.md — Email side channel for notifications

The notification service delivers selected notifications to the recipient **by email**, in
addition to the in-app bell. Email is an **opt-in** channel: each notification carries a
`SendEmail` flag, and only flagged types are mailed. HTML is generated inside the notification
service by a small OOP template subsystem.

## What gets emailed

| Trigger | Event consumed | Email timing | Template |
|---|---|---|---|
| You just registered (onboarding) | `user.registered` | Immediate (after the user replica is populated) | `WelcomeEmailTemplate` |
| Someone sends you a friend request | `friend.request.received` | Immediate (at notification creation) | `FriendRequestEmailTemplate` |
| Someone accepts your friend request | `friend.request.accepted` | Immediate (at notification creation) | `FriendRequestAcceptedEmailTemplate` |
| New direct message left unread | `chat.message.sent` (+ `chat.message.read` to cancel) | **Delayed** — only if still unread after the grace period (default 5 min) | `ChatMessageEmailTemplate` |
| Someone replies to your discussion thread | `discuss.reply.created` | Immediate (at notification creation) | `DiscussReplyEmailTemplate` |
| Weekly league rollover (promoted/demoted/new week) | `league.updated` | Immediate | `LeagueUpdatedEmailTemplate` |

Achievement and streak notifications are **not** emailed (in-app only).

## Shared email transport

The MailerSend transport was moved out of the identity service into
`Sellevate.BuildingBlocks.Email` so every service can send mail with one call:

```csharp
services.AddSellevateEmail(configuration);   // binds MailerSend config + IEmailSender
```

`IEmailSender.SendEmailAsync(EmailMessage)` takes `{ RecipientEmail, RecipientName, Subject,
HtmlBody, TextBody }`. When the API token is a placeholder the sender logs and no-ops, so
local/dev runs without credentials. Identity (verification codes) and notification both consume it.

**Token configuration.** `MAILERSEND_API_TOKEN` must be the API token **only**. The from-email
and from-name have their own variables (`MAILERSEND_FROM_EMAIL` / `MAILERSEND_FROM_NAME`); do not
append them to the token line. The sender trims surrounding whitespace, and treats a token that
contains *internal* whitespace as misconfigured — it no-ops and logs an actionable **error**
("ApiToken contains whitespace …") instead of silently 401-ing against MailerSend. This was a
real prod outage: the token line had the from-email/name appended, so every send failed.

**Every mailing service needs the MailerSend env vars.** Because the no-op-on-placeholder
behavior is silent (a `warning`, not an error), a service that calls `AddSellevateEmail` but is
*missing* `MailerSend__ApiToken` / `MailerSend__FromEmail` / `MailerSend__FromName` in its
container keeps the appsettings placeholder `"INJECTED_FROM_ENV"` and quietly mails nothing.
This bit prod: friend-request emails never went out because only the **identity** container had
the MailerSend block in `docker-compose.yml` — the **notification** container (the one that
actually mails friend/discussion notifications) did not. The three vars are now injected into
the notification service too. When adding any new mailing service, wire the same three vars into
its container env in `docker-compose.yml` *and* any production manifest.

## Resolving the recipient's email (Redis user replica)

The notification service has **no database**, so it keeps a minimal user replica in Redis
(`notifications:user:{userId}` → `{ email, displayName }`), fed by `UserReplicaConsumer` from
Identity's `user.registered`/`user.updated`/`user.deleted` events. `IUserDirectory` reads it;
if a recipient has no replicated email the email is skipped (logged), never throwing.

**Welcome email ordering.** The welcome email is the one type triggered from
`UserReplicaConsumer` rather than `NotificationEventConsumer`: those two consumers share a Kafka
group on **disjoint** topic sets, so `user.registered` can only be owned by one of them. The
welcome notification is created *after* the replica upsert in the same handler, which guarantees
the dispatcher can resolve the brand-new user's email. It is deduped on the user id, so a Kafka
replay of `user.registered` re-upserts the replica but never re-welcomes.

## OOP HTML generation

All rendering lives under `Features/Notifications/Emails/`:

- **`INotificationEmailTemplate`** — one template per `NotificationType`.
- **`NotificationEmailTemplate`** (abstract, template-method) — owns the render algorithm
  (assemble HTML + text + CTA) and exposes `BuildSubject`/`BuildHeadline`/`BuildContentHtml`/
  `BuildTextBody`/`ActionLabel` for subclasses. Provides shared `GreetingHtml`/`Paragraph` helpers.
- **`NotificationEmailLayout`** — the shared, inline-styled, client-safe HTML chrome (header,
  card, CTA button, footer) and the `Encode` helper. All untrusted text is HTML-encoded here.
- **Concrete templates** — `WelcomeEmailTemplate`, `FriendRequestEmailTemplate`,
  `FriendRequestAcceptedEmailTemplate`, `ChatMessageEmailTemplate`, `DiscussReplyEmailTemplate`,
  `LeagueUpdatedEmailTemplate`, plus `GenericNotificationEmailTemplate` as the fallback.
- **`NotificationEmailRenderer`** — indexes templates by type, rewrites the notification's
  relative action path into an absolute frontend URL, and dispatches to the right template
  (falling back to generic for unmapped types).
- **`NotificationEmailDispatcher`** — resolves the recipient, renders, and sends; failures are
  logged and swallowed so email problems never break in-app storage or trigger Kafka redelivery.

To add an email for a new type: add a `NotificationType`, register a template subclass, set
`SendEmail: true` in the mapper. No layout or transport changes needed.

## Delayed unread-chat email

The "email me if I didn't read it within 5 minutes" rule, implemented Redis-only:

1. `chat.message.sent` → an in-app notification is created immediately (`SendEmail` stays
   false), and `RedisDelayedChatEmailScheduler` queues a pending email in a sorted set scored
   by `messageSentAt + ChatUnreadDelayMinutes`.
2. `chat.message.read` (published by Social's `POST /chat/conversations/{id}/read`) records a
   per-(recipient, conversation) **read watermark** in Redis.
3. `DelayedChatEmailDispatcherService` (a `BackgroundService`) polls every
   `DispatcherPollIntervalSeconds`, atomically claims due pending emails (Lua range+remove),
   and emails each one **unless** the watermark shows the conversation was read at/after the
   message was sent.

### Configuration (`NotificationEmail` section)

| Key | Default | Meaning |
|---|---|---|
| `ChatUnreadDelayMinutes` | 5 | Grace period before an unread message is emailed |
| `DispatcherPollIntervalSeconds` | 30 | How often the dispatcher flushes due emails |
| `BookkeepingRetentionHours` | 24 | TTL on the read-watermark keys |

`MailerSend` (`ApiToken`, `FromEmail`, …) and `Frontend:Url` (base for absolute action links)
are also read from configuration; secrets are injected from the environment.

## Producers

- **Identity** — publishes `user.registered` on email and first-time Google sign-up; the
  notification service both replicates the user and sends the welcome email off this event.
- **Social** — publishes `friend.request.received` (recipient notified on new + reactivated
  requests), `friend.request.accepted` (requester notified when accepted),
  `discuss.reply.created` (thread author notified on a non-self reply) and
  `chat.message.read` (from the new read endpoint); already published `chat.message.sent`.
- **Gamification** — publishes `league.updated` per member during the weekly rollover, staged
  in the transactional outbox so it commits atomically with the rollover.

See the event catalogue in [MICROSERVICES.md §4.1](MICROSERVICES.md) and the test plan in
[TESTING/NOTIFICATION_SERVICE.md](TESTING/NOTIFICATION_SERVICE.md).
