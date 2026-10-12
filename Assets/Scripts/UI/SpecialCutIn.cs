using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>
    /// 必殺技の発動カットイン。試合の時間を止め、画面全体に技の色の帯・武器の一文字・技名・掛け声を叩きつける。
    /// 時間は実時間で進める（止まっている試合の時間は使わない）。オンラインのゲストもホストのイベントで同じものを出す。
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class SpecialCutIn : MonoBehaviour
    {
        public const float Duration = 1.45f;
        const float In = .16f, Out = .22f;

        MatchDirector director;
        CameraRig rig;
        BattleIntervention iv;
        RectTransform root, band, bandEdge, bandEdge2, nameRect, glyphRect, lines;
        RectTransform compact;
        Text compactName, compactPrompt;
        CanvasGroup group;
        Image dim, flash, bandFill, edgeFill, edge2Fill, stamp;
        Text nameText, glyphText, callout, reading, player, prompt;
        readonly RectTransform[] speed = new RectTransform[18];
        float age = -1f;
        int side;
        bool simple;

        public bool Showing => age >= 0f;
        public string ShownName => nameText.text;

        public void Bind(MatchDirector d, CameraRig cameraRig, RectTransform screen)
        {
            director = d; rig = cameraRig; iv = d.Context.Interventions;
            root = UguiKit.Fill(screen, "SpecialCutIn");
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            dim = Fill(root, "Dim", new Color(0, 0, 0, .72f));
            lines = Center(root, "SpeedLines", 2600, 1400, Color.clear).rectTransform;
            for (int i = 0; i < speed.Length; i++)
                speed[i] = Center(lines, "Line" + i, Random.Range(300f, 900f), Random.Range(4f, 12f), new Color(1, 1, 1, .5f)).rectTransform;
            bandEdge = Center(root, "BandEdge", 3200, 470, Color.white).rectTransform;
            edgeFill = bandEdge.GetComponent<Image>();
            band = Center(root, "Band", 3200, 400, RetroUi.Red).rectTransform;
            bandFill = band.GetComponent<Image>();
            bandEdge2 = Center(root, "BandEdge2", 3200, 26, RetroUi.Ink).rectTransform;
            edge2Fill = bandEdge2.GetComponent<Image>();
            glyphRect = Center(root, "GlyphBack", 520, 520, Color.clear).rectTransform;
            glyphText = Text(glyphRect, "Glyph", 470, RetroUi.Paper, 6);
            nameRect = Center(root, "NameBack", 1500, 300, Color.clear).rectTransform;
            nameText = Text(nameRect, "Name", 210, RetroUi.Paper, 5);
            nameText.resizeTextForBestFit = true; nameText.resizeTextMinSize = 80; nameText.resizeTextMaxSize = 210;
            stamp = Center(root, "Stamp", 230, 86, RetroUi.Ink);
            var stampText = Text(stamp.rectTransform, "StampText", 54, RetroUi.Paper, 0);
            stampText.text = "必殺技";
            reading = Text(Center(root, "ReadingBack", 1200, 60, Color.clear).rectTransform, "Reading", 40, RetroUi.Paper, 3);
            callout = Text(Center(root, "CalloutBack", 1500, 120, Color.clear).rectTransform, "Callout", 92, RetroUi.Paper, 5);
            player = Text(Center(root, "PlayerBack", 400, 70, Color.clear).rectTransform, "Player", 48, RetroUi.Paper, 3);
            // 受ける側（この端末の操作）への合図: 止まっている間に声援を押せば踏ん張れる
            prompt = Text(Center(root, "PromptBack", 1400, 90, Color.clear).rectTransform, "BracePrompt", 58, Color.white, 4);
            ((RectTransform)prompt.transform.parent).anchoredPosition = new Vector2(0, -430f);
            flash = Fill(root, "Flash", new Color(1, 1, 1, 0));
            compact = Center(root, "CompactCutIn", 1120, 240, RetroUi.Paper).rectTransform;
            compact.anchoredPosition = new Vector2(0, 275);
            compactName = Text(Center(compact, "CompactNameBack", 1060, 110, Color.clear).rectTransform, "CompactName", 65, RetroUi.Ink, 0);
            ((RectTransform)compactName.transform.parent).anchoredPosition = new Vector2(0, 40);
            compactName.resizeTextForBestFit = true; compactName.resizeTextMinSize = 30; compactName.resizeTextMaxSize = 65;
            compactPrompt = Text(Center(compact, "CompactPromptBack", 1060, 66, Color.clear).rectTransform, "CompactPrompt", 32, RetroUi.Ink, 0);
            ((RectTransform)compactPrompt.transform.parent).anchoredPosition = new Vector2(0, -57);
            root.gameObject.SetActive(false);
            d.Events.Intervention += OnIntervention;
        }

        static Image Fill(Transform parent, string name, Color color)
        {
            var image = UguiKit.Fill(parent, name).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }

        static Image Center(Transform parent, string name, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(w, h);
            var image = go.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }

        static Text Text(RectTransform parent, string name, int size, Color color, float outline)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = RetroUi.Font; t.fontSize = size; t.color = color; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.fontStyle = FontStyle.Bold; t.raycastTarget = false;
            if (outline > 0f)
            {
                // Outline 自体が4方向の縁取りを作る。重ねると頂点数が乗算される。
                var o = go.AddComponent<Outline>();
                o.effectColor = RetroUi.Ink; o.effectDistance = Vector2.one * outline;
                var s = go.AddComponent<Shadow>();
                s.effectColor = new Color(0, 0, 0, .8f); s.effectDistance = new Vector2(outline * 2.2f, -outline * 2.2f);
            }
            return t;
        }

        void OnIntervention(InterventionEvent e)
        {
            if (e.kind != InterventionKind.Special || !e.success || e.step != 0) return;
            side = e.fighter;
            simple = GameSettings.SimpleSpecial;
            foreach (Transform child in root) child.gameObject.SetActive(simple ? child == compact : child != compact);
            var plan = iv.Side(side).plan;
            var f = director.Fighters[side];
            Color main = plan.HasAppearance ? plan.move.PrimaryColor : RetroUi.Team(side);
            Color sub = plan.HasAppearance ? plan.move.SecondaryColor : RetroUi.Ink;
            bandFill.color = main;
            edgeFill.color = Color.Lerp(sub, Color.white, .25f);
            edge2Fill.color = RetroUi.Ink;
            nameText.text = plan.name;
            glyphText.text = f.Loadout.grapheme;
            var font = GlyphFontLibrary.Default?.Get(f.Loadout.font)?.font;
            if (font != null) { glyphText.font = font; glyphText.fontStyle = FontStyle.Normal; }
            glyphText.color = Color.Lerp(main, Color.white, .35f);
            reading.text = plan.HasAppearance ? plan.move.reading : "";
            callout.text = string.IsNullOrEmpty(plan.callout) ? "" : "「" + plan.callout + "」";
            string who = iv.Side(side).controller == InterventionController.Com ? "COM" : side == 0 ? "PLAYER 01" : "PLAYER 02";
            player.text = who;
            compactName.text = "必殺「" + plan.name + "」";
            compactName.color = RetroUi.Team(side);
            stamp.color = RetroUi.Team(side);
            age = 0f;
            root.SetAsLastSibling();
            root.gameObject.SetActive(TimeController.PresentationEffectsEnabled);
            // 試合の時間を止める（ホストの停止はゲストにもそのまま届く）
            TimeController.TriggerFreeze(Duration);
        }

        void Update()
        {
            if (age < 0f) return;
            if (!TimeController.Paused) age += Time.unscaledDeltaTime;
            float t = age;
            if (t >= Duration)
            {
                age = -1f;
                root.gameObject.SetActive(false);
                // 動き出しの瞬間に寄って揺らす
                rig?.ZoomPunch(.18f, .45f);
                rig?.Shake(.22f, .3f);
                return;
            }
            if (simple)
            {
                // Preserve the brace input window on both network peers; only the visual is reduced.
                group.alpha = 1;
                var compactVictim = iv.Side(1 - side);
                compactPrompt.text = compactVictim.controller != InterventionController.Local ? "発動中" : compactVictim.braced ? "踏ん張り！ ダメージ軽減"
                    : compactVictim.CheerReady(iv.Now) ? "L / 声援ボタンで踏ん張る" : "声援ゲージ回復待ち";
                return;
            }
            float enter = Mathf.Clamp01(t / In), exit = Mathf.Clamp01((t - (Duration - Out)) / Out);
            float easeIn = 1f - Mathf.Pow(1f - enter, 3f), easeOut = exit * exit;
            int dir = side == 0 ? 1 : -1;
            float tilt = -9f * dir;
            group.alpha = 1f - easeOut;
            dim.color = new Color(0, 0, 0, .72f * Mathf.Min(1f, t / .06f));
            flash.color = new Color(1, 1, 1, GameSettings.ReducedEffects ? 0 : Mathf.Clamp01(1f - t / .14f) * .95f);
            // 帯: 自分の側から突っ込んで止まり、抜けていく
            float bandX = Mathf.Lerp(-2600f * dir, 0f, easeIn) + 2600f * dir * easeOut;
            band.anchoredPosition = new Vector2(bandX, -10f);
            band.localRotation = Quaternion.Euler(0, 0, tilt);
            bandEdge.anchoredPosition = new Vector2(bandX * 1.15f, -10f);
            bandEdge.localRotation = Quaternion.Euler(0, 0, tilt);
            bandEdge2.anchoredPosition = new Vector2(-bandX * 1.3f, -235f);
            bandEdge2.localRotation = Quaternion.Euler(0, 0, tilt);
            // 集中線: 帯に沿って高速で流れる
            lines.localRotation = Quaternion.Euler(0, 0, tilt);
            for (int i = 0; i < speed.Length; i++)
            {
                float lane = (i * 0.618f) % 1f;
                float x = Mathf.Repeat(i * 397f - t * 5200f * dir, 3200f) - 1600f;
                speed[i].anchoredPosition = new Vector2(x, (lane - .5f) * 1100f);
            }
            // 一文字: 大きく叩きつけてから少し脈打つ
            float slam = Mathf.Clamp01((t - .05f) / .12f);
            float glyphScale = Mathf.Lerp(2.6f, 1f, 1f - Mathf.Pow(1f - slam, 3f)) * (1f + .03f * Mathf.Sin(t * 40f) * (1f - slam));
            glyphRect.anchoredPosition = new Vector2(-600f * dir + 120f * dir * easeOut, 20f);
            glyphRect.localScale = Vector3.one * glyphScale;
            glyphRect.localRotation = Quaternion.Euler(0, 0, tilt * .6f);
            glyphText.color = new Color(glyphText.color.r, glyphText.color.g, glyphText.color.b, slam);
            // 技名: 遅れて判子のように押し付ける（押した瞬間に小さく揺れる）
            float stampIn = Mathf.Clamp01((t - .14f) / .1f);
            float nameScale = Mathf.Lerp(3f, 1f, 1f - Mathf.Pow(1f - stampIn, 4f));
            float jitter = !GameSettings.ReducedEffects && stampIn >= 1f && t < .4f ? Mathf.Sin(t * 120f) * 6f * (.4f - t) / .2f : 0f;
            nameRect.anchoredPosition = new Vector2(170f * dir + jitter, -10f);
            nameRect.localScale = Vector3.one * nameScale * (1f + .015f * Mathf.Sin(t * 9f));
            nameRect.localRotation = Quaternion.Euler(0, 0, tilt * .5f);
            nameText.color = new Color(1, 1, 1, stampIn) * RetroUi.Paper;
            // 小見出し・読み・掛け声・名前
            float tagIn = Mathf.Clamp01((t - .2f) / .12f);
            stamp.rectTransform.anchoredPosition = new Vector2(170f * dir - 560f, 205f);
            stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, tilt * .5f);
            stamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, tagIn);
            SetAlpha(stamp, tagIn); SetAlpha(stamp.GetComponentInChildren<Text>(), tagIn);
            ((RectTransform)reading.transform.parent).anchoredPosition = new Vector2(170f * dir + 40f, 205f);
            ((RectTransform)reading.transform.parent).localRotation = Quaternion.Euler(0, 0, tilt * .5f);
            SetAlpha(reading, tagIn);
            float callIn = Mathf.Clamp01((t - .32f) / .14f);
            var callRect = (RectTransform)callout.transform.parent;
            callRect.anchoredPosition = new Vector2(170f * dir + (1f - callIn) * 300f * dir, -290f);
            callRect.localRotation = Quaternion.Euler(0, 0, tilt * .5f);
            SetAlpha(callout, callIn);
            var playerRect = (RectTransform)player.transform.parent;
            playerRect.anchoredPosition = new Vector2(-600f * dir, -300f);
            SetAlpha(player, tagIn);
            var victim = iv.Side(1 - side);
            bool local = victim.controller == InterventionController.Local;
            prompt.gameObject.SetActive(local);
            if (local)
            {
                prompt.text = victim.braced ? "踏ん張り！ ダメージ軽減" : victim.CheerReady(iv.Now)
                    ? "L / 声援ボタンで踏ん張れ！" : "声援ゲージ回復待ち";
                float blink = victim.braced ? 1f : .55f + .45f * Mathf.Abs(Mathf.Sin(t * 9f));
                prompt.color = (victim.braced ? new Color(1f, .85f, .3f) : Color.white) * new Color(1, 1, 1, blink * Mathf.Clamp01((t - .2f) / .1f));
            }
        }

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color; c.a = a; g.color = c;
        }

        void OnDestroy()
        {
            if (director != null) director.Events.Intervention -= OnIntervention;
        }
    }
}
