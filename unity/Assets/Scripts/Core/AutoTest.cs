using System.Collections;
using System.Collections.Generic;
using PolicePursuit.Gameplay;
using UnityEngine;

namespace PolicePursuit.Core
{
    // Plays the game by itself on a device and logs one line per check, read by tools/android_autotest.py
    // (emulator) and usable as an Android "game loop" (Firebase Test Lab). Lines start with "AUTOTEST":
    //   AUTOTEST PASS|FAIL <check> <detail>   AUTOTEST SHOT <name> (the runner takes a screenshot)
    //   AUTOTEST PERF ...   AUTOTEST DONE passed=N failed=M
    // Inert unless started: only builds with the PP_AUTOTEST define (the CI test APK) start it, and only when
    // Android launches them for a test (see AutoTestLaunch below). PlayMode tests run it in the editor too.
    public class AutoTest : MonoBehaviour
    {
        public const string Tag = "AUTOTEST";
        public const float ShotPause = 1.5f; // real seconds held still for a screenshot when nobody answers
        public const float AckWait = 20;     // most real seconds to wait for the runner's "taken" file
        public int Passed { get; private set; }
        public int Failed { get; private set; }
        public bool Done { get; private set; }
        public bool QuitWhenDone { get; set; }
        public bool Shots { get; set; } = true;
        // Set by the runner's launch: after each SHOT line the game freezes until this file appears (the runner has
        // taken the screenshot), so the picture shows the moment of the check even on a slow device.
        public string AckFile { get; set; }
        readonly List<float> frames = new List<float>(4096);
        int errors;
        bool measuring;

        public static AutoTest Run(bool quitWhenDone, bool shots = true)
        {
            var go = new GameObject("AutoTest");
            DontDestroyOnLoad(go);
            var test = go.AddComponent<AutoTest>();
            test.QuitWhenDone = quitWhenDone;
            test.Shots = shots;
            return test;
        }

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;

