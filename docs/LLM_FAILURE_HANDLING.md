# LLM_FAILURE_HANDLING.md — how a failing AI call must behave

> Cross-cutting contract for every code path that calls an LLM or a speech API.
> Applies to `ai-service` (Dialog, Voice, Transcription, Evaluation, Companies) and
> `learning-service` (exercise chat + voice).

## The rule

**A provider failure is an upstream condition, not a defect in our service.**

An invalid request to the LLM, a quota wall, a flapping gateway or a garbage response
body must degrade into a meaningful status code and a `Warning` log line. It must never
become an unhandled exception — which the ASP.NET pipeline turns into a 500 and logs at
`Error`, making an ordinary bad prompt look like the service is broken.

## Exception hierarchy

Both services define the same shape (`Sellevate.Ai.Features.Dialog.Models` /
`Sellevate.Learning.Infrastructure.Ai`):

```
OpenAiException                  (abstract base — controllers catch THIS)
├── OpenAiRequestException       provider rejected the request / returned an unusable body
│                                  carries the upstream StatusCode
├── OpenAiPaymentRequiredException   402
├── OpenAiRateLimitException         429
└── OpenAiAuthenticationException    401 / 403
```

Controllers catch the **base type**, so a failure mode added later cannot bypass them
just because nobody wrote a new `catch` clause for it.

## Status mapping

| Upstream | Exception | HTTP out | Log level |
|---|---|---|---|
| 400 and other 4xx | `OpenAiRequestException` | 502 (dialog) / 503 (elsewhere) | `Warning` |
| 401 / 403 | `OpenAiAuthenticationException` | 503 | `Warning` |
| 402 | `OpenAiPaymentRequiredException` | 402 | `Warning` |
| 429 | `OpenAiRateLimitException` | 429 | `Warning` |
| 5xx | `OpenAiRequestException` | 502 / 503 | `Error` |
| non-JSON / unexpected shape on a 200 | `OpenAiRequestException` | 502 / 503 | `Warning` |
| transport failure, retries exhausted, circuit open | `HttpRequestException` | 503 | `Warning` |

Only a genuine provider-side failure (5xx) is logged at `Error`. Everything the provider
*chose* to reject is a `Warning`.

## Never leak the provider body

`TranslateProviderError` logs the response body through `RedactAndTruncate` (strips
`sk-…` keys and `Authorization` / `X-Auth-Token` values, caps at 500 chars) and throws a
**generic** message — `"AI provider error"`. The raw body never reaches the exception
message, and therefore never reaches the client through `ProblemDetails` or a controller
that echoes `exception.Message`.

## Streaming endpoints

Once a stream has written its 200 headers, no status code can be sent. Both
`VoiceDialogController` and `ExerciseController.StreamVoiceMessage` therefore catch
`OpenAiException` and `HttpRequestException` and **end the stream cleanly** — the client
gets a short reply instead of a torn connection.

Client cancellation (`OperationCanceledException`) is logged at `Information` and is never
answered with a fallback reply.

## A refusal is not a failure (Phase 40.28)

The content pipeline has an outcome this document's table does not cover, and conflating the two
would be the most expensive mistake in it: **«материала не хватает» is the feature working.**

| | A provider failure | A sufficiency refusal |
|---|---|---|
| Cause | upstream: quota, transport, an unparseable body | our own judgement about the customer's input |
| Job state | `failed` after 3 attempts, `FailureReason` set | `insufficient` immediately, `Insufficiency` set |
| Retryable | yes — `POST …/retry` resumes the half that failed | no, and retrying would change nothing: `POST …/material` or `PUT …/structure` is the answer |
| Log level | `Warning` / `Error` per the table above | **`Information`** |
| What the customer sees | «попробуйте позже» | a list of what to add, and each item names an artefact they already have |

Three rules follow.

- **Never log a refusal at `Warning`.** Nothing is wrong. A run of refusals against one organization
  *is* a signal — their onboarding never told them what to upload — but it is a product signal, not
  an incident.
- **Never degrade a provider failure into a refusal.** `MaterialStructuringService` throws on an
  unparseable completion rather than returning an empty structure, precisely because an empty
  structure would be read downstream as «ваш материал ничего не содержит» and the РОП would go and
  rewrite a deck that was fine.
- **Never degrade a refusal into an error.** It is a state on the run, with a machine-readable list,
  reachable by polling. A 400 on the start call would make the customer begin again and re-pay for
  structuring the material that was already read.

A missing or malformed `sufficiency` block in an otherwise valid completion is read as
**"sufficient"** — the one place in this pipeline where a parse problem degrades silently, because
degrading the other way would tell a customer their material is thin on the strength of our own
model dropping a field. The deterministic structure check still runs, so nothing empty gets through.

## A finding is not a failure either, and neither is an empty answer (Phase 40.32)

Batch adaptation adds two more outcomes that must not be conflated with a provider failure, and one
of them is the *absence* of an outcome.

| | Provider failure | «Ничего не меняю» / «замечаний нет» | A finding |
|---|---|---|---|
| Cause | upstream | the exercise was already fine | our judgement about the customer's exercise |
| Item state | `failed` after 2 attempts, `FailureReason` set | `unchanged`, resolved without a person | `proposed`, waiting for a person |
| Log level | `Warning` | — | — |
| Reaches the queue | no | **no** | yes |

Four rules follow.

- **The attempt budget is per item, not per batch.** One exercise the model chokes on must not fail
  the fifty-nine good proposals beside it. `POST …/retry` re-queues exactly the failed items.
