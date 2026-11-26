# DEPLOYMENT — Police Pursuit

_Version 1.0 · 2026-11-25_

> **What this doc is:** the one-time setup you do yourself in a web browser. You never paste a secret into chat. Wherever a value is secret, this guide shows where to click and you type it in yourself. Secret names match `SECURITY-AND-ACCESS.md` §2.
>
> **Cost:** everything here is free, except the US$25 Google Play fee in §8, which you pay yourself. Unity Hub (§4) is the only program you install, and you only use it once.

**Order:** §1 GitHub → §2 Supabase → §3 Firebase → §4 Unity license → §5 keystore → §6 keystore backup → §7 Google sign-in → §8 admin user → §9 Play Console. I'll tell you when each step is needed, so you don't have to do them all on day one.

---

## 1. GitHub (free)
1. Create a free account at github.com.
2. Create a **public** repository named `police-pursuit`. It must be public: public repos get free Actions minutes and free Pages.
3. Go to **Settings → Pages** and set **Source = GitHub Actions**. This hosts the admin panel and the account-deletion page.
4. To add each secret named in this guide, go to **Settings → Secrets and variables → Actions → New repository secret**, then type the name exactly and paste the value.

## 2. Supabase (free plan)
1. Sign up at supabase.com and create a new project on the **Free** plan.
2. Choose a strong database password and save it in your password manager.
3. Add these GitHub secrets (§1.4):

| Secret | Where to find it |
|---|---|
| `SUPABASE_PROJECT_REF` | Project Settings → General → Reference ID |
| `SUPABASE_DB_PASSWORD` | The password from step 2 |
| `SUPABASE_ACCESS_TOKEN` | Your avatar → Account → Access Tokens → Generate new token |

4. Send me the **project URL** and the **anon (public) key** from Project Settings → API. These two are public by design, so they're the only values that may go in chat and in the code.

## 3. Firebase (free "Spark" plan, push only)
1. At console.firebase.google.com, create a project. You can turn Google Analytics off.
2. Add an **Android app** with the package name shown in `PROGRESS.md`.
3. Download `google-services.json`. Open it in Notepad, copy all of it, and save it as the GitHub secret `GOOGLE_SERVICES_JSON`. Don't put the file in the repo; `.gitignore` blocks it anyway.
4. Go to **Project settings → Service accounts → Generate new private key**. This downloads a JSON file.
5. In Supabase, go to **Edge Functions → Secrets**, add `FIREBASE_SERVICE_ACCOUNT`, and paste the whole JSON file.
6. Delete the downloaded JSON file from your computer.

## 4. Unity license (one time, free Personal license)
GameCI needs a license file, and the only way to get a Personal license is through Unity Hub.
1. Create a free Unity account at unity.com.
2. Install **Unity Hub**, sign in, and go to **Preferences → Licenses → Add → Get a free personal license**.
3. Open `C:\ProgramData\Unity\Unity_lic.ulf` in Notepad, copy all of it, and save it as the GitHub secret `UNITY_LICENSE`.
4. Save your Unity login as two more GitHub secrets: `UNITY_EMAIL` and `UNITY_PASSWORD`.
5. Keep Unity Hub installed. The license file lasts about 30 days. Before it expires, open Unity Hub while signed in so it renews, then repeat step 3.

## 5. Android signing key (keystore)
A workflow creates the key for you, so you never handle the file yourself.
1. **Give the workflow permission to write secrets.**
   - GitHub doesn't let workflows save secrets on their own, so they need a token.
   - Go to **your avatar → Settings → Developer settings → Fine-grained tokens → Generate new token**.
   - Repository access: only `police-pursuit`.
   - Permission: **Secrets = Read and write**.
   - Expiry: 1 day.
   - Save the token as the GitHub secret `GH_SECRETS_TOKEN`.
2. Create a long random passphrase in your password manager. Save it as the GitHub secret `KEYSTORE_BACKUP_PASSPHRASE`.
3. Go to **Actions → create-keystore → Run workflow**. It creates these four secrets:
   - `ANDROID_KEYSTORE_BASE64`
   - `ANDROID_KEYSTORE_PASS`
   - `ANDROID_KEYALIAS_NAME`
   - `ANDROID_KEYALIAS_PASS`
4. Delete `GH_SECRETS_TOKEN`, both from the repository secrets and from Developer settings.

## 6. Keystore backup (do this the same day)
The workflow from §5 also leaves an **encrypted** backup as a download on its run page, kept for 1 day.
1. Open the run and download `keystore-backup`.
2. Store it somewhere private and offline, such as a USB stick, plus a personal cloud drive. Keep `KEYSTORE_BACKUP_PASSPHRASE` in your password manager.
3. **Why it matters:** we use **Play App Signing**, so Google holds the real app-signing key and this keystore is only the *upload key*. If you lose it, Play support can reset it, but that takes days. With the backup you lose nothing.
4. The backup is encrypted (AES-256), so the file is useless without your passphrase.
5. To restore, run this (it asks for the passphrase):
   `openssl enc -d -aes-256-cbc -pbkdf2 -iter 200000 -in keystore-backup.tar.gz.enc | tar xz`

