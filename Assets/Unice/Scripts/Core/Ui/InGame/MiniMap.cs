using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// The house plan in the player list panel: rooms, doors and furniture, plus what this player is allowed to know —
    /// the team sees the hiders (the doll holder ringed); the Unice sees only where the doll last giggled.
    /// </summary>
    sealed class MiniMap : UiPart
    {
        static readonly Color Background = new Color32(42, 28, 36, 255), Floor = new Color32(74, 53, 39, 255), FurnitureColor = new Color32(109, 74, 51, 255);
        static readonly Color UniceDot = new Color32(221, 214, 223, 255);
        /// <summary>A giggle stays on the Unice's map for this many seconds.</summary>
        const float GiggleMarkerSeconds = 14;

        readonly VisualElement element;

        public MiniMap(Ui ui, VisualElement parent) : base(ui)
        {
            element = IgnorePointer(AddElement(parent, "mini"));
            element.generateVisualContent += Draw;
        }

        public void Repaint() => element.MarkDirtyRepaint();

        void Draw(MeshGenerationContext context)
        {
            var rect = element.contentRect; float width = rect.width, height = rect.height;
            if (width < 2 || height < 2) return;
            float scale = Mathf.Min(width / Map.WorldW, height / Map.WorldH);
            float offsetX = (width - Map.WorldW * scale) / 2, offsetY = (height - Map.WorldH * scale) / 2;
            var painter = context.painter2D;
            Vector2 ToMap(float x, float y) => new Vector2(offsetX + x * scale, offsetY + y * scale);

            void Box(float x, float y, float boxWidth, float boxHeight, Color color)
            {
                painter.fillColor = color; painter.BeginPath();
                painter.MoveTo(ToMap(x, y)); painter.LineTo(ToMap(x + boxWidth, y)); painter.LineTo(ToMap(x + boxWidth, y + boxHeight)); painter.LineTo(ToMap(x, y + boxHeight));
                painter.ClosePath(); painter.Fill();
            }
            void Dot(float x, float y, float radius, Color? fill, Color? ring)
            {
                painter.BeginPath(); painter.Arc(ToMap(x, y), radius * scale, Angle.Degrees(0), Angle.Degrees(360)); painter.ClosePath();
                if (fill.HasValue) { painter.fillColor = fill.Value; painter.Fill(); }
                if (ring.HasValue) { painter.strokeColor = ring.Value; painter.lineWidth = Mathf.Max(1, 12 * scale); painter.Stroke(); }
            }

            Box(14, 14, Map.WorldW - 28, Map.WorldH - 28, Background);
            foreach (var room in Map.Rooms) Box(room.x, room.y, room.w, room.h, Floor);
            foreach (var door in Map.Doors) Box(door.rect.x, door.rect.y, door.rect.w, door.rect.h, Floor);
            foreach (var piece in Map.Furniture) Box(piece.x, piece.y, piece.w, piece.h, FurnitureColor);
            if (!game.IsLive) return;

            var me = app.myCharacter; var doll = game.doll;
            if (me != null && me.IsUnice)
            {
                // the Unice: a pulsing ring where the doll last giggled
                if (doll.hasLastGiggle && game.roundClock - doll.lastGiggleTime < GiggleMarkerSeconds)
                    Dot(doll.lastGiggleX, doll.lastGiggleY, 40 + Mathf.Sin(Time.unscaledTime * 6) * 10, null, Ui.Rose);
            }
            else if (me == null || me.outOfRound || me.hidingSpot == null)
            {
                // the team (not while peeking out of a spot): every hider, hollow when hidden
                foreach (var hider in game.characters)
                {
                    if (hider.IsUnice || hider.outOfRound || hider == me || hider.x <= 0) continue;
                    var outfitColor = Art.HexColor(Kit.OutfitColor(hider.kit));
                    bool hidden = hider.hidingSpot != null;
                    Dot(hider.x, hider.y, 22, hidden ? (Color?)null : outfitColor, hidden ? outfitColor : (Color?)null);
                    if (hider == doll.holder) Dot(hider.x, hider.y, 42, null, Ui.Rose);
                }
            }

            if (me != null && !me.outOfRound && me.x > 0)
            {
                Dot(me.x, me.y, 28, me.IsUnice ? UniceDot : Color.white, me.IsUnice ? Ui.Rose : Art.HexColor(Kit.OutfitColor(me.kit)));
                if (!me.IsUnice && doll.holder == me) Dot(me.x, me.y, 46, null, Ui.Rose);
            }
        }
    }
}
