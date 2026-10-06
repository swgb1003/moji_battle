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
        static Vector2 pickedGripPx;

        static RectInt GlyphInk(FighterPreviewController p)
        {
            GlyphCatalog.TryGet(p.Build.character, p.Build.fontType, CombatBalance.Default, GlyphCalibration.Default, out var g, out _);
            return g.features.inkBounds;
        }

        static Texture2D GlyphTexture(FighterPreviewController p)
        {
            GlyphCatalog.TryGet(p.Build.character, p.Build.fontType, CombatBalance.Default, GlyphCalibration.Default, out var g, out _);
            return g.texture;
        }

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
            // 字形の無い文字・2 文字・空白は理由を出して受け付けない
            Assert.IsFalse(c.SetCharacter("\u0E01"), "フォントに無い文字が通った");
            Assert.IsNotEmpty(c.CharacterError);
            Assert.IsFalse(c.SetCharacter("ab"));
            Assert.IsFalse(c.SetCharacter(" "));
            // ベイク済みでない好きな文字を入力すると、その場で字形化されてプレビューに出る
            Assert.IsTrue(c.SetCharacter("龍"), c.CharacterError);
            Assert.AreEqual("龍", preview.Build.character);
            Assert.Greater(preview.Geometry.colliderCount, 0);
            Assert.IsTrue(c.SetCharacter("火"));
            Assert.AreEqual("火", preview.Build.character);
            // P2: 鬱・逆手・カウンター
            c.SelectSide(1);
            Assert.IsTrue(c.SetCharacter("剣"), c.CharacterError);
            // 握る場所: プレビューの字形をクリックした位置 → 最も近い画線上の点を握る
            yield return null;
            var ink = preview.Build != null ? GlyphInk(preview) : default;
            var clickPx = new Vector2(ink.xMin + ink.width * 0.85f, ink.yMin + ink.height * 0.3f);
            Assert.IsTrue(preview.TryPickGrip(preview.PixelToWorld(clickPx), out var picked, 40f), "字形の上のクリックが拾えない");
            Assert.AreEqual(0.85f, picked.x, 0.02f);
            Assert.AreEqual(0.3f, picked.y, 0.02f);
            Assert.IsFalse(preview.TryPickGrip(preview.PixelToWorld(new Vector2(ink.xMax + 60f, ink.yMax + 60f)), out _), "字形の外のクリックを拾った");
            screen.PickGripAt(picked);
            yield return null;
            Assert.IsTrue(preview.Build.customGrip);
            var gp = preview.Geometry.gripPx;
            Assert.Greater((gp.x - ink.xMin) / ink.width, 0.6f, "クリックした側を握っていない");
            var px = preview.Build != null ? GlyphTexture(preview).GetPixel(Mathf.FloorToInt(gp.x), Mathf.FloorToInt(gp.y)) : default;
            Assert.GreaterOrEqual(px.a, 0.5f, "握り点が画線の外");
            pickedGripPx = gp;
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
            float t0 = Time.realtimeSinceStartup;
            while (boot.Battle.Director.StepCount < 200 && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            Assert.GreaterOrEqual(boot.Battle.Director.StepCount, 200, "試合が進まない");

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
            Assert.AreEqual("剣", b.Right.Loadout.grapheme);
            Assert.AreEqual(pickedGripPx, b.Right.Weapon.gripPx, "プレビューで選んだ握り点と戦闘の握り点が違う");
            StringAssert.Contains("横85%", b.Right.Mods.gripLabel);
            Assert.IsTrue(b.Right.Glyph.runtimeBaked, "入力した文字が実行時に字形化されていない");
            Assert.AreEqual(GripType.Reverse, b.Right.Mods.grip);
            Assert.AreEqual(BattleStyle.Counter, b.Right.Style.Style);
        }
    }
}
