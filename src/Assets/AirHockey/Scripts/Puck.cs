using UnityEngine;

namespace AirHockey
{
    /// <summary>パック。物理はGameManagerが計算し、ここでは見た目を反映する。</summary>
    public class Puck : MonoBehaviour
    {
        [HideInInspector] public Vector2 pos, vel;
        [HideInInspector] public float spin;
        [HideInInspector] public float drop;

        public void ResetTo(Vector2 p)
        {
            pos = p; vel = Vector2.zero; spin = 0f; drop = 0f;
            Apply(0f);
        }

        public void Apply(float dt)
        {
            transform.position = HockeyConfig.ToWorld(pos, -drop);
            transform.Rotate(0f, spin * dt, 0f, Space.World);
            spin = Mathf.Lerp(spin, 0f, dt * 0.8f);
        }
    }
}
