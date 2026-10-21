using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>Printed spectator UI with a large, clipped camera view.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class BattleHudPresenter : MonoBehaviour
    {
        public const float ArenaX = 42, ArenaWidth = 1836;
        public static float ArenaY => GameFlow.WorldCupMode || GameFlow.RunMode || GameFlow.ChallengeMode || GameFlow.DraftMode ? 245 : 185;
        public static float ArenaHeight => GameFlow.WorldCupMode || GameFlow.RunMode || GameFlow.ChallengeMode || GameFlow.DraftMode ? 726 : 786;
        public static float GroundViewport => 38f / ArenaHeight;
        public bool CompactStageHud => !GameFlow.PracticeMode;
        MatchDirector director;
        CameraRig rig;
        RectTransform root, arena;
        RawImage battleImage;
        RenderTexture battleTexture;
        Camera displayCamera;
        Image groundLine, groundWash;
        Text stageName,stageNotice;
        readonly Text[] names = new Text[2], hp = new Text[2];
        readonly RectTransform[] fill = new RectTransform[2], trail = new RectTransform[2];
        readonly float[] trailValue = { 1, 1 };
        readonly UguiKit.ChoiceButton[] speeds = new UguiKit.ChoiceButton[4];
        Text timer, step, status, center, sub, technique, techniqueEnglish;
        Image techniqueStripe;
        GameObject details, announcement, techniqueTag;
        UguiKit.ChoiceButton pause, again, detailButton, guideButton, practiceEasier, practiceHarder;
        Text practiceLevel;
        PlayerController player;
        bool practice;
        Vector2 techniquePoint;
        float techniqueLife;
        bool isTournament;
        public RawImage BattleImage => battleImage;
        bool HasLocalInterventions => director.Context.Interventions!=null &&
            (director.Context.Interventions.Side(0).controller==InterventionController.Local || director.Context.Interventions.Side(1).controller==InterventionController.Local);
        public RectTransform Arena => arena;
        public Camera DisplayCamera => displayCamera;
        public bool DetailsVisible => details != null && details.activeSelf;

        public void Bind(MatchDirector d, CameraRig cameraRig)
        {
            director = d; rig = cameraRig;
            root = RetroUi.Screen("BattleCanvas", out var canvas, 2);
            canvas.transform.SetParent(transform, false);
            // Keep an actual camera rendering Display 1 as well as the offscreen battle camera.
            // Overlay canvases alone do not suppress the Game view's "No cameras rendering" message.
            var display = new GameObject("BattleDisplayCamera");
            display.transform.SetParent(transform, false);
            displayCamera = display.AddComponent<Camera>();
            displayCamera.orthographic = true;
            displayCamera.cullingMask = 0;
            displayCamera.clearFlags = CameraClearFlags.SolidColor;
            displayCamera.backgroundColor = RetroUi.Paper;
            displayCamera.depth = -100;
            displayCamera.targetDisplay = rig.Cam.targetDisplay;
            RetroUi.Button(root, "BattleBack", "← 戻る", 72, 35, 138, 30, 22, Back, RetroUi.Ink, false);
            RetroUi.Block(root, "HeaderRule", 230, 51, 590, 1, RetroUi.Ink);
            step = RetroUi.Label(root, "BattleStep", "03 / BATTLE", 832, 31, 280, 42, 26, null, TextAnchor.MiddleCenter);
            RetroUi.Block(root, "HeaderRule", 1130, 51, 550, 1, RetroUi.Ink);
            RetroUi.Button(root, "BattleSettings", "⚙ 設定", 1480, 29, 170, 42, 24, SettingsScreen.Open, shadow: false);
            RetroUi.Label(root, "Brand", "MOJI Fighter X", 1684, 32, 160, 40, 20, null, TextAnchor.MiddleRight);
            RetroUi.Block(root, "HeaderBottom", 42, 81, 1836, 1, RetroUi.Ink);
            for (int i = 0; i < 2; i++) BuildPlayer(i);
            float offset=GameFlow.WorldCupMode || GameFlow.RunMode || GameFlow.ChallengeMode || GameFlow.DraftMode?60:0;
            RetroUi.Block(root, "TimerRule", 808, 102+offset, 1, 66, RetroUi.Rule);
            RetroUi.Block(root, "TimerRule", 1112, 102+offset, 1, 66, RetroUi.Rule);
            RetroUi.Label(root, "TimeCaption", "TIME", 885, 86+offset, 152, 25, 21, null, TextAnchor.MiddleCenter);
            timer = RetroUi.Label(root, "BattleTimer", "60", 883, 103+offset, 152, 79, 74, null, TextAnchor.MiddleCenter);
            RetroUi.Frame(root, 1048, 128+offset, 60, 32, RetroUi.Red, 1);
            RetroUi.Label(root, "Automatic", "AUTO", 1048, 128+offset, 60, 32, 20, RetroUi.Red, TextAnchor.MiddleCenter);
            arena = RetroUi.Group(root, "BattleArena", ArenaX, ArenaY, ArenaWidth, ArenaHeight);
            arena.gameObject.AddComponent<RectMask2D>();
            var paper = RetroUi.Group(arena, "ArenaPaper", 0, 0, ArenaWidth, ArenaHeight).gameObject.AddComponent<RawImage>();
            paper.texture = StageCatalog.Get(d.Config.stage).Texture; paper.raycastTarget = false;
            groundWash = RetroUi.Block(arena, "GroundWash", 0, ArenaHeight - 38, ArenaWidth, 38, new Color(.85f, .81f, .70f, .23f));
            groundLine = RetroUi.Block(arena, "GroundLine", 0, ArenaHeight - 40, ArenaWidth, 5, RetroUi.Ink);
            battleImage = RetroUi.Group(arena, "BattleView", 0, 0, ArenaWidth, ArenaHeight).gameObject.AddComponent<RawImage>();
            battleImage.raycastTarget = false;
            // Only hide the stage's renderers. Colliders and simulation remain unchanged.
            foreach (var sr in transform.Find("Arena").GetComponentsInChildren<SpriteRenderer>()) sr.enabled = false;
            if(d.Config.stage!=BattleStageId.Dojo){groundLine.enabled=false;groundWash.enabled=false;}
            RetroUi.Block(arena,"StageNamePaper",580,8,676,48,new Color(.96f,.93f,.86f,.92f));
            stageName=StageUi.Text(arena,"BattleStageName",StageCatalog.Get(d.Config.stage).Name,580,8,676,48,31,null,TextAnchor.MiddleCenter);
            stageNotice=StageUi.Text(arena,"StageNotice","",480,65,876,46,25,RetroUi.Red,TextAnchor.MiddleCenter);
            ResizeTarget();
            foreach (var f in d.Fighters) f.View.PrintedCallouts = true;
            BuildTechniqueTag(); BuildAnnouncement(); BuildControls(); BuildDetails();
            d.Events.Motion += OnMotion; d.Events.Hit += OnHit; d.Events.Guard += OnGuard;
            RetroUi.Enter(root.gameObject, 0, 0);
        }

        public void SetRunInfo()
        {
            var s=RunRuntimeStore.Current;step.text="RUN / BATTLE";
            RetroUi.Label(root,"RunBattleRound",$"第{s.round}戦 / 全5戦",740,89,440,54,31,null,TextAnchor.MiddleCenter);
            names[0].text="あなた / "+s.player.character;names[1].text=s.Opponent.Name;
            again.image.gameObject.SetActive(false);root.Find("BattleAgainShadow")?.gameObject.SetActive(false);
            RetroUi.Label(root,"RunBattlePerks",RunUi.Perks(s),70,930,1100,38,23,RetroUi.Muted);
        }
        public void SetWorldCupInfo(WorldCupSession s,WorldCupMatch m)
        {
            step.text="WORLD MOJI CUP / BATTLE";step.rectTransform.anchoredPosition=new Vector2(650,-31);step.rectTransform.sizeDelta=new Vector2(600,42);
            RetroUi.Label(root,"WorldCupBattleRound",m.Name,520,89,880,54,31,null,TextAnchor.MiddleCenter);
            names[0].text=(m.LeftId==0?"YOU / ":"COM / ")+s.Entrants[m.LeftId].Build.character;
            names[1].text=(m.RightId==0?"YOU / ":"COM / ")+s.Entrants[m.RightId].Build.character;
            again.image.gameObject.SetActive(false);root.Find("BattleAgainShadow")?.gameObject.SetActive(false);
        }
        void BuildPlayer(int side)
        {
            var f = director.Fighters[side]; var team = RetroUi.Team(side);
            float offset=GameFlow.WorldCupMode || GameFlow.RunMode || GameFlow.ChallengeMode || GameFlow.DraftMode?60:0;
            float tileX = side == 0 ? 72 : 1756, textX = side == 0 ? 201 : 1245;
            RetroUi.Block(root, "PlayerTile" + side, tileX, 93+offset, 92, 83, team);
            var glyph = RetroUi.Label(root, "PlayerGlyph" + side, f.Loadout.grapheme, tileX, 92+offset, 92, 84, 66, RetroUi.Paper, TextAnchor.MiddleCenter);
            glyph.font = GlyphFontLibrary.Default.Get(f.Loadout.font).font;
            FusionUi.Glyph(glyph, f.Loadout.build);
            string style = f.Mods.customized ? CustomizeLabels.Style(f.Mods.style) : "オート";
            names[side] = RetroUi.Label(root, "PlayerName" + side, $"PLAYER 0{side + 1}  {FusionUi.Name(f.Loadout)} / {style}", textX, 94+offset, 474, 32, 22, null,
                side == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            float barX = side == 0 ? 201 : 1294;
            RetroUi.Frame(root, barX - 2, 133+offset, 429, 29, RetroUi.Ink, 2);
            trail[side] = RetroUi.Block(root, "HpTrail" + side, barX, 135+offset, 425, 25, RetroUi.Wash).rectTransform;
            fill[side] = RetroUi.Block(root, "HpFill" + side, barX, 135+offset, 425, 25, team).rectTransform;
            if (side == 1)
                foreach (var rect in new[] { trail[side], fill[side] })
                { rect.pivot = new Vector2(1, 1); rect.anchoredPosition = new Vector2(barX + 425, -135-offset); }
            hp[side] = RetroUi.Label(root, "HpValue" + side, "", side == 0 ? 644 : 1136, 126+offset, 140, 40, 24, null,
                side == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
        }

        void ResizeTarget()
        {
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            int h = Mathf.Clamp(Mathf.RoundToInt(ArenaHeight * scale), 256, 1440);
            int w = Mathf.RoundToInt(h * ArenaWidth / ArenaHeight);
            if (battleTexture != null && battleTexture.width == w && battleTexture.height == h) return;
            if (battleTexture != null) { battleTexture.Release(); Destroy(battleTexture); }
            battleTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "BattleArenaTexture", antiAliasing = 2 };
            battleTexture.Create(); battleImage.texture = battleTexture;
            rig.Cam.targetTexture = battleTexture;
            rig.Cam.aspect = ArenaWidth / ArenaHeight;
            rig.Cam.backgroundColor = Color.clear;
            rig.Cam.cullingMask = ~(1 << CustomizeScreen.PreviewLayer);
        }

        void BuildControls()
        {
            RetroUi.Block(root, "ControlsRule", 42, 971, 1836, 1, RetroUi.Ink);
            var dot = RetroUi.Block(root, "LiveDot", 98, 994, 25, 25, RetroUi.Red);
            dot.sprite = UiKit.DiscSprite;
            UguiKit.AddOutline(dot.gameObject, 1, RetroUi.Ink);
            status = RetroUi.Label(root, "BattleStatus", "観戦中", 140, 982, 385, 48, 32);
            pause = RetroUi.Button(root, "BattlePause", "Ⅱ  一時停止", 718, 983, 211, 46, 24, () => TimeController.SetPaused(!TimeController.Paused), shadow: false);
            for (int i = 0; i < speeds.Length; i++)
            {
                float value = i == 0 ? .5f : i == 1 ? 1 : i == 2 ? 2 : 4;
                speeds[i] = RetroUi.Button(root, "BattleSpeed" + i, value.ToString("0.#") + "×", 958 + 102 * i, 983, 102, 46, 24,
                    () => TimeController.SetSpectatorSpeed(value), shadow: false);
            }
            detailButton = RetroUi.Button(root, "BattleDetails", "詳細", 1427, 983, 151, 46, 24, ToggleDetails, shadow: false);
            again = RetroUi.Button(root, "BattleAgain", "もう一度 →", 1604, 983, 240, 40, 24, () => FindFirstObjectByType<BattleBootstrap>()?.RematchSameBuild(), RetroUi.Red);
            if(CompactStageHud)
            {
                status.rectTransform.anchoredPosition=new Vector2(110,-982);status.rectTransform.sizeDelta=new Vector2(165,48);status.fontSize=24;
                dot.rectTransform.anchoredPosition=new Vector2(73,-997);dot.rectTransform.sizeDelta=new Vector2(20,20);
                void Position(UguiKit.ChoiceButton b,float x,float w){b.image.rectTransform.anchoredPosition=new Vector2(x,-983);b.image.rectTransform.sizeDelta=new Vector2(w,46);b.image.GetComponent<RetroButtonMotion>()?.Rebase();}
                Position(pause,280,180);
                for(int i=0;i<speeds.Length;i++)Position(speeds[i],480+i*85,85);
                Position(detailButton,840,130);Position(again,990,185);again.label.fontSize=22;
                root.Find("BattleAgainShadow")?.gameObject.SetActive(false);
            }
        }

        void BuildDetails()
        {
            var panel = RetroUi.Group(root, "BattleDetailsPanel", 1000, 425, 840, 528); details = panel.gameObject;
            RetroUi.Block(panel, "PanelShadow", 8, 8, 840, 528, RetroUi.Ink);
            RetroUi.Block(panel, "PanelPaper", 0, 0, 840, 528, RetroUi.Paper);
            RetroUi.Frame(panel, 0, 0, 840, 528, RetroUi.Ink);
            RetroUi.Label(panel, "DetailsTitle", "対戦の詳細", 32, 15, 580, 60, 34);
            RetroUi.Button(panel, "BattleDetailsClose", "閉じる ×", 658, 24, 151, 42, 22, ToggleDetails, shadow: false);
            for (int i = 0; i < 2; i++)
            {
                var f = director.Fighters[i]; var s = f.Stats; float x = 32 + i * 390;
                RetroUi.Label(panel, "DetailPlayer" + i, $"PLAYER 0{i + 1}「{FusionUi.Name(f.Loadout)}」", x, 83, 360, 44, 28, RetroUi.Team(i));
                string build = f.Mods.customized ? $"{f.Mods.size} / {CustomizeLabels.Grip(f.Mods.grip)}\n{f.Mods.gripLabel} / {CustomizeLabels.Style(f.Mods.style)}" : "既定の構え";
                RetroUi.Label(panel, "DetailStats" + i, $"{GlyphCatalog.FontDisplayName(f.Loadout.font)}\n攻撃 {s.attack}    防御 {s.defense}\n速度 {s.speed}    耐久 {s.durability}\n重量 {new string('★', s.WeightStars)}\n{build}", x, 133, 365, 228, 24);
            }
            RetroUi.Block(panel, "DetailsRule", 32, 374, 776, 1, RetroUi.Rule);
            RetroUi.Label(panel, "BattleSeed", $"SEED {director.Config.seed}   /   SPACE : 一時停止\n1・2・3 : 速度    C : 当たり判定    D : AI表示\nK : 必殺技    L : 声援・踏ん張り（声援ゲージを消費）", 32, 390, 770, 98, 20, RetroUi.Muted);
            details.SetActive(false);
        }

        public void ToggleDetails()
        {
            details.SetActive(!details.activeSelf);
            if (details.activeSelf) RetroUi.Enter(details, 0, 12);
            RetroUi.Selected(detailButton, details.activeSelf, RetroUi.Ink);
        }

        void BuildAnnouncement()
        {
            var panel = RetroUi.Group(root, "BattleAnnouncement", 430, 400, 1060, 250); announcement = panel.gameObject;
            RetroUi.Block(panel, "AnnouncementPaper", 0, 0, 1060, 250, new Color(RetroUi.Paper.r, RetroUi.Paper.g, RetroUi.Paper.b, .95f));
            RetroUi.Frame(panel, 0, 0, 1060, 250, RetroUi.Ink, 2);
            center = RetroUi.Label(panel, "BattleAnnouncementTitle", "", 24, 20, 1012, 130, 84, null, TextAnchor.MiddleCenter);
            sub = RetroUi.Label(panel, "BattleAnnouncementSubtitle", "", 32, 155, 996, 75, 26, null, TextAnchor.MiddleCenter);
            announcement.SetActive(false);
        }

        void BuildTechniqueTag()
        {
            var tag = RetroUi.Group(arena, "PrintedTechniqueTag", 0, 0, 240, 100); techniqueTag = tag.gameObject;
            RetroUi.Block(tag, "TagPaper", 0, 0, 240, 100, RetroUi.Paper);
            RetroUi.Frame(tag, 0, 0, 240, 100, RetroUi.Ink, 1);
            techniqueStripe = RetroUi.Block(tag, "TagStripe", 0, 0, 7, 100, RetroUi.Red);
            technique = RetroUi.Label(tag, "TechniqueName", "突き", 12, 5, 216, 58, 34, RetroUi.Red, TextAnchor.MiddleCenter);
            technique.resizeTextForBestFit = true; technique.resizeTextMinSize = 22; technique.resizeTextMaxSize = 34;
            techniqueEnglish = RetroUi.Label(tag, "TechniqueEnglish", "THRUST", 12, 62, 216, 26, 18, null, TextAnchor.MiddleCenter);
            techniqueEnglish.resizeTextForBestFit = true; techniqueEnglish.resizeTextMinSize = 12; techniqueEnglish.resizeTextMaxSize = 18;
            tag.localRotation = Quaternion.Euler(0, 0, 8); techniqueTag.SetActive(false);
        }

        public void ShowTechnique(string japanese, string english, int fighter, Vector2 point)
        {
            if (!isActiveAndEnabled || !director.Fighters[fighter].View.isActiveAndEnabled) return;
            technique.text = japanese.TrimEnd('！'); techniqueEnglish.text = english;
            technique.color = techniqueStripe.color = RetroUi.Team(fighter);
            techniquePoint = point; techniqueLife = .65f;
            techniqueTag.SetActive(true); RetroUi.Enter(techniqueTag, 0, 12);
        }
        void OnMotion(FighterMotionEvent e)
        {
            string label = AttackTechniques.Callout(e.kind);
            string english = e.kind == FighterMotionKind.GroundWave ? "GROUND WAVE"
                : e.kind == FighterMotionKind.StrokeShot ? "STROKE SHOT"
                : e.kind == FighterMotionKind.AerialChase ? "AIR CHASE"
                : e.kind == FighterMotionKind.AerialSlash ? "AIR SLASH"
                : e.kind == FighterMotionKind.AerialSmash ? "AIR FINISH"
                : e.kind == FighterMotionKind.BindCounter ? "BIND COUNTER" : e.kind.ToString().ToUpperInvariant();
            if (label.Length > 0) ShowTechnique(label, english, e.fighter, director.Fighters[e.fighter].HeadWorld);
        }
        void OnHit(HitEvent e)
        {
            fill[e.defender]?.GetComponent<PaperFaceEffect>()?.Impact();
            root.Find("PlayerTile" + e.defender)?.GetComponent<PaperFaceEffect>()?.Impact();
            if (e.style == AttackStyle.Thrust) ShowTechnique("突き", "THRUST", e.attacker, e.point);
            else if (e.style == AttackStyle.Bash) ShowTechnique("盾当て", "BASH", e.attacker, e.point);
        }
        void OnGuard(GuardEvent e)
        {
            if (e.parried) ShowTechnique("受け流し", "PARRY", e.defender, e.point);
        }

        public void SetTournamentInfo(TournamentSession session, TournamentMatch match)
        {
            isTournament = true; step.text = TournamentSession.RoundName(match.Round) + $"  /  {session.Wins}/3勝";
            names[0].text = $"YOU  {FusionUi.Name(director.Config.left)}";
            names[1].text = session.Entrants[match.RightId].Name + $"  {FusionUi.Name(director.Config.right)}";
            again.button.interactable = false; again.label.text = "大会対戦中";
        }
        public void SetFriendInfo(int side)
        {
            step.text = "FRIEND BATTLE";
            for (int i = 0; i < 2; i++) names[i].text = (i == side ? "YOU " : "FRIEND ") + FusionUi.Name(director.Fighters[i].Loadout);
            pause.button.interactable = false; pause.label.text = "オンライン対戦";
            foreach (var speed in speeds) speed.button.interactable = false;
            again.button.interactable = false; again.label.text = "ルーム対戦中";
        }
        public void SetChallengeInfo()
        {
            var d=ChallengeRuntimeStore.Current;
            step.text="05 / CHALLENGE / BATTLE";
            step.rectTransform.anchoredPosition=new Vector2(640,-31);step.rectTransform.sizeDelta=new Vector2(640,42);
            foreach(Transform child in root)if(child.name=="HeaderRule")
            {
                var rect=child.GetComponent<RectTransform>();
                if(rect.anchoredPosition.x<800)rect.sizeDelta=new Vector2(410,1);
                else {rect.anchoredPosition=new Vector2(1280,-51);rect.sizeDelta=new Vector2(160,1);}
            }
            var banner=RetroUi.Group(root,"ChallengeBattleBanner",580,92,760,50);
            RetroUi.Block(banner,"MissionPaper",0,0,760,50,RetroUi.Paper);
            RetroUi.Frame(banner,0,0,760,50,RetroUi.Rule,1);
            RetroUi.Block(banner,"MissionStripe",0,0,4,50,RetroUi.Red);
            var label=RetroUi.Label(banner,"ChallengeBattleCondition",$"{d.Id+1:00}　{d.Name} ｜ 条件：{d.Condition}",16,0,728,50,23,null,TextAnchor.MiddleCenter);
            label.resizeTextForBestFit=true;label.resizeTextMinSize=18;label.resizeTextMaxSize=23;
            label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Truncate;
            for(int i=0;i<2;i++)names[i].text=(i==0?"あなた":"COM")+" / "+FusionUi.Name(director.Fighters[i].Loadout)+" / "+CustomizeLabels.StyleShort(director.Fighters[i].Mods.style);
            again.button.gameObject.SetActive(false);root.Find("BattleAgainShadow").gameObject.SetActive(false);
        }
        public void SetDraftInfo()
        {
            step.text="DRAFT / BATTLE";
            step.rectTransform.anchoredPosition=new Vector2(720,-31);step.rectTransform.sizeDelta=new Vector2(480,42);
            DraftUi.Score(root,580,91,760);
            for(int i=0;i<2;i++)names[i].text=(i==0?"あなた":"COM")+" / "+FusionUi.Name(director.Fighters[i].Loadout)+" / "+CustomizeLabels.StyleShort(director.Fighters[i].Mods.style);
            again.button.gameObject.SetActive(false);root.Find("BattleAgainShadow").gameObject.SetActive(false);
        }
        public void GoBack() => Back();

        /// <summary>
        /// 自操作の表示: 速度の切り替えを隠して操作説明のボタンを置き、左下に直前の入力と出た技を出す。
        /// 練習は時間を ∞ にし、右下（詳細・もう一度の場所）で相手の強さを切り替える。
        /// </summary>
        public void SetSelfControl(PlayerController controller)
        {
            player = controller;
            practice = director.Context.Practice;
            status.resizeTextForBestFit = true; status.resizeTextMinSize = 18; status.resizeTextMaxSize = 32;
            foreach (var s in speeds) s.image.gameObject.SetActive(false);
            var auto = root.Find("Automatic")?.GetComponent<Text>();
            if (auto != null) auto.text = practice ? "練習" : "操作";
            guideButton = RetroUi.Button(root, "BattleControlsGuide", "操作説明  H", CompactStageHud?480:958, 983, CompactStageHud?340:408, 46, 24, ControlsGuide.Open, shadow: false);
            if (!practice) return;
            detailButton.image.gameObject.SetActive(false);
            again.image.gameObject.SetActive(false);
            practiceEasier = RetroUi.Button(root, "PracticeEasier", "◀", 1404, 983, 54, 46, 24, () => StepPractice(-1), shadow: false);
            RetroUi.Block(root, "PracticeLevelPlate", 1462, 983, 326, 46, RetroUi.Paper);
            RetroUi.Frame(root, 1462, 983, 326, 46, RetroUi.Ink, 2);
            practiceLevel = RetroUi.Label(root, "PracticeLevel", "", 1462, 983, 326, 46, 22, null, TextAnchor.MiddleCenter);
            practiceHarder = RetroUi.Button(root, "PracticeHarder", "▶", 1792, 983, 54, 46, 24, () => StepPractice(1), shadow: false);
        }

        /// <summary>練習の相手の強さを 1 段階変える（真・mojiFighter は解放後のみ）。</summary>
        public void StepPractice(int dir)
        {
            var choices = PracticeSession.Choices;
            int i = System.Array.IndexOf(choices, PracticeSession.Difficulty);
            for (int n = i + dir; n >= 0 && n < choices.Length; n += dir)
                if (PlayControl.Available(choices[n])) { FindFirstObjectByType<BattleBootstrap>()?.SetPracticeDifficulty(choices[n]); break; }
        }

        string PlayerStatus()
        {
            float now = director.Context.SimTime;
            if (player.LastRejectionAt >= 0 && now - player.LastRejectionAt < 1.2f && player.LastRejectionAt > player.LastCommandAt) return player.LastRejection;
            if (player.LastCommandAt >= 0 && now - player.LastCommandAt < 1.2f) return PlayerController.Label(player.LastCommand);
            return practice ? "練習中" : "自操作中";
        }
        void Back()
        {
            if(GameFlow.WorldCupMode){GameFlow.BackFromWorldCupBattle();return;}
            if(GameFlow.RunMode){GameFlow.BackFromRunBattle();return;}
            if(GameFlow.DraftMode)
            { if(DraftRuntimeStore.BackToBuild())GameFlow.ToDraftBuild();else GameFlow.ToDraftPicks(); return; }
            if (GameFlow.FriendMode) { FriendRoomSession.Instance.BackToLobby(); return; }
            if (GameFlow.ChallengeMode) { GameFlow.ToChallengePrepare(); return; }
            if (!isTournament) GameFlow.ToCustomize();
            else if (director.Ended) GameFlow.ToTournamentBoard();
            else GameFlow.ToModeSelect();
        }

        void LateUpdate()
        {
            if (director == null || rig == null || rig.Cam == null) return;
            ResizeTarget();
            float groundY = (1 - rig.Cam.WorldToViewportPoint(Vector3.zero).y) * ArenaHeight;
            groundLine.gameObject.SetActive(groundY >= 0 && groundY <= ArenaHeight);
            groundLine.rectTransform.anchoredPosition = new Vector2(0, -groundY - 2);
            float floorY = Mathf.Clamp(groundY, 0, ArenaHeight);
            groundWash.rectTransform.anchoredPosition = new Vector2(0, -floorY);
            groundWash.rectTransform.sizeDelta = new Vector2(ArenaWidth, ArenaHeight - floorY);
            for (int i = 0; i < 2; i++)
            {
                var r = director.Fighters[i].Runtime;
                float ratio = Mathf.Clamp01(r.hp / r.maxHp);
                trailValue[i] = Mathf.MoveTowards(Mathf.Max(trailValue[i], ratio), ratio, Time.unscaledDeltaTime * .45f);
                fill[i].sizeDelta = new Vector2(425 * ratio, 25);
                trail[i].sizeDelta = new Vector2(425 * trailValue[i], 25);
                hp[i].text = $"{Mathf.CeilToInt(Mathf.Max(0, r.hp))} / {Mathf.CeilToInt(r.maxHp)}";
            }
            timer.text = director.Overtime ? "延長" : Mathf.CeilToInt(director.TimeRemaining).ToString("00");
            timer.color = !director.Ended && director.TimeRemaining <= 10 ? RetroUi.Red : RetroUi.Ink;
            pause.label.text = TimeController.Paused ? "▶  再開" : "Ⅱ  一時停止";
            status.text = director.Ended ? "試合終了" : TimeController.Paused ? "一時停止中" : player != null ? PlayerStatus()
                : HasLocalInterventions ? "観戦＋応援中" : "観戦中";
            if (practice) { timer.text = "∞"; timer.color = RetroUi.Ink; practiceLevel.text = "相手：" + PlayControl.Label(PracticeSession.Difficulty); }
            for (int i = 0; i < speeds.Length; i++) RetroUi.Selected(speeds[i], Mathf.Approximately(TimeController.SpectatorSpeed, i == 0 ? .5f : i == 1 ? 1 : i == 2 ? 2 : 4), RetroUi.Ink);
            if (techniqueLife > 0 && !TimeController.Paused) techniqueLife -= Time.unscaledDeltaTime;
            techniqueTag.SetActive(techniqueLife > 0 && !director.Ended);
            if (techniqueTag.activeSelf)
            {
                Vector3 p = rig.Cam.WorldToViewportPoint(techniquePoint + Vector2.up * 1.2f);
                techniqueTag.GetComponent<RectTransform>().anchoredPosition = new Vector2(Mathf.Clamp(p.x * ArenaWidth - 120, 20, ArenaWidth - 260),
                    -Mathf.Clamp((1 - p.y) * ArenaHeight - 85, 30, ArenaHeight - 145));
            }
            UpdateAnnouncement();
            if(stageNotice!=null)stageNotice.text=director.Fighting?director.Context.Stage?.Notice??"":"";
        }

        void UpdateAnnouncement()
        {
            string title = "", caption = "";
            bool involved = HasLocalInterventions;
            if (director.CountdownRemaining > 0)
            {
                title = Mathf.CeilToInt(director.CountdownRemaining).ToString();
                if (GameSettings.ShowInstructions) caption = player != null ? "自操作　H 操作説明 ／ S 必殺 ／ D 声援" : involved ? "オート戦闘　K 必殺 ／ L 声援・踏ん張り" : "完全オート戦闘";
            }
            else if (director.Clock < .8f && !director.Ended && director.StepCount > 5) title = "FIGHT!";
            if (director.KoInProgress && !director.Ended && !director.ReplayPhase) title = "K.O.";
            if (director.Overtime && director.OvertimeElapsed < 1.5f && !director.Ended) { title = "延長戦"; caption = "先に当てた方の勝ち"; }
            if (director.Ended)
            {
                var r = director.Result;
                title = r.winner < 0 ? "DRAW" : $"「{FusionUi.Name(director.Fighters[r.winner].Loadout)}」の勝ち";
                center.color = r.winner < 0 ? RetroUi.Ink : RetroUi.Team(r.winner);
                string reason = r.finishReason.StartsWith("TIME") ? "TIME UP" : r.finishReason.StartsWith("SUDDEN") ? "延長戦" : "K.O.";
                caption = $"{reason}   /   {r.elapsedSeconds:F1}秒\n残HP {Mathf.CeilToInt(r.hpRemaining[0])} / {Mathf.CeilToInt(r.hpRemaining[1])}   与ダメ {r.metrics[0].damageDealt:F0} / {r.metrics[1].damageDealt:F0}";
            }
            else center.color = RetroUi.Ink;
            bool visible = title.Length > 0;
            if (visible && !announcement.activeSelf) { announcement.SetActive(true); RetroUi.Enter(announcement, 0, 12); }
            else if (!visible) announcement.SetActive(false);
            center.text = title; sub.text = caption;
        }

        void OnDestroy()
        {
            if (director != null) { director.Events.Motion -= OnMotion; director.Events.Hit -= OnHit; director.Events.Guard -= OnGuard; }
            if (battleTexture != null)
            {
                if (rig != null && rig.Cam != null && rig.Cam.targetTexture == battleTexture)
                { rig.Cam.targetTexture = null; rig.Cam.ResetAspect(); }
                battleTexture.Release(); Destroy(battleTexture);
            }
        }
    }
}
