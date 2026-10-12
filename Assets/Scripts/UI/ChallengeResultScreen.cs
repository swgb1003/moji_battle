using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed class ChallengeResultScreen : MonoBehaviour
    {
        Canvas canvas;
        public void Build(ChallengeOutcome outcome)
        {
            var d=outcome.Definition; var r=outcome.Match;
            var root=RetroUi.Screen("ChallengeResultCanvas",out canvas,12); canvas.transform.SetParent(transform,false);
            RetroUi.Header(root,"CHALLENGE / RESULT",GameFlow.ToChallenges);
            root.Find("Back").GetComponentInChildren<Text>().text="← お題一覧";
            RetroUi.Button(root,"ChallengeResultSettings","⚙ 設定",1260,55,220,60,30,SettingsScreen.Open,RetroUi.Ink,false);
            RetroUi.Label(root,"ResultChallengeName",$"#{d.Id+1:00}　{d.Name}",640,115,640,48,27,null,TextAnchor.MiddleCenter);
            // Leave room for CLEAR; otherwise center the visible ink, compensating for the final full-width period's blank space.
            float titleX=outcome.Cleared?425:526;
            RetroUi.Label(root,"ChallengeResultTitle",outcome.Cleared?"お題クリア！":"再び、挑め。",titleX,150,930,133,102,RetroUi.Red,TextAnchor.MiddleCenter);
            if(outcome.Cleared)ChallengeScreen.Stamp(root,"CLEAR",1320,171,220,90);
            var left=ChallengeScreen.Panel(root,"ChallengeResultMatch",80,294,884,556);
            ChallengeScreen.Tag(left,"01",18,18,82);
            RetroUi.Label(left,"MatchResultHeading","対戦結果",134,18,728,74,51,null,TextAnchor.MiddleCenter);
            RetroUi.Block(left,"MatchHeadingRule",28,98,828,2,RetroUi.Ink);
            Pose(left,38,126,.9f,d.PlayerGlyph,RetroUi.Red,r.winner==0);
            Pose(left,666,126,.9f,d.OpponentGlyph,RetroUi.Blue,r.winner==1);
            var verdict=RetroUi.Label(left,"ChallengeVictoryCaption",r.winner<0?"":"あなたの",280,142,350,58,44,r.winner==0?RetroUi.Red:RetroUi.Muted,TextAnchor.MiddleCenter);
            string win=r.winner==0?"勝利":r.winner<0?"引き分け":"敗北";
            RetroUi.Label(left,"ChallengeVictory",win,280,205,350,140,r.winner<0?66:104,r.winner==0?RetroUi.Red:RetroUi.Muted,TextAnchor.MiddleCenter);
            RetroUi.Block(left,"ResultGround",36,362,812,3,RetroUi.Ink);
            RetroUi.Block(left,"ResultStatsRule",28,382,828,1,RetroUi.Ink);
            RetroUi.Label(left,"ChallengeElapsed",$"決着時間　{r.elapsedSeconds:F1} 秒",44,400,392,70,34);
            RetroUi.Block(left,"ResultStatsDivider",450,400,1,60,RetroUi.Rule);
            RetroUi.Label(left,"ChallengeRemainingHp",$"残りHP　{Mathf.CeilToInt(r.hpRemaining[0])} / {Mathf.CeilToInt(r.maxHp[0])}",476,400,366,70,33);
            RetroUi.Block(left,"ResultChecksRule",28,482,828,1,RetroUi.Ink);
            string buildCondition=d.FixedCharacter!=null?"文字「"+d.FixedCharacter+"」を使用":d.FixedSize.HasValue?"Sサイズを使用":"構成の条件";
            Check(left,46,500,buildCondition,outcome.BuildMet);
            string goalCondition=d.Goal==ChallengeGoal.SpecialFinish?"必殺技でとどめ":d.Goal==ChallengeGoal.CheerVictory?"声援を使って勝利":"相手に勝利";
            Check(left,458,500,goalCondition,r.winner==0 && outcome.GoalMet);
            var right=ChallengeScreen.Panel(root,"ChallengeResultReward",990,294,854,556);
            ChallengeScreen.Tag(right,"02",18,18,82);
            RetroUi.Label(right,"RewardHeading",outcome.Cleared?"初回クリア報酬":"次の挑戦へ",122,18,708,74,49,null,TextAnchor.MiddleCenter);
            RetroUi.Block(right,"RewardRule",28,98,798,2,RetroUi.Ink);
            if(outcome.Cleared)
            {
                AchievementBadge.Create(right,d.RewardId,true,42,125,314);
                if(outcome.NewReward)ChallengeScreen.Stamp(right,"NEW",433,145,116,54);
                RetroUi.Label(right,"ChallengeRewardCaption","称号",434,220,370,42,30);
                var rewardName=RetroUi.Label(right,"ChallengeRewardName","「"+AchievementCatalog.Get(d.RewardId).Name+"」",377,266,450,78,48);
                rewardName.resizeTextForBestFit=true; rewardName.resizeTextMinSize=28; rewardName.resizeTextMaxSize=48;
                RetroUi.Label(right,"ChallengeRewardStatus",outcome.NewReward?"獲得しました。":"獲得済みです。",434,346,370,48,32);
            }
            else
            {
                RetroUi.Label(right,"ChallengeFailureReason",r.winner==0?"勝利！ あと一歩でお題達成。":"構成を工夫して、再挑戦しよう。",44,145,766,60,32);
                var requirement=RetroUi.Label(right,"ChallengeFailureCondition","クリア条件："+d.Condition,44,228,766,88,35);
                requirement.resizeTextForBestFit=true; requirement.resizeTextMinSize=25; requirement.resizeTextMaxSize=35;
                RetroUi.Label(right,"ChallengeFailureHint",d.Hint,44,330,766,88,27,RetroUi.Muted);
            }
            RetroUi.Block(right,"ChallengeCountRule",28,410,798,2,RetroUi.Ink);
            RetroUi.Label(right,"ChallengeResultProgressCaption","お題クリア",44,435,440,50,29);
            RetroUi.Label(right,"ChallengeResultCount",$"{ChallengeRuntimeStore.ClearedCount} / {ChallengeCatalog.All.Length}",588,432,218,54,40,null,TextAnchor.MiddleRight);
            RetroUi.Frame(right,44,492,766,42,RetroUi.Ink,2);
            RetroUi.Block(right,"ChallengeResultProgress",48,496,758*ChallengeRuntimeStore.ClearedCount/(float)ChallengeCatalog.All.Length,34,RetroUi.Red);
            RetroUi.Button(root,"ChallengeRetryBuild","構成を変えて再挑戦",80,866,884,78,34,GameFlow.ToChallengePrepare,shadow:false);
            var next=RetroUi.Button(root,"ChallengeNext",ChallengeRuntimeStore.NextId>=0?"次のお題へ →":"お題一覧へ →",990,866,854,78,36,
                ()=>{int id=ChallengeRuntimeStore.NextId;if(id>=0)GameFlow.ToChallengeDetail(id);else GameFlow.ToChallenges();},RetroUi.Red);
            next.button.interactable=outcome.Cleared;
            if(!outcome.Cleared)next.label.text="クリアして次のお題へ";
            RetroUi.Button(root,"ChallengeRematch","もう一度戦う",80,970,884,54,30,()=>FindFirstObjectByType<BattleBootstrap>()?.RematchSameBuild(),shadow:false);
            RetroUi.Button(root,"ChallengeResultList","お題一覧へ",990,970,854,54,30,GameFlow.ToChallenges,shadow:false);
            RetroUi.Enter(root.gameObject,0,0);
        }
        static void Check(Transform root,float x,float y,string text,bool complete)
        {
            RetroUi.Block(root,"CheckPlate",x,y,44,42,complete?RetroUi.Red:RetroUi.Wash);
            RetroUi.Label(root,"CheckIcon",complete?"✓":"—",x,y,44,42,30,complete?RetroUi.Paper:RetroUi.Muted,TextAnchor.MiddleCenter);
            var label=RetroUi.Label(root,"CheckText",text,x+58,y,320,42,28);
            label.resizeTextForBestFit=true;label.resizeTextMinSize=20;label.resizeTextMaxSize=28;
        }
        internal static void Pose(Transform parent,float x,float y,float scale,string glyph,Color team,bool winning)
        {
            var panel=RetroUi.Group(parent,winning?"WinningFighter":"DefeatedFighter",x,y,250*scale,260*scale);
            void Dot(float dx,float dy,float radius)
            {var dot=RetroUi.Block(panel,"Joint",(dx-radius)*scale,(dy-radius)*scale,2*radius*scale,2*radius*scale,RetroUi.Ink);dot.sprite=UiKit.DiscSprite;}
            void Line(float ax,float ay,float bx,float by)
            {RetroUi.Line(panel,new Vector2(ax,ay)*scale,new Vector2(bx,by)*scale,RetroUi.Ink,9*scale);Dot(ax,ay,4.5f);Dot(bx,by,4.5f);}
            if(winning)
            {
                Dot(110,80,18);Line(110,105,110,155);Line(110,155,50,250);Line(110,155,180,250);
                Line(110,105,70,60);Line(70,60,55,20);Line(110,105,150,65);Line(150,65,165,20);
            }
            else
            {
                Dot(66,150,18);Line(78,173,112,198);Line(78,173,58,219);Line(58,219,38,250);
                Line(83,177,118,230);Line(118,230,152,250);Line(112,198,152,230);Line(152,230,206,250);
            }
            var weapon=RetroUi.Label(panel,"ResultWeapon",glyph,(winning?15:80)*scale,(winning?-65:15)*scale,195*scale,160*scale,
                Mathf.RoundToInt(145*scale),team,TextAnchor.MiddleCenter);
            weapon.font=GlyphFontLibrary.Default?.Get(FontStyleId.Brush)?.font ?? RetroUi.Font;
            if(!winning)weapon.rectTransform.localRotation=Quaternion.Euler(0,0,-12);
        }
    }
}