- **Never degrade a provider failure into an empty rewrite.** `ExerciseRewriteService` throws on an
  unparseable completion; returning `{"content": null}` would be recorded as «переписывать нечего»
  and the customer would conclude their stage is already in their voice when nobody ever read it.
- **Never degrade a malformed proposal into a stored one.** A rewritten body that fails
  `ExerciseContentValidator` is a failed item, not a proposal. A person cannot tell a broken body from
  a good one by reading a diff, and an exercise that blanks the screen mid-lesson is worse than one
  that still sounds generic.
- **An empty answer is a success and must stay cheap to give.** Both prompts say so twice. A rewriter
  that always changes something and a reviewer that always finds something are the same failure: sixty
  cosmetic items teach a person to click through the queue without reading it, which costs the
  credibility of every true finding in it.

A review code this service does not know is **dropped**, not rendered — the vocabulary is closed on
purpose, and a code a model invented would otherwise reach a customer as an empty bullet. That is the
same rule 40.28 applies to refusal codes, and it is applied twice: once on the way out of ai-service,
once when the findings document is written.

## A quota refusal is not a failure either, and it is not a provider quota (Phase 40.33)

The table at the top of this document has an entry for the provider telling us **our** balance is
empty (402, `OpenAiPaymentRequiredException`). Phase 40.33 adds a different thing that looks
identical from three feet away: **the customer's own allowance running out.**

| | A provider payment wall | An organization quota refusal |
|---|---|---|
| Whose money | ours — the platform's API balance | the customer's — what they bought |
| Cause | upstream | our own meter, against a number an operator set |
| HTTP out | **402** | **429** with `{resource, period, used, limit}` |
| Log level | `Warning` | **`Information`** |
| Who fixes it | us, by topping up | sales, by raising the limit — or the customer, by waiting for the month |
| Retryable | yes, once we pay | not this month, unless somebody raises it |

Three rules follow, and the first is the one that will be got wrong.

- **Never answer a quota refusal with 402.** A customer's cap rendered as a payment error reads as
  "Sellevate's card declined" and generates an incident on our side for an event that is our product
  working. 429 is what the voice gate has answered since the feature shipped and what the frontend
  already renders.
- **Never log one at `Warning`.** Nothing is wrong. Exactly the rule 40.28 states for a sufficiency
  refusal, for the same reason: a run of these against one organization *is* a signal, but a
  commercial one. `ai_quota_refusals_total` exists so the signal has somewhere to live that is not an
  alert.
- **A refusal must not be degraded into a provider failure by the caller either.** learning-service's
  `AiChatClient` maps a 429 with no provider failure code onto `OpenAiRateLimitException` — which
  `ExerciseDialogService.GenerateAiResponseAsync` already answers with its neutral reply. That is
  deliberate: a learner mid-exercise sees a conversation that goes quiet, not a broken screen, and the
  real reason is in the log and on the spend report where somebody can act on it.

A fourth case sits beside these: an **unattributed** metered call, one arriving with no
`X-Organization-Id`. It is `400`, not 500 and not a silent pass. It is a caller mistake with a fixed
remedy — forward the header — and reporting it as a server fault would send somebody hunting through
ai-service logs for a bug that lives in the client. Every internal caller forwards it as of 40.33.

**Where the failure contract now travels.** learning-service no longer calls a provider; it calls
ai-service (`POST /ai/chat`, `/ai/chat/stream`, `/ai/tts`). The hierarchy above is unchanged and is
rebuilt on the near side from a **named** failure code in the response body — `payment_required`,
`rate_limited`, `provider_auth`, `provider_rejected`, `provider_failed`, `provider_unreachable` — so
`ExerciseController` and `ExerciseDialogService` catch exactly what they always caught. A status code
alone would not have been enough: 503 covers three different upstream conditions in the table above.
The provider's own body is still redacted and dropped inside `OpenAiChatService` and never starts
travelling again on the way out.

## Graceful degradation, where it exists

Some paths prefer a canned answer over an error:

- `ExerciseDialogService.GenerateAiResponseAsync` — falls back to a neutral reply when the
  provider is unavailable (`Warning`, not `Error`). Cancellation is re-thrown, never faked.
- TTS synthesis failures — the reply is still delivered as text (`Warning`).
- `AiEvaluationStrategyBase.ParseAiResponse` — an unparseable grading response becomes a
  failed-but-valid result rather than a 503.

## Transport resilience

Both services wrap the `OpenAI` and `YandexTts` named clients in the same Polly stack
(`Microsoft.Extensions.Http.Resilience`):

- 30s per attempt, up to 2 retries (≤3 attempts), 90s total
- circuit breaker: 5 failures in a 60s window (`SamplingDuration` ≥ 2 × `AttemptTimeout`,
  otherwise Polly fails host startup validation)
- `HttpClient.Timeout` 90s — the outer bound; Polly controls each attempt

Without this a stalled provider pins a request thread for the default 100s.

## Tests

`OpenAiProviderErrorTests` exists in both test projects and pins the whole contract:
status→exception mapping, the shared base type, body redaction, non-JSON bodies, and
unexpected JSON shapes. **The learning-service copy moved one hop out in 40.33** — it now pins the
translation of ai-service's named failure code back into the same exception types, plus the quota
429, because that service no longer has a provider status to translate. `DialogControllerProviderFailureTests` proves the controller
returns 502/503/429 instead of throwing.

See [TESTING/AI_SERVICE.md](TESTING/AI_SERVICE.md) and
[TESTING/LEARNING_SERVICE.md](TESTING/LEARNING_SERVICE.md).
