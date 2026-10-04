using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AirHockey
{
    /// <summary>
    /// 視覚演出。打撃の火花・木くず、パックの残像、ゴールの紙吹雪、照明やLEDの脈動、
    /// ブルームや色収差を試合の盛り上がり度に応じて段階的に強める。
    /// </summary>
    public class HockeyFX : MonoBehaviour
    {
        public Light lamp;
        public Volume volume;
        public Renderer[] ledStrips;

        ParticleSystem sparks, chips, confetti, burst;
        TrailRenderer trail;
        Material ledMat;
        Bloom bloom;
        ChromaticAberration chroma;
        Vignette vignette;
        float baseLamp, flash, chromaKick;
        Color flashColor = Color.white;
        Color ledColor = new Color(1f, 0.55f, 0.2f);
        readonly List<Light> warmLights = new List<Light>();

        void Start()
        {
            var tex = SoftDot();
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
            sparks = MakePS("Sparks", mat, 0.25f, 0.012f, 2.5f, true);
            chips = MakePS("WoodChips", mat, 0.6f, 0.01f, 1.2f, false);
            burst = MakePS("GoalBurst", mat, 1.2f, 0.03f, 4f, true);
            confetti = MakePS("Confetti", mat, 3.5f, 0.025f, 1.5f, false);
            var cm = confetti.main; cm.gravityModifier = 0.25f;
            var cr = confetti.rotationOverLifetime; cr.enabled = true; cr.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var gm = GameManager.Instance;
            trail = gm.puck.gameObject.AddComponent<TrailRenderer>();
            trail.material = mat;
            trail.time = 0.18f;
            trail.widthMultiplier = HockeyConfig.PuckRadius * 1.6f;
            trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
            trail.minVertexDistance = 0.01f;
            trail.emitting = false;

            if (lamp) baseLamp = lamp.intensity;
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.name == "WarmLight") warmLights.Add(l);
            if (ledStrips != null && ledStrips.Length > 0)
            {
                ledMat = new Material(ledStrips[0].sharedMaterial);
                ledMat.EnableKeyword("_EMISSION");
                foreach (var r in ledStrips) r.sharedMaterial = ledMat;
            }
            if (volume && volume.profile)
            {
                volume.profile.TryGet(out bloom);
                volume.profile.TryGet(out chroma);
                volume.profile.TryGet(out vignette);
            }
        }

        void OnEnable()
        {
            GameManager.PuckHit += OnHit;
            GameManager.WallHit += OnWall;
            GameManager.GoalScored += OnGoal;
            GameManager.MatchEnded += OnEnd;
        }

        void OnDisable()
        {
            GameManager.PuckHit -= OnHit;
            GameManager.WallHit -= OnWall;
            GameManager.GoalScored -= OnGoal;
            GameManager.MatchEnded -= OnEnd;
        }

        float K => GameManager.Instance ? GameManager.Instance.Intensity : 0f;

        void OnHit(Vector3 pos, float s)
        {
            float k = K;
            Emit(chips, pos, Mathf.RoundToInt(4 + s * 10), new Color(0.85f, 0.65f, 0.4f), 0.6f + s);
            if (k > 0.15f || s > 0.6f)
                Emit(sparks, pos, Mathf.RoundToInt((6 + s * 30) * (0.5f + k * 1.5f)), Color.Lerp(new Color(1f, 0.75f, 0.35f), new Color(1f, 0.4f, 0.15f), k), 1.5f + s * 2f);
            CameraRig.Instance?.Shake(s * (0.15f + k * 0.5f));
            if (s > 0.55f) { CameraRig.Instance?.Kick(s * k * 3f); chromaKick = Mathf.Max(chromaKick, s * k * 0.6f); }
            flash = Mathf.Max(flash, s * k * 0.6f);
            flashColor = new Color(1f, 0.75f, 0.45f);
        }

        void OnWall(Vector3 pos, float s)
        {
            Emit(chips, pos, Mathf.RoundToInt(2 + s * 6), new Color(0.5f, 0.32f, 0.18f), 0.5f + s);
            if (K > 0.5f) Emit(sparks, pos, Mathf.RoundToInt(s * 15), new Color(1f, 0.6f, 0.25f), 1.2f);
            CameraRig.Instance?.Shake(s * 0.12f);
        }

        void OnGoal(bool byPlayer)
        {
            var gm = GameManager.Instance;
            float k = gm.Intensity;
            Vector3 at = new Vector3(0f, HockeyConfig.TableY + 0.05f, byPlayer ? HockeyConfig.HalfLength : -HockeyConfig.HalfLength);
            Color c = byPlayer ? new Color(1f, 0.7f, 0.25f) : new Color(0.35f, 0.55f, 1f);
            Emit(burst, at, Mathf.RoundToInt(40 + k * 160), c, 2f + k * 2f);
            if (byPlayer && k > 0.1f) Confetti(Mathf.RoundToInt(60 + k * 300));
            CameraRig.Instance?.Shake(0.4f + k * 0.8f);
            CameraRig.Instance?.Kick(4f + k * 6f);
            chromaKick = 0.4f + k * 0.6f;
            flash = 1f + k * 2f;
            flashColor = c;
        }

        void OnEnd(bool won)
        {
            if (won) Confetti(700);
            flash = 3f;
            flashColor = won ? new Color(1f, 0.8f, 0.4f) : new Color(0.4f, 0.5f, 1f);
            CameraRig.Instance?.Shake(1f);
        }

        void Confetti(int n)
        {
            var colors = new[] { new Color(1f, 0.35f, 0.3f), new Color(1f, 0.85f, 0.3f), new Color(0.4f, 0.85f, 0.5f), new Color(0.4f, 0.65f, 1f), new Color(1f, 0.6f, 0.9f) };
            for (int i = 0; i < n; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = new Vector3(Random.Range(-1.2f, 1.2f), 2.6f + Random.value * 0.6f, Random.Range(-1.4f, 1.4f)),
                    velocity = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.6f, 0.2f), Random.Range(-0.4f, 0.4f)),
                    startColor = colors[i % colors.Length],
                    startSize = Random.Range(0.015f, 0.035f),
                    startLifetime = Random.Range(2.5f, 4.5f),
                    rotation = Random.value * 360f,
                };
                confetti.Emit(ep, 1);
            }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (!gm) return;
            float k = gm.Intensity;
            float dt = Time.unscaledDeltaTime;
            float beat = Mathf.Repeat(Time.unscaledTime * 112f / 60f, 1f);
            float pulse = Mathf.Exp(-beat * 6f);

            // パックの残像は試合が進むほど長く・明るく
            float speed = gm.puck.vel.magnitude;
            trail.emitting = gm.Phase == Phase.Playing && speed > 1.2f && k > 0.05f;
            trail.time = Mathf.Lerp(0.08f, 0.3f, k);
            Color tc = Color.Lerp(new Color(1f, 0.8f, 0.6f), new Color(1f, 0.35f, 0.1f), k);
            trail.startColor = new Color(tc.r, tc.g, tc.b, Mathf.Clamp01(speed / 5f) * (0.25f + k * 0.6f));
            trail.endColor = new Color(tc.r, tc.g, tc.b, 0f);

            // テーブル縁のLED：序盤は消灯、盛り上がるとビートに合わせて脈動
            if (ledMat)
            {
                float glow = Mathf.Lerp(0f, 3.5f, k) * (0.6f + 0.4f * pulse) + flash * 2f;
                if (gm.MatchPoint) ledColor = Color.Lerp(ledColor, new Color(1f, 0.2f, 0.1f), dt * 2f);
                Color target = flash > 0.2f ? flashColor : ledColor;
                ledMat.SetColor("_EmissionColor", target * glow);
            }

            // ペンダントランプのフラッシュと壁灯のゆらぎ
            if (lamp) lamp.intensity = baseLamp * (1f + flash * 0.25f) + (k > 0.6f ? pulse * k * 2f : 0f);
            foreach (var l in warmLights)
                l.intensity = 2.5f * (0.92f + Mathf.PerlinNoise(Time.time * 3f, l.transform.position.x) * 0.16f) + flash * 0.6f;

            if (bloom) bloom.intensity.value = Mathf.Lerp(0.6f, 1.6f, k) + flash * 0.6f;
            if (chroma) chroma.intensity.value = chromaKick;
            if (vignette)
            {
                vignette.intensity.value = 0.32f + (gm.MatchPoint ? 0.08f + pulse * 0.06f : 0f);
                vignette.color.value = gm.MatchPoint ? new Color(0.35f, 0.02f, 0f) : Color.black;
            }

            flash = Mathf.MoveTowards(flash, 0f, dt * 2.5f);
            chromaKick = Mathf.MoveTowards(chromaKick, 0f, dt * 1.5f);
        }

        void Emit(ParticleSystem ps, Vector3 pos, int count, Color c, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                var ep = new ParticleSystem.EmitParams
                {
                    position = pos,
                    velocity = dir * speed * Random.Range(0.3f, 1f),
                    startColor = c * Random.Range(0.8f, 1.2f),
                };
                ps.Emit(ep, 1);
            }
        }

        ParticleSystem MakePS(string name, Material mat, float life, float size, float gravity, bool additive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.gravityModifier = gravity * 0.3f;
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(additive ? new Color(1f, 0.5f, 0.2f) : Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = additive;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            if (additive) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.03f; r.lengthScale = 1f; }
            return ps;
        }

        static Texture2D SoftDot()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f - 0.5f, n * 0.5f - 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            t.Apply();
            return t;
        }
    }
}
