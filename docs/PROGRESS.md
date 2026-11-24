# Progress — Police Pursuit

_Last updated: 2026-11-25_

## Current phase
**PRD v1.1, DESIGN v1.0, ARCHITECTURE + TRD v1.0 approved (2026-11-25). All docs written.** T0.1 scaffold done. Next: T0.2 (CI workflows) — needs your GitHub repo and username → test → deploy → handover.

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

## Next
- [ ] Build T0.2 → T0.3, then F1–F14 (order in FEATURE-TICKETS.md)
- [ ] ◐ GitHub setup (DEPLOYMENT §1 + Unity secrets from §4) — in progress, waiting for your GitHub username
- [ ] Supabase setup (DEPLOYMENT §2) — I'll say when it's needed
- [ ] Test, deploy, handover
