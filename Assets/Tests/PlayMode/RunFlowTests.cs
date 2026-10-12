using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MojiBattle.Tests
{
    public class RunFlowTests
    {
        string save,awards;bool hadSave,hadAwards;
        [SetUp] public void Setup(){hadSave=PlayerPrefs.HasKey(RunRuntimeStore.PrefKey);save=PlayerPrefs.GetString(RunRuntimeStore.PrefKey);hadAwards=PlayerPrefs.HasKey(AchievementStore.PrefKey);awards=PlayerPrefs.GetString(AchievementStore.PrefKey);PlayerPrefs.DeleteKey(RunRuntimeStore.PrefKey);RunRuntimeStore.ClearSession();AchievementStore.Reset();TimeController.ResetAll();}
        [TearDown] public void Cleanup(){GameFlow.ToTitle();SimHarness.End();if(hadSave)PlayerPrefs.SetString(RunRuntimeStore.PrefKey,save);else PlayerPrefs.DeleteKey(RunRuntimeStore.PrefKey);if(hadAwards)PlayerPrefs.SetString(AchievementStore.PrefKey,awards);else PlayerPrefs.DeleteKey(AchievementStore.PrefKey);PlayerPrefs.Save();AchievementStore.Reload();}
        static Button B(string name)=>Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name==name);
        static IEnumerator Ready(){yield return null;yield return null;yield return new WaitForSecondsRealtime(.7f);}
        static void Capture(string name){System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath,"../Reports/UI/Run"));MenuUiTests.Capture("Run/"+name);MenuUiTests.Capture("Run/"+name+"_880x450",880,450);MenuUiTests.Capture("Run/"+name+"_1280x960",1280,960);}
        static IEnumerator End(BattleBootstrap boot,bool win)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+12;while(!boot.Battle.Director.Fighting&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.IsTrue(boot.Battle.Director.Fighting);boot.Battle.Director.SilentSimulation=true;
            boot.Battle.Left.Runtime.hp=win?boot.Battle.Left.Runtime.maxHp*.55f:0;boot.Battle.Right.Runtime.hp=win?0:boot.Battle.Right.Runtime.maxHp;
            while(!boot.Battle.Director.Ended&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.IsTrue(boot.Battle.Director.Ended);yield return Ready();
        }
        [UnityTest] public IEnumerator UpgradesAffectActualMovementHpDamageDefenseAndInterventions()
        {
            GameFlow.ToTitle();yield return Ready();SimHarness.Begin(false);RunRuntimeStore.Begin(96);
            var s=RunRuntimeStore.Current;s.hpShare=.4f;for(int i=0;i<12;i++)s.upgrades[i]=1;
            var battle=SimHarness.Build(96,SimHarness.Custom("火"),SimHarness.Custom("刀"));
            try
            {
                var f=battle.Left;float walk=f.WalkSpeed,max=f.Runtime.maxHp,damage=f.Mods.damage,attack=f.Mods.attackSpeed,resistance=f.Mods.knockbackResistance;int defense=f.Stats.defense;
                RunRuntimeStore.ApplyCombat(f,true);
                Assert.AreEqual(walk*1.12f,f.WalkSpeed,.001f);Assert.AreEqual(max*1.15f,f.Runtime.maxHp,.001f);Assert.AreEqual(f.Runtime.maxHp*.4f,f.Runtime.hp,.001f);
                Assert.AreEqual(damage*1.12f,f.Mods.damage,.001f);Assert.AreEqual(attack*1.15f,f.Mods.attackSpeed,.001f);Assert.AreEqual(Mathf.RoundToInt(defense*1.1f),f.Stats.defense);Assert.AreEqual(resistance*1.2f,f.Mods.knockbackResistance,.001f);
                var iv=new BattleIntervention(battle.Director,InterventionController.Local,InterventionController.Com);battle.Context.Interventions=iv;RunRuntimeStore.ApplyInterventions(iv);yield return null;
                var side=iv.Side(0);float now=iv.Now;iv.Request(0,InterventionKind.Cheer,false);
                Assert.AreEqual(battle.Context.Balance.intervention.cheerCooldown/1.2f,side.cheerReadyAt-now,.001f);
                side.specialFromId=side.specialToId=10;side.chainActive=false;
                Assert.AreEqual(side.plan.damage*1.15f*battle.Context.Balance.intervention.cheerDamage*1.15f,iv.DamageScale(f,10,now),.001f);
                Assert.AreEqual(.9f,side.braceDamageScale,.001f);Assert.AreEqual(1.2f,side.gaugeRateScale,.001f);
            }
            finally{battle.Destroy();}
        }
        [UnityTest] public IEnumerator FiveBattleFlowSharedCustomizerFixedSelectionResumeBossAndTitles()
        {
            GameFlow.ToModeSelect();yield return Ready();Capture("01_menu");B("RunModeStart").onClick.Invoke();yield return Ready();Capture("02_guide");
            Assert.IsFalse(B("RunResume").interactable);B("RunBegin").onClick.Invoke();yield return Ready();
            var customize=Object.FindAnyObjectByType<CustomizeScreen>();Assert.IsTrue(customize.TextInputVisible);Capture("03_customize");B("Battle").onClick.Invoke();yield return Ready();
            for(int round=1;round<=5;round++)
            {
                var s=RunRuntimeStore.Current;Assert.AreEqual(round,s.round);Assert.AreEqual(RunStage.Prepare,s.stage);
                if(round==1||round==5)Capture(round==1?"04_prepare":"09_boss");
                B("RunVersus").onClick.Invoke();yield return Ready();if(round==1)Capture("05_vs");B("RunStart").onClick.Invoke();yield return Ready();
                var boot=Object.FindAnyObjectByType<BattleBootstrap>();Assert.IsNotNull(boot.Battle);Assert.AreEqual(s.Hp,boot.Battle.Left.Runtime.hp,.1f);
                Assert.AreEqual(1+s.Bonuses.cheerRecovery,boot.Battle.Context.Interventions.Side(0).cheerRecoveryScale,.001f);
                if(round==1)Capture("06_battle");yield return End(boot,true);Assert.AreEqual(round,s.Wins);
                if(round==5){Assert.IsTrue(s.Cleared);Capture("10_clear");Assert.IsTrue(AchievementStore.Profile.Has(65));break;}
                if(round==1)Capture("07_result");B("RunResultNext").onClick.Invoke();yield return Ready();Assert.IsFalse(B("RunUpgradeConfirm").interactable);
                B("RunUpgradeSelect0").onClick.Invoke();int selection=s.selectedUpgrade;
                ExecuteEvents.Execute(B("RunUpgradeSelect1").gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);EventSystem.current.SetSelectedGameObject(B("RunUpgradeSelect1").gameObject);
                Assert.AreEqual(selection,s.selectedUpgrade);
                if(round==1){Capture("08_upgrade");GameFlow.ToModeSelect();yield return Ready();GameFlow.ResumeRun();yield return Ready();s=RunRuntimeStore.Current;Assert.AreEqual(selection,s.selectedUpgrade);}
                B("RunUpgradeConfirm").onClick.Invoke();yield return Ready();
            }
            GameFlow.ToAchievements();yield return Ready();
            var badges=Object.FindAnyObjectByType<AchievementsScreen>();badges.ChooseCategory(9);yield return Ready();Capture("12_run_titles");
            Assert.AreEqual("五",GameObject.Find("RunRewardGlyph").GetComponent<Text>().text);
            GameFlow.ToRunIntro();yield return Ready();Assert.IsFalse(B("RunResume").interactable);
        }
        [UnityTest] public IEnumerator DefeatHasNoReplayAndBuildEditsDoNotHeal()
        {
            GameFlow.BeginRun(36);yield return Ready();B("Battle").onClick.Invoke();yield return Ready();
            RunRuntimeStore.Current.hpShare=.4f;float share=RunRuntimeStore.Current.hpShare;
            B("RunEditBuild").onClick.Invoke();yield return Ready();var c=Object.FindAnyObjectByType<CustomizeScreen>();Assert.IsFalse(c.TextInputVisible);c.Customizer.SetSize(WeaponSize.XL);Assert.AreEqual(share,RunRuntimeStore.Current.hpShare);
            c.Customizer.SelectSide(1);Assert.IsFalse(c.Customizer.Editable);Assert.IsFalse(B("Size_S").interactable);
            B("Battle").onClick.Invoke();yield return Ready();B("RunVersus").onClick.Invoke();yield return Ready();B("RunStart").onClick.Invoke();yield return Ready();yield return End(Object.FindAnyObjectByType<BattleBootstrap>(),false);
            Assert.IsTrue(RunRuntimeStore.Current.Finished);Assert.IsFalse(RunRuntimeStore.Current.Cleared);Assert.IsFalse(RunRuntimeStore.CanBattle);Capture("11_defeat");
        }
    }
}
