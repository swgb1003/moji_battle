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
        protected override void Awake() { base.Awake(); raycastTarget = false; color = RetroUi.Ink; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 P(float x, float y) => new Vector2((mirrored && !trophy ? 360 - x : x) * artScale, -y * artScale);
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
