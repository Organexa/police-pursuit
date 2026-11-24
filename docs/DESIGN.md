# DESIGN — Police Pursuit

_Version 1.0 · 2026-11-25 · Status: **Approved 2026-11-25**_

> **What this doc is:** how the game looks, feels and flows from screen to screen. It covers what the player sees and touches. The code side goes in the Architecture doc.

## 1. Your design choices
| Topic | Your answer | What we'll do |
|---|---|---|
| Menu style | "Check with Unity and use the best" | **Unity uGUI (Canvas) + TextMeshPro.** This is Unity's recommended default for mobile game menus. It's mature, fast on phones, and gives sharp text at any screen size |
| Controls | "I'm not sure. Recommend" | **On-screen buttons by default**, with tilt as an option in Settings |
| Camera | "Switchable" | **Chase camera by default**, plus a button to switch to a bumper (hood) view |
| City | "I'm not sure. Recommend" | **Rainy night downtown** with neon signs, highways and tunnels. Wet roads give the best reflections and the strongest "console" look |

## 2. Screen flow
```
Launch → Splash + update check ──(critical update)──► "Please update" (blocks play)
            │
            ▼
   Consent popup (ads/privacy, only where the law requires it)
            │
            ▼
   First run? ──yes──► Tutorial (skippable) ──┐
            │no                               │
            ▼                                 ▼
                       MAIN MENU
   ┌───────────┬──────────┬─────────────┬──────────┬────────┐
   PLAY      GARAGE   LEADERBOARD    SETTINGS    SHOP
     │
     ▼
   RUN (HUD) ⇄ PAUSE
     │ health hits 0
     ▼
   BUSTED! → "Watch ad to continue?" (once per run)
     │ no / already used
     ▼
   RESULTS (score, coins, best, "Double coins" ad) → Main menu / Play again
```
- If an **optional update** is available, a small "Update available" banner appears on the main menu. It doesn't block play.
- **Interstitial ads** appear at most once every 3 runs, only on the Results screen, and never during play.

## 3. Screens
| Screen | What's on it |
|---|---|
| **Splash** | Logo, loading bar, and the version check |
| **Main menu** | A 3D garage scene in the background with your car under neon lights. Big **PLAY** button, plus Garage, Leaderboard, Settings and Shop. Coin balance in the top-right. A "Sign in with Google" chip (optional) |
| **Tutorial** | 3 short interactive steps: steer, drift/brake, dodge a police car. A "Skip" button is always visible. It can be replayed from Settings |
| **Garage** | Swipe between 5–6 original cars. Stat bars for Speed, Handling and Armor. A button to unlock with coins or buy (IAP) |
| **Shop** | Remove ads, coin packs, premium cars. Real prices come from Google Play |
| **Leaderboard** | Global top 100, plus your own rank pinned at the bottom. Guests see "Sign in to join" |
| **Settings** | Sound volume, music volume, controls (Buttons / Tilt) with a tilt sensitivity slider, graphics (Low / Medium / High), haptics on/off, language (English), privacy & ads consent, replay tutorial, sign in/out, **Delete my account** |
| **Pause** | Resume, Restart, Settings, Quit to menu |
| **Busted** | Slow-motion crash, a red "BUSTED" title, and a "Continue (watch ad)" button with a 5-second countdown |
| **Results** | Score, distance, near-misses, coins earned, best score, "Double coins (ad)", Play again, Menu |
| **Delete account** | Clear warning text, a confirm button, and a "Done, your online data is removed" message |

## 4. In-run HUD (screen layout, phone held sideways)
```
┌──────────────────────────────────────────────────────────┐
│ ▓▓▓▓▓▓▓░░ HEALTH      ★★★☆☆        SCORE 12,480    ⏸   │
│ 🪙 245   1.8 km                                    🎥   │
│                                                          │
│                     (game view)                          │
│                                                          │
│  [ ◄ ]  [ ► ]                              [ DRIFT ]     │
└──────────────────────────────────────────────────────────┘
```
- **Top-left:** health bar, which turns red and pulses when low, then coins and distance.
- **Top-centre:** wanted stars (1–5). These are filled shapes, so you don't need to see colour to read them.
- **Top-right:** score, pause and camera switch.
- **Bottom:** left/right steer buttons (bottom-left) and a large Drift/Brake button (bottom-right). In tilt mode the steer buttons hide.
- **Pop-ups:** "+NEAR MISS +250" text flies in briefly near the centre.
- Every button is at least **48×48 dp**, Google's recommended minimum tap size, and stays clear of phone notches (safe area).

