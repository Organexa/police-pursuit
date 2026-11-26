"use strict";
// Every rule is enforced by the database and the Edge Function; this page only shows and sends.
// Player-typed text goes in with textContent, never innerHTML.

const $ = (id) => document.getElementById(id);
const say = (text) => { $("status").textContent = text; };
const when = (iso) => new Date(iso).toLocaleString();

let db = null;
let factorId = null;

// Supabase answers { data, error }; turn the error into a throw.
async function get(query) {
  const result = await query;
  if (result.error) throw new Error(result.error.message);
  return result;
}

// Shows failures instead of leaving the page silent, and blocks double submits.
function on(element, event, work) {
  element.addEventListener(event, async (e) => {
    e.preventDefault();
    const buttons = element.matches("button") ? [element] : [...element.querySelectorAll("button")];
    buttons.forEach((b) => { b.disabled = true; });
    say("");
    try {
      await work(e);
    } catch (error) {
      say("Failed: " + error.message);
    } finally {
      buttons.forEach((b) => { b.disabled = false; });
    }
  });
}

function view(id) {
  for (const name of ["login", "mfa", "panel"]) $(name).hidden = name !== id;
}

// Password done -> 2FA code -> admin check.
async function next() {
  const { data: level } = await get(db.auth.mfa.getAuthenticatorAssuranceLevel());
  if (level.currentLevel !== "aal2") return askCode();
  const { data: admin } = await get(db.rpc("is_admin"));
  if (admin !== true) {
    await db.auth.signOut();
    view("login");
    say("This account is not an admin.");
    return;
  }
  view("panel");
  await open("dashboard");
}

async function askCode() {
  const { data: factors } = await get(db.auth.mfa.listFactors());
  const verified = factors.totp.find((f) => f.status === "verified");
  $("enroll").hidden = !!verified;
  if (verified) {
    factorId = verified.id;
  } else {
    // A half-finished setup blocks a new one.
    for (const f of factors.all.filter((f) => f.status === "unverified")) {
      await get(db.auth.mfa.unenroll({ factorId: f.id }));
    }
    const { data } = await get(db.auth.mfa.enroll({ factorType: "totp" }));
    factorId = data.id;
    $("qr").src = data.totp.qr_code;
    $("secret").textContent = data.totp.secret;
  }
  view("mfa");
}

const loaders = {
  async dashboard() {
    const midnight = new Date();
    midnight.setHours(0, 0, 0, 0);
    const [players, runs, top] = await Promise.all([
      get(db.from("profiles").select("*", { count: "exact", head: true })),
      get(db.from("scores").select("*", { count: "exact", head: true }).gte("created_at", midnight.toISOString())),
      get(db.from("scores").select("score").eq("hidden", false).order("score", { ascending: false }).limit(1)),
    ]);
    $("playerCount").textContent = players.count;
    $("runsToday").textContent = runs.count;
    $("topScore").textContent = top.data[0]?.score ?? "none yet";
  },

  async players() {
    const q = $("search").elements.q.value.trim().replace(/[\\%_]/g, "\\$&");
    let query = db.from("admin_players").select("display_name,best_score,last_seen_at");
    if (q) query = query.ilike("display_name", `%${q}%`);
    const { data } = await get(query.order("last_seen_at", { ascending: false }).limit(100));
    fill("playerRows", data, (p) => [p.display_name, p.best_score ?? "-", when(p.last_seen_at)]);
  },

  async scores() {
    const { data } = await get(db.from("scores")
      .select("id,score,car_id,played_at,hidden,hidden_reason,profiles(display_name)")
      .order("score", { ascending: false }).limit(200));
    fill("scoreRows", data, (s) => {
      const button = document.createElement("button");
      button.type = "button";
      button.textContent = s.hidden ? "Restore" : "Remove (cheater)";
      on(button, "click", () => setHidden(s));
      return [s.profiles?.display_name, s.score, s.car_id, when(s.played_at), s.hidden ? s.hidden_reason : "", button];
    });
  },

  async notice() {
    const { data } = await get(db.from("app_config").select("*").eq("id", 1).single());
    const form = $("noticeForm").elements;
    form.latest.value = data.latest_version;
    form.critical.value = data.critical_min_version;
    form.message.value = data.message ?? "";
  },

  async push() {},
};

