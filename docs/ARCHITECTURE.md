# ARCHITECTURE — Police Pursuit

_Version 1.0 · 2026-11-25 · Status: **Approved 2026-11-25**_

> **What this doc is:** the big-picture map of the pieces, how they talk to each other, and where secrets live. The exact versions, tables and APIs are in `TRD.md`.

## 1. The pieces at a glance
```
                         ┌──────────────── GitHub (public repo, $0) ────────────────┐
                         │  Code + docs                                              │
                         │  Actions: test → build APK/AAB (GameCI) → Release         │
                         │  Actions: deploy backend (Supabase CLI)                   │
                         │  Actions: keep-alive ping (every 3 days)                  │
                         │  Pages:  Admin web panel (static site)                    │
                         └───────────────┬───────────────────────────┬──────────────┘
                                         │ APK / AAB                 │ deploys
                                         ▼                           ▼
┌──────────────────────────────┐   HTTPS (anon key + user token)   ┌──────────────────────────┐
│  ANDROID PHONE               │ ────────────────────────────────► │  SUPABASE (free plan)    │
│  Unity game (URP)            │                                   │  Auth: Google, email+2FA │
│  • offline-first local save  │ ◄──────────────────────────────── │  Postgres + RLS          │
│  • score queue               │   leaderboard, save, app_config   │  Edge Functions:         │
│  • LevelPlay ads  ───────────┼──► Unity LevelPlay                │   submit-score           │
│  • Unity IAP     ────────────┼──► Google Play Billing            │   delete-account         │
│  • FCM receiver  ◄───────────┼─── Firebase Cloud Messaging ◄─────┤   send-push (admin only) │
└──────────────────────────────┘                                   └────────────▲─────────────┘
                                                                                │ email + password
                                                         ┌──────────────────────┴───┐ + 6-digit code
                                                         │  ADMIN PANEL (you)       │
                                                         │  GitHub Pages website    │
                                                         └──────────────────────────┘
```

## 2. Why these choices
| Piece | Choice | Why |
|---|---|---|
| Game engine | Unity 6 LTS + URP | Best mobile 3D quality per effort; free under Unity Personal |
| Builds | GitHub Actions + GameCI | Cloud builds, nothing to install, $0 on a public repo |
| Backend | Supabase (one project) | Login, database, server code and admin 2FA in one free service |
| Server talk from Unity | Plain HTTPS (`UnityWebRequest`) to Supabase REST | No extra SDK to maintain; Supabase's API is simple JSON |
| Push | Firebase Cloud Messaging | Free, standard on Android |
| Ads / IAP | Unity LevelPlay / Unity IAP | Official Unity packages, already chosen in PRD |
| Admin panel | Static HTML + JS on GitHub Pages | Free hosting; all security is enforced by Supabase, not the website |

## 3. Main flows

### 3.1 Launch and update check
1. The game shows the splash screen and reads `app_config` from Supabase (public, read-only).
2. If `critical_min_version` > installed version → "Please update" screen with a Play Store button.
3. If `latest_version` > installed → small "Update available" banner.
4. If offline → skip the check and play normally.

### 3.2 Playing a run (works fully offline)
1. All gameplay runs on the phone. Coins, unlocked cars, settings and best score are saved locally (a JSON file in the app's private storage).
2. At the end of a run the result goes into a local **score queue**.
3. When the phone is online and the player is signed in, the queue is sent to `submit-score`. Sent items are removed only after the server says OK, so nothing is lost.

### 3.3 Google sign-in (optional)
1. The Android **Credential Manager** shows Google's account picker and returns a Google ID token. A small Android plugin inside the project handles this.
2. The game hands that token to Supabase Auth (`grant_type=id_token`), which returns a Supabase session.
3. **No deep links or browser redirects are needed**, which matches the PRD's "no deep links in v1".
4. Guests can play everything except the leaderboard and cloud save.

### 3.4 Cloud save
- **When it syncs:** on sign-in, the game pulls the cloud save and merges it with the local save.
  - Coins and best score: the higher value wins.
  - Unlocked cars: both lists are combined.
- **Then:** it pushes after each run and after each purchase.
- **Limit:** one row per player, readable and writable only by that player (RLS).

### 3.5 Leaderboard and anti-cheat
- **What the game sends:** the raw run stats (duration, distance, near-misses, top wanted level, car), not just a score.
- **What `submit-score` does:**
  1. Recomputes the score from those stats using the same formula as the game.
  2. Rejects runs that aren't physically possible. For example, distance can't exceed the car's top speed × duration.
  3. Rate-limits each player and drops runs older than 7 days.
  4. Only then writes the row.
- **Who can write:** players can **read** the top 100 and their own rank. They can't write scores directly; only the function can.
- **Admin:** you can hide a score from the panel with a reason. It's kept in the database for your records.

### 3.6 Account deletion
The **Delete my account** button calls `delete-account`. That function deletes the player's save, scores, push token and Supabase user, then the game wipes local online data and signs out.

### 3.7 Push notifications
1. The game registers with FCM and stores its device token in `push_tokens` (signed-in players) or skips it (guests).
2. In the admin panel you write a message and press Send.
3. That calls `send-push`. The function checks you are an admin with 2FA, then calls FCM, which delivers to all players subscribed to the `all` topic.

### 3.8 Ads and purchases
- **Consent:** a consent popup shows first where the law requires one. Its answer is passed to LevelPlay before any ad loads.
- **Rewarded ads:** "Continue" (once per run) and "Double coins".
- **Interstitials:** at most 1 every 3 runs, on the Results screen only.
- **Purchases:**
  - IAPs go through Unity IAP to Google Play Billing.
  - The receipt is checked on the phone with Google Play's public key (Unity IAP's built-in check).
  - "Remove ads" is restored automatically on reinstall from Google Play.

