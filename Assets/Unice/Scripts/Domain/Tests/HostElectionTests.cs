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
            var quiet = Peer("q", 1, presAt: 0); var other = Peer("o", 3, presAt: 10); var me = Peer("me", 2, me: true);
            Assert.AreSame(me, HostElection.FindOldestPresent(new List<PeerInfo> { quiet, other, me }, 11));
        }

        [Test]
        public void FindOldestPresent_OnlyMeFreshWhileOthersStale_ReturnsNull()
        {
            var host = Peer("h", 1, presAt: 0); var other = Peer("o", 3, presAt: 0); var me = Peer("me", 2, me: true);

            var oldest = HostElection.FindOldestPresent(new List<PeerInfo> { host, other, me }, HostElection.StaleSeconds + 1);

            Assert.IsNull(oldest);
        }

        [Test]
        public void FindOldestPresent_OthersGoneFromPeers_ReturnsMe()
        {
            var me = Peer("me", 2, me: true);

            var oldest = HostElection.FindOldestPresent(new List<PeerInfo> { me }, HostElection.StaleSeconds + 1);

            Assert.AreSame(me, oldest);
        }

        [Test]
        public void FindOldestPresent_OtherNeverSentPresence_ReturnsMe()
        {
            var unknown = Peer("u", 1); unknown.presence = default; var me = Peer("me", 2, me: true);

            var oldest = HostElection.FindOldestPresent(new List<PeerInfo> { unknown, me }, HostElection.StaleSeconds + 1);

            Assert.AreSame(me, oldest);
        }

        [Test]
        public void Oldest_FirstToJoinAmongPresent()
        {
            var a = Peer("a", 1, presAt: 10); var me = Peer("me", 2, me: true);
            Assert.AreSame(a, HostElection.FindOldestPresent(new List<PeerInfo> { me, a }, 11));
        }
    }
}
