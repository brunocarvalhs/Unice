using System.Collections.Generic;
using NUnit.Framework;

namespace Unice
{
    [TestFixture]
    public class HostElectionTests
    {
        static PeerInfo Peer(string id, long joinT, int ep = 0, float snapAt = 0, bool me = false, float presAt = 0, bool snap = true) => new PeerInfo
        {
            id = id, isMe = me, presence = new Presence { name = id, joinTime = joinT },
            lastSnapshot = snap ? new byte[1] : null, lastSnapshotEpoch = ep, lastSnapshotAt = snapAt, presenceAt = presAt,
        };

        [Test]
        public void Find_HighestEpochWins()
        {
            var a = Peer("a", 1, ep: 1); var b = Peer("b", 2, ep: 2);
            Assert.AreSame(b, HostElection.FindHost(new List<PeerInfo> { a, b }, 1));
        }

        [Test]
        public void Find_SameEpoch_FirstToJoinWins()
        {
            var a = Peer("a", 5, ep: 1); var b = Peer("b", 2, ep: 1);
            Assert.AreSame(b, HostElection.FindHost(new List<PeerInfo> { a, b }, 1));
        }

        [Test]
        public void Find_SameJoinTime_LowestIdWins()
        {
            var a = Peer("b", 1, ep: 1); var b = Peer("a", 1, ep: 1);
            Assert.AreSame(b, HostElection.FindHost(new List<PeerInfo> { a, b }, 1));
        }

        [Test]
        public void Find_IgnoresStaleOthersButNotMe()
        {
            var other = Peer("o", 1, ep: 5, snapAt: 0); var me = Peer("me", 9, ep: 1, snapAt: 0, me: true);
            Assert.AreSame(me, HostElection.FindHost(new List<PeerInfo> { other, me }, HostElection.StaleSeconds + 1));
        }

        [Test]
        public void Find_NoSnapshots_NoHost() => Assert.IsNull(HostElection.FindHost(new List<PeerInfo> { Peer("a", 1, snap: false) }, 0));

        [Test]
        public void Oldest_SkipsPeersThatWentQuiet()
        {
            var gone = Peer("g", 1, presAt: 0); var me = Peer("me", 2, me: true, presAt: 0);
            Assert.AreSame(me, HostElection.FindOldestPresent(new List<PeerInfo> { gone, me }, HostElection.StaleSeconds + 1));
        }

        [Test]
        public void Oldest_FirstToJoinAmongPresent()
        {
            var a = Peer("a", 1, presAt: 10); var me = Peer("me", 2, me: true);
            Assert.AreSame(a, HostElection.FindOldestPresent(new List<PeerInfo> { me, a }, 11));
        }
    }
}
