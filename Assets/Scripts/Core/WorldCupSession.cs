using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MojiBattle
{
    public enum WorldCupPage { Draw, Groups, GroupResult, ThirdRanking, Bracket, Versus, MatchResult, Eliminated, Ceremony, Photo, Results }
    [Serializable] public sealed class WorldCupEntrant
    {
        public int Id, Group;
        [SerializeField] FighterBuildData snapshot;
        public FighterBuildData Build => snapshot.Clone();
        public WorldCupEntrant(int id, int group, FighterBuildData build)
        { Id=id;Group=group;snapshot=build.Clone();snapshot.weaponSize=WeaponSize.M; }
    }
    public sealed class WorldCupMatch
    {
        public int Id, Group=-1, Round, LeftId=-1, RightId=-1, WinnerId=-1, Seed, Draws;
        public bool Bronze, Complete, InProgress;
        public MatchResult Result;
        public bool IncludesPlayer => LeftId==0 || RightId==0;
        public bool Ready => !Complete && !InProgress && LeftId>=0 && RightId>=0;
        public int LoserId => !Complete || WinnerId<0 ? -1 : WinnerId==LeftId ? RightId : LeftId;
        public string Name => Group>=0 ? $"グループ{(char)('A'+Group)} / 第{Round+1}節" : Bronze ? "3位決定戦" : RoundName(Round);
        public static string RoundName(int r) => new[]{"ベスト32","ベスト16","準々決勝","準決勝","決勝"}[Mathf.Clamp(r,0,4)];
    }
    public sealed class WorldCupStanding
    {
        public int Id, Group, Played, Wins, Draws, Losses, Points, KoWins;
        public float TimeUpHp, HpDifference;
    }
    [Serializable] public sealed class WorldCupRecordedResult
    { public int matchId; public MatchResult result; }

    /// <summary>48 M-size snapshots, 72 league fixtures and a 32-slot bracket with a bronze match.</summary>
    public sealed class WorldCupSession
    {
        public const string CupName="ワールド文字カップ";
        public readonly int Seed;
        public readonly string Id;
        public IReadOnlyList<WorldCupEntrant> Entrants {get;}
        public IReadOnlyList<WorldCupMatch> Matches => matches.AsReadOnly();
        public readonly List<WorldCupRecordedResult> Journal=new List<WorldCupRecordedResult>();
        readonly List<WorldCupMatch> matches=new List<WorldCupMatch>();
        public WorldCupPage Page=WorldCupPage.Draw;
        public int LastMatch=-1, SelectedMatch=-1;
        public bool WatchRest, CeremonySeen;
        /// <summary>自分の試合をすべて「moji Fighter」以上の COM と自操作で戦った（優勝すると「真・mojiFighter」を解放）。</summary>
        public bool SelfAllHard = true;
        /// <summary>この大会の優勝で「真・mojiFighter」を解放した（結果の画面で知らせる）。</summary>
        public bool UnlockedTrue;
        public bool GroupsFinished => matches.Take(72).All(m=>m.Complete);
        public bool Qualified => GroupsFinished && matches.Skip(72).Take(16).Any(m=>m.IncludesPlayer);
        public bool Finished => matches.Count==104 && matches[102].Complete && matches[103].Complete;
        public int ChampionId => matches.Count==104 ? matches[102].WinnerId : -1;
        public int RunnerUpId => matches.Count==104 ? matches[102].LoserId : -1;
        public int ThirdId => matches.Count==104 ? matches[103].WinnerId : -1;
        public int FourthId => matches.Count==104 ? matches[103].LoserId : -1;
        public int PlayerGroup => Entrants[0].Group;
        public int PlayerGroupGames => matches.Take(72).Count(m=>m.IncludesPlayer && m.Complete);
        public int PlayerWins => matches.Count(m=>m.WinnerId==0 && m.Complete);
        public bool Eliminated
        {
            get
            {
                if(!GroupsFinished)return false;
                if(!Qualified)return true;
                // A semifinal loser still has the bronze fixture to play.
                return matches.Skip(72).Any(m=>m.IncludesPlayer && m.Complete && m.WinnerId!=0 && (m.Bronze || m.Round!=3));
            }
        }
        public string PlayerPlace => ChampionId==0 ? "優勝" : RunnerUpId==0 ? "準優勝" : ThirdId==0 ? "3位" : FourthId==0 ? "4位"
            : !Qualified && GroupsFinished ? "グループリーグ敗退"
            : matches.Skip(72).LastOrDefault(m=>m.IncludesPlayer && m.Complete && m.WinnerId!=0)?.Name ?? "大会進行中";
        public WorldCupMatch NextPlayerMatch => matches.FirstOrDefault(m=>m.Ready && m.IncludesPlayer);
        public WorldCupMatch NextComputerMatch
        {
            get
            {
                if(!GroupsFinished)return matches.Take(72).FirstOrDefault(m=>m.Ready && !m.IncludesPlayer && m.Round<=PlayerGroupGames);
                if(!Qualified || Eliminated || WatchRest || (matches.Count==104 && matches[103].Complete && matches[103].IncludesPlayer))return matches.Skip(72).FirstOrDefault(m=>m.Ready && !m.IncludesPlayer);
                var player=NextPlayerMatch;
                int limit=player==null ? 4 : player.Round;
                // Settle bronze before the final, so podium places are known on the championship bell.
                return matches.Skip(72).FirstOrDefault(m=>m.Ready && !m.IncludesPlayer && (m.Bronze || m.Round<limit));
            }
        }
        public WorldCupMatch Match(int id) => id>=0 && id<matches.Count ? matches[id] : null;
        public WorldCupSession(IList<WorldCupEntrant> entrants,int seed,string id=null)
        {
            if(entrants==null || entrants.Count!=48 || entrants.Select(e=>e?.Id).Distinct().Count()!=48
                || entrants.Where((e,i)=>e==null || e.Id!=i || e.Group<0 || e.Group>11).Any()
                || Enumerable.Range(0,12).Any(g=>entrants.Count(e=>e.Group==g)!=4)
                || entrants.Select(e=>e.Build.character).Distinct().Count()!=48)
                throw new ArgumentException("48文字と各4文字の12グループが必要です。");
            Seed=seed;Id=id??Guid.NewGuid().ToString("N");
            Entrants=Array.AsReadOnly(entrants.Select(e=>new WorldCupEntrant(e.Id,e.Group,e.Build)).ToArray());
            int[,] pairs={{0,3,1,2},{0,2,3,1},{0,1,2,3}};
            for(int round=0;round<3;round++)for(int group=0;group<12;group++)
            {
                var ids=Entrants.Where(e=>e.Group==group).Select(e=>e.Id).ToArray();
                for(int p=0;p<2;p++)Add(group,round,ids[pairs[round,p*2]],ids[pairs[round,p*2+1]]);
            }
        }
        void Add(int group,int round,int left=-1,int right=-1,bool bronze=false)
        {
            var m=new WorldCupMatch {Id=matches.Count,Group=group,Round=round,LeftId=left,RightId=right,Bronze=bronze};
            m.Seed=MatchSeed(m.Id,0);matches.Add(m);
        }
        int MatchSeed(int id,int draws) => (unchecked(Seed+104729*(id+1)+7919*draws)&int.MaxValue)|1;
        public MatchConfig BeginMatch(int id,float duration)
        {
            var m=Match(id);
            if(m==null || !m.Ready)throw new InvalidOperationException("この試合は開始できません。");
            m.InProgress=true;
            return new MatchConfig {left=Entrants[m.LeftId].Build.ToLoadout(),right=Entrants[m.RightId].Build.ToLoadout(),seed=m.Seed,stage=StageCatalog.ForSeed(m.Seed,m.Group<0&&m.Round==4&&!m.Bronze),durationSeconds=duration};
        }
        public void CancelStart(int id){var m=Match(id);if(m!=null && !m.Complete)m.InProgress=false;}
        public bool Record(int id,MatchResult r)
        {
            var m=Match(id);
            if(m==null || !m.InProgress || m.Complete || r.seed!=m.Seed || !ValidResult(r))return false;
            m.InProgress=false;m.Result=Copy(r);
            Journal.Add(new WorldCupRecordedResult {matchId=id,result=Copy(r)});
            if(m.Group<0 && r.winner==-1){m.Draws++;m.Seed=MatchSeed(id,m.Draws);return true;}
            m.Complete=true;m.WinnerId=r.winner==-1 ? -1 : r.winner==0 ? m.LeftId : m.RightId;
            if(m.Group>=0){if(GroupsFinished)CreateKnockout();return true;}
            if(m.Bronze)return true;
            if(m.Round<4)
            {
                int[] offsets={72,88,96,100,102};int slot=id-offsets[m.Round];
                var next=matches[offsets[m.Round+1]+slot/2];
                if(slot%2==0)next.LeftId=m.WinnerId;else next.RightId=m.WinnerId;
                if(m.Round==3){if(slot==0)matches[103].LeftId=m.LoserId;else matches[103].RightId=m.LoserId;}
            }
            return true;
        }
        public static bool ValidResult(MatchResult r)
        {
            bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
            return r.winner>=-1 && r.winner<=1 && Finite(r.elapsedSeconds) && r.elapsedSeconds>=0
                && new[]{"KO","DOUBLE_KO","TIME_UP","TIME_UP_DRAW","SUDDEN_DEATH","SUDDEN_DEATH_DRAW"}.Contains(r.finishReason)
                && r.hpRemaining!=null && r.maxHp!=null && r.hpRemaining.Length==2 && r.maxHp.Length==2
                && Enumerable.Range(0,2).All(i=>Finite(r.hpRemaining[i])&&Finite(r.maxHp[i])&&r.maxHp[i]>0&&r.hpRemaining[i]>=0&&r.hpRemaining[i]<=r.maxHp[i]+.01f);
        }
        static MatchResult Copy(MatchResult r){r.hpRemaining=(float[])r.hpRemaining.Clone();r.maxHp=(float[])r.maxHp.Clone();r.metrics=null;return r;}
        public List<WorldCupStanding> Standings(int group)
        {
            var list=Entrants.Where(e=>e.Group==group).Select(e=>new WorldCupStanding {Id=e.Id,Group=group}).ToList();
            foreach(var m in matches.Take(72).Where(m=>m.Group==group && m.Complete))
                foreach(var row in list.Where(r=>r.Id==m.LeftId || r.Id==m.RightId))
                {
                    int side=row.Id==m.LeftId ? 0 : 1;row.Played++;
                    row.HpDifference+=m.Result.hpRemaining[side]-m.Result.hpRemaining[1-side];
                    if(m.WinnerId<0){row.Draws++;row.Points++;}
                    else if(m.WinnerId==row.Id){row.Wins++;row.Points+=3;if(m.Result.finishReason=="KO")row.KoWins++;
                        else if(m.Result.finishReason=="TIME_UP")row.TimeUpHp+=m.Result.hpRemaining[side];}
                    else row.Losses++;
                }
            // Stable, visible tie breakers; never reroll qualifying ranks.
            return list.OrderByDescending(r=>r.Points).ThenByDescending(r=>r.KoWins).ThenByDescending(r=>r.TimeUpHp)
                .ThenByDescending(r=>r.HpDifference).ThenBy(r=>r.Id).ToList();
        }
        public List<WorldCupStanding> ThirdRanking() => Enumerable.Range(0,12).Select(g=>Standings(g)[2])
            .OrderByDescending(r=>r.KoWins).ThenByDescending(r=>r.TimeUpHp).ThenByDescending(r=>r.Points)
            .ThenByDescending(r=>r.HpDifference).ThenBy(r=>r.Group).ToList();
        public List<int> Qualifiers() => Enumerable.Range(0,12).SelectMany(g=>Standings(g).Take(2).Select(r=>r.Id))
            .Concat(ThirdRanking().Take(8).Select(r=>r.Id)).ToList();
        void CreateKnockout()
        {
            if(matches.Count>72)return;
            var first=Enumerable.Range(0,12).Select(g=>Standings(g)[0].Id).ToList();
            var second=Enumerable.Range(0,12).Select(g=>Standings(g)[1]).OrderByDescending(r=>r.Points)
                .ThenByDescending(r=>r.KoWins).ThenByDescending(r=>r.TimeUpHp).ThenBy(r=>r.Group).Select(r=>r.Id).ToList();
            var seeded=first.Concat(second.Take(4)).ToList();var unseeded=second.Skip(4).Concat(ThirdRanking().Take(8).Select(r=>r.Id)).ToList();
            var rng=new System.Random(Seed);Shuffle(seeded,rng);Shuffle(unseeded,rng);
            // Bipartite matching avoids same-group opponents in the first knockout round.
            var owner=Enumerable.Repeat(-1,16).ToArray();
            bool Assign(int i,bool[] visited)
            {
                for(int j=0;j<16;j++)if(!visited[j] && Entrants[seeded[i]].Group!=Entrants[unseeded[j]].Group)
                {visited[j]=true;if(owner[j]<0 || Assign(owner[j],visited)){owner[j]=i;return true;}}
                return false;
            }
            for(int i=0;i<16;i++)if(!Assign(i,new bool[16]))throw new InvalidOperationException("決勝の組み合わせを作れません。");
            var opponents=new int[16];for(int j=0;j<16;j++)opponents[owner[j]]=unseeded[j];
            for(int i=0;i<16;i++)Add(-1,0,seeded[i],opponents[i]);
            for(int round=1;round<5;round++)for(int i=0;i<(16>>round);i++)Add(-1,round);
            Add(-1,4,bronze:true);
        }
        internal static void Shuffle<T>(IList<T> items,System.Random rng)
        {for(int i=items.Count-1;i>0;i--){int j=rng.Next(i+1);T t=items[i];items[i]=items[j];items[j]=t;}}
    }
}
