using System;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>A deterministic, real-time ceremony using the player's actual component masks.</summary>
    public sealed class FusionBirthEffect : MonoBehaviour
    {
        public const float Duration = 3.8f;
        public float Elapsed { get; private set; }
        public bool ImpactReached { get; private set; }
        public bool Reduced { get; private set; }
        RectTransform left, right, result, stage, stamp;
        CanvasGroup resultAlpha, stampAlpha, all;
        Image flash;
        FusionBurstGraphic burst;
        Text subtitle;
        Action complete;
        AudioSource audioSource;

        public static FusionBirthEffect Play(Transform parent, GlyphDefinitionRuntime l, GlyphDefinitionRuntime r, GlyphDefinitionRuntime whole, Action finished)
        {
            var root = RetroUi.Group(parent, "FusionBirth", 0, 0, 1920, 1080);
            var effect = root.gameObject.AddComponent<FusionBirthEffect>(); effect.complete = finished;
            effect.Reduced = RetroMotionSettings.Reduced || GameSettings.ReducedEffects;
            var bg = root.gameObject.AddComponent<Image>(); bg.color = new Color32(18, 17, 15, 255); bg.raycastTarget = true;
            effect.all = root.gameObject.AddComponent<CanvasGroup>();
            var paper = WorldCupDesign.Art(root, "design_paper", 0, 0, 1920, 1080, false); paper.color = new Color(.5f, .35f, .2f, .16f);
            RetroUi.Frame(root, 38, 38, 1844, 1004, new Color(.8f, .62f, .3f, .6f), 2);
            var eyebrow = RetroUi.Label(root, "BirthEyebrow", "KANJI  /  AWAKENING", 300, 88, 1320, 70, 34, new Color32(219, 179, 97, 255), TextAnchor.MiddleCenter);
            effect.stage = RetroUi.Group(root, "BirthStage", 0, 0, 1920, 1080);
            effect.burst = UguiKit.Fill(effect.stage, "InkGoldBurst").gameObject.AddComponent<FusionBurstGraphic>(); effect.burst.raycastTarget = false;
            RawImage Glyph(string name, GlyphDefinitionRuntime g, float size, Color color)
            {
                var image = RetroUi.Group(effect.stage, name, 960 - size / 2, 480 - size / 2, size, size).gameObject.AddComponent<RawImage>();
                image.texture = g.texture; image.color = color; image.raycastTarget = false;
                image.rectTransform.pivot = Vector2.one * .5f; image.rectTransform.anchoredPosition = new Vector2(960, -480); return image;
            }
            effect.left = Glyph("BirthLeft", l, 610, RetroUi.Paper).rectTransform;
            effect.right = Glyph("BirthRight", r, 610, RetroUi.Paper).rectTransform;
            effect.result = RetroUi.Group(effect.stage, "BornGlyph", 960, 480, 630, 630); effect.result.pivot = Vector2.one * .5f;
            effect.resultAlpha = effect.result.gameObject.AddComponent<CanvasGroup>();
            var shadow = RetroUi.Group(effect.result, "GlyphDepth", 14, 16, 630, 630).gameObject.AddComponent<RawImage>(); shadow.texture = whole.texture; shadow.color = new Color32(83, 17, 11, 255); shadow.raycastTarget = false;
            var face = RetroUi.Group(effect.result, "GlyphFace", 0, 0, 630, 630).gameObject.AddComponent<RawImage>(); face.texture = whole.texture; face.color = new Color32(221, 64, 39, 255); face.raycastTarget = false;
            // A centered root with top-left children keeps the overshoot centered on the glyph.
            effect.stamp = RetroUi.Group(root, "BirthStamp", 510, 800, 900, 124); effect.stampAlpha = effect.stamp.gameObject.AddComponent<CanvasGroup>();
            RetroUi.Frame(effect.stamp, 0, 0, 900, 124, RetroUi.Red, 5);
            RetroUi.Block(effect.stamp, "StampInk", 5, 5, 890, 114, RetroUi.Red);
            var title = RetroUi.Label(effect.stamp, "BirthTitle", "新文字、誕生", 8, 0, 884, 124, 94, RetroUi.Paper, TextAnchor.MiddleCenter); title.font = WorldCupDesign.HeadingFont;
            effect.subtitle = RetroUi.Label(root, "BirthSubtitle", "二つの力が、ひとつになる。", 300, 960, 1320, 50, 30, RetroUi.Paper, TextAnchor.MiddleCenter);
            effect.flash = RetroUi.Block(root, "BirthFlash", 0, 0, 1920, 1080, Color.clear); effect.flash.raycastTarget = false;
            effect.audioSource = root.gameObject.AddComponent<AudioSource>(); effect.audioSource.playOnAwake = false; effect.audioSource.spatialBlend = 0;
            effect.Sound("charge", .7f); effect.Render(); return effect;
        }
        void Update()
        {
            if (SettingsScreen.BlocksInput) return;
            Elapsed += Time.unscaledDeltaTime;
            if (!ImpactReached && Elapsed >= (Reduced ? .45f : 1.35f))
            { ImpactReached = true; Sound("gong_start", .85f); Sound("special_impact", .7f); }
            Render();
            if (Elapsed >= (Reduced ? 1.5f : Duration))
            {
                var callback = complete; complete = null; gameObject.SetActive(false); Destroy(gameObject); callback?.Invoke();
            }
        }
        void Sound(string clip, float volume)
        {
            if (!TimeController.PresentationEffectsEnabled || GameSettings.EffectsGain <= 0) return;
            var asset = Resources.Load<AudioClip>("Audio/Combat/" + clip);
            if (asset != null) audioSource.PlayOneShot(asset, volume * GameSettings.EffectsGain);
        }
        void Render()
        {
            float hit = Reduced ? .45f : 1.35f, t = Elapsed - hit;
            float suction = Mathf.Clamp01(Elapsed / hit); float d = Reduced ? Mathf.Lerp(150, 0, suction) : 560 * (1 - suction * suction * suction);
            float swirl = Reduced ? 0 : Mathf.Sin(suction * Mathf.PI * 2) * 150 * (1 - suction);
            left.anchoredPosition = new Vector2(960 - d, -480 + swirl); right.anchoredPosition = new Vector2(960 + d, -480 - swirl);
            left.localRotation = Quaternion.Euler(0, 0, Reduced ? 0 : (1 - suction) * 24); right.localRotation = Quaternion.Euler(0, 0, Reduced ? 0 : -(1 - suction) * 24);
            left.gameObject.SetActive(t < .03f); right.gameObject.SetActive(t < .03f);
            resultAlpha.alpha = t >= 0 ? Mathf.Clamp01(t * 15) : 0;
            float size = Reduced ? 1 : 1 + Mathf.Exp(-Mathf.Max(t, 0) * 5) * Mathf.Cos(Mathf.Max(t, 0) * 16) * .45f;
            result.localScale = Vector3.one * size;
            stampAlpha.alpha = Mathf.Clamp01((t - .4f) * 4);
            stamp.localRotation = Quaternion.Euler(0, 0, Reduced ? 0 : Mathf.Lerp(-8, -3, Mathf.Clamp01((t - .4f) * 4)));
            float pulse = Reduced ? 0 : Mathf.Clamp01(1 - Mathf.Abs(t - .035f) / .075f) * .9f;
            flash.color = new Color(1, .91f, .66f, pulse);
            if (!Reduced && t >= 0)
            {
                float shake = Mathf.Exp(-t * 7) * 24;
                stage.anchoredPosition = new Vector2(Mathf.Sin(t * 83) * shake, Mathf.Cos(t * 107) * shake);
            }
            burst.TimeAfterImpact = t; burst.Reduced = Reduced; burst.SetVerticesDirty();
            subtitle.text = t < 0 ? "二つの力が、ひとつになる。" : "その文字は、あなただけの武器になる。";
            float ending = Reduced ? 1.5f : Duration; all.alpha = Mathf.Clamp01((ending - Elapsed) / .25f);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FusionBurstGraphic : MaskableGraphic
    {
        public float TimeAfterImpact;
        public bool Reduced;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); float t = TimeAfterImpact;
            var center = rectTransform.rect.center + new Vector2(0, 60); Color gold = new Color(1, .72f, .24f, .9f);
            if (t < 0)
            {
                if (Reduced) return;
                float u = Mathf.Clamp01((t + 1.35f) / 1.35f);
                Ring(vh, center, Mathf.Lerp(690, 100, u * u), 3, new Color(.95f, .65f, .2f, u * .6f));
                for (int i = 0; i < 36; i++)
                {
                    float angle = i * 2.39996f + u * 3; var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float radius = Mathf.Lerp(720 + Hash(i) * 200, 40, u);
                    Quad(vh, center + dir * radius, dir * 32, new Vector2(-dir.y, dir.x) * 2, new Color(1, .75f, .25f, u));
                }
                return;
            }
            if (Reduced) { Ring(vh, center, 290 + t * 80, 3, new Color(.95f, .65f, .2f, Mathf.Clamp01(1 - t))); return; }
            float fade = Mathf.Clamp01(1 - t / 2.2f);
            // Ink splashes persist behind the new character while gold sparks fly beyond it.
            for (int i = 0; i < 44; i++)
            {
                float a = i * 2.39996f; var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float radius = (240 + Hash(i + 80) * 200) * Mathf.Clamp01(t * 8);
                Disc(vh, center + dir * radius, (5 + Hash(i + 20) * 36) * fade, new Color(.64f, .12f, .065f, .9f * fade));
            }
            Ring(vh, center, 100 + t * 780, Mathf.Max(1, 20 * (1 - t)), new Color(1, .75f, .3f, Mathf.Clamp01(1 - t * 1.1f)));
            Ring(vh, center, 100 + t * 500, 3, new Color(1, .9f, .6f, Mathf.Clamp01(1 - t * .8f)));
            for (int i = 0; i < 96; i++)
            {
                float a = i * 2.39996f; var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float speed = 240 + Hash(i + 7) * 850, radius = 80 + speed * (1 - Mathf.Exp(-t * 2));
                float len = (10 + Hash(i + 43) * 150) * Mathf.Exp(-t * 2);
                gold.a = fade * (.4f + Hash(i + 12) * .6f);
                Quad(vh, center + dir * radius + Vector2.down * t * t * 100, dir * len, new Vector2(-dir.y, dir.x) * (1 + Hash(i + 33) * 3), gold);
            }
        }
        static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 127.1f + 71.7f) * 43758.5453f, 1);
        static void Quad(VertexHelper v, Vector2 c, Vector2 a, Vector2 b, Color col)
        {
            int n = v.currentVertCount; v.AddVert(c - a - b, col, Vector2.zero); v.AddVert(c + a - b, col, Vector2.zero);
            v.AddVert(c + a + b, col, Vector2.zero); v.AddVert(c - a + b, col, Vector2.zero); v.AddTriangle(n, n + 1, n + 2); v.AddTriangle(n, n + 2, n + 3);
        }
        static void Disc(VertexHelper v, Vector2 c, float radius, Color color)
        {
            int n = v.currentVertCount; v.AddVert(c, color, Vector2.zero);
            for (int i = 0; i <= 10; i++) { float a = i * Mathf.PI / 5; v.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, Vector2.zero); }
            for (int i = 0; i < 10; i++) v.AddTriangle(n, n + i + 1, n + i + 2);
        }
        static void Ring(VertexHelper v, Vector2 c, float radius, float thickness, Color color)
        {
            for (int i = 0; i < 96; i++)
            {
                float a = (i + .5f) * Mathf.PI * 2 / 96; var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Quad(v, c + dir * radius, new Vector2(-dir.y, dir.x) * radius * Mathf.PI / 96, dir * thickness / 2, color);
            }
        }
    }
}
