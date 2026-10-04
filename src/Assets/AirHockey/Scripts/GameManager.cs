using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AirHockey
{
    public enum Phase { Countdown, Playing, Goal, GameOver }

    /// <summary>試合進行とパック物理を担当する。演出・音はイベントを購読して反応する。</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public Mallet player;
        public Mallet ai;
        public Puck puck;
        public Camera cam;
        [Tooltip("オンにするとプレイヤー側もAIが操作する（AI同士の自動対戦）。プレイ中はTabキーで切り替え可能")]
        public bool autoPlay;

        public int PlayerScore { get; private set; }
        public int AIScore { get; private set; }
        public Phase Phase { get; private set; }

        /// <summary>試合の盛り上がり度 0..1。得点が進むほど上がる。</summary>
        public float Intensity => Mathf.Clamp01((PlayerScore + AIScore) / (float)(HockeyConfig.WinScore * 2 - 2));
        public bool MatchPoint => Mathf.Max(PlayerScore, AIScore) == HockeyConfig.WinScore - 1;
        /// <summary>現在のラリーの経過秒（ゲーム時間）</summary>
        public float RallyTime { get; private set; }

        public static event Action<Vector3, float> PuckHit;   // 位置, 強さ0..1
        public static event Action<Vector3, float> WallHit;
        public static event Action<bool> GoalScored;          // true=プレイヤーの得点
        public static event Action<bool> MatchEnded;          // true=プレイヤーの勝ち
        public static event Action<string> Countdown;         // 表示テキスト
        public static event Action MatchStarted;

        /// <summary>trueならプレイヤー側もAIが操作する（デモ・録画用）</summary>
        public static bool AutoPlay;
        /// <summary>試合終了後、自動で次の試合を始めるか</summary>
        public static bool AutoRestart = true;

        AIController brain, playerBrain;

        /// <summary>スマートフォンなどタッチ操作の端末か</summary>
        public static bool IsTouchDevice => Application.isMobilePlatform || (Touchscreen.current != null && Mouse.current == null);
        // フリック操作：指の移動量をテーブル上の移動量に換算する倍率（画面の短辺をなぞるとテーブル幅の何倍動くか）
        const float TouchGain = 1.6f;
        bool touching;
        Vector2 lastTouch, flickVel;
        float hitCooldown, wallCooldown;

        void Awake()
        {
            Instance = this;
            if (autoPlay) AutoPlay = true;
            brain = new AIController(ai);
            playerBrain = new AIController(player);
#if UNITY_WEBGL && !UNITY_EDITOR
            Application.targetFrameRate = -1; // ブラウザの描画タイミングに合わせる
#else
            Application.targetFrameRate = 120;
#endif
        }

        void Start() => StartCoroutine(MatchRoutine());

        IEnumerator MatchRoutine()
        {
            PlayerScore = AIScore = 0;
            MatchStarted?.Invoke();
            bool serveToPlayer = UnityEngine.Random.value < 0.5f;
            bool first = true;
            while (true)
            {
                yield return Serve(serveToPlayer, first);
                first = false;
                Phase = Phase.Playing;
                RallyTime = 0f;
                while (Phase == Phase.Playing) yield return null;

                // ゴール後の余韻
                yield return Wait(MatchPoint || IsOver ? 2.2f : 1.6f);
                Time.timeScale = 1f;
                if (IsOver) break;
                serveToPlayer = lastGoalByPlayer == false; // 失点した側からサーブ
            }

            Phase = Phase.GameOver;
            Cursor.visible = true;
            MatchEnded?.Invoke(PlayerScore > AIScore);
            yield return Wait(1.5f);
            if (AutoPlay)
            {
                if (!AutoRestart) yield break;
                yield return Wait(4f);
            }
            else
                while (!(Pointer.current != null && Pointer.current.press.wasPressedThisFrame)) yield return null; // クリックまたはタップ
            StartCoroutine(MatchRoutine());
        }

        // 録画時は固定フレームレートで時間が進むため、実時間ではなくunscaledTimeで待つ
        static IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }

        bool IsOver => PlayerScore >= HockeyConfig.WinScore || AIScore >= HockeyConfig.WinScore;
        bool lastGoalByPlayer;

        IEnumerator Serve(bool toPlayer, bool withCountdown)
        {
            Phase = Phase.Countdown;
            Cursor.visible = false;
            puck.gameObject.SetActive(true);
            puck.ResetTo(new Vector2(0f, toPlayer ? -0.35f : 0.35f));
            ai.ResetTo(new Vector2(0f, HockeyConfig.HalfLength - 0.2f));
            if (withCountdown)
            {
                player.ResetTo(new Vector2(0f, -HockeyConfig.HalfLength + 0.25f));
                foreach (var s in new[] { "3", "2", "1" })
                {
                    Countdown?.Invoke(s);
                    yield return Wait(0.75f);
                }
                Countdown?.Invoke("スタート！");
            }
            else
            {
                Countdown?.Invoke(MatchPoint ? "マッチポイント！" : "レディ…");
                yield return Wait(1.0f);
                Countdown?.Invoke("ゴー！");
            }
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (dt <= 0f) return;

            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) AutoPlay = !AutoPlay;

            // プレイヤー入力：自動対戦ならAI、そうでなければマウス位置をテーブル平面に投影
            if (AutoPlay)
            {
                if (Phase == Phase.Playing) playerBrain.Tick(puck, dt, Mathf.Lerp(0.3f, 0.6f, Intensity), RallyTime);
                else player.target = new Vector2(0f, -HockeyConfig.HalfLength + 0.25f);
            }
            else if (IsTouchDevice || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed))
            {
                TouchInput(dt);
            }
            else if (Mouse.current != null && cam)
            {
                Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                Plane plane = new Plane(Vector3.up, new Vector3(0f, HockeyConfig.TableY, 0f));
                if (plane.Raycast(ray, out float enter))
                {
                    Vector3 hit = ray.GetPoint(enter);
                    player.target = new Vector2(hit.x, hit.z);
                }
            }

            if (Phase == Phase.Playing) brain.Tick(puck, dt, Mathf.Lerp(0.25f, 0.6f, Intensity), RallyTime);
            else if (Phase != Phase.Goal) ai.target = new Vector2(0f, HockeyConfig.HalfLength - 0.2f);

            player.Step(dt);
            ai.Step(dt);

            if (Phase == Phase.Playing) { Simulate(dt); RallyTime += dt; }
            else if (Phase == Phase.Goal) AnimateGoalDrop(dt);
            puck.Apply(dt);

            hitCooldown -= dt;
            wallCooldown -= dt;
        }

        /// <summary>
        /// フリック操作。指を置いた位置に関係なく、指の移動量だけマレットを動かす（指でマレットが隠れない）。
        /// 指を離すとフリックの勢いで少しだけ滑る。
        /// </summary>
        void TouchInput(float dt)
        {
            var ts = Touchscreen.current;
            if (ts == null) return;
            var touch = ts.primaryTouch;
            if (touch.press.isPressed)
            {
                Vector2 sp = touch.position.ReadValue();
                if (!touching)
                {
                    touching = true;
                    lastTouch = sp;
                    flickVel = Vector2.zero;
                    player.target = player.pos;
                }
                Vector2 d = sp - lastTouch;
                lastTouch = sp;
                float scale = TouchGain * (HockeyConfig.HalfWidth * 2f) / Mathf.Max(1, Mathf.Min(Screen.width, Screen.height));
                Vector2 move = d * scale; // 画面の上方向＝テーブルの奥方向
                player.target = player.Clamp(player.target + move);
                flickVel = Vector2.Lerp(flickVel, move / dt, 0.5f);
            }
            else
            {
                touching = false;
                if (flickVel.sqrMagnitude > 0.01f)
                {
                    player.target = player.Clamp(player.target + flickVel * dt);
                    flickVel *= Mathf.Exp(-dt * 8f);
                }
            }
        }

        void Simulate(float dt)
        {
            const int sub = 10;
            float h = dt / sub;
            for (int i = 1; i <= sub; i++)
            {
                float t = i / (float)sub;
                puck.pos += puck.vel * h;
                Collide(Vector2.Lerp(player.prevPos, player.pos, t), player.vel);
                Collide(Vector2.Lerp(ai.prevPos, ai.pos, t), ai.vel);
                if (Walls()) return;
            }
            // エアテーブルのわずかな減速
            puck.vel *= 1f - 0.12f * dt;

            // 隅に挟まって止まったパックは、空気の流れで中央へ少しずつ押し出す
            Vector2 p = puck.pos;
            if (Mathf.Abs(p.x) > HockeyConfig.HalfWidth - 0.14f && Mathf.Abs(p.y) > HockeyConfig.HalfLength - 0.14f && puck.vel.magnitude < 1.5f)
                puck.vel += new Vector2(-Mathf.Sign(p.x) * 0.8f, -Mathf.Sign(p.y) * 1.6f) * dt;
        }

        void Collide(Vector2 mp, Vector2 mv)
        {
            float rr = HockeyConfig.PuckRadius + HockeyConfig.MalletRadius;
            Vector2 d = puck.pos - mp;
            float dist = d.magnitude;
            if (dist >= rr) return;
            Vector2 n = dist > 1e-5f ? d / dist : Vector2.up;
            puck.pos = mp + n * rr;
            Vector2 rel = puck.vel - mv;
            float vn = Vector2.Dot(rel, n);
            if (vn < 0f)
            {
                puck.vel -= 1.85f * vn * n;
                puck.vel = Vector2.ClampMagnitude(puck.vel, HockeyConfig.MaxPuckSpeed);
                puck.spin += Vector3.Cross(new Vector3(n.x, 0, n.y), new Vector3(mv.x, 0, mv.y)).y * 400f;
                float strength = Mathf.Clamp01(-vn / 6f);
                if (hitCooldown <= 0f && strength > 0.02f)
                {
                    hitCooldown = 0.06f;
                    PuckHit?.Invoke(HockeyConfig.ToWorld(puck.pos - n * HockeyConfig.PuckRadius), strength);
                }
            }
        }

        bool Walls()
        {
            float r = HockeyConfig.PuckRadius;
            float w = HockeyConfig.HalfWidth - r;
            float l = HockeyConfig.HalfLength - r;
            float g = HockeyConfig.GoalHalfWidth;
            Vector2 p = puck.pos, v = puck.vel;
            bool hit = false;
            bool inSlot = Mathf.Abs(p.x) < g - r * 0.4f;

            if (Mathf.Abs(p.y) > l && inSlot)
            {
                // ゴール口の中：左右のポストで跳ね返る
                float sx = g - r;
                if (Mathf.Abs(p.x) > sx) { p.x = Mathf.Sign(p.x) * sx; v.x = -v.x * 0.7f; hit = true; }
                if (Mathf.Abs(p.y) > HockeyConfig.HalfLength + r * 0.5f)
                {
                    puck.pos = p; puck.vel = v;
                    OnGoal(p.y > 0f);
                    return true;
                }
            }
            else
            {
                if (p.y > l) { p.y = l; v.y = -Mathf.Abs(v.y) * 0.88f; hit = true; }
                if (p.y < -l) { p.y = -l; v.y = Mathf.Abs(v.y) * 0.88f; hit = true; }
            }
            if (p.x > w) { p.x = w; v.x = -Mathf.Abs(v.x) * 0.88f; hit = true; }
            if (p.x < -w) { p.x = -w; v.x = Mathf.Abs(v.x) * 0.88f; hit = true; }

            if (hit && wallCooldown <= 0f && v.magnitude > 0.25f)
            {
                wallCooldown = 0.05f;
                WallHit?.Invoke(HockeyConfig.ToWorld(p), Mathf.Clamp01(v.magnitude / HockeyConfig.MaxPuckSpeed));
            }
            puck.pos = p; puck.vel = v;
            return false;
        }

        void OnGoal(bool intoAIGoal)
        {
            Phase = Phase.Goal;
            lastGoalByPlayer = intoAIGoal;
            if (intoAIGoal) PlayerScore++; else AIScore++;
            if (IsOver) Time.timeScale = 0.35f; // 決勝点はスローモーション
            GoalScored?.Invoke(intoAIGoal);
        }

        void AnimateGoalDrop(float dt)
        {
            if (!puck.gameObject.activeSelf) return;
            puck.pos += puck.vel * dt * 0.5f;
            puck.drop += dt * 0.6f;
            if (puck.drop > 0.25f) puck.gameObject.SetActive(false);
        }
    }
}
