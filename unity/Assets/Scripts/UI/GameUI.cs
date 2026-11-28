using System.Collections.Generic;
using PolicePursuit.Core;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

namespace PolicePursuit.UI
{
    // All screens, built in code.
    // shortcut: legacy Text with the built-in font; switch to TextMeshPro once its resources are imported.
    public class GameUI : MonoBehaviour
    {
        // Canvas units. The reference height is 450, so one unit is about 1 dp on a landscape phone.
        static readonly Vector2 Button = new Vector2(220, 56);
        static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

        GameManager game;
        Font font;
        GameObject menu, hud, pause, settings, busted, results, skip, garage, board, updateBanner, updateScreen, consent, continueButton, doubleButton, shop, deleteButton, deleteScreen, deleteConfirm;
        RectTransform healthFill;
        Text coinsText, distanceText, scoreText, popup, menuText, resultText, presetText, tutorialText,
            carText, carCoins, carButton, signInText, boardText, rankText, updateText, continueText, deleteText, signInProblem;
        ScrollRect boardScroll;
        readonly RectTransform[] statFills = new RectTransform[3];
        int carIndex;
        readonly List<(string id, string name, Text label)> shopRows = new List<(string, string, Text)>();
        readonly Image[] stars = new Image[5];
        HoldButton left, right, drift;
        GameObject roamHud;
        HoldButton roamLeft, roamRight, gas, brake, handbrake;
        Text speedText, bustText, doorText;
        // Open-world status bars and mini-map (milestone 2).
        public RectTransform HealthFill { get; private set; }
        public RectTransform StaminaFill { get; private set; }
        public RectTransform CarFill { get; private set; }
        public RectTransform MapPlayer { get; private set; }
        public RectTransform MapPolice { get; private set; }
        public RectTransform MapFleer { get; private set; }
        public const float MapSize = 128;
        // Controls layout (milestone 2): the open-world controls, mirrored for left hands and scaled from their corner.
        public const float MinButtonScale = 0.8f, MaxButtonScale = 1.3f;
        readonly System.Collections.Generic.List<(RectTransform rect, Vector2 pos, Vector2 anchor)> pads =
            new System.Collections.Generic.List<(RectTransform, Vector2, Vector2)>();
        bool layoutLeft;
        float layoutScale = -1;
        GameObject layout;
        Text handText;
        Slider sizeSlider;
        public static float MapScale => MapSize / (2 * Gameplay.OpenWorld.Edge); // pixels per metre
        GameObject driveControls, footControls;
        HoldButton sprint, jump, fire, block, arrest;
        Text sirenButton;
        Text objectiveText, missionResultText, wantedText;
        GameObject missionIntro, missionResult;
        Text ammoText;
        GameObject crosshair;
        TouchStick stick;
        LookPad look;
        float popupUntil;
        GameState settingsFrom;
        Slider sound, music, tilt;
        Text controlsText, hapticsText;

        public void Init(GameManager manager)
        {
            game = manager;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 450);
            // The canvas is never smaller than the reference, so nothing is cut off on wide or squarish screens.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var safe = Rect("SafeArea", transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            BuildHud(safe);
            BuildRoamHud(safe);
            BuildScreens(safe);
            BuildMission(safe);
        }

        public void Show(GameState state)
        {
            menu.SetActive(state == GameState.Menu);
            hud.SetActive(state == GameState.Run);
            roamHud.SetActive(state == GameState.FreeRoam);
            missionIntro.SetActive(false);
            missionResult.SetActive(state == GameState.MissionResult);
            if (state == GameState.MissionResult) ShowMissionResult();
            pause.SetActive(state == GameState.Pause);
            busted.SetActive(state == GameState.Busted);
            results.SetActive(state == GameState.Results);
            settings.SetActive(false);
            layout.SetActive(false);
            garage.SetActive(false);
            board.SetActive(false);
            consent.SetActive(false);
            shop.SetActive(false);
            deleteScreen.SetActive(false);
            popup.text = "";
            UpdateTutorial();

            if (state == GameState.Menu) RefreshSaved();
            if (state == GameState.Results)
            {
                var s = game.Stats;
                resultText.text = $"Score {s.Score}\nDistance {(int)s.DistanceM} m\nNear misses {s.NearMisses}\n" +
                    $"Coins {s.Coins}\nBest {GameManager.BestScore}";
            }
        }

        // Also called when the cloud save is merged in, so open screens show the new values.
        public void RefreshSaved()
        {
            menuText.text = $"Best {GameManager.BestScore}    Coins {GameManager.TotalCoins}";
            if (garage.activeSelf) ShowCar(carIndex);
            if (shop.activeSelf) ShowShop();
        }

        // Show() never hides the forced screen, so nothing behind it can be reached.
        public void ShowUpdate(UpdateLevel level, string message)
        {
            updateBanner.SetActive(level == UpdateLevel.Optional);
            updateScreen.SetActive(level == UpdateLevel.Critical);
            updateText.text = string.IsNullOrWhiteSpace(message)
                ? "This version no longer works. Update to keep playing." : message;
        }

        public void ShowNearMiss()
        {
            popup.text = "+NEAR MISS +250";
            popupUntil = Time.unscaledTime + 1;
        }

        void Update()
        {
            if (game == null) return;
            if (game.State == GameState.Menu)
                signInProblem.text = game.Auth.Failed && !game.Auth.SignedIn ? game.Auth.FailReason : "";
            if (game.State == GameState.Menu)
                signInText.text = game.Auth.Busy ? "Signing in..." : game.Auth.SignedIn ? "Sign out"
                    : game.Auth.Failed ? "Retry sign-in" : "Sign in with Google";
            continueButton.SetActive(game.State == GameState.Busted && game.ContinueLeft > 0);
            continueText.text = $"Continue (watch ad) {Mathf.CeilToInt(game.ContinueLeft)}";
            doubleButton.SetActive(game.CanDouble);
            if (game.State == GameState.FreeRoam) { TickRoam(); return; }
            if (game.State != GameState.Run) return;
            UpdateTutorial();

            var s = game.Settings;
            left.gameObject.SetActive(!s.tiltControls);
            right.gameObject.SetActive(!s.tiltControls);
            game.Player.ButtonSteer = s.tiltControls
                ? Gameplay.KeyInput.Tilt(s.tiltSensitivity)
                : (right.Held ? 1 : 0) - (left.Held ? 1 : 0);
            game.Player.ButtonDrift = drift.Held;

            healthFill.anchorMax = new Vector2(game.Player.Health / Gameplay.PlayerCar.MaxHealth, 1);
            coinsText.text = $"Coins {game.Stats.Coins}";
            distanceText.text = $"{(int)game.Stats.DistanceM} m";
            scoreText.text = game.Stats.Score.ToString();
            for (int i = 0; i < stars.Length; i++)
                stars[i].color = i < game.Wanted ? Gameplay.Shapes.Magenta : new Color(1, 1, 1, 0.2f);
            if (Time.unscaledTime > popupUntil) popup.text = "";
        }

