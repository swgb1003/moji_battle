using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// P1 観戦 HUD。上部 140px 相当に名前・HP・残り時間、下部に観戦操作のみ（AI操作ボタンは置かない）。
    /// カメラに追従するワールド空間要素で描くため、オフスクリーン撮影にも写る。P4 で UGUI + TMP に置き換える。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class BattleHudPresenter : MonoBehaviour
    {
        const float RefH = 1080f;
        MatchDirector director;
        CameraRig rig;
        readonly Bar[] bars = new Bar[2];
        readonly HudText[] names = new HudText[2], statsText = new HudText[2], buildText = new HudText[2];
        HudText timer, center, sub, footer, speed, seedText;
        readonly float[] trail = new float[2];

        struct Bar { public SpriteRenderer bg, trail, fill; }

        sealed class HudText
        {
            public TextMesh tm;
            public float px, py, h;
        }

        public void Bind(MatchDirector d, CameraRig cameraRig)
        {
            director = d;
            rig = cameraRig;
            var t = transform;
            for (int i = 0; i < 2; i++)
            {
                var f = d.Fighters[i];
                var team = FighterFactory.TeamColor(i);
                bars[i] = new Bar
                {
                    bg = UiKit.Rect(t, "HpBg", FighterFactory.Ink, 50),
                    trail = UiKit.Rect(t, "HpTrail", Color.white, 51),
                    fill = UiKit.Rect(t, "HpFill", team, 52),
                };
                trail[i] = 1f;
                var anchor = i == 0 ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
                string side = i == 0 ? "左・朱" : "右・群青";
                names[i] = MakeText($"{f.Loadout.grapheme}  {GlyphCatalog.FontDisplayName(f.Loadout.font)}  [{side}]", team, i == 0 ? 64 : 1856, 26, 40, anchor);
                var s = f.Stats;
                // カスタマイズの試合はビルド（サイズ / 持ち方 / スタイル）を小さく表示（カスタマイズ仕様 19）
                string build = f.Mods.customized ? $"{f.Mods.size} / {CustomizeLabels.Grip(f.Mods.grip)} / 握り{Mathf.RoundToInt(f.Mods.gripPosition * 100f)}% / {CustomizeLabels.Style(f.Mods.style)}" : "";
                buildText[i] = MakeText(build, team, i == 0 ? 64 : 1856, 150, 26, anchor);
                statsText[i] = MakeText($"攻{s.attack} 防{s.defense} 速{s.speed} 耐{s.durability}  重量{new string('★', s.WeightStars)}  {f.WeightClass}",
                    FighterFactory.Ink, i == 0 ? 64 : 1856, 116, 24, anchor);
            }
            timer = MakeText("60", FighterFactory.Ink, 960, 30, 72, TextAnchor.UpperCenter);
            center = MakeText("", FighterFactory.Ink, 960, 380, 120, TextAnchor.MiddleCenter);
            sub = MakeText("", FighterFactory.Ink, 960, 500, 34, TextAnchor.MiddleCenter);
            footer = MakeText("Space 停止 / 1・2・3 速度 / R・T 再戦 / Z・X・N・M 文字 / C 当たり判定 / D AI / B カスタマイズ",
                new Color(0.22f, 0.22f, 0.22f), 64, 1040, 24, TextAnchor.LowerLeft);
            speed = MakeText("1×", FighterFactory.Ink, 1420, 1040, 30, TextAnchor.LowerCenter);
            seedText = MakeText($"seed {d.Config.seed}", new Color(0.22f, 0.22f, 0.22f), 1856, 1040, 24, TextAnchor.LowerRight);
        }

        HudText MakeText(string s, Color c, float px, float py, float h, TextAnchor anchor)
        {
            var tm = UiKit.Text(transform, "HudText", s, c, 1f, 60, anchor);
            tm.fontStyle = FontStyle.Bold; // 細字は小さい画面で背景に溶けるため太字
            tm.alignment = anchor == TextAnchor.UpperLeft || anchor == TextAnchor.LowerLeft ? TextAlignment.Left
                : anchor == TextAnchor.UpperRight || anchor == TextAnchor.LowerRight ? TextAlignment.Right : TextAlignment.Center;
            return new HudText { tm = tm, px = px, py = py, h = h };
        }

        void LateUpdate()
        {
            if (director == null || rig == null || rig.Cam == null) return;
            var cam = rig.Cam;
            float u = 2f * cam.orthographicSize / RefH;
            float refW = RefH * cam.aspect;
            Vector3 origin = cam.transform.position + new Vector3(-refW * 0.5f * u, RefH * 0.5f * u, 10f);
            Vector3 W(float px, float py) => origin + new Vector3(px * u * (refW / 1920f), -py * u, 0f);

            for (int i = 0; i < 2; i++)
            {
                var rt = director.Fighters[i].Runtime;
                float ratio = Mathf.Clamp01(rt.hp / rt.maxHp);
                trail[i] = Mathf.MoveTowards(Mathf.Max(trail[i], ratio), ratio, Time.unscaledDeltaTime * 0.6f);
                float barW = 720f, barH = 34f, y = 78f;
                float x0 = i == 0 ? 64f : 1856f - barW;
                SetRect(bars[i].bg, W(x0 + barW * 0.5f, y + barH * 0.5f), barW * u * refW / 1920f + 4f * u, barH * u + 4f * u);
                float trailW = barW * trail[i], fillW = barW * ratio;
                // 左は右側から、右は左側から減る（中央から外へ）
                float tx = i == 0 ? x0 + trailW * 0.5f : x0 + barW - trailW * 0.5f;
                float fx = i == 0 ? x0 + fillW * 0.5f : x0 + barW - fillW * 0.5f;
                SetRect(bars[i].trail, W(tx, y + barH * 0.5f), trailW * u * refW / 1920f, barH * u);
                SetRect(bars[i].fill, W(fx, y + barH * 0.5f), fillW * u * refW / 1920f, barH * u);
                Place(names[i], W, u, cam);
                Place(statsText[i], W, u, cam);
                Place(buildText[i], W, u, cam);
            }

            timer.tm.text = director.Overtime ? "延長" : Mathf.CeilToInt(director.TimeRemaining).ToString("00");
            Place(timer, W, u, cam);

            string big = "", small = "";
            if (director.CountdownRemaining > 0f)
            {
                int n = Mathf.CeilToInt(director.CountdownRemaining);
                big = n.ToString();
                small = "完全オート戦闘 — 予想しながら観戦";
            }
            else if (director.Clock < 0.8f && !director.Ended && director.Config.durationSeconds > 0f && director.StepCount > 5) big = "FIGHT!";
            if (director.Overtime && director.OvertimeElapsed < 1.5f && !director.Ended) { big = "延長戦"; small = "先に当てた方の勝ち"; }
            if (director.KoInProgress && !director.Ended) big = "K.O.";
            if (director.Ended)
            {
                var r = director.Result;
                string reason = r.finishReason.StartsWith("SUDDEN") ? "延長戦" : r.finishReason.StartsWith("TIME") ? "TIME UP" : r.finishReason == "DOUBLE_KO" ? "DOUBLE K.O." : "K.O.";
                if (r.winner < 0) big = $"{reason}  引き分け";
                else
                {
                    var w = director.Fighters[r.winner];
                    big = $"「{w.Loadout.grapheme}」の勝ち  {reason}";
                    center.tm.color = FighterFactory.TeamColor(r.winner);
                }
                var m0 = r.metrics[0];
                var m1 = r.metrics[1];
                small = $"{r.elapsedSeconds:F1}秒  残HP {r.hpRemaining[0]:F0} / {r.hpRemaining[1]:F0}  与ダメ {m0.damageDealt:F0} / {m1.damageDealt:F0}  " +
                        $"ガード {m0.guards} / {m1.guards}  最大吹っ飛び {m0.maxKnockbackDistance:F1} / {m1.maxKnockbackDistance:F1}";
            }
            else center.tm.color = FighterFactory.Ink;
            center.tm.text = big;
            sub.tm.text = small;
            Place(center, W, u, cam);
            Place(sub, W, u, cam);
            speed.tm.text = TimeController.Paused ? "一時停止中" : $"{TimeController.SpectatorSpeed:0.#}×";
            Place(speed, W, u, cam);
            Place(footer, W, u, cam);
            Place(seedText, W, u, cam);
        }

        static void SetRect(SpriteRenderer sr, Vector3 pos, float w, float h)
        {
            sr.transform.position = pos;
            sr.transform.localScale = new Vector3(Mathf.Max(0f, w), h, 1f);
        }

        static void Place(HudText t, System.Func<float, float, Vector3> W, float u, Camera cam)
        {
            t.tm.transform.position = W(t.px, t.py);
            UiKit.FitText(t.tm, t.h * u, cam);
        }

        void OnGUI()
        {
            // 観戦操作のみ。AI・棒人間への操作は置かない。
            float s = Screen.height / RefH;
            float bw = 120 * s, bh = 44 * s, y = Screen.height - bh - 56 * s;
            float x = Screen.width * 0.5f - (bw * 4 + 30 * s) * 0.5f;
            if (GUI.Button(new Rect(x, y, bw, bh), TimeController.Paused ? "再開" : "一時停止")) TimeController.SetPaused(!TimeController.Paused);
            if (GUI.Button(new Rect(x + (bw + 10 * s), y, bw, bh), "0.5×")) TimeController.SetSpectatorSpeed(0.5f);
            if (GUI.Button(new Rect(x + (bw + 10 * s) * 2, y, bw, bh), "1×")) TimeController.SetSpectatorSpeed(1f);
            if (GUI.Button(new Rect(x + (bw + 10 * s) * 3, y, bw, bh), "2×")) TimeController.SetSpectatorSpeed(2f);
        }
    }
}
