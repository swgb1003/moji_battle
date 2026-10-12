using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    public sealed class FighterVolumeTests
    {
        BattleInstance battle;
        CombatBalance balance;
        RenderTexture target;
        bool effects;
        [SetUp] public void Setup()
        {effects=TimeController.PresentationEffectsEnabled;TimeController.PresentationEffectsEnabled=false;TimeController.ResetAll();Time.captureDeltaTime=.04f;}
        [TearDown] public void Cleanup()
        {
            battle?.Destroy();if(balance!=null)Object.Destroy(balance);
            if(target!=null){target.Release();Object.Destroy(target);}
            Time.captureDeltaTime=0;TimeController.ResetAll();TimeController.PresentationEffectsEnabled=effects;
            Fighter.ForcedStyleForTests=null;SimHarness.End();GameFlow.ToTitle();
        }
        void Build(string left="刀",string right="盾",WeaponSize size=WeaponSize.M,GripType grip=GripType.TwoHanded,bool presentation=false)
        {
            balance=Object.Instantiate(CombatBalance.Default);balance.suddenDeath=false;
            var config=new MatchConfig {left=SimHarness.Custom(left,size,grip,style:BattleStyle.Aggressive),right=SimHarness.Custom(right,size,GripType.Horizontal,style:BattleStyle.Defensive),seed=1739,durationSeconds=12};
            battle=BattleBuilder.Build(config,balance,GlyphCalibration.Default,presentation,0,out var error);Assert.IsNotNull(battle,error);
            battle.Director.enabled=false;battle.Director.SilentSimulation=true;
            battle.Context.Interventions=new BattleIntervention(battle.Director,InterventionController.Com,InterventionController.Com);
        }
        void RenderViews()
        {
            foreach(var f in battle.Director.Fighters){f.CapturePose();f.CapturePose();f.View.SendMessage("LateUpdate");}
        }
        [UnityTest] public IEnumerator RoundedRig_KeepsHandsOnGrip_AndDoesNotChangePhysicsAcrossBuilds()
        {
            SimHarness.Begin(false);
            foreach(var setup in new[]{("刀","盾",WeaponSize.M,GripType.TwoHanded),("一","火",WeaponSize.S,GripType.OneHanded),("鬱","龍",WeaponSize.XL,GripType.Reverse),("雷","水",WeaponSize.L,GripType.Horizontal)})
            {
                MatchResult prior=default;int priorSteps=0;
                for(int pass=0;pass<2;pass++)
                {
                    Build(setup.Item1,setup.Item2,setup.Item3,setup.Item4);
                    foreach(var f in battle.Director.Fighters)f.View.VolumeRendering=pass!=0;
                    for(int i=0;i<1200 && !battle.Director.Ended;i++)
                    {
                        battle.Director.SimulateStep();
                        if(i%8==0)
                        {
                            var bodies=battle.Director.Fighters.Select(f=>f.Body.position).ToArray();
                            var weapons=battle.Director.Fighters.Select(f=>f.WeaponBody.position).ToArray();RenderViews();
                            for(int side=0;side<2;side++)
                            {
                                var f=battle.Director.Fighters[side];Assert.AreEqual(bodies[side],f.Body.position);Assert.AreEqual(weapons[side],f.WeaponBody.position);
                                if(pass!=0 && !f.Runtime.weaponDetached)
                                {
                                    Assert.Less(Vector3.Distance(f.View.VolumeRig.MainHand,f.WeaponSprite.transform.position),.0001f);
                                    if(f.Mods.grip==GripType.TwoHanded)Assert.Less(Vector3.Distance(f.View.VolumeRig.SupportHand,StickmanView.SecondHand(f,f.WeaponBody.position,Quaternion.Euler(0,0,f.WeaponBody.rotation))),.0001f);
                                    Assert.That(f.View.VolumeRig.VertexCount,Is.InRange(1000,8000));
                                    Assert.IsTrue(f.View.VolumeRig.BodyRenderer.sharedMaterial.shader.isSupported);
                                }
                            }
                        }
                        if(i%160==0)yield return null;
                    }
                    Assert.IsTrue(battle.Director.Ended);
                    if(pass==0){prior=battle.Director.Result;priorSteps=battle.Director.StepCount;}
                    else
                    {
                        Assert.AreEqual(prior.winner,battle.Director.Result.winner);Assert.AreEqual(prior.finishReason,battle.Director.Result.finishReason);
                        Assert.AreEqual(prior.elapsedSeconds,battle.Director.Result.elapsedSeconds);CollectionAssert.AreEqual(prior.hpRemaining,battle.Director.Result.hpRemaining);
                        Assert.AreEqual(priorSteps,battle.Director.StepCount);
                    }
                    battle.Destroy();battle=null;Object.Destroy(balance);balance=null;yield return null;
                }
            }
        }
        [UnityTest] public IEnumerator Rig_HiddenDuringCupAggregation_AndHandsFollowTurningGlyph()
        {
            SimHarness.Begin(false);Build();RenderViews();
            var f=battle.Left;f.Runtime.sweepYaw=63;RenderViews();
            Assert.Less(Vector3.Distance(f.View.VolumeRig.SupportHand,StickmanView.SecondHand(f,f.WeaponBody.position,Quaternion.Euler(0,0,f.WeaponBody.rotation))),.0001f);
            ComputerMatchSimulation.Configure(battle);
            Assert.IsTrue(battle.Root.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled));
            Assert.IsFalse(battle.Left.View.enabled);Assert.IsFalse(battle.Right.View.enabled);
            yield return null;
        }
        [UnityTest] public IEnumerator CaptureBattleHud_AndMeasureRigRenderCost()
        {
            SimHarness.Begin(false);Build(presentation:true);
            for(int i=0;i<150;i++)battle.Director.SimulateStep();
            yield return null;RenderViews();battle.CameraRig.SendMessage("LateUpdate");
            MenuUiTests.Capture("FighterVolume/full_battle");
            var milliseconds=new double[2];
            for(int pass=0;pass<2;pass++)
            {
                foreach(var f in battle.Director.Fighters)f.View.VolumeRendering=pass!=0;
                for(int i=0;i<20;i++)RenderViews();
                var watch=System.Diagnostics.Stopwatch.StartNew();
                for(int i=0;i<300;i++)RenderViews();
                watch.Stop();milliseconds[pass]=watch.Elapsed.TotalMilliseconds/300;
            }
            File.WriteAllText(Path.Combine(Application.dataPath,"../Reports/fighter-volume-render-cost.json"),
                $"{{\"legacyMilliseconds\":{milliseconds[0].ToString(System.Globalization.CultureInfo.InvariantCulture)},\"volumeMilliseconds\":{milliseconds[1].ToString(System.Globalization.CultureInfo.InvariantCulture)},\"verticesPerFighter\":{battle.Left.View.VolumeRig.VertexCount},\"scope\":\"Native CPU pose and mesh update for two fighters; excludes GPU and WebGL\"}}");
        }
        [UnityTest,Timeout(300000)] public IEnumerator CaptureBladeVersusShieldBeforeAfter()
        {
            SimHarness.Begin(false);
            foreach(bool volume in new[]{false,true})
            {
                Build(presentation:true);foreach(var f in battle.Director.Fighters)f.View.VolumeRendering=volume;
                battle.CameraRig.Cam.clearFlags=CameraClearFlags.SolidColor;battle.CameraRig.Cam.backgroundColor=FighterFactory.Paper;
                target=new RenderTexture(960,540,24);target.Create();
                string directory=Path.Combine(Application.dataPath,"../Reports/UI/FighterVolume",volume?"after":"before");Directory.CreateDirectory(directory);
                for(int frame=0;frame<200;frame++)
                {
                    battle.Director.SimulateStep();battle.Director.SimulateStep();yield return null;RenderViews();
                    battle.CameraRig.SendMessage("LateUpdate");
                    var camera=battle.CameraRig.Cam;var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
                    camera.targetTexture=target;camera.aspect=960f/540f;camera.Render();RenderTexture.active=target;
                    var texture=new Texture2D(960,540,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();
                    File.WriteAllBytes(Path.Combine(directory,$"{frame:D4}.jpg"),texture.EncodeToJPG(90));Object.Destroy(texture);
                    RenderTexture.active=previousActive;camera.targetTexture=previousTarget;
                }
                battle.Destroy();battle=null;Object.Destroy(balance);balance=null;target.Release();Object.Destroy(target);target=null;yield return null;
            }
        }
    }
}
