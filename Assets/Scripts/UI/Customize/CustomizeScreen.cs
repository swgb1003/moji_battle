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
        readonly List<(UguiKit.ChoiceButton b, FontStyleId f)> fontButtons = new List<(UguiKit.ChoiceButton, FontStyleId)>();
        readonly List<(UguiKit.ChoiceButton b, WeaponSize s)> sizeButtons = new List<(UguiKit.ChoiceButton, WeaponSize)>();
        readonly List<(UguiKit.ChoiceButton b, GripType g)> gripButtons = new List<(UguiKit.ChoiceButton, GripType)>();
        readonly List<(UguiKit.ChoiceButton b, BattleStyle s)> styleButtons = new List<(UguiKit.ChoiceButton, BattleStyle)>();
        readonly List<(UguiKit.ChoiceButton b, WeaponFlip f)> flipButtons = new List<(UguiKit.ChoiceButton, WeaponFlip)>();
        InputField charInput;
        Text charError, charHint, gripValue, styleNote, gripNote, info, previewTitle;

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
            // 好きな文字を 1 つ入力（確定した時点で字形化して武器にする。入力中の変換は待つ）
            charInput = UguiKit.InputBox(root, "CharInput", PanelX + 220f, y - 8f, 110f, 96f, 64, OnCharacterEntered);
            charInput.characterLimit = 1;
            charInput.onValueChanged.AddListener(OnCharacterEntered);
            charHint = UguiKit.Label(root, "CharHint", "好きな文字を 1 つ入力（漢字・かな・英数字・記号）\n例: 龍 剣 刀 鬼 あ ア W ★",
                PanelX + 350f, y - 8f, 520f, 56f, 22, NoteColor, TextAnchor.UpperLeft);
            charError = UguiKit.Label(root, "CharError", "", PanelX + 350f, y + 52f, 520f, 40f, 22, new Color(0.75f, 0.1f, 0.1f), TextAnchor.UpperLeft);
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
            // 握る場所は左のプレビューの字形をクリック（ドラッグ）して選ぶ
            gripValue = UguiKit.Label(root, "GripValue", "", PanelX + 220f, y - 4f, 470f, 64f, 22, null, TextAnchor.MiddleLeft);
            UguiKit.Choice(root, "GripReset", "中央に戻す", PanelX + 700f, y, 160f, 56f, 24, () => customizer.ResetGrip());
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
            y += 140f;
            Section(root, "FLIP", y);
            float flx = PanelX + 220f;
            foreach (WeaponFlip f in System.Enum.GetValues(typeof(WeaponFlip)))
            {
                var flip = f;
                flipButtons.Add((UguiKit.Choice(root, "Flip_" + f, CustomizeLabels.Flip(f), flx, y, 150f, 56f, 24, () => customizer.SetFlip(flip)), f));
                flx += 160f;
            }

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
            $"{GlyphCatalog.FontDisplayName(b.fontType)}\n{b.weaponSize} / {CustomizeLabels.Grip(b.gripType)} / {CustomizeLabels.GripPlace(b)}\n{CustomizeLabels.Flip(b.weaponFlip)} / {CustomizeLabels.Style(b.battleStyle)}";

        void Refresh()
        {
            refreshing = true;
            var b = customizer.Current;
            int side = customizer.Side;
            for (int i = 0; i < 2; i++) sideTabs[i].SetSelected(i == side);
            charInput.SetTextWithoutNotify(b.character);
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
            foreach (var (btn, f) in flipButtons) btn.SetSelected(f == b.weaponFlip);
            gripValue.text = "← 左の文字をクリックして持つ場所を選ぶ\n" + CustomizeLabels.GripPlace(b);
            charError.text = customizer.CharacterError ?? "";
            gripNote.text = GripNote(b.gripType);
            styleNote.text = StyleNote(b.battleStyle);
            preview.Show(b, side);
            FitPreviewCamera();
            previewTitle.text = $"{(side == 0 ? "P1" : "P2")}「{b.character}」  {b.weaponSize} / {CustomizeLabels.Grip(b.gripType)}{(b.weaponFlip != WeaponFlip.None ? " / " + CustomizeLabels.Flip(b.weaponFlip) : "")} / {CustomizeLabels.Style(b.battleStyle)}";
            previewTitle.color = FighterFactory.TeamColor(side);
            info.text = preview.Describe() + "\n<color=#555555>橙の丸 = 握り点 / 青の× = 重心 / 緑の枠 = 当たり判定</color>";
            info.supportRichText = true;
            refreshing = false;
        }

        void OnCharacterEntered(string s)
        {
            if (refreshing || string.IsNullOrEmpty(s) || s == customizer.Current.character) return;
            if (!customizer.SetCharacter(s)) charInput.SetTextWithoutNotify(s); // 使えない文字は入力欄に残して理由を出す
        }

        static string GripNote(GripType g)
        {
            switch (g)
            {
                case GripType.TwoHanded: return "遅いが強い振り・崩れにくいガード・吹き飛びにくい";
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

        /// <summary>
        /// 大きな字形でもプレビューに収まるよう、字形の最大辺に合わせてカメラを引く。
        /// 握る場所では変えない（クリック・ドラッグ中に字形の大きさが揺れないように）。
        /// </summary>
        void FitPreviewCamera()
        {
            var cam = Camera.main;
            if (cam == null || preview.Geometry == null) return;
            float top = CombatBalance.Default.shoulderLocal.y + preview.Geometry.maxSide + 0.3f;
            cam.orthographicSize = Mathf.Max(2.9f, (top - cam.transform.position.y) / 0.8f);
        }

        /// <summary>左のプレビューの字形をクリック・ドラッグして握る場所を選ぶ。右の設定パネル上の操作は対象外。</summary>
        void UpdateGripPicking()
        {
            var cam = Camera.main;
            if (cam == null || preview == null || VsVisible) return;
            Vector3 mouse = Input.mousePosition;
            bool overUi = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            bool inPreview = !overUi && mouse.x < Screen.width * (PanelX - 20f) / 1920f && mouse.y > 0f && mouse.y < Screen.height;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, -cam.transform.position.z));
            world.z = 0f;
            Vector2 normalized = default;
            bool onGlyph = inPreview && preview.TryPickGrip(world, out normalized);
            preview.Paused = inPreview;
            preview.ShowHover(onGlyph ? world : (Vector3?)null);
            if (onGlyph && Input.GetMouseButton(0)) PickGripAt(normalized);
        }

        /// <summary>握る場所を設定（テストからも使う）。</summary>
        public void PickGripAt(Vector2 normalized)
        {
            var cur = customizer.Current;
            if (cur.customGrip && (cur.gripPoint - normalized).sqrMagnitude < 1e-5f) return;
            customizer.SetGripPoint(normalized);
        }

        void Update()
        {
            UpdateGripPicking();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (VsVisible) HideVs();
                else GameFlow.ToTitle();
            }
        }
    }
}
