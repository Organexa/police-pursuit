using System.Collections;
using System.IO;
using NUnit.Framework;
using PolicePursuit.Core;
using UnityEngine;
using UnityEngine.TestTools;

public class FlowTests
{
    string save; // null = no save.json yet
    string queue;

    // The run writes the real save files; put them back afterwards.
    [SetUp]
    public void Save()
    {
        save = File.Exists(SaveData.DefaultPath) ? File.ReadAllText(SaveData.DefaultPath) : null;
        queue = File.Exists(ScoreQueue.DefaultPath) ? File.ReadAllText(ScoreQueue.DefaultPath) : null;
        File.Delete(ScoreQueue.DefaultPath);
    }

    [TearDown]
    public void Restore()
    {
        if (save == null) File.Delete(SaveData.DefaultPath);
        else File.WriteAllText(SaveData.DefaultPath, save);
        if (queue == null) File.Delete(ScoreQueue.DefaultPath);
        else File.WriteAllText(ScoreQueue.DefaultPath, queue);
        PolicePursuit.Gameplay.Pedestrian.CrossChance = 0.5f;
        if (GameManager.Instance.World != null) GameManager.Instance.World.Police.enabled = true; // some tests park it
        GameManager.Instance.ToMenu();
    }

    [UnityTest]
    public IEnumerator MenuRunPauseBustedResultsMenu()
    {
        yield return null;
        var game = GameManager.Instance;
        Assert.IsNotNull(game);
        game.ToMenu(); // a first launch starts in the tutorial
        Assert.AreEqual(GameState.Menu, game.State);

        game.StartRun();
        yield return null;
        yield return null;
        Assert.AreEqual(GameState.Run, game.State);
        Assert.Greater(game.Stats.DurationS, 0);

        game.Pause();
        Assert.AreEqual(GameState.Pause, game.State);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsTrue(AudioListener.pause);
        var frozenAt = game.Stats.DurationS;
        yield return null;
        Assert.AreEqual(frozenAt, game.Stats.DurationS);

        game.Resume();
        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsFalse(AudioListener.pause);

        game.Player.TakeDamage(1000);
        Assert.AreEqual(GameState.Busted, game.State);
        Assert.GreaterOrEqual(GameManager.BestScore, game.Stats.Score);
        Assert.AreEqual(1, ScoreQueue.Load(ScoreQueue.DefaultPath).runs.Count);

        yield return new WaitForSecondsRealtime(1.7f);
        Assert.AreEqual(GameState.Results, game.State);

        game.ToMenu();
        Assert.AreEqual(GameState.Menu, game.State);
    }

    [UnityTest]
    public IEnumerator CriticalUpdateStopsTheRun()
    {
        yield return null;
        var game = GameManager.Instance;
        game.StartRun();
        game.OnUpdate(UpdateLevel.Critical, "");
        Assert.AreEqual(GameState.Menu, game.State);
        Assert.IsNotNull(GameObject.Find("Update"));
        game.OnUpdate(UpdateLevel.None, ""); // leave the screen hidden for the other tests
        Assert.IsNull(GameObject.Find("Update"));
    }

    class FakeAds : IAds
    {
        public int Interstitials;
        public bool RewardedReady => true;
        public bool InterstitialReady => true;
        public void ShowRewarded(System.Action<bool> done) => done(true);
        public void ShowInterstitial() => Interstitials++;
    }

    [UnityTest]
    public IEnumerator ContinueOnceThenDoubleCoins()
    {
        yield return null;
        var game = GameManager.Instance;
        var real = game.Ads;
        var fake = new FakeAds();
        game.Ads = fake;
        new SaveData { tutorialDone = true, runsSinceAd = 2 }.Save(SaveData.DefaultPath);
        try
        {
            game.StartRun();
            yield return null;
            game.Stats.Coins = 5;
            game.Player.TakeDamage(1000);
            Assert.AreEqual(0, SaveData.Load(SaveData.DefaultPath).coins); // not banked while it can go on
            yield return null;
            game.Continue();
            Assert.AreEqual(GameState.Pause, game.State);
            Assert.Greater(game.Player.Health, 0);

            game.Resume();
            game.Player.TakeDamage(1000); // second time: no offer, banked at once
            Assert.AreEqual(5, SaveData.Load(SaveData.DefaultPath).coins);
            Assert.AreEqual(1, ScoreQueue.Load(ScoreQueue.DefaultPath).runs.Count);

            yield return new WaitForSecondsRealtime(1.7f);
            Assert.AreEqual(GameState.Results, game.State);
            Assert.AreEqual(1, fake.Interstitials);
            Assert.AreEqual(0, SaveData.Load(SaveData.DefaultPath).runsSinceAd);

            game.DoubleCoins();
            game.DoubleCoins();
            Assert.AreEqual(10, SaveData.Load(SaveData.DefaultPath).coins);
        }
        finally { game.Ads = real; }
    }

    // The phone build drew everything with the error shader; every shape must use the shader we ship.
    [UnityTest]
    public IEnumerator ShapesUseTheShippedShader()
    {
        yield return null;
        Assume.That(!PolicePursuit.Gameplay.Models.Available, "the models bring their own shaders");
        var renderers = GameManager.Instance.GetComponentsInChildren<Renderer>();
        Assert.IsNotEmpty(renderers);
        foreach (var r in renderers)
            Assert.AreEqual("PolicePursuit/Flat", r.sharedMaterial.shader.name, r.name);
    }

    [UnityTest]
    public IEnumerator UsesTheUniversalPipeline()
    {
        yield return null;
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        Assert.IsNotNull(pipeline, "no render pipeline assigned");
        StringAssert.Contains("UniversalRenderPipelineAsset", pipeline.GetType().Name);
    }

    // Wide phones used to cut the Back buttons off.
    [UnityTest]
    public IEnumerator CanvasNeverShrinksBelowTheLayout()
    {
        yield return null;
        var scaler = GameManager.Instance.GetComponentInChildren<UnityEngine.UI.CanvasScaler>();
        Assert.AreEqual(UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode);
    }

    [UnityTest]
    public IEnumerator EngineIsHeardOnlyWhileDriving()
    {
        yield return null;
        var game = GameManager.Instance;
        var engine = System.Array.Find(game.Sound.GetComponents<AudioSource>(), s => s.clip != null && s.clip.name == "engine");
        game.StartRun();
        yield return null;
        yield return null;
        Assert.Greater(engine.volume, 0);
        game.ToMenu();
        yield return null;
        yield return null;
        Assert.AreEqual(0, engine.volume);
    }

    [UnityTest]
    public IEnumerator MusicFollowsItsOwnSlider()
    {
        yield return null;
        var game = GameManager.Instance;
        var bass = System.Array.Find(game.Sound.GetComponents<AudioSource>(),
            s => s.clip != null && s.clip.name == "music-bass");
        Assert.IsTrue(bass.ignoreListenerVolume);
        var before = game.Settings;
        game.ToMenu();
        game.ApplySettings(new SaveData { musicVolume = 1, soundVolume = 0 });
        yield return null;
        yield return null;
        Assert.Greater(bass.volume, 0);
        game.ApplySettings(new SaveData { musicVolume = 0 });
        yield return null;
        yield return null;
        Assert.AreEqual(0, bass.volume);
        game.ApplySettings(before);
    }

    [UnityTest]
    public IEnumerator TutorialCanBeStartedAndSkipped()
    {
        yield return null;
        var game = GameManager.Instance;
        File.Delete(SaveData.DefaultPath);

        game.StartTutorial();
        yield return null;
        Assert.AreEqual(GameState.Run, game.State);
        Assert.AreEqual(1, game.TutorialStep);
        Assert.AreEqual(0, game.Stats.DurationS);

        game.FinishTutorial();
        Assert.AreEqual(GameState.Menu, game.State);
        Assert.AreEqual(0, game.TutorialStep);
        Assert.IsTrue(SaveData.Load(SaveData.DefaultPath).tutorialDone);
    }

