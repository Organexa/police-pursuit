// Runs with `deno test` (CI) and `node --test`.
import test from "node:test";
import assert from "node:assert/strict";
import shared from "../../../shared/score_cases.json" with { type: "json" };
import { handle, parseRun, rejectReason, type Run, score } from "./handler.ts";

const now = Date.parse("2026-11-26T12:00:00Z");
const good: Run = {
  duration_s: 60,
  distance_m: 1500,
  near_misses: 3,
  max_wanted: 2,
  car_id: "starter",
  played_at: "2026-11-26T11:00:00.1234567Z",
};

test("score matches the shared cases", () => {
  for (const c of shared.cases) assert.equal(score(c as unknown as Run), c.expected);
});

test("bad fields are refused", () => {
  assert.ok(parseRun(good));
  for (const field of Object.keys(good)) assert.equal(parseRun({ ...good, [field]: undefined }), null, field);
  for (
    const bad of [
      null,
      { ...good, distance_m: -1 },
      { ...good, duration_s: "60" },
      { ...good, near_misses: 1.5 },
      { ...good, max_wanted: 2.5 },
      { ...good, played_at: "yesterday" },
      { ...good, played_at: "2026-11-26" },
    ]
  ) assert.equal(parseRun(bad), null, JSON.stringify(bad));
});

test("impossible runs are refused", () => {
  const why = (change: Partial<Run>) => rejectReason({ ...good, ...change }, now);
  assert.equal(why({}), null);
  assert.equal(why({ car_id: "rocket" }), "unknown_car");
  assert.equal(why({ duration_s: 3601 }), "too_long");
  assert.equal(why({ distance_m: 30 * 60 * 1.1 }), null);
  assert.equal(why({ distance_m: 30 * 60 * 1.1 + 1 }), "too_far");
  assert.equal(why({ near_misses: 120 }), null);
  assert.equal(why({ near_misses: 121 }), "too_many_near_misses");
  assert.equal(why({ max_wanted: 0 }), "bad_wanted_level");
  assert.equal(why({ max_wanted: 6 }), "bad_wanted_level");
  assert.equal(why({ played_at: "2026-11-26T12:06:00Z" }), "bad_time");
  assert.equal(why({ played_at: "2026-11-19T11:59:00Z" }), "bad_time");
});

// A fake Supabase: `rows` already stored for this player, `recent` uploads in the last hour.
async function call(body: unknown, { user = true, rows = [] as unknown[], recent = 0, insert = 201 } = {}) {
  const inserted: Record<string, unknown>[] = [];
  const send = ((url: string, init?: RequestInit) => {
    if (url.includes("/auth/v1/user")) {
      return Promise.resolve(user ? Response.json({ id: "u1" }) : new Response("{}", { status: 401 }));
    }
    if (init?.method === "POST") {
      inserted.push(JSON.parse(init.body as string));
      return Promise.resolve(Response.json({ code: "23505" }, { status: insert }));
    }
    return Promise.resolve(Response.json(url.includes("created_at") ? Array(recent).fill({ id: 1 }) : rows));
  }) as typeof fetch;
  const res = await handle(
    new Request("http://x", { method: "POST", body: JSON.stringify(body) }),
    { url: "http://db", anonKey: "a", serviceKey: "s" },
    send,
    now,
  );
  return { status: res.status, body: await res.json(), inserted };
}

test("a good run is stored with a server score", async () => {
  const r = await call({ ...good, score: 999999, user_id: "someone-else", hidden: false });
  assert.deepEqual([r.status, r.body], [200, { ok: true, score: 1500 + 750 + 1200 }]);
  assert.deepEqual(r.inserted, [{ user_id: "u1", ...good }]);
});

test("nothing is written when refused, repeated or rate limited", async () => {
  const cases = [
    [await call(good, { user: false }), 401],
    [await call({ ...good, car_id: "rocket" }), 400],
    [await call("junk"), 400],
    [await call(good, { rows: [{ score: 7 }], recent: 30 }), 200],
    [await call(good, { recent: 30 }), 429],
  ] as const;
  for (const [r, status] of cases) {
    assert.equal(r.status, status);
    assert.equal(r.inserted.length, 0);
  }
});

test("a duplicate insert is OK, another insert error is retried", async () => {
  assert.equal((await call(good, { insert: 409 })).status, 200);
  assert.equal((await call(good, { insert: 500 })).status, 503);
});
