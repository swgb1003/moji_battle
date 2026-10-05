using System;
using UnityEngine;

namespace MojiBattle
{
    [Serializable]
    public class ClassTendency
    {
        [Tooltip("1回の攻撃判断で出す連撃数の範囲")]
        public int comboMin = 1, comboMax = 1;
        [Range(0, 1)] public float attackWillingness = 0.8f;
        [Range(0, 1)] public float guardBias = 0.4f;
        [Range(0, 1)] public float evadeBias = 0.5f;
        [Tooltip("相手が射程内で攻撃していない時に先にガードを構える確率")]
        [Range(0, 1)] public float proactiveGuard = 0.05f;
        [Tooltip("攻撃後に離脱する確率")]
        [Range(0, 1)] public float retreatAfterAttack = 0.5f;
        [Tooltip("中央より手前で待機して相手の接近を誘う")]
        public bool waitsNearCenter;
        [Range(0, 1)] public float aimHeadChance = 0.4f;
        [Tooltip("相手の攻撃に対して、受けずに自分も攻撃を始める（反撃）重み")]
        [Range(0, 1)] public float counterBias;
        [Tooltip("AI判断1回あたりに相手の攻撃予兆へ反応できる確率（読み違い・反応遅れ）")]
        [Range(0, 1)] public float reactionChance = 0.6f;
    }

    /// <summary>
    /// 能力式・AI重み・ダメージ閾値・物理係数。仕様書 5.3 / 7 / 8 の初期値を既定値にしている。
    /// "Tuned" と書いた項目は P1 のシード検証で調整した値。
    /// </summary>
    [CreateAssetMenu(menuName = "MojiBattle/Combat Balance", fileName = "CombatBalance")]
    public class CombatBalance : ScriptableObject
    {
        [Header("5.3 ステータス式")]
        public float mBase = 5f, mA = 0.70f, mC = 0.20f, mH = 0.10f, mMin = 5f, mMax = 100f;
        [Tooltip("Tuned: 攻撃の下限 20→30（軽い字形の与ダメージが小さすぎたため。仕様の参考値「一」攻撃31）")]
        public float atkM = 0.60f, atkC = 0.40f, atkMin = 30f, atkMax = 100f;
        public float defW = 0.50f, defA = 0.30f, defM = 0.20f, defMin = 20f, defMax = 100f;
        public float spdBase = 105f, spdM = 0.85f, spdC = 0.10f, spdB = 0.05f, spdMin = 10f, spdMax = 100f;
        public float durA = 0.50f, durM = 0.30f, durB = 0.20f, durMin = 20f, durMax = 100f;
        public float hpBase = 60f, hpPerDurability = 1.4f;
        public float weaponMassBase = 1.1f, weaponMassPerM = 0.035f;
        public float bodyMassBase = 2.0f, bodyMassPerDurability = 0.008f;
        public float lightMaxWeight = 35f, heavyMinWeight = 70f;
        public float highDefenseGuardThreshold = 70f, highDefenseGuardBonus = 0.25f;

        [Header("5.1/6.1 字形・Collider")]
        public float alphaThreshold = 0.5f;
        public float emSizePx = 240f;
        public int colliderGrid = 64;
        public float cellOccupancy = 0.35f;
        public int maxColliders = 64;
        public int minComponentPixels = 16;
        public bool dilateForColliders = true;