## 4. Security model (where secrets live)
| Secret | Where it lives | Ever in the public repo? |
|---|---|---|
| Supabase **anon (public) key** + project URL | Inside the game and admin panel | Yes, by design. It's safe to publish; RLS rules decide what it can do |
| Supabase **service role key** | Only inside Supabase Edge Functions (built-in env var) | **Never** |
| Supabase access token (for deploys) | GitHub Secret `SUPABASE_ACCESS_TOKEN` | Never |
| Unity license (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`) | GitHub Secrets | Never |
| Android signing keystore + passwords | GitHub Secrets (created by a one-time workflow, see `DEPLOYMENT.md`) | Never |
| Firebase service account (for sending push) | Supabase function secret | Never |
| Google Play public key (IAP) | Obfuscated in the game, as Unity IAP requires | Yes. It's a public key |
| Your admin password + 2FA app | Only with you | Never |

**Rules enforced by the database (RLS):**
- Players touch only their own rows.
- The leaderboard is read-only.
- Admin tables and actions need an admin account **and** a session that passed 2FA (`aal2`).
- So a stolen admin password alone isn't enough.

**You never paste a secret into chat.** You enter each one yourself in GitHub or Supabase settings, following the guide.

## 5. Unity license plan (open risk #1, now decided)
The official GameCI docs say Unity **Personal** licenses can only be created from Unity Hub. There is no cloud-only way. (The cloud-only "serial" route needs a paid Pro plan, so we don't use it.)

**One-time step for you (about 10 minutes, guided in `DEPLOYMENT.md`):**
1. Install Unity Hub (free) and sign in with a free Unity account.
2. Go to Preferences → Licenses → **Add** → "Get a free personal license".
3. Open the hidden file `C:\ProgramData\Unity\Unity_lic.ulf` in Notepad and copy everything.
4. In GitHub → Settings → Secrets and variables → Actions, add `UNITY_LICENSE` (the file text), `UNITY_EMAIL` and `UNITY_PASSWORD`.
5. Sign out of Unity Hub, then uninstall it if you like.

The license works for cloud builds on Linux even though it was made on Windows.

## 6. Keeping the free plans free
- **Supabase pause:** free projects pause after 7 idle days. A scheduled GitHub Action reads `app_config` every 3 days to keep the project awake.
- **Limits watched:** players, database size and bandwidth stay far below the free limits for a new game (50,000 monthly players, 500 MB). If usage reaches 80% of a limit, I'll tell you before anything could cost money.
- **No paid dependency** anywhere in this design.

## 7. Repository layout
```
/unity/            Unity project (Assets, Packages, ProjectSettings)
/unity/Assets/Plugins/Android/   small Credential Manager sign-in plugin
/supabase/         migrations (SQL), Edge Functions (TypeScript), config
/admin/            admin web panel (HTML, CSS, JS)
/.github/workflows/  test, build, deploy-backend, deploy-admin, keep-alive, create-keystore
/docs/             all project documents
```

## 8. What can fail and what happens
| Failure | What the player sees |
|---|---|
| No internet | Game plays normally; scores wait in the queue |
| Supabase down or paused | Same as offline; leaderboard shows "Can't connect" |
| Ad has no fill | The "Continue" / "Double coins" button is hidden for that moment |
| Sign-in cancelled | Stays a guest, nothing lost |
| Bad or cheating score | Server rejects it quietly; it never appears on the board |
