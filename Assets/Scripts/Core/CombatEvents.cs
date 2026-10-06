using System;
using UnityEngine;

namespace MojiBattle
{
    public struct HitEvent
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
    }

    public struct GuardEvent
    {
        public int attacker, defender, attackId;
        public float load, vN, time;
        public Vector2 point;
        public bool broke;
    }

    public struct ClashEvent
    {
        public int attacker, defender, attackId;
        public Vector2 point;
        public float time;
    }

    public struct EnvImpactEvent
    {
        public int fighter;
        public bool isWall;
        public float vN, damage, timeSinceLaunch, time;
        public Vector2 point;
        /// <summary>持ち上げからの叩きつけ</summary>
        public bool slam;
    }

    public struct StateChangeEvent
    {
        public int fighter;
        public FighterState from, to;
        public float time;
    }

    public struct RecoverEvent
    {
        public int fighter;
        public float sinceKnockdown, sinceSettle, time;
        public bool shiftedToSafePosition;
    }

    public struct EvadeEvent
    {
        public int fighter;
        public EvadeKind kind;
        public float time;
    }

    public struct KoEvent
    {
        public int winner;
        public Vector2 point;
        public float time;
    }

    /// <summary>戦闘結果イベント。UI/VFX/テレメトリ/将来のリプレイが購読する。</summary>
    public sealed class CombatEvents
    {
        public event Action<HitEvent> Hit;
        public event Action<GuardEvent> Guard;
        public event Action<ClashEvent> Clash;
        public event Action<EnvImpactEvent> EnvImpact;
        public event Action<StateChangeEvent> StateChanged;
        public event Action<RecoverEvent> Recovered;
        public event Action<EvadeEvent> Evaded;
        public event Action<KoEvent> KO;
        public event Action<MatchResult> MatchEnded;

        public void Raise(HitEvent e) => Hit?.Invoke(e);
        public void Raise(GuardEvent e) => Guard?.Invoke(e);
        public void Raise(ClashEvent e) => Clash?.Invoke(e);
        public void Raise(EnvImpactEvent e) => EnvImpact?.Invoke(e);
        public void Raise(StateChangeEvent e) => StateChanged?.Invoke(e);
        public void Raise(RecoverEvent e) => Recovered?.Invoke(e);
        public void Raise(EvadeEvent e) => Evaded?.Invoke(e);
        public void Raise(KoEvent e) => KO?.Invoke(e);
        public void Raise(MatchResult r) => MatchEnded?.Invoke(r);
    }
}