        void BuildHud(RectTransform safe)
        {
            hud = Panel(safe, "Hud", Color.clear);
            var root = hud.transform;
            var topLeft = new Vector2(0, 1);
            var topCentre = new Vector2(0.5f, 1);
            var topRight = Vector2.one;
            var bottomLeft = Vector2.zero;
            var bottomRight = new Vector2(1, 0);

            var bar = Box(root, "HealthBar", topLeft, new Vector2(16, -16), new Vector2(180, 16), Gameplay.Shapes.Navy);
            healthFill = Rect("Fill", bar.transform);
            Stretch(healthFill);
            healthFill.gameObject.AddComponent<Image>().color = Gameplay.Shapes.Red;

            coinsText = Label(root, topLeft, new Vector2(16, -40), new Vector2(180, 24), 18, TextAnchor.MiddleLeft);
            distanceText = Label(root, topLeft, new Vector2(16, -66), new Vector2(180, 24), 18, TextAnchor.MiddleLeft);
            for (int i = 0; i < stars.Length; i++)
                // shortcut: wanted "stars" are squares until there is star art.
                stars[i] = Box(root, "Star", topCentre, new Vector2((i - 2) * 28, -16), new Vector2(20, 20), Color.white);
            scoreText = Label(root, topRight, new Vector2(-16, -12), new Vector2(200, 32), 24, TextAnchor.MiddleRight);

            MakeButton(root, "II", topRight, new Vector2(-16, -52), new Vector2(56, 56), game.Pause);
            MakeButton(root, "CAM", topRight, new Vector2(-80, -52), new Vector2(64, 56), () => game.Cam.Toggle());

            left = Hold(root, "<", bottomLeft, new Vector2(16, 16), new Vector2(96, 96));
            right = Hold(root, ">", bottomLeft, new Vector2(128, 16), new Vector2(96, 96));
            drift = Hold(root, "DRIFT", bottomRight, new Vector2(-16, 16), new Vector2(130, 96));

            popup = Label(root, Middle, new Vector2(0, 80), new Vector2(360, 36), 26, TextAnchor.MiddleCenter);
            popup.color = Gameplay.Shapes.Cyan;
            tutorialText = Label(root, topCentre, new Vector2(0, -44), new Vector2(360, 60), 22, TextAnchor.MiddleCenter);
            skip = MakeButton(root, "Skip", new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(120, 56),
                game.FinishTutorial).transform.parent.gameObject;
        }

        // Driving controls for the open world; only shown in that mode.
        // Open-world controls; only the set for driving or for walking is shown.
        void BuildRoamHud(RectTransform safe)
        {
            roamHud = Panel(safe, "RoamHud", Color.clear);
            var root = roamHud.transform;
            var bottomRight = new Vector2(1, 0);

            driveControls = Panel(root, "Drive", Color.clear);
            var drive = driveControls.transform;
            roamLeft = Hold(drive, "<", Vector2.zero, new Vector2(16, 16), new Vector2(96, 96));
            roamRight = Hold(drive, ">", Vector2.zero, new Vector2(128, 16), new Vector2(96, 96));
            gas = Hold(drive, "GAS", bottomRight, new Vector2(-16, 16), new Vector2(100, 120));
            brake = Hold(drive, "BRAKE", bottomRight, new Vector2(-128, 16), new Vector2(100, 96));
            handbrake = Hold(drive, "HAND", bottomRight, new Vector2(-128, 124), new Vector2(100, 56));
            MakeButton(drive, "HORN", bottomRight, new Vector2(-16, 150), new Vector2(100, 48), Honk);
            sirenButton = MakeButton(drive, "SIREN", bottomRight, new Vector2(-128, 184), new Vector2(100, 44), () => game.World.ToggleSiren());

            footControls = Panel(root, "Foot", Color.clear);
            footControls.AddComponent<Image>().color = Color.clear; // catches camera drags behind the buttons
            look = footControls.AddComponent<LookPad>();
            var foot = footControls.transform;
            var pad = Box(foot, "Stick", Vector2.zero, new Vector2(24, 24), new Vector2(150, 150), new Color(1, 1, 1, 0.12f));
            stick = pad.gameObject.AddComponent<TouchStick>();
            stick.Knob = Box(pad.transform, "Knob", Middle, Vector2.zero, new Vector2(60, 60), Gameplay.Shapes.Cyan).rectTransform;
            stick.Knob.GetComponent<Image>().raycastTarget = false;
            sprint = Hold(foot, "SPRINT", bottomRight, new Vector2(-16, 16), new Vector2(110, 96));
            // Columns and rows 20 apart (about 4 mm on a phone): closer, a thumb on one button catches its neighbour.
            jump = Hold(foot, "JUMP", bottomRight, new Vector2(-146, 16), new Vector2(96, 72));
            fire = Hold(foot, "FIRE", bottomRight, new Vector2(-16, 132), new Vector2(110, 96));
            MakeButton(foot, "RELOAD", bottomRight, new Vector2(-146, 108), new Vector2(96, 48), () => game.World.Gun.Reload());
            ammoText = Label(foot, bottomRight, new Vector2(-16, 236), new Vector2(200, 28), 18, TextAnchor.MiddleRight);
            ammoText.raycastTarget = false;
            MakeButton(foot, "PUNCH", bottomRight, new Vector2(-262, 16), new Vector2(96, 72), () => game.World.Melee.LightAttack());
            MakeButton(foot, "KICK", bottomRight, new Vector2(-262, 108), new Vector2(96, 56), () => game.World.Melee.HeavyAttack());
            block = Hold(foot, "BLOCK", bottomRight, new Vector2(-378, 16), new Vector2(96, 72));
            MakeButton(foot, "DODGE", bottomRight, new Vector2(-146, 176), new Vector2(96, 48), Dodge);
            arrest = Hold(foot, "ARREST", Middle, new Vector2(0, -40), new Vector2(160, 56));
            crosshair = Box(foot, "Crosshair", Middle, new Vector2(0, 40), new Vector2(8, 8), Color.white).gameObject;
            crosshair.GetComponent<Image>().raycastTarget = false;

            // After the control sets, so their drag area never covers these.
            speedText = Label(root, new Vector2(0, 1), new Vector2(16, -16), new Vector2(460, 32), 22, TextAnchor.MiddleLeft);
            speedText.horizontalOverflow = HorizontalWrapMode.Overflow; // a long status line runs on instead of losing its end
            HealthFill = Meter(root, "Health", new Vector2(16, -52), Gameplay.Shapes.Red);
            StaminaFill = Meter(root, "Stamina", new Vector2(16, -74), Gameplay.Shapes.Hex(0xFFD23F));
            CarFill = Meter(root, "Car", new Vector2(16, -52), Gameplay.Shapes.Cyan);
            BuildMiniMap(root);
            bustText = Label(root, Middle, new Vector2(0, 80), new Vector2(400, 40), 28, TextAnchor.MiddleCenter);
            bustText.color = Gameplay.Shapes.Red;
            bustText.raycastTarget = false;
            objectiveText = Label(root, new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(420, 28), 18, TextAnchor.MiddleCenter);
            objectiveText.color = Gameplay.Shapes.Cyan;
            objectiveText.raycastTarget = false;
            wantedText = Label(root, new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(420, 26), 18, TextAnchor.MiddleCenter);
            wantedText.color = Gameplay.Shapes.Magenta;
            wantedText.raycastTarget = false;
            MakeButton(root, "Menu", Vector2.one, new Vector2(-16, -16), new Vector2(96, 48), game.ToMenu);
            MakeButton(root, "Restart", Vector2.one, new Vector2(-120, -16), new Vector2(96, 48), () => game.World.Restart());
            doorText = MakeButton(root, "EXIT", bottomRight, new Vector2(-16, 270), new Vector2(110, 56), ToggleDoor);
            CollectPads(driveControls.transform, footControls.transform);
            var door = (RectTransform)doorText.transform.parent;
            pads.Add((door, door.anchoredPosition, door.anchorMin));
        }

