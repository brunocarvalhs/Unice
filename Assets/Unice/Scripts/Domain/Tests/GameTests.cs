using System.Collections.Generic;
using NUnit.Framework;

namespace Unice
{
    /// <summary>
    /// Characterization tests for Game's main flows (NewMatch, StartRound, ApplyAction, EndRound via its public
    /// triggers, MatchHost.TakeOver). Pins down CURRENT behavior of the straight-ported web rules; does not change Game.cs.
    /// MatchHost.Step's Result-phase transition is already covered by MatchHostTests.cs and is not repeated here.
    /// </summary>
    [TestFixture]
    public class GameTests
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

        // ---------- ApplyAction: Laugh (R) ----------

        /// <summary>A hunt in progress with characters[0] as the Unice and characters[1] holding the doll.</summary>
        static Game HuntWithUniceAtZero()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            foreach (var e in g.characters) e.role = Role.Hider;
            var unice = g.characters[0]; unice.role = Role.Unice; g.unice = unice;
            g.doll.holder = g.characters[1];
            g.phase = Phase.Hunt;
            return g;
        }

        [Test]
        public void ApplyAction_LaughWhileOffCooldown_RaisesGiggleAtLaugherAndSetsCooldown()
        {
            var g = HuntWithUniceAtZero();
            var laugher = g.characters[2]; laugher.x = 100.4f; laugher.y = 200.6f; laugher.laughCooldown = 0;

            g.ApplyAction(laugher, PlayerAction.Laugh, 0);

            var ev = g.events[g.events.Count - 1];
            Assert.AreEqual(GameEventType.Giggle, ev.type, "a laugh is heard like the doll's giggle");
            Assert.AreEqual(laugher.index, ev.actor);
            Assert.AreEqual(100, ev.x, "event position is the laugher's, rounded"); Assert.AreEqual(201, ev.y);
            Assert.AreEqual(Rules.LaughCooldown, laugher.laughCooldown);
            Assert.AreEqual(5f, Rules.LaughCooldown, "SPEC: 5 s between laughs");
            Assert.IsTrue(g.doll.hasLastGiggle); Assert.AreEqual(100.4f, g.doll.lastGiggleX); Assert.AreEqual(200.6f, g.doll.lastGiggleY);
        }

        [Test]
        public void ApplyAction_LaughOnCooldown_DoesNothing()
        {
            var g = HuntWithUniceAtZero();
            var laugher = g.characters[2]; laugher.laughCooldown = 0.1f;
            int seqBefore = g.eventSeq;

            g.ApplyAction(laugher, PlayerAction.Laugh, 0);

            Assert.AreEqual(seqBefore, g.eventSeq, "no event");
            Assert.AreEqual(0.1f, laugher.laughCooldown, "cooldown untouched");
        }

        [TestCase(true, TestName = "ApplyAction_LaughAsUnice_DoesNothing")]
        [TestCase(false, TestName = "ApplyAction_LaughAsCaughtHider_DoesNothing")]
        public void ApplyAction_LaughAsUniceOrCaught_DoesNothing(bool asUnice)
        {
            var g = HuntWithUniceAtZero();
            var laugher = asUnice ? g.characters[0] : g.characters[2];
            if (!asUnice) laugher.outOfRound = true;
            int seqBefore = g.eventSeq;

            g.ApplyAction(laugher, PlayerAction.Laugh, 0);

            Assert.AreEqual(seqBefore, g.eventSeq, "no event");
            Assert.AreEqual(0, laugher.laughCooldown, "no cooldown set");
        }

        [Test]
        public void ApplyAction_LaughWhileHoldingDoll_ResetsGiggleTimer()
        {
            var g = HuntWithUniceAtZero();
            var holder = g.doll.holder; g.doll.giggleTimer = 3;

            g.ApplyAction(holder, PlayerAction.Laugh, 0);

            Assert.AreEqual(g.giggleInterval, g.doll.giggleTimer);
        }

        [Test]
        public void ApplyAction_LaughWithoutDoll_KeepsGiggleTimer()
        {
            var g = HuntWithUniceAtZero();
            g.doll.giggleTimer = 3;

            g.ApplyAction(g.characters[2], PlayerAction.Laugh, 0);

            Assert.AreEqual(3, g.doll.giggleTimer);
        }

        [TestCase(Phase.Lobby)]
        [TestCase(Phase.Result)]
        [TestCase(Phase.Final)]
        public void ApplyAction_LaughOutsideRound_DoesNothing(Phase phase)
        {
            var g = HuntWithUniceAtZero();
            g.phase = phase;
            var laugher = g.characters[2];
            int seqBefore = g.eventSeq;

            g.ApplyAction(laugher, PlayerAction.Laugh, 0);

            Assert.AreEqual(seqBefore, g.eventSeq, "no event");
            Assert.AreEqual(0, laugher.laughCooldown, "no cooldown set");
        }

        // ---------- draws: who can be the Unice / get the doll ----------

        const int DrawRuns = 200;

        [Test]
        public void NewMatch_TwoOrMoreHumans_NeverDrawsBotAsUniceOrDollHolder()
        {
            // EligibleForDraw (Game.cs) is used for both the first Unice and the doll holder, so both hold.
            for (int run = 0; run < DrawRuns; run++)
            {
                var g = NewGameAfterMatch(3, Humans(("h1", "Ana"), ("h2", "Beto")));

                Assert.IsFalse(g.unice.isBot, $"run {run}: Unice was a bot");
                Assert.IsFalse(g.doll.holder.isBot, $"run {run}: doll holder was a bot");
            }
        }

        [Test]
        public void EndRound_TimeUpWithTwoOrMoreHumans_NextUniceIsNeverABot()
        {
            for (int run = 0; run < DrawRuns; run++)
            {
                var g = NewGameAfterMatch(3, Humans(("h1", "Ana"), ("h2", "Beto"), ("h3", "Caio")));
                // bots have the fewest turns, so only the "people only" rule keeps them out
                foreach (var e in g.characters) e.uniceTurns = e.isBot ? 0 : 5;
                g.phase = Phase.Hunt; g.timeLeft = 0.01f;

                g.Simulate(0.02f);

                Assert.AreEqual(Phase.Result, g.phase, $"run {run}: round should have ended");
                Assert.IsFalse(g.nextUnice.isBot, $"run {run}: next Unice was a bot");
            }
        }

        [Test]
        public void NewMatch_SingleHuman_CanDrawBotAsUnice()
        {
            bool botDrawn = false;
            for (int run = 0; run < DrawRuns && !botDrawn; run++)
                botDrawn = NewGameAfterMatch(3, Humans(("h1", "Ana"))).unice.isBot;

            Assert.IsTrue(botDrawn, "playing alone, everyone (bots included) can be the Unice");
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

        /// <summary>
        /// CHARACTERIZATION PENDING OWNER DECISION: today every non-Unice gets +2 on time-up, including hiders the Unice
        /// already caught (Game.cs EndRound: `if (!character.IsUnice) character.score += 2`, no outOfRound check).
        /// SPEC says "cada escondido +2" — confirm with the web version whether caught hiders should score.
        /// </summary>
        [Test]
        public void EndRound_TimeUp_CaughtHiderAlsoScoresTwo_CurrentBehavior()
        {
            var g = HuntWithUniceAtZero();
            var caught = g.characters[2]; caught.outOfRound = true; caught.score = 0;
            g.timeLeft = 0.01f;

            g.Simulate(0.02f);

            Assert.AreEqual(Phase.Result, g.phase, "round ended on time-up");
            Assert.AreEqual(2, caught.score, "caught hider still gets the team's +2 (current behavior)");
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

        // ---------- T-03: doll pass (cooldown, range, invalid targets) ----------

        /// <summary>
        /// A hunt with no AI running (everyone flagged as a person, so Simulate only ticks timers and touch catches):
        /// characters[0] is the Unice parked far away, characters[1] holds the doll at (100,100), characters[2] is another hider, far.
        /// </summary>
        static Game QuietHunt()
        {
            var g = HuntWithUniceAtZero();
            foreach (var e in g.characters) { e.isBot = false; e.hidingSpot = null; e.outOfRound = false; e.passCooldown = 0; e.stunTime = 0; }
            g.characters[0].x = 1000; g.characters[0].y = 1000;
            g.characters[1].x = 100; g.characters[1].y = 100;
            g.characters[2].x = 1000; g.characters[2].y = 100;
            g.timeLeft = 60;
            return g;
        }

        [Test]
        public void ApplyAction_PassRightAfterReceiving_BlockedUntilFiveSecondsPass()
        {
            var g = QuietHunt();
            var first = g.characters[1]; var second = g.characters[2];
            second.x = 130; second.y = 100;
            g.ApplyAction(first, PlayerAction.Pass, 0);
            Assert.AreSame(second, g.doll.holder, "precondition: first pass went through");

            g.ApplyAction(second, PlayerAction.Pass, 0);
            Assert.AreSame(second, g.doll.holder, "0 s after receiving: still on cooldown");
            g.Simulate(4.5f);
            g.ApplyAction(second, PlayerAction.Pass, 0);
            Assert.AreSame(second, g.doll.holder, "4.5 s after receiving: still on cooldown");
            g.Simulate(0.5f);
            g.ApplyAction(second, PlayerAction.Pass, 0);

            Assert.AreEqual(5f, Rules.PassCooldown, "SPEC: 5 s between passes");
            Assert.AreSame(first, g.doll.holder, "5 s after receiving: can pass again");
        }

        [TestCase(59f, true)]
        [TestCase(61f, false)]
        public void PassTarget_HiderAtDistance_IsTargetOnlyWithinSixtyPixels(float distance, bool expectedTarget)
        {
            var g = QuietHunt();
            var giver = g.characters[1]; var other = g.characters[2];
            other.x = giver.x + distance; other.y = giver.y;

            var target = g.PassTarget(giver);

            Assert.AreEqual(60f, Rules.PassRange, "SPEC: pass up to 60 px");
            Assert.AreEqual(expectedTarget ? other : null, target);
        }

        /// <summary>
        /// CHARACTERIZATION: ApplyAction adds PassLatencySlack (20 px) to every pass request, so the real reach is &lt; 80 px,
        /// not the SPEC's 60 px. Pinned as-is; confirm with the owner whether local passes should get the slack too.
        /// </summary>
        [TestCase(79f, true)]
        [TestCase(81f, false)]
        public void ApplyAction_PassToHiderAtDistance_ReachIsSixtyPlusTwentySlack(float distance, bool expectedPassed)
        {
            var g = QuietHunt();
            var giver = g.characters[1]; var other = g.characters[2];
            other.x = giver.x + distance; other.y = giver.y;

            g.ApplyAction(giver, PlayerAction.Pass, 0);

            Assert.AreSame(expectedPassed ? other : giver, g.doll.holder);
        }

        [TestCase("unice")]
        [TestCase("caught")]
        [TestCase("hidden")]
        [TestCase("self")]
        public void ApplyAction_PassWithOnlyAnInvalidTargetNearby_KeepsTheDoll(string nearby)
        {
            var g = QuietHunt();
            var holder = g.characters[1];
            var near = nearby == "unice" ? g.characters[0] : g.characters[2];
            if (nearby != "self") { near.x = holder.x + 20; near.y = holder.y; }
            if (nearby == "caught") near.outOfRound = true;
            if (nearby == "hidden") near.hidingSpot = Map.Spots[0];
            int seqBefore = g.eventSeq;

            g.ApplyAction(holder, PlayerAction.Pass, 0);

            Assert.AreSame(holder, g.doll.holder, "doll stays");
            Assert.AreEqual(seqBefore, g.eventSeq, "no pass event");
        }

        // ---------- T-03: stun after catching someone without the doll ----------

        /// <summary>QuietHunt with the Unice at (500,500), 20 px from characters[2] (not the doll holder).</summary>
        static Game HuntWithUniceNextToNonHolder()
        {
            var g = QuietHunt();
            g.characters[0].x = 500; g.characters[0].y = 500;
            g.characters[2].x = 520; g.characters[2].y = 500;
            return g;
        }

        [Test]
        public void ApplyAction_CatchNonHolder_StunsUniceForTwoAndAHalfSeconds()
        {
            var g = HuntWithUniceNextToNonHolder();
            var unice = g.characters[0];

            g.ApplyAction(unice, PlayerAction.Catch, 2);

            Assert.AreEqual(2.5f, Rules.StunTime, "SPEC: 2.5 s stopped");
            Assert.AreEqual(Rules.StunTime, unice.stunTime, "stun set");
            Assert.IsFalse(g.CanMove(unice), "can't move while stunned");
        }

        [Test]
        public void ApplyAction_CatchWhileStunned_DoesNothing()
        {
            var g = HuntWithUniceNextToNonHolder();
            var unice = g.characters[0]; var holder = g.characters[1];
            g.ApplyAction(unice, PlayerAction.Catch, 2);
            holder.x = unice.x + 50; holder.y = unice.y;   // within the Catch request reach (30+45) but outside touch range (30)
            g.Simulate(2.4f);

            g.ApplyAction(unice, PlayerAction.Catch, holder.index);

            Assert.AreEqual(Phase.Hunt, g.phase, "holder not caught while stunned");
            Assert.IsFalse(holder.outOfRound);
        }

        [Test]
        public void ApplyAction_OpenWhileStunned_DoesNothing()
        {
            var g = HuntWithUniceNextToNonHolder();
            var unice = g.characters[0]; var holder = g.characters[1];
            g.ApplyAction(unice, PlayerAction.Catch, 2);
            var spot = Map.Spots[0]; g.Hide(holder, spot);
            unice.x = spot.standX; unice.y = spot.standY;
            int seqBefore = g.eventSeq;

            g.ApplyAction(unice, PlayerAction.Open, 0);

            Assert.AreEqual(seqBefore, g.eventSeq, "no open event");
            Assert.AreSame(holder, spot.occupant, "holder still hidden");
            Assert.AreEqual(Phase.Hunt, g.phase);
        }

        [Test]
        public void ApplyAction_CatchAfterStunEnds_CatchesTheHolder()
        {
            var g = HuntWithUniceNextToNonHolder();
            var unice = g.characters[0]; var holder = g.characters[1];
            g.ApplyAction(unice, PlayerAction.Catch, 2);
            holder.x = unice.x + 50; holder.y = unice.y;
            g.Simulate(2.6f);
            Assert.LessOrEqual(unice.stunTime, 0f, "precondition: stun is over");

            g.ApplyAction(unice, PlayerAction.Catch, holder.index);

            Assert.AreEqual(Phase.Result, g.phase, "catching the holder after the stun ends the round");
        }

        [Test]
        public void ApplyAction_OpenAfterStunEnds_OpensTheSpot()
        {
            var g = HuntWithUniceNextToNonHolder();
            var unice = g.characters[0]; var holder = g.characters[1];
            g.ApplyAction(unice, PlayerAction.Catch, 2);
            var spot = Map.Spots[0]; g.Hide(holder, spot);
            unice.x = spot.standX; unice.y = spot.standY;
            g.Simulate(2.6f);

            g.ApplyAction(unice, PlayerAction.Open, 0);

            Assert.AreEqual(Phase.Result, g.phase, "opening the holder's spot after the stun ends the round");
            Assert.IsNull(spot.occupant);
        }

        // ---------- T-03: touch catch inside Simulate ----------

        [TestCase(29f, true)]
        [TestCase(31f, false)]
        public void Simulate_UniceNearAHiderOutsideASpot_CatchesOnlyWithinThirtyPixels(float distance, bool expectedCaught)
        {
            var g = QuietHunt();
            var unice = g.characters[0]; var hider = g.characters[2];
            unice.x = 500; unice.y = 500; hider.x = 500 + distance; hider.y = 500;

            g.Simulate(0.01f);

            Assert.AreEqual(30f, Rules.CatchRange, "SPEC: touch catch at 30 px");
            Assert.AreEqual(expectedCaught, hider.outOfRound);
        }

        /// <summary>
        /// CHARACTERIZATION: Simulate checks `distance &lt; Rules.CatchRange` (strict), so exactly 30 px is NOT a catch.
        /// SPEC says "encosta (30 px)" and T-03 asked for "&lt;= 30 px". Pinned as-is pending owner decision.
        /// </summary>
        [Test]
        public void Simulate_UniceExactlyThirtyPixelsFromAHider_DoesNotCatch()
        {
            var g = QuietHunt();
            var unice = g.characters[0]; var hider = g.characters[2];
            unice.x = 500; unice.y = 500; hider.x = 530; hider.y = 500;

            g.Simulate(0.01f);

            Assert.IsFalse(hider.outOfRound);
        }

        [Test]
        public void Simulate_UniceTouchesAHiderInsideASpot_DoesNotCatch()
        {
            var g = QuietHunt();
            var unice = g.characters[0]; var hider = g.characters[2];
            var spot = Map.Spots[0]; g.Hide(hider, spot);
            unice.x = hider.x + 10; unice.y = hider.y;

            g.Simulate(0.01f);

            Assert.IsFalse(hider.outOfRound, "not caught");
            Assert.AreSame(hider, spot.occupant, "still hidden");
        }

        // ---------- MatchHost.TakeOver ----------

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

            g.host.TakeOver(peers, me, "Ana");

            Assert.IsTrue(g.isHost);
            Assert.AreEqual(1, g.epoch);
            Assert.AreEqual(5, g.host.lastActionSeq["h1"]);
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
