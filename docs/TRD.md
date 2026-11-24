# TRD — Police Pursuit (Technical Requirements Document)

_Version 1.0 · 2026-11-25 · Status: **Approved 2026-11-25** (together with ARCHITECTURE.md)_

> **What this doc is:** the exact technical details: versions, packages, database tables, server functions, formulas and targets. The big picture is in `ARCHITECTURE.md`.

## 1. Versions and packages
| Area | Choice |
|---|---|
| Engine | Unity 6 LTS (6000.x, latest patch that GameCI publishes an image for) |
| Render pipeline | URP (Universal Render Pipeline) |
| UI | uGUI + TextMeshPro |
| Input | Input System package (touch buttons + accelerometer for tilt) |
| Camera | Cinemachine 3 |
| Ads | `com.unity.services.levelplay` |
| Purchases | `com.unity.purchasing` (Unity IAP) |
| Push | Firebase Cloud Messaging Unity SDK (`FirebaseMessaging.unitypackage`, only the Messaging module) |
| JSON | `com.unity.nuget.newtonsoft-json` |
| Tests | Unity Test Framework (EditMode + PlayMode) |
| Sign-in plugin | Small Java/Kotlin Android plugin using AndroidX Credential Manager + Google ID library |
| Android target | Min SDK 24 (Android 7), target SDK = what Google Play currently requires, IL2CPP, ARM64 + ARMv7 |
| Backend | Supabase (Postgres 15+, Auth, Edge Functions in Deno/TypeScript) |
| Admin panel | Plain HTML + CSS + JavaScript, `@supabase/supabase-js` v2 loaded from a pinned CDN version |
| CI | GitHub Actions, `game-ci/unity-test-runner@v4`, `game-ci/unity-builder@v4`, `supabase/setup-cli` |

## 2. Database tables (all with Row Level Security ON)
| Table | Key columns | Who can read | Who can write |
|---|---|---|---|
| `profiles` | `id` (= auth user), `display_name`, `created_at`, `last_seen_at` | Everyone: `display_name` only (for the leaderboard). Owner: all | Owner: `display_name` only |
| `scores` | `id`, `user_id`, `score`, `duration_s`, `distance_m`, `near_misses`, `max_wanted`, `car_id`, `played_at`, `hidden`, `hidden_reason` | Everyone: rows where `hidden = false` | Only `submit-score` (service role). Admin (aal2): `hidden`, `hidden_reason` |
| `saves` | `user_id` (PK), `data` (jsonb), `updated_at` | Owner | Owner |
| `push_tokens` | `user_id`, `token`, `updated_at` | Owner | Owner |
| `app_config` | `id = 1`, `latest_version`, `critical_min_version`, `message` | Everyone | Admin (aal2) |
| `admins` | `user_id` | Admin (aal2) | Nobody from the app (set once by SQL) |

- **Leaderboard view:** a `leaderboard` view shows each player's best non-hidden score, top 100, with `display_name`.
- **Own rank:** an RPC `my_rank()` returns the signed-in player's position.
- **Admin check:** an `is_admin()` SQL function. It checks that `auth.uid()` is in `admins` **and** that the JWT `aal` claim is `aal2` (2FA done).
- **Save size limit:** `data` is capped at 64 KB by a check constraint.

## 3. Edge Functions
All functions require a signed-in user's token (`Authorization: Bearer <jwt>`) and return JSON.

### `submit-score`
- **Input:** `{ duration_s, distance_m, near_misses, max_wanted, car_id, played_at }`
- **Checks (any failure → `400 { ok: false, reason }`, nothing written):**
  1. All fields present, numbers, not negative.
  2. `car_id` is a known car.
  3. `distance_m ≤ car_top_speed_mps × duration_s × 1.1` (10% margin for boosts).
  4. `near_misses ≤ duration_s × 2` and `max_wanted` between 1 and 5.
  5. `duration_s ≤ 3600`.
  6. `played_at` is within the last 7 days and not in the future (+5 min margin).
  7. Rate limit: max 30 scores per player per hour.
- **On success:** recomputes the score server-side, inserts the row, returns `200 { ok: true, score }`.
- A duplicate (same user + `played_at`) returns `ok: true` without inserting again, so the offline queue can retry safely.

### `delete-account`
- **Input:** none.
- Deletes the user's `scores`, `saves`, `push_tokens` and `profiles` rows, then the auth user. Returns `200 { ok: true }`.

### `send-push`
- **Input:** `{ title, body }` (title ≤ 60 chars, body ≤ 200 chars).
- Only works if `is_admin()` is true (admin + 2FA). Otherwise `403`.
- Sends to the FCM topic `all` using the FCM HTTP v1 API and the Firebase service account (stored as a Supabase secret).

## 4. Score formula (same code in the game and in `submit-score`)
```
score = floor(distance_m × 1)
      + near_misses × 250
      + floor(duration_s × max_wanted × 10)
```
- The game and the server share one test table of example runs with their expected scores, so the two can't drift apart.

## 5. Local data on the phone
| File | Contents |
|---|---|
| `save.json` (app private storage) | coins, unlocked cars, selected car, best score, settings, tutorial done, consent answer |
| `score_queue.json` | runs waiting to upload (max 50; oldest dropped first) |

- **Cloud merge on sign-in:** coins = max(local, cloud), best = max, unlocked cars = union, settings = local.
- **Shortcut (honest note):** coins live on the phone and in the player's own cloud save, so a determined cheater could edit them. They only affect that player's own game; leaderboard scores are checked on the server. This is normal for single-player games.

## 6. Performance targets
| Preset | Target | Main limits |
|---|---|---|
| Low | 30 fps on a budget phone (~3 GB RAM) | 0.7 render scale, no post-FX except colour grading, ≤ 8 traffic cars |
| Medium | 60 fps on a mid-range phone | 0.85 render scale, bloom + colour grading, ≤ 12 cars |
| High | 60 fps on a recent flagship | 1.0 render scale, all post-FX, reflection probes, ≤ 16 cars |

- **Other targets:** APK/AAB under 150 MB; cold start to main menu under 8 seconds on Medium.
- **First launch:** the preset is picked from `SystemInfo.systemMemorySize` and GPU tier.

## 7. Testing approach
| Kind | What | Where |
|---|---|---|
| EditMode tests | score formula, save merge, score queue, update-check version compare, anti-cheat limits | GameCI test runner |
| PlayMode tests | a scripted run starts, takes damage, ends with Busted and Results | GameCI test runner |
| Edge Function tests | `submit-score` accepts good runs and rejects each bad case | Deno test in CI |
| Database tests | RLS rules: a player can't read/write others' rows; non-2FA admin is refused | SQL tests in CI against a local Supabase |
| Manual | how it feels on a real phone | You, with the APK (see `TESTING.md`) |

## 8. CI workflows (`.github/workflows/`)
| File | Runs when | Does |
|---|---|---|
| `test.yml` | every push / pull request | Unity EditMode + PlayMode tests, Edge Function tests, RLS tests |
| `build.yml` | push to `main` and version tags | Builds a signed APK + AAB, attaches them to a GitHub Release |
| `deploy-backend.yml` | push to `main` that changes `/supabase` | Applies migrations, deploys Edge Functions |
| `deploy-admin.yml` | push to `main` that changes `/admin` | Publishes the admin panel to GitHub Pages |
| `keep-alive.yml` | every 3 days (schedule) | Reads `app_config` so Supabase doesn't pause |
| `create-keystore.yml` | run once by hand | Generates the Android signing key and saves it to GitHub Secrets |
