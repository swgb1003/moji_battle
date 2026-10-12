using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class ScreenAudioTests
    {
        FighterBuildData savedLeft, savedRight;

        [SetUp]
        public void Setup()
        {
            savedLeft = BuildRuntimeStore.Get(0).Clone();
            savedRight = BuildRuntimeStore.Get(1).Clone();
            Time.captureDeltaTime = 0;
            TimeController.ResetAll();
            TimeController.PresentationEffectsEnabled = true;
        }

        [TearDown]
        public void Cleanup()
        {
            BuildRuntimeStore.Set(0, savedLeft);
            BuildRuntimeStore.Set(1, savedRight);
            BuildRuntimeStore.Active = false;
            SimHarness.End();
            GameFlow.ToTitle();
        }

        static IEnumerator Frames() { for (int i = 0; i < 3; i++) yield return null; }
        static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
        static void Music(string name)
        {
            var audio = ScreenAudioDirector.Instance;
            Assert.IsNotNull(audio);
            Assert.AreEqual(name, audio.MusicSource.clip.name);
            Assert.IsTrue(audio.MusicSource.loop);
            Assert.IsTrue(audio.MusicSource.isPlaying);
            Assert.AreEqual(AudioClipLoadType.Streaming, audio.MusicSource.clip.loadType);
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<ScreenAudioDirector>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator AudioCreatedInLoadedTitle_StartsMusicWithoutAnotherSceneTransition()
        {
            GameFlow.ToTitle();
            yield return Frames();
            Object.Destroy(ScreenAudioDirector.Instance.gameObject);
            yield return null;
            Assert.IsNull(ScreenAudioDirector.Instance);
            var audio = new GameObject("ColdStartScreenAudio").AddComponent<ScreenAudioDirector>();
            yield return Frames();
            Music("title");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.Greater(audio.MusicSource.timeSamples, 0, "Title music must progress without navigating away and back.");
        }

        [UnityTest]
        public IEnumerator TitleStart_SwitchesMusic_AndMetalCueSurvivesSceneChange()
        {
            GameFlow.ToTitle();
            yield return Frames();
            Music("title");
            var audio = ScreenAudioDirector.Instance;
            Button("Start").onClick.Invoke();
            yield return Frames();
            Music("menu");
            Assert.AreSame(audio, ScreenAudioDirector.Instance);
            Assert.AreEqual("title_start", audio.ButtonSource.clip.name);
            Assert.IsTrue(audio.ButtonSource.isPlaying, "The scene change must not cut off the metal ring.");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.Greater(audio.ButtonSource.timeSamples, 0);
            Button("BattleModeStart").onClick.Invoke();
            yield return Frames();
            Music("custermize");
            Object.FindAnyObjectByType<CustomizeScreen>().ShowVs();
            yield return Frames();
            Music("custermize");
            GameFlow.ToModeSelect();
            yield return Frames();
            Music("menu");
            GameFlow.ToTitle();
            yield return Frames();
            Music("title");
        }

        [UnityTest]
        public IEnumerator CustomizeBattleStart_PlaysDoubleTaiko_AndStopsMenuDuringCountdown()
        {
            GameFlow.ToCustomize();
            yield return Frames();
            var customize = Object.FindAnyObjectByType<CustomizeScreen>();
            customize.Customizer.SetCharacter("火");
            customize.Customizer.SelectSide(1);
            customize.Customizer.SetCharacter("山");
            customize.ShowVs();
            Music("custermize");
            var audio = ScreenAudioDirector.Instance;
            Button("Start").onClick.Invoke();
            yield return Frames();
            Assert.AreSame(audio, ScreenAudioDirector.Instance);
            Assert.AreEqual("battle_start", audio.ButtonSource.clip.name);
            Assert.IsTrue(audio.ButtonSource.isPlaying, "The scene change must preserve both drum hits.");
            Assert.IsFalse(audio.MusicSource.isPlaying);
            Assert.IsNull(audio.MusicSource.clip);
            var combat = Object.FindAnyObjectByType<BattleBootstrap>().Battle.Root.GetComponent<CombatAudioDirector>();
            CollectionAssert.Contains(new[] { "battle", "battle2", "battle3" }, combat.MusicSource.clip.name);
            Assert.IsFalse(combat.MusicSource.isPlaying, "Countdown must remain free of BGM.");
            GameFlow.ToCustomize();
            yield return Frames();
            Music("custermize");
            Assert.IsTrue(combat == null, "Leaving Battle must destroy its audio source.");
        }

        [UnityTest]
        public IEnumerator TournamentEntry_PlaysTaiko_AndBracketUsesTournamentMusic()
        {
            GameFlow.ToTitle();
            yield return Frames();
            GameFlow.ToTournamentCustomize();
            yield return Frames();
            Music("custermize");
            Object.FindAnyObjectByType<CustomizeScreen>().Customizer.SetCharacter("剣");
            Button("Battle").onClick.Invoke();
            yield return Frames();
            Music("taunament");
            Assert.AreEqual("battle_start", ScreenAudioDirector.Instance.ButtonSource.clip.name);
            Assert.IsTrue(ScreenAudioDirector.Instance.ButtonSource.isPlaying);
            Assert.IsNull(Object.FindAnyObjectByType<CombatAudioDirector>(), "Background COM fights must remain silent.");
            GameFlow.ToModeSelect();
            yield return Frames();
            Music("menu");
        }
    }
}
