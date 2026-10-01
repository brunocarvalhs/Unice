using NUnit.Framework;

namespace Unice
{
    /// <summary>
    /// Characterization tests for the wire format: Game.WriteSnapshot/ApplySnapshot (host -> peers, ~15/s)
    /// and Wire.Write/TryRead (every player's presence). Pins the CURRENT encoding, including its lossy bits
    /// (short-rounded positions, quantized facing angle, "tired" collapsed to a bool) and its quirks
    /// (Phase.Home is coerced to Phase.Lobby on read; a snapshot never carries doll.lastSpot). Does not change Snapshot.cs.
    /// </summary>
    [TestFixture]
    public class SnapshotTests
    {
        [Test]
        public void WriteThenApply_FullRoundState_RoundTripsFieldByField()
        {
            var src = new Game { ep = 3, hb = 12.5f, ph = Phase.Hunt, r = 2, rounds = 5, bots = 3, tl = 45.5f, t = 10.25f };

            var ent0 = new Ent(0, "h1", "Ana", new[] { 1, 2, 3, 4, 5, 2, 1 }, false) // this device's own ent ("me")
            {
                role = Role.Unice, score = 7, turns = 2, stun = 0, passCd = 0, tp = 5,
                x = 100, y = 200, fx = 1, fy = 0, // facing angle 0 rad: round-trips through the quantized short exactly
                moving = true, loud = true, sneak = false, tired = 0
            };
            var ent1 = new Ent(1, "b0", "Bia", new[] { 0, 0, 0, 0, 0, 0, 0 }, true) // someone else's ent
            {
                role = Role.Hider, score = 3, turns = 1, stun = 1.5f, passCd = 2.0f, tp = 4,
                x = 300, y = 400, fx = 0, fy = 1, // facing angle pi/2: quantization loses exactness, needs a tolerance below
                moving = false, loud = false, sneak = true, tired = 2.7f, hidden = Map.Spots[0]
            };
            src.ents.Add(ent0); src.ents.Add(ent1);
            src.unice = ent0; src.nextU = ent1;
            src.res = new[] { 1, 0, -1, 1 };
            src.doll.holder = ent1; src.doll.giggleT = 4.5f; src.doll.hasLast = true; src.doll.lastX = 250; src.doll.lastY = 350; src.doll.lastT = src.t - 2f;
            src.doll.lastSpot = Map.Spots[2]; // never actually goes on the wire - see assertion below
            src.evSeq = 42;
            src.evs.Add(new GameEvent { seq = 1, type = EvType.Catch, a = 0, b = 1, c = 1, d = 100, e = 200, s = null });
            src.evs.Add(new GameEvent { seq = 2, type = EvType.Host, s = "Ana" });

            try
            {
                var bytes = src.WriteSnapshot();
                var dst = new Game();
                var me = dst.ApplySnapshot(bytes, "h1", null);

                Assert.AreEqual(3, dst.ep); Assert.AreEqual(12.5f, dst.hb); Assert.AreEqual(Phase.Hunt, dst.ph);
                Assert.AreEqual(2, dst.r); Assert.AreEqual(5, dst.rounds); Assert.AreEqual(3, dst.bots);
                Assert.AreEqual(45.5f, dst.tl); Assert.AreEqual(10.25f, dst.t);

                Assert.AreSame(dst.ents[0], dst.unice); Assert.AreSame(dst.ents[1], dst.nextU);
                CollectionAssert.AreEqual(new[] { 1, 0, -1, 1 }, dst.res);

                Assert.AreSame(dst.ents[1], dst.doll.holder); Assert.AreEqual(4.5f, dst.doll.giggleT);
                Assert.IsTrue(dst.doll.hasLast); Assert.AreEqual(250, dst.doll.lastX); Assert.AreEqual(350, dst.doll.lastY);
                Assert.AreEqual(8.25f, dst.doll.lastT, 0.001f); // t - age, reconstructed from the wire
                Assert.IsNull(dst.doll.lastSpot, "lastSpot is never serialized; ApplySnapshot always resets it to null");

                Assert.AreEqual(42, dst.evSeq); Assert.AreEqual(2, dst.evs.Count);
                Assert.AreEqual(EvType.Catch, dst.evs[0].type); Assert.AreEqual(1, dst.evs[0].b); Assert.AreEqual(1, dst.evs[0].c);
                Assert.AreEqual(100, dst.evs[0].d); Assert.AreEqual(200, dst.evs[0].e); Assert.IsNull(dst.evs[0].s);
                Assert.AreEqual(EvType.Host, dst.evs[1].type); Assert.AreEqual("Ana", dst.evs[1].s);

                var me0 = dst.ents[0];
                Assert.AreSame(me0, me, "the ent whose pid matches myId is returned");
                Assert.AreEqual("Ana", me0.name); Assert.AreEqual("h1", me0.pid);
                CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 2, 1 }, me0.k);
                Assert.IsFalse(me0.bot); Assert.AreEqual(Role.Unice, me0.role);
                Assert.AreEqual(0, me0.passCd); Assert.AreEqual(7, me0.score); Assert.AreEqual(2, me0.turns); Assert.AreEqual(0, me0.stun); Assert.AreEqual(5, me0.tp);
                Assert.AreEqual(100, me0.x); Assert.AreEqual(200, me0.y);
                Assert.AreEqual(1, me0.fx, 0.001f); Assert.AreEqual(0, me0.fy, 0.001f);
                Assert.IsNull(me0.hidden);
                Assert.IsFalse(me0.moving, "ApplySnapshot never touches moving/loud/sneak for your OWN ent, only for others");
                Assert.IsFalse(me0.loud); Assert.IsFalse(me0.sneak);

