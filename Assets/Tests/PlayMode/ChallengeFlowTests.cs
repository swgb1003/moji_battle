using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class ChallengeFlowTests
    {
        string progress,achievements,settings;bool hadProgress,hadAchievements,hadSettings;int motion;
        FighterBuildData left,right;
        [SetUp]public void Setup()
        {
            hadProgress=PlayerPrefs.HasKey(ChallengeRuntimeStore.PrefKey);progress=PlayerPrefs.GetString(ChallengeRuntimeStore.PrefKey);
            hadAchievements=PlayerPrefs.HasKey(AchievementStore.PrefKey);achievements=PlayerPrefs.GetString(AchievementStore.PrefKey);
            hadSettings=PlayerPrefs.HasKey(GameSettings.PrefKey);settings=PlayerPrefs.GetString(GameSettings.PrefKey);
            left=BuildRuntimeStore.Get(0).Clone();right=BuildRuntimeStore.Get(1).Clone();motion=PlayerPrefs.GetInt("MenuReducedMotion",0);
            PlayerPrefs.DeleteKey(ChallengeRuntimeStore.PrefKey);ChallengeRuntimeStore.Reload();AchievementStore.Reset();GameSettings.Reset();
            PlayerPrefs.SetInt("MenuReducedMotion",1);TimeController.ResetAll();TimeController.PresentationEffectsEnabled=true;Time.captureDeltaTime=0;
            Directory.CreateDirectory(Path.Combine(Application.dataPath,"../Reports/UI/Challenges"));
        }
        [TearDown]public void Cleanup()
        {
            if(SettingsScreen.IsOpen)SettingsScreen.Instance.Close();GameFlow.ToTitle();SimHarness.End();
            BuildRuntimeStore.Set(0,left);BuildRuntimeStore.Set(1,right);BuildRuntimeStore.Active=false;
            Restore(ChallengeRuntimeStore.PrefKey,hadProgress,progress);Restore(AchievementStore.PrefKey,hadAchievements,achievements);Restore(GameSettings.PrefKey,hadSettings,settings);
            PlayerPrefs.SetInt("MenuReducedMotion",motion);PlayerPrefs.Save();ChallengeRuntimeStore.Reload();AchievementStore.Reload();GameSettings.Reload();
        }
        static void Restore(string key,bool existed,string value){if(existed)PlayerPrefs.SetString(key,value);else PlayerPrefs.DeleteKey(key);}
        static Button Button(string name)=>Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name==name);
        static IEnumerator Ready(){yield return null;yield return null;yield return new WaitForSecondsRealtime(.65f);}
        static IEnumerator End(BattleBootstrap boot)
        {
            double until=Time.realtimeSinceStartupAsDouble+8;
            while(!boot.Battle.Director.Ended && Time.realtimeSinceStartupAsDouble<until)yield return null;
            Assert.IsTrue(boot.Battle.Director.Ended);yield return Ready();
        }
        static IEnumerator Fighting(BattleBootstrap boot)
        {
            double until=Time.realtimeSinceStartupAsDouble+8;
            while(!boot.Battle.Director.Fighting && Time.realtimeSinceStartupAsDouble<until)yield return null;
            Assert.IsTrue(boot.Battle.Director.Fighting);
        }
        static void Capture(string name)
        {
            MenuUiTests.Capture("Challenges/"+name);MenuUiTests.Capture("Challenges/"+name+"_880x450",880,450);
        }
        [UnityTest]public IEnumerator Cards_HoverMovesFrame_AllRowsHaveButtons_AndKeyboardAndClickWork()
        {
            GameFlow.ToChallenges();yield return Ready();
            PlayerPrefs.SetInt("MenuReducedMotion",0);
            var cards=Object.FindObjectsByType<ChallengeCardHover>(FindObjectsSortMode.None).OrderBy(c=>c.name).ToArray();
            Assert.AreEqual(6,cards.Length);
            foreach(var card in cards)
            {
                Assert.AreEqual(((RectTransform)cards[0].transform).rect.size,((RectTransform)card.transform).rect.size);
                int id=int.Parse(card.name.Substring("ChallengeCard".Length));
                Assert.AreEqual("挑戦する →",Button("ChallengeStart"+id).GetComponentInChildren<Text>().text);
                Assert.AreEqual(0,Button("ChallengeStart"+id).GetComponent<Image>().color.a,.001f);
            }
            var frame=GameObject.Find("ChallengeHoverFrame").GetComponent<RectTransform>();
            var alpha=frame.GetComponent<CanvasGroup>();Assert.AreEqual(0,alpha.alpha,.001f);
            var pointer=new PointerEventData(EventSystem.current);
            var hoverMethod=typeof(BaseInputModule).GetMethod("HandlePointerExitAndEnter",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var inputModule=EventSystem.current.currentInputModule;Assert.IsNotNull(inputModule);
            void MovePointer(GameObject targetObject)=>hoverMethod.Invoke(inputModule,new object[]{pointer,targetObject});
            MovePointer(Button("ChallengeSelect0").gameObject);
            yield return new WaitForSecondsRealtime(.06f);
            Assert.That(Button("ChallengeStart0").GetComponent<Image>().color.a,Is.InRange(.01f,.99f),"The button fades instead of snapping.");
            yield return new WaitForSecondsRealtime(.45f);Capture("12_hover_first");
            yield return new WaitForSecondsRealtime(.15f); // Let the synchronous screenshots' long frame elapse before measuring motion.
            MovePointer(Button("ChallengeStart0").gameObject);yield return null;
            Assert.Greater(alpha.alpha,.99f,"Moving inside the same card retains its highlight.");
            var first=frame.anchoredPosition;
            MovePointer(Button("ChallengeSelect4").gameObject);
            TimeController.SetPaused(true);yield return new WaitForSecondsRealtime(.04f);
            var target=cards[4].Position+new Vector2(-4,4);
            Assert.Greater(Vector2.Distance(first,frame.anchoredPosition),10);
            Assert.Greater(Vector2.Distance(target,frame.anchoredPosition),10,"The frame travels to the other card.");
            yield return new WaitForSecondsRealtime(.5f);Capture("13_hover_lower");
            Assert.Less(Vector2.Distance(target,frame.anchoredPosition),1);
            Assert.Less(Button("ChallengeStart0").GetComponent<Image>().color.a,.01f);
            Assert.Greater(Button("ChallengeStart4").GetComponent<Image>().color.a,.99f);
            MovePointer(null);
            yield return new WaitForSecondsRealtime(.45f);Assert.Less(alpha.alpha,.01f);
            PlayerPrefs.SetInt("MenuReducedMotion",1);
            EventSystem.current.SetSelectedGameObject(Button("ChallengeStart5").gameObject);yield return null;yield return null;
            Assert.Greater(alpha.alpha,.99f);Assert.AreEqual(RetroUi.Red,Button("ChallengeStart5").GetComponent<Image>().color);
            EventSystem.current.SetSelectedGameObject(null);yield return null;yield return null;Assert.AreEqual(0,alpha.alpha,.001f);
            Canvas.ForceUpdateCanvases();
            var button=Button("ChallengeStart5");
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Assert.IsNotEmpty(hits);Assert.AreEqual(button.gameObject,hits[0].gameObject,"The animated frame must not intercept clicks.");
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);yield return Ready();
            Assert.AreEqual(5,ChallengeRuntimeStore.SelectedId);Assert.IsTrue(Object.FindAnyObjectByType<ChallengeScreen>().DetailVisible);
        }
        [UnityTest]public IEnumerator FullJourney_ClearsRewardsRematchesAndPreservesNormalBuilds()
        {
            GameFlow.ToModeSelect();yield return Ready();Capture("01_menu");
            Button("ChallengeModeStart").onClick.Invoke();yield return Ready();Capture("02_list");
            Assert.AreEqual(6,Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Count(b=>b.name.StartsWith("ChallengeSelect")));
            Button("ChallengeSelect0").onClick.Invoke();yield return Ready();Capture("03_detail");
            Assert.IsTrue(Object.FindAnyObjectByType<ChallengeScreen>().DetailVisible);
            Button("ChallengePrepare").onClick.Invoke();yield return Ready();var screen=Object.FindAnyObjectByType<CustomizeScreen>();
            Assert.AreEqual("一",screen.Customizer.Current.character);Assert.IsFalse(Object.FindAnyObjectByType<InputField>().interactable);
            screen.Customizer.SelectSide(1);Assert.AreEqual(0,screen.Customizer.Side);
            Assert.IsFalse(screen.Customizer.SetCharacter("火"));screen.Customizer.SetFont(FontStyleId.Gothic);
            screen.Customizer.SetSize(WeaponSize.M);screen.Customizer.SetGrip(GripType.TwoHanded);screen.Customizer.SetStyle(BattleStyle.Counter);
            screen.PickGripAt(new Vector2(.18f,.5f));yield return Ready();screen.Preview.Paused=true;Capture("04_prepare");
            Assert.AreEqual(JsonUtility.ToJson(left),JsonUtility.ToJson(BuildRuntimeStore.Get(0)));
            Assert.AreEqual(JsonUtility.ToJson(right),JsonUtility.ToJson(BuildRuntimeStore.Get(1)));
            Button("Battle").onClick.Invoke();yield return Ready();var boot=Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.AreEqual("一",boot.Battle.Director.Config.left.grapheme);Assert.AreEqual("刀",boot.Battle.Director.Config.right.grapheme);
            yield return Fighting(boot);yield return new WaitForSecondsRealtime(3);TimeController.SetPaused(true);yield return Ready();Capture("05_battle");
            SettingsScreen.Open();yield return Ready();Assert.IsTrue(TimeController.Paused);SettingsScreen.Instance.Close();yield return Ready();
            TimeController.SetPaused(false);boot.Battle.Right.Runtime.hp=0;yield return End(boot);Capture("06_result");
            Assert.IsTrue(ChallengeRuntimeStore.Outcome.Cleared);Assert.IsTrue(ChallengeRuntimeStore.Outcome.NewReward);Assert.IsTrue(AchievementStore.Profile.Has(51));
            Button("ChallengeRetryBuild").onClick.Invoke();yield return Ready();screen=Object.FindAnyObjectByType<CustomizeScreen>();
            Assert.IsTrue(screen.Customizer.Current.customGrip);Assert.AreEqual(.18f,screen.Customizer.Current.gripPoint.x,.001f);
            Button("Battle").onClick.Invoke();yield return Ready();boot=Object.FindAnyObjectByType<BattleBootstrap>();yield return Fighting(boot);
            boot.Battle.Right.Runtime.hp=0;yield return End(boot);Assert.IsFalse(ChallengeRuntimeStore.Outcome.NewReward);
            Button("ChallengeRematch").onClick.Invoke();yield return Ready();Assert.IsNull(ChallengeRuntimeStore.Outcome);Assert.IsFalse(boot.Battle.Director.Ended);
            yield return Fighting(boot);boot.Battle.Right.Runtime.hp=0;yield return End(boot);
            Button("ChallengeNext").onClick.Invoke();yield return Ready();Assert.AreEqual(1,ChallengeRuntimeStore.SelectedId);
            Button("ChallengePrepare").onClick.Invoke();yield return Ready();screen=Object.FindAnyObjectByType<CustomizeScreen>();
            screen.Customizer.SetSize(WeaponSize.XL);Assert.AreEqual(WeaponSize.S,screen.Customizer.Current.weaponSize);
            Button("Back").onClick.Invoke();yield return Ready();Button("Back").onClick.Invoke();yield return Ready();
            Button("ChallengeFilter2").onClick.Invoke();yield return Ready();Assert.AreEqual(1,Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Count(b=>b.name.StartsWith("ChallengeSelect")));
            Capture("07_cleared_filter");
        }
        [UnityTest]public IEnumerator FirstClearToastAppearsOnResultPreservesOlderQueueAndDoesNotRepeat()
        {
            GameFlow.ToModeSelect();yield return Ready();
            AchievementStore.Change(p=>p.Set(2,1),true);yield return null;yield return null;
            var toast=AchievementToast.Instance;Assert.IsNotNull(toast);Assert.AreEqual(2,toast.CurrentId);
            GameFlow.ToChallengeDetail(0);yield return Ready();
            var toastCanvas=toast.GetComponentInChildren<Canvas>();Assert.IsFalse(toastCanvas.enabled);
            GameFlow.ToChallengeBattle();yield return Ready();
            var boot=Object.FindAnyObjectByType<BattleBootstrap>();yield return Fighting(boot);
            Assert.IsFalse(toastCanvas.enabled);boot.Battle.Right.Runtime.hp=0;yield return End(boot);
            Assert.IsTrue(ChallengeRuntimeStore.Outcome.NewReward);
            Assert.AreEqual(51,toast.CurrentId,"The first-clear reward must be visible on the result, ahead of older notifications.");
            Assert.IsTrue(toastCanvas.enabled);Assert.IsTrue(GameObject.Find("AchievementToast").activeInHierarchy);
            Assert.AreEqual(AchievementCatalog.Get(51).Name,toast.GetComponentsInChildren<Text>().Single(t=>t.name=="Name").text);
            Assert.IsTrue(AchievementStore.Profile.notifications.Contains(2),"Prioritizing the reward must retain the older notification.");
            Assert.AreEqual(1,AchievementStore.Profile.notifications.Count(id=>id==51));
            Capture("14_clear_reward_toast");
            AchievementStore.Reload();Assert.IsTrue(AchievementStore.Profile.notifications.Contains(51));
            TimeController.SetPaused(true);SettingsScreen.Open();yield return Ready();
            yield return new WaitForSecondsRealtime(AchievementToast.Duration+.2f);
            Assert.IsFalse(toastCanvas.enabled);Assert.AreEqual(51,toast.CurrentId,"Hidden notifications must not expire in settings.");
            SettingsScreen.Instance.Close();yield return null;yield return null;
            Assert.IsTrue(toastCanvas.enabled);Assert.AreEqual(51,toast.CurrentId);
            GameFlow.ToModeSelect();yield return null;yield return null;
            Assert.AreSame(toast,AchievementToast.Instance);Assert.AreEqual(51,toast.CurrentId);
            yield return new WaitForSecondsRealtime(AchievementToast.Duration+.2f);yield return null;
            Assert.IsFalse(AchievementStore.Profile.notifications.Contains(51));Assert.AreEqual(2,toast.CurrentId);
            GameFlow.ToChallengeDetail(0);yield return Ready();GameFlow.ToChallengeBattle();yield return Ready();
            boot=Object.FindAnyObjectByType<BattleBootstrap>();yield return Fighting(boot);
            boot.Battle.Right.Runtime.hp=0;yield return End(boot);
            Assert.IsTrue(ChallengeRuntimeStore.Outcome.Cleared);Assert.IsFalse(ChallengeRuntimeStore.Outcome.NewReward);
            Assert.IsFalse(AchievementStore.Profile.notifications.Contains(51));Assert.AreNotEqual(51,toast.CurrentId);
        }
        [UnityTest]public IEnumerator FailedGoalDisplaysRetryAndDoesNotGrantReward()
        {
            GameFlow.ToChallengeDetail(3);yield return Ready();Button("ChallengePrepare").onClick.Invoke();yield return Ready();
            Button("Battle").onClick.Invoke();yield return Ready();var boot=Object.FindAnyObjectByType<BattleBootstrap>();yield return Fighting(boot);
            boot.Battle.Right.Runtime.hp=0;yield return End(boot);Capture("08_goal_not_met");
            Assert.IsFalse(ChallengeRuntimeStore.Outcome.Cleared);Assert.IsFalse(AchievementStore.Profile.Has(54));Assert.IsFalse(Button("ChallengeNext").interactable);
            Button("ChallengeRetryBuild").onClick.Invoke();yield return Ready();Assert.AreEqual(3,ChallengeRuntimeStore.SelectedId);
        }
        [UnityTest]public IEnumerator ActualCheerAndSpecialFinishingHitSatisfyTheirOwnChallenges()
        {
            GameFlow.ToChallengeDetail(4);yield return Ready();Button("ChallengePrepare").onClick.Invoke();yield return Ready();
            Button("Battle").onClick.Invoke();yield return Ready();var boot=Object.FindAnyObjectByType<BattleBootstrap>();yield return Fighting(boot);
            boot.Battle.Context.Interventions.Press(0,InterventionKind.Cheer);
            Assert.AreEqual(1,boot.Battle.Context.Interventions.Side(0).cheers);
            boot.Battle.Right.Runtime.hp=0;yield return End(boot);Assert.IsTrue(ChallengeRuntimeStore.Outcome.Cleared);Assert.IsTrue(AchievementStore.Profile.Has(55));
            GameFlow.ToChallengeDetail(3);yield return Ready();Button("ChallengePrepare").onClick.Invoke();yield return Ready();
            Object.FindAnyObjectByType<CustomizeScreen>().Customizer.SetCharacter("一");
            Button("Battle").onClick.Invoke();yield return Ready();boot=Object.FindAnyObjectByType<BattleBootstrap>();yield return Fighting(boot);
            foreach(var f in boot.Battle.Director.Fighters)
            {
                f.SetTendency(new ClassTendency {reactionChance=0,proactiveGuard=0,guardBias=0,evadeBias=0,counterBias=0,retreatAfterAttack=0,attackWillingness=0});
                f.SetState(FighterState.Approach);f.Runtime.attackReadyAt=1000;f.Runtime.evadeReadyAt=1000;f.Runtime.parryReadyAt=1000;f.WalkSpeed=0;
            }
            boot.Battle.Right.Runtime.hp=1;
            var iv=boot.Battle.Context.Interventions;iv.Side(0).gauge=1;iv.Press(0,InterventionKind.Special);
            Assert.IsTrue(iv.Side(0).SpecialPending(boot.Battle.Context.SimTime));
            yield return End(boot);Assert.IsTrue(ChallengeRuntimeStore.Outcome.Cleared);Assert.IsTrue(AchievementStore.Profile.Has(54));
        }
        [UnityTest]public IEnumerator RewardCategoryShowsAllSixBadgesAndModeExitRestoresNormalCustomize()
        {
            AchievementStore.Change(p=>{foreach(var d in ChallengeCatalog.All)p.Set(d.RewardId,1);p.notifications.Clear();},true);
            GameFlow.ToAchievements();yield return Ready();var awards=Object.FindAnyObjectByType<AchievementsScreen>();awards.ChooseCategory(7);yield return Ready();
            Capture("09_rewards");Assert.AreEqual(6,Object.FindObjectsByType<ChallengeBadgeGraphic>(FindObjectsSortMode.None).Count(b=>b.name.StartsWith("Badge_"))-1);
            GameFlow.ToChallengeDetail(0);yield return Ready();GameFlow.ToCustomize();yield return Ready();
            Assert.IsFalse(GameFlow.ChallengeMode);Assert.AreEqual(left.character,Object.FindAnyObjectByType<CustomizeScreen>().Customizer.Current.character);
        }
        [UnityTest]public IEnumerator AllSixOpponentsBuildWithTheirLockedEquipmentAndInterventions()
        {
            foreach(var d in ChallengeCatalog.All)
            {
                GameFlow.ToChallengeDetail(d.Id);yield return Ready();GameFlow.ToChallengeBattle();yield return Ready();
                var boot=Object.FindAnyObjectByType<BattleBootstrap>();Assert.IsNotNull(boot.Battle,d.Name);
                Assert.AreEqual(d.OpponentGlyph,boot.Battle.Right.Loadout.grapheme);
                Assert.AreEqual(d.Opponent.weaponSize,boot.Battle.Right.Loadout.build.weaponSize);
                Assert.AreEqual(d.Opponent.battleStyle,boot.Battle.Right.Loadout.build.battleStyle);
                Assert.AreEqual(InterventionController.Com,boot.Battle.Context.Interventions.Side(1).controller);
                Assert.IsTrue(d.BuildAllowed(boot.Battle.Left.Loadout.build));
            }
        }
        [UnityTest]public IEnumerator DefeatAndDoubleKoAllowRetryWithoutGrantingReward()
        {
            for(int draw=0;draw<2;draw++)
            {
                GameFlow.ToChallengeDetail(0);yield return Ready();GameFlow.ToChallengeBattle();yield return Ready();
                var boot=Object.FindAnyObjectByType<BattleBootstrap>();yield return Fighting(boot);boot.Battle.Left.Runtime.hp=0;
                if(draw==1)boot.Battle.Right.Runtime.hp=0;
                yield return End(boot);Assert.IsFalse(ChallengeRuntimeStore.Outcome.Cleared);
                Assert.AreEqual(draw==1?-1:1,ChallengeRuntimeStore.Outcome.Match.winner);
                Assert.IsFalse(AchievementStore.Profile.Has(51));Assert.IsFalse(Button("ChallengeNext").interactable);
                Capture(draw==1?"11_draw":"10_defeat");
                Button("ChallengeRetryBuild").onClick.Invoke();yield return Ready();Assert.IsNotNull(Object.FindAnyObjectByType<CustomizeScreen>());
            }
        }
    }
}
