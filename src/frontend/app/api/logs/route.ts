import { logger } from "@/lib/logger";

interface BrowserLogEntry {
  level: number;
  msg: string;
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

  const { level, msg, time: _time, ...rest } = entry;
  const child = logger.child({ source: "browser", ...rest });

  if (level >= 60) child.fatal(msg);
  else if (level >= 50) child.error(msg);
  else if (level >= 40) child.warn(msg);
  else if (level >= 30) child.info(msg);
  else child.debug(msg);

  return new Response(null, { status: 204 });
}
