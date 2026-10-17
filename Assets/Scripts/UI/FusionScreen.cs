using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>The workshop is an overlay so friend rooms and the selected fighter remain intact.</summary>
    public sealed partial class FusionScreen : MonoBehaviour
    {
        public enum Page { Menu, Parts, Layout, Finish, Library }
        static FusionScreen instance;
        static int closedFrame = -1;
        public static bool IsOpen => instance != null;
        public static bool BlocksInput => IsOpen || closedFrame == Time.frameCount;
        public Page CurrentPage { get; private set; }
        public FusionData Recipe => recipe.Clone();
        public bool BirthPlaying => birth != null;
        public FusionBirthEffect Birth => birth;
        RectTransform root, page;
        Canvas canvas;
        Action<FighterBuildData> equip;
        Action closed;
        FusionData recipe = new FusionData();
        FontStyleId font;
        string editingId, selectedId, message = "";
        bool chooseRight, selectedRight, dirty;
        int sourcePage, libraryPage, sourceLayoutFilter = -1;
        float nextRefresh;
        RawImage combinedImage, leftImage, rightImage, layoutMini;
        RectTransform layoutCanvas;
        RectTransform[] bounds = new RectTransform[2];
        Text composition, sourceLabel, status;
        InputField nameInput;
        FusionBirthEffect birth;
        FusionWeaponPreview weaponPreview;
        GameObject deleteDialog;
        readonly List<Action> refreshControls = new List<Action>();
        readonly List<(GraphicRaycaster raycaster, CanvasGroup group, bool interactable, bool created)> blocked =
            new List<(GraphicRaycaster, CanvasGroup, bool, bool)>();
        GameObject previousSelection;

        public static FusionScreen Open(Action<FighterBuildData> onEquip, Action onClose = null, FusionData initial = null, FontStyleId initialFont = FontStyleId.Gothic)
        {
            if (instance != null) return instance;
            var area = RetroUi.Screen("FusionCanvas", out var c, 90);
            // The opaque surface also catches clicks outside the safe area on ultrawide screens.
            c.transform.Find("PaperBackground").GetComponent<Image>().raycastTarget = true;
            var screen = c.gameObject.AddComponent<FusionScreen>(); instance = screen;
            screen.root = area; screen.canvas = c; screen.equip = onEquip; screen.closed = onClose;
            screen.BlockBackgroundInput();
            screen.font = initialFont;
            if (initial != null && initial.Validate(out _)) screen.recipe = initial.Clone();
            screen.Show(Page.Menu); return screen;
        }
        public void Close()
        {
            if (BirthPlaying) return;
            closedFrame = Time.frameCount; instance = null;
            RestoreBackgroundInput();
            gameObject.SetActive(false); closed?.Invoke(); Destroy(gameObject);
        }
        void OnDestroy() { RestoreBackgroundInput(); if (instance == this) { instance = null; closedFrame = Time.frameCount; } }
        void BlockBackgroundInput()
        {
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            foreach (var raycaster in FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None))
            {
                if (!raycaster.enabled || raycaster.gameObject == canvas.gameObject || raycaster.GetComponent<Canvas>().sortingOrder >= 90) continue;
                var group = raycaster.GetComponent<CanvasGroup>(); bool created = group == null;
                if (created) group = raycaster.gameObject.AddComponent<CanvasGroup>();
                blocked.Add((raycaster, group, group.interactable, created)); raycaster.enabled = false; group.interactable = false;
            }
            EventSystem.current?.SetSelectedGameObject(null);
        }
        void RestoreBackgroundInput()
        {
            foreach (var entry in blocked)
            {
                if (entry.raycaster != null) entry.raycaster.enabled = true;
                if (entry.group != null) { entry.group.interactable = entry.interactable; if (entry.created) Destroy(entry.group); }
            }
            blocked.Clear();
            if (previousSelection != null && previousSelection.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(previousSelection);
            previousSelection = null;
        }
        void CloseDeleteDialog()
        {
            if (deleteDialog != null) { deleteDialog.SetActive(false); Destroy(deleteDialog); deleteDialog = null; }
            if (page != null && page.TryGetComponent<CanvasGroup>(out var group)) group.interactable = true;
            EventSystem.current?.SetSelectedGameObject(null);
        }
        public void Back()
        {
            if (BirthPlaying) return;
            if (deleteDialog != null) { CloseDeleteDialog(); return; }
            if (CurrentPage == Page.Menu) Close();
            else Show(CurrentPage == Page.Finish ? Page.Layout : CurrentPage == Page.Layout ? Page.Parts : Page.Menu);
        }
        public void NewCharacter()
        { recipe = new FusionData(); font = FontStyleId.Gothic; editingId = null; sourcePage = 0; sourceLayoutFilter = -1; chooseRight = false; Show(Page.Parts); }
        public void Show(Page value)
        {
            if (BirthPlaying) return;
            if (nameInput != null) recipe.name = nameInput.text.Trim();
            if (page != null) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            weaponPreview = null; nameInput = null; combinedImage = leftImage = rightImage = layoutMini = null;
            layoutCanvas = null; bounds = new RectTransform[2]; composition = sourceLabel = status = null; refreshControls.Clear();
            page = RetroUi.Group(root, "Fusion" + value, 0, 0, 1920, 1080); CurrentPage = value;
            EventSystem.current?.SetSelectedGameObject(null);
            RetroUi.Header(page, value == Page.Menu ? "KANJI WORKSHOP" : value == Page.Parts ? "SELECT PARTS" : value == Page.Layout ? "COMPOSE" : value == Page.Finish ? "FINISH" : "MY CHARACTERS", Back);
            string title = value == Page.Menu ? "文字合成" : value == Page.Parts ? "部品を選ぶ" : value == Page.Layout ? "配置を整える" : value == Page.Finish ? "文字に名前を付ける" : "保存した文字";
            RetroUi.Heading(page, title, value == Page.Finish ? 70 : 82);
            switch (value) { case Page.Menu: Menu(); break; case Page.Parts: Parts(); break; case Page.Layout: Layout(); break; case Page.Finish: Finish(); break; case Page.Library: Library(); break; }
            foreach (var t in page.GetComponentsInChildren<Text>(true))
            { t.supportRichText = false; if (t.fontSize >= 28) { t.font = WorldCupDesign.HeadingFont; t.fontStyle = FontStyle.Normal; } }
            RefreshGlyph(); dirty = false;
        }
        static RectTransform Panel(Transform p, string name, float x, float y, float w, float h)
        {
            var r = RetroUi.Group(p, name, x, y, w, h);
            RetroUi.Block(r, name + "Shadow", 7, 8, w, h, RetroUi.Ink);
            RetroUi.PaperSurface(r, 0, 0, w, h);
            RetroUi.Frame(r, 0, 0, w, h, RetroUi.Ink, 2); RetroUi.Frame(r, 9, 9, w - 18, h - 18, RetroUi.Rule, 1); return r;
        }
        static Text Text(Transform p, string name, string value, float x, float y, float w, float h, int size = 28, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var t = RetroUi.Label(p, name, value, x, y, w, h, size, color, align); t.supportRichText = false;
            if (h < size * 2.2f) { t.resizeTextForBestFit = true; t.resizeTextMinSize = Mathf.Max(14, size - 10); t.resizeTextMaxSize = size; }
            return t;
        }
        static RawImage Mask(Transform p, string name, GlyphDefinitionRuntime glyph, float x, float y, float w, float h, Color? color = null, bool crop = false)
        {
            var raw = RetroUi.Group(p, name, x, y, w, h).gameObject.AddComponent<RawImage>(); raw.raycastTarget = false;
            raw.texture = glyph?.texture; raw.color = color ?? RetroUi.Ink;
            if (crop && glyph != null)
            {
                var b = glyph.features.inkBounds; float scale = Mathf.Min(w / b.width, h / b.height);
                raw.rectTransform.anchoredPosition += new Vector2((w - b.width * scale) / 2, -(h - b.height * scale) / 2);
                raw.rectTransform.sizeDelta = new Vector2(b.width, b.height) * scale;
                raw.uvRect = new Rect(b.xMin / (float)glyph.texture.width, b.yMin / (float)glyph.texture.height, b.width / (float)glyph.texture.width, b.height / (float)glyph.texture.height);
            }
            return raw;
        }
        bool Whole(out GlyphDefinitionRuntime glyph) => FusionGlyphs.TryGet(recipe, font, CombatBalance.Default, GlyphCalibration.Default, -1, out glyph, out _);
        void Menu()
        {
            Text(page, "Intro", "部品を組み合わせて、自分だけの文字武器へ", 400, 244, 1120, 45, 28, null, TextAnchor.MiddleCenter);
            var hero = Panel(page, "FusionHero", 76, 306, 1192, 718);
            var ink = WorldCupDesign.Art(hero, "world_cup_guide_landscape", 12, 270, 1168, 430, false); ink.color = new Color(1, 1, 1, .35f);
            GlyphCatalog.TryGet(recipe.leftSource, font, CombatBalance.Default, GlyphCalibration.Default, out var l, out _);
            GlyphCatalog.TryGet(recipe.rightSource, font, CombatBalance.Default, GlyphCalibration.Default, out var r, out _);
            var cardL = Panel(hero, "ParentLeft", 210, 26, 148, 130); Mask(cardL, "Parent", l, 17, 8, 114, 114);
            var cardR = Panel(hero, "ParentRight", 826, 26, 148, 130); Mask(cardR, "Parent", r, 17, 8, 114, 114);
            Text(hero, "ArrowL", "↓", 280, 151, 100, 60, 48);
            Text(hero, "ArrowR", "↓", 820, 151, 100, 60, 48);
            FusionGlyphs.SourcePart(recipe.leftSource, font, recipe.leftUseSecond, out var lp, out _); FusionGlyphs.SourcePart(recipe.rightSource, font, !recipe.rightUseFirst, out var rp, out _);
            var partL = Panel(hero, "ExampleLeft", 317, 186, 196, 184); Mask(partL, "Part", lp, 28, 14, 140, 140, RetroUi.Red, true);
            var partR = Panel(hero, "ExampleRight", 691, 186, 196, 184); Mask(partR, "Part", rp, 28, 14, 140, 140, RetroUi.Red, true);
            Text(hero, "Plus", "+", 531, 220, 142, 108, 84, null, TextAnchor.MiddleCenter);
            Text(hero, "PartLabelLeft", recipe.FirstRole + "：" + recipe.Left, 280, 374, 260, 44, 28, null, TextAnchor.MiddleCenter);
            Text(hero, "PartLabelRight", recipe.SecondRole + "：" + recipe.Right, 654, 374, 260, 44, 28, null, TextAnchor.MiddleCenter);
            Text(hero, "Down", "↓", 528, 399, 140, 72, 64, null, TextAnchor.MiddleCenter);
            Whole(out var g); Mask(hero, "Extrusion", g, 436, 446, 330, 270, new Color32(99, 27, 19, 255));
            combinedImage = Mask(hero, "FusionGlyph", g, 426, 435, 330, 270, RetroUi.Red);
            var actions = Panel(page, "FusionActions", 1284, 306, 560, 718);
            RetroUi.Button(actions, "FusionNew", "新しく作る →", 24, 28, 512, 146, 48, NewCharacter, RetroUi.Red);
            RetroUi.Button(actions, "FusionLibrary", "保存した文字 →", 24, 194, 512, 140, 42, () => Show(Page.Library));
            Text(actions, "Examples", "できあがりの例", 28, 366, 504, 46, 30, null, TextAnchor.MiddleCenter);
            var example = new FusionData(); FusionGlyphs.TryGet(example, font, CombatBalance.Default, GlyphCalibration.Default, -1, out var eg, out _);
            Panel(actions, "ExampleCard1", 28, 432, 242, 220); Mask(actions, "Example1", eg, 38, 436, 220, 210, RetroUi.Red);
            example.rightSource = "林"; FusionGlyphs.TryGet(example, font, CombatBalance.Default, GlyphCalibration.Default, -1, out eg, out _);
            Panel(actions, "ExampleCard2", 290, 432, 242, 220); Mask(actions, "Example2", eg, 300, 436, 220, 210, RetroUi.Red);
            Text(actions, "Possibilities", "創作文字も作れます", 28, 662, 504, 36, 27, null, TextAnchor.MiddleCenter);
        }
        void Parts()
        {
            Text(page, "PartsIntro", "漢字の二つの部品から、自由に組み合わせる", 400, 244, 1120, 42, 28, null, TextAnchor.MiddleCenter);
            var preview = Panel(page, "CompositionPreview", 76, 296, 680, 728);
            RetroUi.Block(preview, "PreviewTitle", 12, 12, 656, 64, RetroUi.Red);
            Text(preview, "PreviewTitleText", "合成プレビュー", 34, 12, 612, 64, 36, RetroUi.Paper);
            RetroUi.Frame(preview, 14, 88, 652, 430, RetroUi.Rule, 1);
            RetroUi.Line(preview, new Vector2(340, 100), new Vector2(340, 508), RetroUi.Rule, 1);
            Whole(out var g); combinedImage = Mask(preview, "FusionGlyph", g, 124, 88, 430, 430);
            composition = Text(preview, "Composition", "", 20, 536, 640, 82, 40, null, TextAnchor.MiddleCenter);
            sourceLabel = Text(preview, "Sources", "", 20, 616, 640, 76, 25, RetroUi.Muted, TextAnchor.MiddleCenter);
            var choices = Panel(page, "SourceChoices", 780, 296, 1064, 728);
            var l = RetroUi.Button(choices, "ChooseLeft", recipe.FirstRole + "の部品を選ぶ", 14, 12, 512, 72, 40, () => SelectTab(false));
            var r = RetroUi.Button(choices, "ChooseRight", recipe.SecondRole + "の部品を選ぶ", 538, 12, 512, 72, 40, () => SelectTab(true));
            RetroUi.Selected(l, !chooseRight, RetroUi.Red); RetroUi.Selected(r, chooseRight, RetroUi.Red);
            bool second = chooseRight ? !recipe.rightUseFirst : recipe.leftUseSecond;
            var filters = new[] { "すべて", "左右", "上下", "囲み", "左 / 上 / 外", "右 / 下 / 内" };
            for (int i = 0; i < filters.Length; i++)
            {
                int index = i; var b = RetroUi.Button(choices, "SourceFilter_" + i, filters[i], 22 + i * 172, 102, 160, 52, 25,
                    () => {
                        if (index < 4) { sourceLayoutFilter = index - 1; sourcePage = 0; }
                        else { recipe.version = 2; if (chooseRight) recipe.rightUseFirst = index == 4; else recipe.leftUseSecond = index == 5; }
                        Show(Page.Parts);
                    });
                RetroUi.Selected(b, i < 4 ? sourceLayoutFilter == i - 1 : second == (i == 5), RetroUi.Red);
            }
            var entries = Sources(); int pages = Mathf.Max(1, (entries.Count + 5) / 6); sourcePage = Mathf.Clamp(sourcePage, 0, pages - 1);
            for (int i = sourcePage * 6; i < Mathf.Min(entries.Count, sourcePage * 6 + 6); i++)
            {
                var entry = entries[i]; int n = i % 6; float x = 22 + n % 3 * 346, y = 174 + n / 3 * 207;
                bool selected = (chooseRight ? recipe.rightSource : recipe.leftSource) == entry.Character;
                var button = RetroUi.Button(choices, "Source_" + entry.Character, "", x, y, 330, 192, 28, () => SelectSource(entry.Character), shadow: false);
                if (selected) RetroUi.Frame(button.image.transform, 1, 1, 328, 190, RetroUi.Red, 4);
                FusionGlyphs.SourcePart(entry.Character, font, second, out var part, out _);
                Mask(button.image.transform, "SourcePart", part, 88, 12, 154, 132, selected ? RetroUi.Red : RetroUi.Ink, true);
                Text(button.image.transform, "SourceCaption", entry.Character + " の" + FusionData.SourceRole(entry.Layout, second) + "：" + (second ? entry.Second : entry.First), 12, 147, 306, 36, 26, null, TextAnchor.MiddleCenter);
            }
            if (entries.Count == 0) Text(choices, "NoParts", "この部品の素材はありません。", 40, 320, 980, 60, 32, RetroUi.Muted, TextAnchor.MiddleCenter);
            Pager(choices, "Source", 22, 592, 1020, sourcePage, pages, p => { sourcePage = p; Show(Page.Parts); });
            RetroUi.Button(choices, "ResetParts", "選び直す", 24, 656, 320, 52, 27, NewCharacter);
            RetroUi.Button(choices, "ToLayout", "配置調整へ →", 364, 646, 676, 64, 36, () => Show(Page.Layout), RetroUi.Red);
        }
        List<SplitKanjiCatalog.Entry> Sources()
        {
            var list = new List<SplitKanjiCatalog.Entry>();
            string[] preferred = chooseRight ? new[] { "晴", "明", "時", "村", "校", "海" } : new[] { "休", "林", "河", "持", "紅", "好" };
            foreach (string ch in preferred) if (SplitKanjiCatalog.TryGet(ch, out var e) && (sourceLayoutFilter < 0 || (int)e.Layout == sourceLayoutFilter)) list.Add(e);
            foreach (var e in SplitKanjiCatalog.Entries)
                if (!list.Exists(a => a.Character == e.Character) && (sourceLayoutFilter < 0 || (int)e.Layout == sourceLayoutFilter)) list.Add(e);
            return list;
        }
        void SelectTab(bool right) { chooseRight = right; sourcePage = 0; sourceLayoutFilter = -1; Show(Page.Parts); }
        public void SelectSource(string source)
        {
            if (!SplitKanjiCatalog.TryGet(source, out _)) return;
            recipe.version = 2;
            if (chooseRight) recipe.rightSource = source; else recipe.leftSource = source;
            Show(Page.Parts);
        }
        static void Pager(Transform p, string name, float x, float y, float w, int current, int pages, Action<int> change)
        {
            var prev = RetroUi.Button(p, name + "Prev", "← 前へ", x, y, 170, 42, 24, () => change(current - 1), shadow: false); prev.button.interactable = current > 0;
            Text(p, name + "Page", (current + 1) + " / " + pages, x + 190, y, w - 380, 42, 25, RetroUi.Muted, TextAnchor.MiddleCenter);
            var next = RetroUi.Button(p, name + "Next", "次へ →", x + w - 170, y, 170, 42, 24, () => change(current + 1), shadow: false); next.button.interactable = current + 1 < pages;
        }
        void RefreshGlyph()
        {
            if (!Whole(out var g)) return;
            if (combinedImage != null) combinedImage.texture = g.texture;
            if (layoutMini != null) layoutMini.texture = g.texture;
            if (composition != null) composition.text = recipe.Left + " ＋ " + recipe.Right;
            if (sourceLabel != null) sourceLabel.text = recipe.FirstRole + "：" + recipe.leftSource + " から　／　" + recipe.SecondRole + "：" + recipe.rightSource + " から\n分離対応" + SplitKanjiCatalog.Entries.Length + "字のすべての部品から選べます";
            if (layoutCanvas != null)
            {
                for (int p = 0; p < 2; p++)
                {
                    FusionGlyphs.TryGet(recipe, font, CombatBalance.Default, GlyphCalibration.Default, p, out var part, out _);
                    var img = p == 0 ? leftImage : rightImage; if (img != null) img.texture = part.texture;
                    if (bounds[p] != null)
                    {
                        var b = part.features.inkBounds; float s = layoutCanvas.rect.width / 256;
                        bounds[p].anchoredPosition = new Vector2(b.xMin * s, -(256 - b.yMax) * s); bounds[p].sizeDelta = new Vector2(b.width, b.height) * s;
                    }
                }
            }
            foreach (var refresh in refreshControls) refresh();
        }
        void Update()
        {
            if (SettingsScreen.BlocksInput) return;
            if (dirty && Time.unscaledTime >= nextRefresh) { RefreshGlyph(); dirty = false; nextRefresh = Time.unscaledTime + .1f; }
            if (Input.GetKeyDown(KeyCode.Escape)) Back();
        }
    }
}
