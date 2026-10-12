using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>Raised controls without resizing their hit area or native browser input coordinates.</summary>
    public sealed class PaperFaceEffect : BaseMeshEffect
    {
        readonly List<UIVertex> source = new List<UIVertex>(48);
        float depth = PaperUi.ButtonDepth;
        float impactAge = 1;
        public void Impact() { impactAge = 0; if (graphic != null) graphic.SetVerticesDirty(); }
        void Update()
        {
            if (impactAge >= .3f) return;
            impactAge += Time.unscaledDeltaTime;
            if (graphic != null) graphic.SetVerticesDirty();
        }
        public float Depth => depth;
        public void SetDepth(float value)
        {
            if (Mathf.Abs(value - depth) < .15f) return;
            depth = Mathf.Max(0, value); if (graphic != null) graphic.SetVerticesDirty();
        }
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || graphic.color.a < .05f || vh.currentVertCount == 0) return;
            source.Clear(); vh.GetUIVertexStream(source); vh.Clear();
            Vector2 offset = new Vector2(depth * .7f, -depth);
            var rect = graphic.rectTransform.rect;
            // Duplicate the original geometry, preserving alpha on round sprites.
            foreach (var original in source)
            {
                var v = original; v.position += (Vector3)(offset + new Vector2(2, -3));
                var c = PaperUi.Shadow; c.a *= original.color.a / 255f; v.color = c;
                vh.AddVert(v);
            }
            for (int i = 0; i < source.Count; i += 3) vh.AddTriangle(i, i + 1, i + 2);
            int edgeStart = vh.currentVertCount;
            foreach (var original in source)
            {
                var v = original; v.position += (Vector3)offset;
                Color c = original.color; c.r *= .54f; c.g *= .50f; c.b *= .43f; v.color = c;
                vh.AddVert(v);
            }
            for (int i = 0; i < source.Count; i += 3) vh.AddTriangle(edgeStart + i, edgeStart + i + 1, edgeStart + i + 2);
            int start = vh.currentVertCount;
            float impact = RetroMotionSettings.Reduced || GameSettings.ReducedEffects || impactAge >= .3f ? 0
                : Mathf.Sin(impactAge * 38) * (1 - impactAge / .3f) * 2.5f;
            foreach (var original in source)
            {
                var v = original; Color c = v.color;
                v.position.x += impact;
                float t = Mathf.InverseLerp(rect.yMin, rect.yMax, v.position.y);
                float brightness = Mathf.Lerp(.93f, 1.07f, t);
                c.r = Mathf.Clamp01(c.r * brightness); c.g = Mathf.Clamp01(c.g * brightness); c.b = Mathf.Clamp01(c.b * brightness);
                v.color = c; vh.AddVert(v);
            }
            for (int i = 0; i < source.Count; i += 3) vh.AddTriangle(start + i, start + i + 1, start + i + 2);
        }
    }
}