    // Sets the pedal every frame after the UI has written its own (zero) input.
    static IEnumerator Drive(PolicePursuit.Gameplay.VehicleController car, float pedal, float seconds)
    {
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            car.Pedal = pedal;
            car.Steer = 0;
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator OpenWorldCarDrivesBrakesAndReturnsToMenu()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        Assert.AreEqual(GameState.FreeRoam, game.State);
        Assert.IsFalse(game.Player.gameObject.activeSelf);
        var car = game.World.Car;

        yield return new WaitForSeconds(1); // settles on its suspension
        Assert.That(car.transform.position.y, Is.InRange(-0.3f, 1f), "car fell through or floats");
        Assert.Greater(car.transform.up.y, 0.95f);

        var start = car.transform.position;
        yield return Drive(car, 1, 3);
        float top = car.Speed;
        Assert.Greater(top, 8, "accelerates");
        Assert.Greater(car.transform.position.z - start.z, 10, "moves forward");
        Assert.Less(Mathf.Abs(car.transform.position.x - start.x), 2, "drives straight");

        yield return Drive(car, -1, 2);
        Assert.Less(car.Speed, top * 0.5f, "brakes");
        Assert.Greater(car.transform.up.y, 0.9f, "stays on its wheels");

        game.ToMenu();
        Assert.AreEqual(GameState.Menu, game.State);
        Assert.IsTrue(game.Player.gameObject.activeSelf);
        Assert.IsFalse(game.World.gameObject.activeSelf);
        Assert.IsTrue(Camera.main.GetComponent<PolicePursuit.Gameplay.CarCamera>().enabled);
    }

