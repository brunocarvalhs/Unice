using System.Collections.Generic;
using NUnit.Framework;

namespace Unice
{
    /// <summary>Characterization tests for the RoomSession.HumansForNextRound() -> injected delegate seam in MatchHost.Step.</summary>
    [TestFixture]
    public class MatchHostTests
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

        static Game NewGameInResult(out List<Game.Human> humans)
        {
            var g = new Game { isHost = true, botCount = 1, totalRounds = 2 };
            humans = new List<Game.Human> { new Game.Human { id = "h1", name = "Ana", kit = null, joinTime = 0 } };
            g.NewMatch(humans);
            g.phase = Phase.Result;
            g.timeLeft = 0.01f;
            return g;
        }

        [Test]
        public void Step_ResultPhaseTimeUp_StartsNextRoundUsingInjectedHumans()
        {
            var g = NewGameInResult(out var humans);
            int roundBefore = g.round;

            g.host.Step(1f, null, "h1", null, () => humans);

            Assert.AreEqual(Phase.Hide, g.phase, "StartRound() always leaves the game in Phase.Hide");
            Assert.AreEqual(roundBefore + 1, g.round);
        }

        [Test]
        public void Step_ResultPhaseLastRound_GoesFinal_NeverCallsHumansNow()
        {
            var g = NewGameInResult(out _);
            g.round = g.totalRounds; // already on the last round

            bool called = false;
            g.host.Step(1f, null, "h1", null, () => { called = true; return new List<Game.Human>(); });

            Assert.AreEqual(Phase.Final, g.phase);
            Assert.IsFalse(called, "the last round must not need the humans-now delegate at all");
        }

        [Test]
        public void Step_ResultPhaseTimeNotUp_DoesNothing()
        {
            var g = NewGameInResult(out var humans);
            g.timeLeft = 5f;

            g.host.Step(1f, null, "h1", null, () => humans);

            Assert.AreEqual(Phase.Result, g.phase);
        }

        static Game NewLiveGameWithGuest(out Character guest)
        {
            var g = new Game { isHost = true, botCount = 0, totalRounds = 2 };
            g.NewMatch(new List<Game.Human>
            {
                new Game.Human { id = "h1", name = "Ana", kit = null, joinTime = 0 },
                new Game.Human { id = "h2", name = "Beto", kit = null, joinTime = 1 },
            });
            g.phase = Phase.Hunt;
            guest = g.characters.Find(character => character.playerId == "h2");
            guest.role = Role.Hider; guest.hidingSpot = null; guest.outOfRound = false;   // the draw might have made them the Unice
            return g;
        }

        [Test]
        public void ApplyPeerInputs_PeerLeftWhileTalking_BotIsSilent()
        {
            var g = NewLiveGameWithGuest(out var guest);
            guest.talking = true; guest.sneaking = true; guest.speed = 100;

            g.host.ApplyPeerInputs(new List<PeerInfo>(), "h1", 0.05f);

            Assert.IsTrue(guest.isBot, "the guest left, so a bot plays for them");
            Assert.IsFalse(guest.talking, "a bot never talks");
            Assert.IsFalse(guest.sneaking, "the human's sneak toggle is not the bot's");
            Assert.AreEqual(0, guest.speed, "the speed estimated from the human's updates no longer applies");
        }

        [Test]
        public void ApplyPeerInputs_MoveAndHideSamePacket_Hides()
        {
            var g = NewLiveGameWithGuest(out var guest);
            var spot = Map.Spots[0];
            // a standable point out of hiding reach but within one plausible update of the spot
            var start = Navigation.Nodes.Find(node =>
            {
                float distance = Geometry.Distance(node.x, node.y, spot.standX, spot.standY);
                return Map.DistanceToFurniture(spot, node.x, node.y) > 80 && distance < 200;
            });
            guest.x = start.x; guest.y = start.y;
            var presence = new Presence
            {
                hasPosition = true, teleportSeq = guest.teleportSeq, x = spot.standX, y = spot.standY, faceX = 0, faceY = 1,
                actionSeq = 1, action = PlayerAction.Hide, actionArg = 0,
            };

            try
            {
                g.host.ApplyPeerInputs(new List<PeerInfo> { new PeerInfo { id = "h2", presence = presence } }, "h1", 0.05f);

                Assert.AreSame(spot, guest.hidingSpot);
            }
            finally { spot.occupant = null; }
        }
    }
}
