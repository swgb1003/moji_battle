using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>墨の筆線と床を走る衝撃波。描画補間は当たり判定を変更しない。</summary>
    [DefaultExecutionOrder(115)]
    public sealed class RangedProjectileView : MonoBehaviour
    {
        MatchDirector director;
        readonly Dictionary<RangedProjectile, Transform> views = new Dictionary<RangedProjectile, Transform>();
        readonly List<RangedProjectile> expired = new List<RangedProjectile>();
        public void Bind(MatchDirector match) { director = match; }

        Transform Create(RangedProjectile p)
        {
            var root = new GameObject(p.Style == AttackStyle.GroundWave ? "GroundShockwave" : "FlyingStroke").transform;
            root.SetParent(transform, false);
            Color team = FighterFactory.TeamColor(p.Owner.Id);
            bool wave = p.Style == AttackStyle.GroundWave;
            Draw(root, "InkEdge", FighterFactory.Ink, wave ? .085f : .13f,
                wave ? new[] { new Vector2(-.5f, -.23f), new Vector2(-.2f, -.08f), new Vector2(.05f, .22f), new Vector2(.25f, .05f), new Vector2(.32f, -.23f) }
                     : new[] { new Vector2(-.52f, -.03f), new Vector2(-.12f, .02f), new Vector2(.35f, 0f), new Vector2(.49f, .015f) });
            Draw(root, "TeamStroke", team, wave ? .045f : .065f,
                wave ? new[] { new Vector2(-.43f, -.2f), new Vector2(-.17f, -.05f), new Vector2(.06f, .18f), new Vector2(.27f, -.16f) }
                     : new[] { new Vector2(-.43f, .015f), new Vector2(-.1f, .02f), new Vector2(.37f, .03f) });
            for (int i = 0; i < 3; i++)
            {
                var color = team; color.a = .45f - i * .1f;
                float x = -.6f - i * .24f, y = wave ? -.23f + i * .025f : -.14f + i * .14f;
                Draw(root, "BrushTrail", color, .035f,
                    new[] { new Vector2(x - .25f, y), new Vector2(x, y + .025f) });
            }
            root.localScale = new Vector3(Mathf.Sign(p.VelocityX), 1f, 1f);
            return root;
        }

        static void Draw(Transform root, string name, Color color, float width, Vector2[] points)
        {
            var line = UiKit.Line(root, name, color, width, 16);
            line.useWorldSpace = false;
            line.numCapVertices = 2;
            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        }

        void LateUpdate()
        {
            if (director == null || director.Context == null) return;
            var active = director.Context.Ranged.Projectiles;
            float alpha = Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            foreach (var p in active)
            {
                if (!views.TryGetValue(p, out var view)) { view = Create(p); views.Add(p, view); }
                Vector2 pos = Vector2.Lerp(p.PreviousPosition, p.Position, alpha);
                view.position = new Vector3(pos.x, pos.y, -.3f);
            }
            expired.Clear();
            foreach (var entry in views)
            {
                bool found = false;
                foreach (var p in active) if (p == entry.Key) { found = true; break; }
                if (!found) expired.Add(entry.Key);
            }
            foreach (var p in expired) { Destroy(views[p].gameObject); views.Remove(p); }
        }
    }
}
