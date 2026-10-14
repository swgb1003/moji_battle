using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MojiBattle.Tests
{
    public class PlayControlTests
    {
        string settings, cup, awards; bool hadSettings, hadCup, hadAwards, hadUnlock;

        [SetUp] public void Setup()
        {
            hadSettings = PlayerPrefs.HasKey(GameSettings.PrefKey); settings = PlayerPrefs.GetString(GameSettings.PrefKey);
            hadCup = PlayerPrefs.HasKey(WorldCupRuntimeStore.PrefKey); cup = PlayerPrefs.GetString(WorldCupRuntimeStore.PrefKey);
            hadAwards = PlayerPrefs.HasKey(AchievementStore.PrefKey); awards = PlayerPrefs.GetString(AchievementStore.PrefKey);
            hadUnlock = PlayControl.TrueUnlocked;
            PlayControl.ResetUnlockForTests(); GameSettings.Reset(); WorldCupRuntimeStore.ClearSession();
        }
        [TearDown] public void Cleanup()
        {
            WorldCupRuntimeStore.ClearSession();
            Restore(GameSettings.PrefKey, hadSettings, settings); Restore(WorldCupRuntimeStore.PrefKey, hadCup, cup); Restore(AchievementStore.PrefKey, hadAwards, awards);
            if (hadUnlock) PlayControl.Unlock(); else PlayControl.ResetUnlockForTests();
            GameSettings.Reload(); AchievementStore.Reload();
        }
        static void Restore(string key, bool had, string value) { if (had) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }

        [Test] public void LevelsGetStrongerInOrderAndStrongMatchesSpectatorCom()
        {
            var levels = PlayControl.Levels;
            CollectionAssert.AreEqual(new[] { "初心者", "弱い", "ふつう", "強い", "moji Fighter", "真・mojiFighter" }, levels.Select(PlayControl.Label).ToArray());
            for (int i = 1; i < levels.Length; i++)
            {
                DifficultyProfile a = PlayControl.Profile(levels[i - 1]), b = PlayControl.Profile(levels[i]);
                Assert.Greater(a.decisionScale, b.decisionScale); Assert.Less(a.reaction, b.reaction);
                Assert.Less(a.parry, b.parry); Assert.Less(a.damage, b.damage); Assert.LessOrEqual(a.hp, b.hp);
            }
            var strong = PlayControl.Profile(ComDifficulty.Strong);
            Assert.AreEqual(1f, strong.decisionScale); Assert.AreEqual(1f, strong.reaction); Assert.AreEqual(1f, strong.damage);
        }

        [Test] public void SelectionIsSavedAndTrueLevelNeedsUnlock()
        {
            Assert.IsFalse(GameSettings.SelfControl);
            PlayControl.SetSelfControl(true); PlayControl.SetDifficulty(ComDifficulty.MojiFighter);
            GameSettings.Reload();
            Assert.IsTrue(GameSettings.SelfControl); Assert.AreEqual(ComDifficulty.MojiFighter, PlayControl.Difficulty);
            PlayControl.SetDifficulty(ComDifficulty.TrueMojiFighter);
            Assert.AreEqual(ComDifficulty.MojiFighter, PlayControl.Difficulty, "The final level stays locked until the World Cup unlock.");
            Assert.IsTrue(PlayControl.Unlock()); Assert.IsFalse(PlayControl.Unlock());
            PlayControl.SetDifficulty(ComDifficulty.TrueMojiFighter);
            Assert.AreEqual(ComDifficulty.TrueMojiFighter, PlayControl.Difficulty);
        }

        static void PlayCup(System.Func<WorldCupMatch, bool> selfHard)
        {
            WorldCupRuntimeStore.Begin(FighterBuildData.Default("火"), 77);
            var s = WorldCupRuntimeStore.Current;
            for (int n = 0; n < 400 && !s.Finished; n++)
            {
                var m = s.Matches.First(x => x.Ready);
                s.BeginMatch(m.Id, 60);
                int winner = m.IncludesPlayer ? m.LeftId == 0 ? 0 : 1 : 0;
                Assert.IsTrue(WorldCupRuntimeStore.Record(m.Id, WorldCupTests.Result(m, winner), false, m.IncludesPlayer && selfHard(m)));
            }
            Assert.IsTrue(s.Finished); Assert.AreEqual(0, s.ChampionId);
        }

        [Test] public void WinningEveryMatchSelfControlledOnMojiFighterUnlocksTheFinalLevel()
        {
            PlayCup(m => true);
            Assert.IsTrue(PlayControl.TrueUnlocked);
            Assert.IsTrue(WorldCupRuntimeStore.Current.UnlockedTrue);
            Assert.IsTrue(WorldCupRuntimeStore.Resume() && WorldCupRuntimeStore.Current.UnlockedTrue, "The unlock notice survives a reload.");
        }

        [Test] public void OneSpectatedOrEasierMatchKeepsTheFinalLevelLocked()
        {
            bool first = true;
            PlayCup(m => { bool hard = !first; first = false; return hard; });
            Assert.IsFalse(PlayControl.TrueUnlocked);
            Assert.IsFalse(WorldCupRuntimeStore.Current.SelfAllHard);
            Assert.IsFalse(WorldCupRuntimeStore.Current.UnlockedTrue);
        }

        [Test] public void PracticeChoicesStartFromSandbagAndHideTheLockedLevel()
        {
            Assert.AreEqual(ComDifficulty.Sandbag, PracticeSession.Choices[0]);
            Assert.AreEqual(7, PracticeSession.Choices.Length);
            Assert.IsFalse(PlayControl.Available(ComDifficulty.TrueMojiFighter));
            Assert.IsTrue(PlayControl.Available(ComDifficulty.Sandbag));
        }
    }
}
