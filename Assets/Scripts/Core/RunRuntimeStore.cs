using System;
using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    public enum RunStage { Build, Prepare, Battle, Result, Upgrade, Finished }
    [Serializable] public sealed class RunRound
    { public int round,winner; public float seconds,hp,maxHp; public string opponent; }
    [Serializable] public sealed class RunSession
    {
        public int version=1,seed,round=1,selectedUpgrade=-1;
        public string id,lockedGlyph;
        public RunStage stage;
        public FighterBuildData player;
        public float hpShare=1;
        public int[] upgrades=new int[12],offers;
        public List<RunRound> history=new List<RunRound>();
        public bool Finished=>stage==RunStage.Finished;
        public bool Cleared=>Finished && history.Count>0 && history[history.Count-1].round==5 && history[history.Count-1].winner==0;
        public RunBonuses Bonuses=>RunCatalog.Bonuses(upgrades);
        public RunOpponent Opponent=>RunCatalog.Opponent(seed,round);
        public float MaxHp=>RunCatalog.MaxHp(player,Bonuses);
        public float Hp=>MaxHp*hpShare;
        public int Wins {get{int n=0;foreach(var r in history)if(r.winner==0)n++;return n;}}
        public float Seconds {get{float n=0;foreach(var r in history)n+=r.seconds;return n;}}
    }
    /// <summary>One saved five-fight run. Offers, HP, opponents and rewards survive reloads without rerolling.</summary>
    public static class RunRuntimeStore
    {
        public const string PrefKey="MojiBattle.Run.v1";
        public static RunSession Current {get;private set;}
        public static bool Active=>Current!=null;
        public static bool CanBattle=>Current?.stage==RunStage.Prepare && Current.hpShare>0;
        public static void Begin(int seed)
        {
            Current=new RunSession {seed=seed,id=Guid.NewGuid().ToString("N"),player=FighterBuildData.Default("火")};
            Save();
        }
        public static bool SetBuild(FighterBuildData build)
        {
            var s=Current;
            if(s?.stage!=RunStage.Build || !ValidBuild(build) || (!string.IsNullOrEmpty(s.lockedGlyph) && build.character!=s.lockedGlyph))return false;
            s.player=build.Clone();Save();return true;
        }
        public static bool Prepare()
        {
            var s=Current;
            if(s==null || (s.stage!=RunStage.Build && s.stage!=RunStage.Prepare) || !ValidBuild(s.player))return false;
            s.lockedGlyph=s.player.character;s.stage=RunStage.Prepare;Save();return true;
        }
        public static bool EditBuild()
        {if(Current?.stage!=RunStage.Prepare)return false;Current.stage=RunStage.Build;Save();return true;}
        public static MatchConfig BeginMatch(float duration)
        {
            if(!CanBattle)throw new InvalidOperationException("次戦準備から対戦を始めてください。");
            var s=Current;s.stage=RunStage.Battle;Save();
            return new MatchConfig {left=s.player.ToLoadout(),right=s.Opponent.Build.ToLoadout(),
                seed=unchecked(s.seed+s.round*104729+s.history.Count*787),stage=StageCatalog.ForSeed(unchecked(s.seed+s.round*104729),s.round==5),durationSeconds=duration};
        }
        public static bool Record(MatchResult result)
        {
            var s=Current;
            if(s?.stage!=RunStage.Battle || result.winner< -1 || result.winner>1 || result.hpRemaining==null
                || result.maxHp==null || result.hpRemaining.Length!=2 || result.maxHp.Length!=2
                || !Finite(result.hpRemaining[0]) || !Finite(result.maxHp[0]) || result.maxHp[0]<=0 || !Finite(result.elapsedSeconds))return false;
            s.hpShare=Mathf.Clamp01(result.hpRemaining[0]/result.maxHp[0]);
            s.history.Add(new RunRound {round=s.round,winner=result.winner,seconds=Mathf.Max(0,result.elapsedSeconds),
                hp=Mathf.Clamp(result.hpRemaining[0],0,result.maxHp[0]),maxHp=result.maxHp[0],opponent=s.Opponent.Build.character});
            if(result.winner==1 || s.hpShare<=0 || (result.winner==0 && s.round==RunCatalog.Battles))s.stage=RunStage.Finished;
            else s.stage=RunStage.Result;
            if(result.winner==0 && !s.Finished){s.hpShare=Mathf.Clamp01(s.hpShare+RunCatalog.RestRecovery);DealOffers();}
            Save();AchievementStore.Change(p=>p.RecordRun(s),true);return true;
        }
        static void DealOffers()
        {
            var s=Current;var bag=new List<int>();
            foreach(var u in RunCatalog.Upgrades)if(s.upgrades[u.Id]<u.Limit && (u.Id!=2 || s.hpShare<.999f))bag.Add(u.Id);
            var rng=new System.Random(unchecked(s.seed+s.round*3571));
            for(int i=bag.Count-1;i>0;i--){int j=rng.Next(i+1);int t=bag[i];bag[i]=bag[j];bag[j]=t;}
            // A damaged run always has a recovery option; the other two remain random.
            if(s.hpShare<.7f && bag.Remove(2))bag.Insert(0,2);
            s.offers=bag.GetRange(0,3).ToArray();s.selectedUpgrade=-1;
        }
        public static bool OpenUpgrade()
        {
            var s=Current;
            if(s?.stage!=RunStage.Result || s.history[s.history.Count-1].winner!=0 || s.offers==null)return false;
            s.stage=RunStage.Upgrade;Save();return true;
        }
        public static bool ShowResult()
        {if(Current?.stage!=RunStage.Upgrade)return false;Current.stage=RunStage.Result;Save();return true;}
        public static bool SelectUpgrade(int id)
        {
            var s=Current;
            if(s?.stage!=RunStage.Upgrade || s.offers==null || Array.IndexOf(s.offers,id)<0)return false;
            s.selectedUpgrade=id;Save();return true;
        }
        public static bool ConfirmUpgrade()
        {
            var s=Current;
            if(s?.stage!=RunStage.Upgrade || s.offers==null || Array.IndexOf(s.offers,s.selectedUpgrade)<0)return false;
            int id=s.selectedUpgrade;
            if(id==2)s.hpShare=Mathf.Clamp01(s.hpShare+RunCatalog.BreathRecovery);
            s.upgrades[id]++;s.round++;s.offers=null;s.selectedUpgrade=-1;s.stage=RunStage.Prepare;
            Save();return true;
        }
        public static bool ReplayDraw()
        {
            var s=Current;
            if(s?.stage!=RunStage.Result || s.history[s.history.Count-1].winner!=-1)return false;
            s.stage=RunStage.Prepare;Save();return true;
        }
        public static bool AbortMatch()
        {if(Current?.stage!=RunStage.Battle)return false;Current.stage=RunStage.Prepare;Save();return true;}
        public static void ClearSession()=>Current=null;
        public static bool CanResume=>ReadSaved()!=null;
        public static bool Resume()
        {
            Current=ReadSaved();if(Current==null)return false;
            if(Current.stage==RunStage.Battle)Current.stage=RunStage.Prepare;
            Save();AchievementStore.Change(p=>p.RecordRun(Current),true);return true;
        }
        public static void ReconcileSavedRewards()
        {var s=ReadSaved(true);if(s!=null)AchievementStore.Change(p=>p.RecordRun(s),true);}
        static bool Finite(float f)=>!float.IsNaN(f) && !float.IsInfinity(f);
        static bool ValidBuild(FighterBuildData b)=>b!=null && Enum.IsDefined(typeof(FontStyleId),b.fontType)
            && Enum.IsDefined(typeof(WeaponSize),b.weaponSize) && Enum.IsDefined(typeof(GripType),b.gripType)
            && Enum.IsDefined(typeof(BattleStyle),b.battleStyle) && Enum.IsDefined(typeof(WeaponFlip),b.weaponFlip)
            && Finite(b.gripPosition) && Finite(b.gripPoint.x) && Finite(b.gripPoint.y)
            && FighterCustomizer.IsAvailable(b.character,b.fontType,out _);
        static RunSession ReadSaved(bool includeFinished=false)
        {
            try
            {
                var s=JsonUtility.FromJson<RunSession>(PlayerPrefs.GetString(PrefKey,""));
                if(s==null || s.version!=1 || string.IsNullOrEmpty(s.id) || s.round<1 || s.round>5
                    || !Enum.IsDefined(typeof(RunStage),s.stage) || (!includeFinished && s.Finished)
                    || !Finite(s.hpShare) || s.hpShare<0 || s.hpShare>1 || (!s.Finished && s.hpShare<=0)
                    || !ValidBuild(s.player) || s.history==null || s.history.Count>1000
                    || s.upgrades==null || s.upgrades.Length!=RunCatalog.Upgrades.Length
                    || (!string.IsNullOrEmpty(s.lockedGlyph) && s.player.character!=s.lockedGlyph))return null;
                int upgrades=0,wins=0,lastRound=1;
                for(int i=0;i<s.upgrades.Length;i++){if(s.upgrades[i]<0 || s.upgrades[i]>RunCatalog.Upgrades[i].Limit)return null;upgrades+=s.upgrades[i];}
                if(upgrades!=s.round-1)return null;
                foreach(var r in s.history)
                {
                    if(r==null || r.round<lastRound || r.round>s.round || r.winner< -1 || r.winner>1
                        || !Finite(r.seconds) || !Finite(r.hp) || !Finite(r.maxHp) || r.seconds<0 || r.maxHp<=0 || r.hp<0 || r.hp>r.maxHp+.01f)return null;
                    if(r.winner==0)wins++;lastRound=r.round;
                }
                bool needsOffers=s.stage==RunStage.Upgrade || (s.stage==RunStage.Result && s.history.Count>0 && s.history[s.history.Count-1].winner==0);
                if((s.stage==RunStage.Result || s.stage==RunStage.Upgrade || s.Finished) && s.history.Count==0)return null;
                if(needsOffers)
                {
                    if(s.offers==null || s.offers.Length!=3 || new HashSet<int>(s.offers).Count!=3)return null;
                    foreach(int id in s.offers)if(RunCatalog.Upgrade(id)==null || s.upgrades[id]>=RunCatalog.Upgrade(id).Limit)return null;
                    if(s.selectedUpgrade!=-1 && Array.IndexOf(s.offers,s.selectedUpgrade)<0)return null;
                }
                if(wins>s.round || (!s.Finished && wins<s.round-1))return null;
                return s;
            }
            catch(Exception){return null;}
        }
        static void Save(){PlayerPrefs.SetString(PrefKey,JsonUtility.ToJson(Current));PlayerPrefs.Save();}
        public static void ApplyCombat(Fighter f,bool player)
        {
            var s=Current;if(s==null)return;
            var bonus=player?s.Bonuses:s.Opponent.Bonuses;
            var mods=f.Mods;mods.attackSpeed*=1+bonus.attackSpeed;mods.moveSpeed*=1+bonus.moveSpeed;
            mods.damage*=1+bonus.damage;mods.knockbackResistance*=1+bonus.resistance;f.Mods=mods;
            f.WalkSpeed*=1+bonus.moveSpeed;
            var stats=f.Stats;stats.defense=Mathf.RoundToInt(stats.defense*(1+bonus.defense));stats.maxHp*=1+bonus.maxHp;f.Stats=stats;
            f.Runtime.maxHp=stats.maxHp;f.Runtime.hp=stats.maxHp*(player?s.hpShare:1);
        }
        public static void ApplyInterventions(BattleIntervention iv)
        {
            if(iv==null || Current==null)return;
            for(int i=0;i<2;i++)
            {
                var bonus=i==0?Current.Bonuses:Current.Opponent.Bonuses;var side=iv.Side(i);
                side.gaugeRateScale=1+bonus.gaugeRate;side.cheerRecoveryScale=1+bonus.cheerRecovery;
                side.specialDamageScale=1+bonus.specialDamage;side.cheerDamageScale=1+bonus.cheerDamage;
                side.braceDamageScale=1-bonus.braceReduction;
            }
        }
    }
}
