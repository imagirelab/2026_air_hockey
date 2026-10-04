using UnityEngine;

namespace AirHockey
{
    /// <summary>テーブル寸法やルールの定数</summary>
    public static class HockeyConfig
    {
        public const float HalfWidth = 0.6f;
        public const float HalfLength = 1.1f;
        public const float GoalHalfWidth = 0.19f;
        public const float PuckRadius = 0.045f;
        public const float MalletRadius = 0.065f;
        public const float TableY = 0.8f;
        public const int WinScore = 5;
        public const float MaxPuckSpeed = 7.5f;

        public static Vector3 ToWorld(Vector2 p, float yOffset = 0f) => new Vector3(p.x, TableY + yOffset, p.y);
    }
}
