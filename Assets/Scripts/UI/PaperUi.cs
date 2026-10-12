using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>Shared paper diorama tokens. All sizes are in the existing UI design coordinates.</summary>
    public static class PaperUi
    {
        public const float ButtonDepth = 6, PanelDepth = 9, BoardDepth = 12;
        public static readonly Color Edge = new Color32(163, 146, 115, 255);
        public static readonly Color Shadow = new Color(.13f, .10f, .065f, .22f);

        public static PaperFaceEffect Face(Image image)
        {
            var effect = image.GetComponent<PaperFaceEffect>();
            if (effect == null) effect = image.gameObject.AddComponent<PaperFaceEffect>();
            return effect;
        }
        public static void Surface(Image image, string name, float w, float h)
        {
            if (w < 24 || h < 18 || w > 1200 || h > 150 || image.color.a < .5f
                || name.Contains("Shadow") || name.Contains("Shade") || name.Contains("Dim")
                || name.Contains("Rule") || name.Contains("Outline") || name.Contains("Wash")) return;
            Face(image).SetDepth(h < 38 ? 2 : 4);
        }
        public static void Frame(Transform parent, float x, float y, float w, float h, float weight)
        {
            bool board = w > 1500 && h > 800;
            // Selection and focus outlines sit above a card's content, so a paper face there would hide it.
            if ((!board && parent.GetComponent<Graphic>() != null) || parent.name.Contains("Hover")
                || parent.name.Contains("Focus") || parent.name.Contains("Selected") || parent.name.Contains("Border")
                || parent.name.StartsWith("Stamp") || w < 90 || h < 58) return;
            var owner = parent as RectTransform;
            bool full = owner != null && Mathf.Abs(x) < .01f && Mathf.Abs(y) < .01f
                && Mathf.Abs(owner.rect.width - w) < 1 && Mathf.Abs(owner.rect.height - h) < 1;
            if (!board && !full && (weight < 1.5f || w < 240 || h < 70)) return;
            string name = $"PaperDepth_{x:0}_{y:0}_{w:0}_{h:0}";
            if (parent.Find(name) != null) return;
            var rect = UguiKit.Box(parent, name, x, y, w, h);
            if (full)
            {
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            var surface = rect.gameObject.AddComponent<PaperDepthGraphic>();
            surface.depth = board ? BoardDepth : h < 150 ? 3 : PanelDepth;
            surface.drawFace = !board;
            surface.fullPanel = full;
            surface.raycastTarget = false;
            rect.SetAsFirstSibling();
            // Root paper must remain behind the scene board, including full-screen overlays.
            if (board)
            {
                var paper = parent.Find("PrintedPaper");
                if (paper != null) rect.SetSiblingIndex(paper.GetSiblingIndex() + 1);
            }
        }
        public static void Card(RectTransform rect)
        {
            if (rect == null || rect.GetComponent<PaperCardMotion>() != null) return;
            rect.gameObject.AddComponent<PaperCardMotion>();
        }
        public static void Ink(Graphic graphic, float depth)
        {
            if (graphic == null) return;
            var effect = graphic.GetComponent<PaperInkRelief>() ?? graphic.gameObject.AddComponent<PaperInkRelief>();
            effect.depth = depth; graphic.SetVerticesDirty();
        }
        public static void Plinth(Transform parent, float x, float y, float w, float h = 32)
        {
            var rect = UguiKit.Box(parent, "DisplayPlinth", x, y, w, h);
            var surface = rect.gameObject.AddComponent<PaperDepthGraphic>();
            surface.depth = 11; surface.raycastTarget = false;
        }
    }
}
