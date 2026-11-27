using System;
using System.IO;
using NUnit.Framework;
using PolicePursuit.Core;
using PolicePursuit.Gameplay;
using UnityEngine;

public class CoreTests
{
    [Serializable]
    class ScoreCase
    {
        public double distance_m;
        public int near_misses;
        public double duration_s;
        public int max_wanted;
        public long expected;
    }

    [Serializable]
    class ScoreCases { public ScoreCase[] cases; }

    [Test]
    public void ScoreMatchesSharedCases()
    {
        // The same file the server tests read, outside the Unity project.
        var path = Path.Combine(Application.dataPath, "../../shared/score_cases.json");
        var data = JsonUtility.FromJson<ScoreCases>(File.ReadAllText(path));
        Assert.IsNotEmpty(data.cases);
        foreach (var c in data.cases)
            Assert.AreEqual(c.expected,
                ScoreFormula.Compute(c.distance_m, c.near_misses, c.duration_s, c.max_wanted),
                $"distance {c.distance_m}, near {c.near_misses}, duration {c.duration_s}, wanted {c.max_wanted}");
    }

    [TestCase(0, 1)]
    [TestCase(29.9, 1)]
    [TestCase(30, 2)]
    [TestCase(120, 5)]
    [TestCase(3600, 5)]
    public void WantedRisesEvery30Seconds(double seconds, int expected) =>
        Assert.AreEqual(expected, WantedLevel.ForDuration(seconds));

    [TestCase(GfxPreset.Low, false, false)]
    [TestCase(GfxPreset.Medium, true, false)]
    [TestCase(GfxPreset.High, true, true)]
    public void EffectsFollowThePreset(GfxPreset preset, bool bloom, bool blur) =>
        Assert.AreEqual((bloom, blur), PolicePursuit.Gameplay.NightLook.Effects(preset));

    [TestCase(2048, 2048, GfxPreset.Low)]
    [TestCase(8192, 512, GfxPreset.Low)]
    [TestCase(4096, 2048, GfxPreset.Medium)]
    [TestCase(8192, 4096, GfxPreset.High)]
    public void PresetFollowsMemory(int systemMb, int graphicsMb, GfxPreset expected) =>
        Assert.AreEqual(expected, GraphicsPresets.Pick(systemMb, graphicsMb));

    [Test]
    public void TutorialFlagSavesAndLoads()
    {
        var path = Path.Combine(Application.temporaryCachePath, "save_test.json");
        File.Delete(path);
        Assert.IsFalse(SaveData.Load(path).tutorialDone);

        new SaveData { tutorialDone = true }.Save(path);
        Assert.IsTrue(SaveData.Load(path).tutorialDone);

        new SaveData().Save(path); // overwrite path
        Assert.IsFalse(SaveData.Load(path).tutorialDone);
        File.Delete(path);
    }

    [Test]
    public void CarUnlockNeedsEnoughCoins()
    {
        var car = CarList.Get("hatch");
        var save = new SaveData { coins = car.Price - 1 };
        Assert.IsFalse(save.TryUnlock(car));
        Assert.IsFalse(save.Select(car));
        Assert.AreEqual(car.Price - 1, save.coins);

        save.coins = car.Price;
        Assert.IsTrue(save.TryUnlock(car));
        Assert.AreEqual(0, save.coins);
        Assert.IsTrue(save.Select(car));
        Assert.IsFalse(save.TryUnlock(car)); // never charged twice

        var premium = CarList.Get("phantom");
        save.coins = 1000000;
        Assert.IsFalse(save.TryUnlock(premium));
    }

    [System.Serializable]
    class CarRow { public string id, name, color; public float top_speed_mps, handling, armor; public int price; public bool premium; }

    [System.Serializable]
    class CarFile { public CarRow[] cars; }

