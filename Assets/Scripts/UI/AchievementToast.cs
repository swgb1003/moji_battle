using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>A single real-time queue survives scene transitions. Pending notifications are saved with the profile.</summary>
    public sealed class AchievementToast : MonoBehaviour
    {
        public static AchievementToast Instance { get; private set; }
        public int CurrentId { get; private set; }
        public const float Duration=4f;
        RectTransform panel, content, bar;
        Canvas canvas;
        float shownAt, nextSave;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            AchievementStore.Load();
            if (Instance==null) new GameObject("AchievementNotifications").AddComponent<AchievementToast>();
        }
        void Awake()
        {
            if (Instance!=null && Instance!=this) { Destroy(gameObject); return; }
            Instance=this; DontDestroyOnLoad(gameObject);
            canvas=UguiKit.MakeCanvas("AchievementToastCanvas",1000);
            canvas.transform.SetParent(transform,false);
            canvas.GetComponent<CanvasScaler>().screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var area=UguiKit.Box(canvas.transform,"ToastSafeArea",0,0,1920,1080);
            area.anchorMin=area.anchorMax=area.pivot=new Vector2(.5f,.5f); area.anchoredPosition=Vector2.zero;
            panel=UguiKit.Box(area,"AchievementToast",1390,94,440,124);
            RetroUi.Block(panel,"Paper",0,0,440,124,RetroUi.Paper);
            RetroUi.PaperSurface(panel,0,0,440,124); RetroUi.Frame(panel,0,0,440,124,RetroUi.Ink,3);
            RetroUi.Frame(panel,7,7,426,110,RetroUi.Ink,1);
            content=RetroUi.Group(panel,"Content",12,12,414,100);
            bar=RetroUi.Block(panel,"TimeRemaining",9,116,422,3,RetroUi.Red).rectTransform;
            panel.gameObject.SetActive(false);
        }
        void Update()
        {
            if (Time.unscaledTime>=nextSave) { AchievementStore.Save(); nextSave=Time.unscaledTime+1f; }
            bool hidden=(GameFlow.RunMode && RunRuntimeStore.Current.stage!=RunStage.Result && !RunRuntimeStore.Current.Finished) || SettingsScreen.IsOpen || (GameFlow.ChallengeMode && ChallengeRuntimeStore.Outcome==null)
                || (GameFlow.DraftMode && DraftRuntimeStore.Current.stage!=DraftStage.Result);
            canvas.enabled = !hidden;
            if (hidden) { if (CurrentId != 0) shownAt += Time.unscaledDeltaTime; return; }
            var q=AchievementStore.Profile.notifications;
            // A priority reward may move ahead of the paused notification. Keep that older item queued.
            if (CurrentId != 0 && (q.Count==0 || q[0]!=CurrentId))
            { CurrentId = 0; panel.gameObject.SetActive(false); }
            if (CurrentId==0)
            {
                if (q.Count==0) return;
                CurrentId=q[0]; shownAt=Time.unscaledTime;
                foreach(Transform child in content) Destroy(child.gameObject);
                var d=AchievementCatalog.Get(CurrentId);
                if (d==null) { Dismiss(); return; }
                AchievementBadge.Create(content,CurrentId,true,0,0,94);
                RetroUi.Label(content,"Message","称号を獲得！",112,4,276,28,23);
                RetroUi.Label(content,"Name",d.Name,112,34,282,56,31);
                panel.gameObject.SetActive(true);
            }
            float remaining=1f-(Time.unscaledTime-shownAt)/Duration;
            bar.sizeDelta=new Vector2(422*Mathf.Clamp01(remaining),3);
            if (remaining<=0) Dismiss();
        }
        public void Dismiss()
        {
            int id=CurrentId;
            AchievementStore.Change(p => { if(p.notifications.Count>0 && p.notifications[0]==id) p.notifications.RemoveAt(0); },true);
            CurrentId=0; panel.gameObject.SetActive(false);
        }
        void OnApplicationPause(bool paused) { if(paused) AchievementStore.Save(); }
        void OnApplicationQuit() => AchievementStore.Save();
        void OnDestroy() { if(Instance==this) { AchievementStore.Save(); Instance=null; } }
    }
}
