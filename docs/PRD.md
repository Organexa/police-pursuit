# PRD — Police Pursuit (Product Requirements Document)

_Version 1.1 · 2026-11-25 · Status: **Approved 2026-11-25** (v1.1: free or paid assets, console-style visual target)_

> **What a PRD is:** a plain description of *what* we are building and *why*. It doesn't cover how the code works. That goes in the Architecture doc.

## 1. The idea in one sentence
You're a getaway driver in a realistic 3D city. Police cars chase you, roadblocks appear, and you weave through traffic collecting coins. Survive as long as you can and beat your friends on the online leaderboard.

## 2. Who it's for
- Teens and adults who like quick, exciting driving games on their phone.
- Each session lasts 1 to 5 minutes, so it suits short breaks.
- It's single-player only.

## 3. Platforms
| Platform | Plan | Cost |
|---|---|---|
| Android | **First.** A free APK you can share directly, plus a Google Play listing | US$25 one-time Play Console fee (you approved this) |
| iOS | **Optional, later.** Not required for launch | See section 11 |

## 4. Core gameplay (v1)
1. **Endless drive:** the city road keeps going. Steering uses tilt or on-screen buttons (your choice in settings). Gas is automatic; there's a brake/drift button.
2. **Police AI:** police cars chase and try to ram or box you in. The longer you survive, the more police appear: wanted level 1 to 5 stars.
3. **Hazards:** civilian traffic, roadblocks and spike strips.
4. **Damage:** your car has a health bar. When it reaches 0 you're "busted" and the run ends.
5. **Coins and near-misses:** collect coins on the road. Near-misses with police earn bonus points.
6. **Score:** based on distance, near-misses and wanted level.
7. **Second chance:** after being busted, watch an optional ad to continue once.

## 5. Features
| # | Feature | Notes |
|---|---|---|
| F1 | Main menu, play, pause, game over | — |
| F2 | Tutorial | A short first-run guide: steer, drift, dodge. It can be skipped and replayed from settings |
| F3 | Garage | 5 to 6 original cars, unlocked with coins or bought with an IAP. Stats: speed, handling, armor |
| F4 | Settings | Sound/music volume, controls (tilt or buttons), graphics quality (Low/Medium/High), language (English in v1), privacy/ads consent, account deletion |
| F5 | Google sign-in | **Optional.** You can play as a guest; signing in is needed only for the leaderboard and cloud save |
| F6 | Cloud save | Coins, unlocked cars and best score are saved to your Google account |
| F7 | Online leaderboard | Global top 100 plus your own rank. Scores are checked on the server to block obvious cheats |
| F8 | Offline play | The game works fully offline. Scores are queued and uploaded when you're back online |
| F9 | Ads | Rewarded ads (continue, double coins) and a few interstitials between runs (max 1 every 3 runs). Ads come from Unity LevelPlay, with a consent popup where the law requires one |
| F10 | In-app purchases | "Remove ads", coin packs and premium cars, using Google Play Billing via Unity IAP |
| F11 | Update notice | On launch, the game checks the latest version. Optional updates show "Update available"; critical updates show a forced "Please update" screen |
| F12 | Push notifications | Optional, e.g. "Daily bonus ready!" or news. Sent with Firebase (free) |
| F13 | Account deletion | An in-app button that removes your online data (Google Play requires this) |
| F14 | Admin web panel | You log in with email, password and a 2FA code (an authenticator app). From the panel you can see players and scores, remove cheater scores, set the update notice, and send push notifications |

## 6. Look and feel
- Realistic HD 3D city at night with neon lights, police sirens, wet roads and motion blur.
- **Everything is original.** No real brands, car logos or copied game art. Assets can be **free or paid**:
  - Free: Poly Haven (CC0), free Unity Asset Store packs, and AI-generated models.
  - Paid (optional): premium Unity Asset Store packs, such as a city or cars, bought with a commercial licence. **Nothing is bought without your approval.** Before any purchase I'll show you the pack, its price and its licence, and you buy it yourself.
