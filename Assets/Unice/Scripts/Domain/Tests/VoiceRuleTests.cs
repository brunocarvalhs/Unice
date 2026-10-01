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
            var me = new Ent(0, "a", "A", null, false);
            Assert.IsTrue(VoiceRule.CanSpeak(true, me));
        }

        [Test]
        public void CanSpeak_MicOnHidden_IsFalse()
        {
            var me = new Ent(0, "a", "A", null, false);
            me.hidden = new Furn();
            Assert.IsFalse(VoiceRule.CanSpeak(true, me));
        }
    }
}
