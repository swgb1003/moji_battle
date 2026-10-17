using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public static class FusionUi
    {
        public static string Name(FighterLoadout loadout) => Safe(loadout.build?.DisplayName ?? loadout.grapheme);
        public static string Safe(string value) => value?.Replace('<', '〈').Replace('>', '〉');
        public static void Glyph(Text label, FighterBuildData build, bool weaponFlip = false)
        {
            var child = label.transform.Find("FusionGlyphDisplay"); var raw = child != null ? child.GetComponent<RawImage>() : null;
            if (build != null && build.HasFusion && FusionGlyphs.TryGet(build.fusion, build.fontType, CombatBalance.Default, GlyphCalibration.Default, -1, out var g, out _))
            {
                if (raw == null)
                {
                    raw = UguiKit.Fill(label.transform, "FusionGlyphDisplay").gameObject.AddComponent<RawImage>();
                    var aspect = raw.gameObject.AddComponent<AspectRatioFitter>(); aspect.aspectRatio = 1; aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                }
                raw.texture = (weaponFlip ? GlyphFlip.Apply(g, build.weaponFlip) : g).texture;
                raw.color = label.color; raw.raycastTarget = false; raw.enabled = true; label.enabled = false;
            }
            else { label.enabled = true; if (raw != null) raw.enabled = false; }
        }

        public static RectTransform Fighter(Transform parent, float x, float y, float scale, FighterLoadout loadout, Color color, bool mirrored = false)
        {
            RetroUi.Fighter(parent, x, y, scale, loadout.grapheme, color, mirrored);
            var figure = (RectTransform)parent.GetChild(parent.childCount - 1);
            // Invented characters have no Unicode glyph. Draw their saved mask, not the internal fallback character.
            Glyph(figure.Find("GlyphWeapon").GetComponent<Text>(), loadout.build, true);
            return figure;
        }
    }
}