        [Header("6.2 武器")]
        public float weaponMaxSide = 2.2f;
        public Vector2 shoulderLocal = new Vector2(0.22f, 1.32f);
        public float weaponAngularDamping = 0.05f;
        public float weaponMaxAngularSpeedDeg = 1440f;
        [Tooltip("固有角振動数（軽量→重量）")]
        public float poseFrequencyLight = 38f, poseFrequencyHeavy = 11f;
        public float poseDamping = 0.9f;
        [Tooltip("最大角加速度 rad/s^2（軽量）。重量側は倍率で下げる")]
        public float maxAngularAccel = 300f;
        public float heavyAccelFactor = 0.3f;
        public float gravityCompensation = 0.9f;
        [Tooltip("Tuned: 待機・回避中の武器保持トルク倍率（ガード・攻撃時は1）")]
        public float idleHoldTorqueScale = 0.35f;
        [Tooltip("ψ: 握り→重心ベクトルの前方水平からの角度（度）。Tuned: 待機姿勢は軽量=相手の胴へ向けた槍構え、重量=肩に担いで後ろへ倒す")]
        public float readyPsiLight = -6f, readyPsiHeavy = 125f, guardPsi = 72f;
        [Tooltip("Tuned: 振り下ろしに対する高いガード / 斬り上げ・突きに対する低いガード")]
        public float guardPsiHigh = 98f, guardPsiLow = -15f, guardPsiHeavyLow = 30f;
        [Tooltip("斬り上げの開始角の下限（地面を擦らない）")]
        public float risingMinPsi = -40f;
        public float swingArcLight = 80f, swingArcHeavy = 170f;
        public float strikeOvershoot = 25f;

        [Header("7.1/7.2 AI・攻撃時間")]
        public float aiInterval = 0.15f;
        public float windupLight = 0.20f, windupHeavy = 1.20f;
        [Tooltip("Tuned: 溜め時間の補間の指数（1 = 仕様どおり線形。大きいほど中量級が短い）")]
        public float windupCurve = 1.6f;
        public float activeLight = 0.10f, activeHeavy = 0.60f;
        public float recoveryLight = 0.18f, recoveryHeavy = 0.95f;
        public float attackTimingJitter = 0.12f;
        public float threatWindow = 0.6f;
        public float guardMinDwell = 0.3f;
        public float evadeCooldown = 1.0f;
        public float tooCloseDistance = 0.9f;
        [Tooltip("Tuned: これより近いと武器が相手を通り越すため、下がってから打つ")]
        public float minAttackDistance = 0.8f;
        [Tooltip("Tuned: 軽量・中量級が保つ間合い（射程に対する割合）。これより近いと下がってから打つ")]
        public float preferredSpacingFraction = 0.62f;
        public float wallDangerDistance = 1.2f;
        public float minBackstepSpace = 1.6f;
        public float heavyHoldDistanceFromCenter = 1.5f;
        public float stalemateSeconds = 6f;
        [Tooltip("Tuned: 転倒中の相手への追撃を許可する（KO中は不可）")]
        public bool allowAttackOnDowned = true;
        [Tooltip("Tuned: 前進が阻まれた（武器や体が当たって近づけない）と判定するまでの時間")]
        public float blockedAdvanceSeconds = 0.45f;
        public float heavyRangeSlack = 1.05f;
        [Tooltip("突きを選ぶ確率（軽量→重量で補間）と踏み込み速度")]
        public float thrustChanceLight = 0.7f, thrustChanceHeavy = 0.05f;
        public float lungeSpeedLight = 12f, lungeSpeedHeavy = 3f;
        [Tooltip("振り（突き以外）の有効時間開始時の踏み込み速度（軽量→重量で補間）")]
        public float stepInSpeedLight = 1.0f, stepInSpeedHeavy = 1.8f;
        [Tooltip("相手の武器が胸より上/下にある時に斬り上げを選ぶ確率（軽量・中量 / 重量）")]
        public float risingWhenHighLight = 0.75f, risingWhenLowLight = 0.3f, risingWhenHighHeavy = 0.25f, risingWhenLowHeavy = 0.1f;
        public float attackCooldownLight = 0.15f, attackCooldownHeavy = 1.2f;
        public float heavyWhiffRecoveryMultiplier = 1.4f;
        [Tooltip("Tuned: 攻撃後の硬直のうち、武器を振り切った位置に残す割合")]
        public float followThroughFraction = 0.6f;
        [Tooltip("Tuned: 相手の硬直に差し込むため、射程外から踏み込める距離（歩行速度×残り硬直×係数）")]
        public float punishReachFactor = 0.8f;
        public ClassTendency light = new ClassTendency
        {
            comboMin = 2, comboMax = 3, attackWillingness = 0.9f, guardBias = 0.15f, evadeBias = 0.85f,
            proactiveGuard = 0.02f, retreatAfterAttack = 0.6f, waitsNearCenter = false, aimHeadChance = 0.5f, reactionChance = 0.75f
        };
        public ClassTendency medium = new ClassTendency
        {
            comboMin = 1, comboMax = 2, attackWillingness = 0.75f, guardBias = 0.5f, evadeBias = 0.5f,
            proactiveGuard = 0.1f, retreatAfterAttack = 0.4f, waitsNearCenter = false, aimHeadChance = 0.4f
        };
        public ClassTendency heavy = new ClassTendency
        {
            comboMin = 1, comboMax = 1, attackWillingness = 0.55f, guardBias = 0.6f, evadeBias = 0.15f,
            proactiveGuard = 0.3f, retreatAfterAttack = 0f, waitsNearCenter = true, aimHeadChance = 0.35f, counterBias = 0.3f
        };

