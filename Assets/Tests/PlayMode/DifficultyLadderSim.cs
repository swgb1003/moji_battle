using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>
    /// 自操作の強さの段階の確認（バランス計測用）: 決まった動きをする「入力ボット」が各段階の COM と戦った勝率。
    /// ボットは人の操作と同じ入力（PlayerInput）だけを使う。段階が上がるほど勝率が下がることを確かめる。
    /// </summary>
    [Explicit("バランス計測用")]
    public class DifficultyLadderSim
    {
        static readonly string[] Glyphs = { "火", "刀", "山", "一", "龍" };

        /// <summary>間合いに入れば方向つきの斬る・技を混ぜて振り、相手の振りが来れば少し遅れてガード（時々受け流し）する。</summary>
        sealed class Bot
        {
            readonly Fighter self; readonly PlayerInput input; readonly System.Random rng;
            float next, guardUntil;
            public Bot(Fighter self, PlayerInput input, int seed) { this.self = self; input = input; this.input = input; rng = new System.Random(seed); }
            public void Tick(float time)
            {
                if (time < next) return;
                next = time + .12f;
                input.Left = input.Right = input.Up = input.Down = false;
                int toward = self.TowardOpponent;
                if (self.Brain.ThreatIncoming(out float toHit) && rng.NextDouble() < .55)
                {
                    input.GuardHeld = true; guardUntil = time + .45f;
                    if (toHit <= .25f && rng.NextDouble() < .3) input.Press(PlayerButton.Guard);
                    return;
                }
                if (time < guardUntil) return;
                input.GuardHeld = false;
                float d = self.DistanceToOpponent;
                if (d > self.AttackRange * 1.05f) { if (toward > 0) input.Right = true; else input.Left = true; return; }
                double r = rng.NextDouble();
                var button = r < .7 ? PlayerButton.Slash : r < .9 ? PlayerButton.Tech : PlayerButton.Kick;
                int dir = rng.Next(5);
                input.Press(button, dir == 1 ? toward : dir == 2 ? -toward : 0, dir == 3 ? 1 : dir == 4 ? -1 : 0);
            }
        }

        [UnityTest, Timeout(3600000)] public IEnumerator BotWinRateFallsAsComGetsStronger()
        {
            SimHarness.Begin(false);
            int seeds = int.TryParse(System.Environment.GetEnvironmentVariable("LADDER_SEEDS"), out var n) ? n : 8;
            var md = new StringBuilder("| COM の強さ | ボットの勝率 | ボットの与ダメージ/試合 | COM の与ダメージ/試合 |\n|---|---|---|---|\n");
            foreach (var level in PlayControl.Levels)
            {
                int wins = 0, decided = 0, matches = 0; float dealt = 0, taken = 0;
                foreach (var ch in Glyphs)
                for (int i = 0; i < seeds; i++)
                {
                    var battle = SimHarness.Build(9100 + i * 53, SimHarness.Custom(ch), SimHarness.Custom(Glyphs[(i + 2) % Glyphs.Length]), 60f);
                    try
                    {
                        battle.Director.enabled = false; battle.Director.SilentSimulation = true;
                        battle.Context.PlayerSide = 0;
                        battle.Context.Interventions = new BattleIntervention(battle.Director, InterventionController.Local, InterventionController.Com);
                        var player = battle.Left.Brain.AttachPlayer();
                        PlayControl.ApplyDifficulty(battle.Right, level, battle.Context.Interventions);
                        var bot = new Bot(battle.Left, player.Input, 31 * i + 7);
                        int steps = 0;
                        while (!battle.Director.Ended && steps++ < 200000) { bot.Tick(battle.Context.SimTime); battle.Director.SimulateStep(); }
                        var r = battle.Director.Result; matches++;
                        if (r.winner >= 0) { decided++; if (r.winner == 0) wins++; }
                        dealt += r.metrics[0].damageDealt; taken += r.metrics[1].damageDealt;
                    }
                    finally { battle.Destroy(); }
                    yield return null;
                }
                md.Append(System.FormattableString.Invariant($"| {PlayControl.Label(level)} | {100f * wins / Mathf.Max(1, decided):F0}% ({wins}/{decided}) | {dealt / matches:F0} | {taken / matches:F0} |\n"));
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(SimHarness.ReportDir, "difficulty_ladder.md"), md.ToString());
            Debug.Log("[MojiBattle] DifficultyLadder\n" + md);
        }
    }
}
