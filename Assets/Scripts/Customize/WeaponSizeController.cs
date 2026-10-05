namespace MojiBattle
{
    /// <summary>
    /// 文字サイズ（カスタマイズ仕様 9）。表示 Sprite・Collider・握り点・射程は同じ WeaponGeometry の縮尺から作るため、
    /// ここで決めた縮尺がすべてへ同時に反映される（見た目だけ大きくすることはない）。
    /// </summary>
    public static class WeaponSizeController
    {
        /// <summary>字形の最大辺（ワールド単位）。M = CombatBalance.weaponMaxSide。</summary>
        public static float MaxSide(WeaponSize size, CombatBalance b, CustomizeBalance cb) => b.weaponMaxSide * cb.Size(size).scale;

        /// <summary>武器の質量。字形固有の質量 × サイズの質量倍率。</summary>
        public static float WeaponMass(float baseMass, WeaponSize size, CustomizeBalance cb) => baseMass * cb.Size(size).massMultiplier;
    }
}
