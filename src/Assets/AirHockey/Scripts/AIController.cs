using UnityEngine;

namespace AirHockey
{
    /// <summary>
    /// NPCの思考。守備・回り込み・攻撃を切り替える。
    /// 人間らしく負けられるよう、移動速度・加速度の上限、反応の遅れ、予測誤差、
    /// ラリーが長引くほど動きが鈍る「疲労」を持つ。
    /// </summary>
    public class AIController
    {
        readonly Mallet m;
        float thinkTimer;
        Vector2 noise;
        float aimX;
        float predictError;
        float stuckTimer;

        // 反応の遅れ：パックは思考のタイミングでしか見ておらず、間は等速で外挿する
        Vector2 seenPos, seenVel;
        float sinceSeen;

        // 自陣が奥(+z)なら1、手前(-z)なら-1。思考は常に「自陣が+z」の座標系で行う
        readonly float side;

        public AIController(Mallet mallet)
        {
            m = mallet;
            side = mallet.isPlayer ? -1f : 1f;
        }

        Vector2 Flip(Vector2 a) => new Vector2(a.x, a.y * side);

        /// <param name="skill">0..1 強さ</param>
        /// <param name="rallyTime">現在のラリーの経過秒。長いほど疲れて動きが鈍る</param>
        public void Tick(Puck puck, float dt, float skill, float rallyTime)
        {
            const float L = HockeyConfig.HalfLength;
            float rr = HockeyConfig.PuckRadius + HockeyConfig.MalletRadius;
            Vector2 home = new Vector2(0f, L - 0.2f);

            // 疲労：5秒を過ぎると徐々に弱くなり、11秒でかなり鈍くなる
            float fatigue = Mathf.Clamp01((rallyTime - 5f) / 6f);
            float eff = Mathf.Clamp01(skill * (1f - fatigue * 0.9f));

            thinkTimer -= dt;
            sinceSeen += dt;
            if (thinkTimer <= 0f)
            {
                thinkTimer = Mathf.Lerp(0.32f, 0.16f, eff) * Random.Range(0.8f, 1.2f);
                seenPos = Flip(puck.pos);
                seenVel = Flip(puck.vel);
                sinceSeen = 0f;
                noise = Random.insideUnitCircle * Mathf.Lerp(0.12f, 0.04f, eff);
                predictError = Random.Range(-1f, 1f) * Mathf.Lerp(0.22f, 0.07f, eff);
                aimX = Random.Range(-1f, 1f) * HockeyConfig.GoalHalfWidth * 0.7f;
                if (Random.value < 0.2f) aimX = Random.Range(-1f, 1f) * HockeyConfig.HalfWidth * 1.6f; // バンクショット狙い
            }
            Vector2 v = seenVel;
            Vector2 p = seenPos + seenVel * sinceSeen;
            Vector2 mp = Flip(m.pos);

            // 1フレームで動ける距離（＝速度）と加速度を制限する
            float defendSpeed = Mathf.Lerp(1.1f, 1.9f, eff);
            float attackSpeed = Mathf.Lerp(2.0f, 3.0f, eff);
            m.maxAccel = Mathf.Lerp(7f, 14f, eff);

            Vector2 target;
            bool attacking = false;
            if (p.y > 0f)
            {
                stuckTimer = (v.magnitude < 0.3f) ? stuckTimer + dt : 0f;
                bool behind = p.y > mp.y - 0.02f;
                bool inCorner = p.y > L - 0.14f && Mathf.Abs(p.x) > HockeyConfig.HalfWidth - 0.16f;
                if (inCorner)
                {
                    // 隅のパックは押し付けると挟まるので、少し下がって出てくるのを待つ
                    target = new Vector2(p.x - Mathf.Sign(p.x) * 0.22f, p.y - 0.3f);
                }
                else if (behind)
                {
                    // パックの裏へ回り込む
                    float around = (mp.x < p.x) ? -1f : 1f;
                    if (Mathf.Abs(p.x) > HockeyConfig.HalfWidth - 0.15f) around = -Mathf.Sign(p.x);
                    target = new Vector2(p.x + around * (rr + 0.03f), p.y + rr + 0.05f);
                }
                else if (v.y > 1.2f && p.y < L * 0.55f)
                {
                    // 速いパックが向かってくる→迎撃ラインへ（予測には誤差がある）
                    float lineY = Mathf.Max(p.y, home.y - 0.15f);
                    float t = (lineY - p.y) / v.y;
                    target = new Vector2(PredictX(p.x, v.x, t) + predictError, lineY);
                }
                else
                {
                    // 攻撃：パックの後ろから相手ゴールへ打ち抜く
                    attacking = true;
                    Vector2 goal = new Vector2(aimX, -L);
                    Vector2 dir = (goal - p).normalized;
                    Vector2 strikePos = p - dir * (rr + 0.04f);
                    target = (Vector2.Distance(mp, strikePos) < 0.08f || stuckTimer > 1.0f)
                        ? p + dir * 0.25f
                        : strikePos;
                }
            }
            else
            {
                stuckTimer = 0f;
                float x = p.x * 0.45f;
                if (v.y > 0.3f)
                {
                    float t = (home.y - p.y) / v.y;
                    x = PredictX(p.x, v.x, t) + predictError;
                }
                x = Mathf.Clamp(x, -HockeyConfig.GoalHalfWidth - 0.05f, HockeyConfig.GoalHalfWidth + 0.05f);
                target = new Vector2(x, home.y - Mathf.Clamp01(-p.y) * 0.1f);
            }

            m.maxSpeed = attacking ? attackSpeed : defendSpeed;
            m.target = Flip(target + noise);
        }

        static float PredictX(float x, float vx, float t)
        {
            float w = HockeyConfig.HalfWidth - HockeyConfig.PuckRadius;
            float px = x + vx * Mathf.Clamp(t, 0f, 2f);
            // 壁反射を折り返しで近似
            float span = 4f * w;
            px = Mathf.Repeat(px + w, span);
            if (px > 2f * w) px = span - px;
            return px - w;
        }
    }
}
