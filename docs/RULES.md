# RULES — Police Pursuit

_Version 1.0 · 2026-11-25_

> **What this doc is:** the house rules every change must follow, whether I write it or someone else does later.

## 1. Money rules (never broken)
1. **No paid dependency is ever added silently.** Anything that costs money is shown to you with its price and licence, and you approve it first.
2. **No subscriptions.** Only free plans plus the US$25 Play Console fee you already approved.
3. **Nobody asks you for payment details** unless it's explained, necessary and approved by you, for example the Play Console fee you pay yourself.

## 2. Secret rules
1. **No secret is ever written in the repo**, in a commit, in a log or in chat.
   - This covers keys, passwords, tokens and keystores.
   - Only the Supabase anon key and the Google Play public key may be in the code, because they're public by design (ARCHITECTURE §4).
2. **You enter secrets yourself** in GitHub or Supabase settings, following `DEPLOYMENT.md`.
3. **CI must not print secrets.** No `echo` of secret values, and no debug logging of request headers.
4. **`.gitignore` blocks common secret files:** `*.keystore`, `*.jks`, `*.ulf`, `.env*`, `google-services.json`, and service-account JSON files.

## 3. Content rules
1. **Everything is original.** No real car brands or logos, and no copied art, music or level layouts from other games.
2. **Every asset's licence is recorded** in `/docs/ASSETS.md`: its name, source, licence and link.
3. **CC0 and the Unity Asset Store standard licence are fine.** Anything "non-commercial only" is not allowed.

## 4. Code rules
1. **Keep it simple.** The smallest change that fully works. No unused options or "for later" code.
2. **Use the engine first.** Use Unity built-ins and the already-chosen packages (TRD §1) before writing your own.
3. **Shared numbers live in one place:**
   - The score formula has one C# file and one TypeScript file.
   - Both are checked against the same test table, `/shared/score_cases.json`.
4. **The server never trusts the phone.** `submit-score` validates every field (TRD §3).
5. **Every table has RLS on.** A new table without RLS policies fails the database tests.
6. **Tests come with logic:** the score, the queue, the save merge, the version compare and the anti-cheat limits each have tests (TRD §7).
7. **Comments say *why*, not *what*.** Shortcuts get a `shortcut:` comment naming the limit.

## 5. Change rules
1. **Work goes through GitHub.**
   - Push → tests run → `main` builds the APK.
   - Nothing is deployed by hand.
2. **`main` must stay green.** If tests fail, fix them before adding features.
3. **Versions:**
   - The game version follows `MAJOR.MINOR.PATCH`, for example `1.0.0`.
   - The Android `versionCode` goes up by 1 on every release.
4. **Critical updates:** raise `critical_min_version` only for real breaking problems, such as a security bug or a server change old builds can't handle.

## 6. Player-facing rules
1. **Ads:** never during a run, at most 1 interstitial every 3 runs, and only after consent where the law requires it.
2. **Children:** the game is aimed at teens and adults. The Play Store age rating and ad settings must match that (no ads targeted at children).
3. **Honest store forms:** the Data Safety and ad declarations must match what the game really collects (`SECURITY-AND-ACCESS.md` §5).
4. **Accessibility:**
   - Buttons are at least 48 dp.
   - Flashing stays at 3 times per second or less.
   - Haptics and tilt can be turned off.
