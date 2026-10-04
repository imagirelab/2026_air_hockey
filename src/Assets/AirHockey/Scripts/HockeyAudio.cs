using UnityEngine;

namespace AirHockey
{
    /// <summary>
    /// 効果音とBGMをすべて手続き合成する。
    /// BGMは複数レイヤーを同期再生し、試合の盛り上がり度に応じてレイヤーがフェードインする。
    /// </summary>
    public class HockeyAudio : MonoBehaviour
    {
        const int Rate = 44100;
        const float Bpm = 112f;

        AudioClip knock, wall, goalUp, goalDown, beep, beepHi, cheer, fanfare, groan;
        AudioSource[] layers;
        float[] layerTarget;
        AudioSource sfx;
        float duck = 1f;

        void Awake()
        {
            knock = Make("Knock", 0.18f, t => (Sine(880f, t) * 0.6f + Sine(1320f, t) * 0.3f + Noise() * Mathf.Exp(-t * 300f)) * Mathf.Exp(-t * 38f));
            wall = Make("Wall", 0.2f, t => (Sine(240f, t) + Sine(410f, t) * 0.5f + Noise() * Mathf.Exp(-t * 200f) * 0.6f) * Mathf.Exp(-t * 30f) * 0.8f);
            beep = Make("Beep", 0.25f, t => Sine(660f, t) * Env(t, 0.005f, 0.25f) * 0.5f);
            beepHi = Make("BeepHi", 0.6f, t => (Sine(1320f, t) + Sine(990f, t) * 0.5f) * Env(t, 0.005f, 0.6f) * 0.45f);
            goalUp = Make("GoalUp", 1.2f, t => Arp(t, new[] { 523f, 659f, 784f, 1047f }, 0.09f) * Env(t, 0.01f, 1.2f));
            goalDown = Make("GoalDown", 1.0f, t => Arp(t, new[] { 392f, 330f, 262f }, 0.16f) * Env(t, 0.01f, 1.0f) * 0.8f);
            fanfare = Make("Fanfare", 2.6f, t => Fanfare(t));
            groan = Make("Groan", 1.6f, t => Saw(Mathf.Lerp(220f, 110f, t / 1.6f), t) * Env(t, 0.05f, 1.6f) * 0.25f);
            cheer = Make("Cheer", 2.5f, t => CrowdNoise(t) * Env(t, 0.3f, 2.5f));

            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;

            // BGMレイヤー：0=パッド 1=ベース 2=キック 3=ハイハット 4=リード
            float bar = 60f / Bpm * 4f;
            float len = bar * 4f;
            var clips = new[]
            {
                Make("Pad", len, t => Pad(t, bar)),
                Make("Bass", len, t => Bass(t, bar)),
                Make("Kick", len, t => Kick(t)),
                Make("Hat", len, t => Hat(t)),
                Make("Lead", len, t => Lead(t, bar)),
            };
            layers = new AudioSource[clips.Length];
            layerTarget = new float[clips.Length];
            double start = AudioSettings.dspTime + 0.2;
            for (int i = 0; i < clips.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.clip = clips[i];
                src.loop = true;
                src.volume = 0f;
                src.PlayScheduled(start);
                layers[i] = src;
            }
        }

        void OnEnable()
        {
            GameManager.PuckHit += OnHit;
            GameManager.WallHit += OnWall;
            GameManager.GoalScored += OnGoal;
            GameManager.Countdown += OnCountdown;
            GameManager.MatchEnded += OnEnd;
        }

        void OnDisable()
        {
            GameManager.PuckHit -= OnHit;
            GameManager.WallHit -= OnWall;
            GameManager.GoalScored -= OnGoal;
            GameManager.Countdown -= OnCountdown;
            GameManager.MatchEnded -= OnEnd;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            float k = gm ? gm.Intensity : 0f;
            bool over = gm && gm.Phase == Phase.GameOver;
            // 盛り上がり度に応じてレイヤーを重ねる
            layerTarget[0] = 0.35f;
            layerTarget[1] = k > 0.05f ? 0.4f : 0f;
            layerTarget[2] = k > 0.2f ? 0.5f : 0f;
            layerTarget[3] = k > 0.4f ? 0.22f : 0f;
            layerTarget[4] = (k > 0.6f || (gm && gm.MatchPoint)) ? 0.22f : 0f;
            duck = Mathf.MoveTowards(duck, 1f, Time.unscaledDeltaTime * 0.6f);
            for (int i = 0; i < layers.Length; i++)
            {
                float target = layerTarget[i] * duck * (over ? 0.4f : 1f);
                layers[i].volume = Mathf.MoveTowards(layers[i].volume, target, Time.unscaledDeltaTime * 0.4f);
                layers[i].pitch = Time.timeScale < 0.9f ? 0.85f : 1f;
            }
        }

        void OnHit(Vector3 pos, float s)
        {
            float k = GameManager.Instance ? GameManager.Instance.Intensity : 0f;
            AudioSource.PlayClipAtPoint(knock, pos, Mathf.Lerp(0.4f, 1f, s));
            sfx.pitch = Random.Range(0.92f, 1.08f) * Mathf.Lerp(1.1f, 0.85f, s);
            if (s > 0.5f && k > 0.3f) sfx.PlayOneShot(wall, s * k); // 強打は重低音を重ねる
        }

        void OnWall(Vector3 pos, float s) => AudioSource.PlayClipAtPoint(wall, pos, Mathf.Lerp(0.3f, 0.9f, s));

        void OnCountdown(string s)
        {
            sfx.pitch = 1f;
            sfx.PlayOneShot(s.EndsWith("！") ? beepHi : beep, 0.8f);
        }