    [UnityTest]
    public IEnumerator OpenWorldPoliceCatchAParkedCar()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        Assert.Greater(world.PoliceDistance, 100, "police start far away");
        Time.timeScale = 4; // physics steps stay the same size, there are just more per frame
        float closest = float.MaxValue;
        for (float t = 0; t < 90 && world.Busts == 0; t += Time.deltaTime)
        {
            closest = Mathf.Min(closest, world.PoliceDistance);
            yield return null;
        }
        Time.timeScale = 1;
        Assert.Less(closest, PolicePursuit.Gameplay.OpenWorld.BustRange, "police reach the car");
        Assert.AreEqual(1, world.Busts, "a stopped car is busted");
        Assert.Greater(world.PoliceDistance, 100, "both cars start over after a bust");
    }

    [UnityTest]
    public IEnumerator OpenWorldLeaveTheCarWalkAndGetBackIn()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        yield return new WaitForSeconds(1); // settles

        Assert.IsTrue(world.ExitCar(), "room to get out at the crossroads");
        Assert.IsTrue(world.OnFoot);
        Assert.AreEqual(world.Walker.transform, world.Player);
        Assert.Less(Vector3.Distance(world.Walker.transform.position, world.Car.transform.position), 3.5f);
        Assert.IsTrue(world.Car.Handbrake, "the empty car stays parked");

        var start = world.Walker.transform.position;
        for (float t = 0; t < 1.5f; t += Time.deltaTime)
        {
            world.Walker.Move = Vector2.up; // forward, relative to the camera
            world.Walker.Sprint = false;
            yield return null;
        }
        float walked = Vector3.Distance(world.Walker.transform.position, start);
        Assert.That(walked, Is.InRange(3f, 9f), "runs about 4.5 m/s");
        Assert.That(world.Walker.transform.position.y, Is.InRange(-0.1f, 0.3f), "stays on the ground");

        world.Walker.Place(world.Car.transform.position - world.Car.transform.right * 2f, 0);
        yield return null;
        Assert.IsTrue(world.EnterCar());
        Assert.IsFalse(world.OnFoot);
        Assert.AreEqual(world.Car.transform, world.Player);
        Assert.IsFalse(world.Car.Handbrake);
        Assert.IsTrue(world.ExitCar());
        game.ToMenu();
        game.StartFreeRoam();
        Assert.IsFalse(world.OnFoot, "a new visit starts in the car");
    }

    [UnityTest]
    public IEnumerator OpenWorldShootReloadAndGetShotAt()
    {
        yield return null;
        Random.InitState(3); // the hit rolls repeat every run
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        yield return new WaitForSeconds(1);
        Assert.IsTrue(world.ExitCar());
        world.Police.Target = null; // keep the police out of this test

        var post = PolicePursuit.Gameplay.OpenWorld.SuspectPost;
        world.Walker.Place(post - Vector3.forward * 12, 0); // facing the suspect
        world.LookYaw = 10; // aim a little off: assist should still find it
        int magazine = world.Gun.Data.magazine;
        var armed = PolicePursuit.Gameplay.Suspect.State.Armed;
        for (float t = 0; t < 5 && world.Suspect.Now == armed; t += Time.deltaTime)
        {
            world.Shoot();
            yield return null;
        }
        Assert.AreEqual(PolicePursuit.Gameplay.Suspect.State.Surrendered, world.Suspect.Now, "low on health it gives up");
        Assert.Less(world.Gun.Ammo, magazine);

        while (world.Gun.Ammo > 0) { world.Shoot(); yield return null; }
        Assert.IsTrue(world.Gun.Reloading, "an empty gun reloads by itself");
        Assert.IsFalse(world.Shoot());
        yield return new WaitForSeconds(world.Gun.Data.reloadTime + 0.1f);
        Assert.AreEqual(magazine, world.Gun.Ammo);

        world.Suspect.ResetSuspect();
        // 50% hit chance and other systems also draw random numbers: allow a long run of misses.
        for (float t = 0; t < 12 && world.PlayerHealth.Current == world.PlayerHealth.Max; t += Time.deltaTime)
            yield return null;
        Assert.Less(world.PlayerHealth.Current, world.PlayerHealth.Max, "the suspect fires back");

        world.PlayerHealth.TakeDamage(1000);
        Assert.AreEqual(1, world.Downs);
        Assert.IsFalse(world.OnFoot, "incapacitated: back to the start in the car");
        Assert.AreEqual(world.PlayerHealth.Max, world.PlayerHealth.Current);
    }

    [UnityTest]
    public IEnumerator OpenWorldMeleeComboBlockAndDodge()
    {
        yield return null;
        Random.InitState(5);
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        yield return new WaitForSeconds(1);
        Assert.IsTrue(world.ExitCar());
        world.Police.Target = null;
        var melee = world.Melee;

        var post = PolicePursuit.Gameplay.OpenWorld.SuspectPost;
        world.Walker.Place(post - Vector3.forward * 1.3f, 0);
        yield return null;
        Assert.IsTrue(melee.LightAttack());
        Assert.IsFalse(melee.LightAttack(), "no new strike while one is under way");
        Assert.IsFalse(world.Shoot(), "no shooting mid-strike");
        for (int i = 1; i < 3; i++)
        {
            while (melee.Busy) yield return null;
            Assert.IsTrue(melee.LightAttack());
        }
        Assert.AreEqual(3, melee.Combo, "three quick lights chain");
        while (melee.Busy) yield return null;
        Assert.Less(world.Suspect.Health.Current, world.Suspect.Health.Max, "strikes land");
        Assert.Less(melee.Stamina, PolicePursuit.Gameplay.MeleeController.MaxStamina);

        melee.SetBlock(true);
        float before = world.PlayerHealth.Current;
        world.PlayerHealth.TakeDamage(10);
        Assert.AreEqual(before - 10 * PolicePursuit.Gameplay.MeleeController.BlockScale, world.PlayerHealth.Current, 0.01f);
        melee.SetBlock(false);

        var at = world.Walker.transform.position;
        Assert.IsTrue(melee.Dodge(-Vector3.forward));
        before = world.PlayerHealth.Current;
        world.PlayerHealth.TakeDamage(10);
        Assert.AreEqual(before, world.PlayerHealth.Current, "untouchable while dodging");
        yield return new WaitForSeconds(0.4f);
        Assert.Greater(Vector3.Distance(at, world.Walker.transform.position), 1.2f, "dodge moves");

        world.Suspect.Health.TakeDamage(1000);
        Assert.IsTrue(world.Suspect.Health.Down);
    }

    [UnityTest]
    public IEnumerator OpenWorldSurrenderCuffEscortAndDeliver()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var suspect = world.Suspect;
        yield return new WaitForSeconds(1);
        Assert.AreEqual("Stop the armed suspect", world.Objective);
        Assert.IsTrue(world.ExitCar());
        world.Police.Target = null;

        suspect.Health.TakeDamage(70);
        Assert.AreEqual(PolicePursuit.Gameplay.Suspect.State.Surrendered, suspect.Now);
        suspect.Health.TakeDamage(50);
        Assert.AreEqual(30, suspect.Health.Current, 0.01f, "a surrendered suspect cannot be hurt");

        world.Walker.Place(suspect.transform.position - Vector3.forward * 1.5f, 0);
        yield return null;
        Assert.IsTrue(world.CanArrest);
        for (float t = 0; t < 1; t += Time.deltaTime) { world.ArrestHeld = true; yield return null; }
        Assert.Greater(world.ArrestProgress, 0.3f);
        yield return null; // released for a frame
        Assert.AreEqual(0, world.ArrestProgress, "letting go starts the cuffing over");
        for (float t = 0; t < 2.5f && suspect.Now != PolicePursuit.Gameplay.Suspect.State.Cuffed; t += Time.deltaTime)
        {
            world.ArrestHeld = true;
            yield return null;
        }
        Assert.AreEqual(PolicePursuit.Gameplay.Suspect.State.Cuffed, suspect.Now);
        Assert.AreEqual("Bring the suspect to your car", world.Objective);

        world.Walker.Place(world.Walker.transform.position + Vector3.forward * 8, 0);
        float gap = Vector3.Distance(suspect.transform.position, world.Walker.transform.position);
        yield return new WaitForSeconds(1);
        Assert.Less(Vector3.Distance(suspect.transform.position, world.Walker.transform.position), gap - 3, "follows the player");

        world.Walker.Place(world.Car.transform.position - world.Car.transform.right * 2, 0);
        suspect.transform.position = world.Walker.transform.position - Vector3.forward * 1.5f; // skip the long walk
        yield return null;
        Assert.IsTrue(world.EnterCar());
        Assert.AreEqual(PolicePursuit.Gameplay.Suspect.State.InCar, suspect.Now);
        Assert.AreEqual("Drive to the police station", world.Objective);

        world.Car.Place(PolicePursuit.Gameplay.OpenWorld.Station + Vector3.up * 0.6f, 0);
        for (float t = 0; t < 3 && world.Deliveries == 0; t += Time.deltaTime) yield return null;
        Assert.AreEqual(1, world.Deliveries);
        Assert.AreEqual(PolicePursuit.Gameplay.Suspect.State.Armed, suspect.Now, "a new suspect waits at the post");
    }

    [UnityTest]
    public IEnumerator PrisonBreakAsPolice()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        int coins = GameManager.TotalCoins;
        game.StartMission(PolicePursuit.Gameplay.Mission.Role.Police);
        var world = game.World;
        var m = world.Mission;
        Assert.IsFalse(world.Police.gameObject.activeSelf, "no police chaser when you are the police");
        Assert.IsFalse(world.Suspect.gameObject.activeSelf);
        Assert.AreEqual("Chase the escaped prisoner's car", world.Objective);
        yield return new WaitForSeconds(2); // settles, then pulls away from a standstill
        var fleer = world.Fleer.transform;
        Assert.Greater(Vector3.Distance(fleer.position, PolicePursuit.Gameplay.Mission.FleeSpawn), 3, "the escapee drives off");

        for (float t = 0; t < 8 && m.Step == 0; t += Time.deltaTime)
        {
            world.Car.Place(fleer.position - fleer.forward * 12 + Vector3.up * 0.3f, fleer.eulerAngles.y); // tailing it
            yield return null;
        }
        Assert.AreEqual(1, m.Step, "staying close makes the escapee bail out");
        Assert.IsTrue(world.Suspect.gameObject.activeSelf);
        Assert.Less(Vector3.Distance(world.Suspect.transform.position, fleer.position), 4);
        Assert.AreEqual("Stop the armed suspect", world.Objective);

        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue(world.ExitCar());
        world.Suspect.Health.TakeDamage(70);
        var away = Vector3.ProjectOnPlane(world.Suspect.transform.position - fleer.position, Vector3.up).normalized;
        world.Walker.Place(world.Suspect.transform.position + away * 1.5f, 0); // on the far side from the car
        for (float t = 0; t < 2.5f && world.Suspect.Now != PolicePursuit.Gameplay.Suspect.State.Cuffed; t += Time.deltaTime)
        {
            world.ArrestHeld = true;
            yield return null;
        }
        world.Walker.Place(world.Car.transform.position - world.Car.transform.right * 2, 0);
        world.Suspect.transform.position = world.Walker.transform.position - Vector3.forward * 1.5f;
        yield return null;
        Assert.IsTrue(world.EnterCar());
        world.Car.Place(PolicePursuit.Gameplay.OpenWorld.Station + Vector3.up * 0.6f, 0);
        for (float t = 0; t < 3 && game.State != GameState.MissionResult; t += Time.unscaledDeltaTime) yield return null;

        Assert.AreEqual(GameState.MissionResult, game.State);
        Assert.AreEqual(PolicePursuit.Gameplay.Mission.Outcome.Captured, m.Result);
        Assert.AreEqual(0f, Time.timeScale, "frozen behind the result screen");
        Assert.AreEqual(coins + PolicePursuit.Gameplay.Mission.Reward, GameManager.TotalCoins);
        Assert.Contains("midnight_prison_break_police", SaveData.Load(SaveData.DefaultPath).missionsWon);
        Assert.IsNotNull(GameObject.Find("Mission result"));
    }

    [UnityTest]
    public IEnumerator PrisonBreakAsPrisonerEscapesAndCanFail()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartMission(PolicePursuit.Gameplay.Mission.Role.Prisoner);
        var world = game.World;
        var m = world.Mission;
        Assert.IsTrue(world.Police.gameObject.activeSelf);
        Assert.IsFalse(world.Fleer.gameObject.activeSelf);
        world.Police.Target = null;
        yield return new WaitForSeconds(1);

        world.Car.Place(PolicePursuit.Gameplay.Mission.Garage + Vector3.up * 0.6f, 0);
        for (float t = 0; t < 2 && m.Step == 0; t += Time.deltaTime) yield return null;
        Assert.AreEqual(1, m.Step, "the car dies at the garage");
        Assert.IsTrue(world.OnFoot);
        Assert.IsFalse(world.CanEnter, "a broken-down car cannot be used again");
        world.Police.Target = null; // getting out pointed the police at you
        Assert.IsFalse(world.CanArrest, "the prisoner does not arrest the guard");

        world.Walker.Place(PolicePursuit.Gameplay.Mission.Safehouse, 0);
        for (float t = 0; t < 2 && game.State != GameState.MissionResult; t += Time.unscaledDeltaTime) yield return null;
        Assert.AreEqual(PolicePursuit.Gameplay.Mission.Outcome.Escaped, m.Result);
        Assert.IsTrue(m.Won);

        int coins = GameManager.TotalCoins;
        game.StartMission(PolicePursuit.Gameplay.Mission.Role.Prisoner); // Retry
        Assert.AreEqual(1f, Time.timeScale);
        Assert.AreNotSame(m, world.Mission, "a retry is a fresh attempt");
        Assert.IsFalse(world.CarLocked);
        yield return null;
        world.PlayerHealth.TakeDamage(1000); // on foot not needed: health is checked any time
        yield return null;
        Assert.AreEqual(GameState.MissionResult, game.State);
        Assert.AreEqual(PolicePursuit.Gameplay.Mission.Outcome.Incapacitated, world.Mission.Result);
        Assert.AreEqual(coins, GameManager.TotalCoins, "no reward for a failed attempt");
    }

    [UnityTest]
    public IEnumerator OpenWorldTrafficDrivesStopsAndCrashes()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        world.Police.Target = null;
        var cars = world.Traffic.Cars;
        int active = cars.FindAll(c => c.gameObject.activeSelf).Count;
        Assert.AreEqual(GraphicsPresets.MaxCars(GraphicsPresets.Load()) / 2, active, "every car found a free road");

        var starts = cars.ConvertAll(c => c.transform.position);
        yield return new WaitForSeconds(3);
        float most = 0;
        for (int i = 0; i < cars.Count; i++)
        {
            var p = cars[i].transform.position;
            most = Mathf.Max(most, Vector3.Distance(p, starts[i]));
            // On a road: near a north-south or an east-west road centre line.
            float pitch = PolicePursuit.Gameplay.OpenWorld.Pitch;
            float offX = Mathf.Abs(p.x - Mathf.Round(p.x / pitch) * pitch), offZ = Mathf.Abs(p.z - Mathf.Round(p.z / pitch) * pitch);
            Assert.Less(Mathf.Min(offX, offZ), PolicePursuit.Gameplay.OpenWorld.Road / 2, cars[i].name + " left the road");
        }
        Assert.Greater(most, 10, "traffic moves");

        var civ = cars[0];
        world.Car.Place(civ.transform.position + civ.transform.forward * 7 + Vector3.up * 0.3f, civ.transform.eulerAngles.y);
        yield return new WaitForSeconds(1.5f);
        Assert.Less(civ.Speed, 1, "stops for a car in front");
        Assert.IsFalse(civ.Crashed);

        world.Car.Place(civ.transform.position + civ.transform.forward * 9 + Vector3.up * 0.3f, civ.transform.eulerAngles.y + 180);
        yield return new WaitForFixedUpdate();
        world.Car.Body.linearVelocity = -civ.transform.forward * 15;
        for (float t = 0; t < 2 && !civ.Crashed; t += Time.deltaTime) yield return null;
        Assert.IsTrue(civ.Crashed, "a hard hit knocks it loose");
        Assert.IsFalse(civ.Body.isKinematic);
    }

    [UnityTest]
    public IEnumerator OpenWorldPedestriansWalkPanicAndCannotBeHurt()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        world.Police.Target = null;
        var people = world.People.People;
        Assert.AreEqual(GraphicsPresets.MaxCars(GraphicsPresets.Load()), people.Count);

        var starts = people.ConvertAll(p => p.transform.position);
        yield return new WaitForSeconds(2);
        float half = PolicePursuit.Gameplay.Pedestrian.WalkHalf;
        for (int i = 0; i < people.Count; i++)
        {
            var p = people[i];
            if (!p.WaitingToCross) Assert.Greater(Vector3.Distance(p.transform.position, starts[i]), 1, "walks (unless waiting at a corner)");
            var off = p.transform.position - p.Block;
            float ring = Mathf.Max(Mathf.Abs(off.x), Mathf.Abs(off.z));
            if (!p.Crossing) Assert.AreEqual(half, ring, 0.8f, "stays on the pavement (or is on a crossing)");
        }

        // Shots pass through them and they are never a target.
        var walker = people[0];
        var off0 = walker.transform.position - walker.Block;
        var outward = Mathf.Abs(off0.x) > Mathf.Abs(off0.z) ? new Vector3(Mathf.Sign(off0.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(off0.z));
        var from = walker.transform.position + outward * 3 + Vector3.up * 1.2f; // from the road side
        var to = walker.transform.position + Vector3.up * 1.2f;
        Assert.IsFalse(Physics.Raycast(from, (to - from).normalized, out var hit, 3.2f) && hit.collider.GetComponentInParent<PolicePursuit.Gameplay.Pedestrian>() != null);
        Assert.IsNull(walker.GetComponentInParent<PolicePursuit.Gameplay.IDamageable>());

        Assert.IsTrue(world.ExitCar());
        world.Police.Target = null; // getting out points the police (and their dog) at you
        world.Walker.Place(walker.transform.position + outward * 3, 0);
        Assert.IsFalse(walker.Panicking);
        for (int i = 0; i < 3; i++) { world.Shoot(); yield return new WaitForSeconds(0.35f); }
        Assert.IsTrue(walker.Panicking, "gunfire nearby sends people running");
        Assert.Greater(walker.Speed, PolicePursuit.Gameplay.Pedestrian.WalkSpeed);

        var other = people.Find(p => p != walker && !p.Crossing && !p.WaitingToCross); // on the pavement, by a wall
        world.Car.Place(other.transform.position + Vector3.up * 0.6f, 0); // a car right on the pavement
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsTrue(other.Diving, "dives clear of a car");
    }

    [UnityTest]
    public IEnumerator OpenWorldTrafficWaitsForGreen()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        world.Police.Target = null;
        Assert.AreEqual(80, world.Lights.LampCount, "one lamp per road into each crossroads");

        Time.timeScale = 4;
        PolicePursuit.Gameplay.CivilCar waiter = null;
        for (float t = 0; t < 60 && waiter == null; t += Time.deltaTime)
        {
            foreach (var c in world.Traffic.Cars)
                if (c.gameObject.activeSelf && !c.Crashed && c.WaitingAtLight && c.Speed == 0) { waiter = c; break; }
            yield return null;
        }
        Assert.IsNotNull(waiter, "some car stopped at a light");
        var light = PolicePursuit.Gameplay.TrafficLights.For(waiter.Heading, waiter.Coming, Time.time);
        Assert.AreNotEqual(PolicePursuit.Gameplay.TrafficLights.Light.Green, light, "it stopped for amber or red");
        float toCentre = Vector3.Distance(waiter.transform.position, PolicePursuit.Gameplay.PoliceChase.Centre(waiter.Heading));
        Assert.Greater(toCentre, PolicePursuit.Gameplay.CivilCar.NodeClear - 1.5f, "it waits behind the line");

        var heading = waiter.Heading;
        bool moved = false;
        for (float t = 0; t < PolicePursuit.Gameplay.TrafficLights.Cycle + 2 && !moved; t += Time.deltaTime)
        {
            moved = waiter.Speed > 2 || waiter.Heading != heading;
            yield return null;
        }
        Time.timeScale = 1;
        Assert.IsTrue(moved, "and goes on green");
    }

    [UnityTest]
    public IEnumerator OpenWorldPedestriansCrossOnRed()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        world.Police.Target = null;
        PolicePursuit.Gameplay.Pedestrian.CrossChance = 1;
        Time.timeScale = 4;
        PolicePursuit.Gameplay.Pedestrian crossed = null;
        bool seenVisible = false;
        var before = world.People.People.ConvertAll(p => p.Crossings); // people carry over from earlier tests
        for (float t = 0; t < 120 && crossed == null; t += Time.deltaTime)
        {
            for (int i = 0; i < world.People.People.Count; i++)
            {
                var p = world.People.People[i];
                if (p.Crossing && p.gameObject.layer == 0) seenVisible = true;
                if (p.Crossings > before[i]) { crossed = p; break; }
            }
            yield return null;
        }
        Time.timeScale = 1;
        Assert.IsNotNull(crossed, "someone crossed");
        Assert.IsTrue(seenVisible, "cars can see people while they cross");
        Assert.AreEqual(PolicePursuit.Gameplay.TrafficLights.Light.Red, crossed.LightAtStart, "only on red for the road crossed");
        Assert.GreaterOrEqual(crossed.RedLeftAtStart, PolicePursuit.Gameplay.Pedestrian.CrossTime + 1, "with time to get over");
        Assert.AreEqual(2, crossed.gameObject.layer, "back on the pavement, out of the way of shots again");
        var off = crossed.transform.position - crossed.Block;
        Assert.AreEqual(PolicePursuit.Gameplay.Pedestrian.WalkHalf, Mathf.Max(Mathf.Abs(off.x), Mathf.Abs(off.z)), 0.8f, "on the next block's pavement");
    }

    [UnityTest]
    public IEnumerator OpenWorldHornHurriesPeopleNearby()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        world.Police.Target = null;
        var people = world.People.People;
        var near = people[0];
        world.Car.Place(near.transform.position + (near.Block - near.transform.position).normalized * -6 + Vector3.up * 0.6f, 0);
        yield return new WaitForFixedUpdate();
        PolicePursuit.Gameplay.Pedestrian far = null;
        foreach (var p in people)
            if (Vector3.Distance(p.transform.position, world.Car.transform.position) > 40) { far = p; break; }

        Assert.IsTrue(world.Honk());
        Assert.IsFalse(world.Honk(), "a short gap between honks");
        Assert.IsTrue(near.Hurried);
        if (!near.Panicking) Assert.AreEqual(PolicePursuit.Gameplay.Pedestrian.HurrySpeed, near.Speed);
        Assert.IsNotNull(far);
        Assert.IsFalse(far.Hurried, "out of earshot");

        yield return new WaitForSeconds(PolicePursuit.Gameplay.OpenWorld.HornEvery + 0.1f);
        world.Car.Place(world.Car.transform.position, 0);
        Assert.IsTrue(world.ExitCar());
        Assert.IsFalse(world.Honk(), "no horn on foot");
    }

    [UnityTest]
    public IEnumerator OpenWorldTrafficPullsOverForThePolice()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        world.Police.Target = null;
        var police = world.Police.GetComponent<PolicePursuit.Gameplay.VehicleController>();
        var civ = world.Traffic.Cars[1];
        civ.Place(new Vector2Int(4, 0), new Vector2Int(4, 1)); // northbound up the east edge, a long straight
        float laneX = PolicePursuit.Gameplay.CivilCar.LaneStart(new Vector2Int(4, 0), new Vector2Int(4, 1)).x;
        police.Place(civ.transform.position - civ.transform.forward * 12 + Vector3.up * 0.6f, 0);

        yield return new WaitForSeconds(0.5f);
        Assert.IsFalse(civ.PullingOver, "a parked police car has no siren going");
        for (float t = 0; t < 2; t += Time.deltaTime)
        {
            // keep the police car rolling just behind it
            police.Place(civ.transform.position - civ.transform.forward * 10 + Vector3.up * 0.6f, 0);
            police.Body.linearVelocity = Vector3.forward * 6;
            yield return null;
        }
        Assert.IsTrue(civ.PullingOver);
        Assert.LessOrEqual(civ.Speed, PolicePursuit.Gameplay.CivilCar.PullOverSpeed + 0.01f, "slows right down");
        Assert.Greater(civ.transform.position.x - laneX, 0.6f, "eases towards the kerb on its right");

        police.Place(PolicePursuit.Gameplay.OpenWorld.PoliceSpawn, 0);
        yield return new WaitForSeconds(0.5f);
        Assert.IsFalse(civ.PullingOver, "carries on once the police have gone");
    }

    [UnityTest]
    public IEnumerator PoliceSideHasASirenThatClearsTraffic()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        Assert.IsFalse(game.World.ToggleSiren(), "no siren outside the police side");

        game.StartMission(PolicePursuit.Gameplay.Mission.Role.Police);
        var world = game.World;
        Assert.IsTrue(world.Car.HasLightBar);
        Assert.IsFalse(world.SirenOn, "starts off");
        yield return null;
        Assert.IsFalse(world.Car.LightsShowing);
        Assert.IsTrue(world.ToggleSiren());
        yield return null;
        Assert.IsTrue(world.Car.LightsShowing, "lights flash");

        var civ = world.Traffic.Cars[1];
        civ.Place(new Vector2Int(4, 0), new Vector2Int(4, 1));
        for (float t = 0; t < 2; t += Time.deltaTime)
        {
            world.Car.Place(civ.transform.position - civ.transform.forward * 10 + Vector3.up * 0.6f, 0);
            world.Car.Body.linearVelocity = Vector3.forward * 6;
            yield return null;
        }
        Assert.IsTrue(civ.PullingOver, "traffic makes way for your siren");

        world.ToggleSiren();
        for (float t = 0; t < 0.5f; t += Time.deltaTime)
        {
            world.Car.Place(civ.transform.position - civ.transform.forward * 10 + Vector3.up * 0.6f, 0);
            world.Car.Body.linearVelocity = Vector3.forward * 6;
            yield return null;
        }
        Assert.IsFalse(civ.PullingOver, "siren off: no reason to pull over");
        Assert.IsFalse(world.Car.LightsShowing);
    }

    [UnityTest]
    public IEnumerator SirenHoldsPedestriansAtTheKerb()
    {
        yield return null;
        var game = GameManager.Instance;
        game.StartMission(PolicePursuit.Gameplay.Mission.Role.Police);
        var world = game.World;
        PolicePursuit.Gameplay.Pedestrian.CrossChance = 1;
        Time.timeScale = 4;
        PolicePursuit.Gameplay.Pedestrian waiting = null;
        for (float t = 0; t < 60 && waiting == null; t += Time.deltaTime)
        {
            foreach (var p in world.People.People) if (p.WaitingToCross) { waiting = p; break; }
            yield return null;
        }
        Assert.IsNotNull(waiting, "someone waits at a corner");

        world.ToggleSiren();
        var off = waiting.transform.position - waiting.Block;
        var road = Mathf.Abs(off.x) > Mathf.Abs(off.z) ? new Vector3(Mathf.Sign(off.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(off.z));
        int crossed = waiting.Crossings;
        bool started = false;
        // A whole light cycle with your siren going just beside them: they never step out.
        for (float t = 0; t < PolicePursuit.Gameplay.TrafficLights.Cycle + 1; t += Time.deltaTime)
        {
            world.Car.Place(waiting.transform.position + road * 8 + Vector3.up * 0.6f, 0);
            world.Car.Body.linearVelocity = Vector3.forward * 6;
            if (t > 0.3f && waiting.Crossing && waiting.Crossings == crossed) started = true; // the hold is set 4 times a second
            yield return null;
        }
        Assert.IsTrue(waiting.HoldForSiren);
        Assert.IsFalse(started, "no crossing while a siren is close");
        Assert.IsTrue(waiting.WaitingToCross, "still waiting, not wandered off");

        world.ToggleSiren();
        for (float t = 0; t < 1; t += Time.deltaTime) yield return null;
        Time.timeScale = 1;
        Assert.IsFalse(waiting.HoldForSiren, "siren off: free to cross on the next red");
    }

    [UnityTest]
    public IEnumerator SirenSpooksTheEscapee()
    {
        yield return null;
        var game = GameManager.Instance;
        game.StartMission(PolicePursuit.Gameplay.Mission.Role.Police);
        var world = game.World;
        var flee = world.Fleer.GetComponent<PolicePursuit.Gameplay.VehicleController>();
        float top = PolicePursuit.Gameplay.VehicleController.TopSpeed;
        yield return null;
        Assert.AreEqual(top * PolicePursuit.Gameplay.FleeDriver.Calm, flee.SpeedLimit, 0.01f);

        world.ToggleSiren(); // the escapee starts 30 m ahead: in earshot
        yield return null;
        yield return null;
        Assert.IsTrue(world.Fleer.Scared);
        Assert.AreEqual(top * PolicePursuit.Gameplay.FleeDriver.Spooked, flee.SpeedLimit, 0.01f, "drives harder");
        Assert.Less(flee.SpeedLimit, world.Car.SpeedLimit, "still slower than you");

        world.Car.Place(PolicePursuit.Gameplay.OpenWorld.PoliceSpawn, 0); // the far corner, well over 40 m away
        yield return null;
        yield return null;
        Assert.IsFalse(world.Fleer.Scared, "out of earshot");
        world.Car.Place(PolicePursuit.Gameplay.OpenWorld.Spawn, 0);
        yield return null;
        world.ToggleSiren();
        yield return null;
        yield return null;
        Assert.AreEqual(top * PolicePursuit.Gameplay.FleeDriver.Calm, flee.SpeedLimit, 0.01f, "siren off: calms down");
    }

    [UnityTest]
    public IEnumerator AiCarsSwerveAndBrakeForPeopleOnACrossing()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        world.Police.Target = null;
        var car = world.Car; // any car can be driven by the shared AI steering
        car.Place(new Vector3(0, 0.6f, -30), 0);
        yield return new WaitForFixedUpdate();
        car.Body.linearVelocity = Vector3.forward * 10;
        float stuck = 0, reverse = 0;
        var goal = new Vector3(0, 0, 40); // dead ahead: with nothing in the way, no steering at all

        PolicePursuit.Gameplay.PoliceChase.Steer(car, goal, false, 0.02f, ref stuck, ref reverse);
        Assert.AreEqual(0, car.Steer, 0.05f);
        Assert.AreEqual(1, car.Pedal);

        var person = PolicePursuit.Gameplay.Pedestrian.Create(world.transform, Vector3.zero, 0, 1, Color.white);
        person.enabled = false; // stands still where it is put
        person.transform.position = new Vector3(0.6f, 0, -20); // just right of the car's path
        person.gameObject.layer = 0; // as while crossing
        Physics.SyncTransforms();
        PolicePursuit.Gameplay.PoliceChase.Steer(car, goal, false, 0.02f, ref stuck, ref reverse);
        Object.Destroy(person.gameObject);
        Assert.Less(car.Steer, -0.5f, "swerves away, to the left");
        Assert.AreEqual(-1, car.Pedal, "and brakes hard");
    }

    [UnityTest]
    public IEnumerator CivilCarsStopForPeopleOnACrossing()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        world.Police.Target = null;
        var civ = world.Traffic.Cars[2];
        civ.Place(new Vector2Int(4, 0), new Vector2Int(4, 1)); // northbound up the east edge
        yield return new WaitForSeconds(0.5f);

        var person = PolicePursuit.Gameplay.Pedestrian.Create(world.transform, Vector3.zero, 0, 1, Color.white);
        person.enabled = false; // stands where it is put
        person.transform.position = civ.transform.position + civ.transform.forward * 9;
        person.gameObject.layer = 0; // as while crossing
        Physics.SyncTransforms();
        // Longer than the 4 s after which a car stuck behind another car creeps on.
        for (float t = 0; t < 6; t += Time.deltaTime) yield return null;
        float gap = Vector3.Distance(civ.transform.position, person.transform.position);
        Assert.Less(civ.Speed, 0.5f, "stopped");
        Assert.Greater(gap, 3, "short of the person, and never creeps into them");
        Assert.IsFalse(person.Diving, "never reached them");

        Object.Destroy(person.gameObject);
        for (float t = 0; t < 2; t += Time.deltaTime) yield return null;
        Assert.Greater(civ.Speed, 1, "drives on once they have crossed");
    }

    [UnityTest]
    public IEnumerator RoadblockLeavesAGapAndClears()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var block = world.Block;
        Assert.IsFalse(block.Active);

        block.Tick(world.Car, false, PolicePursuit.Gameplay.RoadBlock.FirstAfter + 1);
        Assert.IsFalse(block.Active, "not chased: none, however long you drive");

        // The police chase the car from the far corner for the rest of this test, which keeps the roadblock up.
        block.Spawn(new Vector2Int(2, 3), new Vector2Int(2, 4)); // north of the centre: x = 0, z 54..108
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.IsTrue(block.Active);
        Assert.IsTrue(block[0].LightsShowing, "lights flashing");
        for (int i = 0; i < 2; i++)
        {
            var p = block[i].transform.position;
            Assert.Less(Mathf.Abs(p.x), PolicePursuit.Gameplay.OpenWorld.Road / 2, "on the road");
            Assert.That(p.z, Is.InRange(70f, 92f), "between the two crossroads");
        }
        Physics.SyncTransforms();
        Assert.IsTrue(Physics.CheckBox(new Vector3(-4.5f, 0.8f, 81), new Vector3(0.85f, 0.4f, 1.5f)), "the left lane is blocked");
        Assert.IsFalse(Physics.CheckBox(new Vector3(5.5f, 0.8f, 81), new Vector3(0.85f, 0.4f, 2f)), "a car-wide gap on the right");

        block.Tick(world.Car, true, PolicePursuit.Gameplay.RoadBlock.Lasts + 1);
        Assert.IsFalse(block.Active, "packs up after a while");

        world.Restart();
        world.Car.Place(new Vector3(0, 0.6f, -30), 0);
        yield return new WaitForFixedUpdate();
        world.Car.Body.linearVelocity = Vector3.forward * 12;
        block.Tick(world.Car, true, PolicePursuit.Gameplay.RoadBlock.FirstAfter + 1);
        Assert.IsTrue(block.Active, "set up ahead while you are chased at speed");
        Assert.AreEqual(new Vector2Int(2, 2), block.From);
        block.Tick(world.Car, false, 0.02f);
        Assert.IsFalse(block.Active, "gone when the chase is off");
    }

    [UnityTest]
    public IEnumerator RoadblockCarsChaseOnceYouAreThrough()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var block = world.Block;
        block.Spawn(new Vector2Int(2, 3), new Vector2Int(2, 4)); // x = 0, cars around z = 81
        yield return new WaitForFixedUpdate();
        Assert.IsFalse(block.Chasing, "waits while you are still on the way");

        world.Car.Place(new Vector3(0, 0.6f, 95), 0); // just through, heading on north
        yield return new WaitForFixedUpdate();
        block.Tick(world.Car, true, 0.02f);
        Assert.IsTrue(block.Chasing, "joins the chase once you are past");
        Assert.LessOrEqual(world.PoliceDistance, block.ChaserDistance(world.Car.transform.position) + 0.01f, "they count for being busted");

        var start = new[] { block[0].transform.position, block[1].transform.position };
        float closest = block.ChaserDistance(world.Car.transform.position);
        yield return new WaitForSeconds(1.5f); // short of the 2 s it takes to be busted
        Assert.IsTrue(block.Active);
        Assert.Greater(Vector3.Distance(block[0].transform.position, start[0]) + Vector3.Distance(block[1].transform.position, start[1]), 3, "they drive off after you");
        Assert.Less(block.ChaserDistance(world.Car.transform.position), closest, "and close in");

        block.Tick(world.Car, true, PolicePursuit.Gameplay.RoadBlock.Lasts + 1);
        Assert.IsTrue(block.Active, "a chasing roadblock does not pack up on a timer");
        block.Tick(world.Car, false, 0.02f);
        Assert.IsFalse(block.Active);
        Assert.IsFalse(block.Chasing, "off when the chase is off");
    }

    [UnityTest]
    public IEnumerator TrafficPullsOverForRoadblockCarsOnTheMove()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        world.Police.Target = world.Car.transform;
        var block = world.Block;
        block.Spawn(new Vector2Int(2, 3), new Vector2Int(2, 4));
        var rb = block[0];
        var civ = world.Traffic.Cars[1];
        civ.Place(new Vector2Int(4, 0), new Vector2Int(4, 1)); // away from the main chaser and the player
        rb.Place(civ.transform.position - civ.transform.forward * 12 + Vector3.up * 0.6f, 0);
        yield return new WaitForSeconds(0.5f);
        Assert.IsFalse(civ.PullingOver, "a parked roadblock car is silent");

        for (float t = 0; t < 1.5f; t += Time.deltaTime)
        {
            rb.Place(civ.transform.position - civ.transform.forward * 10 + Vector3.up * 0.6f, 0);
            rb.Body.linearVelocity = Vector3.forward * 6;
            yield return null;
        }
        Assert.IsTrue(civ.PullingOver, "makes way for a roadblock car on the move");
    }

    [UnityTest]
    public IEnumerator HelicopterSearchlightAtWantedFour()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var heli = world.Heli;
        yield return null;
        Assert.AreEqual(1, world.Wanted, "a chase starts at one star");
        Assert.IsFalse(heli.gameObject.activeSelf);

        world.ChaseTime = 95; // 90 s in: four stars
        yield return null;
        Assert.AreEqual(4, world.Wanted);
        Assert.IsTrue(heli.gameObject.activeSelf, "the helicopter joins");
        Assert.IsFalse(heli.Overhead, "it has to fly in first");

        Time.timeScale = 4;
        for (float t = 0; t < 20 && !heli.Overhead; t += Time.deltaTime) yield return null;
        Time.timeScale = 1;
        Assert.IsTrue(heli.Overhead);
        Assert.IsTrue(heli.Beam.enabled, "searchlight on");
        Assert.IsTrue(heli.InBeam(world.Player.position), "it has found the parked car");
        Assert.Greater(heli.transform.position.y, 25, "high up");

        world.Car.Place(world.Car.transform.position + Vector3.forward * 60, 0); // a fast getaway
        yield return null;
        Assert.IsFalse(heli.InBeam(world.Player.position), "a fast car leaves the beam behind");
        for (float t = 0; t < 6 && !heli.InBeam(world.Player.position); t += Time.deltaTime) yield return null;
        Assert.IsTrue(heli.InBeam(world.Player.position), "and it catches up with a car that stops");

        world.ChaseTime = 10;
        yield return null;
        Assert.AreEqual(1, world.Wanted);
        Assert.IsFalse(heli.gameObject.activeSelf, "below four stars it goes");
    }

    [UnityTest]
    public IEnumerator LosingThePoliceDropsTheWantedLevel()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var police = world.Police.GetComponent<PolicePursuit.Gameplay.VehicleController>();
        world.Car.Place(new Vector3(108, 0.6f, 108), 0); // the far corner from the police
        world.ChaseTime = 70; // three stars
        yield return new WaitForFixedUpdate();
        yield return null; // the police check their sight line once: buildings in the way
        Assert.IsFalse(world.Police.InSight);
        world.Police.enabled = false; // and then sit still where they are
        police.Pedal = police.Steer = 0;
        police.Place(PolicePursuit.Gameplay.OpenWorld.PoliceSpawn, 0);
        Assert.AreEqual(3, world.Wanted);

        Time.timeScale = 4;
        for (float t = 0; t < PolicePursuit.Gameplay.OpenWorld.LoseAfter + 2 && world.Wanted > 1; t += Time.deltaTime) yield return null;
        Time.timeScale = 1;
        world.Police.enabled = true;
        Assert.AreEqual(1, world.Wanted, "out of sight and far away long enough: lost them");
    }

    [UnityTest]
    public IEnumerator SearchlightLeadsAMovingCar()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var heli = world.Heli;
        world.Police.Target = world.Car.transform; // still a chase...
        world.Police.enabled = false; // ...but the police car stays parked in its corner, so no bust
        var police = world.Police.GetComponent<PolicePursuit.Gameplay.VehicleController>();
        police.Pedal = police.Steer = 0;
        world.ChaseTime = 95;
        world.Car.Place(new Vector3(0, 0.6f, -100), 0); // a long straight north ahead
        Time.timeScale = 4;
        for (float t = 0; t < 20 && !heli.Overhead; t += Time.deltaTime) yield return null;
        Time.timeScale = 1;
        Assert.IsTrue(heli.Overhead);
        for (float t = 0; t < 3; t += Time.deltaTime)
        {
            world.Car.Body.linearVelocity = Vector3.forward * 10;
            yield return null;
        }
        Assert.Greater(heli.Watching.z, world.Car.transform.position.z + 3, "the beam is on the road ahead of you");
        Assert.Less(heli.Watching.z - world.Car.transform.position.z, PolicePursuit.Gameplay.Helicopter.MaxLead + 1, "close ahead, not far off");
        world.Police.enabled = true;
    }

    [UnityTest]
    public IEnumerator HelicopterLosesYouInTheTunnel()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var heli = world.Heli;
        world.Police.enabled = false; // parked in its corner: still a chase, no bust
        var police = world.Police.GetComponent<PolicePursuit.Gameplay.VehicleController>();
        police.Pedal = police.Steer = 0;
        var entrance = PolicePursuit.Gameplay.PoliceChase.Centre(PolicePursuit.Gameplay.OpenWorld.TunnelFrom);
        world.Car.Place(entrance + Vector3.up * 0.6f, 0); // just outside the tunnel mouth
        world.ChaseTime = 95;
        Time.timeScale = 4;
        for (float t = 0; t < 20 && !heli.InBeam(world.Player.position); t += Time.deltaTime) yield return null;
        Time.timeScale = 1;
        Assert.IsTrue(heli.InBeam(world.Player.position), "found you outside");

        var inside = (entrance + PolicePursuit.Gameplay.PoliceChase.Centre(PolicePursuit.Gameplay.OpenWorld.TunnelTo)) / 2;
        world.Car.Place(inside + Vector3.up * 0.6f, 0);
        yield return null;
        yield return null;
        var lastSeen = heli.Watching;
        yield return new WaitForSeconds(1.5f);
        Assert.IsFalse(heli.CanSee);
        Assert.IsFalse(heli.InBeam(world.Player.position), "no searchlight in the tunnel");
        Assert.Less(Vector3.Distance(heli.Watching, lastSeen), 0.01f, "the beam waits where it last saw you");

        world.Car.Place(PolicePursuit.Gameplay.PoliceChase.Centre(PolicePursuit.Gameplay.OpenWorld.TunnelTo) + Vector3.up * 0.6f, 0);
        yield return null;
        Assert.IsTrue(heli.CanSee, "out the other end: seen again");
        world.Police.enabled = true;
    }

    [UnityTest]
    public IEnumerator PoliceDogChasesYouOnFoot()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var dog = world.Dog;
        var police = world.Police.GetComponent<PolicePursuit.Gameplay.VehicleController>();
        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue(world.ExitCar());
        world.Police.enabled = false; // the police car stops 20 m off and lets the dog out
        police.Pedal = police.Steer = 0;
        police.Place(world.Walker.transform.position - Vector3.forward * 20 + Vector3.up * 0.6f, 0);
        yield return null;
        yield return null;
        Assert.IsTrue(dog.gameObject.activeSelf, "a dog comes out when you are on foot close by");
        Assert.IsNull(dog.GetComponentInParent<PolicePursuit.Gameplay.IDamageable>(), "it cannot be hurt");

        // Sprinting away: it falls behind, slowly.
        float gapBefore = Vector3.Distance(dog.transform.position, world.Walker.transform.position);
        for (float t = 0; t < 1.5f; t += Time.deltaTime) { yield return null; }
        float caughtUp = Vector3.Distance(dog.transform.position, world.Walker.transform.position);
        Assert.Less(caughtUp, gapBefore, "it runs at you");
        for (float t = 0; t < 2; t += Time.deltaTime)
        {
            world.Walker.Move = Vector2.up;
            world.Walker.Sprint = true;
            yield return null;
        }
        Assert.Greater(Vector3.Distance(dog.transform.position, world.Walker.transform.position), caughtUp, "a sprint outruns it");
        int busts = world.Busts;
        // Standing still: it holds you, which is a bust.
        for (float t = 0; t < 8 && world.Busts == busts; t += Time.deltaTime) yield return null;
        Assert.AreEqual(busts + 1, world.Busts, "held by the dog");
        Assert.IsFalse(dog.gameObject.activeSelf, "and it goes back after");
        world.Police.enabled = true;
    }

    [UnityTest]
    public IEnumerator PoliceDogLosesYourScentInWater()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var dog = world.Dog;
        var police = world.Police.GetComponent<PolicePursuit.Gameplay.VehicleController>();
        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue(world.ExitCar());
        var shore = PolicePursuit.Gameplay.OpenWorld.ParkCentre + new Vector3(PolicePursuit.Gameplay.OpenWorld.PondHalf + 3, 0, 0);
        world.Walker.Place(shore, 0);
        world.Police.enabled = false; // parks 20 m off and lets the dog out
        police.Pedal = police.Steer = 0;
        police.Place(shore - Vector3.forward * 20 + Vector3.up * 0.6f, 0);
        yield return null;
        yield return null;
        Assert.IsTrue(dog.gameObject.activeSelf);
        int busts = world.Busts;

        // Into the pond, with the park's north fence between you and the dog: in the water and out of its sight.
        var pond = PolicePursuit.Gameplay.OpenWorld.ParkCentre;
        dog.Release(pond + new Vector3(0, 0.1f, PolicePursuit.Gameplay.OpenWorld.FenceHalf + 3), world.Walker.transform, world.Melee);
        world.Walker.Place(pond, 0);
        yield return null;
        yield return null;
        Assert.IsTrue(dog.LostScent, "in the water and out of sight: the trail is broken");
        Assert.AreEqual(0.5f, world.Walker.SpeedScale, "wading is slow");

        Time.timeScale = 4;
        for (float t = 0; t < 40 && dog.gameObject.activeSelf; t += Time.deltaTime) yield return null;
        Assert.IsFalse(dog.gameObject.activeSelf, "gives up after a sniff around");
        Assert.AreEqual(busts, world.Busts, "and never held you");

        world.Walker.Place(shore, 0); // back on the grass, still near the police car
        for (float t = 0; t < 2; t += Time.deltaTime) yield return null;
        Time.timeScale = 1;
        Assert.IsFalse(dog.gameObject.activeSelf, "no fresh dog straight away");
        Assert.AreEqual(1f, world.Walker.SpeedScale, "full speed out of the water");
        world.Police.enabled = true;
    }

    [UnityTest]
    public IEnumerator PoliceDogBarksWhenItSeesYou()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var dog = world.Dog;
        var police = world.Police.GetComponent<PolicePursuit.Gameplay.VehicleController>();
        int barks = 0;
        System.Action count = () => barks++;
        dog.Barked += count;
        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue(world.ExitCar());
        var shore = PolicePursuit.Gameplay.OpenWorld.ParkCentre + new Vector3(PolicePursuit.Gameplay.OpenWorld.PondHalf + 3, 0, 0);
        world.Walker.Place(shore, 0);
        world.Police.enabled = false;
        police.Pedal = police.Steer = 0;
        police.Place(shore - Vector3.forward * 20 + Vector3.up * 0.6f, 0);
        for (float t = 0; t < 1.5f; t += Time.deltaTime) yield return null; // running in, not yet at you
        Assert.IsTrue(dog.Barking, "barks at you in plain sight");
        Assert.GreaterOrEqual(barks, 1);

        world.Walker.Place(PolicePursuit.Gameplay.OpenWorld.ParkCentre, 0); // into the pond, in plain view
        yield return null;
        yield return null;
        Assert.IsFalse(dog.LostScent, "it can see you: no losing it in the water");
        Assert.IsTrue(dog.Barking, "and it keeps barking");
        dog.Barked -= count;
        world.Police.enabled = true;
    }

    [UnityTest]
    public IEnumerator YouCanJumpTheParkFenceAndTheDogCannot()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var dog = world.Dog;
        var police = world.Police.GetComponent<PolicePursuit.Gameplay.VehicleController>();
        var park = PolicePursuit.Gameplay.OpenWorld.ParkCentre;
        float fence = PolicePursuit.Gameplay.OpenWorld.FenceHalf;
        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue(world.ExitCar());
        // Inside the park, 1.5 m short of the north fence, facing it: a running jump clears 0.7 m from there.
        world.Walker.Place(park + new Vector3(0, 0, fence - 1.5f), 0);
        world.LookYaw = 0;
        world.Police.enabled = false; // drives into the park through its open south side and lets the dog out
        police.Pedal = police.Steer = 0;
        police.Place(park + new Vector3(0, 0.6f, -5), 0); // within the dog's 40 m release range
        yield return null;
        yield return null;
        Assert.IsTrue(dog.gameObject.activeSelf);

        world.Walker.Jump = true;
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            world.Walker.Move = Vector2.up;
            yield return null;
        }
        Assert.Greater(world.Walker.transform.position.z, park.z + fence + 0.2f, "over the fence");

        // Stand still on the far side: the dog runs up to the fence and cannot get over it.
        for (float t = 0; t < 4; t += Time.deltaTime) yield return null;
        Assert.Less(dog.transform.position.z, park.z + fence, "still on the park side");
        Assert.AreEqual(0, dog.HoldProgress, "cannot reach you");
        world.Police.enabled = true;
    }

    [UnityTest]
    public IEnumerator PoliceDogFindsItsWayRoundTheFence()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var dog = world.Dog;
        var police = world.Police.GetComponent<PolicePursuit.Gameplay.VehicleController>();
        var park = PolicePursuit.Gameplay.OpenWorld.ParkCentre;
        float fence = PolicePursuit.Gameplay.OpenWorld.FenceHalf;
        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue(world.ExitCar());
        // You stand just over the north fence; the dog starts inside the park, right behind it.
        world.Walker.Place(park + new Vector3(0, 0, fence + 1.5f), 0);
        world.Police.enabled = false;
        police.Pedal = police.Steer = 0;
        police.Place(park + new Vector3(0, 0.6f, -5), 0); // within the dog's 40 m release range
        yield return null;
        yield return null;
        Assert.IsTrue(dog.gameObject.activeSelf);
        dog.Release(park + new Vector3(0, 0.1f, fence - 2), world.Walker.transform, world.Melee);

        int busts = world.Busts;
        float closest = float.MaxValue;
        for (float t = 0; t < 20 && world.Busts == busts; t += Time.deltaTime)
        {
            if (dog.gameObject.activeSelf) closest = Mathf.Min(closest, Vector3.Distance(dog.transform.position, world.Walker.transform.position));
            yield return null;
        }
        Assert.Less(closest, PolicePursuit.Gameplay.PoliceDog.HoldRange, "it ran round the open end of the fence to you");
        world.Police.enabled = true;
    }

    [UnityTest]
    public IEnumerator PoliceDogSwimsAfterYouInSight()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var dog = world.Dog;
        var police = world.Police.GetComponent<PolicePursuit.Gameplay.VehicleController>();
        var pond = PolicePursuit.Gameplay.OpenWorld.ParkCentre;
        float half = PolicePursuit.Gameplay.OpenWorld.PondHalf;
        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue(world.ExitCar());
        world.Police.enabled = false;
        police.Pedal = police.Steer = 0;
        police.Place(pond + new Vector3(half + 3, 0.6f, -25), 0); // by the park's open east side
        world.Walker.Place(pond + new Vector3(-half + 2, 0, 0), 0); // in the water, near the west bank
        yield return null;
        yield return null;
        Assert.IsTrue(dog.gameObject.activeSelf);
        dog.Release(pond + new Vector3(half - 1, 0.1f, 0), world.Walker.transform, world.Melee); // in the water, east side
        yield return null;
        Assert.IsTrue(dog.Swimming);
        Assert.AreEqual(PolicePursuit.Gameplay.PoliceDog.Speed * PolicePursuit.Gameplay.PoliceDog.SwimScale, dog.CurrentSpeed, 1e-4f);

        var start = dog.transform.position;
        yield return new WaitForSeconds(1);
        float swum = Vector3.Distance(dog.transform.position, start);
        Assert.IsFalse(dog.LostScent, "it can see you across the water");
        Assert.That(swum, Is.InRange(1.5f, PolicePursuit.Gameplay.PoliceDog.Speed * 0.75f), "swims after you, at half speed");
        world.Police.enabled = true;
    }

    [UnityTest]
    public IEnumerator PoliceDogShakesOffAfterSwimming()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var dog = world.Dog;
        var police = world.Police.GetComponent<PolicePursuit.Gameplay.VehicleController>();
        var pond = PolicePursuit.Gameplay.OpenWorld.ParkCentre;
        float half = PolicePursuit.Gameplay.OpenWorld.PondHalf;
        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue(world.ExitCar());
        world.Police.enabled = false;
        police.Pedal = police.Steer = 0;
        police.Place(pond + new Vector3(half + 3, 0.6f, -25), 0);
        world.Walker.Place(pond + new Vector3(-half - 4, 0, 0), 0); // on the dry grass west of the pond
        yield return null;
        dog.Release(pond + new Vector3(-half + 1.5f, 0.1f, 0), world.Walker.transform, world.Melee); // swimming, 1.5 m from the bank
        yield return null;
        Assert.IsTrue(dog.Swimming);

        for (float t = 0; t < 3 && !dog.Shaking; t += Time.deltaTime) yield return null;
        Assert.IsTrue(dog.Shaking, "out of the water: a shake");
        Assert.IsFalse(dog.Swimming);
        var at = dog.transform.position;
        yield return new WaitForSeconds(PolicePursuit.Gameplay.PoliceDog.ShakeFor * 0.6f);
        Assert.Less(Vector3.Distance(at, dog.transform.position), 0.2f, "stands still while it shakes");
        Assert.AreEqual(0, dog.HoldProgress);
        yield return new WaitForSeconds(PolicePursuit.Gameplay.PoliceDog.ShakeFor * 0.6f);
        Assert.IsFalse(dog.Shaking, "then back on your trail");
        world.Police.enabled = true;
    }

    [UnityTest]
    public IEnumerator PoliceDogLeavesWetPawPrints()
    {
        yield return null;
        var game = GameManager.Instance;
        game.ToMenu();
        game.StartFreeRoam();
        var world = game.World;
        var dog = world.Dog;
        var police = world.Police.GetComponent<PolicePursuit.Gameplay.VehicleController>();
        var pond = PolicePursuit.Gameplay.OpenWorld.ParkCentre;
        float half = PolicePursuit.Gameplay.OpenWorld.PondHalf;
        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue(world.ExitCar());
        world.Police.enabled = false;
        police.Pedal = police.Steer = 0;
        police.Place(pond + new Vector3(half + 3, 0.6f, 10), 0); // north-east of the pond, clear of the road you run along
        world.Walker.Place(pond + new Vector3(-half - 5, 0, 0), 0); // dry grass west of the pond
        yield return null;
        dog.Release(pond + new Vector3(-half + 1.5f, 0.1f, 0), world.Walker.transform, world.Melee);
        Assert.AreEqual(0, dog.PrintsShowing, "dry dog: no prints");

        for (float t = 0; t < 4 && dog.PrintsShowing == 0; t += Time.deltaTime) yield return null;
        Assert.IsTrue(dog.Wet);
        Assert.Greater(dog.PrintsShowing, 0, "out of the water and running: wet prints");
        Assert.LessOrEqual(dog.PrintsShowing, PolicePursuit.Gameplay.PoliceDog.PrintPool);

        // Just over the north fence: the dog has a long run round, so it is still out and about when the prints fade.
        // You sprint off west along the road south of the park, away from the pond: the dog follows on dry ground
        // and never quite catches you (7 m/s against its 6.5), so it is still out when the prints fade.
        world.Walker.Place(pond + new Vector3(-15, 0, -((PolicePursuit.Gameplay.OpenWorld.Pitch - PolicePursuit.Gameplay.OpenWorld.Road) / 2 + 3)), 0);
        world.LookYaw = 0;
        int busts = world.Busts;
        Time.timeScale = 4;
        float until = PolicePursuit.Gameplay.PoliceDog.WetFor + PolicePursuit.Gameplay.PoliceDog.PrintLife + 0.5f;
        for (float t = 0; t < until; t += Time.deltaTime)
        {
            world.Walker.Move = Vector2.left;
            world.Walker.Sprint = true;
            yield return null;
        }
        Time.timeScale = 1;
        Assert.AreEqual(busts, world.Busts, "not caught yet, so the dog is still out");
        Assert.IsTrue(dog.gameObject.activeSelf);
        Assert.IsFalse(dog.Wet, "dries off");
        Assert.AreEqual(0, dog.PrintsShowing, "and the prints fade by themselves");
        world.Police.enabled = true;
    }
}
