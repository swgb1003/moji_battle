using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>Battle シーンを読み込み、カウントダウン後に戦闘が始まって進むこと（エラーログが出ないこと）を確認する。</summary>
    public class BattleSceneSmokeTest
    {
        [UnityTest]
        public IEnumerator BattleSceneStartsAndFights()
        {
            SimHarness.Begin(true);
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.IsNotNull(boot);
            yield return null;
            Assert.IsNotNull(boot.Battle, "BattleBootstrap が試合を組み立てていない");
            TimeController.SetSpectatorSpeed(2f);
            for (int i = 0; i < 600 && boot.Battle.Director.Clock < 5f; i++) yield return null;
            Assert.Greater(boot.Battle.Director.Clock, 4.9f, "カウントダウン後に時計が進まない");
            Assert.Greater(boot.Battle.Left.Runtime.metrics.attacksStarted + boot.Battle.Right.Runtime.metrics.attacksStarted, 0);
            boot.StartMatch(boot.CurrentSeed); // 同じ seed で再戦（T キー相当）
            yield return null;
            Assert.IsNotNull(boot.Battle);
            SimHarness.End();
        }
    }
}
