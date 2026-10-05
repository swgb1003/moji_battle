using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>
    /// FIGHTER CUSTOMIZE 画面（カスタマイズ仕様 4）。左がリアルタイムプレビュー、右が設定（文字・字体・サイズ・持ち方・握る位置・スタイル）。
    /// BATTLE で VS 確認を出し、開始で Battle シーンへ。P1（左・朱）/ P2（右・群青）を切り替えて両方を編集できる。
    /// </summary>
    public sealed class CustomizeScreen : MonoBehaviour
    {
        const float PanelX = 980f, PanelW = 880f;

        FighterCustomizer customizer;
        FighterPreviewController preview;
        Canvas canvas;
        readonly UguiKit.ChoiceButton[] sideTabs = new UguiKit.ChoiceButton[2];
        readonly List<(UguiKit.ChoiceButton b, string ch)> glyphButtons = new List<(UguiKit.ChoiceButton, string)>();
        readonly List<(UguiKit.ChoiceButton b, FontStyleId f)> fontButtons = new List<(UguiKit.ChoiceButton, FontStyleId)>();
        readonly List<(UguiKit.ChoiceButton b, WeaponSize s)> sizeButtons = new List<(UguiKit.ChoiceButton, WeaponSize)>();
        readonly List<(UguiKit.ChoiceButton b, GripType g)> gripButtons = new List<(UguiKit.ChoiceButton, GripType)>();
        readonly List<(UguiKit.ChoiceButton b, BattleStyle s)> styleButtons = new List<(UguiKit.ChoiceButton, BattleStyle)>();
        InputField charInput;
        Text charError, gripValue, styleNote, gripNote, info, previewTitle;
        Slider gripSlider;
        GameObject vsPanel;
        Text vsTitle, vsLeft, vsRight;
        bool refreshing;

        public FighterCustomizer Customizer => customizer;
        public FighterPreviewController Preview => preview;
        public bool VsVisible => vsPanel != null && vsPanel.activeSelf;

        void Start()
        {
            customizer = new FighterCustomizer();
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 2.9f;
                cam.transform.position = new Vector3(0f, 1.0f, -10f);
                cam.backgroundColor = FighterFactory.Paper;
                cam.clearFlags = CameraClearFlags.SolidColor;
            }
            preview = new GameObject("FighterPreview").AddComponent<FighterPreviewController>();
            preview.feet = new Vector2(-3.25f, -0.15f);
            BuildUi();
            customizer.Changed += Refresh;
            Refresh();
        }

        void BuildUi()
        {
            canvas = UguiKit.MakeCanvas("CustomizeCanvas");
            var root = canvas.transform;
            // 上部バー
            UguiKit.Panel(root, "TopBar", 0f, 0f, 1920f, 96f, FighterFactory.Ink);
            UguiKit.Choice(root, "Back", "← BACK", 32f, 16f, 200f, 64f, 30, GameFlow.ToTitle);
            UguiKit.Label(root, "Title", "FIGHTER CUSTOMIZE", 0f, 0f, 1920f, 96f, 44, Color.white, TextAnchor.MiddleCenter);
            previewTitle = UguiKit.Label(root, "PreviewTitle", "", 48f, 120f, 880f, 60f, 34);

            // 右パネル
            UguiKit.Panel(root, "Panel", PanelX - 20f, 112f, PanelW + 40f, 820f, Color.white, 4f);
            float y = 128f;
            Section(root, "編集する選手", y);
            for (int i = 0; i < 2; i++)
            {
                int side = i;
                sideTabs[i] = UguiKit.Choice(root, "Side" + i, i == 0 ? "P1  左・朱" : "P2  右・群青", PanelX + 220f + i * 300f, y, 280f, 56f, 28, () => customizer.SelectSide(side));
                sideTabs[i].accent = FighterFactory.TeamColor(i);
            }
            y += 76f;
            Section(root, "文字", y);
            charInput = UguiKit.InputBox(root, "CharInput", PanelX + 220f, y - 8f, 110f, 96f, 64, s =>
            {
                if (!refreshing && !string.IsNullOrEmpty(s)) customizer.SetCharacter(s);
            });
            float gx = PanelX + 350f;
            foreach (var ch in FighterCustomizer.AvailableCharacters(FontStyleId.Gothic))
            {
                string c = ch;
                glyphButtons.Add((UguiKit.Choice(root, "Glyph_" + c, c, gx, y - 8f, 54f, 46f, 30, () => customizer.SetCharacter(c)), c));
                gx += 58f;
            }
            charError = UguiKit.Label(root, "CharError", "", PanelX + 350f, y + 42f, 520f, 46f, 20, new Color(0.75f, 0.1f, 0.1f), TextAnchor.UpperLeft, false);
            y += 104f;
            Section(root, "FONT", y);
            float fx = PanelX + 220f;
            foreach (FontStyleId f in System.Enum.GetValues(typeof(FontStyleId)))
            {
                var font = f;
                var b = UguiKit.Choice(root, "Font_" + f, GlyphCatalog.FontDisplayName(f), fx, y, 150f, 56f, 26, () => customizer.SetFont(font));
                fontButtons.Add((b, f));
                fx += 160f;
            }
            y += 80f;
            Section(root, "SIZE", y);
            float sx = PanelX + 220f;
            foreach (WeaponSize s in System.Enum.GetValues(typeof(WeaponSize)))
            {
                var size = s;
                sizeButtons.Add((UguiKit.Choice(root, "Size_" + s, s.ToString(), sx, y, 140f, 56f, 32, () => customizer.SetSize(size)), s));
                sx += 155f;
            }
            y += 80f;
            Section(root, "GRIP", y);
            float px = PanelX + 220f;
            foreach (GripType g in System.Enum.GetValues(typeof(GripType)))
            {
                var grip = g;
                gripButtons.Add((UguiKit.Choice(root, "Grip_" + g, CustomizeLabels.Grip(g), px, y, 150f, 56f, 28, () => customizer.SetGrip(grip)), g));
                px += 160f;
            }
            gripNote = UguiKit.Label(root, "GripNote", "", PanelX + 220f, y + 62f, 660f, 34f, 22, NoteColor, TextAnchor.UpperLeft);
            y += 112f;
            Section(root, "GRIP POSITION", y);
            UguiKit.Label(root, "GripLeft", "端", PanelX + 220f, y, 50f, 56f, 24, null, TextAnchor.MiddleCenter);
            gripSlider = UguiKit.SliderBar(root, "GripSlider", PanelX + 270f, y, 440f, 56f, v => { if (!refreshing) customizer.SetGripPosition(v); });
            UguiKit.Label(root, "GripRight", "端", PanelX + 715f, y, 50f, 56f, 24, null, TextAnchor.MiddleCenter);
            gripValue = UguiKit.Label(root, "GripValue", "", PanelX + 770f, y, 110f, 56f, 28, null, TextAnchor.MiddleLeft);
            y += 80f;
            Section(root, "BATTLE STYLE", y);
            float bx = PanelX + 220f;
            foreach (BattleStyle s in System.Enum.GetValues(typeof(BattleStyle)))
            {
                var style = s;
                styleButtons.Add((UguiKit.Choice(root, "Style_" + s, CustomizeLabels.StyleShort(s), bx, y, 150f, 56f, 28, () => customizer.SetStyle(style)), s));
                bx += 160f;
            }
            styleNote = UguiKit.Label(root, "StyleNote", "", PanelX + 220f, y + 62f, 660f, 60f, 22, NoteColor, TextAnchor.UpperLeft);

            // プレビューの説明（左下）
            info = UguiKit.Label(root, "Info", "", 40f, 860f, 680f, 140f, 22, null, TextAnchor.UpperLeft);

            // BATTLE
            var battle = UguiKit.Choice(root, "Battle", "BATTLE", 960f - 230f, 950f, 460f, 110f, 56, ShowVs);
            battle.image.color = FighterFactory.Ink;
            battle.label.color = Color.white;

            BuildVs(root);
        }

        static readonly Color NoteColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        static void Section(Transform root, string label, float y) =>
            UguiKit.Label(root, "Section_" + label, label, PanelX, y, 210f, 56f, 26);

        void BuildVs(Transform root)
        {
            var bg = UguiKit.Fill(root, "VsPanel");
            bg.gameObject.AddComponent<Image>().color = new Color(FighterFactory.Paper.r, FighterFactory.Paper.g, FighterFactory.Paper.b, 1f);
            vsPanel = bg.gameObject;
            vsTitle = UguiKit.Label(bg, "VsTitle", "", 0f, 200f, 1920f, 240f, 150, null, TextAnchor.MiddleCenter);
            vsTitle.supportRichText = true;
            vsLeft = UguiKit.Label(bg, "VsLeft", "", 160f, 480f, 760f, 200f, 32, FighterFactory.TeamColor(0), TextAnchor.UpperCenter);
            vsRight = UguiKit.Label(bg, "VsRight", "", 1000f, 480f, 760f, 200f, 32, FighterFactory.TeamColor(1), TextAnchor.UpperCenter);
            var go = UguiKit.Choice(bg, "Start", "BATTLE 開始", 960f - 260f, 760f, 520f, 120f, 56, GameFlow.ToBattle);
            go.image.color = FighterFactory.Ink;
            go.label.color = Color.white;
            UguiKit.Choice(bg, "VsBack", "戻る", 960f - 120f, 910f, 240f, 70f, 30, HideVs);
            vsPanel.SetActive(false);
        }

        public void ShowVs()
        {
            var l = customizer.Get(0);
            var r = customizer.Get(1);
            string Hex(int i) => ColorUtility.ToHtmlStringRGB(FighterFactory.TeamColor(i));
            vsTitle.text = $"<color=#{Hex(0)}>{l.character}</color>  VS  <color=#{Hex(1)}>{r.character}</color>";
            vsLeft.text = Summary(l);
            vsRight.text = Summary(r);
            vsPanel.SetActive(true);
            preview.gameObject.SetActive(false); // VS 確認中は左のプレビューを隠す
        }

        public void HideVs()
        {
            vsPanel.SetActive(false);
            preview.gameObject.SetActive(true);
        }

        static string Summary(FighterBuildData b) =>
            $"{GlyphCatalog.FontDisplayName(b.fontType)}\n{b.weaponSize} / {CustomizeLabels.Grip(b.gripType)} / 握り {Mathf.RoundToInt(b.gripPosition * 100f)}%\n{CustomizeLabels.Style(b.battleStyle)}";

        void Refresh()
        {
            refreshing = true;
            var b = customizer.Current;
            int side = customizer.Side;
            for (int i = 0; i < 2; i++) sideTabs[i].SetSelected(i == side);
            charInput.text = b.character;
            foreach (var (btn, ch) in glyphButtons) { btn.accent = FighterFactory.TeamColor(side); btn.SetSelected(ch == b.character); }
            foreach (var (btn, f) in fontButtons)
            {
                bool ok = FighterCustomizer.IsAvailable(b.character, f, out _);
                btn.button.interactable = ok;
                btn.label.text = ok ? GlyphCatalog.FontDisplayName(f) : GlyphCatalog.FontDisplayName(f) + "\n<size=18>（未収録）</size>";
                btn.label.supportRichText = true;
                btn.SetSelected(f == b.fontType);
            }
            foreach (var (btn, s) in sizeButtons) btn.SetSelected(s == b.weaponSize);
            foreach (var (btn, g) in gripButtons) btn.SetSelected(g == b.gripType);
            foreach (var (btn, s) in styleButtons) btn.SetSelected(s == b.battleStyle);
            gripSlider.SetValueWithoutNotify(b.gripPosition);
            gripValue.text = $"{Mathf.RoundToInt(b.gripPosition * 100f)}%";
            charError.text = customizer.CharacterError ?? "";
            gripNote.text = GripNote(b.gripType);
            styleNote.text = StyleNote(b.battleStyle);
            preview.Show(b, side);
            previewTitle.text = $"{(side == 0 ? "P1" : "P2")}「{b.character}」  {b.weaponSize} / {CustomizeLabels.Grip(b.gripType)} / {CustomizeLabels.Style(b.battleStyle)}";
            previewTitle.color = FighterFactory.TeamColor(side);
            info.text = preview.Describe() + "\n<color=#555555>橙の丸 = 握り点 / 青の× = 重心 / 緑の枠 = 当たり判定</color>";
            info.supportRichText = true;
            refreshing = false;
        }

        static string GripNote(GripType g)
        {
            switch (g)
            {
                case GripType.TwoHanded: return "遅いが強い振り・崩れにくいガード・吹き飛びにくい（重い武器向け）";
                case GripType.Reverse: return "速い・近距離。回避直後の切り返しが速い。ガードは弱い";
                case GripType.Horizontal: return "字形を前へ水平に構えて盾にする。ガードが広く崩れにくい";
                default: return "標準。やや速いが、ガードはやや不安定";
            }
        }

        static string StyleNote(BattleStyle s)
        {
            switch (s)
            {
                case BattleStyle.HitAndAway: return "接近→攻撃→離脱→待ち を繰り返す";
                case BattleStyle.Counter: return "相手の攻撃を受け流し、その直後に反撃する";
                case BattleStyle.Defensive: return "字形を盾に構えてガードを多用し、受けてから返す";
                default: return "張り付いて手数で押す。攻撃後も下がらない";
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (VsVisible) HideVs();
                else GameFlow.ToTitle();
            }
        }
    }
}
