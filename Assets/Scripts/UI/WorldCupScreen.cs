using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>World Cup uses the existing paper UI, customization and combat screens.</summary>
    public sealed partial class WorldCupScreen : MonoBehaviour
    {
        RectTransform root;
        Canvas canvas;
        Text calculation;
        UguiKit.ChoiceButton primary;
        BattleInstance simulation;
        WorldCupSession simulatedSession;
        int simulationId=-1;
        CombatBalance leagueBalance;
        string error;
        int group=-1,quarter;
        bool wholeBracket,bracketFocused;
        public bool Busy {get;private set;}
        public WorldCupSession Session=>WorldCupRuntimeStore.Current;
        public WorldCupCeremony Ceremony {get;private set;}
        void Start()
        {
            TimeController.ResetAll();if(Camera.main!=null)Camera.main.backgroundColor=RetroUi.Paper;
            if(Session?.GroupsFinished==true)
            {var own=Session.Matches.Skip(72).Take(16).FirstOrDefault(m=>m.IncludesPlayer);if(own!=null)quarter=(own.Id-72)/4;}
            Build();
        }
        void Update()
        {
            if(SettingsScreen.BlocksInput || !Input.GetKeyDown(KeyCode.Escape))return;
            if(Ceremony!=null){Ceremony.Skip();return;}
            if(Session?.Page==WorldCupPage.Versus)Open(Session.GroupsFinished?WorldCupPage.Bracket:WorldCupPage.Groups);
            else if(Session?.Page==WorldCupPage.ThirdRanking)Open(WorldCupPage.GroupResult);
            else GameFlow.ToTournamentSelect();
        }
        public void Open(WorldCupPage page)
        {WorldCupRuntimeStore.Show(page);Build();}
        void Build()
        {
            if(canvas!=null){canvas.gameObject.SetActive(false);Destroy(canvas.gameObject);}
            Ceremony=null;primary=null;calculation=null;
            root=WorldCupDesign.Screen("WorldCupCanvas",out canvas,Session?.Page==WorldCupPage.Ceremony?"design_ceremony_backdrop":Session?.Page==WorldCupPage.Versus && Session.SelectedMatch>=0 && Session.Match(Session.SelectedMatch).Round==4 && !Session.Match(Session.SelectedMatch).Bronze?"design_final_backdrop":"design_paper");
            if(Session==null){if(GameFlow.TournamentSelect)Select();else Guide();return;}
            if(group<0)group=Session.PlayerGroup;
            switch(Session.Page)
            {
                case WorldCupPage.Draw:Draw();break;
                case WorldCupPage.Groups:Groups();break;
                case WorldCupPage.GroupResult:GroupResult();break;
                case WorldCupPage.ThirdRanking:ThirdRanking();break;
                case WorldCupPage.Bracket:Bracket();break;
                case WorldCupPage.Versus:Versus();break;
                case WorldCupPage.MatchResult:MatchResult();break;
                case WorldCupPage.Eliminated:FinalResult(false);break;
                case WorldCupPage.Ceremony:Ceremony=canvas.gameObject.AddComponent<WorldCupCeremony>();Ceremony.Build(root,Session,()=>Open(WorldCupPage.Photo));break;
                case WorldCupPage.Photo:Photo();break;
                case WorldCupPage.Results:FinalResult(true);break;
            }
            if(!Busy && !Session.Finished && (Session.Page==WorldCupPage.Groups || Session.Page==WorldCupPage.Bracket))StartCoroutine(Resolve(false));
        }
        void Next()
        {
            if(Busy)
            {
                if(Session.GroupsFinished || Session.NextPlayerMatch==null)return;
                StopAllCoroutines();ReleaseSimulation();Busy=false;
            }
            if(error!=null){error=null;StartCoroutine(Resolve(true));return;}
            if(Session.Finished){Open(Session.ChampionId==0 && !Session.CeremonySeen?WorldCupPage.Ceremony:WorldCupPage.Results);return;}
            if(Session.GroupsFinished && Session.NextPlayerMatch?.Round==4 && !Session.NextPlayerMatch.Bronze && Session.Match(103).Ready)
            {StartCoroutine(Resolve(true));return;}
            var m=Session.NextPlayerMatch;if(m!=null)ShowVersus(m.Id);
            else if(Session.GroupsFinished)Open(Session.Eliminated?WorldCupPage.Eliminated:WorldCupPage.Bracket);
        }
        public void ShowVersus(int id)
        {if(Busy || Session.Match(id)?.Ready!=true)return;Session.SelectedMatch=id;Open(WorldCupPage.Versus);}
        IEnumerator Resolve(bool bronze)
        {
            if(Busy)yield break;Busy=true;if(primary!=null)primary.button.interactable=!Session.GroupsFinished && Session.NextPlayerMatch!=null;
            try
            {
                WorldCupMatch m;
                while((m=Session.NextComputerMatch)!=null)
                {
                    if(m.Bronze && !bronze && Session.Qualified && !Session.Eliminated && !Session.WatchRest)break;
                    if(calculation!=null)calculation.text=$"結果を集計中…　{Session.Matches.Count(a=>a.Complete)}試合終了";
                    simulatedSession=Session;simulationId=m.Id;
                    if(leagueBalance==null){leagueBalance=Instantiate(CombatBalance.Default);leagueBalance.suddenDeath=false;}
                    var balance=m.Group>=0?leagueBalance:CombatBalance.Default;
                    simulation=BattleBuilder.Build(Session.BeginMatch(m.Id,balance.matchDuration),balance,GlyphCalibration.Default,false,0,out error);
                    if(simulation==null){Session.CancelStart(m.Id);break;}
                    ComputerMatchSimulation.Configure(simulation);
                    simulation.Context.Interventions=new BattleIntervention(simulation.Director,InterventionController.Com,InterventionController.Com);
                    while(!simulation.Director.Ended)
                    {
                        if(SettingsScreen.IsOpen || TimeController.Paused){yield return null;continue;}
                        ComputerMatchSimulation.Advance(simulation.Director);
                        yield return null;
                    }
                    if(!WorldCupRuntimeStore.Record(m.Id,simulation.Director.Result))throw new InvalidOperationException("試合結果を記録できません。");
                    ReleaseSimulation();yield return null;
                }
            }
            finally{Busy=false;ReleaseSimulation();}
            if(Session.Page==WorldCupPage.Groups && Session.GroupsFinished)WorldCupRuntimeStore.Show(WorldCupPage.GroupResult);
            if(Session.Finished && Session.Page==WorldCupPage.Bracket)WorldCupRuntimeStore.Show(Session.ChampionId==0&&!Session.CeremonySeen?WorldCupPage.Ceremony:WorldCupPage.Results);
            // Build without scheduling another simulation pass at a deliberate bronze pause.
            Busy=true;Build();Busy=false;
            if(primary!=null){primary.button.interactable=Session.NextPlayerMatch!=null || error!=null || Session.Finished;
                if(error!=null)primary.label.text="集計を再試行 →";}
            if(calculation!=null)calculation.text=error??"各組1・2位＋3位上位8文字が進出 / 大会を中断しても続きから再開できます";
        }
        void ReleaseSimulation()
        {
            simulatedSession?.CancelStart(simulationId);simulationId=-1;simulatedSession=null;
            if(simulation!=null){simulation.Destroy();simulation=null;}
        }
        void OnDestroy(){ReleaseSimulation();if(leagueBalance!=null)Destroy(leagueBalance);if(canvas!=null)Destroy(canvas.gameObject);}
    }
}
