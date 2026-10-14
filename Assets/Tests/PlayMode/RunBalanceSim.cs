using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>勝ち抜き・育成の 5 連戦を COM 同士で回し、強化の選び方ごとの突破率を測る（バランス調整用）。</summary>
    [Explicit("バランス計測用")]
    public class RunBalanceSim
    {
        string save,awards;bool hadSave,hadAwards;
        [SetUp] public void Setup(){hadSave=PlayerPrefs.HasKey(RunRuntimeStore.PrefKey);save=PlayerPrefs.GetString(RunRuntimeStore.PrefKey);hadAwards=PlayerPrefs.HasKey(AchievementStore.PrefKey);awards=PlayerPrefs.GetString(AchievementStore.PrefKey);RunRuntimeStore.ClearSession();}
        [TearDown] public void Cleanup(){RunRuntimeStore.ClearSession();SimHarness.End();if(hadSave)PlayerPrefs.SetString(RunRuntimeStore.PrefKey,save);else PlayerPrefs.DeleteKey(RunRuntimeStore.PrefKey);if(hadAwards)PlayerPrefs.SetString(AchievementStore.PrefKey,awards);else PlayerPrefs.DeleteKey(AchievementStore.PrefKey);PlayerPrefs.Save();AchievementStore.Reload();}

        static readonly string[] Glyphs={"火","刀","山","水","木","大"};
        // 0: 回復を毎回選ぶ / 1: 回復を選ばない / 2: 回復なしだが毎戦 HP 満タン（HP 持ち越しの影響を切り分ける基準）
        [UnityTest,Timeout(3600000)] public IEnumerator ClearRateByStrategy()
        {
            SimHarness.Begin(false);
            int runs=int.TryParse(System.Environment.GetEnvironmentVariable("RUN_SIM_RUNS"),out var n)?n:36;
            var md=new StringBuilder("| 方針 | 突破率 | 1戦 | 2戦 | 3戦 | 4戦 | 5戦 | 開始HP平均(2-5戦) |\n|---|---|---|---|---|---|---|---|\n");
            for(int strategy=0;strategy<3;strategy++)
            {
                int clears=0;int[] fought=new int[6],won=new int[6];float hpSum=0;int hpN=0;
                for(int r=0;r<runs;r++)
                {
                    RunRuntimeStore.Begin(1000+r*31);
                    var build=FighterBuildData.Default(Glyphs[r%Glyphs.Length]);
                    if(!FighterCustomizer.IsAvailable(build.character,build.fontType,out _))build=FighterBuildData.Default("火");
                    Assert.IsTrue(RunRuntimeStore.SetBuild(build));Assert.IsTrue(RunRuntimeStore.Prepare());
                    int guard=0;
                    while(!RunRuntimeStore.Current.Finished && guard++<20)
                    {
                        var s=RunRuntimeStore.Current;
                        if(s.stage==RunStage.Result && s.history[s.history.Count-1].winner==-1){RunRuntimeStore.ReplayDraw();continue;}
                        if(s.stage==RunStage.Result)
                        {
                            RunRuntimeStore.OpenUpgrade();
                            int pick=s.offers[0];
                            foreach(int id in s.offers)if(strategy==0?id==2:id!=2){pick=id;break;}
                            if(strategy>=1 && pick==2)pick=s.offers[1];
                            RunRuntimeStore.SelectUpgrade(pick);RunRuntimeStore.ConfirmUpgrade();continue;
                        }
                        if(strategy==2)s.hpShare=1;
                        if(s.round>1){hpSum+=s.hpShare;hpN++;}
                        int round=s.round;
                        var config=RunRuntimeStore.BeginMatch(CombatBalance.Default.matchDuration);
                        var battle=BattleBuilder.Build(config,CombatBalance.Default,GlyphCalibration.Default,false,0,out var error);
                        Assert.IsNotNull(battle,error);
                        try
                        {
                            ComputerMatchSimulation.Configure(battle);
                            RunRuntimeStore.ApplyCombat(battle.Left,true);RunRuntimeStore.ApplyCombat(battle.Right,false);
                            battle.Context.Interventions=new BattleIntervention(battle.Director,InterventionController.Com,InterventionController.Com);
                            RunRuntimeStore.ApplyInterventions(battle.Context.Interventions);
                            int steps=0;while(!battle.Director.Ended && steps++<200000)battle.Director.SimulateStep();
                            var res=battle.Director.Result;
                            if(res.winner!=-1){fought[round]++;if(res.winner==0)won[round]++;}
                            RunRuntimeStore.Record(res);
                        }
                        finally{battle.Destroy();}
                        yield return null;
                    }
                    if(RunRuntimeStore.Current.Cleared)clears++;
                    RunRuntimeStore.ClearSession();
                }
                string P(int a,int b)=>b==0?"-":$"{100f*a/b:F0}% ({a}/{b})";
                md.Append($"| {new[]{"回復を毎回","回復なし","毎戦満タン(基準)"}[strategy]} | {P(clears,runs)} | {P(won[1],fought[1])} | {P(won[2],fought[2])} | {P(won[3],fought[3])} | {P(won[4],fought[4])} | {P(won[5],fought[5])} | {(hpN==0?0:100*hpSum/hpN):F0}% |\n");
            }
            string tag=System.Environment.GetEnvironmentVariable("RUN_SIM_TAG")??"run_balance";
            System.IO.File.WriteAllText(System.IO.Path.Combine(SimHarness.ReportDir,tag+".md"),md.ToString());
            Debug.Log("[MojiBattle] RunBalance\n"+md);
        }

        /// <summary>強化なし・HP 満タンで各戦の相手と戦ったときの勝率（相手そのものの強さ）。</summary>
        [UnityTest,Timeout(3600000)] public IEnumerator WinRateByOpponentAtFullHp()
        {
            SimHarness.Begin(false);
            int runs=int.TryParse(System.Environment.GetEnvironmentVariable("RUN_SIM_RUNS"),out var n)?n:36;
            var md=new StringBuilder("| 戦 | 相手 | 勝率 |\n|---|---|---|\n");
            for(int round=1;round<=RunCatalog.Battles;round++)
            {
                var won=new System.Collections.Generic.SortedDictionary<string,int[]>();
                for(int r=0;r<runs;r++)
                {
                    RunRuntimeStore.Begin(2000+r*37);var s=RunRuntimeStore.Current;
                    var build=FighterBuildData.Default(Glyphs[r%Glyphs.Length]);
                    Assert.IsTrue(RunRuntimeStore.SetBuild(build));Assert.IsTrue(RunRuntimeStore.Prepare());s.round=round;
                    string enemy=s.Opponent.Build.character;
                    var battle=BattleBuilder.Build(RunRuntimeStore.BeginMatch(CombatBalance.Default.matchDuration),CombatBalance.Default,GlyphCalibration.Default,false,0,out var error);
                    Assert.IsNotNull(battle,error);
                    try
                    {
                        ComputerMatchSimulation.Configure(battle);
                        RunRuntimeStore.ApplyCombat(battle.Left,true);RunRuntimeStore.ApplyCombat(battle.Right,false);
                        battle.Context.Interventions=new BattleIntervention(battle.Director,InterventionController.Com,InterventionController.Com);
                        RunRuntimeStore.ApplyInterventions(battle.Context.Interventions);
                        int steps=0;while(!battle.Director.Ended && steps++<200000)battle.Director.SimulateStep();
                        if(!won.TryGetValue(enemy,out var c))won[enemy]=c=new int[2];
                        if(battle.Director.Result.winner!=-1){c[1]++;if(battle.Director.Result.winner==0)c[0]++;}
                    }
                    finally{battle.Destroy();}
                    RunRuntimeStore.ClearSession();
                    yield return null;
                }
                int a=0,b=0;
                foreach(var kv in won){md.Append($"| {round} | {kv.Key} | {(kv.Value[1]==0?"-":$"{100f*kv.Value[0]/kv.Value[1]:F0}% ({kv.Value[0]}/{kv.Value[1]})")} |\n");a+=kv.Value[0];b+=kv.Value[1];}
                md.Append($"| {round} | 計 | {(b==0?"-":$"{100f*a/b:F0}% ({a}/{b})")} |\n");
            }
            string tag=System.Environment.GetEnvironmentVariable("RUN_SIM_TAG")??"run_opponents";
            System.IO.File.WriteAllText(System.IO.Path.Combine(SimHarness.ReportDir,tag+".md"),md.ToString());
            Debug.Log("[MojiBattle] RunOpponents\n"+md);
        }
    }
}
