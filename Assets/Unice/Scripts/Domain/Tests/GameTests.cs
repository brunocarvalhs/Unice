using System.Collections.Generic;
using NUnit.Framework;

namespace Unice
{
    /// <summary>
    /// Characterization tests for Game's main flows (NewMatch, StartRound, ApplyAction, EndRound via its public
    /// triggers, TakeOverAsHost). Pins down CURRENT behavior of the straight-ported web rules; does not change Game.cs.
    /// HostStep's Result-phase transition is already covered by GameHostStepTests.cs and is not repeated here.
    /// </summary>
    [TestFixture]
    public class GameTests
    {
        static List<Game.Human> Humans(params (string id, string name)[] hs)
        {
            var list = new List<Game.Human>();
            foreach (var (id, name) in hs) list.Add(new Game.Human { id = id, name = name, kit = null, joinTime = 0 });
            return list;
        }

        /// <summary>A fresh match: NewMatch already runs StartRound once (r=1, Phase.Hide).</summary>
        static Game NewGameAfterMatch(int bots, List<Game.Human> humans)
        {
            var g = new Game { isHost = true, botCount = bots, totalRounds = 5 };
            g.NewMatch(humans);
            return g;
        }

        // ---------- NewMatch ----------

        [Test]
        public void NewMatch_HumansPlusBots_FillsEntsAndEntersRoundOne()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana"), ("h2", "Beto")));

            Assert.AreEqual(4, g.characters.Count, "2 humans + bots configured (2) = 4");
            Assert.AreEqual(1, g.round);
            Assert.AreEqual(Phase.Hide, g.phase);
            Assert.IsNotNull(g.unice);
            Assert.IsNotNull(g.doll.holder);
            Assert.AreNotSame(g.unice, g.doll.holder);
        }

        [Test]
        public void NewMatch_SoloWithZeroBotsConfigured_StillAddsOneBotToReachTheMinimumOfTwo()
        {
            var g = NewGameAfterMatch(0, Humans(("h1", "Ana")));

            Assert.AreEqual(2, g.characters.Count, "the loop keeps adding bots while ents.Count < 2, even if bots == 0");
        }

        // ---------- StartRound ----------

        [Test]
        public void StartRound_CalledAgain_IncrementsRoundAndReassignsUnice()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            var humansNow = Humans(("h1", "Ana"));

            g.StartRound(humansNow);

