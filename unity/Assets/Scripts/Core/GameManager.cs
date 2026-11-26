using System;
using System.Collections;
using PolicePursuit.Gameplay;
using PolicePursuit.UI;
using UnityEngine;

namespace PolicePursuit.Core
{
    public enum GameState { Menu, Run, Pause, Busted, Results }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Menu;
        public RunStats Stats { get; } = new RunStats();
        public PlayerCar Player { get; private set; }
        public Traffic Traffic { get; private set; }
        public CarCamera Cam { get; private set; }
        public GfxPreset Preset { get; private set; }
        public int Wanted { get; private set; } = 1;
        public int TutorialStep { get; private set; } // 0 = normal run, 1 steer, 2 drift, 3 dodge
        public SaveData Settings { get; private set; } // read-only snapshot; never save from it
        public Auth Auth { get; private set; }
        public CloudSave Cloud { get; private set; }
        public Shop Shop { get; private set; }
        public IAds Ads { get; set; } // tests swap in a fake
        public float ContinueLeft { get; private set; } // seconds the "Continue" offer stays up
        public bool CanDouble => State == GameState.Results && !doubled && Stats.Coins > 0 && Ads.RewardedReady;

        Ads realAds;
        bool continued, doubled;

        RoadTiles road;
        Coins coins;
        float stepTime;
        GameUI ui;

        // shortcut: no scene yet, the world is built in code; a build still needs one (empty) scene in Build Settings.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance == null) new GameObject("Game").AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            Preset = GraphicsPresets.Load();
            GraphicsPresets.Apply(Preset);

            if (FindAnyObjectByType<Light>() == null)
            {
                var sun = Child<Light>("Sun");
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            Player = Child<PlayerCar>("Player");
            road = Child<RoadTiles>("Road");
            SaveData.MigratePrefs(SaveData.DefaultPath);
            Traffic = Child<Traffic>("Traffic");
            coins = Child<Coins>("Coins");
            Traffic.Init(Player);
            coins.Init(Player);
            Cam = CarCamera.Create(Player.transform);
            ui = Child<GameUI>("UI");
            ApplySettings(SaveData.Load(SaveData.DefaultPath));
            Auth = Child<Auth>("Auth");
            Cloud = Child<CloudSave>("CloudSave");
            Cloud.Init(Auth);
            Ads = realAds = Child<Ads>("Ads");
            Shop = Child<Shop>("Shop");
            ui.Init(this);
            Shop.Changed += () =>
            {
                Cloud.MarkDirty();
                ui.RefreshSaved();
            };
            Shop.Init();
            Child<Push>("Push").Init(Cloud);
            Cloud.Merged += ui.RefreshSaved;

            Traffic.NearMiss += OnNearMiss;
            coins.Collected += OnCoin;
            Player.Destroyed += OnDestroyedCar;
            Player.Damaged += Buzz;

            ToMenu();
            var saved = SaveData.Load(SaveData.DefaultPath);
            // The consent choice comes before the tutorial and before any ad loads.
            if (Core.Ads.Configured && saved.adConsent == 0) ui.ShowConsent();
            else
            {
                realAds.SetConsent(saved.adConsent == 1);
                if (!saved.tutorialDone) StartTutorial();
            }
            UpdateCheck.Fetch(OnUpdate);
        }

        public void OnUpdate(UpdateLevel level, string message)
        {
            if (this == null) return; // the answer can arrive after the game object is gone
            if (level == UpdateLevel.Critical) ToMenu(); // stop a run or tutorial under the blocking screen
            ui.ShowUpdate(level, message);
        }

