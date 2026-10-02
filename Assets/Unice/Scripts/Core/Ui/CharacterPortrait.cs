using UnityEngine;
using UnityEngine.UIElements;

namespace Unice
{
    /// <summary>A child's look for menus and lists, drawn with the same tinted sprite layers as in the game.</summary>
    public sealed class CharacterPortrait : VisualElement
    {
        /// <summary>Character sprites are 52×68.</summary>
        const float AspectRatio = 68f / 52f;

        readonly VisualElement[] layers;
        readonly (string file, Color tint)[] layerSprites;

        public CharacterPortrait(float width)
        {
            int layerCount = KidSprites.LayerOrder.Length;
            layers = new VisualElement[layerCount];
            layerSprites = new (string, Color)[layerCount];
            pickingMode = PickingMode.Ignore;
            style.width = width; style.height = width * AspectRatio; style.flexShrink = 0;
            for (int i = 0; i < layerCount; i++)
            {
                var layer = new VisualElement { pickingMode = PickingMode.Ignore };
                layer.style.position = Position.Absolute; layer.style.left = 0; layer.style.top = 0; layer.style.right = 0; layer.style.bottom = 0;
                layers[i] = layer; Add(layer);
            }
        }

        /// <param name="showBack">Seen from behind (walking up).</param>
        /// <param name="frame">Walk animation frame (0 = standing).</param>
        /// <param name="flip">Mirrored (facing left).</param>
        public void ShowKit(int[] kit, bool showBack = false, int frame = 0, bool flip = false)
        {
            KidSprites.Build(kit, showBack, frame, layerSprites);
            for (int i = 0; i < layers.Length; i++)
            {
                var (file, tint) = layerSprites[i];
                var sprite = file != null ? Art.CharacterSprite(file) : null;
                layers[i].style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
                if (sprite == null) continue;
                layers[i].style.backgroundImage = new StyleBackground(sprite);
                layers[i].style.unityBackgroundImageTintColor = tint;
            }
            style.scale = new Scale(new Vector3(flip ? -1 : 1, 1, 1));
        }

        /// <summary>Shows the Unice (one sprite) instead of a child.</summary>
        public void SetUnice()
        {
            for (int i = 1; i < layers.Length; i++) layers[i].style.display = DisplayStyle.None;
            layers[0].style.display = DisplayStyle.Flex;
            layers[0].style.backgroundImage = new StyleBackground(Art.UniceFrame("unice_i_s_0"));
            layers[0].style.unityBackgroundImageTintColor = Color.white;
        }
    }
}
