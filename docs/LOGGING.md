# Logging Architecture

## Stack

| Component | Technology | Role |
|-----------|-----------|------|
| Backend logs | Serilog + `Serilog.Sinks.Grafana.Loki` | Structured logs → Loki (direct push) |
| Frontend server-side logs | Pino + `pino-loki` | Structured logs → Loki (direct push) |
| Frontend browser logs | `clientLogger` → `POST /api/logs` → pino-loki | Browser logs forwarded through Next.js API route |
| Metrics | `prometheus-net.AspNetCore` → Prometheus | HTTP counters, durations, .NET runtime metrics |
| Storage: logs | Grafana Loki 3.4.2 | Append-only log store, label-based indexing |
| Storage: metrics | Prometheus 3.2.1 | Time-series metrics |
| Visualisation | Grafana 11.6.0 | Unified dashboard (logs + metrics) |

## Decisions

### D1 — Direct push over Promtail/Alloy
Applications push logs directly to Loki via HTTP. No sidecar agent needed. Chosen because:
- Fewer containers, less operational overhead in development
- Single-node setup with no log file persistence
- Pino-loki and Serilog.Sinks.Grafana.Loki handle batching internally

Trade-off: if Loki is down, logs during that window are lost (not buffered to disk). Acceptable for a development environment; revisit with Alloy if going to production.

### D2 — Prometheus for metrics, not OpenTelemetry traces
User chose Loki + Prometheus (not full PLT stack). OpenTelemetry tracing (Tempo) excluded from scope. Can be added later — `prometheus-net` is compatible with OTEL.

### D3 — Browser logging via API proxy
Browsers cannot push directly to Loki (CORS, no auth). Solution: `clientLogger.ts` → `POST /api/logs` → server logger → Loki. This keeps Loki credentials server-side.

### D4 — `pino-loki` as singleton transport in Next.js
In `lib/logger.ts`, the pino transport is built once at module load. With `output: "standalone"` and a persistent Node.js server (not Edge runtime, not lambda), the transport stays alive between requests and batches correctly.

## Label Schema

### Backend (Serilog → Loki)
| Label | Value |
|-------|-------|
| `service` | `sallevate-backend` |
| `env` | `Development` / `Production` |
| `RequestId` | per-request correlation ID (from Serilog context) |

### Frontend server (pino-loki)
| Label | Value |
|-------|-------|
| `service` | `sallevate-frontend` |
| `env` | `development` / `production` |

### Frontend browser (via /api/logs)
Same as frontend server labels + `source: "browser"` in log fields.

## LogQL Quick Reference

```logql
# All errors from any service
{service=~"sallevate-.+"} |= "Error" or level = "error"

# Backend errors only
{service="sallevate-backend"} | json | level = "Error"

# Frontend browser errors
{service="sallevate-frontend"} | json | source = "browser" | level >= 50

# Logs for a specific request
{service="sallevate-backend"} | json | RequestId = "<id>"

# Rate of log lines per minute by service
rate({service=~"sallevate-.+"}[1m])
```

## PromQL Quick Reference

```promql
# HTTP request rate (all)
rate(http_requests_received_total{job="sallevate-backend"}[1m])

# p95 latency
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket{job="sallevate-backend"}[5m]))

# Error rate (5xx)
rate(http_requests_received_total{job="sallevate-backend", code=~"5.."}[1m])
```

## Access URLs (local docker-compose)

| Service | URL | Credentials |
|---------|-----|-------------|
| Grafana | http://localhost:3001 | admin / admin |
| Loki API | http://localhost:3100 | — |
| Prometheus | http://localhost:9090 | — |

## How to Use in Code

### Server Component / API Route (Next.js)
```typescript
import { logger } from "@/lib/logger";

logger.info("User signed in", { userId: "abc123" });
logger.error("Failed to fetch lessons", { error: err.message, userId });
```

### Client Component (React)
```typescript
import { clientLogger } from "@/lib/clientLogger";

clientLogger.error("API call failed", { path: "/api/skills", status: 500 });
```

### Backend (C# / Serilog)
```csharp
// Injected via ILogger<T>
_logger.LogInformation("Exercise submitted {@Request}", request);
_logger.LogError(ex, "Failed to evaluate exercise {ExerciseId}", id);
```
Serilog enriches every log entry with `RequestId` and `Application` properties automatically.

## Extending

- **Add a new service**: set label `service` and push to `http://loki:3100/loki/api/v1/push`
- **Add tracing**: integrate `OpenTelemetry.Instrumentation.AspNetCore` + Tempo; Serilog can emit `TraceId` automatically
- **Production hardening**: replace direct push with Grafana Alloy agent to add disk buffering and retry on Loki unavailability
