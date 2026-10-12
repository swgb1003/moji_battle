using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed class TitleScreen : MonoBehaviour
    {
        void Start()
        {
            if (Camera.main != null) Camera.main.backgroundColor = RetroUi.Paper;
            var root = RetroUi.Screen("TitleCanvas", out _);
            RetroUi.Block(root, "HeaderRule", 100, 83, 580, 3, RetroUi.Ink);
            RetroUi.Block(root, "HeaderRule", 100, 91, 580, 1, RetroUi.Ink);
            RetroUi.Label(root, "Brand", "M O J I  F i g h t e r  X", 684, 48, 552, 75, 30, null, TextAnchor.MiddleCenter);
            RetroUi.Block(root, "HeaderRule", 1240, 83, 580, 3, RetroUi.Ink);
            RetroUi.Block(root, "HeaderRule", 1240, 91, 580, 1, RetroUi.Ink);
            var logo = RetroUi.Group(root, "TitleLogo", 344, 142, 1232, 510).gameObject.AddComponent<RawImage>();
            logo.texture = Resources.Load<Texture2D>("UI/MojiFighterXLogo");
            logo.raycastTarget = false;
            PaperUi.Ink(logo, 10);
            if (logo.texture == null)
            {
                logo.enabled = false;
                RetroUi.Label(root, "Title", "MOJI\nFighter X", 350, 145, 1220, 500, 200, null, TextAnchor.MiddleCenter);
            }
            RetroUi.Enter(logo.gameObject, .04f, 45);
            var sub = RetroUi.Label(root, "Sub", "— 最強の文字使いは君だ —", 400, 624, 1120, 76, 39, null, TextAnchor.MiddleCenter);
            RetroUi.Enter(sub.gameObject, .16f);
            RetroUi.Fighter(root, 58, 484, 1.16f, "火", RetroUi.Red);
            RetroUi.Fighter(root, 1450, 484, 1.16f, "山", RetroUi.Blue, true);
            var cta = RetroUi.Group(root, "StartArea", 645, 778, 630, 176);
            RetroUi.Button(cta, "Start", "S T A R T  →", 0, 0, 604, 126, 62, StartGame, RetroUi.Ink, false);
            RetroUi.Label(cta, "KeyboardHint", "E N T E R  T O  S T A R T", 0, 146, 604, 35, 20, null, TextAnchor.MiddleCenter);
            RetroUi.Enter(cta.gameObject, .24f, 30);
            RetroUi.Block(root, "Baseline", 68, 979, 1784, 5, RetroUi.Ink);
            RetroUi.Label(root, "Version", "VER. " + Application.version, 80, 990, 480, 43, 20);
            RetroUi.Label(root, "MotionHint", "F6  動きを抑える", 1320, 990, 520, 43, 20, null, TextAnchor.MiddleRight);
        }
        void Update()
        {
            if (SettingsScreen.BlocksInput) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) StartGame();
        }
        public void StartGame()
        {
            if (SettingsScreen.BlocksInput) return;
            ScreenAudioDirector.PlayTitleStart();
            GameFlow.ToModeSelect();
        }
    }
}