        [Header("移動・回避")]
        public float walkSpeedMin = 1.4f, walkSpeedMax = 4.5f;
        public float moveAccelGain = 12f, maxMoveAccel = 28f;
        public float backstepSpeed = 7.5f, backstepHop = 2.2f;
        public float hopOverSpeedX = 6.0f, hopOverSpeedY = 7.8f;
        public float evadeMinTime = 0.25f, evadeMaxTime = 1.1f;

        [Header("8.2 ダメージ")]
        public float damageScale = 0.22f;
        public float defenseK = 0.45f;
        public float speedDivisor = 6f, speedFactorMin = 0.4f, speedFactorMax = 1.8f;
        public float minHitRelativeSpeed = 1.5f;
        public float qualityCenter = 1.3f, qualityNormal = 1.0f, qualityGraze = 0.55f;
        public float grazeEdgeFraction = 0.07f, grazeTipFraction = 0.95f, centerFraction = 0.3f;
        public float headMultiplier = 1.5f, torsoMultiplier = 1.0f, armMultiplier = 0.7f, legMultiplier = 0.8f;
        public float guardHalfAngle = 80f;
        public float guardLoadPerMomentum = 1.0f, guardBreakThreshold = 70f, guardLoadDecayPerSec = 4f;
        public float guardBreakStagger = 0.7f;
        public float heavyHitDamage = 15f, hitStopSeconds = 0.06f;

        [Header("8.3 衝撃・環境")]
        public float impulseFactor = 0.7f, impulseMax = 18f;
        [Tooltip("Tuned: 式で得たインパルスを実際の AddForce へ掛ける倍率")]
        public float impulseScale = 2.0f;
        public float launchUpBias = 0.35f;
        public float guardImpulseRatio = 0.5f;
        public float clashPush = 1.5f;
        [Tooltip("Tuned: 弾き合いで重い武器が軽い武器を叩き落とす強さ")]
        public float clashWeaponKnock = 1.0f;
        [Tooltip("Tuned: 攻撃側の武器質量がこの倍率以上なら、弾き合っても軽い武器を叩き落として振り抜く")]
        public float overpowerMassRatio = 1.8f;
        [Tooltip("Tuned: 弾き合いで、勢い（質量×速度）がこの倍率以上なら相手の武器を押し退けて振り抜く")]
        public float overpowerMomentumRatio = 1.3f;
        [Tooltip("Tuned: 弾き合いで勢い負けした側のよろけ時間（その間に勝った側が追撃できる）")]
        public float clashLoserStagger = 0.45f;
        [Tooltip("Tuned: AI が使う射程 = 肩 + 武器の最遠点距離 × この値（四角い字形は角でしか届かないため控えめに）")]
        public float reachFactor = 0.75f;

