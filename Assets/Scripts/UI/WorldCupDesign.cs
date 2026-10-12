using System;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>World Cup mockup coordinates: 1672 × 941. Kept separate from the battle UI.</summary>
    public static class WorldCupDesign
    {
        public static readonly Color Ink=new Color32(9,12,14,255),Navy=new Color32(20,49,77,255),Red=new Color32(174,35,25,255),Gold=new Color32(191,143,43,255);
        public static readonly Color Paper=new Color32(245,238,221,255),Rule=new Color32(141,137,124,255);
        static Font black;
        public static Font HeadingFont=>black!=null?black:black=Resources.Load<Font>("UI/WorldCup/Fonts/NotoSansCJKjp-Black");
        public static RectTransform Screen(string name,out Canvas canvas,string background="design_paper")
        {
            var safe=RetroUi.Screen(name,out canvas);
            foreach(Transform child in safe)child.gameObject.SetActive(false);
            var root=RetroUi.Group(safe,"WorldCupDesign",0,0,1672,941);root.localScale=Vector3.one*(1920f/1672f);
            Art(root,background,0,0,1672,941,false);Frame(root,13,13,1646,915,Ink,2,true);return root;
        }
        public static Text Text(Transform p,string name,string value,float x,float y,float w,float h,int size=28,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft,bool heavy=false)
        {
            var t=UguiKit.Label(p,name,value,x,y,w,h,size,color??Ink,align,false);
            t.font=heavy?HeadingFont:RetroUi.Font;t.resizeTextForBestFit=true;t.resizeTextMinSize=Mathf.Max(12,size-8);t.resizeTextMaxSize=size;
            t.verticalOverflow=VerticalWrapMode.Overflow;
            if(size>=54)PaperUi.Ink(t,Mathf.Clamp(size*.025f,1.5f,7));return t;
        }
        public static Image Block(Transform p,string name,float x,float y,float w,float h,Color color)=>RetroUi.Block(p,name,x,y,w,h,color);
        public static RawImage Art(Transform p,string name,float x,float y,float w,float h,bool fit=true)
        {
            var tex=Resources.Load<Texture2D>("UI/WorldCup/"+name);if(tex==null)throw new InvalidOperationException("Missing World Cup art: "+name);
            if(fit){float s=Mathf.Min(w/tex.width,h/tex.height);float nw=tex.width*s,nh=tex.height*s;x+=(w-nw)/2;y+=(h-nh)/2;w=nw;h=nh;}
            var art=RetroUi.Group(p,name,x,y,w,h).gameObject.AddComponent<RawImage>();art.texture=tex;art.raycastTarget=false;return art;
        }
        public static RawImage InkFill(Transform p,string name,float x,float y,float w,float h,Color color)
        {
            Block(p,name+"Base",x,y,w,h,color);
            var art=Art(p,"design_ink",x,y,w,h,false);art.name=name;
            art.color=Mathf.Max(color.r,color.g,color.b)>.7f?new Color(1,1,1,.08f):new Color(color.r*1.9f,color.g*1.9f,color.b*1.9f,1);return art;
        }
        public static void Frame(Transform p,float x,float y,float w,float h,Color color,float thickness=2,bool corners=false)
        {
            RetroUi.Frame(p,x,y,w,h,color,thickness);
            RetroUi.Frame(p,x+6,y+6,w-12,h-12,color,1);
            if(!corners)return;
            foreach(float cx in new[]{x,x+w-21})foreach(float cy in new[]{y,y+h-21})
            {RetroUi.Frame(p,cx,cy,21,21,color,2);RetroUi.Frame(p,cx+6,cy+6,9,9,color,2);}
        }
        public static RectTransform Panel(Transform p,string name,float x,float y,float w,float h,string title=null,Color? band=null,bool centered=false)
        {
            var panel=RetroUi.Group(p,name,x,y,w,h);Frame(panel,0,0,w,h,Ink,2,true);
            if(title!=null){InkFill(panel,"PanelBand",9,9,w-18,62,band??Navy);Text(panel,"PanelTitle",title,24,8,w-48,64,36,Paper,centered?TextAnchor.MiddleCenter:TextAnchor.MiddleLeft,true);}
            return panel;
        }
        public static UguiKit.ChoiceButton Button(Transform p,string name,string value,float x,float y,float w,float h,Action action,Color? fill=null,int size=34,bool shadow=true)
        {
            if(shadow)Block(p,name+"Shadow",x+7,y+8,w,h,Color.clear);
            var b=UguiKit.Choice(p,name,value,x,y,w,h,size,action);
            b.image.color=fill??Paper;b.accent=fill??Navy;b.image.GetComponent<Outline>().enabled=false;
            if(fill.HasValue){var skin=InkFill(b.image.transform,"ButtonInk",4,4,w-8,h-8,fill.Value);skin.transform.SetAsFirstSibling();}
            Frame(b.image.transform,0,0,w,h,Ink,2);RetroUi.Frame(b.image.transform,5,5,w-10,h-10,fill.HasValue?Paper:Rule,1);
            b.label.font=HeadingFont;b.label.fontStyle=FontStyle.Normal;b.label.color=fill.HasValue?Paper:Ink;
            b.label.resizeTextForBestFit=true;b.label.resizeTextMinSize=Mathf.Max(16,size-10);b.label.resizeTextMaxSize=size;
            b.label.verticalOverflow=VerticalWrapMode.Truncate;b.label.transform.SetAsLastSibling();
            var c=b.button.colors;c.normalColor=Color.white;c.highlightedColor=Color.white;c.selectedColor=Color.white;c.pressedColor=new Color(.85f,.85f,.85f);c.disabledColor=new Color(.68f,.68f,.68f,.65f);b.button.colors=c;
            return b;
        }
        public static void Header(Transform p,string phase,Action back,string label="← 戻る",Color? backColor=null)
        {
            Button(p,"Back",label,55,30,190,52,back,backColor??Red,28,false);
            Text(p,"Step",phase,445,23,740,60,27,Ink,TextAnchor.MiddleCenter,true);
            Button(p,"DraftSettings","⚙ 設定",1218,31,112,48,SettingsScreen.Open,null,23,false);
            Text(p,"Brand","MOJI Fighter",1360,22,230,61,34,Ink,TextAnchor.MiddleRight,true);
            Text(p,"BrandX","X",1592,15,49,73,57,Red,TextAnchor.MiddleCenter,true);
            Block(p,"HeaderLeftRule",270,52,240,1,Ink);Block(p,"HeaderRightRule",1125,52,75,1,Ink);
        }
        public static Text Title(Transform p,string value,float y=107,int size=84,Color? color=null,float x=120,float width=1432)
        {return Text(p,"WorldCupTitle",value,x,y,width,112,size,color,TextAnchor.MiddleCenter,true);}
        public static void HeadingRules(Transform p,float y,Color? color=null)
        {Block(p,"HeadingLeft",85,y,350,2,color??Ink);Block(p,"HeadingRight",1237,y,350,2,color??Ink);Diamond(p,447,y,color??Ink);Diamond(p,1225,y,color??Ink);}
        public static void Diamond(Transform p,float x,float y,Color c)
        {var b=Block(p,"PrintedDiamond",x-6,y-6,12,12,c);b.rectTransform.pivot=new Vector2(.5f,.5f);b.rectTransform.anchoredPosition=new Vector2(x,-y);b.rectTransform.localRotation=Quaternion.Euler(0,0,45);}
        public static RectTransform NumberPlaque(Transform p,string name,float x,float y,float w,float h,Color color)
        {
            var mask=RetroUi.Group(p,name,x,y,w,h);mask.gameObject.AddComponent<WorldCupSlantedPlaque>();mask.gameObject.AddComponent<Mask>().showMaskGraphic=false;InkFill(mask,"NumberInk",0,0,w,h,color);return mask;
        }
        public static void Check(Transform p,string value,float x,float y,float width,int size=24)
        {InkFill(p,"CheckBox",x,y+3,27,27,Red);Text(p,"Checkmark","✓",x,y,27,31,25,Paper,TextAnchor.MiddleCenter);Text(p,"CheckText",value,x+39,y,width-39,34,size);}
        public static void Selected(UguiKit.ChoiceButton b,bool active)
        {b.image.color=active?Red:Paper;b.label.color=active?Paper:Ink;}
        public static RectTransform Fighter(Transform p,float x,float y,float w,float h,FighterBuildData build,Color glyphColor,bool mirror=false,string pose="ready",bool coloredBody=false)
        {
            var root=RetroUi.Group(p,"DesignFighter",x,y,w,h);
            float scale=Mathf.Min(w/240,h/280);var figure=RetroUi.Group(root,"Figure",(w-240*scale)/2,(h-280*scale)/2,240,280);figure.localScale=Vector3.one*scale;
            var body=figure.gameObject.AddComponent<WorldCupFigure>();body.Pose=pose;body.Mirror=mirror;body.color=coloredBody?glyphColor:Ink;
            float gx=pose=="fallen"?77:pose=="kneel"?124:pose=="winner"?66:111,gy=pose=="fallen"?135:pose=="kneel"?118:pose=="winner"?59:0;
            var t=Text(figure,"GlyphWeapon",build.character,mirror?240-gx-120:gx,gy,120,155,134,glyphColor,TextAnchor.MiddleCenter,true);
            t.font=build.fontType==FontStyleId.Gothic?HeadingFont:GlyphFontLibrary.Default?.Get(build.fontType)?.font??HeadingFont;
            t.rectTransform.localRotation=Quaternion.Euler(0,0,mirror?-14:14);return root;
        }
        public static void Hp(Transform p,string value,float hp,float max,float x,float y,float w,Color color)
        {Text(p,"HpLabel","残りHP",x,y,66,32,19,Navy);RetroUi.Frame(p,x+74,y+7,w-173,18,Rule,1);Block(p,"HpFill",x+75,y+8,(w-175)*Mathf.Clamp01(hp/Mathf.Max(1,max)),16,color);Text(p,"HpValue",value,x+w-93,y,93,32,19,Navy,TextAnchor.MiddleRight);}
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorldCupSlantedPlaque : MaskableGraphic
    {
        protected override void Awake(){base.Awake();raycastTarget=false;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float w=rectTransform.rect.width,h=rectTransform.rect.height;
            foreach(var v in new[]{new Vector2(0,-h),new Vector2(w*.12f,0),new Vector2(w,0),new Vector2(w*.83f,-h)})vh.AddVert(v,Color.white,Vector2.zero);
            vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);
        }
    }
    public sealed class WorldCupButtonFocus : MonoBehaviour,UnityEngine.EventSystems.IPointerEnterHandler,UnityEngine.EventSystems.IPointerExitHandler,UnityEngine.EventSystems.ISelectHandler,UnityEngine.EventSystems.IDeselectHandler
    {
        bool over,selected;Vector3 initial;
        void Awake(){initial=transform.localScale;}
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e){over=true;}
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e){over=false;}
        public void OnSelect(UnityEngine.EventSystems.BaseEventData e){selected=true;}
        public void OnDeselect(UnityEngine.EventSystems.BaseEventData e){selected=false;}
        void Update(){transform.localScale=Vector3.Lerp(transform.localScale,initial*((over||selected)&&!RetroMotionSettings.Reduced?1.012f:1),1-Mathf.Exp(-Time.unscaledDeltaTime*20));}
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorldCupFigure : MaskableGraphic
    {
        public string Pose="ready";public bool Mirror;
        protected override void Awake(){base.Awake();raycastTarget=false;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Vector2 P(float x,float y)=>new Vector2(Mirror?240-x:x,-y);
            void Dot(float x,float y,float r,Color c){int n=vh.currentVertCount;vh.AddVert(P(x,y),c,Vector2.zero);for(int i=0;i<=32;i++){float a=i*Mathf.PI/16;vh.AddVert(P(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r),c,Vector2.zero);}for(int i=0;i<32;i++)vh.AddTriangle(n,n+i+1,n+i+2);}
            Color body = Mathf.Max(color.r,color.g,color.b)<.12f ? new Color(.12f,.135f,.15f,color.a) : color;
            void L(float ax,float ay,float bx,float by,float thickness=10)=>PaperSculpture.Capsule(vh,P(ax,ay),P(bx,by),thickness*.5f,body);
            for(int i=0;i<7;i++)Dot(35+i*25,270,10,new Color(.18f,.18f,.17f,.045f));
            if(Pose=="fallen"){PaperSculpture.Ball(vh,P(30,207),22,body);L(55,215,125,231);L(125,231,177,211);L(177,211,210,237);L(125,231,190,252);L(55,223,96,243);return;}
            if(Pose=="kneel"){PaperSculpture.Ball(vh,P(140,68),24,body);L(121,90,98,181,13);L(98,181,55,186,13);L(55,186,36,257,13);L(36,257,95,257,13);L(98,181,87,250,13);L(120,110,148,166);L(148,166,164,183);L(120,108,96,148);return;}
            if(Pose=="winner"){PaperSculpture.Ball(vh,P(109,52),24,body);L(101,79,91,181,12);L(103,88,137,16);L(99,94,60,134);L(60,134,77,151);L(91,181,57,266,12);L(91,181,132,266,12);return;}
            PaperSculpture.Ball(vh,P(95,77),25,body);L(88,102,65,177,13);L(84,119,123,150);L(123,150,161,137);L(83,134,120,165);L(120,165,163,151);L(65,177,27,266,13);L(65,177,128,212,13);L(128,212,150,266,13);
        }
    }
}
