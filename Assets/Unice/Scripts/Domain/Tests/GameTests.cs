using System.Collections.Generic;
using NUnit.Framework;

namespace Unice
{
    /// <summary>
    /// Characterization tests for Game's main flows (NewMatch, StartRound, DoAction, EndRound via its public
    /// triggers, TakeOver). Pins down CURRENT behavior of the straight-ported web rules; does not change Game.cs.
    /// HostStep's Result-phase transition is already covered by GameHostStepTests.cs and is not repeated here.
    /// </summary>
    [TestFixture]
    public class GameTests
    {
        static List<Game.Human> Humans(params (string id, string name)[] hs)
        {
            var list = new List<Game.Human>();
            foreach (var (id, name) in hs) list.Add(new Game.Human { id = id, name = name, k = null, joinT = 0 });
            return list;
        }

        /// <summary>A fresh match: NewMatch already runs StartRound once (r=1, Phase.Hide).</summary>
        static Game NewGameAfterMatch(int bots, List<Game.Human> humans)
        {
            var g = new Game { isHost = true, bots = bots, rounds = 5 };
            g.NewMatch(humans);
            return g;
        }

        // ---------- NewMatch ----------

        [Test]
        public void NewMatch_HumansPlusBots_FillsEntsAndEntersRoundOne()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana"), ("h2", "Beto")));

            Assert.AreEqual(4, g.ents.Count, "2 humans + bots configured (2) = 4");
            Assert.AreEqual(1, g.r);
            Assert.AreEqual(Phase.Hide, g.ph);
            Assert.IsNotNull(g.unice);
            Assert.IsNotNull(g.doll.holder);
            Assert.AreNotSame(g.unice, g.doll.holder);
        }

        [Test]
        public void NewMatch_SoloWithZeroBotsConfigured_StillAddsOneBotToReachTheMinimumOfTwo()
        {
            var g = NewGameAfterMatch(0, Humans(("h1", "Ana")));

            Assert.AreEqual(2, g.ents.Count, "the loop keeps adding bots while ents.Count < 2, even if bots == 0");
        }

        // ---------- StartRound ----------

        [Test]
        public void StartRound_CalledAgain_IncrementsRoundAndReassignsUnice()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            var humansNow = Humans(("h1", "Ana"));

            g.StartRound(humansNow);

