using NUnit.Framework;
using UnityEngine;

namespace MojiBattle.Tests
{
    public class GameSettingsTests
    {
        string saved;
        bool existed;
        int motion;
        [SetUp] public void Setup()
        {
            existed = PlayerPrefs.HasKey(GameSettings.PrefKey);
            saved = PlayerPrefs.GetString(GameSettings.PrefKey);
            motion = PlayerPrefs.GetInt("MenuReducedMotion", 0);
            GameSettings.Reset();
        }
        [TearDown] public void Cleanup()
        {
            if (existed) PlayerPrefs.SetString(GameSettings.PrefKey, saved); else PlayerPrefs.DeleteKey(GameSettings.PrefKey);
            PlayerPrefs.SetInt("MenuReducedMotion", motion); PlayerPrefs.Save(); GameSettings.Reload();
        }
        [Test] public void PreferencesSurviveReload_AndCopiesCannotChangeSavedSettings()
        {
            GameSettings.Change(s => { s.masterVolume = .3f; s.musicVolume = .4f; s.effectsVolume = .7f; s.battleMusic = 3; s.spectatorSpeed = 4; s.showDamage = false; s.showInstructions = false; s.simpleSpecial = true; s.reducedEffects = true; });
            GameSettings.SetMenuMotion(true);
            GameSettings.Reload();
            var s = GameSettings.Current;
            Assert.AreEqual(.12f, GameSettings.MusicGain, .0001f);
            Assert.AreEqual(.21f, GameSettings.EffectsGain, .0001f);
            Assert.AreEqual(3, GameSettings.BattleMusic); Assert.AreEqual(4, GameSettings.InitialSpeed);
            Assert.IsFalse(GameSettings.ShowDamage); Assert.IsFalse(GameSettings.ShowInstructions);
            Assert.IsTrue(GameSettings.SimpleSpecial); Assert.IsTrue(GameSettings.ReducedEffects); Assert.IsTrue(RetroMotionSettings.Reduced);
            s.battleMusic = 0; Assert.AreEqual(3, GameSettings.BattleMusic);
            GameSettings.Reset(); Assert.AreEqual(1, GameSettings.InitialSpeed); Assert.IsFalse(RetroMotionSettings.Reduced);
        }
        [Test] public void OutOfRangeSavedValuesAreClamped()
        {
            PlayerPrefs.SetString(GameSettings.PrefKey, "{\"masterVolume\":2,\"musicVolume\":-1,\"effectsVolume\":0.5,\"battleMusic\":19,\"spectatorSpeed\":-4}");
            GameSettings.Reload();
            Assert.AreEqual(1, GameSettings.Current.masterVolume); Assert.AreEqual(0, GameSettings.Current.musicVolume);
            Assert.AreEqual(3, GameSettings.BattleMusic); Assert.AreEqual(1, GameSettings.InitialSpeed);
            GameSettings.Change(s => s.masterVolume = float.NaN);
            Assert.AreEqual(.8f, GameSettings.Current.masterVolume);
        }
    }
}
