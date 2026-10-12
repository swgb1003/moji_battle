using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    public class UnarmedPressureTests
    {
        BattleInstance battle;
        CombatBalance balance;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(739, countdown: 1000f, left: "一", right: "火");
            balance = Object.Instantiate(battle.Context.Balance);
            battle.Context.Balance = balance;
            balance.expandedActions = false;
            balance.idleBreakSeconds = 1000f;
            foreach (var f in battle.Director.Fighters)
            {
                f.SetTendency(new ClassTendency { attackWillingness = 0f, reactionChance = 0f,
                    guardBias = 0f, evadeBias = 0f, counterBias = 0f, proactiveGuard = 0f,
                    retreatAfterAttack = 0f, comboMin = 1, comboMax = 1 });
                f.Runtime.evadeReadyAt = 1000f;
            }
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(battle.Left.IsGrounded);
        }

        [TearDown]
        public void Cleanup()
        {
            battle?.Destroy();
            if (balance != null) Object.Destroy(balance);
            SimHarness.End();
        }

        static void Move(Fighter f, float x)
        {
            var delta = new Vector2(x, .02f) - f.Body.position;
            f.Body.position += delta;
            f.WeaponBody.position += delta;
            f.Body.transform.position += (Vector3)delta;
            f.WeaponBody.transform.position += (Vector3)delta;
            f.Body.linearVelocity = f.WeaponBody.linearVelocity = Vector2.zero;
        }

        void Detach(bool flying = false)
        {
            var f = battle.Right;
            f.Hinge.enabled = false;
            f.Runtime.weaponDetached = true;
            f.Runtime.throwLive = flying;
            f.Runtime.attackStyle = AttackStyle.Throw;
            f.Runtime.thrownAt = battle.Context.SimTime;
            f.WeaponBody.position = new Vector2(f.X + 3f, .4f);
            f.WeaponBody.linearVelocity = Vector2.zero;
        }

        [UnityTest]
        public IEnumerator BlockedApproach_AttacksUnarmedOpponentWithoutIdleTimeout()
        {
            Move(battle.Left, 0f);
            Move(battle.Right, battle.Left.AttackRange * 1.3f);
            Detach();
            float time = battle.Context.SimTime + 1f;
            // Physical contact can stop forward progress just outside the nominal range.
            for (int i = 0; i < 8 && battle.Left.Runtime.attackSerial == 0; i++)
                battle.Left.Brain.Tick(time + i * (balance.aiInterval + .01f));
            Assert.Greater(battle.Left.Runtime.attackSerial, 0, "A blocked advance must turn into a strike, rather than wait for the global idle breaker.");
            yield break;
        }

        [UnityTest]
        public IEnumerator PassedProjectile_StopsGuardingAndAttacksItsUnarmedOwner()
        {
            var a = battle.Left; var d = battle.Right;
            Move(a, 0f); Move(d, a.AttackRange * .9f);
            Detach(true);
            a.SetState(FighterState.Guard);
            a.Runtime.stateTime = balance.guardMinDwell;
            d.WeaponBody.position = new Vector2(a.X + 1f, 1f);
            d.WeaponBody.linearVelocity = new Vector2(-5f, 0f);
            float time = battle.Context.SimTime + 1f;
            a.Brain.Tick(time);
            Assert.AreEqual(FighterState.Guard, a.Runtime.state, "An incoming weapon still requires defense.");
            d.WeaponBody.position = new Vector2(a.X - 1f, 1f);
            a.Brain.Tick(time + balance.aiInterval + .01f);
            Assert.AreEqual(FighterState.AttackWindup, a.Runtime.state, "Once the weapon has passed, the owner cannot block a follow-up.");
            yield break;
        }

        [UnityTest]
        public IEnumerator CorneredAtCloseRange_AttacksInsteadOfRetreatingIntoWall()
        {
            var a = battle.Left;
            Move(a, -battle.Context.ArenaHalfWidth + .3f);
            Move(battle.Right, a.X + .4f);
            Detach();
            a.Brain.Tick(battle.Context.SimTime + 1f);
            Assert.AreEqual(FighterState.AttackWindup, a.Runtime.state);
            yield break;
        }

        [UnityTest]
        public IEnumerator SafeCloseRange_CreatesSpaceBeforeAttacking()
        {
            Move(battle.Left, 0f); Move(battle.Right, .4f);
            Detach();
            battle.Left.Brain.Tick(battle.Context.SimTime + 1f);
            Assert.AreEqual(FighterState.Approach, battle.Left.Runtime.state);
            Assert.AreEqual(MoveIntent.Retreat, battle.Left.Brain.Move);
            yield break;
        }

        [UnityTest]
        public IEnumerator UnarmedKOAndProtectedDownedOpponent_DoNotTriggerPressureAttack()
        {
            Move(battle.Left, 0f); Move(battle.Right, battle.Left.AttackRange * .9f);
            Detach();
            battle.Right.SetState(FighterState.KO);
            float time = battle.Context.SimTime + 1f;
            battle.Left.Brain.Tick(time);
            Assert.AreEqual(0, battle.Left.Runtime.attackSerial);
            balance.allowAttackOnDowned = false;
            battle.Right.SetState(FighterState.Knockdown);
            battle.Left.Brain.Tick(time + balance.aiInterval + .01f);
            Assert.AreEqual(0, battle.Left.Runtime.attackSerial);
            yield break;
        }

        [UnityTest]
        public IEnumerator RealThrow_DefensiveAndCounterStyles_CloseGapAndDamageUnarmedOwner()
        {
            battle.Destroy();
            yield return null;
            foreach (var style in new[] { BattleStyle.Defensive, BattleStyle.Counter })
            {
                battle = SimHarness.Build(739, SimHarness.Custom("一", style: style), SimHarness.Custom("火"));
                battle.Context.Balance = balance;
                var a = battle.Left; var d = battle.Right;
                float walkSpeed = a.WalkSpeed;
                foreach (var f in battle.Director.Fighters)
                {
                    f.Runtime.attackReadyAt = 1000f;
                    f.Runtime.evadeReadyAt = 1000f;
                    f.WalkSpeed = 0f;
                    f.SetTendency(new ClassTendency { attackWillingness = 0f, reactionChance = 0f,
                        guardBias = 0f, evadeBias = 0f, counterBias = 0f, proactiveGuard = 0f,
                        retreatAfterAttack = 0f, comboMin = 1, comboMax = 1 });
                }
                Move(a, -2.25f); Move(d, 2.25f);
                yield return null;
                bool hitUnarmed = false;
                battle.Context.Events.Hit += e =>
                {
                    if (e.attacker == a.Id && e.defender == d.Id && d.Runtime.weaponDetached && e.damage > 0f)
                        hitUnarmed = true;
                };
                d.StartAttack(battle.Context.SimTime, AttackStyle.Throw);
                for (int i = 0; i < 300 && !d.Runtime.weaponDetached; i++) yield return null;
                Assert.IsTrue(d.Runtime.weaponDetached, "The real throw must release the weapon.");
                a.Runtime.attackReadyAt = 0f;
                a.WalkSpeed = walkSpeed;
                float start = battle.Context.SimTime;
                for (int i = 0; i < 300 && !hitUnarmed && !battle.Director.Ended; i++) yield return null;
                Debug.Log($"[UNARMED] style={style} hit={hitUnarmed} elapsed={battle.Context.SimTime - start:F2} attacks={a.Runtime.attackSerial} decision={a.Brain.LastDecision} distance={a.DistanceToOpponent:F2}");
                Assert.IsTrue(hitUnarmed, $"{style} must follow up with physical damage before the opponent retrieves the weapon.");
                battle.Destroy();
                battle = null;
                yield return null;
            }
        }
    }
}
