using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Shared constants. The web prototype worked in pixels (tile = 32 px).
    /// Here 1 Unity unit = 32 px, so every old tuning value converts with <see cref="Px"/>.
    /// </summary>
    public static class GameSettings
    {
        public const float PixelsPerUnit = 32f;
        public const float Gravity = 1200f / PixelsPerUnit;        // 37.5 u/s^2 (web: 1200 px/s^2)
        public const float MaxWormSpeed = 1400f / PixelsPerUnit;   // 43.75 u/s (web: 1400 px/s)
        public const float PhysicsStepRate = 120f;                 // web: 120 Hz fixed step

        public static float Px(float pixels) { return pixels / PixelsPerUnit; }
    }
}
