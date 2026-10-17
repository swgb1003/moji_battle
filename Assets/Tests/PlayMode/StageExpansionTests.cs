using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class StageExpansionTests
    {
        FighterBuildData left,right;
        BattleStageId selected;
        int reduced;
        [SetUp] public void Setup()
        {
            left=BuildRuntimeStore.Get(0).Clone();right=BuildRuntimeStore.Get(1).Clone();selected=StageSession.Selected;
            reduced=PlayerPrefs.GetInt("MenuReducedMotion",0);PlayerPrefs.SetInt("MenuReducedMotion",1);
            TimeController.ResetAll();GameFlow.ToCustomize();
            BuildRuntimeStore.Set(0,FighterBuildData.Default("火"));BuildRuntimeStore.Set(1,FighterBuildData.Default("水"));
            BuildRuntimeStore.Active=true;Directory.CreateDirectory(Path.Combine(Application.dataPath,"../Reports/UI/Stages"));
        }
        [TearDown] public void Cleanup()
        {
            StageSession.OpenOnCustomize=false;StageSession.Selected=selected;
            TimeController.ResetAll();GameFlow.ToTitle();BuildRuntimeStore.Set(0,left);BuildRuntimeStore.Set(1,right);
            PlayerPrefs.SetInt("MenuReducedMotion",reduced);
        }
        static Button B(string name)=>Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x=>x.name==name);
        static IEnumerator Ready(){yield return null;yield return null;yield return new WaitForSecondsRealtime(.25f);}

        [UnityTest] public IEnumerator SelectPreviewBattleResultAndReturn_PreserveBuildsAndStage()
        {
            yield return Ready();
            GameFlow.ToModeSelect();yield return Ready();MenuUiTests.Capture("Stages/01_mode_select");
            B("BattleModeStart").onClick.Invoke();yield return Ready();
            var screen=Object.FindAnyObjectByType<CustomizeScreen>();
            MenuUiTests.Capture("Stages/02_customize");
            B("Battle").onClick.Invoke();yield return Ready();
            Assert.IsTrue(screen.StageVisible);Assert.IsFalse(screen.TextInputVisible);
            Assert.AreEqual(6,Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Count(x=>x.name.StartsWith("StageCard")));
            B("StageCard1").onClick.Invoke();yield return Ready();
            MenuUiTests.Capture("Stages/03_stage_select");MenuUiTests.Capture("Stages/03_stage_select_880x450",880,450);
            B("ConfirmStage").onClick.Invoke();yield return Ready();
            Assert.IsTrue(screen.VsVisible);Assert.IsFalse(screen.StageVisible);Assert.AreEqual(BattleStageId.Calligraphy,StageSession.Selected);
            MenuUiTests.Capture("Stages/04_vs");MenuUiTests.Capture("Stages/04_vs_880x450",880,450);
            B("StageVsBack").onClick.Invoke();yield return Ready();Assert.IsTrue(screen.StageVisible);
            B("ConfirmStage").onClick.Invoke();B("Start").onClick.Invoke();yield return Ready();
            var boot=Object.FindAnyObjectByType<BattleBootstrap>();Assert.IsNotNull(boot.Battle);
            Assert.AreEqual(BattleStageId.Calligraphy,boot.Battle.Director.Config.stage);
            Assert.AreEqual("火",boot.Battle.Left.Loadout.grapheme);Assert.AreEqual("水",boot.Battle.Right.Loadout.grapheme);
            boot.Battle.Director.Replay=null;boot.Battle.Right.Runtime.hp=0;boot.Battle.Right.Knockdown.EnterKO(boot.Battle.Context.SimTime);
            double deadline=Time.realtimeSinceStartupAsDouble+8;
            while(!boot.Battle.Director.Ended && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.IsTrue(boot.Battle.Director.Ended);yield return Ready();
            MenuUiTests.Capture("Stages/06_result");MenuUiTests.Capture("Stages/06_result_880x450",880,450);
            B("Again").onClick.Invoke();yield return Ready();
            Assert.AreEqual(BattleStageId.Calligraphy,boot.Battle.Director.Config.stage);Assert.IsFalse(boot.Battle.Director.Ended);
            boot.Battle.Director.Replay=null;boot.Battle.Right.Runtime.hp=0;boot.Battle.Right.Knockdown.EnterKO(boot.Battle.Context.SimTime);
            deadline=Time.realtimeSinceStartupAsDouble+8;
            while(!boot.Battle.Director.Ended && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.IsTrue(boot.Battle.Director.Ended);yield return Ready();
            B("ChangeStage").onClick.Invoke();yield return Ready();screen=Object.FindAnyObjectByType<CustomizeScreen>();
            Assert.IsTrue(screen.StageVisible);Assert.AreEqual("火",screen.Customizer.Get(0).character);Assert.AreEqual("水",screen.Customizer.Get(1).character);
            B("StageBack").onClick.Invoke();Assert.IsFalse(screen.StageVisible);
        }

        [UnityTest] public IEnumerator SixStages_RenderAndMechanicsAdvanceOnlyWithMatchClock()
        {
            yield return Ready();
            foreach(var def in StageCatalog.All)
            {
                Assert.IsNotNull(def.Texture,"Missing stage art: "+def.Name);
                StageSession.Selected=def.Id;GameFlow.ToBattle();yield return Ready();
                var boot=Object.FindAnyObjectByType<BattleBootstrap>();var battle=boot.Battle;var d=battle.Director;
                Assert.IsNotNull(battle);d.enabled=false;
                foreach(var f in d.Fighters)f.Runtime.invulnerable=true;
                for(int i=0;i<220;i++)d.SimulateStep();
                if(def.Id==BattleStageId.Exam || def.Id==BattleStageId.EraserFactory || def.Id==BattleStageId.Letterpress)
                {
                    TimeController.SetPaused(true);yield return Ready();MenuUiTests.Capture("Stages/battle_"+(int)def.Id+"_warning");TimeController.ResetAll();
                }
                for(int i=0;i<120;i++)d.SimulateStep();
                Assert.AreEqual(def.Id,d.Context.Stage.Id);
                if(def.Id==BattleStageId.Dictionary || def.Id==BattleStageId.Exam || def.Id==BattleStageId.EraserFactory || def.Id==BattleStageId.Letterpress)
                    Assert.AreEqual(1,d.Context.Stage.Activations,def.Name);
                if(def.Id==BattleStageId.Calligraphy)Assert.Greater(Mathf.Abs(d.Context.Stage.Tilt),.2f);
                TimeController.SetPaused(true);float clock=d.Clock;yield return Ready();Assert.AreEqual(clock,d.Clock);
                MenuUiTests.Capture("Stages/battle_"+(int)def.Id);
                var controls=new[]{"BattlePause","BattleSpeed0","BattleSpeed1","BattleSpeed2","BattleSpeed3","BattleDetails","BattleAgain","SpecialButton","CheerButton"};
                for(int i=1;i<controls.Length;i++)
                {
                    var a=(RectTransform)B(controls[i-1]).transform;var b=(RectTransform)B(controls[i]).transform;
                    var ac=new Vector3[4];var bc=new Vector3[4];a.GetWorldCorners(ac);b.GetWorldCorners(bc);
                    Assert.LessOrEqual(ac.Max(p=>p.x),bc.Min(p=>p.x)+1,"Footer controls overlap: "+controls[i-1]+" / "+controls[i]);
                }
                if(def.Id==BattleStageId.EraserFactory || def.Id==BattleStageId.Exam)MenuUiTests.Capture("Stages/battle_"+(int)def.Id+"_880x450",880,450);
                TimeController.ResetAll();
            }
        }

        [UnityTest] public IEnumerator SameSeedReproducesStageAndSimulation_AndSilentMatchesUseSameMechanics()
        {
            yield return Ready();
            foreach(var stage in new[]{BattleStageId.Calligraphy,BattleStageId.EraserFactory,BattleStageId.Dictionary,BattleStageId.Letterpress,BattleStageId.Exam})
            {
                Vector2 first=Vector2.zero;float firstHp=0;int firstCount=0;
                for(int repeat=0;repeat<2;repeat++)
                {
                    var config=new MatchConfig {left=FighterBuildData.Default("火").ToLoadout(),right=FighterBuildData.Default("水").ToLoadout(),seed=75419,stage=stage,durationSeconds=60};
                    var instance=BattleBuilder.Build(config,CombatBalance.Default,GlyphCalibration.Default,false,0,out var error);
                    Assert.IsNull(error);instance.Director.enabled=false;
                    foreach(var f in instance.Director.Fighters)f.Runtime.invulnerable=true;
                    for(int i=0;i<250;i++)instance.Director.SimulateStep();
                    var p=instance.Left.Body.position;
                    Assert.IsFalse(float.IsNaN(p.x)||float.IsNaN(p.y));Assert.Less(Mathf.Abs(p.x),12);Assert.Greater(p.y,-3);
                    if(repeat==0){first=p;firstHp=instance.Left.Runtime.hp;firstCount=instance.Context.Stage.Activations;}
                    else {Assert.Less(Vector2.Distance(first,p),.03f,stage.ToString());Assert.AreEqual(firstHp,instance.Left.Runtime.hp);Assert.AreEqual(firstCount,instance.Context.Stage.Activations);}
                    instance.Destroy();yield return null;yield return null;
                }
            }
        }

        [UnityTest] public IEnumerator ExamZones_HealWithinCap_AndStrikeOnce_WhilePracticeIsProtected()
        {
            yield return Ready();
            var config=new MatchConfig {left=FighterBuildData.Default("火").ToLoadout(),right=FighterBuildData.Default("水").ToLoadout(),seed=71,stage=BattleStageId.Exam,durationSeconds=60};
            var instance=BattleBuilder.Build(config,CombatBalance.Default,GlyphCalibration.Default,false,0,out var error);
            Assert.IsNull(error);instance.Director.enabled=false;
            var f=instance.Left;var other=instance.Right;
            f.Body.simulated=f.WeaponBody.simulated=other.Body.simulated=other.WeaponBody.simulated=false;
            other.Runtime.invulnerable=true;
            f.Body.position=new Vector2(instance.Context.Stage.HealingX,.3f);f.Runtime.hp=f.Runtime.maxHp-1;
            instance.Context.Stage.Step(3);Assert.AreEqual(f.Runtime.maxHp,f.Runtime.hp,.001f);
            f.Body.position=new Vector2(instance.Context.Stage.HazardX,.3f);float hp=f.Runtime.hp;
            for(int i=0;i<165;i++)instance.Director.SimulateStep();
            Assert.AreEqual(hp-4,f.Runtime.hp,.001f);Assert.AreEqual(1,instance.Context.Stage.Activations);
            instance.Context.Stage.Step(1);Assert.AreEqual(hp-4,f.Runtime.hp,.001f,"The same red-pen strike must not damage twice.");
            f.Runtime.invulnerable=true;float protectedHp=f.Runtime.hp;
            for(int i=0;i<500;i++){f.Body.position=new Vector2(-instance.Context.Stage.HealingX,.3f);instance.Director.SimulateStep();}
            Assert.GreaterOrEqual(f.Runtime.hp,protectedHp,"Practice protection applies to stage strikes too; healing remains possible.");
            instance.Destroy();yield return null;
        }
    }
}
