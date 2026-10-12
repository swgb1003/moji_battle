using System;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed class ChallengeScreen : MonoBehaviour
    {
        RectTransform root, body;
        UguiKit.ChoiceButton[] tabs;
        int filter;
        bool detail;
        RectTransform hoverFrame;
        CanvasGroup hoverAlpha;
        ChallengeCardHover focusedCard;
        public bool DetailVisible => detail;
        void Start()
        {
            if(Camera.main!=null) Camera.main.backgroundColor=RetroUi.Paper;
            root=RetroUi.Screen("ChallengeCanvas",out _);
            if(ChallengeRuntimeStore.ShowDetail && ChallengeRuntimeStore.Active) ShowDetail(); else ShowList();
        }
        void Clear()
        {
            focusedCard=null; hoverFrame=null; hoverAlpha=null;
            foreach(Transform child in root) if(child.name=="ChallengeContent")
            { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            body=RetroUi.Group(root,"ChallengeContent",0,0,1920,1080);
        }
        static void Fit(Text text,int min=18)
        { text.resizeTextForBestFit=true; text.resizeTextMinSize=min; text.resizeTextMaxSize=text.fontSize; text.verticalOverflow=VerticalWrapMode.Truncate; }
        static void Header(Transform target,string step,string back,Action action)
        {
            RetroUi.Header(target,step,action);
            target.Find("Back").GetComponentInChildren<Text>().text=back;
        }
        public void ShowList()
        {
            detail=false; ChallengeRuntimeStore.ShowDetail=false; Clear();
            Header(body,"02 / CHALLENGE / SELECT","← メニュー",GameFlow.ToModeSelect);
            RetroUi.Label(body,"ChallengeHeading","お題バトル",505,142,910,100,84,null,TextAnchor.MiddleCenter);
            RetroUi.Label(body,"ChallengeSubtitle","文字と構成で、勝ち筋を見つけよう",420,245,1080,46,30,null,TextAnchor.MiddleCenter);
            RetroUi.Label(body,"ChallengeCount",$"クリア {ChallengeRuntimeStore.ClearedCount} / {ChallengeCatalog.All.Length}",1530,184,310,46,28);
            RetroUi.Frame(body,1530,235,310,21,RetroUi.Ink,2);
            RetroUi.Block(body,"ChallengeProgress",1533,238,304*ChallengeRuntimeStore.ClearedCount/(float)ChallengeCatalog.All.Length,15,RetroUi.Red);
            string[] labels={"すべて","未クリア","クリア済み"}; tabs=new UguiKit.ChoiceButton[3];
            for(int i=0;i<3;i++)
            {
                int chosen=i; tabs[i]=RetroUi.Button(body,"ChallengeFilter"+i,labels[i],576+i*260,300,250,56,28,
                    ()=>{filter=chosen;ShowList();},shadow:false);
                RetroUi.Selected(tabs[i],i==filter,RetroUi.Ink);
            }
            RetroUi.Block(body,"FilterRuleLeft",76,328,478,2,RetroUi.Ink);
            RetroUi.Block(body,"FilterRuleRight",1376,328,468,2,RetroUi.Ink);
            int slot=0;
            foreach(var d in ChallengeCatalog.All)
            {
                bool cleared=ChallengeRuntimeStore.Progress(d.Id).cleared;
                if(filter==1 && cleared || filter==2 && !cleared) continue;
                Card(d,76+slot%3*597,376+slot/3*318,cleared); slot++;
            }
            hoverFrame=RetroUi.Group(body,"ChallengeHoverFrame",0,0,582,308);
            hoverAlpha=hoverFrame.gameObject.AddComponent<CanvasGroup>();
            hoverAlpha.alpha=0; hoverAlpha.blocksRaycasts=false; hoverAlpha.interactable=false;
            RetroUi.Frame(hoverFrame,0,0,582,308,RetroUi.Red,3);
            if(slot==0) RetroUi.Label(body,"ChallengeEmpty",filter==2?"まだクリアしたお題はありません。":"すべてのお題をクリアしました！",
                350,550,1220,100,38,null,TextAnchor.MiddleCenter);
            RetroUi.Label(body,"ChallengeListHint","ⓘ お題を選ぶと、相手と条件を確認できます",400,1006,1120,35,24,null,TextAnchor.MiddleCenter);
        }
        void Card(ChallengeDefinition d,float x,float y,bool cleared)
        {
            const float height=300;
            var panel=RetroUi.Group(body,"ChallengeCard"+d.Id,x,y,574,height);
            var hit=RetroUi.Button(panel,"ChallengeSelect"+d.Id,"",0,0,574,height,20,()=>Select(d.Id),shadow:false);
            hit.image.color=new Color(0,0,0,0); hit.image.GetComponent<Outline>().enabled=false;
            RetroUi.Frame(panel,0,0,574,height,RetroUi.Ink,2); RetroUi.Frame(panel,8,8,558,height-16,RetroUi.Rule,1);
            RetroUi.Block(panel,"NumberPlate",16,16,72,54,RetroUi.Ink);
            RetroUi.Label(panel,"ChallengeNumber",(d.Id+1).ToString("00"),16,16,72,54,38,RetroUi.Paper,TextAnchor.MiddleCenter);
            Fit(RetroUi.Label(panel,"ChallengeName",d.Name,104,14,cleared?320:450,56,35),25);
            RetroUi.Fighter(panel,20,90,.34f,d.PlayerGlyph,RetroUi.Red);
            RetroUi.Fighter(panel,220,90,.34f,d.OpponentGlyph,RetroUi.Blue,true);
            RetroUi.Label(panel,"Versus","VS",158,177,75,48,29,null,TextAnchor.MiddleCenter);
            RetroUi.Block(panel,"Ground",20,232,324,2,RetroUi.Ink);
            RetroUi.Block(panel,"CardDivider",398,85,1,148,RetroUi.Rule);
            RetroUi.Label(panel,"DifficultyCaption","難易度",414,84,144,32,23);
            for(int i=0;i<3;i++)
            {
                var dot=RetroUi.Block(panel,"DifficultyDot"+i,414+i*35,128,22,22,i<d.Difficulty?RetroUi.Red:RetroUi.Paper);
                dot.sprite=UiKit.DiscSprite;UguiKit.AddOutline(dot.gameObject,1,i<d.Difficulty?RetroUi.Red:RetroUi.Ink);
            }
            Fit(RetroUi.Label(panel,"ChallengeSummary",d.Summary,414,158,144,72,24));
            if(cleared) Stamp(panel,"CLEAR",432,20,116,36);
            var start=RetroUi.Button(panel,"ChallengeStart"+d.Id,"挑戦する →",20,244,534,42,28,()=>Select(d.Id),shadow:false);
            panel.gameObject.AddComponent<ChallengeCardHover>().Bind(this,hit,start);
        }
        internal bool IsFocused(ChallengeCardHover card)=>focusedCard==card;
        internal void Focus(ChallengeCardHover card)
        {
            if(hoverFrame==null || !card.gameObject.activeInHierarchy)return;
            focusedCard=card;
            if(hoverAlpha.alpha<.01f)hoverFrame.anchoredPosition=card.Position+new Vector2(-4,4);
        }
        internal void Unfocus(ChallengeCardHover card)
        { if(focusedCard==card)focusedCard=null; }
        public void Select(int id) { ChallengeRuntimeStore.Select(id); ShowDetail(); }
        public void ShowDetail()
        {
            var d=ChallengeRuntimeStore.Current; if(d==null){ShowList();return;}
            detail=true; Clear();
            Header(body,"CHALLENGE / BRIEFING","← お題一覧",ShowList);
            RetroUi.Button(body,"ChallengeSettings","⚙ 設定",1260,55,220,60,30,SettingsScreen.Open,RetroUi.Ink,false);
            RetroUi.Heading(body,d.Name,76);
            RetroUi.Label(body,"ChallengeDifficulty",$"#{d.Id+1:00} / 難易度 "+new string('●',d.Difficulty)+new string('○',3-d.Difficulty),
                500,235,920,42,28,null,TextAnchor.MiddleCenter);
            var left=Panel(body,"ChallengeOpponent",80,278,910,677);
            Tag(left,"VS プレビュー",18,18,204);
            RetroUi.Fighter(left,64,107,.61f,d.PlayerGlyph,RetroUi.Red);
            RetroUi.Fighter(left,544,107,.61f,d.OpponentGlyph,RetroUi.Blue,true);
            RetroUi.Label(left,"VS","VS",354,251,170,92,54,null,TextAnchor.MiddleCenter);
            RetroUi.Block(left,"DuelGround",40,363,830,3,RetroUi.Ink);
            RetroUi.Label(left,"PlayerPreviewName","あなた / "+d.PlayerGlyph,44,381,390,50,32,null,TextAnchor.MiddleCenter);
            RetroUi.Label(left,"OpponentPreviewName","相手 / "+d.OpponentGlyph,477,381,390,50,32,null,TextAnchor.MiddleCenter);
            RetroUi.Block(left,"OpponentDivider",18,442,874,2,RetroUi.Ink); Tag(left,"相手の構成",18,462,180);
            var opponent=d.Opponent;
            string[] summary={opponent.weaponSize+"サイズ",CustomizeLabels.Grip(opponent.gripType),CustomizeLabels.StyleShort(opponent.battleStyle)};
            for(int i=0;i<3;i++)
            {
                RetroUi.Label(left,"OpponentStat"+i,summary[i],24+i*290,516,270,42,31,null,TextAnchor.MiddleCenter);
                if(i<2) RetroUi.Block(left,"StatDivider",312+i*290,513,1,62,RetroUi.Rule);
            }
            RetroUi.Block(left,"SpecialRule",22,590,866,1,RetroUi.Rule);
            RetroUi.Label(left,"OpponentSpecial","必殺技："+SpecialMovePlan.StandardName(opponent.character),44,606,822,43,32);
            var condition=Panel(body,"ChallengeCondition",1012,278,832,160); Tag(condition,"クリア条件",18,18,178);
            Fit(RetroUi.Label(condition,"ConditionText",d.Condition,36,70,758,70,46),28);
            var restriction=Panel(body,"ChallengeRestriction",1012,450,832,150);
            Tag(restriction,d.FixedSize.HasValue?"使用サイズ":"使用文字",18,18,188);
            if(d.FixedCharacter!=null || d.FixedSize.HasValue)
            {
                RetroUi.Frame(restriction,320,18,96,70,RetroUi.Rule,1);
                RetroUi.Label(restriction,"RestrictionGlyph",d.FixedCharacter??d.FixedSize.Value.ToString(),320,18,96,70,42,null,TextAnchor.MiddleCenter);
                RetroUi.Label(restriction,"RestrictionText","固定",448,22,320,52,36);
            }
            else RetroUi.Label(restriction,"RestrictionText","文字・サイズは自由",284,22,510,52,36);
            Fit(RetroUi.Label(restriction,"EditableText",d.Editable,24,90,784,42,25),19);
            var hint=Panel(body,"ChallengeHint",1012,612,832,132); Tag(hint,"攻略のヒント",18,18,204);
            Fit(RetroUi.Label(hint,"HintText",d.Hint,32,70,768,44,25),20);
            var reward=Panel(body,"ChallengeReward",1012,756,832,199); Tag(reward,"初回クリア報酬",18,18,234);
            AchievementBadge.Create(reward,d.RewardId,true,245,44,150);
            RetroUi.Label(reward,"RewardCaption","称号",430,60,372,40,28);
            Fit(RetroUi.Label(reward,"RewardName","「"+AchievementCatalog.Get(d.RewardId).Name+"」",430,105,372,68,40),27);
            RetroUi.Button(body,"ChallengeListBack","← お題一覧へ",245,974,590,60,30,ShowList,shadow:false);
            RetroUi.Button(body,"ChallengePrepare","構成をつくる →",1084,970,602,54,32,GameFlow.ToChallengePrepare,RetroUi.Red);
        }
        public static RectTransform Panel(Transform parent,string name,float x,float y,float w,float h)
        {
            var panel=RetroUi.Group(parent,name,x,y,w,h); RetroUi.Frame(panel,0,0,w,h,RetroUi.Ink,2);
            RetroUi.Frame(panel,8,8,w-16,h-16,RetroUi.Rule,1); return panel;
        }
        public static void Tag(Transform root,string label,float x,float y,float w)
        { RetroUi.Block(root,"TagPlate",x,y,w,42,RetroUi.Ink); RetroUi.Label(root,"TagText",label,x+8,y,w-16,42,24,RetroUi.Paper); }
        public static void Stamp(Transform root,string label,float x,float y,float w,float h)
        {
            var group=RetroUi.Group(root,"Stamp_"+label,x,y,w,h); RetroUi.Frame(group,0,0,w,h,RetroUi.Red,3);
            RetroUi.Label(group,"StampText",label,2,0,w-4,h,Mathf.RoundToInt(h*.7f),RetroUi.Red,TextAnchor.MiddleCenter);
            group.localRotation=Quaternion.Euler(0,0,12);
        }
        public static void LockIcon(Transform parent,float x,float y)
        {
            var group=RetroUi.Group(parent,"LockIcon",x,y,32,42);
            var arch=RetroUi.Block(group,"LockArch",4,0,24,32,RetroUi.Muted);arch.sprite=UiKit.DiscSprite;
            var hole=RetroUi.Block(group,"LockArchHole",9,5,14,25,RetroUi.Paper);hole.sprite=UiKit.DiscSprite;
            RetroUi.Block(group,"LockBody",0,18,32,24,RetroUi.Muted);
            var key=RetroUi.Block(group,"LockKey",13,24,6,6,RetroUi.Paper);key.sprite=UiKit.DiscSprite;
            RetroUi.Block(group,"LockKeyStem",14.5f,28,3,6,RetroUi.Paper);
        }
        void Update()
        {
            if(hoverFrame!=null)
            {
                float step=RetroMotionSettings.Reduced?1:1-Mathf.Exp(-24*Time.unscaledDeltaTime);
                hoverAlpha.alpha=Mathf.Lerp(hoverAlpha.alpha,focusedCard!=null?1:0,step);
                if(focusedCard!=null)hoverFrame.anchoredPosition=Vector2.Lerp(hoverFrame.anchoredPosition,focusedCard.Position+new Vector2(-4,4),step);
            }
            if(!SettingsScreen.BlocksInput && Input.GetKeyDown(KeyCode.Escape)){if(detail)ShowList();else GameFlow.ToModeSelect();}
        }
    }
}