        void OnGoal(bool byPlayer)
        {
            float k = GameManager.Instance.Intensity;
            sfx.pitch = 1f;
            sfx.PlayOneShot(byPlayer ? goalUp : goalDown, 0.9f);
            if (byPlayer) sfx.PlayOneShot(cheer, Mathf.Lerp(0.25f, 1f, k));
            duck = 0.3f;
        }

        void OnEnd(bool won)
        {
            sfx.pitch = 1f;
            sfx.PlayOneShot(won ? fanfare : groan, 1f);
            if (won) sfx.PlayOneShot(cheer, 1f);
        }

        // ---------- 合成ユーティリティ ----------
        static System.Random rng = new System.Random(7);
        static float Noise() => (float)rng.NextDouble() * 2f - 1f;
        static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        static float Saw(float f, float t) => 2f * Mathf.Repeat(f * t, 1f) - 1f;
        static float Env(float t, float a, float len) => Mathf.Clamp01(t / a) * Mathf.Clamp01((len - t) / (len * 0.6f));
        static float Midi(float n) => 440f * Mathf.Pow(2f, (n - 69f) / 12f);

        static float Arp(float t, float[] notes, float step)
        {
            float v = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float lt = t - i * step;
                if (lt > 0f) v += (Sine(notes[i], lt) + Sine(notes[i] * 2f, lt) * 0.3f) * Mathf.Exp(-lt * 4f) * 0.35f;
            }
            return v;
        }

        static float Fanfare(float t)
        {
            float[] n = { 60, 64, 67, 72, 67, 72, 76, 79 };
            float[] at = { 0f, 0.15f, 0.3f, 0.45f, 0.75f, 0.9f, 1.05f, 1.2f };
            float v = 0f;
            for (int i = 0; i < n.Length; i++)
            {
                float lt = t - at[i];
                if (lt <= 0f) continue;
                float f = Midi(n[i]);
                float dec = i == n.Length - 1 ? 1.2f : 3f;
                v += (Saw(f, lt) * 0.3f + Sine(f, lt) * 0.5f) * Mathf.Exp(-lt * dec) * Mathf.Clamp01(lt / 0.02f);
            }
            return v * 0.35f * Mathf.Clamp01((2.6f - t) / 0.5f);
        }

        static float CrowdNoise(float t)
        {
            // 多数の小さな声と手拍子を雑音でまとめた歓声
            float n = Noise();
            float mod = 0.6f + 0.4f * Mathf.PerlinNoise(t * 9f, 0.5f);
            float clap = Mathf.PerlinNoise(t * 40f, 3f) > 0.62f ? Noise() * 0.8f : 0f;
            return (n * 0.35f * mod + clap * 0.3f) * 0.6f;
        }

        // コード進行：Am - F - C - G
        static readonly float[][] Chords = { new float[] { 57, 60, 64 }, new float[] { 53, 57, 60 }, new float[] { 48, 52, 55 }, new float[] { 55, 59, 62 } };

        static float Pad(float t, float bar)
        {
            int ci = Mathf.FloorToInt(t / bar) % 4;
            float v = 0f;
            foreach (var n in Chords[ci])
            {
                float f = Midi(n);
                v += Sine(f, t) * 0.5f + Sine(f * 1.003f, t) * 0.3f + Sine(f * 2f, t) * 0.1f;
            }
            float lt = Mathf.Repeat(t, bar);
            return v * 0.12f * Mathf.Clamp01(lt / 0.3f) * Mathf.Clamp01((bar - lt) / 0.2f + 0.3f);
        }

        static float Bass(float t, float bar)
        {
            int ci = Mathf.FloorToInt(t / bar) % 4;
            float beat = 60f / Bpm;
            float lt = Mathf.Repeat(t, beat * 0.5f);
            float f = Midi(Chords[ci][0] - 12);
            return (Sine(f, t) * 0.7f + Saw(f, t) * 0.15f) * Mathf.Exp(-lt * 5f) * 0.6f;
        }

        static float Kick(float t)
        {
            float beat = 60f / Bpm;
            float lt = Mathf.Repeat(t, beat);
            float f = 50f + 120f * Mathf.Exp(-lt * 30f);
            float kick = Mathf.Sin(2f * Mathf.PI * f * lt) * Mathf.Exp(-lt * 9f);
            float st = Mathf.Repeat(t + beat, beat * 2f); // 2,4拍目にスネア（ウッドブロック風）
            float snare = Noise() * Mathf.Exp(-st * 25f) * 0.4f + Sine(720f, st) * Mathf.Exp(-st * 40f) * 0.3f;
            return kick * 0.8f + snare;
        }

        static float Hat(float t)
        {
            float step = 60f / Bpm / 4f;
            float lt = Mathf.Repeat(t, step);
            int idx = Mathf.FloorToInt(t / step) % 4;
            return Noise() * Mathf.Exp(-lt * (idx == 2 ? 40f : 90f)) * 0.35f;
        }

        static float Lead(float t, float bar)
        {
            int[] pattern = { 0, 2, 1, 2, 0, 1, 2, 1 };
            int ci = Mathf.FloorToInt(t / bar) % 4;
            float step = bar / 8f;
            int si = Mathf.FloorToInt(Mathf.Repeat(t, bar) / step);
            float lt = Mathf.Repeat(t, step);
            float f = Midi(Chords[ci][pattern[si]] + 12);
            return (Sine(f, t) * 0.6f + Saw(f, t) * 0.12f) * Mathf.Exp(-lt * 7f) * 0.5f;
        }

        static AudioClip Make(string name, float seconds, System.Func<float, float> fn)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(fn(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
