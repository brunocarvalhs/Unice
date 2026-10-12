using NUnit.Framework;

namespace Unice
{
    /// <summary>The sound mix rules in <see cref="Sfx"/> that don't need audio: gain per sound and voice choice.</summary>
    [TestFixture]
    public class SfxTests
    {
        const int VoiceCount = 12;

        [Test]
        public void GainFor_Heart_QuieterThanGiggle()
        {
            Assert.Less(Sfx.GainFor("heart"), Sfx.GainFor("giggle"));
        }

        [Test]
        public void GainFor_UnlistedSound_One()
        {
            Assert.AreEqual(1f, Sfx.GainFor("tick"));
        }

        [TestCase("giggle")]
        [TestCase("sting")]
        [TestCase("boom")]
        public void PickVoice_CriticalSound_ReservedVoiceWithoutAdvancing(string name)
        {
            int nextVoice = 5;

            int voice = Sfx.PickVoice(Sfx.IsCritical(name), ref nextVoice, VoiceCount);

            Assert.AreEqual(0, voice, "the reserved voice");
            Assert.AreEqual(5, nextVoice, "the other sounds' turn is kept");
        }

        [Test]
        public void PickVoice_ManyOrdinarySounds_NeverUseReservedVoice()
        {
            int nextVoice = 1;

            for (int i = 0; i < VoiceCount * 3; i++)
                Assert.AreNotEqual(0, Sfx.PickVoice(Sfx.IsCritical("step"), ref nextVoice, VoiceCount));
        }

        [Test]
        public void PickVoice_LastVoice_WrapsToFirstUnreserved()
        {
            int nextVoice = VoiceCount - 1;

            Sfx.PickVoice(false, ref nextVoice, VoiceCount);

            Assert.AreEqual(1, nextVoice);
        }
    }
}
