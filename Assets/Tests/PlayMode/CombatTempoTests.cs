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
    public class CombatTempoTests
    {
        BattleInstance battle;
        CombatBalance balance;
        RenderTexture target;
        sealed class Sample
        {
            public float seconds, quiet, recovery, down, first = -1, grip, stretched, damage;
            public int starts, hits, recoveryCount;
            public string trace;
        }

        [TearDown] public void Cleanup()
        {
            battle?.Destroy(); battle = null;
            if (balance != null) UnityEngine.Object.Destroy(balance);
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            Fighter.ForcedStyleForTests = null; SimHarness.End();
        }

        void Build(FighterLoadout a, FighterLoadout b, int seed, bool tempo, bool presentation = false, float countdown = 0)
        {
            balance = UnityEngine.Object.Instantiate(CombatBalance.Default);
            balance.combatTempo = tempo;
            battle = BattleBuilder.Build(new MatchConfig { left = a, right = b, seed = seed, durationSeconds = 60 },
                balance, GlyphCalibration.Default, presentation, countdown, out string error);
            Assert.NotNull(battle, error); battle.Director.enabled = false;
        }

        IEnumerator Measure(FighterLoadout a, FighterLoadout b, int seed, bool tempo, Sample s, float seconds = 20)
        {
            Build(a,b,seed,tempo);
            var trace = new StringBuilder();
            battle.Context.Events.StateChanged += e =>
            {
                trace.Append($"{e.fighter}:{(int)e.to}:{battle.Context.SimTime:F3};");
                if (e.to == FighterState.AttackRecovery) s.recoveryCount++;
                if (e.to == FighterState.AttackActive && s.first < 0) s.first = battle.Director.Clock;
            };
            for (int i = 0; i < seconds / TimeController.BaseFixedDelta && !battle.Context.MatchOver; i++)
            {
                battle.Director.SimulateStep();
                if (battle.Context.MatchOver) break;
                if (battle.Director.Fighters.All(f => (f.Runtime.state == FighterState.Approach
                    || f.Runtime.state == FighterState.Guard || f.Runtime.state == FighterState.AttackRecovery)
                    && Mathf.Abs(f.Body.linearVelocity.x) < .5f)) s.quiet += TimeController.BaseFixedDelta;
                foreach (var f in battle.Director.Fighters)
                {
                    if (f.Runtime.state == FighterState.AttackRecovery) s.recovery += TimeController.BaseFixedDelta;
                    if (f.Runtime.state == FighterState.Knockdown || f.Runtime.state == FighterState.Recover) s.down += TimeController.BaseFixedDelta;
                    if (!f.Runtime.weaponDetached && !f.Runtime.IsDown && f.Runtime.state != FighterState.Recover)
                    {
                        float grip = Vector2.Distance(f.GripWorld,f.Body.GetRelativePoint(f.HandLocal(f.Facing)));
                        s.grip = Mathf.Max(s.grip,grip);
                        if (grip > .55f) s.stretched += TimeController.BaseFixedDelta;
                    }
                    Assert.IsFalse(float.IsNaN(f.Body.position.x) || float.IsInfinity(f.WeaponBody.angularVelocity));
                }
                if (i % 100 == 0) yield return null;
            }
            s.seconds = battle.Director.Clock;
            s.starts = battle.Director.Telemetry.fighters.Sum(f => f.attackStarts);
            s.hits = battle.Left.Runtime.metrics.hitsLanded + battle.Right.Runtime.metrics.hitsLanded;
            s.damage = battle.Left.Runtime.maxHp-battle.Left.Runtime.hp + battle.Right.Runtime.maxHp-battle.Right.Runtime.hp;
            s.trace = trace + $"hp={battle.Left.Runtime.hp:F4}/{battle.Right.Runtime.hp:F4};hits={s.hits}";
            battle.Destroy(); battle = null; UnityEngine.Object.Destroy(balance); balance = null; yield return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator SameMatchComparison_ReducesDowntimeAndKeepsPhysicalContacts()
        {
            SimHarness.Begin(false);
            var old = new List<Sample>(); var tuned = new List<Sample>();
            var csv = new StringBuilder("left,right,size,grip,style,seed,tempo,seconds,first_active,quiet,recovery,down,starts,hits,damage,max_grip,stretched_seconds,recovery_count\n");
            foreach (var c in new[] {
                ("一","刀",WeaponSize.S,GripType.OneHanded,BattleStyle.Aggressive),
                ("火","水",WeaponSize.M,GripType.OneHanded,BattleStyle.Aggressive),
                ("鬱","龍",WeaponSize.XL,GripType.TwoHanded,BattleStyle.Aggressive),
                ("盾","刀",WeaponSize.L,GripType.Horizontal,BattleStyle.Defensive),
                ("刀","火",WeaponSize.M,GripType.Reverse,BattleStyle.HitAndAway),
                ("龍","龍",WeaponSize.XL,GripType.TwoHanded,BattleStyle.Counter) })
            foreach (int seed in new[] {739,1518})
            foreach (bool tempo in new[] {false,true})
            {
                var s = new Sample(); var (a,b,size,grip,style) = c;
                yield return Measure(SimHarness.Custom(a,size,grip,style:style),SimHarness.Custom(b,size,grip,style:style),seed,tempo,s);
                (tempo?tuned:old).Add(s);
                csv.AppendLine(FormattableString.Invariant($"{a},{b},{size},{grip},{style},{seed},{tempo},{s.seconds:F3},{s.first:F3},{s.quiet:F3},{s.recovery:F3},{s.down:F3},{s.starts},{s.hits},{s.damage:F2},{s.grip:F3},{s.stretched:F3},{s.recoveryCount}"));
            }
            File.WriteAllText(Path.Combine(SimHarness.ReportDir,"combat-tempo-comparison.csv"),csv.ToString());
            float Rate(List<Sample> list, Func<Sample,float> value) => list.Sum(value)/list.Sum(s=>s.seconds);
            string summary = FormattableString.Invariant($"quiet={Rate(old,s=>s.quiet):F3}->{Rate(tuned,s=>s.quiet):F3}; recovery={Rate(old,s=>s.recovery):F3}->{Rate(tuned,s=>s.recovery):F3}; down={Rate(old,s=>s.down):F3}->{Rate(tuned,s=>s.down):F3}; starts/min={60*Rate(old,s=>s.starts):F2}->{60*Rate(tuned,s=>s.starts):F2}; hits/min={60*Rate(old,s=>s.hits):F2}->{60*Rate(tuned,s=>s.hits):F2}");
            File.WriteAllText(Path.Combine(SimHarness.ReportDir,"combat-tempo-summary.txt"),summary);
            Debug.Log("[COMBAT TEMPO] " + summary);
            Debug.Log(FormattableString.Invariant($"[COMBAT RESET] seconds/reset={old.Sum(s=>s.recovery)/old.Sum(s=>s.recoveryCount):F3}->{tuned.Sum(s=>s.recovery)/tuned.Sum(s=>s.recoveryCount):F3}"));
            Assert.Less(tuned.Sum(s=>s.recovery)/tuned.Sum(s=>s.recoveryCount),
                old.Sum(s=>s.recovery)/old.Sum(s=>s.recoveryCount)*.85f,"Less waiting per actual post-swing reset.");
            Assert.Less(Rate(tuned,s=>s.quiet),Rate(old,s=>s.quiet)*.9f,"Less stationary downtime during the match.");
            Assert.Greater(Rate(tuned,s=>s.starts),Rate(old,s=>s.starts)*1.1f,"More actual attack attempts, not just faster playback.");
            Assert.GreaterOrEqual(Rate(tuned,s=>s.hits),Rate(old,s=>s.hits)*.9f,"The faster pace must retain physical contact damage.");
            Assert.Less(Rate(tuned,s=>s.stretched),.03f,"No sustained grip instability.");
        }

        [UnityTest] public IEnumerator SameSeed_ReplaysAfterAnUnrelatedMatch()
        {
            SimHarness.Begin(false); var a=SimHarness.Custom("火"); var b=SimHarness.Custom("龍",WeaponSize.XL,GripType.TwoHanded);
            var first=new Sample(); var other=new Sample(); var again=new Sample();
            yield return Measure(a,b,739,true,first,12);
            yield return Measure(a,b,1518,true,other,12);
            yield return Measure(a,b,739,true,again,12);
            Assert.AreEqual(first.trace,again.trace); Assert.AreNotEqual(first.trace,other.trace);
        }

        [UnityTest] public IEnumerator HeavyPreparationIsReadableAndWhiffsKeepAnOpening()
        {
            SimHarness.Begin(false);
            Build(SimHarness.Custom("一",WeaponSize.S),SimHarness.Custom("龍",WeaponSize.XL,GripType.TwoHanded),739,true, countdown:1000);
            foreach(var f in battle.Director.Fighters) f.StartAttack(0,AttackStyle.Overhead);
            Assert.Greater(battle.Right.Runtime.windupDuration,battle.Left.Runtime.windupDuration*1.5f);
            Assert.Greater(battle.Right.Runtime.recoveryDuration,battle.Left.Runtime.recoveryDuration*1.5f);
            Assert.Greater(battle.Right.WeaponBody.mass,battle.Left.WeaponBody.mass);
            float recovery=battle.Right.Runtime.recoveryDuration;
            battle.Right.SetState(FighterState.AttackActive);
            battle.Right.Runtime.stateTime=10;
            battle.Director.SimulateStep();
            Assert.GreaterOrEqual(battle.Right.Runtime.recoveryDuration,recovery,"A whiff must not recover faster than a hit.");
            yield break;
        }

        [UnityTest] public IEnumerator SettledDownRecoversQuicklyButAirborneDownAndKoDoNotStandEarly()
        {
            SimHarness.Begin(false);
            Build(SimHarness.Custom("火"),SimHarness.Custom("水"),739,true,countdown:1000);
            var f=battle.Left; RecoverEvent? recovered=null;
            battle.Context.Events.Recovered+=e=>{if(e.fighter==0)recovered=e;};
            f.Knockdown.EnterKnockdown(0); f.Body.linearVelocity=Vector2.zero; f.Body.angularVelocity=0;
            f.Knockdown.Tick(0,TimeController.BaseFixedDelta);
            Assert.That(f.Runtime.recoverAt,Is.InRange(.4f,.8f));
            f.Knockdown.Tick(f.Runtime.recoverAt+.001f,TimeController.BaseFixedDelta);
            Assert.AreEqual(FighterState.Recover,f.Runtime.state); Assert.IsTrue(recovered.HasValue);
            Assert.That(recovered.Value.sinceSettle,Is.InRange(.4f,.82f));
            f.Knockdown.EnterKnockdown(1); f.Body.position+=Vector2.up*5; f.Body.linearVelocity=Vector2.up*5;
            // Update the ground sensor through the actual simulation before testing the grounded timeout.
            battle.Director.SimulateStep(); f.Runtime.stateTime=2.6f; f.Runtime.recoverAt=-1;
            f.Knockdown.Tick(3.6f,TimeController.BaseFixedDelta);
            Assert.AreEqual(FighterState.Knockdown,f.Runtime.state);
            f.Knockdown.EnterKO(4); f.Runtime.stateTime=10; f.Knockdown.Tick(14,TimeController.BaseFixedDelta);
            Assert.AreEqual(FighterState.KO,f.Runtime.state);
            yield break;
        }

        [UnityTest, Timeout(300000)] public IEnumerator CaptureActualBattleBeforeAndAfterAtNormalSpeed()
        {
            SimHarness.Begin(false);
            Time.captureDeltaTime=2f*TimeController.BaseFixedDelta;
            foreach(bool tempo in new[]{false,true})
            {
                Build(SimHarness.Custom("火",WeaponSize.M),SimHarness.Custom("龍",WeaponSize.XL,GripType.TwoHanded),739,tempo,true);
                battle.CameraRig.Cam.clearFlags=CameraClearFlags.SolidColor;
                battle.CameraRig.Cam.backgroundColor=FighterFactory.Paper;
                string directory=Path.Combine(SimHarness.ReportDir,"CombatTempo",tempo?"after":"before");
                Directory.CreateDirectory(directory);
                target=new RenderTexture(960,540,24);
                for(int frame=0;frame<Mathf.RoundToInt(12f/(2f*TimeController.BaseFixedDelta));frame++)
                {
                    battle.Director.SimulateStep(); battle.Director.SimulateStep();
                    yield return null;
                    foreach(var view in battle.Root.GetComponentsInChildren<StickmanView>()) view.SendMessage("LateUpdate");
                    var camera=battle.CameraRig.Cam;
                    var previousTarget=camera.targetTexture; var previousActive=RenderTexture.active;
                    camera.targetTexture=target; camera.aspect=960f/540f; camera.Render();
                    RenderTexture.active=target;
                    var texture=new Texture2D(960,540,TextureFormat.RGB24,false);
                    texture.ReadPixels(new Rect(0,0,960,540),0,0); texture.Apply();
                    File.WriteAllBytes(Path.Combine(directory,$"{frame:D4}.jpg"),texture.EncodeToJPG(85));
                    UnityEngine.Object.Destroy(texture);
                    RenderTexture.active=previousActive; camera.targetTexture=previousTarget;
                }
                battle.Destroy(); battle=null; UnityEngine.Object.Destroy(balance); balance=null;
                target.Release(); UnityEngine.Object.Destroy(target); target=null; yield return null;
            }
        }
    }
}
