using UnityEngine;

namespace MojiBattle
{
    /// <summary>特徴量と CombatBalance から能力・質量を純粋計算する（5.3）。</summary>
    public static class StatCalculator
    {
        public static float WeightScore(in NormalizedGlyphFeatures n, CombatBalance b) =>
            Mathf.Clamp(b.mBase + b.mA * n.A + b.mC * n.C + b.mH * n.H, b.mMin, b.mMax);

        public static FighterStats Compute(in NormalizedGlyphFeatures n, CombatBalance b)
        {
            float m = WeightScore(n, b);
            float attack = Mathf.Clamp(b.atkM * m + b.atkC * n.C, b.atkMin, b.atkMax);
            float defense = Mathf.Clamp(b.defW * n.W + b.defA * n.A + b.defM * m, b.defMin, b.defMax);
            float speed = Mathf.Clamp(b.spdBase - b.spdM * m - b.spdC * n.C + b.spdB * n.B, b.spdMin, b.spdMax);
            float durability = Mathf.Clamp(b.durA * n.A + b.durM * m + b.durB * n.B, b.durMin, b.durMax);
            var s = new FighterStats
            {
                weightScore = Mathf.RoundToInt(m),
                attack = Mathf.RoundToInt(attack),
                defense = Mathf.RoundToInt(defense),
                speed = Mathf.RoundToInt(speed),
                durability = Mathf.RoundToInt(durability),
            };
            // 質量は丸め後の表示値ではなく式の値から算出（表示値を物理へ二重に掛けない）
            s.maxHp = b.hpBase + b.hpPerDurability * s.durability;
            s.weaponMass = b.weaponMassBase + b.weaponMassPerM * m;
            s.bodyMass = b.bodyMassBase + b.bodyMassPerDurability * durability;
            return s;
        }

        public static FighterStats Compute(GlyphDefinitionRuntime glyph, CombatBalance b)
        {
            var s = Compute(glyph.normalized, b);
            s.weaponCenterOfMass = glyph.features.centerOfMass;
            return s;
        }

        public static WeightClass Classify(int weightScore, CombatBalance b) =>
            weightScore < b.lightMaxWeight ? WeightClass.Light
            : weightScore >= b.heavyMinWeight ? WeightClass.Heavy
            : WeightClass.Medium;

        public static float Lerp01(float a, float b, int weightScore) => Mathf.Lerp(a, b, weightScore / 100f);

        /// <summary>溜め時間。M に対して曲線（指数 windupCurve）で補間し、中量級を短めにする（両端は仕様の例に近い）。</summary>
        public static float Windup(int m, CombatBalance b) =>
            Mathf.Lerp(b.windupLight, b.windupHeavy, Mathf.Pow(Mathf.Clamp01(m / 100f), b.windupCurve));
        public static float Active(int m, CombatBalance b) => Lerp01(b.activeLight, b.activeHeavy, m);
        public static float Recovery(int m, CombatBalance b) => Lerp01(b.recoveryLight, b.recoveryHeavy, m);
        public static float WalkSpeed(int speed, CombatBalance b) => Mathf.Lerp(b.walkSpeedMin, b.walkSpeedMax, speed / 100f);
    }
}
