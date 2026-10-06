using System;
using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    [Serializable]
    public class FighterTelemetry
    {
        public static readonly int StyleCount = Enum.GetValues(typeof(AttackStyle)).Length;
        public string grapheme;
        public string weightClass;
        public FighterStats stats;
        public float attackRange, walkSpeed;
        public int weaponColliders;
        public float[] stateSeconds = new float[10];
        public List<float> windups = new List<float>();
        public List<float> hitImpulses = new List<float>();
        public List<float> hitDamages = new List<float>();
        public List<float> recoverSinceSettle = new List<float>();
        public List<float> recoverSinceKnockdown = new List<float>();
        public int safeShifts;
        public int guardEvents, evadeEvents, attackStarts;
        /// <summary>ガード姿勢に入った回数 / 攻撃シーケンス数（連撃は1つと数える）</summary>
        public int guardEntries, attackSequences;
        /// <summary>振り（突き以外）で武器が予定の振り幅の 4 割未満しか動かなかった回数（武器が挟まって振れない固着）</summary>
        public int jammedSwings, swings;
        /// <summary>有効時間開始時の距離 / 自分の射程 の合計（攻撃が届く距離で振っているか）</summary>
        public float distAtActiveSum; public int distAtActiveCount;
        [NonSerialized] public float swingPsiMin, swingPsiMax;
        /// <summary>振り（有効時間）ごとの武器の最大角速度（度/秒）と切っ先の最大速度の合計（カスタマイズの振りの速さ・重さの比較用）</summary>
        public float peakSwingOmegaSum, peakTipSpeedSum; public int peakSwingCount;
        [NonSerialized] public float swingPeakOmega;
        /// <summary>握り（ヒンジ）の最大のずれ（物理の破綻の検出用）</summary>
        public float maxHingeStretch;
        /// <summary>握りのずれが 0.2 を超えていたステップ数</summary>
        public int stretchedSteps;
        public int[] stretchStateSteps = new int[10];
        public int maxComboHits;
        /// <summary>横振りの回数 / 横振りの命中</summary>
        public int sweeps, sweepHits;
        /// <summary>技ごとの開始回数 / 命中 / 与ダメージ（AttackStyle の順）</summary>
        public int[] styleStarts = new int[StyleCount], styleHits = new int[StyleCount];
        public float[] styleDamage = new float[StyleCount];
    }

    /// <summary>受け入れ条件の確認用に、イベントと状態時間を集計する。UI・戦闘計算は持たない。</summary>
    [Serializable]
    public class MatchTelemetry
    {
        public int seed;
        public FighterTelemetry[] fighters = { new FighterTelemetry(), new FighterTelemetry() };
        public int duplicateBodyHits;
        public int envDamageEvents;
        public int envDamageOutsideLaunchWindow;
        public int slams;
        public float longestNoDamageSeconds;
        /// <summary>膠着の指標: 両者とも攻撃を始めなかった最長時間。</summary>
        public float longestIdleSeconds;
        public float firstEngageTime = -1f;
        public int clashes;
        public int koCount;
        public float koTime = -1f;
        public float maxStepMilliseconds;
        public float totalStepMilliseconds;
        public int steps;
        public string finishReason;
        public int winner = -2;
        public float elapsed;
        public float[] hpRemaining;
        public List<string> log = new List<string>();
        public static bool Trace;

        [NonSerialized] readonly MatchDirector director;
        [NonSerialized] readonly Dictionary<long, int> bodyHitsPerSwing = new Dictionary<long, int>();
        [NonSerialized] readonly int[] comboCounter = new int[2];
        [NonSerialized] float lastDamageClock;
        [NonSerialized] float lastAttackClock;

        public MatchTelemetry(MatchDirector director)
        {
            this.director = director;
            seed = director.Config.seed;
            for (int i = 0; i < 2; i++)
            {
                var f = director.Fighters[i];
                var t = fighters[i];
                t.grapheme = f.Loadout.grapheme;
                t.weightClass = f.WeightClass.ToString();
                t.stats = f.Stats;
                t.attackRange = f.AttackRange;
                t.walkSpeed = f.WalkSpeed;
                t.weaponColliders = f.WeaponColliders.Length;
            }
            var ev = director.Events;
            ev.Hit += OnHit;
            ev.Guard += e => { fighters[e.defender].guardEvents++; Log($"GUARD {Side(e.defender)} load={e.load:F1}{(e.broke ? " BREAK" : "")}"); };
            ev.Clash += e => clashes++;
            ev.EnvImpact += OnEnv;
            ev.StateChanged += OnState;
            ev.Recovered += e =>
            {
                var t = fighters[e.fighter];
                if (e.sinceSettle >= 0f) t.recoverSinceSettle.Add(e.sinceSettle);
                t.recoverSinceKnockdown.Add(e.sinceKnockdown);
                if (e.shiftedToSafePosition) t.safeShifts++;
                Log($"RECOVER {Side(e.fighter)} settle+{e.sinceSettle:F2}s down+{e.sinceKnockdown:F2}s{(e.shiftedToSafePosition ? " (safe shift)" : "")}");
            };
            ev.Evaded += e => { fighters[e.fighter].evadeEvents++; Log($"EVADE {Side(e.fighter)} {e.kind}"); };
            ev.KO += e => { koCount++; koTime = director.Clock; Log($"KO winner={e.winner}"); };
        }

        static string Side(int id) => id == 0 ? "L" : "R";

        public void Note(string s) { if (Trace) Log(s); }

        void Log(string s)
        {
            if (log.Count < (Trace ? 3000 : 400)) log.Add($"{director.Clock:F2} {s}");
        }

        void OnHit(HitEvent e)
        {
            long key = ((long)e.attackId << 8) | (uint)e.defender;
            bodyHitsPerSwing.TryGetValue(key, out int n);
            bodyHitsPerSwing[key] = n + 1;
            if (n >= 1) duplicateBodyHits++;
            var t = fighters[e.attacker];
            t.hitImpulses.Add(e.impulse);
            if (e.style == AttackStyle.Sweep) t.sweepHits++;
            t.styleHits[(int)e.style]++;
            t.styleDamage[(int)e.style] += e.damage;
            t.hitDamages.Add(e.damage);
            comboCounter[e.attacker]++;
            comboCounter[e.defender] = 0;
            t.maxComboHits = Mathf.Max(t.maxComboHits, comboCounter[e.attacker]);
            NoteDamage();
            if (Trace && e.defenderStateBefore == FighterState.Guard)
            {
                var dfn = director.Fighters[e.defender];
                var atk = director.Fighters[e.attacker];
                Log($"  guard-hit: defPsi={dfn.WeaponMotor.CurrentPsi:F0} target={dfn.Runtime.guardPsiTarget:F0} defFacing={dfn.Facing} toward={dfn.TowardOpponent} pt=({e.point.x - dfn.X:F2},{e.point.y:F2}) atkStyle={atk.Runtime.attackStyle} guardTime={dfn.Runtime.stateTime:F2}");
            }
            Log($"HIT {Side(e.attacker)}->{Side(e.defender)} [{e.defenderStateBefore}] {e.part} {e.quality} dmg={e.damage:F1} vN={e.vN:F1} imp={e.impulse:F1}{(e.knockdown ? " DOWN" : e.launch ? " LAUNCH" : e.stagger ? " STAGGER" : "")}");
        }

        void OnEnv(EnvImpactEvent e)
        {
            envDamageEvents++;
            float window = e.slam ? director.Context.Balance.slamWindow : director.Context.Balance.launchWindow;
            if (e.timeSinceLaunch > window || e.timeSinceLaunch < 0f) envDamageOutsideLaunchWindow++;
            if (e.slam) slams++;
            NoteDamage();
            Log($"ENV{(e.slam ? " SLAM" : "")} {Side(e.fighter)} {(e.isWall ? "WALL" : "GROUND")} vN={e.vN:F1} dmg={e.damage:F1} t+{e.timeSinceLaunch:F2}");
        }

        void OnState(StateChangeEvent e)
        {
            var t = fighters[e.fighter];
            if (e.to == FighterState.Guard) t.guardEntries++;
            var fr = director.Fighters[e.fighter].Runtime;
            if (e.to == FighterState.AttackActive)
            {
                t.swingPsiMin = 999f; t.swingPsiMax = -999f;
                t.swingPeakOmega = 0f;
                var fa = director.Fighters[e.fighter];
                t.distAtActiveSum += fa.DistanceToOpponent / Mathf.Max(0.1f, fa.AttackRange);
                t.distAtActiveCount++;
            }
            if (e.from == FighterState.AttackActive)
            {
                t.peakSwingOmegaSum += t.swingPeakOmega;
                t.peakTipSpeedSum += t.swingPeakOmega * Mathf.Deg2Rad * director.Fighters[e.fighter].Weapon.length;
                t.peakSwingCount++;
            }
            if (e.from == FighterState.AttackActive && (fr.attackStyle == AttackStyle.Overhead || fr.attackStyle == AttackStyle.Rising))
            {
                t.swings++;
                float planned = Mathf.Abs(fr.swingToPsi - fr.swingFromPsi);
                if (t.swingPsiMax - t.swingPsiMin < planned * 0.4f) t.jammedSwings++;
            }
            if (e.to == FighterState.AttackWindup && e.from != FighterState.AttackRecovery) t.attackSequences++;
            if (e.to == FighterState.AttackWindup)
            {
                float idle = director.Clock - lastAttackClock;
                if (idle > longestIdleSeconds) longestIdleSeconds = idle;
                lastAttackClock = director.Clock;
                t.attackStarts++;
                var st = director.Fighters[e.fighter].Runtime.attackStyle;
                if (st == AttackStyle.Sweep) t.sweeps++;
                t.styleStarts[(int)st]++;
                t.windups.Add(director.Fighters[e.fighter].Runtime.windupDuration);
            }
            if (e.to == FighterState.Knockdown) Log($"KNOCKDOWN {Side(e.fighter)}");
            if (Trace)
            {
                var f = director.Fighters[e.fighter];
                Log($"ST {Side(e.fighter)} {e.from}->{e.to} d={f.DistanceToOpponent:F2} x={f.X:F2} psi={f.WeaponMotor.MeasurePsi():F0} '{f.Brain.LastDecision}' style={f.Runtime.attackStyle}");
            }
        }

        void NoteDamage()
        {
            float gap = director.Clock - lastDamageClock;
            if (gap > longestNoDamageSeconds) longestNoDamageSeconds = gap;
            lastDamageClock = director.Clock;
        }

        public void Step(float dt)
        {
            if (director.CountdownRemaining > 0f) return;
            for (int i = 0; i < 2; i++)
            {
                var f = director.Fighters[i];
                var t = fighters[i];
                t.stateSeconds[(int)f.Runtime.state] += dt;
                if (f.Runtime.state == FighterState.AttackActive)
                {
                    float psi = f.WeaponMotor.CurrentPsi;
                    t.swingPsiMin = Mathf.Min(t.swingPsiMin, psi);
                    t.swingPsiMax = Mathf.Max(t.swingPsiMax, psi);
                    t.swingPeakOmega = Mathf.Max(t.swingPeakOmega, Mathf.Abs(f.WeaponBody.angularVelocity - f.Body.angularVelocity));
                }
                // 投げて手を離れている武器は握りのずれに数えない
                float stretch = f.Runtime.weaponDetached ? 0f : (f.WeaponBody.position - f.Body.GetRelativePoint(f.HandLocal(f.Facing))).magnitude;
                if (stretch > t.maxHingeStretch) t.maxHingeStretch = stretch;
                if (stretch > 0.2f)
                {
                    t.stretchedSteps++;
                    t.stretchStateSteps[(int)f.Runtime.state]++;
                    if (Trace && t.stretchedSteps % 10 == 1)
                        Log($"STRETCH {Side(i)} {stretch:F2} {f.Runtime.state} '{f.Brain.LastDecision}' y={f.Body.position.y:F2} vy={f.Body.linearVelocity.y:F1} psi={f.WeaponMotor.CurrentPsi:F0} tgt={f.WeaponMotor.TargetPsi:F0} tq={f.WeaponMotor.LastTorque:F0} w={f.WeaponBody.angularVelocity:F0} d={f.DistanceToOpponent:F2}");
                }
            }
            if (Trace && director.StepCount % 50 == 0)
            {
                var l = director.Fighters[0]; var r = director.Fighters[1];
                Log($"TICK L[{l.Runtime.state} x={l.X:F2} y={l.Body.position.y:F2} vx={l.Body.linearVelocity.x:F2} g={l.IsGrounded} mv={l.Brain.Move} '{l.Brain.LastDecision}'] " +
                    $"R[{r.Runtime.state} x={r.X:F2} y={r.Body.position.y:F2} vx={r.Body.linearVelocity.x:F2} g={r.IsGrounded} mv={r.Brain.Move} '{r.Brain.LastDecision}' rot={r.Body.rotation:F0}]");
            }
            if (firstEngageTime < 0f)
            {
                var a = director.Fighters[0];
                if (a.DistanceToOpponent <= Mathf.Max(a.AttackRange, a.Opponent.AttackRange)) firstEngageTime = director.Clock;
            }
        }

        public void RecordStepCost(float ms)
        {
            steps++;
            totalStepMilliseconds += ms;
            if (ms > maxStepMilliseconds) maxStepMilliseconds = ms;
        }

        public void Finish(MatchResult r)
        {
            float gap = director.Clock - lastDamageClock;
            if (gap > longestNoDamageSeconds) longestNoDamageSeconds = gap;
            float idleEnd = director.Clock - lastAttackClock;
            if (idleEnd > longestIdleSeconds) longestIdleSeconds = idleEnd;
            finishReason = r.finishReason;
            winner = r.winner;
            elapsed = r.elapsedSeconds;
            hpRemaining = r.hpRemaining;
            Log($"END {r.finishReason} winner={r.winner} hp={r.hpRemaining[0]:F1}/{r.hpRemaining[1]:F1}");
        }
    }
}
