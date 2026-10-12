using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>
    /// カスタマイズ画面の「必殺技」画面。選んだ一文字を入れた 7 文字以内の技名を付け、AI が動きと見た目を設計する。
    /// 一文字につき 3 つまで登録でき、1 つを装備するとバトルの必殺ボタンで出る（未装備なら文字ごとの初期技）。
    /// </summary>
    public sealed class SpecialMovePanel : MonoBehaviour
    {
        const float SlotX = 110, SlotY = 330, SlotW = 800, SlotH = 150, SlotGap = 168;

        sealed class Slot
        {
            public RectTransform root;
            public Image stripe, badge;
            public Text name, element, steps, stats, empty;
            public UguiKit.ChoiceButton equip, remove;
        }

        CustomizeScreen owner;
        FighterCustomizer customizer;
        readonly Slot[] slots = new Slot[SpecialMoveRules.MaxPerCharacter];
        Text title, count, nameHint, status, detail;
        InputField nameInput;
        UguiKit.ChoiceButton generate, standard;
        List<SpecialMoveData> moves = new List<SpecialMoveData>();
        Coroutine job;
        int jobId;

        public bool Busy => job != null;
        public string Status => status.text;
        public InputField NameInput => nameInput;
        public UguiKit.ChoiceButton GenerateButton => generate;

        public static SpecialMovePanel Build(Transform root, CustomizeScreen screen)
        {
            var bg = UguiKit.Fill(root, "SpecialPanel");
            bg.gameObject.AddComponent<Image>().color = RetroUi.Paper;
            var panel = bg.gameObject.AddComponent<SpecialMovePanel>();
            panel.owner = screen; panel.customizer = screen.Customizer;
            panel.BuildUi(bg);
            bg.gameObject.SetActive(false);
            return panel;
        }

        void BuildUi(RectTransform bg)
        {
            RetroUi.PaperSurface(bg, 0, 0, 1920, 1080);
            RetroUi.Frame(bg, 24, 24, 1872, 1032, RetroUi.Ink, 3);
            RetroUi.Frame(bg, 39, 39, 1842, 1002, RetroUi.Ink, 1);
            RetroUi.Header(bg, "02 / SPECIAL MOVE", Hide);
            RetroUi.Heading(bg, "必殺技をつくる", 82);
            title = RetroUi.Label(bg, "SpecialTitle", "", SlotX, 252, 560, 60, 34);
            count = RetroUi.Label(bg, "SpecialCount", "", SlotX + 560, 252, 240, 60, 28, RetroUi.Muted, TextAnchor.MiddleRight);
            for (int i = 0; i < slots.Length; i++) slots[i] = BuildSlot(bg, i);
            standard = RetroUi.Button(bg, "StandardSpecial", "", SlotX, SlotY + SlotGap * 3 + 6, SlotW, 64, 26, () => Equip(null), shadow: false);

            float x = 990, w = 820;
            RetroUi.Label(bg, "NameCaption", "技名", x, 252, 300, 60, 34);
            nameInput = UguiKit.InputBox(bg, "SpecialNameInput", x, 330, w, 110, 60, null);
            nameInput.GetComponent<Image>().color = RetroUi.Paper;
            nameInput.GetComponent<Outline>().effectDistance = new Vector2(1, -1);
            nameInput.textComponent.font = RetroUi.Font;
            nameInput.textComponent.fontStyle = FontStyle.Normal;
            nameInput.characterLimit = 20;
            ((Text)nameInput.placeholder).text = "例：炎龍一閃";
            nameInput.onValueChanged.AddListener(_ => RefreshName());
            nameInput.onEndEdit.AddListener(_ => { if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)) Generate(); });
#if UNITY_WEBGL && !UNITY_EDITOR
            WebCharacterInput.Attach(nameInput, () => gameObject.activeInHierarchy, () => 0, "必殺技の名前", "例：炎龍一閃", 20, .5f);
