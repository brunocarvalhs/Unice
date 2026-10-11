using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// Turns the match's events (<see cref="GameEvent"/>) into what this player sees and hears — banners, toasts, sounds,
    /// ripples, the catch shake — logs the important ones to analytics and reports Play Games achievements and the
    /// "Online wins" leaderboard (rules in <see cref="Achievements"/>). Runs on every device, host included; each event is
    /// played once, so nothing is reported twice.
    /// </summary>
    sealed class EventFeed
    {
        readonly App app;
        Game game => app.game;
        Ui ui => app.ui;
        Character me => app.myCharacter;
        int lastHandledSeq = -1;
        /// <summary>This round, for <see cref="Achievement.HotPotato"/> and <see cref="Achievement.Decoy"/>. Cleared at round start.</summary>
        bool passedDollThisRound, laughedAsDecoyThisRound;
        /// <summary>When this device saw the match's first round start (seconds, device clock), or -1: the leaderboard's duration filter.</summary>
        float matchStartedAt = -1;
        /// <summary>When this device saw the current round start (seconds, device clock), or -1: "duration_s" of "round_end".</summary>
        float roundStartedAt = -1;
        /// <summary>Most humans in this match at any round start (people can join between rounds): "humans_peak" of "match_end".</summary>
        int humansPeak;

        public EventFeed(App app) { this.app = app; }

        /// <summary>New room or match: the next <see cref="PlayNewEvents"/> catches up without replaying old events.</summary>
        public void Reset() { lastHandledSeq = -1; matchStartedAt = roundStartedAt = -1; humansPeak = 0; }

        /// <summary>Plays every event newer than the last one handled. The first call after a reset only catches up.</summary>
        public void PlayNewEvents()
        {
            if (lastHandledSeq < 0) { lastHandledSeq = game.eventSeq; return; }
            // by index, no copy: presenting an event never adds or removes events (only the host's rules and snapshots do)
            for (int i = 0, count = game.events.Count; i < count; i++)
            {
                var gameEvent = game.events[i];
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
        /// <summary>People still in the match: whoever left became a bot and no longer counts.</summary>
        int Humans => game.characters.Count(character => !character.isBot);
        /// <summary>Whole seconds since a time taken from the device clock; 0 when it wasn't seen (joined midway).</summary>
        static int SecondsSince(float startedAt) => startedAt < 0 ? 0 : (int)(Time.realtimeSinceStartup - startedAt);

        /// <summary>Sends what was earned to Play Games (nothing if it isn't signed in) and logs each unlock to analytics.</summary>
        static void Report(List<Achievement> earned)
        {
            foreach (var achievement in earned)
            {
                if (!Achievements.IsIncremental(achievement)) Metrics.Log("achievement_unlock", ("id", achievement.ToString()));
                AccountRegistry.ReportAchievement?.Invoke(achievement);
            }
        }

        void OnRoundStart(GameEvent gameEvent)
        {
            var uniceCharacter = CharacterAt(gameEvent.actor); var dollHolder = CharacterAt(gameEvent.target); int round = gameEvent.value;
            Metrics.Log("round_start", ("round", round), ("rounds", game.totalRounds), ("role", MyRole), ("online", app.IsOnline), ("map", game.mapId));
            passedDollThisRound = laughedAsDecoyThisRound = false;
            if (matchStartedAt < 0) matchStartedAt = Time.realtimeSinceStartup;
            roundStartedAt = Time.realtimeSinceStartup;
            humansPeak = Math.Max(humansPeak, Humans);
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
            if (me != null && laugher == me && !isDoll) laughedAsDecoyThisRound = true;
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
            if (giver == me) passedDollThisRound = true;
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
            Report(Achievements.ForCatch(iCaught: me != null && catcher == me, withDoll));

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
                ("won", me != null && uniceWon == me.IsUnice), ("caught", me != null && me.outOfRound), ("online", app.IsOnline), ("duration_s", SecondsSince(roundStartedAt)));
            roundStartedAt = -1;
            if (me != null)
            {
                int dollHolder = game.roundResult?.dollHolder ?? -1;
                Report(Achievements.ForRoundEnd(new RoundFacts
                {
                    uniceWon = uniceWon, iAmUnice = me.IsUnice, iWasCaught = me.outOfRound, iHeldDoll = me.index == dollHolder,
                    iPassedDoll = passedDollThisRound, iLaughedAsDecoy = laughedAsDecoyThisRound,
                    hiders = game.characters.Count(character => !character.IsUnice),
                    hidersFree = game.characters.Count(character => !character.IsUnice && !character.outOfRound && character.index != dollHolder),
                }));
            }
            if (me != null) app.sfx.Play(uniceWon == me.IsUnice ? "win" : "lose");
            if (!uniceWon) ui.Banner(Localization.Get("ev.end.time"), BannerStyle.Moss, 1.6f);
            app.heartbeat.Stop();
        }

        void OnMatchEnd()
        {
            float seconds = matchStartedAt < 0 ? 0 : Time.realtimeSinceStartup - matchStartedAt;
            int peak = Math.Max(humansPeak, Humans);
            matchStartedAt = -1; humansPeak = 0;
            if (me == null) return;
            int place = 1 + game.characters.Count(character => character.score > me.score);
            Metrics.Log("match_end", ("rounds", game.totalRounds), ("score", me.score), ("place", place), ("players", game.characters.Count), ("online", app.IsOnline),
                ("duration_s", (int)seconds), ("humans_peak", peak));
            if (app.MarkFirstMatchEnd()) Metrics.Log("first_match_end", ("online", app.IsOnline), ("place", place), ("duration_s", (int)seconds));
            Report(Achievements.ForMatchEnd(place, Humans, app.IsOnline));
            if (Achievements.CountsForLeaderboard(place, Humans, app.IsOnline, game.totalRounds, seconds))
                AccountRegistry.ReportOnlineWins?.Invoke(app.AddOnlineWin());
        }

        void OnNewHost(GameEvent gameEvent)
        {
            string hostName = TextFormat.CleanName(gameEvent.text);
            bool itsMe = game.isHost && hostName == app.myName;
            // only the new host logs it: one event per handover, not one per device
            if (itsMe) Metrics.Log("host_changed", ("phase", game.phase.ToString().ToLowerInvariant()), ("round", game.round), ("humans", Humans));
            ui.Toast(itsMe ? Localization.Get("ev.host.you") : Localization.Get("ev.host.other", hostName));
        }

        void OnPlayerLeft(GameEvent gameEvent)
        {
            var leaver = CharacterAt(gameEvent.actor);
            if (leaver != null) ui.Toast(Localization.Get("ev.left", leaver.name));
        }
    }
}
