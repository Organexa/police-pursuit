# FEATURE TICKETS — Police Pursuit

_Version 1.0 · 2026-11-25_

> **What this doc is:** the build to-do list. Each ticket is one piece of work, with what "done" means (acceptance criteria) and how it's tested. Ticket numbers match the PRD features (F1–F14). T0 tickets set up the project and must come first.

**Order:** T0.1 → T0.2 → T0.3 → F1 → F2 → F3/F4 → F8 → F5/F6/F7 → F11 → F9/F10 → F12/F13 → F14.

**Status key:** ☐ to do · ◐ in progress · ☑ done

---

## T0 — Project setup

### T0.1 ☑ Repository and Unity project
- **Tasks:**
  - Create the folder layout (ARCHITECTURE §7).
  - Create a Unity 6 LTS URP project with the packages from TRD §1.
  - Add `.gitignore` with the secret-file blocks (RULES §2.4).
- **Done when:** the project opens in CI without errors and no secret file can be committed.

### T0.2 ◐ CI workflows
- **Tasks:**
  - Write `test.yml`, `build.yml`, `deploy-backend.yml`, `deploy-admin.yml`, `keep-alive.yml`, `create-keystore.yml` (TRD §8).
- **Done when:**
  - a push runs the tests;
  - `main` produces a signed APK + AAB on a GitHub Release;
  - no secret value appears in any log.

### T0.3 ☐ Supabase schema
- **Tasks:**
  - Migrations for every TRD §2 table, the `leaderboard` view, `my_rank()` and `is_admin()`.
  - RLS on every table; the 64 KB save cap.
- **Done when:** the RLS tests pass (TESTING §2.4) and `deploy-backend.yml` applies the migrations.

---

## F1 ☐ Menu, play, pause, game over
- **Goal:** the core loop. Main menu → run → pause → Busted → Results → menu.
- **Tasks:**
  - Endless road built from tiles that spawn ahead and recycle behind.
  - Player car: steer, drift, boost.
  - Traffic cars, police cars that chase and ram, wanted level 1–5.
  - Damage bar; the run ends at zero ("Busted").
  - HUD: score, coins, wanted stars, damage, pause button.
  - Results screen: score, distance, near misses, coins, best score.
  - Switchable chase/bumper camera (DESIGN).
  - Graphics presets Low/Medium/High (TRD §6).
- **Done when:**
  - a full run works from the menu and back with no errors;
  - pause stops the game and the sound;
  - the score matches the formula (TRD §4) for every case in `/shared/score_cases.json`;
  - Medium holds 60 fps on a mid-range phone (manual check).
- **Tests:** EditMode: score formula. PlayMode: scripted run → damage → Busted → Results.

## F2 ☐ Tutorial
- **Goal:** a short first run that teaches steer, drift and dodge.
- **Done when:**
  - it starts on the first launch only;
  - it can be skipped;
  - it can be replayed from Settings;
  - "tutorial done" is saved in `save.json`.
- **Tests:** EditMode: the tutorial flag saves and loads.

## F3 ☐ Garage
- **Goal:** 5–6 original cars with speed, handling and armor stats.
- **Done when:**
  - cars can be unlocked with coins;
  - premium cars can be bought with an IAP (needs F10);
  - the selected car is saved and used in the next run;
  - each car has a `top_speed_mps` in one shared car list that `submit-score` also uses.
- **Tests:** EditMode: unlock with enough coins works, without enough is refused.

## F4 ☐ Settings
- **Goal:** sound, music, controls, graphics, language, privacy, account.
- **Done when:**
  - sound and music sliders work and are saved;
  - controls switch between buttons and tilt; haptics can be turned off;
  - graphics switch Low/Medium/High at once;
  - language shows English (only choice in v1);
  - "Privacy & ads" reopens the consent choice;
  - "Delete my account" is shown when signed in (F13);
  - all buttons are at least 48 dp.
- **Tests:** EditMode: settings save and load.

## F5 ☐ Google sign-in (optional)
- **Goal:** sign in with Google without leaving the app.
- **Tasks:** Android plugin (Credential Manager → Google ID token) → Supabase `grant_type=id_token`.
- **Done when:**
  - guests can play everything except the leaderboard and cloud save;
  - sign-in returns a Supabase session that is stored and refreshed;
  - cancelling keeps the player as a guest with nothing lost;
  - sign-out works.
