using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    public sealed class CombatSpectacleTests
    {
        BattleInstance battle;
        CombatBalance balance;
        bool reduced;
        readonly List<FighterMotionKind> motions = new List<FighterMotionKind>();
        [SetUp] public void Setup() { reduced = GameSettings.ReducedEffects; SimHarness.Begin(false); }
        [TearDown] public void Cleanup()
        {
            battle?.Destroy(); if (balance != null) Object.Destroy(balance);
            GameSettings.Change(s => s.reducedEffects = reduced); SimHarness.End(); motions.Clear();
        }
        void Build(string left = "一", string right = "火")
        {
            balance = Object.Instantiate(CombatBalance.Default);
            balance.suddenDeath = false; balance.idleBreakSeconds = 1000;
            battle = BattleBuilder.Build(new MatchConfig { seed = 1973, durationSeconds = 15,
                left = new FighterLoadout(left, FontStyleId.Gothic), right = new FighterLoadout(right, FontStyleId.Gothic) },
                balance, GlyphCalibration.Default, true, 0, out var error);
            Assert.IsNotNull(battle, error);
            battle.Director.enabled = false;
            battle.Context.AiEnabled = true;
            foreach (var f in battle.Director.Fighters)
            {
                f.WalkSpeed = 0;
                f.Runtime.attackReadyAt = f.Runtime.evadeReadyAt = 1000;
                f.SetTendency(new ClassTendency { reactionChance = 0, attackWillingness = 0, guardBias = 0,
                    evadeBias = 0, proactiveGuard = 0 });
            }
            battle.Context.Events.Motion += e => motions.Add(e.kind);
        }
        static void Move(Fighter f, Vector2 position)
        {
            Vector2 delta = position - f.Body.position;
            f.Body.position += delta; f.WeaponBody.position += delta;
            f.Body.linearVelocity = f.WeaponBody.linearVelocity = Vector2.zero;
            f.CapturePose(); f.CapturePose();
        }
        void Capture(string name)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "../Reports/UI/CombatSpectacle"));
            foreach (var reveal in Object.FindObjectsByType<RetroReveal>(FindObjectsSortMode.None))
            { reveal.enabled = false; reveal.GetComponent<CanvasGroup>().alpha = 1; }
            var title = GameObject.Find("BattleAnnouncementTitle")?.GetComponent<UnityEngine.UI.Text>();
            if (title != null && title.text == "FIGHT!") GameObject.Find("BattleAnnouncement")?.SetActive(false);
            foreach (var f in battle.Director.Fighters) { f.CapturePose(); f.CapturePose(); f.View.SendMessage("LateUpdate"); }
            battle.CameraRig.SendMessage("LateUpdate");
            MenuUiTests.Capture("CombatSpectacle/" + name);
        }
        [UnityTest] public IEnumerator Pose_ReactsByBodyPart_AndKeepsGripAndPhysics()
        {
            Build(); yield return null;
            var f = battle.Left;
            var body = f.Body.position; var weapon = f.WeaponBody.position;
            var head = CombatPose.Sample(f, 1, BodyPart.Head, false);
            var torso = CombatPose.Sample(f, 1, BodyPart.Torso, false);
            var leg = CombatPose.Sample(f, 1, BodyPart.Leg, false);
            Assert.Less(head.head.x * f.Facing, 0);
            Assert.Less(torso.chest.y, 0); Assert.Greater(leg.knee, 0);
            foreach (var part in new[] { BodyPart.Head, BodyPart.Torso, BodyPart.Leg })
            {
                battle.Context.Events.Raise(new HitEvent { attacker = 1, defender = 0, damage = 22, part = part, time = battle.Context.SimTime });
                battle.Context.SimTime += .15f; f.View.SendMessage("LateUpdate");
                Assert.AreEqual(body, f.Body.position); Assert.AreEqual(weapon, f.WeaponBody.position);
                Assert.Less(Vector3.Distance(f.View.VolumeRig.MainHand, f.WeaponSprite.transform.position), .0001f);
                Capture("reaction_" + part);
            }
        }
        [UnityTest] public IEnumerator AerialCombo_HasSeparateHitLedger_ReachesSmash_AndCancelsOnHit()
        {
            Build(); yield return null;
            var a = battle.Left; var d = battle.Right;
            Move(a, new Vector2(-.55f, .02f)); Move(d, new Vector2(.55f, .75f));
            d.SetState(FighterState.Stagger); d.MarkLaunched(0);
            d.AddVelocity(Vector2.up * 3);
            // Refresh grounding from actual positions before issuing the follow-up.
            a.Tick(.2f, .02f); d.Tick(.2f, .02f);
            a.StartAttack(.2f, AttackStyle.Launch); a.SetState(FighterState.AttackRecovery);
            a.Runtime.launchConnectedAt = 0;
            int launchId = a.Runtime.currentAttackId;
            Assert.IsTrue(a.Actions.TryStartAerial(.2f));
            Assert.AreNotEqual(launchId, a.Runtime.currentAttackId);
            Assert.Greater(a.Body.linearVelocity.y, 5);
            Assert.Contains(FighterMotionKind.AerialChase, motions);
            int slashId = a.Runtime.currentAttackId;
            a.Tick(.3f, .02f);
            a.SetState(FighterState.AttackActive); a.Runtime.stateTime = .1f;
            a.Actions.Tick(.32f, .02f);
            float prior = d.Runtime.hp;
            battle.Context.Hits.ResolveStep(.32f);
            Assert.Less(d.Runtime.hp, prior);
            float after = d.Runtime.hp;
            a.Actions.Tick(.34f, .02f); battle.Context.Hits.ResolveStep(.34f);
            Assert.AreEqual(after, d.Runtime.hp, "空中斬りは一段一回だけ当たる");
            a.Actions.Tick(.54f, .02f);
            Assert.AreEqual(AttackStyle.Smash, a.Runtime.attackStyle);
            Assert.AreNotEqual(slashId, a.Runtime.currentAttackId);
            Assert.Contains(FighterMotionKind.AerialSmash, motions);
            Capture("aerial_smash");
            a.EnterStagger(.2f); Assert.AreEqual(0, a.Runtime.aerialStage);
            Assert.IsFalse(a.Actions.TryStartAerial(.6f));
        }
        [UnityTest] public IEnumerator AerialCombo_RejectsOutOfReach_GroundedAndDisabled()
        {
            Build(); yield return null;
            var a = battle.Left; var d = battle.Right;
            a.StartAttack(.2f, AttackStyle.Launch); a.Runtime.launchConnectedAt = 0;
            Assert.IsFalse(a.Actions.TryStartAerial(.2f));
            Move(d, new Vector2(7, 1)); d.Tick(.2f, .02f);
            Assert.IsFalse(a.Actions.TryStartAerial(.2f));
            balance.aerialCombos = false;
            Move(d, new Vector2(a.X + 1, 1)); d.Tick(.2f, .02f);
            Assert.IsFalse(a.Actions.TryStartAerial(.2f));
            balance.aerialCombos = true;
            Move(d, new Vector2(a.X + 1, 0)); d.Tick(.2f, .02f);
            Assert.IsFalse(a.Actions.TryStartAerial(.2f));
        }
        [UnityTest] public IEnumerator AerialCombo_HeavyKeepsGroundFinish_AndSpecialChainCannotBeHijacked()
        {
            Build("鬱", "一"); yield return null;
            var a = battle.Left; var d = battle.Right;
            Move(a, new Vector2(-.7f, .02f)); Move(d, new Vector2(.7f, .75f));
            d.AddVelocity(Vector2.up * 3); d.Tick(.2f, .02f);
            a.StartAttack(.2f, AttackStyle.Launch); a.Runtime.launchConnectedAt = 0;
            Assert.AreEqual(WeightClass.Heavy, a.WeightClass);
            Assert.IsFalse(a.Actions.TryStartAerial(.2f));
            // Switch sides: the light fighter is in a scripted special chain.
            d.StartAttack(.2f, AttackStyle.Launch); d.Runtime.launchConnectedAt = 0;
            Move(a, new Vector2(-.7f, .75f)); a.AddVelocity(Vector2.up * 3); a.Tick(.2f, .02f);
            battle.Context.Interventions = new BattleIntervention(battle.Director, InterventionController.Com, InterventionController.Com);
            battle.Context.Interventions.Side(1).chainActive = true;
            Assert.IsFalse(d.Actions.TryStartAerial(.2f));
        }
        [UnityTest] public IEnumerator ReducedEffects_KoNeverFreezes_AndSilentBattleDoesNotCreateInk()
        {
            Build(); yield return null;
            TimeController.PresentationEffectsEnabled = true;
            GameSettings.Change(s => s.reducedEffects = true);
            var vfx = battle.Root.GetComponent<CombatVfxDirector>();
            battle.Context.Events.Raise(new KoEvent { winner = 0 }); vfx.SendMessage("TickFinish", .2f);
            Assert.IsFalse(TimeController.FreezeActive);
            GameSettings.Change(s => s.reducedEffects = false);
            battle.Director.SilentSimulation = true;
            battle.Context.Events.Raise(new EnvImpactEvent { fighter = 1, smash = true });
            var ink = battle.Root.GetComponent<CombatInkEffects>(); ink.SendMessage("Update");
            Assert.AreEqual(0, ink.ScarCount); Assert.AreEqual(0, ink.ActiveStrokeCount);
        }
        [UnityTest] public IEnumerator Bind_ProducesSingleCounter_AndCannotImmediatelyRebind()
        {
            Build("火", "火"); yield return null;
            var a = battle.Left; var d = battle.Right;
            Move(a, new Vector2(-.65f, .02f)); Move(d, new Vector2(.65f, .02f));
            a.StartAttack(0, AttackStyle.Overhead); d.StartAttack(0, AttackStyle.Overhead);
            a.SetState(FighterState.AttackActive); d.SetState(FighterState.AttackActive);
            Assert.IsTrue(a.Actions.TryBind(d, a.GripWorld, 3, 3, 0));
            Capture("bind");
            a.Runtime.stateTime = balance.bindDuration;
            a.Actions.Tick(.5f, .02f); d.Actions.Tick(.5f, .02f);
            Assert.AreEqual(1, motions.FindAll(m => m == FighterMotionKind.BindCounter).Count);
            Assert.AreNotEqual(FighterState.Bind, a.Runtime.state); Assert.AreNotEqual(FighterState.Bind, d.Runtime.state);
            Assert.IsFalse(a.Actions.TryBind(d, a.GripWorld, 3, 3, .5f));
        }
        [UnityTest] public IEnumerator Ink_IsBounded_AndReducedEffectsClearsExistingMarks()
        {
            Build(); TimeController.PresentationEffectsEnabled = true; GameSettings.Change(s => s.reducedEffects = false);
            var ink = battle.Root.GetComponent<CombatInkEffects>();
            for (int i = 0; i < 100; i++) battle.Context.Events.Raise(new EnvImpactEvent { fighter = 1, point = new Vector2(i % 8 - 4, 0), smash = true });
            ink.SendMessage("Update");
            Assert.AreEqual(24, ink.ScarCount); Assert.LessOrEqual(ink.ActiveStrokeCount, 64);
            yield return null; Capture("ink_impact");
            GameSettings.Change(s => s.reducedEffects = true); ink.SendMessage("Update"); yield return null;
            Assert.AreEqual(0, ink.ScarCount); Assert.AreEqual(0, ink.ActiveStrokeCount);
            Assert.IsFalse(TimeController.FreezeActive);
        }
        [UnityTest] public IEnumerator Finish_ClassifiesActualLastHit_AndDelaysBurstDuringPause()
        {
            Build(); yield return null;
            TimeController.PresentationEffectsEnabled = true; GameSettings.Change(s => s.reducedEffects = false);
            var vfx = battle.Root.GetComponent<CombatVfxDirector>();
            battle.Right.Runtime.hp = 0;
            battle.Context.Events.Raise(new HitEvent { attacker = 0, defender = 1, style = AttackStyle.Bash });
            battle.Director.SimulateStep();
            Assert.AreEqual(FinishKind.Impact, vfx.LastFinishKind);
            Assert.IsTrue(battle.Director.KoInProgress);
            TimeController.SetPaused(true);
            for (int i = 0; i < 15; i++) yield return null;
            Assert.IsFalse(vfx.FinishBurstPlayed);
            TimeController.SetPaused(false);
            vfx.SendMessage("TickFinish", .08f);
            Assert.IsTrue(TimeController.FreezeActive);
            TimeController.Tick(.1f);
            vfx.SendMessage("TickFinish", .11f);
            Assert.IsTrue(vfx.FinishBurstPlayed);
            Capture("finish_impact");
            battle.Context.Events.Raise(new KoEvent { winner = 0, finish = FinishKind.Slash, direction = new Vector2(1, -.6f), point = battle.Right.ChestWorld });
            vfx.SendMessage("TickFinish", .08f); TimeController.Tick(.1f); vfx.SendMessage("TickFinish", .11f);
            Capture("finish_slash");
            battle.Context.Events.Raise(new KoEvent { winner = 0, finish = FinishKind.Ground, point = new Vector2(battle.Right.X, .1f) });
            vfx.SendMessage("TickFinish", .08f); TimeController.Tick(.1f); vfx.SendMessage("TickFinish", .11f);
            Capture("finish_ground");
            TimeController.ResetAll();
        }
        [UnityTest] public IEnumerator RemoteFrame_PreservesAttackTimingAndAerialPose()
        {
            Build(); yield return null;
            var f = battle.Left;
            f.Runtime.aerialStage = 2; f.Runtime.currentAttackId = 173;
            f.Runtime.activeDuration = .24f; f.Runtime.recoveryDuration = .18f;
            var frame = JsonUtility.FromJson<FriendActorFrame>(JsonUtility.ToJson(FriendActorFrame.Capture(f)));
            f.Runtime.aerialStage = 0; f.Runtime.currentAttackId = -1; f.Runtime.activeDuration = 0;
            frame.Apply(f, null, 1);
            Assert.AreEqual(2, f.Runtime.aerialStage); Assert.AreEqual(173, f.Runtime.currentAttackId);
            Assert.AreEqual(.24f, f.Runtime.activeDuration); Assert.AreEqual(.18f, f.Runtime.recoveryDuration);
        }
        [UnityTest] public IEnumerator AerialCombo_PhysicsRunsThroughLaunchSlashSmashAndLanding()
        {
            Build(); yield return null;
            var a = battle.Left; var d = battle.Right;
            Move(a, new Vector2(-.7f, .02f)); Move(d, new Vector2(.7f, .02f));
            foreach (var c in d.WeaponColliders) c.enabled = false;
            a.StartAttack(0, AttackStyle.Launch); a.SetState(FighterState.AttackActive);
            battle.Context.Hits.Report(new HitContact { attacker = a, target = d, part = BodyPart.Torso,
                point = d.ChestWorld, normal = Vector2.up, relativeVelocity = Vector2.up * 6,
                myColliderId = a.WeaponColliders[0].GetInstanceID(), otherColliderId = d.BodyColliders[1].GetInstanceID() });
            int slash = 0, smash = 0; bool landed = false; float apex = 0;
            string trace = "";
            battle.Context.Events.Hit += e => { if (e.attacker == 0 && e.style == AttackStyle.Rising) slash++; if (e.attacker == 0 && e.combo) smash++; };
            battle.Context.Events.EnvImpact += e => { if (e.fighter == 1 && e.smash) landed = true; };
            for (int i = 0; i < 120; i++)
            {
                battle.Director.SimulateStep(); apex = Mathf.Max(apex, a.Body.position.y);
                if (i % 5 == 0 && i < 65) trace += $"{i}:stage{a.Runtime.aerialStage}/{a.Runtime.state} y={a.Body.position.y:F2}/{d.Body.position.y:F2} psi={a.WeaponMotor.CurrentPsi:F0}/{a.Runtime.smashAimPsi:F0} contact={a.Runtime.attackHadContact}; ";
                if (i == 17) Capture("aerial_real");
                if (i % 4 == 0) yield return null;
            }
            Assert.Contains(FighterMotionKind.AerialChase, motions);
            Assert.Contains(FighterMotionKind.AerialSmash, motions);
            Debug.Log("[AERIAL] " + trace);
            Assert.Greater(apex, .5f); Assert.AreEqual(1, slash, "slash: " + trace); Assert.AreEqual(1, smash, "smash: " + trace);
            Assert.IsTrue(landed, "連携の締めで地面へ叩き落とす");
            Assert.AreEqual(0, a.Runtime.aerialStage);
        }
    }
}
