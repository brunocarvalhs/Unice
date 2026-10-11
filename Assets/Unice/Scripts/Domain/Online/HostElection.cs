using System.Collections.Generic;

namespace Unice
{
    /// <summary>
    /// Who runs the match in a room. Every device decides on its own from what the peers last sent, with the same
    /// rule, so they all agree without a server. Peers silent for more than <see cref="StaleSeconds"/> seconds don't count.
    /// </summary>
    public static class HostElection
    {
        public const float StaleSeconds = 4;

        /// <summary>The host is whoever sends the freshest snapshot with the highest epoch (ties: who joined first).</summary>
        public static PeerInfo FindHost(IReadOnlyList<PeerInfo> peers, float now)
        {
            PeerInfo host = null;
            foreach (var peer in peers)
            {
                bool silent = peer.lastSnapshot == null || (!peer.isMe && now - peer.lastSnapshotAt > StaleSeconds);
                if (silent) continue;
                bool better = host == null || peer.lastSnapshotEpoch > host.lastSnapshotEpoch
                           || (peer.lastSnapshotEpoch == host.lastSnapshotEpoch && JoinedBefore(peer, host));
                if (better) host = peer;
            }
            return host;
        }

        /// <summary>
        /// With no host left, the peer who joined first (and is still around) takes over. Null when every other known
        /// peer is still connected but silent: then it's this device that lost the network, and taking over would
        /// steal the match when it comes back. Peers that really left are dropped from the list by the transport.
        /// </summary>
        public static PeerInfo FindOldestPresent(IReadOnlyList<PeerInfo> peers, float now)
        {
            PeerInfo oldest = null;
            bool othersKnown = false, othersFresh = false;
            foreach (var peer in peers)
            {
                bool known = !peer.isMe && peer.presence.name != null;
                bool fresh = known && now - peer.presenceAt < StaleSeconds;
                othersKnown |= known; othersFresh |= fresh;
                if ((peer.isMe || fresh) && (oldest == null || JoinedBefore(peer, oldest))) oldest = peer;
            }
            return othersKnown && !othersFresh ? null : oldest;
        }

        /// <summary>Tie-break everyone agrees on: earlier join time, then lower id.</summary>
        public static bool JoinedBefore(PeerInfo a, PeerInfo b) => a.presence.joinTime < b.presence.joinTime || (a.presence.joinTime == b.presence.joinTime && string.CompareOrdinal(a.id, b.id) < 0);
    }
}
