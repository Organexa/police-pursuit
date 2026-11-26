import { handle } from "./handler.ts";

// All three are provided by Supabase to every Edge Function.
const env = {
  url: Deno.env.get("SUPABASE_URL")!,
  anonKey: Deno.env.get("SUPABASE_ANON_KEY")!,
  serviceKey: Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
};

Deno.serve((req) => handle(req, env));
