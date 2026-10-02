using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>End of a round and end of the match: full screen, no scrolling.</summary>
    sealed class ResultScreen : UiPart
    {
        public readonly VisualElement Root;
        readonly VisualElement resHead, resCards, resSide; Label nextIn; Button againBtn; Label waitHost;

        public ResultScreen(Ui ui) : base(ui)
        {
            Root = E(ui.root, "lobby", "result");
            resHead = E(Root, "res-head");
            var body = E(Root, "lobby-body");
            resCards = E(body, "lobby-players");
            resSide = E(body, "mpanel", "lobby-side");
        }

        /// <summary>Per step: the countdown to the next round, or who can start a new match.</summary>
        public void Sync(bool final)
        {
            if (!final && nextIn != null) Txt(nextIn, Mathf.Max(0, Mathf.CeilToInt(G.tl)).ToString());
            if (final && againBtn != null) { Show(againBtn, G.isHost); Show(waitHost, !G.isHost); }
        }

        public void Render(bool final)
        {
            resHead.Clear(); resCards.Clear(); resSide.Clear(); nextIn = null; againBtn = null; waitHost = null;
            var res = G.res ?? new[] { 0, -1, -1, -1 };
            bool unWin = res[0] == 1; var who = G.EntAt(res[1]); var Un = G.unice; var me = app.me; var nx = G.EntAt(res[3]);
            bool? won = me != null ? unWin == me.IsUnice : (bool?)null;
            string title = Loc.T(unWin ? (Un == me ? "res.doll_back_you" : "res.doll_back_unice") : (Un == me ? "res.timeout_unice" : "res.team_won"));
            var spot = res[2] >= 0 && res[2] < Map.Spots.Length ? Map.Spots[res[2]] : null;
            string reason = who == null ? "" : unWin ? Loc.T("res.reason_won", app.NameUp(Un), app.NameOf(who), Loc.RoomIn(Map.RoomOf(who.x, who.y)))
                                                     : Loc.T("res.reason_lost", app.NameOf(who), spot != null ? " " + Loc.Spot(spot) : "");
            if (final)
            {
                int top = G.ents.Count > 0 ? G.ents.Max(e => e.score) : 0; var ws = G.ents.Where(e => e.score == top).ToList();
                bool iWon = me != null && ws.Contains(me);
                string head = iWon ? Loc.T(ws.Count > 1 ? "res.tie" : "res.you_won") : Loc.T("res.winners", string.Join(" e ", ws.Select(w => w.name)), Loc.T(ws.Count > 1 ? "res.won_plural" : "res.won_single"));
                L(resHead, Loc.T("res.final_header", G.rounds), "res-eyebrow");
                L(resHead, head, "res-title", iWon ? "res-win" : "res-lose");
                L(resHead, Loc.T("res.last_round", title, reason), "res-reason");
                Cards(me, ws);
                L(resSide, Loc.T("res.final_score"), "mstep-l", "res-lbl");
                againBtn = B(resSide, Loc.T("res.new_match"), () => app.NewMatchLobby(), "mbtn", "mbtn-go");
                waitHost = L(resSide, Loc.T("res.waiting_host"), "mnote", "lobby-status");
                B(resSide, Loc.T(app.Online ? "res.leave_room" : "res.back_home"), () => app.LeaveRoom(), "mbtn", "mbtn-back");
            }
            else
            {
                L(resHead, Loc.T("res.round_header", G.r, G.rounds), "res-eyebrow");
                var t = L(resHead, title, "res-title"); if (won != null) t.AddToClassList(won.Value ? "res-win" : "res-lose");
                L(resHead, reason, "res-reason");
                Cards(me, null);
                L(resSide, Loc.T("res.next_unice"), "mstep-l", "res-lbl");
                var nu = E(resSide, "res-next"); var d = new Doll(46); d.SetUnice(); nu.Add(d); L(nu, app.NameUp(nx), "res-next-name");
                L(resSide, Loc.T(unWin ? "res.next_rule_won" : "res.next_rule_timeout"), "mnote", "lobby-status");
                var nr = E(resSide, "res-count"); L(nr, Loc.T("res.next_in"), "mnote"); nextIn = L(nr, "", "res-count-n"); L(nr, Loc.T("res.seconds_suffix"), "mnote");
                L(resSide, Loc.T("res.scoring_rules"), "res-rules");
            }
        }

        /// <summary>Score cards, best first. Winners of the match get a green border.</summary>
        void Cards(Ent me, List<Ent> winners)
        {
            int i = 0;
            foreach (var e in G.ents.OrderByDescending(e => e.score))
            {
                var card = E(resCards, "pcard"); if (e == me) card.AddToClassList("pcard-me"); if (winners != null && winners.Contains(e)) card.AddToClassList("pcard-win");
                L(card, Loc.Ordinal(++i), "pcard-rank");
                var d = new Doll(50); d.Set(e.k); card.Add(d);
                L(card, e.name + (e.bot ? Loc.T("res.bot_tag") : ""), "pcard-name");
                L(card, e.score.ToString(), "pcard-pts");
            }
        }
    }
}
