using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed partial class CustomizeScreen
    {
        GameObject stagePanel;
        StageSelectionScreen stageScreen;
        RectTransform stageVsCard;
        public bool StageVisible => stagePanel!=null && stagePanel.activeSelf;
        bool UsesStageSelection => !tournament && !worldCup && !run && !draft && !challenge && !friend && !practice;
        void ShowStageOrVs() { if(UsesStageSelection)ShowStages();else ShowVs(); }
        public void ShowStages()
        {
            if(!UsesStageSelection || !battle.button.interactable)return;
            CancelGripDrag();preview.EndGripEditing();
            if(stagePanel!=null){stagePanel.SetActive(false);Destroy(stagePanel);}
            stagePanel=RetroUi.Group(menuRoot,"StagePanel",0,0,1920,1080).gameObject;
            stageScreen=stagePanel.AddComponent<StageSelectionScreen>();
            stageScreen.Build(stagePanel.transform,()=>{stagePanel.SetActive(false);ShowVs();},HideStages,customizer.Get(0).character,customizer.Get(1).character);
            vsPanel.SetActive(false);preview.gameObject.SetActive(false);previewCamera.enabled=false;
            stagePanel.transform.SetAsLastSibling();RetroUi.Enter(stagePanel,0,16);
        }
        public void HideStages()
        { if(stagePanel!=null)stagePanel.SetActive(false);preview.gameObject.SetActive(true);previewCamera.enabled=true; }
        void BuildStageVs(Transform parent)
        {
            var root=StageUi.Surface(parent,"StageVsDesign");StageUi.Brand(root);
            WorldCupDesign.Button(root,"StageVsBack","← ステージ変更",35,842,380,70,ShowStages,null,30);
            StageUi.Text(root,"StageVsStep","04 / MATCH PREVIEW",35,30,440,50,28);
            StageUi.Text(root,"StageVsHeading","覚悟はできたか？",395,42,865,85,62,null,TextAnchor.MiddleCenter);
            StageUi.Text(root,"VsTitle","VS",660,226,352,250,150,null,TextAnchor.MiddleCenter);
            vsLeftGlyph=StageUi.Text(root,"VsLeftGlyph","",65,162,530,260,205,RetroUi.Red,TextAnchor.MiddleCenter);
            vsRightGlyph=StageUi.Text(root,"VsRightGlyph","",1078,162,530,260,205,RetroUi.Blue,TextAnchor.MiddleCenter);
            vsLeft=StageUi.Text(root,"VsLeft","",70,425,525,168,25,RetroUi.Red,TextAnchor.UpperCenter);
            vsRight=StageUi.Text(root,"VsRight","",1075,425,525,168,25,RetroUi.Blue,TextAnchor.UpperCenter);
            stageVsCard=RetroUi.Group(root,"VsStageCard",35,625,1602,185);
            WorldCupDesign.Button(root,"Start","BATTLE 開始 →",465,836,710,80,StartBattle,RetroUi.Red,48);
            var picker=RetroUi.Group(root,"StagePlayModePicker",1205,850,425,65);picker.localScale=Vector3.one*.72f;PlayModePicker.Build(picker,0,0);
        }
        void RefreshStageVs()
        {
            if(stageVsCard==null)return;
            foreach(Transform child in stageVsCard){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var def=StageCatalog.Get(StageSession.Selected);
            StageUi.Preview(stageVsCard,def.Id,490,0,1112,185,false);
            WorldCupDesign.Frame(stageVsCard,0,0,1602,185,RetroUi.Ink,2);
            StageUi.Text(stageVsCard,"VsStageNumber",$"STAGE {(int)def.Id+1:00}",25,10,440,42,28);
            StageUi.Text(stageVsCard,"VsStageName",def.Name,25,53,440,67,def.Id==BattleStageId.Exam?29:40);
            StageUi.Text(stageVsCard,"VsStageDescription",def.Description,25,125,450,45,21);
        }
        void StyleStageCustomize()
        {
            if(!UsesStageSelection)return;
            foreach(var t in menuRoot.GetComponentsInChildren<Text>(true))
                if(t.fontSize>=27 && t!=charInput.textComponent){t.font=WorldCupDesign.HeadingFont;t.fontStyle=FontStyle.Normal;}
            battle.label.text="ステージ選択へ →";
            var scene=RetroUi.Group(menuRoot,"CustomizeStageBackdrop",96,380,676,PreviewHeight).gameObject.AddComponent<RawImage>();
            scene.texture=StageCatalog.Get(BattleStageId.Calligraphy).Texture;scene.color=new Color(1,1,1,.5f);scene.raycastTarget=false;
            scene.transform.SetSiblingIndex(previewViewport.transform.GetSiblingIndex());
            if(StageSession.OpenOnCustomize){StageSession.OpenOnCustomize=false;ShowStages();}
        }
    }
}
