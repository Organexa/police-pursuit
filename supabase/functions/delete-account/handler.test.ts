// Runs with `deno test` (CI) and `node --test`.
import test from "node:test";
import assert from "node:assert/strict";
import { handle } from "./handler.ts";

async function call({ signedIn = true, deleteStatus = 200, method = "POST" } = {}) {
  const deleted: string[] = [];
  const send = ((url: string, init?: RequestInit) => {
    if (init?.method === "DELETE") {
      assert.equal((init.headers as Record<string, string>).Authorization, "Bearer service");
      deleted.push(url);
      return Promise.resolve(new Response("{}", { status: deleteStatus }));
    }
    return Promise.resolve(signedIn ? Response.json({ id: "me" }) : new Response("{}", { status: 401 }));
  }) as typeof fetch;
  const res = await handle(
    // A body naming someone else must be ignored.
    new Request("http://x", { method, body: method === "POST" ? '{"id":"someone-else"}' : undefined }),
    { url: "http://db", anonKey: "a", serviceKey: "service" },
    send,
  );
  return { status: res.status, deleted };
}

test("deletes only the caller", async () => {
  assert.deepEqual(await call(), { status: 200, deleted: ["http://db/auth/v1/admin/users/me"] });
});

test("nothing is deleted without a valid token", async () => {
  assert.deepEqual(await call({ signedIn: false }), { status: 401, deleted: [] });
  assert.deepEqual(await call({ method: "GET" }), { status: 405, deleted: [] });
});

test("a failed delete is not reported as done", async () => {
  assert.equal((await call({ deleteStatus: 500 })).status, 503);
});
