# MEMORY — Police Pursuit

_Last updated: 2026-11-25_

> **What this doc is:** a short project memory, so anyone (or any future AI session) can pick up the work without re-reading everything. The live status is in `PROGRESS.md`.

## Who and what
- **Owner:** you. You're a beginner and install no software; everything is built in the cloud.
- **Product:** "Police Pursuit", an original endless 3D getaway-driving game for Android, set in a rainy neon city at night.
- **Approved docs (2026-11-25):**
  - PRD v1.1
  - DESIGN v1.0
  - ARCHITECTURE v1.0 + TRD v1.0

## Key decisions (with the reason)
| Decision | Why |
|---|---|
| Unity 6 LTS + URP, built by GameCI on GitHub Actions | Best mobile 3D for the effort; $0 builds in a public repo |
| Supabase (one free project) | Login, database, server code and admin 2FA in one free service |
| Credential Manager → Google ID token → Supabase | Google sign-in with no deep links or browser redirects |
| Server recomputes scores from raw run stats | Basic anti-cheat without a paid service |
| Coins live on the phone and in the cloud save | Normal for single-player; only leaderboard scores are server-checked |
| Admin panel is a static site on GitHub Pages | Free hosting; Supabase RLS + `aal2` is the real security |
| Unity Personal license comes from a one-time Unity Hub step | GameCI has no cloud-only route for Personal licenses |
| Keep-alive ping every 3 days | Free Supabase projects pause after 7 idle days |

## Things to remember
- **Never ask for secrets in chat.** You add them yourself (`DEPLOYMENT.md`).
- **Paid asset packs:** optional. Each needs your approval, and you buy it yourself.
- **iOS is optional:** it needs the US$99/year Apple account, so it's not in v1.
- **Still to confirm during the build:**
  - the exact Google Play target SDK at release time;
  - the consent setup (LevelPlay's built-in consent vs Google UMP).
- **Known issues:**
  - Earlier, the WebSearch and web-fetch tools failed with "model not found".
  - The built-in browser works for checking official pages.