        // Intro and side choice, then the result screen with Retry.
        void BuildMission(RectTransform safe)
        {
            missionIntro = Panel(safe, "Mission intro", Gameplay.Shapes.Dark);
            Title(missionIntro, "MIDNIGHT PRISON BREAK", 150);
            var intro = Label(missionIntro.transform, Middle, new Vector2(0, 75), new Vector2(620, 70), 18, TextAnchor.MiddleCenter);
            intro.text = Gameplay.Mission.Intro + "\nWhich side are you on?";
            MakeButton(missionIntro.transform, "Play as Police", Middle, new Vector2(-115, 0), Button,
                () => game.StartMission(Gameplay.Mission.Role.Police));
            MakeButton(missionIntro.transform, "Play as Prisoner", Middle, new Vector2(115, 0), Button,
                () => game.StartMission(Gameplay.Mission.Role.Prisoner));
            MakeButton(missionIntro.transform, "Free roam", Middle, new Vector2(-115, -64), Button, game.StartFreeRoam);
            MakeButton(missionIntro.transform, "Back", Middle, new Vector2(115, -64), Button, () => missionIntro.SetActive(false));

            missionResult = Panel(safe, "Mission result", new Color(0.04f, 0.055f, 0.1f, 0.85f));
            missionResultText = Label(missionResult.transform, Middle, new Vector2(0, 70), new Vector2(620, 160), 22, TextAnchor.MiddleCenter);
            MakeButton(missionResult.transform, "Retry", Middle, new Vector2(-115, -64), Button,
                () => game.StartMission(game.World.Mission.Side));
            MakeButton(missionResult.transform, "Menu", Middle, new Vector2(115, -64), Button, game.ToMenu);
        }

        void ShowMissionResult()
        {
            var m = game.World.Mission;
            missionResultText.text = (m.Won ? "MISSION COMPLETE" : "MISSION FAILED") + "\n" + Gameplay.Mission.Describe(m.Result) +
                $"\nTime {Mathf.FloorToInt(m.Elapsed / 60)}:{Mathf.FloorToInt(m.Elapsed % 60):00}" +
                (game.LastReward > 0 ? $"\nReward {game.LastReward} coins" : "");
        }

        void Dodge()
        {
            var w = game.World;
            var move = w.Walker.Move;
            w.Melee.Dodge(Quaternion.Euler(0, w.LookYaw, 0) * new Vector3(move.x, 0, move.y));
        }

        void Honk()
        {
            if (game.World.Honk()) game.Sound.Horn();
        }

        void ToggleDoor()
        {
            var world = game.World;
            if (world.OnFoot) world.EnterCar();
            else world.ExitCar();
        }

