using System.Collections;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>Other group games advance in their own physics scene while the player fights.</summary>
    public sealed class WorldCupLeagueBackground : MonoBehaviour
    {
        WorldCupSession session;
        WorldCupMatch match;
        BattleInstance simulation;
        CombatBalance balance;
        int velocityIterations,positionIterations;
        IEnumerator Start()
        {
            session=WorldCupRuntimeStore.Current;
            balance=Instantiate(CombatBalance.Default);balance.suddenDeath=false;
            while(session!=null && WorldCupRuntimeStore.Current==session && !session.GroupsFinished)
            {
                if(SettingsScreen.IsOpen || TimeController.Paused){yield return null;continue;}
                if(simulation==null)
                {
                    match=session.NextComputerMatch;if(match==null || match.Group<0)yield break;
                    int originalVelocity=Physics2D.velocityIterations,originalPosition=Physics2D.positionIterations;
                    float originalDelta=Time.fixedDeltaTime;
                    try
                    {
                        simulation=BattleBuilder.Build(session.BeginMatch(match.Id,balance.matchDuration),balance,GlyphCalibration.Default,false,0,out _);
                        velocityIterations=Physics2D.velocityIterations;positionIterations=Physics2D.positionIterations;
                    }
                    finally{Physics2D.velocityIterations=originalVelocity;Physics2D.positionIterations=originalPosition;Time.fixedDeltaTime=originalDelta;}
                    if(simulation==null){Release();yield break;}
                    ComputerMatchSimulation.Configure(simulation);
                    simulation.Context.Interventions=new BattleIntervention(simulation.Director,InterventionController.Com,InterventionController.Com);
                }
                int previousVelocity=Physics2D.velocityIterations,previousPosition=Physics2D.positionIterations;
                try
                {
                    Physics2D.velocityIterations=velocityIterations;Physics2D.positionIterations=positionIterations;
                    ComputerMatchSimulation.Advance(simulation.Director,true);
                }
                finally{Physics2D.velocityIterations=previousVelocity;Physics2D.positionIterations=previousPosition;}
                if(simulation.Director.Ended)
                {
                    bool recorded=WorldCupRuntimeStore.Record(match.Id,simulation.Director.Result);Release();
                    if(!recorded)yield break;
                }
                yield return null;
            }
        }
        void Release()
        {
            if(match!=null)session?.CancelStart(match.Id);match=null;
            if(simulation!=null){simulation.Destroy();simulation=null;}
        }
        void OnDestroy(){Release();if(balance!=null)Destroy(balance);}
    }
}
