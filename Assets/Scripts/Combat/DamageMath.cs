using UnityEngine;

namespace MojiBattle
{
    /// <summary>8.2 / 8.3 の式（純粋関数）。</summary>
    public static class DamageMath
    {
        public static float SpeedFactor(float vN, CombatBalance b) =>
            Mathf.Clamp(vN / b.speedDivisor, b.speedFactorMin, b.speedFactorMax);

        public static float Raw(int attack, float vN, HitQuality q, BodyPart part, CombatBalance b) =>
            attack * SpeedFactor(vN, b) * b.QualityMultiplier(q) * b.PartMultiplier(part) * b.damageScale;

        public static float Final(float raw, int defense, CombatBalance b) =>
            Mathf.Max(1f, raw * (100f / (100f + b.defenseK * defense)));

        public static float BodyDamage(int attack, int defense, float vN, HitQuality q, BodyPart part, CombatBalance b) =>
            Final(Raw(attack, vN, q, part, b), defense, b);

        /// <summary>1 発の本体ダメージを相手の最大 HP の一定割合までに抑える。</summary>
        public static float CapHit(float damage, float defenderMaxHp, CombatBalance b) =>
            Mathf.Min(damage, defenderMaxHp * b.maxHitHpFraction);

        /// <summary>式どおりのインパルス量（閾値判定に使う）。実際に加える力は impulseScale 倍。</summary>
        public static float Impulse(float vN, float attackerWeaponMass, float defenderBodyMass, CombatBalance b) =>
            Mathf.Clamp(vN * attackerWeaponMass * b.impulseFactor / (defenderBodyMass + 0.5f), 0f, b.impulseMax);

        public static float EnvironmentDamage(float vN, CombatBalance b) =>
            Mathf.Clamp((vN - b.envDamageMinSpeed) * b.envDamageK, 0f, b.envDamageMax);

        /// <summary>叩きつけダメージ（相手の武器で持ち上げられて落とされた時）。</summary>
        public static float SlamDamage(float vN, CombatBalance b) =>
            Mathf.Clamp((vN - b.slamMinSpeed) * b.slamK, 0f, b.slamMax);

        /// <summary>叩きつけの可否: 持ち上げられていて、離れてから slamWindow 以内、速度が閾値以上、この持ち上げで未使用。</summary>
        public static bool SlamAllowed(float vN, float now, float liftedAt, bool liftActive, bool alreadyUsed, CombatBalance b) =>
            liftActive && !alreadyUsed && vN >= b.slamMinSpeed && now >= liftedAt && now - liftedAt <= b.slamWindow;

        /// <summary>壁・地面の追加ダメージ可否（8.3）。</summary>
        public static bool EnvironmentDamageAllowed(float vN, float now, float launchedAt, bool alreadyUsedThisKind, CombatBalance b) =>
            vN >= b.envDamageMinSpeed && now - launchedAt <= b.launchWindow && now >= launchedAt && !alreadyUsedThisKind;
    }
}