        void TickRoam()
        {
            var world = game.World;
            var car = world.Car;
            TickMeters(world);
            TickMiniMap(world);
            var set = game.Settings;
            if (set.leftHanded != layoutLeft || !Mathf.Approximately(set.buttonScale, layoutScale)) ApplyLayout(set.leftHanded, set.buttonScale);
            if (Gameplay.KeyInput.Interact()) ToggleDoor();
            bool onFoot = world.OnFoot;
            driveControls.SetActive(!onFoot);
            footControls.SetActive(onFoot);
            doorText.text = onFoot ? "ENTER" : "EXIT";
            doorText.transform.parent.gameObject.SetActive(onFoot ? world.CanEnter : world.CanExit);

            if (onFoot)
            {
                var walker = world.Walker;
                var keys = new Vector2(Gameplay.KeyInput.Steer(), Gameplay.KeyInput.Pedal());
                walker.Move = Vector2.ClampMagnitude(stick.Value + keys, 1);
                walker.Sprint = sprint.Held || Gameplay.KeyInput.Sprint();
                if (jump.Held || Gameplay.KeyInput.JumpPressed()) walker.Jump = true;
                world.LookYaw += look.Yaw;
                look.Yaw = 0;
                if (fire.Held || Gameplay.KeyInput.FireHeld()) world.Shoot();
                if (Gameplay.KeyInput.ReloadPressed()) world.Gun.Reload();
                world.Melee.SetBlock(block.Held || Gameplay.KeyInput.BlockHeld());
                arrest.gameObject.SetActive(world.CanArrest || world.ArrestProgress > 0);
                world.ArrestHeld = arrest.Held || Gameplay.KeyInput.ArrestHeld();
                if (Gameplay.KeyInput.PunchPressed()) world.Melee.LightAttack();
                if (Gameplay.KeyInput.KickPressed()) world.Melee.HeavyAttack();
                if (Gameplay.KeyInput.DodgePressed()) Dodge();
                ammoText.text = world.Gun.Reloading ? "Reloading..." : $"{world.Gun.Data.displayName}  {world.Gun.Ammo}/{world.Gun.Data.magazine}";
                speedText.text = $"Police {Mathf.RoundToInt(world.PoliceDistance)} m";
            }
            else
            {
                float steer = (roamRight.Held ? 1 : 0) - (roamLeft.Held ? 1 : 0) + Gameplay.KeyInput.Steer();
                float pedal = (gas.Held ? 1 : 0) - (brake.Held ? 1 : 0) + Gameplay.KeyInput.Pedal();
                car.Steer = Mathf.Clamp(steer, -1, 1);
                car.Pedal = Mathf.Clamp(pedal, -1, 1);
                car.Handbrake = handbrake.Held || Gameplay.KeyInput.Drift();
                if (Gameplay.KeyInput.HornPressed()) Honk();
                if (Gameplay.KeyInput.SirenPressed()) world.ToggleSiren();
                sirenButton.transform.parent.gameObject.SetActive(world.PlayerIsPolice);
                sirenButton.text = world.SirenOn ? "SIREN ON" : "SIREN";
                speedText.text = $"{Mathf.RoundToInt(car.SpeedKmh)} km/h   Police {Mathf.RoundToInt(world.PoliceDistance)} m"
                    + (car.Wrecked ? "   CAR WRECKED: GET OUT" : "") // the Car bar shows the condition
                    + (world.Punctured ? "   TYRES BLOWN" : "");
            }
            var m = world.Mission;
            int wanted = world.Wanted;
            wantedText.text = wanted == 0 ? "" : "WANTED " + new string('*', wanted) + new string('-', 5 - wanted)
                + (!world.Heli.Overhead ? "" : !world.Heli.CanSee ? "   HELICOPTER LOST YOU"
                    : world.Heli.InBeam(world.Player.position) ? "   IN THE SEARCHLIGHT" : "   HELICOPTER");
            objectiveText.text = m == null ? world.Objective
                : $"{world.Objective}   {Mathf.CeilToInt(Mathf.Max(0, Gameplay.Mission.TimeLimit - m.Elapsed))} s";
            if (m != null && m.Elapsed < m.MessageUntil) { bustText.text = m.Message; return; }
            bustText.text = world.ArrestProgress > 0 ? $"HANDCUFFING {Mathf.RoundToInt(world.ArrestProgress * 100)}%"
                : world.Dog.Barking && world.Dog.HoldProgress == 0 ? "THE DOG IS BARKING"
                : world.Dog.HoldProgress > 0 ? $"THE POLICE DOG HAS YOU {Mathf.RoundToInt(world.Dog.HoldProgress * 100)}%"
                : world.BustProgress > 0 ? $"PULLING YOU OVER {Mathf.RoundToInt(world.BustProgress * 100)}%"
                : world.Deliveries > 0 ? $"Suspects delivered {world.Deliveries}"
                : world.Busts > 0 ? $"Busted {world.Busts}x" : "";
        }

        void UpdateTutorial()
        {
            int step = game.TutorialStep;
            skip.SetActive(step > 0);
            tutorialText.text = step == 1 ? (game.Settings.tiltControls ? "Tilt your phone to steer" : "Hold < or > to steer")
                : step == 2 ? "Hold DRIFT to drift and brake"
                : step == 3 ? "Dodge the police car!" : "";
        }

