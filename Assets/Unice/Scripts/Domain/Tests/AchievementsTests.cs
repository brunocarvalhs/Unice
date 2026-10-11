using NUnit.Framework;

namespace Unice
{
    /// <summary>Which Play Games achievements and leaderboard entries each moment earns (docs/PLAY_GAMES.md, section 5).</summary>
    [TestFixture]
    public class AchievementsTests
    {
        // ---------- catch ----------
        [Test]
        public void ForCatch_ICaughtAHider_FoundYouAndOneHuntressStep() =>
            Assert.That(Achievements.ForCatch(iCaught: true, withDoll: false), Is.EquivalentTo(new[] { Achievement.FoundYou, Achievement.Huntress }));

        [Test]
        public void ForCatch_ICaughtTheDollHolder_AlsoGiveHerBack() =>
            Assert.That(Achievements.ForCatch(iCaught: true, withDoll: true), Has.Member(Achievement.GiveHerBack));

        [Test]
        public void ForCatch_SomeoneElseCaught_Nothing() =>
            Assert.That(Achievements.ForCatch(iCaught: false, withDoll: true), Is.Empty);

        // ---------- round end ----------
        /// <summary>A hider who wasn't caught when the time ran out (the Unice lost), with <paramref name="change"/> applied.</summary>
        static RoundFacts HiderSurvived(System.Action<RoundFacts> change = null)
        {
            var facts = new RoundFacts { uniceWon = false, iAmUnice = false, iWasCaught = false, hiders = 4, hidersFree = 2 };
            change?.Invoke(facts);
            return facts;
        }

        [Test]
        public void ForRoundEnd_HiderNotCaughtWhenTimeRanOut_NobodySawMe() =>
            Assert.That(Achievements.ForRoundEnd(HiderSurvived()), Has.Member(Achievement.NobodySawMe));

        [Test]
        public void ForRoundEnd_HiderCaughtButTeamWon_NoNobodySawMe() =>
            Assert.That(Achievements.ForRoundEnd(HiderSurvived(f => f.iWasCaught = true)), Has.No.Member(Achievement.NobodySawMe));

        [Test]
        public void ForRoundEnd_UniceLostTheRound_NoNobodySawMe() =>
            Assert.That(Achievements.ForRoundEnd(HiderSurvived(f => f.iAmUnice = true)), Is.Empty);

        [Test]
        public void ForRoundEnd_HeldTheDollWhenTimeRanOut_DollsKeeper() =>
            Assert.That(Achievements.ForRoundEnd(HiderSurvived(f => f.iHeldDoll = true)), Has.Member(Achievement.DollsKeeper));

        [Test]
        public void ForRoundEnd_HeldTheDollButWasCaught_NoDollsKeeper() =>
            Assert.That(Achievements.ForRoundEnd(HiderSurvived(f => { f.iHeldDoll = true; f.uniceWon = true; })), Has.No.Member(Achievement.DollsKeeper));

        [Test]
        public void ForRoundEnd_PassedTheDollAndTeamWon_HotPotato() =>
            Assert.That(Achievements.ForRoundEnd(HiderSurvived(f => f.iPassedDoll = true)), Has.Member(Achievement.HotPotato));

        [Test]
        public void ForRoundEnd_PassedTheDollButUniceWon_NoHotPotato() =>
            Assert.That(Achievements.ForRoundEnd(HiderSurvived(f => { f.iPassedDoll = true; f.uniceWon = true; })), Has.No.Member(Achievement.HotPotato));

        [Test]
        public void ForRoundEnd_LaughedAsDecoyAndSurvived_Decoy() =>
            Assert.That(Achievements.ForRoundEnd(HiderSurvived(f => f.iLaughedAsDecoy = true)), Has.Member(Achievement.Decoy));

        [Test]
        public void ForRoundEnd_LaughedAsDecoyButCaught_NoDecoy() =>
            Assert.That(Achievements.ForRoundEnd(HiderSurvived(f => { f.iLaughedAsDecoy = true; f.iWasCaught = true; })), Has.No.Member(Achievement.Decoy));

