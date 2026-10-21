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
    public class MenuUiTests
    {
        FighterBuildData left, right, draft;
        int reduced;
        [SetUp]
        public void Setup()
        {
            left = BuildRuntimeStore.Get(0).Clone(); right = BuildRuntimeStore.Get(1).Clone();
            draft = TournamentRuntimeStore.Draft; reduced = PlayerPrefs.GetInt("MenuReducedMotion", 0);
            PlayerPrefs.SetInt("MenuReducedMotion", 0);
            TimeController.ResetAll();
        }
        [TearDown]
        public void Cleanup()
        {
            TimeController.ResetAll(); PlayerPrefs.SetInt("MenuReducedMotion", reduced);
            GameFlow.ToTitle(); BuildRuntimeStore.Set(0, left); BuildRuntimeStore.Set(1, right);
            TournamentRuntimeStore.SetDraft(draft); BuildRuntimeStore.Active = false;
        }
        static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
        static IEnumerator Ready()
        {
            yield return null; yield return null;
            yield return new WaitForSecondsRealtime(.85f);
        }
        [UnityTest]
        public IEnumerator Menus_Render_AndControlsPreserveBothBuilds()
        {
            GameFlow.ToTitle(); yield return Ready();
            Assert.IsNotNull(Resources.Load<Texture2D>("UI/MojiFighterXLogo"));
            Capture("01_title");
            Button("Start").onClick.Invoke(); yield return Ready();
            Capture("02_mode_select");
            Capture("02_mode_select_880x450", 880, 450);
            Button("BattleModeStart").onClick.Invoke(); yield return Ready();
            var screen = Object.FindAnyObjectByType<CustomizeScreen>();
            screen.Customizer.SetCharacter("木"); screen.Customizer.SetSize(WeaponSize.M);
            screen.Customizer.SetGrip(GripType.TwoHanded); screen.Customizer.SetStyle(BattleStyle.Aggressive);
            screen.Preview.Paused = true; yield return Ready(); screen.Preview.Paused = true;
            Capture("03_customize_p1"); Capture("03_customize_880x450", 880, 450);
            Capture("03_customize_1280x960", 1280, 960);
            Button("Side1").onClick.Invoke();
            screen.Customizer.SetCharacter("龍"); Button("Size_XL").onClick.Invoke();
            Button("Grip_Horizontal").onClick.Invoke(); Button("Style_Defensive").onClick.Invoke();
            Button("Flip_Vertical").onClick.Invoke(); yield return Ready();
            Assert.AreEqual(WeaponSize.XL, screen.Preview.Build.weaponSize);
            Assert.AreEqual(GripType.Horizontal, screen.Preview.Build.gripType);
            Assert.AreEqual(RetroUi.Blue, Button("Grip_Horizontal").GetComponent<Image>().color);
            Capture("04_customize_p2_xl");
            Assert.IsFalse(screen.Customizer.SetCharacter("ab"));
            Assert.IsFalse(Button("Battle").interactable);
            Assert.IsTrue(screen.Customizer.SetCharacter("龍"));
            Assert.IsTrue(Button("Battle").interactable);
            var field = Object.FindAnyObjectByType<InputField>();
            field.text = ""; Assert.IsFalse(Button("Battle").interactable);
            field.text = "龍"; Assert.IsTrue(Button("Battle").interactable);
            Button("PreviewGuides").onClick.Invoke(); Assert.IsTrue(screen.Preview.showGuides);
            Button("PreviewGuides").onClick.Invoke(); Assert.IsFalse(screen.Preview.showGuides);
            Button("Battle").onClick.Invoke(); yield return Ready();
            Assert.IsTrue(screen.StageVisible);Button("ConfirmStage").onClick.Invoke();yield return Ready();
            Assert.IsTrue(screen.VsVisible); Capture("05_vs");
            screen.HideVs(); Assert.IsFalse(screen.VsVisible);
            Button("Side0").onClick.Invoke(); Assert.AreEqual("木", screen.Preview.Build.character);
            GameFlow.ToTournamentCustomize(); yield return Ready();
            screen = Object.FindAnyObjectByType<CustomizeScreen>();
            screen.Customizer.SetCharacter("剣");
            screen.EnterTournament();
            yield return null; yield return null;
            var board = Object.FindAnyObjectByType<TournamentScreen>();
            double deadline = Time.realtimeSinceStartupAsDouble + 90;
            while (board.Busy && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsFalse(board.Busy); yield return Ready(); Capture("06_tournament");
        }
        [UnityTest]
        public IEnumerator ModeCards_FrameFollowsAllSixCardsResizesAndPreservesPointerAndKeyboardActions()
        {
            GameFlow.ToModeSelect();yield return Ready();
            var frame=GameObject.Find("ModeHoverFrame").GetComponent<RectTransform>();
            var alpha=frame.GetComponent<CanvasGroup>();Assert.AreEqual(0,alpha.alpha,.001f);
            Assert.AreEqual(8,Object.FindObjectsByType<ModeCardHover>(FindObjectsSortMode.None).Length);
            Capture("02_mode_select");Capture("02_mode_select_880x450",880,450);
            string[] names={"BattleMode","TournamentMode","ChallengeMode","DraftMode","RunMode","PracticeMode","FriendMode","AchievementsMode"};
            string[] actions={"BattleModeStart","TournamentModeStart","ChallengeModeStart","DraftModeStart","RunModeStart","PracticeModeStart","FriendModeStart","AchievementsStart"};
            var pointer=new PointerEventData(EventSystem.current);
            var hoverMethod=typeof(BaseInputModule).GetMethod("HandlePointerExitAndEnter",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var input=EventSystem.current.currentInputModule;Assert.IsNotNull(input);
            void MovePointer(GameObject target)=>hoverMethod.Invoke(input,new object[]{pointer,target});
            Time.timeScale=0;
            for(int i=0;i<names.Length;i++)
            {
                var card=GameObject.Find(names[i]).GetComponent<RectTransform>();var before=frame.anchoredPosition;
                MovePointer(Button(names[i]+"Select").gameObject);
                yield return new WaitForSecondsRealtime(.04f);
                if(i>0)
                {
                    Assert.Greater(Vector2.Distance(before,frame.anchoredPosition),1);
                    Assert.Greater(Vector2.Distance(card.anchoredPosition+new Vector2(-4,4),frame.anchoredPosition),1,"The frame animates between items.");
                }
                yield return new WaitForSecondsRealtime(.45f);
                Assert.Greater(alpha.alpha,.99f);
                Assert.Less(Vector2.Distance(card.anchoredPosition+new Vector2(-4,4),frame.anchoredPosition),1);
                Assert.Less(Vector2.Distance(card.rect.size+Vector2.one*8,frame.sizeDelta),1,"Lower cards need a shorter, wider frame.");
                MovePointer(Button(actions[i]).gameObject);yield return null;
                Assert.Greater(alpha.alpha,.99f,"Entering a nested action button must retain the card highlight.");
                if(i==1||i==4)
                {
                    string name=i==1?"mode_hover_tournament":"mode_hover_friends";
                    Capture(name);Capture(name+"_880x450",880,450);
                    yield return new WaitForSecondsRealtime(.15f);
                }
            }
            MovePointer(null);yield return new WaitForSecondsRealtime(.45f);Assert.Less(alpha.alpha,.01f);
            PlayerPrefs.SetInt("MenuReducedMotion",1);
            EventSystem.current.SetSelectedGameObject(Button("ChallengeModeStart").gameObject);yield return null;yield return null;
            var challenge=GameObject.Find("ChallengeMode").GetComponent<RectTransform>();
            Assert.AreEqual(challenge.anchoredPosition+new Vector2(-4,4),frame.anchoredPosition);
            Assert.AreEqual(challenge.rect.size+Vector2.one*8,frame.sizeDelta);Assert.AreEqual(1,alpha.alpha,.001f);
            ExecuteEvents.Execute(Button("ChallengeModeStart").gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Left},ExecuteEvents.moveHandler);
            yield return null;yield return null;
            Assert.AreEqual(Button("TournamentModeStart").gameObject,EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(GameObject.Find("TournamentMode").GetComponent<RectTransform>().anchoredPosition+new Vector2(-4,4),frame.anchoredPosition);
            EventSystem.current.SetSelectedGameObject(null);yield return null;yield return null;Assert.AreEqual(0,alpha.alpha,.001f);
            Canvas.ForceUpdateCanvases();
            var hit=Button("AchievementsModeSelect");
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,hit.transform.TransformPoint(((RectTransform)hit.transform).rect.center));
            var results=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,results);
            Assert.IsNotEmpty(results);Assert.AreEqual(hit.gameObject,results[0].gameObject,"The red frame must not intercept card clicks.");
            ExecuteEvents.Execute(hit.gameObject,pointer,ExecuteEvents.pointerClickHandler);yield return Ready();
            Assert.IsNotNull(Object.FindAnyObjectByType<AchievementsScreen>());
        }
        [UnityTest]
        public IEnumerator ButtonMotion_WorksWhilePaused_AndReducedMotionSettlesImmediately()
        {
            GameFlow.ToTitle(); yield return Ready();
            var button = Button("Start"); var rt = (RectTransform)button.transform;
            Vector2 home = rt.anchoredPosition;
            Time.timeScale = 0;
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.Greater(rt.anchoredPosition.y, home.y + 2);
            PlayerPrefs.SetInt("MenuReducedMotion", 1);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.Less(Vector2.Distance(home, rt.anchoredPosition), .2f);
            var group = RetroUi.Group(rt.parent, "ReducedMotionTest", 0, 0, 200, 100);
            RetroUi.Enter(group.gameObject, 1, 100);
            yield return null;
            Assert.AreEqual(1, group.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(Vector2.zero, group.anchoredPosition);
        }
        /// <summary>
        /// 技名 → AI 生成（ローカルのルームサーバーを SPECIAL_FAKE=1 で起動しておく）→ 登録・装備 → バトルで発動。
        /// </summary>
        [UnityTest]
        public IEnumerator SpecialMoves_NameRules_GenerateEquip_AndFireInBattle()
        {
            string saved = PlayerPrefs.GetString("MojiBattle.Specials.v1", "");
            FriendRoomSession.DevelopmentServerOverride = "http://127.0.0.1:8787";
            try
            {
                SpecialMoveLibrary.ClearAll();
                GameFlow.ToCustomize(); yield return Ready();
                var screen = Object.FindAnyObjectByType<CustomizeScreen>();
                screen.Customizer.SelectSide(0);
                screen.Customizer.SetCharacter("火"); screen.Customizer.SetSize(WeaponSize.M); screen.Customizer.SetSpecial(null);
                yield return Ready();
                StringAssert.Contains("火炎連斬", Button("SpecialMoves").GetComponentInChildren<Text>().text);
                Button("SpecialMoves").onClick.Invoke(); yield return Ready();
                var panel = screen.SpecialPanel;
                Assert.IsTrue(screen.SpecialVisible); Assert.IsFalse(screen.TextInputVisible, "武器の文字の入力欄は隠す");
                panel.NameInput.text = "水流斬";
                Assert.IsFalse(panel.GenerateButton.button.interactable, "選んだ文字が入っていない名前では作れない");
                panel.NameInput.text = "火火火火火火火火";
                Assert.IsFalse(panel.GenerateButton.button.interactable, "8 文字では作れない");
                panel.NameInput.text = "火炎昇龍波";
                Assert.IsTrue(panel.GenerateButton.button.interactable);
                panel.GenerateButton.button.onClick.Invoke();
                Assert.IsTrue(panel.Busy);
                double deadline = Time.realtimeSinceStartupAsDouble + 30;
                while (panel.Busy && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                StringAssert.Contains("登録して装備", panel.Status);
                var made = screen.Customizer.Current.EquippedSpecial;
                Assert.AreEqual("火炎昇龍波", made.name);
                CollectionAssert.AreEqual(new[] { AttackStyle.Launch, AttackStyle.StrokeShot }, made.Styles());
                Assert.AreEqual(SpecialElement.Fire, made.Element);
                Assert.AreEqual(1, SpecialMoveLibrary.For("火").Count);
                yield return Ready();
                Capture("08_special_moves");
                panel.Hide(); yield return Ready();
                StringAssert.Contains("火炎昇龍波", Button("SpecialMoves").GetComponentInChildren<Text>().text);

                GameFlow.ToBattle(); yield return Ready();
                var boot = Object.FindAnyObjectByType<BattleBootstrap>();
                var iv = boot.Battle.Context.Interventions;
                Assert.AreEqual("火炎昇龍波", iv.Side(0).plan.name);
                deadline = Time.realtimeSinceStartupAsDouble + 8;
                while (!boot.Battle.Director.Fighting && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                iv.Side(0).gauge = 1f; yield return null;
                Button("SpecialButton").onClick.Invoke();
                deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (!iv.InSpecial(0) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.IsTrue(iv.InSpecial(0), "装備した必殺技が出る");
                yield return new WaitForSecondsRealtime(.3f);
                Capture("09_battle_custom_special");
                // カットインの後: 技の最中・当たりの演出
                int specialHits = 0;
                boot.Battle.Context.Events.Hit += h => { if (h.attacker == 0 && iv.Side(0).OwnsAttack(h.attackId)) specialHits++; };
                deadline = Time.realtimeSinceStartupAsDouble + 8;
                while (specialHits == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.Greater(specialHits, 0, "必殺技は必ず当たる");
                yield return new WaitForSecondsRealtime(.05f);
                Capture("10_battle_special_hit");
            }
            finally
            {
                FriendRoomSession.DevelopmentServerOverride = null;
                SpecialMoveLibrary.ClearAll();
                if (!string.IsNullOrEmpty(saved)) PlayerPrefs.SetString("MojiBattle.Specials.v1", saved);
            }
        }
        [UnityTest]
        public IEnumerator ResultActions_RematchThenReturnKeepsLoadout()
        {
            GameFlow.ToCustomize(); yield return Ready();
            var screen = Object.FindAnyObjectByType<CustomizeScreen>();
            screen.Customizer.SetCharacter("火"); screen.Customizer.SetSize(WeaponSize.L);
            GameFlow.ToBattle(); yield return Ready();
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            int seed = boot.CurrentSeed;
            boot.Battle.Right.Runtime.hp = 0;
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!boot.Battle.Director.Ended && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(boot.Battle.Director.Ended); yield return Ready();
            Capture("07_result");
            Button("Again").onClick.Invoke(); yield return null;
            Assert.IsFalse(boot.Battle.Director.Ended);
            Assert.AreNotEqual(seed, boot.CurrentSeed);
            Assert.AreEqual("火", boot.Battle.Left.Loadout.grapheme);
            Assert.AreEqual(WeaponSize.L, boot.Battle.Left.Mods.size);
            boot.Battle.Right.Runtime.hp = 0;
            while (!boot.Battle.Director.Ended) yield return null;
            Button("ToCustomize").onClick.Invoke(); yield return Ready();
            Assert.AreEqual("火", Object.FindAnyObjectByType<CustomizeScreen>().Customizer.Get(0).character);
        }
        public static void Capture(string name, int width = 1920, int height = 1080)
        {
            var hud = Object.FindAnyObjectByType<BattleHudPresenter>();
            RenderTexture arenaTarget = null;
            var oldArenaTarget = Camera.main.targetTexture;
            Texture oldArenaTexture = hud != null ? hud.BattleImage.texture : null;
            if (hud != null)
            {
                float scale = Mathf.Min(width / 1920f, height / 1080f);
                arenaTarget = new RenderTexture(Mathf.RoundToInt(BattleHudPresenter.ArenaWidth * scale),
                    Mathf.RoundToInt(BattleHudPresenter.ArenaHeight * scale), 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
                arenaTarget.Create(); Camera.main.targetTexture = arenaTarget; hud.BattleImage.texture = arenaTarget;
            }
            foreach (var preview in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (preview.targetTexture != null && preview.enabled) preview.Render();
            var camera = new GameObject("MenuCaptureCamera").AddComponent<Camera>();
            camera.CopyFrom(Camera.main); camera.enabled = false; camera.cullingMask = ~(1 << CustomizeScreen.PreviewLayer);
            // The arena and customizer are already rendered into their own textures.
            // Clip world sprites from the UI capture to prevent a second unmasked fighter rendering.
            camera.farClipPlane = 2;
            camera.transform.SetPositionAndRotation(Camera.main.transform.position, Camera.main.transform.rotation);
            var target = new RenderTexture(width, height, 24);
            target.Create(); camera.targetTexture = target; camera.aspect = (float)width / height;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            float[] oldScales = canvases.Select(c => c.scaleFactor).ToArray();
            foreach (var canvas in canvases)
            {
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                canvas.scaleFactor = Mathf.Min(width / 1920f, height / 1080f);
            }
            foreach (var graphic in Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None)) graphic.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 point = camera.WorldToViewportPoint(corner);
                    Assert.That(point.x, Is.InRange(-.01f, 1.01f), button.name + " extends outside the viewport horizontally");
                    Assert.That(point.y, Is.InRange(-.01f, 1.01f), button.name + " extends outside the viewport vertically");
                }
            }
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            string directory = Path.Combine(Application.dataPath, "../Reports/UI");
            // name may include a subfolder (e.g. "Achievements/..."), so create the file's own folder.
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(directory, name + ".png")));
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
            RenderTexture.active = previous;
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = null;
                canvases[i].scaleFactor = oldScales[i]; canvases[i].GetComponent<CanvasScaler>().enabled = true;
            }
            camera.targetTexture = null; target.Release();
            Object.Destroy(target); Object.Destroy(texture); Object.Destroy(camera.gameObject);
            if (arenaTarget != null)
            {
                Camera.main.targetTexture = oldArenaTarget; hud.BattleImage.texture = oldArenaTexture;
                arenaTarget.Release(); Object.Destroy(arenaTarget);
            }
            Canvas.ForceUpdateCanvases();
        }
    }
}