        void BuildScreens(RectTransform safe)
        {
            var dim = new Color(0.04f, 0.055f, 0.1f, 0.85f);

            menu = Panel(safe, "Menu", dim);
            Title(menu, "POLICE PURSUIT", 150);
            menuText = Label(menu.transform, Middle, new Vector2(0, 90), new Vector2(500, 28), 18, TextAnchor.MiddleCenter);
            MakeButton(menu.transform, "Endless Run", Middle, new Vector2(-115, 30), Button, game.StartRun);
            MakeButton(menu.transform, "Prison Break", Middle, new Vector2(115, 30), Button, () => missionIntro.SetActive(true));
            MakeButton(menu.transform, "Garage", Middle, new Vector2(-115, -34), Button, OpenGarage);
            MakeButton(menu.transform, "Leaderboard", Middle, new Vector2(115, -34), Button, OpenBoard);
            if (Shop.Configured)
            {
                MakeButton(menu.transform, "Settings", Middle, new Vector2(-115, -98), Button, OpenSettings);
                MakeButton(menu.transform, "Shop", Middle, new Vector2(115, -98), Button, OpenShop);
            }
            else Choice(menu, "Settings", -98, OpenSettings);
            signInText = Choice(menu, "Sign in with Google", -162, () =>
            {
                if (game.Auth.SignedIn) game.Auth.SignOut();
                else game.Auth.SignIn();
            });
            signInText.transform.parent.gameObject.SetActive(game.Auth.Available);
            signInProblem = Label(menu.transform, Middle, new Vector2(0, -207), new Vector2(780, 32), 12,
                TextAnchor.MiddleCenter);
            signInProblem.supportRichText = false;

            pause = Panel(safe, "Pause", dim);
            Title(pause, "PAUSED", 160);
            Choice(pause, "Resume", 80, game.Resume);
            Choice(pause, "Restart", 14, game.Restart);
            Choice(pause, "Settings", -52, OpenSettings);
            Choice(pause, "Quit", -118, game.ToMenu);

            busted = Panel(safe, "Busted", new Color(0, 0, 0, 0.4f));
            Title(busted, "BUSTED", 0).color = Gameplay.Shapes.Red;
            continueText = Choice(busted, "", -90, game.Continue);
            continueButton = continueText.transform.parent.gameObject;

            results = Panel(safe, "Results", dim);
            Title(results, "RESULTS", 175);
            resultText = Label(results.transform, Middle, new Vector2(0, 60), new Vector2(400, 150), 20, TextAnchor.MiddleCenter);
            Choice(results, "Play again", -60, game.StartRun);
            Choice(results, "Menu", -126, game.ToMenu);
            doubleButton = Choice(results, "Double coins (ad)", -192, game.DoubleCoins).transform.parent.gameObject;

            settings = Panel(safe, "Settings", Gameplay.Shapes.Dark);
            Title(settings, "SETTINGS", 185);
            sound = SliderRow(settings, "Sound", 125, 0, 1, v => Change(s => s.soundVolume = v));
            music = SliderRow(settings, "Music", 45, 0, 1, v => Change(s => s.musicVolume = v));
            tilt = SliderRow(settings, "Tilt sensitivity", -35, 0.5f, 2, v => Change(s => s.tiltSensitivity = v));
            // Right column, 64 apart (8 between buttons): one more row than before, so nothing is pressed by accident.
            Label(settings.transform, Middle, new Vector2(190, 168), new Vector2(220, 24), 18, TextAnchor.MiddleCenter).text = "Language: English";
            controlsText = Option("", 128, () => { Change(s => s.tiltControls = !s.tiltControls); UpdateSettingsText(); });
            hapticsText = Option("", 64, () => { Change(s => s.haptics = !s.haptics); UpdateSettingsText(); });
            presetText = Option("", 0, () => { game.CyclePreset(); UpdateSettingsText(); });
            Option("Controls layout", -64, OpenLayout);
            Option("Replay tutorial", -128, game.StartTutorial);
            Choice(settings, "Back", -188, CloseSettings);

            layout = Panel(safe, "Layout", Gameplay.Shapes.Dark);
            Title(layout, "CONTROLS LAYOUT", 185);
            Label(layout.transform, Middle, new Vector2(0, 130), new Vector2(600, 24), 18, TextAnchor.MiddleCenter).text =
                "For the open world: on foot and in the car";
            sizeSlider = SliderRow(layout, "Button size", 60, MinButtonScale, MaxButtonScale, v => Change(s => s.buttonScale = v));
            handText = MakeButton(layout.transform, "", Middle, new Vector2(190, 40), Button, () =>
            {
                Change(s => s.leftHanded = !s.leftHanded);
                UpdateLayoutText();
            });
            Choice(layout, "Back", -188, () => { layout.SetActive(false); settings.SetActive(true); });
            if (Ads.Configured)
                MakeButton(settings.transform, "Privacy & ads", Middle, new Vector2(-190, -118), Button, ShowConsent);
            var delete = MakeButton(settings.transform, "Delete my account", Middle, new Vector2(-230, -180), Button,
                AskDelete).transform.parent.gameObject;
            delete.GetComponent<Image>().color = Gameplay.Shapes.Red;
            deleteButton = delete;
            // Google Play wants the policy reachable from inside the app.
            MakeButton(settings.transform, "Privacy policy", Middle, new Vector2(230, -180), Button,
                () => Application.OpenURL("https://organexa.github.io/police-pursuit/privacy.html"));

            BuildGarage(safe);
            BuildBoard(safe);
            BuildShop(safe);
            BuildDelete(safe);
            BuildConsent(safe);
            BuildUpdate(safe);
        }