    [Test]
    public void CarListMatchesSharedFile()
    {
        var path = Path.Combine(Application.dataPath, "../../shared/cars.json");
        var rows = JsonUtility.FromJson<CarFile>(File.ReadAllText(path)).cars;
        Assert.AreEqual(CarList.All.Length, rows.Length);
        for (int i = 0; i < rows.Length; i++)
        {
            var car = CarList.All[i];
            Assert.AreEqual(rows[i].id, car.Id);
            Assert.AreEqual(rows[i].name, car.Name);
            Assert.AreEqual(rows[i].top_speed_mps, car.TopSpeed);
            Assert.AreEqual(rows[i].handling, car.Handling);
            Assert.AreEqual(rows[i].armor, car.Armor);
            Assert.AreEqual(rows[i].price, car.Price);
            Assert.AreEqual(rows[i].premium, car.Premium);
            Assert.AreEqual(System.Convert.ToInt32(rows[i].color, 16), car.Color);
        }
    }

    [Test]
    public void SettingsSaveAndLoad()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{\"coins\":5}"); // save from before settings existed
            var data = SaveData.Load(path);
            Assert.AreEqual(1f, data.soundVolume);
            Assert.AreEqual(1f, data.tiltSensitivity);
            Assert.IsTrue(data.haptics);
            Assert.IsFalse(data.tiltControls);

