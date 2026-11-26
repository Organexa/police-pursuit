using System;
using UnityEngine;
using UnityEngine.Android;

namespace PolicePursuit.Core
{
    // FCM through Plugins/Android/Push.androidlib: subscribes to topic `all` and hands the device token to CloudSave.
    public class Push : MonoBehaviour
    {
        const string JavaClass = "com.policepursuit.push.Push";
        const string Permission13 = "android.permission.POST_NOTIFICATIONS";
        const string AskedKey = "push_asked";

        CloudSave cloud;
        volatile string token; // written by the Java thread, read in Update
        bool handed;

        public void Init(CloudSave save)
        {
            cloud = save;
            if (Application.platform != RuntimePlatform.Android) return;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var java = new AndroidJavaClass(JavaClass))
                    java.CallStatic("start", activity, new Callback(this));
            }
            catch (Exception e) { Debug.LogWarning("Push not started: " + e.Message); }
        }

        // A token means push is set up in this build. Signed-in players store it; guests only get topic messages.
        void Update()
        {
            if (token == null) return;
            if (!handed)
            {
                handed = true;
                cloud.SetPushToken(token);
            }
            AskPermission();
        }

        // Android 13+: ask once, on the menu, after the tutorial. "No" changes nothing else.
        void AskPermission()
        {
            if (GameManager.Instance.State != GameState.Menu || PlayerPrefs.HasKey(AskedKey)) return;
            if (!SaveData.Load(SaveData.DefaultPath).tutorialDone) return;
            PlayerPrefs.SetInt(AskedKey, 1);
            PlayerPrefs.Save();
            if (!Permission.HasUserAuthorizedPermission(Permission13)) Permission.RequestUserPermission(Permission13);
        }

        class Callback : AndroidJavaProxy
        {
            readonly Push push;

            public Callback(Push push) : base(JavaClass + "$Callback") => this.push = push;

            // Method name must match the Java interface.
            void onToken(string token) => push.token = token;
        }
    }
}
