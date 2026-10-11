using NUnit.Framework;

namespace Unice
{
    /// <summary>
    /// Characterization tests for the wire format: Game.WriteSnapshot/ApplySnapshot (host -> peers, ~15/s)
    /// and PresenceCodec.Write/TryRead (every player's presence). Pins the CURRENT encoding, including its lossy bits
    /// (short-rounded positions, quantized facing angle, "tired" collapsed to a bool) and its quirks
    /// (Phase.Home is coerced to Phase.Lobby on read; a snapshot never carries doll.lastSpot). Does not change Snapshot.cs.
    /// </summary>
    [TestFixture]
    public class SnapshotTests
    {
        /// <summary>Any fixed value works: it only has to be the same on every run.</summary>
        const int TestSeed = 1234;

        /// <summary>Same map and same dice on every run, whatever fixture ran before (Map and GameRandom are static).</summary>
        [SetUp]
        public void SetUp()
        {
            Map.Load(MapHouse.Build());
            GameRandom.Seed(TestSeed);
        }

        [Test]
        public void WriteThenApply_FullRoundState_RoundTripsFieldByField()
        {
            var src = new Game { epoch = 3, hostClock = 12.5f, phase = Phase.Hunt, round = 2, totalRounds = 5, botCount = 3, timeLeft = 45.5f, roundClock = 10.25f };

            var ent0 = new Character(0, "h1", "Ana", new[] { 1, 2, 3, 4, 5, 2, 1 }, false) // this device's own ent ("me")
            {
                role = Role.Unice, score = 7, uniceTurns = 2, stunTime = 0, passCooldown = 0, teleportSeq = 5,
                x = 100, y = 200, faceX = 1, faceY = 0, // facing angle 0 rad: round-trips through the quantized short exactly
                moving = true, loud = true, sneaking = false, tiredTime = 0
            };
            var ent1 = new Character(1, "b0", "Bia", new[] { 0, 0, 0, 0, 0, 0, 0 }, true) // someone else's ent
            {
                role = Role.Hider, score = 3, uniceTurns = 1, stunTime = 1.5f, passCooldown = 2.0f, teleportSeq = 4,
                x = 300, y = 400, faceX = 0, faceY = 1, // facing angle pi/2: quantization loses exactness, needs a tolerance below
                moving = false, loud = false, sneaking = true, tiredTime = 2.7f, hidingSpot = Map.Spots[0]
            };
            src.characters.Add(ent0); src.characters.Add(ent1);
            src.unice = ent0; src.nextUnice = ent1;
            src.roundResult = new RoundResult { uniceWon = true, dollHolder = 0, holderSpotIndex = -1, nextUnice = 1 };
            src.doll.holder = ent1; src.doll.giggleTimer = 4.5f; src.doll.hasLastGiggle = true; src.doll.lastGiggleX = 250; src.doll.lastGiggleY = 350; src.doll.lastGiggleTime = src.roundClock - 2f;
            src.doll.lastGiggleSpot = Map.Spots[2]; // never actually goes on the wire - see assertion below
            src.eventSeq = 42;
            src.events.Add(new GameEvent { sequence = 1, type = GameEventType.Catch, actor = 0, target = 1, value = 1, x = 100, y = 200, text = null });
            src.events.Add(new GameEvent { sequence = 2, type = GameEventType.Host, text = "Ana" });

            try
            {
                var bytes = src.WriteSnapshot();
                var dst = new Game();
                var me = dst.ApplySnapshot(bytes, "h1", null);

                Assert.AreEqual(3, dst.epoch); Assert.AreEqual(12.5f, dst.hostClock); Assert.AreEqual(Phase.Hunt, dst.phase);
                Assert.AreEqual(2, dst.round); Assert.AreEqual(5, dst.totalRounds); Assert.AreEqual(3, dst.botCount);
                Assert.AreEqual(45.5f, dst.timeLeft); Assert.AreEqual(10.25f, dst.roundClock);

                Assert.AreSame(dst.characters[0], dst.unice); Assert.AreSame(dst.characters[1], dst.nextUnice);
                Assert.IsTrue(dst.roundResult.uniceWon); Assert.AreEqual(0, dst.roundResult.dollHolder); Assert.AreEqual(-1, dst.roundResult.holderSpotIndex); Assert.AreEqual(1, dst.roundResult.nextUnice);

                Assert.AreSame(dst.characters[1], dst.doll.holder); Assert.AreEqual(4.5f, dst.doll.giggleTimer);
                Assert.IsTrue(dst.doll.hasLastGiggle); Assert.AreEqual(250, dst.doll.lastGiggleX); Assert.AreEqual(350, dst.doll.lastGiggleY);
                Assert.AreEqual(8.25f, dst.doll.lastGiggleTime, 0.001f); // t - age, reconstructed from the wire
                Assert.IsNull(dst.doll.lastGiggleSpot, "lastSpot is never serialized; ApplySnapshot always resets it to null");

                Assert.AreEqual(42, dst.eventSeq); Assert.AreEqual(2, dst.events.Count);
                Assert.AreEqual(GameEventType.Catch, dst.events[0].type); Assert.AreEqual(0, dst.events[0].actor); Assert.AreEqual(1, dst.events[0].target); Assert.AreEqual(1, dst.events[0].value);
                Assert.AreEqual(100, dst.events[0].x); Assert.AreEqual(200, dst.events[0].y); Assert.IsNull(dst.events[0].text);
                Assert.AreEqual(GameEventType.Host, dst.events[1].type); Assert.AreEqual("Ana", dst.events[1].text);

                var me0 = dst.characters[0];
                Assert.AreSame(me0, me, "the ent whose pid matches myId is returned");
                Assert.AreEqual("Ana", me0.name); Assert.AreEqual("h1", me0.playerId);
                CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 2, 1 }, me0.kit);
                Assert.IsFalse(me0.isBot); Assert.AreEqual(Role.Unice, me0.role);
                Assert.AreEqual(0, me0.passCooldown); Assert.AreEqual(7, me0.score); Assert.AreEqual(2, me0.uniceTurns); Assert.AreEqual(0, me0.stunTime); Assert.AreEqual(5, me0.teleportSeq);
                Assert.AreEqual(100, me0.x); Assert.AreEqual(200, me0.y);
                Assert.AreEqual(1, me0.faceX, 0.001f); Assert.AreEqual(0, me0.faceY, 0.001f);
                Assert.IsNull(me0.hidingSpot);
                Assert.IsFalse(me0.moving, "ApplySnapshot never touches moving/loud/sneak for your OWN ent, only for others");
                Assert.IsFalse(me0.loud); Assert.IsFalse(me0.sneaking);

