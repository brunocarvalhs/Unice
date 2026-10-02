using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>Name, preview and look options. One instance, moved between "Minha criança" and the waiting room.</summary>
    sealed class KidCustomizer : UiPart
    {
        public readonly VisualElement Root;
        readonly Doll preview;

        public KidCustomizer(Ui ui) : base(ui)
        {
            var c = Root = new VisualElement();
            LL(c, "lbl.name", "lbl");
            var nameIn = Field(c, app.myName, 12);
            nameIn.RegisterValueChangedCallback(e => { app.myName = U.CleanName(e.newValue); app.SaveProfile(); });
            LL(c, "lbl.kid", "lbl").style.marginTop = 10;
            var pr = E(c, "row");
            var pv = E(pr, "preview"); pv.style.alignItems = Align.Center; pv.style.justifyContent = Justify.Center;
            preview = new Doll(100); pv.Add(preview);
            BB(pr, "menu.randomize", () => { app.SetKit(Kit.Random()); Render(); ui.KitChanged(); }, "btn", "btn-ghost");
            var opts = E(c); opts.style.marginTop = 10;
            BuildOpts(opts);
        }

        /// <summary>Moves the customizer under <paramref name="parent"/> (if it isn't there already) and refreshes it.</summary>
        public void ShowIn(VisualElement parent) { if (Root.parent != parent) parent.Add(Root); Render(); }

        static readonly Vector2[] PrevDirs = { new Vector2(.01f, 1), new Vector2(1, .2f), new Vector2(.01f, -1), new Vector2(-1, .2f) };
        /// <summary>Per frame while visible: the preview walks around, facing each direction in turn.</summary>
        public void AnimatePreview()
        {
            float t = Time.unscaledTime; var d = PrevDirs[(int)(t / 1.4f) % 4];
            preview.Set(app.kit, d.y < -.6f, 1 + Mathf.FloorToInt(t * 13 / (Mathf.PI * 2) * 6) % 6, d.x < -.3f);
        }

        static readonly (string labelKey, int slot, string[] list, bool color, string tabKey)[] OptRows =
        {
            ("opt.skin", 0, Kit.Skins, true, "tab.color"), ("opt.haircolor", 2, Kit.HairColors, true, "tab.color"), ("opt.outfitcolor", 4, Kit.OutfitColors, true, "tab.color"),
            ("opt.hair", 1, Kit.Hairs, false, "tab.hair"), ("opt.outfit", 3, Kit.Outfits, false, "tab.outfit"),
            ("opt.hat", 5, Kit.Hats, false, "tab.hat"), ("opt.accessory", 6, Kit.Accessories, false, "tab.accessory")
        };
        static readonly string[] OptTabs = { "tab.color", "tab.hair", "tab.outfit", "tab.hat", "tab.accessory" };
        readonly List<(int slot, int v, VisualElement el)> swatches = new List<(int, int, VisualElement)>();
        readonly Dictionary<int, Label> cycleVals = new Dictionary<int, Label>();
        readonly List<(string tab, VisualElement el)> optRowEls = new List<(string, VisualElement)>();
        readonly Dictionary<string, VisualElement> optTabBtns = new Dictionary<string, VisualElement>();
        string optTab = OptTabs[0];

        void BuildOpts(VisualElement opts)
        {
            var tabBar = E(opts, "tabbar");
            foreach (var tk in OptTabs)
            {
                var btn = B(tabBar, "", () => { optTab = tk; Render(); }, "tabbtn");
                ui.relocalize.Add(() => btn.text = Loc.T(tk).ToUpperInvariant());
                optTabBtns[tk] = btn;
            }
            var rows = E(opts);
            foreach (var (labelKey, slot, list, color, tab) in OptRows)
            {
                var row = E(rows, "opt");
                LL(row, labelKey, "opt-l");
                if (color)
                {
                    var sws = E(row, "sws");
                    for (int j = 0; j < list.Length; j++)
                    {
                        int v = j;
                        var b = B(sws, "", () => { var k = (int[])app.kit.Clone(); k[slot] = v; app.SetKit(k); Render(); ui.KitChanged(); }, "sw");
                        b.style.backgroundColor = Art.Hex(list[j]);
                        swatches.Add((slot, j, b));
                    }
                }
                else
                {
                    var cy = E(row, "cy");
                    void Cycle(int d) { var k = (int[])app.kit.Clone(); k[slot] = (k[slot] + d + Kit.Max[slot]) % Kit.Max[slot]; app.SetKit(k); Render(); ui.KitChanged(); }
                    B(cy, "‹", () => Cycle(-1), "cy-btn");
                    cycleVals[slot] = L(cy, "", "cy-val");
                    B(cy, "›", () => Cycle(1), "cy-btn");
                }
                optRowEls.Add((tab, row));
            }
            Render();
        }

        public void Render()
        {
            foreach (var (slot, v, el) in swatches) el.EnableInClassList("sw-on", app.kit[slot] == v);
            foreach (var (labelKey, slot, list, color, tab) in OptRows) if (!color) cycleVals[slot].text = Loc.KitOpt(slot, app.kit[slot]);
            foreach (var (tab, el) in optRowEls) Show(el, tab == optTab);
            foreach (var kv in optTabBtns) kv.Value.EnableInClassList("tabbtn-on", kv.Key == optTab);
        }
    }
}
