using System;
using System.Text;
using UnityEngine.Networking;

namespace PolicePursuit.Core
{
    // Top 100 (public `leaderboard` view) and the signed-in player's rank (`my_rank()`).
    public static class Leaderboard
    {
        public const string Offline = "Can't connect";

        public static void FetchTop(Action<string> show)
        {
            if (!Auth.Configured) { show("Leaderboard is not set up yet"); return; }
            Send(Auth.Request("GET", "/rest/v1/leaderboard?select=rank,display_name,score&order=rank"),
                json => show(Format(json) ?? Offline));
        }

        public static void FetchRank(AuthSession session, Action<string> show)
        {
            if (!Auth.Configured) { show(""); return; }
            var request = Auth.Request("POST", "/rest/v1/rpc/my_rank", "{}");
            request.SetRequestHeader("Authorization", "Bearer " + session.access_token);
            Send(request, json => show(FormatRank(json) ?? Offline));
        }

        static void Send(UnityWebRequest request, Action<string> done)
        {
            request.SendWebRequest().completed += _ =>
            {
                done(request.result == UnityWebRequest.Result.Success ? request.downloadHandler.text : null);
                request.Dispose();
            };
        }

        [Serializable]
        class Rows
        {
            public Row[] rows;
        }

        [Serializable]
        class Row
        {
            public long rank, score;
            public string display_name;
        }

        // One line per player; null for anything that is not a row list.
        public static string Format(string json)
        {
            if (json == null || !json.TrimStart().StartsWith("[")) return null;
            try
            {
                var text = new StringBuilder();
                foreach (var r in UnityEngine.JsonUtility.FromJson<Rows>("{\"rows\":" + json + "}").rows)
                    // Names are typed by players: keep each on its own line.
                    text.Append(r.rank).Append(".  ").Append(r.display_name?.Replace('\n', ' ').Replace('\r', ' '))
                        .Append("   ").Append(r.score).Append('\n');
                return text.Length == 0 ? "No scores yet" : text.ToString().TrimEnd('\n');
            }
            catch (ArgumentException) { return null; }
        }

        // my_rank() answers a number, or null before the first score.
        public static string FormatRank(string json) =>
            json == null ? null
            : json.Trim() == "null" ? "No ranked run yet"
            : long.TryParse(json.Trim(), out var rank) ? "Your rank: #" + rank : null;
    }
}
