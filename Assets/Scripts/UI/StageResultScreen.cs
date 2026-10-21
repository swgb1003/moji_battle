using System;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public static class StageResultScreen
    {
        public static GameObject Build(MatchDirector director,Action rematch)
        {
            var safe=RetroUi.Screen("StageResultCanvas",out var canvas,10);
            var root=StageUi.Surface(safe,"StageResultDesign");
            var r=director.Result;int winner=r.winner;var def=StageCatalog.Get(director.Config.stage);
            var leftLoadout=director.Fighters[0].Loadout;var rightLoadout=director.Fighters[1].Loadout;
            string left=FusionUi.Name(leftLoadout),right=FusionUi.Name(rightLoadout);
            var wash=RetroUi.Group(root,"ResultBackdrop",22,120,1628,640).gameObject.AddComponent<RawImage>();
            wash.texture=def.Texture;wash.color=new Color(1,1,1,.25f);wash.raycastTarget=false;
            var title=StageUi.Text(root,"ResultTitle",winner<0?"引き分け":$"{FusionUi.Name(director.Fighters[winner].Loadout)} の勝ち",360,65,1000,168,112,winner<0?RetroUi.Ink:RetroUi.Team(winner),TextAnchor.MiddleCenter);
            title.resizeTextMinSize=44;title.verticalOverflow=VerticalWrapMode.Truncate;title.supportRichText=false;
            StageUi.Text(root,"ResultReason",$"{(r.finishReason.StartsWith("TIME")?"TIME UP":r.finishReason.StartsWith("SUDDEN")?"延長戦":"K.O.")} / {r.elapsedSeconds:0.0}秒",470,232,732,68,42,null,TextAnchor.MiddleCenter);
            FusionUi.Fighter(root,86,390,.75f,leftLoadout,RetroUi.Red).name="ResultFighter0";
            FusionUi.Fighter(root,1305,390,.75f,rightLoadout,RetroUi.Blue,true).name="ResultFighter1";
            var panel=RetroUi.Group(root,"ResultStageSummary",485,342,700,410);
            RetroUi.Block(panel,"SummaryPaper",0,0,700,410,RetroUi.Paper);WorldCupDesign.Frame(panel,0,0,700,410,RetroUi.Ink,2);
            var matchup=StageUi.Text(panel,"ResultMatchup",$"P1 {left}   VS   {right} P2",25,16,650,67,48,null,TextAnchor.MiddleCenter);
            matchup.resizeTextMinSize=20;matchup.verticalOverflow=VerticalWrapMode.Truncate;matchup.supportRichText=false;
            StageUi.Preview(panel,def.Id,20,95,410,230,false);
            StageUi.Text(panel,"RemainingHp",$"残りHP\n{Mathf.CeilToInt(winner<0?Mathf.Max(r.hpRemaining[0],r.hpRemaining[1]):r.hpRemaining[winner])}",451,95,220,110,38,null,TextAnchor.MiddleCenter);
            StageUi.Text(panel,"DamageDealt",$"与ダメージ\n{(winner<0?Mathf.Max(r.metrics[0].damageDealt,r.metrics[1].damageDealt):r.metrics[winner].damageDealt):0}",451,225,220,110,34,null,TextAnchor.MiddleCenter);
            StageUi.Text(panel,"ResultStageName","STAGE / "+def.Name,20,338,660,60,31,null,TextAnchor.MiddleCenter);
            WorldCupDesign.Button(root,"Again","同じ条件で再戦 →",40,790,550,110,rematch,RetroUi.Red,40);
            WorldCupDesign.Button(root,"ChangeStage","ステージを変える →",610,790,510,110,GameFlow.ToStageSelect,RetroUi.Blue,38);
            WorldCupDesign.Button(root,"ToCustomize","カスタマイズへ戻る",1140,790,492,110,GameFlow.ToCustomize,null,37);
            WorldCupDesign.Button(root,"ResultModeSelect","← モード選択",40,35,300,60,GameFlow.ToModeSelect,null,28);
            RetroUi.Enter(root.gameObject,0,18);return canvas.gameObject;
        }
    }
}
