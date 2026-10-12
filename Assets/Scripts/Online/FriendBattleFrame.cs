using System;
using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    [Serializable] public sealed class FriendActorFrame
    {
        public Vector2 body, weapon, velocity, hand;
        public float bodyAngle, weaponAngle, hp, stateTime, yaw, crouch, windup, fromPsi, toPsi;
        public float active, recovery;
        public int aerialStage, attackId;
        public int facing;
        public bool flip, detached, jumped;
        public FighterState state;
        public AttackStyle style;
        public EvadeKind evade;
        /// <summary>観戦者の関与（残り秒は送った時点から。無いものは -1）。</summary>
        public float gauge, cheer = -1, cheerWait;
        /// <summary>必殺を押して間合いを詰めている / 必殺技の最中</summary>
        public bool special, chain, braced;
        public static FriendActorFrame Capture(Fighter f)
        {
            var frame = CaptureBody(f);
            var iv = f.Context.Interventions;
            if (iv == null) return frame;
            var s = iv.Side(f.Id); float now = f.Context.SimTime;
            frame.gauge = s.gauge; frame.special = s.SpecialPending(now);
            frame.cheer = s.cheerUntil >= now ? s.cheerUntil - now : -1; frame.cheerWait = Mathf.Max(0, s.cheerReadyAt - now);
            frame.chain = s.chainActive; frame.braced = s.braced;
            return frame;
        }
        /// <summary>体・武器の姿勢と状態だけ（KO のリプレイの記録にも使う）。</summary>
        public static FriendActorFrame CaptureBody(Fighter f) => new FriendActorFrame
        {
            body = f.Body.position, weapon = f.WeaponBody.position, bodyAngle = f.Body.rotation, weaponAngle = f.WeaponBody.rotation,
            velocity = f.Body.linearVelocity, hp = f.Runtime.hp, state = f.Runtime.state, style = f.Runtime.attackStyle,
            stateTime = f.Runtime.stateTime, facing = f.Facing, flip = f.WeaponSprite.flipX, yaw = f.Runtime.sweepYaw,
            detached = f.Runtime.weaponDetached, jumped = f.Runtime.leapJumped, hand = f.Runtime.handOffset,
            crouch = f.Actions.CrouchAmount, windup = f.Runtime.windupDuration, fromPsi = f.Runtime.swingFromPsi,
            toPsi = f.Runtime.swingToPsi, evade = f.Runtime.evadeKind,
            active = f.Runtime.activeDuration, recovery = f.Runtime.recoveryDuration,
            aerialStage = f.Runtime.aerialStage, attackId = f.Runtime.currentAttackId
        };
        public void Apply(Fighter f, FriendActorFrame before, float alpha, bool interventions = true)
        {
            var r = f.Runtime;
            r.hp = hp; r.state = state; r.attackStyle = style; r.stateTime = stateTime; r.sweepYaw = yaw;
            r.weaponDetached = detached; r.leapJumped = jumped; r.handOffset = hand; r.windupDuration = windup;
            r.swingFromPsi = fromPsi; r.swingToPsi = toPsi; r.evadeKind = evade;
            r.activeDuration = active; r.recoveryDuration = recovery; r.aerialStage = aerialStage; r.currentAttackId = attackId;
            f.InitFacing(facing); f.WeaponSprite.flipX = flip; f.View.RemoteCrouch = crouch;
            var p = Vector2.Lerp(before?.body ?? body, body, alpha);
            var w = Vector2.Lerp(before?.weapon ?? weapon, weapon, alpha);
            float a = Mathf.LerpAngle(before?.bodyAngle ?? bodyAngle, bodyAngle, alpha), wa = Mathf.LerpAngle(before?.weaponAngle ?? weaponAngle, weaponAngle, alpha);
            f.Body.position = p; f.Body.rotation = a; f.WeaponBody.position = w; f.WeaponBody.rotation = wa;
            f.Body.linearVelocity = velocity;
            f.PrevBodyPos = f.CurrBodyPos = p; f.PrevWeaponPos = f.CurrWeaponPos = w;
            f.PrevBodyRot = f.CurrBodyRot = a; f.PrevWeaponRot = f.CurrWeaponRot = wa;
            f.Offhand?.Follow(Time.deltaTime);
            var iv = f.Context.Interventions;
            if (iv == null || !interventions) return;
            var s = iv.Side(f.Id); float now = f.Context.SimTime;
            s.gauge = gauge; s.specialRequestUntil = special ? now + 1 : -1;
            s.cheerUntil = cheer >= 0 ? now + cheer : -1; s.cheerReadyAt = now + cheerWait;
            s.chainActive = chain; s.braced = braced;
        }
    }
    [Serializable] public sealed class FriendShotFrame { public int owner, id; public AttackStyle style; public Vector2 position, size; public float velocity; }
    [Serializable] public sealed class FriendEventFrame { public int sequence; public string kind, data; }
    /// <summary>ゲストの関与の操作。kind は special / cheer。</summary>
    [Serializable] public sealed class FriendInputFrame
    {
        public int sequence;
        public string kind;
        public static string Name(InterventionKind k) => k == InterventionKind.Special ? "special" : "cheer";
        public static bool TryParse(string kind, out InterventionKind k)
        {
            k = kind == "special" ? InterventionKind.Special : InterventionKind.Cheer;
            return kind == "special" || kind == "cheer";
        }
    }
    [Serializable] public sealed class FriendBattleFrame
    {
        public int sequence;
        public float clock, countdown, simTime;
        public bool ko, ended, overtime;
        public MatchResult result;
        public FriendActorFrame[] actors;
        public FriendShotFrame[] shots;
        public FriendEventFrame[] events;
        public AchievementMatchStats[] achievements;
        public static FriendBattleFrame Capture(MatchDirector d, int sequence, FriendEventFrame[] events)
        {
            var shots = new List<FriendShotFrame>();
            foreach (var p in d.Context.Ranged.Projectiles) shots.Add(new FriendShotFrame { owner = p.Owner.Id, id = p.AttackId,
                style = p.Style, position = p.Position, size = p.Size, velocity = p.VelocityX });
            return new FriendBattleFrame { sequence = sequence, clock = d.Clock, countdown = d.CountdownRemaining,
                simTime = d.Context.SimTime, ko = d.KoInProgress, ended = d.Ended, overtime = d.Overtime,
                result = d.Result, actors = new[] { FriendActorFrame.Capture(d.Fighters[0]), FriendActorFrame.Capture(d.Fighters[1]) },
                shots = shots.ToArray(), events = events ?? Array.Empty<FriendEventFrame>(),
                achievements = d.GetComponent<AchievementMatchRecorder>()?.Capture() };
        }
    }
}
