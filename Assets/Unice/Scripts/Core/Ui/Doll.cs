using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unice
{
    /// <summary>A child's look for menus and lists: the same tinted layers as in the game.</summary>
    public sealed class Doll : VisualElement
    {
        readonly VisualElement[] ls = new VisualElement[14];
        readonly (string file, Color tint)[] slots = new (string, Color)[14];

        public Doll(float width)
        {
            pickingMode = PickingMode.Ignore;
            style.width = width; style.height = width * 68 / 52; style.flexShrink = 0;
            for (int i = 0; i < ls.Length; i++)
            {
                var l = new VisualElement { pickingMode = PickingMode.Ignore };
                l.style.position = Position.Absolute; l.style.left = 0; l.style.top = 0; l.style.right = 0; l.style.bottom = 0;
                ls[i] = l; Add(l);
            }
        }

        public void Set(int[] k, bool back = false, int frame = 0, bool flip = false)
        {
            EntView.KidLayers(k, back, frame, slots);
            for (int i = 0; i < ls.Length; i++)
            {
                var (file, tint) = slots[i];
                var sp = file != null ? Art.Char(file) : null;
                ls[i].style.display = sp != null ? DisplayStyle.Flex : DisplayStyle.None;
                if (sp == null) continue;
                ls[i].style.backgroundImage = new StyleBackground(sp);
                ls[i].style.unityBackgroundImageTintColor = tint;
            }
            style.scale = new Scale(new Vector3(flip ? -1 : 1, 1, 1));
        }

        public void SetUnice()
        {
            for (int i = 1; i < ls.Length; i++) ls[i].style.display = DisplayStyle.None;
            ls[0].style.display = DisplayStyle.Flex;
            ls[0].style.backgroundImage = new StyleBackground(Art.UniceFrame("unice_i_s_0"));
            ls[0].style.unityBackgroundImageTintColor = Color.white;
        }
    }
}
