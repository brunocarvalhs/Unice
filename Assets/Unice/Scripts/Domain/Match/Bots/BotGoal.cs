namespace Unice
{
    /// <summary>
    /// Where the Unice bot is going. Higher <see cref="priority"/> goals replace lower ones:
    /// 0 patrol, 1 a noise or a follow-up search, 2 where it last saw someone, 3 a giggle.
    /// </summary>
    public sealed class BotGoal
    {
        public enum Kind { Position, Spot }

        public Kind kind;
        /// <summary>For <see cref="Kind.Spot"/>: the furniture to open (x/y are filled from it).</summary>
        public Furniture spot;
        public float x, y;
        public int priority;
        /// <summary>How many more spots to search around <see cref="searchCenterX"/>/<see cref="searchCenterY"/> after this one.</summary>
        public int hopsLeft;
        public bool hasSearchCenter;
        public float searchCenterX, searchCenterY;
    }
}
