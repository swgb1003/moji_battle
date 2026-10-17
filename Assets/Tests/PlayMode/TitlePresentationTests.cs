using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public sealed class TitlePresentationTests
    {
        int reduced;

        [SetUp] public void Setup()
        {
            reduced = PlayerPrefs.GetInt("MenuReducedMotion", 0);
            PlayerPrefs.SetInt("MenuReducedMotion", 0);
            SimHarness.End();
        }

        [TearDown] public void Cleanup()
        {
            PlayerPrefs.SetInt("MenuReducedMotion", reduced);
            SimHarness.End(); GameFlow.ToTitle();
        }

        static TitleFighterMotion[] Fighters() => Object.FindObjectsByType<TitleFighterMotion>(FindObjectsSortMode.None)
            .OrderBy(f => f.Mirrored).ToArray();

        [UnityTest] public IEnumerator ApprovedCompositionAndArticulatedPoses_RenderAtAllAspectRatios()
        {
            GameFlow.ToTitle(); yield return null; yield return null;
            yield return new WaitForSecondsRealtime(.85f);
            var fighters = Fighters(); Assert.AreEqual(2, fighters.Length);
            Assert.IsNotNull(Resources.Load<Texture2D>("UI/Title/TitlePaper"));
            Assert.IsNull(GameObject.Find("DisplayPlinth"));
            Assert.IsNotNull(GameObject.Find("StartRedOffset"));
            foreach (var f in fighters) { f.enabled = false; f.BodyArt.SetTitlePose(0, 0, 0); f.SampleIdle(0); }
            MenuUiTests.Capture("title_approved_design");
            MenuUiTests.Capture("title_approved_design_880x450", 880, 450);
            MenuUiTests.Capture("title_approved_design_1280x960", 1280, 960);
            var left = fighters[0]; var right = fighters[1];
            left.SampleIdle(0); var hand = left.BodyArt.TitlePoint(279, 205);
            var leftFoot = left.BodyArt.TitlePoint(14, 406);
            var rightFoot = left.BodyArt.TitlePoint(275, 406);
            var weapon = left.Weapon.anchoredPosition;
            left.SampleIdle(3.65f); right.SampleIdle(3.65f);
            Assert.Greater(Vector2.Distance(hand, left.BodyArt.TitlePoint(279, 205)), 20, "A practice swing must articulate the arm.");
            Assert.Greater(Vector2.Distance(weapon, left.Weapon.anchoredPosition), 20, "The glyph must follow the hand.");
            Assert.AreEqual(leftFoot, left.BodyArt.TitlePoint(14, 406));
            Assert.AreEqual(rightFoot, left.BodyArt.TitlePoint(275, 406));
            MenuUiTests.Capture("title_fire_practice");
            left.SampleIdle(5.95f); right.SampleIdle(5.95f);
            MenuUiTests.Capture("title_mountain_practice");
            left.SampleCharge(1); right.SampleCharge(1);
            MenuUiTests.Capture("title_start_clash_pose");
        }

        [UnityTest] public IEnumerator IdleUsesUnscaledTime_ReducedMotionSettles_AndStartPlaysOnce()
        {
            GameFlow.ToTitle(); yield return null; yield return null;
            yield return new WaitForSecondsRealtime(.85f);
            var f = Fighters()[0]; var initial = f.BodyArt.TitlePoint(148, 206);
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.Greater(Vector2.Distance(initial, f.BodyArt.TitlePoint(148, 206)), .05f);
            PlayerPrefs.SetInt("MenuReducedMotion", 1); yield return null;
            var settled = f.BodyArt.TitlePoint(148, 206);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.AreEqual(settled, f.BodyArt.TitlePoint(148, 206));
            PlayerPrefs.SetInt("MenuReducedMotion", 0);
            var title = Object.FindAnyObjectByType<TitleScreen>();
            var start = GameObject.Find("Start").GetComponent<Button>();
            start.onClick.Invoke(); title.StartGame();
            Assert.IsTrue(title.Starting); Assert.IsFalse(start.interactable);
            Assert.AreEqual(GameFlow.TitleScene, SceneManager.GetActiveScene().name);
            yield return new WaitForSecondsRealtime(.38f);
            Assert.AreEqual(GameFlow.TitleScene, SceneManager.GetActiveScene().name);
            MenuUiTests.Capture("title_start_midstep");
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(GameFlow.ModeSelectScene, SceneManager.GetActiveScene().name);
            Assert.AreEqual("title_start", ScreenAudioDirector.Instance.ButtonSource.clip.name);
        }

        [UnityTest] public IEnumerator ReducedMotion_StartSkipsClashAndNavigatesImmediately()
        {
            PlayerPrefs.SetInt("MenuReducedMotion", 1);
            GameFlow.ToTitle(); yield return null; yield return null;
            GameObject.Find("Start").GetComponent<Button>().onClick.Invoke();
            yield return null; yield return null;
            Assert.AreEqual(GameFlow.ModeSelectScene, SceneManager.GetActiveScene().name);
        }
    }
}
