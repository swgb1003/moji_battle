using System;
using UnityEngine;

namespace MojiBattle
{
    [Serializable]
    public class SizeProfile
    {
        [Tooltip("武器の表示・Collider の拡大率（M=1）")] public float scale = 1f;
        [Tooltip("武器の質量倍率")] public float massMultiplier = 1f;
        [Tooltip("溜め・振り・硬直の速さ倍率（大きいほど速い）")] public float attackSpeed = 1f;
        [Tooltip("歩行速度倍率")] public float moveSpeed = 1f;
    }

    [Serializable]
    public class GripTypeProfile
    {
        public float attackSpeed = 1f;
        [Tooltip("攻撃中（溜め・振り）の最大トルク倍率")] public float attackTorque = 1f;
        [Tooltip("ガードの安定性: ガード崩し閾値に掛け、ガード時のノックバックを割る")] public float guardStability = 1f;
        public float moveSpeed = 1f;
        [Tooltip("本体ヒット時のノックバックを割る")] public float knockbackResistance = 1f;
        [Tooltip("AI が使う射程（接近距離）の倍率")] public float effectiveReach = 1f;
        [Tooltip("ガード判定の正面角の倍率")] public float guardRange = 1f;
        [Tooltip("回避の直後に始めた攻撃の速さ倍率")] public float postEvadeAttackSpeed = 1f;
        [Tooltip("AI がガードを選ぶ重みへの加算")] public float guardBiasBonus;
        [Tooltip("待機姿勢 ψ を上書きする（false なら重量クラスの既定の構え）")] public bool overrideReadyPsi;
        public float readyPsi;
    }

    /// <summary>
    /// 戦闘スタイルの AI 重み。仕様書の行動比率（例: 猛攻 Attack45% / Approach30% …）を、
    /// 既存 AI の判断パラメータ（攻撃意欲・ガード・回避・離脱など）へ写したもの。
    /// </summary>
    [Serializable]
    public class StyleProfile
    {
        [Range(0, 1)] public float attackWillingness = 0.8f;
        [Tooltip("重量クラスのガード重みへ掛ける")] public float guardBiasScale = 1f;
        [Tooltip("重量クラスの回避重みへ掛ける")] public float evadeBiasScale = 1f;
        [Range(0, 1)] public float proactiveGuard = 0.05f;
        [Tooltip("攻撃後に離脱する確率")]
        [Range(0, 1)] public float retreatAfterAttack = 0.5f;
        [Tooltip("連撃数の上限への加算")] public int comboBonus;
        [Tooltip("相手の攻撃予兆に気づく確率への加算")] public float reactionBonus;
        [Tooltip("相手の攻撃を受けずに打ち返す重み（上書き）")]
        [Range(0, 1)] public float counterBias;
        [Tooltip("-1=重量クラスの既定 / 0=待たない / 1=中央手前で待つ")] public int waitsNearCenter = -1;
    }

    /// <summary>
    /// カスタマイズ（サイズ・持ち方・握る位置・戦闘スタイル）の調整値（カスタマイズ仕様 6〜9）。
    /// 握る位置はステータスへ固定補正せず、握り点・慣性の実配置だけで差を出す。
    /// </summary>
    [CreateAssetMenu(menuName = "MojiBattle/Customize Balance", fileName = "CustomizeBalance")]
    public class CustomizeBalance : ScriptableObject
    {
        [Header("9 文字サイズ（S / M / L / XL）")]
        public SizeProfile sizeS = new SizeProfile { scale = 0.75f, massMultiplier = 0.65f, attackSpeed = 1.20f, moveSpeed = 1.10f };
        public SizeProfile sizeM = new SizeProfile { scale = 1.00f, massMultiplier = 1.00f, attackSpeed = 1.00f, moveSpeed = 1.00f };
        public SizeProfile sizeL = new SizeProfile { scale = 1.30f, massMultiplier = 1.45f, attackSpeed = 0.85f, moveSpeed = 0.92f };
        public SizeProfile sizeXL = new SizeProfile { scale = 1.65f, massMultiplier = 2.10f, attackSpeed = 0.68f, moveSpeed = 0.82f };

