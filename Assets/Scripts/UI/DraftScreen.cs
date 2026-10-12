using System;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public static class DraftUi
    {
        public static void Header(Transform root, string step, Action back, string backLabel)
        {
            RetroUi.Header(root, step, back);
            root.Find("Back").GetComponentInChildren<Text>().text = backLabel;
            RetroUi.Button(root,"DraftSettings","設定",1260,55,220,60,30,SettingsScreen.Open,RetroUi.Ink,false);
        }
        public static Text Title(Transform root,string name,string text,float x,float y,float w,float h,int size,Color color)
        {
            var t = RetroUi.Label(root,name,text,x,y,w,h,size,color,TextAnchor.MiddleCenter);
            t.resizeTextForBestFit=true;t.resizeTextMinSize=Mathf.Max(20,size-25);t.resizeTextMaxSize=size;
            return t;
        }
        public static Text Brush(Transform root,string name,string text,float x,float y,float w,float h,int size,Color color)
        {
            var t = Title(root,name,text,x,y,w,h,size,color);
            t.font = GlyphFontLibrary.Default?.Get(FontStyleId.Brush)?.font ?? RetroUi.Font;
            return t;
        }
        public static RectTransform Panel(Transform parent,string name,float x,float y,float w,float h)
        {
            var panel=RetroUi.Group(parent,name,x,y,w,h);
            RetroUi.Frame(panel,0,0,w,h,RetroUi.Ink,2);RetroUi.Frame(panel,7,7,w-14,h-14,RetroUi.Rule,1);
            return panel;
        }
        public static void Score(Transform root,float x,float y,float w)
        {
            var s=DraftRuntimeStore.Current;
            var p=RetroUi.Group(root,"DraftScore",x,y,w,58);
            RetroUi.Frame(p,0,0,w,58,RetroUi.Ink,2);
            RetroUi.Label(p,"DraftRound",$"第{s.round}ラウンド",16,0,w*.40f-20,58,31);
            RetroUi.Block(p,"PlayerScorePlate",w*.40f,2,w*.30f-2,54,RetroUi.Red);
            RetroUi.Block(p,"ComScorePlate",w*.70f,2,w*.30f-2,54,RetroUi.Blue);
            RetroUi.Label(p,"PlayerWins",$"あなた {s.playerWins}勝",w*.40f,0,w*.30f,58,31,RetroUi.Paper,TextAnchor.MiddleCenter);
            RetroUi.Label(p,"ComWins",$"COM {s.comWins}勝",w*.70f,0,w*.30f,58,31,RetroUi.Paper,TextAnchor.MiddleCenter);
        }
        public static void Pips(Transform root,float x,float y,int wins,Color team)
        {
            for(int i=0;i<2;i++)
            {
                var dot=RetroUi.Block(root,"WinPip",x+i*63,y,42,42,i<wins?team:RetroUi.Paper);
                dot.sprite=UiKit.DiscSprite;UguiKit.AddOutline(dot.gameObject,2,team);
            }
        }
        public static void Meters(Transform root,Vector3 values,float x,float y,float width,Color color,bool squares)
        {
            string[] names={"威力","速度","リーチ"};
            for(int i=0;i<3;i++)
            {
                RetroUi.Label(root,"DraftStat"+i,names[i],x,y+i*36,82,30,24);
                float bx=x+90;
                if(squares)
                {
                    int count=Mathf.Clamp(Mathf.CeilToInt(values[i]*5),1,5);
                    float side=Mathf.Min(25,(width-98)/6);
                    for(int j=0;j<5;j++)
                    {RetroUi.Block(root,"DraftMeter",bx+j*(side+10),y+i*36+5,side,side,j<count?color:RetroUi.Paper);RetroUi.Frame(root,bx+j*(side+10),y+i*36+5,side,side,RetroUi.Ink,1);}
                }
                else
                {
                    float bw=width-90;RetroUi.Frame(root,bx,y+i*36+5,bw,23,RetroUi.Ink,1);
                    RetroUi.Block(root,"DraftMeterFill"+i,bx+2,y+i*36+7,(bw-4)*values[i],19,color);
                }
            }
        }
        public static void CardsArt(Transform root,float x,float y,float scale)
        {
            string[] chars={"火","盾","龍"};
            for(int i=0;i<3;i++)
            {
                var card=Panel(root,"DraftArtCard",x+i*101*scale,y+(i==1?0:22*scale),130*scale,205*scale);
                RetroUi.Block(card,"CardPaper",4,4,122*scale,197*scale,RetroUi.Paper).transform.SetAsFirstSibling();
                card.localRotation=Quaternion.Euler(0,0,i==0?12:i==2?-12:0);
                Brush(card,"CardLetter",chars[i],0,10*scale,130*scale,170*scale,Mathf.RoundToInt(116*scale),i==0?RetroUi.Red:i==2?RetroUi.Blue:RetroUi.Ink);
            }
        }
    }
    public sealed class DraftScreen : MonoBehaviour
    {
        sealed class Card { public UguiKit.ChoiceButton hit; public Text glyph, status; public GameObject border; public Image[] meters; }
        readonly Card[] cards=new Card[6];
        Text turn,playerLetter,comLetter,playerWeapon,comWeapon,playerStatus,comStatus;
        UguiKit.ChoiceButton confirm;
        string pending;
        float comDelay=.75f;
        public string Pending => pending;
        void Start()
        {
            if(Camera.main!=null)Camera.main.backgroundColor=RetroUi.Paper;
            var root=RetroUi.Screen("DraftCanvas",out _);
            if(GameFlow.DraftIntro || !DraftRuntimeStore.Active) BuildIntro(root);
            else if(DraftRuntimeStore.Current.stage==DraftStage.Result) gameObject.AddComponent<DraftResultScreen>().Build();
            else BuildPicks(root);
        }
        void BuildIntro(RectTransform root)
        {
            DraftUi.Header(root,"DRAFT / GUIDE",GameFlow.ToModeSelect,"← メニュー");
            DraftUi.Title(root,"DraftTitle","文字ドラフト",440,135,1040,135,110,RetroUi.Ink);
            RetroUi.Line(root,new Vector2(620,268),new Vector2(1300,268),RetroUi.Red,5);
            RetroUi.Label(root,"DraftSubtitle","引いた文字で、勝ち筋をつくる。",460,278,1000,60,39,null,TextAnchor.MiddleCenter);
            RetroUi.Fighter(root,70,182,.87f,"火",RetroUi.Red);
            RetroUi.Fighter(root,1530,182,.87f,"雷",RetroUi.Blue,true);
            string[] letters={"火","刀","龍","盾","風","雷"};
            for(int i=0;i<6;i++)
            {
                var card=DraftUi.Panel(root,"IntroLetter"+i,420+i*182,350,164,195);
                DraftUi.Brush(card,"Glyph",letters[i],4,8,156,175,135,i==0?RetroUi.Red:RetroUi.Ink);
            }
            string[] titles={"文字を選ぶ","構成を決める","先に2勝で勝利"};
            string[] captions={"6文字の共通候補から選択","字体・サイズ・持ち方を調整","毎ラウンド、新しい候補が登場"};
            for(int i=0;i<3;i++)
            {
                var panel=DraftUi.Panel(root,"DraftRule"+i,76+i*598,575,572,282);
                ChallengeScreen.Tag(panel,(i+1).ToString("00"),18,18,70);
                RetroUi.Label(panel,"Title",titles[i],104,18,442,56,36);
                RetroUi.Label(panel,"Caption",captions[i],104,77,442,50,27);
                if(i==0)DraftUi.CardsArt(panel,110,149,.46f);
                if(i==1)RetroUi.Fighter(panel,227,126,.35f,"刀",RetroUi.Ink);
                if(i==2){DraftUi.Pips(panel,160,182,2,RetroUi.Red);DraftUi.Pips(panel,338,182,0,RetroUi.Blue);}
            }
            RetroUi.Label(root,"DraftRulesNote","選ばれた文字は相手が使えません。先攻はラウンドごとに交代。",266,868,1388,50,27,null,TextAnchor.MiddleCenter);
            RetroUi.Button(root,"DraftBegin","ドラフトを始める →",490,930,650,86,43,()=>GameFlow.BeginDraft((int)(DateTime.UtcNow.Ticks & 0x7fffffff)),RetroUi.Red);
            bool resume=DraftRuntimeStore.CanResume;
            RetroUi.Button(root,"DraftResumeOrMenu",resume?"続きから再開":"メニューへ",1190,930,315,86,31,resume?(Action)GameFlow.ResumeDraft:GameFlow.ToModeSelect,shadow:false);
        }
        void BuildPicks(RectTransform root)
        {
            var s=DraftRuntimeStore.Current;
            DraftUi.Header(root,"DRAFT / SELECT",GameFlow.ToDraftIntro,"← 戻る");
            DraftUi.Score(root,76,146,1128);
            RetroUi.Label(root,"PickTitle","文字を選ぶ",76,216,510,98,79);
            turn=RetroUi.Label(root,"DraftTurn","",622,232,582,80,44,RetroUi.Red,TextAnchor.MiddleCenter);
            for(int i=0;i<6;i++)
            {
                int index=i;float x=76+i%3*386,y=330+i/3*356;
                var p=DraftUi.Panel(root,"DraftCandidate"+i,x,y,368,338);
                var hit=RetroUi.Button(p,"DraftPick"+i,"",0,0,368,338,20,()=>PreviewCandidate(s.pool[index]),shadow:false);
                hit.image.color=Color.clear;hit.button.transition=Selectable.Transition.None;hit.image.GetComponent<Outline>().enabled=false;hit.image.GetComponent<RetroButtonMotion>().enabled=false;
                var c=cards[i]=new Card {hit=hit};
                c.glyph=DraftUi.Brush(p,"CandidateGlyph",s.pool[i],15,36,338,176,170,RetroUi.Ink);
                RetroUi.Block(p,"NumberPlate",10,10,42,40,RetroUi.Ink);
                RetroUi.Label(p,"Number",(i+1).ToString(),10,10,42,40,29,RetroUi.Paper,TextAnchor.MiddleCenter);
                c.status=RetroUi.Label(p,"CandidateStatus","",146,10,210,38,23,RetroUi.Blue,TextAnchor.MiddleRight);
                var b=FighterBuildData.Default(s.pool[i]);b.fontType=FontStyleId.Brush;
                DraftUi.Meters(p,DraftUiStats.Values(b),24,220,330,RetroUi.Ink,true);
                c.meters=System.Array.FindAll(p.GetComponentsInChildren<Image>(),image=>image.name=="DraftMeter");
                var border=RetroUi.Group(p,"CandidateFocus",-3,-3,374,344);RetroUi.Frame(border,0,0,374,344,RetroUi.Red,3);
                c.border=border.gameObject; c.border.SetActive(false);
            }
            var you=PickSide(root,"PlayerDraftPanel",1242,146,RetroUi.Red,"あなた",out playerLetter,out playerWeapon,out playerStatus);
            var com=PickSide(root,"ComDraftPanel",1242,512,RetroUi.Blue,"COM",out comLetter,out comWeapon,out comStatus);
            RetroUi.Label(root,"DraftPickNote","COMが選んだ文字は選べません",1242,884,602,44,25,null,TextAnchor.MiddleCenter);
            confirm=RetroUi.Button(root,"DraftConfirm","文字を選ぶ →",1242,935,602,80,44,Confirm,RetroUi.Red);
            if(s.player!=null)pending=s.player.character;
            Refresh();
        }
        static RectTransform PickSide(Transform root,string name,float x,float y,Color team,string caption,out Text glyph,out Text weapon,out Text status)
        {
            var p=DraftUi.Panel(root,name,x,y,602,346);
            RetroUi.Block(p,"TeamPlate",8,8,586,62,team);
            RetroUi.Label(p,"Team",caption,24,8,300,62,36,RetroUi.Paper);
            status=RetroUi.Label(p,"PickStatus","",356,8,220,62,26,RetroUi.Paper,TextAnchor.MiddleRight);
            glyph=DraftUi.Brush(p,"PickedGlyph","？",16,99,285,202,173,team);
            RetroUi.Fighter(p,345,113,.51f,"？",team);
            weapon=p.Find("GlyphFighter/GlyphWeapon").GetComponent<Text>();
            return p;
        }
        public void PreviewCandidate(string ch)
        {
            var s=DraftRuntimeStore.Current;
            if(SettingsScreen.BlocksInput || !s.PlayerTurn || s.com?.character==ch || Array.IndexOf(s.pool,ch)<0)return;
            pending=ch;Refresh();
        }
        public void Confirm()
        {
            if(SettingsScreen.BlocksInput)return;
            if(DraftRuntimeStore.CanBattle){GameFlow.ToDraftBuild();return;}
            if(pending!=null && DraftRuntimeStore.PickPlayer(pending)){comDelay=.75f;Refresh();}
        }
        void Refresh()
        {
            var s=DraftRuntimeStore.Current;
            turn.text=s.PlayerTurn?"あなたの番です":s.ComTurn?"COMが選択中…":"選択完了";
            turn.color=s.ComTurn?RetroUi.Blue:RetroUi.Red;
            for(int i=0;i<6;i++)
            {
                string ch=s.pool[i];var c=cards[i];bool taken=s.com?.character==ch;
                bool selected=pending==ch || s.player?.character==ch;
                c.hit.button.interactable=s.PlayerTurn && !taken;
                bool highlighted=selected && !taken;
                if(highlighted && !c.border.activeSelf)RetroUi.Enter(c.border,0,0);
                c.border.SetActive(highlighted);
                c.glyph.color=taken?RetroUi.Muted:selected?RetroUi.Red:RetroUi.Ink;
                foreach(var meter in c.meters)if(meter.color!=RetroUi.Paper)meter.color=taken?RetroUi.Muted:selected?RetroUi.Red:RetroUi.Ink;
                c.status.text=taken?"COM選択済み ✓":s.player?.character==ch?"あなた選択済み":selected?"選択中":"";
                c.status.color=taken?RetroUi.Blue:RetroUi.Red;
            }
            playerLetter.text=playerWeapon.text=s.player?.character??pending??"？";
            comLetter.text=comWeapon.text=s.com?.character??"？";
            playerStatus.text=s.player!=null?"選択済み":s.PlayerTurn?"選択中":"待機中";
            comStatus.text=s.com!=null?"選択済み":s.ComTurn?"選択中":"待機中";
            confirm.button.interactable=DraftRuntimeStore.CanBattle || s.PlayerTurn && pending!=null;
            confirm.label.text=DraftRuntimeStore.CanBattle?"構成を決める →":s.ComTurn?"COMの選択を待っています":pending!=null?pending+"を選ぶ →":"文字を選ぶ →";
        }
        void Update()
        {
            if(SettingsScreen.BlocksInput)return;
            if(confirm!=null && DraftRuntimeStore.Current.ComTurn)
            {
                comDelay-=Time.unscaledDeltaTime;
                if(comDelay<=0){DraftRuntimeStore.PickCom();Refresh();}
            }
            if(Input.GetKeyDown(KeyCode.Escape))GameFlow.ToModeSelect();
        }
    }
}
