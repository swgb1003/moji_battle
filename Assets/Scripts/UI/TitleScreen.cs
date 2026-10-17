using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed class TitleScreen : MonoBehaviour
    {
        public bool Starting { get; private set; }
        TitleFighterMotion left, right;
        CanvasGroup startArea, impact;
        Button startButton;

        void Start()
        {
            if (Camera.main != null) Camera.main.backgroundColor = RetroUi.Paper;
            var root = RetroUi.Screen("TitleCanvas", out _);
            var paper = root.Find("PrintedPaper").GetComponent<RawImage>();
            var titlePaper = Resources.Load<Texture2D>("UI/Title/TitlePaper");
            if (titlePaper != null) paper.texture = titlePaper;
            // Only a handful of fine action marks accompany the two printed brush swashes.
            ActionMarks(root, false); ActionMarks(root, true);
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
            left = TitleFighterMotion.Create(root, 58, false);
            right = TitleFighterMotion.Create(root, 1450, true);
            var cta = RetroUi.Group(root, "StartArea", 645, 778, 630, 176);
            var redOffset = RetroUi.Group(cta, "StartRedOffset", 8, 9, 604, 126).gameObject.AddComponent<Image>();
            redOffset.color = RetroUi.Red; redOffset.raycastTarget = false;
            startButton = RetroUi.Button(cta, "Start", "S T A R T  →", 0, 0, 604, 126, 62, StartGame, RetroUi.Ink, false).button;
            // The approved title uses a flat printed button, with its red offset fully visible.
            var startFace = startButton.GetComponent<PaperFaceEffect>();
            if (startFace != null) startFace.enabled = false;
            RetroUi.Label(cta, "KeyboardHint", "E N T E R  T O  S T A R T", 0, 146, 604, 35, 20, null, TextAnchor.MiddleCenter);
            RetroUi.Enter(cta.gameObject, .24f, 30);
            startArea = cta.GetComponent<CanvasGroup>();
            RetroUi.Block(root, "Baseline", 68, 979, 1784, 5, RetroUi.Ink);
            RetroUi.Label(root, "Version", "VER. " + Application.version, 80, 990, 480, 43, 20);
            RetroUi.Label(root, "MotionHint", "F6  動きを抑える", 1320, 990, 520, 43, 20, null, TextAnchor.MiddleRight);
            var clash = RetroUi.Group(root, "TitleClash", 0, 0, 1920, 1080);
            impact = clash.gameObject.AddComponent<CanvasGroup>(); impact.alpha = 0;
            impact.interactable = impact.blocksRaycasts = false;
            foreach (float direction in new[] { -1f, 1f })
            {
                RetroUi.Line(clash, new Vector2(960 + direction * 25, 704), new Vector2(960 + direction * 57, 671), RetroUi.Ink, 3);
                RetroUi.Line(clash, new Vector2(960 + direction * 30, 751), new Vector2(960 + direction * 63, 774), RetroUi.Ink, 3);
            }
        }

        static void ActionMarks(Transform root, bool mirrored)
        {
            Vector2 P(float x, float y) => new Vector2(mirrored ? 1920 - x : x, y);
            RetroUi.Line(root, P(126, 466), P(168, 513), RetroUi.Ink, 2.5f);
            RetroUi.Line(root, P(164, 480), P(205, 535), RetroUi.Ink, 1.5f);
            RetroUi.Line(root, P(405, 696), P(461, 752), RetroUi.Ink, 2.5f);
            RetroUi.Line(root, P(436, 705), P(479, 746), RetroUi.Ink, 1.5f);
        }
        void Update()
        {
            if (Starting || SettingsScreen.BlocksInput) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) StartGame();
        }
        public void StartGame()
        {
            if (Starting || SettingsScreen.BlocksInput) return;
            Starting = true;
            startButton.interactable = false;
            if (RetroMotionSettings.Reduced) { FinishStart(); return; }
            StartCoroutine(StartClash());
        }

        IEnumerator StartClash()
        {
            // Own the CTA after its entry reveal, including when START is pressed immediately.
            var reveal = startArea.GetComponent<RetroReveal>();
            if (reveal != null) reveal.enabled = false;
            float elapsed = 0;
            const float driveSeconds = .55f;
            while (elapsed < driveSeconds)
            {
                if (RetroMotionSettings.Reduced) { FinishStart(); yield break; }
                if (!SettingsScreen.BlocksInput) elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / driveSeconds);
                left.SampleCharge(t); right.SampleCharge(t);
                startArea.alpha = 1 - Mathf.SmoothStep(0, 1, t * 2);
                impact.alpha = t > .9f ? 1 : 0;
                yield return null;
            }
            impact.alpha = 1;
            ScreenAudioDirector.PlayTitleStart();
            float hold = 0;
            while (hold < .1f)
            {
                if (!SettingsScreen.BlocksInput) hold += Time.unscaledDeltaTime;
                yield return null;
            }
            GameFlow.ToModeSelect();
        }

        void FinishStart()
        {
            ScreenAudioDirector.PlayTitleStart();
            GameFlow.ToModeSelect();
        }
    }
}
