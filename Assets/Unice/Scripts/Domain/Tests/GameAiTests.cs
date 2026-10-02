using System.Collections.Generic;
using NUnit.Framework;

namespace Unice
{
    /// <summary>
    /// Characterization tests for Game's bot-AI and host-input paths (UniceBot/HiderBot/Patrol/UniceBot.OnArrived/UniceBot.FinishSpotCheck/
    /// UniceBot.SetGoal/UncheckedSpotNear/FleePoint/BotPath.CanSee/UniceBot.HearNoise, plus the public CanMove/BackToLobby/MatchHost.ApplyPeerInputs/NearestSpot).
    /// These are RNG-driven heuristics: tests assert INVARIANTS that must hold regardless of the random draws
    /// (e.g. "stays inside the house"), or force edge conditions that make the outcome deterministic (e.g. an
    /// empty path so FollowPath "arrives" on the very next tick, or a goal placed exactly on a free spot's
    /// anchor so NearFreeSpot/UncheckedSpotNear have exactly one possible answer). Does not change Game.cs.
    /// </summary>
    [TestFixture]
    public class GameAiTests
    {
        static List<Game.Human> Humans(params (string id, string name)[] hs)
        {
            var list = new List<Game.Human>();
            foreach (var (id, name) in hs) list.Add(new Game.Human { id = id, name = name, kit = null, joinTime = 0 });
            return list;
        }

        static Game NewGameAfterMatch(int bots, List<Game.Human> humans)
        {
            var g = new Game { isHost = true, botCount = bots, totalRounds = 5 };
            g.NewMatch(humans);
            return g;
        }

        // ---------- pure public helpers ----------

        [Test]
        public void CanMoveNow_CoversLiveOutOfRoundHiddenAndUniceGuards()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var e = new Character(0, "x", "X", new int[7], false);

            g.phase = Phase.Hunt;
            Assert.IsTrue(g.CanMove(e), "a plain hider can move during Hunt");

            e.outOfRound = true; Assert.IsFalse(g.CanMove(e)); e.outOfRound = false;
            e.hidingSpot = Map.Spots[0]; Assert.IsFalse(g.CanMove(e)); e.hidingSpot = null;

            e.role = Role.Unice; e.stunTime = 0;
            Assert.IsTrue(g.CanMove(e), "Unice can move during Hunt when not stunned");
            g.phase = Phase.Hide; Assert.IsFalse(g.CanMove(e), "Unice never moves during Hide");
            g.phase = Phase.Hunt; e.stunTime = 1; Assert.IsFalse(g.CanMove(e), "a stunned Unice can't move even during Hunt");

            g.phase = Phase.Result; e.role = Role.Hider; e.stunTime = 0;
            Assert.IsFalse(g.CanMove(e), "nobody moves outside Hide/Hunt");
        }

