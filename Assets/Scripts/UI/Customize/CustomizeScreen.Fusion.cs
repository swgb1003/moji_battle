using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed partial class CustomizeScreen
    {
        RawImage fusionBadge;
        UguiKit.ChoiceButton fusionEntry, normalCharacter;
        UguiKit.ChoiceButton normalOffhand;
        void BuildFusionControls(Transform root, Transform settings)
        {
            fusionEntry = RetroUi.Button(root, "CustomizeFusion", "文字合成・保存一覧 →", 1414, 159, 430, 78, 30, OpenFusion, RetroUi.Red);
            fusionBadge = RetroUi.Group(settings, "EquippedFusionGlyph", 303, 112, 96, 96).gameObject.AddComponent<RawImage>(); fusionBadge.raycastTarget = false; fusionBadge.color = RetroUi.Ink;
            normalCharacter = RetroUi.Button(settings, "NormalCharacter", "通常入力へ", 24, 126, 196, 34, 19,
                () => customizer.SetCharacter("一"), shadow: false);
            normalOffhand = RetroUi.Button(settings, "NormalOffhand", "左手を通常入力へ", 476, 112, 150, 96, 19,
                () => customizer.SetOffhandCharacter("月"), shadow: false);
        }
        void OpenFusion()
        {
            if (!customizer.CanUseFusion || FusionScreen.IsOpen) return;
            CancelGripDrag(); preview.EndGripEditing(); preview.gameObject.SetActive(false); previewCamera.enabled = false;
            FusionScreen.Open(build => customizer.SetFusion(build.fusion, build.fontType), () =>
            { if (this != null) { preview.gameObject.SetActive(true); previewCamera.enabled = true; Refresh(); } }, customizer.Current.HasFusion ? customizer.Current.fusion : null, customizer.Current.fontType);
        }
        void RefreshFusionControls()
        {
            if (fusionBadge == null) return;
            var b = customizer.Current;
            fusionEntry.button.gameObject.SetActive(customizer.CanUseFusion);
            charInput.gameObject.SetActive(!b.HasFusion); fusionBadge.gameObject.SetActive(b.HasFusion);
            normalCharacter.button.gameObject.SetActive(b.HasFusion && customizer.CanUseFusion);
            normalOffhand.button.gameObject.SetActive(b.IsDualWield && !b.IsSplitMing && b.OffhandFusion != null && customizer.Editable);
            charInput.transform.parent.Find("Section_文字").gameObject.SetActive(!b.HasFusion || !customizer.CanUseFusion);
            if (b.HasFusion && FusionGlyphs.TryGet(b.fusion, b.fontType, CombatBalance.Default, GlyphCalibration.Default, -1, out var g, out _))
            {
                fusionBadge.texture = g.texture;
                var hint = charError.transform.parent.Find("CharHint").GetComponent<Text>();
                hint.text = "合成文字：" + b.DisplayName + "\n部品：" + b.fusion.Left + " ＋ " + b.fusion.Right;
            }
            else charError.transform.parent.Find("CharHint").GetComponent<Text>().text = "好きな一文字を入力\n漢字は分離して二刀流にもできます";
            if (b.IsDualWield && b.OffhandFusion != null && !b.IsSplitMing)
            {
                offhandInput.gameObject.SetActive(false);
                offhandHint.text = "左手：" + b.OffhandDisplayName + "\n合成文字を装備中";
            }
        }
        static void RefreshFusionVs(FighterBuildData b, Text label) => FusionUi.Glyph(label, b);
    }
}
