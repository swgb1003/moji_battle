using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>Printed ticket silhouette from the approved room-entry concept.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FriendRoomTicket : MaskableGraphic
    {
        protected override void Awake() { base.Awake(); raycastTarget = false; color = RetroUi.Ink; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 P(Vector2 p) => new Vector2((p.x / 478 - rectTransform.pivot.x) * rectTransform.rect.width,
                ((142 - p.y) / 142 - rectTransform.pivot.y) * rectTransform.rect.height);
            void Stroke(Vector2[] path, float width, bool loop = true)
            {
                for (int i = 0; i < path.Length - (loop ? 0 : 1); i++)
                {
                    Vector2 a = P(path[i]), b = P(path[(i + 1) % path.Length]);
                    Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * width * .5f;
                    int start = vh.currentVertCount;
                    vh.AddVert(a + n - d * .5f, color, Vector2.zero); vh.AddVert(b + n + d * .5f, color, Vector2.zero);
                    vh.AddVert(b - n + d * .5f, color, Vector2.zero); vh.AddVert(a - n - d * .5f, color, Vector2.zero);
                    vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
                }
            }
            Stroke(new[] { new Vector2(36, 8), new Vector2(442, 8), new Vector2(466, 31), new Vector2(466, 53),
                new Vector2(451, 58), new Vector2(445, 71), new Vector2(451, 84), new Vector2(466, 89), new Vector2(466, 111),
                new Vector2(442, 134), new Vector2(36, 134), new Vector2(12, 111), new Vector2(12, 89), new Vector2(27, 84),
                new Vector2(33, 71), new Vector2(27, 58), new Vector2(12, 53), new Vector2(12, 31) }, 7);
            Stroke(new[] { new Vector2(40, 22), new Vector2(438, 22), new Vector2(451, 37), new Vector2(451, 45),
                new Vector2(432, 59), new Vector2(432, 83), new Vector2(451, 97), new Vector2(451, 104),
                new Vector2(438, 120), new Vector2(40, 120), new Vector2(27, 104), new Vector2(27, 97),
                new Vector2(46, 83), new Vector2(46, 59), new Vector2(27, 45), new Vector2(27, 37) }, 1);
            for (int i = 0; i < 6; i++)
            {
                float x = 67 + i * 61;
                Stroke(new[] { new Vector2(x, 44), new Vector2(x + 50, 44), new Vector2(x + 50, 98), new Vector2(x, 98) }, 2.5f);
            }
        }
    }
}
