import { logger } from "@/shared/utils/logger";

interface BrowserLogEntry {
  level: number;
  message: string;
  time?: number;
  [key: string]: unknown;
}

export async function POST(request: Request): Promise<Response> {
  let entry: BrowserLogEntry;
  try {
    entry = await request.json();
  } catch {
    return new Response(null, { status: 400 });
  }

  const { level, message, time: _time, ...rest } = entry;
  const child = logger.child({ source: "browser", ...rest });

  if (level >= 60) child.fatal(message);
  else if (level >= 50) child.error(message);
  else if (level >= 40) child.warn(message);
  else if (level >= 30) child.info(message);
  else child.debug(message);

  return new Response(null, { status: 204 });
}
