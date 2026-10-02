using System.Collections.Generic;
using UnityEngine;

namespace Unice
{
    /// <summary>What made a ripple: each kind has its color, size and duration.</summary>
    public enum RippleKind
    {
        /// <summary>The doll's giggle (or a laugh): big pink ring.</summary>
        Giggle,
        /// <summary>A catch: short red ring.</summary>
        Catch,
        /// <summary>Running footsteps the Unice hears.</summary>
        Step,
        /// <summary>The player's own running steps (faint).</summary>
        Own,
    }

    /// <summary>Expanding rings on the floor that show where a sound came from. Sprites are pooled and reused.</summary>
    sealed class RippleEffects
    {
        /// <summary>The ring sprite is 256 px wide at 1 px per house pixel.</summary>
        const float RingSpritePixels = 256;
        const float StartRadius = 8;

        struct Ripple { public SpriteRenderer renderer; public float age, lifetime, growth; public Color color; }

        readonly Transform parent;
        readonly List<Ripple> active = new List<Ripple>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();

        public RippleEffects(Transform parent) { this.parent = parent; }

        public void Show(float x, float y, RippleKind kind)
        {
            var renderer = pool.Count > 0 ? pool.Pop() : Art.CreateSpriteRenderer("ripple", parent, Art.Centered("ring", 1), 31000);
            renderer.gameObject.SetActive(true);
            renderer.transform.localPosition = Art.ToWorld(x, y + (kind == RippleKind.Giggle ? 0 : 6));
            Color color; float growth, lifetime;
            switch (kind)
            {
                case RippleKind.Giggle: color = Art.HexColor("#ef7394"); growth = 240; lifetime = 1.8f; break;
                case RippleKind.Catch: color = Art.HexColor("#e24a52"); growth = 90; lifetime = .5f; break;
                case RippleKind.Step: color = Art.HexColor("#f0b057"); growth = 46; lifetime = .8f; break;
                default: color = Art.HexColor("#f0b057"); color.a = .45f; growth = 34; lifetime = .8f; break;
            }
            active.Add(new Ripple { renderer = renderer, lifetime = lifetime, growth = growth, color = color });
        }

        /// <summary>Every frame: rings grow and fade; finished ones go back to the pool.</summary>
        public void Tick(float dt)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var ripple = active[i]; ripple.age += dt;
                if (ripple.age >= ripple.lifetime)
                {
                    ripple.renderer.gameObject.SetActive(false); pool.Push(ripple.renderer); active.RemoveAt(i);
                    continue;
                }
                float progress = ripple.age / ripple.lifetime, radius = StartRadius + progress * ripple.growth;
                ripple.renderer.transform.localScale = Vector3.one * (radius * 2 / RingSpritePixels);
                ripple.renderer.color = new Color(ripple.color.r, ripple.color.g, ripple.color.b, ripple.color.a * (1 - progress));
                active[i] = ripple;
            }
        }
    }
}
