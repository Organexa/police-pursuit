using System;
using PolicePursuit.Core;
using UnityEngine;
using Random = System.Random;

namespace PolicePursuit.Gameplay
{
    // Sound effects (DESIGN §9), synthesised so there are no audio files or licences.
    // The Settings "Sound" slider is the master volume; pausing the game pauses everything but button clicks.
    // shortcut: placeholder synth sounds and no music yet; swap for recorded clips with the art pass.
    public class Sound : MonoBehaviour
    {
        public const int Rate = 22050;
        const float SirenRange = 60f; // metres

        GameManager game;
        AudioSource engine, siren, effects, ui;
        AudioClip crash, coin, whoosh, click;

        public static float EnginePitch(float speed, float topSpeed) =>
            Mathf.Lerp(0.8f, 2f, topSpeed > 0 ? speed / topSpeed : 0);

        // Louder as the police get closer, silent beyond SirenRange.
        public static float SirenVolume(float distance) => Mathf.Clamp01(1 - distance / SirenRange);

        public void Init(GameManager manager)
        {
            game = manager;
            engine = Source(Clip("engine", 1f, Engine), true);
            siren = Source(Clip("siren", 1f, Siren), true);
            effects = Source(null, false);
            ui = Source(null, false);
            ui.ignoreListenerPause = true; // buttons on the pause screen still click

            crash = Clip("crash", 0.4f, Crash());
            coin = Clip("coin", 0.18f, Coin);
            whoosh = Clip("whoosh", 0.3f, Whoosh());
            click = Clip("click", 0.04f, t => Tone(660, t) * (1 - t / 0.04f));
        }

        public void Hit() => effects.PlayOneShot(crash, 0.8f);
        public void CoinPickup() => effects.PlayOneShot(coin, 0.5f);
        public void NearMiss() => effects.PlayOneShot(whoosh, 0.6f);
        public void Click() => ui.PlayOneShot(click, 0.5f);

        void Update()
        {
            if (game == null) return;
            bool driving = game.State == GameState.Run;
            var car = game.Player;
            engine.volume = driving ? 0.25f : 0;
            engine.pitch = EnginePitch(car.Speed, car.TopSpeed);
            siren.volume = driving ? 0.35f * SirenVolume(game.Traffic.NearestCop(car.transform.position)) : 0;
        }

        AudioSource Source(AudioClip loop, bool looping)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = loop;
            source.loop = looping;
            if (looping)
            {
                source.volume = 0;
                source.Play();
            }
            return source;
        }

        static AudioClip Clip(string name, float seconds, Func<float, float> wave)
        {
            var samples = Samples(seconds, wave);
            var clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static float[] Samples(float seconds, Func<float, float> wave)
        {
            var samples = new float[(int)(Rate * seconds)];
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(wave((float)i / Rate), -1, 1);
            return samples;
        }

        static float Tone(float hz, float t) => Mathf.Sin(2 * Mathf.PI * hz * t);

        // The two loops are one second long and use whole cycles per second, so they repeat without a click.
        public static float Engine(float t) => 0.5f * Tone(55, t) + 0.3f * Tone(110, t) + 0.2f * Tone(165, t);

        // 750 Hz swinging by 150 Hz once a second (the phase is the integral of that frequency).
        public static float Siren(float t) =>
            0.6f * Mathf.Sin(2 * Mathf.PI * 750 * t + 150 * (1 - Mathf.Cos(2 * Mathf.PI * t)));

        static float Coin(float t) => 0.6f * Tone(t < 0.06f ? 990 : 1320, t) * (1 - t / 0.18f);

        static Func<float, float> Crash()
        {
            var random = new Random(1);
            return t => (float)(random.NextDouble() * 2 - 1) * Mathf.Exp(-8 * t);
        }

        static Func<float, float> Whoosh()
        {
            var random = new Random(2);
            float smooth = 0;
            return t =>
            {
                smooth += 0.15f * ((float)(random.NextDouble() * 2 - 1) - smooth); // soften the hiss
                return 2 * smooth * Mathf.Sin(Mathf.PI * t / 0.3f);
            };
        }
    }
}
