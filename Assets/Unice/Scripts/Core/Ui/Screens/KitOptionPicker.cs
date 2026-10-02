using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// The look options, in tabs: color swatches (skin, hair, outfit) and ‹ › cycles (hair style, outfit, hat, accessory).
    /// Every change goes through the callback given to the constructor.
    /// </summary>
    sealed class KitOptionPicker : UiPart
    {
        /// <summary>One option row: which kit slot it edits, its choices, whether they are colors (swatches) or names (‹ ›), and its tab.</summary>
        readonly struct OptionRow
        {
            public readonly string labelKey, tabKey; public readonly int slot; public readonly string[] choices; public readonly bool isColor;
            public OptionRow(string labelKey, int slot, string[] choices, bool isColor, string tabKey)
            { this.labelKey = labelKey; this.slot = slot; this.choices = choices; this.isColor = isColor; this.tabKey = tabKey; }
        }

        static readonly OptionRow[] Rows =
        {
            new OptionRow("opt.skin", Kit.SkinSlot, Kit.Skins, true, "tab.color"),
            new OptionRow("opt.haircolor", Kit.HairColorSlot, Kit.HairColors, true, "tab.color"),
            new OptionRow("opt.outfitcolor", Kit.OutfitColorSlot, Kit.OutfitColors, true, "tab.color"),
            new OptionRow("opt.hair", Kit.HairSlot, Kit.Hairs, false, "tab.hair"),
            new OptionRow("opt.outfit", Kit.OutfitSlot, Kit.Outfits, false, "tab.outfit"),
            new OptionRow("opt.hat", Kit.HatSlot, Kit.Hats, false, "tab.hat"),
            new OptionRow("opt.accessory", Kit.AccessorySlot, Kit.Accessories, false, "tab.accessory"),
        };
        static readonly string[] Tabs = { "tab.color", "tab.hair", "tab.outfit", "tab.hat", "tab.accessory" };

        readonly Action<int[]> setKit;
        readonly List<(int slot, int choice, VisualElement element)> swatches = new List<(int, int, VisualElement)>();
        readonly Dictionary<int, Label> cycleValueLabels = new Dictionary<int, Label>();
        readonly List<(string tab, VisualElement element)> rowElements = new List<(string, VisualElement)>();
        readonly Dictionary<string, VisualElement> tabButtons = new Dictionary<string, VisualElement>();
        string currentTab = Tabs[0];

        public KitOptionPicker(Ui ui, VisualElement parent, Action<int[]> setKit) : base(ui)
        {
            this.setKit = setKit;
            var tabBar = AddElement(parent, "tabbar");
            foreach (var tab in Tabs)
            {
                var button = AddButton(tabBar, "", () => { currentTab = tab; Render(); }, "tabbtn");
                ui.relocalizers.Add(() => button.text = Localization.Get(tab).ToUpperInvariant());
                tabButtons[tab] = button;
            }

            var rows = AddElement(parent);
            foreach (var option in Rows)
            {
                var row = AddElement(rows, "opt");
                LocalizedLabel(row, option.labelKey, "opt-l");
                if (option.isColor) AddSwatches(row, option);
                else AddCycle(row, option.slot);
                rowElements.Add((option.tabKey, row));
            }
            Render();
        }

        void AddSwatches(VisualElement row, OptionRow option)
        {
            var swatchRow = AddElement(row, "sws");
            for (int choice = 0; choice < option.choices.Length; choice++)
            {
                int chosen = choice;
                var swatch = AddButton(swatchRow, "", () => SetSlot(option.slot, chosen), "sw");
                swatch.style.backgroundColor = Art.HexColor(option.choices[choice]);
                swatches.Add((option.slot, choice, swatch));
            }
        }

        void AddCycle(VisualElement row, int slot)
        {
            var cycle = AddElement(row, "cy");
            int count = Kit.OptionCount[slot];
            AddButton(cycle, "‹", () => SetSlot(slot, (app.kit[slot] - 1 + count) % count), "cy-btn");
            cycleValueLabels[slot] = AddLabel(cycle, "", "cy-val");
            AddButton(cycle, "›", () => SetSlot(slot, (app.kit[slot] + 1) % count), "cy-btn");
        }

        void SetSlot(int slot, int choice)
        {
            var kit = (int[])app.kit.Clone();
            kit[slot] = choice;
            setKit(kit);
        }

        /// <summary>Highlights the current choices and shows the rows of the current tab.</summary>
        public void Render()
        {
            foreach (var (slot, choice, element) in swatches) element.EnableInClassList("sw-on", app.kit[slot] == choice);
            foreach (var (slot, label) in cycleValueLabels) label.text = Localization.KitOptionName(slot, app.kit[slot]);
            foreach (var (tab, element) in rowElements) Show(element, tab == currentTab);
            foreach (var (tab, button) in tabButtons) button.EnableInClassList("tabbtn-on", tab == currentTab);
        }
    }
}
