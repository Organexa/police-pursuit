# Progress — Police Pursuit

_Last updated: 2026-11-26_

## Current phase
- **PRD v1.1, DESIGN v1.0, ARCHITECTURE + TRD v1.0 approved (2026-11-25).**
- **All docs written.** T0.1 scaffold done.
- T0.2 workflows pushed to `Organexa/police-pursuit` (public, 2026-11-25).
- First CI run: backend tests pass (skipped, nothing to test yet); unity tests failed (exit code 1, no license), so the unity test job now skips until `UNITY_LICENSE` exists; the build stops at "Check signing secrets" until the §5 keystore exists.
- Neither is a workflow bug.
- T0.3 Supabase schema done: RLS tests (24 pgTAP) green in CI run 45562016508.
- Secrets setup deferred by you.
- F1 code done 2026-11-26: EditMode 10/10 and PlayMode 1/1 passed locally on a scratch copy in Unity 6000.6.10f1 (project pins 6000.3.29f1; not yet run in CI).
- Tilt steering and engine audio: built later (see the sound note below).
- F2 code done 2026-11-26: EditMode 11/11 and PlayMode 2/2 passed on the same scratch-copy setup.
- F3 code done 2026-11-26: EditMode 13/13 and PlayMode 2/2 passed on the same scratch-copy setup.
- Premium-car IAP waits on F10.
- F4 code done 2026-11-26: EditMode 14/14 and PlayMode 2/2 passed on the same scratch-copy setup.
- F4 not checked: Settings screen layout and touch, tilt and haptics on a phone (Android vibrate code is not compiled by editor tests). Audio added later (see the sound note). "Privacy & ads" (F9/F10) and "Delete my account" (F13) deferred.
- F8 code done 2026-11-26: EditMode 15/15 and PlayMode 2/2 passed on the same scratch-copy setup. Finished runs are queued in `score_queue.json`.
- F8 not done: upload waits on F5 (sign-in) and F7 (`submit-score`); no offline check on a phone. The F7 sender should drop runs the server rejects, or they stay queued.
- F5 code done 2026-11-26: EditMode 16/16 and PlayMode 2/2 passed on the scratch copy. The Android Java plugin is not compiled by these tests.
- F5 not done: Supabase project, Google OAuth web client ID, the three values in `Scripts/Core/Auth.cs` (sign-in button stays hidden until set), real-phone check.
- F6 code done 2026-11-26: EditMode 17/17 and PlayMode 2/2 passed on the scratch copy. Best score moved from PlayerPrefs to `save.json`.
- F6 not done: needs the F5 setup values; no real upload or download has run against Supabase.
- F7 code done 2026-11-26: EditMode 18/18 and PlayMode 2/2 passed on the scratch copy; `submit-score` tests 6/6 passed under Node (Deno not installed here).
- F7 not done: new migration and RLS tests not run (no Supabase CLI here, CI runs them); function never deployed (check the `../../../shared/cars.json` import bundles on first deploy); leaderboard screen not seen on a phone.
- F8 upload is built (CloudSave); the offline phone check stays open.
- F11 code done 2026-11-26: EditMode 19/19 and PlayMode 3/3 passed on the scratch copy. CI build version is now fixed at 1.0.0 (build.yml): raise it each release.
- F11 not done: not tried against a real `app_config` or on a phone.
- Package name set 2026-11-26 by `unity/Assets/Editor/PackageName.cs` (tests passed); not yet seen in a built APK.
- Empty `Assets/Scenes/Main.unity` added to Build Settings 2026-11-26: Windows player built and ran 15 s with no errors on the scratch copy. Android build not tried (module not installed here; CI needs the §4 and §5 secrets).
- F9 code done 2026-11-26: EditMode 20/20 and PlayMode 4/4 passed on the scratch copy (ads faked in tests). Consent: own popup shown to everyone once, answer passed to LevelPlay; no Google UMP.
- F9 not done: LevelPlay account and the three IDs in `Scripts/Core/Ads.cs` (ads and consent stay hidden until set); Android gradle dependencies and real test ads never tried.
- F10 code done 2026-11-26: EditMode 21/21 and PlayMode 4/4 passed on the scratch copy. Product IDs: `remove_ads`, `coins_small` (1000), `coins_medium` (5000), `coins_large` (15000), `car_phantom`; coin amounts are my pick.
- F10 not done: Google Play public key in `Scripts/Core/Shop.cs` (shop stays hidden until set), products created in Play Console, licence-tester purchases and reinstall restore on a phone; the Android store code has never run.
- F12 code done 2026-11-26: `send-push` tests 3/3 passed under Node; Unity EditMode 21/21 and PlayMode 4/4 passed on the scratch copy. Deviation: `firebase-messaging` Android library instead of the Firebase Unity SDK; build.yml writes its config from `GOOGLE_SERVICES_JSON`.
- F12 not done: Firebase setup (DEPLOYMENT §3), the Java plugin has never been compiled, no message sent to a phone; messages are not shown while the game is open; sending needs the F14 admin panel.
- F13 code done 2026-11-26: `delete-account` tests pass (all function tests 12/12 under Node); Unity EditMode 21/21 and PlayMode 4/4 passed on the scratch copy. Deletion relies on the database cascade; pgTAP check added (not run locally).
- F13 not done: contact email added to `admin/delete-account.html` (pushing publishes the page); not deployed; no phone check.
- F14 code done 2026-11-26: panel in `admin/` clicked through on localhost against a fake backend; pinned supabase-js loads under the page security policy; function tests 13/13 under Node. New migration `20261126000001_admin_panel.sql` (admin reads profiles, player list, last seen).
- F14 not done: URL and anon key in `admin/config.js`; admin user (DEPLOYMENT §8); migration and pgTAP not run; never used against real Supabase. Library is version-pinned without an integrity hash.
- Admin panel link (after push): https://organexa.github.io/police-pursuit/
- Pushed to branch `feat/f1-f14` 2026-11-26. CI run 45814665192: Deno function tests and database (pgTAP) tests passed, so the earlier "not run" notes for those are closed. Unity tests skipped (no `UNITY_LICENSE` secret).
- CI Unity tests green 2026-11-26 (run 45820689126, commit ba70bcc, Unity 6000.3.29f1): needed `containerRegistryImageVersion: 4`, `unity-test-runner@v4.3.2` (the @v4 release exited with no message) and the three `UNITY_*` secrets. Sign-in succeeded only after two retries. Test counts not read from the log.
- First Android build green 2026-11-26 (android-check run 45831271887, commit 2130b84, debug-signed APK artifact kept 3 days). It needed `LevelPlaySdk.androidlib` (mediation-sdk 9.6.0); keep that version in step with the LevelPlay package. First phone install: car invisible and buttons hard to see (shapes drew with the error shader; navy buttons on navy road). Fixed 2026-11-26 with `Assets/Resources/Flat.shader`, button outlines and landscape-only (EditMode 21/21, PlayMode 5/5, Windows screenshot); phone recheck and drift feel open. Self-test 2026-11-26: Settings and Leaderboard were cut off on wide screens, fixed (CanvasScaler Expand); EditMode 21/21, PlayMode 6/6, function tests 13/13; drift confirmed working in a Windows build via simulated presses.
- Signing key (§5) created 2026-11-27 (create-keystore run 45877241754). Upload key SHA1 `B3:7E:98:37:AB:D9:48:FF:97:99:D7:CA:43:2E:8C:40:A6:C6:9E:BE` (public; for the Android OAuth client). Backup downloaded by you. Open: delete `GH_SECRETS_TOKEN` and its token, a signed test build. Pull request #1 is still not to be merged before the deploy secrets are set.
- Privacy policy drafted 2026-11-26 at `admin/privacy.html` (for Play Console; needs your review; published only when `admin/` reaches `main`). It discloses that Google sign-in stores name and picture link, which SECURITY §5 does not list yet.
- Sound effects added 2026-11-26 (`Gameplay/Sound.cs`, synthesised: engine, police siren by distance, crash, coin, near miss, button click). EditMode 22/22, PlayMode 7/7 on the scratch copy; nobody has listened to it yet. Music added the same day: an 8-second synthesised tune (bass in the menu; drums from 2 wanted stars, lead from 3) with its own slider. EditMode 23/23, PlayMode 8/8; still not listened to.
- Supabase live 2026-11-26: project `police-pursuit` (ref `dumwtlqllfyhbdxglsvc`, Mumbai, free) in Organexa Tech; `neon-racers` was paused to make room. 3 migrations applied (history versions aligned with the repo files) and 3 functions deployed through the connector; anonymous probes behaved (public reads OK, private reads and writes refused). URL and anon key are in `Auth.cs` and `admin/config.js`. Open: Google client ID, admin user + 2FA, `FIREBASE_SERVICE_ACCOUNT`, GitHub secrets/variables (DEPLOYMENT §2) for CI deploys and keep-alive (free projects pause after about a week idle). Also fixed: version `1.0` was treated as older than `1.0.0`.
- Phone check 2026-11-26: you reported the latest APK (12f9ad8) works. Not itemised: 60 fps on a mid-range phone, tilt, haptics, first-launch tutorial, offline queue.
- Google sign-in started 2026-11-26: web client ID in `Auth.cs`; `android-check` will sign with the upload key once it exists. Open: enable the Google provider in Supabase, signing key, Android OAuth client (package + SHA-1), test user if the consent screen is in Testing.
- First sign-in attempt on the phone (2026-11-27): no `/auth/v1/token` call reached Supabase. Fixed a likely cause (the Java plugin passed null through the Unity proxy, losing the answer); not yet confirmed on a phone.
- **Google sign-in works on the phone (2026-11-27)**, reported by you. Cause: the Android OAuth client did not exist; you created it (consent screen is in Testing). No new APK was needed. Supabase then showed 1 user, 1 save and 1 score. Not checked on a phone: sign-out and back in, save restore after reinstall, leaderboard name, account deletion. A failed sign-in now shows its reason under the button, also when the player just closes the account list (soften before release). The Play Store build will need a second Android client with Google Play's app-signing SHA-1.
- Privacy policy button added to Settings and a server-log sentence to `admin/privacy.html` (2026-11-27; EditMode 23/23, PlayMode 8/8 plus a temporary no-overlap check on a scratch copy). The link stays dead until `admin/` is published from `main`; not seen on a phone. You confirmed the contact email, the publisher name "Organexa" and the 13+ age (2026-11-27).
- At your request (2026-11-27), builds of `main` no longer create a public GitHub Release; only `v*` tags do. Run artifacts can still be downloaded by anyone signed in to GitHub. GitHub Pages source set to GitHub Actions by you.
- Pull request #1 merged 2026-11-27 (`1ce3586`). First deploys failed: Supabase CLI v3 made `db push` target a local database (now pinned to 2.122.1), and Pages hit GitHub's artifact-listing lag (now waits 60 s). Fixed by pull request #2 (`00ac27f`): `deploy-backend` run 45898326883 and `deploy-admin` run 45898347487 succeeded; the three Supabase secrets work. The admin panel, privacy and deletion pages answer at https://organexa.github.io/police-pursuit/, the three functions refuse unsigned calls as expected, and no public release was created. The `main` tests and the signed APK and AAB build (run 45898277471) succeeded; the release job was skipped. Not checked: that build on a phone, the privacy button on the phone, a signed-in score upload after the redeploy, admin sign-in on the published panel.
- Next (2026-11-27): code is merged and deployed. Waiting on you: phone check of the signed `main` build (run 45898277471: sign-in, one run uploading its score, Privacy policy button; confirmed 06:10 UTC on 27 Nov: the leaderboard showed on your phone, the sign-in refreshed, the save synced and 2 new scores arrived after the redeploy; which APK was installed and the Privacy policy button are not confirmed. Seen once: `my_rank` answered 401 when the leaderboard was opened just before the expired sign-in refreshed), then Play Console (§9), LevelPlay (§9a), Firebase (§3).
- Fixed 2026-11-27: the leaderboard waits for an expired sign-in to renew before asking for your rank, and closing the Google account list shows no reason again (only the "reauth" setup fault does). EditMode 23/23, PlayMode 8/8 on a scratch copy; no test covers the rank wait. The test APK build (run 45950659627, commit `710aeb0`) passed and you reported both work on your phone; the rank wait after an expired sign-in may not have been exercised. Not yet in `main`.

