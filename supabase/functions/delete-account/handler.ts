// delete-account (TRD §3): the caller deletes their own account.
// Deleting the auth user is enough: profiles cascades from it, and scores, saves and push_tokens cascade from
// profiles, all in one transaction (supabase/tests/rls_test.sql checks it).
export type Env = { url: string; anonKey: string; serviceKey: string };

const reply = (status: number, body: unknown) => Response.json(body, { status });

export async function handle(req: Request, env: Env, send: typeof fetch = fetch): Promise<Response> {
  if (req.method !== "POST") return reply(405, { ok: false, reason: "method" });

  // The id comes from the caller's verified token, never from the request body.
  const who = await send(`${env.url}/auth/v1/user`, {
    headers: { apikey: env.anonKey, Authorization: req.headers.get("Authorization") ?? "" },
  });
  if (who.status >= 500) return reply(503, { ok: false });
  const userId = who.ok ? (await who.json()).id : null;
  if (!userId) return reply(401, { ok: false, reason: "not_signed_in" });

  const gone = await send(`${env.url}/auth/v1/admin/users/${encodeURIComponent(userId)}`, {
    method: "DELETE",
    headers: { apikey: env.serviceKey, Authorization: `Bearer ${env.serviceKey}` },
  });
  return gone.ok ? reply(200, { ok: true }) : reply(503, { ok: false });
}
