using System;
using UnityEngine;

namespace MojiBattle
{
    public sealed class RunUpgrade
    {
        public readonly int Id, Limit;
        public readonly string Name, Effect, Description;
        public RunUpgrade(int id, string name, string effect, string description, int limit=3)
        { Id=id; Name=name; Effect=effect; Description=description; Limit=limit; }
    }
    public sealed class RunOpponent
    {
        public string Name, Hint, Warning;
        public FighterBuildData Build;
        public RunBonuses Bonuses;
    }
    public struct RunBonuses
    {
        public float attackSpeed, cheerRecovery, damage, defense, moveSpeed, specialDamage,
            gaugeRate, cheerDamage, braceReduction, resistance, maxHp;
    }
    public static class RunCatalog
    {
        public const int Battles=5;
        /// <summary>Share of max HP restored automatically after every win, so damage carries over without forcing a heal pick.</summary>
        public const float RestRecovery=.5f;
        /// <summary>Share of max HP restored by the 一息 upgrade.</summary>
        public const float BreathRecovery=.5f;
        public static readonly RunUpgrade[] Upgrades={
            new RunUpgrade(0,"速筆","攻撃速度 +15%","通常攻撃のテンポが上がる。"),
            new RunUpgrade(1,"声の泉","声援回復 +20%","声援ゲージが早く回復する。",2),
            new RunUpgrade(2,"一息","HPを50%回復","次の戦いに備えて回復する。",4),
            new RunUpgrade(3,"力強い一画","攻撃威力 +12%","一撃に、さらに力を込める。"),
            new RunUpgrade(4,"堅墨","防御 +10%","相手の攻撃を、堅く受け止める。"),
            new RunUpgrade(5,"風の足","移動速度 +12%","間合いを素早く変えられる。"),
            new RunUpgrade(6,"決めの一筆","必殺威力 +15%","切り札の威力と上限が上がる。",2),
            new RunUpgrade(7,"闘志","必殺回復 +20%","必殺ゲージの自然回復が速くなる。",2),
            new RunUpgrade(8,"響く声","声援の威力 +15%","声援中の攻撃が、さらに強くなる。",2),
            new RunUpgrade(9,"不屈の墨","踏ん張り軽減 +10%","踏ん張り中の必殺ダメージを減らす。",2),
            new RunUpgrade(10,"根を張る文字","吹き飛び耐性 +20%","相手の一撃にも、踏みとどまる。",2),
            new RunUpgrade(11,"長く続く筆","最大HP +15%","残りHPも、同じ割合で増える。",2)
        };
        /// <summary>Opponent damage and max-HP offset per round. Early challengers sit below the player and the boss's XL weapon
        /// already carries its threat, so its stats stay below par too; upgrades then build a real lead.</summary>
        static readonly float[] EnemyPower={-.2f,-.2f,-.15f,-.15f,-.3f};
        public static RunUpgrade Upgrade(int id)=>id>=0 && id<Upgrades.Length?Upgrades[id]:null;
        public static RunBonuses Bonuses(int[] counts)
        {
            int N(int id)=>counts!=null && id<counts.Length?counts[id]:0;
            return new RunBonuses {attackSpeed=N(0)*.15f,cheerRecovery=N(1)*.20f,damage=N(3)*.12f,
                defense=N(4)*.10f,moveSpeed=N(5)*.12f,specialDamage=N(6)*.15f,gaugeRate=N(7)*.20f,
                cheerDamage=N(8)*.15f,braceReduction=N(9)*.10f,resistance=N(10)*.20f,maxHp=N(11)*.15f};
        }
        public static RunOpponent Opponent(int seed,int round)
        {
            if(round<1 || round>Battles)throw new ArgumentOutOfRangeException(nameof(round));
            var rng=new System.Random(unchecked(seed+round*7919));
            string[][] pool={new[]{"刀","風","一"},new[]{"盾","鉄","石"},new[]{"雷","光","星"},new[]{"虎","炎","剣"},new[]{"龍","闇","天"}};
            int pick=rng.Next(3);string ch=pool[round-1][pick];
            var build=FighterBuildData.Default(ch);build.fontType=FontStyleId.Gothic;
            if(!FighterCustomizer.IsAvailable(ch,build.fontType,out _))build.character="一";
            build.weaponSize=round==5?WeaponSize.XL:round==4?WeaponSize.L:WeaponSize.M;
            build.gripType=round==2 || round==5?GripType.TwoHanded:round==3?GripType.Reverse:GripType.OneHanded;
            build.battleStyle=round==2?BattleStyle.Defensive:round==3?BattleStyle.HitAndAway:round==4?BattleStyle.Counter:BattleStyle.Aggressive;
            return new RunOpponent {Build=build,
                Name=round==5?new[]{"墨龍","闇の王","天の覇者"}[pick]:new[]{"最初の挑戦者","鉄壁の守り手","光速の一文字","猛き好敵手"}[round-1],
                Hint=round==5?"一文字の力を、ここで証明せよ。":new[]{"まずは一文字の力を試そう。","守りは堅い。連続攻撃で崩そう。","速い相手の動きを見極めよう。","重い一撃に注意して戦おう。"}[round-1],
                Warning=round==5?"巨大な武器と、強力な必殺に注意。":"声援と必殺のタイミングが勝負の鍵。",
                Bonuses=new RunBonuses {damage=EnemyPower[round-1],defense=EnemyPower[round-1]*.6f,
                    maxHp=EnemyPower[round-1],specialDamage=round==5?.15f:0,gaugeRate=round==5?.15f:0}};
        }
        public static float MaxHp(FighterBuildData build,RunBonuses bonuses)
        {
            if(build==null || !GlyphCatalog.TryGet(build.character,build.fontType,CombatBalance.Default,GlyphCalibration.Default,out var glyph,out _))return 100;
            return StatCalculator.Compute(glyph,CombatBalance.Default).maxHp*(1+bonuses.maxHp);
        }
    }
}
