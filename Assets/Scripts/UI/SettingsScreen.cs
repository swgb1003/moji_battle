using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>Reusable settings overlay; opening it preserves the current match and menu.</summary>
    public sealed class SettingsScreen : MonoBehaviour
    {
        public static SettingsScreen Instance { get; private set; }
        public static bool IsOpen => Instance != null;
        static int closedFrame = -1;
        public static bool BlocksInput => IsOpen || closedFrame == Time.frameCount;
        public static bool Previewing => Instance != null && Instance.preview != null && Instance.preview.isPlaying;
        RectTransform root;
        readonly GameObject[] pages = new GameObject[4];
        readonly UguiKit.ChoiceButton[] tabs = new UguiKit.ChoiceButton[4];
        readonly List<Action> refreshers = new List<Action>();
        readonly List<GraphicRaycaster> blocked = new List<GraphicRaycaster>();
        readonly List<(CanvasGroup group, bool interactable, bool created)> blockedGroups = new List<(CanvasGroup, bool, bool)>();
        CanvasGroup rootGroup;
        AudioSource preview;
        BattleMusicSettings music;
        Text footer;
        GameObject confirmation, musicPicker;
        GameObject previousSelection;
        bool inBattle, pausesBattle, previousPause, closing;
        int previewTrack;
        public AudioSource PreviewSource => preview;

        public static void Open()
        {
            if (IsOpen) return;
            new GameObject("SettingsScreen").AddComponent<SettingsScreen>().Build();
        }

        void Build()
        {
            Instance = this;
            inBattle = FindFirstObjectByType<BattleBootstrap>() != null;
            pausesBattle = inBattle && !GameFlow.FriendMode;
            previousPause = TimeController.Paused;
            if (pausesBattle) TimeController.SetPaused(true);
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            foreach (var raycaster in FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None))
                if (raycaster.enabled)
                {
                    blocked.Add(raycaster); raycaster.enabled = false;
                    var group = raycaster.GetComponent<CanvasGroup>(); bool created = group == null;
                    if (created) group = raycaster.gameObject.AddComponent<CanvasGroup>();
                    blockedGroups.Add((group, group.interactable, created)); group.interactable = false;
                }
            root = RetroUi.Screen("SettingsCanvas", out var canvas, 100);
            rootGroup = root.gameObject.AddComponent<CanvasGroup>();
            canvas.transform.SetParent(transform, false);
            canvas.transform.Find("PaperBackground").GetComponent<Image>().raycastTarget = true;
            RetroUi.Header(root, "05 / SETTINGS", Close);
            root.Find("Back").GetComponentInChildren<Text>().text = inBattle ? "← 対戦へ" : "← メニュー";
            RetroUi.Label(root, "SettingsHeading", "設定", 580, 140, 760, 112, 96, null, TextAnchor.MiddleCenter);
            RetroUi.Label(root, "SettingsSubtitle", "自分好みの音と演出に", 580, 256, 760, 34, 26, RetroUi.Muted, TextAnchor.MiddleCenter);
            string[] titles = { "基本", "音楽", "表示・演出", "データ" };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                tabs[i] = RetroUi.Button(root, "SettingsTab" + i, titles[i], 76 + 446 * i, 296, 430, 66, 34, () => ShowTab(index), shadow: false);
                pages[i] = RetroUi.Group(root, "SettingsPage" + i, 76, 380, 1768, 554).gameObject;
            }
            music = Resources.Load<BattleMusicSettings>("Audio/BattleMusic");
            preview = gameObject.AddComponent<AudioSource>();
            preview.playOnAwake = false; preview.loop = false; preview.spatialBlend = 0; preview.priority = 100;
            BuildBasic(pages[0].transform); BuildMusic(pages[1].transform);
            BuildDisplay(pages[2].transform); BuildData(pages[3].transform);
            RetroUi.Button(root, "SettingsReset", "初期設定に戻す", 76, 958, 340, 68, 28,
                () => Confirm("設定を初期値に戻しますか？", "音量・音楽・表示の設定を戻します。", GameSettings.Reset), shadow: false);
            footer = RetroUi.Label(root, "SettingsSaveHint", inBattle && GameFlow.FriendMode ? "オンライン対戦は進行中です" : "変更は自動で保存されます",
                440, 967, 996, 48, 25, RetroUi.Muted, TextAnchor.MiddleCenter);
            RetroUi.Button(root, "SettingsDone", "完了", 1544, 958, 300, 68, 34, Close, RetroUi.Ink, false);
            RetroUi.Block(root, "DoneAccent", 1544, 958, 6, 68, RetroUi.Red);
            GameSettings.Changed += Refresh;
            ShowTab(0);
            EventSystem.current?.SetSelectedGameObject(tabs[0].button.gameObject);
        }

        RectTransform Panel(Transform parent, string name, string title, float x, float width)
        {
            var panel = RetroUi.Group(parent, name, x, 0, width, 554);
            RetroUi.Frame(panel, 0, 0, width, 554, RetroUi.Ink, 2);
            RetroUi.Label(panel, name + "Title", title, 34, 20, width - 68, 65, 43);
            RetroUi.Block(panel, "TitleRule", 34, 91, width - 68, 1, RetroUi.Rule);
            return panel;
        }

        void BuildBasic(Transform parent)
        {
            var audio = Panel(parent, "BasicAudio", "音量・音楽", 0, 860);
            Volume(audio, "Master", "全体音量", 116, s => s.masterVolume, (s, v) => s.masterVolume = v);
            Volume(audio, "Music", "BGM", 204, s => s.musicVolume, (s, v) => s.musicVolume = v);
            Volume(audio, "Effects", "効果音", 292, s => s.effectsVolume, (s, v) => s.effectsVolume = v);
            RetroUi.Block(audio, "MusicRule", 34, 378, 792, 1, RetroUi.Rule);
            RetroUi.Label(audio, "BattleMusicCaption", "戦闘BGM", 34, 390, 730, 44, 29);
            var picker = RetroUi.Button(audio, "MusicSelect", "", 34, 440, 548, 60, 32, ToggleMusicPicker, shadow: false);
            refreshers.Add(() => picker.label.text = MusicName(GameSettings.BattleMusic) + "  ▾");
            RetroUi.Button(audio, "MusicPreview", "▶ 試聴", 614, 440, 212, 60, 29, () => Preview(GameSettings.BattleMusic), shadow: false);
            RetroUi.Label(audio, "MusicHint", "次の対戦から、この曲が流れます", 34, 512, 792, 30, 22, RetroUi.Muted);
            var display = Panel(parent, "BasicDisplay", "戦闘・表示", 884, 884);
            Options(display, "InitialSpeed", "初期観戦速度", 116, new[] { "×1", "×2", "×4" }, () => GameSettings.InitialSpeed == 1 ? 0 : GameSettings.InitialSpeed == 2 ? 1 : 2,
                i => GameSettings.Change(s => s.spectatorSpeed = i == 0 ? 1 : i == 1 ? 2 : 4));
            Options(display, "Instructions", "開始時の操作説明", 196, new[] { "ON", "OFF" }, () => GameSettings.ShowInstructions ? 0 : 1,
                i => GameSettings.Change(s => s.showInstructions = i == 0));
            Options(display, "Damage", "ダメージ数値", 276, new[] { "ON", "OFF" }, () => GameSettings.ShowDamage ? 0 : 1,
                i => GameSettings.Change(s => s.showDamage = i == 0));
            Options(display, "Reduced", "画面の揺れ・点滅", 356, new[] { "通常", "控えめ" }, () => GameSettings.ReducedEffects ? 1 : 0,
                i => GameSettings.Change(s => s.reducedEffects = i == 1));
            Options(display, "Special", "必殺技の演出", 436, new[] { "通常", "簡易" }, () => GameSettings.SimpleSpecial ? 1 : 0,
                i => GameSettings.Change(s => s.simpleSpecial = i == 1));
            RetroUi.Label(display, "DisplayHint", "演出を控えめにすると見やすくなります", 34, 512, 820, 30, 22, RetroUi.Muted);
        }

        void Volume(Transform panel, string name, string title, float y, Func<GameSettingsData, float> get, Action<GameSettingsData, float> set)
        {
            RetroUi.Label(panel, name + "Caption", title, 34, y, 196, 62, 31);
            var value = RetroUi.Label(panel, name + "Value", "", 738, y, 88, 62, 30, null, TextAnchor.MiddleRight);
            var slider = UguiKit.SliderBar(panel, name + "Volume", 228, y + 2, 470, 58, v => GameSettings.Change(s => set(s, v)));
            // A full-height invisible hit surface makes track clicks and drags easy.
            var hit = slider.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            slider.transform.Find("Track").GetComponent<Image>().color = RetroUi.Wash;
            slider.fillRect.GetComponent<Image>().color = RetroUi.Red;
            slider.handleRect.GetComponent<Image>().color = RetroUi.Ink;
            slider.handleRect.GetComponent<Outline>().effectColor = RetroUi.Paper;
            RetroUi.Frame(slider.transform, 0, 22, 470, 14, RetroUi.Ink, 1);
            refreshers.Add(() => { float v = get(GameSettings.Current); slider.SetValueWithoutNotify(v); value.text = Mathf.RoundToInt(v * 100) + "%"; });
        }

        void Options(Transform panel, string name, string title, float y, string[] labels, Func<int> selected, Action<int> change)
        {
            RetroUi.Label(panel, name + "Caption", title, 34, y, 310, 60, 29);
            float w = 494f / labels.Length;
            var choices = new UguiKit.ChoiceButton[labels.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                int option = i;
                choices[i] = RetroUi.Button(panel, name + i, labels[i], 356 + w * i, y, w - 8, 60, 29, () => change(option), shadow: false);
            }
            refreshers.Add(() => { for (int i = 0; i < choices.Length; i++) RetroUi.Selected(choices[i], i == selected(), RetroUi.Red); });
        }

        void BuildMusic(Transform parent)
        {
            var panel = Panel(parent, "MusicList", "戦闘BGMを選ぶ", 0, 1768);
            RetroUi.Label(panel, "MusicDescription", "ランダム、またはお気に入りの曲を選べます。", 34, 104, 1690, 52, 29, RetroUi.Muted);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var choice = RetroUi.Button(panel, "MusicChoice" + i, MusicName(i), 34, 176 + 78 * i, 1240, 62, 30,
                    () => { StopPreview(); GameSettings.Change(s => s.battleMusic = index); }, shadow: false);
                refreshers.Add(() => RetroUi.Selected(choice, GameSettings.BattleMusic == index, RetroUi.Red));
                RetroUi.Button(panel, "MusicListen" + i, "▶ 試聴", 1320, 176 + 78 * i, 410, 62, 28, () => Preview(index), shadow: false);
            }
            RetroUi.Label(panel, "FinalMusicHint", "大会の決勝戦では、専用のBGMが流れます。", 34, 500, 1690, 40, 26, RetroUi.Muted);
        }

        void BuildDisplay(Transform parent)
        {
            var panel = Panel(parent, "DisplayOptions", "表示・演出", 0, 884);
            Options(panel, "DisplayDamage", "ダメージ数値", 126, new[] { "ON", "OFF" }, () => GameSettings.ShowDamage ? 0 : 1,
                i => GameSettings.Change(s => s.showDamage = i == 0));
            Options(panel, "DisplayReduced", "画面の揺れ・点滅", 230, new[] { "通常", "控えめ" }, () => GameSettings.ReducedEffects ? 1 : 0,
                i => GameSettings.Change(s => s.reducedEffects = i == 1));
            Options(panel, "DisplaySpecial", "必殺技の演出", 334, new[] { "通常", "簡易" }, () => GameSettings.SimpleSpecial ? 1 : 0,
                i => GameSettings.Change(s => s.simpleSpecial = i == 1));
            Options(panel, "MenuMotion", "メニューの動き", 438, new[] { "通常", "控えめ" }, () => RetroMotionSettings.Reduced ? 1 : 0,
                i => GameSettings.SetMenuMotion(i == 1));
            var help = Panel(parent, "DisplayHelp", "見やすさの調整", 908, 860);
            RetroUi.Label(help, "ReducedHelp", "控えめ\n画面の揺れ・強い点滅・急なズームを抑えます。\n\n必殺技の簡易演出\n大きなカットインを、小さな技名表示に変更します。\n踏ん張りを入力できる時間は同じです。\n\nメニューの動き\nボタンや文字の移動を抑えます。", 34, 116, 792, 394, 29, RetroUi.Muted);
        }

        void BuildData(Transform parent)
        {
            var panel = Panel(parent, "SavedData", "保存データ", 0, 1768);
            RetroUi.Label(panel, "DataNotice", inBattle ? "データの整理は、対戦を終えてメニューに戻ってから行えます。" : "消去するデータを選んでください。実行前に確認します。", 34, 110, 1690, 52, 28, RetroUi.Muted);
            DataRow(panel, "ResetBuilds", "カスタマイズ", "ふたりの文字・装備・戦い方を初期状態に戻します。", 192,
                () => Confirm("カスタマイズを初期化しますか？", "ふたりの装備設定を初期状態に戻します。", BuildRuntimeStore.ResetSaved));
            DataRow(panel, "ResetSpecials", "登録した必殺技", "自作の必殺技を消去し、初期必殺技に戻します。", 304,
                () => Confirm("登録した必殺技を消去しますか？", "登録済みの必殺技は元に戻せません。", () =>
                {
                    SpecialMoveLibrary.ClearAll();
                    for (int i = 0; i < 2; i++) { var build = BuildRuntimeStore.Get(i).Clone(); build.special = null; BuildRuntimeStore.Set(i, build); }
                    PlayerPrefs.Save();
                }));
            DataRow(panel, "ResetAchievements", "称号の進行", "獲得した称号・達成状況を初期化します。", 416,
                () => Confirm("称号の進行を初期化しますか？", "獲得した称号と達成状況は元に戻せません。", AchievementStore.Reset));
        }

        void DataRow(Transform panel, string name, string title, string description, float y, Action action)
        {
            RetroUi.Label(panel, name + "Title", title, 34, y, 1040, 45, 32);
            RetroUi.Label(panel, name + "Description", description, 34, y + 47, 1210, 42, 25, RetroUi.Muted);
            var button = RetroUi.Button(panel, name, "初期化…", 1360, y + 8, 370, 64, 29, action, shadow: false);
            button.button.interactable = !inBattle;
        }

        static string MusicName(int index) => index == 0 ? "ランダム" : "戦闘曲 0" + index;
        AudioClip Track(int index) => music == null ? null : index == 2 ? music.battleLoop2 : index == 3 ? music.battleLoop3 : music.battleLoop;
        public void Preview(int index)
        {
            int actual = index == 0 ? previewTrack % 3 + 1 : index;
            if (preview.isPlaying && previewTrack == actual) { StopPreview(); return; }
            StopPreview(); previewTrack = actual; preview.clip = Track(actual);
            preview.volume = .35f * GameSettings.MusicGain;
            preview.mute = !TimeController.PresentationEffectsEnabled;
            if (preview.clip != null) preview.Play();
            if (footer != null) footer.text = "試聴中：" + MusicName(actual) + "  ／  もう一度押すと停止";
        }
        void StopPreview()
        {
            if (preview != null) preview.Stop();
            if (footer != null) footer.text = inBattle && GameFlow.FriendMode ? "オンライン対戦は進行中です" : "変更は自動で保存されます";
        }

        void ToggleMusicPicker()
        {
            if (musicPicker != null)
            {
                musicPicker.SetActive(false); Destroy(musicPicker); musicPicker = null; rootGroup.interactable = true;
                EventSystem.current?.SetSelectedGameObject(pages[0].transform.Find("BasicAudio/MusicSelect").gameObject);
                return;
            }
            var picker = RetroUi.Group(root, "MusicPicker", 0, 0, 1920, 1080); musicPicker = picker.gameObject;
            rootGroup.interactable = false;
            picker.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            UguiKit.Panel(picker, "PickerDismiss", 0, 0, 1920, 1080, new Color(0, 0, 0, .25f)).gameObject.AddComponent<Button>().onClick.AddListener(ToggleMusicPicker);
            RetroUi.Block(picker, "PickerPaper", 280, 448, 720, 400, RetroUi.Paper);
            RetroUi.Frame(picker, 280, 448, 720, 400, RetroUi.Ink, 2);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var b = RetroUi.Button(picker, "PickerMusic" + i, MusicName(i), 306, 478 + 86 * i, 668, 68, 32,
                    () => { StopPreview(); GameSettings.Change(s => s.battleMusic = index); ToggleMusicPicker(); }, shadow: false);
                RetroUi.Selected(b, i == GameSettings.BattleMusic, RetroUi.Red);
            }
            EventSystem.current?.SetSelectedGameObject(picker.Find("PickerMusic" + GameSettings.BattleMusic).gameObject);
        }

        public void ShowTab(int index)
        {
            StopPreview();
            if (musicPicker != null) { Destroy(musicPicker); musicPicker = null; rootGroup.interactable = true; }
            for (int i = 0; i < pages.Length; i++) { pages[i].SetActive(i == index); RetroUi.Selected(tabs[i], i == index, RetroUi.Ink); }
            Refresh();
        }
        void Refresh() { foreach (var refresh in refreshers) refresh(); }

        void Confirm(string title, string description, Action action)
        {
            if (confirmation != null) return;
            StopPreview();
            var dialog = RetroUi.Group(root, "SettingsConfirmation", 0, 0, 1920, 1080); confirmation = dialog.gameObject;
            rootGroup.interactable = false;
            dialog.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            UguiKit.Panel(dialog, "ConfirmDim", 0, 0, 1920, 1080, new Color(0, 0, 0, .65f));
            RetroUi.Block(dialog, "DialogPaper", 420, 328, 1080, 420, RetroUi.Paper);
            RetroUi.Frame(dialog, 420, 328, 1080, 420, RetroUi.Ink, 3);
            RetroUi.Label(dialog, "ConfirmTitle", title, 460, 366, 1000, 90, 39, null, TextAnchor.MiddleCenter);
            RetroUi.Label(dialog, "ConfirmDescription", description, 460, 480, 1000, 80, 28, RetroUi.Muted, TextAnchor.MiddleCenter);
            RetroUi.Button(dialog, "SettingsCancel", "キャンセル", 490, 620, 440, 76, 30, DismissConfirmation, shadow: false);
            RetroUi.Button(dialog, "SettingsConfirm", "実行する", 990, 620, 440, 76, 30,
                () => { DismissConfirmation(); action(); Refresh(); footer.text = "初期化しました"; }, RetroUi.Red, false);
            EventSystem.current?.SetSelectedGameObject(dialog.Find("SettingsCancel").gameObject);
        }
        void DismissConfirmation()
        {
            if (confirmation != null) { confirmation.SetActive(false); Destroy(confirmation); }
            confirmation = null; rootGroup.interactable = true;
            EventSystem.current?.SetSelectedGameObject(tabs[0].button.gameObject);
        }

        void Update()
        {
            if (preview != null) preview.volume = .35f * GameSettings.MusicGain;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (confirmation != null) DismissConfirmation();
                else if (musicPicker != null) ToggleMusicPicker();
                else Close();
            }
        }
        public void Close()
        {
            if (closing) return; closing = true;
            gameObject.SetActive(false); Destroy(gameObject);
        }
        void OnDisable() => Restore();
        void OnDestroy() => Restore();
        void Restore()
        {
            if (Instance != this) return;
            GameSettings.Changed -= Refresh;
            StopPreview();
            foreach (var raycaster in blocked) if (raycaster != null) raycaster.enabled = true;
            foreach (var item in blockedGroups)
                if (item.group != null) { item.group.interactable = item.interactable; if (item.created) Destroy(item.group); }
            if (pausesBattle) TimeController.SetPaused(previousPause);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
            closedFrame = Time.frameCount;
            Instance = null;
        }
    }
}
