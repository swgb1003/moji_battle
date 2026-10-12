using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MojiBattle.Tests
{
    public class AchievementTests
    {
        static DraftSession Draft(params int[] winners)
        {
            var s=new DraftSession { achievementId=System.Guid.NewGuid().ToString("N") };
            int round=1;
            foreach(int winner in winners)
            {
                s.history.Add(new DraftRound {round=round,winner=winner,player="火",com="盾"});
                if(winner==0)s.playerWins++;else if(winner==1)s.comWins++;
                if(winner>=0)round++;
            }
            return s;
        }
        [Test] public void DraftRewardsDistinguishParticipationShutoutComebackAndComFirstWins()
        {
            Assert.AreEqual("ドラフト",AchievementCatalog.Categories[8]);
            CollectionAssert.AreEqual(Enumerable.Range(57,8),AchievementCatalog.All.Where(d=>d.Category==8).Select(d=>d.Id));
            var p=new AchievementProfile();p.RecordDraft(Draft(-1));
            Assert.IsTrue(p.Has(57));Assert.IsFalse(p.Has(58));Assert.AreEqual(0,p.Value(64));Assert.AreEqual(0,p.Value(62));
            p=new AchievementProfile();p.RecordDraft(Draft(1,1));
            Assert.IsTrue(p.Has(57));Assert.IsFalse(p.Has(58));Assert.IsFalse(p.Has(61));Assert.AreEqual(0,p.Value(64));
            p=new AchievementProfile();p.RecordDraft(Draft(-1,0,-1,0));
            Assert.IsTrue(p.Has(58));Assert.IsTrue(p.Has(59));Assert.IsTrue(p.Has(61));Assert.IsFalse(p.Has(60));Assert.AreEqual(1,p.Value(64));
            p=new AchievementProfile();p.RecordDraft(Draft(-1,1,-1,0,0));
            Assert.IsTrue(p.Has(60));Assert.IsTrue(p.Has(58));Assert.IsTrue(p.Has(61));Assert.IsFalse(p.Has(59));
            p=new AchievementProfile();p.RecordDraft(Draft(0,1,0));Assert.IsFalse(p.Has(60),"A 1–0 lead followed by 2–1 is not a 0–1 comeback.");
        }
        [Test] public void DraftDistinctCharactersAndTenChampionshipsPersistAndIgnoreRepeatedHistory()
        {
            var p=new AchievementProfile();
            for(int i=0;i<15;i++)
            {
                var s=Draft(0);s.history[0].player=DraftRuntimeStore.Candidates[i];p.RecordDraft(s);p.RecordDraft(s);
                if(i==3)p=JsonUtility.FromJson<AchievementProfile>(JsonUtility.ToJson(p));
                Assert.AreEqual(i+1,p.Value(62));Assert.AreEqual(i+1,p.Value(63));
                Assert.AreEqual(i>=4,p.Has(62));Assert.AreEqual(i>=14,p.Has(63));
            }
            var repeatedLetter=Draft(0);repeatedLetter.history[0].player=DraftRuntimeStore.Candidates[0];p.RecordDraft(repeatedLetter);
            Assert.AreEqual(15,p.draftWinningCharacters.Count);
            for(int i=0;i<10;i++)
            {
                var s=Draft(0,0);p.RecordDraft(s);p=JsonUtility.FromJson<AchievementProfile>(JsonUtility.ToJson(p));p.RecordDraft(s);
                Assert.AreEqual(i+1,p.Value(64));Assert.AreEqual(i==9,p.Has(64));
            }
            Assert.AreEqual(1,p.unlocked.Count(u=>u.id==64));Assert.AreEqual(1,p.notifications.Count(id=>id==64));
        }
        [Test] public void DraftNotificationsPrecedeOrdinaryQueueAndOnlyNewResultsChangeProgress()
        {
            var p=new AchievementProfile();p.Set(2,1);
            var s=Draft(0);p.RecordDraft(s);
            CollectionAssert.AreEqual(new[]{57,2},p.notifications);
            s.history.Add(new DraftRound {round=2,winner=0,player="龍",com="刀"});p.RecordDraft(s);
            CollectionAssert.AreEqual(new[]{58,59,61,57,2},p.notifications);
            int count=p.Count;p.RecordDraft(s);Assert.AreEqual(count,p.Count);Assert.AreEqual(1,p.Value(64));
            Assert.AreEqual(2,p.Value(62));Assert.AreEqual(2,p.draftRecordedMatches);
        }
        [Test]
        public void StableOriginalDefinitionsAndAllChallengeAndDraftRewardsUnlockExactlyOnce()
        {
            Assert.AreEqual(68,AchievementCatalog.All.Length);
            CollectionAssert.AreEqual(Enumerable.Range(1,68),AchievementCatalog.All.Select(d=>d.Id));
            foreach(var definition in AchievementCatalog.All.Where(d=>d.Id!=50))
            {
                var p=new AchievementProfile();
                p.Set(definition.Id,definition.Goal-1); Assert.IsFalse(p.Has(definition.Id),definition.Name);
                p.Add(definition.Id); Assert.IsTrue(p.Has(definition.Id),definition.Name);
                p.Add(definition.Id); p.Check();
                Assert.AreEqual(1,p.unlocked.Count(u=>u.id==definition.Id));
                Assert.AreEqual(1,p.notifications.Count(id=>id==definition.Id));
            }
        }
        [Test]
        public void MetaRequiresFortyOtherTitlesAndQueuesAfterItsTrigger()
        {
            var p=new AchievementProfile();
            foreach(var d in AchievementCatalog.All.Take(39)) p.Set(d.Id,d.Goal);
            Assert.IsFalse(p.Has(50)); Assert.AreEqual(39,p.Value(50));
            p.Set(40,4); Assert.IsTrue(p.Has(50)); Assert.AreEqual(41,p.Count);
            CollectionAssert.AreEqual(new[]{40,50},p.notifications.Skip(39));
        }
        [Test]
        public void DrawPreservesStreakAndLossOrExitResetsAcrossModes()
        {
            var p=new AchievementProfile();
            p.Result(0,0); p.Result(1,1); p.Result(-1,0); p.Result(0,0);
            Assert.AreEqual(3,p.streak); Assert.IsTrue(p.Has(15)); Assert.AreEqual(3,p.Value(17));
            p.Result(1,0); Assert.AreEqual(0,p.streak); Assert.IsTrue(p.Has(15));
            p.Result(1,1); p.Interrupted(); Assert.AreEqual(0,p.streak);
        }
        [Test]
        public void DistinctBuildVariantsAndJapaneseFifteenStrokeBoundary()
        {
            var p=new AchievementProfile();
            for(int i=0;i<4;i++)
                p.WinBuild(new FighterBuildData { fontType=(FontStyleId)i,gripType=(GripType)i,battleStyle=(BattleStyle)i,character="火" },FighterBuildData.Default("一"));
            Assert.IsTrue(p.Has(36)); Assert.IsTrue(p.Has(40)); Assert.IsTrue(p.Has(41));
            Assert.AreEqual(15,AchievementCharacters.Strokes("舞"));
            Assert.AreEqual(14,AchievementCharacters.Strokes("語"));
            Assert.AreEqual(16,AchievementCharacters.Strokes("龍"));
            Assert.AreEqual(29,AchievementCharacters.Strokes("鬱"));
            var b=FighterBuildData.Default("語"); p.WinBuild(b,b); Assert.AreEqual(0,p.Value(45));
            b.character="舞"; p.WinBuild(b,b); Assert.AreEqual(1,p.Value(45));
            Assert.IsTrue(AchievementCharacters.IsKana("あ")); Assert.IsTrue(AchievementCharacters.IsKana("ア"));
            Assert.IsTrue(AchievementCharacters.IsLatinOrDigit("Ａ")); Assert.IsTrue(AchievementCharacters.IsLatinOrDigit("9"));
            Assert.IsTrue(AchievementCharacters.IsSymbol("→")); Assert.IsTrue(AchievementCharacters.IsSymbol("★"));
            Assert.IsFalse(AchievementCharacters.IsSymbol("火"));
        }
        [Test]
        public void JsonRoundtripRetainsProgressBadgesNewStateAndPendingQueue()
        {
            var p=new AchievementProfile(); p.Set(21,18); p.Set(2,1);
            p.unlocked[0].seen=true; p.streak=2;
            var restored=JsonUtility.FromJson<AchievementProfile>(JsonUtility.ToJson(p));
            Assert.AreEqual(18,restored.Value(21)); Assert.IsTrue(restored.Has(2)); Assert.IsFalse(restored.IsNew(2));
            Assert.AreEqual(2,restored.streak); CollectionAssert.AreEqual(new[]{2},restored.notifications);
        }
    }
}
