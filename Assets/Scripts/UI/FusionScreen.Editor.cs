using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed partial class FusionScreen
    {
        void Layout()
        {
            var left = Panel(page, "LayoutPreview", 76, 284, 964, 740);
            RetroUi.Frame(left, 18, 18, 928, 614, RetroUi.Rule, 1);
            for (int i = 1; i < 6; i++)
            {
                float x = 18 + i * 928f / 6, y = 18 + i * 614f / 6;
                RetroUi.Block(left, "GridV", x, 18, 1, 614, new Color(.45f, .4f, .3f, .2f));
                RetroUi.Block(left, "GridH", 18, y, 928, 1, new Color(.45f, .4f, .3f, .2f));
            }
            layoutCanvas = RetroUi.Group(left, "LayoutGlyphCanvas", 202, 36, 568, 568);
            leftImage = Mask(layoutCanvas, "LayoutLeft", null, 0, 0, 568, 568);
            rightImage = Mask(layoutCanvas, "LayoutRight", null, 0, 0, 568, 568);
            for (int p = 0; p < 2; p++)
            {
                var box = RetroUi.Group(layoutCanvas, "PartBounds" + p, 0, 0, 100, 100); bounds[p] = box;
                var hit = box.gameObject.AddComponent<Image>(); hit.color = Color.clear;
                box.gameObject.AddComponent<FusionPartHandle>().Bind(this, p == 1, false, Vector2.one);
                void Edge(string n, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size)
                {
                    var edge = RetroUi.Block(box, n, 0, 0, 0, 0, RetroUi.Red).rectTransform;
                    edge.anchorMin = min; edge.anchorMax = max; edge.pivot = pivot; edge.sizeDelta = size; edge.anchoredPosition = Vector2.zero;
                }
                Edge("Top", new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), new Vector2(0, 2));
                Edge("Bottom", Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), new Vector2(0, 2));
                Edge("Left", Vector2.zero, new Vector2(0, 1), new Vector2(0, .5f), new Vector2(2, 0));
                Edge("Right", new Vector2(1, 0), Vector2.one, new Vector2(1, .5f), new Vector2(2, 0));
                foreach (float x in new[] { 0f, 1f }) foreach (float y in new[] { 0f, 1f })
                {
                    var handle = RetroUi.Block(box, "ResizeHandle", 0, 0, 14, 14, RetroUi.Red);
                    var rt = handle.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(x, y); rt.pivot = Vector2.one * .5f; rt.anchoredPosition = Vector2.zero;
                    handle.raycastTarget = true; handle.gameObject.AddComponent<FusionPartHandle>().Bind(this, p == 1, true, new Vector2(x * 2 - 1, y * 2 - 1));
                }
            }
            Text(left, "DragHint", "部品をドラッグして移動 ／ 四隅でサイズを調整", 20, 614, 924, 36, 23, RetroUi.Muted, TextAnchor.MiddleCenter);
            RetroUi.Button(left, "ResetLayout", "元の配置に戻す", 34, 666, 432, 54, 30, () => { recipe.ResetLayout(); RefreshGlyph(); });
            RetroUi.Button(left, "ReselectParts", "部品を選び直す", 494, 666, 432, 54, 30, () => Show(Page.Parts));
            var right = Panel(page, "LayoutControls", 1058, 284, 786, 740);
            var lt = RetroUi.Button(right, "EditLeft", recipe.FirstRole + "：" + recipe.Left, 16, 18, 370, 74, 36, () => SelectLayoutPart(false));
            var rt2 = RetroUi.Button(right, "EditRight", recipe.SecondRole + "：" + recipe.Right, 400, 18, 370, 74, 36, () => SelectLayoutPart(true));
            refreshControls.Add(() => { RetroUi.Selected(lt, !selectedRight, RetroUi.Red); RetroUi.Selected(rt2, selectedRight, RetroUi.Red); });
            var layouts = new[] { "左右に並べる", "上下に並べる", "囲み・重ねる" };
            for (int i = 0; i < layouts.Length; i++)
            {
                var target = (KanjiLayout)i;
                var button = RetroUi.Button(right, "FusionLayout" + i, layouts[i], 24 + i * 252, 106, 236, 42, 25,
                    () => { recipe.version = 2; recipe.layout = target; Show(Page.Layout); });
                RetroUi.Selected(button, recipe.layout == target, RetroUi.Red);
            }
            LayoutSlider(right, "Width", "横幅", 160, .7f, 1.3f, () => selectedRight ? recipe.rightWidth : recipe.leftWidth,
                v => { if (selectedRight) recipe.rightWidth = v; else recipe.leftWidth = v; }, v => Mathf.RoundToInt(v * 100) + "%");
            LayoutSlider(right, "Height", "高さ", 224, .7f, 1.3f, () => selectedRight ? recipe.rightHeight : recipe.leftHeight,
                v => { if (selectedRight) recipe.rightHeight = v; else recipe.leftHeight = v; }, v => Mathf.RoundToInt(v * 100) + "%");
            LayoutSlider(right, "Horizontal", "左右位置", 288, -20, 20, () => selectedRight ? recipe.rightX : recipe.leftX,
                v => { if (selectedRight) recipe.rightX = v; else recipe.leftX = v; }, v => v.ToString("0"));
            LayoutSlider(right, "Vertical", "上下位置", 352, -24, 24, () => selectedRight ? recipe.rightY : recipe.leftY,
                v => { if (selectedRight) recipe.rightY = v; else recipe.leftY = v; }, v => v.ToString("0"));
            LayoutSlider(right, "Gap", "部品の間隔", 416, 0, 24, () => recipe.gap, v => recipe.gap = v, v => v.ToString("0"));
            RetroUi.Block(right, "FontRule", 20, 502, 746, 1, RetroUi.Rule);
            Text(right, "FontLabel", "字体", 22, 520, 80, 55, 30);
            for (int i = 0; i < 4; i++)
            {
                var f = (FontStyleId)i;
                var b = RetroUi.Button(right, "FusionFont" + i, GlyphCatalog.FontDisplayName(f), 112 + i * 162, 520, 150, 58, 25,
                    () => { font = f; RefreshGlyph(); });
                refreshControls.Add(() => RetroUi.Selected(b, font == f, RetroUi.Red));
            }
            Text(right, "ResultLabel", "合成後の文字", 24, 606, 180, 36, 23);
            layoutMini = Mask(right, "LayoutMini", null, 30, 638, 90, 90);
            RetroUi.Button(right, "FusionConfirm", "完成を確認 →", 224, 632, 538, 84, 44, BeginBirth, RetroUi.Red);
            SelectLayoutPart(selectedRight);
        }
        void LayoutSlider(Transform parent, string name, string label, float y, float min, float max, Func<float> get, Action<float> set, Func<float, string> format)
        {
            Text(parent, name + "Label", label, 24, y, 160, 48, 28);
            Text(parent, name + "Value", "", 654, y, 110, 48, 28, null, TextAnchor.MiddleCenter);
            var value = parent.Find(name + "Value").GetComponent<Text>();
            var slider = UguiKit.SliderBar(parent, "Fusion" + name, 192, y, 436, 48, null);
            slider.minValue = min; slider.maxValue = max; slider.SetValueWithoutNotify(get());
            slider.transform.Find("FillArea/Fill").GetComponent<Image>().color = RetroUi.Red;
            slider.transform.Find("Track").GetComponent<Image>().color = RetroUi.Rule;
            slider.onValueChanged.AddListener(v => { set(Mathf.Round(v * 100) / 100); value.text = format(get()); dirty = true; });
            refreshControls.Add(() => { slider.SetValueWithoutNotify(get()); value.text = format(get()); });
        }
        public void SelectLayoutPart(bool right)
        {
            selectedRight = right;
            if (leftImage != null) leftImage.color = right ? RetroUi.Ink : RetroUi.Red;
            if (rightImage != null) rightImage.color = right ? RetroUi.Red : RetroUi.Ink;
            for (int p = 0; p < 2; p++) if (bounds[p] != null)
                foreach (var img in bounds[p].GetComponentsInChildren<Image>()) if (img.transform != bounds[p]) img.color = (p == 1) == right ? RetroUi.Red : RetroUi.Muted;
            foreach (var refresh in refreshControls) refresh();
        }
        public Vector2 LayoutLocal(Vector2 screen, Camera eventCamera)
        { RectTransformUtility.ScreenPointToLocalPointInRectangle(layoutCanvas, screen, eventCamera, out var p); return p * (256f / layoutCanvas.rect.width); }
        public void TransformPart(bool right, bool resize, Vector2 delta, Vector2 direction, FusionData initial)
        {
            if (resize)
            {
                float width = Mathf.Clamp((right ? initial.rightWidth : initial.leftWidth) + delta.x * direction.x / 110, .7f, 1.3f);
                float height = Mathf.Clamp((right ? initial.rightHeight : initial.leftHeight) + delta.y * direction.y / 180, .7f, 1.3f);
                if (right) { recipe.rightWidth = Mathf.Round(width * 100) / 100; recipe.rightHeight = Mathf.Round(height * 100) / 100; }
                else { recipe.leftWidth = Mathf.Round(width * 100) / 100; recipe.leftHeight = Mathf.Round(height * 100) / 100; }
            }
            else
            {
                float x = Mathf.Round(Mathf.Clamp((right ? initial.rightX : initial.leftX) + delta.x, -20, 20));
                float y = Mathf.Round(Mathf.Clamp((right ? initial.rightY : initial.leftY) + delta.y, -24, 24));
                if (right) { recipe.rightX = x; recipe.rightY = y; } else { recipe.leftX = x; recipe.leftY = y; }
            }
            dirty = true;
        }
        public void BeginBirth()
        {
            if (BirthPlaying || !Whole(out var whole)) return;
            dirty = false; RefreshGlyph();
            FusionGlyphs.TryGet(recipe, font, CombatBalance.Default, GlyphCalibration.Default, 0, out var l, out _);
            FusionGlyphs.TryGet(recipe, font, CombatBalance.Default, GlyphCalibration.Default, 1, out var r, out _);
            page.gameObject.SetActive(false);
            birth = FusionBirthEffect.Play(root, l, r, whole, () => { birth = null; Show(Page.Finish); });
        }
        void Finish()
        {
            var left = Panel(page, "WeaponPreview", 76, 286, 860, 738);
            RetroUi.Block(left, "WeaponBadge", 12, 12, 368, 70, RetroUi.Red);
            Text(left, "WeaponTitle", "武器プレビュー", 32, 12, 328, 70, 35, RetroUi.Paper);
            var scenery = StageUi.Preview(left, BattleStageId.Calligraphy, 18, 94, 824, 494, false);
            weaponPreview = scenery.gameObject.AddComponent<FusionWeaponPreview>();
            weaponPreview.Init(new FusionRecord { font = font, recipe = recipe.Clone() }.Build(), 824, 494);
            Whole(out var glyph); var glyphOnly = Mask(left, "FinishGlyphOnly", glyph, 180, 100, 490, 490); glyphOnly.gameObject.SetActive(false);
            RetroUi.Button(left, "ToggleGlyph", "字形を見る", 548, 22, 282, 52, 28, () =>
            {
                bool show = !glyphOnly.gameObject.activeSelf; glyphOnly.gameObject.SetActive(show); scenery.gameObject.SetActive(!show);
                page.Find("WeaponPreview/ToggleGlyph/Label").GetComponent<Text>().text = show ? "武器を見る" : "字形を見る";
            });
            Text(left, "MaterialLeft", recipe.leftSource + " の " + recipe.Left, 28, 614, 350, 90, 36, null, TextAnchor.MiddleCenter);
            Text(left, "MaterialPlus", "+", 380, 614, 100, 90, 46, RetroUi.Muted, TextAnchor.MiddleCenter);
            Text(left, "MaterialRight", recipe.rightSource + " の " + recipe.Right, 480, 614, 350, 90, 36, null, TextAnchor.MiddleCenter);
            var right = Panel(page, "FinishDetails", 964, 286, 880, 738);
            Text(right, "NameLabel", "文字の名前", 24, 22, 212, 86, 35);
            nameInput = UguiKit.InputBox(right, "FusionNameInput", 254, 22, 600, 86, 44, null);
            nameInput.characterLimit = 24; nameInput.textComponent.font = WorldCupDesign.HeadingFont;
            nameInput.textComponent.fontStyle = FontStyle.Normal; nameInput.textComponent.supportRichText = false;
            nameInput.SetTextWithoutNotify(recipe.name);
            nameInput.placeholder.GetComponent<Text>().text = "名前を入力（12文字まで）";
            nameInput.onValueChanged.AddListener(v => { recipe.name = v.Trim(); if (status != null) status.text = ""; });
            WebCharacterInput.Attach(nameInput, () => IsOpen && CurrentPage == Page.Finish && !BirthPlaying && deleteDialog == null && !SettingsScreen.IsOpen,
                () => 0, "作った文字の名前", "名前を入力（12文字まで）", 12, .65f);
            RetroUi.Frame(right, 24, 130, 832, 346, RetroUi.Rule, 1);
            Mask(right, "FinishGlyph", glyph, 30, 142, 260, 260);
            Text(right, "FusionType", recipe.LayoutLabel, 302, 144, 520, 52, 36);
            Text(right, "FusionFontLabel", "字体：" + GlyphCatalog.FontDisplayName(font), 302, 202, 520, 46, 30);
            var st = StatCalculator.Compute(glyph, CombatBalance.Default);
            StatRow(right, "攻撃", 270, st.attack); StatRow(right, "速さ", 328, st.speed); StatRow(right, "重さ", 386, st.weightScore);
            Text(right, "SplitHint", "あとから部品に分離できます。", 28, 480, 824, 38, 24, RetroUi.Muted);
            RetroUi.Button(right, "FusionSaveEquip", "保存して装備する →", 24, 528, 832, 84, 42, () => Save(true), RetroUi.Red);
            RetroUi.Button(right, "FusionSave", editingId == null ? "保存のみ" : "上書き保存", 24, 632, 406, 66, 34, () => Save(false));
            RetroUi.Button(right, "ReturnLayout", "配置へ戻る", 450, 632, 406, 66, 34, () => Show(Page.Layout));
            status = Text(right, "FusionStatus", message, 28, 704, 824, 28, 20, RetroUi.Red); message = "";
        }
        static void StatRow(Transform parent, string title, float y, int value)
        {
            Text(parent, title + "Label", title, 304, y, 118, 42, 29);
            int bars = Mathf.Clamp(Mathf.CeilToInt(value / 100f * 6), 1, 6);
            for (int i = 0; i < 6; i++) RetroUi.Block(parent, "StatBar", 432 + i * 60, y + 7, 55, 28, i < bars ? RetroUi.Red : RetroUi.Wash);
            Text(parent, title + "Value", value.ToString(), 796, y, 56, 42, 23, RetroUi.Muted, TextAnchor.MiddleCenter);
        }
        public bool Save(bool equipNow)
        {
            if (BirthPlaying || CurrentPage != Page.Finish) return false;
            recipe.name = nameInput != null ? nameInput.text.Trim() : recipe.name;
            if (!FusionLibrary.Save(recipe, font, editingId, out var record, out var error)) { status.text = error ?? "字体を選んでください。"; return false; }
            editingId = selectedId = record.id; recipe = record.recipe.Clone();
            if (equipNow) Equip(record); else { message = "「" + record.recipe.DisplayName + "」を保存しました。"; libraryPage = FusionLibrary.All().FindIndex(r => r.id == selectedId) / 3; Show(Page.Library); }
            return true;
        }
        void Equip(FusionRecord record)
        {
            var build = record.Build(); Close(); equip?.Invoke(build);
        }
        void Library()
        {
            RetroUi.Button(page, "LibraryNew", "＋ 新しく作る", 1504, 159, 340, 78, 30, NewCharacter);
            var all = FusionLibrary.All(); int pages = Mathf.Max(1, (all.Count + 2) / 3); libraryPage = Mathf.Clamp(libraryPage, 0, pages - 1);
            var selected = all.Find(r => r.id == selectedId);
            if (selected == null && all.Count > 0) { selected = all[libraryPage * 3]; selectedId = selected.id; }
            for (int i = libraryPage * 3; i < Mathf.Min(all.Count, libraryPage * 3 + 3); i++)
            {
                var item = all[i]; float x = 76 + (i % 3) * 400;
                var card = Panel(page, "Saved_" + item.id, x, 282, 382, 482);
                RetroUi.Button(card, "SelectSaved", "", 0, 0, 382, 482, 24, () => { selectedId = item.id; Show(Page.Library); }, shadow: false);
                RetroUi.Frame(card, 10, 10, 362, 462, RetroUi.Rule, 1);
                if (item.id == selectedId) { RetroUi.Frame(card, 0, 0, 382, 482, RetroUi.Red, 4); Text(card, "Selected", "✓", 18, 12, 50, 54, 36, RetroUi.Red); }
                FusionGlyphs.TryGet(item.recipe, item.font, CombatBalance.Default, GlyphCalibration.Default, -1, out var g, out _);
                Mask(card, "SavedGlyph", g, 30, 50, 322, 300);
                Text(card, "SavedName", item.recipe.DisplayName, 18, 350, 346, 58, 36, null, TextAnchor.MiddleCenter);
                Text(card, "SavedParts", item.recipe.Left + " ＋ " + item.recipe.Right, 18, 416, 346, 48, 28, null, TextAnchor.MiddleCenter);
            }
            if (all.Count == 0)
            {
                Text(page, "LibraryEmpty", "まだ文字が保存されていません", 100, 360, 1120, 72, 42, RetroUi.Muted, TextAnchor.MiddleCenter);
                Text(page, "LibraryEmptyHint", "部品を組み合わせて、最初の文字を作ろう。", 100, 452, 1120, 54, 28, RetroUi.Muted, TextAnchor.MiddleCenter);
            }
            RetroUi.Button(page, "LibraryCreateTile", "＋　新しく作る", 76, 794, 522, 154, 37, NewCharacter);
            Pager(page, "Library", 632, 842, 626, libraryPage, pages, p => { libraryPage = p; selectedId = all[p * 3].id; Show(Page.Library); });
            Text(page, "LibraryCount", all.Count + " / " + FusionLibrary.Capacity + " 文字を保存", 76, 976, 1182, 40, 23, RetroUi.Muted);
            var details = Panel(page, "LibraryDetails", 1284, 282, 560, 742);
            RetroUi.Block(details, "SelectedBadge", 12, 12, 536, 56, RetroUi.Red);
            Text(details, "SelectedTitle", "選択中の文字", 30, 12, 500, 56, 29, RetroUi.Paper);
            if (selected != null)
            {
                var item = selected;
                FusionGlyphs.TryGet(item.recipe, item.font, CombatBalance.Default, GlyphCalibration.Default, -1, out var g, out _);
                Mask(details, "SelectedExtrusion", g, 130, 95, 320, 290, new Color32(98, 27, 20, 255));
                Mask(details, "SelectedGlyph", g, 122, 87, 320, 290, RetroUi.Red);
                Text(details, "SelectedName", item.recipe.DisplayName, 28, 380, 504, 70, 42, null, TextAnchor.MiddleCenter);
                RetroUi.Block(details, "DetailRule", 28, 452, 504, 1, RetroUi.Rule);
                Text(details, "SelectedSources", item.recipe.Left + "（" + item.recipe.leftSource + "） ＋ " + item.recipe.Right + "（" + item.recipe.rightSource + "）\n" + GlyphCatalog.FontDisplayName(item.font) + " ／ " + item.recipe.LayoutLabel, 28, 460, 504, 80, 25, null, TextAnchor.MiddleCenter);
                RetroUi.Button(details, "LibraryEquip", "装備する →", 28, 548, 504, 76, 40, () => Equip(item), RetroUi.Red);
                RetroUi.Button(details, "LibraryEdit", "編集する", 28, 640, 504, 58, 31, () =>
                { recipe = item.recipe.Clone(); font = item.font; editingId = item.id; Show(Page.Layout); });
                RetroUi.Button(details, "LibraryDelete", "削除", 178, 704, 204, 26, 20, () => ConfirmDelete(item), shadow: false);
            }
            else Text(details, "SelectedEmpty", "作った文字が\nここに並びます", 28, 240, 504, 160, 40, RetroUi.Muted, TextAnchor.MiddleCenter);
            if (!string.IsNullOrEmpty(message)) Text(page, "LibraryNotice", message, 600, 976, 658, 40, 23, RetroUi.Red, TextAnchor.MiddleRight);
            message = "";
        }
        void ConfirmDelete(FusionRecord record)
        {
            if (deleteDialog != null) return;
            var group = page.GetComponent<CanvasGroup>();
            if (group == null) group = page.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            var shade = RetroUi.Block(root, "FusionDeleteDialog", 0, 0, 1920, 1080, new Color(0, 0, 0, .65f)); shade.raycastTarget = true; deleteDialog = shade.gameObject;
            var box = Panel(shade.transform, "DeleteBox", 460, 330, 1000, 420);
            Text(box, "DeleteTitle", "この文字を削除しますか？", 38, 32, 924, 80, 44, null, TextAnchor.MiddleCenter);
            Text(box, "DeleteExplanation", "「" + record.recipe.DisplayName + "」を保存一覧から削除します。\n装備中の文字はそのまま残ります。", 38, 128, 924, 100, 28, null, TextAnchor.MiddleCenter);
            RetroUi.Button(box, "DeleteCancel", "キャンセル", 46, 290, 430, 76, 34, CloseDeleteDialog);
            RetroUi.Button(box, "DeleteConfirm", "削除する", 522, 290, 430, 76, 34, () =>
            { FusionLibrary.Delete(record.id); CloseDeleteDialog(); selectedId = null; Show(Page.Library); }, RetroUi.Red);
        }
    }

    public sealed class FusionPartHandle : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        FusionScreen screen; bool right, resize; Vector2 direction, start; FusionData initial;
        public void Bind(FusionScreen owner, bool rightPart, bool scale, Vector2 corner) { screen = owner; right = rightPart; resize = scale; direction = corner; }
        public void OnPointerDown(PointerEventData e) { if (!SettingsScreen.BlocksInput) screen.SelectLayoutPart(right); }
        public void OnBeginDrag(PointerEventData e) { start = screen.LayoutLocal(e.position, e.pressEventCamera); initial = screen.Recipe; }
        public void OnDrag(PointerEventData e) { if (initial != null && !SettingsScreen.BlocksInput) screen.TransformPart(right, resize, screen.LayoutLocal(e.position, e.pressEventCamera) - start, direction, initial); }
        public void OnEndDrag(PointerEventData e) { OnDrag(e); initial = null; }
    }

    /// <summary>Uses the actual fighter/weapon renderer on an isolated preview layer.</summary>
    public sealed class FusionWeaponPreview : MonoBehaviour
    {
        FighterPreviewController preview; Camera cam; RenderTexture texture;
        public void Init(FighterBuildData build, int width, int height)
        {
            var p = new GameObject("FusionFighterPreview");
            preview = p.AddComponent<FighterPreviewController>(); preview.feet = Vector2.zero; preview.showGuides = false; preview.Paused = true;
            preview.Show(build, 0);
            foreach (var child in p.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 29;
            var c = new GameObject("FusionPreviewCamera"); cam = c.AddComponent<Camera>();
            cam.orthographic = true; cam.cullingMask = 1 << 29; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.clear;
            float visible = Mathf.Max(2.65f, preview.Geometry.maxSide + 1.6f); cam.orthographicSize = visible * .5f; cam.aspect = width / (float)height;
            cam.transform.position = new Vector3(.25f, visible * .5f - .14f, -10);
            texture = new RenderTexture(width, height, 24) { antiAliasing = 4, name = "FusionWeaponPreview" }; cam.targetTexture = texture;
            var raw = UguiKit.Fill(transform, "LiveFighter").gameObject.AddComponent<RawImage>(); raw.texture = texture; raw.raycastTarget = false;
        }
        void OnEnable() { if (preview != null) preview.gameObject.SetActive(true); if (cam != null) cam.enabled = true; }
        void OnDisable() { if (preview != null) preview.gameObject.SetActive(false); if (cam != null) cam.enabled = false; }
        void OnDestroy()
        {
            if (preview != null) Destroy(preview.gameObject);
            if (cam != null) { cam.targetTexture = null; Destroy(cam.gameObject); }
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}
