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
  - `main` produces a signed APK + AAB (a GitHub Release only for a `v*` tag);
  - no secret value appears in any log.

### T0.3 ☑ Supabase schema
- **Tasks:**
  - Migrations for every TRD §2 table, the `leaderboard` view, `my_rank()` and `is_admin()`.
  - RLS on every table; the 64 KB save cap.
- **Done when:** the RLS tests pass (TESTING §2.4) and `deploy-backend.yml` applies the migrations.

---

## F1 ◐ Menu, play, pause, game over
_Code and tests done 2026-11-26. Open: manual 60 fps check on a mid-range phone._

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

## F2 ◐ Tutorial
_Code and tests done 2026-11-26. Open: manual first-launch check on a phone._

- **Goal:** a short first run that teaches steer, drift and dodge.
- **Done when:**
  - it starts on the first launch only;
  - it can be skipped;
  - it can be replayed from Settings;
  - "tutorial done" is saved in `save.json`.
- **Tests:** EditMode: the tutorial flag saves and loads.

## F3 ◐ Garage
- **Goal:** 5–6 original cars with speed, handling and armor stats.
- **Done when:**
  - cars can be unlocked with coins;
  - premium cars can be bought with an IAP (needs F10);
  - the selected car is saved and used in the next run;
  - each car has a `top_speed_mps` in one shared car list that `submit-score` also uses.
- **Tests:** EditMode: unlock with enough coins works, without enough is refused.

## F4 ◐ Settings
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

## F5 ◐ Google sign-in (optional)
- **Goal:** sign in with Google without leaving the app.
- **Tasks:** Android plugin (Credential Manager → Google ID token) → Supabase `grant_type=id_token`.
- **Done when:**
  - guests can play everything except the leaderboard and cloud save;
  - sign-in returns a Supabase session that is stored and refreshed;
  - cancelling keeps the player as a guest with nothing lost;
  - sign-out works.
