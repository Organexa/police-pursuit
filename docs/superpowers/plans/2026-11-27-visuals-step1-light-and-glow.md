# Visuals step 1: light and glow — Implementation Plan

> **For agentic workers:** use superpowers:executing-plans (native, in this session; CLAUDE.md forbids unasked subagents). Steps use checkbox syntax.

**Goal:** night sky, fog and neon glow on a URP setup that CI builds include.

**Architecture:** an editor script creates and assigns the URP assets when the editor loads (as `PackageName.cs` sets player settings), so no version-specific asset is committed from the local 6000.6 editor. A runtime class builds the post-processing volume in code and follows the graphics preset.

**Tech Stack:** Unity 6000.3.29f1, URP 17.3.0.

**Spec:** `docs/superpowers/specs/2026-11-27-visuals-design.md`

## Global Constraints
- Stay on Unity 6000.3.29f1; test on a scratch copy.
- Low: colour grading only. Medium: bloom too. High: motion blur too.
- 60 fps mid-range on Medium, 30 fps budget on Low; download under 150 MB.
- If URP cannot be set up reliably this way, stop and tell the owner.
- No commit or push unless the owner says so.

## Review Focus
- Phone build draws pink or black (shader left out of the build): every object keeps its colour.
- Post effects show in the editor but not on the phone (variants stripped): bloom visible in the APK.
- Changing the preset in Settings mid-run: effects change at once.
- Bloom makes coins or traffic hard to see: only neon, lights and coins glow, faintly.
- Fog hides a car too late to dodge it: traffic stays clear at spawn distance.

---

### Task 1: URP setup

**Files:** Create `unity/Assets/Editor/RenderSetup.cs`; Modify `unity/.gitignore` or root `.gitignore` (ignore `unity/Assets/Settings/`); Test `unity/Assets/Tests/PlayMode/FlowTests.cs`.

**Interfaces:** Produces `RenderSetup.Apply()`; assets `Assets/Settings/Urp.asset` and `UrpRenderer.asset`; HDR on; post-process data assigned; unused post-processing variants not stripped.

- [ ] Add PlayMode test `UsesTheUniversalPipeline`: `Assert.IsInstanceOf<UniversalRenderPipelineAsset>(GraphicsSettings.currentRenderPipeline)`.
- [ ] Run on the scratch copy; expect FAIL.
- [ ] Implement `RenderSetup` (`[InitializeOnLoad]`, idempotent; also `IPreprocessBuildWithReport` that fails the build if no pipeline is assigned).
- [ ] Run again from a scratch copy with no `Library`; expect PASS.

### Task 2: Night look

**Files:** Create `unity/Assets/Scripts/Gameplay/NightLook.cs`; Modify `Resources/Flat.shader` (fog, `_Glow`), `Gameplay/Shapes.cs`, `Gameplay/CarCamera.cs`, `Gameplay/RoadTiles.cs`, `Gameplay/Coins.cs`, `Gameplay/Traffic.cs`, `Core/GameManager.cs`, assembly definition if URP needs referencing; Test `Tests/EditMode/CoreTests.cs`.

**Interfaces:** Produces `NightLook.Effects(GfxPreset p) : (bool bloom, bool motionBlur)`; `NightLook.Apply(GfxPreset p)`; `Shapes.Box(..., float glow = 0)` where glow multiplies the colour above 1.

- [ ] EditMode test `EffectsFollowThePreset`: Low = (false,false), Medium = (true,false), High = (true,true).
- [ ] Run; expect FAIL.
- [ ] Implement: global `Volume` with Bloom, ColorAdjustments and MotionBlur; camera `renderPostProcessing`; linear fog from 90 m to 300 m in the sky colour; glow on road edges, roof bands, police lights and coins; call `Apply` at start and on preset change.
- [ ] Run EditMode and PlayMode; expect all PASS.

### Task 3: Proof

- [ ] Windows player build on the scratch copy; screenshot a run in Medium; look at it and tune.
- [ ] Update `docs/PROGRESS.md`.
- [ ] Ask the owner before commit and push; the CI APK is the phone check.