## 7. Google sign-in (Google Cloud, free)
1. Go to console.cloud.google.com and pick the project Firebase created. Open **APIs & Services → OAuth consent screen**:
   - User type: External.
   - App name: Police Pursuit.
   - Scopes: only `email`, `profile` and `openid`.
2. Go to **Credentials → Create OAuth client ID** and create two:
   - **Web application.** Its client ID is the "server client ID" the game sends to Credential Manager. Send me the client ID; it's public. **Don't create or share a client secret.**
   - **Android.** Use the package name from `PROGRESS.md` and the **SHA-1** of the app signing key. After the first Play upload (§9), the SHA-1 is in Play Console → Setup → App signing. I'll also give you the upload key's SHA-1 from the build log, so test builds work.
3. In Supabase, go to **Authentication → Sign In / Providers → Google**:
   - Turn it on.
   - Under **Client IDs**, enter the Web client ID.
   - If the form asks for a client secret, leave it empty if it lets you. A secret is only needed for browser redirects, and we don't use them.

## 8. Admin user (you)
1. In Supabase, go to **Authentication → Users → Add user**. Use your email and a strong password.
2. Open **SQL Editor** and run this one line. It's safe to run more than once:
   ```sql
   insert into admins (user_id) select id from auth.users where email = 'YOUR-EMAIL' on conflict do nothing;
   ```
3. Put the project URL and anon key from §2 into `admin/config.js` and push. Then open the admin panel (its link is in `PROGRESS.md`) and sign in right away, so nobody else can set up 2FA first. On the first sign-in it shows a QR code. Scan it with an authenticator app, such as Google Authenticator, and enter the 6-digit code.
4. From then on, every admin sign-in needs your password **and** a code from that app.

## 9. Google Play Console (US$25 one-time, you pay)
1. Sign up at play.google.com/console as a **personal** developer and pay the US$25 fee yourself. Then verify your identity.
2. Create the app:
   - Free.
   - "Contains ads" = Yes.
   - Target audience: 13+.
3. **Closed testing first:**
   - New personal accounts must run a closed test before they can publish to everyone.
   - The current rule is at least 12 testers for 14 days. Check the Console for the exact numbers when you get there.
   - Upload the `.aab` from the GitHub Release (made when a `v*` tag is pushed; I'll do that when you say).
4. Fill the **Data safety** form with the answers in `SECURITY-AND-ACCESS.md` §5.
5. For the **account deletion URL**, use `https://<your-github-name>.github.io/police-pursuit/delete-account.html`.
6. **Privacy policy URL:** `https://<your-github-name>.github.io/police-pursuit/privacy.html`. I'll write this page.
7. Content rating questionnaire:
   - Answer honestly. The game has cartoon-style car crashes and police chases, with no blood.
   - Turn on **Play App Signing**. It's the default.

## 9a. LevelPlay (ads, free)
Ads and the consent popup stay hidden until all three values below are filled in.
1. Sign up for Unity LevelPlay and add an **Android app** with the package name `com.policepursuit.game`.
2. Create one **rewarded** ad unit and one **interstitial** ad unit.
3. Send me the **app key** and the two **ad unit IDs**. They are public values that ship inside the app.
4. Use LevelPlay's test mode or a registered test device while testing. Tapping your own live ads can get the account banned.
5. Check LevelPlay's own Data Safety guidance and compare it with `SECURITY-AND-ACCESS.md` §5.

## 9b. Values that go into the code
All are public by design. Each feature stays hidden or off while its values are empty.

| Value | From | Goes into |
|---|---|---|
| Supabase project URL, anon key | §2 | `unity/Assets/Scripts/Core/Auth.cs` and `admin/config.js` |
| Google **web** client ID | §7 | `Auth.cs` |
| LevelPlay app key, rewarded ID, interstitial ID | §9a | `unity/Assets/Scripts/Core/Ads.cs` |
| Google Play public licensing key (Play Console → Monetize → Licensing) | §9 | `unity/Assets/Scripts/Core/Shop.cs` |

**Never** send or commit a service role key, client secret, private key or password.

In Play Console, also create these in-app products: `remove_ads`, `coins_small`, `coins_medium`, `coins_large`, `car_phantom`.

## 10. iOS (optional, not in v1)
- iOS needs an Apple Developer account, which costs US$99 a year.
- GameCI can build the iOS project, but signing and uploading need a macOS build machine. GitHub's macOS minutes are free for public repos.
- Apple's privacy labels and the tracking prompt are covered in SECURITY §6.
- Only do this if you decide to pay Apple.

## 11. After setup
- Every push to `main` does the rest on its own:
  - the tests run;
  - the APK and AAB are built;
  - the backend and the admin panel are deployed.
- You don't need to deploy anything by hand.
- If a run fails, I'll read the log and fix it.
