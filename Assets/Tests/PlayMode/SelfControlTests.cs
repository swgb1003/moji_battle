using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>自操作: 方向＋ボタンが既存の技に対応し、跳ぶ・しゃがむ・ガード・受け流し・入力の記憶が働く。練習は HP が減らない。</summary>
    public class SelfControlTests
    {
        BattleInstance battle;
        PlayerController player;

        [TearDown] public void Cleanup() { battle?.Destroy(); battle = null; SimHarness.End(); }

        IEnumerator Build(WeaponSize size = WeaponSize.M, string left = "火", string right = "山")
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(7177, SimHarness.Custom(left, size), SimHarness.Custom(right), 60f);
            battle.Director.enabled = false;
            battle.Context.PlayerSide = 0;
            player = battle.Left.Brain.AttachPlayer();
            PlayControl.ApplyDifficulty(battle.Right, ComDifficulty.Sandbag, null);
            for (int i = 0; i < 40; i++) battle.Director.SimulateStep();
            yield return null;
        }

        void Ready()
        {
            var f = battle.Left; var rt = f.Runtime;
            // 技・回避が終わって、地面に立つまで待つ（途中で状態を書き換えると振りの勢いで体が跳ねる）
            for (int i = 0; i < 300 && (rt.state != FighterState.Approach || !f.IsGrounded); i++)
            {
                if (rt.state == FighterState.Guard || rt.state == FighterState.Feint) f.SetState(FighterState.Approach);
                battle.Director.SimulateStep();
            }
            rt.attackReadyAt = rt.dashReadyAt = rt.whirlReadyAt = rt.rangedReadyAt = rt.throwReadyAt = rt.kickReadyAt
                = rt.feintReadyAt = rt.hookReadyAt = rt.evadeReadyAt = rt.duckReadyAt = rt.wallJumpReadyAt = -1f;
        }

        void Step(int n = 1) { for (int i = 0; i < n; i++) battle.Director.SimulateStep(); }

        /// <summary>forward: +1 前 / -1 後（向きから画面の左右へ直す）</summary>
        FighterState Fire(PlayerButton button, int forward, int y)
        {
            Ready();
            player.Input.Press(button, forward * battle.Left.Facing, y);
            Step();
            return battle.Left.Runtime.state;
        }

        void Expect(PlayerButton button, int forward, int y, AttackStyle style)
        {
            var state = Fire(button, forward, y);
            Assert.AreEqual(FighterState.AttackWindup, state, $"{button} {forward} {y}");
            Assert.AreEqual(style, battle.Left.Runtime.attackStyle, $"{button} {forward} {y}");
        }

        [UnityTest] public IEnumerator DirectionAndButtonChooseEveryTechnique_MediumWeapon()
        {
            yield return Build();
            Expect(PlayerButton.Slash, 0, 0, AttackStyle.Overhead);
            Expect(PlayerButton.Slash, 0, 1, AttackStyle.Rising);
            Expect(PlayerButton.Slash, 1, 0, AttackStyle.Thrust);
            Expect(PlayerButton.Slash, 0, -1, AttackStyle.LowSweep);
            Expect(PlayerButton.Slash, -1, 0, AttackStyle.Sweep);
            Expect(PlayerButton.Tech, 0, 0, AttackStyle.Bash);
            Expect(PlayerButton.Tech, 0, 1, AttackStyle.Launch);
            Expect(PlayerButton.Tech, 1, 0, AttackStyle.DashSlash);
            Expect(PlayerButton.Tech, 0, -1, AttackStyle.Spin);
            Expect(PlayerButton.Kick, 0, 0, AttackStyle.FrontKick);
            Expect(PlayerButton.Ranged, 0, 0, AttackStyle.StrokeShot);
            Expect(PlayerButton.Ranged, 1, 0, AttackStyle.Leap);
            Assert.AreEqual(FighterState.Feint, Fire(PlayerButton.Kick, -1, 0));
            // 引き崩しは字形のくぼみが要る。無ければ理由を表示して捨てる
            var state = Fire(PlayerButton.Tech, -1, 0);
            if (battle.Left.Actions.HasHook) Assert.AreEqual(AttackStyle.HookPull, battle.Left.Runtime.attackStyle);
            else { Assert.AreEqual(FighterState.Approach, state); StringAssert.Contains("くぼみ", player.LastRejection); }
            Expect(PlayerButton.Ranged, 0, -1, AttackStyle.Throw);
        }

        [UnityTest] public IEnumerator HeavyWeaponUsesWhirlAndGroundWave()
        {
            yield return Build(WeaponSize.XL);
            Expect(PlayerButton.Tech, 1, 0, AttackStyle.Whirl);
            Expect(PlayerButton.Ranged, 0, 0, AttackStyle.GroundWave);
        }

        [UnityTest] public IEnumerator UpAndDownJumpOrDuckUnlessAButtonFollows()
        {
            yield return Build();
            Ready(); player.Input.Press(PlayerButton.Jump, 0, 1);
            Step(2);
            Assert.AreEqual(FighterState.Approach, battle.Left.Runtime.state, "The jump waits for a following button first.");
            Step(8);
            Assert.AreEqual(FighterState.Evade, battle.Left.Runtime.state,
                $"grounded={battle.Left.IsGrounded} evadeReady={battle.Left.Runtime.evadeReadyAt} t={battle.Context.SimTime} ai={battle.Context.AiEnabled} last={player.LastCommand}");
            Assert.AreEqual(EvadeKind.HopOver, battle.Left.Runtime.evadeKind);

            for (int i = 0; i < 80 && !battle.Left.IsGrounded; i++) Step();
            Ready(); player.Input.Press(PlayerButton.Jump, 0, 1); Step(1); player.Input.Press(PlayerButton.Slash, 0, 0); Step(1);
            Assert.AreEqual(AttackStyle.Rising, battle.Left.Runtime.attackStyle, "↑ then Z is a rising slash, not a jump.");
            Step(10);
            Assert.AreNotEqual(FighterState.Evade, battle.Left.Runtime.state);

            Ready(); player.Input.Press(PlayerButton.Duck, 0, -1); Step(10);
            Assert.AreEqual(FighterState.Evade, battle.Left.Runtime.state,
                $"pos={battle.Left.Body.position} v={battle.Left.Body.linearVelocity} opp={battle.Right.Body.position} grounded={battle.Left.IsGrounded} state={battle.Left.Runtime.state} kind={battle.Left.Runtime.evadeKind} last={player.LastCommand} rej={player.LastRejection}");
            Assert.AreEqual(EvadeKind.Duck, battle.Left.Runtime.evadeKind);

            for (int i = 0; i < 60 && battle.Left.Runtime.state == FighterState.Evade; i++) Step();
            Ready(); player.Input.Press(PlayerButton.Backstep, -battle.Left.Facing, 0); Step();
            Assert.AreEqual(EvadeKind.Backstep, battle.Left.Runtime.evadeKind);
        }

        [UnityTest] public IEnumerator HoldingGuardBlocksAndTimedPressArmsParry()
        {
            yield return Build();
            Ready(); player.Input.GuardHeld = true; Step();
            Assert.AreEqual(FighterState.Guard, battle.Left.Runtime.state);
            player.Input.GuardHeld = false; Step();
            Assert.AreEqual(FighterState.Approach, battle.Left.Runtime.state);

            // 相手の振りが当たる直前に C
            var com = battle.Right;
            Vector2 delta = new Vector2(battle.Left.X + 1.3f * battle.Left.Facing - com.X, 0f);
            com.Body.position += delta; com.WeaponBody.position += delta;
            Step(5);
            Ready(); player.Input.GuardHeld = true; Step();
            com.Runtime.attackReadyAt = -1f; com.StartAttack(battle.Context.SimTime, AttackStyle.Overhead);
            float toHit = 99f;
            for (int i = 0; i < 100 && !(battle.Left.Brain.ThreatIncoming(out toHit) && toHit <= .2f); i++) Step();
            Assert.LessOrEqual(toHit, .2f);
            battle.Left.Runtime.parryReadyAt = -1f;
            player.Input.Press(PlayerButton.Guard); Step();
            Assert.IsTrue(battle.Left.Runtime.parryArmed, "A guard press just before the hit arms a parry.");
            player.Input.GuardHeld = false;
        }

        [UnityTest] public IEnumerator ButtonPressedWhileBusyFiresWhenFree()
        {
            yield return Build();
            Expect(PlayerButton.Slash, 0, 0, AttackStyle.Overhead);
            int first = battle.Left.Runtime.currentAttackId;
            // 振り終わりの硬直中に押す
            for (int i = 0; i < 200 && battle.Left.Runtime.state != FighterState.AttackRecovery; i++) Step();
            Assert.AreEqual(FighterState.AttackRecovery, battle.Left.Runtime.state);
            player.Input.Press(PlayerButton.Slash, battle.Left.Facing, 0);
            for (int i = 0; i < 200 && battle.Left.Runtime.currentAttackId == first; i++) Step();
            Assert.AreNotEqual(first, battle.Left.Runtime.currentAttackId, "The buffered slash should start after recovery.");
            Assert.AreEqual(AttackStyle.Thrust, battle.Left.Runtime.attackStyle);
            Assert.AreEqual(0, battle.Left.Runtime.comboRemaining);
        }

        [UnityTest] public IEnumerator SandbagStaysStillAndDifficultyWakesIt()
        {
            yield return Build();
            var com = battle.Right;
            float x = com.X;
            Step(150);
            Assert.AreEqual(0, com.Runtime.metrics.attacksStarted);
            Assert.Less(Mathf.Abs(com.X - x), .3f, "The sandbag does not walk.");
            PlayControl.ApplyDifficulty(com, ComDifficulty.Strong, null);
            Assert.IsFalse(com.Brain.Frozen);
            for (int i = 0; i < 600 && com.Runtime.metrics.attacksStarted == 0; i++) Step();
            Assert.Greater(com.Runtime.metrics.attacksStarted, 0);
            float strongReaction = com.Tendency.reactionChance;
            PlayControl.ApplyDifficulty(com, ComDifficulty.Beginner, null);
            Assert.Less(com.Tendency.reactionChance, strongReaction);
            Assert.Greater(com.Brain.DecisionScale, 1f);
            PlayControl.ApplyDifficulty(com, ComDifficulty.Strong, null);
            Assert.AreEqual(strongReaction, com.Tendency.reactionChance, 1e-5f, "Re-applying starts from the original values.");
        }

        [UnityTest] public IEnumerator PracticeKeepsHpAndNeverTimesOut()
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(7177, SimHarness.Custom("火"), SimHarness.Custom("山"), 1f);
            battle.Director.enabled = false;
            battle.Context.Practice = true; battle.Context.PlayerSide = 0;
            foreach (var f in battle.Director.Fighters) f.Runtime.invulnerable = true;
            player = battle.Left.Brain.AttachPlayer();
            PlayControl.ApplyDifficulty(battle.Right, ComDifficulty.Strong, null);
            for (int i = 0; i < 400; i++) battle.Director.SimulateStep();
            yield return null;
            Assert.IsFalse(battle.Director.Ended, "Practice has no time limit.");
            foreach (var f in battle.Director.Fighters) Assert.AreEqual(f.Runtime.maxHp, f.Runtime.hp);
            Assert.Greater(battle.Left.Runtime.metrics.damageTaken + battle.Right.Runtime.metrics.damageTaken, 0f,
                "Hits still count for the practice display even though HP does not drop.");
        }
    }
}
