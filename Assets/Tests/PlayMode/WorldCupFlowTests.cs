using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class WorldCupFlowTests
    {
        string saved;bool hadSave;
        FighterBuildData left,right;
        [SetUp] public void Setup()
        {
            left=BuildRuntimeStore.Get(0).Clone();right=BuildRuntimeStore.Get(1).Clone();
            hadSave=PlayerPrefs.HasKey(WorldCupRuntimeStore.PrefKey);saved=PlayerPrefs.GetString(WorldCupRuntimeStore.PrefKey);
            WorldCupRuntimeStore.ClearSession();TimeController.ResetAll();TimeController.PresentationEffectsEnabled=false;
            Time.captureDeltaTime=.02f;Directory.CreateDirectory(Path.Combine(Application.dataPath,"../Reports/UI/WorldCup"));
        }
        [TearDown] public void Cleanup()
        {
            GameFlow.ToTitle();WorldCupRuntimeStore.ClearSession();BuildRuntimeStore.Set(0,left);BuildRuntimeStore.Set(1,right);
            BuildRuntimeStore.Active=false;SimHarness.End();Time.captureDeltaTime=0;
            if(hadSave)PlayerPrefs.SetString(WorldCupRuntimeStore.PrefKey,saved);else PlayerPrefs.DeleteKey(WorldCupRuntimeStore.PrefKey);
            PlayerPrefs.Save();
        }
        static Button B(string name)=>Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name==name);
        static WorldCupScreen Screen=>Object.FindAnyObjectByType<WorldCupScreen>();
        static IEnumerator Ready(){yield return null;yield return null;yield return new WaitForSecondsRealtime(.35f);}
        static void Capture(string name)
        {
            MenuUiTests.Capture("WorldCup/"+name);
            foreach(var text in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
                if(text.name=="WorldCupTitle" || text.name=="GlyphWeapon" || text.name.StartsWith("AwardGlyph") || text.name.StartsWith("PodiumRank"))
                    Assert.Greater(text.cachedTextGenerator.vertexCount,0,$"{name}: {text.name} must render its text.");
        }
        static void Finish(WorldCupSession s,WorldCupMatch m,int winner,bool displayed=false)
        {
            s.BeginMatch(m.Id,60);Assert.IsTrue(WorldCupRuntimeStore.Record(m.Id,new MatchResult {seed=m.Seed,winner=winner,
                finishReason="KO",elapsedSeconds=20,hpRemaining=winner==0?new[]{50f,0f}:new[]{0f,50f},maxHp=new[]{100f,100f}},displayed));
        }
        static void WinLeague(WorldCupSession s)
        {foreach(var m in s.Matches.Take(72).Where(m=>m.Ready).ToArray())Finish(s,m,m.IncludesPlayer?(m.LeftId==0?0:1):0);}
        static void ComputersExceptBronze(WorldCupSession s)
        {for(int n=0;n<104;n++){var m=s.NextComputerMatch;if(m==null || m.Bronze)break;Finish(s,m,0);}}
        static IEnumerator BoardReady()
        {
            double deadline=Time.realtimeSinceStartupAsDouble+120;
            while(Time.realtimeSinceStartupAsDouble<deadline)
            {
                if(Screen!=null && !Screen.Busy){yield return Ready();yield break;}
                foreach(var view in Object.FindObjectsByType<StickmanView>(FindObjectsSortMode.None))
                {Assert.IsFalse(view.enabled);Assert.IsTrue(view.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled));}
                yield return null;
            }
            Assert.Fail("ワールド文字カップの集計が完了しませんでした。");
        }
        static IEnumerator PlayAndEnd(WorldCupMatch m,int winner,string capture=null)
        {
            B("WorldCupStart").onClick.Invoke();yield return Ready();
            var boot=Object.FindAnyObjectByType<BattleBootstrap>();Assert.IsNotNull(boot?.Battle);
            var battle=boot.Battle;Assert.IsTrue(GameFlow.WorldCupMode);
            Assert.AreEqual(WeaponSize.M,battle.Left.Mods.size);Assert.AreEqual(WeaponSize.M,battle.Right.Mods.size);
            Assert.AreEqual(battle.Left.Runtime.maxHp,battle.Left.Runtime.hp);Assert.AreEqual(battle.Right.Runtime.maxHp,battle.Right.Runtime.hp);
            Assert.AreEqual(m.LeftId==0?InterventionController.Local:InterventionController.Com,battle.Context.Interventions.Side(0).controller);
            Assert.AreEqual(m.RightId==0?InterventionController.Local:InterventionController.Com,battle.Context.Interventions.Side(1).controller);
            if(!m.IncludesPlayer)Assert.AreEqual(-1,battle.Root.GetComponent<InterventionHud>().LocalSide);
            if(m.Group>=0)Assert.IsFalse(battle.Context.Balance.suddenDeath);
            if(m.Group<0 && m.Round==4 && !m.Bronze)Assert.AreEqual("final2",battle.Root.GetComponent<CombatAudioDirector>().MusicSource.clip.name);
            double deadline=Time.realtimeSinceStartupAsDouble+12;
            while(!battle.Director.Fighting && !battle.Director.Ended && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.IsTrue(battle.Director.Fighting);
            yield return null;yield return new WaitForSecondsRealtime(.15f);if(capture!=null)Capture(capture);
            battle.Director.SilentSimulation=true;
            (winner==0?battle.Right:battle.Left).Runtime.hp=0;
            deadline=Time.realtimeSinceStartupAsDouble+15;
            while(SceneManager.GetActiveScene().name!=GameFlow.WorldCupScene && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.AreEqual(GameFlow.WorldCupScene,SceneManager.GetActiveScene().name);yield return Ready();Assert.IsTrue(m.Complete);
        }
        [UnityTest] public IEnumerator ChampionFlow_SharedCustomize_League_BronzeSpectator_Final_Ceremony_Photo_Result()
        {
            GameFlow.ToTournamentSelect();yield return Ready();Capture("01_tournament_select");
            Assert.IsTrue(Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t=>t.text==TournamentSession.CupName));
            B("TournamentChoiceStart1").onClick.Invoke();yield return Ready();Capture("02_guide");
            Assert.IsNotNull(Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Single(i=>i.name=="world_cup_guide_hero").texture);
            B("WorldCupBegin").onClick.Invoke();yield return Ready();
            var custom=Object.FindAnyObjectByType<CustomizeScreen>().Customizer;Assert.IsTrue(custom.SetCharacter("火"));
            custom.SetSize(WeaponSize.XL);Assert.AreEqual(WeaponSize.M,custom.Current.weaponSize);
            foreach(string size in new[]{"S","M","L","XL"})Assert.IsFalse(B("Size_"+size).interactable);
            Assert.AreEqual(JsonUtility.ToJson(left),JsonUtility.ToJson(BuildRuntimeStore.Get(0)));Capture("03_customize");
            B("Battle").onClick.Invoke();yield return Ready();var s=WorldCupRuntimeStore.Current;
            Assert.AreEqual(48,s.Entrants.Count);Capture("04_group_draw");
            foreach(var m in s.Matches.Where(m=>!m.IncludesPlayer).ToArray())Finish(s,m,0);
            B("WorldCupDrawNext").onClick.Invoke();yield return BoardReady();Capture("05_group_board");
            MenuUiTests.Capture("WorldCup/05_group_board_880x450",880,450);
            var first=s.NextPlayerMatch;B("WorldCupNext").onClick.Invoke();yield return Ready();Capture("06_vs");
            yield return PlayAndEnd(first,first.LeftId==0?0:1,"07_battle");Capture("08_match_result");
            WinLeague(s);Screen.Open(WorldCupPage.GroupResult);yield return Ready();Capture("09_group_qualified");
            B("WorldCupThirdTable").onClick.Invoke();yield return Ready();Capture("10_third_ranking");
            Screen.Open(WorldCupPage.Bracket);yield return BoardReady();Capture("11_knockout_board");
            int playerBlock=(s.Matches.Skip(72).Take(16).Single(m=>m.IncludesPlayer).Id-72)/4;
            Assert.AreEqual(WorldCupDesign.Red,B("WorldCupQuarter"+playerBlock).GetComponent<Image>().color);
            for(int round=0;round<4;round++)
            {ComputersExceptBronze(s);var m=s.NextPlayerMatch;Assert.AreEqual(round,m.Round);Finish(s,m,m.LeftId==0?0:1);}
            ComputersExceptBronze(s);Screen.Open(WorldCupPage.Bracket);yield return BoardReady();
            Assert.IsTrue(s.Match(103).Ready);B("WorldCupWatchBronze").onClick.Invoke();yield return Ready();Capture("12_bronze_match");
            yield return PlayAndEnd(s.Match(103),0);B("WorldCupResultNext").onClick.Invoke();yield return BoardReady();
            B("WorldCupNext").onClick.Invoke();yield return Ready();Capture("13_final");
            var final=s.NextPlayerMatch;yield return PlayAndEnd(final,final.LeftId==0?0:1);
            B("WorldCupResultNext").onClick.Invoke();yield return Ready();Assert.IsNotNull(Screen.Ceremony);
            var awardFigures=Object.FindObjectsByType<WorldCupAwardFigure>(FindObjectsSortMode.None);
            Assert.AreEqual(3,awardFigures.Length);Assert.IsTrue(awardFigures.All(f=>f.GetComponent<CanvasRenderer>()!=null));
            Assert.IsNotNull(Object.FindAnyObjectByType<WorldCupFireworks>().GetComponent<CanvasRenderer>());
            Screen.Ceremony.Preview(6);Capture("14_podium");Screen.Ceremony.Preview(9);Capture("15_congratulations");
            Screen.Ceremony.Skip();yield return Ready();Capture("16_photo");
            B("WorldCupPhotoNext").onClick.Invoke();yield return Ready();Capture("17_tournament_result");
            Assert.IsTrue(s.Finished);Assert.AreEqual(0,s.ChampionId);Assert.AreEqual(8,s.PlayerWins);Assert.IsFalse(WorldCupRuntimeStore.CanResume);
            // ワールド文字カップの称号: 出場・予選突破・八強・表彰台・優勝・無敗優勝（8 戦全勝）・優勝した文字
            var profile=AchievementStore.Profile;
            foreach(int id in new[]{69,70,73,74,75,76})Assert.IsTrue(profile.Has(id),"称号 "+id+" が付かない");
            Assert.GreaterOrEqual(profile.Value(77),1);
            B("WorldCupResultBracket").onClick.Invoke();yield return Ready();Assert.AreEqual(WorldCupPage.Bracket,s.Page);
            B("WorldCupWholeBracket").onClick.Invoke();yield return Ready();Capture("19_whole_bracket");
            MenuUiTests.Capture("WorldCup/19_whole_bracket_1280x960",1280,960);
            // 文字カップの称号の一覧（獲得したバッジ）
            GameFlow.ToAchievements();yield return Ready();
            Object.FindAnyObjectByType<AchievementsScreen>().ChooseCategory(10);yield return Ready();
            MenuUiTests.Capture("Achievements/world_cup_titles");
        }
        [UnityTest] public IEnumerator IllustratedSelectionAndGuide_AssetsAndNavigationRenderAtDesktopAndSmallScreen()
        {
            GameFlow.ToTournamentSelect();yield return Ready();
            foreach(string name in new[]{"watanabe_cup_bracket","world_cup_emblem"})
            {
                var image=Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Single(i=>i.name==name);
                Assert.IsNotNull(image.texture);Assert.Greater(image.texture.width,500);
                Assert.AreEqual((float)image.texture.width/image.texture.height,image.rectTransform.rect.width/image.rectTransform.rect.height,.001f);
                Assert.IsFalse(image.raycastTarget);
            }
            Capture("01_tournament_select");MenuUiTests.Capture("WorldCup/01_tournament_select_880x450",880,450);
            B("TournamentChoiceStart1").onClick.Invoke();yield return Ready();
            foreach(string name in new[]{"world_cup_guide_hero","world_cup_guide_landscape","world_cup_rule_m","world_cup_rule_groups","world_cup_rule_knockout"})
                Assert.IsNotNull(Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Single(i=>i.name==name).texture);
            Capture("02_guide");MenuUiTests.Capture("WorldCup/02_guide_880x450",880,450);
            B("WorldCupBegin").onClick.Invoke();yield return Ready();
            Assert.IsNotNull(Object.FindAnyObjectByType<CustomizeScreen>());
            Assert.AreEqual(WeaponSize.M,Object.FindAnyObjectByType<CustomizeScreen>().Customizer.Current.weaponSize);
        }
        [UnityTest] public IEnumerator SemifinalLoser_PlaysBronzeAndFinishesThird()
        {
            GameFlow.ToWorldCupGuide();yield return Ready();WorldCupRuntimeStore.Begin(FighterBuildData.Default("火"),738);
            var s=WorldCupRuntimeStore.Current;WinLeague(s);
            for(int round=0;round<4;round++)
            {ComputersExceptBronze(s);var m=s.NextPlayerMatch;int side=m.LeftId==0?0:1;Finish(s,m,round==3?1-side:side);}
            ComputersExceptBronze(s);GameFlow.ToWorldCupScreen();Screen?.Open(WorldCupPage.Bracket);yield return Ready();
            Screen.Open(WorldCupPage.Bracket);yield return BoardReady();Assert.IsFalse(s.Eliminated);Assert.IsTrue(s.NextPlayerMatch.Bronze);
            B("WorldCupNext").onClick.Invoke();yield return Ready();var bronze=s.NextPlayerMatch;
            yield return PlayAndEnd(bronze,bronze.LeftId==0?0:1);B("WorldCupResultNext").onClick.Invoke();yield return BoardReady();
            Assert.IsTrue(s.Finished);Assert.AreEqual(0,s.ThirdId);Assert.IsTrue(AchievementStore.Profile.Has(74),"3位でも表彰台の称号が付く");Assert.AreEqual(WorldCupPage.Results,s.Page);Capture("20_third_place_result");
        }
        [UnityTest] public IEnumerator EarlyDefeat_ShowsExitAndWatchRest_AndResumePreservesSeed()
        {
            GameFlow.ToWorldCupGuide();yield return Ready();WorldCupRuntimeStore.Begin(FighterBuildData.Default("火"),938);
            var s=WorldCupRuntimeStore.Current;WinLeague(s);var m=s.NextPlayerMatch;Finish(s,m,m.LeftId==0?1:0);
            WorldCupRuntimeStore.Show(WorldCupPage.Eliminated);GameFlow.ToWorldCupScreen();yield return Ready();Capture("18_eliminated");
            B("WorldCupWatchRest").onClick.Invoke();yield return BoardReady();Assert.IsTrue(s.Finished);Assert.Greater(s.ChampionId,0);
            Assert.AreEqual(WorldCupPage.Results,s.Page);
        }
        [UnityTest] public IEnumerator ResultLabels_FollowRemainingFixtures_AndEliminatedPlacementFitsWithoutCrown()
        {
            GameFlow.ToWorldCupGuide();yield return Ready();
            WorldCupRuntimeStore.Begin(FighterBuildData.Default("レ"),938);
            var s=WorldCupRuntimeStore.Current;
            string[] captions={"次は第2節","次は最終戦","グループリーグ終了"};
            for(int round=0;round<3;round++)
            {
                var match=s.NextPlayerMatch;Finish(s,match,match.LeftId==0?1:0,true);
                WorldCupRuntimeStore.Show(WorldCupPage.MatchResult);GameFlow.ToWorldCupScreen();yield return Ready();
                var caption=Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Single(t=>t.name=="NextResultCaption");
                Assert.AreEqual(captions[round],caption.text);
                Capture("21_result_progress_round"+(round+1));
            }
            foreach(var match in s.Matches.Take(72).Where(m=>m.Ready).ToArray())Finish(s,match,0);
            Assert.IsFalse(s.Qualified);
            for(int i=0;i<32;i++){var match=s.NextComputerMatch;if(match==null)break;Finish(s,match,0);}
            Assert.IsTrue(s.Finished);Assert.IsTrue(s.Eliminated);
            Screen.Open(WorldCupPage.Results);yield return Ready();Capture("22_group_eliminated_final_result");
            // Capture temporarily changes canvas scale; rebuild after it restores
            // the original scale before converting generated vertices to UI units.
            yield return Ready();
            var placement=Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Single(t=>t.name=="PlayerPlacement");
            Assert.AreEqual("グループリーグ\n敗退",placement.text);
            Assert.IsFalse(Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Any(i=>i.name=="design_laurel"));
            Assert.Greater(placement.cachedTextGenerator.vertexCount,0);
            var bounds=placement.rectTransform.rect;
            // Generate using the current canvas units; the screenshot renderer's
            // cached geometry can still use its temporary capture scale.
            var generator=new TextGenerator();
            Assert.IsTrue(generator.Populate(placement.text,placement.GetGenerationSettings(bounds.size)));
            var vertices=generator.verts;
            for(int i=0;i+3<vertices.Count;i+=4)
            {
                // Line breaks can have degenerate quads outside the text bounds;
                // check the geometry that actually draws visible characters.
                var quad=vertices.Skip(i).Take(4).Select(v=>v.position/placement.pixelsPerUnit).ToArray();
                if(quad.Max(v=>v.x)-quad.Min(v=>v.x)<.01f || quad.Max(v=>v.y)-quad.Min(v=>v.y)<.01f)continue;
                foreach(var point in quad)
                {
                    Assert.GreaterOrEqual(point.x,bounds.xMin-1);
                    Assert.LessOrEqual(point.x,bounds.xMax+1);
                    Assert.GreaterOrEqual(point.y,bounds.yMin-1);
                    Assert.LessOrEqual(point.y,bounds.yMax+1);
                }
            }
        }
        [UnityTest] public IEnumerator ComputerLeague_UsesRealSimulationAndStopsBeforePlayerFixture()
        {
            GameFlow.ToWorldCupGuide();yield return Ready();WorldCupRuntimeStore.Begin(FighterBuildData.Default("火"),512);
            var s=WorldCupRuntimeStore.Current;int seed=s.NextPlayerMatch.Seed;WorldCupRuntimeStore.Show(WorldCupPage.Groups);
            GameFlow.ToWorldCupScreen();yield return Ready();yield return BoardReady();
            Assert.AreEqual(23,s.Journal.Count);Assert.AreEqual(seed,s.NextPlayerMatch.Seed);Assert.AreEqual(0,s.PlayerGroupGames);
            Assert.IsTrue(s.Journal.All(r=>WorldCupSession.ValidResult(r.result)));Assert.IsTrue(B("WorldCupNext").interactable);
            WorldCupRuntimeStore.ClearSession();Assert.IsTrue(WorldCupRuntimeStore.Resume());Assert.AreEqual(23,WorldCupRuntimeStore.Current.Journal.Count);
            Assert.AreEqual(seed,WorldCupRuntimeStore.Current.NextPlayerMatch.Seed);
        }
        [System.Serializable] sealed class CupBatchBenchmark
        {
            public string environment="Unity native PlayMode; six identical World Cup COM fixtures";
            public double previousSeconds,optimizedSeconds,speedup;
            public int previousYieldCount,optimizedYieldCount;
            public bool identicalResults;
        }
        [UnityTest] public IEnumerator ComputerMatchBatches_PreserveWinnersHpAndTime_AndMeasureSixFixtures()
        {
            GameFlow.ToWorldCupGuide();yield return Ready();WorldCupRuntimeStore.Begin(FighterBuildData.Default("火"),512);
            var s=WorldCupRuntimeStore.Current;
            var matches=s.Matches.Where(m=>m.Group>=0 && !m.IncludesPlayer).Take(6).ToArray();
            var prior=new MatchResult[6];var stepCounts=new int[6];
            var benchmark=new CupBatchBenchmark();
            var balance=Object.Instantiate(CombatBalance.Default);balance.suddenDeath=false;
            BattleInstance battle=null;
            try
            {
                for(int pass=0;pass<2;pass++)
                {
                    double started=Time.realtimeSinceStartupAsDouble;int yields=0;
                    for(int i=0;i<matches.Length;i++)
                    {
                        var m=matches[i];
                        var config=new MatchConfig {left=s.Entrants[m.LeftId].Build.ToLoadout(),right=s.Entrants[m.RightId].Build.ToLoadout(),seed=m.Seed,durationSeconds=balance.matchDuration};
                        battle=BattleBuilder.Build(config,balance,GlyphCalibration.Default,false,0,out var error);Assert.IsNotNull(battle,error);
                        ComputerMatchSimulation.Configure(battle);battle.Director.CollectStepTelemetry=pass==0;
                        battle.Director.FastSimulationQueries=pass!=0;battle.Director.CapturePresentationPose=pass==0;
                        battle.Context.Interventions=new BattleIntervention(battle.Director,InterventionController.Com,InterventionController.Com);
                        while(!battle.Director.Ended)
                        {
                            if(pass==0)
                            {
                                double slice=Time.realtimeSinceStartupAsDouble;
                                for(int step=0;step<256 && !battle.Director.Ended;step++)
                                {battle.Director.SimulateStep();if(Time.realtimeSinceStartupAsDouble-slice>.008)break;}
                            }
                            else ComputerMatchSimulation.Advance(battle.Director);
                            yields++;yield return null;
                        }
                        if(pass==0){prior[i]=battle.Director.Result;stepCounts[i]=battle.Director.StepCount;}
                        else
                        {
                            var result=battle.Director.Result;
                            Assert.AreEqual(prior[i].winner,result.winner,"Winner changed for fixture "+m.Id);
                            Assert.AreEqual(prior[i].finishReason,result.finishReason);
                            Assert.AreEqual(prior[i].elapsedSeconds,result.elapsedSeconds);
                            CollectionAssert.AreEqual(prior[i].hpRemaining,result.hpRemaining);
                            CollectionAssert.AreEqual(prior[i].maxHp,result.maxHp);
                            Assert.AreEqual(stepCounts[i],battle.Director.StepCount);
                        }
                        battle.Destroy();battle=null;yield return null;
                    }
                    double elapsed=Time.realtimeSinceStartupAsDouble-started;
                    if(pass==0){benchmark.previousSeconds=elapsed;benchmark.previousYieldCount=yields;}
                    else {benchmark.optimizedSeconds=elapsed;benchmark.optimizedYieldCount=yields;}
                }
                benchmark.identicalResults=true;benchmark.speedup=benchmark.previousSeconds/benchmark.optimizedSeconds;
                File.WriteAllText(Path.Combine(Application.dataPath,"../Reports/cup-aggregation-benchmark.json"),JsonUtility.ToJson(benchmark,true));
                Assert.Less(benchmark.optimizedYieldCount,benchmark.previousYieldCount,"Larger batches should require fewer waiting frames.");
            }
            finally {battle?.Destroy();Object.Destroy(balance);}
        }
        [UnityTest] public IEnumerator LeagueCanStartImmediately_BackgroundKeepsPlayerPhysicsAndResumeSafe()
        {
            GameFlow.ToWorldCupGuide();yield return Ready();WorldCupRuntimeStore.Begin(FighterBuildData.Default("火"),613);
            var s=WorldCupRuntimeStore.Current;WorldCupRuntimeStore.Show(WorldCupPage.Groups);GameFlow.ToWorldCupScreen();yield return Ready();
            Assert.IsTrue(Screen.Busy);Assert.IsTrue(B("WorldCupNext").interactable);
            B("WorldCupNext").onClick.Invoke();yield return Ready();Assert.IsFalse(Screen.Busy);
            Assert.IsFalse(s.Matches.Any(m=>m.InProgress));B("WorldCupStart").onClick.Invoke();yield return Ready();
            var boot=Object.FindAnyObjectByType<BattleBootstrap>();Assert.IsNotNull(boot.GetComponent<WorldCupLeagueBackground>());
            int velocity=Physics2D.velocityIterations,position=Physics2D.positionIterations;int before=s.Journal.Count;
            double deadline=Time.realtimeSinceStartupAsDouble+20;
            while(s.Journal.Count==before && Time.realtimeSinceStartupAsDouble<deadline)
            {
                Assert.AreEqual(velocity,Physics2D.velocityIterations);Assert.AreEqual(position,Physics2D.positionIterations);
                Assert.IsTrue(boot.Battle.Left.View.enabled);Assert.IsTrue(boot.Battle.Right.View.enabled);yield return null;
            }
            Assert.Greater(s.Journal.Count,before,"Player combat did not advance other league games.");
            int seed=boot.CurrentSeed;GameFlow.ToTitle();yield return Ready();Assert.IsTrue(WorldCupRuntimeStore.Resume());
            var restored=WorldCupRuntimeStore.Current;Assert.IsFalse(restored.Matches.Any(m=>m.InProgress));
            Assert.IsTrue(restored.Matches.Any(m=>m.Ready && m.IncludesPlayer && m.Seed==seed));
        }
    }
}
