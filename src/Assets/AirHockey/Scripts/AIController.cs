using UnityEngine;

namespace AirHockey
{
    /// <summary>NPCの思考。守備・回り込み・攻撃を切り替え、スキル値で反応速度と精度が変わる。</summary>
    public class AIController
    {
        readonly Mallet m;
        float thinkTimer;
        Vector2 noise;
        float aimX;
        float stuckTimer;

        public AIController(Mallet mallet) { m = mallet; }

        public void Tick(Puck puck, float dt, float skill)
        {
            const float L = HockeyConfig.HalfLength;
            float rr = HockeyConfig.PuckRadius + HockeyConfig.MalletRadius;
            Vector2 home = new Vector2(0f, L - 0.2f);
            Vector2 p = puck.pos, v = puck.vel;

            m.maxSpeed = Mathf.Lerp(2.6f, 5.2f, skill);
            thinkTimer -= dt;
            if (thinkTimer <= 0f)
            {
                thinkTimer = Mathf.Lerp(0.28f, 0.08f, skill);
                noise = Random.insideUnitCircle * Mathf.Lerp(0.07f, 0.015f, skill);
                aimX = Random.Range(-1f, 1f) * HockeyConfig.GoalHalfWidth * 0.8f;
                if (Random.value < 0.35f) aimX = Random.Range(-1f, 1f) * HockeyConfig.HalfWidth * 1.6f; // バンクショット狙い
            }

            Vector2 target;
            if (p.y > 0f)
            {
                stuckTimer = (v.magnitude < 0.3f) ? stuckTimer + dt : 0f;
                bool behind = p.y > m.pos.y - 0.02f;
                if (behind)
                {
                    // パックの裏へ回り込む
                    float side = (m.pos.x < p.x) ? -1f : 1f;
                    if (Mathf.Abs(p.x) > HockeyConfig.HalfWidth - 0.15f) side = -Mathf.Sign(p.x);
                    target = new Vector2(p.x + side * (rr + 0.03f), p.y + rr + 0.05f);
                }
                else if (v.y > 1.2f && p.y < L * 0.55f)
                {
                    // 速いパックが向かってくる→迎撃ラインへ
                    float lineY = Mathf.Max(p.y, home.y - 0.15f);
                    float t = (lineY - p.y) / v.y;
                    target = new Vector2(PredictX(p.x, v.x, t), lineY);
                }
                else
                {
                    // 攻撃：パックの後ろから相手ゴールへ打ち抜く
                    Vector2 goal = new Vector2(aimX, -L);
                    Vector2 dir = (goal - p).normalized;
                    Vector2 strikePos = p - dir * (rr + 0.04f);
                    target = (Vector2.Distance(m.pos, strikePos) < 0.06f || stuckTimer > 1.0f)
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
                    x = PredictX(p.x, v.x, t);
                }
                x = Mathf.Clamp(x, -HockeyConfig.GoalHalfWidth - 0.05f, HockeyConfig.GoalHalfWidth + 0.05f);
                target = new Vector2(x, home.y - Mathf.Clamp01(-p.y) * 0.1f);
            }

            m.target = target + noise;
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
