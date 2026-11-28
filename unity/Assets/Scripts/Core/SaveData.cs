using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PolicePursuit.Core
{
    // save.json in app private storage. A missing or broken file gives the defaults.
    [Serializable]
    public class SaveData
    {
        public bool tutorialDone;
        public int coins;
        public long bestScore;
        public List<string> unlockedCars = new List<string>();
        public string selectedCar = "starter";

        // Settings. Initializers cover saves written before these existed.
        public float soundVolume = 1, musicVolume = 1, tiltSensitivity = 1;
        public bool tiltControls;
        public bool haptics = true;
        public bool leftHanded;        // open-world controls mirrored: stick on the right, buttons on the left
        public float buttonScale = 1;  // open-world control size, 0.8 to 1.3

        // Ads. These stay on this phone (not merged from the cloud).
        public int adConsent; // 0 not asked, 1 allowed, -1 refused
        public int runsSinceAd;
        public bool adsRemoved; // set by the "Remove ads" purchase (F10)
        public List<string> delivered = new List<string>(); // coin-pack transactions already added
        public List<string> missionsWon = new List<string>(); // mission id + "_" + side, e.g. midnight_prison_break_police

        public bool Owns(Car car) => car == CarList.All[0] || unlockedCars.Contains(car.Id);

        // Premium cars are never sold for coins (IAP, F10).
        public bool TryUnlock(Car car)
        {
            if (Owns(car) || car.Premium || coins < car.Price) return false;
            coins -= car.Price;
            unlockedCars.Add(car.Id);
            return true;
        }

        public bool Select(Car car)
        {
            if (!Owns(car)) return false;
            selectedCar = car.Id;
            return true;
        }

        // Cloud save rules: coins = max, best = max, cars and won missions = union, everything else stays local.
        // shortcut: max can give back coins spent while offline; that only affects the player's own game (TRD §5).
        public void Merge(SaveData cloud)
        {
            if (cloud == null) return;
            coins = Math.Max(coins, cloud.coins);
            bestScore = Math.Max(bestScore, cloud.bestScore);
            if (cloud.missionsWon != null)
                foreach (var id in cloud.missionsWon)
                    if (!missionsWon.Contains(id)) missionsWon.Add(id);
            if (cloud.unlockedCars == null) return;
            foreach (var id in cloud.unlockedCars)
                if (!unlockedCars.Contains(id)) unlockedCars.Add(id);
        }

        // Coins and best score used to live in PlayerPrefs; move them over once so none are lost.
        public static void MigratePrefs(string path)
        {
            const string oldCoins = "coins_total", oldBest = "best_score";
            if (!PlayerPrefs.HasKey(oldCoins) && !PlayerPrefs.HasKey(oldBest)) return;
            var save = Load(path);
            save.coins += PlayerPrefs.GetInt(oldCoins, 0);
            long.TryParse(PlayerPrefs.GetString(oldBest, "0"), out var best);
            save.bestScore = Math.Max(save.bestScore, best);
            if (!save.Save(path)) return; // keep the old values and retry next launch
            PlayerPrefs.DeleteKey(oldCoins);
            PlayerPrefs.DeleteKey(oldBest);
            PlayerPrefs.Save();
        }

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "save.json");

        public static SaveData Load(string path)
        {
            try
            {
                if (File.Exists(path)) return JsonUtility.FromJson<SaveData>(File.ReadAllText(path)) ?? new SaveData();
            }
            catch (Exception e) { Debug.LogWarning("save.json not read: " + e.Message); }
            return new SaveData();
        }

        // Written to a temp file first, so a crash cannot leave half a save.
        public bool Save(string path)
        {
            try
            {
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(this));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("save.json not written: " + e.Message); }
            return false;
        }
    }
}
