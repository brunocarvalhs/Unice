using System.Collections.Generic;
using NUnit.Framework;

namespace Unice
{
    /// <summary>
    /// Characterization tests for Game's bot-AI and host-input paths (UniceAI/HiderAI/Patrol/ArriveU/ResolveCheck/
    /// SetGoal/SpotNear/FleePoint/Los/AiHear, plus the public CanMoveNow/BackToLobby/ReadInputs/NearSpotFurn).
    /// These are RNG-driven heuristics: tests assert INVARIANTS that must hold regardless of the random draws
    /// (e.g. "stays inside the house"), or force edge conditions that make the outcome deterministic (e.g. an
    /// empty path so FollowPath "arrives" on the very next tick, or a goal placed exactly on a free spot's
    /// anchor so NearFreeSpot/SpotNear have exactly one possible answer). Does not change Game.cs.
    /// </summary>
    [TestFixture]
    public class GameAiTests
    {
        static List<Game.Human> Humans(params (string id, string name)[] hs)
        {
            var list = new List<Game.Human>();
            foreach (var (id, name) in hs) list.Add(new Game.Human { id = id, name = name, k = null, joinT = 0 });
            return list;
        }

        static Game NewGameAfterMatch(int bots, List<Game.Human> humans)
        {
            var g = new Game { isHost = true, bots = bots, rounds = 5 };
            g.NewMatch(humans);
            return g;
        }

        // ---------- pure public helpers ----------

        [Test]
        public void CanMoveNow_CoversLiveOutOfRoundHiddenAndUniceGuards()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var e = new Ent(0, "x", "X", new int[7], false);

            g.ph = Phase.Hunt;
            Assert.IsTrue(g.CanMoveNow(e), "a plain hider can move during Hunt");

            e.outOfRound = true; Assert.IsFalse(g.CanMoveNow(e)); e.outOfRound = false;
            e.hidden = Map.Spots[0]; Assert.IsFalse(g.CanMoveNow(e)); e.hidden = null;

            e.role = Role.Unice; e.stun = 0;
            Assert.IsTrue(g.CanMoveNow(e), "Unice can move during Hunt when not stunned");
            g.ph = Phase.Hide; Assert.IsFalse(g.CanMoveNow(e), "Unice never moves during Hide");
            g.ph = Phase.Hunt; e.stun = 1; Assert.IsFalse(g.CanMoveNow(e), "a stunned Unice can't move even during Hunt");

            g.ph = Phase.Result; e.role = Role.Hider; e.stun = 0;
            Assert.IsFalse(g.CanMoveNow(e), "nobody moves outside Hide/Hunt");
        }

