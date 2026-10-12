using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class SettingsFlowTests
    {
        string savedSettings, savedAchievements, savedSpecials;
        bool hadSettings, hadAchievements, hadSpecials;
        int motion;
        FighterBuildData left, right;
        [SetUp] public void Setup()
        {
            hadSettings = PlayerPrefs.HasKey(GameSettings.PrefKey); savedSettings = PlayerPrefs.GetString(GameSettings.PrefKey);
            hadAchievements = PlayerPrefs.HasKey(AchievementStore.PrefKey); savedAchievements = PlayerPrefs.GetString(AchievementStore.PrefKey);
            hadSpecials = PlayerPrefs.HasKey("MojiBattle.Specials.v1"); savedSpecials = PlayerPrefs.GetString("MojiBattle.Specials.v1");
            left = BuildRuntimeStore.Get(0).Clone(); right = BuildRuntimeStore.Get(1).Clone();
            motion = PlayerPrefs.GetInt("MenuReducedMotion", 0);
            Time.captureDeltaTime = 0; TimeController.PresentationEffectsEnabled = true; TimeController.ResetAll();
            GameSettings.Reset();
        }
        [TearDown] public void Cleanup()
        {
            if (SettingsScreen.IsOpen) SettingsScreen.Instance.Close();
            GameFlow.ToTitle(); SimHarness.End();
            BuildRuntimeStore.Set(0, left); BuildRuntimeStore.Set(1, right); BuildRuntimeStore.Active = false;
            if (hadSettings) PlayerPrefs.SetString(GameSettings.PrefKey, savedSettings); else PlayerPrefs.DeleteKey(GameSettings.PrefKey);
            if (hadAchievements) PlayerPrefs.SetString(AchievementStore.PrefKey, savedAchievements); else PlayerPrefs.DeleteKey(AchievementStore.PrefKey);
            if (hadSpecials) PlayerPrefs.SetString("MojiBattle.Specials.v1", savedSpecials); else PlayerPrefs.DeleteKey("MojiBattle.Specials.v1");
            PlayerPrefs.SetInt("MenuReducedMotion", motion); PlayerPrefs.Save(); GameSettings.Reload(); AchievementStore.Reload();
            SpecialMoveLibrary.Reload();
        }
        static IEnumerator Frames() { for (int i = 0; i < 4; i++) yield return null; }
        static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
        static Text Label(string name) => Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Single(t => t.name == name);
        static Slider Slider(string name) => Object.FindObjectsByType<Slider>(FindObjectsSortMode.None).Single(s => s.name == name);

        [UnityTest] public IEnumerator MenuSettings_AllTabsAndSavedControls_WorkAtBothSizes()
        {
            GameFlow.ToModeSelect(); yield return Frames();
            Button("MenuSettings").onClick.Invoke(); yield return Frames();
            Assert.IsTrue(SettingsScreen.IsOpen);
            Assert.IsFalse(Button("BattleModeStart").IsInteractable(), "Keyboard navigation must not activate the menu behind settings.");
            MenuUiTests.Capture("settings_01_basic"); MenuUiTests.Capture("settings_01_basic_880x450", 880, 450);
            Slider("MasterVolume").value = .45f; Slider("MusicVolume").value = .35f; Slider("EffectsVolume").value = 0;
            Button("Instructions1").onClick.Invoke(); Button("Damage1").onClick.Invoke(); Button("InitialSpeed2").onClick.Invoke();
            Button("MusicSelect").onClick.Invoke(); yield return Frames();
            MenuUiTests.Capture("settings_02_music_picker");
            Button("PickerMusic2").onClick.Invoke(); yield return Frames();
            Assert.AreEqual(2, GameSettings.BattleMusic);
            Button("SettingsTab1").onClick.Invoke(); yield return Frames(); MenuUiTests.Capture("settings_03_music");
            Button("SettingsTab2").onClick.Invoke(); yield return Frames();
            Button("DisplayReduced1").onClick.Invoke(); Button("DisplaySpecial1").onClick.Invoke(); Button("MenuMotion1").onClick.Invoke();
            MenuUiTests.Capture("settings_04_display"); MenuUiTests.Capture("settings_04_display_880x450", 880, 450);
            Button("SettingsTab3").onClick.Invoke(); yield return Frames(); MenuUiTests.Capture("settings_05_data");
            Button("ResetBuilds").onClick.Invoke(); yield return Frames(); MenuUiTests.Capture("settings_06_confirmation");
            Assert.IsFalse(Button("SettingsDone").IsInteractable(), "Confirmation must block the page behind it.");
            Assert.IsTrue(Button("SettingsCancel").IsInteractable());
            Button("SettingsCancel").onClick.Invoke(); yield return Frames();
            Assert.AreEqual(left.character, BuildRuntimeStore.Get(0).character, "Cancel must preserve builds.");
            Button("SettingsDone").onClick.Invoke(); yield return Frames();
            Assert.IsFalse(SettingsScreen.IsOpen); GameSettings.Reload();
            Assert.IsTrue(Button("BattleModeStart").IsInteractable());
            Button("MenuSettings").onClick.Invoke(); yield return Frames();
            Assert.AreEqual("45%", Label("MasterValue").text); Assert.AreEqual("35%", Label("MusicValue").text); Assert.AreEqual("0%", Label("EffectsValue").text);
            Assert.IsFalse(GameSettings.ShowInstructions); Assert.IsFalse(GameSettings.ShowDamage); Assert.AreEqual(4, GameSettings.InitialSpeed);
            string achievements = PlayerPrefs.GetString(AchievementStore.PrefKey), specials = PlayerPrefs.GetString("MojiBattle.Specials.v1");
            Button("SettingsReset").onClick.Invoke(); yield return Frames(); Button("SettingsConfirm").onClick.Invoke(); yield return Frames();
            Assert.AreEqual(.8f, GameSettings.Current.masterVolume); Assert.IsTrue(GameSettings.ShowDamage); Assert.IsFalse(RetroMotionSettings.Reduced);
            Assert.AreEqual(achievements, PlayerPrefs.GetString(AchievementStore.PrefKey)); Assert.AreEqual(specials, PlayerPrefs.GetString("MojiBattle.Specials.v1"));
            Assert.AreEqual(left.character, BuildRuntimeStore.Get(0).character);
        }

        [UnityTest] public IEnumerator VolumeAndMusicPreview_ApplyLiveAndStopOnClose()
        {
            GameFlow.ToModeSelect(); yield return Frames(); SettingsScreen.Open(); yield return Frames();
            Slider("MasterVolume").value = .5f; Slider("MusicVolume").value = .25f; Slider("EffectsVolume").value = .75f;
            yield return Frames();
            var audio = ScreenAudioDirector.Instance;
            Assert.AreEqual(audio.MusicVolume * .125f, audio.MusicSource.volume, .0001f);
            ScreenAudioDirector.PlayTitleStart(); yield return Frames();
            Assert.AreEqual(audio.ButtonVolume * .375f, audio.ButtonSource.volume, .0001f);
            Button("SettingsTab1").onClick.Invoke(); yield return Frames(); Button("MusicListen2").onClick.Invoke(); yield return Frames();
            var preview = SettingsScreen.Instance.PreviewSource;
            Assert.AreEqual("battle2", preview.clip.name); Assert.IsTrue(preview.isPlaying); Assert.IsTrue(audio.MusicSource.mute);
            Assert.AreEqual(.35f * .125f, preview.volume, .0001f);
            Button("MusicListen2").onClick.Invoke(); yield return Frames(); Assert.IsFalse(preview.isPlaying); Assert.IsFalse(audio.MusicSource.mute);
            Button("MusicListen3").onClick.Invoke(); yield return Frames();
            SettingsScreen.Instance.Close(); yield return Frames(); Assert.IsFalse(audio.MusicSource.mute); Assert.IsTrue(preview == null);
        }

        [UnityTest] public IEnumerator BattlePreferencesAndOverlay_PreserveMatchSpeedAndPauseState()
        {
            GameSettings.Change(s => { s.battleMusic = 2; s.spectatorSpeed = 4; s.showInstructions = false; s.simpleSpecial = true; s.reducedEffects = true; });
            GameFlow.ToCustomize(); yield return Frames(); GameFlow.ToBattle(); yield return Frames();
            var boot = Object.FindFirstObjectByType<BattleBootstrap>(); var battle = boot.Battle;
            Assert.AreEqual(4, TimeController.SpectatorSpeed);
            Assert.AreEqual("", Label("BattleAnnouncementSubtitle").text);
            Assert.AreEqual("battle2", battle.Root.GetComponent<CombatAudioDirector>().MusicSource.clip.name);
            Button("BattleSettings").onClick.Invoke(); yield return Frames();
            Assert.IsTrue(TimeController.Paused); float clock = battle.Director.Clock;
            yield return new WaitForSecondsRealtime(.15f); Assert.AreEqual(clock, battle.Director.Clock);
            Button("InitialSpeed0").onClick.Invoke(); Assert.AreEqual(4, TimeController.SpectatorSpeed, "Initial speed applies to the next match.");
            Button("SettingsTab3").onClick.Invoke(); yield return Frames(); Assert.IsFalse(Button("ResetAchievements").interactable);
            Button("SettingsDone").onClick.Invoke(); yield return Frames(); Assert.IsFalse(TimeController.Paused); Assert.AreEqual(4, TimeController.SpectatorSpeed);
            TimeController.SetPaused(true); SettingsScreen.Open(); yield return Frames(); SettingsScreen.Instance.Close(); yield return Frames(); Assert.IsTrue(TimeController.Paused);
            boot.useCountdown = false; boot.StartMatch(52719); yield return Frames(); Assert.AreEqual(1, TimeController.SpectatorSpeed);
            battle = boot.Battle; battle.Context.Interventions.Side(0).gauge = 1;
            battle.Context.Interventions.Press(0, InterventionKind.Special);
            double deadline = Time.realtimeSinceStartupAsDouble + 7;
            var cutin = battle.Root.GetComponent<SpecialCutIn>();
            while (!cutin.Showing && !battle.Director.Ended && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(cutin.Showing); Assert.IsTrue(TimeController.FreezeActive);
            Assert.IsTrue(Label("CompactName").text.Contains("必殺"));
            Assert.IsFalse(Label("CompactPrompt").text.Contains("ジャストガード"));
            TimeController.SetPaused(true); yield return new WaitForSecondsRealtime(.85f); MenuUiTests.Capture("settings_07_simple_special");
        }

        [UnityTest] public IEnumerator DataReset_RequiresConfirmationAndUpdatesRuntimeStores()
        {
            GameFlow.ToModeSelect(); yield return Frames();
            BuildRuntimeStore.Set(0, FighterBuildData.Default("龍"));
            AchievementStore.Change(p => p.Set(2, 1), true);
            SettingsScreen.Open(); yield return Frames(); Button("SettingsTab3").onClick.Invoke(); yield return Frames();
            Button("ResetBuilds").onClick.Invoke(); yield return Frames(); Assert.AreEqual("龍", BuildRuntimeStore.Get(0).character);
            Button("SettingsConfirm").onClick.Invoke(); yield return Frames(); Assert.AreEqual("一", BuildRuntimeStore.Get(0).character);
            Assert.IsTrue(AchievementStore.Profile.Has(2));
            SpecialMoveLibrary.ClearAll(); var move = BuiltinSpecialMoves.For("火"); Assert.IsTrue(SpecialMoveLibrary.Add(move));
            var build = FighterBuildData.Default("火"); build.special = move; BuildRuntimeStore.Set(0, build);
            Button("ResetSpecials").onClick.Invoke(); yield return Frames(); Assert.AreEqual(1, SpecialMoveLibrary.All().Count);
            Button("SettingsConfirm").onClick.Invoke(); yield return Frames();
            SpecialMoveLibrary.Reload(); Assert.AreEqual(0, SpecialMoveLibrary.All().Count); Assert.IsNull(BuildRuntimeStore.Get(0).special);
            Assert.AreEqual("火", BuildRuntimeStore.Get(0).character); Assert.IsTrue(AchievementStore.Profile.Has(2));
            Button("ResetAchievements").onClick.Invoke(); yield return Frames(); Button("SettingsConfirm").onClick.Invoke(); yield return Frames();
            Assert.AreEqual(0, AchievementStore.Profile.Count); AchievementStore.Reload(); Assert.AreEqual(0, AchievementStore.Profile.Count);
        }

        [UnityTest] public IEnumerator DamageNumbersAndReducedFlash_AffectPresentationWithoutChangingHp()
        {
            GameFlow.ToCustomize(); yield return Frames(); GameFlow.ToBattle(); yield return Frames();
            var boot = Object.FindFirstObjectByType<BattleBootstrap>(); boot.useCountdown = false; boot.StartMatch(52719); yield return Frames();
            TimeController.SetPaused(true); var battle = boot.Battle;
            yield return new WaitForSecondsRealtime(1.1f);
            float hp = battle.Right.Runtime.hp;
            var hit = new HitEvent { attacker = 0, defender = 1, damage = 1, quality = HitQuality.Normal, style = AttackStyle.Overhead, point = battle.Right.ChestWorld };
            GameSettings.Change(s => s.showDamage = false); battle.Context.Events.Raise(hit); yield return Frames();
            Assert.IsFalse(battle.Root.GetComponentsInChildren<TextMesh>().Any(t => t.name == "Popup" && t.text == "1"));
            GameSettings.Change(s => s.showDamage = true); battle.Context.Events.Raise(hit); yield return Frames();
            Assert.IsTrue(battle.Root.GetComponentsInChildren<TextMesh>().Any(t => t.name == "Popup" && t.text == "1"));
            Assert.AreEqual(hp, battle.Right.Runtime.hp);
            GameSettings.Change(s => s.reducedEffects = true);
            battle.Context.Events.Raise(new KoEvent { winner = 0, point = battle.Right.ChestWorld }); yield return Frames();
            Assert.AreEqual(0, battle.Root.GetComponentsInChildren<SpriteRenderer>().Single(s => s.name == "KoFlash").color.a);
            Assert.IsTrue(battle.Root.GetComponentsInChildren<LineRenderer>().Where(l => l.name == "SpeedLine").All(l => l.positionCount == 0));
        }
    }
}
