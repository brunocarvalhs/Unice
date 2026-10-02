using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>Arcs on a ring around the player pointing at sounds it can't see (direction and loudness).</summary>
    sealed class SoundCues : UiPart
    {
        const float CueSeconds = 1.3f;
        /// <summary>Half the angle each arc spans, in radians.</summary>
        const float ArcHalfAngle = .28f;

        readonly VisualElement layer;
        struct SoundCue { public float angle, loudness, age; public Color color; }
        readonly List<SoundCue> cues = new List<SoundCue>();
        Vector2 ringCenter; float ringRadius; bool hadCues;

        public SoundCues(Ui ui) : base(ui)
        {
            layer = IgnorePointer(AddElement(ui.root, "layer"));
            layer.generateVisualContent += Draw;
        }

        /// <param name="angle">Direction of the sound from the player, in radians.</param>
        /// <param name="loudness">0..1: louder sounds draw thicker and stronger.</param>
        public void ShowSoundCue(float angle, float loudness, Color color) => cues.Add(new SoundCue { angle = angle, loudness = loudness, color = color });

        public void Frame(float dt, Camera camera)
        {
            for (int i = cues.Count - 1; i >= 0; i--)
            {
                var cue = cues[i]; cue.age += dt;
                if (cue.age >= CueSeconds) cues.RemoveAt(i); else cues[i] = cue;
            }
            var perspective = app.perspective;
            if (perspective.hasView)
            {
                ringCenter = RuntimePanelUtils.CameraTransformWorldToPanel(ui.root.panel, Art.ToWorld(perspective.view.x, perspective.view.y - 18), camera);
                ringRadius = Mathf.Min(ui.root.layout.width, ui.root.layout.height) * .44f;
            }
            // repaint while there are cues, and once more to clear the last one
            if (cues.Count > 0 || hadCues) { layer.MarkDirtyRepaint(); hadCues = cues.Count > 0; }
        }

        void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D; painter.lineCap = LineCap.Round;
            foreach (var cue in cues)
            {
                float progress = cue.age / CueSeconds;
                var color = cue.color; color.a = (1 - progress) * Mathf.Clamp(.35f + cue.loudness * 1.5f, .35f, 1);
                painter.strokeColor = color; painter.lineWidth = 3 + cue.loudness * 9;
                painter.BeginPath();
                painter.Arc(ringCenter, ringRadius + progress * 10, Angle.Radians(cue.angle - ArcHalfAngle), Angle.Radians(cue.angle + ArcHalfAngle));
                painter.Stroke();
            }
        }
    }
}
