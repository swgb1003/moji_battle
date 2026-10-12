using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MojiBattle
{
    public sealed partial class CustomizeScreen : MonoBehaviour
    {
        public const int PreviewLayer = 30;
        float PreviewHeight => challenge ? 314 : 380;
        FighterCustomizer customizer;
        FighterPreviewController preview;
        Canvas canvas;
        Camera previewCamera;
        RenderTexture previewTexture;
        RawImage previewViewport;
        RectTransform menuRoot;
        readonly UguiKit.ChoiceButton[] sideTabs = new UguiKit.ChoiceButton[2];
        readonly List<(UguiKit.ChoiceButton b, FontStyleId f)> fontButtons = new List<(UguiKit.ChoiceButton, FontStyleId)>();
        readonly List<(UguiKit.ChoiceButton b, WeaponSize s)> sizeButtons = new List<(UguiKit.ChoiceButton, WeaponSize)>();
        readonly List<(UguiKit.ChoiceButton b, GripType g)> gripButtons = new List<(UguiKit.ChoiceButton, GripType)>();
        readonly List<(UguiKit.ChoiceButton b, BattleStyle s)> styleButtons = new List<(UguiKit.ChoiceButton, BattleStyle)>();
        readonly List<(UguiKit.ChoiceButton b, WeaponFlip f)> flipButtons = new List<(UguiKit.ChoiceButton, WeaponFlip)>();
        const int StatCount = 8;
        readonly Text[] statValues = new Text[StatCount];
        readonly Image[,] statBars = new Image[StatCount, 5];
        InputField charInput;
        Text charError, gripValue, styleNote, gripNote, previewTitle;
        UguiKit.ChoiceButton battle, guides, specialButton;
        SpecialMovePanel specialPanel;
        GameObject vsPanel;
        Text vsLeftGlyph, vsRightGlyph, vsLeft, vsRight;
        bool refreshing, tournament, friend, challenge, draft, run, worldCup;
        bool pointerOverPreview, gripDragging, gripDropValid;
        Vector2 pendingGrip;
        int dragSide;
        public bool GripDragging => gripDragging;
        public FighterCustomizer Customizer => customizer;
        public FighterPreviewController Preview => preview;
        public bool VsVisible => vsPanel != null && vsPanel.activeSelf;
        public bool SpecialVisible => specialPanel != null && specialPanel.gameObject.activeSelf;
        public SpecialMovePanel SpecialPanel => specialPanel;
        /// <summary>武器の文字の入力欄を出してよいか（重なる画面を開いている間は隠す）。</summary>
        public bool TextInputVisible => (!run || (customizer.Side==0 && string.IsNullOrEmpty(RunRuntimeStore.Current.lockedGlyph))) && !draft && !VsVisible && !SpecialVisible && (!challenge || ChallengeRuntimeStore.Current.FixedCharacter == null);

        void Start()
        {
            tournament = GameFlow.TournamentMode;
            friend = GameFlow.FriendMode;
            challenge = GameFlow.ChallengeMode;
            draft = GameFlow.DraftMode;
            run = GameFlow.RunMode;
            worldCup=GameFlow.WorldCupMode;
            customizer = new FighterCustomizer(tournament);
            if (Camera.main != null) { Camera.main.backgroundColor = RetroUi.Paper; Camera.main.cullingMask = 0; }
            preview = new GameObject("FighterPreview").AddComponent<FighterPreviewController>();
            preview.feet = Vector2.zero;
            preview.showGuides = false;
            previewCamera = new GameObject("PreviewCamera").AddComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0, 0, 0, 0);
            previewCamera.transform.position = new Vector3(.2f, 1.8f, -10);
            previewTexture = new RenderTexture(1014, Mathf.RoundToInt(1014 * PreviewHeight / 676), 24) { name = "FighterPreviewTexture", antiAliasing = 4 };
            previewTexture.Create(); previewCamera.targetTexture = previewTexture;
            BuildUi(); customizer.Changed += Refresh; Refresh();
        }

        void BuildUi()
        {
            menuRoot = RetroUi.Screen("CustomizeCanvas", out canvas);
            var root = menuRoot;
            RetroUi.Header(root, worldCup ? "WORLD CUP / CUSTOMIZE" : run ? "RUN / CUSTOMIZE" : draft ? "DRAFT / CUSTOMIZE" : challenge ? "04 / CHALLENGE / PREPARE" : "02 / CUSTOMIZE",
                worldCup ? (System.Action)GameFlow.ToWorldCupGuide : tournament ? (System.Action)GameFlow.ToTournamentSelect : run ? (System.Action)BackRun : draft ? (System.Action)GameFlow.ToDraftPicks : challenge ? (System.Action)GameFlow.BackToChallengeDetail : friend ? (System.Action)(() => FriendRoomSession.Instance.SaveAndReturn()) : GameFlow.ToModeSelect);
            if(worldCup)
            {
                RetroUi.Heading(root,"世界に挑む一文字を決める",58);
                ((RectTransform)root.Find("Title")).sizeDelta=new Vector2(910,82);
                RetroUi.Label(root,"WorldCupEquipmentNote","文字サイズM限定・大会中は構成固定",505,225,910,40,30,null,TextAnchor.MiddleCenter);
            }
            else if (draft)
            {
                root.Find("Back").GetComponentInChildren<Text>().text = "← ドラフト";
                RetroUi.Heading(root, "選手をつくる", 64);
                ((RectTransform)root.Find("Title")).sizeDelta = new Vector2(910, 82);
                var session = DraftRuntimeStore.Current;
                RetroUi.Label(root, "DraftBuildScore", $"第{session.round}ラウンド　 あなた {session.playerWins}勝 / COM {session.comWins}勝", 505, 225, 910, 40,
                    30, null, TextAnchor.MiddleCenter);
            }
            else if (tournament)
            {
                RetroUi.Heading(root, "大会準備", 64);
                ((RectTransform)root.Find("Title")).sizeDelta = new Vector2(910, 82);
                RetroUi.Label(root, "TournamentEquipmentNote", "（大会中は装備固定）", 505, 225, 910, 40,
                    32, null, TextAnchor.MiddleCenter);
            }
            else if(run)
            {
                RetroUi.Heading(root,string.IsNullOrEmpty(RunRuntimeStore.Current.lockedGlyph)?"最初の一文字を決める":"構成を調整",64);
                ((RectTransform)root.Find("Title")).sizeDelta=new Vector2(910,82);
                var s=RunRuntimeStore.Current;
                RetroUi.Label(root,"RunBuildRound",$"第{s.round}戦 / 全5戦　残りHP {Mathf.CeilToInt(s.Hp)} / {Mathf.CeilToInt(s.MaxHp)}",505,225,910,40,30,null,TextAnchor.MiddleCenter);
            }
            else RetroUi.Heading(root, challenge ? "挑戦の構成をつくる" : "選手をつくる", challenge ? 72 : 82);
            var left = RetroUi.Group(root, "PreviewPanel", 76, 292, 716, 736);
            RetroUi.Frame(left, 0, 0, 716, 736, RetroUi.Ink, 2);
            RetroUi.Frame(left, 8, 8, 700, 720, RetroUi.Rule, 1);
            RetroUi.Block(left, "PreviewBadge", 12, 12, 360, 68, RetroUi.Red);
            previewTitle = RetroUi.Label(left, "PreviewTitle", "P1 / PREVIEW", 28, 12, 332, 68, 34, RetroUi.Paper);
            guides = RetroUi.Button(left, "PreviewGuides", "ガイド表示", 486, 26, 198,  h: 40, size: 22, action: ToggleGuides, shadow: false);
            previewViewport = RetroUi.Group(root, "PreviewViewport", 96, 380, 676, PreviewHeight).gameObject.AddComponent<RawImage>();
            previewViewport.texture = previewTexture; previewViewport.raycastTarget = true;
            previewViewport.gameObject.AddComponent<GripDragHandle>().Bind(this);
            RetroUi.Label(left, "PickHint", "文字の上でドラッグ → 離して持ち手を決定", 24, 470, 668, 38, 22, null, TextAnchor.MiddleCenter);
            gripValue = RetroUi.Label(left, "GripValue", "", 24, 510, 440,  h: 36, size: 20, color: RetroUi.Muted);
            RetroUi.Button(left, "GripReset", "中央に戻す", 480, 510, 202, 36, 20, () => customizer.ResetGrip(), shadow: false);
            // 能力値: 上段は武器（重さ・射程・扱いやすさ・攻撃）、下段は選手（防御・速度・耐久・重量）
            string[] statNames = { "重さ", "射程", "扱いやすさ", "攻撃", "防御", "速度", "耐久", "重量" };
            for (int s = 0; s < StatCount; s++)
            {
                float x = 20 + s % 4 * 170, sy = 556 + s / 4 * 86;
                RetroUi.Frame(left, x, sy, 160, 80, RetroUi.Rule, weight: 1);
                // 項目名と数値は左右に分けた枠に収める（長い名前・数値は文字を小さくして重ならないようにする）
                var statName = RetroUi.Label(left, "StatName" + s, statNames[s], x + 8, sy + 6, 84, 32, 22);
                statValues[s] = RetroUi.Label(left, "StatValue" + s, "", x + 94, sy + 6, 58, 32, 21, null, TextAnchor.MiddleRight);
                foreach (var label in new[] { statName, statValues[s] })
                {
                    label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
                    label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = label.fontSize;
                }
                for (int n = 0; n < 5; n++) statBars[s, n] = RetroUi.Block(left, "StatBar", x + 12 + n * 28, sy + 52, 24, 12, color: RetroUi.Wash);
            }
            var settings = RetroUi.Group(root, "SettingsPanel", 816, 292, 1028, 736);
            RetroUi.Frame(settings, 0, 0, 1028, 736, RetroUi.Ink, 2);
            for (int i = 0; i < 2; i++)
            {
                int side = i;
                sideTabs[i] = RetroUi.Button(settings, "Side" + i, (draft || run) ? i == 0 ? "YOU" : "COM / 構成を確認" : tournament ? i == 0 ? "YOU" : "COM" : "P" + (i + 1),
                    12 + i * 512, 12, 500, 76, 44, () => customizer.SelectSide(side), shadow: false);
                if (tournament) sideTabs[i].button.interactable = i == 0;
                if(worldCup){sideTabs[i].label.text=i==0?"YOU":"参加条件";sideTabs[i].button.interactable=i==0;}
                if (friend) { sideTabs[i].label.text = i == 0 ? "YOU" : "友人の装備はロビーで確認"; sideTabs[i].button.interactable = i == 0; }
            }
            RetroUi.Frame(settings, 12, 108, 1004, 532, RetroUi.Rule, 1);
            RetroUi.Block(settings, "LabelDivider", 232, 108, 1, 532, RetroUi.Rule);
            foreach (float lineY in new[] { 220f, 296f, 376f, 476f, 566f })
                RetroUi.Block(settings, "RowRule", 12, lineY, 1004, 1, RetroUi.Rule);
            Section(settings, "文字", 112);
            charInput = UguiKit.InputBox(settings, "CharInput", 252, 112, 198, 96, 80, OnCharacterEntered);
            charInput.GetComponent<Image>().color = RetroUi.Paper;
            charInput.GetComponent<Outline>().effectDistance = new Vector2(1, -1);
            charInput.textComponent.font = RetroUi.Font;
            charInput.textComponent.fontStyle = FontStyle.Normal;
            charInput.characterLimit = 1;
            charInput.onValueChanged.AddListener(OnCharacterEntered);
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!draft && (!run || string.IsNullOrEmpty(RunRuntimeStore.Current.lockedGlyph)) && (!challenge || ChallengeRuntimeStore.Current.FixedCharacter == null)) WebCharacterInput.Attach(charInput, this);
#endif
            RetroUi.Label(settings, "CharHint", "好きな一文字を入力\n漢字・かな・英数字・記号", 476, 112, 500, 64, 24, RetroUi.Muted);
            if (draft)
            {
                charInput.interactable = false;
                var inputColors = charInput.colors; inputColors.disabledColor = Color.white; charInput.colors = inputColors;
                settings.Find("CharHint").GetComponent<Text>().text = "ドラフトで選んだ文字（固定）";
            }
            charError = RetroUi.Label(settings, "CharError", "", 476, 177, 508, 42, 20, RetroUi.Red);
            float y = 228;
            Section(settings, "字体", y);
            foreach (FontStyleId f in System.Enum.GetValues(typeof(FontStyleId)))
            {
                var value = f;
                fontButtons.Add((Option(settings, "Font_" + f, GlyphCatalog.FontDisplayName(f), fontButtons.Count, y, () => customizer.SetFont(value)), f));
            }
            y = 308; Section(settings, tournament ? "サイズ / S限定" : "サイズ", y);
            foreach (WeaponSize s in System.Enum.GetValues(typeof(WeaponSize)))
            {
                var value = s;
                var option = Option(settings, "Size_" + s, s.ToString(), sizeButtons.Count, y, () => customizer.SetSize(value));
                if (tournament) option.button.interactable = false;
                if(worldCup){option.button.interactable=false;if(s==WeaponSize.M)option.label.text="M 固定";}
                sizeButtons.Add((option, s));
            }
            y = 388; Section(settings, "持ち方", y);
            foreach (GripType g in System.Enum.GetValues(typeof(GripType)))
            {
                var value = g;
                gripButtons.Add((Option(settings, "Grip_" + g, CustomizeLabels.Grip(g), gripButtons.Count, y, () => customizer.SetGrip(value), NotedRowHeight), g));
            }
            gripNote = RetroUi.Label(settings, "GripNote", "", 252, y + 60, 750, 26, 18, RetroUi.Muted);
            y = 478; Section(settings, "戦い方", y);
            foreach (BattleStyle s in System.Enum.GetValues(typeof(BattleStyle)))
            {
                var value = s;
                styleButtons.Add((Option(settings, "Style_" + s, CustomizeLabels.StyleShort(s), styleButtons.Count, y, () => customizer.SetStyle(value), NotedRowHeight), s));
            }
            styleNote = RetroUi.Label(settings, "StyleNote", "", 252, y + 60, 750, 26, 18, RetroUi.Muted);
            y = 568; Section(settings, "反転", y);
            string[] flips = { "なし", "左右", "上下", "180°" };
            foreach (WeaponFlip f in System.Enum.GetValues(typeof(WeaponFlip)))
            {
                var value = f;
                flipButtons.Add((Option(settings, "Flip_" + f, flips[flipButtons.Count], flipButtons.Count, y, () => customizer.SetFlip(value)), f));
            }
            battle = RetroUi.Button(settings, "Battle", tournament ? "大会エントリー →" : "対戦確認へ →", 496, 653, 508, 60, 36,
                tournament ? (System.Action)EnterTournament : ShowVs, RetroUi.Red);
            if (friend) battle.label.text = "装備を決定 → ロビー";
            if(worldCup)battle.label.text="この構成でエントリー →";
            specialButton = RetroUi.Button(settings, "SpecialMoves", "", 24, 653, 448, 60, 28, ShowSpecial, shadow: false);
            RetroUi.Enter(left.gameObject, .06f);
            RetroUi.Enter(settings.gameObject, .12f);
            BuildVs(root);
            specialPanel = SpecialMovePanel.Build(root, this);
            if(challenge) { ConfigureChallengeLayout(left,settings); specialPanel.transform.SetAsLastSibling(); }
            if(worldCup) ConfigureWorldCupDesign();
        }
        void ConfigureWorldCupDesign()
        {
            menuRoot.Find("PrintedPaper").GetComponent<RawImage>().texture=Resources.Load<Texture2D>("UI/WorldCup/design_paper");
            foreach(var text in menuRoot.GetComponentsInChildren<Text>(true))
                if(text.fontSize>=30){text.font=WorldCupDesign.HeadingFont;text.fontStyle=FontStyle.Normal;}
            menuRoot.Find("Title").GetComponent<Text>().fontSize=66;
            sideTabs[1].label.fontSize=38;
            var infoColors=sideTabs[1].button.colors;infoColors.disabledColor=Color.white;sideTabs[1].button.colors=infoColors;
            charInput.textComponent.font=WorldCupDesign.HeadingFont;
        }
        void ConfigureChallengeLayout(RectTransform left,RectTransform settings)
        {
            var d=ChallengeRuntimeStore.Current;
            menuRoot.Find("Back").GetComponentInChildren<Text>().text="← お題の詳細";
            var strip=RetroUi.Group(menuRoot,"ChallengePreparationCondition",76,274,1768,84);
            RetroUi.Frame(strip,0,0,1768,84,RetroUi.Ink,2);
            RetroUi.Block(strip,"ChallengeTitlePlate",10,10,370,64,RetroUi.Red);
            RetroUi.Label(strip,"ChallengeTitle",$"#{d.Id+1:00}　{d.Name}",24,10,342,64,30,RetroUi.Paper);
            var condition=RetroUi.Label(strip,"ChallengeConditionText","条件："+d.Condition,400,12,824,60,31);
            condition.resizeTextForBestFit=true; condition.resizeTextMinSize=23; condition.resizeTextMaxSize=31;
            RetroUi.Block(strip,"OpponentRule",1248,18,1,48,RetroUi.Rule);
            var opp=d.Opponent;
            var opponent=RetroUi.Label(strip,"ChallengeOpponentText",$"相手：{opp.character} / {opp.weaponSize} / {CustomizeLabels.Grip(opp.gripType)} / {CustomizeLabels.StyleShort(opp.battleStyle)}",1268,12,476,60,25,RetroUi.Blue);
            opponent.resizeTextForBestFit=true; opponent.resizeTextMinSize=19; opponent.resizeTextMaxSize=25;
            left.anchoredPosition=new Vector2(76,-374); left.sizeDelta=new Vector2(716,654);
            settings.anchoredPosition=new Vector2(816,-374); settings.sizeDelta=new Vector2(1028,654);
            left.GetComponent<RetroReveal>().Rebase(); settings.GetComponent<RetroReveal>().Rebase();
            ResizeFrame(left,716,654); ResizeFrame(settings,1028,654);
            previewViewport.rectTransform.anchoredPosition=new Vector2(96,-466);
            left.Find("PickHint").GetComponent<RectTransform>().anchoredPosition=new Vector2(24,-412);
            gripValue.gameObject.SetActive(false); left.Find("GripReset").gameObject.SetActive(false);
            int[] shown={1,2,3,4};
            for(int s=0;s<StatCount;s++)
            {
                int index=System.Array.IndexOf(shown,s); bool visible=index>=0;
                left.Find("StatName"+s).gameObject.SetActive(visible); statValues[s].gameObject.SetActive(visible);
                for(int n=0;n<5;n++)statBars[s,n].gameObject.SetActive(visible);
                if(!visible)continue;
                float x=20+index*170;
                Move(left.Find("StatName"+s),x+8,522);
                Move(statValues[s].transform,x+94,522);
                for(int n=0;n<5;n++)Move(statBars[s,n].transform,x+12+n*28,574);
            }
            // Rebuild the four statistic frames rather than retaining empty frames from both old rows.
            foreach(Transform child in left)
            {
                var rect=child.GetComponent<RectTransform>();
                if(child.name.StartsWith("Rule") && (Mathf.Approximately(rect.sizeDelta.x,160) || Mathf.Approximately(rect.sizeDelta.y,80)))
                    child.gameObject.SetActive(false);
            }
            for(int i=0;i<4;i++)RetroUi.Frame(left,20+i*170,516,160,80,RetroUi.Rule,1);
            sideTabs[1].button.gameObject.SetActive(false);
            sideTabs[0].label.text="あなたの構成"; sideTabs[0].image.rectTransform.sizeDelta=new Vector2(1004,76);
            string[] sections={"文字","字体","サイズ","持ち方","戦い方","反転"};
            float[] ys={92,196,270,344,418,492};
            for(int i=0;i<sections.Length;i++) Move(settings.Find("Section_"+sections[i]),36,ys[i]);
            Move(charInput.transform,252,92); charInput.interactable=d.FixedCharacter==null;
            var inputColors=charInput.colors;inputColors.disabledColor=Color.white;charInput.colors=inputColors;
            Move(settings.Find("CharHint"),476,92);
            settings.Find("CharHint").GetComponent<Text>().text=d.FixedCharacter!=null?"お題指定":"好きな一文字を入力\n漢字・かな・英数字・記号";
            if(d.FixedCharacter!=null){ChallengeScreen.LockIcon(settings,482,116);Move(settings.Find("CharHint"),534,92);}
            Move(charError.transform,476,155);
            MoveOptions(fontButtons.ConvertAll(v=>v.b),196); MoveOptions(sizeButtons.ConvertAll(v=>v.b),270);
            MoveOptions(gripButtons.ConvertAll(v=>v.b),344); MoveOptions(styleButtons.ConvertAll(v=>v.b),418);
            MoveOptions(flipButtons.ConvertAll(v=>v.b),492);
            if(d.FixedSize.HasValue)foreach(var item in sizeButtons)item.b.button.interactable=false;
            gripNote.gameObject.SetActive(false); styleNote.gameObject.SetActive(false);
            foreach(Transform child in settings) if(child.name=="RowRule" || child.name=="LabelDivider")child.gameObject.SetActive(false);
            // Remove the original table frame, then replace it at the compact challenge coordinates.
            foreach(Transform child in settings)
            {
                var rect=child.GetComponent<RectTransform>();
                if(child.name.StartsWith("Rule") && rect!=null && (Mathf.Approximately(-rect.anchoredPosition.y,108)
                    || Mathf.Approximately(-rect.anchoredPosition.y,639)))child.gameObject.SetActive(false);
            }
            RetroUi.Frame(settings,12,88,1004,472,RetroUi.Rule,1);
            RetroUi.Block(settings,"ChallengeLabelDivider",232,88,1,472,RetroUi.Rule);
            foreach(float y in new[]{188f,262,336,410,484})RetroUi.Block(settings,"ChallengeRowRule",12,y,1004,1,RetroUi.Rule);
            Move(specialButton.button.transform,264,574); specialButton.image.rectTransform.sizeDelta=new Vector2(328,60);
            specialButton.label.fontSize=26; specialButton.label.resizeTextMaxSize=26;
            Move(battle.button.transform,604,574); battle.image.rectTransform.sizeDelta=new Vector2(400,60);
            Move(settings.Find("BattleShadow"),613,584); settings.Find("BattleShadow").GetComponent<RectTransform>().sizeDelta=new Vector2(400,60);
            battle.label.text="この構成で挑戦 →"; battle.label.resizeTextMaxSize=31;
            RetroUi.Button(settings,"ChallengeReview","条件を確認",24,574,228,60,26,GameFlow.BackToChallengeDetail,shadow:false);
        }
        static void Move(Transform target,float x,float y)
        { if(target!=null){target.GetComponent<RectTransform>().anchoredPosition=new Vector2(x,-y);target.GetComponent<RetroButtonMotion>()?.Rebase();} }
        static void MoveOptions(List<UguiKit.ChoiceButton> choices,float y)
        { for(int i=0;i<choices.Count;i++)Move(choices[i].button.transform,252+i*187,y); }
        static void ResizeFrame(Transform panel,float width,float height)
        {
            foreach(Transform child in panel)
            {
                var rect=child.GetComponent<RectTransform>(); if(rect==null)continue;
                bool inner=Mathf.Approximately(rect.anchoredPosition.x,8) || Mathf.Approximately(rect.anchoredPosition.x,width-9);
                if(child.name=="RuleBottom" && -rect.anchoredPosition.y>650)rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,-height+(inner?9:2));
                if((child.name=="RuleLeft" || child.name=="RuleRight") && rect.sizeDelta.y>650)rect.sizeDelta=new Vector2(rect.sizeDelta.x,height-(inner?16:0));
            }
        }
        static void Section(Transform root, string title, float y)
        {
            RetroUi.Label(root, "Section_" + title, title, 36, y, 206, 56, 32);
        }
        // Rows with a note under the buttons keep the paper edge clear of the note text.
        const float NotedRowHeight = 48;
        static UguiKit.ChoiceButton Option(Transform root, string name, string title, int i, float y, System.Action action, float h = 56) =>
            RetroUi.Button(root, name, title, 252 + i * 187, y, 178, h, 27, action, shadow: false);
        void ToggleGuides()
        {
            preview.showGuides = !preview.showGuides;
            guides.label.text = preview.showGuides ? "ガイド非表示" : "ガイド表示";
            RetroUi.Selected(guides, preview.showGuides, RetroUi.Ink);
        }
        void BuildVs(Transform root)
        {
            if(draft){BuildDraftVs(root);return;}
            var bg = UguiKit.Fill(root, "VsPanel");
            bg.gameObject.AddComponent<Image>().color = RetroUi.Paper;
            vsPanel = bg.gameObject;
            RetroUi.PaperSurface(bg, 0, 0, 1920, 1080);
            RetroUi.Frame(bg, 24, 24, 1872, 1032, RetroUi.Ink, 3);
            RetroUi.Frame(bg, 39, 39, 1842, 1002, RetroUi.Ink, 1);
            RetroUi.Header(bg, "02 / MATCH PREVIEW", HideVs);
            RetroUi.Heading(bg, "覚悟はできたか？", 64);
            RetroUi.Label(bg, "VsTitle", "VS", 780, 290, 360, 300, 220, null, TextAnchor.MiddleCenter);
            // Each glyph and its summary share one column, independent of glyph width or spacing around VS.
            var left = RetroUi.Group(bg, "VsPlayer0", 110, 290, 700, 524);
            var right = RetroUi.Group(bg, "VsPlayer1", 1110, 290, 700, 524);
            vsLeftGlyph = RetroUi.Label(left, "VsLeftGlyph", "文字", 0, 0, 700, 300, 220, RetroUi.Red, TextAnchor.MiddleCenter);
            vsRightGlyph = RetroUi.Label(right, "VsRightGlyph", "文字", 0, 0, 700, 300, 220, RetroUi.Blue, TextAnchor.MiddleCenter);
            vsLeft = RetroUi.Label(left, "VsLeft", "", 0, 330, 700, 194, 31, RetroUi.Red, TextAnchor.UpperCenter);
            vsRight = RetroUi.Label(right, "VsRight", "", 0, 330, 700, 194, 31, RetroUi.Blue, TextAnchor.UpperCenter);
            RetroUi.Button(bg, "Start", "BATTLE 開始 →", 650, 866, 620, 106, 52, StartBattle, RetroUi.Ink);
            RetroUi.Enter(bg.gameObject, 0, 24);
            vsPanel.SetActive(false);
        }
        void BackRun() { if(string.IsNullOrEmpty(RunRuntimeStore.Current.lockedGlyph)) GameFlow.ToRunIntro(); else GameFlow.ToRunPrepare(); }
        public void ShowVs()
        {
            if(worldCup)
            {
                if(!battle.button.interactable)return;
                CancelGripDrag();preview.EndGripEditing();
                GameFlow.BeginWorldCup(customizer.Get(0),(int)(System.DateTime.Now.Ticks & 0x7fffffff)|1);return;
            }
            if(run) { CancelGripDrag(); preview.EndGripEditing(); GameFlow.ToRunPrepare(); return; }
            if (!battle.button.interactable) return;
            if (friend) { CancelGripDrag(); preview.EndGripEditing(); FriendRoomSession.Instance.SaveAndReturn(); return; }
            if (tournament) { EnterTournament(); return; }
            if (challenge) { CancelGripDrag(); preview.EndGripEditing(); StartBattle(); return; }
            CancelGripDrag(); preview.EndGripEditing();
            var l = customizer.Get(0); var r = customizer.Get(1);
            vsLeftGlyph.text = l.character; vsRightGlyph.text = r.character;
            if(draft)
            {
                vsLeftGlyph.font=GlyphFontLibrary.Default.Get(l.fontType).font;
                vsRightGlyph.font=GlyphFontLibrary.Default.Get(r.fontType).font;
                vsLeft.text=DraftSummary(l);vsRight.text=DraftSummary(r);
                vsPanel.SetActive(true);preview.gameObject.SetActive(false);previewCamera.enabled=false;return;
            }
            vsLeft.text = "P1\n" + Summary(l); vsRight.text = "P2\n" + Summary(r);
            vsPanel.SetActive(true); preview.gameObject.SetActive(false); previewCamera.enabled = false;
        }
        public void EnterTournament()
        {
            if (!tournament || !battle.button.interactable) return;
            ScreenAudioDirector.PlayBattleStart();
            GameFlow.BeginTournament(customizer.Get(0), (int)(System.DateTime.Now.Ticks & 0x7fffffff) | 1);
        }
        public void StartBattle()
        {
            if(draft && (!VsVisible || SpecialVisible || !DraftRuntimeStore.CanBattle))return;
            ScreenAudioDirector.PlayBattleStart();
            GameFlow.ToBattle();
        }
        public void ShowSpecial()
        {
            if (!specialButton.button.interactable) return;
            CancelGripDrag(); preview.EndGripEditing();
            specialPanel.Show(); preview.gameObject.SetActive(false); previewCamera.enabled = false;
        }
        public void OnSpecialPanelClosed() { preview.gameObject.SetActive(true); previewCamera.enabled = true; Refresh(); }
        public void HideVs() { vsPanel.SetActive(false); preview.gameObject.SetActive(true); previewCamera.enabled = true; }
        static string Summary(FighterBuildData b) => $"{GlyphCatalog.FontDisplayName(b.fontType)} / {b.weaponSize}\n{CustomizeLabels.Grip(b.gripType)} / {CustomizeLabels.GripPlace(b)}\n{CustomizeLabels.Flip(b.weaponFlip)} / {CustomizeLabels.Style(b.battleStyle)}\n必殺「{SpecialLabel(b)}」";
        static string SpecialLabel(FighterBuildData b) => b.EquippedSpecial?.name ?? SpecialMovePlan.StandardName(b.character);
        void Refresh()
        {
            if (gripDragging) CancelGripDrag();
            refreshing = true;
            var b = customizer.Current; int side = customizer.Side; Color accent = RetroUi.Team(side);
            for (int i = 0; i < 2; i++)
            {
                RetroUi.Selected(sideTabs[i], i == side, RetroUi.Team(i));
                if (i != side) sideTabs[i].label.color = RetroUi.Team(i);
                var motion = sideTabs[i].image.GetComponent<RetroButtonMotion>();
                motion.restBorder = motion.focusBorder = RetroUi.Team(i);
            }
            charInput.SetTextWithoutNotify(b.character);
            if(run) { charInput.interactable=customizer.Editable && string.IsNullOrEmpty(RunRuntimeStore.Current.lockedGlyph); battle.label.text="次戦準備へ →"; }
            foreach (var (btn, f) in fontButtons)
            {
                bool ok = FighterCustomizer.IsAvailable(b.character, f, out _);
                btn.button.interactable = ok && customizer.Editable;
                btn.label.text = GlyphCatalog.FontDisplayName(f) + (ok ? "" : " ×");
                RetroUi.Selected(btn, f == b.fontType, accent);
            }
            foreach (var (btn, s) in sizeButtons) RetroUi.Selected(btn, s == b.weaponSize, accent);
            foreach (var (btn, g) in gripButtons) RetroUi.Selected(btn, g == b.gripType, accent);
            foreach (var (btn, s) in styleButtons) RetroUi.Selected(btn, s == b.battleStyle, accent);
            foreach (var (btn, f) in flipButtons) RetroUi.Selected(btn, f == b.weaponFlip, accent);
            if (draft || run)
            {
                foreach (var (btn, _) in sizeButtons) btn.button.interactable = customizer.Editable;
                foreach (var (btn, _) in gripButtons) btn.button.interactable = customizer.Editable;
                foreach (var (btn, _) in styleButtons) btn.button.interactable = customizer.Editable;
                foreach (var (btn, _) in flipButtons) btn.button.interactable = customizer.Editable;
                menuRoot.Find("PreviewPanel/GripReset").GetComponent<Button>().interactable = customizer.Editable;
                menuRoot.Find("PreviewPanel/PickHint").GetComponent<Text>().text = customizer.Editable
                    ? "文字の上でドラッグ → 離して持ち手を決定" : "COMの構成を確認中（変更できません）";
            }
            gripValue.text = CustomizeLabels.GripPlace(b);
            charError.text = customizer.CharacterError ?? "";
            battle.button.interactable = string.IsNullOrEmpty(customizer.CharacterError) && (!draft || DraftRuntimeStore.CanBattle);
            specialButton.button.interactable = battle.button.interactable && customizer.Editable;
            specialButton.label.text = $"必殺技：{SpecialLabel(b)} →";
            gripNote.text = GripNote(b.gripType); styleNote.text = StyleNote(b.battleStyle);
            preview.Show(b, side);
            foreach (var child in preview.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
            FitPreviewCamera();
            previewTitle.text = ((draft || run || worldCup) ? side == 0 ? "YOU" : "COM" : challenge ? "あなた" : tournament ? "YOU" : side == 0 ? "P1" : "P2") + " / PREVIEW";
            previewTitle.transform.parent.Find("PreviewBadge").GetComponent<Image>().color = accent;
            var st = preview.Stats; var mods = preview.Mods;
            // 攻撃・速度はサイズと持ち方の補正込み（与ダメージ倍率・振りの速さ）。重量は字形とサイズで決まる重さ（0〜100）
            int attack = Mathf.RoundToInt(st.attack * mods.damage), speed = Mathf.RoundToInt(st.speed * mods.attackSpeed);
            int weight = Mathf.Clamp(mods.EffectiveWeightScore(st.weightScore, CustomizeBalance.Default), 0, 100);
            string[] values = { preview.WeaponMass.ToString("F2"), preview.AttackRange.ToString("F2"), (mods.handlingAccel * 100).ToString("F0") + "%",
                attack.ToString(), st.defense.ToString(), speed.ToString(), st.durability.ToString(), weight.ToString() };
            float[] fill = { preview.WeaponMass / 8f, preview.AttackRange / 4f, mods.handlingAccel / 1.5f,
                Mathf.InverseLerp(35f, 85f, attack), Mathf.InverseLerp(15f, 75f, st.defense), speed / 100f,
                Mathf.InverseLerp(15f, 100f, st.durability), weight / 100f };
            for (int s = 0; s < StatCount; s++)
            {
                statValues[s].text = values[s];
                int bars = Mathf.Clamp(Mathf.CeilToInt(fill[s] * 5), 1, 5);
                for (int n = 0; n < 5; n++) statBars[s, n].color = n < bars ? accent : RetroUi.Wash;
            }
            refreshing = false;
        }
        void OnCharacterEntered(string s)
        {
            if (refreshing) return;
            if (string.IsNullOrWhiteSpace(s))
            {
                charError.text = "文字を一つ入力してください";
                battle.button.interactable = false;
                return;
            }
            if (!customizer.SetCharacter(s)) charInput.SetTextWithoutNotify(s);
        }
        static string GripNote(GripType g) => g == GripType.TwoHanded ? "強い振り・安定したガード。動きはゆっくり。"
            : g == GripType.Reverse ? "速い・近距離。回避からの切り返しが得意。"
            : g == GripType.Horizontal ? "文字を盾のように構える。広く安定したガード。" : "標準的な構え。やや速いが、ガードはやや不安定。";
        static string StyleNote(BattleStyle s) => s == BattleStyle.HitAndAway ? "接近 → 攻撃 → 離脱。間合いを活かす。"
            : s == BattleStyle.Counter ? "攻撃を受け流し、その直後に反撃。"
            : s == BattleStyle.Defensive ? "ガードを多用し、受けてから返す。" : "張り付いて手数で押す。攻撃後も下がらない。";
        void FitPreviewCamera()
        {
            if (preview.Geometry == null) return;
            if(draft)
            {
                var weapon=preview.transform.Find("WeaponSprite").GetComponent<SpriteRenderer>();
                var bounds=weapon.sprite.bounds;
                float radius=0;
                foreach(float x in new[]{bounds.min.x,bounds.max.x})foreach(float y in new[]{bounds.min.y,bounds.max.y})
                    radius=Mathf.Max(radius,new Vector2(x,y).magnitude);
                // Reserve the entire swing envelope, including transparent glyph margins and off-center grips.
                previewCamera.orthographicSize=Mathf.Max(1.85f,radius+.22f);
                previewCamera.aspect=676f/PreviewHeight;
                previewCamera.transform.position=new Vector3(weapon.transform.position.x,weapon.transform.position.y,-10);
                return;
            }
            float side = preview.Geometry.maxSide;
            float visibleHeight = challenge || draft ? Mathf.Max(2.6f,side+.7f) : Mathf.Max(2.65f, side + 1.6f);
            previewCamera.orthographicSize = visibleHeight * .5f;
            previewCamera.aspect = 676f / PreviewHeight;
            // Leave a little floor below the fighter, even for XL and edge grips.
            previewCamera.transform.position = new Vector3(.25f, visibleHeight * .5f - .14f, -10);
        }
        public void SetPreviewPointerOver(bool value)
        {
            pointerOverPreview = value;
            if (preview == null || VsVisible || !customizer.Editable) return;
            if (value) preview.BeginGripEditing();
            else if (!gripDragging) preview.EndGripEditing();
        }
        public bool PreviewWorldPoint(Vector2 screen, Camera eventCamera, out Vector3 world)
        {
            world = default;
            if (previewViewport == null || previewCamera == null || VsVisible) return false;
            RectTransform rect = previewViewport.rectTransform;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rect, screen, eventCamera)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, eventCamera, out var local)) return false;
            Rect bounds = rect.rect;
            world = previewCamera.ViewportToWorldPoint(new Vector3((local.x - bounds.xMin) / bounds.width,
                (local.y - bounds.yMin) / bounds.height, 10));
            world.z = 0;
            return true;
        }
        public bool BeginGripDrag(Vector2 screen, Camera eventCamera)
        {
            if (preview == null || !customizer.Editable || gripDragging || !PreviewWorldPoint(screen, eventCamera, out var world)
                || !preview.TryPickGrip(world, out pendingGrip)) return false;
            preview.BeginGripEditing();
            gripDragging = true; gripDropValid = true; dragSide = customizer.Side;
            preview.ShowPendingGrip(pendingGrip);
            return true;
        }
        public void DragGrip(Vector2 screen, Camera eventCamera)
        {
            if (!gripDragging) return;
            gripDropValid = PreviewWorldPoint(screen, eventCamera, out var world) && preview.ProjectGrip(world, out pendingGrip);
            if (gripDropValid) preview.ShowPendingGrip(pendingGrip);
        }
        public void EndGripDrag(Vector2 screen, Camera eventCamera, bool moved)
        {
            if (!gripDragging) return;
            DragGrip(screen, eventCamera);
            bool commit = moved && gripDropValid && dragSide == customizer.Side;
            Vector2 value = pendingGrip;
            gripDragging = false; preview.ShowPendingGrip(null);
            if (commit) PickGripAt(value);
            if (!pointerOverPreview) preview.EndGripEditing();
        }
        public void CancelGripDrag()
        {
            gripDragging = false; gripDropValid = false;
            if (preview != null)
            {
                preview.ShowPendingGrip(null);
                if (!pointerOverPreview) preview.EndGripEditing();
            }
        }
        public void PickGripAt(Vector2 normalized)
        {
            var cur = customizer.Current;
            if (cur.customGrip && (cur.gripPoint - normalized).sqrMagnitude < 1e-5f) return;
            customizer.SetGripPoint(normalized);
        }
        void Update()
        {
            if(SettingsScreen.BlocksInput)return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (gripDragging) CancelGripDrag();
                else if (SpecialVisible) specialPanel.Hide();
                else if (VsVisible) HideVs(); else if(worldCup) GameFlow.ToWorldCupGuide(); else if(run) BackRun(); else if(draft) GameFlow.ToDraftPicks(); else if (challenge) GameFlow.BackToChallengeDetail(); else if (friend) FriendRoomSession.Instance.SaveAndReturn(); else GameFlow.ToModeSelect();
            }
            if (Input.GetKeyDown(KeyCode.Return) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            { if (SpecialVisible) return; if (VsVisible) StartBattle(); else ShowVs(); }
        }
        void OnDestroy()
        {
            if (customizer != null) customizer.Changed -= Refresh;
            if (previewTexture != null)
            {
                if (previewCamera != null) previewCamera.targetTexture = null;
                previewTexture.Release(); Destroy(previewTexture);
            }
        }
    }
}
