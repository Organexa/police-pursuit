# TESTING — Police Pursuit

_Version 1.0 · 2026-11-25_

> **What this doc is:** what gets tested, where it runs and how results are reported. The rules behind it are in RULES §4.6 and TRD §7.

## 1. Overview
- Every push runs `test.yml` on GitHub Actions. If a test fails, the build doesn't run.
- You don't run anything yourself. The only exception is the short phone checklist in §3.
- Some things can't be tested in CI: Google sign-in, real ads, real purchases and frame rate on a phone. Those are in §3.

| Layer | Tool | Runs in |
|---|---|---|
| Game logic | Unity Test Framework, EditMode | `test.yml` (GameCI) |
| Game flow | Unity Test Framework, PlayMode | `test.yml` (GameCI) |
| Edge Functions | `deno test` | `test.yml` |
| Database rules | SQL tests on a local Supabase in CI (`supabase start` + `supabase test db`, pgTAP) | `test.yml` |

## 2. Automated tests

### 2.1 EditMode (pure C#, fast)
| Area | Cases |
|---|---|
| Score formula | Every row of `/shared/score_cases.json` |
| Score queue (F8) | Add a run; at 50 runs the oldest is dropped; an item is removed only on server OK; the same run id isn't added twice |
| Cloud save merge (F6) | coins = max, best = max, cars = union, settings = local; works with an empty cloud save and with an empty local save |
| Version compare (F11) | `1.10.0` > `1.9.3`; equal versions; bad input means "no update" |
| Garage (F3) | Unlock with enough coins works; without enough it's refused and coins don't change |
| Settings / tutorial (F2, F4) | Save and load give back the same values; a missing file gives the defaults |
| Ads pacing (F9) | Interstitial at most once every 3 runs; never when "Remove ads" is owned |

### 2.2 PlayMode (scene runs headless)
- **Scripted run:** start a run, apply damage until zero, then check that Busted → Results shows the right score.
- **Pause:** while paused, time scale is 0 and audio is paused.

### 2.3 Edge Functions (Deno)
| Function | Must accept | Must reject |
|---|---|---|
| `submit-score` | A valid run; its score is recomputed and matches `score_cases.json` | No/invalid token; distance > top speed × duration × 1.1; near misses > duration × 2; wanted level outside 1–5; duration > 3600 s; played more than 7 days (+5 min) ago or in the future; more than 30 per hour; unknown car. A duplicate run id returns the first result and adds no new row |
| `delete-account` | A valid user token: afterwards no rows remain for that user in any table, and the auth user is gone | No/invalid token |
| `send-push` | An admin with `aal2`; title ≤ 60, body ≤ 200 | Non-admin; admin with only `aal1`; text that is too long |

FCM is replaced with a fake in tests, so no real push is sent.

### 2.4 Database rules (RLS)
These run as real database roles (`anon`, a signed-in player, a second player, an admin with `aal1`, an admin with `aal2`).

| Check | Expected |
|---|---|
| Every table in `public` has RLS on | Test lists all tables and fails if any has RLS off, so a new table without RLS can't slip in |
| Player A reads or writes player B's `saves`, `push_tokens`, `profiles` row | 0 rows / refused |
| Anyone (anon, player, admin) inserts into `scores` directly | Refused |
| Anyone reads `scores` with `hidden = true` | Not returned |
| Save over 64 KB | Refused |
| Display name of 0 or 21+ characters | Refused |
| Admin with `aal1` updates `scores.hidden` or `app_config` | Refused |
| Admin with `aal2` does the same | Allowed |
| Anyone inserts into `admins` from the app | Refused |
| `anon` reads `leaderboard` and `app_config` | Allowed |

## 3. Manual checks on a phone
Done once per release with a test build from the GitHub Release. I'll send you the list for each release; it takes about 15 minutes.

- [ ] Install and first launch: the tutorial starts and can be skipped.
- [ ] A full run reaches Results; pause works.
- [ ] Medium graphics feel smooth (about 60 fps).
- [ ] Google sign-in works; cancelling keeps you a guest; sign-out works.
- [ ] A score appears on the leaderboard.
- [ ] Airplane mode: play a run, go back online, and the score uploads.
- [ ] Test ads: the consent prompt shows first; "Continue" and "Double coins" work.
- [ ] Test purchase with a licence-tester account: "Remove ads" works and comes back after reinstall.
- [ ] A push sent from the admin panel arrives. Saying no to notifications causes no problems.
- [ ] "Delete my account" removes the account.

## 4. Honest reporting
- I report results exactly as they come out: what passed, what failed (with the log lines), and what wasn't run.
- A test is never deleted or weakened just to get green. If a test is wrong, I say why before changing it.
- "Done" means the tests for that ticket pass in CI, not just on my side.
