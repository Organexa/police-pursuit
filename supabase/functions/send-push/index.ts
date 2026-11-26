import { handle } from "./handler.ts";

const env = {
  url: Deno.env.get("SUPABASE_URL")!,
  anonKey: Deno.env.get("SUPABASE_ANON_KEY")!,
  // Set by you in Edge Functions -> Secrets (DEPLOYMENT §3).
  serviceAccount: Deno.env.get("FIREBASE_SERVICE_ACCOUNT") ?? "",
};

Deno.serve((req) => handle(req, env));
