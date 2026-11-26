using System;
using UnityEngine;
using UnityEngine.Networking;

namespace PolicePursuit.Core
{
    public enum UpdateLevel { None, Optional, Critical }

    // Reads the public app_config row once at launch. Offline or a bad answer means no notice.
    public static class UpdateCheck
    {
        public const string PackageName = "com.policepursuit.game";
        public const string StoreUrl = "https://play.google.com/store/apps/details?id=" + PackageName;

        public static void Fetch(Action<UpdateLevel, string> done)
        {
            if (!Auth.Configured) return;
            var request = Auth.Request("GET",
                "/rest/v1/app_config?select=latest_version,critical_min_version,message&id=eq.1");
            request.SendWebRequest().completed += _ =>
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    var level = Evaluate(request.downloadHandler.text, Application.version, out var message);
                    if (level != UpdateLevel.None) done(level, message);
                }
                request.Dispose();
            };
        }

        // Numbers, not text: 1.10.0 is newer than 1.9.3. Anything unreadable is not newer, so a typo blocks nobody.
        public static bool Newer(string candidate, string installed) =>
            Version.TryParse(candidate, out var c) && Version.TryParse(installed, out var i) && c > i;

        [Serializable]
        class Rows
        {
            public Row[] rows;
        }

        [Serializable]
        class Row
        {
            public string latest_version, critical_min_version, message;
        }

        public static UpdateLevel Evaluate(string json, string installed, out string message)
        {
            message = "";
            if (json == null || !json.TrimStart().StartsWith("[")) return UpdateLevel.None;
            try
            {
                var rows = JsonUtility.FromJson<Rows>("{\"rows\":" + json + "}").rows;
                if (rows.Length == 0) return UpdateLevel.None;
                message = rows[0].message ?? "";
                return Newer(rows[0].critical_min_version, installed) ? UpdateLevel.Critical
                    : Newer(rows[0].latest_version, installed) ? UpdateLevel.Optional
                    : UpdateLevel.None;
            }
            catch (ArgumentException) { return UpdateLevel.None; }
        }
    }
}