## 5. Controls
- **Gas is automatic.** The car always drives forward.
- **Steer:** hold the ◄ / ► buttons (default), or tilt the phone (option).
- **Drift/Brake:** hold to slow down and slide around corners. A good drift charges a small speed boost.
- **Haptics:** a short buzz on crashes and near-misses. You can turn it off.

## 6. Camera
- **Chase cam (default):** sits behind the car. It pulls back and widens the view as speed goes up, for a stronger sense of speed. It shakes slightly on hits.
- **Bumper cam:** a low view from the front of the car, more intense. Switch with the 🎥 button. The choice is remembered.

## 7. World and art direction
- **Setting:** an original city at night in the rain, with neon shop signs (made-up names), glass towers, elevated highways and tunnels.
- **Mood:**
  - Wet, shiny roads reflect red/blue police lights and neon.
  - Light fog adds depth.
  - Sparks fly on crashes.
- **Palette:** near-black and deep blue, with cyan and magenta neon accents. Police lights are red and blue.
- **Cars:** 5–6 original designs with no real brands or logos. Police cars use original livery.
- **Endless road:** built from repeating road "tiles" (straights, curves, tunnels, highway), so the drive never ends.
- **Hazards:**
  - civilian traffic
  - roadblocks, marked with flashing barriers
  - spike strips, shown by a bright warning arrow about 1 second before they appear
- **Assets:** original free assets (Poly Haven CC0, free Unity Asset Store packs, Meshy AI models). Paid packs only if you approve each one.

## 8. Menu look (UI style)
- **Colours:**
  - dark glass panels (near-black, slightly see-through)
  - cyan for primary buttons
  - magenta for highlights and rewards
  - red for danger (busted, low health)
- **Text:** one bold, wide, free sci-fi font (open licence) for titles and one clean font for body text, both through TextMeshPro.
- **Motion:** buttons gently scale on tap, and screens slide in quickly (under 0.3 s). Nothing slows you down.
- **Text size:** at least 14 sp equivalent, with strong contrast against dark panels.

## 9. Sound
- **Music:** original royalty-free synthwave/electronic tracks. They get more intense as wanted stars rise.
- **Effects:** engine, sirens that get louder as police get closer, crashes, coin pickup, near-miss "whoosh", and UI clicks.
- Music and effects have separate volume sliders.

## 10. Graphics presets
| Preset | For | What's on |
|---|---|---|
| **Low** | Older or budget phones | 30 fps, lower resolution, no motion blur, simple reflections |
| **Medium** | Most phones (default on mid-range) | 60 fps target, bloom, colour grading, basic wet-road reflections |
| **High** | Recent flagship phones | Everything: bloom, motion blur, colour grading, better reflections, more traffic and detail |

The game picks a preset automatically on first launch. You can change it in Settings.

## 11. Accessibility
- Big tap targets (48 dp or more) and safe-area aware layout.
- Wanted level and health are readable without colour (shapes, bar length).
- Spike-strip and roadblock warnings use **both** a sound and an icon.
- Haptics can be turned off. Tilt is optional.
- No flashing faster than 3 times per second (photosensitivity guideline).

## 12. Admin web panel (for you only)
A simple dark dashboard website, hosted free on GitHub Pages.

| Page | What it does |
|---|---|
| **Login** | Email + password, then a 6-digit code from your authenticator app (2FA) |
| **Dashboard** | Player count, runs today, top score |
| **Players** | Search players, see their best score and when they were last seen |
| **Scores** | The leaderboard list with a "Remove (cheater)" button and a reason note |
| **Update notice** | Set the latest version, mark it optional or **critical**, and add a message |
| **Push** | Write a title and message, preview, then send to all players |

## 13. Not in this design (v1)
Multiplayer, story missions, real car brands, extra languages and a photo mode are all left out. They can be added later.
