using System.Collections.Generic;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// The world on screen: the map's scenery, the characters, the ripples and the fog of war, all made of sprites.
    /// Everything that stands up is sorted by the y of its base, so you walk in front of or behind things like in the web version.
    /// </summary>
    public sealed class WorldView
    {
        readonly Transform root;
        readonly MapScenery scenery;
        readonly RippleEffects ripples;
        readonly List<CharacterView> characterViews = new List<CharacterView>();
        public readonly FogView fogView;

        /// <summary>Alpha each character was drawn with this frame (0 = not shown), for the name tags.</summary>
        public readonly List<float> CharacterAlphas = new List<float>();

        public WorldView(Transform parent)
        {
            Art.Load();
            root = new GameObject("House").transform; root.SetParent(parent, false);
            scenery = new MapScenery(root);
            ripples = new RippleEffects(root);
            fogView = new FogView(parent);
        }

        /// <summary>Every step: rebuilds the scenery when the active map changed.</summary>
        public void SyncMap() => scenery.Sync();

        /// <summary>Walls and furniture you walk behind turn see-through, so you never lose your own character.</summary>
        public void UpdateOcclusion(Character me, bool live) => scenery.UpdateOcclusion(me, live);

        /// <summary>
        /// Every frame: draws each character this player can see. Hidden characters only show to the team (faded) and to
        /// their own player; <paramref name="canSee"/> is the line-of-sight test from the player's point of view.
        /// </summary>
        public void SyncCharacters(Game game, Character me, System.Func<float, float, bool> canSee, bool showsTeamInfo, float time)
        {
            while (characterViews.Count < game.characters.Count) characterViews.Add(new CharacterView(root, characterViews.Count));
            CharacterAlphas.Clear();
            bool roundOnScreen = game.IsLive || game.phase == Phase.Result;
            for (int i = 0; i < characterViews.Count; i++)
            {
                var view = characterViews[i];
                if (i >= game.characters.Count || !roundOnScreen)
                {
                    view.Hide();
                    if (i < game.characters.Count) CharacterAlphas.Add(0);
                    continue;
                }
                var character = game.characters[i];
                float alpha = 1;
                bool visible = !character.outOfRound && character.x > 0 && (character == me || canSee(character.x, character.y));
                if (visible && character.hidingSpot != null)
                {
                    if (character == me) alpha = .45f;
                    else if (showsTeamInfo && !character.IsUnice) alpha = .3f;
                    else visible = false;
                }
                if (!visible) { view.Hide(); CharacterAlphas.Add(0); continue; }
                bool showDoll = showsTeamInfo && character == game.doll.holder && !character.IsUnice;
                view.Show(character, character == me, alpha, showDoll, time);
                CharacterAlphas.Add(alpha);
            }
        }

        public void ShowRipple(float x, float y, RippleKind kind) => ripples.Show(x, y, kind);

        /// <summary>Punches the given character's sprite (scale pop + white flash) — the catch moment, without new art.</summary>
        public void FlashHit(int characterIndex) { if (characterIndex >= 0 && characterIndex < characterViews.Count) characterViews[characterIndex].Hit(); }

        /// <summary>Every frame: animates the ripples.</summary>
        public void Tick(float dt) => ripples.Tick(dt);
    }
}
