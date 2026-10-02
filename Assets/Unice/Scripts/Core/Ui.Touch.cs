using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unice
{
    public sealed partial class Ui
    {
        // ---------- touch ----------
        VisualElement joyZone, joyBase, joyKnob, actE, actQ, actS, actR;
        public Vector2 Joy { get; private set; }
        public bool SneakOn { get; private set; }
        int joyPointer = -1; Vector2 joyCenter;

        void BuildTouch()
        {
            joyZone = E(touch); joyZone.style.position = Position.Absolute; joyZone.style.left = 0; joyZone.style.bottom = 0;
            joyZone.style.width = Length.Percent(45); joyZone.style.height = Length.Percent(70);
            joyBase = Ig(E(joyZone, "joy")); joyKnob = Ig(E(joyBase, "joy-knob"));
            joyZone.RegisterCallback<PointerDownEvent>(e =>
            {
                if (joyPointer >= 0) return;
                joyPointer = e.pointerId; joyZone.CapturePointer(e.pointerId);
                var lp = (Vector2)e.localPosition; joyCenter = lp;
                joyBase.style.left = lp.x - 102; joyBase.style.bottom = StyleKeyword.Auto; joyBase.style.top = lp.y - 102;
                MoveJoy(lp);
            });
            joyZone.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == joyPointer) MoveJoy(e.localPosition); });
            void End(int id) { if (id != joyPointer) return; if (joyZone.HasPointerCapture(id)) joyZone.ReleasePointer(id); joyPointer = -1; ResetJoy(); }
            joyZone.RegisterCallback<PointerUpEvent>(e => End(e.pointerId));
            joyZone.RegisterCallback<PointerCancelEvent>(e => End(e.pointerId));
            joyZone.RegisterCallback<PointerCaptureOutEvent>(e => { joyPointer = -1; ResetJoy(); });

            var acts = E(touch, "acts");
            actS = ActBtn(acts, "sneak", "act-s", () => { SneakOn = !SneakOn; actS.EnableInClassList("act-on", SneakOn); });
            actR = ActBtn(acts, "laugh", "act-r", () => app.TryLaugh());
            actQ = ActBtn(acts, "pass", "act-q", () => app.TryPass());
            actE = ActBtn(acts, "door", null, () => app.DoAct());
            actEIcon = actE.Q(className: "act-icon");
            actEIcon.generateVisualContent = mgc => DrawIcon(mgc, actEIconKind, actE.resolvedStyle.color);
            ResetJoy();
        }

        /// <summary>A round touch button drawn as a simple vector icon (language-neutral) instead of text.</summary>
        VisualElement ActBtn(VisualElement p, string iconKind, string cls, Action down)
        {
            var b = E(p, "act"); if (cls != null) b.AddToClassList(cls);
            var icon = Ig(E(b, "act-icon"));
            icon.generateVisualContent += mgc => DrawIcon(mgc, iconKind, b.resolvedStyle.color);
            b.RegisterCallback<PointerDownEvent>(e => { down(); e.StopPropagation(); });
            return b;
        }

        VisualElement actEIcon; string actEIconKind = "door";
        void SetActEIcon(string kind) { if (actEIconKind == kind) return; actEIconKind = kind; actEIcon.MarkDirtyRepaint(); }

        static void DrawIcon(MeshGenerationContext mgc, string kind, Color col)
        {
            var p = mgc.painter2D;
            var r = mgc.visualElement.contentRect;
            float cx = r.width / 2, cy = r.height / 2, s = Mathf.Min(r.width, r.height);
            p.strokeColor = col; p.fillColor = col; p.lineWidth = s * .08f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            void Dot(Vector2 c, float rad) { p.BeginPath(); p.Arc(c, rad, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); }
            switch (kind)
            {
                case "sneak":
                    foreach (var dx in new[] { -1f, 1f }) Dot(new Vector2(cx + dx * s * .14f, cy - Mathf.Abs(dx) * s * .08f), s * .11f);
                    break;
                case "laugh":
                    p.BeginPath(); p.Arc(new Vector2(cx, cy - s * .02f), s * .22f, Angle.Degrees(15), Angle.Degrees(165)); p.Stroke();
                    foreach (var dx in new[] { -1f, 1f }) Dot(new Vector2(cx + dx * s * .16f, cy - s * .2f), s * .035f);
                    break;
                case "pass":
                    p.BeginPath(); p.MoveTo(new Vector2(cx - s * .22f, cy)); p.LineTo(new Vector2(cx + s * .16f, cy)); p.Stroke();
                    p.BeginPath(); p.MoveTo(new Vector2(cx + s * .04f, cy - s * .15f)); p.LineTo(new Vector2(cx + s * .22f, cy)); p.LineTo(new Vector2(cx + s * .04f, cy + s * .15f)); p.Stroke();
                    break;
                case "door":
                    p.BeginPath(); p.MoveTo(new Vector2(cx - s * .15f, cy + s * .22f)); p.LineTo(new Vector2(cx - s * .15f, cy - s * .22f)); p.LineTo(new Vector2(cx + s * .15f, cy - s * .22f)); p.LineTo(new Vector2(cx + s * .15f, cy + s * .22f)); p.Stroke();
                    Dot(new Vector2(cx + s * .07f, cy), s * .025f);
                    break;
                case "eye":
                    p.BeginPath(); p.MoveTo(new Vector2(cx - s * .24f, cy));
                    p.BezierCurveTo(new Vector2(cx - s * .1f, cy - s * .18f), new Vector2(cx + s * .1f, cy - s * .18f), new Vector2(cx + s * .24f, cy));
                    p.Stroke();
                    p.BeginPath(); p.MoveTo(new Vector2(cx - s * .24f, cy));
                    p.BezierCurveTo(new Vector2(cx - s * .1f, cy + s * .18f), new Vector2(cx + s * .1f, cy + s * .18f), new Vector2(cx + s * .24f, cy));
                    p.Stroke();
                    Dot(new Vector2(cx, cy), s * .07f);
                    break;
            }
        }

        void MoveJoy(Vector2 lp)
        {
            var d = lp - joyCenter; float m = d.magnitude; const float R = 84;
            if (m > R) d *= R / m;
            joyKnob.style.translate = new Translate(d.x, d.y);
            Joy = d / R;
        }
        void ResetJoy()
        {
            Joy = Vector2.zero; joyKnob.style.translate = new Translate(0, 0);
            joyBase.style.left = 36; joyBase.style.top = StyleKeyword.Auto; joyBase.style.bottom = 30;
        }
    }
}
