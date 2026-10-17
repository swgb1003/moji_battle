using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    public sealed class JumpCrossoverTests
    {
        BattleInstance battle;

        [TearDown] public void Cleanup() { battle?.Destroy(); SimHarness.End(); }

        static void Move(Fighter f, float x)
        {
            var delta = new Vector2(x, .02f) - f.Body.position;
            f.Body.position += delta; f.WeaponBody.position += delta;
            f.Body.linearVelocity = f.WeaponBody.linearVelocity = Vector2.zero;
            f.Body.angularVelocity = f.WeaponBody.angularVelocity = 0;
        }

        void Step(int n = 1) { for (int i = 0; i < n; i++) battle.Director.SimulateStep(); }

        IEnumerator Cross(int side, WeaponSize defenderSize, bool fastQueries)
        {
            SimHarness.Begin(false);
            var medium = SimHarness.Custom("火", WeaponSize.M);
            var wide = SimHarness.Custom("山", defenderSize);
            battle = SimHarness.Build(7177, side == 0 ? medium : wide, side == 0 ? wide : medium);
            battle.Director.enabled = false; battle.Director.FastSimulationQueries = fastQueries;
            battle.Context.PlayerSide = side;
            var jumper = battle.Director.Fighters[side]; var defender = jumper.Opponent;
            var player = jumper.Brain.AttachPlayer();
            PlayControl.ApplyDifficulty(defender, ComDifficulty.Sandbag, null);
            Step(35);
            int direction = side == 0 ? 1 : -1;
            Move(jumper, -direction * 1.25f); Move(defender, direction * 1.25f);
            Step(2);
            jumper.Runtime.evadeReadyAt = -1;
            player.Input.Press(PlayerButton.Jump, direction, 1);
            for (int i = 0; i < 15 && jumper.Runtime.state != FighterState.Evade; i++) Step();
            Assert.AreEqual(EvadeKind.HopOver, jumper.Runtime.evadeKind);
            Assert.AreEqual(FighterState.Evade, jumper.Runtime.state);
            // All relevant pairs must allow passage from the first physics step.
            foreach (var body in jumper.BodyColliders)
            {
                foreach (var other in defender.BodyColliders) Assert.IsTrue(Physics2D.GetIgnoreCollision(body, other));
                foreach (var other in defender.WeaponColliders) Assert.IsTrue(Physics2D.GetIgnoreCollision(body, other));
            }
            float peak = 0; bool crossed = false;
            for (int i = 0; i < 180; i++)
            {
                Step(); peak = Mathf.Max(peak, jumper.Body.position.y);
                crossed |= (jumper.X - defender.X) * direction > .35f;
                Assert.Less(Mathf.Abs(jumper.X), battle.Context.ArenaHalfWidth);
            }
            Assert.Greater(peak, .5f);
            Assert.IsTrue(crossed, $"Ordinary jump must cross the opponent: side={side}, size={defenderSize}, x={jumper.X:F2}, other={defender.X:F2}");
            Assert.IsTrue(jumper.IsGrounded);
            Assert.AreEqual(-direction, jumper.Facing, "Landing on the far side must turn back toward the opponent.");
            // Clearing the opponent must restore normal collisions; the jump is not permanent phasing.
            foreach (var body in jumper.BodyColliders)
            foreach (var other in defender.BodyColliders)
                Assert.IsFalse(Physics2D.GetIgnoreCollision(body, other));
            yield return null;
        }

        [UnityTest] public IEnumerator NormalJump_CrossesFromLeft_AndRestoresContact() { yield return Cross(0, WeaponSize.M, false); }
        [UnityTest] public IEnumerator NormalJump_CrossesFromRight_AndRestoresContact() { yield return Cross(1, WeaponSize.M, false); }
        [UnityTest] public IEnumerator NormalJump_CrossesExtraLargeGlyph_FromLeft() { yield return Cross(0, WeaponSize.XL, false); }
        [UnityTest] public IEnumerator NormalJump_CrossesExtraLargeGlyph_FromRight_InFastSimulation() { yield return Cross(1, WeaponSize.XL, true); }

        [UnityTest] public IEnumerator NormalJump_DoesNotBecomeInvulnerableToAnOverlappingAttack()
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(731, left: "一", right: "一");
            battle.Director.enabled = false;
            foreach (var f in battle.Director.Fighters)
            {
                f.Runtime.attackReadyAt = f.Runtime.evadeReadyAt = 1000;
                f.WalkSpeed = 0;
                f.SetTendency(new ClassTendency { reactionChance = 0, attackWillingness = 0,
                    guardBias = 0, evadeBias = 0, counterBias = 0, proactiveGuard = 0 });
            }
            Step(30);
            var jumper = battle.Left; var attacker = battle.Right;
            jumper.StartEvade(EvadeKind.HopOver, battle.Context.SimTime);
            Step(8);
            Assert.AreEqual(FighterState.Evade, jumper.Runtime.state);
            attacker.Runtime.attackReadyAt = -1;
            attacker.StartAttack(battle.Context.SimTime, AttackStyle.Overhead);
            attacker.Runtime.activeDuration = 2;
            attacker.SetState(FighterState.AttackActive);
            attacker.Hinge.enabled = false;
            // Place an actual stroke through the airborne body. Ghosting should remove
            // the obstruction while the normal overlap pipeline still reports damage.
            var stroke = attacker.WeaponColliders[0];
            attacker.WeaponBody.position += jumper.ChestWorld - (Vector2)stroke.bounds.center;
            attacker.WeaponBody.linearVelocity = new Vector2(-8, 0);
            attacker.WeaponBody.angularVelocity = 0;
            float hp = jumper.Runtime.hp;
            Step(2);
            Assert.Less(jumper.Runtime.hp, hp, "A crossover changes physical blocking, not attack damage.");
            yield return null;
        }
    }
}