        [Test]
        public void BackToLobby_ResetsMatchStateAndRaisesLobbyEvent()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));

            g.BackToLobby();

            Assert.AreEqual(Phase.Lobby, g.ph);
            Assert.AreEqual(0, g.ents.Count); Assert.AreEqual(0, g.r);
            Assert.IsNull(g.unice); Assert.IsNull(g.doll.holder);
            Assert.AreEqual(EvType.Lobby, g.evs[g.evs.Count - 1].type);
        }

        [Test]
        public void NearSpotFurn_ReturnsNearestSpotWithin34Units_OrNullWhenFar()
        {
            var atTheBed = new Ent(0, "x", "X", new int[7], false) { x = Map.Spots[0].ax, y = Map.Spots[0].ay };
            Assert.AreSame(Map.Spots[0], Game.NearSpotFurn(atTheBed));

            var inTheOpenHall = new Ent(0, "x", "X", new int[7], false) { x = 900, y = 474 };
            Assert.IsNull(Game.NearSpotFurn(inTheOpenHall));
        }

        // ---------- ReadInputs (host applying what peers sent) ----------

        [Test]
        public void ReadInputs_PeerMissing_TurnsTheEntIntoABotAndRaisesLeftWhileLive()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana"), ("h2", "Beto")));
            g.ph = Phase.Hunt;
            var h2 = g.ents.Find(e => e.pid == "h2");

            g.ReadInputs(new List<PeerInfo>(), "h1", 0.05f);

            Assert.IsTrue(h2.bot, "a human peer that's gone from the room becomes a bot so the match keeps going");
            var ev = g.evs[g.evs.Count - 1];
            Assert.AreEqual(EvType.Left, ev.type); Assert.AreEqual(h2.idx, ev.a);
        }

        [Test]
        public void ReadInputs_NewerActionSequence_AppliesTheActionAndRemembersTheSequence()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana"), ("h2", "Beto")));
            var h2 = g.ents.Find(e => e.pid == "h2");
            h2.role = Role.Hider; h2.hidden = null; h2.outOfRound = false; // might have been randomly drawn as this round's Unice otherwise
            var spot = Map.Spots[0]; h2.x = spot.ax; h2.y = spot.ay;
            var peer = new PeerInfo { id = "h2", pres = new Presence { aSeq = 1, aType = Act.Hide, aArg = 0, hasPos = false } };

            try
            {
                g.ReadInputs(new List<PeerInfo> { peer }, "h1", 0.05f);

                Assert.AreSame(spot, h2.hidden);
                Assert.AreEqual(1, g.aseq["h2"]);
            }
            finally { spot.occ = null; }
        }

        [Test]
        public void ReadInputs_PositionSync_TeleportsWithinThreshold_AndRejectsAnImpossibleJump()
        {
            var g = NewGameAfterMatch(0, Humans(("h1", "Ana"), ("h2", "Beto"), ("h3", "Caio")));
            g.ph = Phase.Hunt;
            var h2 = g.ents.Find(e => e.pid == "h2"); h2.tp = 5; h2.x = 100; h2.y = 100;
            var h3 = g.ents.Find(e => e.pid == "h3"); h3.tp = 5; h3.x = 100; h3.y = 100;
            var peers = new List<PeerInfo>
            {
                new PeerInfo { id = "h2", pres = new Presence { aSeq = 0, hasPos = true, tp = 5, x = 150, y = 100, fx = 1, fy = 0, sneak = true } },
                new PeerInfo { id = "h3", pres = new Presence { aSeq = 0, hasPos = true, tp = 5, x = 2000, y = 2000, fx = 1, fy = 0 } }, // way beyond the 260 jump threshold
            };

            g.ReadInputs(peers, "h1", 0.1f);

            Assert.AreEqual(150, h2.x); Assert.AreEqual(100, h2.y); Assert.AreEqual(1, h2.fx); Assert.AreEqual(0, h2.fy); Assert.IsTrue(h2.sneak);
            Assert.AreEqual(100, h3.x); Assert.AreEqual(100, h3.y, "an impossible jump is rejected, not applied");
            Assert.AreEqual(6, h3.tp, "a rejected jump still bumps tp to force the client to resync");
        }

        // ---------- UniceAI: arrival -> checking -> resolving a spot ----------

        [Test]
        public void UniceAI_ArrivingAtASpotGoal_StartsCheckingThenResolvesAndOpensTheEmptySpot()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var other = g.ents.Find(e => !e.bot); other.outOfRound = true; // keep it out of Unice's target search
            var unice = g.ents.Find(e => e.bot); unice.role = Role.Unice; g.unice = unice;
            unice.stun = 0; unice.tired = 0; unice.checkT = 0; unice.target = null;
            var spot = Map.Spots[0];
            unice.x = spot.ax; unice.y = spot.ay; unice.goal = new Goal { kind = "spot", spot = spot, pri = 0, left = 0 }; unice.path.Clear();
            g.ph = Phase.Hunt; g.tl = 1000;

            g.SimUpdate(0.05f); // FollowPath "arrives" immediately (empty path) -> ArriveU starts the check
            Assert.AreEqual(Rules.AiCheck, unice.checkT);
            Assert.AreSame(spot, unice.checkSpot);

            g.SimUpdate(1.1f); // checkT runs out -> ResolveCheck
            Assert.IsNull(unice.goal); Assert.IsNull(unice.checkSpot);
            var ev = g.evs[g.evs.Count - 1];
            Assert.AreEqual(EvType.Open, ev.type); Assert.AreEqual(0, ev.b, "nobody was hiding there");
        }

        [Test]
        public void UniceAI_ResolveCheck_CapturesAHiderFoundInTheSpot()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var other = g.ents.Find(e => !e.bot); other.outOfRound = true;
            var unice = g.ents.Find(e => e.bot); unice.role = Role.Unice; g.unice = unice;
            unice.stun = 0; unice.tired = 0; unice.checkT = 0.01f; // one small tick away from resolving
            var spot = Map.Spots[0]; unice.x = spot.ax; unice.y = spot.ay; unice.checkSpot = spot;
            var hidden = new Ent(5, "ghost", "Ghost", new int[7], true) { hidden = spot };
            spot.occ = hidden; g.ents.Add(hidden);
            g.ph = Phase.Hunt; g.tl = 1000;

            try
            {
                g.SimUpdate(0.02f);

                Assert.IsTrue(hidden.outOfRound);
                Assert.AreEqual(1, unice.score);
            }
            finally { spot.occ = null; }
        }

        // ---------- UniceAI: no target -> Patrol/SetGoal, and ArriveU expanding a "pos" goal via SpotNear ----------

        [Test]
        public void UniceAI_WithNoVisibleTarget_PatrolsAndSetsAValidGoal()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var other = g.ents.Find(e => !e.bot); other.outOfRound = true;
            var unice = g.ents.Find(e => e.bot); unice.role = Role.Unice; g.unice = unice;
            unice.stun = 0; unice.tired = 0; unice.checkT = 0; unice.target = null; unice.hasMem = false; unice.pending = null;
            unice.x = unice.tx = 900; unice.y = unice.ty = 474; // in case this bot was originally the round's Unice and never got off (-600,-600)
            g.ph = Phase.Hunt; g.tl = 1000;

            for (int i = 0; i < 40; i++) { unice.goal = null; g.SimUpdate(0.016f); } // force Patrol to run fresh every tick

            Assert.IsNotNull(unice.goal);
            Assert.That(unice.goal.kind, Is.EqualTo("pos").Or.EqualTo("spot"));
            if (unice.goal.kind == "spot") Assert.IsNotNull(unice.goal.spot);
        }

        [Test]
        public void UniceAI_ArrivingAtAPosGoalWithStepsRemaining_ExpandsSearchViaSpotNear()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var other = g.ents.Find(e => !e.bot); other.outOfRound = true;
            var unice = g.ents.Find(e => e.bot); unice.role = Role.Unice; g.unice = unice;
            unice.stun = 0; unice.tired = 0; unice.checkT = 0; unice.target = null; unice.hasMem = false; unice.pending = null;
            var spot = Map.Spots[0];
            unice.goal = new Goal { kind = "pos", x = spot.ax, y = spot.ay, pri = 2, left = 2, hasAround = true, ax = spot.ax, ay = spot.ay };
            unice.path.Clear();
            g.ph = Phase.Hunt; g.tl = 1000;

            g.SimUpdate(0.05f); // empty path -> arrives immediately -> ArriveU expands the "pos" goal via SpotNear

            Assert.IsNotNull(unice.goal);
            Assert.AreEqual("spot", unice.goal.kind);
            Assert.AreSame(spot, unice.goal.spot, "the nearest never-checked spot to the goal's position is this one");
            Assert.AreEqual(1, unice.goal.left);
        }

        // ---------- AiHear (and, as a side effect, Los) ----------

        [Test]
        public void AiHear_NoiseFromAnUnseenHider_RedirectsTheUniceTowardIt()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var hider = g.ents.Find(e => !e.bot); // human: skipped by the bot-AI loop, so its manual state below isn't overwritten
            var unice = g.ents.Find(e => e.bot); unice.role = Role.Unice; g.unice = unice;
            unice.target = null; unice.checkT = 0; unice.goal = null;
            hider.role = Role.Hider; hider.outOfRound = false; hider.hidden = null; hider.loud = true; hider.noiseT = 0.01f;
            hider.x = 400; hider.y = 300; // master bedroom
            unice.x = 650; unice.y = 300; // bath: 250 units away (within NoiseR) but behind the dividing wall (no sightline)
            g.ph = Phase.Hunt; g.tl = 1000;

            Assert.IsFalse(Map.SightLine(hider.x, hider.y, unice.x, unice.y), "the two rooms' only door isn't on this line");

            g.SimUpdate(0.02f);

            Assert.IsNotNull(unice.goal);
            Assert.AreEqual("pos", unice.goal.kind);
            Assert.AreEqual(400, unice.goal.x); Assert.AreEqual(300, unice.goal.y);
            Assert.AreEqual(1, unice.goal.pri);
        }

        // ---------- Giggle (the doll giving away a hidden holder's position) ----------

        [Test]
        public void SimUpdate_GiggleTimerRunsOutWhileHolderIsHidden_RevealsItsLastPosition()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana"))); // 3 ents: human + 2 bots
            var unice = g.ents[0]; unice.role = Role.Unice; g.unice = unice; // human, far away: inert and harmless to HiderAI's distance check
            unice.x = 900; unice.y = 474;
            var holder = g.ents[1]; holder.role = Role.Hider; holder.outOfRound = false;
            var spot = Map.Spots[0]; holder.x = spot.ax; holder.y = spot.ay; holder.hidden = spot; spot.occ = holder;
            g.doll.holder = holder;
            var other = g.ents[2]; other.outOfRound = true; // keep the 3rd ent out of the way entirely
            g.ph = Phase.Hunt; g.tl = 100; g.doll.giggleT = 0.01f;

            try
            {
                g.SimUpdate(0.02f);

                Assert.AreEqual(Rules.GiggleEvery, g.doll.giggleT, "Giggle() resets its own timer");
                Assert.IsTrue(g.doll.hasLast);
                Assert.AreEqual(spot.ax, g.doll.lastX); Assert.AreEqual(spot.ay, g.doll.lastY);
                Assert.AreEqual(g.t, g.doll.lastT, 0.0001f);
                var ev = g.evs[g.evs.Count - 1];
                Assert.AreEqual(EvType.Giggle, ev.type);
                Assert.AreEqual((int)spot.ax, ev.a); Assert.AreEqual((int)spot.ay, ev.b);
            }
            finally { spot.occ = null; }
        }

        // ---------- HiderAI: seeking a spot, and the two "flee" sub-paths ----------

        [Test]
        public void HiderAI_SeekingWithNoThreat_EventuallyFindsAndTakesAHidingSpot()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var unice = g.ents.Find(e => !e.bot); unice.role = Role.Unice; g.unice = unice; // human: inert, no AI runs for it
            var hider = g.ents.Find(e => e.bot); hider.role = Role.Hider; hider.hidden = null; hider.outOfRound = false; hider.st = "seek";
            // StartRound only repositions the round's non-Unice ents to the spawn row; if this bot was originally
            // picked as Unice (before the override above), it would still be sitting at ResetRound's (-600,-600).
            hider.x = hider.tx = 900; hider.y = hider.ty = 474; // open hall floor, known clear of furniture
            g.ph = Phase.Hide; g.tl = 1000;

            try
            {
                for (int i = 0; i < 100 && hider.hidden == null; i++)
                {
                    g.SimUpdate(0.1f);
                    Assert.IsTrue(Map.CanStand(hider.x, hider.y), $"tick {i}: the hider must never end up inside a wall or furniture");
                }

                Assert.IsNotNull(hider.hidden, "within 10 simulated seconds the hider should have found and taken a spot");
            }
            finally { if (hider.hidden != null) hider.hidden.occ = null; }
        }

        [Test]
        public void HiderAI_FleeingNearAFreeSpot_HidesInItImmediately()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var unice = g.ents.Find(e => !e.bot); unice.role = Role.Unice; g.unice = unice;
            var hider = g.ents.Find(e => e.bot); hider.role = Role.Hider; hider.outOfRound = false; hider.hidden = null;
            var spot = Map.Spots[0];
            hider.x = spot.ax; hider.y = spot.ay; hider.st = "flee"; hider.lastSeenU = 0; hider.bolted = false;
            unice.x = 650; unice.y = 300; // far away (> 200) and behind a wall (no sightline) from the hider
            g.ph = Phase.Hunt; g.tl = 1000; g.t = 0;

            try
            {
                g.SimUpdate(0.02f);

                Assert.AreSame(spot, hider.hidden, "NearFreeSpot found the spot the hider was already standing next to");
            }
            finally { spot.occ = null; }
        }

        [Test]
        public void HiderAI_FleeingFarFromAnySpot_FallsBackToFleePointAndKeepsMoving()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana")));
            var unice = g.ents.Find(e => !e.bot); unice.role = Role.Unice; g.unice = unice;
            var hider = g.ents.Find(e => e.bot); hider.role = Role.Hider; hider.outOfRound = false; hider.hidden = null;
            hider.x = 400; hider.y = 300; hider.st = "flee"; hider.lastSeenU = 0; hider.bolted = false; hider.repath = 0;
            unice.x = 650; unice.y = 300; // same wall-blocked pair as AiHear's test: far (>200) and no sightline, but no spot nearby this time
            g.ph = Phase.Hunt; g.tl = 1000; g.t = 0;

            g.SimUpdate(0.02f);

            Assert.IsNull(hider.hidden, "no spot was close enough, so it falls back to FleePoint instead of hiding");
            Assert.AreEqual(1.2f, hider.repath);
            Assert.IsFalse(hider.sneak);
            Assert.IsTrue(Map.CanStand(hider.x, hider.y));
        }
    }
}
