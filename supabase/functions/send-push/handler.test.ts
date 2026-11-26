// Runs with `deno test` (CI) and `node --test`.
import test from "node:test";
import assert from "node:assert/strict";
import { handle } from "./handler.ts";

const algorithm = { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256", modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]) };

// A throwaway key made for each test run.
async function fakeAccount() {
  const pair = await crypto.subtle.generateKey(algorithm, true, ["sign", "verify"]);
  const der = new Uint8Array(await crypto.subtle.exportKey("pkcs8", pair.privateKey));
  const pem = `-----BEGIN PRIVATE KEY-----\n${btoa(String.fromCharCode(...der))}\n-----END PRIVATE KEY-----\n`;
  return {
    publicKey: pair.publicKey,
    json: JSON.stringify({ project_id: "p1", client_email: "svc@p1.test", private_key: pem }),
  };
}

const decode = (part: string) =>
  Uint8Array.from(atob(part.replace(/-/g, "+").replace(/_/g, "/")), (c) => c.charCodeAt(0));

async function call(body: unknown, serviceAccount: string, { admin = true as unknown, fcm = 200 } = {}) {
  const calls: { url: string; init?: RequestInit }[] = [];
  const send = ((url: string, init?: RequestInit) => {
    calls.push({ url, init });
    if (url.includes("is_admin")) return Promise.resolve(Response.json(admin));
    if (url.includes("oauth2")) return Promise.resolve(Response.json({ access_token: "t" }));
    return Promise.resolve(Response.json({}, { status: fcm }));
  }) as typeof fetch;
  const res = await handle(
    new Request("http://x", { method: "POST", body: JSON.stringify(body) }),
    { url: "http://db", anonKey: "a", serviceAccount },
    send,
    1_000_000_000,
  );
  return { status: res.status, calls };
}

const message = { title: "Daily bonus", body: "Come and drive" };

test("non-admins and bad text are refused and nothing is sent", async () => {
  const { json } = await fakeAccount();
  const cases = [
    [await call(message, json, { admin: false }), 403],
    [await call(message, json, { admin: "yes" }), 403],
    [await call({ title: "x".repeat(61), body: "b" }, json), 400],
    [await call({ title: "t", body: "x".repeat(201) }, json), 400],
    [await call({ title: " ", body: "b" }, json), 400],
    [await call({ title: "t" }, json), 400],
  ] as const;
  for (const [r, status] of cases) {
    assert.equal(r.status, status);
    assert.equal(r.calls.length, 1); // only the admin check
  }
});

test("an admin's message goes to topic all with a signed service account token", async () => {
  const account = await fakeAccount();
  const r = await call({ title: "x".repeat(60), body: "y".repeat(200) }, account.json);
  assert.equal(r.status, 200);
  const assertion = new URLSearchParams(r.calls[1].init!.body as URLSearchParams).get("assertion")!;
  const [head, claims, signature] = assertion.split(".");
  assert.ok(await crypto.subtle.verify(algorithm, account.publicKey, decode(signature),
    new TextEncoder().encode(`${head}.${claims}`)));
  assert.equal(JSON.parse(new TextDecoder().decode(decode(claims))).iss, "svc@p1.test");
  assert.match(r.calls[2].url, /projects\/p1\/messages:send$/);
  assert.equal(JSON.parse(r.calls[2].init!.body as string).message.topic, "all");
});

test("the browser preflight allows the headers supabase-js sends", async () => {
  const res = await handle(new Request("http://x", { method: "OPTIONS" }), { url: "", anonKey: "", serviceAccount: "" });
  assert.equal(res.status, 204);
  const allowed = res.headers.get("Access-Control-Allow-Headers")!;
  for (const name of ["authorization", "apikey", "content-type", "x-client-info"]) assert.ok(allowed.includes(name), name);
});

test("a failed send or missing secret is reported", async () => {
  const { json } = await fakeAccount();
  assert.equal((await call(message, json, { fcm: 403 })).status, 502);
  assert.equal((await call(message, "")).status, 502);
});
