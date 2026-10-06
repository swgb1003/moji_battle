using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>目視確認用: カスタマイズ画面・VS・カスタマイズした試合を撮影して Reports/Screenshots に保存する。</summary>
    [Category("Screenshots")]
    public class CustomizeScreenshots
    {
        FighterBuildData savedLeft, savedRight;

        [SetUp]
        public void SetUp()
        {
            savedLeft = BuildRuntimeStore.Get(0).Clone();
            savedRight = BuildRuntimeStore.Get(1).Clone();
        }

        [TearDown]
        public void TearDown()
        {
            BuildRuntimeStore.Set(0, savedLeft);
            BuildRuntimeStore.Set(1, savedRight);
            BuildRuntimeStore.Active = false;
            ColliderDebugView.Visible = false;
            StickmanView.ShowDebugLabels = false;
            TimeController.ResetAll();
        }

        /// <summary>オーバーレイの UI もカメラ経由で撮れるよう Screen Space - Camera に切り替えて撮影する。</summary>
        static void Capture(string name, int w = 1920, int h = 1080)
        {
            var cam = Camera.main;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();
            var rt = new RenderTexture(w, h, 24);
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
            rt.Release();
            Object.Destroy(rt);
        }

        /// <summary>各技の振りの途中を撮る（刺す・足払い・盾当て・打ち上げ・回転斬り）。</summary>
        [UnityTest]
        public IEnumerator CaptureTechniques()
        {
            Time.captureDeltaTime = TimeController.BaseFixedDelta;
            var styles = new[] { AttackStyle.Thrust, AttackStyle.LowSweep, AttackStyle.Bash, AttackStyle.Launch, AttackStyle.Spin };
            var got = new System.Collections.Generic.List<string>();
            try
            {
                for (int k = 0; k < styles.Length; k++)
                {
                    var st = styles[k];
                    Fighter.ForcedStyleForTests = st;
                    var battle = SimHarness.Build(1300 + k, SimHarness.Custom("火"), SimHarness.Custom("口", style: BattleStyle.Defensive), presentation: true);
                    battle.CameraRig.Cam.aspect = 16f / 9f;

                    TimeController.SetSpectatorSpeed(1f);
                    for (int frame = 0; frame < 3000; frame++)
                    {
                        yield return null;
                        var rt = battle.Left.Runtime;
                        if (rt.attackStyle == st && rt.state == FighterState.AttackActive && rt.stateTime >= rt.activeDuration * 0.5f)
                        {
                            Capture($"3{k}_tech_{st}");
                            got.Add(st.ToString());
                            break;
                        }
                    }
                    battle.Destroy();
                    yield return null;
                }
            }
            finally
            {
                Fighter.ForcedStyleForTests = null;
                Time.captureDeltaTime = 0f;
            }
            Assert.AreEqual(styles.Length, got.Count, "撮れなかった技がある: " + string.Join(",", got));
        }

        /// <summary>横振りの 3 場面（後ろへ回す溜め / 奥を通る / 前へ振り抜く）を撮る。</summary>
        [UnityTest]
        public IEnumerator CaptureSweep()
        {
            var b = CombatBalance.Default;
            float saved = b.sweep.weight, savedCap = b.specialAttackCap;
            b.sweep.weight = 10f; b.specialAttackCap = 1f; // 撮影用に必ず横振りを選ばせる
            Time.captureDeltaTime = TimeController.BaseFixedDelta;
            var battle = SimHarness.Build(1201, SimHarness.Custom("火", WeaponSize.L), SimHarness.Custom("口"), presentation: true);
            battle.CameraRig.Cam.aspect = 16f / 9f;
            TimeController.SetSpectatorSpeed(1f);
            bool back = false, mid = false, front = false;
            for (int frame = 0; frame < 3000 && !(back && mid && front); frame++)
            {
                yield return null;
                var rt = battle.Left.Runtime;
                if (rt.attackStyle != AttackStyle.Sweep) continue;
                if (!back && rt.state == FighterState.AttackWindup && rt.sweepYaw > 150f) { Capture("27_sweep_windup"); back = true; }
                else if (back && !mid && rt.state == FighterState.AttackActive && rt.sweepYaw < 110f) { Capture("28_sweep_depth"); mid = true; }
                else if (mid && !front && rt.state == FighterState.AttackActive && rt.sweepYaw < 10f) { Capture("29_sweep_strike"); front = true; }
            }
            b.sweep.weight = saved; b.specialAttackCap = savedCap;
            Time.captureDeltaTime = 0f;
            battle.Destroy();
            Assert.IsTrue(back && mid && front, "横振りの場面を撮れない");
        }

        [UnityTest]
        public IEnumerator CaptureCustomizeFlow()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.TitleScene, LoadSceneMode.Single);
            for (int i = 0; i < 3; i++) yield return null;
            Capture("20_title");

            BuildRuntimeStore.ResetToDefaults();
            GameFlow.ToCustomize();
            for (int i = 0; i < 3; i++) yield return null;
            var screen = Object.FindAnyObjectByType<CustomizeScreen>();
            var c = screen.Customizer;
            c.SelectSide(0);
            c.SetCharacter("木");
            c.SetSize(WeaponSize.XL);
            c.SetGrip(GripType.TwoHanded);
            screen.PickGripAt(new Vector2(0.15f, 0.6f)); // 字形の左の払いの辺りをクリックした想定
            c.SetStyle(BattleStyle.Aggressive);
            for (int i = 0; i < 30; i++) yield return null;
            Capture("21_customize_xl_two_hand_edge");
            c.SetSize(WeaponSize.S);
            c.SetGrip(GripType.Reverse);
            c.SetGripPosition(0.5f);
            c.SetStyle(BattleStyle.HitAndAway);
            for (int i = 0; i < 30; i++) yield return null;
            Capture("22_customize_s_reverse_center");
            c.SelectSide(1);
            c.SetCharacter("龍");
            c.SetSize(WeaponSize.L);
            c.SetGrip(GripType.Horizontal);
            c.SetStyle(BattleStyle.Defensive);
            for (int i = 0; i < 30; i++) yield return null;
            Capture("23_customize_p2_horizontal");
            screen.ShowVs();
            yield return null;
            Capture("24_vs");

            GameFlow.ToBattle();
            for (int i = 0; i < 3; i++) yield return null;
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            TimeController.SetSpectatorSpeed(1f);
            for (int i = 0; i < 400; i++) yield return null;
            Capture("25_battle_custom");
            ColliderDebugView.Visible = true;
            StickmanView.ShowDebugLabels = true;
            for (int i = 0; i < 3; i++) yield return null;
            Capture("26_battle_custom_debug");
            Assert.IsNotNull(boot.Battle);
        }
    }
}
