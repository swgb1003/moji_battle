using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>Situational actions, updated on the match's deterministic physics clock.</summary>
    public sealed class FighterActions
    {
        readonly Fighter f;
        readonly List<Vector2> hookPoints = new List<Vector2>();
        readonly Vector3[] hitboxScales;
        bool crouched;
        Vector2 previousFoot;
        bool footTracked;
        public bool HasHook => hookPoints.Count > 0;
        public int BindContacts { get; private set; }
        public int BindActiveContacts { get; private set; }
        public float ClosestBindRatio { get; private set; } = float.PositiveInfinity;
        public float CrouchAmount => crouched ? 0.45f : 0f;
        FighterRuntime Rt => f.Runtime;
        CombatBalance B => f.Balance;

        public FighterActions(Fighter fighter)
        {
            f = fighter;
            hitboxScales = new Vector3[f.BodyColliders.Length];
            for (int i = 0; i < hitboxScales.Length; i++) hitboxScales[i] = f.BodyColliders[i].transform.localScale;
            FindHooks();
        }

        // Empty cells between strokes identify cavities in the actual collider union.
        // A straight bar has none. The cache follows size/grip geometry, not character meaning.
        void FindHooks()
        {
            const int n = 20;
            Rect bounds = f.Weapon.boundsLocal;
            var ink = new bool[n, n];
            for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            {
                Vector2 p = new Vector2(bounds.xMin + (x + .5f) * bounds.width / n,
                    bounds.yMin + (y + .5f) * bounds.height / n);
                foreach (var box in f.WeaponColliderBase)
                    if (Mathf.Abs(p.x - box.x) <= box.z * .5f && Mathf.Abs(p.y - box.y) <= box.w * .5f)
                    { ink[x, y] = true; break; }
            }
            for (int x = 1; x < n - 1; x++)
            for (int y = 1; y < n - 1; y++)
            {
                if (ink[x, y]) continue;
                bool left = false, right = false, up = false, down = false;
                for (int k = 0; k < n; k++)
                {
                    if (k < x && ink[k, y]) left = true;
                    if (k > x && ink[k, y]) right = true;
                    if (k < y && ink[x, k]) down = true;
                    if (k > y && ink[x, k]) up = true;
                }
                if ((left && right) || (up && down))
                    hookPoints.Add(new Vector2(bounds.xMin + (x + .5f) * bounds.width / n,
                        bounds.yMin + (y + .5f) * bounds.height / n));
            }
        }

        public bool IsHookContact(Vector2 world)
        {
            Vector2 p = f.WeaponLocalPoint(world);
            float radius = f.Weapon.maxSide * .18f;
            foreach (var hook in hookPoints) if ((hook - p).sqrMagnitude <= radius * radius) return true;
            return false;
        }

        void Motion(FighterMotionKind kind) => f.Context.Events.Raise(new FighterMotionEvent
            { fighter = f.Id, kind = kind, time = f.Context.SimTime });

        public void OnStateChanged(FighterState previous, FighterState next)
        {
            if (next != FighterState.Evade) SetCrouch(false);
            if (next == FighterState.Stagger || next == FighterState.Knockdown || next == FighterState.KO)
            {
                Rt.duckCounterPending = false;
                Rt.aerialStage = 0;
                Rt.launchConnectedAt = -1f;
            }
            footTracked = false;
        }

        void SetCrouch(bool value)
        {
            if (crouched == value) return;
            crouched = value;
            for (int i = 0; i < hitboxScales.Length; i++)
                f.BodyColliders[i].transform.localScale = value
                    ? Vector3.Scale(hitboxScales[i], new Vector3(1f, .55f, 1f)) : hitboxScales[i];
        }

        public bool TryDuck(float time)
        {
            var o = f.Opponent.Runtime;
            if (!B.expandedActions || f.Context.MatchOver || !f.IsGrounded || !Rt.CanBeControlled || Rt.weaponDetached
                || time < Rt.evadeReadyAt || time < Rt.duckReadyAt || f.WeightClass == WeightClass.Heavy
                || !(o.attackStyle == AttackStyle.Sweep || o.attackStyle == AttackStyle.Spin || o.attackStyle == AttackStyle.Whirl)) return false;
            if (!f.Brain.Rng.Chance(B.duckChance * f.Brain.TechniqueScale)) return false;
            Rt.duckReadyAt = time + B.duckCooldown;
            Rt.duckCounterPending = true;
            f.StartEvade(EvadeKind.Duck, time);
            return true;
        }

        public bool TryFeint(float time)
        {
            var o = f.Opponent.Runtime;
            if (!B.expandedActions || f.Context.MatchOver || !f.IsGrounded || !Rt.CanBeControlled || Rt.weaponDetached || o.IsDown
                || time < Rt.attackReadyAt || time < Rt.feintReadyAt || f.DistanceToOpponent > f.AttackRange * 1.15f
                || (o.state != FighterState.Guard && f.Opponent.Tendency.guardBias < .3f)) return false;
            float chance = B.feintChance * (f.Mods.customized && f.Mods.style == BattleStyle.Counter ? 1.5f : 1f);
            if (f.Mods.customized && f.Mods.style == BattleStyle.Counter && f.Style.AttackWillingnessBonus(time) <= 0f) return false;
            if (f.WeightClass == WeightClass.Heavy) chance *= .35f;
            if (!f.Brain.Rng.Chance(chance * f.Brain.TechniqueScale)) return false;
            StartFeint(time);
            return true;
        }

        public void StartFeint(float time)
        {
            if (!B.expandedActions || !Rt.CanBeControlled || !f.IsGrounded || Rt.weaponDetached
                || time < Rt.feintReadyAt || f.Context.MatchOver) return;
            Rt.feintReadyAt = time + B.feintCooldown;
            Rt.comboRemaining = 0;
            Rt.swingFromPsi = 110f;
            Rt.swingToPsi = 0f;
            Rt.attackStyle = AttackStyle.Overhead;
            Rt.windupDuration = B.feintDuration;
            f.SetState(FighterState.Feint);
            f.Context.LastAttackStartTime = time;
            Motion(FighterMotionKind.Feint);
        }

        public bool TryKick(float time)
        {
            if (!B.expandedActions || f.Context.MatchOver || !f.IsGrounded || !Rt.CanBeControlled || time < Rt.kickReadyAt
                || time < Rt.attackReadyAt || f.DistanceToOpponent > B.kickRange * .85f || f.Opponent.Runtime.IsDown
                || !f.Brain.Rng.Chance(B.kickChance * f.Brain.TechniqueScale)) return false;
            f.StartAttack(time, AttackStyle.FrontKick);
            return true;
        }

        public bool TryWallJump(float time)
        {
            if (!B.expandedActions || f.Context.MatchOver || !f.IsGrounded || !Rt.CanBeControlled || Rt.weaponDetached
                || f.WeightClass == WeightClass.Heavy || time < Rt.wallJumpReadyAt || time < Rt.evadeReadyAt
                || f.BackSpace > B.wallJumpDistance || f.DistanceToOpponent > 2.3f
                || f.Opponent.Runtime.IsDown) return false;
            Rt.wallJumpReadyAt = time + B.wallJumpCooldown;
            f.StartEvade(EvadeKind.WallJump, time);
            return true;
        }

        public void ConfigureAttack(float time)
        {
            switch (Rt.attackStyle)
            {
                case AttackStyle.FrontKick:
                case AttackStyle.RecoverKick:
                    Rt.windupDuration = .12f; Rt.activeDuration = .28f; Rt.recoveryDuration = .38f;
                    Rt.kickReadyAt = time + B.kickCooldown;
                    Rt.comboRemaining = 0;
                    break;
                case AttackStyle.PickupSlash:
                    Rt.windupDuration = Mathf.Min(Rt.windupDuration, .2f);
                    Rt.activeDuration = Mathf.Clamp(Rt.activeDuration, .3f, .5f);
                    Rt.comboRemaining = 0;
                    Rt.pickupSlashReadyAt = time + B.pickupSlashCooldown;
                    Motion(FighterMotionKind.PickupSlash);
                    break;
                case AttackStyle.HookPull:
                    Rt.windupDuration = .24f; Rt.activeDuration = .42f; Rt.recoveryDuration = .45f;
                    Rt.comboRemaining = 0;
                    Rt.hookReadyAt = time + B.hookCooldown;
                    break;
            }
        }

        public void OnEvade(EvadeKind kind)
        {
            if (kind == EvadeKind.Duck) { SetCrouch(true); Motion(FighterMotionKind.Duck); }
            if (kind == EvadeKind.WallJump) Motion(FighterMotionKind.WallJump);
            if (kind == EvadeKind.RecoverRoll) Motion(FighterMotionKind.RecoverRoll);
        }

        public void OnAttackActive()
        {
            if (AttackTechniques.IsKick(Rt.attackStyle)) Motion(Rt.attackStyle == AttackStyle.FrontKick
                ? FighterMotionKind.FrontKick : FighterMotionKind.RecoverKick);
        }

        public void OnPickup(bool reached)
        {
            // 自操作は拾った直後に斬るを押すと拾い斬り
            if (f.Brain.IsPlayer) { if (reached) f.Brain.Player.OnPickedUp(f.Context.SimTime); return; }
            if (!B.expandedActions || !reached || f.Context.MatchOver || !f.Context.AiEnabled
                || !Rt.CanBeControlled || f.Context.SimTime < Rt.pickupSlashReadyAt || f.Opponent.Runtime.IsDown
                || f.DistanceToOpponent > f.AttackRange * 1.1f) return;
            f.StartAttack(f.Context.SimTime, AttackStyle.PickupSlash);
        }

        public bool TryKickFollowup(float time)
        {
            if (f.Brain.IsPlayer || Rt.attackStyle != AttackStyle.FrontKick || !Rt.attackHadContact || f.Opponent.Runtime.IsDown
                || !AttackTechniques.CanDash(f) || time < Rt.dashReadyAt || f.DistanceToOpponent < .8f
                || f.DistanceToOpponent > f.AttackRange + B.dashMaxTravel || f.Context.MatchOver) return false;
            f.StartAttack(time, AttackStyle.DashSlash);
            return true;
        }

        public void FinishRecover(float time)
        {
            // 倒れてから起き上がるまで（自操作の起き上がりの入力を受け付ける間）
            float downSince = time - Rt.stateTime - 1.5f;
            f.SetState(FighterState.Approach);
            if (!B.expandedActions || f.Context.MatchOver || !f.Context.AiEnabled || f.Opponent.Runtime.IsDown) return;
            if (f.Brain.IsPlayer) { f.Brain.Player.OnRecovered(time, downSince); return; }
            if (f.DistanceToOpponent <= B.kickRange && time >= Rt.kickReadyAt)
                f.StartAttack(time, AttackStyle.RecoverKick);
            else if (f.BackSpace > .8f && f.IsGrounded)
                f.StartEvade(EvadeKind.RecoverRoll, time);
        }

        public bool TryBind(Fighter other, Vector2 point, float momentumA, float momentumD, float time)
        {
            var o = other.Runtime;
            BindContacts++;
            if (Rt.state == FighterState.AttackActive && o.state == FighterState.AttackActive)
            {
                BindActiveContacts++;
                if (Mathf.Min(momentumA, momentumD) > .05f)
                    ClosestBindRatio = Mathf.Min(ClosestBindRatio, Mathf.Max(momentumA, momentumD) / Mathf.Min(momentumA, momentumD));
            }
            if (!B.expandedActions || Rt.state != FighterState.AttackActive || o.state != FighterState.AttackActive
                || Rt.throwLive || o.throwLive || Rt.weaponDetached || o.weaponDetached
                || AttackTechniques.IsKick(Rt.attackStyle) || AttackTechniques.IsKick(o.attackStyle)
                || time < Rt.bindReadyAt || time < o.bindReadyAt
                || Rt.attackStyle == AttackStyle.Leap || o.attackStyle == AttackStyle.Leap
                || Mathf.Max(f.WeaponBody.mass, other.WeaponBody.mass) > Mathf.Min(f.WeaponBody.mass, other.WeaponBody.mass) * B.bindMassRatio
                || Mathf.Min(momentumA, momentumD) < .05f
                || Mathf.Max(momentumA, momentumD) > Mathf.Min(momentumA, momentumD) * B.bindMomentumRatio) return false;
            f.Context.Hits.Ledger.MarkResolved(Rt.currentAttackId, other.Id);
            f.Context.Hits.Ledger.MarkResolved(o.currentAttackId, f.Id);
            foreach (var fighter in new[] { f, other })
            {
                var r = fighter.Runtime;
                if (!r.clashedThisAttack) r.metrics.clashes++;
                r.clashedThisAttack = true;
                r.attackHadContact = true; r.comboRemaining = 0; r.bindReadyAt = time + B.bindCooldown;
                r.bindPsi = fighter.WeaponMotor.MeasurePsi();
                fighter.SetState(FighterState.Bind);
                fighter.AddVelocity(new Vector2(-fighter.Body.linearVelocity.x, 0f));
                fighter.WeaponBody.angularVelocity *= .2f;
                fighter.Actions.Motion(FighterMotionKind.Bind);
            }
            return true;
        }

        public bool TryHook(Fighter other, Vector2 point, float time)
        {
            if (Rt.attackStyle != AttackStyle.HookPull || Rt.state != FighterState.AttackActive
                || !HasHook || !IsHookContact(point) || other.Runtime.IsDown || other.Runtime.weaponDetached) return false;
            if (f.Context.Hits.Ledger.IsResolved(Rt.currentAttackId, other.Id)) return false;
            f.Context.Hits.Ledger.MarkResolved(Rt.currentAttackId, other.Id);
            Rt.attackHadContact = true;
            if (other.WeaponBody.mass > f.WeaponBody.mass * B.hookMaxMassRatio)
            {
                f.EnterStagger(B.hookStagger);
                f.AddVelocity(new Vector2(f.TowardOpponent * 1.2f, 0f));
                Motion(FighterMotionKind.HookReversed);
            }
            else
            {
                other.Runtime.guardLoad = 0f;
                other.EnterStagger(B.hookStagger);
                other.AddVelocity(new Vector2(-f.TowardOpponent * 1.4f, 0f));
                other.WeaponBody.angularVelocity += other.Facing * 90f;
                f.SetState(FighterState.AttackRecovery);
                Motion(FighterMotionKind.HookPull);
            }
            return true;
        }

        public Vector2 KickFoot
        {
            get
            {
                float t = Rt.state == FighterState.AttackActive ? Mathf.Clamp01(Rt.stateTime / Rt.activeDuration) : 0f;
                float extension = Mathf.Sin(t * Mathf.PI);
                return f.Body.GetRelativePoint(new Vector2(f.Facing * (.12f + extension * B.kickRange), .6f + .35f * extension));
            }
        }

        void DetectKick(float dt)
        {
            Vector2 foot = KickFoot;
            Vector2 velocity = footTracked ? (foot - previousFoot) / Mathf.Max(.001f, dt) : new Vector2(f.Facing * 4f, 0f);
            previousFoot = foot; footTracked = true;
            var d = f.Opponent;
            if (Rt.attackHadContact || d.Runtime.IsDown || f.Context.Hits.Ledger.IsResolved(Rt.currentAttackId, d.Id)) return;
            foreach (var collider in d.BodyColliders)
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                Vector2 point = collider.ClosestPoint(foot);
                if ((point - foot).sqrMagnitude > .18f * .18f) continue;
                bool guarding = d.Runtime.state == FighterState.Guard && d.Facing == -f.Facing
                    && (float.IsNaN(d.Runtime.guardPsiTarget) || d.Runtime.guardPsiTarget < 60f);
                f.Context.Hits.Report(new HitContact { attacker = f, target = d, targetIsWeapon = guarding,
                    bodyAttack = true, part = collider.GetComponent<BodyPartHitbox>().Part, point = point,
                    normal = new Vector2(f.Facing, 0f), relativeVelocity = velocity,
                    myColliderId = f.BodyColliders[3].GetInstanceID(), otherColliderId = collider.GetInstanceID() });
            }
        }

        void DetectHook(float time)
        {
            var d = f.Opponent;
            if (Rt.attackHadContact || !HasHook || d.Runtime.IsDown || d.Runtime.weaponDetached
                || f.Context.Hits.Ledger.IsResolved(Rt.currentAttackId, d.Id)) return;
            // A blade inside an empty cavity does not produce an ink-on-ink collision.
            // Probe the cached empty cells against actual opposing weapon colliders.
            foreach (var cavity in hookPoints)
            {
                Vector2 point = f.WeaponBody.GetRelativePoint(new Vector2(cavity.x * f.Facing, cavity.y));
                foreach (var collider in d.WeaponColliders)
                {
                    if (!collider.enabled || !collider.gameObject.activeInHierarchy || !collider.bounds.Contains(point)
                        || !collider.OverlapPoint(point)) continue;
                    if (TryHook(d, point, time)) return;
                }
            }
        }

        public void Tick(float time, float dt)
        {
            if (f.Context.MatchOver) { Rt.duckCounterPending = false; Rt.aerialStage = 0; SetCrouch(false); return; }
            TickAerial(time);
            if (Rt.state == FighterState.Feint && Rt.stateTime >= B.feintDuration)
            {
                var o = f.Opponent.Runtime;
                float guard = float.IsNaN(o.guardPsiTarget) ? B.guardPsi : o.guardPsiTarget;
                f.StartAttack(time, o.state == FighterState.Guard && guard < 30f ? AttackStyle.Overhead : AttackStyle.Thrust);
                Rt.windupDuration = Mathf.Min(f.WeightClass == WeightClass.Heavy ? .35f : .16f, Rt.windupDuration);
                Rt.comboRemaining = f.Brain.IsPlayer ? 0 : f.Brain.Rng.RangeInclusive(f.Tendency.comboMin, f.Tendency.comboMax) - 1;
            }
            if (Rt.state == FighterState.Evade && Rt.evadeKind == EvadeKind.Duck && Rt.stateTime >= B.duckDuration)
            {
                // 自操作はしゃがんでいる間に斬るを押した時だけ斬り返す
                bool counter = Rt.duckCounterPending && (!f.Brain.IsPlayer || f.Brain.Player.TakeFollowUp(time - Rt.stateTime, PlayerCommand.DuckCounter));
                Rt.duckCounterPending = false;
                Rt.evadeEndedAt = time;
                f.SetState(FighterState.Approach);
                if (counter && !f.Opponent.Runtime.IsDown && !Rt.weaponDetached && f.DistanceToOpponent <= f.AttackRange * 1.15f)
                {
                    f.StartAttack(time, AttackStyle.Rising);
                    Rt.windupDuration = Mathf.Min(.15f, Rt.windupDuration);
                    Rt.comboRemaining = f.Brain.IsPlayer ? 0 : f.Brain.Rng.RangeInclusive(f.Tendency.comboMin, f.Tendency.comboMax) - 1;
                    Motion(FighterMotionKind.DuckCounter);
                }
            }
            if (Rt.state == FighterState.Bind)
            {
                var d = f.Opponent;
                if (d.Runtime.state != FighterState.Bind) { f.EnterStagger(.18f); return; }
                if (f.Id < d.Id && Rt.stateTime >= B.bindDuration)
                {
                    var heavy = f.WeaponBody.mass >= d.WeaponBody.mass ? f : d;
                    var light = heavy == f ? d : f;
                    if (heavy.WeaponBody.mass >= light.WeaponBody.mass * 1.15f && heavy.WeightClass != WeightClass.Light)
                    {
                        heavy.SetState(FighterState.Approach);
                        light.EnterStagger(.3f);
                        light.AddVelocity(new Vector2(heavy.TowardOpponent * B.kickPushSpeed, .1f));
                        heavy.Actions.Motion(FighterMotionKind.BindPush);
                        if (B.bindCounters && f.Context.AiEnabled && !heavy.Runtime.weaponDetached
                            && heavy.DistanceToOpponent <= heavy.AttackRange + .4f
                            && (!heavy.Brain.IsPlayer || heavy.Brain.Player.TakeFollowUp(time - Rt.stateTime, PlayerCommand.BindCounter)))
                        {
                            heavy.StartAttack(time, AttackStyle.Bash);
                            heavy.Runtime.windupDuration = .16f;
                            heavy.Runtime.comboRemaining = 0;
                            heavy.Actions.Motion(FighterMotionKind.BindCounter);
                        }
                    }
                    else
                    {
                        // One fighter flows sideways into a riposte; the other disengages.
                        // Resolve from the seeded AI stream, once per bind (lower id owns it).
                        // 自操作は鍔迫り合いの間に斬るを押していれば崩し反撃。押していなければ COM が半々で反撃する
                        var player = f.Brain.IsPlayer ? f : d.Brain.IsPlayer ? d : null;
                        Fighter counter;
                        if (player == null) counter = B.bindCounters && f.Context.AiEnabled && f.Brain.Rng.Chance(.5f) ? f : d;
                        else counter = player.Brain.Player.TakeFollowUp(time - Rt.stateTime, PlayerCommand.BindCounter) ? player
                            : player.Opponent.Brain.Frozen || !player.Opponent.Brain.Rng.Chance(.5f * player.Opponent.Brain.TechniqueScale) ? null : player.Opponent;
                        foreach (var fighter in new[] { f, d })
                        {
                            fighter.SetState(FighterState.Approach);
                            fighter.Runtime.attackReadyAt = time + .15f;
                            if (fighter.BackSpace > .6f) fighter.AddVelocity(new Vector2(-fighter.TowardOpponent * 1.8f, .1f));
                            fighter.Actions.Motion(FighterMotionKind.BindEscape);
                        }
                        if (B.bindCounters && f.Context.AiEnabled && counter != null && !counter.Runtime.weaponDetached
                            && counter.DistanceToOpponent <= counter.AttackRange + .4f)
                        {
                            var other = counter.Opponent;
                            other.EnterStagger(.24f);
                            other.WeaponBody.angularVelocity += other.Facing * 95f;
                            counter.StartAttack(time, AttackStyle.Riposte);
                            counter.Runtime.windupDuration = .14f;
                            counter.Runtime.comboRemaining = 0;
                            counter.Actions.Motion(FighterMotionKind.BindCounter);
                        }
                    }
                }
            }
            if (Rt.state == FighterState.AttackActive && AttackTechniques.IsKick(Rt.attackStyle)) DetectKick(dt);
            if (Rt.state == FighterState.AttackActive && Rt.attackStyle == AttackStyle.HookPull) DetectHook(time);
        }

        public bool TryStartAerial(float time)
        {
            var d = f.Opponent;
            if (!B.expandedActions || !B.aerialCombos || !f.Context.AiEnabled || f.Context.MatchOver
                || f.WeightClass == WeightClass.Heavy || Rt.weaponDetached || time < Rt.aerialReadyAt
                || Rt.launchConnectedAt < 0f || time - Rt.launchConnectedAt < .1f
                || (Rt.state != FighterState.AttackActive && Rt.state != FighterState.AttackRecovery)
                || d.IsGrounded || d.Runtime.state == FighterState.KO
                || f.DistanceToOpponent > f.AttackRange + .65f
                || f.Context.Interventions?.InSpecial(f.Id) == true) return false;
            // 自操作は打ち上げが当たった後に斬るを押した時だけ追いかける
            if (f.Brain.IsPlayer && !f.Brain.Player.TakeFollowUp(Rt.launchConnectedAt, PlayerCommand.AerialChase)) return false;
            Rt.launchConnectedAt = -1f;
            Rt.aerialReadyAt = time + B.aerialCooldown;
            Rt.aerialStartedAt = time;
            f.StartAttack(time, AttackStyle.Rising);
            Rt.aerialStage = 1;
            Rt.tempoAttack = false;
            Rt.comboRemaining = 0;
            Rt.windupDuration = .12f; Rt.activeDuration = .24f; Rt.recoveryDuration = .18f;
            float speed = Mathf.Clamp((d.X - f.X - f.TowardOpponent * f.Weapon.length * .55f) * 3f,
                -B.aerialMaxSpeedX, B.aerialMaxSpeedX);
            Rt.aerialVelocityX = speed;
            f.AddVelocity(new Vector2(speed, B.aerialJumpSpeed) - f.Body.linearVelocity);
            Motion(FighterMotionKind.AerialChase);
            return true;
        }

        void TickAerial(float time)
        {
            if (Rt.aerialStage == 0) return;
            var d = f.Opponent;
            float age = time - Rt.aerialStartedAt;
            if (Rt.weaponDetached || d.Runtime.state == FighterState.KO || d.IsGrounded
                || age > B.aerialTimeout || (age > .16f && f.IsGrounded)
                || (Rt.state != FighterState.AttackWindup && Rt.state != FighterState.AttackActive
                    && Rt.state != FighterState.AttackRecovery))
            { Rt.aerialStage = 0; return; }
            // Preserve the jump arc, as for Leap. Weapon torque must not lift the
            // light body several metres above its intended target.
            float gravity = Mathf.Max(.1f, -Physics2D.gravity.y * f.Body.gravityScale);
            f.AddVelocity(new Vector2(Rt.aerialVelocityX, B.aerialJumpSpeed - gravity * age) - f.Body.linearVelocity);
            if (Rt.aerialStage == 1 && Rt.state == FighterState.AttackActive && !Rt.attackHadContact)
            {
                // Real reach and one-hit ledger; no teleporting or guaranteed hit.
                if (Vector2.Distance(f.GripWorld, d.ChestWorld) <= f.Weapon.length + .25f
                    && Rt.stateTime >= .06f)
                {
                    f.Context.Hits.Report(new HitContact { attacker = f, target = d, part = BodyPart.Torso,
                        point = d.ChestWorld, normal = Vector2.up, relativeVelocity = Vector2.up * 5f,
                        myColliderId = f.WeaponColliders[0].GetInstanceID(), otherColliderId = d.BodyColliders[1].GetInstanceID() });
                    Motion(FighterMotionKind.AerialSlash);
                }
            }
            if (Rt.aerialStage == 1 && age >= .32f && (Rt.attackHadContact || Rt.state == FighterState.AttackRecovery))
            {
                f.StartAttack(time, AttackStyle.Smash);
                Rt.aerialStage = 2;
                Rt.tempoAttack = false;
                Rt.comboRemaining = 0;
                Rt.windupDuration = .12f;
                Motion(FighterMotionKind.AerialSmash);
            }
        }
    }
}