        [Header("試合後半の攻め（残り時間が少ないほど攻撃的）")]
        [Tooltip("この経過秒から攻めが強まり始め、Ramp 秒かけて最大になる")]
        public float lateGameStart = 20f, lateGameRamp = 30f;
        [Tooltip("最大時に攻撃意欲へ足す量 / ガード・待ちの選びやすさを減らす割合")]
        public float lateAttackBonus = 0.3f, lateDefenseReduction = 0.7f;
        public float staggerImpulse = 5.5f, launchImpulse = 7f, knockdownImpulse = 12f, legKnockdownImpulse = 8f;
        public float staggerDuration = 0.45f;
        public float knockdownAccumPerImpulse = 1f, legKnockdownBonus = 3f, knockdownAccumThreshold = 16f, knockdownAccumDecayPerSec = 4f;
        public float envDamageMinSpeed = 7f, envDamageK = 1.2f, envDamageMax = 18f, launchWindow = 1.5f;
        public float envAccumPerSpeed = 0.5f;
        public float heavyWhiffStaggerMomentum = 40f;
        [Tooltip("安全上限: 本体・武器の最大速度（物理の破綻防止。壁衝突ダメージの閾値7より十分大きい）")]
        public float maxBodySpeed = 22f, maxWeaponSpeed = 30f;

        [Header("8.4 転倒・KO")]
        public float settleSpeed = 0.6f, settleAngularSpeed = 90f;
        public float recoverDelayMin = 1.0f, recoverDelayMax = 2.0f;
        public float maxKnockdownTime = 4.0f;
        public float recoverDuration = 0.35f;
        public float koTimeScale = 0.25f, koPresentationSeconds = 0.7f;

        [Header("4 試合")]
        public float matchDuration = 60f;
        public float startX = 5.5f;
        public float arenaHalfWidth = 9f;
        public float countdownSeconds = 3f;
        [Tooltip("仕様変更（P2 で合意）: 時間切れで同率なら延長戦。先にダメージが入った時点で判定。上限を過ぎても同率なら引き分け")]
        public bool suddenDeath = true;
        public float suddenDeathMaxSeconds = 30f;

        /// <summary>下段ガード角。大きい字形ほど浅くする（地面に突き立てて体が浮かないように）。</summary>
        public float GuardPsiLow(int weightScore) => Mathf.Lerp(guardPsiLow, guardPsiHeavyLow, Mathf.InverseLerp(lightMaxWeight, heavyMinWeight, weightScore));

        /// <summary>待機姿勢。軽量（槍構え）から、中量以上はすぐ肩担ぎへ寄せる（大きな字形を体の前に立てると打ち合いが弾きだけになる）。</summary>
        public float ReadyPsi(int weightScore) => Mathf.Lerp(readyPsiLight, readyPsiHeavy, Mathf.InverseLerp(lightMaxWeight, lightMaxWeight + readyBlendRange, weightScore));
        [Tooltip("Tuned: 軽量の上限からこの幅の重量で肩担ぎへ移る")]
        public float readyBlendRange = 15f;
        [Tooltip("Tuned: 前進が阻まれた時に武器を立てる角度（ψ）")]
        public float carryPsi = 80f;

        public ClassTendency TendencyFor(WeightClass c) =>
            c == WeightClass.Light ? light : c == WeightClass.Heavy ? heavy : medium;

        public float PartMultiplier(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head: return headMultiplier;
                case BodyPart.Arm: return armMultiplier;
                case BodyPart.Leg: return legMultiplier;
                default: return torsoMultiplier;
            }
        }

        public float QualityMultiplier(HitQuality q) =>
            q == HitQuality.Center ? qualityCenter : q == HitQuality.Graze ? qualityGraze : qualityNormal;

        static CombatBalance cachedDefault;

        /// <summary>シーン参照がない場合（テスト等）の既定値インスタンス。</summary>
        public static CombatBalance Default
        {
            get
            {
                if (cachedDefault == null)
                {
                    cachedDefault = Resources.Load<CombatBalance>("Balance/CombatBalance");
                    if (cachedDefault == null) cachedDefault = CreateInstance<CombatBalance>();
                }
                return cachedDefault;
            }
        }
    }
}
