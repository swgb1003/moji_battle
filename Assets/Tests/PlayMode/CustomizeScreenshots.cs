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
            c.SetCharacter("一");
            c.SetSize(WeaponSize.XL);
            c.SetGrip(GripType.TwoHanded);
            c.SetGripPosition(0f);
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
            c.SetCharacter("鬱");
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
