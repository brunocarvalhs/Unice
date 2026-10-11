using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// Floating touch joystick: touching anywhere in the left part of the screen places the stick there; dragging moves the knob.
    /// <see cref="Direction"/> is the knob offset, up to length 1.
    /// </summary>
    sealed class Joystick
    {
        /// <summary>How far the knob travels (panel pixels); half the base's size.</summary>
        const float KnobTravel = 84, BaseHalfSize = 102;

        public Vector2 Direction { get; private set; }

        readonly VisualElement zone, stickBase, knob;
        int pointerId = -1; Vector2 center;

        public Joystick(VisualElement parent)
        {
            zone = AddElement(parent);
            zone.style.position = Position.Absolute; zone.style.left = 0; zone.style.bottom = 0;
            zone.style.width = Length.Percent(45); zone.style.height = Length.Percent(70);
            stickBase = IgnorePointer(AddElement(zone, "joy"));
            knob = IgnorePointer(AddElement(stickBase, "joy-knob"));

            zone.RegisterCallback<PointerDownEvent>(e =>
            {
                if (pointerId >= 0) return;   // already following another finger
                pointerId = e.pointerId; zone.CapturePointer(e.pointerId);
                center = e.localPosition;
                stickBase.style.left = center.x - BaseHalfSize; stickBase.style.bottom = StyleKeyword.Auto; stickBase.style.top = center.y - BaseHalfSize;
                MoveKnob(center);
            });
            zone.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == pointerId) MoveKnob(e.localPosition); });
            zone.RegisterCallback<PointerUpEvent>(e => Release(e.pointerId));
            zone.RegisterCallback<PointerCancelEvent>(e => Release(e.pointerId));
            zone.RegisterCallback<PointerCaptureOutEvent>(e => { pointerId = -1; Reset(); });
            Reset();
        }

        /// <summary>Lets go of the finger it follows, if any (app sent to the background, controls hidden).</summary>
        public void Cancel() => Release(pointerId);

        void Release(int releasedPointerId)
        {
            if (releasedPointerId != pointerId) return;
            if (zone.HasPointerCapture(releasedPointerId)) zone.ReleasePointer(releasedPointerId);
            pointerId = -1; Reset();
        }

        void MoveKnob(Vector2 position)
        {
            var offset = position - center;
            if (offset.magnitude > KnobTravel) offset *= KnobTravel / offset.magnitude;
            knob.style.translate = new Translate(offset.x, offset.y);
            Direction = offset / KnobTravel;
        }

        /// <summary>Back to the resting place in the bottom-left corner, centered.</summary>
        void Reset()
        {
            Direction = Vector2.zero; knob.style.translate = new Translate(0, 0);
            stickBase.style.left = 36; stickBase.style.top = StyleKeyword.Auto; stickBase.style.bottom = 30;
        }
    }
}
