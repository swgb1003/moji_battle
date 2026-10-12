using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    public class GlyphBalanceTests
    {
        BattleInstance battle;
        CombatBalance tuning;
        sealed class Sample { public int winner, hits, starts, ranged; public float elapsed, quiet, damage; }

        [TearDown]
        public void Cleanup()
        {
            battle?.Destroy();
            if (tuning != null) UnityEngine.Object.Destroy(tuning);
            SimHarness.End();
        }

        void BuildControlled()
        {
            SimHarness.Begin(false);
            tuning = UnityEngine.Object.Instantiate(CombatBalance.Default);
            tuning.expandedActions = false;
            battle = BattleBuilder.Build(new MatchConfig { left = SimHarness.Custom("→"), right = SimHarness.Custom("鬱"), seed = 739, durationSeconds = 60 },
                tuning, GlyphCalibration.Default, false, 0, out var error);
            Assert.IsNotNull(battle, error);
            battle.Director.enabled = false;
            battle.Context.AiEnabled = false;
            battle.Director.SimulateStep();
        }

        void Attack(Fighter a, AttackStyle style = AttackStyle.Overhead)
        {
            a.Runtime.attackStyle = style;
            a.Runtime.currentAttackId = a.Runtime.NextAttackId(a.Id);
            a.Runtime.clashedThisAttack = false;
            a.SetState(FighterState.AttackActive);
        }

        void Contact(Fighter a, Fighter d, bool weapon, Vector2 point, float speed = 2f)
        {
            battle.Context.Hits.Report(new HitContact { attacker = a, target = d, targetIsWeapon = weapon,
                part = BodyPart.Torso, point = point, normal = Vector2.right, relativeVelocity = Vector2.right * speed,
                myColliderId = a.WeaponColliders[0].GetInstanceID(), otherColliderId = (weapon ? d.WeaponColliders[0] : d.BodyColliders[1]).GetInstanceID() });
            battle.Context.Hits.ResolveStep(battle.Context.SimTime);
        }

        [UnityTest]
        public IEnumerator FasterLightWeapon_WinsClashAgainstSlowerHeavyWeapon()
        {
            BuildControlled();
            var light = battle.Left; var heavy = battle.Right;
            Attack(light); Attack(heavy);
            light.PreWeaponVelocity = Vector2.right * 12f;
            heavy.PreWeaponVelocity = Vector2.left * 2f;
            Contact(heavy, light, true, light.ChestWorld);
            Assert.AreEqual(FighterState.Stagger, heavy.Runtime.state);
            Assert.AreEqual(FighterState.AttackActive, light.Runtime.state);
            yield break;
        }

        [UnityTest]
        public IEnumerator NearlyStationaryClash_DoesNotAutomaticallyFavorHeavyWeapon()
        {
            BuildControlled();
            Attack(battle.Left); Attack(battle.Right);
            Contact(battle.Right, battle.Left, true, battle.Left.ChestWorld);
            Assert.AreEqual(FighterState.AttackActive, battle.Left.Runtime.state);
            Assert.AreEqual(FighterState.AttackActive, battle.Right.Runtime.state);
            Assert.IsFalse(battle.Right.Runtime.weaponPassThrough);
            yield break;
        }

        [UnityTest]
        public IEnumerator TipThrust_PiercesHeavierGuard_Once_AndParryStillStopsIt()
        {
            BuildControlled();
            var a = battle.Left; var d = battle.Right;
            // Put the defender in front of the actual tip; this also makes the guard contact frontal.
            Vector2 point = a.WeaponBody.GetRelativePoint(new Vector2(a.Weapon.length, 0));
            Vector2 delta = point + Vector2.right * .1f - d.ChestWorld;
            d.Body.position += delta; d.WeaponBody.position += delta;
            d.SetState(FighterState.Guard);
            Attack(a, AttackStyle.Thrust);
            Assert.Greater(d.WeaponBody.mass, a.WeaponBody.mass * 2f);
            float hp = d.Runtime.hp;
            int pierces = 0;
            battle.Context.Events.Hit += e => { if (e.pierce) pierces++; };
            Contact(a, d, true, point);
            Assert.Less(d.Runtime.hp, hp);
            Assert.Greater(d.Runtime.hp, hp - d.Runtime.maxHp * .3f);
            float after = d.Runtime.hp;
            Contact(a, d, true, point);
            Assert.AreEqual(after, d.Runtime.hp);
            Assert.AreEqual(1, pierces);
            // Parry protection is independent of ordinary guard penetration.
            Attack(a, AttackStyle.Thrust);
            d.ArmParry(battle.Context.SimTime);
            Contact(a, d, true, point);
            Assert.AreEqual(after, d.Runtime.hp);
            Assert.AreEqual(1, pierces);
            yield break;
        }

        [UnityTest]
        public IEnumerator PrecisionBonus_AffectsOnlyTipContacts()
        {
            BuildControlled();
            var a = battle.Left; var d = battle.Right;
            d.SetState(FighterState.Approach);
            float bonus = tuning.precisionTipBonus;
            var tip = a.WeaponBody.GetRelativePoint(Vector2.right * a.Weapon.length);
            var shaft = a.WeaponBody.GetRelativePoint(Vector2.zero);
            float Hit(Vector2 point, float coefficient)
            {
                tuning.precisionTipBonus = coefficient;
                Attack(a, AttackStyle.Thrust);
                float hp = d.Runtime.hp;
                Contact(a, d, false, point);
                return hp - d.Runtime.hp;
            }
            float ordinaryTip = Hit(tip, 0);
            float preciseTip = Hit(tip, bonus);
            Assert.Greater(preciseTip, ordinaryTip * 1.1f);
            float ordinaryShaft = Hit(shaft, 0);
            float preciseShaft = Hit(shaft, bonus);
            Assert.AreEqual(ordinaryShaft, preciseShaft, .001f, "Shaft contacts must not receive a precision bonus.");
            yield break;
        }

        [UnityTest]
        public IEnumerator RepeatedLightHits_BreakArmor_WithoutCountingDuplicateContacts()
        {
            BuildControlled();
            var a = battle.Left; var d = battle.Right;
            Attack(d);
            // A glancing hit has low impulse, so only accumulated armor pressure can interrupt it.
            var point = a.WeaponBody.GetRelativePoint(Vector2.zero);
            bool broken = false;
            for (int i = 0; i < 8; i++)
            {
                Attack(a);
                Contact(a, d, false, point);
                float pressure = d.Runtime.armorPressure;
                float hp = d.Runtime.hp;
                Contact(a, d, false, point);
                Assert.AreEqual(pressure, d.Runtime.armorPressure);
                Assert.AreEqual(hp, d.Runtime.hp);
                if (i == 0) { Assert.AreEqual(FighterState.AttackActive, d.Runtime.state); Assert.Greater(pressure, 0); }
                if (d.Runtime.state == FighterState.Stagger) { broken = true; break; }
            }
            Assert.IsTrue(broken, "Light follow-up hits should interrupt heavy armor before a knockout.");
            Assert.Greater(d.Runtime.hp, 0);
            Assert.AreEqual(0, d.Runtime.armorPressure);
            yield break;
        }

        [UnityTest]
        public IEnumerator ArmorPressure_Decays_AndResetsAfterEvasion()
        {
            BuildControlled();
            var a = battle.Left; var d = battle.Right;
            Attack(d); Attack(a);
            Contact(a, d, false, a.WeaponBody.GetRelativePoint(Vector2.zero));
            Assert.Greater(d.Runtime.armorPressure, 0);
            d.Runtime.Decay(4f, tuning);
            Assert.AreEqual(0, d.Runtime.armorPressure);
            Attack(a);
            Contact(a, d, false, a.WeaponBody.GetRelativePoint(Vector2.zero));
            Assert.Greater(d.Runtime.armorPressure, 0);
            d.SetState(FighterState.Evade);
            Assert.AreEqual(0, d.Runtime.armorPressure);
            yield break;
        }

        IEnumerator Measure(FighterLoadout left, FighterLoadout right, int seed, Sample sample, string label, StringBuilder csv)
        {
            battle = SimHarness.Build(seed, left, right);
            battle.Director.enabled = false;
            for (int step = 0; step < 6000 && !battle.Director.Ended; step++)
            {
                battle.Director.SimulateStep();
                if (battle.Director.Fighters.All(f => (f.Runtime.state == FighterState.Approach || f.Runtime.state == FighterState.Guard || f.Runtime.state == FighterState.AttackRecovery)
                    && Mathf.Abs(f.Body.linearVelocity.x) < .5f)) sample.quiet += TimeController.BaseFixedDelta;
                if (step % 100 == 0) yield return null;
            }
            Assert.IsTrue(battle.Director.Ended, label);
            Assert.AreEqual(0, battle.Director.Telemetry.duplicateBodyHits, label);
            sample.winner = battle.Director.Result.winner;
            sample.elapsed = battle.Director.Clock;
            sample.hits = battle.Left.Runtime.metrics.hitsLanded + battle.Right.Runtime.metrics.hitsLanded;
            sample.damage = battle.Left.Runtime.metrics.damageDealt + battle.Right.Runtime.metrics.damageDealt;
            foreach (var t in battle.Director.Telemetry.fighters)
            {
                sample.starts += t.attackStarts;
                sample.ranged += t.styleStarts[(int)AttackStyle.StrokeShot] + t.styleStarts[(int)AttackStyle.GroundWave];
            }
            csv.AppendLine(FormattableString.Invariant($"{label},{seed},{left.grapheme},{right.grapheme},{sample.winner},{sample.elapsed:F2},{sample.hits},{sample.starts},{sample.ranged},{sample.quiet:F2},{sample.damage:F2},{battle.Left.Runtime.hp:F2},{battle.Right.Runtime.hp:F2}"));
            battle.Destroy(); battle = null;
            yield return null;
        }

        static StringBuilder Csv() => new StringBuilder("scenario,seed,left,right,winner,seconds,hits,attacks,ranged_attacks,quiet_seconds,damage,left_hp,right_hp\n");

        [UnityTest, Timeout(600000)]
        public IEnumerator ArrowAndDense_BothHaveWinningChances_AcrossIndependentMirroredSeeds()
        {
            SimHarness.Begin(false);
            int arrowWins = 0, denseWins = 0;
            var csv = Csv();
            foreach (int start in new[] { 1037, 7001 })
            for (int i = 0; i < 20; i++)
            for (int side = 0; side < 2; side++)
            {
                var sample = new Sample();
                yield return Measure(SimHarness.Custom(side == 0 ? "→" : "鬱"), SimHarness.Custom(side == 0 ? "鬱" : "→"), start + i * 37, sample, "M_Gothic_OneHanded_Aggressive", csv);
                if (sample.winner == side) arrowWins++;
                else if (sample.winner == 1-side) denseWins++;
            }
            File.WriteAllText(Path.Combine(SimHarness.ReportDir, "glyph-balance-mirrored.csv"), csv.ToString());
            Debug.Log($"[GLYPH BALANCE] arrow={arrowWins}/80 dense={denseWins}/80 draws={80-arrowWins-denseWins}");
            Assert.That(arrowWins, Is.InRange(20, 60), "Both the precise light weapon and broad heavy weapon need a practical winning chance.");
            Assert.AreEqual(80, arrowWins + denseWins);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator MatchupMatrix_AndCustomization_KeepBattlesActive()
        {
            SimHarness.Begin(false);
            var samples = new List<Sample>(); var csv = Csv();
            var chars = new[] { "一", "I", "→", "口", "山", "龍", "鬱" };
            for (int l = 0; l < chars.Length; l++)
            for (int r = l + 1; r < chars.Length; r++)
            foreach (int seed in new[] { 4201, 9871 })
            for (int side = 0; side < 2; side++)
            {
                var sample = new Sample();
                yield return Measure(SimHarness.Custom(chars[side == 0 ? l : r]), SimHarness.Custom(chars[side == 0 ? r : l]), seed, sample, "matrix_M", csv);
                samples.Add(sample);
            }
            foreach (WeaponSize size in Enum.GetValues(typeof(WeaponSize)))
            foreach (FontStyleId font in Enum.GetValues(typeof(FontStyleId)))
            for (int side = 0; side < 2; side++)
            {
                var grip = (GripType)(((int)font + (int)size) % 4);
                var style = (BattleStyle)(((int)font + (int)size) % 4);
                FighterLoadout Load(string ch) => new FighterBuildData { character = ch, fontType = font, weaponSize = size,
                    gripType = grip, gripPosition = .5f, battleStyle = style }.ToLoadout();
                var sample = new Sample();
                yield return Measure(Load(side == 0 ? "→" : "鬱"), Load(side == 0 ? "鬱" : "→"), 6151, sample, $"{size}_{font}_{grip}_{style}", csv);
                samples.Add(sample);
            }
            File.WriteAllText(Path.Combine(SimHarness.ReportDir, "glyph-balance-matrix.csv"), csv.ToString());
            float elapsed = samples.Sum(s => s.elapsed);
            Debug.Log(FormattableString.Invariant($"[GLYPH MATRIX] matches={samples.Count} draws={samples.Count(s => s.winner < 0)} hits/min={60*samples.Sum(s => s.hits)/elapsed:F2} quiet={samples.Sum(s => s.quiet)/elapsed:F3} ranged={(float)samples.Sum(s => s.ranged)/samples.Sum(s => s.starts):F3}"));
            Assert.IsTrue(samples.All(s => s.hits > 0 && s.damage > 0));
            Assert.LessOrEqual(samples.Count(s => s.winner < 0), 3);
            Assert.Greater(60*samples.Sum(s => s.hits)/elapsed, 10f);
            Assert.Less(samples.Sum(s => s.quiet)/elapsed, .18f);
        }
    }
}
