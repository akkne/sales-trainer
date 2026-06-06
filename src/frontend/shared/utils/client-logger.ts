"use client";

type LogLevel = "debug" | "info" | "warn" | "error";

const LEVEL_MAP: Record<LogLevel, number> = {
  debug: 20,
  info: 30,
  warn: 40,
  error: 50,
};

function send(level: LogLevel, message: string, context?: Record<string, unknown>) {
  const entry = {
    level: LEVEL_MAP[level],
    message,
    time: Date.now(),
    ...context,
  };

  if (process.env.NODE_ENV === "development") {
    const consoleFn = level === "error" ? console.error : level === "warn" ? console.warn : console.log;
    consoleFn(`[${level.toUpperCase()}]`, message, context ?? "");
  }

  fetch("/api/logs", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(entry),
  }).catch(() => {});
}

export const clientLogger = {
  debug: (message: string, ctx?: Record<string, unknown>) => send("debug", message, ctx),
  info:  (message: string, ctx?: Record<string, unknown>) => send("info",  message, ctx),
  warn:  (message: string, ctx?: Record<string, unknown>) => send("warn",  message, ctx),
  error: (message: string, ctx?: Record<string, unknown>) => send("error", message, ctx),
};
