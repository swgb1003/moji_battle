using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>観戦者の関与（必殺技・声援）と、AI が設計した必殺技の連携。</summary>
    public class InterventionTests
    {
        BattleInstance battle;

        [TearDown]
        public void Cleanup()
        {
            battle?.Destroy();
            SimHarness.End();
        }

        BattleIntervention Build(WeaponSize size = WeaponSize.M, SpecialMoveData special = null, string character = "火")
        {
            SimHarness.Begin(false);
            var left = SimHarness.Custom(character, size);
            left.build.special = special;
            battle = SimHarness.Build(73, left, SimHarness.Custom("火", WeaponSize.M));
            foreach (var f in battle.Director.Fighters)
            {
                f.SetTendency(new ClassTendency { reactionChance = 0f, proactiveGuard = 0f, guardBias = 0f,
                    evadeBias = 0f, counterBias = 0f, retreatAfterAttack = 0f, attackWillingness = 0f });
                f.Runtime.attackReadyAt = 1000f;
                f.Runtime.evadeReadyAt = 1000f;
                f.Runtime.parryReadyAt = 1000f;
                f.WalkSpeed = 0f;
            }
            var iv = new BattleIntervention(battle.Director, InterventionController.Local, InterventionController.Local);
            battle.Context.Interventions = iv;
            return iv;
        }

        static void Move(Fighter f, Vector2 position)
        {
            var delta = position - f.Body.position;
            f.Body.position += delta;
            f.WeaponBody.position += delta;
            f.Body.transform.position += (Vector3)delta;
            f.WeaponBody.transform.position += (Vector3)delta;
            f.Body.linearVelocity = f.WeaponBody.linearVelocity = Vector2.zero;
        }

        void ReportHit(Fighter attacker, Fighter defender, bool weapon)
        {
            battle.Context.Hits.Report(new HitContact { attacker = attacker, target = defender, targetIsWeapon = weapon,
                part = BodyPart.Torso, point = defender.ChestWorld + Vector2.right * defender.Facing * .2f, normal = Vector2.right,
                relativeVelocity = Vector2.right * 10f, myColliderId = attacker.WeaponColliders[0].GetInstanceID(),
                otherColliderId = (weapon ? defender.WeaponColliders[0] : defender.BodyColliders[0]).GetInstanceID() });
        }

        IEnumerator Settle(Fighter a, Fighter d)
        {
            Move(d, new Vector2(a.X + d.AttackRange * .8f, .02f));
            for (int i = 0; i < 15; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Special_NeedsFullGauge_StartsGlyphTechnique_AndBreaksGuard()
        {
            var iv = Build();
            var a = battle.Left; var d = battle.Right;
            yield return Settle(a, d);
            int specials = 0;
            battle.Context.Events.Intervention += e => { if (e.kind == InterventionKind.Special && e.success) specials++; };
            iv.Side(a.Id).gauge = .5f;
            iv.Request(a.Id, InterventionKind.Special, false);
            Assert.IsFalse(iv.Side(a.Id).SpecialPending(battle.Context.SimTime));
            iv.Side(a.Id).gauge = 1f;
            iv.Request(a.Id, InterventionKind.Special, false);
            for (int i = 0; i < 5 && specials == 0; i++) yield return null;
            Assert.AreEqual(1, specials);
            Assert.AreEqual(AttackStyle.DashSlash, a.Runtime.attackStyle, "M サイズの字形は踏み込み斬り");
            Assert.IsTrue(iv.IsSpecial(a));
            Assert.AreEqual(0f, iv.Side(a.Id).gauge);
            Assert.AreEqual("火炎連斬", iv.Side(a.Id).plan.name);
            var tuning = battle.Context.Balance.intervention;
            Assert.AreEqual((tuning.customDamageBase + tuning.customDamagePerPower * 4) / Mathf.Sqrt(2), iv.DamageScale(a, battle.Context.SimTime), 1e-4f);

            bool broke = false;
            battle.Context.Events.Guard += e => { if (e.defender == d.Id) broke |= e.broke; };
            a.SetState(FighterState.AttackActive);
            d.SetState(FighterState.Guard);
            d.Runtime.guardLoad = 0f;
            ReportHit(a, d, true);
            yield return null;
            Assert.IsTrue(broke, "必殺はガードを崩す");
        }

        [UnityTest]
        public IEnumerator Special_LargeGlyph_AdaptsCharacterMove_AndAdvancesUntilInRange()
        {
            var iv = Build(WeaponSize.XL);
            var a = battle.Left; var d = battle.Right;
            a.WalkSpeed = 2f;
            Move(d, new Vector2(a.X + a.AttackRange * 2.5f, .02f));
            for (int i = 0; i < 15; i++) yield return null;
            iv.Side(a.Id).gauge = 1f;
            iv.Request(a.Id, InterventionKind.Special, false);
            float x = a.X;
            for (int i = 0; i < 150 && !iv.IsSpecial(a); i++) yield return null;
            Assert.IsTrue(iv.IsSpecial(a), "間合いを詰めてから必殺を出す");
            Assert.AreEqual("火炎連斬", iv.Side(a.Id).plan.name);
            Assert.AreEqual(BattleIntervention.Usable(a, AttackStyle.DashSlash), a.Runtime.attackStyle);
            Assert.Greater(a.X - x, .2f);
        }

        static SpecialMoveData Move(string name, int power, int speed, int reach, params string[] steps) => new SpecialMoveData
        {
            name = name, character = "火", steps = steps, power = power, speed = speed, reach = reach,
            element = "fire", effect = "pillar", primary = "#E2462B", secondary = "#F6B23C", callout = "燃えろ！",
        };

        [UnityTest]
        public IEnumerator CustomSpecial_ChainsEveryStep_WithItsOwnDamageAndSpeed()
        {
            var move = Move("火炎三連", 3, 5, 1, "Thrust", "Overhead", "Rising");
            var iv = Build(WeaponSize.M, move);
            var a = battle.Left; var d = battle.Right;
            yield return Settle(a, d);
            var s = iv.Side(a.Id);
            Assert.AreEqual("火炎三連", s.plan.name);
            var t = battle.Context.Balance.intervention;
            float plain = a.WindupTime * AttackTechniques.Tuning(AttackStyle.Thrust, battle.Context.Balance).windup;
            var started = new System.Collections.Generic.List<AttackStyle>();
            battle.Context.Events.StateChanged += e => { if (e.fighter == a.Id && e.to == FighterState.AttackWindup) started.Add(a.Runtime.attackStyle); };
            s.gauge = 1f;
            iv.Request(a.Id, InterventionKind.Special, false);
            for (int i = 0; i < 150 && !s.chainActive; i++) yield return null;
            Assert.IsTrue(s.chainActive,$"{s.plan.name}: state={a.Runtime.state} grounded={a.IsGrounded} detached={a.Runtime.weaponDetached}");
            Assert.AreEqual(plain * (t.customWindupBase - t.customWindupPerSpeed * 5), a.Runtime.windupDuration, 1e-3f, "速さで溜めが短くなる");
            Assert.AreEqual((t.customDamageBase + t.customDamagePerPower * 3) / Mathf.Sqrt(3), iv.DamageScale(a, battle.Context.SimTime), 1e-4f);
            for (int i = 0; i < 900 && s.chainActive; i++)
            {
                d.Runtime.hp = d.Runtime.maxHp; // 連携の途中で倒れないように
                // 歩けない設定なので、技の合間は相手を間合いに戻す（実戦では Brain が詰める）
                if (a.Runtime.state == FighterState.Approach && d.Runtime.state != FighterState.Knockdown) Move(d, new Vector2(a.X + a.AttackRange * .8f, .02f));
                yield return null;
            }
            Assert.IsFalse(s.chainActive, "技を出し切ると連携が終わる");
            CollectionAssert.AreEqual(new[] { AttackStyle.Thrust, AttackStyle.Overhead, AttackStyle.Rising }, started);
            Assert.AreEqual(3, s.specialSteps);
            a.StartAttack(battle.Context.SimTime, AttackStyle.Overhead);
            Assert.AreEqual(1f, iv.DamageScale(a, battle.Context.SimTime), "連携の後の攻撃は通常の威力");
        }

        [UnityTest]
        public IEnumerator CustomSpecial_RangedStep_FiresFromAfar_AndMissingWeaponMotionsAreReplaced()
        {
            // M サイズは衝撃波を出せない（大きい字形の技）→ 一画飛ばしに置き換え、離れたまま撃つ
            var iv = Build(WeaponSize.M, Move("火の波動", 4, 3, 2, "GroundWave"));
            var a = battle.Left; var d = battle.Right;
            Move(d, new Vector2(a.X + a.AttackRange * 2.2f, .02f));
            for (int i = 0; i < 15; i++) yield return null;
            Assert.AreEqual(AttackStyle.StrokeShot, BattleIntervention.Usable(a, AttackStyle.GroundWave));
            int launched = 0, ownedHits = 0;
            battle.Context.Events.RangedLaunch += e => { if (e.fighter == a.Id && iv.Side(a.Id).OwnsAttack(e.attackId)) launched++; };
            battle.Context.Events.Hit += e => { if (e.attacker == a.Id && iv.Side(a.Id).OwnsAttack(e.attackId)) ownedHits++; };
            float x = a.X;
            iv.Side(a.Id).gauge = 1f;
            iv.Request(a.Id, InterventionKind.Special, false);
            for (int i = 0; i < 120 && launched == 0; i++) yield return null;
            Assert.AreEqual(1, launched, "飛び道具の必殺は射程内なら詰めずに撃つ");
            Assert.Less(Mathf.Abs(a.X - x), .3f);
            Assert.AreEqual(AttackStyle.StrokeShot, a.Runtime.attackStyle);
        }

        [UnityTest]
        public IEnumerator CustomSpecial_DashesIn_HoldsTheOpponent_AndEveryStepLands()
        {
            var iv = Build(WeaponSize.M, Move("火柱", 3, 3, 3, "Thrust", "Overhead", "Thrust"));
            var a = battle.Left; var d = battle.Right;
            // 遠く離れて、しかも相手は構えている
            Move(d, new Vector2(a.X + a.AttackRange * 4f, .02f));
            for (int i = 0; i < 15; i++) yield return null;
            d.SetState(FighterState.Guard);
            var s = iv.Side(a.Id);
            var hits = new System.Collections.Generic.HashSet<int>();
            int dashEvents = 0;
            battle.Context.Events.Hit += e => { if (e.attacker == a.Id && s.OwnsAttack(e.attackId)) hits.Add(e.attackId); };
            battle.Context.Events.Intervention += e => { if (e.kind == InterventionKind.Special && e.dashed) dashEvents++; };
            s.gauge = 1f;
            iv.Request(a.Id, InterventionKind.Special, false);
            for (int i = 0; i < 5 && !s.chainActive; i++) yield return null;
            Assert.IsTrue(s.chainActive, "離れていても一瞬で詰めて出す");
            Assert.LessOrEqual(a.DistanceToOpponent, a.AttackRange, "瞬歩で間合いに入る");
            Assert.AreEqual(1, dashEvents);
            for (int i = 0; i < 900 && s.chainActive; i++)
            {
                d.Runtime.hp = d.Runtime.maxHp;
                var st = d.Runtime.state;
                if (s.chainActive && st != FighterState.Stagger && st != FighterState.Knockdown && d.IsGrounded && !d.Runtime.IsDown)
                    Assert.Fail($"技の間、相手は捕まって動けない（{st}）");
                // 二段目の前に相手を大きく離しても、また詰めて当てる
                if (s.specialSteps == 1 && a.Runtime.state == FighterState.Approach) Move(d, new Vector2(a.X + a.AttackRange * 3f, .02f));
                yield return null;
            }
            Assert.IsFalse(s.chainActive);
            Assert.AreEqual(3, s.specialSteps);
            Assert.AreEqual(3, hits.Count, "必殺技の各段は必ず当たる");
            Assert.GreaterOrEqual(s.dashes, 2);
        }

        [UnityTest]
        public IEnumerator Special_RangedStep_IsAlsoCertainToLand()
        {
            var iv = Build(WeaponSize.M, Move("火の波動", 3, 3, 3, "StrokeShot"));
            var a = battle.Left; var d = battle.Right;
            yield return Settle(a, d);
            var s = iv.Side(a.Id);
            int hits = 0;
            battle.Context.Events.Hit += e => { if (e.attacker == a.Id && s.OwnsAttack(e.attackId)) hits++; };
            float hp = d.Runtime.hp;
            s.gauge = 1f;
            iv.Request(a.Id, InterventionKind.Special, false);
            for (int i = 0; i < 600 && (hits == 0 || s.chainActive); i++) yield return null;
            Assert.AreEqual(1, hits);
            Assert.Less(d.Runtime.hp, hp);
        }

        /// <summary>必殺技の当たりの合計（与えたダメージ）を数えながら、相手が倒れないよう HP を戻して連携を最後まで見る。</summary>
        IEnumerator RunChain(BattleIntervention iv, Fighter a, Fighter d, System.Action<float> dealt)
        {
            var s = iv.Side(a.Id);
            battle.Context.Events.Hit += e => { if (e.attacker == a.Id && s.OwnsAttack(e.attackId)) dealt(e.damage); };
            s.gauge = 1f;
            iv.Request(a.Id, InterventionKind.Special, false);
            for (int i = 0; i < 150 && !s.chainActive; i++) yield return null;
            Assert.IsTrue(s.chainActive,$"{s.plan.name}: state={a.Runtime.state} grounded={a.IsGrounded} detached={a.Runtime.weaponDetached}");
            for (int i = 0; i < 1200 && s.chainActive; i++)
            {
                if (d.Runtime.hp < d.Runtime.maxHp * .5f) d.Runtime.hp = d.Runtime.maxHp * .5f + 1f;
                yield return null;
            }
            Assert.IsFalse(s.chainActive);
        }

        [UnityTest]
        public IEnumerator Special_TotalDamage_IsCapped_RegardlessOfSteps()
        {
            var iv = Build(WeaponSize.XL, Move("火火火火", 5, 2, 2, "Overhead", "Rising", "Overhead", "Rising"));
            var a = battle.Left; var d = battle.Right;
            yield return Settle(a, d);
            float total = 0f;
            yield return RunChain(iv, a, d, x => total += x);
            Assert.Greater(total, 0f);
            Assert.LessOrEqual(total, d.Runtime.maxHp * battle.Context.Balance.intervention.specialMaxShare + .01f, "1 回の必殺技の合計は上限まで");
        }

        [UnityTest]
        public IEnumerator Brace_DuringOpponentSpecial_RequiresAndConsumesCheerGauge_AndReducesDamage()
        {
            var iv = Build(WeaponSize.M, Move("火柱", 3, 3, 3, "Thrust"));
            var a = battle.Left; var d = battle.Right;
            yield return Settle(a, d);
            var ds = iv.Side(d.Id);
            ds.cheerReadyAt = battle.Context.SimTime + 100f; // 再使用待ち中
            iv.Request(d.Id, InterventionKind.Cheer, false);
            Assert.IsFalse(ds.braced, "相手の必殺技が来ていない時は押せない");
            int braces = 0;
            battle.Context.Events.Intervention += e => { if (e.kind == InterventionKind.Cheer && e.brace) braces++; };
            var s = iv.Side(a.Id);
            s.gauge = 1f;
            iv.Request(a.Id, InterventionKind.Special, false);
            for (int i = 0; i < 5 && !s.chainActive; i++) yield return null;
            iv.Request(d.Id, InterventionKind.Cheer, false);
            Assert.IsFalse(ds.braced, "必殺技を受けてもゲージ未回復なら踏ん張れない");
            Assert.AreEqual(0, braces);
            ds.cheerReadyAt = battle.Context.SimTime;
            iv.Request(d.Id, InterventionKind.Cheer, false);
            Assert.IsTrue(ds.braced);
            Assert.AreEqual(1, braces);
            Assert.AreEqual(battle.Context.SimTime + battle.Context.Balance.intervention.cheerCooldown, ds.cheerReadyAt, 1e-4f);
            Assert.AreEqual(1, ds.cheers);
            iv.Request(d.Id, InterventionKind.Cheer, false);
            Assert.AreEqual(1, braces, "同じ必殺に連打しても重複しない");
            float plain = 20f;
            Assert.AreEqual(plain * battle.Context.Balance.intervention.braceDamageScale,
                iv.ApplySpecialLimits(a, a.Runtime.currentAttackId, d, plain), 1e-3f);
            for (int i = 0; i < 600 && s.chainActive; i++) yield return null;
            Assert.IsFalse(ds.braced, "必殺技が終われば元に戻る");
        }

        [UnityTest]
        public IEnumerator Brace_IsAutomatic_WhenCheerIsAlreadyActive()
        {
            var iv = Build(WeaponSize.M, Move("火柱", 3, 3, 3, "Thrust"));
            var a = battle.Left; var d = battle.Right;
            yield return Settle(a, d);
            iv.Request(d.Id, InterventionKind.Cheer, false);
            Assert.IsFalse(iv.Side(d.Id).braced);
            var s = iv.Side(a.Id);
            s.gauge = 1f;
            iv.Request(a.Id, InterventionKind.Special, false);
            for (int i = 0; i < 5 && !s.chainActive; i++) yield return null;
            Assert.IsTrue(iv.Side(d.Id).braced, "声援の効果中に必殺技を受けたら自動で踏ん張る");
        }

        [UnityTest]
        public IEnumerator Cheer_ShortensWindup_ForAFewSeconds_WithCooldown()
        {
            var iv = Build();
            var a = battle.Left;
            yield return Settle(a, battle.Right);
            float t = battle.Context.SimTime;
            a.StartAttack(t, AttackStyle.Overhead);
            float plain = a.Runtime.windupDuration;
            a.SetState(FighterState.Approach);
            iv.Request(a.Id, InterventionKind.Cheer, false);
            var s = iv.Side(a.Id);
            Assert.Greater(s.cheerUntil, t);
            float until = s.cheerUntil;
            iv.Request(a.Id, InterventionKind.Cheer, false);
            Assert.AreEqual(until, s.cheerUntil, "再使用までは延長できない");
            a.StartAttack(battle.Context.SimTime, AttackStyle.Overhead);
            Assert.AreEqual(plain * battle.Context.Balance.intervention.cheerWindupScale, a.Runtime.windupDuration, 1e-4f);
            Assert.Greater(iv.ReactionBonus(a, battle.Context.SimTime), 0f);
            Assert.AreEqual(0f, iv.ReactionBonus(a, until + .01f));
        }

        [UnityTest]
        public IEnumerator ComSides_UseInterventions_InAFullMatch()
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(4242, left: "火", right: "山");
            var iv = new BattleIntervention(battle.Director, InterventionController.Com, InterventionController.Com);
            battle.Context.Interventions = iv;
            yield return SimHarness.RunToEnd(battle, 4f);
            Assert.IsTrue(battle.Director.Ended);
            int specials = iv.Side(0).specials + iv.Side(1).specials, cheers = iv.Side(0).cheers + iv.Side(1).cheers;
            Debug.Log($"[INTERVENE] specials={specials} cheers={cheers} result={battle.Director.Result.finishReason}");
            Assert.Greater(cheers, 0, "COM も声援を使う");
        }

        [UnityTest]
        public IEnumerator Com_CheersAtFullHealth_AndUsesItAgainAfterRefill()
        {
            var iv=Build();
            yield return Settle(battle.Left,battle.Right);
            battle.Director.enabled=false;
            var s=iv.Side(1); s.controller=InterventionController.Com;
            float start=battle.Context.SimTime;
            for (int n=0;n<1200 && s.cheers<2;n++)
            {
                battle.Context.SimTime=start+n*.02f;
                iv.Tick(battle.Context.SimTime,.02f);
            }
            Assert.AreEqual(battle.Right.Runtime.maxHp,battle.Right.Runtime.hp);
            Assert.AreEqual(2,s.cheers,"HPが減っていなくても、ゲージ回復後に再使用する");
            Assert.Greater(s.cheerReadyAt,battle.Context.SimTime);
        }

        [UnityTest]
        public IEnumerator Com_BraceConsumesGauge_AndCannotBraceAgainBeforeRefill()
        {
            var iv=Build(WeaponSize.M,Move("火柱",3,3,3,"Thrust"));
            yield return Settle(battle.Left,battle.Right);
            battle.Director.enabled=false;
            var tuning=battle.Context.Balance.intervention;
            float savedChance=tuning.comBraceChance;
            try
            {
                tuning.comBraceChance=1f;
                var s=iv.Side(1); s.controller=InterventionController.Com;
                iv.Side(0).gauge=1f;
                iv.Request(0,InterventionKind.Special,false);
                iv.Tick(battle.Context.SimTime,.02f);
                Assert.IsTrue(s.braced);
                Assert.AreEqual(1,s.cheers);
                Assert.IsFalse(s.CheerReady(battle.Context.SimTime));
                var frame=FriendActorFrame.Capture(battle.Right);
                Assert.AreEqual(tuning.cheerCooldown,frame.cheerWait,1e-4f,"踏ん張りのゲージ消費を友人対戦にも送る");
                Assert.IsTrue(frame.braced);
                s.braced=false; s.cheerUntil=-1f;
                iv.Tick(battle.Context.SimTime+.02f,.02f);
                Assert.IsFalse(s.braced,"効果が切れても未回復のゲージでは無料で踏ん張れない");
                Assert.AreEqual(1,s.cheers);
            }
            finally { tuning.comBraceChance=savedChance; }
        }

        [UnityTest]
        public IEnumerator AllThirtyBuiltinSpecials_CompleteTheirChainsInCombat()
        {
            for(int i=0;i<BuiltinSpecialMoves.Count;i++)
            {
                string character=BuiltinSpecialMoves.Representative(i);
                var iv=Build(character:character);
                var a=battle.Left; var d=battle.Right;
                yield return Settle(a,d);
                var s=iv.Side(0);
                Assert.AreEqual(BuiltinSpecialMoves.For(character).name,s.plan.name);
                float damage=0;
                yield return RunChain(iv,a,d,x=>damage+=x);
                Assert.AreEqual(s.plan.styles.Length,s.specialSteps,s.plan.name+" の全段が出る");
                Assert.Greater(damage,0,s.plan.name+" が実際にダメージを与える");
                Debug.Log($"[BUILTIN] {character} {s.plan.name} steps={s.specialSteps} damage={damage:F1}");
                battle.Destroy(); battle=null;
                yield return null;
                SimHarness.End();
            }
        }

        [UnityTest]
        public IEnumerator NoInterventions_LeavesAutoBattleUntouched()
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(4242, left: "火", right: "山");
            Assert.IsNull(battle.Context.Interventions);
            yield return SimHarness.RunToEnd(battle, 4f);
            Assert.IsTrue(battle.Director.Ended);
        }
    }
}
