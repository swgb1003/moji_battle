using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    public class FriendServerWarmupTests
    {
        sealed class HealthFixture : IDisposable
        {
            readonly HttpListener listener = new HttpListener();
            public readonly string Url;
            public int Gets, Posts;
            public HealthFixture(Func<int, string> response)
            {
                var port = new TcpListener(IPAddress.Loopback, 0); port.Start();
                int number = ((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
                Url = "http://127.0.0.1:" + number;
                listener.Prefixes.Add(Url + "/"); listener.Start();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (listener.IsListening)
                        {
                            var ctx = await listener.GetContextAsync();
                            if (ctx.Request.HttpMethod == "POST") Interlocked.Increment(ref Posts);
                            int count = Interlocked.Increment(ref Gets);
                            byte[] bytes = Encoding.UTF8.GetBytes(response(count));
                            ctx.Response.ContentType = "application/json"; ctx.Response.ContentLength64 = bytes.Length;
                            await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length); ctx.Response.Close();
                        }
                    }
                    catch (HttpListenerException) { }
                    catch (ObjectDisposedException) { }
                });
            }
            public void Dispose() { listener.Close(); }
        }
        const string Healthy = "{\"ok\":true,\"version\":\"moji-fighter-x-rooms-v3\"}";

        [UnityTest]
        public IEnumerator SleepingServer_RetriesHealthAndRecoversWithoutPostingRooms()
        {
            using (var server = new HealthFixture(count => count == 1 ? "<html>Starting</html>" : Healthy))
            {
                var client = new FriendRoomClient(server.Url); int waiting = 0;
                yield return client.WaitUntilReady(onWaiting: () => waiting++, waitSeconds: 6);
                Assert.IsNull(client.Error); Assert.AreEqual(2, server.Gets);
                Assert.AreEqual(1, waiting); Assert.AreEqual(0, server.Posts);
            }
        }
        [UnityTest]
        public IEnumerator LeaveWhileWaiting_StopsFurtherRequests()
        {
            using (var server = new HealthFixture(_ => "{}"))
            {
                var client = new FriendRoomClient(server.Url); bool current = true;
                yield return client.WaitUntilReady(() => current, () => current = false, 6);
                Assert.AreEqual(1, server.Gets); Assert.AreEqual(0, server.Posts);
            }
        }
        [UnityTest]
        public IEnumerator UnavailableServer_FinishesWithRecoverableError()
        {
            using (var server = new HealthFixture(_ => "{}"))
            {
                var client = new FriendRoomClient(server.Url);
                yield return client.WaitUntilReady(waitSeconds: .3f);
                Assert.IsNotEmpty(client.Error); Assert.AreEqual(0, server.Posts);
            }
        }
        [UnityTest]
        public IEnumerator IncompatibleServer_FailsWithoutWaitingOrCreatingRooms()
        {
            using (var server = new HealthFixture(_ => "{\"ok\":true,\"version\":\"other\"}"))
            {
                var client = new FriendRoomClient(server.Url);
                yield return client.WaitUntilReady(waitSeconds: 6);
                Assert.IsNotEmpty(client.Error); Assert.AreEqual(1, server.Gets); Assert.AreEqual(0, server.Posts);
            }
        }
    }
}
