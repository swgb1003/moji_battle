using System;
using UnityEngine;

namespace MojiBattle
{
    public enum FighterMotionKind { Jump, Throw, Pickup, Dash, Riposte, Whirl, Duck, DuckCounter, Feint, Bind, BindPush, BindEscape, FrontKick, WallJump, PickupSlash, RecoverKick, RecoverRoll, HookPull, HookReversed, GroundWave, StrokeShot, AerialChase, AerialSlash, AerialSmash, BindCounter }
    public enum FinishKind { Slash, Impact, Ground, DoubleKO }

    [Serializable] public struct RangedLaunchEvent
    {
        public int fighter, attackId;
        public AttackStyle style;
        public Vector2 point;
        public float time;
    }

    [Serializable] public struct FighterMotionEvent
    {
        public int fighter;
        public FighterMotionKind kind;
        public float time;
    }

    [Serializable] public struct SurfaceContactEvent
    {
        public int fighter;
        public bool isWall, down;
        public float speed, time;
        public Vector2 point;
    }

    [Serializable] public struct HitEvent
    {
        public int attacker, defender, attackId;
        public BodyPart part;
        public HitQuality quality;
        public float damage, vN, impulse, time;
        public Vector2 point;
        public bool critical, stagger, launch, knockdown;
        public FighterState defenderStateBefore;
        /// <summary>当てた技</summary>
        public AttackStyle style;
        /// <summary>刺すの先端がガードを貫いた削り（本体には触れていない）</summary>
        public bool pierce;
        /// <summary>二刀流の副武器の当たり（主武器とは別に 1 スイング 1 回まで）</summary>
        public bool offhand;
        /// <summary>スーパーアーマーで踏みとどまった（ダメージは受けたが、ひるまず振り切る）</summary>
        public bool armored;
        /// <summary>連携（打ち上げ → 叩き落とし）の締めが当たった</summary>
        public bool combo;
    }

    [Serializable] public struct GuardEvent
    {
        public int attacker, defender, attackId;
        public float load, vN, time;
        public Vector2 point;
        public bool broke, parried;
    }

    [Serializable] public struct ClashEvent
    {
        public int attacker, defender, attackId;
        public Vector2 point;
        public float time;
    }

    [Serializable] public struct EnvImpactEvent
    {
        public int fighter;
        public bool isWall;
        public float vN, damage, timeSinceLaunch, time;
        public Vector2 point;
        /// <summary>持ち上げからの叩きつけ</summary>
        public bool slam;
        /// <summary>叩き落とされて地面に激突した（連携の締め）</summary>
        public bool smash;
    }

    [Serializable] public struct StateChangeEvent
    {
        public int fighter;
        public FighterState from, to;
        public float time;
    }

    [Serializable] public struct RecoverEvent
    {
        public int fighter;
        public float sinceKnockdown, sinceSettle, time;
        public bool shiftedToSafePosition;
    }

    [Serializable] public struct EvadeEvent
    {
        public int fighter;
        public EvadeKind kind;
        public float time;
    }

    [Serializable] public struct KoEvent
    {
        public int winner;
        public Vector2 point;
        public float time;
        public FinishKind finish;
        public AttackStyle style;
        public Vector2 direction;
    }

    /// <summary>戦闘結果イベント。UI/VFX/テレメトリ/将来のリプレイが購読する。</summary>
    public sealed class CombatEvents
    {
        public event Action<FighterMotionEvent> Motion;
        public event Action<RangedLaunchEvent> RangedLaunch;
        public event Action<SurfaceContactEvent> SurfaceContact;
        public event Action<HitEvent> Hit;
        public event Action<GuardEvent> Guard;
        public event Action<ClashEvent> Clash;
        public event Action<EnvImpactEvent> EnvImpact;
        public event Action<StateChangeEvent> StateChanged;
        public event Action<RecoverEvent> Recovered;
        public event Action<EvadeEvent> Evaded;
        public event Action<KoEvent> KO;
        public event Action<MatchResult> MatchEnded;
        public event Action<InterventionEvent> Intervention;

        public void Raise(FighterMotionEvent e) => Motion?.Invoke(e);
        public void Raise(RangedLaunchEvent e) => RangedLaunch?.Invoke(e);
        public void Raise(SurfaceContactEvent e) => SurfaceContact?.Invoke(e);
        public void Raise(HitEvent e) => Hit?.Invoke(e);
        public void Raise(GuardEvent e) => Guard?.Invoke(e);
        public void Raise(ClashEvent e) => Clash?.Invoke(e);
        public void Raise(EnvImpactEvent e) => EnvImpact?.Invoke(e);
        public void Raise(StateChangeEvent e) => StateChanged?.Invoke(e);
        public void Raise(RecoverEvent e) => Recovered?.Invoke(e);
        public void Raise(EvadeEvent e) => Evaded?.Invoke(e);
        public void Raise(KoEvent e) => KO?.Invoke(e);
        public void Raise(MatchResult r) => MatchEnded?.Invoke(r);
        public void Raise(InterventionEvent e) => Intervention?.Invoke(e);
    }
}
