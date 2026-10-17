using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class FusionSpecialTests
    {
        string saved, achievements; FighterBuildData left, right; string oldServer;
        [SetUp] public void Setup()
        {
            saved = PlayerPrefs.GetString("MojiBattle.Specials.v1", ""); achievements = PlayerPrefs.GetString(AchievementStore.PrefKey, ""); SpecialMoveLibrary.ClearAll();
            left = BuildRuntimeStore.Get(0).Clone(); right = BuildRuntimeStore.Get(1).Clone();
            oldServer = FriendRoomSession.DevelopmentServerOverride;
            FriendRoomSession.DevelopmentServerOverride = "http://127.0.0.1:8787"; SimHarness.Begin(false);
        }
        [TearDown] public void Cleanup()
        {
            SimHarness.End(); GameFlow.ToTitle(); FriendRoomSession.DevelopmentServerOverride = oldServer;
            PlayerPrefs.SetString("MojiBattle.Specials.v1", saved); SpecialMoveLibrary.Reload(); PlayerPrefs.Save();
            PlayerPrefs.SetString(AchievementStore.PrefKey, achievements); AchievementStore.Reload();
            BuildRuntimeStore.Set(0, left); BuildRuntimeStore.Set(1, right); BuildRuntimeStore.Active = false;
        }
        static FighterBuildData Fused(string source = "紅") => new FusionRecord { font = FontStyleId.Brush,
            recipe = new FusionData { name = "海織", leftSource = source, rightSource = "海" } }.Build();
        static SpecialMoveData Move(FighterBuildData b, string name = "蒼流一閃") => new SpecialMoveData
        { character = b.character, name = name, fusionEnabled = true, fusion = b.fusion.Clone(), steps = new[] { "DashSlash", "Spin" } };
        static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
        static IEnumerator Ready() { yield return null; yield return null; yield return new WaitForSecondsRealtime(.25f); }
        static void Capture(string name) { MenuUiTests.Capture(name); MenuUiTests.Capture(name + "_880x450", 880, 450); }

        [Test] public void FreeNamesAreBoundToTheirFusionWithIndependentSlotsAndPersistentCopies()
        {
            var b = Fused(); var other = Fused("持"); Assert.AreEqual("合", b.character); Assert.AreEqual(b.character, other.character);
            Assert.IsNull(SpecialMoveRules.CheckName("蒼流一閃", b.character, true));
            Assert.IsNotNull(SpecialMoveRules.CheckName("蒼流一閃", "合"));
            foreach (string invalid in new[] { "", " ", "字字字字字字字字", "<b>", "a\nb" })
                Assert.IsNotNull(SpecialMoveRules.CheckName(invalid, b.character, true));
            foreach (string name in new[] { "蒼流一閃", "昇龍波", "星の舞" }) Assert.IsTrue(SpecialMoveLibrary.Add(Move(b, name)));
            Assert.IsFalse(SpecialMoveLibrary.Add(Move(b, "四つ目")));
            Assert.IsTrue(SpecialMoveLibrary.Add(Move(other))); Assert.AreEqual(3, SpecialMoveLibrary.For(b).Count);
            Assert.AreEqual(1, SpecialMoveLibrary.For(other).Count); Assert.IsEmpty(SpecialMoveLibrary.For("合"));
            Assert.IsTrue(SpecialMoveLibrary.Add(new SpecialMoveData { character = "合", name = "合輪斬" }));
            Assert.AreEqual(1, SpecialMoveLibrary.For("合").Count);
            b.special = Move(b); Assert.IsNotNull(b.EquippedSpecial);
            var copy = JsonUtility.FromJson<FighterBuildData>(JsonUtility.ToJson(b));
            Assert.AreEqual("蒼流一閃", copy.EquippedSpecial.name); copy.fusion.name = "名前を変える";
            Assert.AreEqual("蒼流一閃", copy.EquippedSpecial.name); copy.fusion.gap = 18;
            Assert.IsNull(copy.EquippedSpecial); Assert.IsNull(SpecialMoveRules.Sanitize(b.special, "合"));
            Assert.IsNull(SpecialMoveRules.Sanitize(b.special, other.character, other.fusion));
            var clone = b.special.Clone(); clone.fusion.leftSource = "林"; Assert.AreEqual("紅", b.special.fusion.leftSource);
            SpecialMoveLibrary.Reload(); Assert.AreEqual(3, SpecialMoveLibrary.For(b).Count);
            b.special = new SpecialMoveData { character = "合", name = "合輪斬" };
            Assert.IsTrue(b.EquippedSpecial.fusionEnabled, "Previously equipped legacy moves remain usable.");
            StringAssert.StartsWith("創字", BuiltinSpecialMoves.For(b).name);
            Assert.AreEqual("合輪斬", BuiltinSpecialMoves.For("合").name);
        }

        [UnityTest] public IEnumerator ActualUiGeneratesSavesEquipsAndUsesFreeNamedFusionSpecialInBattleAndRooms()
        {
            GameFlow.ToCustomize(); yield return Ready(); var screen = Object.FindAnyObjectByType<CustomizeScreen>();
            var b = Fused(); Assert.IsTrue(screen.Customizer.SetFusion(b.fusion, b.fontType)); yield return Ready();
            Button("SpecialMoves").onClick.Invoke(); yield return Ready(); var panel = screen.SpecialPanel;
            StringAssert.Contains("海織", GameObject.Find("SpecialTitle").GetComponent<Text>().text);
            StringAssert.Contains("自由に命名", GameObject.Find("NameHint").GetComponent<Text>().text);
            Assert.IsFalse(GameObject.Find("NameHint").GetComponent<Text>().text.Contains("「合」"));
            panel.NameInput.text = "蒼流一閃"; Assert.IsTrue(panel.GenerateButton.button.interactable);
            Capture("fusion_special_free_name"); panel.Generate();
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (panel.Busy && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsFalse(panel.Busy); StringAssert.Contains("登録して装備", panel.Status);
            var made = screen.Customizer.Current.EquippedSpecial; Assert.IsNotNull(made); Assert.AreEqual("蒼流一閃", made.name);
            Assert.IsTrue(made.fusionEnabled); Assert.AreEqual(b.fusion.Key, made.fusion.Key);
            Capture("fusion_special_generated"); panel.Hide(); yield return Ready();
            var current = screen.Customizer.Current.Clone(); SpecialMoveLibrary.Reload();
            Assert.AreEqual(1, SpecialMoveLibrary.For(current).Count);
            var host = new FriendRoomClient("http://127.0.0.1:8787"); var guest = new FriendRoomClient("http://127.0.0.1:8787");
            yield return host.Create(current); Assert.IsNull(host.Error);
            yield return guest.Join(host.Code, new FighterBuildData { character = "刀" }); Assert.IsNull(guest.Error);
            Assert.AreEqual("蒼流一閃", guest.State.players[0].build.EquippedSpecial.name);
            Assert.AreEqual(current.fusion.Key, guest.State.players[0].build.special.fusion.Key);
            yield return host.Leave();
            GameFlow.ToBattle(); yield return Ready(); var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.AreEqual("蒼流一閃", boot.Battle.Context.Interventions.Side(0).plan.name);
            var custom = new FighterCustomizer(); Assert.IsTrue(custom.SetFusion(Fused("林").fusion, b.fontType));
            Assert.IsNull(custom.Current.EquippedSpecial); Assert.IsTrue(custom.SetFusion(b.fusion, b.fontType));
            Assert.AreEqual("蒼流一閃", custom.Current.EquippedSpecial.name);
            Assert.IsTrue(custom.SetCharacter("刀")); Assert.IsNull(custom.Current.EquippedSpecial);
        }
    }
}
