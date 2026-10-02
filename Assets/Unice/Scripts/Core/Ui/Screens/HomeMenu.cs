using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>The main menu over the house map: the main page and its sub-pages (online, how to play, my child).</summary>
    sealed class HomeMenu : UiPart
    {
        enum Page { Main, Online, HowToPlay, MyKid }

        /// <summary>A typed room code needs at least this many characters.</summary>
        const int MinRoomCodeLength = 4;

        public readonly VisualElement Root;
        readonly VisualElement mainPage, onlinePage, myKidPage, kidCustomizerSlot;
        VisualElement howToPlayPage;   // set by BuildHowToPlay
        readonly TextField roomCodeField;
        readonly Label netStatusLabel;
        readonly Button createRoomButton, joinRoomButton, soloButton, onlineButton;
        Page currentPage = Page.Main;

        public HomeMenu(Ui ui) : base(ui)
        {
            Root = AddElement(ui.root, "menu");
            AddLabel(Root, "v" + Application.version, "menu-version");

            // main page
            mainPage = AddElement(Root, "menu-page");
            AddLabel(mainPage, "Unice", "menu-title");
            LocalizedLabel(mainPage, "menu.sub", "menu-sub");
            var grid = AddElement(mainPage, "menu-grid");
            soloButton = LocalizedButton(grid, "menu.local", () => app.PlaySolo(), "mbtn");
            onlineButton = LocalizedButton(grid, "menu.online", () => { app.SetNetStatus(""); ShowPage(Page.Online); }, "mbtn");
            LocalizedButton(grid, "menu.howto", () => ShowPage(Page.HowToPlay), "mbtn", "mbtn-sm");
            LocalizedButton(grid, "menu.mykid", () => ShowPage(Page.MyKid), "mbtn", "mbtn-sm");

            // online: create a room (the code is generated for you) or join one with a code
            var online = AddPanelPage(out onlinePage, "menu.online_title");
            LocalizedLabel(online, "menu.online_desc", "mnote");
            createRoomButton = LocalizedButton(online, "menu.create_room", () => app.EnterRoom(true, null), "mbtn", "mbtn-wide");
            LocalizedLabel(online, "menu.or_join", "menu-or");
            var joinRow = AddElement(online, "row");
            roomCodeField = Add(joinRow, new TextField { maxLength = 10 }, new[] { "mfield", "grow" });
            // codes are A-Z/0-9: show them that way while typing
            roomCodeField.RegisterValueChangedCallback(e =>
            {
                var clean = TextFormat.CleanCode(e.newValue);
                if (clean != e.newValue) roomCodeField.SetValueWithoutNotify(clean);
            });
            joinRoomButton = LocalizedButton(joinRow, "menu.join", JoinTypedRoom, "mbtn", "mbtn-join");
            netStatusLabel = AddLabel(online, "", "mnote", "mnet");
            LocalizedButton(online, "menu.back", () => ShowPage(Page.Main), "mbtn", "mbtn-back");

            BuildHowToPlay();

            // my child: the same customization as in the waiting room
            var myKid = AddPanelPage(out myKidPage, "menu.mykid_title");
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("mscroll"); myKid.Add(scroll);
            kidCustomizerSlot = AddElement(scroll, "mkid");
            LocalizedButton(myKid, "menu.ready", () => ShowPage(Page.Main), "mbtn", "mbtn-back");

            var footer = AddElement(Root, "menu-foot");
            ui.settings.AddSoundButton(footer, "mpill"); ui.settings.AddMicButton(footer, "mpill");
            ui.settings.AddWhisperButton(footer, "mpill"); ui.settings.AddLanguageButton(footer, "mpill");
            LocalizedButton(Root, "menu.quit", () => Application.Quit(), "mbtn", "mexit");
            ShowPage(Page.Main);
        }

        void BuildHowToPlay()
        {
            var panel = AddPanelPage(out howToPlayPage, "menu.howto_title");
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("mscroll"); panel.Add(scroll);
            void Rule(string titleKey, string bodyKey, string titleClass)
            {
                var rule = AddElement(scroll, "mrule");
                var title = LocalizedLabel(rule, titleKey, "mrule-t");
                if (titleClass != null) title.AddToClassList(titleClass);
                LocalizedLabel(rule, bodyKey, "mnote");
            }
            Rule("rule.unice.t", "rule.unice.b", "mrule-rose");
            Rule("rule.teamwin.t", "rule.teamwin.b", "mrule-moss");
            Rule("rule.unicewin.t", "rule.unicewin.b", "mrule-rose");
            Rule("rule.hidetime.t", "rule.hidetime.b", null);
            Rule("rule.dark.t", "rule.dark.b", null);
            Rule("rule.gigglerule.t", "rule.gigglerule.b", "mrule-rose");
            Rule("rule.laugh.t", "rule.laugh.b", "mrule-rose");
            Rule("rule.rotation.t", "rule.rotation.b", null);
            Rule("rule.controls.t", "rule.controls.b", null);
            LocalizedButton(panel, "menu.back", () => ShowPage(Page.Main), "mbtn", "mbtn-back");
        }

        /// <summary>Each time home appears: back to the main page, or to "online" if there's a connection error to read.</summary>
        public void OnShow() => ShowPage(app.netStatusIsError && app.netStatusText.Length > 0 ? Page.Online : Page.Main);

        /// <summary>Per step: the buttons follow the connection state.</summary>
        public void Refresh()
        {
            bool canGoOnline = app.NetAvailable && !app.isBusy;
            createRoomButton.SetEnabled(canGoOnline); joinRoomButton.SetEnabled(canGoOnline);
            soloButton.SetEnabled(!app.isBusy); onlineButton.SetEnabled(!app.isBusy);
            bool hasStatus = app.netStatusText.Length > 0;
            SetText(netStatusLabel, hasStatus ? app.netStatusText : Localization.Get(app.NetAvailable ? "menu.net_ready" : "menu.net_unavailable"));
            netStatusLabel.EnableInClassList("net-err", app.netStatusIsError && hasStatus);
        }

        /// <summary>Per frame while showing.</summary>
        public void Frame() { if (currentPage == Page.MyKid) ui.kidCustomizer.AnimatePreview(); }

        /// <summary>A sub-page: a framed panel with a title. Returns the panel to fill.</summary>
        VisualElement AddPanelPage(out VisualElement page, string titleKey)
        {
            page = AddElement(Root, "menu-page");
            var panel = AddElement(page, "mpanel");
            LocalizedLabel(panel, titleKey, "mpanel-t");
            return panel;
        }

        void ShowPage(Page page)
        {
            currentPage = page;
            Show(mainPage, page == Page.Main); Show(onlinePage, page == Page.Online);
            Show(howToPlayPage, page == Page.HowToPlay); Show(myKidPage, page == Page.MyKid);
            if (page == Page.MyKid) ui.kidCustomizer.ShowIn(kidCustomizerSlot);
        }

        /// <summary>Back button on the menu: sub-pages go back to the main page. False when already there.</summary>
        public bool Back()
        {
            if (ui.currentScreen != UiScreen.Home || currentPage == Page.Main) return false;
            ShowPage(Page.Main);
            return true;
        }

        void JoinTypedRoom()
        {
            var code = TextFormat.CleanCode(roomCodeField.value);
            if (code.Length < MinRoomCodeLength) { app.SetNetStatus(Localization.Get("menu.need_code"), true); return; }
            app.EnterRoom(false, code);
        }
    }
}
