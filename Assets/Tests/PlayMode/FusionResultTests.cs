using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class FusionResultTests
    {
        FighterBuildData oldLeft, oldRight;
        BattleStageId oldStage;
        bool oldActive, oldSelfControl;
        int oldMotion;
        [SetUp] public void Setup()
        {
            oldLeft = BuildRuntimeStore.Get(0).Clone(); oldRight = BuildRuntimeStore.Get(1).Clone(); oldActive = BuildRuntimeStore.Active;
            oldStage = StageSession.Selected; oldMotion = PlayerPrefs.GetInt("MenuReducedMotion", 0); oldSelfControl = GameSettings.SelfControl;
            GameFlow.ToModeSelect(); PlayerPrefs.SetInt("MenuReducedMotion", 1); GameSettings.Change(x => x.selfControl = false); SimHarness.Begin(false);
        }
        [TearDown] public void Cleanup()
        {
            SimHarness.End(); GameFlow.ToTitle(); BuildRuntimeStore.Set(0, oldLeft); BuildRuntimeStore.Set(1, oldRight); BuildRuntimeStore.Active = oldActive;
            StageSession.Selected = oldStage; StageSession.OpenOnCustomize = false; PlayerPrefs.SetInt("MenuReducedMotion", oldMotion); GameSettings.Change(x => x.selfControl = oldSelfControl);
        }
        static FighterBuildData Fused(string name, FontStyleId font = FontStyleId.Brush) => new FusionRecord
        { font = font, recipe = new FusionData { name = name, leftSource = "紅", rightSource = "海" } }.Build();
        static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
        static IEnumerator Ready() { yield return null; yield return null; yield return new WaitForSecondsRealtime(.2f); }
        static void EndBattle(BattleBootstrap boot, int winner)
        {
            var d = boot.Battle.Director; d.Replay = null; d.enabled = false;
            foreach (var fighter in d.Fighters) fighter.Runtime.invulnerable = true;
            for (int i = 0; i < 220; i++) d.SimulateStep(); // Advance the real countdown before forcing the terminal health condition.
            for (int side = 0; side < 2; side++) if (side != winner)
            {
                var f = d.Fighters[side]; f.Runtime.hp = 0; f.Knockdown.EnterKO(d.Context.SimTime);
            }
            for (int i = 0; i < 100 && !d.Ended; i++) d.SimulateStep();
            Assert.IsTrue(d.Ended); Assert.AreEqual(winner, d.Result.winner);
        }
        static void AssertArt(int side, FighterBuildData build)
        {
            var weapon = GameObject.Find("ResultFighter" + side).transform.Find("GlyphWeapon").GetComponent<Text>();
            if (!build.HasFusion) { Assert.IsTrue(weapon.enabled); Assert.IsNull(weapon.transform.Find("FusionGlyphDisplay")); return; }
            Assert.IsFalse(weapon.enabled, "An invented glyph must never draw its internal Unicode placeholder.");
            var raw = weapon.transform.Find("FusionGlyphDisplay").GetComponent<RawImage>();
            Assert.IsTrue(raw.enabled); Assert.IsTrue(FusionGlyphs.TryGet(build.fusion, build.fontType, CombatBalance.Default, GlyphCalibration.Default, -1, out var glyph, out _));
            Assert.AreSame(GlyphFlip.Apply(glyph, build.weaponFlip).texture, raw.texture);
        }
        [UnityTest] public IEnumerator SavedItoAndUmiGlyphSurvivesResultRematchAndReturn()
        {
            yield return Ready(); var fused = Fused("海織"); Assert.AreEqual("合", fused.character); Assert.AreEqual("糸", fused.fusion.Left); Assert.AreEqual("毎", fused.fusion.Right);
            Assert.IsTrue(FusionLibrary.Save(fused.fusion, fused.fontType, null, out var record, out _));
            // Remove only this test's library entry; equipped copies and recipes must survive.
            fused = record.Build(); FusionLibrary.Delete(record.id);
            BuildRuntimeStore.Set(0, fused); BuildRuntimeStore.Set(1, FighterBuildData.Default("投")); StageSession.Selected = BattleStageId.Dojo;
            GameFlow.ToBattle(); yield return Ready(); var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            EndBattle(boot, 0); yield return Ready();
            Assert.AreEqual("海織 の勝ち", GameObject.Find("ResultTitle").GetComponent<Text>().text);
            Assert.AreEqual("P1 海織   VS   投 P2", GameObject.Find("ResultMatchup").GetComponent<Text>().text);
            AssertArt(0, fused); AssertArt(1, FighterBuildData.Default("投"));
            MenuUiTests.Capture("fusion_result_fixed"); MenuUiTests.Capture("fusion_result_fixed_880x450", 880, 450);
            Button("Again").onClick.Invoke(); yield return Ready(); Assert.IsFalse(boot.Battle.Director.Ended);
            Assert.AreEqual(fused.fusion.Key, boot.Battle.Left.Loadout.build.fusion.Key);
            EndBattle(boot, 0); yield return Ready(); Assert.AreEqual("海織 の勝ち", GameObject.Find("ResultTitle").GetComponent<Text>().text);
            Button("ChangeStage").onClick.Invoke(); yield return Ready();
            var customize = Object.FindAnyObjectByType<CustomizeScreen>(); Assert.IsTrue(customize.StageVisible); Assert.IsTrue(customize.Customizer.Get(0).HasFusion);
            Assert.AreEqual(fused.fusion.Key, customize.Customizer.Get(0).fusion.Key);
        }
        [UnityTest] public IEnumerator UnnamedLongNamedBothSidesAndDrawUseRecipesInResults()
        {
            yield return Ready();
            for (int scenario = 0; scenario < 3; scenario++)
            {
                var left = Fused(""); var right = scenario == 0 ? FighterBuildData.Default("投") : Fused("海織海織海織海織海織海織", FontStyleId.Mincho);
                right.weaponFlip = WeaponFlip.Both;
                BuildRuntimeStore.Set(0, left); BuildRuntimeStore.Set(1, right); StageSession.Selected = BattleStageId.Dojo;
                GameFlow.ToBattle(); yield return Ready(); var boot = Object.FindAnyObjectByType<BattleBootstrap>();
                int winner = scenario == 2 ? -1 : scenario; EndBattle(boot, winner); yield return Ready();
                Assert.AreEqual(winner < 0 ? "引き分け" : (winner == 0 ? "糸＋毎" : right.DisplayName) + " の勝ち", GameObject.Find("ResultTitle").GetComponent<Text>().text);
                Assert.AreEqual("P1 糸＋毎   VS   " + right.DisplayName + " P2", GameObject.Find("ResultMatchup").GetComponent<Text>().text);
                AssertArt(0, left); AssertArt(1, right);
                MenuUiTests.Capture("fusion_result_case_" + scenario); MenuUiTests.Capture("fusion_result_case_" + scenario + "_880x450", 880, 450);
            }
        }
    }
}
