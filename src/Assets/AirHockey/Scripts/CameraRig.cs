using UnityEngine;

namespace AirHockey
{
    /// <summary>主観視点カメラ。プレイヤーの手の動きに少し追従し、揺れ演出を受け付ける。</summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }
        public Mallet follow;
        public Vector3 basePos = new Vector3(0f, 1.42f, -1.72f);
        public Vector3 lookAt = new Vector3(0f, 0.72f, -0.05f);

        float shake;
        float fovKick;
        Camera cam;
        float baseFov;

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            baseFov = cam ? cam.fieldOfView : 60f;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);
        public void Kick(float fov) => fovKick = Mathf.Max(fovKick, fov);

        void LateUpdate()
        {
            Vector3 pos = basePos;
            if (follow)
            {
                pos.x += follow.pos.x * 0.18f;
                pos.z += (follow.pos.y + 0.8f) * 0.06f;
            }
            Vector3 look = lookAt + new Vector3(follow ? follow.pos.x * 0.1f : 0f, 0f, 0f);
            float t = Time.unscaledTime;
            pos.y += Mathf.Sin(t * 1.1f) * 0.004f; // 呼吸のような微かな揺れ
            Vector3 shakeOffset = Vector3.zero;
            if (shake > 0f)
            {
                shakeOffset = new Vector3(Mathf.PerlinNoise(t * 40f, 0f) - 0.5f, Mathf.PerlinNoise(0f, t * 40f) - 0.5f, 0f) * Mathf.Min(shake, 1.5f) * 0.05f;
                shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 2.5f);
            }
            Vector3 smooth = Vector3.Lerp(transform.position - lastShake, pos, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
            transform.position = smooth + shakeOffset;
            lastShake = shakeOffset;
            transform.rotation = Quaternion.LookRotation(look - smooth);
            if (cam)
            {
                cam.fieldOfView = baseFov + fovKick;
                fovKick = Mathf.MoveTowards(fovKick, 0f, Time.unscaledDeltaTime * 12f);
            }
        }

        Vector3 lastShake;
    }
}
