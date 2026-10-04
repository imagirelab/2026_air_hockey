using UnityEngine;

namespace AirHockey
{
    /// <summary>マレット（プレイヤー/NPC共通）。座標はテーブル平面(x,z)の2D。</summary>
    public class Mallet : MonoBehaviour
    {
        public bool isPlayer;
        public float maxSpeed = 10f;

        [HideInInspector] public Vector2 pos, prevPos, vel, target;

        public void ResetTo(Vector2 p)
        {
            pos = prevPos = target = p;
            vel = Vector2.zero;
            Apply();
        }

        public void Step(float dt)
        {
            prevPos = pos;
            Vector2 d = Clamp(target) - pos;
            float max = maxSpeed * dt;
            if (d.magnitude > max) d = d.normalized * max;
            pos = Clamp(pos + d);
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
