using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class FusionTests
    {
        string librarySave; FighterBuildData oldLeft, oldRight; bool reduced, menuReduced; BattleInstance battle;
        [SetUp] public void Setup()
        {
            librarySave = PlayerPrefs.GetString("MojiBattle.Fusion.v1", ""); PlayerPrefs.DeleteKey("MojiBattle.Fusion.v1");
            oldLeft = BuildRuntimeStore.Get(0).Clone(); oldRight = BuildRuntimeStore.Get(1).Clone();
            reduced = GameSettings.ReducedEffects; menuReduced = RetroMotionSettings.Reduced;
            GameSettings.Change(x => x.reducedEffects = false); GameSettings.SetMenuMotion(false);
            BuildRuntimeStore.ResetSaved(); SimHarness.Begin(false);
        }
        [TearDown] public void Cleanup()
        {
            var screen = Object.FindAnyObjectByType<FusionScreen>(); if (screen != null) Object.Destroy(screen.gameObject);
            battle?.Destroy(); battle = null; SimHarness.End(); GameFlow.ToTitle();
            PlayerPrefs.SetString("MojiBattle.Fusion.v1", librarySave); PlayerPrefs.Save();
            BuildRuntimeStore.Set(0, oldLeft); BuildRuntimeStore.Set(1, oldRight); BuildRuntimeStore.Active = false;
            GameSettings.Change(x => x.reducedEffects = reduced); GameSettings.SetMenuMotion(menuReduced);
        }
        static FusionData Invented() => new FusionData { name = "月樹", leftSource = "林", rightSource = "明", leftY = 12, rightWidth = 1.15f, gap = 12 };
        static GlyphDefinitionRuntime Glyph(FusionData recipe, FontStyleId font, int part = -1)
        { Assert.IsTrue(FusionGlyphs.TryGet(recipe, font, CombatBalance.Default, GlyphCalibration.Default, part, out var glyph, out var error), error); return glyph; }
        static void Click(string name) { var go = GameObject.Find(name); Assert.IsNotNull(go, name); var b = go.GetComponent<Button>(); Assert.IsTrue(b.interactable, name); b.onClick.Invoke(); }
        [Test] public void RecipeRejectsUnknownSourcesNonFiniteOutOfRangeAndLongNames()
        {
            Assert.IsTrue(new FusionData().Validate(out _)); Assert.AreEqual("清", new FusionData().Character);
            foreach (var r in new[] { new FusionData { leftSource = "一" }, new FusionData { rightSource = "A" }, new FusionData { leftWidth = float.NaN },
                new FusionData { gap = 25 }, new FusionData { rightY = -25 }, new FusionData { version = 3 }, new FusionData { layout = (KanjiLayout)3 },
                new FusionData { version = 1, leftSource = "国" }, new FusionData { name = new string('字', 13) }, new FusionData { name = "a\nb" } })
                Assert.IsFalse(r.Validate(out _));
            var normal = JsonUtility.FromJson<FighterBuildData>(JsonUtility.ToJson(new FighterBuildData { character = "刀" }));
            Assert.IsFalse(normal.HasFusion); Assert.IsNull(normal.OffhandFusion);
            Assert.IsTrue(BattleBuilder.MainGlyph(normal.ToLoadout(), CombatBalance.Default, GlyphCalibration.Default, out var normalGlyph, out _)); Assert.AreEqual("刀", normalGlyph.grapheme);
        }
        [Test] public void LibraryPersistsCopiesEditsDeletesAndToleratesCorruptSaves()
        {
            var r = Invented(); Assert.IsTrue(FusionLibrary.Save(r, FontStyleId.Brush, null, out var record, out _));
            r.leftSource = "河"; Assert.AreEqual("林", FusionLibrary.All()[0].recipe.leftSource);
            var all = FusionLibrary.All(); all[0].recipe.name = "変更"; Assert.AreEqual("月樹", FusionLibrary.All()[0].recipe.name);
            var build = record.Build(); record.recipe.name = "編集";
            Assert.IsTrue(FusionLibrary.Save(record.recipe, record.font, record.id, out _, out _)); Assert.AreEqual(1, FusionLibrary.All().Count);
            Assert.AreEqual("月樹", build.DisplayName); FusionLibrary.Delete(record.id); Assert.IsEmpty(FusionLibrary.All()); Assert.AreEqual("月樹", build.DisplayName);
            PlayerPrefs.SetString("MojiBattle.Fusion.v1", "{garbage"); Assert.IsEmpty(FusionLibrary.All());
        }
        [Test] public void EverySourcePartCanComposeInAllFontsWithoutClipping()
        {
            int count = 0; var sources = SplitKanjiCatalog.Entries;
            foreach (FontStyleId f in System.Enum.GetValues(typeof(FontStyleId)))
            foreach (var source in sources)
            foreach (KanjiLayout layout in System.Enum.GetValues(typeof(KanjiLayout)))
            {
                // Both halves of every supported source participate, with extreme valid edits.
                bool swapped = layout == KanjiLayout.TopBottom;
                var r = new FusionData { leftSource = source.Character, rightSource = source.Character, layout = layout,
                    leftUseSecond = swapped, rightUseFirst = swapped, leftWidth = .7f, rightWidth = 1.3f, leftY = -24, rightY = 24, gap = 24 };
                var whole = Glyph(r, f); var b = whole.features.inkBounds;
                Assert.AreEqual(swapped ? source.Second : source.First, Glyph(r, f, 0).grapheme);
                Assert.AreEqual(swapped ? source.First : source.Second, Glyph(r, f, 1).grapheme);
                Assert.GreaterOrEqual(b.xMin, 8, source.Character); Assert.GreaterOrEqual(b.yMin, 8, source.Character);
                Assert.LessOrEqual(b.xMax, 248, source.Character); Assert.LessOrEqual(b.yMax, 248, source.Character);
                Assert.Greater(whole.features.colliderRects.Length, 0); count++;
            }
            Assert.AreEqual(1452, count);
        }
        [Test] public void ComposedGeometryPartsMassAndJsonRoundTripAgree()
        {
            var dir = Path.Combine(SimHarness.ReportDir, "Fusion"); Directory.CreateDirectory(dir);
            foreach (FontStyleId f in System.Enum.GetValues(typeof(FontStyleId)))
            foreach (var r in new[] { new FusionData(), Invented(),
                new FusionData { name = "上下", leftSource = "思", rightSource = "雪", layout = KanjiLayout.TopBottom, leftUseSecond = true, rightUseFirst = true },
                new FusionData { name = "囲み", leftSource = "国", rightSource = "海", layout = KanjiLayout.Enclosure } })
            {
                var build = new FusionRecord { font = f, recipe = r }.Build();
                var json = JsonUtility.ToJson(build); var remote = JsonUtility.FromJson<FighterBuildData>(json);
                var g = Glyph(r, f); var copy = Glyph(remote.fusion, remote.fontType);
                Assert.AreEqual(g.features.inkRatio, copy.features.inkRatio);
                Assert.IsTrue(BattleBuilder.MainGlyph(remote.ToLoadout(), CombatBalance.Default, GlyphCalibration.Default, out var actual, out _)); Assert.AreSame(g, actual);
                var l = Glyph(r, f, 0); var right = Glyph(r, f, 1); var px = g.texture.GetPixels32(); var lp = l.texture.GetPixels32(); var rp = right.texture.GetPixels32();
                for (int i = 0; i < px.Length; i++) Assert.AreEqual(Mathf.Max(lp[i].a, rp[i].a), px[i].a);
                float mass = StatCalculator.Compute(g, CombatBalance.Default).weaponMass;
                Assert.AreEqual(mass, l.weaponMassBasis + right.weaponMassBasis, .0001f);
                build.gripType = GripType.DualWield; build.splitMing = true; Assert.IsTrue(build.IsSplitMing);
                Assert.IsTrue(SplitMingGlyphs.TryGet(build, CombatBalance.Default, GlyphCalibration.Default, false, out var part, out _)); Assert.AreSame(l, part);
                File.WriteAllBytes(Path.Combine(dir, (r.name == "" ? "qing" : r.name) + "_" + f + ".png"), g.texture.EncodeToPNG());
                var clone = build.Clone(); clone.fusion.gap = 1; Assert.AreEqual(r.gap, build.fusion.gap);
            }
        }
        [UnityTest] public IEnumerator FiveScreensCraftSaveEquipEditAndDeleteThroughActualUi()
        {
            GameFlow.ToModeSelect(); yield return null; yield return null;
            Click("FusionMode"); yield return null; var screen = Object.FindAnyObjectByType<FusionScreen>(); Assert.IsNotNull(screen);
            Assert.IsFalse(GameObject.Find("FusionMode").GetComponent<Button>().IsInteractable(), "Background menu must reject keyboard as well as pointer input.");
            Capture("fusion_01_menu"); Click("FusionLibrary"); yield return null; Assert.IsNotNull(GameObject.Find("LibraryEmpty"));
            Click("LibraryCreateTile"); yield return null; Capture("fusion_02_parts");
            Click("Source_林"); yield return null; Click("ChooseRight"); yield return null; Click("Source_明"); yield return null;
            Assert.AreEqual("林", screen.Recipe.leftSource); Assert.AreEqual("明", screen.Recipe.rightSource);
            Click("ToLayout"); yield return null; Capture("fusion_03_layout");
            GameObject.Find("FusionWidth").GetComponent<Slider>().value = 1.2f; Click("EditRight");
            GameObject.Find("FusionVertical").GetComponent<Slider>().value = 10; yield return null;
            Assert.AreEqual(1.2f, screen.Recipe.leftWidth, .01f); Assert.AreEqual(10, screen.Recipe.rightY);
            Click("FusionFont3"); Click("FusionConfirm"); yield return null; Assert.IsTrue(screen.BirthPlaying);
            while (screen.Birth != null && screen.Birth.Elapsed < .7f) yield return null; Capture("fusion_birth_01_suction");
            while (screen.Birth != null && screen.Birth.Elapsed < 1.65f) yield return null;
            Assert.IsTrue(screen.Birth.ImpactReached);
            var burst = Object.FindAnyObjectByType<FusionBurstGraphic>(); Assert.IsNotNull(burst.GetComponent<CanvasRenderer>());
            Canvas.ForceUpdateCanvases(); var mesh = burst.canvasRenderer.GetMesh(); Assert.IsNotNull(mesh); Assert.Greater(mesh.vertexCount, 500);
            Capture("fusion_birth_02_impact");
            while (screen.Birth != null && screen.Birth.Elapsed < 2.7f) yield return null; Capture("fusion_birth_03_reveal");
            while (screen.BirthPlaying) yield return null;
            Assert.AreEqual(FusionScreen.Page.Finish, screen.CurrentPage);
            GameObject.Find("FusionNameInput").GetComponent<WebCharacterInput>().ReceiveText("月樹"); yield return null; Capture("fusion_04_finish");
            Click("FusionSave"); yield return null; Assert.AreEqual(1, FusionLibrary.All().Count);
            Assert.IsTrue(FusionLibrary.Save(new FusionData { name = "蒼流" }, FontStyleId.Gothic, null, out _, out _));
            Assert.IsTrue(FusionLibrary.Save(new FusionData { name = "木波", rightSource = "林" }, FontStyleId.Gothic, null, out _, out _));
            screen.Show(FusionScreen.Page.Library); yield return null; Capture("fusion_05_library");
            Click("LibraryEdit"); yield return null; Assert.AreEqual(FusionScreen.Page.Layout, screen.CurrentPage);
            GameSettings.Change(x => x.reducedEffects = true); Click("FusionConfirm"); yield return null; Assert.IsTrue(screen.Birth.Reduced);
            while (screen.BirthPlaying) yield return null;
            Click("FusionSaveEquip"); yield return null; yield return null;
            Assert.IsFalse(FusionScreen.IsOpen); var customize = Object.FindAnyObjectByType<CustomizeScreen>(); Assert.IsNotNull(customize);
            Assert.AreEqual("月樹", customize.Customizer.Current.DisplayName); Assert.IsTrue(customize.Customizer.Current.HasFusion);
            yield return new WaitForSecondsRealtime(.5f);
            Capture("fusion_06_equipped"); Click("SplitMing"); Assert.IsTrue(customize.Customizer.Current.IsSplitMing); yield return null; Capture("fusion_07_split");
            Click("CustomizeFusion"); yield return null; Assert.IsFalse(customize.TextInputVisible);
            Click("FusionLibrary"); yield return null; Click("LibraryDelete"); yield return null;
            Assert.IsFalse(GameObject.Find("LibraryEquip").GetComponent<Button>().IsInteractable());
            Click("DeleteCancel"); yield return null; Assert.IsTrue(GameObject.Find("LibraryEquip").GetComponent<Button>().IsInteractable()); Assert.AreEqual(3, FusionLibrary.All().Count);
            Click("LibraryDelete"); yield return null; Click("DeleteConfirm"); yield return null; Assert.AreEqual(2, FusionLibrary.All().Count);
            screen = Object.FindAnyObjectByType<FusionScreen>(); screen.Close(); yield return null;
            Assert.AreEqual("月樹", customize.Customizer.Current.DisplayName); Assert.IsTrue(customize.TextInputVisible);
            Assert.IsTrue(GameObject.Find("CustomizeFusion").GetComponent<Button>().IsInteractable());
            Click("NormalCharacter"); Assert.IsFalse(customize.Customizer.Current.HasFusion);
        }
        static void Capture(string name) { MenuUiTests.Capture(name); MenuUiTests.Capture(name + "_880x450", 880, 450); }
        [Test] public void LegacyVersionOneLibraryAndEquippedBuildKeepTheirRecipeAndPixels()
        {
            const string json = "{\"version\":1,\"name\":\"旧文字\",\"leftSource\":\"河\",\"rightSource\":\"晴\",\"leftWidth\":1,\"leftHeight\":1,\"rightWidth\":1,\"rightHeight\":1,\"leftX\":0,\"leftY\":0,\"rightX\":0,\"rightY\":0,\"gap\":8}";
            var legacy = JsonUtility.FromJson<FusionData>(json); Assert.IsTrue(legacy.Validate(out _));
            Assert.AreEqual("1|河|晴|1|1|1|1|0|0|0|0|8", legacy.Key); Assert.AreEqual("清", legacy.Character);
            foreach (FontStyleId f in System.Enum.GetValues(typeof(FontStyleId)))
            {
                var pixels = Glyph(legacy, f).texture.GetPixels32();
                Assert.IsTrue(FusionLibrary.Save(legacy, f, null, out var record, out _));
                var saved = FusionLibrary.All().Single(r => r.id == record.id); Assert.AreEqual(1, saved.recipe.version);
                var remote = JsonUtility.FromJson<FighterBuildData>(JsonUtility.ToJson(saved.Build()));
                FusionGlyphs.ClearCache(); Assert.AreEqual(pixels, Glyph(remote.fusion, f).texture.GetPixels32());
            }
        }
        [UnityTest] public IEnumerator AllSourceLayoutsAndBothComponentsAreSelectableAndSavedThroughUi()
        {
            GameFlow.ToModeSelect(); yield return null; yield return null;
            Click("FusionMode"); yield return null; Click("FusionNew"); yield return null;
            var screen = Object.FindAnyObjectByType<FusionScreen>();
            Click("SourceFilter_2"); yield return null; Click("SourceFilter_5"); yield return null;
            Click("Source_思"); yield return null; Assert.AreEqual("心", screen.Recipe.Left); Capture("fusion_all_parts_top_bottom");
            Click("ChooseRight"); yield return null; Click("SourceFilter_2"); yield return null;
            Click("Source_思"); yield return null; Assert.AreEqual("心", screen.Recipe.Right);
            Click("ChooseLeft"); yield return null; Click("SourceFilter_3"); yield return null;
            Click("SourceFilter_4"); yield return null; Click("Source_国"); yield return null;
            Assert.AreEqual("囗", screen.Recipe.Left); Capture("fusion_all_parts_enclosure");
            Click("ToLayout"); yield return null; Click("FusionLayout1"); yield return null;
            Assert.AreEqual(KanjiLayout.TopBottom, screen.Recipe.layout); Capture("fusion_all_parts_vertical_layout");
            Click("FusionLayout2"); yield return null; Assert.AreEqual(KanjiLayout.Enclosure, screen.Recipe.layout);
            Capture("fusion_all_parts_enclosure_layout");
            GameSettings.Change(x => x.reducedEffects = true); Click("FusionConfirm"); yield return null;
            while (screen.BirthPlaying) yield return null;
            Assert.AreEqual("囲み合成", GameObject.Find("FusionType").GetComponent<Text>().text);
            GameObject.Find("FusionNameInput").GetComponent<WebCharacterInput>().ReceiveText("心の箱");
            yield return null; Capture("fusion_all_parts_finish"); Click("FusionSave"); yield return null;
            var saved = FusionLibrary.All().Single(); Assert.AreEqual("心の箱", saved.recipe.name);
            Assert.AreEqual(KanjiLayout.Enclosure, saved.recipe.layout); Assert.AreEqual("心", saved.recipe.Right);
            Click("LibraryEdit"); yield return null; Assert.AreEqual(saved.recipe.Key, screen.Recipe.Key);
            screen.Close(); yield return null;
        }
        [UnityTest] public IEnumerator HttpRoomRoundTripRebuildsIdenticalMasksCollidersAndGripPhysics()
        {
            var host = new FriendRoomClient("http://127.0.0.1:8787"); var guest = new FriendRoomClient("http://127.0.0.1:8787");
            var build = new FusionRecord { recipe = new FusionData { name = "心雪", leftSource = "思", rightSource = "雪",
                leftUseSecond = true, rightUseFirst = true, layout = KanjiLayout.TopBottom }, font = FontStyleId.Brush }.Build(); build.gripType = GripType.DualWield; build.splitMing = true;
            build.EnsureIndependentGrips(); build.customGrip = build.offhandCustomGrip = true; build.gripPoint = new Vector2(.2f, .7f); build.offhandGripPoint = new Vector2(.8f, .3f);
            yield return host.Create(build); Assert.IsNull(host.Error);
            yield return guest.Join(host.Code, new FighterBuildData { character = "刀" }); Assert.IsNull(guest.Error);
            var remote = guest.State.players[0].build; Assert.AreEqual(build.fusion.Key, remote.fusion.Key); Assert.AreEqual(build.fusion.name, remote.fusion.name);
            foreach (bool offhand in new[] { false, true })
            {
                Assert.IsTrue(SplitMingGlyphs.TryGet(build, CombatBalance.Default, GlyphCalibration.Default, offhand, out var localGlyph, out _));
                Assert.IsTrue(SplitMingGlyphs.TryGet(remote, CombatBalance.Default, GlyphCalibration.Default, offhand, out var remoteGlyph, out _));
                Assert.AreEqual(localGlyph.texture.GetPixels32(), remoteGlyph.texture.GetPixels32()); Assert.AreEqual(localGlyph.features.colliderRects, remoteGlyph.features.colliderRects);
                var localGeometry = FighterFactory.ResolveWeapon(offhand ? build.ForOffhand() : build, localGlyph, StatCalculator.Compute(localGlyph, CombatBalance.Default), CombatBalance.Default, CustomizeBalance.Default, out var lm);
                var remoteGeometry = FighterFactory.ResolveWeapon(offhand ? remote.ForOffhand() : remote, remoteGlyph, StatCalculator.Compute(remoteGlyph, CombatBalance.Default), CombatBalance.Default, CustomizeBalance.Default, out var rm);
                Assert.AreEqual(lm, rm); Assert.AreEqual(localGeometry.scale, remoteGeometry.scale); Assert.AreEqual(localGeometry.gripPx, remoteGeometry.gripPx);
            }
            yield return host.SetReady(true); yield return guest.SetReady(true); yield return host.StartBattle(); Assert.IsNull(host.Error);
            yield return guest.Poll(); Assert.AreEqual(host.State.seed, guest.State.seed); Assert.AreEqual("battle", guest.State.stage);
            yield return host.Leave();
        }
        [UnityTest] public IEnumerator InventedWeaponsSurviveIndependentGripsSwapsPreviewAndRealMatch()
        {
            GameFlow.ToCustomize(); yield return null; yield return null; var screen = Object.FindAnyObjectByType<CustomizeScreen>(); var c = screen.Customizer;
            Assert.IsTrue(c.SetFusion(Invented(), FontStyleId.Gothic)); c.SetGrip(GripType.DualWield); c.SetOffhandCharacter("月");
            c.SetGripPoint(new Vector2(.25f, .7f)); c.SetOffhandGripPoint(new Vector2(.8f, .4f));
            Assert.IsTrue(BattleBuilder.MainGlyph(c.Current.ToLoadout(), CombatBalance.Default, GlyphCalibration.Default, out var fused, out _));
            c.SwapHands(); Assert.IsFalse(c.Current.HasFusion); Assert.IsNotNull(c.Current.offhandFusion);
            var off = BattleBuilder.OffhandGlyph(c.Current.ToLoadout(), null, CombatBalance.Default, GlyphCalibration.Default); Assert.AreSame(fused, off);
            c.SwapHands(); Assert.IsTrue(c.Current.HasFusion); Assert.AreEqual(new Vector2(.25f, .7f), c.Current.gripPoint);
            Assert.IsTrue(c.SetSplitMing(true)); c.SwapHands(); c.SetFont(FontStyleId.Mincho); yield return null;
            var stored = BuildRuntimeStore.Get(0).Clone(); Assert.AreEqual(c.Current.fusion.Key, stored.fusion.Key); Assert.IsTrue(stored.splitSwapped);
            battle = SimHarness.Build(98127, stored.ToLoadout(), new FighterBuildData { character = "刀" }.ToLoadout(), duration: 4);
            Assert.IsNotNull(battle.Left.Offhand); yield return SimHarness.RunToEnd(battle, 4); Assert.IsTrue(battle.Director.Ended);
        }
    }
}
