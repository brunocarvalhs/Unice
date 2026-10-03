using System;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// The waiting room's match options: the host changes them with ‹ › steppers; everyone else sees a summary line.
    /// The values live in <see cref="Game"/> and reach the guests in the snapshot.
    /// </summary>
    sealed class MatchOptions : UiPart
    {
        static readonly int[] RoundChoices = { 3, 5, 7 };
        /// <summary>Quintal/praça are a work in progress (flat-tint placeholder art) — only selectable in the Editor or a dev build.</summary>
        static readonly string[] PlayableMapIds = Debug.isDebugBuild ? Maps.AllIds : new[] { MapHouse.Id };

        readonly VisualElement hostSteppers;
        readonly Label botsValue, roundsValue, hideTimeValue, huntTimeValue, giggleValue, mapValue, summaryLabel;
        readonly Action onChanged;

        public MatchOptions(Ui ui, VisualElement parent, Action onChanged) : base(ui)
        {
            this.onChanged = onChanged;
            hostSteppers = AddElement(parent);
            botsValue = AddStepper("lobby.bots_plural", step => game.botCount = Mathf.Clamp(Math.Min(game.botCount, MaxBots) + step, 0, MaxBots));
            roundsValue = AddStepper("lobby.rounds_label", step =>
            {
                int index = Array.IndexOf(RoundChoices, game.totalRounds); if (index < 0) index = 1;
                game.totalRounds = RoundChoices[Mathf.Clamp(index + step, 0, RoundChoices.Length - 1)];
            });
            hideTimeValue = AddStepper("lobby.hide_label", step => game.hideTime = Mathf.Clamp(game.hideTime + step * 5, 15, 60));
            huntTimeValue = AddStepper("lobby.hunt_label", step => game.huntTime = Mathf.Clamp(game.huntTime + step * 15, 60, 240));
            giggleValue = AddStepper("lobby.giggle_label", step => game.giggleInterval = Mathf.Clamp(game.giggleInterval + step * 5, 5, 20));
            mapValue = AddStepper("lobby.map_label", step =>
            {
                int index = Array.IndexOf(PlayableMapIds, game.mapId); if (index < 0) index = 0;
                game.mapId = PlayableMapIds[(index + step + PlayableMapIds.Length) % PlayableMapIds.Length];   // wraps around
            });
            summaryLabel = AddLabel(parent, "", "mnote");
        }

        /// <summary>Bots fill the free seats, up to <see cref="Rules.MaxPlayers"/> in total.</summary>
        public int MaxBots => Math.Max(0, Rules.MaxPlayers - app.LobbyPlayers().Count);

        /// <summary>The bots that will actually play (the setting, capped by the free seats).</summary>
        public int BotsInMatch => Math.Min(game.botCount, MaxBots);

        /// <summary>A "label  ‹ value ›" row; returns the value label.</summary>
        Label AddStepper(string labelKey, Action<int> change)
        {
            var stepper = AddElement(hostSteppers, "mstep");
            LocalizedLabel(stepper, labelKey, "mstep-l");
            AddButton(stepper, "‹", () => { change(-1); onChanged(); }, "mstep-btn");
            var value = AddLabel(stepper, "", "mstep-v");
            AddButton(stepper, "›", () => { change(1); onChanged(); }, "mstep-btn");
            return value;
        }

        public void Render()
        {
            int bots = BotsInMatch; string mapName = Localization.Get("map." + game.mapId + ".name");
            SetText(botsValue, bots.ToString()); SetText(roundsValue, game.totalRounds.ToString());
            SetText(hideTimeValue, $"{game.hideTime:0}s"); SetText(huntTimeValue, $"{game.huntTime:0}s"); SetText(giggleValue, $"{game.giggleInterval:0}s");
            SetText(mapValue, mapName);
            Show(hostSteppers, game.isHost);
            SetText(summaryLabel, game.isHost ? "" : Localization.Get("lobby.info", bots, game.totalRounds, game.hideTime, game.huntTime, game.giggleInterval, mapName));
            Show(summaryLabel, !game.isHost);
        }
    }
}