- **Tests:** manual on a real phone (Google sign-in can't run in CI).

## F6 ☐ Cloud save
- **Goal:** coins, unlocked cars and best score follow the player's Google account.
- **Done when:**
  - on sign-in the cloud save is merged: coins = max, best = max, cars = union, settings = local;
  - it is pushed after each run and each purchase;
  - a save over 64 KB is refused by the database.
- **Tests:** EditMode: merge rules. RLS: a player can't read another player's save.

## F7 ☐ Leaderboard
- **Goal:** global top 100 plus your own rank, checked by the server.
- **Done when:**
  - the board shows top 100 names and scores;
  - your rank shows when signed in;
  - "Can't connect" shows when offline;
  - `submit-score` rejects every bad case in TRD §3 and never trusts a score from the phone;
  - hidden scores never show.
- **Tests:** Deno: every accept/reject case. RLS: nobody can insert into `scores` directly.

## F8 ☐ Offline play
- **Goal:** the game works with no internet; scores upload later.
- **Done when:**
  - runs go into `score_queue.json` (max 50, oldest dropped first);
  - the queue is sent when online and signed in;
  - an item is removed only after the server says OK;
  - a retry of the same run doesn't create a duplicate.
- **Tests:** EditMode: queue add/limit/remove-on-OK.

## F9 ☐ Ads
- **Goal:** rewarded ads and limited interstitials through LevelPlay.
- **Done when:**
  - the consent choice shows first where the law requires it, and is passed to LevelPlay before any ad loads;
  - "Continue" (once per run) and "Double coins" work with rewarded ads;
  - interstitials show at most once every 3 runs, on Results only, never during a run;
  - with no ad fill, the button is hidden;
  - "Remove ads" (F10) stops interstitials.
- **Tests:** EditMode: the "1 in 3 runs" rule. Manual: test ads on a phone.
- **Open item:** LevelPlay built-in consent vs Google UMP, decided during this ticket.

## F10 ☐ In-app purchases
- **Goal:** remove ads, coin packs, premium cars, through Google Play Billing.
- **Done when:**
  - all products are bought through Unity IAP;
  - receipts are checked on the phone with Unity IAP's built-in check;
  - "Remove ads" and premium cars are restored after reinstall;
  - a cancelled or failed purchase changes nothing.
- **Tests:** manual with Google Play test accounts (licence testers, no real money).

## F11 ☐ Update notice
- **Goal:** tell players about new versions.
- **Done when:**
  - `latest_version` > installed → "Update available" banner;
  - `critical_min_version` > installed → forced "Please update" screen with a Play Store button;
  - offline → no check, normal play.
- **Tests:** EditMode: version compare (e.g. `1.10.0` > `1.9.3`).

## F12 ☐ Push notifications
- **Goal:** daily-bonus and news messages through FCM.
- **Done when:**
  - the game subscribes to topic `all`;
  - signed-in players store their token in `push_tokens`;
  - Android 13+ asks for the notification permission, and saying no causes no problems;
  - a message sent from the admin panel arrives.
- **Tests:** Deno: `send-push` refuses non-admins and too-long text. Manual: a message arrives.

## F13 ☐ Account deletion
- **Goal:** an in-app button that removes all online data (a Google Play rule).
- **Done when:**
  - "Delete my account" asks to confirm, then calls `delete-account`;
  - scores, save, push token, profile and auth user are gone;
  - the game signs out and clears local online data;
  - a public web page explains how to request deletion (Play Console needs this link).
- **Tests:** Deno/SQL: after the call no rows remain for that user.

## F14 ☐ Admin web panel
- **Goal:** your website for running the game.
- **Done when:**
  - sign-in needs email, password and a 6-digit 2FA code;
  - without 2FA (`aal1`) every admin action is refused by the database;
  - you can see players and scores, and hide/unhide a score with a reason;
  - you can edit `latest_version`, `critical_min_version` and the message;
  - you can send a push (title ≤ 60, body ≤ 200);
  - no secret key is in the site's code (only the anon key).
- **Tests:** RLS: non-admin and `aal1` admin are refused. Manual: each action once.
