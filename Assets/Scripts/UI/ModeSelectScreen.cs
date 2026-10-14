using UnityEngine;

namespace MojiBattle
{
    public sealed class ModeSelectScreen : MonoBehaviour
    {
        UnityEngine.UI.Text achievementCount;
        GameObject achievementNew;
        RectTransform hoverFrame;
        CanvasGroup hoverAlpha;
        ModeCardHover hoveredCard, selectedCard;
        void Start()
        {
            if (Camera.main != null) Camera.main.backgroundColor = RetroUi.Paper;
            var root = RetroUi.Screen("ModeSelectCanvas", out _);
            RetroUi.Header(root, "01 / MODE SELECT", GameFlow.ToTitle);
            RetroUi.Button(root, "MenuSettings", "⚙ 設定", 1260, 55, 220, 60, 30, SettingsScreen.Open, RetroUi.Ink, false);
            RetroUi.Heading(root, "遊び方を選ぶ", 88);
            Card(root, 78, "BattleMode", "01", "バトル", "文字を自由にカスタマイズ", "バトルへ →", RetroUi.Red, GameFlow.ToCustomize, false);
            Card(root, 526, "TournamentMode", "02", "トーナメント", "大会を選んで頂点を目指す", "大会へ →", RetroUi.Blue, GameFlow.ToTournamentSelect, true);
            Card(root, 974, "ChallengeMode", "03", "お題バトル", "条件を攻略して腕を試す", "お題を選ぶ →", RetroUi.Red, GameFlow.ToChallenges, false);
            Card(root, 1422, "DraftMode", "04", "文字ドラフト", "候補から選んだ文字で戦う", "ドラフトへ →", RetroUi.Red, GameFlow.ToDraftIntro, false);
            var run=DraftUi.Panel(root,"RunMode",78,724,1040,284);
            var runHit=CardHit(run,"RunMode",GameFlow.ToRunIntro);
            ChallengeScreen.Tag(run,"05",18,18,70);
            RetroUi.Label(run,"Title","勝ち抜き・育成",114,15,600,68,44);
            ChallengeScreen.Stamp(run,"NEW",910,25,92,34);
            RetroUi.Label(run,"Caption","一文字を育て、五連戦を突破せよ。",32,93,970,50,30);
            RunUi.Progress(run,36,162,500,1,0);
            var runStart=RetroUi.Button(run,"RunModeStart","挑戦を始める →",610,180,394,68,34,GameFlow.ToRunIntro,RetroUi.Red);
            run.gameObject.AddComponent<ModeCardHover>().Bind(this,runHit,runStart);
            // 右の列: 練習・友人と対戦・称号（各 88 の高さ）
            var practice=DraftUi.Panel(root,"PracticeMode",1142,724,700,88);
            var practiceHit=CardHit(practice,"PracticeMode",GameFlow.ToPractice);
            ChallengeScreen.Tag(practice,"06",18,14,70);
            RetroUi.Fighter(practice,112,6,.17f,"練",RetroUi.Red);
            RetroUi.Label(practice,"Title","練習",228,4,200,48,33);
            RetroUi.Label(practice,"Caption","自操作で技を試す",228,48,260,32,20);
            var practiceStart=RetroUi.Button(practice,"PracticeModeStart","練習へ →",502,18,168,52,27,GameFlow.ToPractice,RetroUi.Red);
            practice.gameObject.AddComponent<ModeCardHover>().Bind(this,practiceHit,practiceStart);
            var friends=DraftUi.Panel(root,"FriendMode",1142,822,700,88);
            var friendHit=CardHit(friends,"FriendMode",()=>GameFlow.ToFriendRoom());
            ChallengeScreen.Tag(friends,"07",18,14,70);
            var symbol=RetroUi.Group(friends,"FriendConnectionIcon",120,8,80,70).gameObject.AddComponent<AchievementSymbolGraphic>();symbol.friends=true;symbol.raycastTarget=false;
            RetroUi.Label(friends,"Title","友人と対戦",228,4,260,48,33);
            RetroUi.Label(friends,"Caption","ルームコードでつながる",228,48,260,32,20);
            var friendStart=RetroUi.Button(friends,"FriendModeStart","対戦へ →",502,18,168,52,27,()=>GameFlow.ToFriendRoom(),RetroUi.Ink);
            friends.gameObject.AddComponent<ModeCardHover>().Bind(this,friendHit,friendStart);
            var awards=DraftUi.Panel(root,"AchievementsMode",1142,920,700,88);
            var awardsHit=CardHit(awards,"AchievementsMode",GameFlow.ToAchievements);
            ChallengeScreen.Tag(awards,"08",18,14,70);
            AchievementBadge.Create(awards,3,true,120,6,76);
            RetroUi.Label(awards,"Title","称号",228,4,150,48,33);
            achievementCount=RetroUi.Label(awards,"Count","",228,48,265,32,22);
            var newRoot=RetroUi.Group(awards,"NewAchievements",378,14,70,29);
            RetroUi.Block(newRoot,"Plate",0,0,70,29,RetroUi.Red);RetroUi.Label(newRoot,"Label","NEW",0,0,70,29,22,RetroUi.Paper,TextAnchor.MiddleCenter);
            achievementNew=newRoot.gameObject;
            var awardsStart=RetroUi.Button(awards,"AchievementsStart","称号を見る →",502,18,168,52,25,GameFlow.ToAchievements,RetroUi.Ink);
            awards.gameObject.AddComponent<ModeCardHover>().Bind(this,awardsHit,awardsStart);
            CreateHoverFrame(root);
            AchievementStore.Changed+=RefreshAchievements;
            RefreshAchievements();
        }
        void Card(Transform root, float x, string name, string number, string title, string description, string label,
            Color color, System.Action action, bool tournament)
        {
            var panel = RetroUi.Group(root, name, x, 275, 420, 425);
            var hit=CardHit(panel,name,action);
            RetroUi.Frame(panel, 0, 0, 420, 425, RetroUi.Ink, 3);
            RetroUi.Frame(panel, 10, 10, 400, 405, RetroUi.Ink, 1);
            bool challenge=name=="ChallengeMode";
            bool draft=name=="DraftMode";
            if(draft) ChallengeScreen.Stamp(panel,"NEW",334,22,65,30);
            RetroUi.Block(panel, "NumberPlate", 18, 18, 60, 60, RetroUi.Ink);
            RetroUi.Label(panel, "Number", number, 18, 18, 60, 60, 36, RetroUi.Paper, TextAnchor.MiddleCenter);
            var heading=RetroUi.Label(panel, "Heading", title, 84, 16, draft?255:314,70,38,null,TextAnchor.MiddleCenter);
            heading.resizeTextForBestFit=true; heading.resizeTextMinSize=27; heading.resizeTextMaxSize=38;
            RetroUi.Block(panel, "HeadingRule", 25, 96, 370, 2, RetroUi.Ink);
            if (tournament)
            {
                RetroUi.Group(panel, "TournamentIcon", 50, 106, 320, 180).gameObject.AddComponent<TournamentMenuGraphic>();
            }
            else if(draft) DraftUi.CardsArt(panel,92,119,.62f);
            else
            {
                RetroUi.Fighter(panel, 24, 124, .36f, challenge?"一":"火", RetroUi.Red);
                RetroUi.Fighter(panel, 256, 124, .36f, challenge?"刀":"水", RetroUi.Blue, true);
                RetroUi.Label(panel, "Versus", "VS", 163, 212, 100, 60, 34, null, TextAnchor.MiddleCenter);
                RetroUi.Block(panel, "Ground", 30, 285, 360, 4, RetroUi.Ink);
            }
            RetroUi.Block(panel, "DescriptionRule", 30, 300, 360, 2, RetroUi.Ink);
            RetroUi.Label(panel, "Description", description, 18, 305, 384, 42, 24, null, TextAnchor.MiddleCenter);
            var start=RetroUi.Button(panel, name + "Start", label, 28, 354, 358, 50, 32, action, color);
            panel.gameObject.AddComponent<ModeCardHover>().Bind(this,hit,start);
            RetroUi.Enter(panel.gameObject, tournament ? .16f : .08f, 36);
        }
        static UguiKit.ChoiceButton CardHit(RectTransform panel,string name,System.Action action)
            => RetroUi.Button(panel,name+"Select","",0,0,panel.rect.width,panel.rect.height,20,action,shadow:false);
        void CreateHoverFrame(Transform root)
        {
            hoverFrame=RetroUi.Group(root,"ModeHoverFrame",0,0,576,556);
            hoverAlpha=hoverFrame.gameObject.AddComponent<CanvasGroup>();
            hoverAlpha.alpha=0;hoverAlpha.blocksRaycasts=false;hoverAlpha.interactable=false;
            void Edge(string name,Vector2 min,Vector2 max,Vector2 pivot,Vector2 size)
            {
                var edge=RetroUi.Block(hoverFrame,name,0,0,0,0,RetroUi.Red).rectTransform;
                edge.anchorMin=min;edge.anchorMax=max;edge.pivot=pivot;edge.sizeDelta=size;edge.anchoredPosition=Vector2.zero;
            }
            Edge("Top",new Vector2(0,1),Vector2.one,new Vector2(.5f,1),new Vector2(0,3));
            Edge("Bottom",Vector2.zero,new Vector2(1,0),new Vector2(.5f,0),new Vector2(0,3));
            Edge("Left",Vector2.zero,new Vector2(0,1),new Vector2(0,.5f),new Vector2(3,0));
            Edge("Right",new Vector2(1,0),Vector2.one,new Vector2(1,.5f),new Vector2(3,0));
        }
        internal void HoverCard(ModeCardHover card)=>hoveredCard=card;
        internal void ExitCard(ModeCardHover card){if(hoveredCard==card)hoveredCard=null;}
        internal void SelectCard(ModeCardHover card)=>selectedCard=card;
        internal void DeselectCard(ModeCardHover card){if(selectedCard==card)selectedCard=null;}
        void Update()
        {
            if(hoverFrame!=null)
            {
                var focus=SettingsScreen.IsOpen?null:hoveredCard!=null?hoveredCard:selectedCard;
                float step=RetroMotionSettings.Reduced?1:1-Mathf.Exp(-24*Time.unscaledDeltaTime);
                if(focus!=null)
                {
                    var position=focus.Rect.anchoredPosition+new Vector2(-4,4);
                    var size=focus.Rect.rect.size+Vector2.one*8;
                    if(hoverAlpha.alpha<.01f){hoverFrame.anchoredPosition=position;hoverFrame.sizeDelta=size;}
                    else
                    {
                        hoverFrame.anchoredPosition=Vector2.Lerp(hoverFrame.anchoredPosition,position,step);
                        hoverFrame.sizeDelta=Vector2.Lerp(hoverFrame.sizeDelta,size,step);
                    }
                }
                hoverAlpha.alpha=Mathf.Lerp(hoverAlpha.alpha,focus!=null?1:0,step);
            }
            if (!SettingsScreen.BlocksInput && Input.GetKeyDown(KeyCode.Escape)) GameFlow.ToTitle();
        }
        void RefreshAchievements()
        {
            if(achievementCount==null) return;
            achievementCount.text=$"獲得 {AchievementStore.Profile.Count} / {AchievementCatalog.All.Length}";
            achievementNew.SetActive(AchievementStore.Profile.UnseenCount>0);
        }
        void OnDestroy() => AchievementStore.Changed-=RefreshAchievements;
    }
}
