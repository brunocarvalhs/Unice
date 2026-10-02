using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>The main menu over the house map: main page, online (create/join a room), how to play, my child.</summary>
    sealed class HomeMenu : UiPart
    {
        public readonly VisualElement Root;
        readonly VisualElement home, menuMain, menuOnline, menuHelp, menuKid, kidSlot; readonly TextField codeIn; readonly Label netLbl;
        readonly Button createBtn, joinBtn, localBtn, onlineBtn; string page = "main";

        public HomeMenu(Ui ui) : base(ui)
        {
            Root = home = E(ui.root, "menu");
            L(home, "v" + Application.version, "menu-version");

            menuMain = E(home, "menu-page");
            L(menuMain, "Unice", "menu-title");
            LL(menuMain, "menu.sub", "menu-sub");
            var grid = E(menuMain, "menu-grid");
            localBtn = BB(grid, "menu.local", () => app.PlaySolo(), "mbtn");
            onlineBtn = BB(grid, "menu.online", () => { app.SetNet(""); Page("online"); }, "mbtn");
            BB(grid, "menu.howto", () => Page("help"), "mbtn", "mbtn-sm");
            BB(grid, "menu.mykid", () => Page("kid"), "mbtn", "mbtn-sm");

            // online: create a room (the code is generated for you) or join one with a code
            var on = Panel(out menuOnline, "menu.online_title");
            LL(on, "menu.online_desc", "mnote");
            createBtn = BB(on, "menu.create_room", () => app.EnterRoom(true, null), "mbtn", "mbtn-wide");
            LL(on, "menu.or_join", "menu-or");
            var jr = E(on, "row");
            codeIn = new TextField { maxLength = 10 }; codeIn.AddToClassList("mfield"); codeIn.AddToClassList("grow"); jr.Add(codeIn);
            codeIn.RegisterValueChangedCallback(e => { var c = U.CleanCode(e.newValue); if (c != e.newValue) codeIn.SetValueWithoutNotify(c); });   // codes are A-Z/0-9: show them that way while typing
            joinBtn = BB(jr, "menu.join", JoinFromInput, "mbtn", "mbtn-join");
            netLbl = L(on, "", "mnote", "mnet");
            BB(on, "menu.back", () => Page("main"), "mbtn", "mbtn-back");

            // how to play
            var hp = Panel(out menuHelp, "menu.howto_title");
            var sc = new ScrollView(ScrollViewMode.Vertical); sc.AddToClassList("mscroll"); hp.Add(sc);
            void Rule(string tKey, string bKey, string cls) { var x = E(sc, "mrule"); var tl = LL(x, tKey, "mrule-t"); if (cls != null) tl.AddToClassList(cls); LL(x, bKey, "mnote"); }
            Rule("rule.unice.t", "rule.unice.b", "mrule-rose");
            Rule("rule.teamwin.t", "rule.teamwin.b", "mrule-moss");
            Rule("rule.unicewin.t", "rule.unicewin.b", "mrule-rose");
            Rule("rule.hidetime.t", "rule.hidetime.b", null);
            Rule("rule.dark.t", "rule.dark.b", null);
            Rule("rule.gigglerule.t", "rule.gigglerule.b", "mrule-rose");
            Rule("rule.laugh.t", "rule.laugh.b", "mrule-rose");
            Rule("rule.rotation.t", "rule.rotation.b", null);
            Rule("rule.controls.t", "rule.controls.b", null);
            BB(hp, "menu.back", () => Page("main"), "mbtn", "mbtn-back");

            // my child: the same customization as in the waiting room
            var kp = Panel(out menuKid, "menu.mykid_title");
            var ks = new ScrollView(ScrollViewMode.Vertical); ks.AddToClassList("mscroll"); kp.Add(ks);
            kidSlot = E(ks, "mkid");
            BB(kp, "menu.ready", () => Page("main"), "mbtn", "mbtn-back");
            
            var foot = E(home, "menu-foot");
            ui.SoundBtn(foot, "mpill"); ui.MicBtn(foot, "mpill"); ui.WhisperBtn(foot, "mpill"); ui.LangBtn(foot, "mpill");
            BB(home, "menu.quit", () => Application.Quit(), "mbtn", "mexit");
            Page("main");
        }

        /// <summary>Each time home appears: back to the main page, or to "online" if there's a connection error to read.</summary>
        public void OnShow() => Page(app.netErr && app.netText.Length > 0 ? "online" : "main");

        /// <summary>Per step: buttons follow the connection state.</summary>
        public void Sync()
        {
            bool ok = app.NetAvailable && !app.busy;
            createBtn.SetEnabled(ok); joinBtn.SetEnabled(ok); localBtn.SetEnabled(!app.busy); onlineBtn.SetEnabled(!app.busy);
            Txt(netLbl, app.netText.Length > 0 ? app.netText : Loc.T(app.NetAvailable ? "menu.net_ready" : "menu.net_unavailable"));
            netLbl.EnableInClassList("net-err", app.netErr && app.netText.Length > 0);
        }

        public void Frame() { if (page == "kid") ui.kid.AnimatePreview(); }

        VisualElement Panel(out VisualElement page, string titleKey)
        {
            page = E(home, "menu-page");
            var p = E(page, "mpanel");
            LL(p, titleKey, "mpanel-t");
            return p;
        }

        void Page(string p)
        {
            page = p;
            Show(menuMain, p == "main"); Show(menuOnline, p == "online"); Show(menuHelp, p == "help"); Show(menuKid, p == "kid");
            if (p == "kid") ui.kid.ShowIn(kidSlot);
        }

        /// <summary>Back button on the menu: sub-pages go back to the main page. False when already there.</summary>
        public bool Back() { if (ui.screen != "home" || page == "main") return false; Page("main"); return true; }

        void JoinFromInput()
        {
            var c = U.CleanCode(codeIn.value);
            if (c.Length < 4) { app.SetNet(Loc.T("menu.need_code"), true); return; }
            app.EnterRoom(false, c);
        }

        /// <summary>Mobile: the on-screen keyboard covers the lower half of the menu, so slide it up until the field being typed in is visible.</summary>
        public void LiftForKeyboard()
        {
            float lift = 0;
            if (ui.Typing && TouchScreenKeyboard.visible && ui.root.panel.focusController.focusedElement is VisualElement f && Screen.height > 0)
            {
                float k = ui.root.layout.height / Screen.height, kb = TouchScreenKeyboard.area.height;
                if (kb < 1) kb = Screen.height * .5f;   // some Android keyboards don't report their size
                float kbTop = ui.root.layout.height - kb * k - 16, fieldBottom = f.worldBound.yMax - home.resolvedStyle.translate.y;
                lift = Mathf.Max(0, fieldBottom - kbTop);
            }
            if (home.resolvedStyle.translate.y != -lift) home.style.translate = new Translate(0, -lift);
        }
    }
}
