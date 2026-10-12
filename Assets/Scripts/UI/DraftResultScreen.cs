using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed class DraftResultScreen : MonoBehaviour
    {
        public void Build()
        {
            var s=DraftRuntimeStore.Current;
            if(s==null || s.stage!=DraftStage.Result || s.history.Count==0)return;
            var r=s.history[s.history.Count-1];
            var root=RetroUi.Screen("DraftResultCanvas",out var canvas,12);canvas.transform.SetParent(transform,false);
            DraftUi.Header(root,"DRAFT / RESULT",GameFlow.ToModeSelect,"← メニュー");
            RetroUi.Label(root,"DraftResultRound",$"第{s.round}ラウンド",590,136,740,56,32,null,TextAnchor.MiddleCenter);
            bool won=s.Finished?s.playerWins==2:r.winner==0;
            string title=r.winner<0?"引き分け！":won?"あなたの勝利！":"COMの勝利！";
            DraftUi.Title(root,"DraftResultTitle",title,330,185,1260,145,120,r.winner<0?RetroUi.Ink:won?RetroUi.Red:RetroUi.Blue);
            var score=DraftUi.Panel(root,"DraftResultScore",346,339,1228,94);
            RetroUi.Block(score,"YouPlate",4,4,258,86,RetroUi.Red);
            RetroUi.Label(score,"You","あなた",4,4,258,86,46,RetroUi.Paper,TextAnchor.MiddleCenter);
            DraftUi.Pips(score,310,27,s.playerWins,RetroUi.Red);
            RetroUi.Label(score,"FinalScore",$"{s.playerWins} - {s.comWins}",465,0,298,94,78,null,TextAnchor.MiddleCenter);
            DraftUi.Pips(score,815,27,s.comWins,RetroUi.Blue);
            RetroUi.Block(score,"ComPlate",966,4,258,86,RetroUi.Blue);
            RetroUi.Label(score,"Com","COM",966,4,258,86,46,RetroUi.Paper,TextAnchor.MiddleCenter);
            if(s.Finished) ChallengeScreen.Stamp(root,won?"ドラフト勝者":"決着",845,450,230,50);
            else RetroUi.Label(root,"DraftNextNote",r.winner<0?"勝数はそのまま。同じ文字で再戦。":"次のラウンドは先攻が交代します",540,448,840,50,29,null,TextAnchor.MiddleCenter);
            var left=DraftUi.Panel(root,"DraftResultMatch",76,515,892,414);
            ChallengeScreen.Tag(left,"01",14,14,76);
            RetroUi.Label(left,"Title","対戦結果",112,14,750,56,38);
            RetroUi.Block(left,"HeadingRule",22,75,848,1,RetroUi.Ink);
            ChallengeResultScreen.Pose(left,150,125,.76f,r.player,RetroUi.Red,r.winner==0);
            ChallengeResultScreen.Pose(left,575,125,.76f,r.com,RetroUi.Blue,r.winner==1);
            RetroUi.Block(left,"Ground",25,315,842,3,RetroUi.Ink);
            RetroUi.Label(left,"Elapsed",$"決着時間　{r.seconds:F1} 秒",24,340,410,54,31,null,TextAnchor.MiddleCenter);
            RetroUi.Block(left,"StatsDivider",445,341,1,51,RetroUi.Rule);
            RetroUi.Label(left,"RemainingHp",$"残りHP　{Mathf.CeilToInt(r.hp)} / {Mathf.CeilToInt(r.maxHp)}",462,340,404,54,31,null,TextAnchor.MiddleCenter);
            var right=DraftUi.Panel(root,"DraftHistory",990,515,854,414);
            ChallengeScreen.Tag(right,"02",14,14,76);
            RetroUi.Label(right,"Title","ドラフトの記録",112,14,718,56,38);
            // Three rounds fit; repeated draws are summarized in the most recent three entries.
            int start=Mathf.Max(0,s.history.Count-3);
            for(int i=start;i<s.history.Count;i++)
            {
                var h=s.history[i];float y=88+(i-start)*88;
                var row=DraftUi.Panel(right,"DraftHistoryRow"+i,24,y,806,78);
                RetroUi.Label(row,"Round",$"第{h.round}ラウンド",14,0,222,78,26);
                DraftUi.Brush(row,"PlayerGlyph",h.player,235,0,125,78,68,RetroUi.Red);
                RetroUi.Label(row,"VS","VS",370,0,65,78,29,null,TextAnchor.MiddleCenter);
                DraftUi.Brush(row,"ComGlyph",h.com,445,0,125,78,68,RetroUi.Blue);
                RetroUi.Block(row,"VerdictPlate",602,14,184,50,h.winner<0?RetroUi.Muted:h.winner==0?RetroUi.Red:RetroUi.Blue);
                RetroUi.Label(row,"Verdict",h.winner<0?"引き分け":h.winner==0?"勝利":"敗北",602,14,184,50,30,RetroUi.Paper,TextAnchor.MiddleCenter);
            }
            RetroUi.Label(right,"HistoryCaption",s.Finished?won?"選んだ文字と構成が、勝利をつかんだ。":"新しい文字で、再び挑もう。":"先に2勝した方がドラフト勝者。",24,355,806,46,28,null,TextAnchor.MiddleCenter);
            string action=s.Finished?"もう一度ドラフト →":r.winner<0?"同じ文字で再戦 →":"次のドラフトへ →";
            RetroUi.Button(root,"DraftNext",action,490,956,650,72,38,s.Finished?(System.Action)GameFlow.ToDraftIntro:GameFlow.NextDraftRound,RetroUi.Red);
            RetroUi.Button(root,"DraftResultMenu","メニューへ",1190,956,360,72,35,GameFlow.ToModeSelect,shadow:false);
            RetroUi.Enter(root.gameObject,0,0);
        }
    }
}
