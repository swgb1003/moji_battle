using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>Small shaded sculptures drawn in the same UI batch; no additional camera or light.</summary>
    public static class PaperSculpture
    {
        static Color Shade(Color ink, float light)
        {
            return new Color(Mathf.Clamp01(ink.r * light), Mathf.Clamp01(ink.g * light), Mathf.Clamp01(ink.b * light), ink.a);
        }
        public static void Ball(VertexHelper vh, Vector2 center, float radius, Color ink)
        {
            const int steps = 24;
            int start = vh.currentVertCount;
            vh.AddVert(center, Shade(ink, 1.65f), Vector2.zero);
            for (int ring = 0; ring < 3; ring++)
            {
                float r = ring == 0 ? .35f : ring == 1 ? .72f : 1;
                for (int i = 0; i <= steps; i++)
                {
                    float a = i * Mathf.PI * 2 / steps;
                    Vector2 normal = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float light = Mathf.Lerp(1.4f, .34f, r * r) + Vector2.Dot(normal, new Vector2(-.55f, .8f)) * r * .45f;
                    vh.AddVert(center + normal * radius * r, Shade(ink, Mathf.Max(.28f, light)), Vector2.zero);
                }
            }
            for (int i = 0; i < steps; i++) vh.AddTriangle(start, start + 1 + i, start + 2 + i);
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < steps; i++)
                {
                    int a = start + 1 + ring * (steps + 1) + i, b = a + steps + 1;
                    vh.AddTriangle(a, b, a + 1); vh.AddTriangle(a + 1, b, b + 1);
                }
        }
        public static void Capsule(VertexHelper vh, Vector2 a, Vector2 b, float radius, Color ink)
        {
            Vector2 dir = (b - a).normalized, normal = new Vector2(-dir.y, dir.x);
            int start = vh.currentVertCount;
            for (int i = 0; i < 7; i++)
            {
                float across = i / 3f - 1;
                float light = .38f + Mathf.Sqrt(Mathf.Max(0, 1 - across * across)) * 1.25f;
                light += Vector2.Dot(normal, new Vector2(-.55f, .8f)) * across * .25f;
                vh.AddVert(a + normal * radius * across, Shade(ink, light), Vector2.zero);
                vh.AddVert(b + normal * radius * across, Shade(ink, light), Vector2.zero);
            }
            for (int i = 0; i < 6; i++)
            {
                int v = start + i * 2;
                vh.AddTriangle(v, v + 1, v + 2); vh.AddTriangle(v + 2, v + 1, v + 3);
            }
            Ball(vh, a, radius, ink); Ball(vh, b, radius, ink);
        }
    }
}