## Decisions so far
| Topic | Decision |
|---|---|
| Product | Mobile game "Police Pursuit" — original endless 3D getaway driving (dodge AI police, collect coins, survive) |
| Engine | Unity 6 LTS (URP), built in the cloud with GameCI + GitHub Actions |
| Platforms | Android first (free APK + Google Play). iOS optional later |
| Players / audience | Single-player, teens & adults |
| Look | Realistic HD 3D, console-style target on high-end phones (URP post-FX, Low/Med/High presets). Original free or paid assets; paid packs are optional, approved one at a time, and bought by you. Not full AAA (honest note) |
| Money | Ads (Unity LevelPlay) + in-app purchases (Unity IAP / Google Play Billing) |
| Online | Leaderboard, in-game update notice, Google sign-in, cloud save |
| Admin | Web admin panel with 2FA (TOTP), hosted on GitHub Pages |
| Extras | Tutorial, settings, offline play (scores queued), push notifications (FCM) |
| Play Console | You approved the US$25 one-time fee (you pay it yourself) |
| Design | uGUI + TextMeshPro menus; on-screen buttons (tilt optional); switchable chase/bumper camera; rainy-night neon city |
| Backend | One Supabase free project: Auth, Postgres with RLS, Edge Functions (`submit-score`, `delete-account`, `send-push`) |
| Sign-in | Android Credential Manager → Google ID token → Supabase. No deep links |
| Anti-cheat | Game sends raw run stats; server recomputes the score and rejects impossible runs |
| Unity license | One-time 10-minute Unity Hub step by you; license text saved as a GitHub Secret |
| Recommended defaults | Cloud save via Google account; no deep links in v1; optional update notice + forced for critical; in-app account deletion; small car garage (coins or IAP) |
| Package name | `com.policepursuit.game` |

