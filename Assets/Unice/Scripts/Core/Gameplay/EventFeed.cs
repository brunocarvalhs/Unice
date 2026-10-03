using System;
using System.Linq;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// Turns the match's events (<see cref="GameEvent"/>) into what this player sees and hears — banners, toasts, sounds,
    /// ripples, the catch shake — and logs the important ones to analytics. Runs on every device, host included.
    /// </summary>
    sealed class EventFeed
    {
        readonly App app;
        Game game => app.game;
        Ui ui => app.ui;
        Character me => app.myCharacter;
        int lastHandledSeq = -1;

        public EventFeed(App app) { this.app = app; }

        /// <summary>New room or match: the next <see cref="PlayNewEvents"/> catches up without replaying old events.</summary>
        public void Reset() => lastHandledSeq = -1;

        /// <summary>Plays every event newer than the last one handled. The first call after a reset only catches up.</summary>
        public void PlayNewEvents()
        {
            if (lastHandledSeq < 0) { lastHandledSeq = game.eventSeq; return; }
            foreach (var gameEvent in game.events.ToArray())
            {
                if (gameEvent.sequence <= lastHandledSeq) continue;
                lastHandledSeq = gameEvent.sequence;
                try { Present(gameEvent); }
                catch (Exception error) { Metrics.Error(error, "event " + gameEvent.type); }   // one broken event must not stop the others
            }
            // a new host restarted the numbering: follow it
            if (game.eventSeq < lastHandledSeq - 50) lastHandledSeq = game.eventSeq;
        }

        void Present(GameEvent gameEvent)
        {
            switch (gameEvent.type)
            {
                case GameEventType.Start: OnRoundStart(gameEvent); break;
                case GameEventType.Enter: OnUniceEnters(); break;
                case GameEventType.Giggle: OnGiggle(gameEvent); break;
                case GameEventType.Pass: OnDollPassed(gameEvent); break;
                case GameEventType.Open: OnSpotOpened(gameEvent); break;
                case GameEventType.Catch: OnCatch(gameEvent); break;
                case GameEventType.End: OnRoundEnd(gameEvent); break;
                case GameEventType.Final: OnMatchEnd(); break;
                case GameEventType.Host: OnNewHost(gameEvent); break;
                case GameEventType.Left: OnPlayerLeft(gameEvent); break;
            }
        }

        string MyRole => me == null ? "spectator" : me.IsUnice ? "unice" : me == game.doll.holder ? "holder" : "hider";
        Character CharacterAt(int index) => game.CharacterAt(index);
        string NameFor(Character character) => app.perspective.NameForCapitalized(character);

        void OnRoundStart(GameEvent gameEvent)
        {
            var uniceCharacter = CharacterAt(gameEvent.actor); var dollHolder = CharacterAt(gameEvent.target); int round = gameEvent.value;
            Metrics.Log("round_start", ("round", round), ("rounds", game.totalRounds), ("role", MyRole), ("online", app.IsOnline), ("map", game.mapId));
            if (me == null) ui.Banner(Localization.Get("ev.start.watch", round, NameFor(uniceCharacter)), BannerStyle.Info, 3);
            else if (me == uniceCharacter) ui.Banner(Localization.Get("ev.start.you_unice"), BannerStyle.Rose, 3.2f);
            else if (me == dollHolder) ui.Banner(Localization.Get("ev.start.you_holder"), BannerStyle.Rose, 3.2f);
            else ui.Banner(Localization.Get("ev.start.other", NameFor(uniceCharacter)), BannerStyle.Candle, 3.2f);
        }

        void OnUniceEnters()
        {
            app.sfx.Play("boom");
            app.soundscape.PlayFrom(Map.Entrance.x, Map.Entrance.y, 1400, Localization.Get("ev.enter.sound"), Ui.Rose, "creak", .35f);
            ui.Banner(Localization.Get(game.unice == me ? "ev.enter.unice" : "ev.enter.others"), BannerStyle.Rose, 2.4f);
        }

        /// <summary>The doll giggled or someone laughed: everyone hears it; the Unice is told the room.</summary>
        void OnGiggle(GameEvent gameEvent)
        {
            float x = gameEvent.x, y = gameEvent.y; var laugher = CharacterAt(gameEvent.actor); var room = Map.RoomAt(x, y);
            bool isDoll = laugher != null && laugher == game.doll.holder;
            app.worldView.ShowRipple(x, y, RippleKind.Giggle);
            ui.ShowWorldText(x, y - 30, Localization.Get("ev.giggle.fx"), Ui.Rose, 18, 1.8f, true);
            app.soundscape.PlayFrom(x, y, 2200, Localization.Get("ev.giggle.sound"), Ui.Rose, "giggle", .3f);
            if (me != null && me.IsUnice && game.IsLive) ui.Banner(Localization.Get("ev.giggle.unice", Localization.RoomPhrase(room)), BannerStyle.Rose, 2);
            else if (laugher == me) ui.Banner(Localization.Get(isDoll ? "ev.giggle.holder" : "ev.giggle.laughed"), BannerStyle.Rose, 2.4f);
            else if (isDoll) ui.Toast(Localization.Get("ev.giggle.toast_doll", Localization.RoomPhrase(room)));
            else ui.Toast(Localization.Get("ev.giggle.toast_who", NameFor(laugher), Localization.RoomPhrase(room)));
        }

        void OnDollPassed(GameEvent gameEvent)
        {
            var giver = CharacterAt(gameEvent.actor); var receiver = CharacterAt(gameEvent.target);
            if (giver == null || receiver == null) return;
            var perspective = app.perspective;
            app.soundscape.PlayFrom(giver.x, giver.y, 220, Localization.Get("ev.pass.sound"), Ui.Rose, "swish");
            if (perspective.ShowsTeamInfo || perspective.CanSee(giver.x, giver.y))
                ui.ShowWorldText((giver.x + receiver.x) / 2, (giver.y + receiver.y) / 2 - 26, Localization.Get("ev.pass.fx"), Ui.Rose, 14, 1);
            if (receiver == me) ui.Banner(Localization.Get("ev.pass.you_got"), BannerStyle.Rose, 2);
            else if (giver == me) ui.Banner(Localization.Get("ev.pass.you_gave", receiver.name), BannerStyle.Rose, 1.5f);
            else if (perspective.ShowsTeamInfo) ui.Toast(Localization.Get("ev.pass.toast", giver.name, receiver.name));
        }

        void OnSpotOpened(GameEvent gameEvent)
        {
            int spotIndex = gameEvent.value;
            if (spotIndex < 0 || spotIndex >= Map.Spots.Length) return;
            var spot = Map.Spots[spotIndex];
            app.soundscape.PlayFrom(spot.CenterX, spot.CenterY, 450, Localization.Get("ev.open.sound"), Ui.Candle, "creak");
            bool wasEmpty = gameEvent.target < 0;
            if (wasEmpty && app.perspective.CanSee(spot.CenterX, spot.CenterY))
                ui.ShowWorldText(spot.standX, spot.standY - 24, Localization.Get("ev.open.empty"), Ui.Muted, 14, 1);
        }

        /// <summary>The catch moment: sting, ripple, screen shake and red flash (stronger when the doll was caught).</summary>
        void OnCatch(GameEvent gameEvent)
        {
            var caught = CharacterAt(gameEvent.target); var catcher = CharacterAt(gameEvent.actor);
            bool withDoll = gameEvent.value != 0; float x = gameEvent.x, y = gameEvent.y;
            if (caught == null) return;
            if (caught == me || catcher == me)
                Metrics.Log("catch", ("side", caught == me ? "caught" : "catcher"), ("with_doll", withDoll), ("time_left", (int)game.timeLeft), ("room", Map.RoomAt(x, y).id));

            app.sfx.Play("sting");
            ui.ShowWorldText(x, y - 26, Localization.Get(withDoll ? "ev.catch.mine" : "ev.catch.gotcha"), Ui.Blood, withDoll ? 24 : 20, 1.8f, true);
            app.worldView.ShowRipple(x, y, RippleKind.Catch);
            app.cameraRig.Shake(withDoll ? 16f : 10f);
            ui.Flash(new Color(.75f, .12f, .16f, withDoll ? .4f : .22f), withDoll ? .3f : .18f);
            app.worldView.FlashHit(caught.index); if (catcher != null) app.worldView.FlashHit(catcher.index);

            if (caught == me) ui.Banner(Localization.Get(withDoll ? "ev.catch.you_doll" : "ev.catch.you_out"), BannerStyle.Blood, 2.6f);
            else if (catcher == me && !withDoll) ui.Banner(Localization.Get("ev.catch.not_holder", caught.name), BannerStyle.Candle, 2);
            else if (!withDoll) ui.Toast(Localization.Get("ev.catch.toast", caught.name, Localization.RoomPhrase(Map.RoomAt(x, y))));
        }

        void OnRoundEnd(GameEvent gameEvent)
        {
            bool uniceWon = gameEvent.value != 0;
            Metrics.Log("round_end", ("round", game.round), ("unice_won", uniceWon), ("role", me == null ? "spectator" : me.IsUnice ? "unice" : "hider"),
                ("won", me != null && uniceWon == me.IsUnice), ("caught", me != null && me.outOfRound), ("online", app.IsOnline));
            if (me != null) app.sfx.Play(uniceWon == me.IsUnice ? "win" : "lose");
            if (!uniceWon) ui.Banner(Localization.Get("ev.end.time"), BannerStyle.Moss, 1.6f);
            app.heartbeat.Stop();
        }

        void OnMatchEnd()
        {
            if (me == null) return;
            int place = 1 + game.characters.Count(character => character.score > me.score);
            Metrics.Log("match_end", ("rounds", game.totalRounds), ("score", me.score), ("place", place), ("players", game.characters.Count), ("online", app.IsOnline));
        }

        void OnNewHost(GameEvent gameEvent)
        {
            string hostName = TextFormat.CleanName(gameEvent.text);
            bool itsMe = game.isHost && hostName == app.myName;
            ui.Toast(itsMe ? Localization.Get("ev.host.you") : Localization.Get("ev.host.other", hostName));
        }

        void OnPlayerLeft(GameEvent gameEvent)
        {
            var leaver = CharacterAt(gameEvent.actor);
            if (leaver != null) ui.Toast(Localization.Get("ev.left", leaver.name));
        }
    }
}
