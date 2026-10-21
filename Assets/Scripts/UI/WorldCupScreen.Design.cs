using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using D=MojiBattle.WorldCupDesign;

namespace MojiBattle
{
    public sealed partial class WorldCupScreen
    {
        void Header(string phase,Action back=null,string label="← 戻る")=>D.Header(root,"WORLD MOJI CUP / "+phase,back??GameFlow.ToTournamentSelect,label);
        static Text T(Transform p,string name,string value,float x,float y,float w,float h,int size=28,Color? c=null,TextAnchor a=TextAnchor.MiddleLeft)
            =>D.Text(p,name,value,x,y,w,h,size,c,a,size>=34);
        UguiKit.ChoiceButton Button(string name,string text,float x,float y,float w,Action action,Color? color=null,float h=68,int size=32)
            =>D.Button(root,name,text,x,y,w,h,action,color,size);
        string Glyph(int id)=>id<0?"—":Session.Entrants[id].Build.character;
        static string Special(FighterBuildData b)=>b.EquippedSpecial?.name??SpecialMovePlan.StandardName(b);
        string Pair(WorldCupMatch m)=>m==null?"—":Glyph(m.LeftId)+" VS "+Glyph(m.RightId);
        void Fighter(Transform p,int id,float x,float y,float w,float h,bool mirror=false,string pose="ready")
        {if(id>=0)D.Fighter(p,x,y,w,h,Session.Entrants[id].Build,id==0?D.Red:D.Navy,mirror,pose,pose=="kneel");}
        void Select()
        {
            D.Header(root,"TOURNAMENT / SELECT",GameFlow.ToModeSelect,"← メニュー",D.Navy);
            D.Title(root,"<color=#AE2319>大会</color>を選ぶ",83,108);D.HeadingRules(root,147);
            SelectCard(0,57,TournamentSession.CupName,"8文字の大会で優勝を目指す\nサイズ S限定",GameFlow.ToTournamentCustomize);
            SelectCard(1,855,WorldCupSession.CupName,"48文字・12グループ\nサイズ M限定\nグループリーグから世界一へ",GameFlow.ToWorldCupGuide);
        }
        void SelectCard(int i,float x,string name,string copy,Action action)
        {
            var p=D.Panel(root,"TournamentChoice"+i,x,212,760,689);
            D.NumberPlaque(p,"CupNumber",22,18,139,85,i==0?D.Navy:D.Red);T(p,"CupNumberLabel",(i+1).ToString("00"),23,17,131,86,60,D.Paper,TextAnchor.MiddleCenter).fontStyle=FontStyle.Italic;
            T(p,"CupName",name,178,18,560,86,i==0?35:45);
            if(i==1){var stamp=T(p,"NewStamp","NEW",625,20,116,58,33,D.Red,TextAnchor.MiddleCenter);D.Frame(stamp.transform,0,0,116,58,D.Red,3);stamp.rectTransform.localRotation=Quaternion.Euler(0,0,10);}
            D.Art(p,i==0?"watanabe_cup_bracket":"world_cup_emblem",25,106,710,316);
            D.Block(p,"CupDividerLeft",45,443,320,2,D.Ink);D.Block(p,"CupDividerRight",395,443,320,2,D.Ink);D.Diamond(p,380,443,D.Ink);
            T(p,"CupSummary",copy,30,454,700,111,31,null,TextAnchor.MiddleCenter);
            var b=D.Button(p,"TournamentChoiceStart"+i,i==0?"ワタナベチャレンジカップへ →":"ワールド文字カップへ →",100,578,560,84,action,i==0?D.Navy:D.Red,i==0?28:36);
            var hit=p.gameObject.AddComponent<Image>();hit.color=Color.clear;
            p.gameObject.AddComponent<WorldCupCardFocus>().Bind(p,b);
        }
        void Guide()
        {
            // The guide's hero deliberately shares the upper left area with a compact header.
            D.Art(root,"world_cup_guide_landscape",603,225,1050,184,false);
            D.Art(root,"world_cup_guide_hero",106,32,484,384);
            Header("GUIDE",null,"← 戻る");
            D.Text(root,"WorldCupTitle","ワールド<color=#AE2319>文字</color>カップ",616,108,1010,144,97,D.Navy,TextAnchor.MiddleCenter,true);
            D.Text(root,"CupSubtitle","48文字の頂点へ",841,271,510,61,45,D.Navy,TextAnchor.MiddleCenter,true);
            D.Block(root,"SubtitleLeft",705,298,141,2,D.Ink);D.Block(root,"SubtitleRight",1358,298,144,2,D.Ink);
            string[] titles={"参加条件","グループリーグ","決勝トーナメント"},assets={"world_cup_rule_m","world_cup_rule_groups","world_cup_rule_knockout"};
            string[][] copy={new[]{"文字サイズはMのみ","構成は大会中固定"},new[]{"12グループ・各4文字","1・2位＋成績上位の3位8文字が進出"},new[]{"32文字で勝ち抜き","準決勝敗者は3位決定戦へ"}};
            float[] xs={55,550,1125},ws={484,561,494};
            for(int i=0;i<3;i++)
            {
                var p=D.Panel(root,"WorldCupRule"+i,xs[i],421,ws[i],392);
                D.InkFill(p,"RuleBand",8,8,ws[i]-16,78,D.Navy);D.NumberPlaque(p,"NumberBand",8,8,117,78,D.Red);
                T(p,"RuleNumber",(i+1).ToString("00"),15,8,94,78,54,D.Paper,TextAnchor.MiddleCenter);
                T(p,"RuleTitle",titles[i],143,8,ws[i]-158,78,i==2?32:39,D.Paper);
                D.Art(p,assets[i],17,96,ws[i]-34,193);
                D.Check(p,copy[i][0],i==0?100:60,294,ws[i]-80,24);
                D.Check(p,copy[i][1],i==0?100:60,338,ws[i]-80,i==1?22:24);
            }
            T(root,"PointsRule","勝ち3点・引き分け1点・負け0点",70,867,395,43,20);
            Button("WorldCupBegin","文字を決める →",486,835,410,GameFlow.ToWorldCupCustomize,D.Red,70,40);
            var b=Button("WorldCupResume","続きから再開",921,842,273,GameFlow.ResumeWorldCup,null,62,29);b.button.interactable=WorldCupRuntimeStore.CanResume;
            T(root,"GuideRecoveryNote","毎試合HP・ゲージを回復",1225,836,350,28,18,D.Navy);
            T(root,"GuideThirdNote","3位比較：KO勝ち数 → タイムアップ勝ちの残りHP",1225,870,363,40,16,D.Navy);
        }
        void Draw()
        {
            Header("DRAW");D.Title(root,"組み合わせ抽選",72,69);
            T(root,"DrawSubtitle","出場 <color=#AE2319>48</color> 文字 / <color=#AE2319>12</color>グループ",545,167,600,40,31,null,TextAnchor.MiddleCenter);
            for(int g=0;g<12;g++)
            {
                bool own=g==Session.PlayerGroup;var p=D.Panel(root,"DrawGroup"+g,42+g%4*403,215+g/4*168,386,146);
                if(own)D.Frame(p,0,0,386,146,D.Red,3);
                D.InkFill(p,"GroupBand",4,4,142,35,own?D.Red:D.Navy);T(p,"GroupLetter",((char)('A'+g)).ToString(),5,4,50,35,33,D.Paper,TextAnchor.MiddleCenter);T(p,"GroupName","グループ",60,4,86,35,20,D.Paper);
                var entrants=Session.Entrants.Where(e=>e.Group==g).ToArray();
                for(int i=0;i<4;i++){Fighter(p,entrants[i].Id,10+i*94,40,90,101);}
                if(own)T(p,"DrawPlayerTag","YOU",154,4,70,35,22,D.Red,TextAnchor.MiddleLeft);
                RetroUi.Enter(p.gameObject,RetroMotionSettings.Reduced?0:g*.05f,12);
            }
            D.Block(root,"DrawFooterRule",20,717,1632,2,D.Ink);Fighter(root,0,66,728,345,175);
            D.Block(root,"OwnGroupDivider",482,741,1,161,D.Ink);
            T(root,"OwnGroup",$"あなたは <color=#AE2319>グループ{(char)('A'+Session.PlayerGroup)}</color> です",530,724,550,58,37);
            T(root,"OwnOpponents","対戦相手　"+string.Join("・",Session.Entrants.Where(e=>e.Group==Session.PlayerGroup && e.Id!=0).Select(e=>e.Build.character)),535,790,540,39,27,D.Navy);
            var ownEntrants=Session.Entrants.Where(e=>e.Group==Session.PlayerGroup).ToArray();for(int i=0;i<4;i++)Fighter(root,ownEntrants[i].Id,625+i*106,831,99,73);
            Button("WorldCupDrawNext","グループリーグへ →",1172,754,439,()=>Open(WorldCupPage.Groups),D.Ink,76,32);
            Button("WorldCupDrawSkip","抽選スキップ",1287,852,210,()=>Open(WorldCupPage.Groups),D.Ink,47,23);
        }
        void Tabs()
        {for(int i=0;i<12;i++){int g=i;var b=Button("WorldCupGroupTab"+i,((char)('A'+i)).ToString(),568+i*86,114,86,()=>{group=g;Build();},null,68,34);D.Selected(b,i==group);}}
        void Table(Transform p,int g,float x,float y,float width,float rowHeight=67,bool summary=false)
        {
            var rows=Session.Standings(g);string[] heads=summary?new[]{"順位","文字","勝","分","負","勝点"}:new[]{"順位","文字","試合","勝","分","負","勝点"};
            float[] fractions=summary?new[]{0,.12f,.38f,.52f,.66f,.80f,1}:new[]{0,.13f,.28f,.41f,.53f,.65f,.77f,1};
            D.InkFill(p,"TableBand",x,y,width,summary?57:44,summary?D.Navy:D.Paper);float hh=summary?57:44;
            for(int c=0;c<heads.Length;c++)T(p,"TableHead"+c,heads[c],x+width*fractions[c],y,width*(fractions[c+1]-fractions[c]),hh,25,summary?D.Paper:D.Ink,TextAnchor.MiddleCenter);
            for(int r=0;r<4;r++)
            {
                var s=rows[r];float ry=y+hh+r*rowHeight;
                Color wash=s.Id==0?new Color(.85f,.20f,.12f,.14f):r==1?new Color(.8f,.66f,.2f,.13f):Color.clear;
                if(wash.a>0)D.Block(p,"RankWash",x,ry,width,rowHeight,wash);
                string[] values=summary?new[]{(r+1).ToString(),Glyph(s.Id),s.Wins.ToString(),s.Draws.ToString(),s.Losses.ToString(),s.Points.ToString()}:new[]{(r+1).ToString(),Glyph(s.Id),s.Played.ToString(),s.Wins.ToString(),s.Draws.ToString(),s.Losses.ToString(),s.Points.ToString()};
                for(int c=0;c<values.Length;c++)T(p,"StandingCell"+r+"_"+c,values[c],x+width*fractions[c],ry,width*(fractions[c+1]-fractions[c]),rowHeight,c==1?46:30,s.Id==0?D.Red:c==1?D.Navy:D.Ink,TextAnchor.MiddleCenter);
                if(s.Id==0 && !summary)T(p,"PlayerRowTag","YOU",x+width-87,ry+13,77,38,21,D.Red,TextAnchor.MiddleCenter);
                D.Block(p,"RowRule",x,ry+rowHeight,width,1,D.Rule);
            }
            for(int c=0;c<fractions.Length;c++)D.Block(p,"ColumnRule",x+width*fractions[c],y,1,hh+4*rowHeight,D.Rule);
        }
        void Groups()
        {
            Header("GROUP");D.Text(root,"WorldCupTitle","グループリーグ",49,101,505,89,59,null,TextAnchor.MiddleLeft,true);Tabs();
            var p=D.Panel(root,"GroupStandings",41,203,952,410,$"グループ{(char)('A'+group)}");
            T(p,"LeagueStatus",$"第{Mathf.Max(1,Session.PlayerGroupGames)}節終了",715,9,220,61,28,D.Paper,TextAnchor.MiddleRight);Table(p,group,13,84,926,67);
            var fixtures=D.Panel(root,"GroupFixtures",41,625,952,254,"これまでの対戦結果");
            var matches=Session.Matches.Where(m=>m.Group==group).ToArray();
            for(int i=0;i<6;i++){var m=matches[i];float x=20+i/3*474,y=78+i%3*57;D.Frame(fixtures,x,y,435,56,D.Rule,1);T(fixtures,"Fixture"+i,$"{Glyph(m.LeftId)}　{(m.Complete?m.WinnerId<0?"△":m.WinnerId==m.LeftId?"○":"●":"VS")}　{Glyph(m.RightId)}",x+10,y,415,56,31,m.IncludesPlayer?D.Red:D.Navy,TextAnchor.MiddleCenter);}
            NextPanel(1007,203,628,523);
            D.Frame(root,1007,738,628,62,D.Ink,2);T(root,"QualifyingRule","1・2位は決勝へ　/　3位は成績上位8文字",1024,746,594,47,24,D.Navy,TextAnchor.MiddleCenter);
            Button("WorldCupPause","大会を中断",1007,814,251,GameFlow.ToTournamentSelect,D.Ink,78,32);
            primary=Button("WorldCupNext","次の試合へ →",1284,814,350,Next,D.Red,78,38);primary.button.interactable=Session.NextPlayerMatch!=null;
            calculation=T(root,"WorldCupCalculation","",49,896,1570,18,15,D.Navy);
        }
        void NextPanel(float x,float y,float w,float h)
        {
            var p=D.Panel(root,"WorldCupNextOpponent",x,y,w,h,"次の対戦");var m=Session.NextPlayerMatch;
            if(m==null){T(p,"Waiting","対戦結果を集計しています",25,130,w-50,180,30,D.Navy,TextAnchor.MiddleCenter);return;}
            T(p,"UpcomingRound",m.Name,25,82,w-50,47,31,null,TextAnchor.MiddleCenter);
            float fy=126,fh=h-236;Fighter(p,m.LeftId,22,fy,w*.41f,fh);Fighter(p,m.RightId,w*.58f,fy,w*.39f,fh,true);
            T(p,"VS","VS",w*.41f,fy+fh*.38f,w*.17f,66,43,null,TextAnchor.MiddleCenter);
            D.InkFill(p,"LeftName",w*.11f,h-137,w*.24f,48,m.LeftId==0?D.Red:D.Navy);T(p,"LeftGlyph",Glyph(m.LeftId),w*.11f,h-137,w*.24f,48,36,D.Paper,TextAnchor.MiddleCenter);
            D.InkFill(p,"RightName",w*.64f,h-137,w*.24f,48,m.RightId==0?D.Red:D.Navy);T(p,"RightGlyph",Glyph(m.RightId),w*.64f,h-137,w*.24f,48,36,D.Paper,TextAnchor.MiddleCenter);
            D.Frame(p,12,h-72,w-24,59,D.Ink,1);T(p,"NextBuild","M / "+GlyphCatalog.FontDisplayName(Session.Entrants[m.RightId].Build.fontType)+" / "+CustomizeLabels.Grip(Session.Entrants[m.RightId].Build),20,h-68,w-40,50,25,null,TextAnchor.MiddleCenter);
        }
        void GroupResult()
        {
            Header("GROUP RESULT");D.Title(root,Session.Qualified?"決勝トーナメント進出":"グループリーグ終了",102,74,Session.Qualified?D.Red:D.Ink);
            T(root,"GroupResultSubtitle",$"グループ{(char)('A'+Session.PlayerGroup)} 最終順位",420,215,832,51,39,D.Navy,TextAnchor.MiddleCenter);
            var p=D.Panel(root,"FinalGroupTable",68,277,800,394);Table(p,Session.PlayerGroup,8,7,784,82,true);
            Fighter(root,0,908,265,299,264,false,"winner");D.Art(root,"design_laurel_badge",1232,272,380,238);
            int rank=Session.Standings(Session.PlayerGroup).FindIndex(s=>s.Id==0)+1;
            T(root,"GroupPlacement",$"グループ{rank}位\n"+(Session.Qualified?"ベスト32へ":"次の挑戦へ"),1280,355,275,128,35,D.Paper,TextAnchor.MiddleCenter);
            var progress=D.Panel(root,"QualificationProgress",909,537,699,133,"決勝トーナメント進出までの流れ");T(progress,"QualificationNumbers","48文字　 →→→　32文字",20,66,659,58,43,D.Navy,TextAnchor.MiddleCenter);
            D.Frame(root,70,697,1538,85,D.Ink,1,true);T(root,"QualifiedCounts","各グループ1・2位 <color=#AE2319>24文字</color>　＋　3位上位 <color=#14314D>8文字</color>　＝　<color=#AE2319>32文字</color>",96,703,1486,73,40,null,TextAnchor.MiddleCenter);
            Button("WorldCupThirdTable","3位の成績を見る",319,807,489,()=>Open(WorldCupPage.ThirdRanking),null,80,38);
            Button("WorldCupToKnockout",Session.Qualified?"決勝トーナメントへ →":"大会結果へ →",859,807,496,()=>Open(Session.Qualified?WorldCupPage.Bracket:WorldCupPage.Eliminated),D.Red,80,37);
        }
        void ThirdRanking()
        {
            Header("THIRD PLACE",()=>Open(WorldCupPage.GroupResult));D.Title(root,"<color=#AE2319>3位</color>通過ランキング",106,75);
            T(root,"ThirdRule","12文字のうち上位8文字が決勝へ",475,204,720,37,29,null,TextAnchor.MiddleCenter);
            string[] heads={"順位","グループ","文字","KO勝ち","タイムアップ勝ちの残りHP","結果"};float[] x={31,220,415,618,916,1372,1641};
            D.InkFill(root,"ThirdBand",31,246,1610,49,D.Navy);for(int c=0;c<6;c++)T(root,"ThirdHead"+c,heads[c],x[c],246,x[c+1]-x[c],49,c==4?24:27,D.Paper,TextAnchor.MiddleCenter);
            var rows=Session.ThirdRanking();for(int i=0;i<12;i++)
            {
                var r=rows[i];float y=295+i*46+(i>=8?18:0);bool qualifies=i<8;
                if(!qualifies)D.Block(root,"EliminatedWash",31,y,1610,46,new Color(.5f,.5f,.46f,.10f));
                var cells=new[]{(i+1).ToString(),((char)('A'+r.Group)).ToString(),Glyph(r.Id),r.KoWins.ToString(),r.TimeUpHp>0?r.TimeUpHp.ToString("F0"):"—",qualifies?"✓　進出":"×　敗退"};
                for(int c=0;c<6;c++)T(root,"ThirdCell"+i+"_"+c,cells[c],x[c],y,x[c+1]-x[c],46,c==2?36:27,!qualifies?new Color(.35f,.35f,.32f):r.Id==0?D.Red:c==2||c==5?D.Navy:D.Ink,TextAnchor.MiddleCenter);
                D.Block(root,"ThirdRow",31,y+46,1610,1,D.Rule);
            }
            for(int c=0;c<7;c++)D.Block(root,"ThirdColumn",x[c],246,1,619,D.Rule);
            D.Block(root,"QualifyingLine",31,668,1610,5,D.Red);D.InkFill(root,"LineTag",739,657,178,32,D.Red);T(root,"LineTagText","進出ライン",740,655,178,34,22,D.Paper,TextAnchor.MiddleCenter);
            T(root,"ThirdTieRule","判定順：KO勝ち数 → タイムアップ勝利時の残りHP。同成績：勝点 → HP差 → グループ順",51,878,1188,35,20);
            Button("WorldCupThirdBack","グループ結果へ →",1270,879,350,()=>Open(WorldCupPage.GroupResult),D.Red,35,20);
        }
        void Bracket()
        {
            if(!bracketFocused){var own=Session.Matches.Skip(72).Take(16).FirstOrDefault(m=>m.IncludesPlayer);if(own!=null)quarter=(own.Id-72)/4;bracketFocused=true;}
            Header("KNOCKOUT");D.Title(root,"決勝トーナメント",83,70);
            string[] rounds={"ベスト32","ベスト16","準々決勝","準決勝","決勝"};int current=Session.NextPlayerMatch?.Round??4;
            for(int i=0;i<5;i++){D.InkFill(root,"RoundStage",100+i*294,192,294,49,i==current?D.Ink:new Color(.7f,.69f,.64f));T(root,"RoundText",rounds[i],100+i*294,191,294,50,27,i==current?D.Paper:D.Ink,TextAnchor.MiddleCenter);}
            var graph=D.Panel(root,"WorldCupBracket",36,260,1165,620);D.InkFill(graph,"BracketCaptionBand",11,9,393,44,D.Navy);T(graph,"BracketCaption",wholeBracket?"全32文字を表示":"第"+(quarter+1)+"ブロックを表示",24,10,365,41,27,D.Paper);
            for(int i=0;i<4;i++){int q=i;var b=D.Button(graph,"WorldCupQuarter"+i,(i+1).ToString(),429+i*64,9,57,42,()=>{quarter=q;wholeBracket=false;Build();},null,22,false);D.Selected(b,!wholeBracket&&i==quarter);}
            if(wholeBracket)DrawWhole(graph);else DrawQuarter(graph);
            NextPanel(1217,260,421,342);
            Button("WorldCupWholeBracket",wholeBracket?"ブロックを拡大":"全32文字を見る",1240,624,375,()=>{wholeBracket=!wholeBracket;Build();},null,60,29);
            Button("WorldCupPause","大会を中断",1240,706,375,GameFlow.ToTournamentSelect,null,60,29);
            primary=Button("WorldCupNext",Session.Finished?"大会結果へ →":"次の試合へ →",1240,788,375,Next,D.Red,83,38);primary.button.interactable=!Busy&&(Session.NextPlayerMatch!=null||Session.Finished);
            if(Session.Match(103)?.Ready==true&&!Session.Match(103).IncludesPlayer)D.Button(graph,"WorldCupWatchBronze","3位決定戦を観戦 →",729,558,404,56,()=>ShowVersus(103),D.Navy,26);
            calculation=T(root,"WorldCupCalculation","",50,896,1545,18,15,D.Navy);
            if(Session.Eliminated || Session.Match(103).Complete&&Session.Match(103).IncludesPlayer)
            {primary.label.text=Session.Finished?"大会結果へ →":"大会の続きを集計 →";primary.button.interactable=!Busy;primary.button.onClick.RemoveAllListeners();primary.button.onClick.AddListener(()=>{if(Busy)return;Session.WatchRest=true;WorldCupRuntimeStore.Save();StartCoroutine(Resolve(true));});}
        }
        void Slot(Transform p,int id,float x,float y,float w,float h=52)
        {
            D.Block(p,"SlotPaper",x,y,w,h,new Color(D.Paper.r,D.Paper.g,D.Paper.b,.75f));D.Frame(p,x,y,w,h,id==0?D.Red:D.Ink,id==0?2:1);
            T(p,"BracketSlot",Glyph(id),x+12,y,w*.35f,h,32,id==0?D.Red:D.Ink);
            if(id>=0)Fighter(p,id,x+w*.45f,y+2,w*.52f,h-4);
        }
        void DrawQuarter(Transform p)
        {
            float[] xs={14,300,580,943};for(int stage=0;stage<4;stage++)
            {
                int count=8>>stage;
                for(int i=0;i<count;i++)
                {
                    float center=95+65*((1<<stage)*i+((1<<stage)-1)*.5f);int id;
                    if(stage==0){var m=Session.Match(72+quarter*4+i/2);id=i%2==0?m.LeftId:m.RightId;}
                    else id=Session.Match(stage==1?72+quarter*4+i:stage==2?88+quarter*2+i:96+quarter).WinnerId;
                    float width=stage<2?230:stage==2?205:160;Slot(p,id,xs[stage],center-26,width);
                    if(stage<3){float dest=95+65*((1<<(stage+1))*(i/2)+((1<<(stage+1))-1)*.5f);Color c=id==0?D.Red:D.Navy;RetroUi.Line(p,new Vector2(xs[stage]+width,center),new Vector2(xs[stage]+width+35,center),c,4);RetroUi.Line(p,new Vector2(xs[stage]+width+35,center),new Vector2(xs[stage]+width+35,dest),c,4);RetroUi.Line(p,new Vector2(xs[stage]+width+35,dest),new Vector2(xs[stage+1],dest),c,4);}
                }
            }
            D.Art(p,"design_trophy",955,66,173,145);T(p,"QuarterWinnerCaption","準決勝へ",932,22,204,43,28,null,TextAnchor.MiddleCenter);
            T(p,"BronzeStatus","3位決定戦\n準決勝の敗者同士",848,410,290,89,23,null,TextAnchor.MiddleCenter);
            T(p,"FinalFixtures","決勝："+Pair(Session.Match(102))+"\n3位："+Pair(Session.Match(103)),848,500,290,63,21,D.Navy,TextAnchor.MiddleCenter);
        }
        void DrawWhole(Transform p)
        {
            int[] offsets={72,88,96,100,102};for(int stage=0;stage<5;stage++)
            {float x=12+stage*229;T(p,"AllRound"+stage,WorldCupMatch.RoundName(stage),x,60,215,34,24,null,TextAnchor.MiddleCenter);int count=32>>stage;
                for(int i=0;i<count;i++){int id=stage==0?(i%2==0?Session.Match(72+i/2).LeftId:Session.Match(72+i/2).RightId):Session.Match(offsets[stage-1]+i).WinnerId;float cy=112+15.5f*((1<<stage)*i+((1<<stage)-1)*.5f);T(p,"AllSlot",Glyph(id)+(id==0?" YOU":""),x,cy-8,207,20,17,id==0?D.Red:D.Ink,TextAnchor.MiddleCenter);
                    if(stage<4){float dest=112+15.5f*((1<<(stage+1))*(i/2)+((1<<(stage+1))-1)*.5f);RetroUi.Line(p,new Vector2(x+207,cy),new Vector2(x+218,cy),D.Rule,1);RetroUi.Line(p,new Vector2(x+218,cy),new Vector2(x+218,dest),D.Rule,1);RetroUi.Line(p,new Vector2(x+218,dest),new Vector2(x+229,dest),D.Rule,1);}}
            }
        }
        void Versus()
        {
            var m=Session.Match(Session.SelectedMatch);if(m==null){Open(WorldCupPage.Bracket);return;}
            if(m.Bronze){BronzeVersus(m);return;}if(m.Group<0&&m.Round==4){FinalVersus(m);return;}
            Header("VS",()=>Open(Session.GroupsFinished?WorldCupPage.Bracket:WorldCupPage.Groups),"← 大会表");
            D.InkFill(root,"VersusRoundBand",484,103,704,78,D.Navy);T(root,"VersusRound",m.Name,499,103,674,78,43,D.Paper,TextAnchor.MiddleCenter);
            Fighter(root,m.LeftId,181,184,470,322);Fighter(root,m.RightId,1058,160,466,345,true);D.Art(root,"design_vs",660,215,350,302);
            BuildCard(m.LeftId,82,519,710,216,false);BuildCard(m.RightId,880,519,710,216,false);
            if(m.IncludesPlayer)PlayModePicker.Build(root,536,776);
            else{D.Frame(root,453,758,766,95,D.Ink,2,true);T(root,"VersusCaption","次の勝利で決勝進出に近づく",474,763,724,86,40,null,TextAnchor.MiddleCenter);}
            Button("WorldCupVersusBack","← 大会表",52,798,351,()=>Open(Session.GroupsFinished?WorldCupPage.Bracket:WorldCupPage.Groups),D.Ink,87,37);
            StartButton(m,1264,801,351,84,"試合開始 →");
        }
        void BuildCard(int id,float x,float y,float w,float h,bool compact)
        {
            var b=Session.Entrants[id].Build;var p=D.Panel(root,"VersusBuild"+id,x,y,w,h);D.InkFill(p,"NameBand",4,4,w-8,compact?40:62,id==0?D.Red:D.Navy);
            T(p,"EntrantName",(id==0?"YOU / ":"COM / ")+b.character,10,0,w-20,compact?48:69,compact?29:38,D.Paper,TextAnchor.MiddleCenter);
            if(compact){string[] heads={"サイズ","フォント","持ち方"},values={"M",GlyphCatalog.FontDisplayName(b.fontType),CustomizeLabels.Grip(b)};for(int i=0;i<3;i++){T(p,"BuildLabel",heads[i],i*w/3+6,48,w/3-12,25,16,null,TextAnchor.MiddleCenter);T(p,"Build",values[i],i*w/3+6,71,w/3-12,34,26,null,TextAnchor.MiddleCenter);}return;}
            string[] labels={"サイズ・フォント・持ち方","戦い方の特徴","必殺技"},texts={"M・"+GlyphCatalog.FontDisplayName(b.fontType)+"・"+CustomizeLabels.Grip(b),CustomizeLabels.StyleShort(b.battleStyle),Special(b)};
            for(int i=0;i<3;i++){float ry=67+i*48;D.Block(p,"BuildRow",4,ry,w-8,1,D.Rule);if(i==2){D.InkFill(p,"SpecialLabel",4,ry,w*.42f-4,48,D.Ink);D.InkFill(p,"SpecialValue",w*.42f,ry,w*.58f-4,48,id==0?D.Red:D.Navy);}T(p,"BuildLabel",labels[i],12,ry,w*.42f-20,48,21,i==2?D.Paper:D.Ink);T(p,"Build",texts[i],w*.42f+7,ry,w*.58f-15,48,26,i==2?D.Paper:D.Ink,TextAnchor.MiddleCenter);D.Block(p,"BuildColumn",w*.42f,67,1,144,D.Ink);}
        }
        void StartButton(WorldCupMatch m,float x,float y,float width,float height,string label)
        {
            var stage=StageCatalog.Get(StageCatalog.ForSeed(m.Seed,m.Group<0&&m.Round==4&&!m.Bronze));
            T(root,"WorldCupStageAnnouncement","会場："+stage.Name,55,908,1480,24,18,D.Navy,TextAnchor.MiddleCenter);
            Button("WorldCupStart",m.IncludesPlayer?label:m.Bronze?"3位決定戦を観戦 →":label,x,y,width,()=>{ScreenAudioDirector.PlayBattleStart();GameFlow.ToWorldCupBattle();},D.Red,height,36);
        }
        void BronzeVersus(WorldCupMatch m)
        {
            Header("BRONZE MATCH",()=>Open(WorldCupPage.Bracket));D.Title(root,"<color=#AE2319>3位</color>決定戦",119,98);
            if(m.IncludesPlayer)PlayModePicker.Build(root,535,290);
            else T(root,"VersusCaption","準決勝の敗者同士が、最後の表彰台を争う。",282,300,1110,50,34,null,TextAnchor.MiddleCenter);
            foreach(int side in new[]{0,1}){int id=side==0?m.LeftId:m.RightId;var b=Session.Entrants[id].Build;var p=D.Panel(root,"BronzeBuild"+side,side==0?65:1279,386,329,297);D.InkFill(p,"BronzeNameBand",9,9,311,103,D.Navy);T(p,"BronzeName",b.character,10,9,309,103,67,D.Paper,TextAnchor.MiddleCenter);T(p,"BronzeQualification","準決勝敗退",10,133,309,63,29,null,TextAnchor.MiddleCenter);T(p,"BronzeBuild","M・"+GlyphCatalog.FontDisplayName(b.fontType)+"・"+CustomizeLabels.Grip(b),10,217,309,62,26,null,TextAnchor.MiddleCenter);}
            Fighter(root,m.LeftId,426,377,345,311);Fighter(root,m.RightId,915,377,344,311,true);T(root,"Versus","VS",755,480,160,136,79,null,TextAnchor.MiddleCenter);
            T(root,"BronzeCondition","この試合の勝者が3位。",455,718,760,53,34,D.Red,TextAnchor.MiddleCenter);
            StartButton(m,417,790,522,83,"3位決定戦を始める →");Button("WorldCupVersusBack","トーナメント表へ",979,790,309,()=>Open(WorldCupPage.Bracket),null,83,30);
        }
        void FinalVersus(WorldCupMatch m)
        {
            Header("FINAL",()=>Open(WorldCupPage.Bracket));D.Title(root,"決勝戦",83,101);
            if(m.IncludesPlayer)PlayModePicker.Build(root,536,206);
            else T(root,"VersusCaption","48文字の頂点を決める、最後の一戦。",450,213,772,48,35,null,TextAnchor.MiddleCenter);
            Fighter(root,m.LeftId,237,266,480,285);Fighter(root,m.RightId,1059,214,469,336,true);D.Art(root,"design_trophy",737,270,195,121);T(root,"FinalVS","VS",737,403,195,127,86,D.Gold,TextAnchor.MiddleCenter);
            BuildCard(m.LeftId,215,565,560,109,true);BuildCard(m.RightId,897,565,560,109,true);
            var bronze=D.Panel(root,"FinalBronzeResult",97,704,712,120);T(bronze,"BronzeResultCaption","3位決定戦\n結果",25,12,181,89,28);Fighter(bronze,Session.ThirdId,226,8,143,96);Fighter(bronze,Session.FourthId,480,8,144,96);T(bronze,"BronzeWinner","○",379,26,70,68,44,D.Red,TextAnchor.MiddleCenter);
            StartButton(m,236,844,534,65,"決勝戦を始める →");Button("WorldCupVersusBack","トーナメント表へ",801,844,269,()=>Open(WorldCupPage.Bracket),null,65,26);
        }
        void MatchResult()
        {
            var m=Session.Match(Session.LastMatch);if(m==null){Open(WorldCupPage.Groups);return;}bool won=m.WinnerId==0,draw=m.WinnerId<0;
            Header("MATCH RESULT");D.Title(root,draw?"引き分け":m.IncludesPlayer?won?"勝利":"敗北":m.Bronze?"3位決定戦 結果":m.Round==4?"決勝戦 結果":"対戦結果",104,105,won?D.Red:D.Navy);D.HeadingRules(root,170,won?D.Red:D.Navy);
            T(root,"ResultRound",m.Name,477,216,718,43,29,D.Navy,TextAnchor.MiddleCenter);
            var p=D.Panel(root,"MatchResult",49,260,841,567,"対戦結果",D.Red,true);
            Fighter(p,m.LeftId,88,82,264,233,false,m.WinnerId==m.LeftId?"winner":"fallen");Fighter(p,m.RightId,468,116,322,213,true,m.WinnerId==m.RightId?"winner":"fallen");T(p,"ResultVS","VS",339,160,146,91,44,D.Navy,TextAnchor.MiddleCenter);
            for(int side=0;side<2;side++){float x=79+side*425;int id=side==0?m.LeftId:m.RightId;D.InkFill(p,"ResultNameBand",x,310,256,36,id==0?D.Red:D.Navy);T(p,"ResultName",Glyph(id),x,310,256,36,27,D.Paper,TextAnchor.MiddleCenter);D.Hp(p,$"{Mathf.CeilToInt(m.Result.hpRemaining[side])} / {Mathf.CeilToInt(m.Result.maxHp[side])}",m.Result.hpRemaining[side],m.Result.maxHp[side],x-40,353,335,id==0?D.Red:D.Navy);}
            D.Frame(p,24,400,794,150,D.Rule,1);D.InkFill(p,"StatsBand",24,400,181,150,D.Navy);T(p,"StatsBandTitle","詳細データ",30,400,169,150,25,D.Paper,TextAnchor.MiddleCenter);int ps=m.LeftId==0?0:1;string[] labels={"決着","決着時間","残りHP"},values={m.Result.finishReason,$"{m.Result.elapsedSeconds:F1}秒",$"{Mathf.CeilToInt(m.Result.hpRemaining[ps])} / {Mathf.CeilToInt(m.Result.maxHp[ps])}"};for(int i=0;i<3;i++){D.Block(p,"StatsRule",205,400+i*50,613,1,D.Rule);T(p,"StatLabel",labels[i],235,400+i*50,190,50,26,D.Navy);T(p,"MatchStats",values[i],480,400+i*50,290,50,i==0?38:33,i==0?D.Red:D.Navy);}
            var q=D.Panel(root,"MatchProgress",912,260,713,567,m.Group>=0?"グループ順位":"次の挑戦へ",null,true);
            if(m.Group>=0){Table(q,m.Group,13,72,687,75,true);var next=Session.NextPlayerMatch;T(q,"NextResultCaption",NextResultCaption(next),30,439,170,93,28,D.Navy);if(next!=null){Fighter(q,next.LeftId,223,427,152,119);Fighter(q,next.RightId,490,427,152,119,true);T(q,"NextVS","VS",386,452,83,65,35,D.Navy,TextAnchor.MiddleCenter);}}
            else T(q,"KnockoutOutcome",draw?"勝者が決まるまで再試合":Session.Finished?Session.PlayerPlace:m.IncludesPlayer&&!won&&m.Round==3?"次は3位決定戦。\n表彰台を目指して、もう一戦。":Session.Eliminated?"別の文字で、次の挑戦へ。":"次の対戦相手を確認しよう。",30,126,653,322,39,D.Navy,TextAnchor.MiddleCenter);
            string nextLabel=draw&&m.Group<0?"同じ相手と再試合 →":Session.Finished?Session.ChampionId==0?"表彰式へ →":"大会結果へ →":m.Group>=0?"グループリーグへ →":"トーナメント表へ →";
            Button("WorldCupResultNext",nextLabel,454,843,445,()=>{if(draw&&m.Group<0){ShowVersus(m.Id);return;}if(Session.Finished){Open(Session.ChampionId==0&&!Session.CeremonySeen?WorldCupPage.Ceremony:WorldCupPage.Results);return;}Open(m.Group>=0?WorldCupPage.Groups:Session.Eliminated?WorldCupPage.Eliminated:WorldCupPage.Bracket);},D.Red,63,32);
            Button("WorldCupMatchDetails","試合の詳細",932,843,281,()=>ShowMatchDetails(m),null,63,29);
        }
        string NextResultCaption(WorldCupMatch next)
        {
            if(next==null)return "グループリーグ終了";
            if(next.Group<0)return "次は"+next.Name;
            int remaining=Session.Matches.Count(m=>m.Group==next.Group && m.IncludesPlayer && !m.Complete);
            return remaining==1?"次は最終戦":"次は第"+(next.Round+1)+"節";
        }
        void ShowMatchDetails(WorldCupMatch m)
        {
            var shade=RetroUi.Group(root,"MatchDetailsOverlay",0,0,1672,941);var blocker=shade.gameObject.AddComponent<Image>();blocker.color=new Color(0,0,0,.45f);
            D.Block(shade,"DetailsPaper",380,223,912,487,D.Paper);
            var p=D.Panel(shade,"DetailsPanel",380,223,912,487,"試合の詳細",null,true);
            T(p,"DetailsPair",Pair(m),30,82,852,62,43,D.Navy,TextAnchor.MiddleCenter);
            T(p,"DetailsStats",$"決着：{m.Result.finishReason}\n決着時間：{m.Result.elapsedSeconds:F1}秒\n{Glyph(m.LeftId)}　残りHP {Mathf.CeilToInt(m.Result.hpRemaining[0])} / {Mathf.CeilToInt(m.Result.maxHp[0])}\n{Glyph(m.RightId)}　残りHP {Mathf.CeilToInt(m.Result.hpRemaining[1])} / {Mathf.CeilToInt(m.Result.maxHp[1])}",80,158,752,199,30,D.Navy);
            D.Button(p,"WorldCupCloseDetails","閉じる",294,389,324,65,()=>Destroy(shade.gameObject),D.Red,31);
        }
        void Photo()
        {
            Header("PHOTO",()=>Open(WorldCupPage.Results));D.Title(root,"<color=#AE2319>優勝</color>の一枚",85,83);
            var photo=WorldCupCeremony.Photo(root,Session,274,216,1125,590);photo.localRotation=Quaternion.Euler(0,0,2);
            T(root,"PhotoCaption","この一文字が、世界を制した。",515,829,590,73,37,null,TextAnchor.MiddleCenter);
            Button("WorldCupSavePhoto","↓ 記念写真を保存",1136,832,222,()=>canvas.gameObject.AddComponent<WorldCupPhotoExport>().Save(photo,Session),null,65,25);
            Button("WorldCupPhotoNext","大会結果へ →",1384,832,236,()=>{Session.CeremonySeen=true;Open(WorldCupPage.Results);},D.Red,65,29);
        }
        void FinalResult(bool finished)
        {
            Header("RESULT");bool champion=finished&&Session.ChampionId==0;
            D.Title(root,champion?"世界一の文字、誕生。":finished?Session.PlayerPlace:"挑戦は、次の一文字へ。",88,champion?88:74,champion?D.Red:D.Ink);
            if(!finished){EliminatedResult();return;}
            var p=D.Panel(root,"WorldCupPlayerResult",39,199,955,604);D.InkFill(p,"PlayerBand",278,13,454,66,D.Red);T(p,"PlayerBandTitle","あなたの成績",284,12,442,68,41,D.Paper,TextAnchor.MiddleCenter);
            bool podium=Session.ChampionId==0 || Session.RunnerUpId==0 || Session.ThirdId==0;
            Fighter(p,0,35,podium?87:115,podium?365:320,podium?471:420,false,podium?"winner":Session.Eliminated?"kneel":"ready");
            if(champion)D.Art(p,"design_laurel",394,102,524,224);
            // 「moji Fighter」以上で全試合を自操作して優勝: 最強の COM「真・mojiFighter」を解放
            if(champion && Session.UnlockedTrue){D.InkFill(p,"TrueMojiUnlocked",18,14,250,64,D.Ink);T(p,"TrueMojiUnlockedText","真・mojiFighter\n解放！",22,14,242,64,22,D.Paper,TextAnchor.MiddleCenter);}
            string placement=Session.PlayerPlace;
            if(Session.Eliminated && Session.Qualified && Session.FourthId!=0)placement+="敗退";
            if(!Session.Qualified)placement="グループリーグ\n敗退";
            var placementText=T(p,"PlayerPlacement",placement,champion?475:410,champion?145:133,champion?354:515,champion?139:165,champion?94:podium?76:45,D.Red,TextAnchor.MiddleCenter);
            placementText.horizontalOverflow=HorizontalWrapMode.Wrap;
            placementText.verticalOverflow=VerticalWrapMode.Truncate;
            placementText.resizeTextMinSize=24;
            int rank=Session.Standings(Session.PlayerGroup).FindIndex(s=>s.Id==0)+1;var own=Session.Matches.Where(m=>m.Complete&&m.IncludesPlayer).ToArray();int wins=own.Count(m=>m.WinnerId==0),losses=own.Count(m=>m.WinnerId>=0&&m.WinnerId!=0);
            string[] labels={$"グループ{(char)('A'+Session.PlayerGroup)}","グループリーグ","決勝トーナメント","大会通算"},values={$"{rank}位",$"{own.Count(m=>m.Group>=0&&m.WinnerId==0)}勝 {own.Count(m=>m.Group>=0&&m.WinnerId>0)}敗",$"{own.Count(m=>m.Group<0&&m.WinnerId==0)}勝",$"{wins}勝 {losses}敗"};for(int i=0;i<4;i++){D.Frame(p,410,339+i*59,515,59,D.Rule,1);T(p,"PlayerStatLabel",labels[i],425,339+i*59,243,59,27);T(p,"PlayerStatistics",values[i],685,339+i*59,232,59,32,D.Navy);}
            var q=D.Panel(root,"WorldCupFinalPlaces",1018,199,616,328,"最終順位",null,true);int[] ids={Session.ChampionId,Session.RunnerUpId,Session.ThirdId,Session.FourthId};string[] names={"優勝","準優勝","3位","4位"};Color[] colors={D.Gold,WorldCupCeremony.Silver,WorldCupCeremony.Bronze,D.Navy};
            for(int i=0;i<4;i++){float ry=78+i*57;D.Frame(q,23,ry,566,55,D.Rule,1);D.InkFill(q,"PlaceColor",23,ry,86,55,colors[i]);T(q,"FinalRank",(i+1).ToString(),23,ry,86,55,36,D.Paper,TextAnchor.MiddleCenter);Fighter(q,ids[i],119,ry,88,55);T(q,"FinalGlyph",Glyph(ids[i]),232,ry,116,55,37,ids[i]==0?D.Red:D.Navy,TextAnchor.MiddleCenter);if(i<3)D.Art(q,"design_cup_"+new[]{"gold","silver","bronze"}[i],372,ry+7,44,41);T(q,"FinalPlace"+i,names[i],432,ry,138,55,29,i==0?D.Red:D.Ink);}
            var photoPanel=D.Panel(root,"WorldCupResultPhotoPanel",1018,542,616,260,"記念写真",null,true);WorldCupCeremony.PreviewPodium(photoPanel,Session,44,74,529,171);
            Button("WorldCupResultPhoto","▣ 記念写真を見る",50,821,369,()=>Open(WorldCupPage.Photo),D.Navy,73,30);Button("WorldCupResultBracket","トーナメント表を見る",449,821,367,()=>Open(WorldCupPage.Bracket),D.Navy,73,27);Button("WorldCupReplay","もう一度挑戦 →",846,821,428,GameFlow.ToWorldCupCustomize,D.Red,73,35);Button("WorldCupMenu","⌂ メニューへ",1308,821,310,GameFlow.ToModeSelect,D.Navy,73,30);
        }
        void EliminatedResult()
        {
            T(root,"PlayerPlacement",Session.PlayerPlace,575,209,522,51,39,null,TextAnchor.MiddleCenter);var p=D.Panel(root,"WorldCupPlayerResult",41,266,863,505,"あなたの成績",D.Red,true);Fighter(p,0,241,105,388,217,false,"kneel");
            var lost=Session.Matches.LastOrDefault(m=>m.IncludesPlayer&&m.Complete&&m.WinnerId!=0);T(p,"ExitRound",lost==null?Session.PlayerPlace:lost.Name+" 敗退",13,331,837,61,39,null,TextAnchor.MiddleCenter);
            int rank=Session.Standings(Session.PlayerGroup).FindIndex(s=>s.Id==0)+1;T(p,"ExitStatistics",$"グループ {(char)('A'+Session.PlayerGroup)} {rank}位\n大会通算 {Session.PlayerWins}勝 {Session.Matches.Count(m=>m.IncludesPlayer&&m.Complete&&m.WinnerId>0)}敗",26,408,811,87,28,null,TextAnchor.MiddleCenter);
            var q=D.Panel(root,"WorldCupFinalPlaces",925,266,708,505,"大会の続き",null,true);T(q,"WatchRestCaption","他の文字の試合を最後まで見届ける",21,88,666,46,28,null,TextAnchor.MiddleCenter);D.Art(q,"design_trophy",205,160,298,204);T(q,"RemainingStats","出場　48文字\n決勝進出　32文字\n現在の進行　"+WorldCupMatch.RoundName(Session.NextComputerMatch?.Round??4),28,376,652,113,27,D.Navy);
            Button("WorldCupReplay","別の文字で再挑戦 →",56,791,609,GameFlow.ToWorldCupCustomize,D.Red,90,39);Button("WorldCupWatchRest","大会の続きを見る →",692,791,596,()=>{Session.WatchRest=true;Open(WorldCupPage.Bracket);},D.Navy,90,38);Button("WorldCupMenu","メニューへ",1316,791,302,GameFlow.ToModeSelect,null,90,37);
        }
    }
}
