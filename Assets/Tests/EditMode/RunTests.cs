using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MojiBattle.Tests
{
    public class RunTests
    {
        string save,awards;bool hadSave,hadAwards;
        [SetUp] public void Setup(){hadSave=PlayerPrefs.HasKey(RunRuntimeStore.PrefKey);save=PlayerPrefs.GetString(RunRuntimeStore.PrefKey);hadAwards=PlayerPrefs.HasKey(AchievementStore.PrefKey);awards=PlayerPrefs.GetString(AchievementStore.PrefKey);RunRuntimeStore.ClearSession();AchievementStore.Reset();}
        [TearDown] public void Cleanup(){RunRuntimeStore.ClearSession();if(hadSave)PlayerPrefs.SetString(RunRuntimeStore.PrefKey,save);else PlayerPrefs.DeleteKey(RunRuntimeStore.PrefKey);if(hadAwards)PlayerPrefs.SetString(AchievementStore.PrefKey,awards);else PlayerPrefs.DeleteKey(AchievementStore.PrefKey);PlayerPrefs.Save();AchievementStore.Reload();}
        static MatchResult Result(int winner,float hp=50)=>new MatchResult{winner=winner,hpRemaining=new[]{hp,0f},maxHp=new[]{100f,100f},elapsedSeconds=20};
        static void Win(){Assert.IsTrue(RunRuntimeStore.Prepare());RunRuntimeStore.BeginMatch(60);Assert.IsTrue(RunRuntimeStore.Record(Result(0)));Assert.IsFalse(RunRuntimeStore.Record(Result(0)));}
        [Test] public void OffersSelectionHpAndEnemySurviveResumeWithoutReroll()
        {
            RunRuntimeStore.Begin(61);Win();Assert.IsTrue(RunRuntimeStore.OpenUpgrade());var s=RunRuntimeStore.Current;
            Assert.AreEqual(3,s.offers.Distinct().Count());Assert.Contains(2,s.offers);Assert.IsFalse(RunRuntimeStore.ConfirmUpgrade());
            Assert.IsTrue(RunRuntimeStore.SelectUpgrade(2));var offers=(int[])s.offers.Clone();var enemy=s.Opponent.Build.character;
            RunRuntimeStore.ClearSession();Assert.IsTrue(RunRuntimeStore.Resume());s=RunRuntimeStore.Current;
            CollectionAssert.AreEqual(offers,s.offers);Assert.AreEqual(2,s.selectedUpgrade);Assert.AreEqual(enemy,s.Opponent.Build.character);Assert.AreEqual(.5f,s.hpShare);
            float hp=s.Hp;Assert.IsTrue(RunRuntimeStore.ConfirmUpgrade());Assert.AreEqual(2,s.round);Assert.AreEqual(Mathf.Min(s.MaxHp,hp+30),s.Hp,.001f);Assert.IsFalse(RunRuntimeStore.ConfirmUpgrade());
        }
        [Test] public void AdjustingBuildPreservesHpShareAndLockedGlyphAndOrdinaryBuilds()
        {
            var ordinary=JsonUtility.ToJson(BuildRuntimeStore.Get(0));RunRuntimeStore.Begin(18);Win();RunRuntimeStore.OpenUpgrade();RunRuntimeStore.SelectUpgrade(RunRuntimeStore.Current.offers[0]);RunRuntimeStore.ConfirmUpgrade();
            Assert.IsTrue(RunRuntimeStore.EditBuild());var s=RunRuntimeStore.Current;float share=s.hpShare;
            var c=new FighterCustomizer();c.SetSize(WeaponSize.XL);Assert.AreEqual(share,s.hpShare);Assert.IsFalse(c.SetCharacter("刀"));
            c.SelectSide(1);var com=JsonUtility.ToJson(c.Current);c.SetSize(WeaponSize.S);Assert.AreEqual(com,JsonUtility.ToJson(c.Current));
            Assert.AreEqual(ordinary,JsonUtility.ToJson(BuildRuntimeStore.Get(0)));
        }
        [Test] public void FiveWinsFinishAndAwardOnceAcrossSavedRewardReconciliation()
        {
            RunRuntimeStore.Begin(100);for(int n=1;n<=5;n++){Win();if(n<5){RunRuntimeStore.OpenUpgrade();RunRuntimeStore.SelectUpgrade(RunRuntimeStore.Current.offers[0]);RunRuntimeStore.ConfirmUpgrade();}}
            Assert.IsTrue(RunRuntimeStore.Current.Cleared);Assert.AreEqual(5,RunRuntimeStore.Current.Wins);Assert.IsFalse(RunRuntimeStore.CanBattle);Assert.IsFalse(RunRuntimeStore.CanResume);
            Assert.IsTrue(AchievementStore.Profile.Has(65));Assert.AreEqual(1,AchievementStore.Profile.Value(68));RunRuntimeStore.ReconcileSavedRewards();Assert.AreEqual(1,AchievementStore.Profile.Value(68));Assert.AreEqual(1,AchievementStore.Profile.notifications.Count(x=>x==65));
        }
        [Test] public void DefeatCannotReplayAndInterruptedBattleRestoresSameStart()
        {
            RunRuntimeStore.Begin(49);RunRuntimeStore.Prepare();int seed=RunRuntimeStore.BeginMatch(60).seed;
            RunRuntimeStore.ClearSession();Assert.IsTrue(RunRuntimeStore.Resume());Assert.AreEqual(RunStage.Prepare,RunRuntimeStore.Current.stage);Assert.AreEqual(seed,RunRuntimeStore.BeginMatch(60).seed);
            RunRuntimeStore.Record(Result(1,0));Assert.IsTrue(RunRuntimeStore.Current.Finished);Assert.IsFalse(RunRuntimeStore.Current.Cleared);Assert.IsFalse(RunRuntimeStore.ReplayDraw());Assert.IsFalse(RunRuntimeStore.CanResume);
        }
        [Test] public void DrawDoesNotAdvanceAndMaxHpUpgradeKeepsShare()
        {
            RunRuntimeStore.Begin(14);RunRuntimeStore.Prepare();RunRuntimeStore.BeginMatch(60);RunRuntimeStore.Record(Result(-1));Assert.IsTrue(RunRuntimeStore.ReplayDraw());Assert.AreEqual(1,RunRuntimeStore.Current.round);Assert.AreEqual(0,RunRuntimeStore.Current.Wins);
            var counts=new int[12];counts[11]=1;counts[0]=2;counts[1]=1;var b=RunCatalog.Bonuses(counts);Assert.AreEqual(.15f,b.maxHp);Assert.AreEqual(.3f,b.attackSpeed);Assert.AreEqual(.2f,b.cheerRecovery);
        }
        [Test] public void MalformedOrUnsupportedSaveIsRejected()
        {
            PlayerPrefs.SetString(RunRuntimeStore.PrefKey,"garbage");Assert.IsFalse(RunRuntimeStore.CanResume);
            RunRuntimeStore.Begin(6);RunRuntimeStore.Current.round=6;PlayerPrefs.SetString(RunRuntimeStore.PrefKey,JsonUtility.ToJson(RunRuntimeStore.Current));Assert.IsFalse(RunRuntimeStore.CanResume);
        }
    }
}