- **Tests:** manual on a real phone (Google sign-in can't run in CI).

## F6 ◐ Cloud save
_Code and tests done 2026-11-26. Open: Supabase setup values and a real-phone check._

- **Goal:** coins, unlocked cars and best score follow the player's Google account.
- **Done when:**
  - on sign-in the cloud save is merged: coins = max, best = max, cars = union, settings = local;
  - it is pushed after each run and each purchase;
  - a save over 64 KB is refused by the database.
- **Tests:** EditMode: merge rules. RLS: a player can't read another player's save.

## F7 ◐ Leaderboard
_Code and tests done 2026-11-26. Open: deploy to Supabase and a real-phone check._

- **Goal:** global top 100 plus your own rank, checked by the server.
- **Done when:**
  - the board shows top 100 names and scores;
  - your rank shows when signed in;
  - "Can't connect" shows when offline;
  - `submit-score` rejects every bad case in TRD §3 and never trusts a score from the phone;
  - hidden scores never show.
- **Tests:** Deno: every accept/reject case. RLS: nobody can insert into `scores` directly.

## F8 ◐ Offline play
- **Goal:** the game works with no internet; scores upload later.
- **Done when:**
  - runs go into `score_queue.json` (max 50, oldest dropped first);
  - the queue is sent when online and signed in;
  - an item is removed only after the server says OK;
  - a retry of the same run doesn't create a duplicate.
- **Tests:** EditMode: queue add/limit/remove-on-OK.

## F9 ◐ Ads
_Code and tests done 2026-11-26. Open: LevelPlay IDs and test ads on a phone._

- **Goal:** rewarded ads and limited interstitials through LevelPlay.
- **Done when:**
  - the consent choice shows first where the law requires it, and is passed to LevelPlay before any ad loads;
  - "Continue" (once per run) and "Double coins" work with rewarded ads;
  - interstitials show at most once every 3 runs, on Results only, never during a run;
  - with no ad fill, the button is hidden;
  - "Remove ads" (F10) stops interstitials.
- **Tests:** EditMode: the "1 in 3 runs" rule. Manual: test ads on a phone.
- **Consent (decided 2026-11-26):** own popup, shown to everyone once, passed to LevelPlay. Revisit if AdMob is added (it requires a certified consent tool in the EEA).

## F10 ◐ In-app purchases
_Code and tests done 2026-11-26. Open: public key, Play Console products and test purchases on a phone._

- **Goal:** remove ads, coin packs, premium cars, through Google Play Billing.
- **Done when:**
  - all products are bought through Unity IAP;
  - receipts are checked on the phone with Unity IAP's built-in check;
  - "Remove ads" and premium cars are restored after reinstall;
  - a cancelled or failed purchase changes nothing.
- **Tests:** manual with Google Play test accounts (licence testers, no real money).

## F11 ◐ Update notice
_Code and tests done 2026-11-26. Open: real `app_config` and phone check._

- **Goal:** tell players about new versions.
- **Done when:**
  - `latest_version` > installed → "Update available" banner;
  - `critical_min_version` > installed → forced "Please update" screen with a Play Store button;
  - offline → no check, normal play.
- **Tests:** EditMode: version compare (e.g. `1.10.0` > `1.9.3`).

## F12 ◐ Push notifications
_Code and tests done 2026-11-26. Open: Firebase setup and a message received on a phone._

- **Goal:** daily-bonus and news messages through FCM.
- **Done when:**
  - the game subscribes to topic `all`;
  - signed-in players store their token in `push_tokens`;
  - Android 13+ asks for the notification permission, and saying no causes no problems;
  - a message sent from the admin panel arrives.
- **Tests:** Deno: `send-push` refuses non-admins and too-long text. Manual: a message arrives.

## F13 ◐ Account deletion
_Code and tests done 2026-11-26. Open: deploy and phone check._

- **Goal:** an in-app button that removes all online data (a Google Play rule).
- **Done when:**
  - "Delete my account" asks to confirm, then calls `delete-account`;
  - scores, save, push token, profile and auth user are gone;
  - the game signs out and clears local online data;
  - a public web page explains how to request deletion (Play Console needs this link).
- **Tests:** Deno/SQL: after the call no rows remain for that user.

## F14 ◐ Admin web panel
_Code done 2026-11-26. Open: Supabase setup, database tests and each action once on the real project._

- **Goal:** your website for running the game.
- **Done when:**
  - sign-in needs email, password and a 6-digit 2FA code;
  - without 2FA (`aal1`) every admin action is refused by the database;
  - you can see players and scores, and hide/unhide a score with a reason;
  - you can edit `latest_version`, `critical_min_version` and the message;
  - you can send a push (title ≤ 60, body ≤ 200);
  - no secret key is in the site's code (only the anon key).
- **Tests:** RLS: non-admin and `aal1` admin are refused. Manual: each action once.

---

## OW — Open-world mode ("Midnight Prison Break" slice)
_Added 2026-11-27 at your request: a second mode next to the endless run, which stays as it is. Steps run one at a time, each tested before the next._

- OW1 ◐ Physics car and test district (menu "Open World" button; Rigidbody + WheelColliders; gas, brake/reverse, handbrake, steering that tightens with speed; roll-over recovery; chase camera with speed FOV; walled 4×4 box district; Back or Menu returns). **Tests:** PlayMode drive/brake/return.
- OW2 ◐ Police AI car that chases you: straight at you in sight, else crossroads to crossroads; 90% of your top speed; brakes for corners; backs out when stuck; busted after 2 s within 7 m while slower than 3 m/s. **Tests:** EditMode grid route, PlayMode catch a parked car.
- OW3 ◐ Leave the car and walk: EXIT under 3 m/s (driver side, other side, or behind, whichever is free), ENTER within 4 m; capsule figure; joystick, sprint, jump, drag to turn the camera; camera stays out of walls; police chase you on foot. **Tests:** PlayMode exit, walk, enter.
- OW4 ◐ Ranged combat: `IDamageable` + `Health`, `WeaponData` ScriptableObject (fictional Pulse Pistol and Scrap Blaster), instant hit with 15° aim assist, magazine and reload, flash and spark only; one armed suspect that is incapacitated (lies down, goes grey); player incapacitated = start over. **Tests:** EditMode health, PlayMode shoot, reload, get shot at.
- OW5 ◐ Melee combat: PUNCH (light) and KICK (heavy) with wind-up, hit window and recovery; three-light combo (third ×1.5); lock-on to the nearest target in 2.5 m; BLOCK takes 20% and costs stamina; DODGE is untouchable for 0.25 s; stamina regenerates; no walking or shooting mid-strike. Suspect brawls within 2.2 m (guards 35% of strikes, strikes every 1.4 s). Difficulty settings not yet. **Tests:** PlayMode combo, spam refusal, block, dodge.
- OW6 ◐ Surrender and arrest: suspect gives up at 35% health (hands up, can no longer be hurt); hold ARREST 2 s within 2 m of a surrendered or incapacitated suspect (letting go restarts); cuffed suspect follows you, rides along when you get in within 6 m; stop on the blue station pad to deliver; objective line on the HUD. **Tests:** PlayMode surrender, cuff, escort, load, deliver.
- OW7 ◐ Midnight Prison Break: menu "Prison Break" → text intro → Police, Prisoner or Free roam. Police: chase the fleeing car (80% of your top speed, picks the road away from you); stay within 25 m for 4 s (or 90 s pass) and the escapee bails out armed; shots, then fists, surrender, cuff, deliver. Prisoner: police chase you; the car dies at the green garage; run past the blue guard to the green safehouse. 5-minute limit; busted, incapacitated or time up fails. Result screen with Retry; a win pays 500 coins and is saved in `missionsWon` (cloud merge: union). Not yet: civilian traffic, cutscenes, dialogue, music. **Tests:** EditMode merge and flee choice; PlayMode police win, prisoner win, prisoner fail.
- OW8 ◐ Civilian traffic: right-hand lanes on the road grid, random turns (never straight back), stop for anything ahead (creep on after 4 s nose to nose), a hard hit (over 5 m/s) knocks a car loose and it respawns 8 s later at least 40 m from you; half the endless run's car count per graphics preset (4/6/8). Police and the escapee swerve round traffic and ease off close behind it. No traffic lights yet. **Tests:** EditMode lanes and turns, PlayMode drive, stay on road, stop, crash.
- OW9 ◐ Pedestrians: walk round their block's 2 m pavement (clear of the traffic lanes), run for 4 s on shots within 30 m, the police car within 15 m or a car over 10 m/s within 10 m; cannot be hurt (Ignore Raycast layer, no Health: shots pass through, never an aim-assist target); a car reaching them makes them dive to the wall and crouch. Count = the preset's car count (8/12/16). No road crossing. **Tests:** EditMode loop and pavement, PlayMode walk, panic, shot pass-through, dive.
- OW10 ◐ Traffic lights: a lamp on the kerb for each road into every crossroads (80); north-south 8 s green + 2 s amber, then east-west; neighbours out of step. Civilian cars stop at the line on red (and amber with room to stop) and queue without creeping; police and player ignore them. **Tests:** EditMode phases and amber rule, PlayMode a car waits behind the line and goes on green.
- OW11 ◐ Pedestrians cross at the lights: at a corner half of them wait for a fresh red on the road ahead with time to get over (about 7 s of 10), then cross beside the crossroads; while crossing they are on the Default layer so civilian cars stop for them; stop lines moved back to 9 m so a stopped car's bumper is clear of the crossing. **Tests:** EditMode red time left and corners, PlayMode someone crosses only on a fresh red.
- OW12 ◐ Horn (HORN, H): people within 15 m walk faster for 3 s; never makes anyone cross against the lights; 0.6 s between honks; not on foot.
- OW13 ◐ Pull-over: civilian cars within 25 m ahead of or beside a moving police car (over 3 m/s) sidestep 1 m to the kerb and slow to 2 m/s, then pull back.
- OW14 ◐ Player siren (police side only): light bar on your car, SIREN button (N); flashing lights, siren sound; traffic pulls over for you while moving. The chaser's siren is now heard in the open world by distance.
- OW15 ◐ Siren hold: within 30 m of a sounding siren, people waiting at a corner keep waiting and anyone crossing hurries.
- OW16 ◐ Escapee under your siren (within 40 m): speed cap 80% → 90% of top speed, still below yours.
- OW17 ◐ AI cars (police chaser, escapee) swerve harder and brake fully for people on a crossing.
- OW18 ◐ Civilian cars stop for people on a crossing and never creep into them (test added; creep fix).
- OW19 ◐ Roadblock (while the police chase you in your car at over 8 m/s; first after 20 s, then every 45 s): two flashing police cars angled across the road beyond the next crossroads ahead, leaving a 3 m gap on the right; packs up after 25 s, once you are 60 m past, or when the chase is off. **Tests:** EditMode where it goes, PlayMode lanes blocked, gap clear, timed spawn, clearing.
- OW20 ◐ Roadblock cars join the chase once you are 3 m past them (same AI as the main chaser, 90% of your top speed), count for being busted, and stay on you until the chase is off. **Tests:** PlayMode waits, joins, drives after you, no timer once chasing, off with the chase.
- **Open questions:** character art and animations (you choose and buy a pack; placeholders until then); open-world scores do not go to the leaderboard until the server has a rule for them.