            Assert.AreEqual(2, g.round);
            Assert.AreEqual(Phase.Hide, g.phase);
            Assert.AreEqual(Rules.HideTime, g.timeLeft);
            Assert.IsNull(g.roundResult);
            Assert.AreEqual(Role.Unice, g.unice.role);
            Assert.AreEqual(1, g.characters.FindAll(e => e.role == Role.Unice).Count, "exactly one Unice per round");
        }

        [Test]
        public void StartRound_NonUniceHiders_AreLinedUpOnTheSpawnRow()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));

            var hiders = g.characters.FindAll(e => e != g.unice);
            for (int i = 0; i < hiders.Count; i++)
            {
                Assert.AreEqual(620 + i * 70, hiders[i].x);
                Assert.AreEqual(474, hiders[i].y);
            }
        }

        // ---------- ApplyAction: hiders (Hide / Exit / Pass) ----------

        [Test]
        public void DoAction_HideNearAFreeSpot_OccupiesItAndSnapsToItsAnchor()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            var hider = g.characters.Find(e => e != g.unice);
            var spot = Map.Spots[0]; // "bed", anchor at (245,237)
            hider.x = spot.standX; hider.y = spot.standY;

            g.ApplyAction(hider, PlayerAction.Hide, 0);

            Assert.AreSame(spot, hider.hidingSpot);
            Assert.AreSame(hider, spot.occupant);
            Assert.AreEqual(spot.standX, hider.x); Assert.AreEqual(spot.standY, hider.y);
            Assert.IsFalse(hider.moving);
        }

        [Test]
        public void DoAction_HideTooFarFromTheSpot_DoesNothing()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            var hider = g.characters.Find(e => e != g.unice); // still at its spawn-row position, far from any furniture

            g.ApplyAction(hider, PlayerAction.Hide, 0);

            Assert.IsNull(hider.hidingSpot);
            Assert.IsNull(Map.Spots[0].occupant);
        }

        [Test]
        public void DoAction_Exit_ClearsBothTheHiderAndTheSpot()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            var hider = g.characters.Find(e => e != g.unice);
            var spot = Map.Spots[0];
            hider.x = spot.standX; hider.y = spot.standY;
            g.ApplyAction(hider, PlayerAction.Hide, 0);

            g.ApplyAction(hider, PlayerAction.Exit, 0);

            Assert.IsNull(hider.hidingSpot);
            Assert.IsNull(spot.occupant);
        }

        [Test]
        public void DoAction_PassWhileHoldingTheDollWithATargetNearby_TransfersTheDoll()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana"))); // 3 characters: 1 human + 2 bots
            foreach (var e in g.characters) e.role = Role.Hider;
            var unice = g.characters[0]; unice.role = Role.Unice; g.unice = unice;
            var holder = g.characters[1]; var other = g.characters[2];
            g.doll.holder = holder; holder.hidingSpot = null; holder.passCooldown = 0;
            other.hidingSpot = null; other.outOfRound = false;
            holder.x = 100; holder.y = 100; other.x = 130; other.y = 100; // 30 units apart, well within Rules.PassRange(60)+20

            g.ApplyAction(holder, PlayerAction.Pass, 0);

            Assert.AreSame(other, g.doll.holder);
            Assert.AreEqual(Rules.PassCooldown, other.passCooldown);
            Assert.AreEqual(0, holder.passCooldown);
        }

        // ---------- ApplyAction: Unice (Open / Catch) ----------

        [Test]
        public void DoAction_UniceOpensAnEmptySpot_RaisesOpenEventWithoutCapturing()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            foreach (var e in g.characters) e.role = Role.Hider;
            var unice = g.characters[0]; unice.role = Role.Unice; g.unice = unice; unice.stunTime = 0;
            g.phase = Phase.Hunt;
            var spot = Map.Spots[0]; unice.x = spot.standX; unice.y = spot.standY;

            g.ApplyAction(unice, PlayerAction.Open, 0);

            var ev = g.events[g.events.Count - 1];
            Assert.AreEqual(GameEventType.Open, ev.type);
            Assert.AreEqual(-1, ev.target, "no one was in the spot");
            Assert.AreEqual(Phase.Hunt, g.phase, "an empty spot never ends the round");
            Assert.AreEqual(0, unice.score);
        }

        [Test]
        public void DoAction_UniceOpensASpotHoldingTheDoll_CapturesAndEndsRoundAsAUniceWin()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            foreach (var e in g.characters) e.role = Role.Hider;
            var unice = g.characters[0]; unice.role = Role.Unice; g.unice = unice; unice.stunTime = 0;
            g.phase = Phase.Hunt;
            var spot = Map.Spots[0]; unice.x = spot.standX; unice.y = spot.standY;
            var holder = g.characters[1]; holder.hidingSpot = spot; spot.occupant = holder; g.doll.holder = holder;

            g.ApplyAction(unice, PlayerAction.Open, 0);

            Assert.AreEqual(Phase.Result, g.phase);
            Assert.AreEqual(3, unice.score, "Unice gets +3 for getting the doll back");
            Assert.AreEqual(Rules.ResultTime, g.timeLeft);
            Assert.AreSame(holder, g.nextUnice, "whoever was caught with the doll becomes the next Unice");
            Assert.IsTrue(g.roundResult.uniceWon); Assert.AreEqual(holder.index, g.roundResult.dollHolder); Assert.AreEqual(-1, g.roundResult.holderSpotIndex); Assert.AreEqual(holder.index, g.roundResult.nextUnice);
            Assert.IsNull(holder.hidingSpot); Assert.IsNull(spot.occupant);
        }

        [Test]
        public void DoAction_UniceCatchesAHiderWhoIsNotTheDollHolder_RemovesThemWithoutEndingTheRound()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            foreach (var e in g.characters) e.role = Role.Hider;
            var unice = g.characters[0]; unice.role = Role.Unice; g.unice = unice; unice.stunTime = 0;
            g.phase = Phase.Hunt;
            var target = g.characters[1]; var holder = g.characters[2];
            g.doll.holder = holder; target.hidingSpot = null; target.outOfRound = false;
            unice.x = 100; unice.y = 100; target.x = 120; target.y = 100; // 20 units, well within Rules.CatchRange(30)+45

            g.ApplyAction(unice, PlayerAction.Catch, target.index);

            Assert.IsTrue(target.outOfRound);
            Assert.AreEqual(1, unice.score);
            Assert.AreEqual(Rules.StunTime, unice.stunTime);
            Assert.AreEqual(Phase.Hunt, g.phase, "catching a non-holder never ends the round");
            var ev = g.events[g.events.Count - 1];
            Assert.AreEqual(GameEventType.Catch, ev.type); Assert.AreEqual(0, ev.value, "value=0 marks a non-holder capture");
        }

        // ---------- EndRound, reached via Simulate's clock running out (team win) ----------

        [Test]
        public void SimUpdate_ClockRunsOutDuringHunt_EndsRoundAsATeamWinAndScoresEveryone()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            foreach (var e in g.characters) e.role = Role.Hider;
            var unice = g.characters[0]; unice.role = Role.Unice; g.unice = unice;
            var holder = g.characters[1]; var other = g.characters[2];
            holder.uniceTurns = 0; other.uniceTurns = 0; holder.hidingSpot = null;
            g.doll.holder = holder;
            g.phase = Phase.Hunt; g.timeLeft = 0.01f;

            g.Simulate(0.02f);

            Assert.AreEqual(Phase.Result, g.phase);
            Assert.AreEqual(Rules.ResultTime, g.timeLeft);
            Assert.AreEqual(3, holder.score, "holder gets the team's +2 plus its own +1");
            Assert.AreEqual(2, other.score, "every other hider gets +2 when the team wins");
            Assert.IsFalse(g.roundResult.uniceWon);
            Assert.AreEqual(holder.index, g.roundResult.dollHolder);
            Assert.AreEqual(-1, g.roundResult.holderSpotIndex, "holder wasn't hidden when the clock ran out");
            Assert.Contains(g.nextUnice, new[] { holder, other }, "next Unice is drawn from the tied-lowest-turns pool");
            Assert.AreEqual(g.nextUnice.index, g.roundResult.nextUnice);
        }

        // ---------- Simulate: Hide -> Hunt transition ----------

        [Test]
        public void SimUpdate_HideTimerExpires_MovesToHuntAndSendsUniceThroughTheFrontDoor()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            foreach (var e in g.characters) e.role = Role.Hider;
            var unice = g.characters[0]; unice.role = Role.Unice; g.unice = unice; // human Unice: no bot AI runs this tick to move it again
            g.phase = Phase.Hide; g.timeLeft = 0.01f;
            int tpBefore = unice.teleportSeq;

            g.Simulate(0.02f);

            Assert.AreEqual(Phase.Hunt, g.phase);
            Assert.AreEqual(Rules.HuntTime, g.timeLeft);
            Assert.AreEqual(Map.Entrance.x, unice.x); Assert.AreEqual(Map.Entrance.y, unice.y);
            Assert.AreEqual(0, unice.faceX); Assert.AreEqual(-1, unice.faceY);
            Assert.AreEqual(tpBefore + 1, unice.teleportSeq);
        }

        // ---------- TakeOverAsHost ----------

        [Test]
        public void TakeOver_PromotesToHostAndReconcilesPeerState()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana"), ("h2", "Beto"))); // characters: h1, h2, 1 bot
            g.isHost = false; g.epoch = 0;
            var me = g.characters.Find(e => e.playerId == "h1");
            var h2 = g.characters.Find(e => e.playerId == "h2");
            var bot = g.characters.Find(e => e.isBot);
            foreach (var e in g.characters) { e.targetX = e.x + 1; e.targetY = e.y + 1; } // distinct from current x/y, so the reset is observable
            var peers = new List<PeerInfo> { new PeerInfo { id = "h1", presence = new Presence { actionSeq = 5 } } }; // h2 is gone

            g.TakeOverAsHost(peers, me, "Ana");

            Assert.IsTrue(g.isHost);
            Assert.AreEqual(1, g.epoch);
            Assert.AreEqual(5, g.lastActionSeq["h1"]);
            Assert.IsTrue(h2.isBot, "a human peer missing from the room becomes a bot");
            Assert.IsTrue(bot.isBot, "an existing bot stays a bot");
            Assert.AreEqual(h2.targetX, h2.x); Assert.AreEqual(h2.targetY, h2.y, "non-local ents snap to their last known target");
            Assert.AreEqual(bot.targetX, bot.x); Assert.AreEqual(bot.targetY, bot.y);
            foreach (var s in Map.Spots) Assert.IsNull(s.reservedBy);
            foreach (var e in g.characters) { Assert.AreEqual(HiderMode.Seek, e.brain.mode); Assert.IsNull(e.brain.goal); Assert.AreEqual(0, e.brain.path.Count); }
            var ev = g.events[g.events.Count - 1];
            Assert.AreEqual(GameEventType.Host, ev.type); Assert.AreEqual("Ana", ev.text);
        }
    }
}
