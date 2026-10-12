using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>Extrudes actual glyph/logo alpha, so lettering stays sharp and holes remain open.</summary>
    public sealed class PaperInkRelief : BaseMeshEffect
    {
        public float depth = 3;
        readonly List<UIVertex> source = new List<UIVertex>(2048);
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            source.Clear(); vh.GetUIVertexStream(source); vh.Clear();
            for (int pass = 2; pass >= 0; pass--)
            {
                int start = vh.currentVertCount;
                foreach (var original in source)
                {
                    var v = original;
                    if (pass > 0)
                    {
                        v.position += new Vector3(depth * .7f * pass / 2, -depth * pass / 2, 0);
                        Color c = original.color;
                        c.r *= .48f; c.g *= .45f; c.b *= .38f; c.a *= pass == 2 ? .35f : .8f; v.color = c;
                    }
                    vh.AddVert(v);
                }
                for (int i = 0; i < source.Count; i += 3) vh.AddTriangle(start + i, start + i + 1, start + i + 2);
            }
        }
    }
}
