namespace Unice
{
    /// <summary>The cursed doll: who has it, when it giggles next, and where it was last heard.</summary>
    public sealed class DollState
    {
        public Character holder;
        /// <summary>Seconds until the next giggle (counts down only while the holder is hidden).</summary>
        public float giggleTimer = Rules.GiggleInterval;

        /// <summary>The last giggle (or laugh) this round: where and when, so the Unice knows where to search.</summary>
        public bool hasLastGiggle;
        public float lastGiggleX, lastGiggleY, lastGiggleTime;
        public Furniture lastGiggleSpot;
    }
}