## Verified costs (checked 2026-11-25 on official pages)
| Service | Cost | Source |
|---|---|---|
| Unity Personal | $0 (under US$200K revenue/funding in 12 months) | https://unity.com/products/unity-personal |
| GitHub Actions | $0 for public repos on standard runners | https://docs.github.com/en/billing/concepts/product-billing/github-actions |
| Supabase Free | $0 — 50,000 MAU, 500 MB DB, 5 GB egress, 1 GB storage; pauses after 1 week idle | https://supabase.com/pricing |
| Firebase Cloud Messaging | $0 (no-cost product, Spark plan needs no card) | https://firebase.google.com/pricing |
| Google Play Console | US$25 one-time | https://support.google.com/googleplay/android-developer/answer/6112435 |
| GameCI | $0 (open source) — needs Unity license file | https://game.ci/docs/github/activation |

## Open risks
1. ~~Unity license file~~ **Resolved:** a one-time 10-minute Unity Hub step (ARCHITECTURE §5).
2. Supabase free projects pause after 1 week idle — a scheduled GitHub Action ping every 3 days prevents it.
3. Realistic visuals on free assets will be good mobile quality, not AAA.
4. Coins and IAP receipts are checked only on the phone (normal for single-player; leaderboard is server-checked).
5. Unity license file expires **2026-12-24**. Before then: open Unity Hub (signed in) so it renews, then re-copy `Unity_lic.ulf` into the `UNITY_LICENSE` secret. The first CI run (T0.2) confirms GameCI accepts it.