        [Header("6 持ち方")]
        public GripTypeProfile oneHanded = new GripTypeProfile
        {
            attackSpeed = 1.10f, attackTorque = 0.90f, guardStability = 0.85f, moveSpeed = 1.00f,
        };
        public GripTypeProfile twoHanded = new GripTypeProfile
        {
            attackSpeed = 0.80f, attackTorque = 1.30f, guardStability = 1.25f, moveSpeed = 0.90f, knockbackResistance = 1.15f,
        };
        [Tooltip("逆手: 字形を前腕に沿って後ろへ流す構え")]
        public GripTypeProfile reverse = new GripTypeProfile
        {
            attackSpeed = 1.15f, attackTorque = 0.80f, guardStability = 0.75f, effectiveReach = 0.85f, postEvadeAttackSpeed = 1.25f,
            overrideReadyPsi = true, readyPsi = 175f,
        };
        [Tooltip("横持ち: 字形を体の前へ水平に構えて盾にする")]
        public GripTypeProfile horizontal = new GripTypeProfile
        {
            attackSpeed = 0.85f, attackTorque = 0.90f, guardStability = 1.40f, moveSpeed = 0.90f, guardRange = 1.25f, guardBiasBonus = 0.25f,
            overrideReadyPsi = true, readyPsi = 0f,
        };
        [Tooltip("回避の終了からこの秒数以内に始めた攻撃を「回避後攻撃」とみなす")]
        public float postEvadeWindow = 1.0f;
        [Tooltip("両手持ちの添え手: 握りから重心方向へこの距離（ワールド単位、重心までの距離が上限）")]
        public float secondHandOffset = 0.32f;

        [Header("8 握る位置")]
        [Tooltip("端ギリギリは物理が不安定になるため内部値を Clamp する")]
        public float gripClampMin = 0.08f, gripClampMax = 0.92f;
        [Tooltip("扱いにくさ: 同じ筋力（トルク）での角加速度 ∝ (基準の慣性 / 実際の慣性)^この指数。基準 = M サイズ・中央握り・質量補正なし")]
        public float inertiaExponent = 0.5f;
        [Tooltip("カスタマイズした武器の振りの最大角加速度の倍率。余裕が大きいと慣性の大きい持ち方（端持ち・大型）でも予定の振りに追いつき、扱いにくさが速さに出ない")]
        public float handlingAccelScale = 0.7f;
        [Tooltip("重量クラス（AI の連撃・間隔・構え）の判定: 字形の重量 + (-ln 扱いやすさ) × この値（下限・上限つき）")]
        public float weightFromHandling = 30f, weightFromHandlingMin = -20f, weightFromHandlingMax = 70f;
        [Tooltip("デバッグ表示用の目安 handlingPenalty = 1 + leverArm × HandlingFactor（戦闘計算には使わない）")]
        public float handlingFactor = 0.6f;

        [Header("攻撃の段階（カスタマイズ時は武器の実際の角度で進める）")]
        [Tooltip("振りかぶり: 開始角との差がこの角度以内になったら振り始める")]
        public float windupReadyToleranceDeg = 20f;
        [Tooltip("振り抜き: 予定の振り幅のこの割合まで武器が回ったら終える")]
        public float swingFinishProgress = 0.85f;
        [Tooltip("硬直: 構えの角度との差がこの角度以内に戻ったら動ける")]
        public float recoverReadyToleranceDeg = 35f;
        [Tooltip("溜め・振り抜きは予定時間のこの倍率までは武器の到達を待つ")]
        public float maxPhaseStretch = 3f;