        [Test]
        public void ForRoundEnd_UniceCaughtEveryoneOfThreePlusHiders_NoWitnesses() =>
            Assert.That(Achievements.ForRoundEnd(new RoundFacts { uniceWon = true, iAmUnice = true, hiders = 3, hidersFree = 0 }),
                Has.Member(Achievement.NoWitnesses));

        [Test]
        public void ForRoundEnd_UniceCaughtTheDollWithOneHiderStillFree_NoNoWitnesses() =>
            Assert.That(Achievements.ForRoundEnd(new RoundFacts { uniceWon = true, iAmUnice = true, hiders = 3, hidersFree = 1 }), Is.Empty);

        [Test]
        public void ForRoundEnd_UniceCaughtEveryoneOfTwoHiders_NoNoWitnesses() =>
            Assert.That(Achievements.ForRoundEnd(new RoundFacts { uniceWon = true, iAmUnice = true, hiders = 2, hidersFree = 0 }), Is.Empty);

        // ---------- match end ----------
        [Test]
        public void ForMatchEnd_SoloFirstPlace_ChampionAndOneVeteranStep() =>
            Assert.That(Achievements.ForMatchEnd(place: 1, humans: 1, online: false), Is.EquivalentTo(new[] { Achievement.Champion, Achievement.Veteran }));

        [Test]
        public void ForMatchEnd_SecondPlace_NoChampion() =>
            Assert.That(Achievements.ForMatchEnd(place: 2, humans: 1, online: false), Is.EquivalentTo(new[] { Achievement.Veteran }));

        [Test]
        public void ForMatchEnd_OnlineWithAnotherPerson_AmongFriends() =>
            Assert.That(Achievements.ForMatchEnd(place: 2, humans: 2, online: true), Has.Member(Achievement.AmongFriends));

        [Test]
        public void ForMatchEnd_OnlineAloneWithBots_NoAmongFriends() =>
            Assert.That(Achievements.ForMatchEnd(place: 1, humans: 1, online: true), Has.No.Member(Achievement.AmongFriends));

        [Test]
        public void ForMatchEnd_OnlineWinWithThreePeople_TopOfTheRoom() =>
            Assert.That(Achievements.ForMatchEnd(place: 1, humans: 3, online: true), Has.Member(Achievement.TopOfTheRoom));

        [Test]
        public void ForMatchEnd_OnlineWinWithTwoPeople_NoTopOfTheRoom() =>
            Assert.That(Achievements.ForMatchEnd(place: 1, humans: 2, online: true), Has.No.Member(Achievement.TopOfTheRoom));

        [Test]
        public void IsIncremental_VeteranAndHuntress_True() =>
            Assert.IsTrue(Achievements.IsIncremental(Achievement.Veteran) && Achievements.IsIncremental(Achievement.Huntress));

        [Test]
        public void IsIncremental_Champion_False() => Assert.IsFalse(Achievements.IsIncremental(Achievement.Champion));

        // ---------- leaderboard ----------
        [Test]
        public void CountsForLeaderboard_OnlineWinTwoPeopleThreeRoundsSixMinutes_True() =>
            Assert.IsTrue(Achievements.CountsForLeaderboard(place: 1, humans: 2, online: true, totalRounds: 3, seconds: 360));

        [TestCase(2, 2, true, 3, 600f, TestName = "CountsForLeaderboard_NotFirst_False")]
        [TestCase(1, 1, false, 5, 900f, TestName = "CountsForLeaderboard_Solo_False")]
        [TestCase(1, 1, true, 5, 900f, TestName = "CountsForLeaderboard_OnlineOneHuman_False")]
        [TestCase(1, 4, true, 3, 359.9f, TestName = "CountsForLeaderboard_UnderSixMinutes_False")]
        [TestCase(1, 4, true, 2, 900f, TestName = "CountsForLeaderboard_TwoRounds_False")]
        public void CountsForLeaderboard_FilterFails_False(int place, int humans, bool online, int totalRounds, float seconds) =>
            Assert.IsFalse(Achievements.CountsForLeaderboard(place, humans, online, totalRounds, seconds));
    }
}
