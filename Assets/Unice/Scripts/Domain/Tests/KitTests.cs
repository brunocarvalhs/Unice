using System;
using NUnit.Framework;

namespace Unice
{
    [TestFixture]
    public class KitTests
    {
        [Test]
        public void Clean_ValidValues_ReturnsUnchanged()
        {
            var k = new[] { 1, 2, 3, 4, 5, 2, 1 };
            CollectionAssert.AreEqual(k, Kit.Sanitize(k));
        }

        [Test]
        public void Clean_NullArray_ReturnsAllZeros() => CollectionAssert.AreEqual(new int[Kit.Slots], Kit.Sanitize(null));

        [Test]
        public void Clean_ShorterArray_PadsRemainderWithZeros()
        {
            var r = Kit.Sanitize(new[] { 1, 2 });
            CollectionAssert.AreEqual(new[] { 1, 2, 0, 0, 0, 0, 0 }, r);
        }

        [Test]
        public void Clean_LongerArray_IgnoresExtraElements()
        {
            var r = Kit.Sanitize(new[] { 1, 2, 3, 4, 5, 2, 1, 999, 999 });
            Assert.AreEqual(Kit.Slots, r.Length);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 2, 1 }, r);
        }

        [Test]
        public void Clean_NegativeValue_ClampsToZero() => CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0, 0, 0 }, Kit.Sanitize(new[] { -1, -5, 0, 0, 0, 0, 0 }));

        [Test]
        public void Clean_ValueAtOrAboveMax_ClampsToZero()
        {
            // Max = { 4, 4, 5, 5, 8, 6, 4 }: values equal to Max (one past the valid range) fall back to 0.
            var r = Kit.Sanitize(new[] { 4, 4, 5, 5, 8, 6, 4 });
            CollectionAssert.AreEqual(new int[Kit.Slots], r);
        }

        [Test]
        public void Random_ReturnsArrayOfSlotsLength() => Assert.AreEqual(Kit.Slots, Kit.Random().Length);

        [Test]
        public void Random_EachSlotStaysWithinItsMax()
        {
            for (int n = 0; n < 200; n++)
            {
                var r = Kit.Random();
                for (int i = 0; i < Kit.Slots; i++) Assert.That(r[i], Is.InRange(0, Kit.OptionCount[i] - 1));
            }
        }

        [Test]
        public void Pack_Unpack_RoundTripsCleanValues()
        {
            var k = new[] { 3, 1, 4, 2, 7, 5, 3 };
            Assert.Less(Kit.Pack(k), 1 << (3 * Kit.Slots)); // fits in 3 bits/slot as documented
            CollectionAssert.AreEqual(k, Kit.Unpack(Kit.Pack(k)));
        }

        [Test]
        public void Pack_Unpack_RoundTripsAllZeros() => CollectionAssert.AreEqual(new int[Kit.Slots], Kit.Unpack(Kit.Pack(new int[Kit.Slots])));

        [Test]
        public void Unpack_AnyIntValue_AlwaysReturnsACleanArray()
        {
            // Unpack runs its 3-bit extraction through Clean(), so even garbage ints come back valid.
            foreach (int v in new[] { 0, -1, int.MaxValue, int.MinValue, 12345678 })
            {
                var r = Kit.Unpack(v);
                Assert.AreEqual(Kit.Slots, r.Length);
                for (int i = 0; i < Kit.Slots; i++) Assert.That(r[i], Is.InRange(0, Kit.OptionCount[i] - 1));
            }
        }

        [Test]
        public void Unpack_BitsBeyondSlotRange_AreIgnored()
        {
            // Slots * 3 = 21 bits are consumed; anything packed above bit 21 has no effect.
            int highBitNoise = 1 << 25;
            CollectionAssert.AreEqual(Kit.Unpack(0), Kit.Unpack(highBitNoise));
        }

        [Test]
        public void Pack_NullArray_ThrowsNullReferenceException()
        {
            // Unlike Clean(), Pack() has no null-guard: documenting the current (crashy) behavior, not fixing it.
            Assert.Throws<NullReferenceException>(() => Kit.Pack(null));
        }

        [Test]
        public void Pack_ArrayShorterThanSlots_ThrowsIndexOutOfRangeException()
        {
            // Unlike Clean(), Pack() doesn't bounds-check the input length.
            Assert.Throws<IndexOutOfRangeException>(() => Kit.Pack(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Pack_ValueAboveSevenForASlot_SilentlyWrapsViaBitmask()
        {
            // Pack masks each slot with "& 7": an unclamped value of 9 wraps around to 1 instead of
            // being rejected. Only reachable if a caller skips Clean()/Random() before packing.
            var k = new[] { 9, 0, 0, 0, 0, 0, 0 };
            Assert.AreEqual(1, Kit.Pack(k) & 7);
        }

        [Test]
        public void OutfitColor_ValidIndex_ReturnsMatchingColor()
        {
            var k = new[] { 0, 0, 0, 0, 3, 0, 0 };
            Assert.AreEqual(Kit.OutfitColors[3], Kit.OutfitColor(k));
        }

        [Test]
        public void OutfitColor_NullArray_ThrowsNullReferenceException() => Assert.Throws<NullReferenceException>(() => Kit.OutfitColor(null));

        [Test]
        public void OutfitColor_IndexOutOfRange_ThrowsIndexOutOfRangeException()
        {
            // OutfitColor() does no bounds-check of its own; it relies on callers having run Clean() first.
            var k = new[] { 0, 0, 0, 0, 99, 0, 0 };
            Assert.Throws<IndexOutOfRangeException>(() => Kit.OutfitColor(k));
        }
    }
}
