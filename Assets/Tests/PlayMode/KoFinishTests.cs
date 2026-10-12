using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>とどめの吹き飛びと、KO 後のとどめのリプレイ。</summary>
    public class KoFinishTests
    {
        [TearDown]
        public void TearDown() { SimHarness.End(); KoReplay.Enabled = true; }

        /// <summary>どの技でとどめを刺しても、倒れた側は勝者から離れる向きへ大きく飛ぶ。</summary>
        [UnityTest]
        public IEnumerator FinishingBlow_AlwaysLaunchesLoserAway()
        {
            SimHarness.Begin(false);
            for (int side = 0; side < 2; side++)
            {
                var battle = SimHarness.Build(5100 + side, SimHarness.Custom("火"), SimHarness.Custom("山"));
                var loser = side == 0 ? battle.Right : battle.Left;
                var winner = loser.Opponent;
                yield return null;
                float startX = loser.X;
                loser.Runtime.hp = 0f;
                battle.Director.SimulateStep();
                Assert.IsTrue(loser.Runtime.koLaunched, "とどめで吹き飛ばない");
                float away = Mathf.Sign(loser.X - winner.X);
                Assert.Greater(loser.Body.linearVelocity.x * away, 5f, "勝者から離れる向きに飛ばない");
                Assert.Greater(loser.Body.linearVelocity.y, 3f, "浮き上がらない");
                for (int i = 0; i < 20; i++) battle.Director.SimulateStep();
                Assert.Greater((loser.X - startX) * away, .4f, "倒れた側が飛んでいない");
                battle.Destroy();
                yield return null;
            }
        }

        /// <summary>対戦画面では KO の後にとどめの瞬間を再生し、終わったら実際の姿勢に戻して結果を出す。</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Presentation_ReplaysFinishingMomentThenShowsResult()
        {
            GameFlow.ToCustomize(); yield return null; yield return null;
            GameFlow.ToBattle(); yield return null; yield return null;
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            var director = boot.Battle.Director;
            Assert.IsNotNull(director.Replay);
            double deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (!director.Fighting && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            // 少し戦わせてから決着させる
            deadline = Time.realtimeSinceStartupAsDouble + 1.5;
            while (Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            // 次に当たった一撃でとどめになる
            boot.Battle.Right.Runtime.hp = 1f;
            boot.Battle.Left.Runtime.hp = boot.Battle.Left.Runtime.maxHp * 10f;
            bool played = false;
            deadline = Time.realtimeSinceStartupAsDouble + 40;
            while (!director.Ended && Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (director.Replay.Playing && !played)
                {
                    played = true;
                    yield return new WaitForSecondsRealtime(1.2f);
                    MenuUiTests.Capture("ko_replay_before");
                    yield return new WaitForSecondsRealtime(1.0f);
                    MenuUiTests.Capture("ko_replay_finish");
                    Assert.IsFalse(director.Ended, "リプレイ中に結果を出さない");
                    Assert.IsNotNull(GameObject.Find("ReplayBanner"));
                }
                yield return null;
            }
            Assert.IsTrue(played, "リプレイが流れない");
            Assert.IsTrue(director.Ended, "リプレイの後に結果へ進まない");
            Assert.IsFalse(director.Replay.Playing);
            Assert.IsNull(GameObject.Find("ReplayBanner"), "リプレイの表示が残っている");
            Assert.LessOrEqual(boot.Battle.Right.Runtime.hp, 0f, "リプレイの後に実際の状態へ戻っていない");
        }
    }
}
