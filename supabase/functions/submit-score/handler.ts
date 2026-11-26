// submit-score (TRD §3). The phone sends raw run stats; the score is computed here, never trusted.
import cars from "../../../shared/cars.json" with { type: "json" };

export type Run = {
  duration_s: number;
  distance_m: number;
  near_misses: number;
  max_wanted: number;
  car_id: string;
  played_at: string;
};

export type Env = { url: string; anonKey: string; serviceKey: string };

const HOUR = 3_600_000;
export const MAX_PER_HOUR = 30;

export const score = (r: Run) =>
  Math.floor(r.distance_m) + r.near_misses * 250 + Math.floor(r.duration_s * r.max_wanted * 10);

// Fields present, right types, not negative. Null if not.
export function parseRun(body: unknown): Run | null {
  const r = body as Run;
  const num = (v: unknown) => typeof v === "number" && Number.isFinite(v) && v >= 0;
  const ok = !!r && num(r.duration_s) && num(r.distance_m) &&
    num(r.near_misses) && Number.isInteger(r.near_misses) &&
    Number.isInteger(r.max_wanted) && typeof r.car_id === "string" &&
    typeof r.played_at === "string" &&
    /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?(Z|[+-]\d\d:\d\d)$/.test(r.played_at) &&
    !Number.isNaN(Date.parse(r.played_at));
  return ok ? r : null;
}

// Null when the run is possible, otherwise why not.
export function rejectReason(r: Run, now: number): string | null {
  const car = cars.cars.find((c) => c.id === r.car_id);
  if (!car) return "unknown_car";
  if (r.duration_s > 3600) return "too_long";
  if (r.distance_m > car.top_speed_mps * r.duration_s * 1.1) return "too_far";
  if (r.near_misses > r.duration_s * 2) return "too_many_near_misses";
  if (r.max_wanted < 1 || r.max_wanted > 5) return "bad_wanted_level";
  const played = Date.parse(r.played_at);
  if (played > now + 5 * 60_000 || played < now - 7 * 24 * HOUR) return "bad_time";
  return null;
}

const reply = (status: number, body: unknown) => Response.json(body, { status });

export async function handle(req: Request, env: Env, send: typeof fetch = fetch, now = Date.now()): Promise<Response> {
  if (req.method !== "POST") return reply(405, { ok: false, reason: "method" });

  const who = await send(`${env.url}/auth/v1/user`, {
    headers: { apikey: env.anonKey, Authorization: req.headers.get("Authorization") ?? "" },
  });
  if (who.status >= 500) return reply(503, { ok: false });
  const userId = who.ok ? (await who.json()).id : null;
  if (!userId) return reply(401, { ok: false, reason: "not_signed_in" });

  const run = parseRun(await req.json().catch(() => null));
  if (!run) return reply(400, { ok: false, reason: "bad_fields" });

  const db = (query: string, init: RequestInit = {}) =>
    send(`${env.url}/rest/v1/scores${query}`, {
      ...init,
      headers: {
        apikey: env.serviceKey,
        Authorization: `Bearer ${env.serviceKey}`,
        "Content-Type": "application/json",
        ...init.headers,
      },
    });

  // Before the other checks, so a retried upload is still OK a week later or over the rate limit.
  const same = await db("?" + new URLSearchParams({
    select: "score", user_id: `eq.${userId}`, played_at: `eq.${run.played_at}`,
  }));
  if (!same.ok) return reply(503, { ok: false });
  const existing = await same.json();
  if (existing.length > 0) return reply(200, { ok: true, score: existing[0].score });

  const reason = rejectReason(run, now);
  if (reason) return reply(400, { ok: false, reason });

  // shortcut: count-then-insert, parallel requests can slip past 30; the board only shows each player's best.
  const recent = await db("?" + new URLSearchParams({
    select: "id", user_id: `eq.${userId}`, created_at: `gt.${new Date(now - HOUR).toISOString()}`,
    limit: String(MAX_PER_HOUR),
  }));
  if (!recent.ok) return reply(503, { ok: false });
  // 429, not 400: the game keeps the run and sends it later.
  if ((await recent.json()).length >= MAX_PER_HOUR) return reply(429, { ok: false, reason: "rate_limit" });

  const { duration_s, distance_m, near_misses, max_wanted, car_id, played_at } = run;
  const added = await db("", {
    method: "POST",
    body: JSON.stringify({ user_id: userId, duration_s, distance_m, near_misses, max_wanted, car_id, played_at }),
  });
  // 409 with unique_violation: the same run arrived twice at once.
  if (!added.ok && !(added.status === 409 && (await added.json()).code === "23505")) {
    return reply(503, { ok: false });
  }
  return reply(200, { ok: true, score: score(run) });
}
