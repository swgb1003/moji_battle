using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed class RunResultScreen : MonoBehaviour
    {
        public void Build()
        {
            var s=RunRuntimeStore.Current;if(s==null || s.history.Count==0)return;
            var last=s.history[s.history.Count-1];bool clear=s.Cleared,finished=s.Finished,won=last.winner==0;
            var root=RetroUi.Screen("RunResultCanvas",out var canvas,12);canvas.transform.SetParent(transform,false);
            RunUi.Header(root,finished?clear?"CLEAR":"DEFEAT":"RESULT",GameFlow.ToModeSelect);
            RunUi.Title(root,finished?clear?"五連戦、制覇！":"挑戦、ここまで。":won?$"第{s.round}戦 突破！":"引き分け！");
            RunUi.Progress(root,540,292,840,s.round,s.Wins,finished&&!clear);
            var left=DraftUi.Panel(root,"RunResultMatch",76,408,864,470);
            var right=DraftUi.Panel(root,"RunResultGrowth",970,408,874,470);
            ChallengeScreen.Tag(left,"01",18,18,70);ChallengeScreen.Tag(right,"02",18,18,70);
            RetroUi.Label(left,"Heading",finished?"挑戦の記録":"対戦結果",114,18,700,60,38);
            RetroUi.Label(right,"Heading",clear?"育てた一文字":finished?"次の挑戦へ":"今回の成長",114,18,730,60,38);
            if(clear)
            {
                var laurel=RetroUi.Group(left,"RunClearLaurel",284,93,280,250).gameObject.AddComponent<ChallengeBadgeGraphic>();laurel.color=RetroUi.Rule;
                ChallengeResultScreen.Pose(left,305,128,.82f,s.player.character,RetroUi.Red,true);
                ChallengeScreen.Stamp(left,"CLEAR",615,165,170,70);
            }
            else
            {
                ChallengeResultScreen.Pose(left,42,108,.95f,s.player.character,RetroUi.Red,won);
                ChallengeResultScreen.Pose(left,573,108,.95f,last.opponent,RetroUi.Blue,last.winner==1);
            }
            foreach(var t in left.GetComponentsInChildren<Text>())if(t.name=="ResultWeapon")t.font=RetroUi.Font;
            if(!clear)DraftUi.Title(left,"RunVerdict",last.winner<0?"引分":won?"勝利":"敗北",290,142,280,120,78,won?RetroUi.Red:RetroUi.Muted);
            RetroUi.Block(left,"Ground",30,354,804,2,RetroUi.Ink);
            RetroUi.Label(left,"Elapsed",finished?$"突破　{s.Wins} / 5戦　　合計 {s.Seconds:F1}秒":$"決着時間　{last.seconds:F1}秒",28,365,808,46,31);
            // After a win the rest recovery is already applied; show the battle-end HP alongside it.
            bool rested=won && !finished;
            RetroUi.Label(left,"Remaining",$"残りHP　{Mathf.CeilToInt(rested?last.hp:s.Hp)} / {Mathf.CeilToInt(s.MaxHp)}"+(rested?$"　→　休息後 {Mathf.CeilToInt(s.Hp)}":""),28,416,808,40,31);
            RetroUi.Label(right,"Growth",RunUi.Perks(s),30,108,814,148,32);
            RetroUi.Block(right,"Rule",28,282,818,2,RetroUi.Ink);
            string note=clear?"称号「五連の覇者」\n一文字を育て、五連戦を突破した。":finished?"回復と強化を使い分けて、再び挑もう。\n相手に合わせた構成の調整も勝利の鍵。":won?"次は3つの候補から、強化をひとつ選択。\n休息でHPが最大値の半分回復します。":"勝数はそのまま。同じ相手と再戦します。\nHPは引き継ぎます。";
            RetroUi.Label(right,"NextNote",note,30,311,814,126,30,null);
            if(finished)
            {
                RetroUi.Button(root,"RunAgain","新しく挑戦 →",350,940,740,82,42,GameFlow.ToRunIntro,RetroUi.Red);
                RetroUi.Button(root,"RunResultMenu","メニューへ",1160,940,410,82,35,GameFlow.ToModeSelect,shadow:false);
            }
            else
            {
                RetroUi.Button(root,"RunResultNext",won?"強化を選ぶ →":"同じ相手と再戦 →",350,940,740,82,42,()=>{if(won)GameFlow.ToRunUpgrade();else if(RunRuntimeStore.ReplayDraw())GameFlow.ToRunScreen();},RetroUi.Red);
                RetroUi.Button(root,"RunSuspend","中断してメニューへ",1160,940,490,82,31,GameFlow.ToModeSelect,shadow:false);
            }
        }
    }
}
