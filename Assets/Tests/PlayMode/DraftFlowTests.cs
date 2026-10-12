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
    public class DraftFlowTests
    {
        string draft,awards,settings;bool hadDraft,hadAwards,hadSettings;int motion;
        FighterBuildData left,right;
        [SetUp] public void Setup()
        {
            hadDraft=PlayerPrefs.HasKey(DraftRuntimeStore.PrefKey);draft=PlayerPrefs.GetString(DraftRuntimeStore.PrefKey);
            hadAwards=PlayerPrefs.HasKey(AchievementStore.PrefKey);awards=PlayerPrefs.GetString(AchievementStore.PrefKey);
            hadSettings=PlayerPrefs.HasKey(GameSettings.PrefKey);settings=PlayerPrefs.GetString(GameSettings.PrefKey);
            left=BuildRuntimeStore.Get(0).Clone();right=BuildRuntimeStore.Get(1).Clone();motion=PlayerPrefs.GetInt("MenuReducedMotion",0);
            PlayerPrefs.DeleteKey(DraftRuntimeStore.PrefKey);DraftRuntimeStore.ClearSession();AchievementStore.Reset();GameSettings.Reset();
            PlayerPrefs.SetInt("MenuReducedMotion",1);TimeController.ResetAll();Time.captureDeltaTime=0;
            Directory.CreateDirectory(Path.Combine(Application.dataPath,"../Reports/UI/Draft"));
        }
        [TearDown] public void Cleanup()
        {
            if(SettingsScreen.IsOpen)SettingsScreen.Instance.Close();GameFlow.ToTitle();SimHarness.End();
            BuildRuntimeStore.Set(0,left);BuildRuntimeStore.Set(1,right);BuildRuntimeStore.Active=false;
            Restore(DraftRuntimeStore.PrefKey,hadDraft,draft);Restore(AchievementStore.PrefKey,hadAwards,awards);Restore(GameSettings.PrefKey,hadSettings,settings);
            PlayerPrefs.SetInt("MenuReducedMotion",motion);PlayerPrefs.Save();AchievementStore.Reload();GameSettings.Reload();
        }
        static void Restore(string key,bool existed,string value){if(existed)PlayerPrefs.SetString(key,value);else PlayerPrefs.DeleteKey(key);}
        static Button Button(string name)=>Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name==name);
        static IEnumerator Ready(){yield return null;yield return null;yield return new WaitForSecondsRealtime(.75f);}
        static void Capture(string name)
        {
            MenuUiTests.Capture("Draft/"+name);MenuUiTests.Capture("Draft/"+name+"_880x450",880,450);MenuUiTests.Capture("Draft/"+name+"_1280x960",1280,960);
        }
        static IEnumerator Fighting(BattleBootstrap boot)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+8;
            while(!boot.Battle.Director.Fighting && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.IsTrue(boot.Battle.Director.Fighting);
        }
        static IEnumerator End(BattleBootstrap boot,int winner)
        {
            boot.Battle.Director.SilentSimulation=true;
            if(winner<0){boot.Battle.Left.Runtime.hp=0;boot.Battle.Right.Runtime.hp=0;}
            else boot.Battle.Director.Fighters[1-winner].Runtime.hp=0;
            double deadline=Time.realtimeSinceStartupAsDouble+8;
            while(!boot.Battle.Director.Ended && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.IsTrue(boot.Battle.Director.Ended);Assert.AreEqual(winner,boot.Battle.Director.Result.winner);yield return Ready();
        }
        static IEnumerator Choose()
        {
            var s=DraftRuntimeStore.Current;
            while(s.ComTurn)yield return null;
            var screen=Object.FindAnyObjectByType<DraftScreen>();string ch=s.pool.First(c=>c!=s.com?.character);
            var target=Button("DraftPick"+System.Array.IndexOf(s.pool,ch));
            ExecuteEvents.Execute(target.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.selectHandler);
            target.onClick.Invoke();Assert.AreEqual(ch,screen.Pending);Button("DraftConfirm").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.9f);Assert.AreEqual(DraftStage.Build,s.stage);
            Assert.AreNotEqual(s.player.character,s.com.character);Assert.IsTrue(Button("DraftConfirm").interactable);
        }
        [UnityTest] public IEnumerator ClickedCandidateStaysSelectedAcrossHoverAndFocusUntilAnotherActivation()
        {
            GameFlow.BeginDraft(31);yield return Ready();
            var screen=Object.FindAnyObjectByType<DraftScreen>();var s=DraftRuntimeStore.Current;
            var first=Button("DraftPick0");var second=Button("DraftPick1");
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(first.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
            EventSystem.current.SetSelectedGameObject(first.gameObject);
            Assert.IsNull(screen.Pending);Assert.IsFalse(Button("DraftConfirm").interactable);
            ExecuteEvents.Execute(first.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(s.pool[0],screen.Pending);
            for(int i=1;i<6;i++)
            {
                var other=Button("DraftPick"+i);
                ExecuteEvents.Execute(other.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
                EventSystem.current.SetSelectedGameObject(other.gameObject);
                ExecuteEvents.Execute(other.gameObject,pointer,ExecuteEvents.pointerExitHandler);
                Assert.AreEqual(s.pool[0],screen.Pending);
                Assert.IsTrue(first.transform.parent.Find("CandidateFocus").gameObject.activeSelf);
                Assert.IsFalse(other.transform.parent.Find("CandidateFocus").gameObject.activeSelf);
                Assert.AreEqual(s.pool[0]+"を選ぶ →",Button("DraftConfirm").GetComponentInChildren<Text>().text);
            }
            yield return Ready();Capture("15_click_locked");
            ExecuteEvents.Execute(second.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(s.pool[1],screen.Pending);Assert.IsFalse(first.transform.parent.Find("CandidateFocus").gameObject.activeSelf);
            Assert.IsTrue(second.transform.parent.Find("CandidateFocus").gameObject.activeSelf);
            // Keyboard focus alone does not replace the choice; submitting the focused card does.
            EventSystem.current.SetSelectedGameObject(first.gameObject);
            Assert.AreEqual(s.pool[1],screen.Pending);
            ExecuteEvents.Execute(first.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
            Assert.AreEqual(s.pool[0],screen.Pending);
            Button("DraftConfirm").onClick.Invoke();Assert.AreEqual(s.pool[0],s.player.character);
            ExecuteEvents.Execute(second.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(s.pool[0],screen.Pending);yield return new WaitForSecondsRealtime(.9f);
            Assert.AreEqual(DraftStage.Build,s.stage);Assert.AreNotEqual(s.player.character,s.com.character);
        }
        [UnityTest] public IEnumerator DraftGuideAndPicksLoopSuppliedMusicAcrossSettingsAndReturnToCustomizeMusic()
        {
            GameFlow.ToDraftIntro();yield return Ready();
            var audio=ScreenAudioDirector.Instance;var clip=Resources.Load<BattleMusicSettings>("Audio/BattleMusic").draftLoop;
            Assert.IsNotNull(clip);Assert.AreEqual("draft",clip.name);Assert.AreEqual(AudioClipLoadType.Streaming,clip.loadType);
            Assert.AreSame(clip,audio.MusicSource.clip);Assert.IsTrue(audio.MusicSource.loop);Assert.IsTrue(audio.MusicSource.isPlaying);
            int before=audio.MusicSource.timeSamples;yield return new WaitForSecondsRealtime(.2f);Assert.Greater(audio.MusicSource.timeSamples,before);
            Button("DraftBegin").onClick.Invoke();yield return Ready();
            Assert.AreSame(audio,ScreenAudioDirector.Instance);Assert.AreSame(clip,audio.MusicSource.clip);Assert.IsTrue(audio.MusicSource.isPlaying);
            Assert.AreEqual(1,Object.FindObjectsByType<ScreenAudioDirector>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            SettingsScreen.Open();yield return Ready();SettingsScreen.Instance.Close();yield return Ready();
            Assert.AreSame(clip,audio.MusicSource.clip);Assert.IsTrue(audio.MusicSource.isPlaying);
            Assert.AreEqual(audio.MusicVolume*GameSettings.MusicGain,audio.MusicSource.volume,.001f);
            yield return Choose();Button("DraftConfirm").onClick.Invoke();yield return Ready();
            Assert.AreEqual("custermize",audio.MusicSource.clip.name);
            Button("Back").onClick.Invoke();yield return Ready();Assert.AreSame(clip,audio.MusicSource.clip);
            GameFlow.ToModeSelect();yield return Ready();Assert.AreEqual("menu",audio.MusicSource.clip.name);
        }
        [UnityTest] public IEnumerator DraftRewardsNotifyOnResultsPauseAndShowAllEightStandaloneBadges()
        {
            GameFlow.ToModeSelect();yield return Ready();AchievementStore.Change(p=>p.Set(2,1),true);yield return null;yield return null;
            var toast=AchievementToast.Instance;Assert.AreEqual(2,toast.CurrentId);
            GameFlow.BeginDraft(303);yield return Ready();var toastCanvas=toast.GetComponentInChildren<Canvas>();Assert.IsFalse(toastCanvas.enabled);
            for(int round=1;round<=2;round++)
            {
                yield return Choose();Button("DraftConfirm").onClick.Invoke();yield return Ready();
                Button("Battle").onClick.Invoke();yield return Ready();Button("Start").onClick.Invoke();yield return Ready();
                var boot=Object.FindAnyObjectByType<BattleBootstrap>();yield return Fighting(boot);Assert.IsFalse(toastCanvas.enabled);
                yield return End(boot,0);Assert.IsTrue(toastCanvas.enabled);Assert.AreEqual(round==1?57:58,toast.CurrentId);
                Assert.IsTrue(AchievementStore.Profile.notifications.Contains(2));
                if(round==1){Button("DraftNext").onClick.Invoke();yield return Ready();Assert.IsFalse(toastCanvas.enabled);}
            }
            foreach(int id in new[]{57,58,59,61})Assert.IsTrue(AchievementStore.Profile.Has(id));
            Assert.AreEqual(1,AchievementStore.Profile.Value(64));Assert.AreEqual("ドラフト王者",toast.GetComponentsInChildren<Text>().Single(t=>t.name=="Name").text);
            Capture("16_draft_reward_toast");AchievementStore.Reload();Assert.AreEqual(1,AchievementStore.Profile.notifications.Count(id=>id==58));
            SettingsScreen.Open();yield return Ready();yield return new WaitForSecondsRealtime(AchievementToast.Duration+.2f);
            Assert.IsFalse(toastCanvas.enabled);Assert.AreEqual(58,toast.CurrentId);SettingsScreen.Instance.Close();yield return null;yield return null;
            Assert.IsTrue(toastCanvas.enabled);Assert.AreEqual(58,toast.CurrentId);
            GameFlow.ToAchievements();yield return Ready();var screen=Object.FindAnyObjectByType<AchievementsScreen>();screen.ChooseCategory(8);yield return Ready();
            Capture("17_draft_titles_progress");screen.ChangePage(1);yield return Ready();Assert.AreEqual(63,screen.SelectedId);Capture("18_draft_titles_locked");
            AchievementStore.Change(p=>{foreach(var d in AchievementCatalog.All.Where(d=>d.Category==8))p.Set(d.Id,d.Goal);p.notifications.Clear();},true);
            screen.ChooseCategory(8);yield return Ready();Capture("19_draft_badges_first");
            var grid=GameObject.Find("AchievementGrid");Assert.AreEqual(6,grid.GetComponentsInChildren<DraftBadgeGraphic>().Length);
            foreach(var badge in grid.GetComponentsInChildren<DraftBadgeGraphic>())
            {var mesh=badge.canvasRenderer.GetMesh();Assert.IsNotNull(mesh);Assert.Greater(mesh.vertexCount,20);}
            screen.ChangePage(1);yield return Ready();Assert.AreEqual(2,grid.GetComponentsInChildren<DraftBadgeGraphic>().Length);
            screen.Select(64);yield return Ready();Capture("20_draft_badges_second");
            Assert.AreEqual("選び手の極み",GameObject.Find("DetailName").GetComponent<Text>().text);
            Assert.AreEqual(77,AchievementCatalog.All.Length);
        }
        [UnityTest] public IEnumerator FullSeriesMenuPickBuildVsBattleResultsAndRestartPreserveSavedBuilds()
        {
            GameFlow.ToModeSelect();yield return Ready();Capture("01_menu");
            Button("DraftModeStart").onClick.Invoke();yield return Ready();Capture("02_intro");
            Button("DraftBegin").onClick.Invoke();yield return Ready();
            Assert.AreEqual(6,Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Count(b=>b.name.StartsWith("DraftPick")));
            yield return Choose();Capture("03_draft");Button("DraftConfirm").onClick.Invoke();yield return Ready();
            var screen=Object.FindAnyObjectByType<CustomizeScreen>();Assert.IsFalse(screen.TextInputVisible);
            var input=Object.FindAnyObjectByType<InputField>();Assert.AreEqual("CharInput",input.name);Assert.IsFalse(input.interactable);
            Assert.AreEqual(4,Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Count(b=>b.name.StartsWith("Font_")));
            Assert.IsNotNull(Button("Grip_Reverse"));Assert.IsNotNull(Button("Style_HitAndAway"));Assert.IsNotNull(Button("Flip_Vertical"));
            var settingsPanel=GameObject.Find("SettingsPanel").GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(816,-292),settingsPanel.anchoredPosition);Assert.AreEqual(new Vector2(1028,736),settingsPanel.sizeDelta);
            var com=JsonUtility.ToJson(screen.Customizer.Get(1));
            Button("Side1").onClick.Invoke();Assert.AreEqual(1,screen.Customizer.Side);
            Assert.IsFalse(Button("Size_XL").interactable);Assert.IsFalse(Button("SpecialMoves").interactable);Assert.IsFalse(Button("GripReset").interactable);
            screen.ShowSpecial();Assert.IsFalse(screen.SpecialVisible);
            // Invoking a disabled control directly must still leave the COM build unchanged.
            Button("Grip_Reverse").onClick.Invoke();Assert.AreEqual(com,JsonUtility.ToJson(screen.Customizer.Get(1)));
            screen.Preview.Paused=true;yield return Ready();Capture("14_shared_com");
            Button("Side0").onClick.Invoke();Assert.IsTrue(Button("Grip_Reverse").interactable);Assert.IsTrue(Button("Flip_Vertical").interactable);
            Button("Font_RoundedGothic").onClick.Invoke();Button("Grip_Reverse").onClick.Invoke();Button("Style_HitAndAway").onClick.Invoke();Button("Flip_Vertical").onClick.Invoke();
            Assert.AreEqual(FontStyleId.RoundedGothic,screen.Customizer.Current.fontType);Assert.AreEqual(GripType.Reverse,screen.Customizer.Current.gripType);
            Assert.AreEqual(BattleStyle.HitAndAway,screen.Customizer.Current.battleStyle);Assert.AreEqual(WeaponFlip.Vertical,screen.Customizer.Current.weaponFlip);
            string ch=screen.Customizer.Current.character;
            Button("Size_XL").onClick.Invoke();Button("Grip_TwoHanded").onClick.Invoke();Button("Style_Counter").onClick.Invoke();Button("Font_Mincho").onClick.Invoke();
            Assert.AreEqual(ch,screen.Customizer.Current.character);screen.Preview.Paused=true;yield return Ready();Capture("04_build_xl");
            screen.ShowSpecial();yield return Ready();Assert.IsTrue(screen.SpecialVisible);Assert.IsFalse(screen.TextInputVisible);screen.SpecialPanel.Hide();yield return Ready();
            Button("Battle").onClick.Invoke();yield return Ready();Assert.IsTrue(screen.VsVisible);Capture("05_vs");
            var ring=Object.FindAnyObjectByType<DraftInkRing>();Assert.IsNotNull(ring);
            var ringMesh=ring.canvasRenderer.GetMesh();Assert.IsNotNull(ringMesh);Assert.Greater(ringMesh.vertexCount,1000);
            Button("DraftVsBack").onClick.Invoke();Assert.IsFalse(screen.VsVisible);Button("Battle").onClick.Invoke();Button("Start").onClick.Invoke();yield return Ready();
            var boot=Object.FindAnyObjectByType<BattleBootstrap>();yield return Fighting(boot);
            Assert.IsNotNull(boot.Battle.Context.Interventions);Assert.IsNull(GameObject.Find("BattleAgain"));
            Assert.AreEqual(ch,boot.Battle.Director.Config.left.grapheme);yield return new WaitForSecondsRealtime(2);TimeController.SetPaused(true);yield return Ready();Capture("06_battle");TimeController.SetPaused(false);
            boot.RematchSameBuild();Assert.AreEqual(DraftStage.Battle,DraftRuntimeStore.Current.stage);
            yield return End(boot,0);Assert.AreEqual(1,DraftRuntimeStore.Current.playerWins);Capture("07_round_win");
            Assert.IsFalse(DraftRuntimeStore.Record(boot.Battle.Director.Result));
            for(int round=2;round<=3;round++)
            {
                Button("DraftNext").onClick.Invoke();yield return Ready();Assert.AreEqual(round,DraftRuntimeStore.Current.round);
                Assert.AreEqual(round%2==1,DraftRuntimeStore.Current.PlayerFirst);
                yield return Choose();if(round==2)Capture("08_com_first");Button("DraftConfirm").onClick.Invoke();yield return Ready();
                screen=Object.FindAnyObjectByType<CustomizeScreen>();Button("Battle").onClick.Invoke();yield return Ready();Button("Start").onClick.Invoke();yield return Ready();
                boot=Object.FindAnyObjectByType<BattleBootstrap>();yield return Fighting(boot);yield return End(boot,round==2?1:0);
                if(round==2)Capture("09_round_loss");
            }
            Assert.IsTrue(DraftRuntimeStore.Current.Finished);Assert.AreEqual(2,DraftRuntimeStore.Current.playerWins);Assert.AreEqual(1,DraftRuntimeStore.Current.comWins);
            Capture("10_final_win");Button("DraftNext").onClick.Invoke();yield return Ready();Assert.IsFalse(GameFlow.DraftMode);
            Assert.IsFalse(DraftRuntimeStore.CanResume);Button("DraftBegin").onClick.Invoke();yield return Ready();Assert.AreEqual(1,DraftRuntimeStore.Current.round);
            Assert.AreEqual(JsonUtility.ToJson(left),JsonUtility.ToJson(BuildRuntimeStore.Get(0)));Assert.AreEqual(JsonUtility.ToJson(right),JsonUtility.ToJson(BuildRuntimeStore.Get(1)));
        }
        [UnityTest] public IEnumerator SettingsPauseComPickerBackPreservesPoolAndResumeRestoresBuild()
        {
            GameFlow.BeginDraft(13);yield return Ready();var s=DraftRuntimeStore.Current;
            var picker=Object.FindAnyObjectByType<DraftScreen>();picker.PreviewCandidate(s.pool[0]);Button("DraftConfirm").onClick.Invoke();
            SettingsScreen.Open();yield return new WaitForSecondsRealtime(1.2f);Assert.IsNull(s.com);SettingsScreen.Instance.Close();
            yield return new WaitForSecondsRealtime(.9f);Assert.IsNotNull(s.com);
            string pool=string.Join("",s.pool),ch=s.player.character;
            Button("DraftConfirm").onClick.Invoke();yield return Ready();
            var screen=Object.FindAnyObjectByType<CustomizeScreen>();screen.Customizer.SetSize(WeaponSize.L);
            Button("Back").onClick.Invoke();yield return Ready();Assert.AreEqual(pool,string.Join("",s.pool));
            Assert.IsFalse(Button("DraftPick0").interactable);Button("DraftConfirm").onClick.Invoke();yield return Ready();
            GameFlow.ToModeSelect();yield return Ready();GameFlow.ToDraftIntro();yield return Ready();Button("DraftResumeOrMenu").onClick.Invoke();yield return Ready();
            Button("DraftConfirm").onClick.Invoke();yield return Ready();screen=Object.FindAnyObjectByType<CustomizeScreen>();Assert.AreEqual(ch,screen.Customizer.Current.character);Assert.AreEqual(WeaponSize.L,screen.Customizer.Current.weaponSize);
            Button("Battle").onClick.Invoke();yield return Ready();Button("Start").onClick.Invoke();yield return Ready();
            var boot=Object.FindAnyObjectByType<BattleBootstrap>();yield return Fighting(boot);Button("BattleBack").onClick.Invoke();yield return Ready();
            Assert.AreEqual(DraftStage.Build,DraftRuntimeStore.Current.stage);Assert.IsNotNull(Object.FindAnyObjectByType<CustomizeScreen>());
        }
        [UnityTest] public IEnumerator DrawReplaysAndComFinalVictoryUsesCorrectResultAndMenuExit()
        {
            GameFlow.BeginDraft(14);yield return Ready();yield return Choose();Button("DraftConfirm").onClick.Invoke();yield return Ready();
            var ch=DraftRuntimeStore.Current.player.character;
            Button("Battle").onClick.Invoke();yield return Ready();Button("Start").onClick.Invoke();yield return Ready();
            var boot=Object.FindAnyObjectByType<BattleBootstrap>();yield return Fighting(boot);yield return End(boot,-1);Capture("11_draw");
            Button("DraftNext").onClick.Invoke();yield return Ready();Assert.AreEqual(ch,Object.FindAnyObjectByType<CustomizeScreen>().Customizer.Current.character);Assert.AreEqual(1,DraftRuntimeStore.Current.round);
            for(int round=1;round<=2;round++)
            {
                Button("Battle").onClick.Invoke();yield return Ready();Button("Start").onClick.Invoke();yield return Ready();
                boot=Object.FindAnyObjectByType<BattleBootstrap>();yield return Fighting(boot);yield return End(boot,1);
                if(round==1){Button("DraftNext").onClick.Invoke();yield return Ready();yield return Choose();Button("DraftConfirm").onClick.Invoke();yield return Ready();}
            }
            Assert.IsTrue(DraftRuntimeStore.Current.Finished);Assert.AreEqual(2,DraftRuntimeStore.Current.comWins);Capture("12_final_loss");
            Button("DraftResultMenu").onClick.Invoke();yield return Ready();Assert.IsFalse(GameFlow.DraftMode);Assert.IsNotNull(Object.FindAnyObjectByType<ModeSelectScreen>());
        }
        [UnityTest] public IEnumerator PreviewContainsWholeWeaponAcrossFontsSizesAndOffCenterGrips()
        {
            GameFlow.BeginDraft(20);yield return Ready();yield return Choose();Button("DraftConfirm").onClick.Invoke();yield return Ready();
            var screen=Object.FindAnyObjectByType<CustomizeScreen>();screen.Preview.Paused=true;
            var camera=GameObject.Find("PreviewCamera").GetComponent<Camera>();
            foreach(var font in new[]{FontStyleId.Mincho,FontStyleId.Gothic,FontStyleId.Brush,FontStyleId.RoundedGothic})
            foreach(WeaponSize size in System.Enum.GetValues(typeof(WeaponSize)))
            foreach(var grip in new[]{new Vector2(.1f,.1f),new Vector2(.5f,.5f),new Vector2(.9f,.9f)})
            {
                screen.Customizer.SetFont(font);screen.Customizer.SetSize(size);screen.Customizer.SetGripPoint(grip);yield return null;
                var weapon=screen.Preview.transform.Find("WeaponSprite").GetComponent<SpriteRenderer>();
                var bounds=weapon.bounds;
                foreach(float x in new[]{bounds.min.x,bounds.max.x})foreach(float y in new[]{bounds.min.y,bounds.max.y})
                {
                    Vector3 p=camera.WorldToViewportPoint(new Vector3(x,y,0));
                    Assert.That(p.x,Is.InRange(0f,1f),font+"/"+size+"/"+grip);
                    Assert.That(p.y,Is.InRange(0f,1f),font+"/"+size+"/"+grip);
                }
            }
            Capture("13_preview_extreme_grip");
        }
    }
}
