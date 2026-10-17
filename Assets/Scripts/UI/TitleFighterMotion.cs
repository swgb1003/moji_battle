using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>Quiet breathing and occasional practice swings, using the existing menu fighter mesh.</summary>
    public sealed class TitleFighterMotion : MonoBehaviour
    {
        public const float CycleSeconds = 8f;
        public RetroFighterArt BodyArt { get; private set; }
        public RectTransform Weapon { get; private set; }
        public bool Mirrored { get; private set; }
        RectTransform rect;
        Vector2 home, weaponHome, handHome;
        Quaternion weaponRotation;
        float elapsed;
        bool charging;

        public static TitleFighterMotion Create(Transform parent, float x, bool mirrored)
        {
            const float s = 1.16f;
            var root = RetroUi.Group(parent, mirrored ? "TitleFighterRight" : "TitleFighterLeft", x, 484, 360 * s, 420 * s);
            // A soft printed ground shadow replaces the raised display stand.
            var shadow = RetroUi.Group(root, "GroundShadow", mirrored ? 50 * s : 0, 401 * s, 310 * s, 18 * s).gameObject.AddComponent<TitleGroundShadow>();
            shadow.color = new Color(.14f, .12f, .09f, .18f);
            var body = RetroUi.Group(root, "BodyArt", 0, 0, 360 * s, 420 * s).gameObject.AddComponent<RetroFighterArt>();
            body.mirrored = mirrored; body.artScale = s;
            var weapon = RetroUi.Label(root, "GlyphWeapon", mirrored ? "山" : "火", (mirrored ? -55 : 65) * s,
                -30 * s, 350 * s, 280 * s, Mathf.RoundToInt(264 * s), mirrored ? RetroUi.Blue : RetroUi.Red, TextAnchor.MiddleCenter);
            weapon.font = GlyphFontLibrary.Default?.Get(FontStyleId.Brush)?.font ?? RetroUi.Font;
            weapon.rectTransform.localRotation = Quaternion.Euler(0, 0, mirrored ? 14 : -14);
            // The hand geometry draws above the glyph just as it does on the original title.
            weapon.transform.SetSiblingIndex(1);
            var motion = root.gameObject.AddComponent<TitleFighterMotion>();
            motion.rect = root; motion.home = root.anchoredPosition;
            motion.BodyArt = body; motion.Weapon = weapon.rectTransform; motion.Mirrored = mirrored;
            motion.weaponHome = motion.Weapon.anchoredPosition; motion.weaponRotation = motion.Weapon.localRotation;
            motion.handHome = body.TitlePoint(279, 205);
            motion.ApplyPose(0, 0, 0);
            return motion;
        }

        void Update()
        {
            if (charging) return;
            if (RetroMotionSettings.Reduced) { ApplyPose(0, 0, 0); return; }
            // Unscaled time keeps title motion independent of spectator pause/speed.
            if (!SettingsScreen.BlocksInput) elapsed += Time.unscaledDeltaTime;
            SampleIdle(elapsed);
        }

        public void SampleIdle(float seconds)
        {
            float cycle = Mathf.Repeat(seconds, CycleSeconds);
            float breath = Mathf.Sin(seconds * 1.6f + (Mirrored ? 1.7f : 0));
            float swing = Swing(cycle - (Mirrored ? 5.5f : 3.2f));
            // The other fighter acknowledges a swing with a small lean and guard adjustment.
            float reaction = Swing(cycle - (Mirrored ? 3.35f : 5.65f));
            ApplyPose(breath, swing, Mathf.Max(0, reaction));
        }

        static float Swing(float t)
        {
            if (t < 0 || t >= 1.1f) return 0;
            if (t < .24f) return Mathf.Lerp(0, -.22f, Mathf.SmoothStep(0, 1, t / .24f));
            if (t < .4f) return Mathf.Lerp(-.22f, .68f, Mathf.SmoothStep(0, 1, (t - .24f) / .16f));
            if (t < .55f) return .68f;
            return Mathf.Lerp(.68f, 0, Mathf.SmoothStep(0, 1, (t - .55f) / .55f));
        }

        void ApplyPose(float breath, float swing, float recoil)
        {
            BodyArt.SetTitlePose(breath, swing, recoil);
            float angle = (swing * 36 + breath * .7f) * (Mirrored ? 1 : -1);
            var rotation = Quaternion.Euler(0, 0, angle);
            Weapon.anchoredPosition = BodyArt.TitlePoint(279, 205) + (Vector2)(rotation * (weaponHome - handHome));
            Weapon.localRotation = rotation * weaponRotation;
        }

        public void SampleCharge(float progress)
        {
            charging = true;
            float t = Mathf.Clamp01(progress);
            float drive = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .12f) / .65f));
            rect.anchoredPosition = home + new Vector2((Mirrored ? -1 : 1) * 550 * drive,
                Mathf.Sin(drive * Mathf.PI) * 18);
            ApplyPose(0, -.12f * Mathf.Sin(drive * Mathf.PI), -.5f * drive);
        }
    }

    /// <summary>Low-opacity feathered ellipse, with no texture allocation.</summary>
    public sealed class TitleGroundShadow : MaskableGraphic
    {
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; Vector2 center = r.center;
            vh.AddVert(center, color, Vector2.zero);
            Color edge = color; edge.a = 0;
            const int n = 40;
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                vh.AddVert(center + new Vector2(Mathf.Cos(a) * r.width * .5f, Mathf.Sin(a) * r.height * .5f), edge, Vector2.zero);
                if (i > 0) vh.AddTriangle(0, i, i + 1);
            }
        }
    }
}