        [Header("12 ダメージ")]
        [Tooltip("与ダメージ × (武器質量 / サイズ補正前の質量)^この指数。サイズは質量を通じてだけ威力に効く")]
        public float massDamageExponent = 0.5f;

        [Header("7 戦闘スタイル")]
        public StyleProfile aggressive = new StyleProfile
        {
            attackWillingness = 0.97f, guardBiasScale = 0.35f, evadeBiasScale = 0.5f, proactiveGuard = 0f, retreatAfterAttack = 0f,
            comboBonus = 1, reactionBonus = -0.15f, counterBias = 0.35f, waitsNearCenter = 0,
        };
        public StyleProfile hitAndAway = new StyleProfile
        {
            attackWillingness = 0.85f, guardBiasScale = 0.6f, evadeBiasScale = 1.3f, proactiveGuard = 0f, retreatAfterAttack = 1f,
            comboBonus = -1, reactionBonus = 0.05f, counterBias = 0f, waitsNearCenter = 0,
        };
        public StyleProfile counter = new StyleProfile
        {
            attackWillingness = 0.08f, guardBiasScale = 1.3f, evadeBiasScale = 1.3f, proactiveGuard = 0.1f, retreatAfterAttack = 0.5f,
            comboBonus = 0, reactionBonus = 0.25f, counterBias = 0f, waitsNearCenter = 1,
        };
        public StyleProfile defensive = new StyleProfile
        {
            attackWillingness = 0.45f, guardBiasScale = 2.0f, evadeBiasScale = 0.6f, proactiveGuard = 0.6f, retreatAfterAttack = 0.3f,
            comboBonus = 0, reactionBonus = 0.15f, counterBias = 0.1f, waitsNearCenter = 1,
        };
        [Tooltip("ヒット＆アウェイの離脱時間（軽量 → 重量で補間。重量級ほど短く）")]
        public float hitAndAwayRetreatLight = 1.2f, hitAndAwayRetreatHeavy = 0.5f;
        [Tooltip("カウンター: 相手の攻撃終了からこの秒数は攻撃を最優先")]
        public float counterWindow = 0.6f;
        [Tooltip("カウンター: 待ちの間、相手の射程のこの倍率の外で構える")]
        public float counterWaitRangeScale = 1.15f;
        [Tooltip("鉄壁: 連続ガードの上限秒数 / 上限後にガードを解く最短秒数")]
        public float defensiveMaxGuardSeconds = 2.5f, defensiveGuardCooldown = 0.5f;
        [Tooltip("鉄壁: 相手がこの距離（相手の脅威距離＋この値）以内なら字形を相手へ向けて構える")]
        public float defensiveFaceMargin = 0.8f;

        public SizeProfile Size(WeaponSize s)
        {
            switch (s)
            {
                case WeaponSize.S: return sizeS;
                case WeaponSize.L: return sizeL;
                case WeaponSize.XL: return sizeXL;
                default: return sizeM;
            }
        }

        public GripTypeProfile Grip(GripType g)
        {
            switch (g)
            {
                case GripType.TwoHanded: return twoHanded;
                case GripType.Reverse: return reverse;
                case GripType.Horizontal: return horizontal;
                default: return oneHanded;
            }
        }

        public StyleProfile Style(BattleStyle s)
        {
            switch (s)
            {
                case BattleStyle.HitAndAway: return hitAndAway;
                case BattleStyle.Counter: return counter;
                case BattleStyle.Defensive: return defensive;
                default: return aggressive;
            }
        }

        public float ClampGrip(float p) => Mathf.Clamp(p, gripClampMin, gripClampMax);

        static CustomizeBalance cachedDefault;

        public static CustomizeBalance Default
        {
            get
            {
                if (cachedDefault == null)
                {
                    cachedDefault = Resources.Load<CustomizeBalance>("Balance/CustomizeBalance");
                    if (cachedDefault == null) cachedDefault = CreateInstance<CustomizeBalance>();
                }
                return cachedDefault;
            }
        }
    }
}
