using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// カスタマイズから導いた戦闘中の補正一式（カスタマイズ仕様 11）。
    /// FinalStats = BaseStats × サイズ × 持ち方。握る位置は数値補正を持たず、握り点と慣性（handlingAccel）だけに効く。
    /// 戦闘スタイルは数値に掛けず AI（BattleStyleStrategy）へ渡す。カスタマイズ無し（P1/P2 検証）は Identity。
    /// </summary>
    public struct FighterModifiers
    {
        public bool customized;
        public WeaponSize size;
        public GripType grip;
        public BattleStyle style;
        public float gripPosition;
        /// <summary>HUD 等の表示用の握る場所</summary>
        public string gripLabel;

        public float sizeScale, weaponMassMultiplier;
        /// <summary>溜め・振り・硬直の速さ（サイズ × 持ち方）</summary>
        public float attackSpeed;
        public float moveSpeed;
        public float attackTorque, guardStability, knockbackResistance, reach, guardRange, postEvadeAttackSpeed, guardBiasBonus;
        /// <summary>同じトルクで出せる角加速度の倍率（基準構成との慣性比から。端持ち・大型ほど小さい）</summary>
        public float handlingAccel;
        /// <summary>与ダメージ倍率（質量を通じたサイズの威力）</summary>
        public float damage;
        /// <summary>待機姿勢 ψ（NaN なら重量クラスの既定）</summary>
        public float readyPsi;
        /// <summary>握り→重心の距離（ワールド単位）</summary>
        public float leverArm;
        /// <summary>デバッグ表示用の扱いにくさの目安</summary>
        public float handlingPenalty;

        /// <summary>重量クラスの判定に使う重量: 字形の重量 + 扱いにくさ（慣性比の対数）による加減。</summary>
        public int EffectiveWeightScore(int glyphWeight, CustomizeBalance cb) =>
            !customized ? glyphWeight
            : Mathf.Clamp(glyphWeight + Mathf.RoundToInt(Mathf.Clamp(-Mathf.Log(Mathf.Max(1e-3f, handlingAccel)) * cb.weightFromHandling,
                cb.weightFromHandlingMin, cb.weightFromHandlingMax)), 0, 100);

        public static FighterModifiers Identity => new FighterModifiers
        {
            customized = false, size = WeaponSize.M, grip = GripType.OneHanded, style = BattleStyle.Aggressive, gripPosition = -1f,
            sizeScale = 1f, weaponMassMultiplier = 1f, attackSpeed = 1f, moveSpeed = 1f, attackTorque = 1f, guardStability = 1f,
            knockbackResistance = 1f, reach = 1f, guardRange = 1f, postEvadeAttackSpeed = 1f, guardBiasBonus = 0f,
            handlingAccel = 1f, damage = 1f, readyPsi = float.NaN, handlingPenalty = 1f,
        };

        /// <summary>
        /// 握り点・縮尺・質量が決まった後で呼ぶ。慣性は「M・中央握り・サイズ質量補正なし」を基準とした比で扱いにくさにする。
        /// </summary>
        public static FighterModifiers From(FighterBuildData build, GlyphDefinitionRuntime glyph, FighterStats baseStats,
            WeaponGeometry geo, float weaponMass, CombatBalance b, CustomizeBalance cb)
        {
            var size = cb.Size(build.weaponSize);
            var grip = cb.Grip(build.gripType);
            var m = Identity;
            m.customized = true;
            m.size = build.weaponSize;
            m.grip = build.gripType;
            m.style = build.battleStyle;
            m.gripPosition = cb.ClampGrip(build.gripPosition);
            m.gripLabel = CustomizeLabels.GripPlace(build);
            m.sizeScale = size.scale;
            m.weaponMassMultiplier = size.massMultiplier;
            m.attackSpeed = size.attackSpeed * grip.attackSpeed;
            m.moveSpeed = size.moveSpeed * grip.moveSpeed;
            m.attackTorque = grip.attackTorque;
            m.guardStability = grip.guardStability;
            m.knockbackResistance = grip.knockbackResistance;
            m.reach = grip.effectiveReach;
            m.guardRange = grip.guardRange;
            m.postEvadeAttackSpeed = grip.postEvadeAttackSpeed;
            m.guardBiasBonus = grip.guardBiasBonus;
            m.readyPsi = grip.overrideReadyPsi ? grip.readyPsi : float.NaN;
            m.damage = Mathf.Pow(size.massMultiplier, cb.massDamageExponent);

            var f = glyph.features;
            float maxSidePx = Mathf.Max(f.inkBounds.width, f.inkBounds.height);
            float refScale = b.weaponMaxSide / Mathf.Max(1f, maxSidePx);
            var refGrip = WeaponGripController.GripPixel(glyph, 0.5f);
            float iRef = WeaponGripController.InertiaAboutGrip(f, refGrip, refScale, baseStats.weaponMass);
            float iNow = WeaponGripController.InertiaAboutGrip(f, geo.gripPx, geo.scale, weaponMass);
            m.handlingAccel = iNow > 1e-5f ? Mathf.Pow(iRef / iNow, cb.inertiaExponent) : 1f;
            m.leverArm = geo.comLocal.magnitude;
            m.handlingPenalty = 1f + m.leverArm * cb.handlingFactor;
            return m;
        }
    }
}
