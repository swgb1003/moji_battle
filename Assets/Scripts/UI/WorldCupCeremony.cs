using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed class WorldCupCardFocus : MonoBehaviour,UnityEngine.EventSystems.IPointerEnterHandler,UnityEngine.EventSystems.IPointerExitHandler
    {
        CanvasGroup alpha;
        UguiKit.ChoiceButton button;
        bool focus;
        public void Bind(RectTransform panel,UguiKit.ChoiceButton action)
        {
            PaperUi.Card(panel);
            button=action;var border=RetroUi.Group(panel,"CupHoverBorder",-3,-3,panel.rect.width+6,panel.rect.height+6);
            RetroUi.Frame(border,0,0,panel.rect.width+6,panel.rect.height+6,WorldCupDesign.Red,3);
            alpha=border.gameObject.AddComponent<CanvasGroup>();alpha.alpha=0;alpha.blocksRaycasts=false;
        }
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e){focus=true;}
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e){focus=false;}
        void Update(){if(alpha!=null)alpha.alpha=Mathf.MoveTowards(alpha.alpha,focus?1:0,Time.unscaledDeltaTime*8);}
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorldCupGlobe : MaskableGraphic
    {
        protected override void Awake(){base.Awake();raycastTarget=false;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float w=rectTransform.rect.width,h=rectTransform.rect.height,r=Mathf.Min(w,h)*.43f;var c=new Vector2(w/2,-h/2);
            void L(Vector2 a,Vector2 b,float thickness)
            {
                var n=new Vector2(-(b-a).y,(b-a).x).normalized*thickness/2;int i=vh.currentVertCount;
                foreach(var p in new[]{a+n,b+n,b-n,a-n})vh.AddVert(p,RetroUi.Blue,Vector2.zero);
                vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
            }
            for(int ring=0;ring<3;ring++)for(int i=0;i<96;i++)
            {
                float a=i*Mathf.PI/48,b=(i+1)*Mathf.PI/48;
                float rx=ring==0?r:ring==1?r*.5f:r*.18f;
                L(c+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*r),c+new Vector2(Mathf.Cos(b)*rx,Mathf.Sin(b)*r),2.5f);
            }
            for(int i=-2;i<=2;i++){float y=i*r/3;float span=Mathf.Sqrt(r*r-y*y);L(c+new Vector2(-span,y),c+new Vector2(span,y),2);}
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorldCupAwardFigure : MaskableGraphic
    {
        public float Raise,Walk;public bool Celebrate,Champion;
        protected override void Awake(){base.Awake();color=RetroUi.Ink;raycastTarget=false;}
        public void Pose(float raise,float walk){Raise=raise;Walk=walk;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Vector2 P(float x,float y)=>new Vector2(x,-y);
            void Circle(float x,float y,float r)
            {
                int start=vh.currentVertCount;vh.AddVert(P(x,y),color,Vector2.zero);
                for(int i=0;i<=32;i++){float a=i*Mathf.PI/16;vh.AddVert(P(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r),color,Vector2.zero);}
                for(int i=0;i<32;i++)vh.AddTriangle(start,start+i+1,start+i+2);
            }
            void Line(float ax,float ay,float bx,float by,float thickness=9)
            {
                var a=P(ax,ay);var b=P(bx,by);var n=new Vector2(-(b-a).y,(b-a).x).normalized*thickness/2;int j=vh.currentVertCount;
                foreach(var p in new[]{a+n,b+n,b-n,a-n})vh.AddVert(p,color,Vector2.zero);
                vh.AddTriangle(j,j+1,j+2);vh.AddTriangle(j,j+2,j+3);Circle(ax,ay,thickness/2);Circle(bx,by,thickness/2);
            }
            Circle(75,92,22);Line(75,119,75,184,12);
            Line(75,184,45+Walk,257,10);Line(75,184,108-Walk,257,10);
            if(Celebrate)
            {
                if(Champion){Line(75,123,42,98);Line(42,98,35,45);}
                else {Line(75,123,35,155);Line(35,155,15,175);}
                Line(75,123,108,96);Line(108,96,123,48);return;
            }
            float ly=Mathf.Lerp(175,74,Raise),ry=Mathf.Lerp(175,74,Raise);
            Line(75,123,42,ly);Line(42,ly,45,Mathf.Lerp(205,13,Raise));
            Line(75,123,108,ry);Line(108,ry,107,Mathf.Lerp(205,13,Raise));
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorldCupFireworks : MaskableGraphic
    {
        public float Clock;
        public bool Still,Reduced;
        protected override void Awake(){base.Awake();raycastTarget=false;}
        public void SetClock(float t){Clock=t;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(Reduced || Clock<5.5f)return;
            float w=rectTransform.rect.width,h=rectTransform.rect.height;
            void Dot(float x,float y,float size,Color c)
            {
                if(!Still && x>470 && x<1130 && y<80)return;
                int k=vh.currentVertCount;var center=new Vector2(x,-y);vh.AddVert(center,c,Vector2.zero);
                for(int i=0;i<=8;i++){float a=i*Mathf.PI/4;vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*size,c,Vector2.zero);}
                for(int i=0;i<8;i++)vh.AddTriangle(k,k+i+1,k+i+2);
            }
            void Streak(float x,float y,float angle,float length,float thickness,Color c)
            {
                Vector2 dir=new Vector2(Mathf.Cos(angle),-Mathf.Sin(angle)),n=new Vector2(-dir.y,dir.x)*thickness/2;
                Vector2 center=new Vector2(x,-y),end=center+dir*length;int k=vh.currentVertCount;
                foreach(var v in new[]{center+n,end+n,end-n,center-n})vh.AddVert(v,c,Vector2.zero);
                vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
            }
            for(int burst=0;burst<6;burst++)
            {
                float age=Still?1: .8f+Mathf.Repeat(Clock-5.5f-burst*.15f,1.8f)*.25f;
                float radius=125*age;Color c=burst%3==0?WorldCupDesign.Red:burst%3==1?WorldCupDesign.Navy:WorldCupCeremony.Gold;c.a=.8f;
                float x=w*(.10f+burst*.16f),y=230+(burst%3)*60;
                for(int ray=0;ray<32;ray++)
                {
                    float angle=ray*Mathf.PI/16+.07f*burst,inner=radius*.25f;
                    Streak(x+Mathf.Cos(angle)*inner,y+Mathf.Sin(angle)*inner,angle,radius*.52f,ray%3==0?3:2,c);
                    Streak(x+Mathf.Cos(angle)*radius*.88f,y+Mathf.Sin(angle)*radius*.88f,angle,12,3,c);
                }
            }
            for(int i=0;i<135;i++)
            {
                float x=Mathf.Repeat(i*127.13f,w),y=Still?Mathf.Repeat(i*47.6f,h):Mathf.Repeat(i*47.6f+(Clock-5.5f)*39,h);
                Streak(x+Mathf.Sin(i+Clock)*12,y,i*.63f,8+i%3,5,i%3==0?WorldCupDesign.Red:i%3==1?WorldCupDesign.Navy:WorldCupCeremony.Gold);
            }
        }
    }
    public sealed class WorldCupCeremony : MonoBehaviour
    {
        public static readonly Color Gold=new Color(.75f,.56f,.19f),Silver=new Color(.58f,.61f,.62f),Bronze=new Color(.58f,.34f,.20f);
        readonly RectTransform[] figures=new RectTransform[3],cups=new RectTransform[3];
        readonly WorldCupAwardFigure[] bodies=new WorldCupAwardFigure[3];
        readonly Vector2[] targets=new Vector2[3];
        WorldCupFireworks fireworks;
        CanvasGroup congrats;
        Image flash;
        Text title;RawImage titleLeftArt,titleRightArt;GameObject titleRules;
        RectTransform stage;GameObject podium;CanvasGroup championPlate;Text headerStep;UguiKit.ChoiceButton skip;
        RawImage backdrop;
        UguiKit.ChoiceButton next;
        Action done;
        bool completed,celebrating;
        public float Elapsed {get;private set;}
        public void Build(RectTransform root,WorldCupSession s,Action finished)
        {
            done=finished;backdrop=root.Find("design_ceremony_backdrop").GetComponent<RawImage>();
            WorldCupDesign.Header(root,"WORLD CUP / CEREMONY",Skip);headerStep=root.Find("Step").GetComponent<Text>();
            var titleLeft=WorldCupDesign.Art(root,"design_laurel",272,91,76,118,false);titleLeft.uvRect=new Rect(0,0,.4f,1);
            titleLeftArt=titleLeft;var titleRight=WorldCupDesign.Art(root,"design_laurel",1324,91,76,118,false);titleRight.uvRect=new Rect(.6f,0,.4f,1);titleRightArt=titleRight;
            title=WorldCupDesign.Title(root,"ワールド文字カップ <color=#AE2319>表彰式</color>",101,72,WorldCupDesign.Navy);
            var titleDecor=RetroUi.Group(root,"CeremonyTitleRules",320,91,1012,124);
            titleRules=titleDecor.gameObject;RetroUi.Block(titleDecor,"TitleTopRule",0,0,1012,2,WorldCupDesign.Navy);RetroUi.Block(titleDecor,"TitleBottomLeftRule",0,121,350,2,WorldCupDesign.Navy);RetroUi.Block(titleDecor,"TitleBottomRightRule",662,121,350,2,WorldCupDesign.Navy);
            stage=RetroUi.Group(root,"AwardStage",336,120,1000,710);
            CreateStage(stage,s,figures,bodies,cups,targets,false);
            fireworks=stage.Find("Fireworks").GetComponent<WorldCupFireworks>();podium=stage.Find("design_podium").gameObject;
            var plain=RetroUi.Group(stage,"ChampionSteps",0,0,1000,710);plain.gameObject.SetActive(false);
            float[] px={70,380,690},py={455,396,487};int[] rank={2,1,3};Color[] colors={Silver,Gold,Bronze};
            for(int i=0;i<3;i++){RetroUi.Block(plain,"StepPaper",px[i],py[i],270,583-py[i],WorldCupDesign.Paper);WorldCupDesign.Frame(plain,px[i],py[i],270,583-py[i],colors[i],2);WorldCupDesign.Text(plain,"ChampionRank",rank[i].ToString(),px[i],py[i]+4,270,76,70,colors[i],TextAnchor.MiddleCenter,true);}
            var plate=WorldCupDesign.Panel(root,"ChampionPlaque",620,720,430,95);RetroUi.Block(plate,"ChampionPaper",7,7,416,81,WorldCupDesign.Paper).transform.SetAsFirstSibling();
            championPlate=plate.gameObject.AddComponent<CanvasGroup>();championPlate.alpha=0;championPlate.blocksRaycasts=false;
            WorldCupDesign.Text(plate,"ChampionGlyph",s.Entrants[s.ChampionId].Build.character,10,3,410,47,46,WorldCupDesign.Red,TextAnchor.MiddleCenter,true);
            WorldCupDesign.Text(plate,"ChampionTitle","WORLD CHAMPION",12,50,406,35,25,WorldCupDesign.Ink,TextAnchor.MiddleCenter,true);
            var text=RetroUi.Group(root,"Congratulations",460,92,750,234);
            congrats=text.gameObject.AddComponent<CanvasGroup>();congrats.alpha=0;congrats.blocksRaycasts=false;
            var leftLeaves=WorldCupDesign.Art(text,"design_laurel",0,12,140,230,false);leftLeaves.uvRect=new Rect(0,0,.4f,1);
            var rightLeaves=WorldCupDesign.Art(text,"design_laurel",610,12,140,230,false);rightLeaves.uvRect=new Rect(.6f,0,.4f,1);
            WorldCupDesign.Text(text,"ChampionCongratulations","優勝\nおめでとう",75,0,600,234,94,WorldCupDesign.Red,TextAnchor.MiddleCenter,true);
            next=WorldCupDesign.Button(root,"WorldCupSkipCeremony","演出をスキップ →",612,852,448,54,Skip,null,28,false);
            skip=WorldCupDesign.Button(root,"WorldCupSkipPhoto","スキップ →",1066,852,154,54,Skip,null,21,false);skip.image.gameObject.SetActive(false);
            flash=RetroUi.Block(root,"PhotoFlash",0,0,1672,941,new Color(1,1,1,0));
        }
        public void Preview(float seconds){Elapsed=seconds;Pose();}
        void Update()
        {
            if(completed || SettingsScreen.IsOpen)return;
            Elapsed+=Time.unscaledDeltaTime;Pose();if(Elapsed>=14)Skip();
        }
        void Pose()
        {
            float raise=Mathf.SmoothStep(0,1,Mathf.Clamp01((Elapsed-4)/1.7f));
            for(int i=0;i<3;i++)
            {
                float progress=RetroMotionSettings.Reduced?1:Mathf.SmoothStep(0,1,Mathf.Clamp01((Elapsed-i*.35f)/3.8f));
                var from=new Vector2(i==0?-200:i==1?1100:1300,-330);
                figures[i].anchoredPosition=Vector2.Lerp(from,targets[i],progress)+new Vector2(0,Mathf.Sin(progress*Mathf.PI)*32);
                bodies[i].Pose(raise,progress<.99f?Mathf.Sin(Elapsed*12+i)*15:0);
                cups[i].gameObject.SetActive(raise>.02f);cups[i].anchoredPosition=new Vector2(12,-Mathf.Lerp(175,-63,raise));
            }
            fireworks.SetClock(Elapsed);congrats.alpha=Mathf.Clamp01((Elapsed-7.5f)*1.8f);
            bool phase=Elapsed>=7.5f;title.gameObject.SetActive(!phase);titleLeftArt.gameObject.SetActive(!phase);titleRightArt.gameObject.SetActive(!phase);titleRules.SetActive(!phase);
            if(phase!=celebrating)
            {
                celebrating=phase;podium.SetActive(!phase);stage.Find("ChampionSteps").gameObject.SetActive(phase);
                foreach(Transform child in stage)if(child.name.StartsWith("PodiumRank") || child.name.StartsWith("PodiumPlace"))child.gameObject.SetActive(!phase);
                championPlate.alpha=phase?1:0;skip.image.gameObject.SetActive(phase);headerStep.text=phase?"WORLD CUP / CHAMPION":"WORLD CUP / CEREMONY";
                backdrop.texture=Resources.Load<Texture2D>("UI/WorldCup/"+(phase?"design_champion_backdrop":"design_ceremony_backdrop"));
                stage.anchoredPosition=new Vector2(phase?421:336,phase?-270:-120);
                stage.localScale=Vector3.one*(phase?.83f:1);next.label.text=phase?"記念撮影へ":"演出をスキップ →";
                if(phase){next.image.color=WorldCupDesign.Red;next.label.color=WorldCupDesign.Paper;}else{next.image.color=WorldCupDesign.Paper;next.label.color=WorldCupDesign.Ink;}
            }
            for(int i=0;i<3;i++)
            {
                bodies[i].Celebrate=phase;bodies[i].Champion=i==1;bodies[i].SetVerticesDirty();
                if(phase){cups[i].anchoredPosition=new Vector2(90,-22);cups[i].sizeDelta=new Vector2(100,92);}
                var glyph=figures[i].Find("AwardGlyph"+(i==0?2:i==1?1:3)).GetComponent<RectTransform>();
                glyph.anchoredPosition=phase?new Vector2(i==1?-65:-100,i==1?60:-110):new Vector2(107,10);
                figures[i].Find("AwardWeaponShaft").gameObject.SetActive(!phase);
            }
            float a=RetroMotionSettings.Reduced?0:Elapsed>=12&&Elapsed<=12.6f?Mathf.Max(0,1-(Elapsed-12)/.6f):0;
            flash.color=new Color(1,1,1,a*.8f);
        }
        public void Skip(){if(completed)return;completed=true;done?.Invoke();}
        static void CreateStage(RectTransform stage,WorldCupSession s,RectTransform[] figures,WorldCupAwardFigure[] bodies,RectTransform[] cups,Vector2[] targets,bool photo,int champion=-1,int runner=-1,int third=-1)
        {
            float scale=Mathf.Min(stage.rect.width/1000,stage.rect.height/710);stage.sizeDelta=new Vector2(1000,710);stage.localScale=Vector3.one*scale;
            var fire=RetroUi.Group(stage,"Fireworks",-175,-40,1350,730).gameObject.AddComponent<WorldCupFireworks>();fire.Still=photo;fire.Reduced=RetroMotionSettings.Reduced;fire.SetClock(photo?8:0);
            WorldCupDesign.Art(stage,"design_podium",0,350,1000,375);
            int[] ids={runner>=0?runner:s.RunnerUpId,champion>=0?champion:s.ChampionId,third>=0?third:s.ThirdId};
            float[] x={120,425,745},tops={455,396,487};int[] place={2,1,3};Color[] colors={Silver,Gold,Bronze};
            for(int i=0;i<3;i++)
            {
                float center=i==0?184:i==1?500:813;
                WorldCupDesign.Text(stage,"PodiumRank"+place[i],place[i].ToString(),center-70,517,140,99,91,i==1?WorldCupDesign.Gold:WorldCupDesign.Paper,TextAnchor.MiddleCenter,true);
                WorldCupDesign.Text(stage,"PodiumPlace"+place[i],i==1?"優勝":i==0?"準優勝":"3位",center-99,627,198,46,33,WorldCupDesign.Navy,TextAnchor.MiddleCenter,true);
                var fig=RetroUi.Group(stage,"AwardFigure"+place[i],x[i],tops[i]-257,150,270);figures[i]=fig;targets[i]=fig.anchoredPosition;
                bodies[i]=fig.gameObject.AddComponent<WorldCupAwardFigure>();bodies[i].Pose(photo?1:0,0);
                cups[i]=WorldCupDesign.Art(fig,"design_cup_"+(i==1?"gold":i==0?"silver":"bronze"),12,-63,128,112).rectTransform;cups[i].gameObject.SetActive(photo);
                if(ids[i]>=0 && ids[i]<s.Entrants.Count)
                {
                    var build=s.Entrants[ids[i]].Build;
                    var shaft=RetroUi.Group(fig,"AwardWeaponShaft",0,0,240,280);
                    RetroUi.Line(shaft,new Vector2(110,254),new Vector2(185,95),WorldCupDesign.Ink,11);
                    RetroUi.Line(shaft,new Vector2(110,254),new Vector2(185,95),ids[i]==0?WorldCupDesign.Red:WorldCupDesign.Navy,6);
                    var glyph=WorldCupDesign.Text(fig,"AwardGlyph"+place[i],build.character,107,-10,143,171,146,ids[i]==0?WorldCupDesign.Red:WorldCupDesign.Navy,TextAnchor.MiddleCenter,true);
                    glyph.font=build.fontType==FontStyleId.Gothic?WorldCupDesign.HeadingFont:GlyphFontLibrary.Default?.Get(build.fontType)?.font??RetroUi.Font;
                    glyph.rectTransform.localRotation=Quaternion.Euler(0,0,i==0?-14:14);
                }
            }
        }
        public static RectTransform PreviewPodium(Transform root,WorldCupSession s,float x,float y,float width,float height,int champion=-1,int runner=-1,int third=-1)
        {
            float scale=Mathf.Min(width/1000,height/710);
            var stage=RetroUi.Group(root,"PodiumPreview",x+(width-1000*scale)/2,y,1000*scale,710*scale);
            CreateStage(stage,s,new RectTransform[3],new WorldCupAwardFigure[3],new RectTransform[3],new Vector2[3],true,champion,runner,third);return stage;
        }
        public static RectTransform Photo(Transform root,WorldCupSession s,float x,float y,float width,float height)
        {
            var frame=RetroUi.Group(root,"WorldCupCommemorativePhoto",x,y,width,height);
            RetroUi.Block(frame,"PhotoShadow",9,10,width,height,WorldCupDesign.Ink);RetroUi.Block(frame,"PhotoPaper",0,0,width,height,WorldCupDesign.Paper);
            WorldCupDesign.Art(frame,"design_paper",14,14,width-28,height-28,false);WorldCupDesign.Frame(frame,0,0,width,height,WorldCupDesign.Ink,3);
            PreviewPodium(frame,s,20,23,width-40,height-89);
            foreach(float cx in new[]{40f,width-65})foreach(float cy in new[]{35f,height-110})
            {RetroUi.Block(frame,"CameraCornerHorizontal",cx,cy,25,3,WorldCupDesign.Ink);RetroUi.Block(frame,"CameraCornerVertical",cx+(cx>width/2?22:0),cy,3,25,WorldCupDesign.Ink);}
            WorldCupDesign.Text(frame,"PhotoPlacements",$"ワールド文字カップ　優勝 {s.Entrants[s.ChampionId].Build.character}　準優勝 {s.Entrants[s.RunnerUpId].Build.character}　3位 {s.Entrants[s.ThirdId].Build.character}",20,height-65,width-40,54,29,WorldCupDesign.Navy,TextAnchor.MiddleCenter,true);
            return frame;
        }
    }
    public sealed class WorldCupPhotoExport : MonoBehaviour
    {
        public void Save(RectTransform frame,WorldCupSession s){StartCoroutine(Export(frame,"moji-world-cup-"+s.Id.Substring(0,8)+".png"));}
        IEnumerator Export(RectTransform frame,string name)
        {
            yield return new WaitForEndOfFrame();
            var corners=new Vector3[4];frame.GetWorldCorners(corners);
            float left=corners[0].x,right=left,bottom=corners[0].y,top=bottom;
            foreach(var point in corners){left=Mathf.Min(left,point.x);right=Mathf.Max(right,point.x);bottom=Mathf.Min(bottom,point.y);top=Mathf.Max(top,point.y);}
            int x=Mathf.Clamp(Mathf.FloorToInt(left),0,Screen.width-1),y=Mathf.Clamp(Mathf.FloorToInt(bottom),0,Screen.height-1);
            int w=Mathf.Clamp(Mathf.CeilToInt(right)-x,1,Screen.width-x),h=Mathf.Clamp(Mathf.CeilToInt(top)-y,1,Screen.height-y);
            var texture=new Texture2D(w,h,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(x,y,w,h),0,0);texture.Apply();
            byte[] bytes=texture.EncodeToPNG();Destroy(texture);
#if UNITY_WEBGL && !UNITY_EDITOR
            var pointer=System.Runtime.InteropServices.Marshal.AllocHGlobal(bytes.Length);
            try{System.Runtime.InteropServices.Marshal.Copy(bytes,0,pointer,bytes.Length);MojiSaveWorldCupPhoto(pointer,bytes.Length,name);}
            finally{System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer);}
#else
            string path=System.IO.Path.Combine(Application.persistentDataPath,name);System.IO.File.WriteAllBytes(path,bytes);Debug.Log("記念写真を保存しました: "+path);
#endif
            Destroy(this);
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]static extern void MojiSaveWorldCupPhoto(System.IntPtr data,int length,string filename);
#endif
    }
}
