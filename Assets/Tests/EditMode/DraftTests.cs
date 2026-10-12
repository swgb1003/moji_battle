using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MojiBattle.Tests
{
    public class DraftTests
    {
        string saved,awards;bool hadSaved,hadAwards;
        FighterBuildData left,right;
        [SetUp] public void Setup()
        {
            hadSaved=PlayerPrefs.HasKey(DraftRuntimeStore.PrefKey);saved=PlayerPrefs.GetString(DraftRuntimeStore.PrefKey);
            hadAwards=PlayerPrefs.HasKey(AchievementStore.PrefKey);awards=PlayerPrefs.GetString(AchievementStore.PrefKey);
            left=BuildRuntimeStore.Get(0).Clone();right=BuildRuntimeStore.Get(1).Clone();
            DraftRuntimeStore.ClearSession();ChallengeRuntimeStore.ClearSession();PlayerPrefs.DeleteKey(DraftRuntimeStore.PrefKey);
            AchievementStore.Reset();
        }
        [TearDown] public void Cleanup()
        {
            DraftRuntimeStore.ClearSession();Restore(DraftRuntimeStore.PrefKey,hadSaved,saved);Restore(AchievementStore.PrefKey,hadAwards,awards);
            AchievementStore.Reload();BuildRuntimeStore.Set(0,left);BuildRuntimeStore.Set(1,right);PlayerPrefs.Save();
        }
        static void Restore(string key,bool exists,string value){if(exists)PlayerPrefs.SetString(key,value);else PlayerPrefs.DeleteKey(key);}
        static void Picks()
        {
            var s=DraftRuntimeStore.Current;
            if(s.ComTurn)Assert.IsTrue(DraftRuntimeStore.PickCom());
            Assert.IsTrue(DraftRuntimeStore.PickPlayer(s.pool.First(ch=>ch!=s.com?.character)));
            if(s.ComTurn)Assert.IsTrue(DraftRuntimeStore.PickCom());
            Assert.AreNotEqual(s.player.character,s.com.character);
        }
        static MatchResult Result(int winner)=>new MatchResult {winner=winner,elapsedSeconds=18.4f,hpRemaining=new[]{72f,0f},maxHp=new[]{100f,100f}};
        [Test] public void DraftAwardsDoNotCountAbortsDuplicateResultsOrResumeTwice()
        {
            DraftRuntimeStore.Begin(402);Picks();DraftRuntimeStore.BeginMatch(9,60);DraftRuntimeStore.BackToBuild();
            Assert.IsFalse(AchievementStore.Profile.Has(57));
            DraftRuntimeStore.BeginMatch(10,60);DraftRuntimeStore.Record(Result(0));
            Assert.IsTrue(AchievementStore.Profile.Has(57));Assert.AreEqual(0,AchievementStore.Profile.Value(64));
            string id=DraftRuntimeStore.Current.achievementId;DraftRuntimeStore.ClearSession();AchievementStore.Reload();
            Assert.IsTrue(DraftRuntimeStore.Resume());Assert.AreEqual(id,DraftRuntimeStore.Current.achievementId);
            Assert.IsTrue(DraftRuntimeStore.Resume());Assert.AreEqual(1,AchievementStore.Profile.Value(62));
            DraftRuntimeStore.Next();Picks();DraftRuntimeStore.BeginMatch(11,60);Assert.IsTrue(DraftRuntimeStore.Record(Result(0)));
            Assert.IsFalse(DraftRuntimeStore.Record(Result(0)));Assert.AreEqual(1,AchievementStore.Profile.Value(64));
            AchievementStore.Reload();AchievementStore.Change(p=>p.RecordDraft(DraftRuntimeStore.Current),true);
            Assert.AreEqual(1,AchievementStore.Profile.Value(64));Assert.IsFalse(DraftRuntimeStore.CanResume);
            Assert.AreEqual(1,AchievementStore.Profile.notifications.Count(x=>x==58));
        }
        [Test] public void LegacyProfileAndUnfinishedDraftUpgradeWithoutLosingOriginalTitles()
        {
            PlayerPrefs.SetString(AchievementStore.PrefKey,"{\"version\":1,\"migrated\":true,\"progress\":[{\"id\":2,\"value\":1}],\"unlocked\":[{\"id\":2,\"seen\":true}],\"notifications\":[]}");
            AchievementStore.Reload();Assert.IsTrue(AchievementStore.Profile.Has(2));Assert.IsFalse(AchievementStore.Profile.IsNew(2));
            Assert.IsNotNull(AchievementStore.Profile.draftWinningCharacters);
            DraftRuntimeStore.Begin(404);Picks();DraftRuntimeStore.BeginMatch(9,60);DraftRuntimeStore.Record(Result(0));
            var s=DraftRuntimeStore.Current;s.achievementId=null;PlayerPrefs.SetString(DraftRuntimeStore.PrefKey,JsonUtility.ToJson(s));
            DraftRuntimeStore.ClearSession();Assert.IsTrue(DraftRuntimeStore.Resume());Assert.IsNotEmpty(DraftRuntimeStore.Current.achievementId);
            Assert.IsTrue(AchievementStore.Profile.Has(2));Assert.AreEqual(1,AchievementStore.Profile.Value(62));
            Assert.AreEqual(0,AchievementStore.Profile.Value(64));Assert.AreEqual(1,AchievementStore.Profile.notifications.Count(x=>x==57));
        }
        [Test] public void PoolContainsSixUniqueBakedKanjiAndChangesWithSeedAndRound()
        {
            var seen=new System.Collections.Generic.HashSet<string>();
            foreach(var ch in DraftRuntimeStore.Candidates)foreach(FontStyleId font in System.Enum.GetValues(typeof(FontStyleId)))
                Assert.IsTrue(GlyphCatalog.AvailableGraphemes(font).Contains(ch),ch+" / "+font);
            DraftRuntimeStore.Begin(1337);var first=DraftRuntimeStore.Current.pool.ToArray();DraftRuntimeStore.Begin(1337);
            CollectionAssert.AreEqual(first,DraftRuntimeStore.Current.pool);
            for(int i=0;i<100;i++)
            {
                DraftRuntimeStore.Begin(i);var pool=DraftRuntimeStore.Current.pool;
                Assert.AreEqual(6,pool.Distinct().Count());Assert.IsTrue(pool.All(DraftRuntimeStore.Candidates.Contains));
                foreach(var ch in pool)seen.Add(ch);
            }
            Assert.AreEqual(30,seen.Count);
        }
        [Test] public void ExclusivePicksAlternateAndFinishAtTwoWinsWithDuplicateResultsIgnored()
        {
            DraftRuntimeStore.Begin(78);var s=DraftRuntimeStore.Current;Assert.IsTrue(s.PlayerTurn);
            Assert.IsFalse(DraftRuntimeStore.PickCom());Assert.IsFalse(DraftRuntimeStore.PickPlayer("invalid"));
            Assert.IsTrue(DraftRuntimeStore.PickPlayer(s.pool[0]));Assert.IsFalse(DraftRuntimeStore.PickPlayer(s.pool[1]));
            Assert.IsTrue(DraftRuntimeStore.PickCom());Assert.AreNotEqual(s.player.character,s.com.character);
            Assert.IsFalse(DraftRuntimeStore.PickCom());Assert.IsFalse(DraftRuntimeStore.Next());
            DraftRuntimeStore.BeginMatch(4,60);Assert.IsTrue(DraftRuntimeStore.Record(Result(0)));Assert.IsFalse(DraftRuntimeStore.Record(Result(0)));
            Assert.AreEqual(1,s.playerWins);Assert.AreEqual(1,s.history.Count);
            Assert.IsTrue(DraftRuntimeStore.Next());Assert.AreEqual(2,s.round);Assert.IsTrue(s.ComTurn);
            Assert.IsFalse(DraftRuntimeStore.PickPlayer(s.pool[0]));Assert.IsTrue(DraftRuntimeStore.PickCom());
            Assert.IsFalse(DraftRuntimeStore.PickPlayer(s.com.character));Picks();
            DraftRuntimeStore.BeginMatch(5,60);Assert.IsTrue(DraftRuntimeStore.Record(Result(0)));
            Assert.IsTrue(s.Finished);Assert.AreEqual(2,s.playerWins);Assert.IsFalse(DraftRuntimeStore.Next());Assert.IsFalse(DraftRuntimeStore.CanBattle);
        }
        [Test] public void LossesAndThirdRoundAreCountedForEitherSide()
        {
            DraftRuntimeStore.Begin(46);var s=DraftRuntimeStore.Current;
            foreach(int winner in new[]{1,0,1})
            {
                Picks();DraftRuntimeStore.BeginMatch(9,60);DraftRuntimeStore.Record(Result(winner));
                if(!s.Finished)Assert.IsTrue(DraftRuntimeStore.Next());
            }
            Assert.AreEqual(3,s.round);Assert.AreEqual(1,s.playerWins);Assert.AreEqual(2,s.comWins);Assert.IsTrue(s.Finished);
        }
        [Test] public void DrawReplaysSameLettersWithoutChangingScoreOrPickerOrder()
        {
            DraftRuntimeStore.Begin(39);Picks();var s=DraftRuntimeStore.Current;var pool=s.pool.ToArray();string player=s.player.character,com=s.com.character;
            DraftRuntimeStore.BeginMatch(9,60);DraftRuntimeStore.Record(Result(-1));Assert.IsTrue(DraftRuntimeStore.Next());
            Assert.AreEqual(1,s.round);Assert.AreEqual(0,s.playerWins+s.comWins);Assert.AreEqual(player,s.player.character);Assert.AreEqual(com,s.com.character);
            CollectionAssert.AreEqual(pool,s.pool);Assert.IsTrue(DraftRuntimeStore.CanBattle);
        }
        [Test] public void CharacterIsLockedBuildEditsPreserveRegularBothSidesAndCom()
        {
            DraftRuntimeStore.Begin(23);Picks();var s=DraftRuntimeStore.Current;var com=JsonUtility.ToJson(s.com);
            var customizer=new FighterCustomizer();string player=customizer.Current.character;
            Assert.IsFalse(customizer.SetCharacter(s.com.character));customizer.SelectSide(1);Assert.AreEqual(1,customizer.Side);
            Assert.IsFalse(customizer.Editable);Assert.IsFalse(customizer.SetCharacter(player));
            customizer.SetFont(FontStyleId.Mincho);customizer.SetSize(WeaponSize.XL);customizer.SetGrip(GripType.Reverse);
            customizer.SetGripPoint(Vector2.zero);customizer.SetGripPosition(0);customizer.ResetGrip();
            customizer.SetStyle(BattleStyle.Counter);customizer.SetFlip(WeaponFlip.Vertical);customizer.SetSpecial(null);
            Assert.AreEqual(com,JsonUtility.ToJson(customizer.Current));Assert.AreEqual(com,JsonUtility.ToJson(s.com));
            customizer.SelectSide(0);Assert.IsTrue(customizer.Editable);
            customizer.SetFont(FontStyleId.Mincho);customizer.SetSize(WeaponSize.XL);customizer.SetGrip(GripType.TwoHanded);customizer.SetStyle(BattleStyle.Counter);
            Assert.AreEqual(player,s.player.character);Assert.AreEqual(WeaponSize.XL,s.player.weaponSize);Assert.AreEqual(com,JsonUtility.ToJson(s.com));
            Assert.AreEqual(JsonUtility.ToJson(left),JsonUtility.ToJson(BuildRuntimeStore.Get(0)));Assert.AreEqual(JsonUtility.ToJson(right),JsonUtility.ToJson(BuildRuntimeStore.Get(1)));
            Vector3 values=DraftUiStats.Values(s.player);for(int i=0;i<3;i++)Assert.That(values[i],Is.InRange(0f,1f));
        }
        [Test] public void ResumePreservesPartialPicksScoresAndInterruptedBattleButRejectsInvalidData()
        {
            DraftRuntimeStore.Begin(87);DraftRuntimeStore.PickPlayer(DraftRuntimeStore.Current.pool[0]);string chosen=DraftRuntimeStore.Current.player.character;
            DraftRuntimeStore.ClearSession();Assert.IsTrue(DraftRuntimeStore.Resume());Assert.AreEqual(chosen,DraftRuntimeStore.Current.player.character);
            DraftRuntimeStore.PickCom();DraftRuntimeStore.BeginMatch(77,60);DraftRuntimeStore.ClearSession();Assert.IsTrue(DraftRuntimeStore.Resume());Assert.IsTrue(DraftRuntimeStore.CanBattle);
            DraftRuntimeStore.BeginMatch(78,60);DraftRuntimeStore.Record(Result(0));DraftRuntimeStore.Next();DraftRuntimeStore.ClearSession();Assert.IsTrue(DraftRuntimeStore.Resume());Assert.AreEqual(1,DraftRuntimeStore.Current.playerWins);
            var invalid=DraftRuntimeStore.Current;invalid.playerWins=12;PlayerPrefs.SetString(DraftRuntimeStore.PrefKey,JsonUtility.ToJson(invalid));DraftRuntimeStore.ClearSession();Assert.IsFalse(DraftRuntimeStore.Resume());
            PlayerPrefs.SetString(DraftRuntimeStore.PrefKey,"{bad json");Assert.IsFalse(DraftRuntimeStore.CanResume);
        }
    }
}
