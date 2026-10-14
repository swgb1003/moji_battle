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

        /// <summary>
        /// 自操作（AI の絡みほどきが働かない）で前後に詰めたり下がったりしながら振っても、
        /// 武器が相手に引っ掛かって「下がれない・握りが引き伸ばされたまま」が長く続かない。武器の大きさ・カスタマイズに関係なく。
        /// </summary>
        [UnityTest]
        public IEnumerator PlayerControlledWeapon_DoesNotStayHookedOnOpponent()
        {
            SimHarness.Begin(false);
            float worst = 0f;
            string worstAt = "";
            int releases = 0;
            var cases = new[] { ("回", "龍", WeaponSize.M), ("鬱", "品", WeaponSize.M), ("龍", "回", WeaponSize.L), ("品", "鬱", WeaponSize.L), ("火", "山", WeaponSize.M), ("刀", "龍", WeaponSize.S) };
            for (int c = 0; c < cases.Length; c++)
            for (int seed = 0; seed < 3; seed++)
            {
                var (l, r, size) = cases[c];
                battle = SimHarness.Build(4100 + 37 * seed + c, SimHarness.Custom(l, size), SimHarness.Custom(r, size), 25f);
                battle.Director.enabled = false;
                battle.Context.PlayerSide = 0;
                var player = battle.Left.Brain.AttachPlayer();
                var rng = new System.Random(seed * 13 + c);
                float next = 0f, hooked = 0f;
                var f = battle.Left;
                for (int step = 0; step < 1500 && !battle.Director.Ended; step++)
                {
                    float t = battle.Context.SimTime;
                    if (t >= next)
                    {
                        next = t + .25f;
                        var input = player.Input;
                        input.Left = input.Right = false;
                        int toward = f.TowardOpponent;
                        double roll = rng.NextDouble();
                        // 詰める・下がる・その場で振るを混ぜる（下がる入力は引っ掛かりを引き伸ばす）
                        if (roll < .4) { if (toward > 0) input.Right = true; else input.Left = true; }
                        else if (roll < .7) { if (toward > 0) input.Left = true; else input.Right = true; }
                        if (rng.NextDouble() < .5) input.Press(rng.NextDouble() < .7 ? PlayerButton.Slash : PlayerButton.Tech, rng.Next(3) - 1, rng.Next(3) - 1);
                    }
                    battle.Director.SimulateStep();
                    var rt = f.Runtime;
                    float stretch = Vector2.Distance(f.GripWorld, f.Body.GetRelativePoint(f.HandLocal(f.Facing)));
                    bool stuckBack = f.Brain.Move == MoveIntent.Retreat && f.IsGrounded && f.BackSpace > .6f && Mathf.Abs(f.Body.linearVelocity.x) < .25f;
                    bool isHooked = !rt.IsDown && !rt.weaponDetached && rt.state != FighterState.Bind && Touching(f) && (stretch > .25f || stuckBack);
                    hooked = isHooked ? hooked + TimeController.BaseFixedDelta : 0f;
                    if (hooked > worst) { worst = hooked; worstAt = $"{l}/{r} {size} seed={seed} t={t:F2} state={rt.state} stretch={stretch:F2}"; }
                    if (step % 100 == 0) yield return null;
                }
                releases += battle.Director.HookReleases;
                battle.Destroy(); battle = null;
                yield return null;
            }
            Debug.Log($"[CLOSE] player hook worst={worst:F2} at ({worstAt}) releases={releases}");
            Assert.Less(worst, 1.2f, $"A player's weapon stayed hooked on the opponent: {worstAt}");
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
