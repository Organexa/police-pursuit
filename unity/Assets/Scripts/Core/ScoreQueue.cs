using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PolicePursuit.Core
{
    // Field names match the submit-score payload (docs/TRD.md).
    [Serializable]
    public class QueuedRun
    {
        public int duration_s, distance_m, near_misses, max_wanted;
        public string car_id;
        public string played_at; // UTC, set once; the server uses it to ignore a retried upload
    }

    // Runs waiting to upload. CloudSave calls Remove once the server has stored or refused the run.
    [Serializable]
    public class ScoreQueue
    {
        public const int Max = 50;
        public List<QueuedRun> runs = new List<QueuedRun>();

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "score_queue.json");

        public void Add(QueuedRun run)
        {
            runs.Add(run);
            if (runs.Count > Max) runs.RemoveRange(0, runs.Count - Max);
        }

        public void Remove(string playedAt) => runs.RemoveAll(r => r.played_at == playedAt);

        public static ScoreQueue Load(string path)
        {
            try
            {
                if (File.Exists(path)) return JsonUtility.FromJson<ScoreQueue>(File.ReadAllText(path)) ?? new ScoreQueue();
            }
            catch (Exception e) { Debug.LogWarning("score_queue.json not read: " + e.Message); }
            return new ScoreQueue();
        }

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
            catch (Exception e) { Debug.LogWarning("score_queue.json not written: " + e.Message); }
            return false;
        }
    }
}