function fill(id, rows, cells) {
  $(id).replaceChildren(...rows.map((row) => {
    const tr = document.createElement("tr");
    for (const value of cells(row)) {
      const td = document.createElement("td");
      td.append(value instanceof Node ? value : String(value ?? ""));
      tr.append(td);
    }
    return tr;
  }));
}

async function open(tab) {
  for (const button of document.querySelectorAll("nav [data-tab]")) {
    const current = button.dataset.tab === tab;
    button.setAttribute("aria-current", current);
    $(button.dataset.tab).hidden = !current;
  }
  await loaders[tab]();
}

// The database refuses quietly (0 rows) when the session is not an aal2 admin, so count what changed.
async function change(query) {
  const { data } = await get(query.select());
  if (data.length === 0) throw new Error("not allowed. Sign out and sign in again.");
}

async function setHidden(score) {
  let reason = null;
  if (!score.hidden) {
    reason = prompt("Why remove this score?")?.trim();
    if (!reason) return;
  }
  await change(db.from("scores").update({ hidden: !score.hidden, hidden_reason: reason }).eq("id", score.id));
  await loaders.scores();
}

// Numbers compared part by part, as the game does: 1.10.0 is newer than 1.9.3.
function newer(a, b) {
  const x = a.split(".").map(Number);
  const y = b.split(".").map(Number);
  for (let i = 0; i < Math.max(x.length, y.length); i++) {
    const difference = (x[i] ?? 0) - (y[i] ?? 0);
    if (difference !== 0) return difference > 0;
  }
  return false;
}

async function saveNotice() {
  const form = $("noticeForm").elements;
  const latest = form.latest.value.trim();
  const critical = form.critical.value.trim();
  if (![latest, critical].every((v) => /^\d+(\.\d+){1,3}$/.test(v))) {
    throw new Error("versions look like 1.2.0");
  }
  // Otherwise players would be blocked with nothing to update to.
  if (newer(critical, latest)) throw new Error("the critical minimum cannot be newer than the latest version");
  if (!confirm(`Players older than ${critical} will be blocked until they update. Save?`)) return;
  await change(db.from("app_config")
    .update({ latest_version: latest, critical_min_version: critical, message: form.message.value.trim() || null })
    .eq("id", 1));
  say("Saved.");
}

async function sendPush() {
  const form = $("pushForm").elements;
  const title = form.title.value.trim();
  const body = form.body.value.trim();
  if (!title || !body) throw new Error("write a title and a message");
  if (!confirm(`Send to ALL players now?\n\n${title}\n${body}`)) return;
  await get(db.functions.invoke("send-push", { body: { title, body } }));
  $("pushForm").reset();
  showPreview();
  say("Sent.");
}

function showPreview() {
  const form = $("pushForm").elements;
  $("previewTitle").textContent = form.title.value;
  $("previewBody").textContent = form.body.value;
}

on($("login"), "submit", async () => {
  const form = $("login").elements;
  await get(db.auth.signInWithPassword({ email: form.email.value, password: form.password.value }));
  $("login").reset();
  await next();
});

on($("mfa"), "submit", async () => {
  const code = $("mfa").elements.code.value;
  $("mfa").reset();
  await get(db.auth.mfa.challengeAndVerify({ factorId, code }));
  $("qr").removeAttribute("src");
  $("secret").textContent = "";
  await next();
});

for (const button of document.querySelectorAll("nav [data-tab]")) {
  on(button, "click", () => open(button.dataset.tab));
}
// Reload so no player data stays on the page.
on($("logout"), "click", async () => {
  await db.auth.signOut();
  location.reload();
});
on($("search"), "submit", () => loaders.players());
on($("noticeForm"), "submit", saveNotice);
on($("pushForm"), "submit", sendPush);
$("pushForm").addEventListener("input", showPreview);

if (!SUPABASE_URL || !SUPABASE_ANON_KEY) {
  say("Not set up yet: add the project URL and anon key to config.js.");
} else if (!window.supabase) {
  say("Could not load the Supabase library. Check your connection and reload.");
} else {
  // sessionStorage: closing the tab ends the admin session.
  db = window.supabase.createClient(SUPABASE_URL, SUPABASE_ANON_KEY, { auth: { storage: sessionStorage } });
  db.auth.getSession()
    .then(({ data }) => (data.session ? next() : view("login")))
    .catch((error) => { view("login"); say("Failed: " + error.message); });
}
