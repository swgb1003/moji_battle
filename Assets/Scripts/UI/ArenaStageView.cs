using UnityEngine;

namespace MojiBattle
{
    /// <summary>World-space stage geometry stays aligned with physical surfaces as the fight camera tracks.</summary>
    public sealed class ArenaStageView : MonoBehaviour
    {
        ArenaStage stage;
        MatchDirector director;
        Transform floor, hazard;
        Transform pen;
        SpriteRenderer[] beltMarks;
        LineRenderer warning, heal, slash, page;
        readonly Vector3[] points=new Vector3[33];
        static readonly Color Amber=new Color32(222,148,33,255);
        SpriteRenderer Block(Transform root,string name,Vector2 p,Vector2 size,Color color,int order=-4)
        {
            var b=UiKit.Rect(root,name,color,order);b.transform.localPosition=p;b.transform.localScale=new Vector3(size.x,size.y,1);return b;
        }
        public void Bind(ArenaStage s,MatchDirector d)
        {
            stage=s;director=d;float w=d.Context.ArenaHalfWidth*2+4;
            floor=new GameObject("StageFloorArt").transform;floor.SetParent(transform,false);floor.localPosition=new Vector2(0,-1);
            var definition=StageCatalog.Get(s.Id);
            Block(floor,"FloorEdge",Vector2.zero,new Vector2(w,2),FighterFactory.Ink,-8);
            Block(floor,"FloorFace",new Vector2(0,-.02f),new Vector2(w-.1f,1.94f),definition.Floor,-7);
            if(s.Id==BattleStageId.Calligraphy)
            {
                for(int i=0;i<7;i++)Block(floor,"WoodGrain",new Vector2(0,-.75f+i*.25f),new Vector2(w,.015f),new Color(.25f,.15f,.08f,.5f),-6);
                var ink=Block(floor,"InkPuddle",new Vector2(w*.12f,.98f),new Vector2(4.8f,.19f),FighterFactory.Ink,-3);ink.sprite=UiKit.DiscSprite;
            }
            if(s.Id==BattleStageId.EraserFactory)
            {
                beltMarks=new SpriteRenderer[17];
                for(int i=0;i<beltMarks.Length;i++)
                {
                    var roller=Block(floor,"ConveyorRoller",new Vector2(-w/2+i*1.5f,-.2f),new Vector2(.7f,.7f),FighterFactory.Ink,-6);roller.sprite=UiKit.DiscSprite;
                    beltMarks[i]=Block(floor,"BeltStripe",new Vector2(-w/2+i*1.5f,.88f),new Vector2(.22f,.12f),RetroUi.Wash,-5);
                }
            }
            if(s.Id==BattleStageId.Dictionary)
            {
                for(int i=0;i<10;i++)Block(floor,"PageEdge",new Vector2(0,-.8f+i*.17f),new Vector2(w-.15f,.02f),new Color(.44f,.34f,.21f,.65f),-6);
                page=UiKit.Line(transform,"TurningPage",new Color(.94f,.87f,.7f,.95f),.22f,-2);page.positionCount=points.Length;
            }
            if(s.Id==BattleStageId.Exam)
            {
                for(int i=0;i<40;i++)Block(floor,"ExamGrid",new Vector2(-w/2+i*.6f,0),new Vector2(.012f,1.8f),new Color(.58f,.4f,.33f,.4f),-6);
                heal=UiKit.Line(transform,"HealingCircle",RetroUi.Red,.07f,-2,true);heal.positionCount=points.Length;
                slash=UiKit.Line(transform,"RedPenSlash",RetroUi.Red,.18f,6);slash.positionCount=4;
                pen=Prop(transform,"GradingPen","prop_pen",new Vector2(5,5),5).transform;
            }
            if(s.Id==BattleStageId.EraserFactory || s.Id==BattleStageId.Letterpress)
            {
                hazard=new GameObject("FallingStagePropArt").transform;hazard.SetParent(transform,false);
                bool eraser=s.Id==BattleStageId.EraserFactory;
                Vector2 size=eraser?new Vector2(1.5f,2.1f):new Vector2(1.6f,1.6f);
                Prop(hazard,"FallingPropSprite",eraser?"prop_eraser":"prop_type",size,4);
            }
            warning=UiKit.Line(transform,"StageWarningZone",Amber,.07f,-1);warning.positionCount=5;
            Draw();
        }
        static SpriteRenderer Prop(Transform parent,string name,string asset,Vector2 size,int order)
        {
            var sr=UiKit.Rect(parent,name,Color.white,order);sr.sprite=Resources.Load<Sprite>("UI/Stages/"+asset);
            if(sr.sprite==null)throw new System.InvalidOperationException("Missing stage sprite: "+asset);
            sr.transform.localScale=new Vector3(size.x/sr.sprite.bounds.size.x,size.y/sr.sprite.bounds.size.y,1);
            return sr;
        }
        public void Draw()
        {
            if(stage==null || floor==null)return;
            floor.rotation=Quaternion.Euler(0,0,stage.Tilt);
            bool danger=stage.Id==BattleStageId.Exam || stage.Id==BattleStageId.EraserFactory || stage.Id==BattleStageId.Letterpress;
            warning.enabled=danger && stage.Warning;
            if(warning.enabled)
            {
                float x=stage.HazardX,r=stage.Id==BattleStageId.Exam?1.8f:1.1f;
                warning.SetPosition(0,new Vector3(x-r,.09f));warning.SetPosition(1,new Vector3(x-r,.32f));warning.SetPosition(2,new Vector3(x+r,.32f));warning.SetPosition(3,new Vector3(x+r,.09f));warning.SetPosition(4,new Vector3(x-r,.09f));
            }
            if(hazard!=null){hazard.gameObject.SetActive(stage.Warning || stage.ObstacleActive);hazard.localPosition=new Vector2(stage.HazardX,stage.Warning?8:stage.HazardY);}
            if(beltMarks!=null)
            {
                float w=director.Context.ArenaHalfWidth*2+4;
                for(int i=0;i<beltMarks.Length;i++)beltMarks[i].transform.localPosition=new Vector2(-w/2+Mathf.Repeat(i*1.5f+director.Clock*stage.ConveyorDirection,w),.88f);
            }
            if(heal!=null)
            {
                for(int i=0;i<points.Length;i++){float a=i*Mathf.PI*2/(points.Length-1);points[i]=new Vector3(stage.HealingX+Mathf.Cos(a)*1.65f,.08f+Mathf.Sin(a)*.12f);}
                heal.SetPositions(points);
                bool striking=stage.Phase>=3 && stage.Phase<3.45f;slash.enabled=striking;
                float x=stage.HazardX;slash.SetPosition(0,new Vector3(x-1,2.8f));slash.SetPosition(1,new Vector3(x+1,.12f));slash.SetPosition(2,new Vector3(x+1,2.8f));slash.SetPosition(3,new Vector3(x-1,.12f));
                pen.gameObject.SetActive(stage.Phase<3.45f);
                pen.localPosition=new Vector2(x+2.35f,stage.Warning?3.5f:2.45f);
            }
            if(page!=null)
            {
                float p=Mathf.Clamp01((stage.Phase-2.8f)/1.7f);page.enabled=p>0 && p<1;
                for(int i=0;i<points.Length;i++){float k=i/(float)(points.Length-1);points[i]=new Vector3(-9+k*18,.13f+Mathf.Sin(k*Mathf.PI)*Mathf.Sin(p*Mathf.PI)*5.2f);}
                page.SetPositions(points);
            }
        }
    }
}