#endif
            nameHint = RetroUi.Label(bg, "NameHint", "", x, 448, w, 44, 24, RetroUi.Muted);
            generate = RetroUi.Button(bg, "GenerateSpecial", "AI で技をつくる →", x, 504, w, 88, 38, Generate, RetroUi.Red);
            status = RetroUi.Label(bg, "SpecialStatus", "", x, 610, w, 80, 24, RetroUi.Ink, TextAnchor.UpperLeft);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            RetroUi.Block(bg, "DetailRule", x, 702, w, 2, RetroUi.Ink);
            detail = RetroUi.Label(bg, "SpecialDetail", "", x, 718, w, 300, 26, RetroUi.Ink, TextAnchor.UpperLeft);
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            RetroUi.Enter(bg.gameObject, 0, 24);
        }

        Slot BuildSlot(Transform bg, int i)
        {
            var s = new Slot();
            float y = SlotY + i * SlotGap;
            s.root = RetroUi.Group(bg, "SpecialSlot" + i, SlotX, y, SlotW, SlotH);
            RetroUi.Frame(s.root, 0, 0, SlotW, SlotH, RetroUi.Ink, 2);
            s.stripe = RetroUi.Block(s.root, "Stripe", 2, 2, 18, SlotH - 4, RetroUi.Ink);
            s.name = RetroUi.Label(s.root, "Name", "", 38, 6, 470, 62, 44);
            s.name.resizeTextForBestFit = true; s.name.resizeTextMinSize = 26; s.name.resizeTextMaxSize = 44;
            s.badge = RetroUi.Block(s.root, "ElementBadge", 510, 10, 70, 56, RetroUi.Ink);
            s.element = RetroUi.Label(s.root, "Element", "", 510, 10, 70, 56, 40, RetroUi.Paper, TextAnchor.MiddleCenter);
            s.steps = RetroUi.Label(s.root, "Steps", "", 38, 70, 550, 36, 22, RetroUi.Ink);
            s.steps.resizeTextForBestFit = true; s.steps.resizeTextMinSize = 16; s.steps.resizeTextMaxSize = 22;
            s.stats = RetroUi.Label(s.root, "Stats", "", 38, 106, 550, 36, 22, RetroUi.Muted);
            s.empty = RetroUi.Label(s.root, "Empty", "空き枠", 0, 0, SlotW, SlotH, 30, RetroUi.Muted, TextAnchor.MiddleCenter);
            int index = i;
            s.equip = RetroUi.Button(s.root, "Equip", "装備する", 600, 14, 184, 60, 26, () => Equip(index), shadow: false);
            s.remove = RetroUi.Button(s.root, "Remove", "削除", 600, 84, 184, 50, 22, () => Remove(index), shadow: false);
            return s;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            nameInput.SetTextWithoutNotify("");
            SetStatus("");
            Refresh();
        }

        public void Hide()
        {
            if (job != null) { StopCoroutine(job); job = null; jobId++; }
            gameObject.SetActive(false);
            owner.OnSpecialPanelClosed();
        }

        string Character => customizer.Current.character;

        public void Refresh()
        {
            moves = SpecialMoveLibrary.For(Character);
            var equipped = customizer.Current.EquippedSpecial;
            Color accent = RetroUi.Team(customizer.Side);
            title.text = $"「{Character}」の必殺技";
            count.text = $"登録 {moves.Count} / {SpecialMoveRules.MaxPerCharacter}";
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                var m = i < moves.Count ? moves[i] : null;
                bool on = m != null && m.SameAs(equipped);
                s.empty.gameObject.SetActive(m == null);
                foreach (var t in new[] { s.name, s.element, s.steps, s.stats }) t.gameObject.SetActive(m != null);
                s.stripe.gameObject.SetActive(m != null);
                s.badge.gameObject.SetActive(m != null);
                s.equip.button.gameObject.SetActive(m != null);
                s.remove.button.gameObject.SetActive(m != null);
                if (m == null) continue;
                s.stripe.color = m.PrimaryColor;
                s.name.text = m.name;
                s.element.text = SpecialMoveRules.ElementLabel(m.Element);
                s.element.color = ReadableOn(m.PrimaryColor);
                s.badge.color = m.PrimaryColor;
                s.steps.text = m.StepsLabel;
                s.stats.text = $"威力 {Pips(m.power)}　速さ {Pips(m.speed)}　伸び {Pips(m.reach)}";
                s.equip.label.text = on ? "装備中" : "装備する";
                RetroUi.Selected(s.equip, on, accent);
            }
            string standardName = SpecialMovePlan.StandardName(Character);
            standard.label.text = equipped == null ? $"標準の必殺技（{standardName}）を装備中" : $"標準の必殺技（{standardName}）に戻す";
            RetroUi.Selected(standard, equipped == null, accent);
            detail.text = Detail(equipped ?? BuiltinSpecialMoves.For(Character), standardName);
            RefreshName();
        }

        static string Pips(int n) => new string('●', Mathf.Clamp(n, 0, 5)) + new string('○', 5 - Mathf.Clamp(n, 0, 5));
        static Color ReadableOn(Color c) => c.r * .3f + c.g * .59f + c.b * .11f > .6f ? RetroUi.Ink : RetroUi.Paper;

        static string Detail(SpecialMoveData m, string standardName)
        {
            if (m == null) return $"装備中：{standardName}（標準）\n文字に合わせた初期必殺技。\n技名を付けると、AI が名前から動きと見た目を考えます。";
            var lines = new List<string> { $"装備中：{m.name}" + (string.IsNullOrEmpty(m.reading) ? "" : $"（{m.reading}）") };
            if (!string.IsNullOrEmpty(m.callout)) lines.Add($"掛け声「{m.callout}」");
            if (!string.IsNullOrEmpty(m.description)) lines.Add(m.description);
            lines.Add($"動き：{m.StepsLabel}");
            lines.Add($"属性：{SpecialMoveRules.ElementLabel(m.Element)}");
            return string.Join("\n", lines);
        }

        void RefreshName()
        {
            string name = nameInput.text ?? "";
            string error = name.Trim().Length == 0 ? null : SpecialMoveRules.CheckName(name, Character);
            int n = SpecialMoveRules.Graphemes(name.Trim()).Count;
            bool full = moves.Count >= SpecialMoveRules.MaxPerCharacter;
            nameHint.text = full ? $"登録は一文字につき{SpecialMoveRules.MaxPerCharacter}つまで。削除すると新しく作れます"
                : error ?? $"「{Character}」を必ず入れて{SpecialMoveRules.MaxName}文字以内（いま{n}文字）";
            nameHint.color = full || error != null ? RetroUi.Red : RetroUi.Muted;
            generate.button.interactable = !Busy && !full && name.Trim().Length > 0 && error == null;
            generate.label.text = Busy ? "AI が考えています…" : "AI で技をつくる →";
        }

        void SetStatus(string text, bool error = false)
        {
            status.text = text;
            status.color = error ? RetroUi.Red : RetroUi.Ink;
        }

        public void Generate()
        {
            if (!generate.button.interactable) return;
            string name = nameInput.text.Trim(), character = Character;
            int id = ++jobId;
            job = StartCoroutine(GenerateRoutine(character, name, id));
            RefreshName();
        }

        IEnumerator GenerateRoutine(string character, string name, int id)
        {
            SetStatus($"「{name}」の技を AI が考えています…");
            var client = new SpecialMoveClient();
            yield return client.Generate(character, name, () => id == jobId,
                () => SetStatus("サーバーを起動しています。1 分ほどかかることがあります…"));
            if (id != jobId) yield break;
            job = null;
            if (client.Result == null) { SetStatus(client.Error ?? "技を作れませんでした。", true); RefreshName(); yield break; }
            if (!SpecialMoveLibrary.Add(client.Result)) { SetStatus("登録枠がいっぱいです。", true); RefreshName(); yield break; }
            customizer.SetSpecial(client.Result);
            nameInput.SetTextWithoutNotify("");
            SetStatus($"「{client.Result.name}」を登録して装備しました。");
            Refresh();
        }

        void Equip(int? index)
        {
            if (index.HasValue && index.Value >= moves.Count) return;
            customizer.SetSpecial(index.HasValue ? moves[index.Value] : null);
            Refresh();
        }

        void Remove(int index)
        {
            if (index >= moves.Count) return;
            var m = moves[index];
            SpecialMoveLibrary.Remove(m);
            if (m.SameAs(customizer.Current.special)) customizer.SetSpecial(null);
            SetStatus($"「{m.name}」を削除しました。");
            Refresh();
        }

        /// <summary>テスト用: AI を通さずに技を登録・装備する。</summary>
        public void AddForTests(SpecialMoveData move)
        {
            SpecialMoveLibrary.Add(move);
            customizer.SetSpecial(move);
            Refresh();
        }
    }
}
