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
        var renderers = GameManager.Instance.GetComponentsInChildren<Renderer>();
        Assert.IsNotEmpty(renderers);
        foreach (var r in renderers)
            Assert.AreEqual("PolicePursuit/Flat", r.sharedMaterial.shader.name, r.name);
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
}
