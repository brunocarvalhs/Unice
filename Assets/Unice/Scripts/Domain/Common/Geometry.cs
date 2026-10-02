using System;

namespace Unice
{
    /// <summary>Small math helpers on house-pixel coordinates.</summary>
    public static class Geometry
    {
        public static float Clamp(float value, float min, float max) => value < min ? min : (value > max ? max : value);

        public static float Distance(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx, dy = ay - by;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        public static float Distance(Character a, Character b) => Distance(a.x, a.y, b.x, b.y);
    }
}
