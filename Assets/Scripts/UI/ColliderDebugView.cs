using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>Debug: 武器の BoxCollider2D と本体の部位 Collider を字形に重ねて表示（C キー）。</summary>
    [DefaultExecutionOrder(120)]
    public sealed class ColliderDebugView : MonoBehaviour
    {
        public static bool Visible;
        readonly List<(BoxCollider2D box, LineRenderer lr)> boxes = new List<(BoxCollider2D, LineRenderer)>();
        readonly List<(Collider2D col, LineRenderer lr)> parts = new List<(Collider2D, LineRenderer)>();

        public void Bind(MatchDirector d)
        {
            foreach (var f in d.Fighters)
            {
                foreach (var c in f.WeaponColliders)
                    if (c is BoxCollider2D b) boxes.Add((b, UiKit.Line(transform, "DbgBox", new Color(0f, 0.7f, 0.2f, 0.9f), 0.015f, 70, loop: true)));
                foreach (var c in f.BodyColliders)
                    parts.Add((c, UiKit.Line(transform, "DbgPart", new Color(0.6f, 0f, 0.8f, 0.8f), 0.015f, 70, loop: true)));
            }
        }

        void LateUpdate()
        {
            foreach (var (box, lr) in boxes)
            {
                if (!Visible) { lr.positionCount = 0; continue; }
                var t = box.transform;
                Vector2 o = box.offset, h = box.size * 0.5f;
                lr.positionCount = 4;
                lr.SetPosition(0, t.TransformPoint(o + new Vector2(-h.x, -h.y)));
                lr.SetPosition(1, t.TransformPoint(o + new Vector2(h.x, -h.y)));
                lr.SetPosition(2, t.TransformPoint(o + new Vector2(h.x, h.y)));
                lr.SetPosition(3, t.TransformPoint(o + new Vector2(-h.x, h.y)));
            }
            foreach (var (col, lr) in parts)
            {
                if (!Visible) { lr.positionCount = 0; continue; }
                var bnd = col.bounds;
                lr.positionCount = 4;
                lr.SetPosition(0, new Vector3(bnd.min.x, bnd.min.y));
                lr.SetPosition(1, new Vector3(bnd.max.x, bnd.min.y));
                lr.SetPosition(2, new Vector3(bnd.max.x, bnd.max.y));
                lr.SetPosition(3, new Vector3(bnd.min.x, bnd.max.y));
            }
        }
    }
}
