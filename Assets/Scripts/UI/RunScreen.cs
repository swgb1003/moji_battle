using System;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public static class RunUi
    {
        public static void Header(Transform root,string step,Action back,string label="← メニュー")=>DraftUi.Header(root,"RUN / "+step,back,label);
        public static void Title(Transform root,string text)=>DraftUi.Title(root,"RunTitle",text,300,145,1320,130,104,RetroUi.Ink);
        public static void Progress(Transform root,float x,float y,float width,int round,int wins,bool failed=false)
        {
            float gap=(width-70)/4;
            RetroUi.Block(root,"RunRoute",x+35,y+34,width-70,2,RetroUi.Rule);
            for(int i=1;i<=5;i++)
            {
                bool guide=round==0;
                Color c=guide?(i==5?RetroUi.Blue:RetroUi.Red):i<=wins?RetroUi.Red:RetroUi.Paper;
                var dot=RetroUi.Block(root,"RunNode"+i,x+(i-1)*gap,y,70,70,c);dot.sprite=UiKit.DiscSprite;
                UguiKit.AddOutline(dot.gameObject,i==round?4:2,i==5?RetroUi.Blue:i<=wins||i==round?RetroUi.Red:RetroUi.Ink);
                RetroUi.Label(root,"RunNodeText"+i,i<=wins?"✓":failed&&i==round?"×":i==5?"BOSS":guide?i.ToString():"",x+(i-1)*gap,y,70,70,i==5?21:34,c==RetroUi.Paper?i==5?RetroUi.Blue:RetroUi.Ink:RetroUi.Paper,TextAnchor.MiddleCenter);
                if(!guide)RetroUi.Label(root,"RunNodeCaption"+i,$"第{i}戦",x+(i-1)*gap-18,y+76,106,33,25,i==5?RetroUi.Blue:RetroUi.Ink,TextAnchor.MiddleCenter);
            }
        }
        public static void Fighter(Transform root,float x,float y,float scale,FighterBuildData build,Color color,bool mirror=false)
        {
            RetroUi.Fighter(root,x,y,scale,build.character,color,mirror);
            var art=root.GetChild(root.childCount-1);var t=art.Find("GlyphWeapon").GetComponent<Text>();
            t.font=GlyphFontLibrary.Default?.Get(build.fontType)?.font??RetroUi.Font;
        }
        public static void Hp(Transform root,float x,float y,float width,RunSession s)
        {
            RetroUi.Label(root,"RunHp",$"残りHP　{Mathf.CeilToInt(s.Hp)} / {Mathf.CeilToInt(s.MaxHp)}",x,y,width,48,32);
            RetroUi.Frame(root,x,y+54,width,26,RetroUi.Ink,2);
            RetroUi.Block(root,"RunHpFill",x+3,y+57,(width-6)*s.hpShare,20,RetroUi.Red);
        }
        public static string BuildText(FighterBuildData b)=>$"{GlyphCatalog.FontDisplayName(b.fontType)}　{b.weaponSize}　{CustomizeLabels.Grip(b.gripType)}\n{CustomizeLabels.Style(b.battleStyle)}";
        public static string Perks(RunSession s)
        {
            string text="";
            for(int i=0;i<s.upgrades.Length;i++)if(s.upgrades[i]>0)text+=$"{RunCatalog.Upgrades[i].Name} ×{s.upgrades[i]}　";
            return text.Length==0?"強化は勝利後に獲得":text;
        }
        public static void PerkChips(Transform root,RunSession s,float x,float y,float width)
        {
            int count=0;
            for(int id=0;id<s.upgrades.Length;id++)if(s.upgrades[id]>0 && id!=2)
            {
                var chip=DraftUi.Panel(root,"RunPerkChip"+id,x+(count%2)*(width/2+8),y+(count/2)*52,width/2-8,44);
                RetroUi.Label(chip,"Effect",RunCatalog.Upgrade(id).Effect+(s.upgrades[id]>1?$" ×{s.upgrades[id]}":""),10,0,width/2-28,44,24,RetroUi.Red,TextAnchor.MiddleCenter);count++;
            }
            if(count==0)RetroUi.Label(root,"EmptyPerks","強化は勝利後に獲得",x,y,width,44,27,RetroUi.Muted);
        }
        public static void Symbol(Transform root,int id,float x,float y,float scale)
        {
            var p=RetroUi.Group(root,"RunUpgradeArt",x,y,400*scale,200*scale);
            void L(float ax,float ay,float bx,float by,Color c,float weight=10)=>RetroUi.Line(p,new Vector2(ax,ay)*scale,new Vector2(bx,by)*scale,c,weight*scale);
            void Disc(float cx,float cy,float radius,Color c){var dot=RetroUi.Block(p,"SymbolDisc",(cx-radius)*scale,(cy-radius)*scale,radius*2*scale,radius*2*scale,c);dot.sprite=UiKit.DiscSprite;}
            Disc(173,57,21,RetroUi.Ink);L(173,83,164,136,RetroUi.Ink,20);L(164,136,114,185,RetroUi.Ink,18);L(164,136,214,184,RetroUi.Ink,18);
            if(id==1 || id==7 || id==8)
            {
                L(169,90,124,52,RetroUi.Ink,16);L(177,91,220,51,RetroUi.Ink,16);
                for(int side=0;side<2;side++)for(int n=0;n<3;n++)
                {float r=57+n*23;for(int t=0;t<14;t++){float a=(-.75f+t*1.5f/14)+(side==0?Mathf.PI:0);float b=a+1.5f/14;L(173+Mathf.Cos(a)*r,90+Mathf.Sin(a)*r,173+Mathf.Cos(b)*r,90+Mathf.Sin(b)*r,RetroUi.Blue,6);}}
            }
            else if(id==2 || id==11)
            {
                L(174,93,242,116,RetroUi.Ink,16);Disc(293,99,49,RetroUi.Red);
                L(265,99,321,99,RetroUi.Paper,15);L(293,71,293,127,RetroUi.Paper,15);
            }
            else
            {
                L(177,94,239,111,RetroUi.Ink,16);L(239,111,269,79,RetroUi.Ink,16);
                L(262,88,333,12,id==4||id==9||id==10?RetroUi.Blue:RetroUi.Ink,14);
                for(int n=0;n<3;n++)L(20+n*8,88+n*31,108+n*6,77+n*31,RetroUi.Ink,3);
            }
        }
    }
    public sealed class RunScreen : MonoBehaviour
    {
        RectTransform root;
        Canvas versusCanvas;
        GameObject enemyModal;
        UguiKit.ChoiceButton confirm;
        readonly UguiKit.ChoiceButton[] choices=new UguiKit.ChoiceButton[3];
        readonly GameObject[] borders=new GameObject[3];
        void Update()
        {
            if(SettingsScreen.BlocksInput || !Input.GetKeyDown(KeyCode.Escape))return;
            if(enemyModal!=null){Destroy(enemyModal);return;}
            if(versusCanvas!=null){Destroy(versusCanvas.gameObject);root.gameObject.SetActive(true);return;}
            if(RunRuntimeStore.Current?.stage==RunStage.Upgrade){BackToResult();return;}
            GameFlow.ToModeSelect();
        }
        void BackToResult(){if(RunRuntimeStore.ShowResult())GameFlow.ToRunScreen();}
        void Start()
        {
            if(Camera.main!=null)Camera.main.backgroundColor=RetroUi.Paper;
            if(!GameFlow.RunIntro && RunRuntimeStore.Current!=null && (RunRuntimeStore.Current.stage==RunStage.Result || RunRuntimeStore.Current.Finished))
            {gameObject.AddComponent<RunResultScreen>().Build();return;}
            root=RetroUi.Screen("RunCanvas",out _);
            if(GameFlow.RunIntro || !RunRuntimeStore.Active)Guide();
            else if(RunRuntimeStore.Current.stage==RunStage.Upgrade)Upgrade();
            else Prepare();
        }
        void Guide()
        {
            RunUi.Header(root,"GUIDE",GameFlow.ToModeSelect);RunUi.Title(root,"勝ち抜き・育成");
            RetroUi.Label(root,"RunSubtitle","一文字を育て、五連戦を突破せよ。",380,284,1160,60,40,null,TextAnchor.MiddleCenter);
            RunUi.Fighter(root,85,215,.72f,FighterBuildData.Default("火"),RetroUi.Red);
            RunUi.Fighter(root,1540,215,.72f,FighterBuildData.Default("龍"),RetroUi.Blue,true);
            RunUi.Progress(root,460,395,1000,0,0);
            string[] titles={"一文字で出発","勝つたびに強化","全5戦を突破"};
            string[] copy={"好きな文字と構成で挑戦\n途中で構成を調整できます","3つの候補から1つを選択\n回復か強化か、選択が鍵","HPを引き継いで連戦\n最終戦にはボスが待つ"};
            for(int i=0;i<3;i++)
            {
                var p=DraftUi.Panel(root,"RunRule"+i,76+i*598,560,572,288);
                ChallengeScreen.Tag(p,(i+1).ToString("00"),18,18,70);
                RetroUi.Label(p,"RuleTitle",titles[i],110,18,430,66,38);
                RetroUi.Label(p,"RuleCopy",copy[i].Split('\n')[0],104,85,430,48,28,null,TextAnchor.MiddleCenter);
                if(i==0)
                {
                    RunUi.Fighter(p,26,143,.31f,FighterBuildData.Default("火"),RetroUi.Red);
                    string[] chars={"火","水","木","金","土","風"};
                    for(int n=0;n<6;n++){float cx=230+n%3*89,cy=145+n/3*62;DraftUi.Panel(p,"LetterChoice"+n,cx,cy,76,53);RetroUi.Label(p,"Letter",chars[n],cx,cy,76,53,35,null,TextAnchor.MiddleCenter);}
                }
                if(i==1)RunUi.Symbol(p,1,128,140,.62f);
                if(i==2){for(int n=0;n<4;n++)RunUi.Fighter(p,22+n*94,154,.23f,FighterBuildData.Default("一"),RetroUi.Ink);RunUi.Fighter(p,415,135,.32f,FighterBuildData.Default("龍"),RetroUi.Blue);}
            }
            RetroUi.Label(root,"RunNote","回復か強化か。選択が、次の勝負を変える。",250,864,1420,52,31,null,TextAnchor.MiddleCenter);
            RetroUi.Button(root,"RunBegin","新しく始める →",490,934,650,82,42,()=>GameFlow.BeginRun((int)(DateTime.UtcNow.Ticks&0x7fffffff)),RetroUi.Red);
            var resume=RetroUi.Button(root,"RunResume","続きから再開",1190,934,315,82,32,GameFlow.ResumeRun,shadow:false);
            resume.button.interactable=RunRuntimeStore.CanResume;
        }
        void Prepare()
        {
            var s=RunRuntimeStore.Current;bool boss=s.round==5;
            RunUi.Header(root,"PREPARE",GameFlow.ToModeSelect);RunUi.Title(root,boss?"最終戦・ボス":"次の一戦へ");
            RetroUi.Label(root,"RunRound",$"第{s.round}戦 / 全5戦",710,278,500,58,34,null,TextAnchor.MiddleCenter);
            RunUi.Progress(root,540,340,840,s.round,s.Wins);
            var left=DraftUi.Panel(root,"RunPlayerPanel",76,457,864,456);
            var right=DraftUi.Panel(root,"RunEnemyPanel",970,457,874,456);
            ChallengeScreen.Tag(left,"01",18,18,70);ChallengeScreen.Tag(right,"02",18,18,70);
            RetroUi.Label(left,"Heading","あなたの構成",114,18,700,60,44);
            RetroUi.Label(right,"Heading",boss?"最後の相手 / BOSS":"次の相手",114,18,730,60,44,boss?RetroUi.Blue:RetroUi.Ink);
            RunUi.Fighter(left,50,87,.57f,s.player,RetroUi.Red);
            RunUi.Fighter(right,38,87,.57f,s.Opponent.Build,RetroUi.Blue,true);
            RunUi.Hp(left,300,105,518,s);
            RetroUi.Label(left,"PlayerBuild",RunUi.BuildText(s.player),300,199,518,86,27);
            RunUi.PerkChips(left,s,300,314,518);
            RetroUi.Label(right,"EnemyName",s.Opponent.Name,300,102,530,66,43,RetroUi.Blue);
            RetroUi.Label(right,"EnemyHint",s.Opponent.Hint,300,176,530,95,29);
            RetroUi.Label(right,"EnemyBuild",RunUi.BuildText(s.Opponent.Build),300,274,530,60,25);
            RetroUi.Label(right,"EnemyWarning",s.Opponent.Warning,30,332,814,98,27,RetroUi.Muted);
            RetroUi.Button(root,"RunEditBuild","構成を調整",310,945,540,74,36,GameFlow.ToRunBuild,shadow:false);
            RetroUi.Button(root,"RunVersus",boss?"ボスに挑む →":"対戦へ →",980,945,630,74,40,Versus,RetroUi.Red);
        }
        void Versus()
        {
            root.gameObject.SetActive(false);
            var overlay=RetroUi.Screen("RunVsCanvas",out var canvas,5);
            versusCanvas=canvas;
            canvas.transform.SetParent(transform,false);
            var s=RunRuntimeStore.Current;
            RunUi.Header(overlay,"VS",()=>{Destroy(canvas.gameObject);root.gameObject.SetActive(true);},"← 次戦準備");
            RunUi.Progress(overlay,560,150,800,s.round,s.Wins);
            RetroUi.Group(overlay,"RunVersusInkRing",715,310,490,320).gameObject.AddComponent<DraftInkRing>();
            DraftUi.Title(overlay,"RunVsTitle","VS",760,330,400,260,190,RetroUi.Ink);
            RunUi.Fighter(overlay,230,275,1.08f,s.player,RetroUi.Red);
            RunUi.Fighter(overlay,1330,275,1.08f,s.Opponent.Build,RetroUi.Blue,true);
            var p=DraftUi.Panel(overlay,"RunVsPlayer",76,760,764,150);
            RunUi.Hp(p,24,14,710,s);
            RetroUi.Label(p,"Perks",RunUi.Perks(s),24,95,710,40,25);
            var e=DraftUi.Panel(overlay,"RunVsEnemy",1080,760,764,150);
            RetroUi.Label(e,"Opponent",s.Opponent.Name,24,12,710,60,38);
            RetroUi.Label(e,"OpponentHint",s.Opponent.Hint,24,80,710,55,26);
            RetroUi.Button(overlay,"RunStart","対戦開始 →",610,947,700,75,43,GameFlow.ToRunBattle,RetroUi.Red);
        }
        void Upgrade()
        {
            var s=RunRuntimeStore.Current;
            RunUi.Header(root,"UPGRADE",BackToResult,"← 結果");RunUi.Title(root,"勝利の強化を選ぶ");
            RetroUi.Label(root,"UpgradeNote","3つの候補からひとつ。選んだ効果は次戦から。",360,282,1200,58,32,null,TextAnchor.MiddleCenter);
            var strip=DraftUi.Panel(root,"RunUpgradeHpStrip",310,347,1300,84);
            RetroUi.Label(strip,"RoundCleared",$"第{s.round}戦 突破",24,10,310,62,33);
            RetroUi.Label(strip,"HpValue",$"残りHP {Mathf.CeilToInt(s.Hp)} / {Mathf.CeilToInt(s.MaxHp)}",358,10,400,62,31);
            RetroUi.Frame(strip,786,24,482,36,RetroUi.Ink,2);RetroUi.Block(strip,"HpFill",789,27,476*s.hpShare,30,RetroUi.Red);
            for(int i=0;i<3;i++)
            {
                int index=i;var u=RunCatalog.Upgrade(s.offers[i]);
                var p=DraftUi.Panel(root,"RunUpgradeCard"+i,76+i*598,458,572,446);
                var hit=RetroUi.Button(p,"RunUpgradeHit"+i,"",0,0,572,446,20,()=>{RunRuntimeStore.SelectUpgrade(s.offers[index]);RefreshChoices();},shadow:false);
                hit.image.color=Color.clear;hit.button.transition=Selectable.Transition.None;hit.image.GetComponent<Outline>().enabled=false;hit.image.GetComponent<RetroButtonMotion>().enabled=false;
                ChallengeScreen.Tag(p,(i+1).ToString("00"),18,18,70);
                RetroUi.Label(p,"UpgradeName",u.Name,110,18,432,64,43);
                RunUi.Symbol(p,u.Id,130,100,.78f);
                RetroUi.Label(p,"UpgradeEffect",u.Effect,24,264,524,54,33,RetroUi.Red,TextAnchor.MiddleCenter);
                RetroUi.Label(p,"UpgradeDescription",u.Description,30,u.Id==2?343:321,512,35,25,null,TextAnchor.MiddleCenter);
                if(u.Id==2)RetroUi.Label(p,"RecoveryPreview",$"{Mathf.CeilToInt(s.Hp)} → {Mathf.CeilToInt(Mathf.Min(s.MaxHp,s.Hp+30))} / {Mathf.CeilToInt(s.MaxHp)}",30,310,512,35,28,null,TextAnchor.MiddleCenter);
                choices[i]=RetroUi.Button(p,"RunUpgradeSelect"+i,"選択する",28,382,516,46,29,()=>{RunRuntimeStore.SelectUpgrade(s.offers[index]);RefreshChoices();},shadow:false);
                var border=RetroUi.Group(p,"UpgradeSelected",-3,-3,578,452);RetroUi.Frame(border,0,0,578,452,RetroUi.Red,3);borders[i]=border.gameObject;
            }
            confirm=RetroUi.Button(root,"RunUpgradeConfirm","この強化で次の一戦へ →",400,947,820,76,39,()=>{if(RunRuntimeStore.ConfirmUpgrade())GameFlow.ToRunScreen();},RetroUi.Red);
            RetroUi.Button(root,"RunNextEnemy","次の相手を確認",1270,947,360,76,30,PreviewEnemy,shadow:false);
            RefreshChoices();
        }
        void RefreshChoices()
        {
            var s=RunRuntimeStore.Current;
            for(int i=0;i<3;i++){bool selected=s.selectedUpgrade==s.offers[i];borders[i].SetActive(selected);RetroUi.Selected(choices[i],selected,RetroUi.Red);choices[i].label.text=selected?"選択中":"選択する";}
            confirm.button.interactable=Array.IndexOf(s.offers,s.selectedUpgrade)>=0;
        }
        void PreviewEnemy()
        {
            if(enemyModal!=null)return;
            var s=RunRuntimeStore.Current;var next=RunCatalog.Opponent(s.seed,s.round+1);
            var p=DraftUi.Panel(root,"RunNextEnemyModal",425,320,1070,540);
            enemyModal=p.gameObject;
            var blocker=RetroUi.Button(p,"RunPreviewBlocker","",-425,-320,1920,1080,20,()=>{},shadow:false);blocker.image.color=new Color(0,0,0,.18f);blocker.image.GetComponent<Outline>().enabled=false;blocker.image.GetComponent<RetroButtonMotion>().enabled=false;blocker.image.transform.SetAsFirstSibling();
            RetroUi.Block(p,"ModalPaper",3,3,1064,534,RetroUi.Paper).transform.SetAsFirstSibling();
            DraftUi.Title(p,"NextEnemyName",next.Name,40,30,990,70,46,RetroUi.Blue);
            RunUi.Fighter(p,70,130,.7f,next.Build,RetroUi.Blue);
            RetroUi.Label(p,"NextHint",next.Hint+"\n\n"+next.Warning,390,135,620,220,32);
            RetroUi.Button(p,"RunClosePreview","強化選択に戻る",360,435,520,65,34,()=>Destroy(p.gameObject),RetroUi.Ink,false);
        }
    }
}
