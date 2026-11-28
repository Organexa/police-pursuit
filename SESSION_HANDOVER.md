# Session handover — Police Pursuit (2026-11-28)

Read this, then `docs/PROGRESS.md` (history) and `docs/FEATURE-TICKETS.md` (ticket OW list). Do not redo anything marked done.

## Objective
Original mobile police-pursuit game for Organexa (Unity 6 URP, Android first). Two modes:
- **Endless run** (F1–F14, approved PRD): lane-based getaway, ads, IAP, sign-in, cloud save, leaderboard, admin panel. Live on `main`.
- **Open world** (ticket OW1–OW39, added 2026-11-27 at the user's request, "add as new mode"): a walled 4×4 test district with physics cars, police AI, on-foot play, ranged + melee combat (no gore), arrest, the "Midnight Prison Break" mission (police or prisoner side), traffic, pedestrians, lights, weather, helicopter, dogs, spike strips. All placeholder art (boxes/capsules).

## Architecture (open world)
Everything is built in code at runtime (no scene edits). `Core/GameManager.cs` owns states (`FreeRoam`, `MissionResult`) and creates `Gameplay/OpenWorld.cs`, which builds the district and owns every open-world system:
`VehicleController` (Rigidbody + WheelColliders) · `PoliceChase` (grid-route AI, shared `Steer`) · `FleeDriver` · `Walker` + `TouchStick`/`LookPad` · `Health`/`IDamageable` · `WeaponData` (ScriptableObject) / `WeaponController` · `MeleeController` · `Suspect` · `Mission` · `CivilTraffic` (+`CivilCar`) · `TrafficLights` · `Pedestrians` · `RoadBlock` · `Helicopter` · `PoliceDog` · `PrintTrail` · `Weather` · `Puddles` · `SpikeStrip` · `CrossroadSpikes`. UI in `UI/GameUI.cs` (`BuildRoamHud`, `TickRoam`, mission intro/result). Tests: `Tests/EditMode/CoreTests.cs`, `Tests/PlayMode/FlowTests.cs` (one big file).

## State of the branch `feat/f1-f14`
- Remote (GitHub, public repo Organexa/police-pursuit) is at `bb261e5`. Its CI (`test` + `android-check`) passed for `4af9b47`; nothing later has been checked in CI.
- **19 local commits not pushed** (`6ff84de` … `ff7ea67`): roadblock, roadblock chase/pull-over, wanted level + helicopter, searchlight ahead, tunnel, police dogs, pond, bark, fences, dog routing, swim, shake, paw prints, footprints, rain, puddles, splashes, image scripts.
- **Uncommitted in the working tree** (written, not yet passing as a set): hide in traffic (OW37), lane spike strip (OW38), roadblock spikes, crossroads spikes, helicopter spike drop, tunnel-exit spikes, canal + bridges + bridge spikes. Files: `OpenWorld.cs`, `RoadBlock.cs`, `GameUI.cs`, `CoreTests.cs`, `FlowTests.cs`, new `SpikeStrip.cs`, `CrossroadSpikes.cs`.
- A full test run of that working tree was started after fixing a compile error (an extra `}` at the end of `FlowTests.cs`); its result was not read before handover. **Re-run it.**

## The user's standing instructions for the queue
Each step: test → commit only once it passes → the user said **push after the last of these commits** (the latest instruction: push once the tunnel-spikes commit is in; canal/bridge spikes then wait for the user's word). Commit order: hide in traffic → spike strip → roadblock spikes → crossroads spikes → helicopter spike drop → tunnel spikes → **push** → canal & bridge spikes (commit only if the user asks).
Because these steps edit the same files, they are tested together and split into commits by removing later steps' pieces; say so to the user (the in-between commits are not tested on their own).

## How tests are run (no Unity MCP here)
- Never open the real project for tests and never upgrade its Unity (project pins 6000.3.29f1). Use a scratch copy with Unity **6000.6.10f1**: `C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Unity.exe`.
- Copy `unity/Assets`, `unity/Packages`, `unity/ProjectSettings` to a scratch folder; also copy repo `shared/` next to it (EditMode tests read `../shared/*.json`). Run `-batchmode -projectPath <copy> -runTests -testPlatform EditMode|PlayMode -testResults <xml> -logFile <log>`; check `<log>` for `error CS` first, then the XML.
- Delete old result XML before each run (stale XML once looked like a pass). Don't run two Unity instances on one copy; never kill all `Unity.exe` (the user may have their own editor open; closing processes needs the user's OK).
- Last passing results: OW33–OW36 set: EditMode 40/40, PlayMode 47/47.

## Known issues / not verified
- Nothing in the open world has been played by hand or on a phone; no Android build since `4af9b47` (that APK passed CI and was ready as artifact `debug-apk`).
- Placeholder art, no animations; dogs can get stuck at fence corners only when their corner rule finds no clear corner; puddle "reflections" are a tint.
- `.gitignore` comment above `art/generated/` mentions only the OpenAI script (cosmetic).
- An untracked copy of the Go language source sits in the repo root (`api/ bin/ doc/ lib/ misc/ pkg/ src/ test/ go.env PATENTS VERSION CONTRIBUTING.md LICENSE README.md SECURITY.md codereview.cfg`): not ours, never commit it.
- Unity license file expires 2026-12-24 (renew via Unity Hub, update `UNITY_LICENSE` secret).

## Tools, MCP, plugins
- No Unity MCP. GitHub CLI `gh` is not installed; the unauthenticated GitHub API was rate-limited once (60/h) — read run pages in the built-in browser instead.
- Connected/relevant: Supabase MCP (project `police-pursuit`, ref `dumwtlqllfyhbdxglsvc`), built-in browser, Artifact tool. Playwright and the Fusion plugin failed to connect.
- Image generation: `tools/gen_image.py` (OpenAI images; needs `OPENAI_API_KEY` in the user's environment, not set yet; never handle the key in chat) and `tools/comfy_image.py` (local ComfyUI). ComfyUI 0.46.0 installed at `C:\ComfyUI` (venv Python 3.12, PyTorch 2.15 CPU, SDXL Turbo fp16 checked by SHA-256). Start with `C:\ComfyUI\run_cpu.bat` (bf16 model, fp32 VAE — bf16 VAE hangs on this CPU). One 512×512 image ≈ 214 s; close Unity first (16 GB RAM). Output: `art/generated/` (git-ignored).

## Key decisions
Open world is a separate mode beside the endless run · placeholder art until the user buys character/animation packs · fairness rules: AI never faster than the player, strips/roadblocks always leave a way through, civilians and dogs cannot be hurt · keyboard keys exist for editor testing · `com.unity.modules.vehicles` and `.particlesystem` added to the manifest.

## Exact next steps
1. Re-run the scratch tests on the current working tree; fix failures (usually test setup); read `error CS` first.
2. Split into commits in the order above, docs (`FEATURE-TICKETS.md` OW37+, `PROGRESS.md`) in the last one; verify the last commit equals the tested copy.
3. Push `feat/f1-f14` after the tunnel-spikes commit; check CI on the run page; report the APK.
4. Ask before committing canal/bridge spikes.
