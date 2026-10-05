using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// Debug: 武器の BoxCollider2D と本体の部位 Collider、握り点（橙の丸）と武器の重心（青の×）を字形に重ねて表示（C キー）。
    /// リリースビルドでは表示しない。
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class ColliderDebugView : MonoBehaviour
    {
        public static bool Visible;
        static bool Allowed => Application.isEditor || Debug.isDebugBuild;
        readonly List<(BoxCollider2D box, LineRenderer lr)> boxes = new List<(BoxCollider2D, LineRenderer)>();
        readonly List<(Collider2D col, LineRenderer lr)> parts = new List<(Collider2D, LineRenderer)>();
        readonly List<(Fighter f, LineRenderer grip, LineRenderer com)> markers = new List<(Fighter, LineRenderer, LineRenderer)>();

        public void Bind(MatchDirector d)
        {
            foreach (var f in d.Fighters)
            {
                foreach (var c in f.WeaponColliders)
                    if (c is BoxCollider2D b) boxes.Add((b, UiKit.Line(transform, "DbgBox", new Color(0f, 0.7f, 0.2f, 0.9f), 0.015f, 70, loop: true)));
                foreach (var c in f.BodyColliders)
                    parts.Add((c, UiKit.Line(transform, "DbgPart", new Color(0.6f, 0f, 0.8f, 0.8f), 0.015f, 70, loop: true)));
                markers.Add((f, UiKit.Line(transform, "DbgGrip", new Color(1f, 0.55f, 0f, 1f), 0.03f, 72, loop: true),
                    UiKit.Line(transform, "DbgCom", new Color(0f, 0.45f, 1f, 1f), 0.03f, 72)));
            }
        }

        void LateUpdate()
        {
            bool show = Visible && Allowed;
            foreach (var (f, grip, com) in markers)
            {
                if (!show || f == null) { grip.positionCount = 0; com.positionCount = 0; continue; }
                // 表示中の字形 Sprite（補間済み姿勢）の原点 = 握り点、重心は右向き基準のローカル値を向きで反転
                var t = f.WeaponSprite.transform;
                DrawGripMarker(grip, t.position);
                DrawComMarker(com, t.TransformPoint(new Vector3(f.Weapon.comLocal.x * f.Facing, f.Weapon.comLocal.y, 0f)));
            }
            foreach (var (box, lr) in boxes)
            {
                if (!show) { lr.positionCount = 0; continue; }
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
                if (!show) { lr.positionCount = 0; continue; }
                var bnd = col.bounds;
                lr.positionCount = 4;
                lr.SetPosition(0, new Vector3(bnd.min.x, bnd.min.y));
                lr.SetPosition(1, new Vector3(bnd.max.x, bnd.min.y));
                lr.SetPosition(2, new Vector3(bnd.max.x, bnd.max.y));
                lr.SetPosition(3, new Vector3(bnd.min.x, bnd.max.y));
            }
        }

        public static void DrawGripMarker(LineRenderer lr, Vector3 at, float radius = 0.07f)
        {
            lr.positionCount = 12;
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                lr.SetPosition(i, at + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
            }
        }

        public static void DrawComMarker(LineRenderer lr, Vector3 at, float size = 0.08f)
        {
            lr.positionCount = 5;
            lr.SetPosition(0, at + new Vector3(-size, -size));
            lr.SetPosition(1, at + new Vector3(size, size));
            lr.SetPosition(2, at);
            lr.SetPosition(3, at + new Vector3(-size, size));
            lr.SetPosition(4, at + new Vector3(size, -size));
        }
    }
}
