using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MojiBattle.Tests
{
    public class CombatRulesTests
    {
        static CombatBalance B => CombatBalance.Default;

        /// <summary>投げの初速: 指定の速さで、放物線が狙った点を通る（左右どちら向きでも）。</summary>
        [TestCase(4f, 0f, 12f)]
        [TestCase(-6f, -0.3f, 9f)]
        [TestCase(2.5f, 0.5f, 8f)]
        public void BallisticVelocity_PassesThroughTarget(float dx, float dy, float speed)
        {
            const float g = 9.81f;
            var v = Fighter.BallisticVelocity(Vector2.zero, new Vector2(dx, dy), speed, g);
            Assert.AreEqual(speed, v.magnitude, 1e-3f);
            Assert.AreEqual(Mathf.Sign(dx), Mathf.Sign(v.x));
            float t = dx / v.x;
            float y = v.y * t - 0.5f * g * t * t;
            Assert.AreEqual(dy, y, 0.02f);
        }

        [Test]
        public void PartMultipliersDiffer()
        {
            float head = DamageMath.BodyDamage(60, 40, 8f, HitQuality.Normal, BodyPart.Head, B);
            float torso = DamageMath.BodyDamage(60, 40, 8f, HitQuality.Normal, BodyPart.Torso, B);
            float arm = DamageMath.BodyDamage(60, 40, 8f, HitQuality.Normal, BodyPart.Arm, B);
            float leg = DamageMath.BodyDamage(60, 40, 8f, HitQuality.Normal, BodyPart.Leg, B);
            Assert.Greater(head, torso);
            Assert.Greater(torso, leg);
            Assert.Greater(leg, arm);
            Assert.AreEqual(1.5f, head / torso, 1e-4f);
        }

        [Test]
        public void DamageFormulaMatchesSpec()
        {
            // raw = 攻撃 × speedFactor × hitQuality × 部位倍率 × damageScale ; final = max(1, raw × 100/(100+K×防御))
            // damageScale は仕様初期値 0.22、KO を増やす調整後は CombatBalance.damageScale。K も同様に CombatBalance.defenseK
            float raw = 100f * (9f / 6f) * 1.3f * 1.0f * B.damageScale;
            float expected = raw * 100f / (100f + B.defenseK * 50f);
            Assert.AreEqual(expected, DamageMath.BodyDamage(100, 50, 9f, HitQuality.Center, BodyPart.Torso, B), 1e-3f);
            Assert.AreEqual(0.4f, DamageMath.SpeedFactor(0.5f, B), 1e-5f);
            Assert.AreEqual(1.8f, DamageMath.SpeedFactor(50f, B), 1e-5f);
            Assert.AreEqual(1f, DamageMath.BodyDamage(5, 100, 0.1f, HitQuality.Graze, BodyPart.Arm, B), 1e-5f, "最低1");
        }

        [Test]
        public void ImpulseAndEnvironmentFormulas()
        {
            Assert.AreEqual(10f * 2f * 0.7f / (2.5f + 0.5f), DamageMath.Impulse(10f, 2f, 2.5f, B), 1e-4f);
            Assert.AreEqual(B.impulseMax, DamageMath.Impulse(100f, 5f, 2f, B), 1e-4f);
            Assert.AreEqual(0f, DamageMath.EnvironmentDamage(6.9f, B));
            Assert.AreEqual((10f - 7f) * 1.2f, DamageMath.EnvironmentDamage(10f, B), 1e-4f);
            Assert.AreEqual(18f, DamageMath.EnvironmentDamage(40f, B));
        }

        [Test]
        public void EnvironmentDamageOnlyAfterLaunch()
        {
            Assert.IsFalse(DamageMath.EnvironmentDamageAllowed(12f, 10f, -999f, false, B), "吹っ飛びなし（歩行・起き上がり）");
            Assert.IsTrue(DamageMath.EnvironmentDamageAllowed(12f, 10f, 9.0f, false, B));
            Assert.IsFalse(DamageMath.EnvironmentDamageAllowed(12f, 10f, 8.4f, false, B), "1.5秒超過");
            Assert.IsFalse(DamageMath.EnvironmentDamageAllowed(6.5f, 10f, 9.5f, false, B), "速度不足");
            Assert.IsFalse(DamageMath.EnvironmentDamageAllowed(12f, 10f, 9.5f, true, B), "同一吹っ飛びで同種2回目");
        }

        [Test]
        public void SlamDamageOnlyAfterLift()
        {
            Assert.AreEqual(0f, DamageMath.SlamDamage(B.slamMinSpeed - 0.1f, B));
            Assert.AreEqual((9f - B.slamMinSpeed) * B.slamK, DamageMath.SlamDamage(9f, B), 1e-4f);
            Assert.AreEqual(B.slamMax, DamageMath.SlamDamage(100f, B));
            Assert.IsTrue(DamageMath.SlamAllowed(8f, 10f, 9.5f, true, false, B));
            Assert.IsFalse(DamageMath.SlamAllowed(8f, 10f, 9.5f, false, false, B), "持ち上げられていない（普通の着地）");
            Assert.IsFalse(DamageMath.SlamAllowed(8f, 10f, 9.5f, true, true, B), "同じ持ち上げで2回目");
            Assert.IsFalse(DamageMath.SlamAllowed(8f, 10f, 8.0f, true, false, B), "離れてから猶予を過ぎた");
            Assert.IsFalse(DamageMath.SlamAllowed(B.slamMinSpeed - 0.5f, 10f, 9.5f, true, false, B), "速度不足");
        }

        [Test]
        public void MatchOutcomeRules()
        {
            Assert.AreEqual(MatchRules.Draw, MatchRules.DecideKo(0f, -3f), "同一ステップで両者0以下は引き分け");
            Assert.AreEqual(1, MatchRules.DecideKo(0f, 10f));
            Assert.AreEqual(0, MatchRules.DecideKo(5f, -1f));
            Assert.AreEqual(MatchRules.NoOutcome, MatchRules.DecideKo(1f, 1f));
            Assert.AreEqual(0, MatchRules.DecideTimeUp(50f, 100f, 80f, 200f), "残HP割合で判定");
            Assert.AreEqual(MatchRules.Draw, MatchRules.DecideTimeUp(50f, 100f, 100f, 200f));
        }

        [Test]
        public void SameSwingMultipleColliderContacts_SingleBodyOutcome()
        {
            var list = new List<ContactCandidate>
            {
                new ContactCandidate { isWeapon = false, vN = 6f, damage = 9f },
                new ContactCandidate { isWeapon = false, vN = 8f, damage = 14f },
                new ContactCandidate { isWeapon = false, vN = 7f, damage = 11f },
            };
            var kind = HitResolver.Choose(list, false, false, B.minHitRelativeSpeed, out int idx);
            Assert.AreEqual(HitOutcomeKind.Body, kind);
            Assert.AreEqual(1, idx, "最も有効な接触を一つだけ選ぶ");

            var ledger = new AttackLedger();
            ledger.MarkResolved(100001, 1);
            Assert.AreEqual(HitOutcomeKind.None, HitResolver.Choose(list, ledger.IsResolved(100001, 1), false, B.minHitRelativeSpeed, out _),
                "同一スイングの2回目以降は本体ダメージなし");
            Assert.IsFalse(ledger.IsResolved(100002, 1));
        }

        [Test]
        public void ValidWeaponGuardBeatsBodyHitInSameStep()
        {
            var list = new List<ContactCandidate>
            {
                new ContactCandidate { isWeapon = false, vN = 9f, damage = 20f },
                new ContactCandidate { isWeapon = true, vN = 8f, frontal = true },
            };
            Assert.AreEqual(HitOutcomeKind.Guard, HitResolver.Choose(list, false, true, B.minHitRelativeSpeed, out int idx));
            Assert.AreEqual(1, idx);
            // ガード状態でなければ武器接触は弾き扱いで、本体ヒットが優先
            Assert.AreEqual(HitOutcomeKind.Body, HitResolver.Choose(list, false, false, B.minHitRelativeSpeed, out _));
            // ガード角外の武器接触はガードにならない
            list[1] = new ContactCandidate { isWeapon = true, vN = 8f, frontal = false };
            Assert.AreEqual(HitOutcomeKind.Body, HitResolver.Choose(list, false, true, B.minHitRelativeSpeed, out _));
        }

        [Test]
        public void SlowTouchDealsNoDamage()
        {
            var list = new List<ContactCandidate> { new ContactCandidate { isWeapon = false, vN = 0.5f, damage = 5f } };
            Assert.AreEqual(HitOutcomeKind.None, HitResolver.Choose(list, false, false, B.minHitRelativeSpeed, out _));
        }

        [Test]
        public void MatchRandomIsDeterministic()
        {
            var a = new MatchRandom(1234);
            var b = new MatchRandom(1234);
            var c = new MatchRandom(1235);
            bool differs = false;
            for (int i = 0; i < 100; i++)
            {
                float x = a.Value();
                Assert.AreEqual(x, b.Value());
                if (x != c.Value()) differs = true;
                Assert.That(x, Is.InRange(0f, 1f));
            }
            Assert.IsTrue(differs);
        }
    }
}
