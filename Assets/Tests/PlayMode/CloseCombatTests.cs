using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    public class CloseCombatTests
    {
        BattleInstance battle;
        readonly ContactPoint2D[] contacts = new ContactPoint2D[64];

        [TearDown]
        public void Cleanup() { battle?.Destroy(); SimHarness.End(); }

        bool Touching(Fighter f)
        {
            int count = f.WeaponBody.GetContacts(contacts);
            for (int i = 0; i < count; i++)
            {
                var c = contacts[i];
                var other = c.collider.attachedRigidbody == f.WeaponBody ? c.otherCollider : c.collider;
                if (other != null && (other.attachedRigidbody == f.Opponent.Body || other.attachedRigidbody == f.Opponent.WeaponBody)) return true;
            }
            return false;
        }

        [UnityTest]
        public IEnumerator LargeComplexGlyphs_DoNotStayEntangledWithoutProgress()
        {
            SimHarness.Begin(false);
            var report = new StringBuilder("glyphs,size,seed,max_locked_seconds,hits,damage,max_grip_error,breaks\n");
            float worst = 0f, worstGripDuration = 0f;
            foreach (var pair in new[] { ("鬱", "鬱"), ("鬱", "龍"), ("龍", "龍") })
            foreach (var size in new[] { WeaponSize.L, WeaponSize.XL })
            {
                battle = SimHarness.Build(739, SimHarness.Custom(pair.Item1, size), SimHarness.Custom(pair.Item2, size), duration: 30f);
                battle.Director.enabled = false;
                var a = battle.Left; var b = battle.Right;
                float quiet = 0f, longest = 0f, maxGrip = 0f;
                string gripAt = "";
                var gripTimes = new float[2];
                float longestGripStretch = 0f;
                float ax = a.X, bx = b.X, ap = a.WeaponMotor.MeasurePsi(), bp = b.WeaponMotor.MeasurePsi();
                for (int step = 0; step < 1800 && !battle.Director.Ended; step++)
                {
                    battle.Director.SimulateStep();
                    bool stable = Mathf.Abs(a.X - ax) < .2f && Mathf.Abs(b.X - bx) < .2f
                        && Mathf.Abs(Mathf.DeltaAngle(ap, a.WeaponMotor.MeasurePsi())) < 20f
                        && Mathf.Abs(Mathf.DeltaAngle(bp, b.WeaponMotor.MeasurePsi())) < 20f;
                    bool locked = !a.Runtime.IsDown && !b.Runtime.IsDown && !a.Runtime.weaponDetached && !b.Runtime.weaponDetached
                        && battle.Context.SimTime - battle.Context.LastDamageTime > 1f && (Touching(a) || Touching(b));
                    if (stable && locked) quiet += TimeController.BaseFixedDelta;
                    else
                    {
                        quiet = 0f; ax = a.X; bx = b.X; ap = a.WeaponMotor.MeasurePsi(); bp = b.WeaponMotor.MeasurePsi();
                    }
                    longest = Mathf.Max(longest, quiet);
                    foreach (var f in battle.Director.Fighters)
                    {
                        if (!f.Runtime.weaponDetached && !f.Runtime.IsDown && f.Runtime.state != FighterState.Recover)
                        {
                            float grip = Vector2.Distance(f.GripWorld, f.Body.GetRelativePoint(f.HandLocal(f.Facing)));
                            gripTimes[f.Id] = grip > .55f ? gripTimes[f.Id] + TimeController.BaseFixedDelta : 0f;
                            longestGripStretch = Mathf.Max(longestGripStretch, gripTimes[f.Id]);
                            if (grip > maxGrip)
                            {
                                maxGrip = grip;
                                gripAt = $"side={f.Id} t={battle.Context.SimTime:F2} state={f.Runtime.state} style={f.Runtime.attackStyle} other={f.Opponent.Runtime.state} y={f.Body.position.y:F2}";
                            }
                        }
                        else gripTimes[f.Id] = 0f;
                    }
                    if (step % 50 == 0) yield return null;
                }
                var left = a.Runtime.metrics; var right = b.Runtime.metrics;
                float damage = a.Runtime.maxHp - a.Runtime.hp + b.Runtime.maxHp - b.Runtime.hp;
                report.AppendLine($"{pair.Item1}/{pair.Item2},{size},739,{longest:F2},{left.hitsLanded + right.hitsLanded},{damage:F2},{maxGrip:F3},{battle.Director.WeaponLockBreaks}");
                Debug.Log($"[CLOSE] {pair.Item1}/{pair.Item2} {size}: lock={longest:F2} hits={left.hitsLanded + right.hitsLanded} damage={damage:F2} grip={maxGrip:F3} gripDuration={longestGripStretch:F2} breaks={battle.Director.WeaponLockBreaks} gripAt=({gripAt})");
                worst = Mathf.Max(worst, longest);
                worstGripDuration = Mathf.Max(worstGripDuration, longestGripStretch);
                battle.Destroy(); battle = null;
                yield return null;
            }
            File.WriteAllText(Path.Combine(SimHarness.ReportDir, "close-combat.csv"), report.ToString());
            Assert.Less(worst, 1.5f, "Large glyphs should resume movement or a physical strike before a stationary contact lock becomes prolonged.");
            Assert.Less(worstGripDuration, .15f, "A held weapon must not remain stretched away from the hand after contact clearance.");
        }

        [UnityTest]
        public IEnumerator LargeWeaponsWaitingApart_DoNotTriggerASeparationOrDamage()
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(739, SimHarness.Custom("鬱", WeaponSize.XL), SimHarness.Custom("龍", WeaponSize.XL));
            battle.Director.enabled = false;
            foreach (var f in battle.Director.Fighters)
            {
                f.WalkSpeed = 0f;
                f.Runtime.attackReadyAt = f.Runtime.evadeReadyAt = 1000f;
                f.SetTendency(new ClassTendency { attackWillingness = 0f, reactionChance = 0f, proactiveGuard = 0f });
            }
            for (int i = 0; i < 250; i++) battle.Director.SimulateStep();
            Assert.AreEqual(0, battle.Director.WeaponLockBreaks);
            Assert.AreEqual(battle.Left.Runtime.maxHp, battle.Left.Runtime.hp);
            Assert.AreEqual(battle.Right.Runtime.maxHp, battle.Right.Runtime.hp);
            yield break;
        }
    }
}
