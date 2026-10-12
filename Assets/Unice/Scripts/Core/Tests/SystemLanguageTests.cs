using NUnit.Framework;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// The language the game opens in: the one the player picked on this device wins; on the first run (nothing saved)
    /// it follows the device's language — Portuguese → Pt, Spanish → Es, anything else → En.
    /// </summary>
    [TestFixture]
    public class SystemLanguageTests
    {
        [Test]
        public void FirstRun_SpanishSystem_PicksEs()
        {
            Assert.That(Localization.Initial(null, SystemLanguage.Spanish), Is.EqualTo(Language.Es));
        }

        [Test]
        public void FirstRun_PortugueseSystem_PicksPt()
        {
            Assert.That(Localization.Initial(null, SystemLanguage.Portuguese), Is.EqualTo(Language.Pt));
        }

        [TestCase(SystemLanguage.English)]
        [TestCase(SystemLanguage.French)]
        [TestCase(SystemLanguage.Unknown)]
        public void FirstRun_OtherSystem_PicksEnglish(SystemLanguage systemLanguage)
        {
            Assert.That(Localization.Initial(null, systemLanguage), Is.EqualTo(Language.En));
        }

        [Test]
        public void SavedLanguage_Wins()
        {
            Assert.That(Localization.Initial((int)Language.Es, SystemLanguage.Portuguese), Is.EqualTo(Language.Es));
        }
    }
}
