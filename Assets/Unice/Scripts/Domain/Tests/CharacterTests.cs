using System.Collections.Generic;
using NUnit.Framework;

namespace Unice
{
    /// <summary>
    /// The previous position kept at each fixed step, so frames between steps draw characters part of the way (render
    /// interpolation), and the teleports that must not be drawn sliding across the house.
    /// </summary>
    [TestFixture]
    public class CharacterTests
    {
        /// <summary>Any fixed value works: it only has to be the same on every run.</summary>
        const int TestSeed = 1234;

        [SetUp]
        public void SetUp()
        {
            Map.Load(MapHouse.Build());
            GameRandom.Seed(TestSeed);
        }

        static Character Walked(float fromX, float toX)
        {
            var character = new Character(0, "h1", "Ana", new int[7], false);
            character.Teleport(fromX, 100);
            character.KeepPrevious();
            character.x = toX;
            return character;
        }

        static Game HostWithHuman()
        {
            var game = new Game { isHost = true, botCount = 2, totalRounds = 5 };
            game.NewMatch(new List<Game.Human> { new Game.Human { id = "h1", name = "Ana", kit = new int[7], joinTime = 0 } });
            return game;
        }

        [Test]
        public void DrawX_HalfwayThroughStep_IsHalfwayBetweenPreviousAndCurrent()
        {
            var character = Walked(100, 110);

            Assert.That(character.DrawX(.5f), Is.EqualTo(105).Within(1e-4f));
        }

        [Test]
        public void Teleport_AfterWalking_SetsPreviousToNewPosition()
        {
            var character = Walked(100, 110);

            character.Teleport(500, 300);

            Assert.That(character.prevX, Is.EqualTo(500), "x");
            Assert.That(character.prevY, Is.EqualTo(300), "y");
        }

        [Test]
        public void StartRound_HiderWalkedLastStep_DrawnAtSpawnWithoutSliding()
        {
            var game = HostWithHuman();
            foreach (var character in game.characters) { character.KeepPrevious(); character.x += 40; }

            game.StartRound(new List<Game.Human>());

            foreach (var character in game.characters)
                Assert.That(character.DrawX(.5f), Is.EqualTo(character.x), character.name);
        }

        [Test]
        public void ApplySnapshot_HostTeleportedMyCharacter_DrawnThereWithoutSliding()
        {
            var host = HostWithHuman();
            var guest = new Game();
            var me = guest.ApplySnapshot(host.WriteSnapshot(), "h1", null);
            me.KeepPrevious(); me.x += 40;
            host.StartRound(new List<Game.Human>());   // a new round moves everyone (teleportSeq changes)

            guest.ApplySnapshot(host.WriteSnapshot(), "h1", me);

            Assert.That(me.prevX, Is.EqualTo(me.x));
        }
    }
}
