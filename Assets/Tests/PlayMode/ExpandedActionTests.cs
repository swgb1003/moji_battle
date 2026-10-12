using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    public class ExpandedActionTests
    {
        BattleInstance battle;
        CombatBalance balance;
        readonly List<FighterMotionKind> motions = new List<FighterMotionKind>();

        [TearDown]
        public void Cleanup()
        {
            battle?.Destroy();
            if (balance != null) Object.Destroy(balance);
            motions.Clear();
            SimHarness.End();
        }

        void Build(string left = "火", string right = "火")
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(739, presentation: true, left: left, right: right);
            balance = Object.Instantiate(battle.Context.Balance);
            battle.Context.Balance = balance;
            balance.expandedActions = true;
            balance.idleBreakSeconds = 1000f;
            balance.duckChance = 1f;
            balance.kickChance = 0f;
            balance.feintChance = 0f;
            foreach (var f in battle.Director.Fighters)
            {
                f.Runtime.attackReadyAt = 1000f;
                f.Runtime.evadeReadyAt = 1000f;
                f.WalkSpeed = 0f;
                f.SetTendency(new ClassTendency { reactionChance = 0f, attackWillingness = 0f,
                    guardBias = 0f, evadeBias = 0f, counterBias = 0f, proactiveGuard = 0f });
            }
            battle.Context.Events.Motion += e => motions.Add(e.kind);
        }

        static void Move(Fighter f, float x)
        {
            Vector2 delta = new Vector2(x, .02f) - f.Body.position;
            f.Body.position += delta;
            f.WeaponBody.position += delta;
            f.Body.transform.position += (Vector3)delta;
            f.WeaponBody.transform.position += (Vector3)delta;
            f.Body.linearVelocity = f.WeaponBody.linearVelocity = Vector2.zero;
        }

        void Capture(string name)
        {
            // Render the current physics pose even when a manual setup precedes the first fixed step.
            foreach (var fighter in battle.Director.Fighters)
            {
                fighter.CapturePose();
                fighter.CapturePose();
                fighter.View.SendMessage("LateUpdate");
            }
            var hidden = new List<Renderer>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (renderer.enabled && (!renderer.transform.IsChildOf(battle.Root.transform) || renderer.name.StartsWith("Dbg")))
                { hidden.Add(renderer); renderer.enabled = false; }
            foreach (var text in battle.Root.GetComponentsInChildren<TextMesh>())
                if (text.name == "HudText" && text.transform.parent == battle.Root.transform && text.anchor == TextAnchor.MiddleCenter)
                    text.gameObject.SetActive(false);
            var cam = battle.CameraRig.Cam;
            var target = new RenderTexture(1280, 720, 24);
            var old = cam.targetTexture;
            var active = RenderTexture.active;
            cam.targetTexture = target;
            cam.Render();
            RenderTexture.active = target;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            Directory.CreateDirectory(Path.Combine(SimHarness.ReportDir, "Screenshots"));
            File.WriteAllBytes(Path.Combine(SimHarness.ReportDir, "Screenshots", name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = old;
            RenderTexture.active = active;
            foreach (var renderer in hidden) if (renderer != null) renderer.enabled = true;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator Duck_LowersRealHitboxes_Counters_AndRestoresOnInterruption()
        {
            Build("一", "火");
            var a = battle.Left; var d = battle.Right;
            Move(a, -.7f); Move(d, .7f);
            yield return null;
            a.Runtime.evadeReadyAt = 0f;
            d.Runtime.attackStyle = AttackStyle.Sweep;
            float headY = a.BodyColliders[0].bounds.center.y;
            Assert.IsTrue(a.Actions.TryDuck(battle.Context.SimTime));
            yield return null;
            Assert.Less(a.BodyColliders[0].bounds.center.y, headY - .5f);
            Capture("action_duck");
            for (int i = 0; i < 40 && a.Runtime.state == FighterState.Evade; i++) yield return null;
            Assert.Contains(FighterMotionKind.DuckCounter, motions);
            Assert.AreEqual(Vector3.one, a.BodyColliders[0].transform.localScale);
            a.SetState(FighterState.Approach);
            Move(a, -.7f); Move(d, .7f);
            yield return null;
            a.Runtime.evadeReadyAt = a.Runtime.duckReadyAt = 0f;
            Assert.IsTrue(a.Actions.TryDuck(battle.Context.SimTime));
            Assert.IsTrue(a.Runtime.duckCounterPending);
            a.EnterStagger(.2f);
            Assert.AreEqual(Vector3.one, a.BodyColliders[0].transform.localScale);
            Assert.IsFalse(a.Runtime.duckCounterPending);
        }

        [UnityTest]
        public IEnumerator Duck_DoesNotAvoidLowAttacks_OrGuaranteeACounter()
        {
            Build("一", "火"); yield return null;
            var a = battle.Left;
            a.Runtime.evadeReadyAt = 0f;
            battle.Right.Runtime.attackStyle = AttackStyle.LowSweep;
            Assert.IsFalse(a.Actions.TryDuck(battle.Context.SimTime));
            a.StartEvade(EvadeKind.Duck, battle.Context.SimTime);
            a.Knockdown.EnterKnockdown(battle.Context.SimTime);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsFalse(motions.Contains(FighterMotionKind.DuckCounter));
        }

        [UnityTest]
        public IEnumerator Feint_ProvokesHighGuard_ThenChangesToThrust()
        {
            Build("一", "一");
            var a = battle.Left; var d = battle.Right;
            Move(a, -.7f); Move(d, .7f);
            d.SetTendency(new ClassTendency { reactionChance = 1f, guardBias = 1f });
            foreach (var c in a.WeaponColliders) c.enabled = false;
            foreach (var c in d.WeaponColliders) c.enabled = false;
            yield return null;
            a.Actions.StartFeint(battle.Context.SimTime);
            bool guard = false;
            for (int i = 0; i < 25; i++)
            {
                yield return null;
                if (a.Runtime.state == FighterState.Feint && d.Runtime.state == FighterState.Guard)
                { guard = true; Capture("action_feint"); }
                if (a.Runtime.state == FighterState.AttackWindup) break;
            }
            Assert.IsTrue(guard, "The fake overhead must be visible to the opponent's threat evaluation.");
            Assert.AreEqual(AttackStyle.Thrust, a.Runtime.attackStyle);
            Assert.Greater(a.Runtime.feintReadyAt, battle.Context.SimTime);
        }

        [UnityTest]
        public IEnumerator Feint_IsCancelledByDamage()
        {
            Build(); yield return null;
            var a = battle.Left;
            a.Actions.StartFeint(battle.Context.SimTime);
            a.EnterStagger(1f);
            for (int i = 0; i < 25; i++) yield return null;
            Assert.AreEqual(FighterState.Stagger, a.Runtime.state);
        }

        [UnityTest]
        public IEnumerator FrontKick_HitsOnce_Pushes_AndWeaponCannotAlsoHit()
        {
            Build("一", "火");
            var a = battle.Left; var d = battle.Right;
            Move(a, -.45f); Move(d, .45f);
            foreach (var c in d.WeaponColliders) c.enabled = false;
            yield return null;
            int hits = 0; float damage = 0f; float gap = a.DistanceToOpponent;
            battle.Context.Events.Hit += e => { if (e.style == AttackStyle.FrontKick) { hits++; damage += e.damage; } };
            a.StartAttack(battle.Context.SimTime, AttackStyle.FrontKick);
            bool captured = false;
            for (int i = 0; i < 25; i++)
            {
                yield return null;
                if (a.Runtime.state == FighterState.AttackActive)
                {
                    Assert.IsFalse(a.InAttackWindow);
                    if (!captured && a.Runtime.stateTime >= a.Runtime.activeDuration * .4f)
                    { Capture("action_front_kick"); captured = true; }
                }
            }
            Assert.AreEqual(1, hits);
            Assert.Greater(damage, 0f);
            Assert.Less(damage, d.Runtime.maxHp * .15f);
            Assert.Greater(a.DistanceToOpponent, gap);
        }

        [UnityTest]
        public IEnumerator Bind_ResolvesWithinHalfASecond_AndHasCooldown()
        {
            Build(); yield return null;
            var a = battle.Left; var d = battle.Right;
            Move(a, -.8f); Move(d, .8f);
            a.StartAttack(battle.Context.SimTime, AttackStyle.Overhead);
            d.StartAttack(battle.Context.SimTime, AttackStyle.Overhead);
            a.SetState(FighterState.AttackActive); d.SetState(FighterState.AttackActive);
            foreach (var fighter in battle.Director.Fighters)
            {
                fighter.WeaponBody.rotation = fighter.Body.rotation + (balance.guardPsi - fighter.Weapon.alpha0Deg) * fighter.Facing;
                fighter.WeaponBody.angularVelocity = 0f;
            }
            Assert.IsFalse(a.Actions.TryBind(d, a.GripWorld, 1f, 3f, battle.Context.SimTime));
            Assert.IsTrue(a.Actions.TryBind(d, a.GripWorld, 3f, 3f, battle.Context.SimTime));
            Assert.IsTrue(battle.Context.Hits.Ledger.IsResolved(a.Runtime.currentAttackId, d.Id));
            yield return null; Capture("action_bind");
            for (int i = 0; i < 35; i++) yield return null;
            Assert.AreNotEqual(FighterState.Bind, a.Runtime.state);
            Assert.AreNotEqual(FighterState.Bind, d.Runtime.state);
            Assert.Contains(FighterMotionKind.BindEscape, motions);
            a.SetState(FighterState.AttackActive); d.SetState(FighterState.AttackActive);
            Assert.IsFalse(a.Actions.TryBind(d, a.GripWorld, 3f, 3f, battle.Context.SimTime));
        }

        [UnityTest]
        public IEnumerator WallJump_CrossesOpponent_StaysInsideArena_AndLands()
        {
            Build("一", "一");
            var a = battle.Left; var d = battle.Right;
            Move(a, -battle.Context.ArenaHalfWidth + .8f); Move(d, a.X + 1.2f);
            yield return null;
            a.Runtime.evadeReadyAt = 0f;
            Assert.IsTrue(a.Actions.TryWallJump(battle.Context.SimTime));
            float apex = 0f; bool crossed = false;
            for (int i = 0; i < 100; i++)
            {
                yield return null;
                apex = Mathf.Max(apex, a.Body.position.y);
                if (a.X > d.X) crossed = true;
                Assert.Less(Mathf.Abs(a.X), battle.Context.ArenaHalfWidth);
                if (i == 12) Capture("action_wall_jump");
            }
            Assert.Greater(apex, .5f);
            Assert.IsTrue(crossed);
            Assert.AreNotEqual(FighterState.Evade, a.Runtime.state);
            Assert.IsTrue(a.IsGrounded);
        }

        [UnityTest]
        public IEnumerator PickupSlash_OnlyTriggersOnPhysicalPickupNearOpponent()
        {
            Build("一", "火");
            var a = battle.Left;
            Move(a, -.8f); Move(battle.Right, .8f);
            yield return null;
            a.Runtime.weaponDetached = true; a.Runtime.throwLive = false;
            a.Runtime.thrownAt = battle.Context.SimTime;
            a.Hinge.enabled = false;
            a.WeaponBody.rotation = 0f;
            a.WeaponBody.position = a.Body.position + new Vector2(0f, .4f) - a.WeaponBody.centerOfMass;
            for (int i = 0; i < 8; i++) yield return null;
            Assert.IsFalse(a.Runtime.weaponDetached);
            Assert.Contains(FighterMotionKind.PickupSlash, motions);
            Assert.AreEqual(AttackStyle.PickupSlash, a.Runtime.attackStyle);
            Capture("action_pickup_slash");
        }

        [UnityTest]
        public IEnumerator Recovery_SelectsKickNearby_AndRollFarAway()
        {
            Build("一", "一");
            var a = battle.Left;
            Move(a, -.45f); Move(battle.Right, .45f);
            yield return null;
            a.SetState(FighterState.Recover);
            a.Actions.FinishRecover(battle.Context.SimTime);
            Assert.AreEqual(AttackStyle.RecoverKick, a.Runtime.attackStyle);
            for (int i = 0; i < 9; i++) yield return null;
            Assert.Contains(FighterMotionKind.RecoverKick, motions);
            Capture("action_recover_kick");
            a.SetState(FighterState.Approach);
            Move(a, -3f); Move(battle.Right, 3f);
            yield return null;
            a.SetState(FighterState.Recover);
            a.Actions.FinishRecover(battle.Context.SimTime);
            Assert.AreEqual(EvadeKind.RecoverRoll, a.Runtime.evadeKind);
            Assert.AreEqual(FighterState.Evade, a.Runtime.state);
            yield return null; Capture("action_recover_roll");
        }

        [UnityTest]
        public IEnumerator Hook_UsesGlyphGeometry_PullsOnce_AndReversesAgainstHeavyWeapon()
        {
            Build("O", "一"); yield return null;
            var a = battle.Left; var d = battle.Right;
            Move(a, -.9f); Move(d, .9f);
            Assert.IsTrue(a.Actions.HasHook);
            Assert.IsFalse(d.Actions.HasHook, "A straight stroke must not gain a hook from its collider segmentation.");
            Vector2 point = a.WeaponBody.GetRelativePoint(new Vector2(a.Weapon.comLocal.x * a.Facing, a.Weapon.comLocal.y));
            Assert.IsTrue(a.Actions.IsHookContact(point));
            a.StartAttack(battle.Context.SimTime, AttackStyle.HookPull);
            a.SetState(FighterState.AttackActive);
            d.SetState(FighterState.Guard);
            Assert.IsTrue(a.Actions.TryHook(d, point, battle.Context.SimTime));
            Assert.AreEqual(FighterState.Stagger, d.Runtime.state);
            Assert.IsFalse(a.Actions.TryHook(d, point, battle.Context.SimTime));
            yield return null; Capture("action_hook_pull");
            a.SetState(FighterState.Approach); d.SetState(FighterState.Approach);
            a.StartAttack(battle.Context.SimTime, AttackStyle.HookPull);
            a.SetState(FighterState.AttackActive);
            d.WeaponBody.mass = a.WeaponBody.mass * 3f;
            point = a.WeaponBody.GetRelativePoint(new Vector2(a.Weapon.comLocal.x * a.Facing, a.Weapon.comLocal.y));
            Assert.IsTrue(a.Actions.TryHook(d, point, battle.Context.SimTime));
            Assert.AreEqual(FighterState.Stagger, a.Runtime.state);
            Assert.Contains(FighterMotionKind.HookReversed, motions);
        }

        [UnityTest]
        public IEnumerator FinishedMatch_DoesNotStartNewActions()
        {
            Build("O", "火"); yield return null;
            battle.Context.MatchOver = true;
            var a = battle.Left;
            a.Actions.StartFeint(battle.Context.SimTime);
            a.StartAttack(battle.Context.SimTime, AttackStyle.FrontKick);
            a.StartEvade(EvadeKind.Duck, battle.Context.SimTime);
            Assert.AreEqual(FighterState.Approach, a.Runtime.state);
            Assert.IsEmpty(motions);
        }

        [UnityTest]
        public IEnumerator Hook_ContactResolverUsesLedger_AndCannotDamageBody()
        {
            Build("O", "一"); yield return null;
            var a = battle.Left; var d = battle.Right;
            a.StartAttack(battle.Context.SimTime, AttackStyle.HookPull);
            a.SetState(FighterState.AttackActive);
            Vector2 point = a.WeaponBody.GetRelativePoint(new Vector2(a.Weapon.comLocal.x * a.Facing, a.Weapon.comLocal.y));
            float hp = d.Runtime.hp;
            battle.Context.Hits.Report(new HitContact { attacker = a, target = d, targetIsWeapon = false,
                point = d.ChestWorld, normal = Vector2.right, relativeVelocity = Vector2.right * 8f,
                myColliderId = a.WeaponColliders[0].GetInstanceID(), otherColliderId = d.BodyColliders[1].GetInstanceID() });
            battle.Context.Hits.ResolveStep(battle.Context.SimTime);
            Assert.AreEqual(hp, d.Runtime.hp);
            battle.Context.Hits.Report(new HitContact { attacker = a, target = d, targetIsWeapon = true,
                point = point, normal = Vector2.right, relativeVelocity = Vector2.zero,
                myColliderId = a.WeaponColliders[0].GetInstanceID(), otherColliderId = d.WeaponColliders[0].GetInstanceID() });
            battle.Context.Hits.ResolveStep(battle.Context.SimTime);
            Assert.IsTrue(battle.Context.Hits.Ledger.IsResolved(a.Runtime.currentAttackId, d.Id));
            Assert.Contains(FighterMotionKind.HookPull, motions);
            Assert.AreEqual(hp, d.Runtime.hp);
        }

        [UnityTest]
        public IEnumerator Hook_DetectsWeaponInsideCavity_WithoutAnInkCollision()
        {
            Build("O", "一"); yield return null;
            battle.Director.enabled = false;
            var a = battle.Left; var d = battle.Right;
            a.StartAttack(battle.Context.SimTime, AttackStyle.HookPull);
            a.SetState(FighterState.AttackActive);
            Vector2 cavity = a.WeaponBody.GetRelativePoint(new Vector2(a.Weapon.comLocal.x * a.Facing, a.Weapon.comLocal.y));
            d.WeaponBody.rotation = 0f;
            d.WeaponBody.position = cavity - d.WeaponBody.centerOfMass;
            Physics2D.SyncTransforms();
            a.Actions.Tick(battle.Context.SimTime, .02f);
            Assert.AreEqual(FighterState.Stagger, d.Runtime.state);
            Assert.Contains(FighterMotionKind.HookPull, motions);
            Assert.IsTrue(battle.Context.Hits.Ledger.IsResolved(a.Runtime.currentAttackId, d.Id));
        }

        [UnityTest]
        public IEnumerator RealAI_UsesExpandedActions_AndCompletesVariedMatches()
        {
            SimHarness.Begin(false);
            var counts = new Dictionary<FighterMotionKind, int>();
            var styles = new HashSet<AttackStyle>();
            string[] glyphs = { "O", "山", "火", "X", "口", "一", "鬱", "A" };
            for (int seed = 0; seed < 32; seed++)
            {
                bool symmetric = seed >= 16;
                battle = seed >= 24 ? SimHarness.Build(3700 + seed,
                    SimHarness.Custom(seed % 2 == 0 ? "O" : "口", WeaponSize.M, GripType.Horizontal, style: BattleStyle.Aggressive),
                    SimHarness.Custom("一", WeaponSize.M, style: BattleStyle.Defensive)) : SimHarness.Build(3700 + seed,
                    SimHarness.Custom(glyphs[seed % glyphs.Length], !symmetric && seed % 3 == 0 ? WeaponSize.S : WeaponSize.M,
                        style: seed % 2 == 0 ? BattleStyle.Aggressive : BattleStyle.Counter),
                    SimHarness.Custom(glyphs[(seed + (symmetric ? 0 : 1)) % glyphs.Length], !symmetric && seed % 2 == 0 ? WeaponSize.L : WeaponSize.M,
                        style: symmetric ? BattleStyle.Aggressive : seed % 2 == 0 ? BattleStyle.Defensive : BattleStyle.Counter));
                battle.Context.Events.Motion += e => { counts.TryGetValue(e.kind, out int count); counts[e.kind] = count + 1; };
                battle.Context.Events.StateChanged += e => { if (e.to == FighterState.AttackActive) styles.Add(battle.Director.Fighters[e.fighter].Runtime.attackStyle); };
                yield return SimHarness.RunToEnd(battle, 10f);
                Assert.IsTrue(battle.Director.Ended);
                Assert.AreEqual(0, battle.Director.Telemetry.duplicateBodyHits);
                Assert.Less(battle.Director.Telemetry.longestIdleSeconds, 10f);
                foreach (var fighter in battle.Director.Fighters)
                    Debug.Log($"[BIND] seed={3700 + seed} {fighter.Id} contacts={fighter.Actions.BindContacts} active={fighter.Actions.BindActiveContacts} ratio={fighter.Actions.ClosestBindRatio}");
                battle.Destroy(); battle = null;
                yield return null;
            }
            foreach (var entry in counts) Debug.Log($"[ACTIONS] {entry.Key}: {entry.Value}");
            Debug.Log("[ACTIONS] attacks: " + string.Join(",", styles));
            Assert.IsTrue(counts.ContainsKey(FighterMotionKind.Feint));
            Assert.IsTrue(counts.ContainsKey(FighterMotionKind.Duck));
            Assert.IsTrue(counts.ContainsKey(FighterMotionKind.FrontKick));
            Assert.IsTrue(counts.ContainsKey(FighterMotionKind.Bind));
            Assert.IsTrue(counts.ContainsKey(FighterMotionKind.HookPull));
        }
    }
}