        void BuildShop(RectTransform safe)
        {
            shop = Panel(safe, "Shop", Gameplay.Shapes.Dark);
            Title(shop, "SHOP", 185);
            var items = new List<(string id, string name)> { (Shop.RemoveAds, "Remove ads") };
            foreach (var pack in Shop.Packs) items.Add((pack.id, pack.name));
            foreach (var car in CarList.All)
                if (car.Premium) items.Add((Shop.CarProduct(car), car.Name));
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var label = MakeButton(shop.transform, item.name, Middle, new Vector2(0, 125 - i * 60),
                    new Vector2(420, 52), () => Buy(item.id));
                shopRows.Add((item.id, item.name, label));
            }
            Choice(shop, "Back", -185, () => Show(GameState.Menu)); // fits up to five products
            shop.SetActive(false);
        }

        void OpenShop()
        {
            menu.SetActive(false);
            shop.SetActive(true);
            ShowShop();
        }

        void ShowShop()
        {
            var save = SaveData.Load(SaveData.DefaultPath);
            foreach (var row in shopRows)
                row.label.text = row.name + "   " + (Owned(save, row.id) ? "Owned" : game.Shop.Price(row.id) ?? "...");
        }

        static bool Owned(SaveData save, string id)
        {
            if (id == Shop.RemoveAds) return save.adsRemoved;
            foreach (var car in CarList.All)
                if (car.Premium && Shop.CarProduct(car) == id) return save.Owns(car);
            return false;
        }

        void Buy(string id)
        {
            if (!Owned(SaveData.Load(SaveData.DefaultPath), id)) game.Shop.Buy(id);
        }

        void BuildDelete(RectTransform safe)
        {
            deleteScreen = Panel(safe, "Delete account", Gameplay.Shapes.Dark);
            Title(deleteScreen, "DELETE ACCOUNT", 120);
            deleteText = Label(deleteScreen.transform, Middle, new Vector2(0, 20), new Vector2(560, 110), 20,
                TextAnchor.MiddleCenter);
            var confirm = Choice(deleteScreen, "Delete", -80, ConfirmDelete).transform.parent.gameObject;
            confirm.GetComponent<Image>().color = Gameplay.Shapes.Red;
            deleteConfirm = confirm;
            Choice(deleteScreen, "Back", -150, () =>
            {
                deleteScreen.SetActive(false);
                OpenSettingsAgain();
            });
            deleteScreen.SetActive(false);
        }

        void AskDelete()
        {
            settings.SetActive(false);
            deleteText.text = "This removes your account, leaderboard scores and cloud save for good. " +
                "Coins and cars on this phone stay. You cannot undo this.";
            deleteConfirm.SetActive(true);
            deleteScreen.SetActive(true);
        }

        void ConfirmDelete()
        {
            deleteConfirm.SetActive(false); // one request at a time
            deleteText.text = "Deleting...";
            game.Auth.DeleteAccount(ok => deleteText.text = ok
                ? "Done, your online data is removed."
                : "Could not delete. Check your connection and try again.");
        }

        void OpenSettingsAgain()
        {
            deleteButton.SetActive(game.Auth.SignedIn);
            settings.SetActive(true);
        }

        // Both answers look the same, so neither is pushed on the player.
        void BuildConsent(RectTransform safe)
        {
            consent = Panel(safe, "Consent", Gameplay.Shapes.Dark);
            Title(consent, "PRIVACY & ADS", 120);
            Label(consent.transform, Middle, new Vector2(0, 20), new Vector2(560, 110), 20, TextAnchor.MiddleCenter).text =
                "This free game shows ads. May our ad partners use data from your device " +
                "to show ads that fit you better?\nYou can change this in Settings.";
            MakeButton(consent.transform, "Allow", Middle, new Vector2(-125, -90), Button, () => game.SetAdConsent(true));
            MakeButton(consent.transform, "No thanks", Middle, new Vector2(125, -90), Button, () => game.SetAdConsent(false));
            consent.SetActive(false);
        }

        public void ShowConsent() => consent.SetActive(true);
        public void HideConsent() => consent.SetActive(false);

        // Built last so the forced screen draws over every other one.
        void BuildUpdate(RectTransform safe)
        {
            updateBanner = MakeButton(menu.transform, "Update available", Vector2.one, new Vector2(-8, -8),
                new Vector2(200, 48), OpenStore).transform.parent.gameObject;
            updateBanner.GetComponent<Image>().color = Gameplay.Shapes.Magenta;
            updateBanner.SetActive(false);

            updateScreen = Panel(safe, "Update", Gameplay.Shapes.Dark);
            Title(updateScreen, "PLEASE UPDATE", 110);
            updateText = Label(updateScreen.transform, Middle, new Vector2(0, 20), new Vector2(520, 90), 20,
                TextAnchor.MiddleCenter);
            updateText.supportRichText = false;
            Choice(updateScreen, "Play Store", -90, OpenStore);
            updateScreen.SetActive(false);
        }

        static void OpenStore() => Application.OpenURL(UpdateCheck.StoreUrl);

        void BuildBoard(RectTransform safe)
        {
            board = Panel(safe, "Leaderboard", Gameplay.Shapes.Dark);
            Title(board, "LEADERBOARD", 185);

            var view = Box(board.transform, "View", Middle, new Vector2(0, 20), new Vector2(520, 260), Gameplay.Shapes.Navy);
            view.gameObject.AddComponent<RectMask2D>();
            boardText = Label(view.transform, new Vector2(0.5f, 1), Vector2.zero, new Vector2(500, 260), 18,
                TextAnchor.UpperCenter);
            boardText.supportRichText = false; // names are typed by players
            boardText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            boardScroll = view.gameObject.AddComponent<ScrollRect>();
            boardScroll.content = boardText.rectTransform;
            boardScroll.horizontal = false;
            boardScroll.scrollSensitivity = 20;

            rankText = Label(board.transform, Middle, new Vector2(0, -132), new Vector2(520, 28), 20, TextAnchor.MiddleCenter);
            rankText.color = Gameplay.Shapes.Cyan;
            Choice(board, "Back", -185, () => Show(GameState.Menu));
        }

        void OpenBoard()
        {
            menu.SetActive(false);
            board.SetActive(true);
            boardText.text = "Loading...";
            boardScroll.verticalNormalizedPosition = 1;
            Leaderboard.FetchTop(text => boardText.text = text);
            if (!game.Auth.SignedIn) { rankText.text = "Sign in to join"; return; }
            rankText.text = "";
            StartCoroutine(ShowRank());
        }

        // Auth renews an expired sign-in by itself; asking for the rank before that is refused (401).
        System.Collections.IEnumerator ShowRank()
        {
            float giveUp = Time.unscaledTime + 10;
            while (game.Auth.SignedIn && Time.unscaledTime < giveUp
                && game.Auth.Session.NeedsRefresh(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
                yield return null;
            if (game.Auth.SignedIn) Leaderboard.FetchRank(game.Auth.Session, text => rankText.text = text);
        }

        void BuildGarage(RectTransform safe)
        {
            garage = Panel(safe, "Garage", Gameplay.Shapes.Dark);
            var root = garage.transform;
            Title(garage, "GARAGE", 175);
            carCoins = Label(root, Vector2.one, new Vector2(-16, -12), new Vector2(200, 32), 18, TextAnchor.MiddleRight);
            carText = Label(root, Middle, new Vector2(0, 115), new Vector2(360, 36), 24, TextAnchor.MiddleCenter);
            // shortcut: arrows instead of swipe and no 3D preview, add both with the art pass.
            MakeButton(root, "<", Middle, new Vector2(-230, 115), new Vector2(56, 56), () => ShowCar(carIndex - 1));
            MakeButton(root, ">", Middle, new Vector2(230, 115), new Vector2(56, 56), () => ShowCar(carIndex + 1));

            var names = new[] { "Speed", "Handling", "Armor" };
            for (int i = 0; i < names.Length; i++)
            {
                float y = 55 - i * 34;
                Label(root, Middle, new Vector2(-130, y), new Vector2(100, 24), 18, TextAnchor.MiddleLeft).text = names[i];
                var bar = Box(root, names[i], Middle, new Vector2(40, y), new Vector2(220, 14), Gameplay.Shapes.Navy);
                statFills[i] = Rect("Fill", bar.transform);
                Stretch(statFills[i]);
                statFills[i].gameObject.AddComponent<Image>().color = Gameplay.Shapes.Cyan;
            }

            carButton = Choice(garage, "", -70, UseCar);
            Choice(garage, "Back", -136, () => Show(GameState.Menu));
        }

        void OpenGarage()
        {
            menu.SetActive(false);
            garage.SetActive(true);
            var selected = CarList.Get(SaveData.Load(SaveData.DefaultPath).selectedCar);
            ShowCar(System.Array.IndexOf(CarList.All, selected));
        }

        void ShowCar(int index)
        {
            carIndex = (index + CarList.All.Length) % CarList.All.Length;
            var car = CarList.All[carIndex];
            var save = SaveData.Load(SaveData.DefaultPath);
            carText.text = car.Name;
            carText.color = Gameplay.Shapes.Hex(car.Color);
            carCoins.text = $"Coins {save.coins}";
            // Bars are relative to a little above the best car.
            statFills[0].anchorMax = new Vector2(car.TopSpeed / 42f, 1);
            statFills[1].anchorMax = new Vector2(car.Handling / 10f, 1);
            statFills[2].anchorMax = new Vector2(0.1f + car.Armor * 2f, 1);
            carButton.text = save.selectedCar == car.Id ? "Selected"
                : save.Owns(car) ? "Select"
                : car.Premium ? (game.Shop.Price(Shop.CarProduct(car)) is string price
                    ? "Buy: " + price : "Premium: not available")
                : $"Unlock: {car.Price} coins";
        }

        // Unlocks the shown car if needed, then selects it.
        void UseCar()
        {
            var car = CarList.All[carIndex];
            var save = SaveData.Load(SaveData.DefaultPath);
            if (car.Premium && !save.Owns(car))
            {
                game.Shop.Buy(Shop.CarProduct(car));
                return;
            }
            bool bought = !save.Owns(car);
            if (bought && !save.TryUnlock(car)) return;
            save.Select(car);
            save.Save(SaveData.DefaultPath);
            if (bought) game.Cloud.MarkDirty();
            game.ApplySelectedCar();
            ShowCar(carIndex);
        }

        void OpenSettings()
        {
            settingsFrom = game.State;
            sound.SetValueWithoutNotify(game.Settings.soundVolume);
            music.SetValueWithoutNotify(game.Settings.musicVolume);
            tilt.SetValueWithoutNotify(game.Settings.tiltSensitivity);
            UpdateSettingsText();
            menu.SetActive(false);
            pause.SetActive(false);
            deleteButton.SetActive(game.Auth.SignedIn);
            settings.SetActive(true);
        }

        void CloseSettings() => Show(settingsFrom);

        void OpenLayout()
        {
            sizeSlider.SetValueWithoutNotify(game.Settings.buttonScale);
            UpdateLayoutText();
            settings.SetActive(false);
            layout.SetActive(true);
        }

        void UpdateLayoutText() => handText.text = "Hand: " + (game.Settings.leftHanded ? "Left" : "Right");

        // Remembers every bottom-anchored open-world control (buttons, stick, ammo count) as built, for ApplyLayout.
        void CollectPads(params Transform[] parents)
        {
            foreach (var parent in parents)
                foreach (RectTransform r in parent)
                    if (r.anchorMin.y == 0 && r.anchorMin == r.anchorMax) pads.Add((r, r.anchoredPosition, r.anchorMin));
        }

        // Mirrors the controls left to right and scales each from its screen corner: gaps grow with the buttons,
        // so they never close up (the on-foot spacing test's 20 units become 20 x scale).
        public void ApplyLayout(bool leftHanded, float scale)
        {
            scale = Mathf.Clamp(scale, MinButtonScale, MaxButtonScale);
            foreach (var (rect, pos, anchor) in pads)
            {
                var a = leftHanded ? new Vector2(1 - anchor.x, anchor.y) : anchor;
                rect.anchorMin = rect.anchorMax = rect.pivot = a;
                rect.anchoredPosition = new Vector2(leftHanded ? -pos.x : pos.x, pos.y) * scale;
                rect.localScale = Vector3.one * scale;
            }
            layoutLeft = leftHanded;
            layoutScale = scale;
        }

        void UpdateSettingsText()
        {
            controlsText.text = "Controls: " + (game.Settings.tiltControls ? "Tilt" : "Buttons");
            hapticsText.text = "Haptics: " + (game.Settings.haptics ? "On" : "Off");
            presetText.text = "Graphics: " + game.Preset;
        }

        // shortcut: writes the file on every slider drag step; save on release if it stutters.
        void Change(System.Action<SaveData> edit)
        {
            var s = SaveData.Load(SaveData.DefaultPath);
            edit(s);
            s.Save(SaveData.DefaultPath);
            game.ApplySettings(s);
        }

        Text Option(string label, float y, UnityEngine.Events.UnityAction onClick) =>
            MakeButton(settings.transform, label, Middle, new Vector2(190, y), Button, onClick);

        // Label with a slider under it, left column. The 48-high root is the touch target.
        Slider SliderRow(GameObject panel, string label, float y, float min, float max, UnityEngine.Events.UnityAction<float> onChange)
        {
            Label(panel.transform, Middle, new Vector2(-190, y), new Vector2(320, 24), 18, TextAnchor.MiddleCenter).text = label;

            var root = Box(panel.transform, label + " slider", Middle, new Vector2(-190, y - 35), new Vector2(320, 48), Color.clear);
            var track = Box(root.transform, "Track", Middle, Vector2.zero, new Vector2(320, 12), Gameplay.Shapes.Navy);

            var fillArea = Rect("Fill area", track.transform);
            Stretch(fillArea);
            var fill = Rect("Fill", fillArea);
            fill.gameObject.AddComponent<Image>().color = Gameplay.Shapes.Cyan;
            fill.sizeDelta = Vector2.zero;

            var handleArea = Rect("Handle area", root.transform);
            Stretch(handleArea);
            var handle = Rect("Handle", handleArea);
            var knob = handle.gameObject.AddComponent<Image>();
            knob.color = Color.white;
            handle.sizeDelta = new Vector2(32, 0);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = knob;
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // A labelled bar; returns the fill, whose width is set as a 0..1 share.
        RectTransform Meter(Transform root, string label, Vector2 pos, Color colour)
        {
            var bar = Box(root, label + "Bar", new Vector2(0, 1), pos, new Vector2(150, 18), Gameplay.Shapes.Navy);
            bar.raycastTarget = false;
            var fill = Rect("Fill", bar.transform);
            Stretch(fill);
            var image = fill.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            var text = Label(bar.transform, Middle, Vector2.zero, new Vector2(150, 18), 14, TextAnchor.MiddleCenter);
            text.text = label;
            var edge = text.gameObject.AddComponent<Outline>(); // white with a dark edge: readable on the fill and on the empty bar
            edge.effectColor = new Color(0, 0, 0, 0.85f);
            edge.effectDistance = new Vector2(1, -1);
            return fill;
        }

        static void SetMeter(RectTransform fill, float share) =>
            fill.anchorMax = new Vector2(Mathf.Clamp01(share), 1);

        void TickMeters(Gameplay.OpenWorld world)
        {
            bool onFoot = world.OnFoot;
            HealthFill.parent.gameObject.SetActive(onFoot);
            StaminaFill.parent.gameObject.SetActive(onFoot);
            CarFill.parent.gameObject.SetActive(!onFoot);
            SetMeter(HealthFill, world.PlayerHealth.Current / world.PlayerHealth.Max);
            SetMeter(StaminaFill, world.Melee.Stamina / Gameplay.MeleeController.MaxStamina);
            SetMeter(CarFill, world.Car.Condition / Gameplay.VehicleController.MaxCondition);
        }

        // North-up map of the district from plain UI boxes (no second camera, so it costs almost nothing):
        // roads, the park, pond and canal, the police station, and dots for you, the chasing police and the escapee.
        void BuildMiniMap(Transform root)
        {
            var map = Box(root, "MiniMap", new Vector2(0, 1), new Vector2(16, -100), new Vector2(MapSize, MapSize), new Color(0, 0, 0, 0.55f));
            map.raycastTarget = false;
            var m = map.transform;
            float s = MapScale, span = 2 * Gameplay.OpenWorld.Edge, blocks = Gameplay.OpenWorld.Blocks, pitch = Gameplay.OpenWorld.Pitch;
            Image Mark(string name, Vector3 at, Vector2 size, Color colour)
            {
                var image = Box(m, name, Middle, new Vector2(at.x, at.z) * s, size * s, colour);
                image.raycastTarget = false;
                return image;
            }
            var road = new Color(0.45f, 0.5f, 0.6f);
            for (int i = 0; i <= blocks; i++)
            {
                float at = (i - blocks / 2) * pitch;
                Mark("Road", new Vector3(at, 0, 0), new Vector2(Gameplay.OpenWorld.Road, span), road);
                Mark("Road", new Vector3(0, 0, at), new Vector2(span, Gameplay.OpenWorld.Road), road);
            }
            var park = Gameplay.OpenWorld.ParkCentre;
            float green = pitch - Gameplay.OpenWorld.Road;
            Mark("Park", park, new Vector2(green, green), Gameplay.Shapes.Hex(0x1F6B2E));
            Mark("Pond", park, Vector2.one * Gameplay.OpenWorld.PondHalf * 2, Gameplay.Shapes.Hex(0x2D7FD1));
            Mark("Canal", new Vector3(0, 0, Gameplay.OpenWorld.CanalZ), new Vector2(blocks * pitch, Gameplay.OpenWorld.CanalHalf * 2), Gameplay.Shapes.Hex(0x2D7FD1));
            Mark("Station", Gameplay.OpenWorld.Station, new Vector2(12, 12), Color.blue);
            MapFleer = Box(m, "Escapee", Middle, Vector2.zero, new Vector2(8, 8), Gameplay.Shapes.Hex(0xFF8A00)).rectTransform;
            MapPolice = Box(m, "Police", Middle, Vector2.zero, new Vector2(8, 8), Gameplay.Shapes.Red).rectTransform;
            MapPlayer = Box(m, "You", Middle, Vector2.zero, new Vector2(9, 9), Gameplay.Shapes.Cyan).rectTransform;
            foreach (var r in new[] { MapFleer, MapPolice, MapPlayer }) r.GetComponent<Image>().raycastTarget = false;
        }

        public static Vector2 MapPoint(Gameplay.OpenWorld world, Vector3 at)
        {
            var local = world.transform.InverseTransformPoint(at);
            return new Vector2(local.x, local.z) * MapScale;
        }

        void TickMiniMap(Gameplay.OpenWorld world)
        {
            var player = world.Player;
            MapPlayer.anchoredPosition = MapPoint(world, player.position);
            MapPlayer.localRotation = Quaternion.Euler(0, 0, 45 - player.eulerAngles.y); // a diamond turned to your heading
            bool police = world.Police.gameObject.activeSelf, fleer = world.Fleer.gameObject.activeSelf;
            MapPolice.gameObject.SetActive(police);
            MapFleer.gameObject.SetActive(fleer);
            if (police) MapPolice.anchoredPosition = MapPoint(world, world.Police.transform.position);
            if (fleer) MapFleer.anchoredPosition = MapPoint(world, world.Fleer.transform.position);
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static GameObject Panel(Transform parent, string name, Color color)
        {
            var rect = Rect(name, parent);
            Stretch(rect);
            if (color.a > 0) rect.gameObject.AddComponent<Image>().color = color;
            return rect.gameObject;
        }

        static Image Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var rect = Rect(name, parent);
            Place(rect, anchor, pos, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        Text Label(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
        {
            var rect = Rect("Text", parent);
            Place(rect, anchor, pos, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        Text Title(GameObject panel, string value, float y)
        {
            var text = Label(panel.transform, Middle, new Vector2(0, y), new Vector2(600, 60), 40, TextAnchor.MiddleCenter);
            text.color = Gameplay.Shapes.Cyan;
            text.text = value;
            return text;
        }

        Text MakeButton(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            var image = Box(parent, "Button " + label, anchor, pos, size, Gameplay.Shapes.Navy);
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            // Navy buttons vanish into the navy road without an edge.
            var edge = image.gameObject.AddComponent<Outline>();
            edge.effectColor = Gameplay.Shapes.Cyan;
            edge.effectDistance = new Vector2(2, 2);
            if (onClick != null) // the hold-to-drive buttons have no click sound
                button.onClick.AddListener(() =>
                {
                    game.Sound.Click();
                    onClick();
                });
            var text = Label(image.transform, Middle, Vector2.zero, size, 20, TextAnchor.MiddleCenter);
            text.text = label;
            return text;
        }

        Text Choice(GameObject panel, string label, float y, UnityEngine.Events.UnityAction onClick) =>
            MakeButton(panel.transform, label, Middle, new Vector2(0, y), Button, onClick);

        HoldButton Hold(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size) =>
            MakeButton(parent, label, anchor, pos, size, null).transform.parent.gameObject.AddComponent<HoldButton>();
    }
}