- **Visual target:** a console-style look on high-end phones:
  - URP post-processing (bloom, motion blur, colour grading)
  - reflections on wet roads
  - baked lighting
  - a "High" graphics preset
  - Low and Medium presets keep older phones smooth.
- **Honest expectation:** the game will look like a top modern mobile racing game, with console-style effects. It will not fully match a big-studio AAA console game, which needs teams of artists, custom art and millions of dollars. Paid packs close part of that gap, not all of it.

## 7. Money (monetization)
- **Free to play.**
- **Ads:** Unity LevelPlay.
- **In-app purchases:** Google Play Billing.
- Revenue is paid out by Google and Unity to your accounts. You set up the payout details yourself in their dashboards; I never handle them.

## 8. Recommended defaults (already applied)
- Cloud save is linked to Google sign-in.
- No deep links in v1.
- The update notice is optional by default, and forced only for critical updates.
- Account deletion is available inside the app.
- Android first.
- A small garage of unlockable cars.

## 9. Out of scope for v1 (can be added later)
- Multiplayer or racing against friends live
- iOS release
- Extra languages
- Story mode or missions
- Real car brands
- Chat
- Deep links
- Web or PC version

## 10. Costs (verified 2026-11-25 on official pages)
| Service | Cost to you | Source |
|---|---|---|
| Unity Personal | $0 while revenue and funding stay under US$200K a year | https://unity.com/products/unity-personal |
| GitHub (code + cloud builds) | $0 for a public repository | https://docs.github.com/en/billing/concepts/product-billing/github-actions |
| Supabase (login, leaderboard, saves, admin) | $0 on the free plan (50,000 monthly players, 500 MB database) | https://supabase.com/pricing |
| Firebase Cloud Messaging (push) | $0 | https://firebase.google.com/pricing |
| GitHub Pages (admin panel hosting) | $0 | — |
| Google Play Console | **US$25 one-time** (approved) | https://support.google.com/googleplay/android-developer/answer/6112435 |
| Premium art packs (optional) | **$0 by default.** Each pack is a one-time purchase, typically US$20–$100. Bought only if you approve that exact pack | https://assetstore.unity.com |
| **Total** | **US$25 one-time, $0 per month** (plus only the asset packs you approve) | — |

No paid subscriptions. If you ever outgrow the free plans (tens of thousands of daily players), I will tell you before anything costs money.

## 11. iOS — what it would need (optional, not required)
- An Apple Developer account at **US$99 per year**.
- Code signing certificates. These can be made in the cloud, but you need the Apple account.
- Builds can run on GitHub's macOS cloud machines, so you don't need to own a Mac for builds. A Mac or iPhone is still very helpful for testing.
- Because of the cost, iOS stays **optional** and is not part of v1.

## 12. Risks and honest notes
1. **Unity license file:** the cloud builder (GameCI) needs a Unity license file. Normally that file is created once with Unity Hub on a computer, which conflicts with "install nothing". I will find the most hands-off option in the Architecture phase. Worst case, it's a 10-minute one-time step, and I'll guide you through it.
2. **Supabase sleeps:** free Supabase projects pause after 1 week with no activity. A free scheduled GitHub job will "ping" it to keep it awake.
3. **Visual quality:** as described in section 6, expect a console-style look on high-end phones, but not full AAA quality. The High preset needs a recent phone, while older phones use Low or Medium. Paid packs are optional; the game ships with free assets if you don't buy any.
4. **Store review:** Google Play reviews apps and can ask for changes. Ad and data-safety forms must be filled in honestly; I will prepare the answers.
5. **Testing:** I can build and run automated tests in the cloud, but you'll need to try the APK on a real Android phone to judge how it feels.

## 13. What "done" means for v1
- An Android APK built automatically from GitHub.
- All features F1 to F14 working.
- Automated tests passing.
- The admin panel live with 2FA.
- All /docs written.
- A handover guide that explains each step you need to take, in plain words.
