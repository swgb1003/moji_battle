using NUnit.Framework;
using UnityEngine;

namespace MojiBattle.Tests
{
    public class SpecialMoveTests
    {
        [Test]
        public void Builtins_HaveThirtyDistinctChains_ValidNamesAndBalancedStats()
        {
            Assert.AreEqual(30,BuiltinSpecialMoves.Count);
            var chains=new System.Collections.Generic.HashSet<string>();
            var elements=new System.Collections.Generic.HashSet<SpecialElement>();
            for(int i=0;i<BuiltinSpecialMoves.Count;i++)
            {
                string ch=BuiltinSpecialMoves.Representative(i);
                var m=BuiltinSpecialMoves.For(ch);
                Assert.IsNull(SpecialMoveRules.CheckName(m.name,ch));
                Assert.IsNotNull(SpecialMoveRules.Sanitize(m,ch));
                Assert.LessOrEqual(m.power+m.speed+m.reach,SpecialMoveRules.StatBudget);
                Assert.That(m.steps.Length,Is.InRange(1,4));
                foreach(var step in m.steps) Assert.IsTrue(SpecialMoveRules.TryStyle(step,out _));
                Assert.AreEqual(m.name,SpecialMovePlan.StandardName(ch));
                chains.Add(string.Join(",",m.steps)); elements.Add(m.Element);
            }
            Assert.AreEqual(30,chains.Count,"名前以外にも全30種類の連携がある");
            Assert.AreEqual(11,elements.Count);
        }

        [TestCase("漢")][TestCase("あ")][TestCase("A")][TestCase("→")][TestCase("𠮷")]
        public void Builtins_UnknownCharactersKeepStableAssignment_AndReturnIndependentCopies(string ch)
        {
            var first=BuiltinSpecialMoves.For(ch);
            string json=JsonUtility.ToJson(first);
            Assert.AreEqual(json,JsonUtility.ToJson(BuiltinSpecialMoves.For(ch)));
            Assert.IsNull(SpecialMoveRules.CheckName(first.name,ch));
            first.steps[0]="Throw"; first.name="changed";
            Assert.AreEqual(json,JsonUtility.ToJson(BuiltinSpecialMoves.For(ch)));
        }

        [SetUp]
        public void SetUp() => SpecialMoveLibrary.ClearAll();

        [TearDown]
        public void TearDown() => SpecialMoveLibrary.ClearAll();

        static SpecialMoveData Move(string name, string character = "炎") => new SpecialMoveData
        {
            name = name, character = character, steps = new[] { "Launch", "StrokeShot" }, power = 3, speed = 3, reach = 3,
            element = "fire", effect = "pillar", primary = "#E2462B", secondary = "#F6B23C",
        };

        [TestCase("炎龍一閃", null)]
        [TestCase("炎", null)]
        [TestCase("龍一閃", "技名に「炎」を入れてください")]
        [TestCase("炎炎炎炎炎炎炎炎", "技名は7文字以内にしてください（いま8文字）")]
        [TestCase("  ", "技名を入力してください")]
        [TestCase("炎<b>", "使えない記号が入っています")]
        public void Name_MustContainCharacter_WithinSevenGraphemes(string name, string error)
        {
            Assert.AreEqual(error, SpecialMoveRules.CheckName(name, "炎"));
        }

        [Test]
        public void Name_CountsSurrogatePairsAsOneCharacter()
        {
            Assert.IsNull(SpecialMoveRules.CheckName("𠮷𠮷𠮷𠮷𠮷𠮷𠮷", "𠮷"));
        }

        [Test]
        public void Sanitize_ClampsStatsToBudget_DropsUnknownMotions_AndRejectsOtherCharacters()
        {
            var m = Move("炎龍一閃");
            m.power = 9; m.speed = 9; m.reach = 9;
            m.steps = new[] { "Teleport", "Whirl", "Throw", "Thrust", "Spin", "Bash", "Rising" };
            var s = SpecialMoveRules.Sanitize(m, "炎");
            Assert.LessOrEqual(s.power + s.speed + s.reach, SpecialMoveRules.StatBudget);
            CollectionAssert.AreEqual(new[] { "Whirl", "Thrust", "Spin", "Bash" }, s.steps, "使えない技を除き 4 つまで");
            Assert.IsNull(SpecialMoveRules.Sanitize(m, "龍"));
            m.steps = new[] { "Teleport" };
            CollectionAssert.AreEqual(new[] { AttackStyle.DashSlash }, SpecialMoveRules.Sanitize(m, "炎").Styles());
        }

        [Test]
        public void Colors_FallBackToElement()
        {
            var m = Move("炎龍一閃");
            m.primary = "red";
            Assert.AreEqual(SpecialMoveRules.ElementColor(SpecialElement.Fire, false), m.PrimaryColor);
            m.element = "nonsense";
            Assert.AreEqual(SpecialElement.None, m.Element);
        }

        [Test]
        public void Library_HoldsThreePerCharacter_AndBuildsKeepEquippedCopy()
        {
            Assert.IsTrue(SpecialMoveLibrary.Add(Move("炎一")));
            Assert.IsTrue(SpecialMoveLibrary.Add(Move("炎二")));
            Assert.IsTrue(SpecialMoveLibrary.Add(Move("炎三")));
            Assert.IsFalse(SpecialMoveLibrary.Add(Move("炎四")), "一文字につき 3 つまで");
            Assert.IsTrue(SpecialMoveLibrary.Add(Move("龍", "龍")), "別の文字は別枠");
            Assert.IsTrue(SpecialMoveLibrary.Add(Move("炎二")), "同じ名前は上書き");
            Assert.AreEqual(3, SpecialMoveLibrary.For("炎").Count);
            SpecialMoveLibrary.Remove(Move("炎一"));
            Assert.AreEqual(2, SpecialMoveLibrary.For("炎").Count);

            var build = new FighterBuildData { character = "炎", special = Move("炎二") };
            var copy = build.Clone();
            copy.special.name = "変更";
            Assert.AreEqual("炎二", build.special.name, "Clone は技も複製する");
            var json = JsonUtility.FromJson<FighterBuildData>(JsonUtility.ToJson(build));
            Assert.AreEqual("炎二", json.EquippedSpecial.name);
            json.character = "龍";
            Assert.IsNull(json.EquippedSpecial, "別の文字の技は出せない");
            Assert.IsNull(new FighterBuildData { character = "炎" }.EquippedSpecial);
        }
    }
}
