using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MojiBattle.Tests
{
    public class TournamentTests
    {
        static List<FighterBuildData> Computers() => Enumerable.Range(0, 7)
            .Select(i => new FighterBuildData { character = "口", weaponSize = WeaponSize.XL,
                gripType = (GripType)(i % 4), battleStyle = (BattleStyle)(i % 4), gripPosition = .12f + i * .1f }).ToList();
        static TournamentSession Make() => new TournamentSession(new FighterBuildData { character = "火", weaponSize = WeaponSize.L,
            gripType = GripType.Reverse, battleStyle = BattleStyle.Counter, weaponFlip = WeaponFlip.Both,
            customGrip = true, gripPoint = new Vector2(.2f, .7f) }, Computers(), 4203);
        static MatchResult Result(MatchConfig config, int winner) => new MatchResult { seed = config.seed, winner = winner, finishReason = "KO" };
        static void ComputersFinish(TournamentSession session)
        {
            for (int n = 0; session.NextComputerMatch != null && n < 7; n++)
            {
                int id = session.NextComputerMatch.Id;
                var config = session.BeginMatch(id, 60f);
                Assert.IsTrue(session.RecordResult(id, Result(config, 0)));
            }
        }

        [Test]
        public void Bracket_HasEightEntrants_AndAllBuildsArePrivateSSizeSnapshots()
        {
            var player = new FighterBuildData { character = "火", weaponSize = WeaponSize.XL, gripType = GripType.Reverse,
                battleStyle = BattleStyle.HitAndAway, customGrip = true, gripPoint = new Vector2(.2f, .7f), weaponFlip = WeaponFlip.Both };
            var opponents = Computers();
            var s = new TournamentSession(player, opponents, 10);
            player.character = "山";
            opponents[0].weaponSize = WeaponSize.L;
            var exposed = s.Entrants[0].Build;
            exposed.character = "鬱";
            Assert.AreEqual(8, s.Entrants.Count);
            Assert.AreEqual(7, s.Matches.Count);
            Assert.IsTrue(s.Entrants.All(e => e.Build.weaponSize == WeaponSize.S));
            Assert.AreEqual(WeaponSize.XL, player.weaponSize, "Preparing the tournament must not modify the caller's build.");
            var c = s.BeginMatch(0, 60);
            Assert.AreEqual("火", c.left.grapheme);
            Assert.AreEqual(WeaponSize.S, c.left.build.weaponSize);
            Assert.AreEqual(GripType.Reverse, c.left.build.gripType);
            Assert.AreEqual(BattleStyle.HitAndAway, c.left.build.battleStyle);
            Assert.AreEqual(new Vector2(.2f, .7f), c.left.build.gripPoint);
            Assert.AreEqual(WeaponFlip.Both, c.left.build.weaponFlip);
            c.left.build.gripType = GripType.TwoHanded;
            Assert.AreEqual(GripType.Reverse, s.Entrants[0].Build.gripType);
        }

        [Test]
        public void ThreeWins_AdvanceThroughRealBracketSlots_AndCrownPlayer()
        {
            var s = Make();
            for (int round = 0; round < 3; round++)
            {
                var match = s.NextPlayerMatch;
                Assert.AreEqual(round, match.Round);
                var config = s.BeginMatch(match.Id, 60);
                Assert.IsNull(s.NextPlayerMatch, "A started match must not be started twice.");
                Assert.IsTrue(s.RecordResult(match.Id, Result(config, 0)));
                Assert.AreEqual(round + 1, s.Wins);
                Assert.AreEqual(round == 2, s.Champion);
                ComputersFinish(s);
            }
            Assert.IsTrue(s.Finished);
            Assert.IsFalse(s.Eliminated);
            Assert.AreEqual(0, s.ChampionId);
            Assert.IsTrue(s.Matches.All(m => m.Complete));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void DefeatAtAnyRound_EliminatesPlayer_AndComputerBracketCanFinish(int lostRound)
        {
            var s = Make();
            for (int round = 0; round <= lostRound; round++)
            {
                var m = s.NextPlayerMatch;
                var config = s.BeginMatch(m.Id, 60);
                s.RecordResult(m.Id, Result(config, round == lostRound ? 1 : 0));
                ComputersFinish(s);
            }
            Assert.AreEqual(lostRound, s.Wins);
            Assert.IsTrue(s.Eliminated);
            Assert.IsTrue(s.Finished);
            Assert.IsNull(s.NextPlayerMatch);
            Assert.Greater(s.ChampionId, 0);
            Assert.IsTrue(s.Matches.All(m => m.Complete));
        }

        [Test]
        public void Draw_RetainsOpponentAndBuilds_AndRetriesWithoutAWinOrDuplicateResult()
        {
            var s = Make();
            var match = s.NextPlayerMatch;
            var original = s.BeginMatch(match.Id, 60);
            Assert.IsFalse(s.RecordResult(match.Id, new MatchResult { seed = original.seed + 2, winner = 0 }));
            var draw = Result(original, -1);
            Assert.IsTrue(s.RecordResult(match.Id, draw));
            Assert.IsFalse(s.RecordResult(match.Id, draw));
            Assert.AreEqual(0, s.Wins);
            Assert.IsFalse(s.Finished);
            Assert.AreEqual(1, match.Draws);
            Assert.AreSame(match, s.NextPlayerMatch);
            var rematch = s.BeginMatch(match.Id, 60);
            Assert.AreNotEqual(original.seed, rematch.seed);
            Assert.AreEqual(JsonUtility.ToJson(original.left.build), JsonUtility.ToJson(rematch.left.build));
            Assert.AreEqual(JsonUtility.ToJson(original.right.build), JsonUtility.ToJson(rematch.right.build));
            Assert.IsTrue(s.RecordResult(match.Id, Result(rematch, 0)));
            Assert.IsFalse(s.RecordResult(match.Id, Result(rematch, 0)));
            Assert.AreEqual(1, s.Wins);
        }

        [Test]
        public void Opponents_UseSupportedRandomCharactersAndCustomizations_AndAreSeeded()
        {
            var a = TournamentOpponentGenerator.Generate(6421);
            var b = TournamentOpponentGenerator.Generate(6421);
            var c = TournamentOpponentGenerator.Generate(6422);
            Assert.AreEqual(7, a.Count);
            Assert.AreEqual(string.Join("|", a.Select(x => JsonUtility.ToJson(x))), string.Join("|", b.Select(x => JsonUtility.ToJson(x))));
            Assert.AreNotEqual(string.Join("|", a.Select(x => JsonUtility.ToJson(x))), string.Join("|", c.Select(x => JsonUtility.ToJson(x))));
            foreach (var build in a.Concat(c))
            {
                Assert.AreEqual(WeaponSize.S, build.weaponSize);
                Assert.IsTrue(FighterCustomizer.IsAvailable(build.character, build.fontType, out var reason), reason);
                Assert.That(build.gripPosition, Is.InRange(.08f, .92f));
            }
            Assert.Greater(a.Concat(c).Select(x => x.character).Distinct().Count(), 8);
            Assert.Greater(a.Concat(c).Select(x => x.gripType).Distinct().Count(), 1);
            Assert.Greater(a.Concat(c).Select(x => x.battleStyle).Distinct().Count(), 1);
            Assert.Greater(a.Concat(c).Select(x => x.weaponFlip).Distinct().Count(), 1);
            Assert.IsTrue(a.Any(x => !FighterCustomizer.GlyphOrder.Contains(x.character)));
        }
    }
}