                var other = dst.characters[1];
                Assert.AreEqual("Bia", other.name); Assert.AreEqual("b0", other.playerId);
                Assert.IsTrue(other.isBot); Assert.AreEqual(Role.Hider, other.role);
                Assert.AreEqual(3, other.score); Assert.AreEqual(1, other.uniceTurns); Assert.AreEqual(1.5f, other.stunTime); Assert.AreEqual(2.0f, other.passCooldown); Assert.AreEqual(4, other.teleportSeq);
                Assert.AreEqual(300, other.x); Assert.AreEqual(400, other.y); Assert.AreEqual(300, other.targetX); Assert.AreEqual(400, other.targetY);
                Assert.AreEqual(0, other.faceX, 0.001f); Assert.AreEqual(1, other.faceY, 0.001f);
                Assert.IsFalse(other.moving); Assert.IsFalse(other.loud); Assert.IsTrue(other.sneaking, "these DO carry over for other players' ents");
                Assert.AreEqual(1, other.tiredTime, "only whether tired>0 survives the wire, not its magnitude (src had 2.7)");
                Assert.AreSame(Map.Spots[0], other.hidingSpot);
                Assert.AreSame(other, Map.Spots[0].occupant, "ApplySnapshot re-occupies the spot as a side effect");
            }
            finally { Map.Spots[0].occupant = null; } // don't leak this into other tests sharing the static Map
        }

        [Test]
        public void WriteThenApply_EmptyMatchState_RoundTripsCleanly()
        {
            var src = new Game { phase = Phase.Lobby };

            var bytes = src.WriteSnapshot();
            var dst = new Game();
            var me = dst.ApplySnapshot(bytes, "nobody", null);

            Assert.AreEqual(Phase.Lobby, dst.phase);
            Assert.AreEqual(0, dst.characters.Count);
            Assert.IsNull(dst.unice); Assert.IsNull(dst.nextUnice); Assert.IsNull(dst.roundResult);
            Assert.IsNull(dst.doll.holder); Assert.IsFalse(dst.doll.hasLastGiggle); Assert.AreEqual(Rules.GiggleInterval, dst.doll.giggleTimer);
            Assert.AreEqual(0, dst.events.Count); Assert.AreEqual(0, dst.eventSeq);
            Assert.IsNull(me, "no ents at all means nobody to return, even for a spectator's own id");
        }

        [Test]
        public void WriteThenApply_PhaseHome_IsCoercedToLobbyOnRead()
        {
            // Documented quirk, not a bug: a peer never mirrors Phase.Home, it always lands on Phase.Lobby instead.
            var src = new Game { phase = Phase.Home };

            var dst = new Game();
            dst.ApplySnapshot(src.WriteSnapshot(), "x", null);

            Assert.AreEqual(Phase.Lobby, dst.phase);
        }

        [Test]
        public void WriteThenApply_ReturnsTheEntMatchingMyId_OrNullWhenSpectating()
        {
            var src = new Game();
            src.characters.Add(new Character(0, "h1", "Ana", new int[7], false));
            src.characters.Add(new Character(1, "h2", "Beto", new int[7], false));
            var bytes = src.WriteSnapshot();

            var dst1 = new Game();
            var me1 = dst1.ApplySnapshot(bytes, "h2", null);
            Assert.AreSame(dst1.characters[1], me1);

            var dst2 = new Game();
            var me2 = dst2.ApplySnapshot(bytes, "ghost", null);
            Assert.IsNull(me2);
        }

        [Test]
        public void ReadEpoch_ReturnsTheEpoch_OrMinusOneWhenMalformed()
        {
            var bytes = new Game { epoch = 77 }.WriteSnapshot();

            Assert.AreEqual(77, Game.ReadEpoch(bytes));
            Assert.AreEqual(-1, Game.ReadEpoch(null));
            Assert.AreEqual(-1, Game.ReadEpoch(new byte[] { 9, 9, 9 })); // shorter than 5 bytes
            Assert.AreEqual(-1, Game.ReadEpoch(new byte[] { 0, 0, 0, 0, 0, 0 })); // wrong version byte (not SnapshotVersion = 5)
        }

        [Test]
        public void ApplySnapshot_DifferentVersion_LeavesGameUnchanged()
        {
            var dst = new Game { epoch = 3, phase = Phase.Hunt, round = 2, timeLeft = 45.5f };
            var ent = new Character(0, "h1", "Ana", new int[7], false) { x = 100, y = 200, score = 4 };
            dst.characters.Add(ent); dst.unice = ent; dst.eventSeq = 9;
            var before = dst.WriteSnapshot();
            var incoming = new Game { epoch = 99, phase = Phase.Lobby }.WriteSnapshot();
            incoming[0]++;   // a version this build doesn't know

            var me = dst.ApplySnapshot(incoming, "h1", ent);

            Assert.AreSame(ent, me, "returns the character it was given");
            CollectionAssert.AreEqual(before, dst.WriteSnapshot(), "every snapshot field is unchanged");
        }

        [Test]
        public void ApplySnapshot_UnknownMapId_LeavesGameUnchanged()
        {
            var dst = new Game { epoch = 3, phase = Phase.Hunt, round = 2, timeLeft = 45.5f };
            var ent = new Character(0, "h1", "Ana", new int[7], false) { x = 100, y = 200, score = 4 };
            dst.characters.Add(ent); dst.unice = ent; dst.eventSeq = 9;
            var before = dst.WriteSnapshot();
            var incoming = new Game { epoch = 99, phase = Phase.Lobby, mapId = "map-from-a-newer-build" }.WriteSnapshot();

            var me = dst.ApplySnapshot(incoming, "h1", ent);

            Assert.AreSame(ent, me, "returns the character it was given");
            CollectionAssert.AreEqual(before, dst.WriteSnapshot(), "every snapshot field is unchanged");
            Assert.AreEqual(MapHouse.Id, Map.Id, "the loaded map is unchanged");
        }

        /// <summary>
        /// Golden bytes: the exact wire layout of a small known state (generated from the current code).
        /// If this fails, the snapshot layout changed: bump Game.SnapshotVersion and regenerate the array —
        /// devices on an older build must ignore the new snapshots instead of misreading them.
        /// </summary>
        [Test]
        public void WriteSnapshot_KnownState_MatchesGoldenBytes()
        {
            var game = new Game { epoch = 7, phase = Phase.Lobby };
            game.characters.Add(new Character(0, "h1", "Ana", new int[7], false) { x = 100, y = 200, faceX = 1, faceY = 0 });
            var golden = new byte[]
            {
                5, 7, 0, 0, 0, 0, 0, 0, 0, 1, 0, 5, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 240, 65, 0, 0, 240, 66, 0, 0, 32, 65,
                5, 104, 111, 117, 115, 101, 255, 255, 0, 255, 0, 0, 32, 65, 0, 1, 2, 104, 49, 3, 65, 110, 97, 0, 0, 0, 0,
                0, 100, 0, 200, 0, 0, 0, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            };

            var bytes = game.WriteSnapshot();

            CollectionAssert.AreEqual(golden, bytes);
        }

        /// <summary>
        /// Golden bytes for presence (generated from the current code). If this fails, the presence layout changed:
        /// bump PresenceCodec.Version and regenerate the array.
        /// </summary>
        [Test]
        public void PresenceCodecWrite_KnownPresence_MatchesGoldenBytes()
        {
            var presence = new Presence
            {
                name = "Ana", kit = 0, joinTime = 1, ready = true, teleportSeq = 2, hasPosition = true, x = 10, y = 20, faceX = 1, faceY = 0,
                actionSeq = 3, action = PlayerAction.Laugh, actionArg = 4, voiceId = "v1"
            };
            var golden = new byte[]
            {
                2, 3, 65, 110, 97, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 2, 0, 0, 0, 1, 0, 0, 32, 65, 0, 0, 160, 65,
                0, 0, 128, 63, 0, 0, 0, 0, 0, 3, 0, 0, 0, 6, 4, 0, 0, 2, 118, 49,
            };

            var bytes = PresenceCodec.Write(presence);

            CollectionAssert.AreEqual(golden, bytes);
        }

        // ---------- PresenceCodec: per-player presence, sent far more often than a full snapshot ----------

        [Test]
        public void Wire_WriteThenTryRead_RoundTripsPresenceWithPosition()
        {
            var p = new Presence
            {
                name = "Ana", kit = Kit.Pack(new[] { 1, 1, 1, 1, 1, 1, 1 }), joinTime = 123456789L, ready = true, teleportSeq = 5,
                hasPosition = true, x = 10.5f, y = 20.5f, faceX = 0.6f, faceY = 0.8f, sneaking = true, talking = true,
                actionSeq = 9, action = PlayerAction.Pass, actionArg = 3, voiceId = "Ab12-cd_34"
            };

            bool ok = PresenceCodec.TryRead(PresenceCodec.Write(p), out var p2);

            Assert.IsTrue(ok);
            Assert.AreEqual("Ana", p2.name); Assert.AreEqual(p.kit, p2.kit); Assert.AreEqual(123456789L, p2.joinTime);
            Assert.IsTrue(p2.ready); Assert.AreEqual(5, p2.teleportSeq);
            Assert.IsTrue(p2.hasPosition); Assert.AreEqual(10.5f, p2.x); Assert.AreEqual(20.5f, p2.y); Assert.AreEqual(0.6f, p2.faceX); Assert.AreEqual(0.8f, p2.faceY);
            Assert.IsTrue(p2.sneaking); Assert.AreEqual(9, p2.actionSeq); Assert.AreEqual(PlayerAction.Pass, p2.action); Assert.AreEqual(3, p2.actionArg);
            Assert.IsTrue(p2.talking, "voice-level noise flag round-trips over the wire like sneak does");
            Assert.AreEqual("Ab12-cd_34", p2.voiceId, "voice id round-trips so others can mute this player");
        }

        [Test]
        public void Wire_WriteThenTryRead_RoundTripsPresenceWithoutPosition()
        {
            var p = new Presence { name = "Bob", kit = 0, joinTime = 0, ready = false, teleportSeq = 0, hasPosition = false, actionSeq = 0, action = PlayerAction.None, actionArg = 0 };

            PresenceCodec.TryRead(PresenceCodec.Write(p), out var p2);

            Assert.IsFalse(p2.hasPosition);
            Assert.AreEqual(0, p2.x); Assert.AreEqual(0, p2.y); // position fields are simply skipped on the wire, defaulted on read
        }

        [Test]
        public void Wire_TryRead_NoVoice_VoiceIdIsNull()
        {
            var p = new Presence { name = "A", voiceId = null };

            PresenceCodec.TryRead(PresenceCodec.Write(p), out var p2);

            Assert.IsNull(p2.voiceId);
        }

        [TestCase("id with spaces")]
        [TestCase("<script>")]
        [TestCase("ünïcode")]
        public void CleanVoiceId_NotAnId_ReturnsNull(string voiceId) => Assert.IsNull(PresenceCodec.CleanVoiceId(voiceId));

        [Test]
        public void CleanVoiceId_TooLong_ReturnsNull() => Assert.IsNull(PresenceCodec.CleanVoiceId(new string('a', 65)));

        [Test]
        public void Wire_TryRead_NaNPosition_StillSucceedsButClearsHasPos()
        {
            var p = new Presence { name = "A", hasPosition = true, x = float.NaN, y = 1, faceX = 1, faceY = 0 };

            bool ok = PresenceCodec.TryRead(PresenceCodec.Write(p), out var p2);

            Assert.IsTrue(ok);
            Assert.IsFalse(p2.hasPosition, "a NaN coordinate is sanitized by clearing hasPos, not by rejecting the whole payload");
        }

        [Test]
        public void Wire_TryRead_MalformedOrNullData_ReturnsFalse()
        {
            Assert.IsFalse(PresenceCodec.TryRead(new byte[] { 0 }, out _), "wrong version marker");
            Assert.IsFalse(PresenceCodec.TryRead(new byte[] { 1 }, out _), "previous format version (1) is rejected");
            Assert.IsFalse(PresenceCodec.TryRead(new byte[] { 2 }, out _), "truncated right after the version byte");
            Assert.IsFalse(PresenceCodec.TryRead(null, out _), "null payload");
        }
    }
}
