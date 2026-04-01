"use client";

/**
 * Browser-side logger. Sends log entries to /api/logs, which forwards them to Loki.
 * Use this in Client Components instead of lib/logger.ts.
 */

type LogLevel = "debug" | "info" | "warn" | "error";

// Pino numeric levels
const LEVEL_MAP: Record<LogLevel, number> = {
  debug: 20,
  info: 30,
  warn: 40,
  error: 50,
};

function send(level: LogLevel, msg: string, context?: Record<string, unknown>) {
  const entry = {
    level: LEVEL_MAP[level],
    msg,
    time: Date.now(),
    ...context,
  };

  if (process.env.NODE_ENV === "development") {
    // eslint-disable-next-line no-console
    const consoleFn = level === "error" ? console.error : level === "warn" ? console.warn : console.log;
    consoleFn(`[${level.toUpperCase()}]`, msg, context ?? "");
  }

  fetch("/api/logs", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(entry),
    // fire-and-forget: do not await
  }).catch(() => {});
}

export const clientLogger = {
  debug: (msg: string, ctx?: Record<string, unknown>) => send("debug", msg, ctx),
  info:  (msg: string, ctx?: Record<string, unknown>) => send("info",  msg, ctx),
  warn:  (msg: string, ctx?: Record<string, unknown>) => send("warn",  msg, ctx),
  error: (msg: string, ctx?: Record<string, unknown>) => send("error", msg, ctx),
};
