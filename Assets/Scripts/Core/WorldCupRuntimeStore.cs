using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MojiBattle
{
    public static class WorldCupCatalog
    {
        public const string Letters="火水雷龍刀風土光闇石木金鉄氷森岩空海山星狐鳥月雪花草盾矢剣鎧翼獣鏡炎砂霧雲鬼糸針鐘鼓城玉舟車扇笛猫犬虎狼鷹魚虫天地人日雨虹夢心力王斧弓槍戦武音波竹丸一口";
        public static WorldCupSession Create(FighterBuildData player,int seed)
        {
            var rng=new System.Random(seed);var bag=Letters.Select(c=>c.ToString()).Distinct().Where(c=>c!=player.character).ToList();
            WorldCupSession.Shuffle(bag,rng);var builds=new List<FighterBuildData>{player.Clone()};
            foreach(string c in bag)
            {
                if(builds.Count==48)break;
                var fonts=Enum.GetValues(typeof(FontStyleId)).Cast<FontStyleId>().ToList();WorldCupSession.Shuffle(fonts,rng);
                foreach(var f in fonts)if(FighterCustomizer.IsAvailable(c,f,out _))
                {
                    var b=FighterBuildData.Default(c);b.fontType=f;b.weaponSize=WeaponSize.M;
                    b.gripType=(GripType)rng.Next(4);b.battleStyle=(BattleStyle)rng.Next(4);b.weaponFlip=(WeaponFlip)rng.Next(4);
                    b.gripPosition=.12f+(float)rng.NextDouble()*.76f;builds.Add(b.MaybeDualWield(builds.Count));break;
                }
            }
            if(builds.Count!=48)throw new InvalidOperationException("参加できる文字が足りません。");
            var slots=Enumerable.Range(0,48).ToList();WorldCupSession.Shuffle(slots,rng);
            return new WorldCupSession(builds.Select((b,i)=>new WorldCupEntrant(i,slots[i]/4,b)).ToList(),seed);
        }
    }
    public static class WorldCupRuntimeStore
    {
        public const string PrefKey="MojiBattle.WorldCup.v1";
        public static WorldCupSession Current {get;private set;}
        public static bool Preparing {get;private set;}
        public static bool Active => Preparing || Current!=null;
        public static FighterBuildData Draft => draft.Clone();
        static FighterBuildData draft=FighterBuildData.Default("火");
        [Serializable] sealed class Saved
        {
            public int version=1,seed,lastMatch=-1;
            public string id;
            public List<WorldCupEntrant> entrants;
            public List<WorldCupRecordedResult> results;
            public WorldCupPage page;
            public bool watchRest,ceremonySeen;
        }
        public static void Prepare()
        {Current=null;Preparing=true;draft=BuildRuntimeStore.Get(0).Clone();draft.weaponSize=WeaponSize.M;}
        public static bool SetDraft(FighterBuildData b)
        {if(!Preparing || !ValidBuild(b))return false;draft=b.Clone();draft.weaponSize=WeaponSize.M;return true;}
        public static void Begin(FighterBuildData b,int seed)
        {
            if(!ValidBuild(b))throw new ArgumentException("使用できる文字を選んでください。");
            Current=WorldCupCatalog.Create(b,seed);Preparing=false;Save();
        }
        public static bool Record(int id,MatchResult r,bool displayed=false)
        {
            if(Current==null || !Current.Record(id,r))return false;
            if(displayed){Current.LastMatch=id;Current.SelectedMatch=-1;Current.Page=WorldCupPage.MatchResult;}
            Save();return true;
        }
        public static void Show(WorldCupPage page){if(Current==null)return;Current.Page=page;Save();}
        public static void AbortMatch()
        {
            if(Current==null)return;
            foreach(var m in Current.Matches)Current.CancelStart(m.Id);
            Current.SelectedMatch=-1;Current.Page=Current.GroupsFinished?WorldCupPage.Bracket:WorldCupPage.Groups;Save();
        }
        public static void Save()
        {
            var s=Current;if(s==null)return;
            var saved=new Saved {seed=s.Seed,id=s.Id,entrants=s.Entrants.ToList(),results=s.Journal,page=s.Page,lastMatch=s.LastMatch,
                watchRest=s.WatchRest,ceremonySeen=s.CeremonySeen};
            PlayerPrefs.SetString(PrefKey,JsonUtility.ToJson(saved));PlayerPrefs.Save();
        }
        public static void ClearSession(){Current=null;Preparing=false;}
        public static bool CanResume => ReadSaved(false)!=null;
        public static bool Resume()
        {Current=ReadSaved(false);Preparing=false;return Current!=null;}
        static WorldCupSession ReadSaved(bool includeFinished)
        {
            try
            {
                var x=JsonUtility.FromJson<Saved>(PlayerPrefs.GetString(PrefKey,""));
                if(x==null || x.version!=1 || string.IsNullOrEmpty(x.id) || x.entrants==null || x.entrants.Count!=48
                    || x.results==null || x.results.Count>1000 || !Enum.IsDefined(typeof(WorldCupPage),x.page)
                    || x.entrants.Any(e=>e==null || !ValidBuild(e.Build) || e.Build.weaponSize!=WeaponSize.M))return null;
                var s=new WorldCupSession(x.entrants,x.seed,x.id);
                foreach(var r in x.results)
                {
                    var m=s.Match(r.matchId);if(m==null || !m.Ready)return null;
                    s.BeginMatch(m.Id,60);if(!s.Record(m.Id,r.result))return null;
                }
                s.LastMatch=x.lastMatch;
                if(s.LastMatch!=-1 && (s.Match(s.LastMatch)==null || !s.Journal.Any(r=>r.matchId==s.LastMatch)))return null;
                s.WatchRest=x.watchRest;s.CeremonySeen=x.ceremonySeen;
                s.Page=x.page==WorldCupPage.Versus ? (s.GroupsFinished?WorldCupPage.Bracket:WorldCupPage.Groups) : x.page;
                if((s.Page==WorldCupPage.Bracket || s.Page==WorldCupPage.GroupResult || s.Page==WorldCupPage.ThirdRanking || s.Page==WorldCupPage.Eliminated) && !s.GroupsFinished)return null;
                if(s.Page==WorldCupPage.MatchResult && s.LastMatch<0)return null;
                if((s.Page==WorldCupPage.Photo || s.Page==WorldCupPage.Ceremony || s.Page==WorldCupPage.Results) && !s.Finished)return null;
                if(!includeFinished && s.Finished && s.CeremonySeen)return null;
                return s;
            }
            catch(Exception){return null;}
        }
        static bool ValidBuild(FighterBuildData b) => b!=null && !string.IsNullOrEmpty(b.character)
            && Enum.IsDefined(typeof(FontStyleId),b.fontType) && Enum.IsDefined(typeof(GripType),b.gripType)
            && Enum.IsDefined(typeof(BattleStyle),b.battleStyle) && Enum.IsDefined(typeof(WeaponFlip),b.weaponFlip)
            && !float.IsNaN(b.gripPosition) && !float.IsInfinity(b.gripPosition) && b.gripPosition>=0 && b.gripPosition<=1
            && !float.IsNaN(b.gripPoint.x) && !float.IsInfinity(b.gripPoint.x) && !float.IsNaN(b.gripPoint.y) && !float.IsInfinity(b.gripPoint.y)
            && b.gripPoint.x>=0 && b.gripPoint.x<=1 && b.gripPoint.y>=0 && b.gripPoint.y<=1
            && FighterCustomizer.IsAvailable(b.character,b.fontType,out _);
    }
}
