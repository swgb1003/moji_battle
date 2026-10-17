using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed partial class CustomizeScreen
    {
        UguiKit.ChoiceButton handMain, handOff, handSwap, splitButton;
        UguiKit.ChoiceButton splitCatalogButton;
        GameObject splitCatalogPanel;
        public bool SplitCatalogVisible => splitCatalogPanel != null;

        void BuildSplitCatalogButton(Transform settings)
        {
            charError.rectTransform.sizeDelta = new Vector2(316, 42);
            splitCatalogButton = RetroUi.Button(settings, "SplitCatalog", "分離できる字 →", 806, challenge ? 150 : 177, 190, 34, 20,
                () => ShowSplitCatalog(KanjiLayout.LeftRight), shadow: false);
        }

        public void ShowSplitCatalog(KanjiLayout layout)
        {
            if (!customizer.Editable || !TextInputVisible || !FighterBuildData.DualWieldAllowed(customizer.Current.weaponSize)) return;
            CloseSplitCatalog(); CancelGripDrag(); preview.EndGripEditing();
            var overlay = RetroUi.Group(menuRoot, "SplitCatalogOverlay", 0, 0, 1920, 1080);
            splitCatalogPanel = overlay.gameObject;
            var blocker = RetroUi.Button(overlay, "SplitCatalogBlocker", "", 0, 0, 1920, 1080, 20, CloseSplitCatalog, shadow: false);
            blocker.image.color = new Color(0, 0, 0, .5f);
            var panel = RetroUi.Group(overlay, "SplitCatalogPaper", 160, 150, 1600, 800);
            var background = UguiKit.Panel(panel, "Paper", 0, 0, 1600, 800, RetroUi.Paper);
            background.raycastTarget = true;
            RetroUi.Frame(panel, 0, 0, 1600, 800, RetroUi.Ink, 3);
            RetroUi.Label(panel, "Heading", $"分離できる漢字 / {SplitKanjiCatalog.Entries.Length}字", 40, 22, 1260, 64, 40);
            RetroUi.Button(panel, "CloseSplitCatalog", "閉じる ×", 1370, 30, 190, 48, 26, CloseSplitCatalog, shadow: false);
            for (int i = 0; i < 3; i++)
            {
                var category = (KanjiLayout)i;
                string name = i == 0 ? "左右：明 → 日・月" : i == 1 ? "上下：思 → 田・心" : "囲み：国 → 囗・玉";
                var tab = RetroUi.Button(panel, "SplitLayout_" + i, name, 40 + 510 * i, 106, 500, 54, 28,
                    () => { CloseSplitCatalog(); ShowSplitCatalog(category); }, shadow: false);
                RetroUi.Selected(tab, layout == category, RetroUi.Red);
            }
            int index = 0;
            foreach (var entry in SplitKanjiCatalog.Entries)
            {
                if (entry.Layout != layout) continue;
                string ch = entry.Character;
                var button = RetroUi.Button(panel, "SplitPick_" + ch, ch, 40 + (index % 13) * 117,
                    180 + (index / 13) * 65, 108, 56, 38, () =>
                    {
                        customizer.SetCharacter(ch); customizer.SetSplitMing(true); CloseSplitCatalog();
                    }, shadow: false);
                button.button.interactable = FighterCustomizer.IsAvailable(ch, customizer.Current.fontType, out _);
                index++;
            }
            RetroUi.Label(panel, "Help", "文字を選ぶと分離して装備します（S・Mサイズ）。分離後も左右の握りを調整できます。", 40, 728, 1520, 44, 25, RetroUi.Muted);
        }

        public void CloseSplitCatalog()
        {
            if (splitCatalogPanel != null)
            {
                splitCatalogPanel.SetActive(false); Destroy(splitCatalogPanel); splitCatalogPanel = null;
                RefreshDualGripControls();
            }
        }

        void BuildDualGripControls(Transform panel)
        {
            handMain = RetroUi.Button(panel, "GripHandMain", "右手の握り", 24, 470, 210, 38, 22, () => SelectGripHand(false), shadow: false);
            handOff = RetroUi.Button(panel, "GripHandOff", "左手の握り", 248, 470, 210, 38, 22, () => SelectGripHand(true), shadow: false);
            handSwap = RetroUi.Button(panel, "SwapHands", "左右交換", 472, 470, 210, 38, 22, () => customizer.SwapHands(), shadow: false);
        }

        public void SelectGripHand(bool offhand)
        {
            CancelGripDrag(); preview.EndGripEditing();
            preview.SelectHand(offhand);
            RefreshDualGripControls();
        }

        void ResetSelectedGrip()
        {
            if (preview.EditingOffhand) customizer.ResetOffhandGrip();
            else customizer.ResetGrip();
        }

        void RefreshDualGripControls()
        {
            var b = customizer.Current;
            bool dual = b.IsDualWield;
            handMain.button.gameObject.SetActive(dual);
            handOff.button.gameObject.SetActive(dual);
            handSwap.button.gameObject.SetActive(dual);
            menuRoot.Find("PreviewPanel/PickHint").gameObject.SetActive(!dual);
            handMain.label.text = "右手「" + b.MainWeaponLabel + "」";
            handOff.label.text = "左手「" + b.OffhandWeaponLabel + "」";
            handMain.button.interactable = handOff.button.interactable = customizer.Editable;
            handSwap.button.interactable = customizer.Editable && (b.IsSplitMing || (!draft
                && (!run || string.IsNullOrEmpty(RunRuntimeStore.Current.lockedGlyph))
                && (!challenge || ChallengeRuntimeStore.Current.FixedCharacter == null)));
            RetroUi.Selected(handMain, !preview.EditingOffhand, RetroUi.Team(customizer.Side));
            RetroUi.Selected(handOff, preview.EditingOffhand, RetroUi.Team(customizer.Side));
            var selected = preview.EditingOffhand ? b.ForOffhand() : b;
            gripValue.text = (dual ? (preview.EditingOffhand ? "左手 / " : "右手 / ") : "") + CustomizeLabels.GripPlace(selected);
            if (challenge)
            {
                gripValue.gameObject.SetActive(dual);
                var reset = menuRoot.Find("PreviewPanel/GripReset"); reset.gameObject.SetActive(dual);
                Move(gripValue.transform,24,412); Move(reset,480,412);
            }
            bool supported = SplitKanjiCatalog.TryGet(b.character, out var parts) || b.HasFusion;
            splitCatalogButton.button.interactable = customizer.Editable && TextInputVisible && FighterBuildData.DualWieldAllowed(b.weaponSize);
            splitButton.button.interactable = customizer.Editable && supported && FighterBuildData.DualWieldAllowed(b.weaponSize);
            splitButton.label.text = b.IsSplitMing ? "分離を解除" : b.HasFusion ? b.fusion.Left + "・" + b.fusion.Right + "に分離" : supported ? parts.Left + "・" + parts.Right + "に分離" : "分離：対応漢字のみ";
            RetroUi.Selected(splitButton, b.IsSplitMing, RetroUi.Team(customizer.Side));
        }
    }
}
