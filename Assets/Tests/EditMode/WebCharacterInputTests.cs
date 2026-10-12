using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class WebCharacterInputTests
    {
        GameObject root;
        InputField field;
        WebCharacterInput bridge;
        FighterCustomizer customizer;
        bool accepted;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("CharInput", typeof(RectTransform));
            field = root.AddComponent<InputField>();
            var label = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            label.transform.SetParent(root.transform, false);
            label.font = RetroUi.Font;
            field.textComponent = label;
            field.characterLimit = 0;
            bridge = WebCharacterInput.Attach(field, null);
            customizer = new FighterCustomizer();
            customizer.SetFont(FontStyleId.Gothic);
            field.onValueChanged.AddListener(value =>
            {
                accepted = customizer.SetCharacter(value);
                if (accepted) field.SetTextWithoutNotify(customizer.Current.character);
            });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [TestCase("あ")]
        [TestCase("カ")]
        [TestCase("鬱")]
        [TestCase("龍")]
        [TestCase("→")]
        public void BrowserText_ReachesGlyphValidationAndWeaponConfiguration(string value)
        {
            bridge.ReceiveText(value);
            Assert.IsTrue(accepted, customizer.CharacterError);
            Assert.AreEqual(value.Trim(), customizer.Current.character);
            Assert.AreEqual(value.Trim(), field.text);
        }

        [TestCase("漢字")]
        [TestCase(" あ ")]
        public void MultipleCharacters_AreRejectedWithoutSilentlyTakingTheFirst(string value)
        {
            string previous = customizer.Current.character;
            bridge.ReceiveText(value);
            Assert.IsFalse(accepted);
            Assert.AreEqual(previous, customizer.Current.character);
            Assert.AreEqual(value, field.text);
            StringAssert.Contains("1 文字", customizer.CharacterError);
        }
    }
}
