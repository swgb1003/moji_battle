using NUnit.Framework;

namespace MojiBattle.Tests
{
    public class FriendRoomServerConfigTests
    {
        [Test]
        public void BundledPublicUrl_TakesPrecedenceOverPreviouslySavedLocalhost()
        {
            Assert.AreEqual("https://rooms.example.com", FriendRoomServerConfig.Resolve(" https://rooms.example.com/\n", "http://127.0.0.1:8787"));
        }
        [TestCase(null)]
        [TestCase("")]
        [TestCase("http://rooms.example.com")]
        [TestCase("https://127.0.0.1:8787")]
        [TestCase("https://user:secret@rooms.example.com")]
        [TestCase("https://rooms.example.com?token=secret")]
        [TestCase("https://rooms.example.com#fragment")]
        public void InvalidPublicConfig_PreservesLocalConnectionOptions(string value)
        {
            Assert.IsNull(FriendRoomServerConfig.PublicUrl(value));
            Assert.AreEqual("http://192.168.1.10:8787", FriendRoomServerConfig.Resolve(value, "http://192.168.1.10:8787"));
        }
        [TestCase("https://moji-fighter-x.vercel.app/", "https://moji-fighter-x.vercel.app/rooms-api")]
        [TestCase("https://moji-fighter-x.vercel.app/index.html?x=1", "https://moji-fighter-x.vercel.app/rooms-api")]
        [TestCase("http://localhost:8000/", null)]
        [TestCase("", null)]
        public void WebPage_RelaysThroughItsOwnOrigin(string page, string relay)
        {
            Assert.AreEqual(relay, FriendRoomServerConfig.RelayUrl(page));
        }
        [Test]
        public void UnconfiguredGame_UsesLocalServer()
        {
            Assert.AreEqual(FriendRoomServerConfig.LocalUrl, FriendRoomServerConfig.Resolve(null, ""));
        }
    }
}
