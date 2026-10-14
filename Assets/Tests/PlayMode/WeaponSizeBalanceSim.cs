using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>武器サイズごとの勝率と手応え（M 相手・同じ文字・左右入れ替え）を測る（バランス調整用）。</summary>
    [Explicit("バランス計測用")]
    public class WeaponSizeBalanceSim
    {
        static readonly string[] Glyphs={"火","刀","山","一","龍"};
        static readonly WeaponSize[] Sizes={WeaponSize.S,WeaponSize.M,WeaponSize.L,WeaponSize.XL};

        [UnityTest,Timeout(3600000)] public IEnumerator SizeVsMedium()
        {
            SimHarness.Begin(false);
            // SIZE_SIM_NEUTRAL=1: 重量級の手応えの調整（溜め・振り・硬直・吹き飛ばし上限・アーマー）を外した比較用
            var cb=CustomizeBalance.Default;var saved=new[]{Copy(cb.sizeL),Copy(cb.sizeXL)};
            if(System.Environment.GetEnvironmentVariable("SIZE_SIM_NEUTRAL")=="1")
                foreach(var p in new[]{cb.sizeL,cb.sizeXL}){p.windupTime=p.swingSpeed=p.recoveryTime=p.impulseCap=p.knockbackForce=p.damage=1;p.armorMassRatio=0;}
            try
            {
                int seeds=int.TryParse(System.Environment.GetEnvironmentVariable("SIZE_SIM_SEEDS"),out var n)?n:10;
                var md=new StringBuilder("| サイズ | 勝率 | 命中/分 | 1発平均 | 強打率 | 与ダウン/試合 | 壁床激突/試合 | 最大吹き飛び | 被よろけ/試合 | 硬直割合 | 振り時間割合 | 平均溜め | 先端速度 | 平均衝撃 | 上限到達 | アーマー/試合 |\n"
                    +"|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
                var only=System.Environment.GetEnvironmentVariable("SIZE_SIM_SIZES");
                foreach(var size in Sizes)if(string.IsNullOrEmpty(only) || System.Array.IndexOf(only.Split(','),size.ToString())>=0)
                {
                    int wins=0,decided=0,hits=0,heavy=0,downs=0,env=0,staggered=0,matches=0,windups=0,swings=0,capped=0,armored=0;
                    float damage=0,seconds=0,recovery=0,active=0,fly=0,windup=0,tip=0,impulse=0;
                    foreach(var ch in Glyphs)
                    for(int i=0;i<seeds;i++)
                    for(int side=0;side<2;side++)
                    {
                        FighterLoadout Load(WeaponSize s)=>new FighterBuildData{character=ch,weaponSize=s,gripType=GripType.OneHanded,gripPosition=.5f,battleStyle=BattleStyle.Aggressive}.ToLoadout();
                        var test=Load(size);var medium=Load(WeaponSize.M);
                        var battle=SimHarness.Build(5000+i*131,side==0?test:medium,side==0?medium:test);
                        int staggers=0;
                        try
                        {
                            var b=battle.Context.Balance;
                            battle.Director.enabled=false;battle.Director.SilentSimulation=true;
                            battle.Context.Interventions=new BattleIntervention(battle.Director,InterventionController.Com,InterventionController.Com);
                            battle.Context.Events.StateChanged+=e=>{if(e.fighter==side && e.to==FighterState.Stagger)staggers++;};
                            battle.Context.Events.Hit+=e=>
                            {
                                if(e.defender==side && e.armored)armored++;
                                if(e.attacker!=side || e.offhand)return;
                                hits++;damage+=e.damage;impulse+=e.impulse;
                                if(e.damage>=b.heavyHitDamage)heavy++;
                                if(e.impulse*battle.Director.Fighters[1-side].Mods.knockbackResistance>=b.impulseMax*.99f)capped++;
                            };
                            int steps=0;while(!battle.Director.Ended && steps++<200000)battle.Director.SimulateStep();
                            var r=battle.Director.Result;matches++;seconds+=r.elapsedSeconds;
                            if(r.winner>=0){decided++;if(r.winner==side)wins++;}
                            var opp=r.metrics[1-side];downs+=opp.knockdowns;env+=opp.envImpacts;fly+=opp.maxKnockbackDistance;staggered+=staggers;
                            var tel=battle.Director.Telemetry.fighters[side];
                            recovery+=tel.stateSeconds[(int)FighterState.AttackRecovery];active+=tel.stateSeconds[(int)FighterState.AttackActive];
                            foreach(var w in tel.windups){windup+=w;windups++;}
                            tip+=tel.peakTipSpeedSum;swings+=tel.peakSwingCount;
                        }
                        finally{battle.Destroy();}
                        yield return null;
                    }
                    md.Append(System.FormattableString.Invariant($"| {size} | {100f*wins/Mathf.Max(1,decided):F0}% ({wins}/{decided}) | {60f*hits/seconds:F1} | {damage/Mathf.Max(1,hits):F1} | {100f*heavy/Mathf.Max(1,hits):F0}% | {(float)downs/matches:F2} | {(float)env/matches:F2} | {fly/matches:F2} | {(float)staggered/matches:F2} | {100f*recovery/seconds:F0}% | {100f*active/seconds:F0}% | {windup/Mathf.Max(1,windups):F2}s | {tip/Mathf.Max(1,swings):F1} | {impulse/Mathf.Max(1,hits):F1} | {100f*capped/Mathf.Max(1,hits):F0}% | {(float)armored/matches:F2} |\n"));
                }
                string tag=System.Environment.GetEnvironmentVariable("SIZE_SIM_TAG")??"weapon_size_balance";
                System.IO.File.WriteAllText(System.IO.Path.Combine(SimHarness.ReportDir,tag+".md"),md.ToString());
                Debug.Log("[MojiBattle] WeaponSize\n"+md);
            }
            finally{Restore(cb.sizeL,saved[0]);Restore(cb.sizeXL,saved[1]);}
        }

        static SizeProfile Copy(SizeProfile p)=>JsonUtility.FromJson<SizeProfile>(JsonUtility.ToJson(p));
        static void Restore(SizeProfile to,SizeProfile from)=>JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(from),to);
    }
}
