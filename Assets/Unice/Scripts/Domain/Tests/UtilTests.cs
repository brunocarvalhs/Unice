using System.Collections.Generic;
using NUnit.Framework;

namespace Unice
{
    [TestFixture]
    public class UtilTests
    {
        [Test]
        public void Clamp_WithinRange_ReturnsValue() => Assert.AreEqual(5f, U.Clamp(5f, 0f, 10f));

        [Test]
        public void Clamp_BelowMin_ReturnsMin() => Assert.AreEqual(0f, U.Clamp(-5f, 0f, 10f));

        [Test]
        public void Clamp_AboveMax_ReturnsMax() => Assert.AreEqual(10f, U.Clamp(15f, 0f, 10f));

        [Test]
        public void Dist_Coordinates_MatchesEuclidean() => Assert.AreEqual(5f, U.Dist(0f, 0f, 3f, 4f), 0.0001f);

        [Test]
        public void Dist_SamePoint_IsZero() => Assert.AreEqual(0f, U.Dist(2f, 2f, 2f, 2f), 0.0001f);

        [Test]
        public void Dist_Ents_UsesTheirPositions()
        {
            var a = new Ent(0, "a", "A", null, false); a.x = 0; a.y = 0;
            var b = new Ent(1, "b", "B", null, false); b.x = 3; b.y = 4;
            Assert.AreEqual(5f, U.Dist(a, b), 0.0001f);
        }

        [Test]
        public void CleanName_Null_ReturnsFallback() => Assert.AreEqual("Alguém", U.CleanName(null));

        [Test]
        public void CleanName_Empty_ReturnsFallback() => Assert.AreEqual("Alguém", U.CleanName(""));

        [Test]
        public void CleanName_NormalName_IsUnchanged() => Assert.AreEqual("Bruno", U.CleanName("Bruno"));

        [Test]
        public void CleanName_TrimsWhitespace() => Assert.AreEqual("Bruno", U.CleanName("  Bruno  "));

        [Test]
        public void CleanName_StripsControlAndInvisibleChars() => Assert.AreEqual("Bruno", U.CleanName("Brun​o"));

        [Test]
        public void CleanName_CapsAtTwelveChars() => Assert.AreEqual("123456789012", U.CleanName("1234567890123456"));

        [Test]
        public void CleanName_OnlyJunkChars_ReturnsFallback() => Assert.AreEqual("Alguém", U.CleanName("​‌"));

        [Test]
        public void Mmss_ZeroSeconds_IsZeroZero() => Assert.AreEqual("0:00", U.Mmss(0f));

        [Test]
        public void Mmss_OverAMinute_FormatsMinutesAndSeconds() => Assert.AreEqual("2:05", U.Mmss(125f));

        [Test]
        public void Mmss_Negative_ClampsToZero() => Assert.AreEqual("0:00", U.Mmss(-10f));

        [Test]
        public void Mmss_FractionalSeconds_RoundsUp() => Assert.AreEqual("0:01", U.Mmss(0.2f));

        [Test]
        public void CleanCode_Null_ReturnsEmpty() => Assert.AreEqual("", U.CleanCode(null));

        [Test]
        public void CleanCode_LowercaseLetters_AreUppercased() => Assert.AreEqual("AB12", U.CleanCode("ab12"));

        [Test]
        public void CleanCode_DropsSymbolsAndSpaces() => Assert.AreEqual("AB12", U.CleanCode("a-b 1_2!"));

        [Test]
        public void RandInt_StaysWithinRange()
        {
            for (int i = 0; i < 500; i++)
            {
                int v = U.RandInt(7);
                Assert.That(v, Is.InRange(0, 6));
            }
        }

        [Test]
        public void Rand_StaysWithinRange()
        {
            for (int i = 0; i < 500; i++)
            {
                float v = U.Rand(2f, 5f);
                Assert.That(v, Is.InRange(2f, 5f));
            }
        }

        [Test]
        public void Pick_ReturnsElementFromTheList()
        {
            var list = new List<string> { "a", "b", "c", "d" };
            for (int i = 0; i < 100; i++) Assert.Contains(U.Pick(list), list);
        }

        [Test]
        public void Shuffle_PreservesMultiset()
        {
            var original = new List<int> { 1, 2, 3, 4, 5, 5, 6 };
            var list = new List<int>(original);
            U.Shuffle(list);
            CollectionAssert.AreEquivalent(original, list);
        }

        [Test]
        public void OfflineCode_HasExpectedLengthAndAlphabet()
        {
            const string alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
            for (int i = 0; i < 50; i++)
            {
                string code = U.OfflineCode();
                Assert.AreEqual(5, code.Length);
                foreach (char c in code) Assert.That(alphabet, Does.Contain(c));
            }
        }
    }
}