            Assert.AreEqual(2, g.r);
            Assert.AreEqual(Phase.Hide, g.ph);
            Assert.AreEqual(Rules.HideTime, g.tl);
            Assert.IsNull(g.res);
            Assert.AreEqual(Role.Unice, g.unice.role);
            Assert.AreEqual(1, g.ents.FindAll(e => e.role == Role.Unice).Count, "exactly one Unice per round");
        }

        [Test]
        public void StartRound_NonUniceHiders_AreLinedUpOnTheSpawnRow()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));

            var hiders = g.ents.FindAll(e => e != g.unice);
            for (int i = 0; i < hiders.Count; i++)
            {
                Assert.AreEqual(620 + i * 70, hiders[i].x);
                Assert.AreEqual(474, hiders[i].y);
            }
        }

        // ---------- DoAction: hiders (Hide / Exit / Pass) ----------

        [Test]
        public void DoAction_HideNearAFreeSpot_OccupiesItAndSnapsToItsAnchor()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            var hider = g.ents.Find(e => e != g.unice);
            var spot = Map.Spots[0]; // "bed", anchor at (245,237)
            hider.x = spot.ax; hider.y = spot.ay;

            g.DoAction(hider, Act.Hide, 0);

            Assert.AreSame(spot, hider.hidden);
            Assert.AreSame(hider, spot.occ);
            Assert.AreEqual(spot.ax, hider.x); Assert.AreEqual(spot.ay, hider.y);
            Assert.IsFalse(hider.moving);
        }

        [Test]
        public void DoAction_HideTooFarFromTheSpot_DoesNothing()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            var hider = g.ents.Find(e => e != g.unice); // still at its spawn-row position, far from any furniture

            g.DoAction(hider, Act.Hide, 0);

            Assert.IsNull(hider.hidden);
            Assert.IsNull(Map.Spots[0].occ);
        }

        [Test]
        public void DoAction_Exit_ClearsBothTheHiderAndTheSpot()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            var hider = g.ents.Find(e => e != g.unice);
            var spot = Map.Spots[0];
            hider.x = spot.ax; hider.y = spot.ay;
            g.DoAction(hider, Act.Hide, 0);

            g.DoAction(hider, Act.Exit, 0);

            Assert.IsNull(hider.hidden);
            Assert.IsNull(spot.occ);
        }

        [Test]
        public void DoAction_PassWhileHoldingTheDollWithATargetNearby_TransfersTheDoll()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana"))); // 3 ents: 1 human + 2 bots
            foreach (var e in g.ents) e.role = Role.Hider;
            var unice = g.ents[0]; unice.role = Role.Unice; g.unice = unice;
            var holder = g.ents[1]; var other = g.ents[2];
            g.doll.holder = holder; holder.hidden = null; holder.passCd = 0;
            other.hidden = null; other.outOfRound = false;
            holder.x = 100; holder.y = 100; other.x = 130; other.y = 100; // 30 units apart, well within PassR(60)+20

            g.DoAction(holder, Act.Pass, 0);

            Assert.AreSame(other, g.doll.holder);
            Assert.AreEqual(Rules.PassCd, other.passCd);
            Assert.AreEqual(0, holder.passCd);
        }

        // ---------- DoAction: Unice (Open / Catch) ----------

        [Test]
        public void DoAction_UniceOpensAnEmptySpot_RaisesOpenEventWithoutCapturing()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            foreach (var e in g.ents) e.role = Role.Hider;
            var unice = g.ents[0]; unice.role = Role.Unice; g.unice = unice; unice.stun = 0;
            g.ph = Phase.Hunt;
            var spot = Map.Spots[0]; unice.x = spot.ax; unice.y = spot.ay;

            g.DoAction(unice, Act.Open, 0);

            var ev = g.evs[g.evs.Count - 1];
            Assert.AreEqual(EvType.Open, ev.type);
            Assert.AreEqual(0, ev.b, "no one was in the spot");
            Assert.AreEqual(Phase.Hunt, g.ph, "an empty spot never ends the round");
            Assert.AreEqual(0, unice.score);
        }

        [Test]
        public void DoAction_UniceOpensASpotHoldingTheDoll_CapturesAndEndsRoundAsAUniceWin()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            foreach (var e in g.ents) e.role = Role.Hider;
            var unice = g.ents[0]; unice.role = Role.Unice; g.unice = unice; unice.stun = 0;
            g.ph = Phase.Hunt;
            var spot = Map.Spots[0]; unice.x = spot.ax; unice.y = spot.ay;
            var holder = g.ents[1]; holder.hidden = spot; spot.occ = holder; g.doll.holder = holder;

            g.DoAction(unice, Act.Open, 0);

            Assert.AreEqual(Phase.Result, g.ph);
            Assert.AreEqual(3, unice.score, "Unice gets +3 for getting the doll back");
            Assert.AreEqual(Rules.ResultTime, g.tl);
            Assert.AreSame(holder, g.nextU, "whoever was caught with the doll becomes the next Unice");
            Assert.AreEqual(1, g.res[0]); Assert.AreEqual(holder.idx, g.res[1]); Assert.AreEqual(-1, g.res[2]); Assert.AreEqual(holder.idx, g.res[3]);
            Assert.IsNull(holder.hidden); Assert.IsNull(spot.occ);
        }

        [Test]
        public void DoAction_UniceCatchesAHiderWhoIsNotTheDollHolder_RemovesThemWithoutEndingTheRound()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            foreach (var e in g.ents) e.role = Role.Hider;
            var unice = g.ents[0]; unice.role = Role.Unice; g.unice = unice; unice.stun = 0;
            g.ph = Phase.Hunt;
            var target = g.ents[1]; var holder = g.ents[2];
            g.doll.holder = holder; target.hidden = null; target.outOfRound = false;
            unice.x = 100; unice.y = 100; target.x = 120; target.y = 100; // 20 units, well within CatchR(30)+45

            g.DoAction(unice, Act.Catch, target.idx);

            Assert.IsTrue(target.outOfRound);
            Assert.AreEqual(1, unice.score);
            Assert.AreEqual(Rules.Stun, unice.stun);
            Assert.AreEqual(Phase.Hunt, g.ph, "catching a non-holder never ends the round");
            var ev = g.evs[g.evs.Count - 1];
            Assert.AreEqual(EvType.Catch, ev.type); Assert.AreEqual(0, ev.c, "c=0 marks a non-holder capture");
        }

        // ---------- EndRound, reached via SimUpdate's clock running out (team win) ----------

        [Test]
        public void SimUpdate_ClockRunsOutDuringHunt_EndsRoundAsATeamWinAndScoresEveryone()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            foreach (var e in g.ents) e.role = Role.Hider;
            var unice = g.ents[0]; unice.role = Role.Unice; g.unice = unice;
            var holder = g.ents[1]; var other = g.ents[2];
            holder.turns = 0; other.turns = 0; holder.hidden = null;
            g.doll.holder = holder;
            g.ph = Phase.Hunt; g.tl = 0.01f;

            g.SimUpdate(0.02f);

            Assert.AreEqual(Phase.Result, g.ph);
            Assert.AreEqual(Rules.ResultTime, g.tl);
            Assert.AreEqual(3, holder.score, "holder gets the team's +2 plus its own +1");
            Assert.AreEqual(2, other.score, "every other hider gets +2 when the team wins");
            Assert.AreEqual(0, g.res[0]);
            Assert.AreEqual(holder.idx, g.res[1]);
            Assert.AreEqual(-1, g.res[2], "holder wasn't hidden when the clock ran out");
            Assert.That(g.nextU, Is.AnyOf(new[] { holder, other }), "next Unice is drawn from the tied-lowest-turns pool");
            Assert.AreEqual(g.nextU.idx, g.res[3]);
        }

        // ---------- SimUpdate: Hide -> Hunt transition ----------

        [Test]
        public void SimUpdate_HideTimerExpires_MovesToHuntAndSendsUniceThroughTheFrontDoor()
        {
            var g = NewGameAfterMatch(2, Humans(("h1", "Ana")));
            foreach (var e in g.ents) e.role = Role.Hider;
            var unice = g.ents[0]; unice.role = Role.Unice; g.unice = unice; // human Unice: no bot AI runs this tick to move it again
            g.ph = Phase.Hide; g.tl = 0.01f;
            int tpBefore = unice.tp;

            g.SimUpdate(0.02f);

            Assert.AreEqual(Phase.Hunt, g.ph);
            Assert.AreEqual(Rules.RoundTime, g.tl);
            Assert.AreEqual(Map.DoorIn.x, unice.x); Assert.AreEqual(Map.DoorIn.y, unice.y);
            Assert.AreEqual(0, unice.fx); Assert.AreEqual(-1, unice.fy);
            Assert.AreEqual(tpBefore + 1, unice.tp);
        }

        // ---------- TakeOver ----------

        [Test]
        public void TakeOver_PromotesToHostAndReconcilesPeerState()
        {
            var g = NewGameAfterMatch(1, Humans(("h1", "Ana"), ("h2", "Beto"))); // ents: h1, h2, 1 bot
            g.isHost = false; g.ep = 0;
            var me = g.ents.Find(e => e.pid == "h1");
            var h2 = g.ents.Find(e => e.pid == "h2");
            var bot = g.ents.Find(e => e.bot);
            foreach (var e in g.ents) { e.tx = e.x + 1; e.ty = e.y + 1; } // distinct from current x/y, so the reset is observable
            var peers = new List<PeerInfo> { new PeerInfo { id = "h1", pres = new Presence { aSeq = 5 } } }; // h2 is gone

            g.TakeOver(peers, me, "Ana");

            Assert.IsTrue(g.isHost);
            Assert.AreEqual(1, g.ep);
            Assert.AreEqual(5, g.aseq["h1"]);
            Assert.IsTrue(h2.bot, "a human peer missing from the room becomes a bot");
            Assert.IsTrue(bot.bot, "an existing bot stays a bot");
            Assert.AreEqual(h2.tx, h2.x); Assert.AreEqual(h2.ty, h2.y, "non-local ents snap to their last known target");
            Assert.AreEqual(bot.tx, bot.x); Assert.AreEqual(bot.ty, bot.y);
            foreach (var s in Map.Spots) Assert.IsNull(s.res);
            foreach (var e in g.ents) { Assert.AreEqual("seek", e.st); Assert.IsNull(e.goal); Assert.AreEqual(0, e.path.Count); }
            var ev = g.evs[g.evs.Count - 1];
            Assert.AreEqual(EvType.Host, ev.type); Assert.AreEqual("Ana", ev.s);
        }
    }
}
