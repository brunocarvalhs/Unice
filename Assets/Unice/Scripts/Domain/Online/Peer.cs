namespace Unice
{
    /// <summary>What a player tells everyone else about themselves (the web version's "presence").</summary>
    public struct Presence
    {
        public string name; public int kit; public long joinT; public bool rdy; public int tp;
        public bool hasPos; public float x, y, fx, fy; public bool sneak, talking;
        public int aSeq; public Act aType; public int aArg;
    }

    /// <summary>Another device in the room as this one knows it: its last presence and last snapshot.</summary>
    public sealed class PeerInfo
    {
        public string id; public bool isMe; public Presence pres;
        public byte[] lastSnap; public int lastSnapEp; public float lastSnapAt = -999;
        public float presAt = -999;   // when their presence last arrived (stale = app paused or gone)
    }
}
