using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>Text attached to the world: name tags over the characters, and short floating texts ("PEGO!", "hihi").</summary>
    sealed class WorldLabels : UiPart
    {
        readonly VisualElement layer;
        readonly List<Label> nameTags = new List<Label>();

        struct FloatingText { public float x, y, size, lifetime, age; public bool alwaysVisible; public Label label; }
        readonly List<FloatingText> floatingTexts = new List<FloatingText>();

        public WorldLabels(Ui ui) : base(ui)
        {
            layer = IgnorePointer(AddElement(ui.root, "layer"));
        }

        /// <summary>
        /// A text at (x, y) in house pixels that rises and fades over <paramref name="lifetime"/> seconds. Unless
        /// <paramref name="alwaysVisible"/>, it only shows when the player can see that spot.
        /// </summary>
        public void ShowWorldText(float x, float y, string text, Color color, float size, float lifetime, bool alwaysVisible = false)
        {
            var label = (Label)IgnorePointer(AddLabel(layer, text, "fxtext")); label.style.color = color;
            floatingTexts.Add(new FloatingText { x = x, y = y, size = size, lifetime = lifetime, alwaysVisible = alwaysVisible, label = label });
        }

        public void Frame(float dt, Camera camera)
        {
            float panelHeight = ui.root.layout.height;
            float panelPixelsPerHousePixel = panelHeight / (camera.orthographicSize * 2 * Art.PxPerUnit);
            var me = app.myCharacter; bool inGame = game.IsLive || game.phase == Phase.Result;

            // name tags, faded with the character itself
            var alphas = app.worldView.CharacterAlphas;
            for (int i = 0; i < game.characters.Count || i < nameTags.Count; i++)
            {
                if (i >= nameTags.Count) nameTags.Add((Label)IgnorePointer(AddLabel(layer, "", "wlabel")));
                var tag = nameTags[i];
                float alpha = inGame && i < game.characters.Count && i < alphas.Count ? alphas[i] : 0;
                if (alpha <= 0) { Show(tag, false); continue; }
                var character = game.characters[i]; bool isUnice = character.IsUnice;
                Show(tag, true);
                SetText(tag, isUnice ? (character == me ? Localization.Get("tag.unice_you") : Localization.Get("tag.unice_named", character.name)) : character.name);
                tag.style.color = isUnice ? Ui.Rose : character == me ? Ui.Candle : Ui.Ink; tag.style.opacity = alpha;
                tag.style.fontSize = 12 * panelPixelsPerHousePixel;
                var position = WorldToPanel(camera, character.DrawX(app.stepBlend), character.DrawY(app.stepBlend) + (isUnice ? -66 : -54));
                tag.style.left = position.x; tag.style.top = position.y;
            }

            // floating texts: pop in, rise, fade out
            for (int i = floatingTexts.Count - 1; i >= 0; i--)
            {
                var text = floatingTexts[i]; text.age += dt;
                if (text.age >= text.lifetime) { text.label.RemoveFromHierarchy(); floatingTexts.RemoveAt(i); continue; }
                floatingTexts[i] = text;
                bool visible = inGame && (text.alwaysVisible || app.perspective.CanSee(text.x, text.y + 24));
                Show(text.label, visible); if (!visible) continue;
                float progress = text.age / text.lifetime;
                float scale = progress < .15f ? .6f + progress / .15f * .5f : 1.1f - .1f * Mathf.Min(1, (progress - .15f) / .2f);
                var position = WorldToPanel(camera, text.x, text.y - progress * 24);
                text.label.style.left = position.x; text.label.style.top = position.y;
                text.label.style.fontSize = text.size * panelPixelsPerHousePixel; text.label.style.scale = new Scale(new Vector3(scale, scale, 1));
                text.label.style.opacity = 1 - Mathf.Max(0, (progress - .6f) / .4f);
            }
        }

        Vector2 WorldToPanel(Camera camera, float x, float y) => RuntimePanelUtils.CameraTransformWorldToPanel(ui.root.panel, Art.ToWorld(x, y), camera);
    }
}
