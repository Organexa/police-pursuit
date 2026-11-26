using System;
using System.Collections;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace PolicePursuit.Core
{
    public interface IAds
    {
        bool RewardedReady { get; } // false = no fill, so the button is hidden
        void ShowRewarded(Action<bool> done);
        bool InterstitialReady { get; }
        void ShowInterstitial();
    }

    // LevelPlay rewarded and interstitial ads. Nothing loads before the player has answered the consent choice.
    public class Ads : MonoBehaviour, IAds
    {
        // Public values from the LevelPlay dashboard. While any is empty, ads and the consent choice are hidden.
        static readonly string AppKey = "";
        static readonly string RewardedId = "";
        static readonly string InterstitialId = "";

        const float Retry = 30;

        public static bool Configured => Application.platform == RuntimePlatform.Android
            && AppKey != "" && RewardedId != "" && InterstitialId != "";

        public static bool InterstitialDue(int runsSinceLast) => runsSinceLast >= 3;

        LevelPlayRewardedAd rewarded;
        LevelPlayInterstitialAd interstitial;
        Action<bool> done;
        bool started, earned;

        public bool RewardedReady => rewarded != null && rewarded.IsAdReady();
        public bool InterstitialReady => interstitial != null && interstitial.IsAdReady();

        // Called at launch with the saved answer, and again when the player changes it.
        public void SetConsent(bool consent)
        {
            if (!Configured) return;
            LevelPlayPrivacySettings.SetGDPRConsent(consent);
            LevelPlayPrivacySettings.SetCCPA(!consent); // true = opted out of the sale of data
            if (started) return;
            started = true;
            LevelPlay.OnInitSuccess += _ => Create();
            LevelPlay.OnInitFailed += e =>
            {
                Debug.LogWarning("Ads not started: " + e);
                Invoke(nameof(StartSdk), Retry);
            };
            StartSdk();
        }

        void StartSdk() => LevelPlay.Init(AppKey);

        void Create()
        {
            if (rewarded != null) return;
            rewarded = new LevelPlayRewardedAd(RewardedId);
            rewarded.OnAdRewarded += (ad, reward) => earned = true;
            rewarded.OnAdClosed += _ => StartCoroutine(Closed());
            rewarded.OnAdDisplayFailed += (ad, error) =>
            {
                Complete(false);
                LoadRewarded();
            };
            rewarded.OnAdLoadFailed += _ => Invoke(nameof(LoadRewarded), Retry);

            interstitial = new LevelPlayInterstitialAd(InterstitialId);
            interstitial.OnAdClosed += _ => LoadInterstitial();
            interstitial.OnAdDisplayFailed += (ad, error) => LoadInterstitial();
            interstitial.OnAdLoadFailed += _ => Invoke(nameof(LoadInterstitial), Retry);

            LoadRewarded();
            LoadInterstitial();
        }

        void LoadRewarded() => rewarded.LoadAd();
        void LoadInterstitial() => interstitial.LoadAd();

        public void ShowRewarded(Action<bool> result)
        {
            if (done != null || !RewardedReady) return;
            done = result;
            earned = false;
            rewarded.ShowAd();
        }

        public void ShowInterstitial() => interstitial.ShowAd();

        // shortcut: the reward can arrive just after the close; one later than a second is lost.
        IEnumerator Closed()
        {
            if (!earned) yield return new WaitForSecondsRealtime(1);
            Complete(earned);
            LoadRewarded();
        }

        void Complete(bool result)
        {
            var callback = done;
            done = null;
            callback?.Invoke(result);
        }
    }
}
