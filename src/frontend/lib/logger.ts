/**
 * Server-side logger: pino → Loki (direct push).
 * Import only in Server Components, API Routes, and middleware — never in Client Components.
 */
import pino from "pino";

function buildLogger() {
  const lokiUrl = process.env.LOKI_URL ?? "http://loki:3100";
  const isDev = process.env.NODE_ENV === "development";

  const targets: pino.TransportTargetOptions[] = [
    {
      target: "pino-loki",
      level: "info",
      options: {
        host: lokiUrl,
        labels: {
          service: "salestrainer-frontend",
          env: process.env.NODE_ENV ?? "development",
        },
        batching: true,
        interval: 5,
      },
    },
  ];

  if (isDev) {
    targets.push({
      target: "pino-pretty",
      level: "debug",
      options: { colorize: true },
    });
  }

  return pino(
    {
      level: isDev ? "debug" : "info",
      base: { service: "salestrainer-frontend" },
    },
    pino.transport({ targets })
  );
}

export const logger = buildLogger();
