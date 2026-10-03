using UnityEngine;
using UnityEngine.UIElements;

namespace Unice
{
    /// <summary>The symbols on the round touch buttons, drawn as vectors (language-neutral, no art needed).</summary>
    enum ActionIcon { Sneak, Laugh, Pass, Door, Eye }

    static class ActionIcons
    {
        /// <summary>Draws <paramref name="icon"/> centered in the element being painted, scaled to its size.</summary>
        public static void Draw(MeshGenerationContext context, ActionIcon icon, Color color)
        {
            var painter = context.painter2D;
            var rect = context.visualElement.contentRect;
            float centerX = rect.width / 2, centerY = rect.height / 2, size = Mathf.Min(rect.width, rect.height);
            painter.strokeColor = color; painter.fillColor = color; painter.lineWidth = size * .08f; painter.lineCap = LineCap.Round; painter.lineJoin = LineJoin.Round;
            Vector2 At(float dx, float dy) => new Vector2(centerX + dx * size, centerY + dy * size);
            void Dot(Vector2 center, float radius) { painter.BeginPath(); painter.Arc(center, radius * size, Angle.Degrees(0), Angle.Degrees(360)); painter.Fill(); }

            switch (icon)
            {
                case ActionIcon.Sneak:   // two footprints
                    Dot(At(-.14f, -.08f), .11f); Dot(At(.14f, -.08f), .11f);
                    break;
                case ActionIcon.Laugh:   // a smile and two eyes
                    painter.BeginPath(); painter.Arc(At(0, -.02f), size * .22f, Angle.Degrees(15), Angle.Degrees(165)); painter.Stroke();
                    Dot(At(-.16f, -.2f), .035f); Dot(At(.16f, -.2f), .035f);
                    break;
                case ActionIcon.Pass:    // an arrow to the right
                    painter.BeginPath(); painter.MoveTo(At(-.22f, 0)); painter.LineTo(At(.16f, 0)); painter.Stroke();
                    painter.BeginPath(); painter.MoveTo(At(.04f, -.15f)); painter.LineTo(At(.22f, 0)); painter.LineTo(At(.04f, .15f)); painter.Stroke();
                    break;
                case ActionIcon.Door:    // a door with its knob
                    painter.BeginPath(); painter.MoveTo(At(-.15f, .22f)); painter.LineTo(At(-.15f, -.22f)); painter.LineTo(At(.15f, -.22f)); painter.LineTo(At(.15f, .22f)); painter.Stroke();
                    Dot(At(.07f, 0), .025f);
                    break;
                case ActionIcon.Eye:     // an eye (the Unice looking inside)
                    painter.BeginPath(); painter.MoveTo(At(-.24f, 0)); painter.BezierCurveTo(At(-.1f, -.18f), At(.1f, -.18f), At(.24f, 0)); painter.Stroke();
                    painter.BeginPath(); painter.MoveTo(At(-.24f, 0)); painter.BezierCurveTo(At(-.1f, .18f), At(.1f, .18f), At(.24f, 0)); painter.Stroke();
                    Dot(At(0, 0), .07f);
                    break;
            }
        }
    }
}
