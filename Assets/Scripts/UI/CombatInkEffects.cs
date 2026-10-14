using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>Bounded, reusable brush strokes and paper scars, driven by combat events.</summary>
    [DefaultExecutionOrder(120)]
    public sealed class CombatInkEffects : MonoBehaviour
    {
        const int StrokeLimit = 64, ScarLimit = 24;
        sealed class Stroke { public LineRenderer line; public float age, life, width; public Color color; public Vector3 origin; public bool expand; }
        readonly List<Stroke> strokes = new List<Stroke>();
        readonly Queue<GameObject> scars = new Queue<GameObject>();
        readonly Vector3[][] trailPoints = { new Vector3[12], new Vector3[12] };
        readonly int[] trailCounts = new int[2];
        readonly float[] nextLand = new float[2];
        readonly int[] trailIds = { -1, -1 };
        readonly Vector2[] tips = new Vector2[2];
        readonly LineRenderer[] trails = new LineRenderer[2];
        MatchDirector director;
        float nextBind, trailSampleAt;
        public int ScarCount => scars.Count;
        public int ActiveStrokeCount { get; private set; }
        bool EnabledEffects => isActiveAndEnabled && director != null && !director.SilentSimulation
            && TimeController.PresentationEffectsEnabled && !GameSettings.ReducedEffects;

        public void Bind(MatchDirector d)
        {
            director = d;
            d.Events.Hit += OnHit; d.Events.Guard += OnGuard;
            d.Events.SurfaceContact += OnSurface; d.Events.EnvImpact += OnImpact; d.Events.Motion += OnMotion;
            for (int i = 0; i < 2; i++)
            {
                var bounds = d.Fighters[i].Weapon.boundsLocal;
                tips[i] = bounds.max;
                foreach (var p in new[] { bounds.min, new Vector2(bounds.xMin, bounds.yMax), new Vector2(bounds.xMax, bounds.yMin) })
                    if (p.sqrMagnitude > tips[i].sqrMagnitude) tips[i] = p;
                trails[i] = UiKit.Line(transform, "BrushTrail" + i, FighterFactory.TeamColor(i), .16f, 9);
                trails[i].widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.3f, .8f), new Keyframe(1, 0));
                trails[i].numCapVertices = 3;
            }
        }
        void OnDestroy()
        {
            if (director == null) return;
            director.Events.Hit -= OnHit; director.Events.Guard -= OnGuard;
            director.Events.SurfaceContact -= OnSurface; director.Events.EnvImpact -= OnImpact; director.Events.Motion -= OnMotion;
        }
        Stroke Acquire(Color color, float width, float life)
        {
            Stroke s = null;
            foreach (var candidate in strokes) if (candidate.age >= candidate.life) { s = candidate; break; }
            if (s == null && strokes.Count < StrokeLimit)
            {
                s = new Stroke { line = UiKit.Line(transform, "InkStroke", color, width, 12) };
                s.line.numCapVertices = 3;
                s.line.widthCurve = new AnimationCurve(new Keyframe(0, .2f), new Keyframe(.25f, 1), new Keyframe(.7f, .7f), new Keyframe(1, 0));
                strokes.Add(s);
            }
            if (s == null) return null;
            s.age = 0; s.life = life; s.color = color; s.width = width; s.expand = false;
            s.line.widthMultiplier = width; s.line.enabled = true;
            s.line.startColor = s.line.endColor = color;
            return s;
        }
        public void Slash(Vector2 point, Vector2 direction, Color color, float power = 1)
        {
            if (!EnabledEffects) return;
            direction = direction.sqrMagnitude < .001f ? new Vector2(1, -.5f).normalized : direction.normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            for (int i = 0; i < 3; i++)
            {
                var s = Acquire(color, (.14f - i * .035f) * power, .27f);
                if (s == null) break;
                s.line.positionCount = 5;
                for (int k = 0; k < 5; k++)
                {
                    float t = k / 4f;
                    s.line.SetPosition(k, (Vector3)(point + direction * ((t - .5f) * 2.1f * power)
                        + normal * ((i - 1) * .1f + Mathf.Sin(t * Mathf.PI) * .15f * power)));
                }
            }
        }
        public void Ring(Vector2 point, Color color, float power = 1)
        {
            if (!EnabledEffects) return;
            var s = Acquire(color, .055f * power, .34f);
            if (s == null) return;
            s.expand = true; s.origin = point; s.line.positionCount = 33;
            for (int i = 0; i < 33; i++)
            {
                float a = i * Mathf.PI / 16;
                s.line.SetPosition(i, (Vector3)point + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * .55f * power);
            }
        }
        public void GroundBurst(Vector2 point, float power = 1)
        {
            if (!EnabledEffects) return;
            point.y = .04f;
            Ring(point, new Color(.12f, .09f, .06f, .55f), power);
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.Lerp(.18f, Mathf.PI - .18f, i / 6f);
                var s = Acquire(FighterFactory.Ink, .065f * power, .42f);
                if (s == null) break;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                s.line.positionCount = 3;
                s.line.SetPosition(0, point);
                s.line.SetPosition(1, point + dir * .45f * power);
                s.line.SetPosition(2, point + dir * (i % 2 == 0 ? 1.1f : .7f) * power);
            }
        }
        void Scar(Vector2 point, float power, bool crack)
        {
            if (!EnabledEffects) return;
            var root = new GameObject(crack ? "PaperCrack" : "InkScar"); root.transform.SetParent(transform, false);
            point.y = .018f;
            for (int i = 0; i < 3; i++)
            {
                var mark = UiKit.Rect(root.transform, "InkBlot", new Color(.12f, .08f, .035f, .12f), 1);
                mark.sprite = UiKit.DiscSprite;
                mark.transform.position = point + new Vector2((i - 1) * .2f * power, .005f);
                mark.transform.localScale = new Vector3((.5f + i * .1f) * power, .06f * power, 1);
                if (!crack) continue;
                var line = UiKit.Line(root.transform, "PaperTear", new Color(.16f, .10f, .045f, .25f), .018f, 1);
                line.positionCount = 4;
                for (int k = 0; k < 4; k++)
                    line.SetPosition(k, point + new Vector2((k * .22f - .3f) * power, .035f + ((k + i) % 2) * .065f));
            }
            scars.Enqueue(root);
            while (scars.Count > ScarLimit) Destroy(scars.Dequeue());
        }
        void OnHit(HitEvent e)
        {
            if (!EnabledEffects) return;
            bool strike = AttackTechniques.IsKick(e.style) || e.style == AttackStyle.Bash || e.style == AttackStyle.GroundWave;
            if (strike) Ring(e.point, FighterFactory.TeamColor(e.attacker), e.critical ? 1.2f : .8f);
            else Slash(e.point, e.style == AttackStyle.Thrust ? Vector2.right : new Vector2(director.Fighters[e.attacker].Facing, -.65f),
                FighterFactory.TeamColor(e.attacker), e.critical || e.combo ? 1.15f : .7f);
            float feel = AttackTechniques.StrikeFeel(director.Fighters[e.attacker], e);
            if (e.damage >= director.Context.Balance.heavyHitDamage || feel > 0f) Scar(e.point, .65f + .5f * feel, false);
            // 大型武器の一撃は足元の紙まで割る
            if (feel > 0f)
            {
                Vector2 feet = director.Fighters[e.defender].Body.position;
                GroundBurst(feet, .5f + .5f * feel);
                if (feel >= 1f) Scar(feet, 1f, true);
            }
        }
        void OnGuard(GuardEvent e)
        {
            if (!EnabledEffects) return;
            Ring(e.point, e.parried ? new Color(.85f, .64f, .24f, 1) : FighterFactory.Ink, e.broke ? 1.1f : .45f);
            if (e.broke) Slash(e.point, Vector2.up, FighterFactory.Ink, .6f);
        }
        void OnImpact(EnvImpactEvent e)
        {
            if (e.isWall) { Slash(e.point, Vector2.up, FighterFactory.Ink, .8f); return; }
            GroundBurst(e.point, e.smash || e.slam ? 1.3f : .65f); Scar(e.point, e.smash || e.slam ? 1.25f : .7f, true);
        }
        void OnSurface(SurfaceContactEvent e)
        {
            if (e.isWall || e.speed < 3f || e.time < nextLand[e.fighter]) return;
            nextLand[e.fighter] = e.time + .4f;
            GroundBurst(e.point, Mathf.Clamp(e.speed * .07f, .4f, .8f));
            Scar(e.point, .4f, false);
        }
        void OnMotion(FighterMotionEvent e)
        {
            if (e.kind == FighterMotionKind.AerialChase) GroundBurst(director.Fighters[e.fighter].Body.position, .7f);
            if (e.kind == FighterMotionKind.BindCounter) Slash(director.Fighters[e.fighter].GripWorld, Vector2.up, FighterFactory.TeamColor(e.fighter), .8f);
        }
        void Update()
        {
            if (director == null) return;
            if (!EnabledEffects)
            {
                foreach (var s in strokes) { s.age = s.life; s.line.enabled = false; }
                foreach (var t in trails) if (t != null) t.positionCount = 0;
                while (scars.Count > 0) Destroy(scars.Dequeue());
                ActiveStrokeCount = 0; return;
            }
            float dt = TimeController.Paused ? 0 : Time.unscaledDeltaTime;
            ActiveStrokeCount = 0;
            foreach (var s in strokes)
            {
                if (s.age >= s.life) { s.line.enabled = false; continue; }
                s.age += dt; ActiveStrokeCount++;
                var color = s.color; color.a *= 1 - Mathf.Clamp01(s.age / s.life);
                s.line.startColor = s.line.endColor = color;
                if (s.expand && dt > 0)
                    for (int k = 0; k < s.line.positionCount; k++)
                        s.line.SetPosition(k, s.origin + (s.line.GetPosition(k) - s.origin) * (1 + dt * 4));
            }
            if (TimeController.Paused || TimeController.FreezeActive) return;
            if (director.Fighters[0].Runtime.state == FighterState.Bind && director.Context.SimTime >= nextBind)
            {
                nextBind = director.Context.SimTime + .12f;
                Vector2 at = (director.Fighters[0].WeaponBody.worldCenterOfMass + director.Fighters[1].WeaponBody.worldCenterOfMass) * .5f;
                Slash(at, new Vector2(1, 1), new Color(.85f, .64f, .24f, .8f), .3f);
                Scar(at, .25f, false);
            }
        }
        void LateUpdate()
        {
            if (director == null || !EnabledEffects || TimeController.Paused || TimeController.FreezeActive) return;
            bool sample = director.Context.SimTime - trailSampleAt >= .015f;
            if (sample) trailSampleAt = director.Context.SimTime;
            for (int i = 0; i < 2; i++)
            {
                var f = director.Fighters[i]; var r = f.Runtime;
                bool active = r.state == FighterState.AttackActive && !AttackTechniques.IsKick(r.attackStyle)
                    && !r.weaponDetached && !AttackTechniques.IsRanged(r.attackStyle);
                if (!active) { trails[i].positionCount = 0; trailCounts[i] = 0; continue; }
                if (trailIds[i] != r.currentAttackId) { trailCounts[i] = 0; trailIds[i] = r.currentAttackId; }
                if (sample)
                {
                    Vector2 tip = tips[i];
                    if (f.WeaponSprite.flipX) tip.x *= -1;
                    var at = f.WeaponSprite.transform.TransformPoint(tip);
                    // Teleport corrections must not draw a stroke across the arena.
                    if (trailCounts[i] > 0 && Vector3.Distance(at, trailPoints[i][0]) > 2.5f) trailCounts[i] = 0;
                    for (int k = 11; k > 0; k--) trailPoints[i][k] = trailPoints[i][k - 1];
                    trailPoints[i][0] = at; trailCounts[i] = Mathf.Min(12, trailCounts[i] + 1);
                }
                trails[i].positionCount = trailCounts[i];
                for (int k = 0; k < trailCounts[i]; k++) trails[i].SetPosition(k, trailPoints[i][k]);
                trails[i].widthMultiplier = Mathf.Lerp(.10f, .22f, Mathf.Clamp01(f.Stats.weightScore / 100f)) * (1f + .6f * f.ImpactFeel);
                Color c = FighterFactory.TeamColor(i); c.a = .65f;
                trails[i].startColor = c; c.a = 0; trails[i].endColor = c;
            }
        }
    }
}
