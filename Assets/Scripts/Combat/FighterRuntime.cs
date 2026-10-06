using UnityEngine;

namespace MojiBattle
{
    /// <summary>試合中に変化する値。MatchConfig / FighterStats は不変のまま分離する。</summary>
    public sealed class FighterRuntime
    {
        public float hp, maxHp;
        public FighterState state = FighterState.Approach;
        public FighterState prevState = FighterState.Approach;
        public float stateTime;

        // 攻撃
        public int attackSerial;
        public int currentAttackId = -1;
        public int comboRemaining;
        public bool attackHadContact;
        public float windupDuration, activeDuration, recoveryDuration;
        public float swingFromPsi, swingToPsi;
        public AttackStyle attackStyle;
        public float attackReadyAt;
        public bool clashedThisAttack;
        public bool weaponPassThrough;
        /// <summary>横振りの奥行き方向の角度（度）。0 = 字形が前を向く、180 = 後ろ、±90 = 奥／手前を向いて細く見える</summary>
        public float sweepYaw;
        /// <summary>横振りの角速度（度/秒、前へ振る向きが負）</summary>
        public float sweepYawRate;
        /// <summary>このガードは横振りに反応して構えた（回り込む横振りも止められる）</summary>
        public bool guardAgainstSweep;
        /// <summary>この時刻まで武器の保持トルクを抜く（自分の武器を地面に突いて体が浮いた時に落とす）</summary>
        public float weaponLimpUntil = -1f;

        // 防御・転倒
        public float guardLoad;
        public float guardPsiTarget = float.NaN;
        public float knockdownAccum;
        public float stateDurationOverride;
        public float launchedAt = -999f;
        public bool launchWallUsed, launchGroundUsed;
        public float launchOriginX;
        public bool launchTracking;
        public float knockdownStartedAt = -1f;

        // 叩きつけ（相手の武器で持ち上げられた）
        public bool liftActive, slamUsed;
        public float liftedAt = -999f, groundedSince, liftPeakY;
        public float settledAt = -1f;
        public float recoverAt = -1f;

        // 回避
        public float evadeReadyAt;
        public float evadeEndedAt = -999f;
        public EvadeKind evadeKind;

        public readonly FighterMetrics metrics = new FighterMetrics();

        public bool IsDown => state == FighterState.Knockdown || state == FighterState.KO;
        public bool CanBeControlled => state == FighterState.Approach || state == FighterState.Guard;

        public void ApplyDamage(float amount)
        {
            hp -= amount;
            metrics.damageTaken += amount;
        }

        public int NextAttackId(int fighterId) => fighterId * 100000 + (++attackSerial);

        public void Decay(float dt, CombatBalance b)
        {
            guardLoad = Mathf.Max(0f, guardLoad - b.guardLoadDecayPerSec * dt);
            knockdownAccum = Mathf.Max(0f, knockdownAccum - b.knockdownAccumDecayPerSec * dt);
        }
    }
}