        [Test]
        public void BackToLobby_ResetsMatchStateAndRaisesLobbyEvent()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));

            g.BackToLobby();

            Assert.AreEqual(Phase.Lobby, g.phase);
            Assert.AreEqual(0, g.characters.Count); Assert.AreEqual(0, g.round);
            Assert.IsNull(g.unice); Assert.IsNull(g.doll.holder);
            Assert.AreEqual(GameEventType.Lobby, g.events[g.events.Count - 1].type);
        }

        [Test]
        public void NearSpotFurn_ReturnsNearestSpotWithin34Units_OrNullWhenFar()
        {
            var atTheBed = new Character(0, "x", "X", new int[7], false) { x = Map.Spots[0].standX, y = Map.Spots[0].standY };
            Assert.AreSame(Map.Spots[0], Game.NearestSpot(atTheBed));

            var inTheOpenHall = new Character(0, "x", "X", new int[7], false) { x = 900, y = 474 };
            Assert.IsNull(Game.NearestSpot(inTheOpenHall));
        }

        // ---------- MatchHost.ApplyPeerInputs (host applying what peers sent) ----------

        [Test]
        public void ReadInputs_PeerMissing_TurnsTheEntIntoABotAndRaisesLeftWhileLive()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana"), ("h2", "Beto")));
            g.phase = Phase.Hunt;
            var h2 = g.characters.Find(e => e.playerId == "h2");

            g.host.ApplyPeerInputs(new List<PeerInfo>(), "h1", 0.05f);

            Assert.IsTrue(h2.isBot, "a human peer that's gone from the room becomes a bot so the match keeps going");
            var ev = g.events[g.events.Count - 1];
            Assert.AreEqual(GameEventType.Left, ev.type); Assert.AreEqual(h2.index, ev.actor);
        }

        [Test]
        public void ReadInputs_NewerActionSequence_AppliesTheActionAndRemembersTheSequence()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana"), ("h2", "Beto")));
            var h2 = g.characters.Find(e => e.playerId == "h2");
            h2.role = Role.Hider; h2.hidingSpot = null; h2.outOfRound = false; // might have been randomly drawn as this round's Unice otherwise
            var spot = Map.Spots[0]; h2.x = spot.standX; h2.y = spot.standY;
            var peer = new PeerInfo { id = "h2", presence = new Presence { actionSeq = 1, action = PlayerAction.Hide, actionArg = 0, hasPosition = false } };

            try
            {
                g.host.ApplyPeerInputs(new List<PeerInfo> { peer }, "h1", 0.05f);

                Assert.AreSame(spot, h2.hidingSpot);
                Assert.AreEqual(1, g.host.lastActionSeq["h2"]);
            }
            finally { spot.occupant = null; }
        }

        [Test]
        public void ReadInputs_PositionSync_TeleportsWithinThreshold_AndRejectsAnImpossibleJump()
        {
            var g = NewGameAfterMatch(0, Humans(("h1", "Ana"), ("h2", "Beto"), ("h3", "Caio")));
            g.phase = Phase.Hunt;
            var h2 = g.characters.Find(e => e.playerId == "h2"); h2.teleportSeq = 5; h2.x = 100; h2.y = 100;
            var h3 = g.characters.Find(e => e.playerId == "h3"); h3.teleportSeq = 5; h3.x = 100; h3.y = 100;
            var peers = new List<PeerInfo>
            {
                new PeerInfo { id = "h2", presence = new Presence { actionSeq = 0, hasPosition = true, teleportSeq = 5, x = 150, y = 100, faceX = 1, faceY = 0, sneaking = true } },
                new PeerInfo { id = "h3", presence = new Presence { actionSeq = 0, hasPosition = true, teleportSeq = 5, x = 2000, y = 2000, faceX = 1, faceY = 0 } }, // way beyond the 260 jump threshold
            };

            g.host.ApplyPeerInputs(peers, "h1", 0.1f);

            Assert.AreEqual(150, h2.x); Assert.AreEqual(100, h2.y); Assert.AreEqual(1, h2.faceX); Assert.AreEqual(0, h2.faceY); Assert.IsTrue(h2.sneaking);
            Assert.AreEqual(100, h3.x); Assert.AreEqual(100, h3.y, "an impossible jump is rejected, not applied");
            Assert.AreEqual(6, h3.teleportSeq, "a rejected jump still bumps tp to force the client to resync");
        }

        // ---------- UniceBot: arrival -> checking -> resolving a spot ----------

        [Test]
        public void UniceAI_ArrivingAtASpotGoal_StartsCheckingThenResolvesAndOpensTheEmptySpot()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var other = g.characters.Find(e => !e.isBot); other.outOfRound = true; // keep it out of Unice's target search
            var unice = g.characters.Find(e => e.isBot); unice.role = Role.Unice; g.unice = unice;
            unice.stunTime = 0; unice.tiredTime = 0; unice.brain.checkTimer = 0; unice.brain.chaseTarget = null;
            var spot = Map.Spots[0];
            unice.x = spot.standX; unice.y = spot.standY; unice.brain.goal = new BotGoal { kind = BotGoal.Kind.Spot, spot = spot, priority = 0, hopsLeft = 0 }; unice.brain.path.Clear();
            g.phase = Phase.Hunt; g.timeLeft = 1000;

            g.Simulate(0.05f); // FollowPath "arrives" immediately (empty path) -> UniceBot.OnArrived starts the check
            Assert.AreEqual(Rules.BotSpotCheckTime, unice.brain.checkTimer);
            Assert.AreSame(spot, unice.brain.checkingSpot);

            g.Simulate(1.1f); // brain.checkTimer runs out -> UniceBot.FinishSpotCheck
            Assert.IsNull(unice.brain.goal); Assert.IsNull(unice.brain.checkingSpot);
            var ev = g.events[g.events.Count - 1];
            Assert.AreEqual(GameEventType.Open, ev.type); Assert.AreEqual(-1, ev.target, "nobody was hiding there");
        }

        [Test]
        public void UniceAI_ResolveCheck_CapturesAHiderFoundInTheSpot()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var other = g.characters.Find(e => !e.isBot); other.outOfRound = true;
            var unice = g.characters.Find(e => e.isBot); unice.role = Role.Unice; g.unice = unice;
            unice.stunTime = 0; unice.tiredTime = 0; unice.brain.checkTimer = 0.01f; // one small tick away from resolving
            var spot = Map.Spots[0]; unice.x = spot.standX; unice.y = spot.standY; unice.brain.checkingSpot = spot;
            var hidden = new Character(5, "ghost", "Ghost", new int[7], true) { hidingSpot = spot };
            spot.occupant = hidden; g.characters.Add(hidden);
            g.phase = Phase.Hunt; g.timeLeft = 1000;

            try
            {
                g.Simulate(0.02f);

                Assert.IsTrue(hidden.outOfRound);
                Assert.AreEqual(1, unice.score);
            }
            finally { spot.occupant = null; }
        }

        // ---------- UniceBot: no target -> Patrol/UniceBot.SetGoal, and UniceBot.OnArrived expanding a Position goal via UncheckedSpotNear ----------

        [Test]
        public void UniceAI_WithNoVisibleTarget_PatrolsAndSetsAValidGoal()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var other = g.characters.Find(e => !e.isBot); other.outOfRound = true;
            var unice = g.characters.Find(e => e.isBot); unice.role = Role.Unice; g.unice = unice;
            unice.stunTime = 0; unice.tiredTime = 0; unice.brain.checkTimer = 0; unice.brain.chaseTarget = null; unice.brain.remembersTarget = false; unice.brain.pendingGoal = null;
            unice.x = unice.targetX = 900; unice.y = unice.targetY = 474; // in case this bot was originally the round's Unice and never got off (-600,-600)
            g.phase = Phase.Hunt; g.timeLeft = 1000;

            for (int i = 0; i < 40; i++) { unice.brain.goal = null; g.Simulate(0.016f); } // force Patrol to run fresh every tick

            Assert.IsNotNull(unice.brain.goal);
            Assert.That(unice.brain.goal.kind, Is.EqualTo(BotGoal.Kind.Position).Or.EqualTo(BotGoal.Kind.Spot));
            if (unice.brain.goal.kind == BotGoal.Kind.Spot) Assert.IsNotNull(unice.brain.goal.spot);
        }

        [Test]
        public void UniceAI_ArrivingAtAPosGoalWithStepsRemaining_ExpandsSearchViaSpotNear()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var other = g.characters.Find(e => !e.isBot); other.outOfRound = true;
            var unice = g.characters.Find(e => e.isBot); unice.role = Role.Unice; g.unice = unice;
            unice.stunTime = 0; unice.tiredTime = 0; unice.brain.checkTimer = 0; unice.brain.chaseTarget = null; unice.brain.remembersTarget = false; unice.brain.pendingGoal = null;
            var spot = Map.Spots[0];
            unice.brain.goal = new BotGoal { kind = BotGoal.Kind.Position, x = spot.standX, y = spot.standY, priority = 2, hopsLeft = 2, hasSearchCenter = true, searchCenterX = spot.standX, searchCenterY = spot.standY };
            unice.brain.path.Clear();
            g.phase = Phase.Hunt; g.timeLeft = 1000;

            g.Simulate(0.05f); // empty path -> arrives immediately -> UniceBot.OnArrived expands the Position goal via UncheckedSpotNear

            Assert.IsNotNull(unice.brain.goal);
            Assert.AreEqual(BotGoal.Kind.Spot, unice.brain.goal.kind);
            Assert.AreSame(spot, unice.brain.goal.spot, "the nearest never-checked spot to the goal's position is this one");
            Assert.AreEqual(1, unice.brain.goal.hopsLeft);
        }

        // ---------- UniceBot.HearNoise (and, as a side effect, BotPath.CanSee) ----------

        [Test]
        public void AiHear_NoiseFromAnUnseenHider_RedirectsTheUniceTowardIt()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var hider = g.characters.Find(e => !e.isBot); // human: skipped by the bot-AI loop, so its manual state below isn't overwritten
            var unice = g.characters.Find(e => e.isBot); unice.role = Role.Unice; g.unice = unice;
            unice.brain.chaseTarget = null; unice.brain.checkTimer = 0; unice.brain.goal = null;
            hider.role = Role.Hider; hider.outOfRound = false; hider.hidingSpot = null; hider.loud = true; hider.noiseTimer = 0.01f;
            hider.x = 400; hider.y = 300; // master bedroom
            unice.x = 650; unice.y = 300; // bath: 250 units away (within Rules.NoiseRange) but behind the dividing wall (no sightline)
            g.phase = Phase.Hunt; g.timeLeft = 1000;

            Assert.IsFalse(Map.HasLineOfSight(hider.x, hider.y, unice.x, unice.y), "the two rooms' only door isn't on this line");

            g.Simulate(0.02f);

            Assert.IsNotNull(unice.brain.goal);
            Assert.AreEqual(BotGoal.Kind.Position, unice.brain.goal.kind);
            Assert.AreEqual(400, unice.brain.goal.x); Assert.AreEqual(300, unice.brain.goal.y);
            Assert.AreEqual(1, unice.brain.goal.priority);
        }

        [Test]
        public void AiHear_TalkingWithoutLoud_AlsoRedirectsTheUnice()
        {
            // "normal" voice level counts as noise for the AI exactly like movement noise, even while standing still.
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var hider = g.characters.Find(e => !e.isBot);
            var unice = g.characters.Find(e => e.isBot); unice.role = Role.Unice; g.unice = unice;
            unice.brain.chaseTarget = null; unice.brain.checkTimer = 0; unice.brain.goal = null;
            hider.role = Role.Hider; hider.outOfRound = false; hider.hidingSpot = null; hider.loud = false; hider.talking = true; hider.noiseTimer = 0.01f;
            hider.x = 400; hider.y = 300; // master bedroom
            unice.x = 650; unice.y = 300; // bath: 250 units away (within Rules.NoiseRange) but behind the dividing wall (no sightline)
            g.phase = Phase.Hunt; g.timeLeft = 1000;

            g.Simulate(0.02f);

            Assert.IsNotNull(unice.brain.goal);
            Assert.AreEqual(BotGoal.Kind.Position, unice.brain.goal.kind);
            Assert.AreEqual(400, unice.brain.goal.x); Assert.AreEqual(300, unice.brain.goal.y);
            Assert.AreEqual(1, unice.brain.goal.priority);
        }

        [Test]
        public void AiHear_NeitherLoudNorTalking_DoesNotRedirectTheUnice()
        {
            // "sussurro" (whisper): no movement noise and no voice noise either, so the Unice must not be pulled toward the hider.
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var hider = g.characters.Find(e => !e.isBot);
            var unice = g.characters.Find(e => e.isBot); unice.role = Role.Unice; g.unice = unice;
            unice.brain.chaseTarget = null; unice.brain.checkTimer = 0; unice.brain.goal = null;
            hider.role = Role.Hider; hider.outOfRound = false; hider.hidingSpot = null; hider.loud = false; hider.talking = false; hider.noiseTimer = 0.01f;
            hider.x = 400; hider.y = 300;
            unice.x = 650; unice.y = 300;
            g.phase = Phase.Hunt; g.timeLeft = 1000;

            g.Simulate(0.02f);

            // UniceBot.HearNoise's signature is a Position goal at priority 1 placed exactly on the noise source; Patrol only ever sets priority 0.
            bool heardTheHider = unice.brain.goal != null && unice.brain.goal.kind == BotGoal.Kind.Position && unice.brain.goal.priority == 1 && unice.brain.goal.x == 400 && unice.brain.goal.y == 300;
            Assert.IsFalse(heardTheHider, "with loud=false and talking=false, AiHear must not fire");
        }

        // ---------- Giggle (the doll giving away a hidden holder's position) ----------

        [Test]
        public void SimUpdate_GiggleTimerRunsOutWhileHolderIsHidden_RevealsItsLastPosition()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana"))); // 3 characters: human + 2 bots
            var unice = g.characters[0]; unice.role = Role.Unice; g.unice = unice; // human, far away: inert and harmless to HiderBot's distance check
            unice.x = 900; unice.y = 474;
            var holder = g.characters[1]; holder.role = Role.Hider; holder.outOfRound = false;
            var spot = Map.Spots[0]; holder.x = spot.standX; holder.y = spot.standY; holder.hidingSpot = spot; spot.occupant = holder;
            g.doll.holder = holder;
            var other = g.characters[2]; other.outOfRound = true; // keep the 3rd ent out of the way entirely
            g.phase = Phase.Hunt; g.timeLeft = 100; g.doll.giggleTimer = 0.01f;

            try
            {
                g.Simulate(0.02f);

                Assert.AreEqual(Rules.GiggleInterval, g.doll.giggleTimer, "Giggle() resets its own timer");
                Assert.IsTrue(g.doll.hasLastGiggle);
                Assert.AreEqual(spot.standX, g.doll.lastGiggleX); Assert.AreEqual(spot.standY, g.doll.lastGiggleY);
                Assert.AreEqual(g.roundClock, g.doll.lastGiggleTime, 0.0001f);
                var ev = g.events[g.events.Count - 1];
                Assert.AreEqual(GameEventType.Giggle, ev.type);
                Assert.AreEqual((int)spot.standX, ev.x); Assert.AreEqual((int)spot.standY, ev.y);
            }
            finally { spot.occupant = null; }
        }

        // ---------- HiderBot: seeking a spot, and the two "flee" sub-paths ----------

        [Test]
        public void HiderAI_SeekingWithNoThreat_EventuallyFindsAndTakesAHidingSpot()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var unice = g.characters.Find(e => !e.isBot); unice.role = Role.Unice; g.unice = unice; // human: inert, no AI runs for it
            var hider = g.characters.Find(e => e.isBot); hider.role = Role.Hider; hider.hidingSpot = null; hider.outOfRound = false; hider.brain.mode = HiderMode.Seek;
            // StartRound only repositions the round's non-Unice characters to the spawn row; if this bot was originally
            // picked as Unice (before the override above), it would still be sitting at ResetRound's (-600,-600).
            hider.x = hider.targetX = 900; hider.y = hider.targetY = 474; // open hall floor, known clear of furniture
            g.phase = Phase.Hide; g.timeLeft = 1000;

            try
            {
                for (int i = 0; i < 100 && hider.hidingSpot == null; i++)
                {
                    g.Simulate(0.1f);
                    Assert.IsTrue(Map.CanStand(hider.x, hider.y), $"tick {i}: the hider must never end up inside a wall or furniture");
                }

                Assert.IsNotNull(hider.hidingSpot, "within 10 simulated seconds the hider should have found and taken a spot");
            }
            finally { if (hider.hidingSpot != null) hider.hidingSpot.occupant = null; }
        }

        [Test]
        public void HiderAI_FleeingNearAFreeSpot_HidesInItImmediately()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var unice = g.characters.Find(e => !e.isBot); unice.role = Role.Unice; g.unice = unice;
            var hider = g.characters.Find(e => e.isBot); hider.role = Role.Hider; hider.outOfRound = false; hider.hidingSpot = null;
            var spot = Map.Spots[0];
            hider.x = spot.standX; hider.y = spot.standY; hider.brain.mode = HiderMode.Flee; hider.brain.lastSawUniceAt = 0; hider.brain.startled = false;
            unice.x = 650; unice.y = 300; // far away (> 200) and behind a wall (no sightline) from the hider
            g.phase = Phase.Hunt; g.timeLeft = 1000; g.roundClock = 0;

            try
            {
                g.Simulate(0.02f);

                Assert.AreSame(spot, hider.hidingSpot, "NearFreeSpot found the spot the hider was already standing next to");
            }
            finally { spot.occupant = null; }
        }

        [Test]
        public void HiderAI_FleeingFarFromAnySpot_FallsBackToFleePointAndKeepsMoving()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var unice = g.characters.Find(e => !e.isBot); unice.role = Role.Unice; g.unice = unice;
            var hider = g.characters.Find(e => e.isBot); hider.role = Role.Hider; hider.outOfRound = false; hider.hidingSpot = null;
            hider.x = 400; hider.y = 300; hider.brain.mode = HiderMode.Flee; hider.brain.lastSawUniceAt = 0; hider.brain.startled = false; hider.brain.repathTimer = 0;
            unice.x = 650; unice.y = 300; // same wall-blocked pair as UniceBot.HearNoise's test: far (>200) and no sightline, but no spot nearby this time
            g.phase = Phase.Hunt; g.timeLeft = 1000; g.roundClock = 0;

            g.Simulate(0.02f);

            Assert.IsNull(hider.hidingSpot, "no spot was close enough, so it falls back to FleePoint instead of hiding");
            Assert.AreEqual(1.2f, hider.brain.repathTimer);
            Assert.IsFalse(hider.sneaking);
            Assert.IsTrue(Map.CanStand(hider.x, hider.y));
        }
    }
}
