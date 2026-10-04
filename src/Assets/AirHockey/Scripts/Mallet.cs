using UnityEngine;

namespace AirHockey
{
    /// <summary>マレット（プレイヤー/NPC共通）。座標はテーブル平面(x,z)の2D。</summary>
    public class Mallet : MonoBehaviour
    {
        public bool isPlayer;
        public float maxSpeed = 10f;

        /// <summary>加速度の上限（m/s²）。0なら無制限（マウス操作用）</summary>
        [HideInInspector] public float maxAccel;

        [HideInInspector] public Vector2 pos, prevPos, vel, target;
        Vector2 moveVel;

        public void ResetTo(Vector2 p)
        {
            pos = prevPos = target = p;
            vel = moveVel = Vector2.zero;
            Apply();
        }

        public void Step(float dt)
        {
            prevPos = pos;
            Vector2 d = Clamp(target) - pos;
            if (maxAccel > 0f)
            {
                // 加速度制限つきの移動：止まれる速度までしか出さず、急な切り返しもできない
                float dist = d.magnitude;
                float speed = Mathf.Min(maxSpeed, Mathf.Sqrt(2f * maxAccel * dist));
                Vector2 desired = dist > 1e-4f ? d / dist * speed : Vector2.zero;
                moveVel = Vector2.MoveTowards(moveVel, desired, maxAccel * dt);
                d = moveVel * dt;
            }
            float max = maxSpeed * dt; // 1フレームで動ける距離の上限
            if (d.magnitude > max) d = d.normalized * max;
            pos = Clamp(pos + d);
            if (maxAccel > 0f) moveVel = (pos - prevPos) / Mathf.Max(dt, 1e-5f);
            vel = Vector2.Lerp(vel, (pos - prevPos) / Mathf.Max(dt, 1e-5f), 0.7f);
            Apply();
        }

        public Vector2 Clamp(Vector2 p)
        {
            float r = HockeyConfig.MalletRadius;
            p.x = Mathf.Clamp(p.x, -HockeyConfig.HalfWidth + r, HockeyConfig.HalfWidth - r);
            p.y = isPlayer
                ? Mathf.Clamp(p.y, -HockeyConfig.HalfLength + r, -r)
                : Mathf.Clamp(p.y, r, HockeyConfig.HalfLength - r);
            return p;
        }

        void Apply() => transform.position = HockeyConfig.ToWorld(pos);
    }
}
