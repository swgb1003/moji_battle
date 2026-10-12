using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>A symmetric trophy and eight-player bracket with screen-space antialiased edges.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TournamentMenuGraphic : MaskableGraphic
    {
        float drawnScale;
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        void LateUpdate()
        {
            if (canvas != null && !Mathf.Approximately(drawnScale, canvas.scaleFactor)) SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            drawnScale = canvas != null ? canvas.scaleFactor : 1;
            float contentScale=Mathf.Min(rectTransform.rect.width/508f,rectTransform.rect.height/250f);
            float feather = 1 / Mathf.Max(.1f, drawnScale*contentScale);
            Vector2 P(float x, float y) => new Vector2(x, -y);
            void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
            {
                int index = vh.currentVertCount;
                vh.AddVert(a, ca, Vector2.zero); vh.AddVert(b, cb, Vector2.zero);
                vh.AddVert(c, cc, Vector2.zero); vh.AddVert(d, cd, Vector2.zero);
                vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
            }
            Vector2 Join(Vector2 before, Vector2 after, float distance)
            {
                var sum = before + after;
                return sum * (distance / Mathf.Max(.1f, Vector2.Dot(sum, after)));
            }
            Vector2 Normal(Vector2 delta) => new Vector2(-delta.y, delta.x).normalized;
            // Solid shapes here are convex. A one-pixel transparent fringe smooths their silhouette.
            void Fill(Color ink, params Vector2[] points)
            {
                int start = vh.currentVertCount;
                foreach (var point in points) vh.AddVert(point, ink, Vector2.zero);
                for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
                Color clear = new Color(ink.r, ink.g, ink.b, 0);
                var outside = new Vector2[points.Length];
                for (int i = 0; i < points.Length; i++)
                    outside[i] = points[i] + Join(Normal(points[i] - points[(i + points.Length - 1) % points.Length]),
                        Normal(points[(i + 1) % points.Length] - points[i]), feather);
                for (int i = 0; i < points.Length; i++)
                {
                    int next = (i + 1) % points.Length;
                    Quad(points[i], points[next], outside[next], outside[i], ink, ink, clear, clear);
                }
            }
            void Box(float x, float y, float width, float height, Color ink)
                => Fill(ink, P(x, y), P(x + width, y), P(x + width, y + height), P(x, y + height));
            // Continuous strips avoid the overlapping triangles of a concave polygon fan.
            void Stroke(Color ink, float width, bool closed, params Vector2[] points)
            {
                var normals = new Vector2[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    var before = Normal(points[i] - points[(i + points.Length - 1) % points.Length]);
                    var after = Normal(points[(i + 1) % points.Length] - points[i]);
                    normals[i] = !closed && i == 0 ? after : !closed && i == points.Length - 1 ? before : Join(before, after, 1);
                }
                Color clear = new Color(ink.r, ink.g, ink.b, 0);
                float half = width * .5f;
                for (int i = 0; i < (closed ? points.Length : points.Length - 1); i++)
                {
                    int next = (i + 1) % points.Length;
                    Vector2 a = points[i], b = points[next], na = normals[i], nb = normals[next];
                    Quad(a + na * half, b + nb * half, b - nb * half, a - na * half, ink, ink, ink, ink);
                    Quad(a + na * half, a + na * (half + feather), b + nb * (half + feather), b + nb * half, ink, clear, clear, ink);
                    Quad(a - na * half, b - nb * half, b - nb * (half + feather), a - na * (half + feather), ink, ink, clear, clear);
                }
                if (closed) return;
                foreach (int end in new[] { 0, points.Length - 1 })
                {
                    Vector2 point = points[end], normal = normals[end];
                    Vector2 outward = (point - points[end == 0 ? 1 : end - 1]).normalized * feather;
                    Quad(point + normal * half, point - normal * half, point - normal * half + outward,
                        point + normal * half + outward, ink, ink, clear, clear);
                }
            }
            Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
            {
                float u = 1 - t;
                return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
            }
            var trophy = new List<Vector2> { P(224, 8), P(284, 8) };
            for (int i = 1; i < 24; i++) trophy.Add(Bezier(P(284, 8), P(282, 73), P(226, 73), P(224, 8), i / 24f));
            for (int side = 0; side < 2; side++)
            {
                float X(float x) => side == 0 ? x : 508 - x;
                var handle = new List<Vector2> { P(X(226), 19), P(X(216), 19) };
                for (int i = 1; i <= 16; i++) handle.Add(Bezier(P(X(216), 19), P(X(212), 40), P(X(224), 47), P(X(239), 49), i / 16f));
                Stroke(RetroUi.Ink, 6, false, handle.ToArray());
            }
            Box(248, 51, 12, 26, RetroUi.Ink);
            Fill(RetroUi.Ink, trophy.ToArray());
            Box(234, 76, 40, 8, RetroUi.Ink);

            // All rounds share one coordinate space and consistent stroke width.
            const float weight = 2.5f;
            float[] entries = { 86, 134, 182, 230, 278, 326, 374, 422 };
            for (int i = 0; i < 4; i++)
            {
                float left = entries[i * 2], right = entries[i * 2 + 1], middle = (left + right) * .5f;
                Stroke(RetroUi.Ink, weight, false, P(left, 206), P(left, 180), P(right, 180), P(right, 206));
                Stroke(RetroUi.Ink, weight, false, P(middle, 180), P(middle, 152));
            }
            for (int i = 0; i < 2; i++)
            {
                float left = 110 + i * 192, right = left + 96;
                Stroke(RetroUi.Ink, weight, false, P(left, 152), P(right, 152));
                Stroke(RetroUi.Ink, weight, false, P((left + right) * .5f, 152), P((left + right) * .5f, 124));
            }
            Stroke(RetroUi.Ink, weight, false, P(158, 124), P(350, 124));
            for (int i = 0; i < entries.Length; i++)
            {
                float x = entries[i];
                if (i % 3 != 1) Box(x - 8, 207, 16, 18, RetroUi.Red);
                Stroke(RetroUi.Ink, 2, true, P(x - 9, 206), P(x + 9, 206), P(x + 9, 226), P(x - 9, 226));
            }
            // Fit the entire authored illustration inside either a three-column or four-column card.
            Vector3 offset=new Vector3((rectTransform.rect.width-508*contentScale)*.5f,-(rectTransform.rect.height-250*contentScale)*.5f,0);
            for(int i=0;i<vh.currentVertCount;i++)
            {var vertex=new UIVertex();vh.PopulateUIVertex(ref vertex,i);vertex.position=vertex.position*contentScale+offset;vh.SetUIVertex(vertex,i);}
        }
    }
}