            data.soundVolume = 0.25f;
            data.musicVolume = 0;
            data.tiltControls = true;
            data.haptics = false;
            Assert.IsTrue(data.Save(path));
            var loaded = SaveData.Load(path);
            Assert.AreEqual(0.25f, loaded.soundVolume);
            Assert.AreEqual(0f, loaded.musicVolume);
            Assert.IsTrue(loaded.tiltControls);
            Assert.IsFalse(loaded.haptics);
            Assert.AreEqual(5, loaded.coins);
        }
        finally { File.Delete(path); }

        Assert.AreEqual(0f, KeyInput.TiltSteer(0, 1));
        Assert.AreEqual(1f, KeyInput.TiltSteer(0.9f, 1));
        Assert.AreEqual(-1f, KeyInput.TiltSteer(-0.9f, 2));
    }

    [Test]
    public void CloudSaveMergeRules()
    {
        var local = new SaveData { coins = 10, bestScore = 900, soundVolume = 0.5f, selectedCar = "hatch" };
        local.unlockedCars.Add("hatch");
        var cloud = new SaveData { coins = 70, bestScore = 100, soundVolume = 1, selectedCar = "sport" };
        cloud.unlockedCars.AddRange(new[] { "hatch", "sport" });

        local.Merge(cloud);
        Assert.AreEqual(70, local.coins);
        Assert.AreEqual(900, local.bestScore);
        CollectionAssert.AreEquivalent(new[] { "hatch", "sport" }, local.unlockedCars);
        Assert.AreEqual(0.5f, local.soundVolume);
        Assert.AreEqual("hatch", local.selectedCar);

        Assert.IsTrue(CloudSave.TryParseRows("[]", out var none));
        Assert.IsNull(none);
        Assert.IsTrue(CloudSave.TryParseRows("[{\"data\":{\"coins\":5,\"bestScore\":6}}]", out var row));
        Assert.AreEqual(5, row.coins);
        Assert.AreEqual(6, row.bestScore);
        Assert.IsFalse(CloudSave.TryParseRows("{\"message\":\"error\"}", out _));
        Assert.IsFalse(CloudSave.TryParseRows("[oops", out _));
    }

    [Test]
    public void ScoreUploadAndLeaderboardReplies()
    {
        Assert.IsTrue(CloudSave.ScoreDone(200, "{\"ok\":true,\"score\":5}"));
        Assert.IsTrue(CloudSave.ScoreDone(400, "{\"ok\":false,\"reason\":\"too_far\"}"));
        Assert.IsFalse(CloudSave.ScoreDone(400, "{\"message\":\"gateway\"}"));
        Assert.IsFalse(CloudSave.ScoreDone(429, "{\"ok\":false,\"reason\":\"rate_limit\"}"));
        Assert.IsFalse(CloudSave.ScoreDone(200, "<html>"));
        Assert.IsFalse(CloudSave.ScoreDone(0, ""));

        Assert.AreEqual("1.  A B   90\n2.  C   5", Leaderboard.Format(
            "[{\"rank\":1,\"display_name\":\"A\\nB\",\"score\":90},{\"rank\":2,\"display_name\":\"C\",\"score\":5}]"));
        Assert.AreEqual("No scores yet", Leaderboard.Format("[]"));
        Assert.IsNull(Leaderboard.Format("{\"message\":\"error\"}"));
        Assert.IsNull(Leaderboard.Format(null));
        Assert.AreEqual("Your rank: #7", Leaderboard.FormatRank("7"));
        Assert.AreEqual("No ranked run yet", Leaderboard.FormatRank("null"));
        Assert.IsNull(Leaderboard.FormatRank("{}"));
    }

    [Test]
    public void UpdateCheckComparesVersions()
    {
        Assert.AreEqual("com.policepursuit.game", UnityEditor.PlayerSettings.GetApplicationIdentifier(
            UnityEditor.Build.NamedBuildTarget.Android));
        Assert.IsFalse(UpdateCheck.Newer("1.0.0", "1.0")); // Unity's default version must not be blocked
        Assert.IsFalse(UpdateCheck.Newer("1.0", "1.0.0"));
        Assert.IsTrue(UpdateCheck.Newer("1.0.1", "1.0"));
        Assert.IsTrue(UpdateCheck.Newer("1.10.0", "1.9.3"));
        Assert.IsFalse(UpdateCheck.Newer("1.9.3", "1.10.0"));
        Assert.IsFalse(UpdateCheck.Newer("1.0.0", "1.0.0"));
        Assert.IsFalse(UpdateCheck.Newer("soon", "1.0.0"));
        Assert.IsFalse(UpdateCheck.Newer(null, "1.0.0"));

        const string row = "[{\"latest_version\":\"1.2.0\",\"critical_min_version\":\"1.1.0\",\"message\":null}]";
        Assert.AreEqual(UpdateLevel.Critical, UpdateCheck.Evaluate(row, "1.0.0", out var message));
        Assert.AreEqual("", message);
        Assert.AreEqual(UpdateLevel.Optional, UpdateCheck.Evaluate(row, "1.1.0", out _));
        Assert.AreEqual(UpdateLevel.None, UpdateCheck.Evaluate(row, "1.2.0", out _));
        Assert.AreEqual(UpdateLevel.None, UpdateCheck.Evaluate("[]", "1.0.0", out _));
        Assert.AreEqual(UpdateLevel.None, UpdateCheck.Evaluate("{\"message\":\"error\"}", "1.0.0", out _));
        Assert.AreEqual(UpdateLevel.None, UpdateCheck.Evaluate(null, "1.0.0", out _));
    }

    [Test]
    public void InterstitialAtMostOnceEveryThreeRuns()
    {
        Assert.IsFalse(Ads.InterstitialDue(1));
        Assert.IsFalse(Ads.InterstitialDue(2));
        Assert.IsTrue(Ads.InterstitialDue(3));
        Assert.IsTrue(Ads.InterstitialDue(9)); // no fill earlier: still due
    }

    [Test]
    public void PurchasesAreGrantedOnce()
    {
        var save = new SaveData { coins = 10 };
        Assert.IsTrue(Shop.Grant(save, "coins_small", "t1"));
        Assert.IsTrue(Shop.Grant(save, "coins_small", "t1")); // delivered again after a crash
        Assert.AreEqual(1010, save.coins);
        Assert.IsTrue(Shop.Grant(save, "coins_small", "t2"));
        Assert.AreEqual(2010, save.coins);

        Assert.IsTrue(Shop.Grant(save, Shop.RemoveAds, "t3"));
        Assert.IsTrue(save.adsRemoved);

        var premium = Array.Find(CarList.All, c => c.Premium);
        Assert.IsTrue(Shop.Grant(save, Shop.CarProduct(premium), "t4"));
        Assert.IsTrue(Shop.Grant(save, Shop.CarProduct(premium), "t4"));
        Assert.IsTrue(save.Owns(premium));
        Assert.AreEqual(1, save.unlockedCars.Count);

        Assert.IsFalse(Shop.Grant(save, "car_hatch", "t5")); // coin cars are not for sale
        Assert.IsFalse(Shop.Grant(save, "coins_huge", "t6"));
        Assert.IsFalse(Shop.Grant(save, "coins_small", ""));
        Assert.AreEqual(2010, save.coins);
    }

    [Test]
    public void SoundLoopsAndLevels()
    {
        // A loop that ends where it starts has no audible click.
        foreach (var wave in new Func<float, float>[] { Sound.Engine, Sound.Siren })
        {
            var samples = Sound.Samples(1f, wave);
            Assert.AreEqual(Sound.Rate, samples.Length);
            Assert.AreEqual(wave(0), wave(1), 0.01f);
            Assert.Greater(Array.FindAll(samples, s => Mathf.Abs(s) > 0.2f).Length, samples.Length / 4);
        }
        Assert.AreEqual(1f, Sound.SirenVolume(0));
        Assert.AreEqual(0.5f, Sound.SirenVolume(30), 0.001f);
        Assert.AreEqual(0f, Sound.SirenVolume(float.MaxValue));
        Assert.AreEqual(0.8f, Sound.EnginePitch(0, 30), 0.001f);
        Assert.AreEqual(2f, Sound.EnginePitch(39, 30), 0.001f); // boost does not go higher
    }

    [Test]
    public void MusicLayersFollowTheWantedLevel()
    {
        foreach (var wave in new Func<float, float>[] { Sound.Bass, Sound.Drums(), Sound.Lead })
        {
            var samples = Sound.Samples(Sound.MusicSeconds, wave);
            Assert.AreEqual(0f, samples[0], 0.01f); // quiet at both ends: no click when it loops
            Assert.AreEqual(0f, samples[samples.Length - 1], 0.05f);
            Assert.Greater(Array.FindAll(samples, s => Mathf.Abs(s) > 0.05f).Length, samples.Length / 20);
        }
        Assert.Greater(Sound.MusicGain(0, 1, false), 0); // the menu has the bass line
        Assert.AreEqual(0f, Sound.MusicGain(1, 5, false));
        Assert.AreEqual(0f, Sound.MusicGain(1, 1, true));
        Assert.Greater(Sound.MusicGain(1, 2, true), 0);
        Assert.AreEqual(0f, Sound.MusicGain(2, 2, true));
        Assert.Greater(Sound.MusicGain(2, 3, true), 0);
        Assert.Greater(Sound.MusicGain(2, 5, true), Sound.MusicGain(2, 3, true));
    }

    [Test]
    public void ScoreQueueLimitAndRemove()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var queue = ScoreQueue.Load(path);
            for (var i = 0; i <= ScoreQueue.Max; i++)
                queue.Add(new QueuedRun { distance_m = i, played_at = "run" + i });
            Assert.AreEqual(ScoreQueue.Max, queue.runs.Count);
            Assert.AreEqual("run1", queue.runs[0].played_at); // oldest dropped
            Assert.IsTrue(queue.Save(path));

            var loaded = ScoreQueue.Load(path);
            Assert.AreEqual(ScoreQueue.Max, loaded.runs.Count);
            loaded.Remove("run1");
            Assert.IsTrue(loaded.Save(path));
            var after = ScoreQueue.Load(path);
            Assert.AreEqual(ScoreQueue.Max - 1, after.runs.Count);
            Assert.AreEqual(2, after.runs[0].distance_m);
        }
        finally { File.Delete(path); }
        }

        [Test]
        public void AuthSessionParsesExpiresAndRoundTrips()
        {
            Assert.IsNull(AuthSession.Parse("{\"access_token\":\"a\"}", 1000));
            Assert.IsNull(AuthSession.Parse("not json", 1000));
            var session = AuthSession.Parse("{\"access_token\":\"a\",\"refresh_token\":\"r\",\"expires_in\":3600}", 1000);
            Assert.AreEqual(4600, session.expires_at);
            Assert.IsFalse(session.NeedsRefresh(4539));
            Assert.IsTrue(session.NeedsRefresh(4540));
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Assert.IsTrue(session.Save(path));
                Assert.IsTrue(session.Save(path)); // overwrite
                Assert.AreEqual("r", AuthSession.Load(path).refresh_token);
                Assert.AreEqual(4600, AuthSession.Load(path).expires_at);
            }
            finally { File.Delete(path); }
            Assert.IsTrue(AuthSession.IsRejected(400) && AuthSession.IsRejected(401) && AuthSession.IsRejected(403));
            Assert.IsFalse(AuthSession.IsRejected(0) || AuthSession.IsRejected(429) || AuthSession.IsRejected(500));
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Auth.HashNonce("abc"));
        }

    [Test]
    public void PoliceRouteFollowsTheRoadGrid()
    {
        Assert.AreEqual(new Vector2Int(2, 2), PoliceChase.Node(Vector3.zero));
        Assert.AreEqual(new Vector2Int(0, 4), PoliceChase.Node(new Vector3(-500, 0, 500))); // clamped to the grid
        Assert.AreEqual(Vector3.zero, PoliceChase.Centre(new Vector2Int(2, 2)));
        // Larger gap first, one crossroads at a time, never diagonal.
        Assert.AreEqual(new Vector2Int(1, 0), PoliceChase.Step(new Vector2Int(0, 0), new Vector2Int(3, 1)));
        Assert.AreEqual(new Vector2Int(0, 1), PoliceChase.Step(new Vector2Int(0, 0), new Vector2Int(1, 3)));
        Assert.AreEqual(new Vector2Int(2, 2), PoliceChase.Step(new Vector2Int(2, 2), new Vector2Int(2, 2)));
        var at = new Vector2Int(0, 0);
        for (int i = 0; i < 8; i++) at = PoliceChase.Step(at, new Vector2Int(4, 4));
        Assert.AreEqual(new Vector2Int(4, 4), at);
    }

    [Test]
    public void HealthGoesDownOnceAndRestores()
    {
        var go = new GameObject("health test");
        try
        {
            var h = go.AddComponent<Health>();
            int downs = 0;
            h.Downed += () => downs++;
            h.TakeDamage(-5);
            Assert.AreEqual(100, h.Current, "negative damage heals nothing");
            h.TakeDamage(60);
            Assert.AreEqual(40, h.Current);
            h.TakeDamage(60);
            h.TakeDamage(60);
            Assert.AreEqual(0, h.Current);
            Assert.IsTrue(h.Down);
            Assert.AreEqual(1, downs);
            h.Restore();
            Assert.IsFalse(h.Down);
            var gun = WeaponData.PlayerPistol();
            Assert.Greater(gun.magazine, 0);
            Assert.That(WeaponData.SuspectPistol().damage, Is.LessThan(gun.damage), "suspects hit softer than the player");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void MissionsWonMergeAndFleeingPicksTheFarRoad()
    {
        var local = new SaveData();
        local.missionsWon.Add("a");
        var cloud = new SaveData();
        cloud.missionsWon.AddRange(new[] { "a", "b" });
        local.Merge(cloud);
        CollectionAssert.AreEquivalent(new[] { "a", "b" }, local.missionsWon);
        local.Merge(new SaveData { missionsWon = null });
        Assert.AreEqual(2, local.missionsWon.Count);

        // Pursuer to the south: run north, never straight back the way it came.
        var pursuer = PoliceChase.Centre(new Vector2Int(2, 0));
        Assert.AreEqual(new Vector2Int(2, 3), FleeDriver.Choose(new Vector2Int(2, 2), new Vector2Int(2, 1), pursuer));
        Assert.AreNotEqual(new Vector2Int(2, 3), FleeDriver.Choose(new Vector2Int(2, 4), new Vector2Int(2, 3), pursuer));
        Assert.AreNotEqual(new Vector2Int(2, 5), FleeDriver.Choose(new Vector2Int(2, 4), new Vector2Int(2, 3), pursuer), "stays on the grid");
    }
}