        // Any error or exception the game logs while the test runs fails it.
        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log || type == LogType.Warning || message.StartsWith(Tag)) return;
            errors++;
            Debug.Log($"{Tag} LOGGED {type}: {message.Split('\n')[0]}");
        }

        void Update()
        {
            if (measuring) frames.Add(Time.unscaledDeltaTime);
        }

        bool Check(string name, bool ok, string detail = "")
        {
            if (ok) Passed++; else Failed++;
            Debug.Log($"{Tag} {(ok ? "PASS" : "FAIL")} {name} {detail}");
            return ok;
        }

        IEnumerator Shot(string name)
        {
            if (!Shots) yield break;
            if (AckFile == null)
            {
                Debug.Log($"{Tag} SHOT {name}");
                yield return new WaitForSecondsRealtime(ShotPause);
                yield break;
            }
            TryDelete(AckFile);
            float scale = Time.timeScale;
            Time.timeScale = 0; // nothing moves until the picture is taken
            Debug.Log($"{Tag} SHOT {name}");
            float until = Time.realtimeSinceStartup + AckWait;
            while (!System.IO.File.Exists(AckFile) && Time.realtimeSinceStartup < until) yield return null;
            if (!System.IO.File.Exists(AckFile)) Debug.Log($"{Tag} NOACK {name}");
            TryDelete(AckFile);
            Time.timeScale = scale;
        }

        static void TryDelete(string path)
        {
            try { System.IO.File.Delete(path); }
            catch (System.Exception) { } // a file the runner made may not be ours to delete; it is overwritten next time
        }

        static IEnumerator Hold(VehicleController car, float pedal, float steer, bool handbrake, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                car.Pedal = pedal;
                car.Steer = steer;
                car.Handbrake = handbrake;
                yield return null;
            }
            car.Steer = 0;
            car.Handbrake = false;
        }

        IEnumerator Start()
        {
            Debug.Log($"{Tag} START {Application.version} {SystemInfo.deviceModel} {SystemInfo.graphicsDeviceName} {Screen.width}x{Screen.height}");
            float until = Time.realtimeSinceStartup + 60;
            while (GameManager.Instance == null && Time.realtimeSinceStartup < until) yield return null;
            var game = GameManager.Instance;
            if (!Check("boot", game != null, "the game started")) { Finish(); yield break; }
            Check("app-name", Application.productName == "Police Pursuit", $"'{Application.productName}' by '{Application.companyName}'");
            yield return new WaitForSecondsRealtime(2);
            yield return Shot("menu");

            // Free roam: drive, brake, slide.
            game.ToMenu();
            game.StartFreeRoam();
            yield return new WaitForSeconds(1);
            var world = game.World;
            if (!Check("free-roam", world != null && game.State == GameState.FreeRoam)) { Finish(); yield break; }
            measuring = true;
            world.Police.Target = null; // no bust in the middle of the checks
            var car = world.Car;
            yield return Shot("free-roam");
            yield return Hold(car, 1, 0, false, 3);
            Check("accelerates", car.Speed > 8, $"{car.SpeedKmh:F0} km/h after 3 s");
            yield return Shot("driving");
            yield return Hold(car, -1, 0, false, 0.4f);
            Check("brake-lights", car.BrakeLightsOn);
            yield return Hold(car, 1, 0, false, 1.5f);
            yield return Hold(car, 0, 1, true, 1.2f);
            Check("skid-marks", world.Tyres.Showing > 0, $"{world.Tyres.Showing} marks");
            yield return Shot("skid");

            // On foot: stop, get out, walk, get back in.
            for (float t = 0; t < 6 && Mathf.Abs(car.Speed) > 1; t += Time.deltaTime) { car.Pedal = -1; yield return null; }
            car.Pedal = 0;
            if (Check("exit-car", world.ExitCar()))
            {
                var start = world.Walker.transform.position;
                for (float t = 0; t < 2; t += Time.deltaTime) { world.Walker.Move = Vector2.up; yield return null; }
                world.Walker.Move = Vector2.zero;
                Check("walks", Vector3.Distance(world.Walker.transform.position, start) > 1);
                yield return Shot("on-foot");

                int ammo = world.Gun.Ammo;
                for (float t = 0; t < 1 && world.Gun.Ammo == ammo; t += Time.deltaTime) { world.Shoot(); yield return null; }
                Check("shoots", world.Gun.Ammo < ammo);
                world.Melee.LightAttack();
                yield return new WaitForSeconds(1);
                yield return Shot("combat");
                world.Walker.Place(car.transform.position - car.transform.right * 2f, 0);
                yield return null;
                Check("enter-car", world.EnterCar());
            }

            // A hard crash into the north wall (x = 0 is a road running up to it).
            car.Place(new Vector3(0, 0.6f, OpenWorld.Edge - 12), 0);
            yield return new WaitForFixedUpdate();
            car.Body.linearVelocity = car.transform.forward * 20;
            for (float t = 0; t < 1.5f; t += Time.deltaTime) { car.Pedal = 0; yield return null; }
            Check("crash-damage", car.Condition < VehicleController.MaxCondition, $"condition {car.Condition:F0}%");
            yield return Shot("crash");
            world.ResetCar();

            // Midnight Prison Break, police side: the escapee drives off and your siren works.
            game.StartMission(Mission.Role.Police);
            yield return new WaitForSeconds(3);
            world = game.World;
            Check("mission-start", world.Mission != null && world.Fleer.gameObject.activeSelf);
            world.ToggleSiren();
            yield return new WaitForSeconds(1);
            Check("siren", world.SirenOn);
            yield return Shot("mission");
            game.ToMenu();
            measuring = false;

            Check("no-errors", errors == 0, $"{errors} errors or exceptions logged");
            Finish();
        }

        void Finish()
        {
            if (frames.Count > 0)
            {
                var sorted = new List<float>(frames);
                sorted.Sort();
                float avg = 0;
                foreach (var f in frames) avg += f;
                avg /= frames.Count;
                float slow = sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.95f))]; // 95th-percentile frame
                Debug.Log($"{Tag} PERF fps_avg={1 / avg:F1} fps_p5={1 / slow:F1} frames={frames.Count} " +
                          $"memory_mb={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576}");
            }
            Done = true;
            Debug.Log($"{Tag} DONE passed={Passed} failed={Failed}");
            if (QuitWhenDone) Application.Quit();
        }
    }

#if PP_AUTOTEST && UNITY_ANDROID && !UNITY_EDITOR
    // Starts the autotest when Android launches the app as a game loop (Firebase Test Lab) or with the
    // "autotest" extra (tools/android_autotest.py). Only in builds made with the PP_AUTOTEST define.
    static class AutoTestLaunch
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Check()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
            {
                if (intent == null) return;
                bool loop = intent.Call<string>("getAction") == "com.google.intent.action.TEST_LOOP";
                if (!loop && !intent.Call<bool>("hasExtra", "autotest")) return;
                var test = AutoTest.Run(quitWhenDone: true);
                if (intent.Call<string>("getStringExtra", "autotest") == "ack") // the runner answers every SHOT
                    test.AckFile = System.IO.Path.Combine(Application.persistentDataPath, "autotest-ack");
            }
        }
    }
#endif
}
