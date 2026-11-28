# Session handover — Police Pursuit (2026-11-28, late)

Read this, then `docs/PROGRESS.md` (history) and `docs/FEATURE-TICKETS.md` (ticket OW list). Do not redo anything marked done.

## Objective
Original mobile police-pursuit game for Organexa (Unity 6 URP, Android first). Two modes:
- **Endless run** (F1–F14, approved PRD): lane-based getaway, ads, IAP, sign-in, cloud save, leaderboard, admin panel. Live on `main`.
- **Open world** (ticket OW1–OW43, added 2026-11-27 at the user's request, "add as new mode"): a walled 4×4 test district with physics cars, police AI, on-foot play, ranged + melee combat (no gore), arrest, the "Midnight Prison Break" mission (police or prisoner side), traffic, pedestrians, lights, weather, helicopter, dogs, spike strips, canal. All placeholder art (boxes/capsules).

## Architecture (open world)
Everything is built in code at runtime (no scene edits). `Core/GameManager.cs` owns states (`FreeRoam`, `MissionResult`) and creates `Gameplay/OpenWorld.cs`, which builds the district and owns every open-world system:
`VehicleController` (Rigidbody + WheelColliders) · `PoliceChase` (grid-route AI, shared `Steer`) · `FleeDriver` · `Walker` + `TouchStick`/`LookPad` · `Health`/`IDamageable` · `WeaponData` (ScriptableObject) / `WeaponController` · `MeleeController` · `Suspect` · `Mission` · `CivilTraffic` (+`CivilCar`) · `TrafficLights` · `Pedestrians` · `RoadBlock` · `Helicopter` · `PoliceDog` · `PrintTrail` · `Weather` · `Puddles` · `SpikeStrip` · `CrossroadSpikes`. UI in `UI/GameUI.cs` (`BuildRoamHud`, `TickRoam`, mission intro/result). Tests: `Tests/EditMode/CoreTests.cs`, `Tests/PlayMode/FlowTests.cs` (one big file).

## State of the branch `feat/f1-f14`
- Everything is committed and pushed: GitHub is at `7ab263d` (parked police cars; `2a79b3a` before it makes every escape start calm). CI passes on it: PlayMode 56/56, EditMode 43/43. Latest phone build: `Builds/7ab263d/`.
- A separate project for Organexa was started on 2026-11-28 in `C:\Organexa-Business-Suite` (its own session); it is not part of this repo.
- **All 43 open-world tickets are ☑ (done)**: played on the phone in five rounds with `docs/PHONE-CHECK.md` (every box ticked, frame rate / buttons / text included).
- Today's fixes, all in PlayMode tests and on CI:
  - `11ed156`: `PoliceChase` and `FleeDriver` steer in `FixedUpdate` (long CI frames ran many physics steps on stale inputs). Real game bug found by it: within 8 m the police stalled at ~7.7 m, outside the 7 m bust range; they now creep in (coast above 2 m/s, pedal 0.5 below). Checked on the phone.
  - `b437033`: on-foot buttons at least 20 canvas units apart (FIRE/RELOAD/DODGE caught each other on the phone; new test `OnFootButtonsKeepAThumbsWidthApart`); police slow to 8 m/s (`PoliceChase.CornerSpeed`) before a crossroads where their route turns (they ran wide into building corners); `SirenSpooksTheEscapee` waits a physics step. Checked on the phone.
- A CI run can show red only because the backend job's Supabase CLI download hit GitHub's rate limit (happened on #80); re-run it, it is not a test failure.

## The user's standing pattern
Test → commit only once it passes → push when the user says (they reply "Commit and push" each time; ask). When several steps edit the same files they are tested together and split into commits; tell the user the in-between commits were not tested on their own. A side session will not commit on a relayed message: it needs the user's own words in that session, or the parent session commits.

## How tests are run (no Unity MCP here)
- Never open the real project for tests and never upgrade its Unity (project pins 6000.3.29f1). Use a scratch copy with Unity **6000.6.10f1**: `C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Unity.exe`.
- Copy `unity/Assets`, `unity/Packages`, `unity/ProjectSettings` to a scratch folder; also copy repo `shared/` next to it (EditMode tests read `../shared/*.json`). Run `-batchmode -projectPath <copy> -runTests -testPlatform EditMode|PlayMode -testResults <xml> -logFile <log>`; check `<log>` for `error CS` first, then the XML. In sh scripts, build Windows paths with `/`, not `\\$var` (that escapes the `$`).
- Delete old result XML before each run (stale XML once looked like a pass). Don't run two Unity instances on one copy; never kill all `Unity.exe` (the user may have their own editor open; closing processes needs the user's OK).
- Last passing results (2026-11-28, `b437033`): scratch EditMode 43/43, PlayMode 55/55 (three full runs in a row).
- Last scratch copy (warm `Library`, in a temp folder that may be cleaned): `C:\Users\Pritam Singh\AppData\Local\Temp\claude\d2999c7a72780b98\f1a41871-1fd4-4da9-8b1e-16d877f85094\scratchpad\proj`. Seeding a new copy's `Library` from an old one saves most of the import.
- New PlayMode tests: wait on conditions with generous limits, not fixed short waits; CI frames are long.

## Phone builds
android-check builds a `debug-apk` artifact (kept 3 days) on every push to `feat/f1-f14`. To fetch it: open the run in the built-in browser (signed in), `navigate` to `/actions/runs/<run>/artifacts/<id>` (reports "failed", but the zip downloads to the user's Desktop), check its SHA-256 against the one on the run page, move it to `Builds/<sha>/` (git-ignored) and unzip. Latest: `Builds/b7b488f/PolicePursuit.apk`. The user installs it themselves.

## Known issues / not verified
- Placeholder art, no animations; dogs can get stuck at fence corners only when their corner rule finds no clear corner; puddle "reflections" are a tint.
- `.gitignore` comment above `art/generated/` mentions only the OpenAI script (cosmetic).
- An untracked copy of the Go language source sits in the repo root (`api/ bin/ doc/ lib/ misc/ pkg/ src/ test/ go.env PATENTS VERSION CONTRIBUTING.md LICENSE README.md SECURITY.md codereview.cfg`): not ours, never commit it.
- Unity license file expires 2026-12-24 (renew via Unity Hub, update `UNITY_LICENSE` secret).

## Tools, MCP, plugins
- No Unity MCP. GitHub CLI `gh` is not installed; the unauthenticated GitHub API was rate-limited once (60/h) — read run pages in the built-in browser instead.
- **Finding failing CI tests:** game-ci prints the results XML into the job log. In the built-in browser (signed in to GitHub), on a github.com page run `fetch('/Organexa/police-pursuit/commit/<full sha>/checks/<jobId>/logs')` with javascript_tool and regex `<test-case ... result="Failed">` for name, message and `FlowTests.cs:<line>`. No download needed; the `unity-test-results` artifact is the alternative.
- Connected/relevant: Supabase MCP (project `police-pursuit`, ref `dumwtlqllfyhbdxglsvc`), built-in browser, Artifact tool. Playwright and the Fusion plugin failed to connect.
- **Art: the user wants ComfyUI for every image, scene, design and 3D model** (saved in memory). ComfyUI 0.46.0 at `C:\ComfyUI` (venv Python 3.12, PyTorch 2.15 CPU). Start `C:\ComfyUI\run_cpu.bat`; close Unity first if RAM is short (16 GB). Output `art/generated/` (git-ignored, never commit). `tools/gen_image.py` (OpenAI) only if the user asks.
  - Images: `tools/comfy_image.py "<prompt>" --name x` (SDXL Turbo, ~1–3 min per 512×512).
  - 3D: `C:\ComfyUI\venv\Scripts\python.exe tools/comfy_3d.py <image>` (not with `-I`) → BiRefNet removes the background, TripoSR builds a vertex-coloured `.obj` (~3.5 min). Then `... tools/game_mesh.py <mesh.obj> --length L --width W --height H` → game-ready `<name>-game/` (Y up, front +Z, metres, pivot on the ground, ~8k triangles, colours baked to a PNG, white material); pass real width/height, TripoSR makes things too fat. Checked importing into Unity 6000.6.
  - Installed: custom node `ComfyUI-Flowty-TripoSR` (commit a0c94ac, with a local patch in `tsr/system.py` renaming ViT keys for transformers 5; never `pip install -r` its requirements.txt, it pins transformers 4.35), `models/checkpoints/triposr.ckpt` (1.68 GB), `models/background_removal/birefnet.safetensors` (444 MB, MIT), pip `trimesh scikit-image omegaconf imageio fast-simplification xatlas`. All downloads were SHA-256-checked. Hunyuan3D 2.1 is in ComfyUI core but needs a GPU. Ask before any new model download.
  - Made so far: a police car (8,000 triangles, 4.6 × 1.9 × 1.5 m). Good for a parked/background prop, not the car you drive (hidden side and wheels are blobs). **In the game since `7ab263d`:** `Resources/ParkedPoliceCar/` (URP Lit, no `.meta` files committed, as in the rest of the repo), placed by `OpenWorld.ParkPoliceCars()` as 8 parked cars in the strip between the outer roads and the wall (off every road, so roadblock gaps and spike strips are untouched); test `ParkedPoliceCarsSitOffEveryRoad`. The user checked them on the phone (2026-11-28): fine.

## Key decisions
Open world is a separate mode beside the endless run · placeholder art until the user buys character/animation packs (or ComfyUI props stand in) · fairness rules: AI never faster than the player, strips/roadblocks always leave a way through, civilians and dogs cannot be hurt · keyboard keys exist for editor testing · `com.unity.modules.vehicles` and `.particlesystem` added to the manifest.

## Exact next steps
Nothing is queued: CI is green, everything is pushed, every open-world ticket is done. Ask the user what is next. Open questions that are theirs to decide:
- Whether to make more props with ComfyUI (the parked police cars are in and checked).
- Character art and animations (buy a pack, or placeholders).
- Whether open-world scores go to the leaderboard (needs a server rule).
