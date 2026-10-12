using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>One batched UI mesh: soft contact shadow, paper edges and a lightly shaded face.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PaperDepthGraphic : MaskableGraphic
    {
        public float depth = PaperUi.PanelDepth;
        public bool drawFace = true, fullPanel;
        float elevation;
        static Texture paper;
        public override Texture mainTexture => paper != null ? paper : paper = Resources.Load<Texture2D>("UI/PrintedPaper");
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        public void SetElevation(float value)
        {
            if (Mathf.Abs(elevation - value) < .1f) return;
            elevation = value; SetVerticesDirty();
        }
        void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color lower, Color upper, bool textured = false)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, lower, textured ? new Vector2(0, 0) : Vector2.zero);
            vh.AddVert(b, upper, textured ? new Vector2(0, 1) : Vector2.zero);
            vh.AddVert(c, upper, textured ? new Vector2(1, 1) : Vector2.zero);
            vh.AddVert(d, lower, textured ? new Vector2(1, 0) : Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            float d = depth + elevation;
            Vector2 off = new Vector2(d * .72f, -d);
            Vector2 bl = new Vector2(r.xMin, r.yMin), tl = new Vector2(r.xMin, r.yMax);
            Vector2 tr = new Vector2(r.xMax, r.yMax), br = new Vector2(r.xMax, r.yMin);
            // Without a face, the shadow would lie over the paper it supports and darken the whole board.
            for (int i = drawFace ? 3 : 0; i > 0; i--)
            {
                float spread = i * 3.5f + elevation * .15f;
                Vector2 drift = off + new Vector2(2, -4);
                var shade = PaperUi.Shadow; shade.a = i == 1 ? .13f : .045f;
                Quad(vh, bl + drift + new Vector2(-spread, -spread), tl + drift + new Vector2(-spread, spread),
                    tr + drift + new Vector2(spread, spread), br + drift + new Vector2(spread, -spread), shade, shade);
            }
            Quad(vh, bl + off, bl, br, br + off, PaperUi.Edge * new Color(.74f, .74f, .74f, 1), PaperUi.Edge);
            Quad(vh, br + off, br, tr, tr + off, PaperUi.Edge, new Color(.76f, .69f, .56f, 1));
            // Laminated sheets distinguish a paper edge from a solid slab.
            for (int i = 1; i <= 2; i++)
            {
                Vector2 p = off * (i / 3f);
                Quad(vh, bl + p, bl + p + Vector2.up, br + p + Vector2.up, br + p,
                    new Color(.91f, .85f, .73f, .85f), new Color(.91f, .85f, .73f, .85f));
            }
            if (!drawFace) return;
            Quad(vh, bl, tl, tr, br, new Color(.96f, .93f, .85f, 1), Color.white, true);
            Quad(vh, tl - Vector2.up * 2, tl, tr, tr - Vector2.up * 2, new Color(1, 1, 1, .8f), new Color(1, 1, 1, .8f));
        }
    }
}
