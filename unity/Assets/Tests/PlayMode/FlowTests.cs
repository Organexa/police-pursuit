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
        world.Walker.Place(walker.transform.position + outward * 3, 0);
        Assert.IsFalse(walker.Panicking);
        for (int i = 0; i < 3; i++) { world.Shoot(); yield return new WaitForSeconds(0.35f); }
        Assert.IsTrue(walker.Panicking, "gunfire nearby sends people running");
        Assert.Greater(walker.Speed, PolicePursuit.Gameplay.Pedestrian.WalkSpeed);

        var other = people[1];
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
}
