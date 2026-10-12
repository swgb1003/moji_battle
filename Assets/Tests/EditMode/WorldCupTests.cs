using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MojiBattle.Tests
{
    public class WorldCupTests
    {
        string saved;bool hadSave;
        [SetUp] public void Setup(){hadSave=PlayerPrefs.HasKey(WorldCupRuntimeStore.PrefKey);saved=PlayerPrefs.GetString(WorldCupRuntimeStore.PrefKey);WorldCupRuntimeStore.ClearSession();}
        [TearDown] public void Cleanup(){WorldCupRuntimeStore.ClearSession();if(hadSave)PlayerPrefs.SetString(WorldCupRuntimeStore.PrefKey,saved);else PlayerPrefs.DeleteKey(WorldCupRuntimeStore.PrefKey);PlayerPrefs.Save();}
        public static WorldCupSession Make(int seed=91)
        {
            var letters=WorldCupCatalog.Letters.Take(48).Select(c=>c.ToString()).ToArray();
            return new WorldCupSession(letters.Select((c,i)=>new WorldCupEntrant(i,i/4,FighterBuildData.Default(c))).ToList(),seed);
        }
        public static MatchResult Result(WorldCupMatch m,int winner,string reason="KO",float hp=50)
            => new MatchResult {seed=m.Seed,winner=winner,finishReason=reason,elapsedSeconds=reason=="KO"?20:60,
                hpRemaining=winner==0?new[]{hp,0f}:winner==1?new[]{0f,hp}:new[]{40f,40f},maxHp=new[]{100f,100f}};
        public static void Finish(WorldCupSession s,WorldCupMatch m,int winner,string reason="KO",float hp=50)
        {s.BeginMatch(m.Id,60);Assert.IsTrue(s.Record(m.Id,Result(m,winner,reason,hp)));}
        public static void WinGroups(WorldCupSession s)
        {foreach(var m in s.Matches.Take(72).ToArray())Finish(s,m,m.IncludesPlayer?m.LeftId==0?0:1:0);}
        public static void Computers(WorldCupSession s)
        {
            for(int n=0;n<104 && s.NextComputerMatch!=null;n++){var m=s.NextComputerMatch;Finish(s,m,0);}
        }
        [Test] public void ScheduleHasEveryPairExactlyOnceAndAll48PrivateBuildsAreM()
        {
            var s=Make();Assert.AreEqual(72,s.Matches.Count);Assert.AreEqual(48,s.Entrants.Count);
            foreach(int g in Enumerable.Range(0,12))
            {
                var fixtures=s.Matches.Where(m=>m.Group==g).ToArray();Assert.AreEqual(6,fixtures.Length);
                Assert.AreEqual(6,fixtures.Select(m=>Math.Min(m.LeftId,m.RightId)+"-"+Math.Max(m.LeftId,m.RightId)).Distinct().Count());
                foreach(var e in s.Entrants.Where(e=>e.Group==g))Assert.AreEqual(3,fixtures.Count(m=>m.LeftId==e.Id || m.RightId==e.Id));
            }
            var copy=s.Entrants[0].Build;copy.weaponSize=WeaponSize.XL;copy.character="猫";
            Assert.AreEqual("火",s.Entrants[0].Build.character);Assert.IsTrue(s.Entrants.All(e=>e.Build.weaponSize==WeaponSize.M));
        }
        [Test] public void LeagueDrawAwardsOnePointEachAndDoesNotReplay()
        {
            var s=Make();var m=s.NextPlayerMatch;Finish(s,m,-1,"TIME_UP_DRAW");
            Assert.IsTrue(m.Complete);Assert.AreEqual(0,m.Draws);Assert.AreEqual(1,s.Standings(0).Single(r=>r.Id==0).Points);
            Assert.AreEqual(1,s.Standings(0).Single(r=>r.Id==m.RightId).Points);Assert.AreNotSame(m,s.NextPlayerMatch);
        }
        [Test] public void LeagueSortingAndThirdComparisonPrioritizeKoThenWinningHp()
        {
            var s=Make();
            foreach(var m in s.Matches.Take(72).ToArray())Finish(s,m,0,"TIME_UP",10+m.Group);
            var third=s.ThirdRanking();Assert.AreEqual(12,third.Count);Assert.AreEqual(11,third[0].Group);
            Assert.IsTrue(third.All(r=>r.KoWins==0));Assert.Greater(third[0].TimeUpHp,third[1].TimeUpHp);
            Assert.AreEqual(32,s.Qualifiers().Distinct().Count());
            foreach(int g in Enumerable.Range(0,12))Assert.IsTrue(s.Standings(g).Take(2).All(r=>s.Qualifiers().Contains(r.Id)));
        }
        [Test] public void KoWinRanksAboveMoreTimeUpHp()
        {
            var s=Make();foreach(var m in s.Matches.Take(72).ToArray())Finish(s,m,0,m.Group==0?"KO":"TIME_UP",m.Group==0?1:90);
            Assert.AreEqual(0,s.ThirdRanking()[0].Group);Assert.AreEqual(1,s.ThirdRanking()[0].KoWins);
            Assert.AreEqual(0,s.ThirdRanking()[0].TimeUpHp);
        }
        [TestCase(1)][TestCase(27)][TestCase(98)] public void KnockoutHas32UniqueEntrantsAndNoFirstRoundSameGroup(int seed)
        {
            var s=Make(seed);WinGroups(s);Assert.AreEqual(104,s.Matches.Count);
            var ids=s.Matches.Skip(72).Take(16).SelectMany(m=>new[]{m.LeftId,m.RightId}).ToArray();Assert.AreEqual(32,ids.Distinct().Count());
            CollectionAssert.AreEquivalent(s.Qualifiers(),ids);
            Assert.IsTrue(s.Matches.Skip(72).Take(16).All(m=>s.Entrants[m.LeftId].Group!=s.Entrants[m.RightId].Group));
        }
        [Test] public void EightPlayerWinsCrownChampionAndResolveDistinctPodium()
        {
            var s=Make();WinGroups(s);Computers(s);
            for(int r=0;r<5;r++){var m=s.NextPlayerMatch;Assert.IsNotNull(m);Assert.AreEqual(r,m.Round);Finish(s,m,m.LeftId==0?0:1);Computers(s);}
            Assert.IsTrue(s.Finished);Assert.AreEqual(0,s.ChampionId);Assert.AreEqual(8,s.PlayerWins);
            Assert.AreEqual(4,new[]{s.ChampionId,s.RunnerUpId,s.ThirdId,s.FourthId}.Distinct().Count());
            Assert.AreEqual(104,s.Matches.Count(m=>m.Complete));Assert.AreEqual(0,s.Matches[103].WinnerId==s.ChampionId?1:0);
        }
        [TestCase(true)][TestCase(false)] public void SemifinalDefeatKeepsPlayerForBronzeAndFinishesFinal(bool bronzeWin)
        {
            var s=Make();WinGroups(s);Computers(s);
            for(int round=0;round<4;round++){var m=s.NextPlayerMatch;int side=m.LeftId==0?0:1;Finish(s,m,round==3?1-side:side);Computers(s);}
            Assert.IsFalse(s.Eliminated);Assert.IsTrue(s.NextPlayerMatch.Bronze);
            var bronze=s.NextPlayerMatch;int playerSide=bronze.LeftId==0?0:1;Finish(s,bronze,bronzeWin?playerSide:1-playerSide);Computers(s);
            Assert.IsTrue(s.Finished);Assert.AreEqual(0,bronzeWin?s.ThirdId:s.FourthId);Assert.Greater(s.ChampionId,0);
        }
        [Test] public void KnockoutDrawKeepsBuildsAndChangesSeedWithoutAdvancing()
        {
            var s=Make();WinGroups(s);Computers(s);var m=s.NextPlayerMatch;var config=s.BeginMatch(m.Id,60);
            Assert.IsTrue(s.Record(m.Id,Result(m,-1,"DOUBLE_KO")));Assert.IsFalse(m.Complete);Assert.AreEqual(1,m.Draws);
            Assert.AreNotEqual(config.seed,m.Seed);Assert.AreSame(m,s.NextPlayerMatch);Assert.AreEqual(-1,s.Match(88+(m.Id-72)/2).LeftId);
        }
        [Test] public void StaleDuplicateAndInvalidResultsAreRejected()
        {
            var s=Make();var m=s.NextPlayerMatch;s.BeginMatch(m.Id,60);var r=Result(m,0);r.seed+=2;Assert.IsFalse(s.Record(m.Id,r));
            r=Result(m,0);r.hpRemaining[0]=float.NaN;Assert.IsFalse(s.Record(m.Id,r));
            r=Result(m,0);r.winner=2;Assert.IsFalse(s.Record(m.Id,r));
            r=Result(m,0);Assert.IsTrue(s.Record(m.Id,r));Assert.IsFalse(s.Record(m.Id,r));Assert.AreEqual(1,s.Journal.Count);
        }
        [Test] public void SaveReplaysJournalAndPreservesDrawSeedAndIgnoresInterruptedStart()
        {
            WorldCupRuntimeStore.Begin(FighterBuildData.Default("火"),281);var s=WorldCupRuntimeStore.Current;
            foreach(var m in s.Matches.Take(72).ToArray()){s.BeginMatch(m.Id,60);WorldCupRuntimeStore.Record(m.Id,Result(m,m.IncludesPlayer?(m.LeftId==0?0:1):0));}
            var first=s.NextPlayerMatch;s.BeginMatch(first.Id,60);WorldCupRuntimeStore.Record(first.Id,Result(first,-1,"TIME_UP_DRAW"));
            int seed=first.Seed;var builds=s.Entrants.Select(e=>JsonUtility.ToJson(e.Build)).ToArray();s.BeginMatch(first.Id,60);WorldCupRuntimeStore.ClearSession();
            Assert.IsTrue(WorldCupRuntimeStore.Resume());var restored=WorldCupRuntimeStore.Current;
            Assert.AreEqual(73,restored.Journal.Count);Assert.AreEqual(seed,restored.NextPlayerMatch.Seed);Assert.IsTrue(restored.NextPlayerMatch.Ready);
            CollectionAssert.AreEqual(builds,restored.Entrants.Select(e=>JsonUtility.ToJson(e.Build)).ToArray());
        }
        [Test] public void InvalidSavesAreRejectedAndCommonCustomizerDoesNotOverwriteOrdinaryBuild()
        {
            PlayerPrefs.SetString(WorldCupRuntimeStore.PrefKey,"garbage");Assert.IsFalse(WorldCupRuntimeStore.CanResume);
            string ordinary=JsonUtility.ToJson(BuildRuntimeStore.Get(0));WorldCupRuntimeStore.Prepare();var c=new FighterCustomizer();
            c.SetSize(WeaponSize.XL);Assert.AreEqual(WeaponSize.M,c.Current.weaponSize);c.SetGrip(GripType.Reverse);
            Assert.AreEqual(GripType.Reverse,WorldCupRuntimeStore.Draft.gripType);Assert.AreEqual(ordinary,JsonUtility.ToJson(BuildRuntimeStore.Get(0)));
            c.SelectSide(1);Assert.AreEqual(0,c.Side);WorldCupRuntimeStore.Begin(c.Current,55);
            var locked=new FighterCustomizer();Assert.IsFalse(locked.Editable);Assert.IsFalse(locked.SetCharacter("水"));
        }
        [TestCase("SUDDEN_DEATH")][TestCase("SUDDEN_DEATH_DRAW")]
        public void KnockoutAcceptsOvertimeResults(string reason)
        {
            var s=Make();WinGroups(s);var m=s.NextPlayerMatch;Finish(s,m,reason.EndsWith("DRAW")?-1:0,reason);
            Assert.AreEqual(reason.EndsWith("DRAW"),!m.Complete);
        }
        [Test] public void SavedPagesCannotReferToUnbuiltBracketOrMissingMatchResult()
        {
            WorldCupRuntimeStore.Begin(FighterBuildData.Default("火"),18);
            WorldCupRuntimeStore.Show(WorldCupPage.Bracket);Assert.IsFalse(WorldCupRuntimeStore.CanResume);
            WorldCupRuntimeStore.Show(WorldCupPage.MatchResult);Assert.IsFalse(WorldCupRuntimeStore.CanResume);
            WorldCupRuntimeStore.Show(WorldCupPage.Draw);Assert.IsTrue(WorldCupRuntimeStore.CanResume);
        }
    }
}
