namespace Unice
{
    /// <summary>A room. Its display name and phrase ("na cozinha") live in Core's Localization table, keyed by <see cref="id"/>.</summary>
    public sealed class Room
    {
        public string id;
        public float x, y, w, h;
        public Room(string id, float x, float y, float w, float h) { this.id = id; this.x = x; this.y = y; this.w = w; this.h = h; }
    }

    /// <summary>An axis-aligned rectangle (x, y = top-left corner).</summary>
    public struct RectF
    {
        public float x, y, w, h;
        public RectF(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
    }

    /// <summary>An opening between two rooms: the walkable area across the wall gap, and its center.</summary>
    public sealed class Door
    {
        public RectF rect;
        public float centerX, centerY;
        /// <summary>True for a gap in a horizontal wall (walked through vertically).</summary>
        public bool vertical;
    }

    /// <summary>
    /// A piece of furniture. When <see cref="hideSide"/> is set, a child can hide in it; it is then also a "spot"
    /// (see <see cref="Map.Spots"/>), and its phrase ("under the bed") lives in Localization as "spot." + type.
    /// </summary>
    public sealed class Furniture
    {
        /// <summary>Position in <see cref="Map.Furniture"/> (the art manifest refers to it).</summary>
        public int id;
        /// <summary>Position in <see cref="Map.Spots"/>, or -1 if it's not a hiding spot.</summary>
        public int spotIndex = -1;
        public string type;
        /// <summary>'\0' = not a hiding spot; otherwise the side (n/s/e/w) it is approached from.</summary>
        public char hideSide;
        public float x, y, w, h;
        /// <summary>Where a child hiding in it stands.</summary>
        public float standX, standY;

        // round state (host)
        /// <summary>Who is hiding in it.</summary>
        public Character occupant;
        /// <summary>A bot walking here to hide; other bots leave it alone.</summary>
        public Character reservedBy;
        /// <summary>Round clock when the Unice last opened it.</summary>
        public float checkedAt = -99;

        public float CenterX => x + w / 2;
        public float CenterY => y + h / 2;
    }
}
