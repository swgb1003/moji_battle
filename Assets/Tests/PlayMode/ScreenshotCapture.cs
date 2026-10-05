using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>観戦画面をオフスクリーン撮影して Reports/Screenshots に保存する（目視確認用）。</summary>
    [Category("Screenshots")]
    public class ScreenshotCapture
    {
        BattleInstance battle;
        RenderTexture rt;

        [TearDown]
        public void TearDown()
        {
            battle?.Destroy();
            if (rt != null) { rt.Release(); Object.Destroy(rt); }
            ColliderDebugView.Visible = false;
            SimHarness.End();
        }

        void Capture(string name, int w = 1920, int h = 1080)
        {
            var cam = battle.CameraRig.Cam;
            if (rt == null || rt.width != w) { if (rt != null) rt.Release(); rt = new RenderTexture(w, h, 24); }
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.aspect = w / (float)h;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = prevTarget;
            var dir = Path.Combine(SimHarness.ReportDir, "Screenshots");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator CaptureBattleMoments()
        {
            SimHarness.Begin(true);
            int seed = 1074; // KO 決着・被弾・転倒を含む seed
            battle = SimHarness.Build(seed, presentation: true, countdown: 1.0f);
            var cam = battle.CameraRig.Cam;
            cam.aspect = 16f / 9f;
            TimeController.SetSpectatorSpeed(1f);
            for (int i = 0; i < 10; i++) yield return null;
            Capture("00_countdown");
            ColliderDebugView.Visible = true;
            for (int i = 0; i < 3; i++) yield return null;
            Capture("01_collider_debug");
            ColliderDebugView.Visible = false;

            bool windup = false, guard = false, hit = false, down = false, ko = false, critical = false, env = false;
            int guardFrame = -1, hitFrame = -1, downFrame = -1, critFrame = -1, envFrame = -1;
            var ev = battle.Context.Events;
            int frame = 0;
            ev.Guard += e => { if (guardFrame < 0) guardFrame = frame + 2; };
            ev.Hit += e =>
            {
                if (hitFrame < 0 && e.damage >= 10f) hitFrame = frame + 2;
                if (critFrame < 0 && e.critical) critFrame = frame + 2;
            };
            ev.EnvImpact += e => { if (envFrame < 0) envFrame = frame + 2; };
            ev.StateChanged += e => { if (e.to == FighterState.Knockdown && downFrame < 0) downFrame = frame + 12; };

            var frameTimes = new System.Collections.Generic.List<float>();
            while (!battle.Director.Ended && frame < 20000)
            {
                frame++;
                float t0 = Time.realtimeSinceStartup;
                yield return null;
                frameTimes.Add((Time.realtimeSinceStartup - t0) * 1000f);
                var r = battle.Right.Runtime;
                if (!windup && r.state == FighterState.AttackWindup && r.stateTime > r.windupDuration * 0.7f) { windup = true; Capture("02_heavy_windup_telegraph"); }
                if (!guard && guardFrame >= 0 && frame >= guardFrame) { guard = true; Capture("03_guard"); }
                if (!hit && hitFrame >= 0 && frame >= hitFrame) { hit = true; Capture("04_heavy_hit"); }
                if (!critical && critFrame >= 0 && frame >= critFrame) { critical = true; Capture("05_critical"); }
                if (!env && envFrame >= 0 && frame >= envFrame) { env = true; Capture("06_wall_or_ground_impact"); }
                if (!down && downFrame >= 0 && frame >= downFrame) { down = true; Capture("07_knockdown"); }
                if (!ko && battle.Director.KoInProgress) { ko = true; for (int i = 0; i < 3; i++) yield return null; Capture("08_ko"); }
            }
            for (int i = 0; i < 5; i++) yield return null;
            // 小さい Game 画面（約 880x450）相当で HUD を組み直してから撮る
            var small = new RenderTexture(880, 450, 24);
            cam.targetTexture = small;
            cam.aspect = 880f / 450f;
            for (int i = 0; i < 3; i++) yield return null;
            cam.targetTexture = null;
            Capture("11_result_880x450", 880, 450);
            small.Release();
            cam.aspect = 16f / 9f;
            for (int i = 0; i < 3; i++) yield return null;
            Capture("09_result");
            Capture("10_result_1280x720", 1280, 720);
            frameTimes.Sort();
            float avg = 0f;
            foreach (var x in frameTimes) avg += x;
            avg /= Mathf.Max(1, frameTimes.Count);
            Debug.Log($"[SHOTS] seed={seed} windup={windup} guard={guard} hit={hit} crit={critical} env={env} down={down} ko={ko} " +
                      $"frames={frameTimes.Count} avgFrameMs={avg:F2} p95={frameTimes[(int)(frameTimes.Count * 0.95f)]:F2} result={battle.Director.Result.finishReason}");
            Assert.IsTrue(battle.Director.Ended);
        }
    }
}
