// send-push (TRD §3): an admin with 2FA sends one message to every phone subscribed to the FCM topic `all`.
export type Env = { url: string; anonKey: string; serviceAccount: string };

// The admin panel calls this from a browser. Auth is a bearer token, not a cookie.
const cors = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, apikey, content-type, x-client-info",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};
const reply = (status: number, body: unknown) => Response.json(body, { status, headers: cors });

const base64url = (data: string | Uint8Array) =>
  btoa(String.fromCharCode(...(typeof data === "string" ? new TextEncoder().encode(data) : data)))
    .replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");

// Google service account sign-in: a signed JWT exchanged for a short-lived access token.
async function accessToken(account: { client_email: string; private_key: string }, send: typeof fetch, now: number) {
  const der = Uint8Array.from(atob(account.private_key.replace(/-----[^-]+-----|\s/g, "")), (c) => c.charCodeAt(0));
  const algorithm = { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" };
  const key = await crypto.subtle.importKey("pkcs8", der, algorithm, false, ["sign"]);
  const iat = Math.floor(now / 1000);
  const unsigned = base64url(JSON.stringify({ alg: "RS256", typ: "JWT" })) + "." + base64url(JSON.stringify({
    iss: account.client_email,
    scope: "https://www.googleapis.com/auth/firebase.messaging",
    aud: "https://oauth2.googleapis.com/token",
    iat,
    exp: iat + 600,
  }));
  const signature = await crypto.subtle.sign(algorithm, key, new TextEncoder().encode(unsigned));
  const res = await send("https://oauth2.googleapis.com/token", {
    method: "POST",
    body: new URLSearchParams({
      grant_type: "urn:ietf:params:oauth:grant-type:jwt-bearer",
      assertion: `${unsigned}.${base64url(new Uint8Array(signature))}`,
    }),
  });
  if (!res.ok) throw new Error("token " + res.status);
  return (await res.json()).access_token as string;
}

export async function handle(req: Request, env: Env, send: typeof fetch = fetch, now = Date.now()): Promise<Response> {
  if (req.method === "OPTIONS") return new Response(null, { status: 204, headers: cors });
  if (req.method !== "POST") return reply(405, { ok: false, reason: "method" });

  // The database decides who is an admin (listed and signed in with 2FA), using the caller's own token.
  const who = await send(`${env.url}/rest/v1/rpc/is_admin`, {
    method: "POST",
    headers: {
      apikey: env.anonKey,
      Authorization: req.headers.get("Authorization") ?? "",
      "Content-Type": "application/json",
    },
    body: "{}",
  });
  if (who.status >= 500) return reply(503, { ok: false });
  if (!who.ok || await who.json() !== true) return reply(403, { ok: false, reason: "not_admin" });

  const message = await req.json().catch(() => null);
  const fits = (v: unknown, max: number) => typeof v === "string" && v.trim().length > 0 && [...v].length <= max;
  if (!fits(message?.title, 60) || !fits(message?.body, 200)) return reply(400, { ok: false, reason: "bad_text" });

  try {
    const account = JSON.parse(env.serviceAccount);
    const token = await accessToken(account, send, now);
    const sent = await send(`https://fcm.googleapis.com/v1/projects/${account.project_id}/messages:send`, {
      method: "POST",
      headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
      body: JSON.stringify({
        message: { topic: "all", notification: { title: message.title, body: message.body } },
      }),
    });
    if (!sent.ok) throw new Error("send " + sent.status);
  } catch (e) {
    // Never log the service account itself.
    console.error("send-push failed:", e instanceof SyntaxError ? "bad FIREBASE_SERVICE_ACCOUNT" : (e as Error).message);
    return reply(502, { ok: false, reason: "send_failed" });
  }
  return reply(200, { ok: true });
}
