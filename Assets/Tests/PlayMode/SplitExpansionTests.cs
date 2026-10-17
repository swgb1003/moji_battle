using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class SplitExpansionTests
    {
        FighterBuildData left, right;
        BattleInstance battle;
        [SetUp] public void Setup()
        {
            left = BuildRuntimeStore.Get(0).Clone(); right = BuildRuntimeStore.Get(1).Clone(); SimHarness.Begin(false);
        }
        [TearDown] public void Cleanup()
        {
            battle?.Destroy(); battle = null; SimHarness.End(); GameFlow.ToTitle();
            BuildRuntimeStore.Set(0, left); BuildRuntimeStore.Set(1, right); BuildRuntimeStore.Active = false;
        }
        static FighterBuildData Split(string ch, FontStyleId font = FontStyleId.Gothic) => new FighterBuildData
        { character = ch, fontType = font, gripType = GripType.DualWield, splitMing = true, independentOffhandGrip = true };

        [Test]
        public void StructuralDataHasThreeLayoutsAndDoesNotGuessUnsupportedCharacters()
        {
            Assert.Greater(SplitKanjiCatalog.Entries.Length, 100);
            var seen = new HashSet<string>(); var layouts = new HashSet<KanjiLayout>();
            foreach (var entry in SplitKanjiCatalog.Entries)
            {
                Assert.IsTrue(seen.Add(entry.Character)); layouts.Add(entry.Layout);
                Assert.IsTrue(SplitKanjiCatalog.TryGet(entry.Character, out var same)); Assert.AreEqual(entry.First, same.First);
                Assert.That(entry.Divider, Is.InRange(.1f, .9f));
            }
            Assert.AreEqual(3, layouts.Count);
            foreach (string ch in new[] { "一", "日", "木", "A", "あ", "😀", "" }) Assert.IsFalse(SplitKanjiCatalog.Supports(ch), ch);
            Assert.IsFalse(SplitKanjiCatalog.Supports(null));
            Assert.AreEqual(KanjiLayout.TopBottom, GetEntry("思").Layout);
            Assert.AreEqual(KanjiLayout.Enclosure, GetEntry("国").Layout);
        }
        static SplitKanjiCatalog.Entry GetEntry(string ch)
        { Assert.IsTrue(SplitKanjiCatalog.TryGet(ch, out var entry)); return entry; }

        [UnityTest]
        public IEnumerator StructuralCatalogPickerAndBothHandsSurviveSaveAndBattle()
        {
            GameFlow.ToCustomize(); yield return null; yield return null;
            var screen = Object.FindAnyObjectByType<CustomizeScreen>(); var c = screen.Customizer;
            c.SetSize(WeaponSize.M);
            foreach (string ch in new[] { "校", "思", "国" })
            {
                GameObject.Find("SplitCatalog").GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(screen.SplitCatalogVisible); Assert.IsFalse(screen.TextInputVisible);
                var entry = GetEntry(ch);
                if (entry.Layout != KanjiLayout.LeftRight)
                    GameObject.Find("SplitLayout_" + (int)entry.Layout).GetComponent<Button>().onClick.Invoke();
                yield return null;
                MenuUiTests.Capture("structural_catalog_" + entry.Layout);
                GameObject.Find("SplitPick_" + ch).GetComponent<Button>().onClick.Invoke();
                Assert.IsFalse(screen.SplitCatalogVisible); Assert.IsTrue(screen.TextInputVisible);
                Assert.IsTrue(c.Current.IsSplitMing); Assert.AreEqual(ch, c.Current.character);
                c.SetGripPoint(new Vector2(.2f, .8f)); c.SetOffhandGripPoint(new Vector2(.8f, .2f)); c.SwapHands();
                Assert.AreEqual(entry.Second, c.Current.MainWeaponLabel); Assert.AreEqual(entry.First, c.Current.OffhandWeaponLabel);
                var saved = JsonUtility.FromJson<FighterBuildData>(JsonUtility.ToJson(c.Current));
                Assert.IsTrue(saved.splitSwapped); Assert.AreEqual(new Vector2(.8f, .2f), saved.gripPoint);
                battle = SimHarness.Build(98910, saved.ToLoadout(), SimHarness.Custom("口"), 1f);
                Assert.AreEqual(entry.Second, battle.Left.Glyph.grapheme); Assert.AreEqual(entry.First, battle.Left.Offhand.Glyph.grapheme);
                battle.Destroy(); battle = null;
                yield return null;
            }
            c.SetCharacter("一"); Assert.IsFalse(c.SetSplitMing(true)); Assert.IsFalse(c.Current.IsSplitMing);
            c.SetSize(WeaponSize.XL); Assert.IsFalse(GameObject.Find("SplitCatalog").GetComponent<Button>().interactable);
        }

        [Test]
        public void AllStructuralKanjiAndFourFontsHaveCleanUsableParts()
        {
            string dir = Path.Combine(SimHarness.ReportDir, "SplitExpansion"); Directory.CreateDirectory(dir);
            var failures = new List<string>();
            foreach (var entry in SplitKanjiCatalog.Entries)
            foreach (FontStyleId font in System.Enum.GetValues(typeof(FontStyleId)))
            {
                try
                {
                var build = Split(entry.Character, font);
                Assert.IsTrue(GlyphCatalog.TryGet(entry.Character, font, CombatBalance.Default, GlyphCalibration.Default, out var original, out var error), error);
                Export(original.texture, Path.Combine(dir, entry.Character + "_" + font + "_source.png"));
                float totalMass = 0;
                var splitPixels = new Color32[2][]; var partBounds = new RectInt[2];
                for (int hand = 0; hand < 2; hand++)
                {
                    string context = entry.Character + " " + font + " " + hand;
                    Assert.IsTrue(SplitMingGlyphs.TryGet(build, CombatBalance.Default, GlyphCalibration.Default, hand == 1, out var part, out error), context + ": " + error);
                    Assert.AreEqual(hand == 0 ? entry.Left : entry.Right, part.grapheme, context);
                    Assert.Greater(part.features.colliderRects.Length, 0, context);
                    var pixels = part.texture.GetPixels32();
                    splitPixels[hand] = pixels;
                    partBounds[hand] = part.features.inkBounds;
                    Export(part.texture, Path.Combine(dir, entry.Character + "_" + font + "_" + hand + ".png"));
                    if (entry.Character == "明") Assert.AreEqual(1, Components(pixels, part.texture.width).Count,
                        context + ": 日 and 月 should each be a single connected weapon without a stray sweeping stroke");
                    foreach (var component in Components(pixels, part.texture.width))
                        Assert.GreaterOrEqual(component, 24, context + ": a detached fragment remains");
                    var geo = FighterFactory.ResolveWeapon(hand == 1 ? build.ForOffhand() : build, part,
                        StatCalculator.Compute(part, CombatBalance.Default), CombatBalance.Default, CustomizeBalance.Default, out var mass);
                    totalMass += mass;
                    Assert.Greater(geo.length, .1f, context);
                }
                Assert.AreEqual(StatCalculator.Compute(original, CombatBalance.Default).weaponMass, totalMass, .001f, entry.Character + " " + font);
                var sourcePixels = original.texture.GetPixels32(); int ink = 0, removed = 0, overlaps = 0, introduced = 0;
                var centres = new Vector2[2]; var counts = new int[2];
                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    int a = splitPixels[0][i].a, b = splitPixels[1][i].a;
                    if (a > 0 && b > 0) overlaps++;
                    if (Mathf.Max(a, b) > sourcePixels[i].a) introduced++;
                    if (sourcePixels[i].a >= 128) { ink++; if (a < 128 && b < 128) removed++; }
                    for (int hand = 0; hand < 2; hand++) if (splitPixels[hand][i].a >= 128)
                    { centres[hand] += new Vector2(i % original.texture.width, i / original.texture.width); counts[hand]++; }
                }
                Assert.AreEqual(0, overlaps, entry.Character + ": components overlap");
                Assert.AreEqual(0, introduced, entry.Character + ": invented strokes");
                Assert.Less(removed / (float)ink, .02f, entry.Character + " " + font + ": lost actual strokes");
                for (int hand = 0; hand < 2; hand++) centres[hand] /= counts[hand];
                if (entry.Layout == KanjiLayout.LeftRight) Assert.Less(centres[0].x, centres[1].x, entry.Character);
                if (entry.Layout == KanjiLayout.TopBottom) Assert.Greater(centres[0].y, centres[1].y, entry.Character);
                if (entry.First == "亻") Assert.Greater(partBounds[0].height, original.features.inkBounds.height * .65f,
                    entry.Character + " " + font + ": the thin radical's vertical stroke was absorbed");
                if (entry.Layout == KanjiLayout.Enclosure)
                {
                    Assert.Less(partBounds[0].xMin, partBounds[1].xMin, entry.Character + " outer left");
                    Assert.Greater(partBounds[0].xMax, partBounds[1].xMax, entry.Character + " outer right");
                    Assert.Less(partBounds[0].yMin, partBounds[1].yMin, entry.Character + " outer bottom");
                    Assert.Greater(partBounds[0].yMax, partBounds[1].yMax, entry.Character + " outer top");
                    var box = original.features.inkBounds;
                    for (int sample = 0; sample < 3; sample++)
                    {
                        int y = Mathf.RoundToInt(box.yMin + box.height * (.35f + .15f * sample));
                        int first = box.xMin, last = box.xMax - 1;
                        while (first < last && sourcePixels[y * original.texture.width + first].a < 128) first++;
                        while (last > first && sourcePixels[y * original.texture.width + last].a < 128) last--;
                        Assert.GreaterOrEqual(splitPixels[0][y * original.texture.width + first].a, 128, entry.Character + " outer left side");
                        Assert.GreaterOrEqual(splitPixels[0][y * original.texture.width + last].a, 128, entry.Character + " outer right side");
                    }
                }
                }
                catch (AssertionException exception) { failures.Add(entry.Character + " " + font + ": " + exception.Message); }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
        static List<int> Components(Color32[] pixels, int width)
        {
            var visited = new bool[pixels.Length]; var counts = new List<int>();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (visited[i] || pixels[i].a < 128) continue;
                var q = new Queue<int>(); q.Enqueue(i); visited[i] = true; int count = 0;
                while (q.Count > 0)
                {
                    int p = q.Dequeue(), x = p % width, y = p / width; count++;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= width) continue;
                        int n = ny * width + nx;
                        if (!visited[n] && pixels[n].a >= 128) { visited[n] = true; q.Enqueue(n); }
                    }
                }
                counts.Add(count);
            }
            return counts;
        }
        static void Export(Texture2D source, string path)
        {
            var px = source.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(px[i].a, px[i].a, px[i].a, 255);
            var outTex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            outTex.SetPixels32(px); outTex.Apply(); File.WriteAllBytes(path, outTex.EncodeToPNG()); Object.Destroy(outTex);
        }

        [UnityTest]
        public IEnumerator ModeAndCustomizeHaveNoBottomProgressAndNewCharactersCanSplit()
        {
            GameFlow.ToModeSelect(); yield return null; yield return null;
            Assert.IsNull(GameObject.Find("StageProgress"));
            GameFlow.ToCustomize(); yield return null; yield return null;
            Assert.IsNull(GameObject.Find("StageProgress"));
            var screen = Object.FindAnyObjectByType<CustomizeScreen>(); var customizer = screen.Customizer;
            foreach (var entry in SplitKanjiCatalog.Entries)
            {
                customizer.SetCharacter(entry.Character); customizer.SetSize(WeaponSize.M);
                GameObject.Find("SplitMing").GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(customizer.Current.IsSplitMing, entry.Character);
                Assert.AreEqual(entry.Left, screen.Preview.EditingGlyph.grapheme);
                screen.SelectGripHand(true); Assert.AreEqual(entry.Right, screen.Preview.EditingGlyph.grapheme);
                screen.SelectGripHand(false);
            }
            customizer.SetCharacter("明"); customizer.SetSplitMing(true); customizer.SetFont(FontStyleId.Brush);
            yield return new WaitForSecondsRealtime(.8f);
            MenuUiTests.Capture("split_expansion_brush_ming");
            MenuUiTests.Capture("split_expansion_brush_ming_880x450", 880, 450);
            foreach (string ch in new[] { "思", "国", "花" })
            {
                customizer.SetCharacter(ch); customizer.SetSplitMing(true);
                yield return new WaitForSecondsRealtime(.5f); MenuUiTests.Capture("structural_split_" + ch);
            }
        }

        [UnityTest]
        public IEnumerator ManualKicksRepeatImmediatelyAfterTheirAnimationEvenWithXLWeapon()
        {
            battle = SimHarness.Build(97211, SimHarness.Custom("鬱", WeaponSize.XL), SimHarness.Custom("口"), 60f);
            battle.Director.enabled = false;
            var player = battle.Left.Brain.AttachPlayer(); battle.Context.PlayerSide = 0;
            PlayControl.ApplyDifficulty(battle.Right, ComDifficulty.Sandbag, null);
            for (int i = 0; i < 50; i++) battle.Director.SimulateStep();
            float lastStart = -1;
            for (int kick = 0; kick < 4; kick++)
            {
                int attack = battle.Left.Runtime.currentAttackId;
                player.Input.Press(PlayerButton.Kick, 0, 0);
                int steps = 0;
                while (battle.Left.Runtime.currentAttackId == attack && steps++ < 65) battle.Director.SimulateStep();
                Assert.AreNotEqual(attack, battle.Left.Runtime.currentAttackId, "V should start the next kick without a multi-second cooldown.");
                Assert.AreEqual(AttackStyle.FrontKick, battle.Left.Runtime.attackStyle);
                float start = battle.Context.SimTime;
                if (lastStart >= 0) Assert.Less(start - lastStart, 1.2f);
                lastStart = start;
                // Buffer the next input during the preceding kick, without changing timers.
                for (int i = 0; i < 25; i++) battle.Director.SimulateStep();
            }
            float time = battle.Context.SimTime;
            battle.Right.StartAttack(time, AttackStyle.FrontKick);
            Assert.AreEqual(time + battle.Right.Balance.kickCooldown, battle.Right.Runtime.kickReadyAt, .001f, "AI cooldown should remain unchanged.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator EverySupportedKanjiCanCompleteABattle()
        {
            int seed = 94100;
            foreach (var entry in SplitKanjiCatalog.Entries)
            {
                battle = SimHarness.Build(seed++, Split(entry.Character).ToLoadout(), SimHarness.Custom("口"), 3f);
                for (int i = 0; i < 1500 && !battle.Director.Ended; i++) battle.Director.SimulateStep();
                Assert.IsTrue(battle.Director.Ended, entry.Character);
                Assert.AreEqual(0, battle.Director.Telemetry.duplicateBodyHits);
                Assert.IsFalse(float.IsNaN(battle.Left.Runtime.hp));
                battle.Destroy(); battle = null; yield return null;
            }
        }
    }
}
