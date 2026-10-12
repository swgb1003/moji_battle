using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    public sealed class RangedProjectile
    {
        public Fighter Owner { get; internal set; }
        public AttackStyle Style { get; internal set; }
        public int AttackId { get; internal set; }
        public Vector2 Position { get; internal set; }
        public Vector2 PreviousPosition { get; internal set; }
        public Vector2 Size { get; internal set; }
        public float VelocityX { get; internal set; }
        public float RemainingRange { get; internal set; }
    }

    /// <summary>遠距離技と弾は試合の固定時計で進む。発射時の技・攻撃IDを保持し、次の攻撃へ当たりを持ち越さない。</summary>
    public sealed class RangedCombat
    {
        readonly MatchDirector director;
        readonly List<RangedProjectile> projectiles = new List<RangedProjectile>();
        readonly ContactPoint2D[] contacts = new ContactPoint2D[64];
        public IReadOnlyList<RangedProjectile> Projectiles => projectiles;
        public int LaunchCount { get; private set; }
        MatchContext Context => director.Context;

        public RangedCombat(MatchDirector director) { this.director = director; }
        public void ApplyRemote(FriendShotFrame[] frames)
        {
            if (!director.RemotePlayback) return;
            var next = new List<RangedProjectile>();
            foreach (var frame in frames ?? System.Array.Empty<FriendShotFrame>())
            {
                var p = projectiles.Find(item => item.Owner.Id == frame.owner && item.AttackId == frame.id);
                if (p == null) p = new RangedProjectile { Owner = director.Fighters[frame.owner], AttackId = frame.id,
                    Style = frame.style, Position = frame.position };
                p.PreviousPosition = p.Position; p.Position = frame.position; p.Size = frame.size; p.VelocityX = frame.velocity;
                next.Add(p);
            }
            projectiles.Clear(); projectiles.AddRange(next);
        }

        public bool TickFighter(Fighter f, float time, float dt)
        {
            var rt = f.Runtime;
            if (!AttackTechniques.IsRanged(rt.attackStyle) || (rt.state != FighterState.AttackWindup
                && rt.state != FighterState.AttackActive && rt.state != FighterState.AttackRecovery)) return false;
            if (rt.state == FighterState.AttackRecovery)
            {
                if (rt.stateTime >= rt.recoveryDuration && !rt.leapJumped && f.IsGrounded)
                {
                    rt.attackReadyAt = time + .25f;
                    f.SetState(FighterState.Approach);
                    f.Brain.OnAttackSequenceFinished(time);
                }
                return true;
            }
            bool wave = rt.attackStyle == AttackStyle.GroundWave;
            if (wave && rt.leapJumped)
            {
                float g = -Physics2D.gravity.y * f.Body.gravityScale;
                float t = time - rt.leapJumpTime;
                float height = rt.rangedJumpY + rt.leapVy * t - .5f * g * t * t;
                if (height >= rt.rangedJumpY && t <= 2f * rt.leapVy / g)
                {
                    var delta = new Vector2(0f, height - f.Body.position.y);
                    f.Body.position += delta;
                    f.WeaponBody.position += delta;
                    f.AddVelocity(new Vector2(0f, rt.leapVy - g * t) - f.Body.linearVelocity);
                }
                else rt.leapJumped = false;
            }
            if (rt.state == FighterState.AttackWindup)
            {
                if (!wave && rt.stateTime >= rt.windupDuration) f.SetState(FighterState.AttackActive);
                else if (wave && !rt.leapJumped && rt.stateTime >= Context.Balance.groundWaveCrouch)
                {
                    float g = -Physics2D.gravity.y * f.Body.gravityScale;
                    rt.leapJumped = true;
                    rt.rangedJumpY = f.Body.position.y;
                    rt.leapJumpTime = time;
                    rt.leapVy = Mathf.Sqrt(2f * g * Context.Balance.groundWaveApex);
                    rt.windupDuration = rt.stateTime + rt.leapVy / g;
                    f.AddVelocity(new Vector2(0f, rt.leapVy) - f.Body.linearVelocity);
                    Context.Events.Raise(new FighterMotionEvent { fighter = f.Id, kind = FighterMotionKind.Jump, time = time });
                }
                else if (wave && rt.leapJumped && time - rt.leapJumpTime >= rt.leapVy / (-Physics2D.gravity.y * f.Body.gravityScale))
                    f.SetState(FighterState.AttackActive);
            }
            else if (rt.state == FighterState.AttackActive)
            {
                if (!wave && !rt.rangedReleased && rt.stateTime >= .08f) Launch(f);
                if ((rt.rangedReleased && (!wave || !rt.leapJumped)) || rt.stateTime >= 1.3f)
                {
                    if (!rt.rangedReleased) rt.metrics.attacksWhiffed++;
                    f.SetState(FighterState.AttackRecovery);
                }
            }
            return true;
        }

        void Launch(Fighter f, Vector2? floorPoint = null)
        {
            var rt = f.Runtime;
            if (rt.rangedReleased || Context.MatchOver) return;
            bool wave = rt.attackStyle == AttackStyle.GroundWave;
            float side = f.Facing;
            Vector2 point = wave ? new Vector2(floorPoint.HasValue ? floorPoint.Value.x : f.GripWorld.x + side * .25f, .28f)
                : new Vector2(f.X + side * .65f, f.Body.position.y + 1.05f);
            projectiles.Add(new RangedProjectile
            {
                Owner = f, Style = rt.attackStyle, AttackId = rt.currentAttackId,
                Position = point, PreviousPosition = point,
                Size = wave ? new Vector2(.75f, .5f) : new Vector2(.65f, .16f),
                VelocityX = side * (wave ? Context.Balance.groundWaveSpeed : Context.Balance.strokeShotSpeed),
                RemainingRange = wave ? Context.Balance.groundWaveRange : Context.Balance.strokeShotRange,
            });
            rt.rangedReleased = true;
            LaunchCount++;
            Context.Events.Raise(new RangedLaunchEvent { fighter = f.Id, style = rt.attackStyle,
                attackId = rt.currentAttackId, point = point, time = Context.SimTime });
        }

        public void Tick(float dt)
        {
            if (Context.MatchOver || director.KoInProgress || director.Ended) { projectiles.Clear(); return; }
            foreach (var f in director.Fighters)
            {
                var rt = f.Runtime;
                if (rt.attackStyle != AttackStyle.GroundWave || rt.state != FighterState.AttackActive
                    || rt.rangedReleased || Context.SimTime - rt.leapJumpTime < .1f || f.Body.linearVelocity.y > .2f) continue;
                int count = f.WeaponBody.GetContacts(contacts);
                for (int i = 0; i < count; i++)
                {
                    var c = contacts[i];
                    var other = c.collider != null && c.collider.attachedRigidbody == f.WeaponBody ? c.otherCollider : c.collider;
                    var surface = other != null ? other.GetComponent<ArenaSurface>() : null;
                    if (surface != null && surface.Kind == ArenaSurface.SurfaceKind.Ground) { Launch(f, c.point); break; }
                }
            }
            // Creation order is stable; already-fired attacks can trade even if one owner is knocked out this step.
            for (int i = 0; i < projectiles.Count;)
            {
                var p = projectiles[i];
                float travel = Mathf.Min(Mathf.Abs(p.VelocityX) * dt, p.RemainingRange);
                p.PreviousPosition = p.Position;
                p.Position += new Vector2(Mathf.Sign(p.VelocityX) * travel, 0f);
                p.RemainingRange -= travel;
                var d = p.Owner.Opponent;
                bool hit = false;
                if (d.Runtime.state != FighterState.KO && (!d.Runtime.IsDown || Context.Balance.allowAttackOnDowned))
                {
                    Bounds sweep = new Bounds((p.PreviousPosition + p.Position) * .5f,
                        new Vector3(p.Size.x + travel, p.Size.y, 2f));
                    // Sweep the full path, rather than testing the endpoint, so fast strokes cannot tunnel through a body.
                    foreach (var collider in d.BodyColliders)
                    {
                        if (!collider.enabled || !sweep.Intersects(collider.bounds)) continue;
                        var part = collider.GetComponent<BodyPartHitbox>();
                        Vector2 point = collider.ClosestPoint(p.Position);
                        Context.Hits.ResolveRanged(p, d, point, part != null ? part.Part : BodyPart.Torso, Context.SimTime);
                        hit = true;
                        break;
                    }
                }
                if (hit || p.RemainingRange <= 0f || Mathf.Abs(p.Position.x) >= Context.ArenaHalfWidth)
                {
                    if (!hit) p.Owner.Runtime.metrics.attacksWhiffed++;
                    projectiles.RemoveAt(i);
                }
                else i++;
            }
        }

        public bool Threatens(Fighter defender, out AttackStyle style, out float arrival)
        {
            style = default; arrival = float.MaxValue;
            foreach (var p in projectiles)
            {
                if (p.Owner == defender) continue;
                float dx = defender.X - p.Position.x;
                if (dx * p.VelocityX <= 0f || Mathf.Abs(dx) > p.RemainingRange + .5f) continue;
                float t = Mathf.Abs(dx) / Mathf.Abs(p.VelocityX);
                if (t >= arrival) continue;
                style = p.Style; arrival = t;
            }
            return arrival <= .55f;
        }
    }
}
