using System;
using PolicePursuit.Core;
using UnityEngine;
using Random = System.Random;

namespace PolicePursuit.Gameplay
{
    // Sound effects and music (DESIGN §9), synthesised so there are no audio files or licences.
    // "Sound" (the listener volume) covers effects, "Music" the music; pausing the game pauses all but button clicks.
    // shortcut: placeholder synth sounds and one 8-second tune; swap for recorded tracks with the art pass.
    public class Sound : MonoBehaviour
    {
        public const int Rate = 22050;
        public const float MusicSeconds = 8; // four bars at 120 beats a minute
        const float SirenRange = 60f; // metres

        GameManager game;
        AudioSource engine, siren, effects, ui;
        AudioClip crash, coin, whoosh, click, horn, bark, splash;
        readonly AudioSource[] music = new AudioSource[3];

        // Layer 0 (bass and kick) always plays; drums join at two stars and the lead at three, only while driving.
        public static float MusicGain(int layer, int wanted, bool driving) =>
            layer == 0 ? 0.35f
            : !driving ? 0
            : layer == 1 ? (wanted >= 2 ? 0.25f : 0)
            : wanted >= 3 ? 0.12f + 0.04f * (wanted - 3) : 0;

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

            var layers = new[] { Clip("music-bass", MusicSeconds, Bass), Clip("music-drums", MusicSeconds, Drums()),
                Clip("music-lead", MusicSeconds, Lead) };
            // Same length and started in the same frame, so they stay in step.
            for (int i = 0; i < music.Length; i++)
            {
                music[i] = Source(layers[i], true);
                music[i].ignoreListenerVolume = true; // has its own slider
            }

            crash = Clip("crash", 0.4f, Crash());
            coin = Clip("coin", 0.18f, Coin);
            whoosh = Clip("whoosh", 0.3f, Whoosh());
            click = Clip("click", 0.04f, t => Tone(660, t) * (1 - t / 0.04f));
            // Two-note car horn, faded in and out so it does not click.
            // Two short gruff yaps: a falling tone roughened with noise.
            var yap = new Random(3);
            bark = Clip("bark", 0.32f, t =>
            {
                float local = t % 0.16f;
                float pitch = Mathf.Lerp(520, 260, local / 0.16f);
                return (Tone(pitch, local) * 0.6f + (float)(yap.NextDouble() * 2 - 1) * 0.4f) * Mathf.Exp(-14 * local);
            });
            var drops = new Random(4);
            splash = Clip("splash", 0.35f, t => (float)(drops.NextDouble() * 2 - 1) * 0.5f * Mathf.Exp(-9 * t) * Mathf.Min(1, t * 60));
            horn = Clip("horn", 0.45f, t => (Tone(415, t) + Tone(523, t)) * 0.35f * Mathf.Min(1, t * 40, (0.45f - t) * 20));
        }

        public void Hit() => effects.PlayOneShot(crash, 0.8f);
        public void CoinPickup() => effects.PlayOneShot(coin, 0.5f);
        public void NearMiss() => effects.PlayOneShot(whoosh, 0.6f);
        public void Click() => ui.PlayOneShot(click, 0.5f);
        public void Horn() => effects.PlayOneShot(horn, 0.7f);
        public void Bark() => effects.PlayOneShot(bark, 0.6f);
        public void Splash() => effects.PlayOneShot(splash, 0.5f);

        void Update()
        {
            if (game == null) return;
            bool driving = game.State == GameState.Run;
            var car = game.Player;
            engine.volume = driving ? 0.25f : 0;
            engine.pitch = EnginePitch(car.Speed, car.TopSpeed);
            float sirenAt = driving ? game.Traffic.NearestCop(car.transform.position) : float.MaxValue;
            var world = game.State == GameState.FreeRoam ? game.World : null;
            if (world != null) // your own siren is right on top of you; the chaser's fades with distance
                sirenAt = world.SirenOn ? 0 : world.Police.gameObject.activeSelf ? world.PoliceDistance : float.MaxValue;
            siren.volume = 0.35f * SirenVolume(sirenAt);
            for (int i = 0; i < music.Length; i++)
                music[i].volume = game.Settings.musicVolume * MusicGain(i, game.Wanted, driving);
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

        // A minor, F, C, G: one chord per two-second bar (root in Hz, semitones to its third).
        static readonly (float root, int third)[] Chords = { (110f, 3), (87.31f, 4), (130.81f, 4), (98f, 4) };

        static (float root, int third) Chord(float t) => Chords[(int)(t / 2) % Chords.Length];

        // Every note fades in and out inside its own slot, so neither notes nor the loop point click.
        static float Note(float t, float length)
        {
            float into = t % length;
            return Mathf.Min(1, into / 0.005f) * (1 - into / length);
        }

        // Eighth-note bass with a kick drum on every beat.
        public static float Bass(float t)
        {
            float root = Chord(t).root, into = t % 0.25f, beat = t % 0.5f;
            float bass = (Tone(root, into) + 0.5f * Tone(2 * root, into) + 0.25f * Tone(3 * root, into)) * Note(t, 0.25f);
            // The pitch drops from 120 Hz to 50 Hz: the phase is the integral of that fall.
            float kick = Mathf.Sin(2 * Mathf.PI * (50 * beat + 70 * (1 - Mathf.Exp(-30 * beat)) / 30)) * Note(t, 0.5f);
            return 0.35f * bass + 0.6f * kick * Mathf.Exp(-10 * beat);
        }

        // Snare on beats two and four, hi-hat between the beats.
        public static Func<float, float> Drums()
        {
            var random = new Random(3);
            return t =>
            {
                float noise = (float)(random.NextDouble() * 2 - 1);
                float snare = (t + 0.5f) % 1f, hat = (t + 0.25f) % 0.5f;
                return noise * (0.6f * Mathf.Exp(-18 * snare) + 0.35f * Mathf.Exp(-60 * hat)) * Note(t, 0.25f);
            };
        }

        // Sixteenth-note arpeggio two octaves above the bass.
        public static float Lead(float t)
        {
            var chord = Chord(t);
            int step = (int)(t / 0.125f) % 4;
            int semitones = step == 0 ? 0 : step == 1 ? chord.third : step == 2 ? 7 : 12;
            float hz = 4 * chord.root * Mathf.Pow(2, semitones / 12f), into = t % 0.125f;
            return (0.6f * Tone(hz, into) + 0.2f * Tone(2 * hz, into)) * Note(t, 0.125f);
        }

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
