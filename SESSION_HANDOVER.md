# Session handover — Police Pursuit (2026-11-28)

Read this, then `docs/PROGRESS.md` (history) and `docs/FEATURE-TICKETS.md` (ticket OW list). Do not redo anything marked done.

## Objective
Original mobile police-pursuit game for Organexa (Unity 6 URP, Android first). Two modes:
- **Endless run** (F1–F14, approved PRD): lane-based getaway, ads, IAP, sign-in, cloud save, leaderboard, admin panel. Live on `main`.
- **Open world** (ticket OW1–OW39, added 2026-11-27 at the user's request, "add as new mode"): a walled 4×4 test district with physics cars, police AI, on-foot play, ranged + melee combat (no gore), arrest, the "Midnight Prison Break" mission (police or prisoner side), traffic, pedestrians, lights, weather, helicopter, dogs, spike strips. All placeholder art (boxes/capsules).

## Architecture (open world)
Everything is built in code at runtime (no scene edits). `Core/GameManager.cs` owns states (`FreeRoam`, `MissionResult`) and creates `Gameplay/OpenWorld.cs`, which builds the district and owns every open-world system:
`VehicleController` (Rigidbody + WheelColliders) · `PoliceChase` (grid-route AI, shared `Steer`) · `FleeDriver` · `Walker` + `TouchStick`/`LookPad` · `Health`/`IDamageable` · `WeaponData` (ScriptableObject) / `WeaponController` · `MeleeController` · `Suspect` · `Mission` · `CivilTraffic` (+`CivilCar`) · `TrafficLights` · `Pedestrians` · `RoadBlock` · `Helicopter` · `PoliceDog` · `PrintTrail` · `Weather` · `Puddles` · `SpikeStrip` · `CrossroadSpikes`. UI in `UI/GameUI.cs` (`BuildRoamHud`, `TickRoam`, mission intro/result). Tests: `Tests/EditMode/CoreTests.cs`, `Tests/PlayMode/FlowTests.cs` (one big file).

## State of the branch `feat/f1-f14` (updated 2026-11-28, evening)
- Everything is committed and pushed: GitHub is at `11ed156`. OW1–OW43 are in (spike strips, canal and bridges included), plus the helicopter-strip-clears-on-leave fix and `tools/` image scripts.
- **CI is green again** on `11ed156` (run #81: unity and backend jobs passed). Runs #76–#78 had failed on flaky PlayMode tests on CI's slow runner (not a Unity-version issue): `OpenWorldPoliceCatchAParkedCar` (police circled the car), `OpenWorldPedestriansWalkPanicAndCannotBeHurt` (someone who briefly waited moved 0.95 m), `SirenHoldsPedestriansAtTheKerb` (a crossing started in one long frame before the hold).
- `11ed156` fix: `PoliceChase` and `FleeDriver` steer in `FixedUpdate` (were `Update`: long frames ran many physics steps on stale inputs). That exposed a **real game bug**: within 8 m the police eased to pedal 0.2 and stalled at ~7.7 m, outside the 7 m bust range, so they could never bust a stopped player; now they creep in at walking pace (coast above 2 m/s, pedal 0.5 below). The two pedestrian tests no longer depend on frame size.
- A run can show red only because the backend job's Supabase CLI download hit GitHub's rate limit (happened on #80); re-run it, it is not a test failure.

## The user's standing pattern
Test → commit only once it passes → push when the user says. When several steps edit the same files they are tested together and split into commits; tell the user the in-between commits were not tested on their own.

## How tests are run (no Unity MCP here)
- Never open the real project for tests and never upgrade its Unity (project pins 6000.3.29f1). Use a scratch copy with Unity **6000.6.10f1**: `C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Unity.exe`.
- Copy `unity/Assets`, `unity/Packages`, `unity/ProjectSettings` to a scratch folder; also copy repo `shared/` next to it (EditMode tests read `../shared/*.json`). Run `-batchmode -projectPath <copy> -runTests -testPlatform EditMode|PlayMode -testResults <xml> -logFile <log>`; check `<log>` for `error CS` first, then the XML.
- Delete old result XML before each run (stale XML once looked like a pass). Don't run two Unity instances on one copy; never kill all `Unity.exe` (the user may have their own editor open; closing processes needs the user's OK).
- Last passing results (2026-11-28, `11ed156`): scratch EditMode 43/43, PlayMode 54/54; CI run #81 the same.
- Last scratch copy (warm `Library`, in a temp folder that may be cleaned): `C:\Users\Pritam Singh\AppData\Local\Temp\claude\d2999c7a72780b98\f1a41871-1fd4-4da9-8b1e-16d877f85094\scratchpad\proj`. Seeding a new copy's `Library` from an old one saves most of the import.

## Known issues / not verified
- Only the police bust has been checked on a phone (`11ed156` APK); the rest of the open world has not been played by hand or on a phone. android-check builds a `debug-apk` artifact (kept 3 days) on every push to `feat/f1-f14`.
- Placeholder art, no animations; dogs can get stuck at fence corners only when their corner rule finds no clear corner; puddle "reflections" are a tint.
- `.gitignore` comment above `art/generated/` mentions only the OpenAI script (cosmetic).
- An untracked copy of the Go language source sits in the repo root (`api/ bin/ doc/ lib/ misc/ pkg/ src/ test/ go.env PATENTS VERSION CONTRIBUTING.md LICENSE README.md SECURITY.md codereview.cfg`): not ours, never commit it.
- Unity license file expires 2026-12-24 (renew via Unity Hub, update `UNITY_LICENSE` secret).

## Tools, MCP, plugins
- No Unity MCP. GitHub CLI `gh` is not installed; the unauthenticated GitHub API was rate-limited once (60/h) — read run pages in the built-in browser instead.
- **Finding failing CI tests:** game-ci prints the results XML into the job log. In the built-in browser (signed in to GitHub), on a github.com page run `fetch('/Organexa/police-pursuit/commit/<full sha>/checks/<jobId>/logs')` with javascript_tool and regex `<test-case ... result="Failed">` for name, message and `FlowTests.cs:<line>`. No download needed; the `unity-test-results` artifact is the alternative.
- Connected/relevant: Supabase MCP (project `police-pursuit`, ref `dumwtlqllfyhbdxglsvc`), built-in browser, Artifact tool. Playwright and the Fusion plugin failed to connect.
- Image generation: `tools/gen_image.py` (OpenAI images; needs `OPENAI_API_KEY` in the user's environment, not set yet; never handle the key in chat) and `tools/comfy_image.py` (local ComfyUI). ComfyUI 0.46.0 installed at `C:\ComfyUI` (venv Python 3.12, PyTorch 2.15 CPU, SDXL Turbo fp16 checked by SHA-256). Start with `C:\ComfyUI\run_cpu.bat` (bf16 model, fp32 VAE — bf16 VAE hangs on this CPU). One 512×512 image ≈ 214 s; close Unity first (16 GB RAM). Output: `art/generated/` (git-ignored).

## Key decisions
Open world is a separate mode beside the endless run · placeholder art until the user buys character/animation packs · fairness rules: AI never faster than the player, strips/roadblocks always leave a way through, civilians and dogs cannot be hurt · keyboard keys exist for editor testing · `com.unity.modules.vehicles` and `.particlesystem` added to the manifest.

## Exact next steps
Nothing is queued: CI is green and everything is pushed. Take the next ticket from `docs/FEATURE-TICKETS.md` (OW list) or what the user asks.
- 2026-11-28: the user installed the `11ed156` test APK (android-check run #23, `Builds/11ed156/`, git-ignored) and confirmed police now bust a stopped player on the phone. The rest of the open world has not been checked on the phone.
- New PlayMode tests: wait on conditions with generous limits, not fixed short waits; CI frames are long.