                var other = dst.ents[1];
                Assert.AreEqual("Bia", other.name); Assert.AreEqual("b0", other.pid);
                Assert.IsTrue(other.bot); Assert.AreEqual(Role.Hider, other.role);
                Assert.AreEqual(3, other.score); Assert.AreEqual(1, other.turns); Assert.AreEqual(1.5f, other.stun); Assert.AreEqual(2.0f, other.passCd); Assert.AreEqual(4, other.tp);
                Assert.AreEqual(300, other.x); Assert.AreEqual(400, other.y); Assert.AreEqual(300, other.tx); Assert.AreEqual(400, other.ty);
                Assert.AreEqual(0, other.fx, 0.001f); Assert.AreEqual(1, other.fy, 0.001f);
                Assert.IsFalse(other.moving); Assert.IsFalse(other.loud); Assert.IsTrue(other.sneak, "these DO carry over for other players' ents");
                Assert.AreEqual(1, other.tired, "only whether tired>0 survives the wire, not its magnitude (src had 2.7)");
                Assert.AreSame(Map.Spots[0], other.hidden);
                Assert.AreSame(other, Map.Spots[0].occ, "ApplySnapshot re-occupies the spot as a side effect");
            }
            finally { Map.Spots[0].occ = null; } // don't leak this into other tests sharing the static Map
        }

        [Test]
        public void WriteThenApply_EmptyMatchState_RoundTripsCleanly()
        {
            var src = new Game { ph = Phase.Lobby };

            var bytes = src.WriteSnapshot();
            var dst = new Game();
            var me = dst.ApplySnapshot(bytes, "nobody", null);

            Assert.AreEqual(Phase.Lobby, dst.ph);
            Assert.AreEqual(0, dst.ents.Count);
            Assert.IsNull(dst.unice); Assert.IsNull(dst.nextU); Assert.IsNull(dst.res);
            Assert.IsNull(dst.doll.holder); Assert.IsFalse(dst.doll.hasLast); Assert.AreEqual(Rules.GiggleEvery, dst.doll.giggleT);
            Assert.AreEqual(0, dst.evs.Count); Assert.AreEqual(0, dst.evSeq);
            Assert.IsNull(me, "no ents at all means nobody to return, even for a spectator's own id");
        }

        [Test]
        public void WriteThenApply_PhaseHome_IsCoercedToLobbyOnRead()
        {
            // Documented quirk, not a bug: a peer never mirrors Phase.Home, it always lands on Phase.Lobby instead.
            var src = new Game { ph = Phase.Home };

            var dst = new Game();
            dst.ApplySnapshot(src.WriteSnapshot(), "x", null);

            Assert.AreEqual(Phase.Lobby, dst.ph);
        }

        [Test]
        public void WriteThenApply_ReturnsTheEntMatchingMyId_OrNullWhenSpectating()
        {
            var src = new Game();
            src.ents.Add(new Ent(0, "h1", "Ana", new int[7], false));
            src.ents.Add(new Ent(1, "h2", "Beto", new int[7], false));
            var bytes = src.WriteSnapshot();

            var dst1 = new Game();
            var me1 = dst1.ApplySnapshot(bytes, "h2", null);
            Assert.AreSame(dst1.ents[1], me1);

            var dst2 = new Game();
            var me2 = dst2.ApplySnapshot(bytes, "ghost", null);
            Assert.IsNull(me2);
        }

        [Test]
        public void ReadEpoch_ReturnsTheEpoch_OrMinusOneWhenMalformed()
        {
            var bytes = new Game { ep = 77 }.WriteSnapshot();

            Assert.AreEqual(77, Game.ReadEpoch(bytes));
            Assert.AreEqual(-1, Game.ReadEpoch(null));
            Assert.AreEqual(-1, Game.ReadEpoch(new byte[] { 9, 9, 9 })); // shorter than 5 bytes
            Assert.AreEqual(-1, Game.ReadEpoch(new byte[] { 0, 0, 0, 0, 0, 0 })); // wrong version byte (not 1)
        }

        // ---------- Wire: per-player presence, sent far more often than a full snapshot ----------

        [Test]
        public void Wire_WriteThenTryRead_RoundTripsPresenceWithPosition()
        {
            var p = new Presence
            {
                name = "Ana", kit = Kit.Pack(new[] { 1, 1, 1, 1, 1, 1, 1 }), joinT = 123456789L, rdy = true, tp = 5,
                hasPos = true, x = 10.5f, y = 20.5f, fx = 0.6f, fy = 0.8f, sneak = true,
                aSeq = 9, aType = Act.Pass, aArg = 3
            };

            bool ok = Wire.TryRead(Wire.Write(p), out var p2);

            Assert.IsTrue(ok);
            Assert.AreEqual("Ana", p2.name); Assert.AreEqual(p.kit, p2.kit); Assert.AreEqual(123456789L, p2.joinT);
            Assert.IsTrue(p2.rdy); Assert.AreEqual(5, p2.tp);
            Assert.IsTrue(p2.hasPos); Assert.AreEqual(10.5f, p2.x); Assert.AreEqual(20.5f, p2.y); Assert.AreEqual(0.6f, p2.fx); Assert.AreEqual(0.8f, p2.fy);
            Assert.IsTrue(p2.sneak); Assert.AreEqual(9, p2.aSeq); Assert.AreEqual(Act.Pass, p2.aType); Assert.AreEqual(3, p2.aArg);
        }

        [Test]
        public void Wire_WriteThenTryRead_RoundTripsPresenceWithoutPosition()
        {
            var p = new Presence { name = "Bob", kit = 0, joinT = 0, rdy = false, tp = 0, hasPos = false, aSeq = 0, aType = Act.None, aArg = 0 };

            Wire.TryRead(Wire.Write(p), out var p2);

            Assert.IsFalse(p2.hasPos);
            Assert.AreEqual(0, p2.x); Assert.AreEqual(0, p2.y); // position fields are simply skipped on the wire, defaulted on read
        }

        [Test]
        public void Wire_TryRead_NaNPosition_StillSucceedsButClearsHasPos()
        {
            var p = new Presence { name = "A", hasPos = true, x = float.NaN, y = 1, fx = 1, fy = 0 };

            bool ok = Wire.TryRead(Wire.Write(p), out var p2);

            Assert.IsTrue(ok);
            Assert.IsFalse(p2.hasPos, "a NaN coordinate is sanitized by clearing hasPos, not by rejecting the whole payload");
        }

        [Test]
        public void Wire_TryRead_MalformedOrNullData_ReturnsFalse()
        {
            Assert.IsFalse(Wire.TryRead(new byte[] { 0 }, out _), "wrong version marker");
            Assert.IsFalse(Wire.TryRead(new byte[] { 1 }, out _), "truncated right after the version byte");
            Assert.IsFalse(Wire.TryRead(null, out _), "null payload");
        }
    }
}
