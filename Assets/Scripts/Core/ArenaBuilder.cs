using UnityEngine;

namespace MojiBattle
{
    /// <summary>固定アリーナ: 地面（y=0）と左右の壁（内面 x=±9、舞台幅18）。リングアウトなし。</summary>
    public static class ArenaBuilder
    {
        public static Transform Build(Transform parent, CombatBalance b)
        {
            var root = new GameObject("Arena").transform;
            root.SetParent(parent, false);
            var groundMat = new PhysicsMaterial2D("Ground") { friction = 0.6f, bounciness = 0f };
            var wallMat = new PhysicsMaterial2D("Wall") { friction = 0.2f, bounciness = 0.05f };
            float half = b.arenaHalfWidth;

            Surface(root, "Ground", ArenaSurface.SurfaceKind.Ground, new Vector2(0f, -1f), new Vector2(half * 2f + 4f, 2f), groundMat);
            Surface(root, "Wall_Left", ArenaSurface.SurfaceKind.Wall, new Vector2(-half - 0.5f, 6f), new Vector2(1f, 14f), wallMat);
            Surface(root, "Wall_Right", ArenaSurface.SurfaceKind.Wall, new Vector2(half + 0.5f, 6f), new Vector2(1f, 14f), wallMat);
            // 天井（場外なし。壁を越えて飛び出さない）
            Surface(root, "Ceiling", ArenaSurface.SurfaceKind.Wall, new Vector2(0f, 12.5f), new Vector2(half * 2f + 4f, 1f), wallMat);

            // 表示: 墨の地面線と壁、紙の床
            var ink = FighterFactory.Ink;
            var floor = UiKit.Rect(root, "FloorFill", new Color(0.86f, 0.83f, 0.77f), -10);
            floor.transform.position = new Vector3(0f, -2.5f, 0f);
            floor.transform.localScale = new Vector3(half * 2f + 6f, 5f, 1f);
            var line = UiKit.Rect(root, "GroundLine", ink, -5);
            line.transform.position = new Vector3(0f, -0.04f, 0f);
            line.transform.localScale = new Vector3(half * 2f + 0.6f, 0.08f, 1f);
            foreach (float sx in new[] { -1f, 1f })
            {
                var w = UiKit.Rect(root, "WallVisual", ink, -5);
                w.transform.position = new Vector3(sx * (half + 0.15f), 6f, 0f);
                w.transform.localScale = new Vector3(0.3f, 12.1f, 1f);
            }
            // 中央線（開始距離・間合いの目安）
            var center = UiKit.Rect(root, "CenterMark", new Color(ink.r, ink.g, ink.b, 0.25f), -6);
            center.transform.position = new Vector3(0f, -0.25f, 0f);
            center.transform.localScale = new Vector3(0.05f, 0.35f, 1f);
            return root;
        }

        static void Surface(Transform root, string name, ArenaSurface.SurfaceKind kind, Vector2 center, Vector2 size, PhysicsMaterial2D mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = size;
            box.sharedMaterial = mat;
            go.AddComponent<ArenaSurface>().Kind = kind;
        }
    }
}
