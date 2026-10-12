using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MojiBattle
{
    /// <summary>The connection survives customization and battle scenes, but ordinary modes remain independent.</summary>
    public sealed class FriendRoomSession : MonoBehaviour
    {
        public static FriendRoomSession Instance { get; private set; }
        public static FriendRoomSession Ensure()
        {
            if (Instance == null) new GameObject("FriendRoomSession").AddComponent<FriendRoomSession>();
            return Instance;
        }
        public FriendRoomClient Client { get; private set; }
        public RoomState Room => Client?.State;
        public FighterBuildData Draft { get; private set; }
        public bool Busy { get; private set; }
        public bool Connected => Client != null && !string.IsNullOrEmpty(Client.Token) && !Lost;
        public bool Lost { get; private set; }
        public bool IsHost => Room?.side == 0;
        public string Message { get; private set; }
        public event Action Changed;
        int generation;
        double lastSuccess;
        public static string BundledServerUrl => FriendRoomServerConfig.PublicUrl(Resources.Load<TextAsset>("Online/RoomServer")?.text);
        public static bool HasBundledServer => BundledServerUrl != null;
        public static string DefaultServerUrl => BundledServerUrl ?? FriendRoomServerConfig.LocalUrl;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Explicit, non-persistent override for local development and automated connection tests.
        public static string DevelopmentServerOverride { get; set; }
#endif
        public string ServerUrl => ResolvedServerUrl;
        /// <summary>接続先（部屋がなくても使う。AI の必殺技づくりも同じサーバー）。</summary>
        public static string ResolvedServerUrl
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!string.IsNullOrWhiteSpace(DevelopmentServerOverride)) return DevelopmentServerOverride;
#endif
                return FriendRoomServerConfig.Resolve(BundledServerUrl, PlayerPrefs.GetString("FriendRoom.ServerUrl", ""));
            }
        }
        void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; DontDestroyOnLoad(gameObject); }
        void OnDestroy() { if (Instance == this) Instance = null; }
        public bool SetServer(string url)
        {
            if (HasBundledServer) return false;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo)) return false;
            if (Connected || Busy) return false;
            PlayerPrefs.SetString("FriendRoom.ServerUrl", url.Trim().TrimEnd('/')); PlayerPrefs.Save(); return true;
        }
        public void Connect(bool create, string code = "")
        {
            if (Busy || Connected) return;
            if (!create && !FriendRoomClient.ValidCode(FriendRoomClient.NormalizeCode(code))) { Message = "6桁の英数字のコードを入力してください。"; Changed?.Invoke(); return; }
            StartCoroutine(ConnectRoutine(create, code));
        }
        IEnumerator ConnectRoutine(bool create, string code)
        {
            int epoch = ++generation;
            Busy = true; Message = "サーバーに接続しています…"; Changed?.Invoke();
            var client = new FriendRoomClient(ServerUrl); Client = client; Lost = false;
            Draft = BuildRuntimeStore.Get(0).Clone();
            yield return client.WaitUntilReady(() => epoch == generation && Client == client, () =>
            {
                if (epoch != generation || Client != client) return;
                Message = "サーバーの起動を待っています…（初回は約1分かかる場合があります）"; Changed?.Invoke();
            });
            if (epoch != generation || Client != client) yield break;
            if (client.Error != null) { Busy = false; Message = client.Error; Changed?.Invoke(); yield break; }
            Message = create ? "部屋を作成しています…" : "部屋に参加しています…"; Changed?.Invoke();
            if (create) yield return client.Create(Draft); else yield return client.Join(code, Draft);
            if (epoch != generation || Client != client) { if (!string.IsNullOrEmpty(client.Token)) yield return client.Leave(); yield break; }
            Busy = false; Message = client.Error;
            if (client.Error == null)
            {
                lastSuccess = Time.realtimeSinceStartupAsDouble;
                Draft = Room.players[Room.side].build.Clone();
                StartCoroutine(PollLoop(epoch));
            }
            Changed?.Invoke();
        }
        IEnumerator PollLoop(int epoch)
        {
            while (epoch == generation && Connected)
            {
                yield return new WaitForSecondsRealtime(Room.stage == "battle" ? .08f : .35f);
                if (epoch != generation || !Connected) yield break;
                if (Busy) continue;
                var client = Client;
                yield return client.Poll();
                if (epoch != generation) yield break;
                if (client.Error != null)
                {
                    Message = client.Error;
                    if (client.StatusCode == 401 || client.StatusCode == 404 || Time.realtimeSinceStartupAsDouble - lastSuccess > 12)
                    { Lost = true; Message = "部屋への接続が切れました。入り直してください。"; }
                }
                else { lastSuccess = Time.realtimeSinceStartupAsDouble; Message = null; }
                Changed?.Invoke();
                Navigate();
            }
        }
        void Navigate()
        {
            if (!GameFlow.FriendMode) return;
            string scene = SceneManager.GetActiveScene().name;
            if (Lost && scene != GameFlow.FriendRoomScene) { TimeController.ResetAll(); GameFlow.ToFriendRoom(false); }
            else if (Connected && Room.stage == "battle" && scene != GameFlow.BattleScene) GameFlow.ToFriendBattle();
            else if (Connected && Room.stage == "lobby" && scene == GameFlow.BattleScene) GameFlow.ToFriendRoom(false);
        }
        public void ToggleReady() => Run(Client.SetReady(!Room.players[Room.side].ready));
        public void BeginBattle() { if (Connected && IsHost && Room.BothReady) Run(Client.StartBattle()); }
        public void Customize() { if (Connected && Room.stage == "lobby") Run(Client.SetReady(false), () => { Draft = Room.players[Room.side].build.Clone(); GameFlow.ToFriendCustomize(); }); }
        public void SaveDraft(FighterBuildData build) => Draft = build.Clone();
        public void SaveAndReturn() { if (Connected) Run(Client.SetBuild(Draft), () => GameFlow.ToFriendRoom(false)); else GameFlow.ToFriendRoom(false); }
        public void BackToLobby() { if (Connected) Run(Client.ReturnToLobby(), () => GameFlow.ToFriendRoom(false)); else GameFlow.ToFriendRoom(false); }
        void Run(IEnumerator operation, Action after = null) { if (!Busy && Connected) StartCoroutine(Operation(operation, after)); }
        IEnumerator Operation(IEnumerator operation, Action after)
        {
            int epoch = generation; var client = Client;
            Busy = true; Message = null; Changed?.Invoke();
            yield return operation;
            if (epoch != generation || Client != client) yield break;
            Busy = false; Message = client.Error;
            if (Message == null) { lastSuccess = Time.realtimeSinceStartupAsDouble; after?.Invoke(); Navigate(); }
            Changed?.Invoke();
        }
        public void Leave(bool toModeSelect = false)
        {
            if (Busy && !Connected) { CloseImmediately(); if (toModeSelect) GameFlow.ToModeSelect(); return; }
            if (!Busy) StartCoroutine(LeaveRoutine(toModeSelect));
        }
        IEnumerator LeaveRoutine(bool toModeSelect)
        {
            Busy = true; generation++; Changed?.Invoke();
            if (Client != null && !string.IsNullOrEmpty(Client.Token)) yield return Client.Leave();
            Client = null; Draft = null; Lost = false; Busy = false; Message = null; Changed?.Invoke();
            if (toModeSelect) GameFlow.ToModeSelect();
        }
        public void CloseImmediately()
        {
            generation++; var old = Client;
            Client = null; Draft = null; Lost = false; Message = null; Busy = false;
            if (old != null && !string.IsNullOrEmpty(old.Token)) StartCoroutine(old.Leave());
            Changed?.Invoke();
        }
    }
}
