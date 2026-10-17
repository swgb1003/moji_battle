using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
        /// <summary>Rounded, shaded UI sculptures using the shared paper stage material.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RetroFighterArt : MaskableGraphic
    {
        public bool trophy, mirrored;
        public float artScale = 1;
        float breath, swing, recoil;
        bool titlePose;

        // Title-only articulation; other menu sculptures retain their original pose.
        public void SetTitlePose(float breathing, float weaponSwing, float leanBack)
        {
            titlePose = true; breath = breathing; swing = weaponSwing; recoil = leanBack;
            SetVerticesDirty();
        }

        public Vector2 TitlePoint(float x, float y)
        {
            Vector2 p = new Vector2(x, y);
            if (titlePose)
            {
                float upper = Mathf.Clamp01((406 - y) / 100f);
                p += new Vector2((swing * 5 - recoil * 9) * upper, -breath * 2.2f * upper);
                if (x >= 200 && y < 300)
                {
                    Vector2 shoulder = new Vector2(141, 246);
                    Vector2 d = new Vector2(x, y) - shoulder;
                    float angle = (swing * 36 + breath * .7f) * Mathf.Deg2Rad;
                    p = shoulder + new Vector2(d.x * Mathf.Cos(angle) - d.y * Mathf.Sin(angle),
                        d.x * Mathf.Sin(angle) + d.y * Mathf.Cos(angle))
                        + new Vector2(swing * 5 - recoil * 9, -breath * 2.2f);
                }
                // Bend the knees slightly while the feet stay planted.
                if (y > 330 && y < 380) p += new Vector2(-breath * 1.2f, breath * 1.4f);
            }
            return new Vector2((mirrored ? 360 - p.x : p.x) * artScale, -p.y * artScale);
        }
        protected override void Awake() { base.Awake(); raycastTarget = false; color = RetroUi.Ink; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 P(float x, float y) => trophy ? new Vector2(x, -y) * artScale : TitlePoint(x, y);
            void Poly(params Vector2[] pts)
            {
                int start = vh.currentVertCount;
                foreach (var p in pts) vh.AddVert(p, color, Vector2.zero);
                for (int i = 1; i < pts.Length - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
            }
            if (!trophy)
            {
                Color ink = new Color(.12f, .135f, .15f, 1), dark = new Color(.08f, .09f, .105f, 1);
                void Limb(float ax, float ay, float bx, float by, float radius, bool back = false)
                    => PaperSculpture.Capsule(vh, P(ax, ay), P(bx, by), radius * artScale, back ? dark : ink);
                Limb(119, 306, 205, 346, 9, true); Limb(205, 346, 275, 406, 7, true);
                Limb(136, 265, 203, 287, 6, true); Limb(203, 287, 278, 217, 6, true);
                Limb(146, 236, 119, 306, 14);
                Limb(119, 306, 62, 354, 9); Limb(62, 354, 14, 406, 7);
                Limb(14, 406, 35, 410, 7); Limb(275, 406, 295, 410, 7, true);
                Limb(141, 246, 218, 265, 6); Limb(218, 265, 279, 205, 6);
                Limb(146, 236, 148, 218, 7);
                PaperSculpture.Ball(vh, P(148, 206), 24 * artScale, ink);
                PaperSculpture.Ball(vh, P(279, 205), 9 * artScale, ink);
                return;
            }
            Poly(P(24, 12), P(96, 12), P(88, 62), P(74, 77), P(46, 77), P(32, 62));
            Poly(P(52, 68), P(68, 68), P(68, 105), P(52, 105));
            Poly(P(36, 104), P(84, 104), P(90, 118), P(30, 118));
            Poly(P(10, 23), P(30, 23), P(30, 32), P(19, 32), P(22, 49), P(42, 59), P(42, 68), P(15, 56));
            Poly(P(90, 23), P(110, 23), P(105, 56), P(78, 68), P(78, 59), P(98, 49), P(101, 32), P(90, 32));
        }
    }
}
