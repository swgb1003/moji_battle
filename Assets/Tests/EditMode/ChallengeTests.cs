using System;
using NUnit.Framework;
using UnityEngine;

namespace MojiBattle.Tests
{
    public class ChallengeTests
    {
        string progress,achievements;
        bool hadProgress,hadAchievements;
        [SetUp] public void Setup()
        {
            hadProgress=PlayerPrefs.HasKey(ChallengeRuntimeStore.PrefKey);progress=PlayerPrefs.GetString(ChallengeRuntimeStore.PrefKey);
            hadAchievements=PlayerPrefs.HasKey(AchievementStore.PrefKey);achievements=PlayerPrefs.GetString(AchievementStore.PrefKey);
            PlayerPrefs.DeleteKey(ChallengeRuntimeStore.PrefKey);ChallengeRuntimeStore.Reload();AchievementStore.Reset();
        }
        [TearDown] public void Cleanup()
        {
            Restore(ChallengeRuntimeStore.PrefKey,hadProgress,progress);Restore(AchievementStore.PrefKey,hadAchievements,achievements);
            PlayerPrefs.Save();ChallengeRuntimeStore.Reload();AchievementStore.Reload();
        }
        static void Restore(string key,bool existed,string value){if(existed)PlayerPrefs.SetString(key,value);else PlayerPrefs.DeleteKey(key);}
        static MatchResult Result(int winner=0,string reason="KO")=>new MatchResult {winner=winner,finishReason=reason,elapsedSeconds=18,
            hpRemaining=new[]{68f,0f},maxHp=new[]{100f,100f},metrics=new[]{new FighterMetrics(),new FighterMetrics()},seed=7001};
        [Test] public void EveryChallengeHasUsableWeaponsAndAUniqueReward()
        {
            Assert.AreEqual(6,ChallengeCatalog.All.Length);
            foreach(var d in ChallengeCatalog.All)
            {
                Assert.IsNotNull(AchievementCatalog.Get(d.RewardId));Assert.AreEqual(7,AchievementCatalog.Get(d.RewardId).Category);
                Assert.IsTrue(FighterCustomizer.IsAvailable(d.DefaultPlayer.character,d.DefaultPlayer.fontType,out var own),own);
                Assert.IsTrue(FighterCustomizer.IsAvailable(d.Opponent.character,d.Opponent.fontType,out var enemy),enemy);
            }
        }
        [Test] public void DraftsEnforceLocksAndPersistIndependentlyWithoutSharingReferences()
        {
            ChallengeRuntimeStore.Select(0);var draft=ChallengeRuntimeStore.Draft;draft.character="火";draft.weaponSize=WeaponSize.XL;
            ChallengeRuntimeStore.SetDraft(draft);Assert.AreEqual("一",ChallengeRuntimeStore.Draft.character);
            var otherCopy=ChallengeRuntimeStore.Draft;otherCopy.weaponSize=WeaponSize.S;Assert.AreEqual(WeaponSize.XL,ChallengeRuntimeStore.Draft.weaponSize);
            ChallengeRuntimeStore.Select(1);draft=ChallengeRuntimeStore.Draft;draft.character="火";draft.weaponSize=WeaponSize.XL;
            ChallengeRuntimeStore.SetDraft(draft);Assert.AreEqual(WeaponSize.S,ChallengeRuntimeStore.Draft.weaponSize);
            ChallengeRuntimeStore.Reload();ChallengeRuntimeStore.Select(0);Assert.AreEqual(WeaponSize.XL,ChallengeRuntimeStore.Draft.weaponSize);
            ChallengeRuntimeStore.Select(1);Assert.AreEqual("火",ChallengeRuntimeStore.Draft.character);Assert.AreEqual(WeaponSize.S,ChallengeRuntimeStore.Draft.weaponSize);
        }
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(5)]
        public void VictoryGrantsPersistentFirstClearRewardExactlyOnce(int id)
        {
            ChallengeRuntimeStore.Select(id);ChallengeRuntimeStore.BeginMatch(7001,60);
            Assert.IsTrue(ChallengeRuntimeStore.Record(Result(),new AchievementMatchStats()).Cleared);
            Assert.IsTrue(ChallengeRuntimeStore.Outcome.NewReward);Assert.AreEqual(1,ChallengeRuntimeStore.ClearedCount);
            Assert.AreEqual(51+id,AchievementStore.Profile.notifications[0]);
            Assert.AreEqual(1,ChallengeRuntimeStore.Progress(id).attempts);
            ChallengeRuntimeStore.Record(Result(),new AchievementMatchStats());Assert.AreEqual(1,ChallengeRuntimeStore.Progress(id).attempts);
            ChallengeRuntimeStore.Reload();ChallengeRuntimeStore.Select(id);Assert.IsTrue(ChallengeRuntimeStore.Progress(id).cleared);
            Assert.IsTrue(AchievementStore.Profile.Has(51+id));ChallengeRuntimeStore.BeginMatch(7002,60);
            AchievementStore.Reload();Assert.AreEqual(51+id,AchievementStore.Profile.notifications[0]);
            AchievementStore.Change(p=>p.notifications.Remove(51+id),true); // The first notification has been shown.
            var outcome=ChallengeRuntimeStore.Record(Result(),new AchievementMatchStats());Assert.IsTrue(outcome.Cleared);Assert.IsFalse(outcome.NewReward);
            Assert.IsFalse(AchievementStore.Profile.notifications.Contains(51+id));Assert.AreEqual(2,ChallengeRuntimeStore.Progress(id).attempts);
        }
        [TestCase(3)][TestCase(4)] public void WinningWithoutTheExtraGoalDoesNotClear(int id)
        {
            ChallengeRuntimeStore.Select(id);ChallengeRuntimeStore.BeginMatch(7001,60);
            Assert.IsFalse(ChallengeRuntimeStore.Record(Result(),new AchievementMatchStats()).Cleared);
            Assert.IsFalse(AchievementStore.Profile.Has(51+id));Assert.AreEqual(0,ChallengeRuntimeStore.ClearedCount);
            var stats=new AchievementMatchStats { specialFinish=true };stats.totals[5]=1;
            ChallengeRuntimeStore.BeginMatch(7002,60);Assert.IsTrue(ChallengeRuntimeStore.Record(Result(),stats).Cleared);
        }
        [TestCase(-1)][TestCase(1)]public void LossAndDrawNeverGrantRewardsEvenIfExtraGoalWasMet(int winner)
        {
            ChallengeRuntimeStore.Select(4);ChallengeRuntimeStore.BeginMatch(7001,60);
            var stats=new AchievementMatchStats();stats.totals[5]=1;
            Assert.IsFalse(ChallengeRuntimeStore.Record(Result(winner),stats).Cleared);Assert.IsFalse(AchievementStore.Profile.Has(55));
        }
        [Test]public void SpecialFinishRequiresKoAndInvalidIdsCannotEnter()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>ChallengeRuntimeStore.Select(6));
            ChallengeRuntimeStore.Select(3);ChallengeRuntimeStore.BeginMatch(7001,60);
            Assert.IsFalse(ChallengeRuntimeStore.Record(Result(0,"TIME_UP"),new AchievementMatchStats {specialFinish=true}).Cleared);
        }
    }
}
