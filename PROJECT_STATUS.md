# Project status — Police Pursuit (2026-11-28)

- **Endless-run game (F1–F14):** built, merged to `main`, deployed backend and admin panel; Android build works on the user's phone. Details: `docs/PROGRESS.md`.
- **Open-world mode (OW1–OW39):** built step by step on `feat/f1-f14`. OW1–OW36 committed (19 of those commits not yet pushed). Hide-in-traffic, spike strips (lane, roadblock, crossroads, helicopter, tunnel exit) and the canal with bridges are written but not yet committed. See `SESSION_HANDOVER.md`.
- **Tests:** scratch copy, Unity 6000.6.10f1. Last full pass: EditMode 40/40, PlayMode 47/47 (before the uncommitted steps).
- **Not verified anywhere:** open world by hand or on a phone; no Android build after `4af9b47`.
- **Image generation:** OpenAI script (key not set) and local ComfyUI with SDXL Turbo (works, ~3.5 min per image on this laptop).
- **Next:** see "Exact next steps" in `SESSION_HANDOVER.md`.
