using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class AchievementFlowTests
    {
        string saved;
        bool hadSave;
        BattleInstance battle;
        [SetUp]
        public void Setup()
        {
            hadSave=PlayerPrefs.HasKey(AchievementStore.PrefKey); saved=PlayerPrefs.GetString(AchievementStore.PrefKey,"");
            PlayerPrefs.SetString(AchievementStore.PrefKey,JsonUtility.ToJson(new AchievementProfile { migrated=true }));
            AchievementStore.Reload(); SimHarness.Begin(false);
            if(AchievementToast.Instance?.CurrentId>0) AchievementToast.Instance.Dismiss();
        }
        [TearDown]
        public void Cleanup()
        {
            battle?.Destroy(); battle=null; SimHarness.End();
            if(hadSave) PlayerPrefs.SetString(AchievementStore.PrefKey,saved); else PlayerPrefs.DeleteKey(AchievementStore.PrefKey);
            AchievementStore.Reload(); PlayerPrefs.Save();
            GameFlow.ToTitle();
        }
        AchievementMatchRecorder Build(int side=0)
        {
            battle=SimHarness.Build(773,SimHarness.Custom("火"),SimHarness.Custom("火"));
            battle.Director.enabled=false;
            battle.Context.Interventions=new BattleIntervention(battle.Director,InterventionController.Local,InterventionController.Local);
            var recorder=battle.Root.AddComponent<AchievementMatchRecorder>(); recorder.Bind(battle.Director,side,false);
            return recorder;
        }
        [UnityTest]
        public IEnumerator CountsOnlyOwnSideAndDeduplicatesHitAndSpecialSteps()
        {
            var tracker=Build();
            var e=battle.Context.Events;
            e.Raise(new HitEvent { attacker=1,defender=0,attackId=1,critical=true,style=AttackStyle.StrokeShot });
            Assert.AreEqual(0,AchievementStore.Profile.Value(21));
            var hit=new HitEvent { attacker=0,defender=1,attackId=2,critical=true,style=AttackStyle.StrokeShot };
            e.Raise(hit); e.Raise(hit);
            Assert.AreEqual(1,AchievementStore.Profile.Value(21)); Assert.AreEqual(1,AchievementStore.Profile.Value(19));
            for(int step=0;step<4;step++)
            {
                e.Raise(new InterventionEvent { fighter=0,kind=InterventionKind.Special,success=true,castId=1,castStep=step,attackId=10+step,stepCount=4 });
                e.Raise(new HitEvent { attacker=0,defender=1,attackId=10+step,style=AttackStyle.Overhead });
                if(step<3) Assert.IsFalse(AchievementStore.Profile.Has(29));
            }
            Assert.IsTrue(AchievementStore.Profile.Has(29)); Assert.AreEqual(1,AchievementStore.Profile.Value(28));
            yield return null;
        }
        [UnityTest]
        public IEnumerator GuestCumulativeSnapshotsSurviveSkippedAndRepeatedFrames()
        {
            var tracker=Build(1); battle.Director.RemotePlayback=true;
            var stats=new[]{new AchievementMatchStats(),new AchievementMatchStats()};
            stats[1].totals[21]=18; stats[1].totals[34]=2;
            tracker.ApplyRemote(stats,true); tracker.ApplyRemote(stats,true);
            Assert.AreEqual(18,AchievementStore.Profile.Value(21)); Assert.AreEqual(2,AchievementStore.Profile.Value(34));
            stats[1].totals[21]=30; tracker.ApplyRemote(stats,true);
            Assert.IsTrue(AchievementStore.Profile.Has(21)); Assert.AreEqual(30,AchievementStore.Profile.Value(21));
            // Stale/replayed presentation events are not another source of progress.
            battle.Context.Events.Raise(new HitEvent { attacker=1,defender=0,attackId=99,critical=true });
            Assert.AreEqual(30,AchievementStore.Profile.Value(21));
            yield return null;
        }
        [UnityTest]
        public IEnumerator DamageHistoryComebackAndFinishAreSingleResult()
        {
            var tracker=Build();
            battle.Left.Runtime.hp=battle.Left.Runtime.maxHp*.04f;
            tracker.SampleVitals();
            var r=new MatchResult { winner=0,elapsedSeconds=15,finishReason="KO",seed=773,
                hpRemaining=new[]{battle.Left.Runtime.hp,0f},maxHp=new[]{battle.Left.Runtime.maxHp,battle.Right.Runtime.maxHp} };
            battle.Context.Events.Raise(r); battle.Context.Events.Raise(r);
            Assert.IsFalse(AchievementStore.Profile.Has(8)); Assert.IsTrue(AchievementStore.Profile.Has(9));
            Assert.IsTrue(AchievementStore.Profile.Has(10)); Assert.IsTrue(AchievementStore.Profile.Has(11));
            Assert.AreEqual(1,AchievementStore.Profile.Value(17));
            yield return null;
        }
        [UnityTest]
        public IEnumerator AbandoningAPlayerMatchResetsStreakButUntrackedComDoesNotCount()
        {
            AchievementStore.Change(p=>p.Result(0,0),true);
            battle=SimHarness.Build(123); battle.Director.enabled=false;
            Assert.AreEqual(1,AchievementStore.Profile.streak); Assert.IsFalse(AchievementStore.Profile.matchInProgress);
            var tracker=battle.Root.AddComponent<AchievementMatchRecorder>(); tracker.Bind(battle.Director,0,false);
            battle.Root.SetActive(false);
            Assert.AreEqual(0,AchievementStore.Profile.streak); Assert.IsFalse(AchievementStore.Profile.matchInProgress);
            yield return null;
        }
        [UnityTest]
        public IEnumerator TournamentIronWallUsesLowestHpAcrossAllPlayerRounds()
        {
            var tracker=Build(); Object.DestroyImmediate(tracker);
            var computers=Enumerable.Range(0,7).Select(_=>FighterBuildData.Default("火")).ToList();
            var cup=new TournamentSession(FighterBuildData.Default("火"),computers,5);
            tracker=battle.Root.AddComponent<AchievementMatchRecorder>(); tracker.Bind(battle.Director,0,false,cup,2);
            battle.Left.Runtime.hp=battle.Left.Runtime.maxHp*.5f; tracker.SampleVitals();
            battle.Left.Runtime.hp=battle.Left.Runtime.maxHp;
            var r=new MatchResult { winner=0,elapsedSeconds=16,finishReason="TIME_UP",seed=773,
                hpRemaining=new[]{battle.Left.Runtime.hp,0f},maxHp=new[]{battle.Left.Runtime.maxHp,battle.Right.Runtime.maxHp} };
            battle.Context.Events.Raise(r);
            Assert.IsTrue(AchievementStore.Profile.Has(47)); Assert.IsFalse(AchievementStore.Profile.Has(48));
            Assert.IsFalse(AchievementStore.Profile.Has(8)); Assert.IsFalse(AchievementStore.Profile.Has(11));
            Assert.IsTrue(AchievementStore.Profile.Has(12)); Assert.IsFalse(cup.AchievementIronWall);
            yield return null;
        }
        [UnityTest]
        public IEnumerator RealSimulatedMatchRecordsOneResultAtSpectatorDoubleSpeed()
        {
            SimHarness.Begin(false);
            battle=SimHarness.Build(799,SimHarness.Custom("火"),SimHarness.Custom("火"));
            battle.Root.AddComponent<AchievementMatchRecorder>().Bind(battle.Director,0,false);
            yield return SimHarness.RunToEnd(battle,2f,6000);
            Assert.IsTrue(battle.Director.Ended); Assert.IsTrue(AchievementStore.Profile.Has(1));
            Assert.AreEqual(battle.Director.Result.winner==0?1:0,AchievementStore.Profile.Value(17));
            Assert.IsFalse(AchievementStore.Profile.matchInProgress);
        }
        [UnityTest]
        public IEnumerator AllFiftyBadges_RenderEveryPageAndMatchingDetails()
        {
            SimHarness.End();
            AchievementStore.Change(p=> {
                foreach(var d in AchievementCatalog.All) p.Set(d.Id,d.Goal);
                p.notifications.Clear();
            },true);
            GameFlow.ToAchievements(); yield return new WaitForSecondsRealtime(.35f);
            var screen=Object.FindFirstObjectByType<AchievementsScreen>();
            for(int page=0;page<9;page++)
            {
                if(page>0) screen.ChangePage(1);
                yield return null;
                for(int id=page*6+1;id<=Mathf.Min(50,page*6+6);id++)
                {
                    screen.Select(id); yield return null;
                    var card=GameObject.Find("Achievement_"+id);
                    var detail=GameObject.Find("AchievementDetails");
                    var small=card.GetComponentInChildren<RawImage>();
                    var large=detail.GetComponentsInChildren<RawImage>().Single();
                    Assert.AreEqual(small.uvRect,large.uvRect);
                    if(id==10) Assert.AreSame(Resources.Load<Texture2D>("UI/Achievements/Badge10"),small.texture,"大逆転は隣の拳を含まない独立画像");
                    Assert.AreEqual(AchievementCatalog.Get(id).Name,GameObject.Find("DetailName").GetComponent<Text>().text);
                    var cell=AchievementBadge.PixelRect(id);
                    Assert.That(cell.xMin,Is.GreaterThanOrEqualTo(0));
                    Assert.That(cell.xMax,Is.LessThanOrEqualTo(1774));
                    Assert.That(cell.yMax,Is.LessThanOrEqualTo(887));
                }
                screen.Select(page*6+1); yield return null;
                Capture("fixed_page_"+(page+1));
            }
            screen.ChooseCategory(0); screen.ChangePage(2); screen.Select(13); yield return null;
            MenuUiTests.Capture("Achievements/fixed_page_3_880x450",880,450);
            var gallery=RetroUi.Screen("BadgeReviewCanvas",out _,100);
            RetroUi.PaperSurface(gallery,0,0,1920,1080);
            for(int id=1;id<=50;id++)
            {
                float x=28+(id-1)%10*188,y=26+(id-1)/10*207;
                AchievementBadge.Create(gallery,id,true,x+12,y,164);
                RetroUi.Label(gallery,"Label_"+id,id+" / "+AchievementCatalog.Get(id).Name,x,y+167,188,38,19,RetroUi.Ink,TextAnchor.MiddleCenter);
            }
            yield return null;
            Capture("fixed_all_50_badges");
            Object.Destroy(gallery.parent.gameObject);
        }

        [UnityTest]
        public IEnumerator ApprovedScreensCategoriesLockedConditionsAndToastSurviveSceneChange()
        {
            SimHarness.End();
            AchievementStore.Change(p=> { p.Set(2,1); p.Set(3,1); p.Set(8,1); p.Set(21,18); p.Set(4,2); p.Set(45,4); },true);
            GameFlow.ToModeSelect(); yield return new WaitForSecondsRealtime(1f);
            Assert.IsNotNull(GameObject.Find("FriendConnectionIcon"));
            Assert.IsNotNull(GameObject.Find("AchievementsStart")); Capture("01_menu");
            MenuUiTests.Capture("Achievements/01_menu_880x450",880,450);
            GameFlow.ToAchievements(); yield return new WaitForSecondsRealtime(.3f);
            var screen=Object.FindFirstObjectByType<AchievementsScreen>(); Assert.IsNotNull(screen);
            screen.Select(2); yield return null; Capture("02_achievements");
            MenuUiTests.Capture("Achievements/02_achievements_880x450",880,450);
            Assert.IsFalse(AchievementStore.Profile.IsNew(2));
            screen.ChooseCategory(3); yield return null;
            screen.Select(21); yield return null; Capture("03_locked_details");
            var name=GameObject.Find("DetailName").GetComponent<Text>(); Assert.AreEqual("？？？",name.text);
            Assert.AreEqual("18 / 30",GameObject.Find("Achievement_21").GetComponentsInChildren<Text>().Single(t=>t.name=="ProgressText").text);
            Assert.IsNotNull(AchievementToast.Instance);
            // Clear migrations/earlier notifications so this test controls both queue slots.
            if(AchievementToast.Instance.CurrentId!=0) AchievementToast.Instance.Dismiss();
            AchievementStore.Change(p=> { p.notifications.Clear(); p.notifications.Add(2); p.notifications.Add(3); },true);
            yield return null;
            Assert.AreEqual(2,AchievementToast.Instance.CurrentId);
            var persistent=AchievementToast.Instance;
            TimeController.SetPaused(true);
            GameFlow.ToModeSelect(); yield return new WaitForSecondsRealtime(4.2f);
            Assert.AreSame(persistent,AchievementToast.Instance); Assert.AreEqual(3,persistent.CurrentId);
            Capture("04_toast_after_transition");
            AchievementStore.Save(); AchievementStore.Reload();
            Assert.IsTrue(AchievementStore.Profile.Has(2)); Assert.AreEqual(18,AchievementStore.Profile.Value(21));
            Assert.AreEqual(1,AchievementStore.Profile.notifications.Count);
        }
        static void Capture(string name)
        {
            string dir=Path.Combine(SimHarness.ReportDir,"UI/Achievements"); Directory.CreateDirectory(dir);
            MenuUiTests.Capture("Achievements/"+name);
        }
    }
}
