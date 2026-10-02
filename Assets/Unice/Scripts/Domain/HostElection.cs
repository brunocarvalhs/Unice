using System.Collections.Generic;

namespace Unice
{
    /// <summary>
    /// Who runs the match in a room. Every device decides on its own from what the peers last sent, with the same
    /// rule, so they all agree without a server. Peers silent for more than <see cref="Stale"/> seconds don't count.
    /// </summary>
    public static class HostElection
    {
        public const float Stale = 4;

        /// <summary>The host is whoever sends the freshest snapshot with the highest epoch (ties: who joined first).</summary>
        public static PeerInfo Find(IReadOnlyList<PeerInfo> peers, float now)
        {
            PeerInfo h = null;
            foreach (var p in peers)
            {
                if (p.lastSnap == null || (!p.isMe && now - p.lastSnapAt > Stale)) continue;
                if (h == null || p.lastSnapEp > h.lastSnapEp || (p.lastSnapEp == h.lastSnapEp && Older(p, h))) h = p;
            }
            return h;
        }

        /// <summary>With no host left, the peer who joined first (and is still around) takes over.</summary>
        public static PeerInfo Oldest(IReadOnlyList<PeerInfo> peers, float now)
        {
            PeerInfo oldest = null;
            foreach (var p in peers) if ((p.isMe || (p.pres.name != null && now - p.presAt < Stale)) && (oldest == null || Older(p, oldest))) oldest = p;
            return oldest;
        }

        public static bool Older(PeerInfo a, PeerInfo b) => a.pres.joinT < b.pres.joinT || (a.pres.joinT == b.pres.joinT && string.CompareOrdinal(a.id, b.id) < 0);
    }
}
