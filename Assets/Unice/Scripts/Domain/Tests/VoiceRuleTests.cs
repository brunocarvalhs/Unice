using NUnit.Framework;

namespace Unice
{
    [TestFixture]
    public class VoiceRuleTests
    {
        [Test]
        public void CanSpeak_MicOff_IsFalse() => Assert.IsFalse(VoiceRule.CanSpeak(false, null));

        [Test]
        public void CanSpeak_MicOnNoEnt_IsTrue() => Assert.IsTrue(VoiceRule.CanSpeak(true, null));   // lobby / not in a round: free chat

        [Test]
        public void CanSpeak_MicOnNotHidden_IsTrue()
        {
            var me = new Character(0, "a", "A", null, false);
            Assert.IsTrue(VoiceRule.CanSpeak(true, me));
        }

        [Test]
        public void CanSpeak_MicOnHidden_IsFalse()
        {
            var me = new Character(0, "a", "A", null, false);
            me.hidingSpot = new Furniture();
            Assert.IsFalse(VoiceRule.CanSpeak(true, me));
        }

        static Character Hider(bool caught) => new Character(0, "h", "H", null, false) { outOfRound = caught };

        [Test]
        public void CanHear_CaughtSpeakerLiveListener_IsFalse() => Assert.IsFalse(VoiceRule.CanHear(true, Hider(false), Hider(true)));

        [Test]
        public void CanHear_CaughtSpeakerUniceListener_IsFalse()
        {
            var unice = new Character(1, "u", "U", null, false) { role = Role.Unice };
            Assert.IsFalse(VoiceRule.CanHear(true, unice, Hider(true)));
        }

        [Test]
        public void CanHear_LiveSpeakerLiveListener_IsTrue() => Assert.IsTrue(VoiceRule.CanHear(true, Hider(false), Hider(false)));

        [TestCase(false)]
        [TestCase(true)]
        public void CanHear_CaughtListener_HearsEveryone(bool speakerCaught) => Assert.IsTrue(VoiceRule.CanHear(true, Hider(true), Hider(speakerCaught)));

        [Test]
        public void CanHear_CaughtSpeakerOutsideRound_IsTrue() => Assert.IsTrue(VoiceRule.CanHear(false, Hider(false), Hider(true)));   // result screen keeps outOfRound

        [Test]
        public void CanHear_NoListenerCharacter_IsTrue() => Assert.IsTrue(VoiceRule.CanHear(true, null, Hider(true)));
    }
}
