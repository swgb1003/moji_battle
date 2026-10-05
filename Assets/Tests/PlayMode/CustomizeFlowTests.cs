using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>タイトル → カスタマイズ → VS → BATTLE → リザルト（もう一度 / 戻る）の通し確認（カスタマイズ仕様 23「全体」）。</summary>
    public class CustomizeFlowTests
    {
        FighterBuildData savedLeft, savedRight;

        [SetUp]
        public void SetUp()
        {
            savedLeft = BuildRuntimeStore.Get(0).Clone();
            savedRight = BuildRuntimeStore.Get(1).Clone();
            BuildRuntimeStore.ResetToDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            BuildRuntimeStore.Set(0, savedLeft);
            BuildRuntimeStore.Set(1, savedRight);
            BuildRuntimeStore.Active = false;
            TimeController.ResetAll();
        }

        [UnityTest]
        public IEnumerator Title_Customize_Battle_Rematch_Back_KeepsBuild()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.TitleScene, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(Object.FindAnyObjectByType<TitleScreen>());

            GameFlow.ToCustomize();
            yield return null;
            yield return null;
            var screen = Object.FindAnyObjectByType<CustomizeScreen>();
            Assert.IsNotNull(screen, "カスタマイズ画面が開かない");
            var c = screen.Customizer;
            var preview = screen.Preview;

            // P1: XL・両手・端寄り・ヒット＆アウェイ。変更の瞬間にプレビューへ反映される
            c.SelectSide(0);
            float mScale = preview.Geometry.scale;
            c.SetSize(WeaponSize.XL);
            Assert.Greater(preview.Geometry.scale, mScale * 1.5f, "サイズがプレビューに反映されない");
            Vector2 gripBefore = preview.Geometry.gripPx;
            c.SetGripPosition(0.1f);
            Assert.AreNotEqual(gripBefore, preview.Geometry.gripPx, "握る位置がプレビューに反映されない");
            c.SetGrip(GripType.TwoHanded);
            Assert.AreEqual(GripType.TwoHanded, preview.Mods.grip, "持ち方がプレビューに反映されない");
            c.SetStyle(BattleStyle.HitAndAway);
            Assert.IsFalse(c.SetCharacter("あ"), "未収録の文字が通った");
            Assert.IsNotEmpty(c.CharacterError);
            Assert.IsTrue(c.SetCharacter("火"));
            Assert.AreEqual("火", preview.Build.character);
            // P2: 鬱・逆手・カウンター
            c.SelectSide(1);
            c.SetCharacter("鬱");
            c.SetGrip(GripType.Reverse);
            c.SetStyle(BattleStyle.Counter);
            Assert.AreEqual(1, preview.Side);

            screen.ShowVs();
            Assert.IsTrue(screen.VsVisible, "VS 確認が出ない");
            GameFlow.ToBattle();
            yield return null;
            yield return null;
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.IsNotNull(boot?.Battle, "試合が始まらない");
            AssertBuild(boot.Battle);

            // 数秒戦わせる（例外・継続エラーが出ないこと）
            TimeController.SetSpectatorSpeed(2f);
            for (int i = 0; i < 120; i++) yield return null;
            Assert.Greater(boot.Battle.Director.StepCount, 10);

            // リザルト: 同じ設定でもう一度
            boot.RematchSameBuild();
            yield return null;
            AssertBuild(boot.Battle);

            // カスタマイズへ戻っても設定が残っている
            GameFlow.ToCustomize();
            yield return null;
            yield return null;
            var again = Object.FindAnyObjectByType<CustomizeScreen>();
            Assert.AreEqual(WeaponSize.XL, again.Customizer.Get(0).weaponSize);
            Assert.AreEqual("火", again.Customizer.Get(0).character);
            Assert.AreEqual(BattleStyle.Counter, again.Customizer.Get(1).battleStyle);
        }

        static void AssertBuild(BattleInstance b)
        {
            Assert.AreEqual("火", b.Left.Loadout.grapheme);
            Assert.IsTrue(b.Left.Mods.customized);
            Assert.AreEqual(WeaponSize.XL, b.Left.Mods.size);
            Assert.AreEqual(GripType.TwoHanded, b.Left.Mods.grip);
            Assert.AreEqual(BattleStyle.HitAndAway, b.Left.Style.Style);
            Assert.AreEqual(0.1f, b.Left.Mods.gripPosition, 0.001f);
            Assert.AreEqual("鬱", b.Right.Loadout.grapheme);
            Assert.AreEqual(GripType.Reverse, b.Right.Mods.grip);
            Assert.AreEqual(BattleStyle.Counter, b.Right.Style.Style);
        }
    }
}
