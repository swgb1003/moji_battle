using System;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public static class StageUi
    {
        public static RectTransform Surface(Transform parent,string name)
        {
            var root=RetroUi.Group(parent,name,0,0,1672,941);root.localScale=Vector3.one*(1920f/1672f);
            var blocker=root.gameObject.AddComponent<Image>();blocker.color=RetroUi.Paper;
            WorldCupDesign.Art(root,"design_paper",0,0,1672,941,false);
            WorldCupDesign.Frame(root,12,12,1648,917,RetroUi.Ink,2);
            return root;
        }
        public static Text Text(Transform p,string name,string value,float x,float y,float w,float h,int size=28,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft)
            => WorldCupDesign.Text(p,name,value,x,y,w,h,size,color,align,true);
        public static void Brand(Transform root)
        {
            Text(root,"StageBrand","MOJI Fighter",1320,24,245,60,32,null,TextAnchor.MiddleRight);
            Text(root,"StageBrandX","X",1574,13,55,82,65,RetroUi.Red,TextAnchor.MiddleCenter);
        }
        public static void Progress(Transform root,int current)
        {
            root=RetroUi.Group(root,"StageProgress",0,0,1920,1080);
            string[] names={"モード","カスタマイズ","ステージ","対戦"};
            for(int i=0;i<4;i++)
            {
                float x=450+i*250;
                RetroUi.Block(root,"FlowPlate",x,1029,226,29,i==current?RetroUi.Red:RetroUi.Wash);
                Text(root,"FlowStep",$"{i+1}  {names[i]}",x,1029,226,29,18,i==current?RetroUi.Paper:RetroUi.Muted,TextAnchor.MiddleCenter);
            }
        }
        public static RectTransform Preview(Transform parent,BattleStageId id,float x,float y,float w,float h,bool fighters=true,string left="火",string right="水")
        {
            var def=StageCatalog.Get(id);var p=RetroUi.Group(parent,"StagePreview"+id,x,y,w,h);
            p.gameObject.AddComponent<RectMask2D>();
            var image=RetroUi.Group(p,"StageArtwork",0,0,w,h).gameObject.AddComponent<RawImage>();image.texture=def.Texture;image.raycastTarget=false;
            if(image.texture!=null)
            {
                float source=image.texture.width/(float)image.texture.height,target=w/h;
                image.uvRect=source>target?new Rect((1-target/source)/2,0,target/source,1):new Rect(0,(1-source/target)*.62f,1,source/target);
            }
            float ground=h*.86f;
            var floor=RetroUi.Group(p,"StagePreviewFloor",-w*.05f,ground,w*1.1f,h*.3f);
            if(id==BattleStageId.Calligraphy)floor.localRotation=Quaternion.Euler(0,0,-4);
            RetroUi.Block(floor,"Floor",0,0,w*1.1f,h*.3f,def.Floor);
            RetroUi.Block(floor,"FloorEdge",0,0,w*1.1f,Mathf.Max(2,h*.012f),RetroUi.Ink);
            if(id==BattleStageId.Calligraphy)
            {
                var ink=RetroUi.Block(floor,"Ink",w*.6f,-h*.012f,w*.22f,h*.034f,RetroUi.Ink);ink.sprite=UiKit.DiscSprite;
                for(int i=0;i<4;i++)RetroUi.Block(floor,"Grain",0,h*(.04f+i*.05f),w*1.1f,1,new Color(.28f,.16f,.08f,.4f));
            }
            if(id==BattleStageId.EraserFactory)
            {
                for(int i=0;i<9;i++){var roller=RetroUi.Block(floor,"Roller",w*i/8,h*.05f,h*.1f,h*.1f,RetroUi.Ink);roller.sprite=UiKit.DiscSprite;}
                var eraser=RetroUi.Group(p,"PreviewEraser",w*.69f,h*.22f,w*.12f,h*.38f);
                var sprite=eraser.gameObject.AddComponent<RawImage>();sprite.texture=Resources.Load<Texture2D>("UI/Stages/prop_eraser");sprite.raycastTarget=false;
            }
            if(id==BattleStageId.Dictionary)for(int i=0;i<5;i++)RetroUi.Block(floor,"Page",0,h*(.03f+i*.035f),w*1.1f,1,RetroUi.Rule);
            if(id==BattleStageId.Letterpress)
            {
                var type=RetroUi.Group(p,"TypeFace",w*.45f,h*.58f,h*.28f,h*.28f).gameObject.AddComponent<RawImage>();
                type.texture=Resources.Load<Texture2D>("UI/Stages/prop_type");type.raycastTarget=false;
            }
            if(id==BattleStageId.Exam)
            {
                Text(p,"Circle","○",w*.14f,h*.62f,w*.22f,h*.28f,Mathf.RoundToInt(h*.25f),RetroUi.Red,TextAnchor.MiddleCenter);
                Text(p,"Cross","×",w*.66f,h*.62f,w*.22f,h*.28f,Mathf.RoundToInt(h*.25f),RetroUi.Red,TextAnchor.MiddleCenter);
            }
            if(fighters)
            {
                float scale=h*.43f/420;
                RetroUi.Fighter(p,w*.17f,ground-420*scale,scale,left,RetroUi.Red);
                RetroUi.Fighter(p,w*.70f,ground-420*scale,scale,right,RetroUi.Blue,true);
            }
            return p;
        }
    }

    public sealed class StageSelectionScreen : MonoBehaviour
    {
        RectTransform root,preview;
        Text title,description,traits;
        readonly RectTransform[] frames=new RectTransform[6];
        Action confirm,back;
        string left,right;
        public void Build(Transform parent,Action onConfirm,Action onBack,string l="火",string r="水")
        {
            confirm=onConfirm;back=onBack;left=l;right=r;
            root=StageUi.Surface(parent,"StageSelectionDesign");StageUi.Brand(root);
            StageUi.Text(root,"StageStep","03 / STAGE SELECT",42,32,430,60,31);
            StageUi.Text(root,"StageHeading","戦う場所を選ぶ",460,25,800,80,56,null,TextAnchor.MiddleCenter);
            WorldCupDesign.Block(root,"HeadingStroke",535,105,640,4,RetroUi.Red);
            for(int i=0;i<6;i++)
            {
                int index=i;float x=38+i%3*364,y=145+i/3*332;
                var card=WorldCupDesign.Button(root,"StageCard"+i,"",x,y,350,316,()=>Select((BattleStageId)index),null,20,false);
                var art=StageUi.Preview(card.image.transform,(BattleStageId)i,8,64,334,244);
                StageUi.Text(card.image.transform,"StageName",StageCatalog.All[i].Name,8,8,334,50,i==5?23:30,null,TextAnchor.MiddleCenter);
                var frame=RetroUi.Group(card.image.transform,"SelectedFrame",0,0,350,316);
                RetroUi.Frame(frame,0,0,350,316,RetroUi.Red,5);
                frames[i]=frame;
            }
            title=StageUi.Text(root,"SelectedStageName","",1145,540,482,83,43,null,TextAnchor.MiddleCenter);
            description=StageUi.Text(root,"SelectedStageDescription","",1140,637,490,95,26,null,TextAnchor.MiddleCenter);
            traits=StageUi.Text(root,"SelectedStageTraits","",1145,745,480,55,26,RetroUi.Blue,TextAnchor.MiddleCenter);
            WorldCupDesign.Button(root,"StageBack","← カスタマイズ",38,835,338,72,()=>back(),null,30);
            StageUi.Text(root,"StageMatchup",$"P1  {l}   VS   {r}  P2",397,840,540,66,44,null,TextAnchor.MiddleCenter);
            WorldCupDesign.Button(root,"RandomStage","⚄ おまかせ",967,835,232,72,()=>Select((BattleStageId)UnityEngine.Random.Range(0,6)),null,27);
            WorldCupDesign.Button(root,"ConfirmStage","このステージで対戦 →",1220,835,413,72,()=>confirm(),RetroUi.Red,33);
            Select(StageSession.Selected);
        }
        public void Select(BattleStageId id)
        {
            StageSession.Selected=id;var def=StageCatalog.Get(id);
            for(int i=0;i<frames.Length;i++)frames[i].gameObject.SetActive(i==(int)id);
            if(preview!=null){preview.gameObject.SetActive(false);Destroy(preview.gameObject);}
            preview=StageUi.Preview(root,id,1138,153,490,379,true,left,right);
            WorldCupDesign.Frame(preview,0,0,490,379,RetroUi.Ink,2);
            title.text=def.Name;description.text=def.Description;traits.text=def.Traits;
        }
    }
}
