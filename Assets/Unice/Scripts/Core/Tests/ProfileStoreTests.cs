using NUnit.Framework;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// Characterization tests for ProfileStore, extracted out of App (see docs/ARCHITECTURE_AUDIT.md,
    /// "App/Ui Split Plan", Slice 1). Saves/restores the real "unice.name"/"unice.kit" PlayerPrefs keys
    /// around each test so this doesn't clobber whatever profile is saved on the machine running the suite.
    /// </summary>
    [TestFixture]
    public class ProfileStoreTests
    {
        const string NameKey = "unice.name", KitKey = "unice.kit";
        bool hadName, hadKit; string savedName; int savedKit;

        [SetUp]
        public void SetUp()
        {
            hadName = PlayerPrefs.HasKey(NameKey); if (hadName) savedName = PlayerPrefs.GetString(NameKey);
            hadKit = PlayerPrefs.HasKey(KitKey); if (hadKit) savedKit = PlayerPrefs.GetInt(KitKey);
            PlayerPrefs.DeleteKey(NameKey); PlayerPrefs.DeleteKey(KitKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadName) PlayerPrefs.SetString(NameKey, savedName); else PlayerPrefs.DeleteKey(NameKey);
            if (hadKit) PlayerPrefs.SetInt(KitKey, savedKit); else PlayerPrefs.DeleteKey(KitKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void Ctor_NoSavedProfile_DefaultsToNovaAndARandomValidKit()
        {
            var p = new ProfileStore();

            Assert.AreEqual("Nova", p.Name);
            Assert.AreEqual(Kit.Slots, p.Kit.Length);
            for (int i = 0; i < Kit.Slots; i++) Assert.That(p.Kit[i], Is.InRange(0, Kit.OptionCount[i] - 1));
        }

        [Test]
        public void Save_ThenNewInstance_ReloadsTheSameNameAndKit()
        {
            var p = new ProfileStore { Name = "Bruno", Kit = new[] { 1, 2, 3, 4, 5, 2, 1 } };
            p.Save();

            var reloaded = new ProfileStore();

            Assert.AreEqual("Bruno", reloaded.Name);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 2, 1 }, reloaded.Kit);
        }

        [Test]
        public void SetKit_CleansTheKitAndPersistsIt()
        {
            var p = new ProfileStore();

            p.SetKit(new[] { 99, -1, 2, 3, 4, 5, 2 }); // slots 0 and 1 are out of range -> Kit.Clean zeroes them

            CollectionAssert.AreEqual(new[] { 0, 0, 2, 3, 4, 5, 2 }, p.Kit);
            var reloaded = new ProfileStore();
            CollectionAssert.AreEqual(new[] { 0, 0, 2, 3, 4, 5, 2 }, reloaded.Kit, "SetKit saves immediately, like SaveProfile() used to");
        }
    }
}
