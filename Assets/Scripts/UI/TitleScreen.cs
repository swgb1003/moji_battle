using UnityEngine;

namespace MojiBattle
{
    /// <summary>タイトル画面。START でカスタマイズへ。</summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        void Start()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = FighterFactory.Paper;
                cam.clearFlags = CameraClearFlags.SolidColor;
            }
            var canvas = UguiKit.MakeCanvas("TitleCanvas");
            var root = canvas.transform;
            UguiKit.Panel(root, "Band", 0f, 250f, 1920f, 330f, FighterFactory.Ink);
            UguiKit.Label(root, "Title", "文字武器オートバトル", 0f, 270f, 1920f, 220f, 150, Color.white, TextAnchor.MiddleCenter);
            UguiKit.Label(root, "Sub", "文字を選び、持ち方と戦い方を決めて、あとは見守るだけ", 0f, 490f, 1920f, 70f, 34, Color.white, TextAnchor.MiddleCenter, false);
            var start = UguiKit.Choice(root, "Start", "START", 960f - 220f, 700f, 440f, 120f, 60, GameFlow.ToCustomize);
            start.image.color = FighterFactory.LeftColor;
            start.label.color = Color.white;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) GameFlow.ToCustomize();
        }
    }
}
