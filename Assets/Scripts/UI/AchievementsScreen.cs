using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed class AchievementsScreen : MonoBehaviour
    {
        RectTransform root, grid, details;
        Text count, percent, pageLabel;
        RectTransform progressBar;
        readonly List<AchievementDefinition> filtered=new List<AchievementDefinition>();
        readonly List<UguiKit.ChoiceButton> tabs=new List<UguiKit.ChoiceButton>();
        UguiKit.ChoiceButton previous,next;
        int category,page,selected;
        bool refreshing;
        public int SelectedId => selected;
        public int Page => page;
        void Start()
        {
            if(Camera.main!=null) Camera.main.backgroundColor=RetroUi.Paper;
            root=RetroUi.Screen("AchievementsCanvas",out _);
            RetroUi.Header(root,"04 / ACHIEVEMENTS",GameFlow.ToModeSelect);
            root.Find("Back").GetComponentInChildren<Text>().text="← メニュー";
            RetroUi.Label(root,"Title","称号",720,142,330,110,92);
            count=RetroUi.Label(root,"AcquiredCount","",1065,149,306,50,34);
            RetroUi.Frame(root,1065,207,275,25,RetroUi.Ink,2);
            progressBar=RetroUi.Block(root,"CollectionProgress",1069,211,0,17,RetroUi.Red).rectTransform;
            percent=RetroUi.Label(root,"Percent","",1350,201,80,36,24);
            for(int i=0;i<AchievementCatalog.Categories.Length;i++)
            {
                int chosen=i;
                float width=1768f/AchievementCatalog.Categories.Length;
                tabs.Add(RetroUi.Button(root,"Category_"+i,AchievementCatalog.Categories[i],76+i*width,260,width-10,62,28,()=>ChooseCategory(chosen),shadow:false));
            }
            RetroUi.Frame(root,76,338,1168,665,RetroUi.Ink,3);
            grid=RetroUi.Group(root,"AchievementGrid",89,351,1142,566);
            RetroUi.Frame(root,1260,338,584,665,RetroUi.Ink,3);
            details=RetroUi.Group(root,"AchievementDetails",1277,355,550,630);
            previous=RetroUi.Button(root,"PreviousPage","〈",508,940,65,48,32,()=>ChangePage(-1),RetroUi.Ink,false);
            next=RetroUi.Button(root,"NextPage","〉",770,940,65,48,32,()=>ChangePage(1),RetroUi.Ink,false);
            pageLabel=RetroUi.Label(root,"PageNumber","",585,940,172,48,28,null,TextAnchor.MiddleCenter);
            AchievementStore.Changed+=Refresh;
            Refresh();
        }
        public void ChooseCategory(int value)
        { category=Mathf.Clamp(value,0,AchievementCatalog.Categories.Length-1); page=0; selected=0; Refresh(); }
        public void ChangePage(int delta)
        { page=Mathf.Clamp(page+delta,0,Mathf.Max(0,(filtered.Count-1)/6)); selected=0; Refresh(); }
        public void Select(int id)
        {
            if(!filtered.Exists(d=>d.Id==id)) return;
            selected=id;
            AchievementStore.Change(p=> { var item=p.Unlock(id); if(item!=null) item.seen=true; },true);
            Refresh();
        }
        static void Clear(Transform parent)
        { foreach(Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
        public void Refresh()
        {
            if(root==null || refreshing) return;
            refreshing=true;
            var p=AchievementStore.Profile;
            count.text=$"獲得 {p.Count} / {AchievementCatalog.All.Length}";
            percent.text=$"{p.Count*100/AchievementCatalog.All.Length}%";
            progressBar.sizeDelta=new Vector2(267*p.Count/(float)AchievementCatalog.All.Length,17);
            filtered.Clear();
            foreach(var d in AchievementCatalog.All) if(category==0 || d.Category==category) filtered.Add(d);
            page=Mathf.Clamp(page,0,Mathf.Max(0,(filtered.Count-1)/6));
            if(selected==0 && filtered.Count>0) selected=filtered[page*6].Id;
            for(int i=0;i<tabs.Count;i++) RetroUi.Selected(tabs[i],category==i,RetroUi.Ink);
            Clear(grid);
            for(int i=0;i<6 && page*6+i<filtered.Count;i++)
            {
                var d=filtered[page*6+i];
                float x=(i%3)*383,y=(i/3)*283;
                var card=RetroUi.Group(grid,"Achievement_"+d.Id,x,y,374,274);
                var target=UguiKit.Panel(card,"HitArea",0,0,374,274,new Color(1,1,1,.001f));
                var button=target.gameObject.AddComponent<Button>(); button.onClick.AddListener(()=>Select(d.Id));
                PaperUi.Card(card);
                RetroUi.Frame(card,0,0,374,274,selected==d.Id?RetroUi.Red:RetroUi.Rule,selected==d.Id?4:1);
                bool acquired=p.Has(d.Id);
                AchievementBadge.Create(card,d.Id,acquired,112,9,150);
                var name=RetroUi.Label(card,"AchievementName",acquired?d.Name:"？？？",12,160,350,44,30,null,TextAnchor.MiddleCenter);
                name.resizeTextForBestFit=true; name.resizeTextMinSize=21; name.resizeTextMaxSize=30;
                if(acquired)
                {
                    RetroUi.Label(card,"Status","獲得済み",12,210,350,42,23,RetroUi.Muted,TextAnchor.MiddleCenter);
                    if(p.IsNew(d.Id))
                    { RetroUi.Block(card,"NewBadge",290,12,72,28,RetroUi.Red); RetroUi.Label(card,"NewLabel","NEW",290,12,72,28,21,RetroUi.Paper,TextAnchor.MiddleCenter); }
                }
                else
                {
                    var condition=RetroUi.Label(card,"Condition",d.Condition,14,207,346,39,20,null,TextAnchor.MiddleCenter);
                    condition.resizeTextForBestFit=true; condition.resizeTextMinSize=17; condition.resizeTextMaxSize=20;
                    condition.verticalOverflow=VerticalWrapMode.Truncate;
                    int value=Mathf.Min(p.Value(d.Id),d.Goal);
                    RetroUi.Frame(card,14,254,242,10,RetroUi.Ink,1);
                    RetroUi.Block(card,"Progress",16,256,238*value/(float)d.Goal,6,RetroUi.Red);
                    RetroUi.Label(card,"ProgressText",$"{value} / {d.Goal}",263,244,97,28,20,null,TextAnchor.MiddleRight);
                }
            }
            pageLabel.text=$"{page+1} / {Mathf.Max(1,(filtered.Count+5)/6)}";
            previous.button.interactable=page>0; next.button.interactable=(page+1)*6<filtered.Count;
            ShowDetails(); refreshing=false;
        }
        void ShowDetails()
        {
            Clear(details);
            var d=AchievementCatalog.Get(selected); if(d==null) return;
            var p=AchievementStore.Profile; bool acquired=p.Has(d.Id);
            AchievementBadge.Create(details,d.Id,acquired,141,20,268);
            var name=RetroUi.Label(details,"DetailName",acquired?d.Name:"？？？",10,296,530,71,46,null,TextAnchor.MiddleCenter);
            name.resizeTextForBestFit=true; name.resizeTextMinSize=28; name.resizeTextMaxSize=46;
            RetroUi.Block(details,"StatusBadge",172,372,206,44,acquired?RetroUi.Ink:RetroUi.Wash);
            RetroUi.Label(details,"Status",acquired?"獲得済み":"未獲得",172,372,206,44,27,acquired?RetroUi.Paper:RetroUi.Muted,TextAnchor.MiddleCenter);
            RetroUi.Block(details,"Rule",12,435,526,2,RetroUi.Ink);
            RetroUi.Label(details,"ConditionHeading","獲得条件",18,450,510,35,24);
            var condition=RetroUi.Label(details,"DetailCondition",d.Condition,18,491,510,70,26);
            condition.resizeTextForBestFit=true; condition.resizeTextMinSize=20; condition.resizeTextMaxSize=26;
            condition.verticalOverflow=VerticalWrapMode.Truncate;
            if(acquired)
                RetroUi.Label(details,"Description",d.Description,18,568,510,58,24,RetroUi.Muted);
            else
            {
                int value=Mathf.Min(p.Value(d.Id),d.Goal);
                RetroUi.Label(details,"DetailProgress",$"進捗  {value} / {d.Goal}",18,575,510,46,26,RetroUi.Muted);
            }
        }
        void Update() { if(!SettingsScreen.BlocksInput && Input.GetKeyDown(KeyCode.Escape)) GameFlow.ToModeSelect(); }
        void OnDestroy() => AchievementStore.Changed-=Refresh;
    }
}
