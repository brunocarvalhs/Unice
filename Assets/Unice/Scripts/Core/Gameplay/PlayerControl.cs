using UnityEngine;
using UnityEngine.InputSystem;

namespace Unice
{
    /// <summary>This device's player: keyboard/joystick input, movement, and the E/Q/R actions (sent to the host when a guest).</summary>
    sealed class PlayerControl
    {
        readonly App app;
        Game G => app.G;
        bool kUp, kDown, kLeft, kRight, kSneak;
        float freeze, catchCd;
        /// <summary>The last action, published in this device's presence; the host applies it once per new <see cref="aSeq"/>.</summary>
        public int aSeq, aArg; public Act aType;

        public PlayerControl(App app) { this.app = app; }

        public void Reset() { aSeq = 0; aType = Act.None; aArg = -1; }
        public void Cooldowns(float dt) { if (freeze > 0) freeze -= dt; if (catchCd > 0) catchCd -= dt; }

        /// <summary>Per frame: keyboard state, plus the one-shot keys (E/Q/R/P, Esc).</summary>
        public void ReadKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) { kUp = kDown = kLeft = kRight = kSneak = false; return; }
            bool typing = app.ui.Typing;
            kUp = !typing && (kb.wKey.isPressed || kb.upArrowKey.isPressed);
            kDown = !typing && (kb.sKey.isPressed || kb.downArrowKey.isPressed);
            kLeft = !typing && (kb.aKey.isPressed || kb.leftArrowKey.isPressed);
            kRight = !typing && (kb.dKey.isPressed || kb.rightArrowKey.isPressed);
            kSneak = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (kb.escapeKey.wasPressedThisFrame) app.Back();   // also the Android back button
            if (typing || !G.Live || app.paused) return;
            if (kb.eKey.wasPressedThisFrame) DoAct();
            if (kb.qKey.wasPressedThisFrame) TryPass();
            if (kb.rKey.wasPressedThisFrame) TryLaugh();
            if (kb.pKey.wasPressedThisFrame) app.TogglePause();
        }

        public Vector2 MoveInput()
        {
            var v = new Vector2((kRight ? 1 : 0) - (kLeft ? 1 : 0), (kDown ? 1 : 0) - (kUp ? 1 : 0)) + app.ui.Joy;
            return v.magnitude < .3f ? Vector2.zero : v.normalized;
        }

        /// <summary>Moves this player from the input (and, as the Unice on a guest device, asks the host to catch whoever it touches).</summary>
        public void Move(float dt)
        {
            var p = app.me; p.moving = false;
            if (!G.CanMoveNow(p) || freeze > 0 || app.paused) { p.loud = false; p.talking = false; return; }
            p.sneak = !p.IsUnice && (kSneak || app.ui.SneakOn);
            var mv = MoveInput();
            if (mv != Vector2.zero)
            {
                float sp = p.IsUnice ? Rules.SpdUnicePlayer : p.sneak ? Rules.SpdSneak : Rules.SpdRun, ox = p.x, oy = p.y;
                Game.MoveEnt(p, mv.x * sp * dt, mv.y * sp * dt); p.fx = mv.x; p.fy = mv.y;
                p.moving = U.Dist(p.x, p.y, ox, oy) > .01f;
            }
            p.loud = !p.IsUnice && p.moving && !p.sneak;
            p.talking = app.Talking;
            if (p.IsUnice && G.ph == Phase.Hunt && !G.isHost && catchCd <= 0)
                foreach (var h in G.ents)
                    if (!h.IsUnice && !h.outOfRound && h.hidden == null && U.Dist(p, h) < Rules.CatchR) { SendAction(Act.Catch, h.idx); catchCd = .3f; break; }
        }

        void SendAction(Act type, int arg = -1)
        {
            if (G.isHost) { G.DoAction(app.me, type, arg); return; }
            aSeq++; aType = type; aArg = arg; app.room.PublishSoon();
        }

        /// <summary>E: hide / leave the hiding spot, or (Unice) look inside the furniture.</summary>
        public void DoAct()
        {
            var p = app.me; if (p == null || p.outOfRound || !G.Live || app.paused) return;
            if (p.IsUnice)
            {
                if (G.ph != Phase.Hunt || p.stun > 0) return;
                var s = Game.NearSpotFurn(p);
                if (s == null) { app.ui.Toast(Loc.T("act.unice.need_near")); return; }
                SendAction(Act.Open, s.si); Metrics.Log("player_action", ("type", "open")); return;
            }
            if (p.hidden != null) { app.sfx.Play("creak"); SendAction(Act.Exit); Metrics.Log("player_action", ("type", "exit")); return; }
            var f = Game.NearSpotFurn(p);
            if (f == null) { app.ui.Toast(Loc.T("act.hide.need_near")); return; }
            if (f.occ != null) { app.ui.Toast(Loc.T("act.hide.occupied", f.occ.name)); app.sfx.Play("bad"); return; }
            app.sfx.Play("creak"); freeze = .35f; SendAction(Act.Hide, f.si); Metrics.Log("player_action", ("type", "hide"), ("spot", f.type));
        }

        /// <summary>Q: hand the doll to someone right next to you.</summary>
        public void TryPass()
        {
            var p = app.me; if (p == null || p.outOfRound || p.IsUnice || !G.Live || app.paused) return;
            if (G.doll.holder != p) { app.ui.Toast(Loc.T("act.pass.no_doll")); return; }
            if (p.passCd > 0) { app.ui.Toast(Loc.T("act.pass.cooldown", Mathf.CeilToInt(p.passCd))); app.sfx.Play("bad"); return; }
            if (G.PassTarget(p) == null) { app.ui.Toast(Loc.T("act.pass.no_target")); return; }
            SendAction(Act.Pass); Metrics.Log("player_action", ("type", "pass"));
        }

        /// <summary>R: laugh on purpose. Anyone can, but it gives away your position to the Unice like the doll's own giggle.</summary>
        public void TryLaugh()
        {
            var p = app.me; if (p == null || p.outOfRound || p.IsUnice || !G.Live || app.paused) return;
            if (p.laughCd > 0) { app.ui.Toast(Loc.T("act.laugh.cooldown", Mathf.CeilToInt(p.laughCd))); app.sfx.Play("bad"); return; }
            SendAction(Act.Laugh); Metrics.Log("player_action", ("type", "laugh"));
        }
    }
}
