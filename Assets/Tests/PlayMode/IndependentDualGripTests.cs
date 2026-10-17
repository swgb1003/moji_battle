using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class IndependentDualGripTests
    {
        FighterBuildData savedLeft, savedRight;
        BattleInstance battle;

        [SetUp]
        public void Setup()
        {
            savedLeft = BuildRuntimeStore.Get(0).Clone(); savedRight = BuildRuntimeStore.Get(1).Clone();
            BuildRuntimeStore.ResetSaved(); SimHarness.Begin(false);
        }
        [TearDown]
        public void Cleanup()
        {
            battle?.Destroy(); battle = null; SimHarness.End(); GameFlow.ToTitle();
            BuildRuntimeStore.Set(0, savedLeft); BuildRuntimeStore.Set(1, savedRight); BuildRuntimeStore.Active = false;
        }
        static FighterBuildData Dual(bool split = false) => new FighterBuildData
        {
            character = split ? "明" : "刀", offhandCharacter = "月", gripType = GripType.DualWield,
            splitMing = split, independentOffhandGrip = true, customGrip = true, gripPoint = new Vector2(.15f, .8f),
            offhandCustomGrip = true, offhandGripPoint = new Vector2(.85f, .2f),
        };

        [Test]
        public void LegacyGripMigratesAndBothGripsSurviveJson()
        {
            var old = JsonUtility.FromJson<FighterBuildData>("{\"character\":\"明\",\"gripType\":4,\"weaponSize\":1,\"customGrip\":true,\"gripPoint\":{\"x\":0.2,\"y\":0.8}}");
            old.EnsureIndependentGrips();
            Assert.AreEqual(old.gripPoint, old.offhandGripPoint);
            var build = Dual(true); build.splitSwapped = true;
            var copy = JsonUtility.FromJson<FighterBuildData>(JsonUtility.ToJson(build));
            Assert.IsTrue(copy.IsSplitMing); Assert.IsTrue(copy.splitSwapped);
            Assert.AreEqual(build.gripPoint, copy.gripPoint);
            Assert.AreEqual(build.offhandGripPoint, copy.ForOffhand().gripPoint);
        }

        [Test]
        public void MingComponentsReconstructSourceAndConserveSizeAndMass()
        {
            int checkedFonts = 0;
            foreach (FontStyleId font in System.Enum.GetValues(typeof(FontStyleId)))
            {
                if (!GlyphCatalog.TryGet("明", font, CombatBalance.Default, GlyphCalibration.Default, out var source, out _)) continue;
                var build = Dual(true); build.fontType = font;
                Assert.IsTrue(SplitMingGlyphs.TryGet(build, CombatBalance.Default, GlyphCalibration.Default, false, out var sun, out var error), error);
                Assert.IsTrue(SplitMingGlyphs.TryGet(build, CombatBalance.Default, GlyphCalibration.Default, true, out var moon, out error), error);
                Assert.AreEqual("日", sun.grapheme); Assert.AreEqual("月", moon.grapheme);
                var original = source.texture.GetPixels32(); var left = sun.texture.GetPixels32(); var right = moon.texture.GetPixels32();
                int sourceInk = 0, missingInk = 0;
                for (int i = 0; i < original.Length; i++)
                {
                    Assert.IsFalse(left[i].a > 0 && right[i].a > 0, "Parts must not overlap.");
                    Assert.LessOrEqual(Mathf.Max(left[i].a, right[i].a), original[i].a, "No new strokes may be introduced.");
                    if (original[i].a >= 128) { sourceInk++; if (left[i].a < 128 && right[i].a < 128) missingInk++; }
                }
                Assert.Less(missingInk / (float)sourceInk, .02f, "Only small stray fragments may be removed.");
                float originalMass = WeaponSizeController.WeaponMass(StatCalculator.Compute(source, CombatBalance.Default).weaponMass, build.weaponSize, CustomizeBalance.Default);
                var sunGeometry = FighterFactory.ResolveWeapon(build, sun, StatCalculator.Compute(sun, CombatBalance.Default), CombatBalance.Default, CustomizeBalance.Default, out float massSun);
                var moonGeometry = FighterFactory.ResolveWeapon(build.ForOffhand(), moon, StatCalculator.Compute(moon, CombatBalance.Default), CombatBalance.Default, CustomizeBalance.Default, out float massMoon);
                Assert.AreEqual(originalMass, massSun + massMoon, .001f);
                Assert.AreEqual(sunGeometry.scale, moonGeometry.scale, .00001f);
                checkedFonts++;
            }
            Assert.Greater(checkedFonts, 0);
        }

        [UnityTest]
        public IEnumerator NormalDualWieldUsesIndependentGeometryAndPersists()
        {
            BuildRuntimeStore.Set(0, Dual());
            GameFlow.ToCustomize(); yield return null; yield return null;
            var screen = Object.FindAnyObjectByType<CustomizeScreen>();
            var main = screen.Preview.Geometry.gripPx;
            screen.SelectGripHand(true);
            screen.PickGripAt(new Vector2(.1f, .95f));
            Assert.AreEqual(Dual().gripPoint, screen.Customizer.Current.gripPoint);
            Assert.AreEqual(main, screen.Preview.Geometry.gripPx);
            var expectedOff = screen.Preview.EditingGeometry.gripPx;
            GameFlow.ToBattle(); yield return null; yield return null;
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.AreEqual(main, boot.Battle.Left.Weapon.gripPx);
            Assert.AreEqual(expectedOff, boot.Battle.Left.Offhand.Geometry.gripPx);
            GameFlow.ToCustomize(); yield return null; yield return null;
            screen = Object.FindAnyObjectByType<CustomizeScreen>(); screen.SelectGripHand(true);
            Assert.AreEqual(expectedOff, screen.Preview.EditingGeometry.gripPx);
            Assert.IsTrue(PlayerPrefs.GetString("MojiBattle.Builds.v1").Contains("offhandGripPoint"));
        }

        [UnityTest]
        public IEnumerator MingSplitSwapResetAndBattleFlow()
        {
            GameFlow.ToCustomize(); yield return null; yield return null;
            var screen = Object.FindAnyObjectByType<CustomizeScreen>(); var c = screen.Customizer;
            c.SetCharacter("明"); c.SetSize(WeaponSize.M);
            GameObject.Find("SplitMing").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(c.Current.IsSplitMing);
            c.SetGripPoint(new Vector2(.15f, .85f)); c.SetOffhandGripPoint(new Vector2(.85f, .15f));
            screen.SelectGripHand(true); yield return new WaitForSecondsRealtime(.8f);
            MenuUiTests.Capture("dual_grip_ming_left");
            var mainGrip = c.Current.gripPoint; var offGrip = c.Current.offhandGripPoint;
            GameObject.Find("SwapHands").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("月", c.Current.MainWeaponLabel); Assert.AreEqual("日", c.Current.OffhandWeaponLabel);
            Assert.AreEqual(offGrip, c.Current.gripPoint); Assert.AreEqual(mainGrip, c.Current.offhandGripPoint);
            Assert.AreEqual("月", screen.Preview.Build.MainWeaponLabel);
            GameObject.Find("GripReset").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(c.Current.offhandCustomGrip); Assert.IsTrue(c.Current.customGrip);
            screen.SelectGripHand(false); yield return null;
            MenuUiTests.Capture("dual_grip_ming_right");
            GameFlow.ToBattle(); yield return null; yield return null;
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.AreEqual("明", boot.Battle.Left.Loadout.grapheme);
            Assert.AreEqual("月", boot.Battle.Left.Glyph.grapheme);
            Assert.AreEqual("日", boot.Battle.Left.Offhand.Glyph.grapheme);
            yield return new WaitForSecondsRealtime(1f);
            MenuUiTests.Capture("dual_grip_ming_battle");
            GameFlow.ToCustomize(); yield return null; yield return null;
            c = Object.FindAnyObjectByType<CustomizeScreen>().Customizer;
            Assert.IsTrue(c.Current.IsSplitMing);
            c.SetSplitMing(false); Assert.IsFalse(c.Current.IsSplitMing);
            c.SetSplitMing(true); c.SetFont(FontStyleId.Brush);
            Assert.AreEqual(FontStyleId.Brush, c.Current.fontType);
            yield return null; yield return null;
            MenuUiTests.Capture("dual_grip_ming_brush");
            c.SetSplitMing(true); c.SetSize(WeaponSize.L); Assert.IsFalse(c.Current.IsSplitMing);
            c.SetSize(WeaponSize.M); c.SetGrip(GripType.DualWield); c.SetCharacter("休"); Assert.IsFalse(c.Current.IsSplitMing);
        }

        [UnityTest]
        public IEnumerator OffhandDragFreezesSelectedGlyphAndCommitsOnlyOffhand()
        {
            BuildRuntimeStore.Set(0, Dual(true));
            GameFlow.ToCustomize(); yield return null; yield return null;
            yield return new WaitForSecondsRealtime(.8f);
            var screen = Object.FindAnyObjectByType<CustomizeScreen>(); screen.SelectGripHand(true);
            var preview = screen.Preview; var glyph = preview.EditingGlyph;
            var viewport = GameObject.Find("PreviewViewport").GetComponent<RawImage>();
            var cam = GameObject.Find("PreviewCamera").GetComponent<Camera>();
            Vector2 ToScreen(Vector3 world)
            {
                var uv = cam.WorldToViewportPoint(world); var rect = viewport.rectTransform;
                var local = rect.rect.min + new Vector2(uv.x * rect.rect.width, uv.y * rect.rect.height);
                return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local));
            }
            Vector2 pixel = glyph.features.gripPoint;
            var sourceMain = screen.Customizer.Current.gripPoint;
            var sourceOff = screen.Customizer.Current.offhandGripPoint;
            var reference = preview.PixelToWorld(pixel);
            screen.SetPreviewPointerOver(true);
            Assert.IsTrue(screen.BeginGripDrag(ToScreen(reference), null));
            Vector3 endWorld = preview.PixelToWorld(new Vector2(glyph.features.inkBounds.xMin, glyph.features.inkBounds.yMin));
            var end = ToScreen(endWorld);
            screen.DragGrip(end, null); yield return null; yield return null;
            Assert.Less(Vector3.Distance(reference, preview.PixelToWorld(pixel)), .0001f);
            Assert.AreEqual(sourceOff, screen.Customizer.Current.offhandGripPoint);
            screen.EndGripDrag(end, null, true);
            Assert.AreEqual(sourceMain, screen.Customizer.Current.gripPoint);
            Assert.AreNotEqual(sourceOff, screen.Customizer.Current.offhandGripPoint);
            screen.SetPreviewPointerOver(false);
            MenuUiTests.Capture("dual_grip_ming_880x450", 880, 450);
        }

        [UnityTest]
        public IEnumerator SplitBattlesFinishWithValidHitsAndDeterministicResults()
        {
            Vector2[] hp = new Vector2[2]; int[] hits = new int[2];
            for (int run = 0; run < 2; run++)
            {
                var build = Dual(true); build.splitSwapped = true; build.weaponFlip = WeaponFlip.Both;
                battle = SimHarness.Build(14831, build.ToLoadout(), SimHarness.Custom("山"), 12f);
                int offhandHits = 0; battle.Context.Events.Hit += e => { if (e.offhand) offhandHits++; };
                int steps = 0;
                while (!battle.Director.Ended && steps++ < 5000) battle.Director.SimulateStep();
                Assert.IsTrue(battle.Director.Ended); Assert.AreEqual(0, battle.Director.Telemetry.duplicateBodyHits);
                hp[run] = new Vector2(battle.Left.Runtime.hp, battle.Right.Runtime.hp); hits[run] = offhandHits;
                Assert.IsFalse(float.IsNaN(hp[run].x)); Assert.Greater(offhandHits, 0);
                battle.Destroy(); battle = null; yield return null;
            }
            Assert.AreEqual(hp[0], hp[1]); Assert.AreEqual(hits[0], hits[1]);
        }

        [UnityTest]
        public IEnumerator RoomRoundTripBuildsIdenticalSplitWeaponsForHostAndGuest()
        {
            string url = System.Environment.GetEnvironmentVariable("MOJI_DUAL_TEST_ROOM_URL") ?? "http://127.0.0.1:8787";
            var host = new FriendRoomClient(url); var guest = new FriendRoomClient(url);
            var build = Dual(true); build.splitSwapped = true; build.weaponFlip = WeaponFlip.Horizontal;
            yield return host.Create(build); Assert.IsNull(host.Error);
            yield return guest.Join(host.Code, Dual()); Assert.IsNull(guest.Error);
            yield return host.SetReady(true); yield return guest.SetReady(true);
            yield return host.StartBattle(); Assert.IsNull(host.Error);
            yield return guest.Poll(); Assert.IsNull(guest.Error);
            var remote = guest.State.players[0].build;
            Assert.IsTrue(remote.IsSplitMing); Assert.IsTrue(remote.splitSwapped);
            Assert.AreEqual(build.gripPoint, remote.gripPoint); Assert.AreEqual(build.offhandGripPoint, remote.offhandGripPoint);
            battle = SimHarness.Build(host.State.seed, build.ToLoadout(), Dual().ToLoadout());
            var hostGeometry = battle.Left.Weapon.gripPx; var hostOffGeometry = battle.Left.Offhand.Geometry.gripPx;
            battle.Destroy(); battle = null; yield return null;
            battle = SimHarness.Build(guest.State.seed, remote.ToLoadout(), guest.State.players[1].build.ToLoadout());
            Assert.AreEqual(hostGeometry, battle.Left.Weapon.gripPx);
            Assert.AreEqual(hostOffGeometry, battle.Left.Offhand.Geometry.gripPx);
            Assert.AreEqual("月", battle.Left.Glyph.grapheme); Assert.AreEqual("日", battle.Left.Offhand.Glyph.grapheme);
            yield return host.Leave();
        }
    }
}
