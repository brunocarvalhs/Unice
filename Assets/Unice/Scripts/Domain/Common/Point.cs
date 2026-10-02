namespace Unice
{
    /// <summary>A point on the floor plan, in "house pixels" (x right, y down), same units as the web version.</summary>
    public struct Point
    {
        public float x, y;
        public Point(float x, float y) { this.x = x; this.y = y; }
    }
}
