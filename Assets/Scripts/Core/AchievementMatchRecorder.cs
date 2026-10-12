using System;
using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    [Serializable]
    public sealed class AchievementMatchStats
    {
        public int[] totals = new int[51];
        public float minHp = 1f, largestDeficit;
        public bool damaged, braced, specialTaken, specialFinish, cheerFinish, detached;
        public AchievementMatchStats Copy() => new AchievementMatchStats { totals=(int[])totals.Clone(), minHp=minHp,
            largestDeficit=largestDeficit, damaged=damaged, braced=braced, specialTaken=specialTaken,
            specialFinish=specialFinish, cheerFinish=cheerFinish, detached=detached };
    }

    /// <summary>Only attached to player matches by BattleBootstrap, never to previews or tournament COM simulations.
    /// Host records both fighters; the guest consumes cumulative snapshots so dropped/repeated network events do not change progress.</summary>
    public sealed class AchievementMatchRecorder : MonoBehaviour
    {
        public readonly AchievementMatchStats[] Stats = { new AchievementMatchStats(), new AchievementMatchStats() };
        readonly int[] applied = new int[51];
        readonly HashSet<int>[] hits = { new HashSet<int>(), new HashSet<int>() };
        readonly HashSet<int>[] brokenGuards = { new HashSet<int>(), new HashSet<int>() };
        readonly Dictionary<int,InterventionEvent>[] specialAttacks = { new Dictionary<int,InterventionEvent>(), new Dictionary<int,InterventionEvent>() };
        readonly Dictionary<int,int>[] castHits = { new Dictionary<int,int>(), new Dictionary<int,int>() };
        readonly int[] braceCast = { -1,-1 };
        MatchDirector director;
        TournamentSession tournament;
        int localSide, round;
        bool friend, started, finished, bound, receivedStats;
        public void Bind(MatchDirector match, int side, bool friendMode, TournamentSession cup = null, int tournamentRound = -1)
        {
            director=match; localSide=side; friend=friendMode; tournament=cup; round=tournamentRound; bound=true;
            AchievementStore.Change(p => p.matchInProgress=true,true);
            var e=director.Events;
            e.Hit+=Hit; e.EnvImpact+=Environment; e.Guard+=Guard; e.Motion+=Motion; e.Intervention+=Intervention; e.MatchEnded+=End;
        }
        void FixedUpdate()
        {
            if (!bound || finished || director.RemotePlayback) return;
            if (director.Fighting || director.KoInProgress || director.Ended) StartCounting();
            SampleVitals();
        }
        void StartCounting()
        {
            if (started) return;
            started=true;
            AchievementStore.Change(p => { p.Set(1,1); if (friend) p.Set(7,1); },true);
        }
        public void SampleVitals()
        {
            for (int i=0;i<2;i++)
            {
                var own=director.Fighters[i].Runtime; var opponent=director.Fighters[1-i].Runtime;
                float hp=Mathf.Clamp01(own.hp/Mathf.Max(1f,own.maxHp));
                var s=Stats[i]; s.minHp=Mathf.Min(s.minHp,hp);
                s.largestDeficit=Mathf.Max(s.largestDeficit,opponent.hp/Mathf.Max(1f,opponent.maxHp)-hp);
                s.detached=own.weaponDetached;
                if (hp < 1f-1e-6f) s.damaged=true;
            }
        }
        void Commit()
        {
            var totals=Stats[localSide].totals;
            bool any=false;
            for (int i=1;i<51;i++) if (totals[i]>applied[i]) { any=true; break; }
            if (!any) return;
            AchievementStore.Change(p =>
            {
                for (int i=1;i<51;i++)
                {
                    int delta=Math.Max(0,totals[i]-applied[i]);
                    if (delta>0) p.Add(i,delta);
                    applied[i]=Math.Max(applied[i],totals[i]);
                }
            });
        }
        void Hit(HitEvent e)
        {
            if (director.RemotePlayback || finished) return;
            StartCounting(); SampleVitals();
            var iv=director.Context.Interventions;
            bool special=iv!=null && iv.Side(e.attacker).OwnsAttack(e.attackId);
            if (e.damage>0)
            {
                var victim=Stats[e.defender]; victim.damaged=true;
                if (special)
                {
                    victim.specialTaken=true;
                    if (iv.Side(e.defender).braced)
                    {
                        victim.braced=true;
                        int cast=iv.Side(e.attacker).specials;
                        if (braceCast[e.defender]!=cast) { braceCast[e.defender]=cast; victim.totals[31]++; }
                    }
                }
                if (director.Fighters[e.defender].Runtime.hp<=0)
                { Stats[e.attacker].specialFinish=special; Stats[e.attacker].cheerFinish=iv!=null && e.time<=iv.Side(e.attacker).cheerUntil; }
            }
            if (!hits[e.attacker].Add(e.attackId)) { Commit(); return; }
            var s=Stats[e.attacker];
            if (e.combo) s.totals[18]++;
            if (e.style==AttackStyle.StrokeShot) s.totals[19]++;
            if (e.style==AttackStyle.GroundWave) s.totals[20]++;
            if (e.critical) s.totals[21]++;
            if (e.style==AttackStyle.Riposte) s.totals[23]++;
            if (AttackTechniques.IsKick(e.style)) s.totals[25]++;
            if (e.style==AttackStyle.Throw) s.totals[27]++;
            if (specialAttacks[e.attacker].TryGetValue(e.attackId,out var step) && step.stepCount==4)
            {
                castHits[e.attacker].TryGetValue(step.castId,out int mask);
                mask |= 1 << step.castStep; castHits[e.attacker][step.castId]=mask;
                if (mask==15) s.totals[29]=1;
            }
            Commit();
        }
        void Environment(EnvImpactEvent e)
        {
            if (director.RemotePlayback || finished) return;
            SampleVitals();
            if (e.damage>0) Stats[e.fighter].damaged=true;
            // Environmental damage cannot be a special/cheer finishing hit.
            if (e.damage>0 && director.Fighters[e.fighter].Runtime.hp<=0)
            { Stats[1-e.fighter].specialFinish=false; Stats[1-e.fighter].cheerFinish=false; }
        }
        void Guard(GuardEvent e)
        {
            if (!director.RemotePlayback && !finished && e.broke && brokenGuards[e.attacker].Add(e.attackId))
            { Stats[e.attacker].totals[22]++; Commit(); }
        }
        void Motion(FighterMotionEvent e)
        {
            if (director.RemotePlayback || finished) return;
            if (e.kind==FighterMotionKind.BindPush) Stats[e.fighter].totals[24]++;
            if (e.kind==FighterMotionKind.WallJump) Stats[e.fighter].totals[26]++;
            Commit();
        }
        void Intervention(InterventionEvent e)
        {
            if (director.RemotePlayback || finished || !e.success) return;
            var s=Stats[e.fighter];
            if (e.kind==InterventionKind.Cheer) { s.totals[5]++; s.totals[34]++; }
            else
            {
                specialAttacks[e.fighter][e.attackId]=e;
                if (e.castStep==0) s.totals[28]++;
                if (e.dashed) s.totals[30]++;
            }
            Commit();
        }
        public AchievementMatchStats[] Capture()
        {
            if (!director.RemotePlayback && !finished) SampleVitals();
            return new[] { Stats[0].Copy(),Stats[1].Copy() };
        }
        public void ApplyRemote(AchievementMatchStats[] stats, bool active)
        {
            if (!bound || finished || !director.RemotePlayback || stats==null || stats.Length!=2) return;
            if (active) StartCounting();
            for (int i=0;i<2;i++)
            {
                if (stats[i]?.totals==null || stats[i].totals.Length!=51) continue;
                Stats[i]=stats[i].Copy();
                if(i==localSide) receivedStats=true;
            }
            Commit();
        }
        void End(MatchResult result)
        {
            if (finished) return;
            StartCounting();
            if (!director.RemotePlayback) SampleVitals();
            Commit(); finished=true;
            var s=Stats[localSide];
            // Older hosts have no achievement journal: missing history is not proof of an undamaged win.
            if(director.RemotePlayback && !receivedStats) { s.damaged=true; s.minHp=0; }
            if (tournament!=null) tournament.AchievementIronWall &= s.minHp>.5f && !float.IsNaN(s.minHp);
            AchievementStore.Change(p =>
            {
                p.Result(result.winner,localSide);
                if (friend) p.Add(49);
                if (result.winner!=localSide) return;
                var own=localSide==0?director.Config.left:director.Config.right;
                var enemy=localSide==0?director.Config.right:director.Config.left;
                p.WinBuild(own.build??new FighterBuildData {character=own.grapheme,fontType=own.font},
                    enemy.build??new FighterBuildData {character=enemy.grapheme,fontType=enemy.font});
                if (!s.damaged) p.Add(8);
                if (result.hpRemaining[localSide]/Mathf.Max(1f,result.maxHp[localSide])<=.05f+1e-6f) p.Add(9);
                if (s.largestDeficit>=.3f-1e-6f) p.Add(10);
                if (result.finishReason=="KO" && result.elapsedSeconds<=15f+1e-5f) p.Add(11);
                if (result.finishReason=="TIME_UP") p.Add(12);
                if (s.specialFinish && result.finishReason=="KO") p.Add(13);
                if (s.detached) p.Add(14);
                if (s.braced) p.Add(32);
                if (s.cheerFinish && result.finishReason=="KO") p.Add(33);
                if (s.specialTaken) p.Add(35);
                if (tournament!=null && round==2)
                { p.Set(47,1); if (tournament.AchievementIronWall) p.Set(48,1); }
            },true);
        }
        void OnDisable()
        {
            if (!bound) return;
            if (!finished) AchievementStore.Change(p => p.Interrupted(),true);
            var e=director.Events;
            e.Hit-=Hit; e.EnvImpact-=Environment; e.Guard-=Guard; e.Motion-=Motion; e.Intervention-=Intervention; e.MatchEnded-=End;
            bound=false;
        }
    }
}
