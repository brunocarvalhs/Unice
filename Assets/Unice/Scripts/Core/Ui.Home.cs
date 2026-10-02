using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unice
{
    public sealed partial class Ui
    {
        // ---------- main menu (over the house map) ----------
        VisualElement home, menuMain, menuOnline, menuHelp, menuKid, kidSlot, custom; TextField nameIn, codeIn; Label netLbl;
        Button createBtn, joinBtn, soundBtn, micBtn, whisperBtn, langBtn, localBtn, onlineBtn; string page = "main";

        void BuildHome()
        {
            home = E(root, "menu");
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
            custom = BuildCustom();

            var foot = E(home, "menu-foot");
            soundBtn = B(foot, "", ToggleSound, "mpill");
            micBtn = B(foot, "", ToggleMic, "mpill");
            whisperBtn = B(foot, "", ToggleWhisper, "mpill");
            langBtn = B(foot, "", CycleLang, "mpill");
            BB(home, "menu.quit", () => Application.Quit(), "mbtn", "mexit");
            Page("main");
        }

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
            if (p == "kid") { kidSlot.Add(custom); RenderOpts(); }
        }

        /// <summary>Back button on the menu: sub-pages go back to the main page. False when already there.</summary>
        public bool MenuBack() { if (screen != "home" || page == "main") return false; Page("main"); return true; }

        /// <summary>Name, preview and look options. Lives in the waiting room or in "Minha criança".</summary>
        VisualElement BuildCustom()
        {
            var c = new VisualElement();
            LL(c, "lbl.name", "lbl");
            nameIn = Field(c, app.myName, 12);
            nameIn.RegisterValueChangedCallback(e => { app.myName = U.CleanName(e.newValue); app.SaveProfile(); });
            LL(c, "lbl.kid", "lbl").style.marginTop = 10;
            var pr = E(c, "row");
            var pv = E(pr, "preview"); pv.style.alignItems = Align.Center; pv.style.justifyContent = Justify.Center;
            preview = new Doll(100); pv.Add(preview);
            BB(pr, "menu.randomize", () => { app.SetKit(Kit.Random()); RenderOpts(); wSig = null; }, "btn", "btn-ghost");
            opts = E(c); opts.style.marginTop = 10;
            BuildOpts();
            return c;
        }

        void JoinFromInput()
        {
            var c = U.CleanCode(codeIn.value);
            if (c.Length < 4) { app.SetNet(Loc.T("menu.need_code"), true); return; }
            app.EnterRoom(false, c);
        }

        void ToggleSound() { app.sfx.On = !app.sfx.On; RefreshToggleBtns(); }

        /// <summary>Personal choice: when on, the room hears you through proximity voice chat as long as you're not hidden.</summary>
        void ToggleMic() { app.MicOn = !app.MicOn; RefreshToggleBtns(); }

        /// <summary>Personal choice: "whisper" transmits quieter and never counts as noise for the Unice. Needs the mic on.</summary>
        void ToggleWhisper() { app.Whisper = !app.Whisper; RefreshToggleBtns(); }

        void CycleLang() { Loc.Set((Lang)(((int)Loc.Current + 1) % 3)); Relocalize(); }

        /// <summary>Re-applies every toggle/language button's text, in every screen it appears, from current state.</summary>
        void RefreshToggleBtns()
        {
            string snd = Loc.T(app.sfx.On ? "menu.sound_on" : "menu.sound_off");
            soundBtn.text = snd; pauseSound.text = snd;
            string mic = Loc.T(app.MicOn ? "menu.mic_on" : "menu.mic_off");
            micBtn.text = mic; pauseMic.text = mic; waitMic.text = mic;
            string t = Loc.T(app.Whisper ? "menu.voice_whisper" : "menu.voice_normal");
            whisperBtn.text = t; whisperBtn.SetEnabled(app.MicOn);
            waitWhisper.text = t; waitWhisper.SetEnabled(app.MicOn);
            pauseWhisper.text = t; pauseWhisper.SetEnabled(app.MicOn);
            langBtn.text = $"{Loc.T("menu.lang_prefix")}: {Loc.Current.ToString().ToUpperInvariant()}";
        }
    }
}
