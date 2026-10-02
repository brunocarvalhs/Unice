using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// The end of a round (who won, who is the next Unice, countdown) and the end of the match (final scoreboard).
    /// Full screen, no scrolling. Built once when it appears (<see cref="Render"/>), then only the countdown updates.
    /// </summary>
    sealed class ResultScreen : UiPart
    {
        public readonly VisualElement Root;
        readonly VisualElement header, scoreCards, sidePanel;
        // only exist on one of the two variants; null otherwise
        Label nextRoundCountdown, waitingHostLabel;
        Button newMatchButton;

        public ResultScreen(Ui ui) : base(ui)
        {
            Root = AddElement(ui.root, "lobby", "result");
            header = AddElement(Root, "res-head");
            var body = AddElement(Root, "lobby-body");
            scoreCards = AddElement(body, "lobby-players");
            sidePanel = AddElement(body, "mpanel", "lobby-side");
        }

        /// <summary>Per step: the countdown to the next round, or who can start a new match.</summary>
        public void Refresh(bool final)
        {
            if (!final && nextRoundCountdown != null) SetText(nextRoundCountdown, Mathf.Max(0, Mathf.CeilToInt(game.timeLeft)).ToString());
            if (final && newMatchButton != null) { Show(newMatchButton, game.isHost); Show(waitingHostLabel, !game.isHost); }
        }

        public void Render(bool final)
        {
            header.Clear(); scoreCards.Clear(); sidePanel.Clear();
            nextRoundCountdown = null; newMatchButton = null; waitingHostLabel = null;
            var result = game.roundResult ?? new RoundResult();
            string title = RoundTitle(result), reason = RoundReason(result);
            if (final) RenderMatchEnd(title, reason);
            else RenderRoundEnd(result, title, reason);
        }

        string RoundTitle(RoundResult result)
        {
            bool iAmUnice = game.unice == app.myCharacter;
            if (result.uniceWon) return Localization.Get(iAmUnice ? "res.doll_back_you" : "res.doll_back_unice");
            return Localization.Get(iAmUnice ? "res.timeout_unice" : "res.team_won");
        }

        /// <summary>"The Unice caught Bia in the kitchen" / "Bia kept the doll under the bed".</summary>
        string RoundReason(RoundResult result)
        {
            var holder = game.CharacterAt(result.dollHolder);
            if (holder == null) return "";
            var perspective = app.perspective;
            if (result.uniceWon)
                return Localization.Get("res.reason_won", perspective.NameForCapitalized(game.unice), perspective.NameFor(holder), Localization.RoomPhrase(Map.RoomAt(holder.x, holder.y)));
            int spotIndex = result.holderSpotIndex;
            var spot = spotIndex >= 0 && spotIndex < Map.Spots.Length ? Map.Spots[spotIndex] : null;
            return Localization.Get("res.reason_lost", perspective.NameFor(holder), spot != null ? " " + Localization.SpotPhrase(spot) : "");
        }

        void RenderRoundEnd(RoundResult result, string title, string reason)
        {
            var me = app.myCharacter;
            AddLabel(header, Localization.Get("res.round_header", game.round, game.totalRounds), "res-eyebrow");
            var titleLabel = AddLabel(header, title, "res-title");
            if (me != null) titleLabel.AddToClassList(result.uniceWon == me.IsUnice ? "res-win" : "res-lose");
            AddLabel(header, reason, "res-reason");
            AddScoreCards(null);

            AddLabel(sidePanel, Localization.Get("res.next_unice"), "mstep-l", "res-lbl");
            var nextUniceRow = AddElement(sidePanel, "res-next");
            var portrait = new CharacterPortrait(46); portrait.SetUnice(); nextUniceRow.Add(portrait);
            AddLabel(nextUniceRow, app.perspective.NameForCapitalized(game.CharacterAt(result.nextUnice)), "res-next-name");
            AddLabel(sidePanel, Localization.Get(result.uniceWon ? "res.next_rule_won" : "res.next_rule_timeout"), "mnote", "lobby-status");
            var countdownRow = AddElement(sidePanel, "res-count");
            AddLabel(countdownRow, Localization.Get("res.next_in"), "mnote");
            nextRoundCountdown = AddLabel(countdownRow, "", "res-count-n");
            AddLabel(countdownRow, Localization.Get("res.seconds_suffix"), "mnote");
            AddLabel(sidePanel, Localization.Get("res.scoring_rules"), "res-rules");
        }

        void RenderMatchEnd(string lastRoundTitle, string lastRoundReason)
        {
            var me = app.myCharacter;
            int topScore = game.characters.Count > 0 ? game.characters.Max(character => character.score) : 0;
            var winners = game.characters.Where(character => character.score == topScore).ToList();
            bool iWon = me != null && winners.Contains(me);
            string headline = iWon
                ? Localization.Get(winners.Count > 1 ? "res.tie" : "res.you_won")
                : Localization.Get("res.winners", string.Join(" e ", winners.Select(winner => winner.name)), Localization.Get(winners.Count > 1 ? "res.won_plural" : "res.won_single"));

            AddLabel(header, Localization.Get("res.final_header", game.totalRounds), "res-eyebrow");
            AddLabel(header, headline, "res-title", iWon ? "res-win" : "res-lose");
            AddLabel(header, Localization.Get("res.last_round", lastRoundTitle, lastRoundReason), "res-reason");
            AddScoreCards(winners);

            AddLabel(sidePanel, Localization.Get("res.final_score"), "mstep-l", "res-lbl");
            newMatchButton = AddButton(sidePanel, Localization.Get("res.new_match"), () => app.BackToLobby(), "mbtn", "mbtn-go");
            waitingHostLabel = AddLabel(sidePanel, Localization.Get("res.waiting_host"), "mnote", "lobby-status");
            AddButton(sidePanel, Localization.Get(app.IsOnline ? "res.leave_room" : "res.back_home"), () => app.LeaveRoom(), "mbtn", "mbtn-back");
        }

        /// <summary>Score cards, best first. Winners of the match (if given) get a green border.</summary>
        void AddScoreCards(List<Character> winners)
        {
            var me = app.myCharacter; int rank = 0;
            foreach (var character in game.characters.OrderByDescending(character => character.score))
            {
                var card = AddElement(scoreCards, "pcard");
                if (character == me) card.AddToClassList("pcard-me");
                if (winners != null && winners.Contains(character)) card.AddToClassList("pcard-win");
                AddLabel(card, Localization.Ordinal(++rank), "pcard-rank");
                var portrait = new CharacterPortrait(50); portrait.ShowKit(character.kit); card.Add(portrait);
                AddLabel(card, character.name + (character.isBot ? Localization.Get("res.bot_tag") : ""), "pcard-name");
                AddLabel(card, character.score.ToString(), "pcard-pts");
            }
        }
    }
}