## Completed
- [x] Requirements gathering (3 question groups)
- [x] Free-tier cost verification
- [x] PRD (v1.1, approved 2026-11-25)
- [x] Design (DESIGN.md v1.0, approved 2026-11-25)
- [x] Architecture (ARCHITECTURE.md, TRD.md v1.0, approved 2026-11-25)
- [x] RULES, MEMORY, FEATURE-TICKETS, SECURITY-AND-ACCESS, DEPLOYMENT, TESTING docs
- [x] Unity Personal license created and validated (2026-11-25; expires 2026-12-24)
- [x] T0.1 scaffold (folders, Packages/manifest.json, ProjectVersion 6000.3.29f1, .gitignore), 2026-11-25; confirmed when CI opens it in T0.2
- [x] T0.3 Supabase schema (migration + RLS pgTAP tests, green in CI), 2026-11-25

## Next
- [x] T0.2 CI: tests, signed APK/AAB and deploys green on `main` (2026-11-27)
- [ ] ◐ F1 Menu, play, pause, game over (code and tests done 2026-11-26; manual 60 fps phone check open)
- [ ] ◐ F2 Tutorial (code and tests done 2026-11-26; manual first-launch phone check open)
- [ ] ◐ F3 Garage (code and tests done 2026-11-26; premium IAP needs F10; manual phone check open)
- [ ] ◐ F4 Settings (code and tests done 2026-11-26; privacy/account buttons need F9/F10/F13; manual phone check open)
- [ ] ◐ F8 Offline play (queue and tests done 2026-11-26; upload needs F5/F7; manual offline phone check open)
- [ ] ◐ F5 Google sign-in (works on your phone 2026-11-27; sign-out and sign-in-again check open)
- [ ] ◐ F6 Cloud save (code and tests done 2026-11-26; Supabase setup and phone check open)
- [ ] ◐ F7 Leaderboard (code and tests done 2026-11-26; deploy and phone check open)
- [ ] ◐ F11 Update notice (code and tests done 2026-11-26; real-backend and phone check open)
- [ ] ◐ F9 Ads (code and tests done 2026-11-26; LevelPlay setup and phone check open)
- [ ] ◐ F10 In-app purchases (code and tests done 2026-11-26; Play Console setup and phone check open)
- [ ] ◐ F12 Push notifications (code and tests done 2026-11-26; Firebase setup and phone check open)
- [ ] ◐ F13 Account deletion (code and tests done 2026-11-26; deploy and phone check open)
- [ ] ◐ F14 Admin panel (code done 2026-11-26; Supabase setup and manual check open)
- [ ] ◐ GitHub setup for **Organexa** (DEPLOYMENT.md), in order:
  - [x] §1 repo `police-pursuit` created, pushed, public (2026-11-25)
  - [x] §4 secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` (2026-11-26; license expires 2026-12-24)
  - [x] §5 signing key created by **create-keystore** (2026-11-27); deleting `GH_SECRETS_TOKEN` and its token is not confirmed
  - [x] §6 encrypted `keystore-backup` downloaded (reported by you)
  - [x] §2 secrets `SUPABASE_ACCESS_TOKEN`, `SUPABASE_DB_PASSWORD`, `SUPABASE_PROJECT_REF`; variables `SUPABASE_URL`, `SUPABASE_ANON_KEY` (added by you 2026-11-27; keep-alive run 45890653333 reached Supabase, so the variables work; the three secrets are untested until the first deploy)
  - [x] §8 admin user added 2026-11-27 (a separate email-and-password account, listed in `admins`); signed in to the panel run on this PC and set up 2FA (verified in Supabase). Not checked: the panel's tabs against real data.
- [x] Supabase setup (DEPLOYMENT §2), live and deployed from `main`
- [ ] Test, deploy, handover
