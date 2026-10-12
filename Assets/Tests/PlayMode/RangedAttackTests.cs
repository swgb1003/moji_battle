using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    public class RangedAttackTests
    {
        BattleInstance battle;
        CombatBalance tuning;

        [TearDown]
        public void Cleanup()
        {
            battle?.Destroy();
            if (tuning != null) Object.Destroy(tuning);
            SimHarness.End();
        }

        void Build(bool heavy, float distance = 6f, bool ai = false, bool presentation = false)
        {
            SimHarness.Begin(presentation);
            battle = SimHarness.Build(902, SimHarness.Custom(heavy ? "鬱" : "一", heavy ? WeaponSize.L : WeaponSize.S), SimHarness.Custom("一", WeaponSize.S), presentation: presentation);
            battle.Director.enabled = false;
            tuning = Object.Instantiate(battle.Context.Balance);
            battle.Context.Balance = tuning;
            if (!ai) tuning.aiInterval = 1000f; // Decide once before the forced action, then leave the test in manual control.
            foreach (var f in battle.Director.Fighters)
            {
                Vector2 shift = new Vector2((f.Id == 0 ? -1f : 1f) * distance * .5f - f.X, 0f);
                f.Body.position += shift; f.WeaponBody.position += shift;
                if (!ai)
                {
                    f.WalkSpeed = 0f;
                    f.Runtime.attackReadyAt = f.Runtime.evadeReadyAt = 1000f;
                    f.SetTendency(new ClassTendency { attackWillingness = 0f, reactionChance = 0f, proactiveGuard = 0f });
                }
            }
            battle.Director.SimulateStep();
        }

        [UnityTest]
        public IEnumerator StrokeShot_HitsOnce_AfterOwnerStartsAnotherAttack()
        {
            Build(false);
            var a = battle.Left; var b = battle.Right;
            var hits = new List<HitEvent>();
            battle.Context.Events.Hit += e => hits.Add(e);
            a.StartAttack(battle.Context.SimTime, AttackStyle.StrokeShot);
            int id = a.Runtime.currentAttackId;
            for (int i = 0; i < 60 && battle.Context.Ranged.LaunchCount == 0; i++) battle.Director.SimulateStep();
            Assert.AreEqual(1, battle.Context.Ranged.LaunchCount);
            Assert.IsFalse(a.Runtime.weaponDetached);
            a.StartAttack(battle.Context.SimTime, AttackStyle.Overhead);
            for (int i = 0; i < 70; i++) battle.Director.SimulateStep();
            var shotHits = hits.FindAll(e => e.style == AttackStyle.StrokeShot);
            Assert.AreEqual(1, shotHits.Count);
            Assert.AreEqual(id, shotHits[0].attackId);
            Assert.Less(b.Runtime.hp, b.Runtime.maxHp);
            Assert.AreEqual(0, battle.Context.Ranged.Projectiles.Count);
            yield break;
        }

        [UnityTest]
        public IEnumerator StrokeShot_IsBlockedByAFrontalGuard()
        {
            Build(false);
            int guards = 0;
            battle.Context.Events.Guard += e => guards++;
            battle.Left.StartAttack(battle.Context.SimTime, AttackStyle.StrokeShot);
            for (int i = 0; i < 100; i++)
            {
                battle.Right.SetState(FighterState.Guard);
                battle.Right.Runtime.stateTime = 0f;
                battle.Director.SimulateStep();
            }
            Assert.AreEqual(1, guards);
            Assert.AreEqual(battle.Right.Runtime.maxHp, battle.Right.Runtime.hp);
            yield break;
        }

        [UnityTest]
        public IEnumerator GroundWave_JumpsThenHitsTheFloor_AndDamagesOnce()
        {
            Build(true);
            float peak = 0f, contactY = 99f;
            int launches = 0, hits = 0;
            battle.Context.Events.RangedLaunch += e =>
            {
                launches++;
                foreach (var c in battle.Left.WeaponColliders) contactY = Mathf.Min(contactY, c.bounds.min.y);
                Assert.AreEqual(FighterState.AttackActive, battle.Left.Runtime.state);
            };
            battle.Context.Events.Hit += e => { if (e.style == AttackStyle.GroundWave) hits++; };
            battle.Left.StartAttack(battle.Context.SimTime, AttackStyle.GroundWave);
            for (int i = 0; i < 220; i++)
            {
                battle.Director.SimulateStep(); peak = Mathf.Max(peak, battle.Left.Body.position.y);
                if (i % 50 == 0) yield return null;
            }
            Debug.Log($"[RANGED] wave peak={peak:F2} floorY={contactY:F2} launches={launches} hits={hits} finalState={battle.Left.Runtime.state} bodyY={battle.Left.Body.position.y:F2}");
            Assert.Greater(peak, .5f);
            Assert.AreEqual(1, launches);
            Assert.Less(contactY, .1f);
            Assert.AreEqual(1, hits);
            Assert.IsFalse(battle.Left.Runtime.weaponDetached);
            Assert.AreNotEqual(FighterState.AttackActive, battle.Left.Runtime.state);
            Assert.AreNotEqual(FighterState.AttackRecovery, battle.Left.Runtime.state);
        }

        [UnityTest]
        public IEnumerator GroundWave_CanBeJumpedOver()
        {
            Build(true);
            battle.Right.Runtime.evadeReadyAt = 0f;
            battle.Left.StartAttack(battle.Context.SimTime, AttackStyle.GroundWave);
            bool jumped = false;
            for (int i = 0; i < 210; i++)
            {
                if (!jumped && battle.Context.Ranged.Threatens(battle.Right, out _, out float arrival) && arrival <= .5f)
                {
                    battle.Right.StartEvade(EvadeKind.HopOver, battle.Context.SimTime);
                    jumped = true;
                }
                battle.Director.SimulateStep();
                if (i % 50 == 0) yield return null;
            }
            Assert.IsTrue(jumped);
            Assert.AreEqual(battle.Right.Runtime.maxHp, battle.Right.Runtime.hp);
        }

        [UnityTest]
        public IEnumerator InterruptedGroundWave_DoesNotCreateAProjectile()
        {
            Build(true);
            battle.Left.StartAttack(battle.Context.SimTime, AttackStyle.GroundWave);
            for (int i = 0; i < 25; i++) battle.Director.SimulateStep();
            Assert.Greater(battle.Left.Body.position.y, .3f);
            battle.Left.EnterStagger(.5f);
            for (int i = 0; i < 90; i++) battle.Director.SimulateStep();
            Assert.AreEqual(0, battle.Context.Ranged.LaunchCount);
            yield break;
        }

        [UnityTest]
        public IEnumerator RangedCooldownAndWeightRequirements_PreventSpam()
        {
            Build(false, 11f);
            battle.Left.StartAttack(battle.Context.SimTime, AttackStyle.GroundWave);
            Assert.AreEqual(0, battle.Left.Runtime.attackSerial);
            battle.Left.StartAttack(battle.Context.SimTime, AttackStyle.StrokeShot);
            for (int i = 0; i < 60; i++) battle.Director.SimulateStep();
            int serial = battle.Left.Runtime.attackSerial;
            battle.Left.StartAttack(battle.Context.SimTime, AttackStyle.StrokeShot);
            Assert.AreEqual(serial, battle.Left.Runtime.attackSerial);
            for (int i = 0; i < 70; i++) battle.Director.SimulateStep();
            Assert.AreEqual(0, battle.Context.Ranged.Projectiles.Count);
            Assert.AreEqual(battle.Right.Runtime.maxHp, battle.Right.Runtime.hp);
            yield break;
        }

        [UnityTest]
        public IEnumerator FastProjectile_DoesNotTunnelThroughTarget()
        {
            Build(false);
            tuning.strokeShotSpeed = 500f;
            int hits = 0;
            battle.Context.Events.Hit += e => { if (e.style == AttackStyle.StrokeShot) hits++; };
            battle.Left.StartAttack(battle.Context.SimTime, AttackStyle.StrokeShot);
            for (int i = 0; i < 50; i++) battle.Director.SimulateStep();
            Assert.AreEqual(1, hits);
            yield break;
        }

        [UnityTest]
        public IEnumerator RealAI_UsesBothRangedAttacks()
        {
            Build(true, 6f, true);
            tuning.rangedChance = 1f;
            bool wave = false, stroke = false;
            battle.Context.Events.Motion += e =>
            {
                wave |= e.kind == FighterMotionKind.GroundWave;
                stroke |= e.kind == FighterMotionKind.StrokeShot;
            };
            for (int i = 0; i < 500 && !(wave && stroke); i++)
            {
                battle.Director.SimulateStep();
                if (i % 50 == 0) yield return null;
            }
            Assert.IsTrue(wave, "Large weapon AI should choose the ground wave at range.");
            Assert.IsTrue(stroke, "Light weapon AI should choose the flying stroke at range.");
        }

        [UnityTest]
        public IEnumerator RangedPresentation_RendersBothProjectiles_AndPlaysReleaseSounds()
        {
            foreach (bool wave in new[] { true, false })
            {
                Build(wave, presentation: true);
                for (int warmup = 0; warmup < 45; warmup++) { battle.Director.SimulateStep(); yield return null; }
                var sounds = new List<CombatSound>();
                battle.Root.GetComponent<CombatAudioDirector>().SoundPlayed += e => sounds.Add(e.sound);
                battle.Left.StartAttack(battle.Context.SimTime, wave ? AttackStyle.GroundWave : AttackStyle.StrokeShot);
                bool airCaptured = false, flightCaptured = false;
                for (int i = 0; i < 150 && !flightCaptured; i++)
                {
                    battle.Director.SimulateStep();
                    yield return null;
                    if (wave && !airCaptured && battle.Left.Body.position.y > .55f && battle.Left.Runtime.leapJumped)
                    {
                        MenuUiTests.Capture("ranged_wave_jump", 1230, 690);
                        airCaptured = true;
                    }
                    if (battle.Context.Ranged.Projectiles.Count > 0)
                    {
                        var p = battle.Context.Ranged.Projectiles[0];
                        if (p.Position.x > battle.Left.X + 2f && p.Position.x < battle.Right.X - .8f)
                        {
                            var view = battle.Root.GetComponent<RangedProjectileView>();
                            Assert.IsNotNull(view.transform.Find(wave ? "GroundShockwave" : "FlyingStroke"));
                            MenuUiTests.Capture(wave ? "ranged_wave_travel" : "ranged_stroke_travel", 1230, 690);
                            flightCaptured = true;
                        }
                    }
                }
                Assert.IsTrue(flightCaptured);
                Assert.IsTrue(!wave || airCaptured);
                Assert.IsTrue(sounds.Contains(wave ? CombatSound.Slam : CombatSound.CounterSwing));
                battle.Destroy(); battle = null;
                Object.Destroy(tuning); tuning = null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RangedAI_SameSeed_RepeatsLaunchesAndDamage()
        {
            var runs = new List<string>();
            for (int run = 0; run < 2; run++)
            {
                Build(true, 6f, true);
                tuning.rangedChance = .5f;
                string trace = "";
                battle.Context.Events.RangedLaunch += e => trace += $"{e.fighter}/{e.style}/{e.attackId}/{e.time:F2};";
                for (int i = 0; i < 600 && !battle.Director.Ended; i++)
                {
                    battle.Director.SimulateStep();
                    if (i % 50 == 0) yield return null;
                }
                Assert.Greater(battle.Context.Ranged.LaunchCount, 0);
                runs.Add(trace + $"hp={battle.Left.Runtime.hp:F3}/{battle.Right.Runtime.hp:F3}");
                battle.Destroy(); battle = null;
                Object.Destroy(tuning); tuning = null;
                yield return null;
            }
            Assert.AreEqual(runs[0], runs[1]);
        }
    }
}
