using System.Collections.Generic;
using NUnit.Framework;

namespace Unice
{
    /// <summary>Characterization tests for the RoomSession.HumansForNextRound() -> injected delegate seam in HostStep.</summary>
    [TestFixture]
    public class GameHostStepTests
    {
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
        public void HostStep_ResultPhaseTimeUp_StartsNextRoundUsingInjectedHumans()
        {
            var g = NewGameInResult(out var humans);
            int roundBefore = g.round;

            g.HostStep(1f, null, "h1", null, () => humans);

            Assert.AreEqual(Phase.Hide, g.phase, "StartRound() always leaves the game in Phase.Hide");
            Assert.AreEqual(roundBefore + 1, g.round);
        }

        [Test]
        public void HostStep_ResultPhaseLastRound_GoesFinal_NeverCallsHumansNow()
        {
            var g = NewGameInResult(out _);
            g.round = g.totalRounds; // already on the last round

            bool called = false;
            g.HostStep(1f, null, "h1", null, () => { called = true; return new List<Game.Human>(); });

            Assert.AreEqual(Phase.Final, g.phase);
            Assert.IsFalse(called, "the last round must not need the humans-now delegate at all");
        }

        [Test]
        public void HostStep_ResultPhaseTimeNotUp_DoesNothing()
        {
            var g = NewGameInResult(out var humans);
            g.timeLeft = 5f;

            g.HostStep(1f, null, "h1", null, () => humans);

            Assert.AreEqual(Phase.Result, g.phase);
        }
    }
}
