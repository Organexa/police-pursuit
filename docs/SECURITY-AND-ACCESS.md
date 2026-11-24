# SECURITY AND ACCESS — Police Pursuit

_Version 1.0 · 2026-11-25_

> **What this doc is:** who can do what, where every secret lives, and the honest answers for Google Play's Data Safety form. The big picture is in `ARCHITECTURE.md` §4.

## 1. Roles
| Role | Who | Can do | Can't do |
|---|---|---|---|
| Guest | Anyone who installs the game | Play offline, ads, purchases, read the leaderboard and `app_config` | Submit scores, cloud save, store a push token |
| Player | Signed in with Google | Everything a guest can, plus: submit scores (through `submit-score` only), read/write **own** save, profile name and push token, delete own account | See or change anyone else's rows, insert scores directly, hide scores |
| Admin | You, with email + password **and** a 2FA code (`aal2`) | Everything in the admin panel: view players and scores, hide/unhide scores, edit `app_config`, send push | Anything if 2FA wasn't done (`aal1` is refused) |
| Server functions | Supabase Edge Functions | Use the service role key to write `scores` and delete accounts | Run without a valid user token |

## 2. Secrets: where each lives and who holds it
| Secret | Lives in | Entered by | In the public repo? |
|---|---|---|---|
| Supabase project URL + anon key | Game code, admin panel code | Me (it's public) | Yes, by design. RLS limits it |
| Google Play licence public key | Game code (obfuscated by Unity IAP) | Me | Yes. It's a public key |
| Supabase service role key | Supabase Edge Functions (built-in) | Supabase itself | **Never** |
| `SUPABASE_ACCESS_TOKEN`, `SUPABASE_PROJECT_REF`, `SUPABASE_DB_PASSWORD` | GitHub Secrets | You | Never |
| `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` | GitHub Secrets | You | Never |
| Android keystore + passwords (`ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS`) | GitHub Secrets | The one-time `create-keystore.yml` workflow | Never |
| `GH_SECRETS_TOKEN` (temporary; deleted right after `create-keystore.yml` runs) | GitHub Secrets | You | Never |
| `KEYSTORE_BACKUP_PASSPHRASE` | GitHub Secrets + your password manager | You | Never |
| `GOOGLE_SERVICES_JSON` (Firebase app config) | GitHub Secrets | You | Never |
| Firebase service account JSON (`FIREBASE_SERVICE_ACCOUNT`) | Supabase function secret | You | Never |
| Google OAuth client IDs | Supabase Auth settings + game code | You (settings) / me (code) | Client IDs only (they're public). Never a client secret |
| Admin password + 2FA app | Only with you | You | Never |

**Rules:**
- You never paste any of these into chat. `DEPLOYMENT.md` shows where to click.
- If a secret leaks, rotate it at the source first, then update the GitHub/Supabase secret. A leaked keystore can't be rotated, so back it up privately (DEPLOYMENT §6).

## 3. Database access (Row Level Security)
RLS is ON for every table. Default is "nobody", and each policy below opens one door.

| Table | Read | Insert / update / delete |
|---|---|---|
| `profiles` | Everyone: `display_name` (via the `leaderboard` view). Owner: own row | Owner: update `display_name` (1–20 chars) |
| `scores` | Everyone: `hidden = false` rows | Nobody from the app. `submit-score` only. Admin (`is_admin()`): update `hidden`, `hidden_reason` |
| `saves` | Owner | Owner (64 KB cap) |
| `push_tokens` | Owner | Owner |
| `app_config` | Everyone | Admin (`is_admin()`) |
| `admins` | Admin (`is_admin()`) | Nobody from the app. One SQL insert by you |

- **`is_admin()`** = `auth.uid()` is in `admins` **and** the JWT's `aal` claim is `aal2`.
- **Every rule has a test** (TESTING §2.4).

## 4. Other protections
| Threat | Protection |
|---|---|
| Fake high scores | Server recomputes the score and checks speed, near misses, wanted level, duration, date and rate (TRD §3) |
| Spam | 30 scores per player per hour; display names 1–20 chars |
| Stolen admin password | Useless without your 2FA app (`aal2`) |
| Big uploads | Save capped at 64 KB; push text capped at 60/200 chars |
| Traffic snooping | All traffic is HTTPS (TLS) |
| Secrets in the repo | `.gitignore` blocks secret files; CI never prints secrets (RULES §2) |
| Edited coins on the phone | Accepted risk: only affects that player's own game (TRD §5) |

## 5. Google Play Data Safety answers
Fill the Play Console form with these. They must match the real game (RULES §6.3). Re-check them whenever a feature or SDK changes.

**General answers:**
- Does the app collect or share user data? **Yes.**
- Is all data encrypted in transit? **Yes** (HTTPS everywhere).
- Can users ask for their data to be deleted? **Yes**: the in-app "Delete my account" button, plus the public deletion web page (F13).

**Data types:**
| Data type (Play category) | What it is | Collected | Shared | Optional? | Why |
|---|---|---|---|---|---|
| Personal info → Email address | Google account email (in Supabase Auth) | Yes | No | Optional (sign-in) | Account management |
| Personal info → User IDs | Supabase user ID, Google account ID | Yes | No | Optional (sign-in) | Account management, app functionality |
| Personal info → Name | Display name on the leaderboard | Yes | No | Optional | App functionality |
| App activity → Other user-generated content / In-app actions | Scores and run stats, cloud save | Yes | No | Optional (sign-in) | App functionality |
| Device or other IDs | FCM token; advertising ID and device IDs used by LevelPlay | Yes | Yes (ad networks) | FCM: optional. Ads: needed for ads | Push messages, advertising, fraud prevention, analytics |
| Financial info → Purchase history | Handled by Google Play Billing; the game only stores what it unlocked | Yes | No | Optional | App functionality |
| App info and performance → Crash logs / Diagnostics | Collected by the LevelPlay SDK | Yes | Yes (ad networks) | Needed for ads | Analytics, advertising |
| Location → Approximate location | Inferred from IP by ad networks | Yes | Yes (ad networks) | Needed for ads | Advertising |

> **To confirm before release:** LevelPlay publishes its own Data Safety guidance. Check its current page and add anything it lists that's missing here. The final list depends on which ad networks are switched on.

**Ads declaration:** "Contains ads" = **Yes**. Target audience = 13+ (teens and adults), not "Designed for Families".

## 6. iOS note
If you add iOS later, Apple's "App Privacy" labels need the same list, plus an App Tracking Transparency prompt before ads use the advertising ID. Not part of v1.
