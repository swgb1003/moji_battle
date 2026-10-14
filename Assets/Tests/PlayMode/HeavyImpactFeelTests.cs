using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>L・XL の重量級の手応え: 溜めは重く振りは速く、重い演出、吹き飛ばし、スーパーアーマー。</summary>
    public class HeavyImpactFeelTests
    {
        BattleInstance battle;
        readonly List<CombatSoundPlayback> played = new List<CombatSoundPlayback>();

        [TearDown] public void Cleanup() { battle?.Destroy(); battle = null; played.Clear(); SimHarness.End(); }

        void Build(WeaponSize left, WeaponSize right, bool presentation)
        {
            SimHarness.Begin(presentation);
            battle = SimHarness.Build(4242, SimHarness.Custom("火", left), SimHarness.Custom("火", right), 60f, presentation);
            battle.Director.enabled = false;
            battle.Context.AiEnabled = false;
            battle.Director.SimulateStep();
        }

        void Attack(Fighter a)
        {
            a.Runtime.attackStyle = AttackStyle.Overhead;
            a.Runtime.currentAttackId = a.Runtime.NextAttackId(a.Id);
            a.Runtime.clashedThisAttack = false;
            a.SetState(FighterState.AttackActive);
        }

        HitEvent BodyContact(Fighter a, Fighter d, float speed)
        {
            HitEvent hit = default; bool seen = false;
            System.Action<HitEvent> grab = e => { if (e.attacker == a.Id && !seen) { hit = e; seen = true; } };
            battle.Context.Events.Hit += grab;
            battle.Context.Hits.Report(new HitContact { attacker = a, target = d, targetIsWeapon = false,
                part = BodyPart.Torso, point = d.ChestWorld, normal = Vector2.right * a.Facing, relativeVelocity = Vector2.right * a.Facing * speed,
                myColliderId = a.WeaponColliders[0].GetInstanceID(), otherColliderId = d.BodyColliders[1].GetInstanceID() });
            battle.Context.Hits.ResolveStep(battle.Context.SimTime);
            battle.Context.Events.Hit -= grab;
            Assert.IsTrue(seen, "The contact should land as a body hit.");
            return hit;
        }

        [UnityTest] public IEnumerator HeavySizes_WindUpLonger_SwingFaster_AndRecoverSooner()
        {
            Build(WeaponSize.XL, WeaponSize.M, false);
            var xl = battle.Left; var cb = CustomizeBalance.Default;
            float baseWindup = StatCalculator.Windup(xl.Stats.weightScore, xl.Balance) / xl.Mods.attackSpeed;
            float baseActive = StatCalculator.Active(xl.Stats.weightScore, xl.Balance) / xl.Mods.attackSpeed;

            Assert.Greater(cb.sizeXL.windupTime, 1f); Assert.Greater(cb.sizeXL.swingSpeed, 1f); Assert.Less(cb.sizeXL.recoveryTime, 1f);
            Assert.AreEqual(baseWindup * cb.sizeXL.windupTime, xl.WindupTime, 1e-4f);
            Assert.AreEqual(baseActive / cb.sizeXL.swingSpeed, xl.ActiveTime, 1e-4f);
            // 硬直の短縮はテンポの上限の後に掛ける: 同じ振りを倍率 1 で始めた時と比べる
            xl.Runtime.attackReadyAt = -1f; xl.StartAttack(battle.Context.SimTime, AttackStyle.Overhead);
            float shortened = xl.Runtime.recoveryDuration;
            var mods = xl.Mods; mods.recoveryTime = 1f; xl.Mods = mods;
            xl.SetState(FighterState.Approach); xl.StartAttack(battle.Context.SimTime, AttackStyle.Overhead);
            Assert.AreEqual(xl.Runtime.recoveryDuration * cb.sizeXL.recoveryTime, shortened, 1e-4f);
            // M keeps its previous timing.
            var m = battle.Right;
            Assert.AreEqual(StatCalculator.Active(m.Stats.weightScore, m.Balance) / m.Mods.attackSpeed, m.ActiveTime, 1e-4f);
            yield break;
        }

        [UnityTest] public IEnumerator HeavySwing_HoldsThroughLighterHit_ButLighterWeaponHasNoArmor()
        {
            Build(WeaponSize.L, WeaponSize.M, false);
            var heavy = battle.Left; var light = battle.Right;
            Attack(heavy); Attack(light);
            var hit = BodyContact(light, heavy, 9f);
            Assert.IsTrue(hit.armored, "L should hold its swing against a lighter M weapon.");
            Assert.AreEqual(FighterState.AttackActive, heavy.Runtime.state);
            Assert.Greater(heavy.Runtime.armorPressure, 0f);

            Attack(light); Attack(heavy);
            hit = BodyContact(heavy, light, 9f);
            Assert.IsFalse(hit.armored);
            Assert.AreEqual(0f, light.Runtime.armorPressure);
            yield break;
        }

        [UnityTest] public IEnumerator HeavyHit_KnocksFartherThanSameImpulseFromMedium()
        {
            float Push(WeaponSize size)
            {
                Build(size, WeaponSize.M, false);
                var a = battle.Left; var d = battle.Right;
                Attack(a);
                d.Body.linearVelocity = Vector2.zero;
                var hit = BodyContact(a, d, 4f);
                float v = Mathf.Abs(d.Body.linearVelocity.x) / Mathf.Max(.01f, hit.impulse);
                battle.Destroy(); battle = null; SimHarness.End();
                return v;
            }
            float medium = Push(WeaponSize.M), xl = Push(WeaponSize.XL);
            Assert.Greater(xl, medium * 1.5f, "XL hits push harder per unit of impulse.");
            yield break;
        }

        [UnityTest] public IEnumerator HeavyHit_StopsLonger_SoundsLower_AndLeavesHeavyInk()
        {
            Build(WeaponSize.XL, WeaponSize.M, true);
            var audio = battle.Root.GetComponent<CombatAudioDirector>();
            if (Object.FindFirstObjectByType<AudioListener>() == null) battle.Root.AddComponent<AudioListener>();
            audio.SoundPlayed += e => played.Add(e);
            var b = battle.Context.Balance;
            yield return null;

            // A light M hit below the heavy-damage threshold has no hitstop.
            battle.Context.Events.Raise(new HitEvent { attacker = 1, defender = 0, damage = 5, part = BodyPart.Torso,
                quality = HitQuality.Normal, style = AttackStyle.Overhead, point = battle.Left.ChestWorld, time = battle.Context.SimTime });
            Assert.AreEqual(0f, TimeController.HitStopRemaining);
            Assert.IsTrue(played.Any(p => p.sound == CombatSound.Hit));

            played.Clear();
            int scars = battle.Root.GetComponent<CombatInkEffects>().ScarCount;
            battle.Context.Events.Raise(new HitEvent { attacker = 0, defender = 1, damage = 5, part = BodyPart.Torso,
                quality = HitQuality.Normal, style = AttackStyle.Overhead, point = battle.Right.ChestWorld, time = battle.Context.SimTime });
            Assert.AreEqual(b.heavyFeelHitStop, TimeController.HitStopRemaining, 1e-4f);
            var sound = played.Single(p => p.sound == CombatSound.HeavyHit);
            Assert.LessOrEqual(sound.pitch, .74f + 1e-4f);
            Assert.GreaterOrEqual(battle.Root.GetComponent<CombatInkEffects>().ScarCount, scars + 2, "Heavy hits mark the paper under the defender.");
            for (int i = 0; i < 3; i++) yield return null;
            Capture("xl_hit");
        }

        static void Capture(string name)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "../Reports/UI/HeavyImpact"));
            MenuUiTests.Capture("HeavyImpact/" + name);
        }
    }
}
