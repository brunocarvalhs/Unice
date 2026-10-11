using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unice
{
    /// <summary>One character on screen: a paper-doll child (tinted layers) or the Unice, plus shadow, ring (you) and doll icon.</summary>
    public sealed class CharacterView
    {
        const float HitDuration = .35f;

        readonly GameObject root;
        readonly SortingGroup sortingGroup;
        readonly SpriteRenderer shadow, ring, dollIcon, uniceSprite;
        readonly Dictionary<string, SpriteRenderer> kidLayers = new Dictionary<string, SpriteRenderer>();
        readonly (string file, Color tint)[] kidLayerSprites = new (string, Color)[KidSprites.LayerOrder.Length];
        float hitTime = -99;

        public CharacterView(Transform parent, int index)
        {
            root = new GameObject("character" + index); root.transform.SetParent(parent, false);
            sortingGroup = root.AddComponent<SortingGroup>();
            shadow = Art.CreateSpriteRenderer("shadow", root.transform, Art.Centered("shadow", 5.6f), 0); shadow.color = new Color(0, 0, 0, .45f);
            ring = Art.CreateSpriteRenderer("ring", root.transform, Art.Centered("ring", 8), 1); ring.color = new Color(.94f, .69f, .34f, .7f);
            ring.transform.localScale = new Vector3(1, .34f, 1);
            for (int i = 0; i < KidSprites.LayerOrder.Length; i++)
            {
                var layer = Art.CreateSpriteRenderer(KidSprites.LayerOrder[i], root.transform, null, 10 + i);
                layer.sharedMaterial = Art.PixelMat;
                kidLayers[KidSprites.LayerOrder[i]] = layer;
            }
            uniceSprite = Art.CreateSpriteRenderer("unice", root.transform, null, 40); uniceSprite.sharedMaterial = Art.PixelMat;
            dollIcon = Art.CreateSpriteRenderer("doll", root.transform, Art.Centered("doll", Art.Manifest.charScale), 50);
            dollIcon.transform.localPosition = new Vector3(.12f, .46f, 0);
            root.SetActive(false);
        }

        public void Hide() { if (root.activeSelf) root.SetActive(false); }

        /// <summary>Starts the hit punch (scale pop + white flash).</summary>
        public void Hit() { hitTime = Time.unscaledTime; }

        /// <param name="isMe">Draws the ring under this device's own character.</param>
        /// <param name="showDoll">Draws the doll in its hands (only for those allowed to know who has it).</param>
        /// <param name="stepBlend">How far between the last two fixed steps to draw it (see <see cref="Character.DrawX"/>).</param>
        public void Show(Character character, bool isMe, float alpha, bool showDoll, float time, float stepBlend)
        {
            if (!root.activeSelf) root.SetActive(true);
            if (character.faceX > .3f) character.facingSign = 1; else if (character.faceX < -.3f) character.facingSign = -1;
            bool showBack = character.faceY < -.6f && Mathf.Abs(character.faceX) < .55f;
            float walkBob = character.IsUnice || !character.moving ? 0 : Mathf.Abs(Mathf.Sin(time * 13 + character.index)) * 1.2f;
            float drawY = character.DrawY(stepBlend);
            root.transform.localPosition = Art.ToWorld(character.DrawX(stepBlend), drawY - walkBob);
            sortingGroup.sortingOrder = Art.SortingOrder(drawY, 2);
            shadow.transform.localPosition = new Vector3(0, walkBob / Art.PxPerUnit, 0);
            ring.gameObject.SetActive(isMe && alpha >= 1);
            dollIcon.gameObject.SetActive(showDoll);
            // the doll blinks while it can't be passed again
            if (showDoll) dollIcon.color = new Color(1, 1, 1, character.passCooldown > 0 ? .5f + .5f * Mathf.Abs(Mathf.Sin(time * 8)) : 1);

            float hit = Mathf.Max(0, 1 - (time - hitTime) / HitDuration);
            root.transform.localScale = Vector3.one * (1 + hit * hit * .4f);

            if (character.IsUnice) ShowUnice(character, showBack, alpha, hit, time);
            else ShowKid(character, showBack, alpha, hit, time);
        }

        void ShowUnice(Character character, bool showBack, float alpha, float hit, float time)
        {
            foreach (var layer in kidLayers.Values) layer.enabled = false;
            uniceSprite.enabled = true;
            int frame = Mathf.FloorToInt(time * (character.moving ? 8f : 3f)) % Art.Manifest.unice.frames;   // 8 frames = one stride (two steps)
            uniceSprite.sprite = Art.UniceFrame($"unice_{(character.moving ? "m" : "i")}_{(showBack ? "b" : "s")}_{frame}");
            uniceSprite.flipX = character.facingSign < 0;
            uniceSprite.transform.localPosition = Vector3.zero;
            uniceSprite.color = Color.Lerp(new Color(1, 1, 1, alpha), Color.white, hit * .7f);
            shadow.transform.localScale = new Vector3(1.1f, 1.1f, 1);
        }

        void ShowKid(Character character, bool showBack, float alpha, float hit, float time)
        {
            uniceSprite.enabled = false; shadow.transform.localScale = Vector3.one;
            int frame = character.moving ? 1 + Mathf.FloorToInt(time * 13 / (Mathf.PI * 2) * 6) % 6 : 0;
            KidSprites.Build(character.kit, showBack, frame, kidLayerSprites);
            for (int i = 0; i < KidSprites.LayerOrder.Length; i++)
            {
                var (file, tint) = kidLayerSprites[i];
                SetLayer(kidLayers[KidSprites.LayerOrder[i]], file, Color.Lerp(tint, Color.white, hit * .7f), alpha, character.facingSign);
            }
        }

        static void SetLayer(SpriteRenderer layer, string file, Color tint, float alpha, int facingSign)
        {
            if (file == null) { layer.enabled = false; return; }
            layer.enabled = true; layer.sprite = Art.CharacterSprite(file); layer.flipX = facingSign < 0;
            tint.a = alpha; layer.color = tint;
        }
    }
}