        T Child<T>(string childName) where T : Component
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            // A Light must be added, every other T is a script; AddComponent covers both.
            return go.AddComponent<T>();
        }

        void Update()
        {
            if (KeyInput.BackPressed()) TogglePause();
            if (State != GameState.Run) return;

            float dt = Time.deltaTime;
            float moved = Player.Tick(dt, KeyInput.Steer(), KeyInput.Drift());
            if (TutorialStep > 0) { TickTutorial(dt); return; }
            Stats.DistanceM += moved;
            // Capped so a very long run still passes the server's duration check.
            Stats.DurationS = System.Math.Min(Stats.DurationS + dt, 3600);
            Wanted = WantedLevel.ForDuration(Stats.DurationS);
            Stats.MaxWanted = Mathf.Max(Stats.MaxWanted, Wanted);

            road.Recycle(Player.transform.position.z);
            Traffic.Tick(dt, Wanted, GraphicsPresets.MaxCars(Preset));
            coins.Tick(dt);
        }

        // Steer, then drift, then dodge. No score, coins or wanted level.
        void TickTutorial(float dt)
        {
            road.Recycle(Player.transform.position.z);
            if (TutorialStep == 1)
            {
                if (Mathf.Abs(Player.transform.position.x) > 1.5f) TutorialStep = 2;
            }
            else if (TutorialStep == 2)
            {
                if (KeyInput.Drift() || Player.ButtonDrift) stepTime += dt;
                if (stepTime >= 0.5f) { TutorialStep = 3; stepTime = 0; }
            }
            else
            {
                Traffic.Tick(dt, 1, 2);
                stepTime += dt;
                // A hit repairs the car and restarts the timer, so the tutorial cannot be lost.
                if (Player.Health < PlayerCar.MaxHealth) { Player.Repair(); stepTime = 0; }
                if (stepTime >= 12) FinishTutorial();
            }
        }

        public void SetAdConsent(bool allowed)
        {
            var save = SaveData.Load(SaveData.DefaultPath);
            bool first = save.adConsent == 0;
            save.adConsent = allowed ? 1 : -1;
            save.Save(SaveData.DefaultPath);
            realAds.SetConsent(allowed);
            ui.HideConsent();
            if (first && !save.tutorialDone && State == GameState.Menu) StartTutorial();
        }

        public void FinishTutorial()
        {
            var save = SaveData.Load(SaveData.DefaultPath);
            save.tutorialDone = true;
            save.Save(SaveData.DefaultPath);
            ToMenu();
        }

        public void StartRun() => Begin(0);
        public void StartTutorial() => Begin(1);
        public void Restart() => Begin(TutorialStep > 0 ? 1 : 0);

        void Begin(int tutorialStep)
        {
            StopAllCoroutines();
            SetFrozen(false);
            Stats.Reset();
            Wanted = 1;
            continued = false;
            ContinueLeft = 0;
            ApplySelectedCar();
            road.ResetRoad();
            Traffic.ResetTraffic();
            coins.ResetCoins();
            Cam.Snap();
            TutorialStep = tutorialStep;
            stepTime = 0;
            SetState(GameState.Run);
        }

        public void ToMenu()
        {
            // Quitting a run from pause keeps no score and no coins.
            StopAllCoroutines();
            SetFrozen(false);
            ApplySelectedCar();
            road.ResetRoad();
            Traffic.ResetTraffic();
            coins.ResetCoins();
            Cam.Snap();
            TutorialStep = 0;
            ContinueLeft = 0;
            SetState(GameState.Menu);
        }

        public void Pause()
        {
            if (State != GameState.Run) return;
            SetFrozen(true);
            SetState(GameState.Pause);
        }

        public void Resume()
        {
            if (State != GameState.Pause) return;
            SetFrozen(false);
            SetState(GameState.Run);
        }

        public void TogglePause()
        {
            if (State == GameState.Run) Pause();
            else if (State == GameState.Pause) Resume();
        }

        public void CyclePreset()
        {
            Preset = (GfxPreset)(((int)Preset + 1) % 3);
            GraphicsPresets.Save(Preset);
            GraphicsPresets.Apply(Preset);
            PlayerPrefs.Save();
        }

        public static long BestScore => SaveData.Load(SaveData.DefaultPath).bestScore;

        public static int TotalCoins => SaveData.Load(SaveData.DefaultPath).coins;

        // Also repaints and resets the car, so the garage choice shows at once.
        public void ApplySelectedCar() =>
            Player.Apply(CarList.Get(SaveData.Load(SaveData.DefaultPath).selectedCar));

        void OnNearMiss()
        {
            if (State != GameState.Run) return;
            Stats.NearMisses++;
            ui.ShowNearMiss();
            Buzz();
        }

        // shortcut: the game has no audio yet, so sound drives the master volume and music is only saved;
        // split into mixer groups when audio is added.
        public void ApplySettings(SaveData s)
        {
            Settings = s;
            AudioListener.volume = s.soundVolume;
        }

        void Buzz()
        {
            if (!Settings.haptics) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate(); // shortcut: fixed-length buzz; use VibrationEffect if it feels too long
#endif
        }

        void OnCoin()
        {
            if (State == GameState.Run) Stats.Coins++;
        }

        void QueueScore(string carId)
        {
            var queue = ScoreQueue.Load(ScoreQueue.DefaultPath);
            queue.Add(new QueuedRun
            {
                duration_s = (int)Math.Ceiling(Stats.DurationS), // up, so the server's distance check can't fail on rounding
                distance_m = (int)Stats.DistanceM,
                near_misses = Stats.NearMisses,
                max_wanted = Stats.MaxWanted,
                car_id = CarList.Get(carId).Id,
                played_at = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            });
            queue.Save(ScoreQueue.DefaultPath);
        }

        void OnDestroyedCar()
        {
            if (State != GameState.Run) return;
            // A run that can still be continued is only banked once it really ends.
            bool offer = TutorialStep == 0 && !continued && Ads.RewardedReady;
            if (!offer) Bank();
            SetState(GameState.Busted);
            StartCoroutine(offer ? OfferContinue() : ShowResults());
        }

        void Bank()
        {
            var save = SaveData.Load(SaveData.DefaultPath);
            save.coins += Stats.Coins;
            if (TutorialStep == 0) save.bestScore = Math.Max(save.bestScore, Stats.Score);
            save.Save(SaveData.DefaultPath);
            if (TutorialStep == 0) QueueScore(save.selectedCar);
            Cloud.MarkDirty();
        }

        IEnumerator ShowResults()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            ToResults();
        }

        // shortcut: closing the app during these 5 seconds loses that run's coins and score.
        IEnumerator OfferContinue()
        {
            for (ContinueLeft = 5; ContinueLeft > 0; ContinueLeft -= Time.unscaledDeltaTime) yield return null;
            Bank();
            ToResults();
        }

        // Rewarded ad, then the same run goes on, paused so the player can get ready.
        public void Continue()
        {
            if (State != GameState.Busted || ContinueLeft <= 0) return;
            StopAllCoroutines();
            ContinueLeft = 0;
            continued = true;
            Ads.ShowRewarded(earned =>
            {
                if (this == null || State != GameState.Busted) return;
                if (!earned)
                {
                    Bank();
                    ToResults();
                    return;
                }
                Player.Repair();
                Traffic.ResetTraffic();
                SetState(GameState.Run);
                Pause();
            });
        }

        // Interstitials: here only, at most once every 3 runs.
        void ToResults()
        {
            doubled = false;
            SetState(GameState.Results);
            var save = SaveData.Load(SaveData.DefaultPath);
            save.runsSinceAd++;
            if (!save.adsRemoved && Core.Ads.InterstitialDue(save.runsSinceAd) && Ads.InterstitialReady)
            {
                save.runsSinceAd = 0;
                Ads.ShowInterstitial();
            }
            save.Save(SaveData.DefaultPath);
        }

        public void DoubleCoins()
        {
            if (!CanDouble) return;
            doubled = true;
            int amount = Stats.Coins;
            Ads.ShowRewarded(earned =>
            {
                if (this == null) return;
                if (!earned) { doubled = false; return; }
                var save = SaveData.Load(SaveData.DefaultPath);
                save.coins += amount;
                save.Save(SaveData.DefaultPath);
                Cloud.MarkDirty();
            });
        }

        void SetState(GameState s)
        {
            State = s;
            ui.Show(s);
        }

        static void SetFrozen(bool frozen)
        {
            Time.timeScale = frozen ? 0 : 1;
            AudioListener.pause = frozen;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Pause();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            SetFrozen(false);
        }
    }
}
