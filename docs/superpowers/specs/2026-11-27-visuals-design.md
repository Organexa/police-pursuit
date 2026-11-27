# Visual upgrade — design

_2026-11-27 · Status: awaiting approval_

## Intent
Replace the placeholder boxes with the rainy neon night city from `DESIGN.md` §7, before any store release.

## Goals
- Cars that read as cars; police recognisable at a glance by shape and red/blue lights.
- A night city with neon glow, light fog, wet-looking road, rain and crash sparks.
- 60 fps on a mid-range phone on Medium; 30 fps on a budget phone on Low (TRD §6).
- Download under 150 MB.

## Not in this work
- Photo-realism like the app icon.
- Curves, tunnels, elevated highways, roadblocks, spike strips (gameplay work, later).
- Helicopter, new fonts, menu restyle.
- Any change to driving, damage, scoring or online features.

## Anti-goals
- Looks nicer but stutters, or the player can no longer see traffic and coins clearly.

## Approaches
| Approach | Trade-off |
|---|---|
| A. Shapes built in code | Free, tiny, still looks simple |
| **B. Free CC0 model packs (chosen by you)** | Free, good stylised look, not realistic |
| C. Paid realistic packs | Closest to realistic; costs money, heavier on phones |

## Assets
Kenney, licence Creative Commons CC0 on each page (checked 2026-11-27):
- Car Kit — https://kenney.nl/assets/car-kit
- City Kit (Roads) — https://kenney.nl/assets/city-kit-roads
- City Kit (Commercial) — https://kenney.nl/assets/city-kit-commercial

Each download is approved by you first. Only the models used go into the repo, with the licence file.

## Steps (each ends in a test APK you judge)
1. **Light and glow.** Set up URP (not set up today), night sky and fog, bloom and colour grading by preset: Low colour grading only, Medium adds bloom, High adds motion blur.
2. **Cars.** Models for the player (every garage car distinct), traffic and police; flashing red/blue lights at no more than 3 per second. Hit boxes keep today's size and models are scaled to fit.
3. **City and road.** Buildings, road pieces, street lights and neon signs with made-up names, reused as the road recycles.
4. **Weather.** Rain, wet-road shine, crash sparks. Real reflections only on High, and dropped if they cost the frame rate.

## Constraints
- The project stays on Unity 6000.3.29f1.
- The world stays built in code; there is no hand-made scene.
- CI builds from a clean machine, so render settings must be committed files or set at build time.
- If URP cannot be set up reliably that way, stop and tell you before going further.

## How each step is checked
- Existing EditMode and PlayMode tests pass; new logic (preset → effects) gets a test.
- A screenshot from a desktop build before you get the APK.
- You judge look and smoothness on your phone. Frame rate is not measured automatically.

## Left to the builder
Colours, layout of buildings and signs, which model suits which car, effect strengths.
