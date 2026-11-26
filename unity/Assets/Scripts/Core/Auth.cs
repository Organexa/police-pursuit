using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PolicePursuit.Core
{
    [Serializable]
    public class AuthSession
    {
        public string access_token, refresh_token;
        public long expires_at, expires_in; // unix seconds, seconds
        public AuthUser user;

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "auth.json");

        public bool SignedIn => !string.IsNullOrEmpty(refresh_token);

        public bool NeedsRefresh(long now) => now >= expires_at - 60;

        // The server said the refresh token is no good; anything else (offline, 5xx, 429) keeps the session.
        public static bool IsRejected(long status) => status == 400 || status == 401 || status == 403;

        // Reads a Supabase token response (or our saved file); null unless both tokens are there.
        public static AuthSession Parse(string json, long now)
        {
            try
            {
                var s = JsonUtility.FromJson<AuthSession>(json);
                if (s == null || string.IsNullOrEmpty(s.access_token) || !s.SignedIn) return null;
                if (s.expires_at == 0) s.expires_at = now + s.expires_in;
                return s;
            }
            catch (ArgumentException) { return null; }
        }

        public static AuthSession Load(string path)
        {
            try { return File.Exists(path) ? Parse(File.ReadAllText(path), 0) : null; }
            catch (Exception e)
            {
                Debug.LogWarning("Session not loaded: " + e.Message);
                return null;
            }
        }

        // shortcut: plain text in app-private storage (Android auto-backup may copy it);
        // move to the Android Keystore if the account ever guards more than scores and cloud save.
        public bool Save(string path)
        {
            try
            {
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(this));
                if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
                else File.Move(path + ".tmp", path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Session not saved: " + e.Message);
                return false;
            }
        }
    }

    [Serializable]
    public class AuthUser
    {
        public string id;
    }

    // Optional Google sign-in: Android account picker -> Google ID token -> Supabase session.
    public class Auth : MonoBehaviour
    {
        // Public values, safe in the game (never a client secret or service role key).
        // While any is empty, sign-in is hidden and the rest of the game is unaffected.
        static readonly string SupabaseUrl = "https://dumwtlqllfyhbdxglsvc.supabase.co";
        static readonly string AnonKey =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImR1bXd0bHFsbGZ5aGJkeGdsc3ZjIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTU3MDgzOTIsImV4cCI6MjExMTI4NDM5Mn0.wV3hoyQmP4SRv2szu3bkzo-oOHTgJZI3e5r4Sh3weig";
        static readonly string GoogleWebClientId =
            "949505289565-an3u02lbbr4b8i4d6qv5q5sqthqq3v7p.apps.googleusercontent.com";

        const string JavaClass = "com.policepursuit.signin.GoogleSignIn";

        // Enough for public reads (leaderboard); sign-in also needs Android and the Google client ID.
        public static bool Configured => SupabaseUrl != "" && AnonKey != "";
        public bool Available => Application.platform == RuntimePlatform.Android
            && Configured && GoogleWebClientId != "";
        public bool Busy { get; private set; }
        public bool Failed { get; private set; }
        public AuthSession Session { get; private set; }
        public bool SignedIn => Session != null;

        // Written by the Java thread, read in Update.
        volatile bool hasResult;
        string idToken, error, nonce;
        float retryAt;

        static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static string HashNonce(string raw)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)))
                    .Replace("-", "").ToLowerInvariant();
        }

        void Awake() => Session = AuthSession.Load(AuthSession.DefaultPath);

        // Covers game start, return from background and expiry during play.
        void Update()
        {
            if (hasResult)
            {
                hasResult = false;
                OnGoogle();
            }
            if (Available && !Busy && SignedIn && Session.NeedsRefresh(Now) && Time.unscaledTime >= retryAt)
                Refresh();
        }

        public void SignIn()
        {
            if (!Available || Busy) return;
            Busy = true;
            Failed = false;
            // Google gets the hash, Supabase gets the raw value, so a stolen ID token cannot be replayed.
            nonce = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            try
            {
                using (var java = new AndroidJavaClass(JavaClass))
                    java.CallStatic("signIn", Activity(), GoogleWebClientId, HashNonce(nonce), new Callback(this));
            }
            catch (Exception e) { Fail(e.Message); }
        }

        public void SignOut()
        {
            if (!SignedIn || Busy) return;
            if (Available)
            {
                var request = Request("POST", "/auth/v1/logout?scope=local", "{}");
                request.SetRequestHeader("Authorization", "Bearer " + Session.access_token);
                request.SendWebRequest().completed += _ => request.Dispose(); // best effort
                ForgetGoogleAccount();
            }
            Forget();
        }

        // Removes every online row of this account, then the local session and waiting uploads.
        // Coins and cars on the phone stay.
        public void DeleteAccount(Action<bool> done)
        {
            if (!Available || !SignedIn || Busy) { done(false); return; }
            Busy = true; // also pauses CloudSave and token refresh
            var request = Request("POST", "/functions/v1/delete-account", "{}");
            request.SetRequestHeader("Authorization", "Bearer " + Session.access_token);
            request.SendWebRequest().completed += _ =>
            {
                bool ok = request.result == UnityWebRequest.Result.Success;
                if (ok)
                {
                    ForgetGoogleAccount();
                    Forget();
                    try { File.Delete(ScoreQueue.DefaultPath); }
                    catch (Exception e) { Debug.LogWarning("Score queue not deleted: " + e.Message); }
                }
                else Debug.LogWarning("Account not deleted: HTTP " + request.responseCode);
                Busy = false;
                request.Dispose();
                done(ok);
            };
        }

        static void ForgetGoogleAccount()
        {
            try
            {
                using (var java = new AndroidJavaClass(JavaClass)) java.CallStatic("signOut", Activity());
            }
            catch (Exception e) { Debug.LogWarning("Google sign-out failed: " + e.Message); }
        }

        void OnGoogle()
        {
            if (!string.IsNullOrEmpty(error)) { Fail(error); return; }
            if (string.IsNullOrEmpty(idToken)) { Busy = false; return; } // cancelled: stay a guest
            var request = Request("POST", "/auth/v1/token?grant_type=id_token",
                JsonUtility.ToJson(new IdTokenBody { id_token = idToken, nonce = nonce }));
            idToken = null;
            request.SendWebRequest().completed += _ =>
            {
                var session = request.result == UnityWebRequest.Result.Success
                    ? AuthSession.Parse(request.downloadHandler.text, Now) : null;
                if (session == null) Fail("HTTP " + request.responseCode);
                else
                {
                    Busy = false;
                    Session = session;
                    session.Save(AuthSession.DefaultPath);
                }
                request.Dispose();
            };
        }

        void Refresh()
        {
            retryAt = Time.unscaledTime + 30;
            var old = Session;
            var request = Request("POST", "/auth/v1/token?grant_type=refresh_token",
                JsonUtility.ToJson(new RefreshBody { refresh_token = old.refresh_token }));
            request.SendWebRequest().completed += _ =>
            {
                if (Session == old) // signed out or in again meanwhile: drop this answer
                {
                    var session = request.result == UnityWebRequest.Result.Success
                        ? AuthSession.Parse(request.downloadHandler.text, Now) : null;
                    if (session != null)
                    {
                        Session = session;
                        session.Save(AuthSession.DefaultPath);
                    }
                    else if (AuthSession.IsRejected(request.responseCode)) Forget();
                }
                request.Dispose();
            };
        }

        void Forget()
        {
            Session = null;
            try { File.Delete(AuthSession.DefaultPath); }
            catch (Exception e) { Debug.LogWarning("Session not deleted: " + e.Message); }
        }

        void Fail(string reason)
        {
            Debug.LogWarning("Sign-in failed: " + reason);
            Busy = false;
            Failed = true;
        }

        // A Supabase request with the public key; add the Authorization header for the player's own rows.
        internal static UnityWebRequest Request(string method, string path, string json = null)
        {
            var request = new UnityWebRequest(SupabaseUrl + path, method)
            {
                uploadHandler = json == null ? null : new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 20
            };
            request.SetRequestHeader("apikey", AnonKey);
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        static AndroidJavaObject Activity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                return player.GetStatic<AndroidJavaObject>("currentActivity");
        }

        [Serializable]
        class IdTokenBody
        {
            public string provider = "google";
            public string id_token, nonce;
        }

        [Serializable]
        class RefreshBody
        {
            public string refresh_token;
        }

        class Callback : AndroidJavaProxy
        {
            readonly Auth auth;

            public Callback(Auth auth) : base(JavaClass + "$Callback") => this.auth = auth;

            // Method name must match the Java interface.
            void onResult(string idToken, string error)
            {
                auth.idToken = idToken;
                auth.error = error;
                auth.hasResult = true;
            }
        }
    }
}
