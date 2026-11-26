using System;
using UnityEngine;
using UnityEngine.Networking;

namespace PolicePursuit.Core
{
    // Everything the signed-in player uploads: the `saves` row and the queued scores (submit-score).
    // Save: pull and merge once per sign-in (and app start), then push.
    // Nothing is pushed before a successful pull, so a new phone cannot overwrite the cloud with an empty save.
    public class CloudSave : MonoBehaviour
    {
        public event Action Merged;

        Auth auth;
        string mergedFor; // user id already pulled in this sign-in
        bool busy, dirty, scoresWaiting, tokenWaiting;
        string pushToken;
        float retryAt;

        public void Init(Auth a) => auth = a;

        // Call after save.json or the score queue changes (end of run, purchase).
        public void MarkDirty() => dirty = scoresWaiting = true;

        public void SetPushToken(string token)
        {
            pushToken = token;
            tokenWaiting = true;
        }

        void Update()
        {
            var session = auth == null || !auth.Available ? null : auth.Session;
            if (session == null) { mergedFor = null; return; }
            var id = session.user?.id;
            if (!Guid.TryParse(id, out _) || busy || auth.Busy || Time.unscaledTime < retryAt) return;
            if (session.NeedsRefresh(DateTimeOffset.UtcNow.ToUnixTimeSeconds())) return; // Auth refreshes it
            if (id != mergedFor) Pull(session, id);
            else if (dirty) Push(session, id);
            else if (scoresWaiting) UploadScore(session, id);
            else if (tokenWaiting && pushToken != null) UploadToken(session, id);
        }

        void Pull(AuthSession session, string id) =>
            Send(session, id, Auth.Request("GET", "/rest/v1/saves?select=data&user_id=eq." + id), response =>
            {
                if (response.result != UnityWebRequest.Result.Success
                    || !TryParseRows(response.downloadHandler.text, out var cloud)) return false;
                var local = SaveData.Load(SaveData.DefaultPath);
                local.Merge(cloud);
                if (!local.Save(SaveData.DefaultPath)) return false;
                mergedFor = id;
                MarkDirty();
                tokenWaiting = true; // a new account on this phone needs the token too
                Merged?.Invoke();
                return true;
            });

        void Push(AuthSession session, string id)
        {
            dirty = false; // a change made while uploading sets it again
            var data = JsonUtility.ToJson(SaveData.Load(SaveData.DefaultPath));
            var request = Auth.Request("POST", "/rest/v1/saves?on_conflict=user_id",
                "{\"user_id\":\"" + id + "\",\"updated_at\":\"now\",\"data\":" + data + "}");
            request.SetRequestHeader("Prefer", "resolution=merge-duplicates");
            Send(session, id, request, r => r.result == UnityWebRequest.Result.Success, () => dirty = true);
        }

        [Serializable]
        class TokenRow
        {
            public string user_id, token, updated_at = "now";
        }

        void UploadToken(AuthSession session, string id)
        {
            tokenWaiting = false;
            var request = Auth.Request("POST", "/rest/v1/push_tokens?on_conflict=user_id",
                JsonUtility.ToJson(new TokenRow { user_id = id, token = pushToken }));
            request.SetRequestHeader("Prefer", "resolution=merge-duplicates");
            Send(session, id, request, r => r.result == UnityWebRequest.Result.Success, () => tokenWaiting = true);
        }

        // Oldest first, one at a time.
        void UploadScore(AuthSession session, string id)
        {
            var runs = ScoreQueue.Load(ScoreQueue.DefaultPath).runs;
            if (runs.Count == 0) { scoresWaiting = false; return; }
            var run = runs[0];
            var request = Auth.Request("POST", "/functions/v1/submit-score", JsonUtility.ToJson(run));
            Send(session, id, request, r =>
            {
                if (!ScoreDone(r.responseCode, r.downloadHandler.text)) return false;
                var queue = ScoreQueue.Load(ScoreQueue.DefaultPath); // a run may have been added meanwhile
                queue.Remove(run.played_at);
                return queue.Save(ScoreQueue.DefaultPath);
            });
        }

        [Serializable]
        class SubmitReply
        {
            public bool ok;
            public string reason;
        }

        // True when the run must leave the queue: stored, or refused for good. Offline, 401, 429 and 5xx keep it.
        public static bool ScoreDone(long status, string body)
        {
            if (status != 200 && status != 400) return false;
            try
            {
                var reply = JsonUtility.FromJson<SubmitReply>(body);
                return reply != null && (status == 200 ? reply.ok : !string.IsNullOrEmpty(reply.reason));
            }
            catch (ArgumentException) { return false; }
        }

        // `handle` returns false to have the request retried later.
        void Send(AuthSession session, string id, UnityWebRequest request, Func<UnityWebRequest, bool> handle,
            Action onFail = null)
        {
            busy = true;
            request.SetRequestHeader("Authorization", "Bearer " + session.access_token);
            request.SendWebRequest().completed += _ =>
            {
                busy = false;
                // Signed out or switched account meanwhile: drop this answer.
                if (auth.Session?.user?.id == id && !handle(request))
                {
                    Debug.LogWarning("Upload failed: HTTP " + request.responseCode);
                    retryAt = Time.unscaledTime + 30;
                    onFail?.Invoke();
                }
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
            public SaveData data;
        }

        // False for anything that is not a row list; true with a null save when the player has none yet.
        public static bool TryParseRows(string json, out SaveData cloud)
        {
            cloud = null;
            if (json == null || !json.TrimStart().StartsWith("[")) return false;
            try
            {
                var rows = JsonUtility.FromJson<Rows>("{\"rows\":" + json + "}").rows;
                if (rows.Length > 0) cloud = rows[0].data;
                return true;
            }
            catch (ArgumentException) { return false; }
        }
    }
}
