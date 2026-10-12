using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    public class NewTechniqueTests
    {
        BattleInstance battle;
        readonly List<CombatSound> sounds = new List<CombatSound>();
        RenderTexture target;

        [TearDown]
        public void Cleanup()
        {
            battle?.Destroy();
            if (target != null) { target.Release(); Object.Destroy(target); }
            sounds.Clear();
            SimHarness.End();
        }

        void Build(WeaponSize size = WeaponSize.M)
        {
            SimHarness.Begin(true);
            battle = SimHarness.Build(73, SimHarness.Custom("火", size), SimHarness.Custom("火", WeaponSize.M), presentation: true);
            battle.Root.GetComponent<CombatAudioDirector>().SoundPlayed += e => sounds.Add(e.sound);
            foreach (var f in battle.Director.Fighters)
            {
                f.SetTendency(new ClassTendency { reactionChance = 0f, proactiveGuard = 0f, guardBias = 0f,
                    evadeBias = 0f, counterBias = 0f, retreatAfterAttack = 0f, attackWillingness = 0f });
                f.Runtime.attackReadyAt = 1000f;
                f.Runtime.evadeReadyAt = 1000f;
                f.WalkSpeed = 0f;
            }
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

        UnityEngine.UI.Text Label(Fighter f) => battle.Root.GetComponentsInChildren<UnityEngine.UI.Text>(true)
            .First(t => t.name == "TechniqueName");

        void Capture(string name)
        {
            // Keep the technique review images focused on the fighters.
            foreach (var text in battle.Root.GetComponentsInChildren<TextMesh>())
                if (text.name == "HudText" && text.transform.parent == battle.Root.transform && text.anchor == TextAnchor.MiddleCenter)
                    text.gameObject.SetActive(false);
            var cam = battle.CameraRig.Cam;
            if (target != null) { target.Release(); Object.Destroy(target); }
            target = new RenderTexture(1280, 720, 24);
            var old = cam.targetTexture;
            cam.targetTexture = target;
            cam.Render();
            var oldActive = RenderTexture.active;
            RenderTexture.active = target;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = oldActive;
            cam.targetTexture = old;
            string dir = Path.Combine(SimHarness.ReportDir, "Screenshots");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator DashSlash_ClosesGap_HitsOnce_WithCalloutAndSounds()
        {
            Build();
            var a = battle.Left;
            var d = battle.Right;
            Move(d, new Vector2(a.X + a.AttackRange + .75f, .02f));
            foreach (var c in d.WeaponColliders) c.enabled = false;
            yield return null;
            float x = a.X;
            int hits = 0, starts = 0;
            battle.Context.Events.Hit += e => { if (e.attacker == a.Id && e.style == AttackStyle.DashSlash) hits++; };
            battle.Context.Events.Motion += e => { if (e.fighter == a.Id && e.kind == FighterMotionKind.Dash) starts++; };
            a.StartAttack(battle.Context.SimTime, AttackStyle.DashSlash);
            bool captured = false;
            for (int i = 0; i < 350; i++)
            {
                yield return null;
                if (!captured && a.Runtime.state == FighterState.AttackActive)
                { Capture("technique_dash_slash"); captured = true; Assert.AreEqual("踏込斬り", Label(a).text); }
                if (captured && a.Runtime.state == FighterState.Approach) break;
            }
            Assert.AreEqual(1, starts);
            Assert.AreEqual(1, hits, "踏み込んだ振りが一度だけ相手に届く");
            Assert.Greater(a.X - x, .35f);
            Assert.IsTrue(sounds.Contains(CombatSound.DashStep));
            Assert.IsTrue(sounds.Contains(CombatSound.DashSwing));
            Assert.IsTrue(sounds.Contains(CombatSound.DashHit));
            Assert.AreEqual(FighterFactory.TeamColor(a.Id).r, Label(a).color.r);
            Debug.Log($"[TECH] Dash: travel={a.X-x:F2} hits={hits}");
        }

        [UnityTest]
        public IEnumerator Whirl_CompletesRevolution_HitsOnce_AndHasRecovery()
        {
            Build(WeaponSize.XL);
            var a = battle.Left;
            var d = battle.Right;
            Move(d, new Vector2(a.X + a.AttackRange * .85f, .02f));
            foreach (var c in d.WeaponColliders) c.enabled = false;
            yield return null;
            int hits = 0, starts = 0;
            float yaw = 0f, recovery = 0f;
            battle.Context.Events.Hit += e => { if (e.attacker == a.Id && e.style == AttackStyle.Whirl) hits++; };
            battle.Context.Events.Motion += e => { if (e.fighter == a.Id && e.kind == FighterMotionKind.Whirl) starts++; };
            a.StartAttack(battle.Context.SimTime, AttackStyle.Whirl);
            bool captured = false;
            for (int i = 0; i < 600; i++)
            {
                yield return null;
                if (a.Runtime.state == FighterState.AttackActive) yaw = Mathf.Max(yaw, a.Runtime.sweepYaw);
                if (!captured && a.Runtime.state == FighterState.AttackActive && a.Runtime.sweepYaw > 0f)
                { Capture("technique_whirl"); captured = true; Assert.AreEqual("ぶん回し", Label(a).text); }
                if (a.Runtime.state == FighterState.AttackRecovery) recovery = Mathf.Max(recovery, a.Runtime.stateTime);
                if (captured && a.Runtime.state == FighterState.Approach) break;
            }
            Assert.AreEqual(1, starts);
            Assert.AreEqual(1, hits);
            Assert.Greater(yaw, 340f);
            Assert.Greater(recovery, .8f, "全身回転後には隙がある");
            Assert.AreEqual(FighterState.Approach, a.Runtime.state);
            Assert.IsTrue(sounds.Contains(CombatSound.WhirlSwing));
            Assert.IsTrue(sounds.Contains(CombatSound.WhirlHit));
            Debug.Log($"[TECH] Whirl: yaw={yaw:F1} recovery={recovery:F2} hits={hits}");
        }

        void ReportWeaponHit(Fighter attacker, Fighter defender)
        {
            battle.Context.Hits.Report(new HitContact { attacker = attacker, target = defender, targetIsWeapon = true,
                point = defender.ChestWorld + Vector2.right * defender.Facing * .2f, normal = Vector2.right,
                relativeVelocity = Vector2.right * 10f, myColliderId = attacker.WeaponColliders[0].GetInstanceID(),
                otherColliderId = defender.WeaponColliders[0].GetInstanceID() });
        }

        [UnityTest]
        public IEnumerator TimedParry_StopsDamage_AndStartsOneRiposte()
        {
            Build();
            var a = battle.Left;
            var d = battle.Right;
            Move(d, new Vector2(a.X + d.AttackRange * .8f, .02f));
            for (int i = 0; i < 15; i++) yield return null;
            a.StartAttack(battle.Context.SimTime, AttackStyle.Overhead);
            a.SetState(FighterState.AttackActive);
            d.SetState(FighterState.Guard);
            d.ArmParry(battle.Context.SimTime);
            int parries = 0, counters = 0, hits = 0;
            battle.Context.Events.Guard += e => { if (e.parried) parries++; };
            battle.Context.Events.Motion += e => { if (e.kind == FighterMotionKind.Riposte) counters++; };
            battle.Context.Events.Hit += e => { if (e.attacker == d.Id && e.style == AttackStyle.Riposte) hits++; };
            float hp = d.Runtime.hp;
            Debug.Log($"[TECH] Parry ready={d.CanParry(a, battle.Context.SimTime)} grounded={d.IsGrounded} y={d.Body.position.y:F2} vx={d.Body.linearVelocity.x:F2} vy={d.Body.linearVelocity.y:F2} d={d.DistanceToOpponent:F2} range={d.AttackRange:F2} state={d.Runtime.state}");
            ReportWeaponHit(a, d);
            yield return null;
            Assert.AreEqual(1, parries);
            Assert.AreEqual(hp, d.Runtime.hp);
            Assert.IsTrue(battle.Context.Hits.Ledger.IsResolved(a.Runtime.currentAttackId, d.Id));
            Assert.AreEqual("受け流し", Label(d).text);
            Capture("technique_parry");
            ReportWeaponHit(a, d);
            bool counterShot = false;
            for (int i = 0; i < 180; i++)
            {
                yield return null;
                if (!counterShot && d.Runtime.attackStyle == AttackStyle.Riposte && d.Runtime.state == FighterState.AttackActive)
                { Capture("technique_riposte"); counterShot = true; Assert.AreEqual("反撃", Label(d).text); }
            }
            Assert.AreEqual(1, parries);
            Assert.AreEqual(1, counters);
            Assert.AreEqual(1, hits, "受け流し後の斬り返しが一度だけ当たる");
            Assert.IsTrue(counterShot);
            Assert.IsTrue(sounds.Contains(CombatSound.Parry));
            Assert.IsTrue(sounds.Contains(CombatSound.CounterSwing));
            Assert.IsTrue(sounds.Contains(CombatSound.CounterHit));
            Assert.Greater(d.Runtime.parryReadyAt, battle.Context.SimTime - 4f);
        }

        [UnityTest]
        public IEnumerator ExpiredParry_RemainsOrdinaryGuard_AndStaggerCancelsCounter()
        {
            Build();
            var a = battle.Left;
            var d = battle.Right;
            Move(d, new Vector2(a.X + d.AttackRange * .8f, .02f));
            yield return null;
            a.StartAttack(battle.Context.SimTime, AttackStyle.Overhead);
            a.SetState(FighterState.AttackActive);
            d.SetState(FighterState.Guard);
            d.ArmParry(battle.Context.SimTime);
            d.Runtime.parryUntil = battle.Context.SimTime - .1f;
            int parries = 0, counters = 0;
            battle.Context.Events.Guard += e => { if (e.parried) parries++; };
            battle.Context.Events.Motion += e => { if (e.kind == FighterMotionKind.Riposte) counters++; };
            ReportWeaponHit(a, d);
            yield return null;
            Assert.AreEqual(0, parries);
            Assert.IsTrue(sounds.Contains(CombatSound.Guard));
            d.ScheduleRiposte(battle.Context.SimTime);
            d.EnterStagger(.5f);
            for (int i = 0; i < 35; i++) yield return null;
            Assert.AreEqual(0, counters);
            Assert.AreEqual(-1f, d.Runtime.riposteAt);
        }

        [UnityTest]
        public IEnumerator RealAI_ChoosesDashWhirlAndParry()
        {
            SimHarness.Begin(false);
            var seen = new HashSet<AttackStyle>();
            int parries = 0;
            // Situational actions add PRNG decisions, so sample enough matches to cover all three techniques.
            for (int seed = 10; seed < 34; seed++)
            {
                battle = SimHarness.Build(seed, SimHarness.Custom("火", WeaponSize.M, style: BattleStyle.Counter),
                    SimHarness.Custom("山", seed % 2 == 0 ? WeaponSize.XL : WeaponSize.M, style: BattleStyle.Counter));
                battle.Context.Events.StateChanged += e => { if (e.to == FighterState.AttackActive) seen.Add(battle.Director.Fighters[e.fighter].Runtime.attackStyle); };
                battle.Context.Events.Guard += e => { if (e.parried) parries++; };
                yield return SimHarness.RunToEnd(battle, 10f);
                Assert.IsTrue(battle.Director.Ended);
                battle.Destroy(); battle = null;
                yield return null;
                if (seen.Contains(AttackStyle.DashSlash) && seen.Contains(AttackStyle.Whirl) && parries > 0) break;
            }
            Assert.IsTrue(seen.Contains(AttackStyle.DashSlash));
            Assert.IsTrue(seen.Contains(AttackStyle.Whirl));
            Assert.Greater(parries, 0);
            Debug.Log("[TECH] AI: " + string.Join(",", seen) + " parries=" + parries);
        }
    }
}
