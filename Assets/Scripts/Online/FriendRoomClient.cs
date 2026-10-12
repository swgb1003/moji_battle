using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MojiBattle
{
    [Serializable] public sealed class RoomPlayer { public bool joined, ready; public FighterBuildData build; }
    [Serializable] public sealed class RoomState
    {
        public string code, token, stage;
        public int side, revision, seed, matchId;
        public RoomPlayer[] players;
        public FriendBattleFrame frame;
        /// <summary>ゲストの関与の操作（ホストだけが読んで試合に反映する）。</summary>
        public FriendInputFrame[] inputs;
        public bool BothReady => players?.Length == 2 && players[0].joined && players[1].joined && players[0].ready && players[1].ready;
    }
    [Serializable] sealed class RoomRequest
    {
        public string version = FriendRoomClient.Version;
        public FighterBuildData build;
        public bool ready;
        public int matchId;
        public FriendBattleFrame frame;
        public FriendInputFrame input;
    }
    [Serializable] sealed class RoomError { public string error; }
    [Serializable] sealed class RoomHealth { public bool ok; public string version; }

    /// <summary>Each client holds its own bearer credential; room state never exposes another player's token.</summary>
    public sealed class FriendRoomClient
    {
        public const string Version = "moji-fighter-x-rooms-v3";
        public string ServerUrl { get; private set; }
        /// <summary>
        /// 予備の経路: ゲームのページと同じドメインの /rooms-api（Vercel がサーバーへ中継する）。
        /// 拡張機能やフィルタが別ドメインへの通信だけを止める環境でもつながる。直接つながらなかった時だけ使い、その後はこのページを開いている間ずっと使う。
        /// </summary>
        public string RelayUrl { get; }
        public static bool PreferRelay { get; private set; }
        public string Code { get; private set; }
        public string Token { get; private set; }
        public RoomState State { get; private set; }
        public string Error { get; private set; }
        public long StatusCode { get; private set; }
        bool sending;
        public FriendRoomClient(string url, string relayUrl = null)
        {
            ServerUrl = url.TrimEnd('/');
            RelayUrl = relayUrl ?? (ServerUrl == FriendRoomSession.BundledServerUrl ? FriendRoomServerConfig.RelayUrl(Application.absoluteURL) : null);
            if (PreferRelay && RelayUrl != null) ServerUrl = RelayUrl;
        }
        public IEnumerator WaitUntilReady(Func<bool> stillCurrent = null, Action onWaiting = null, float waitSeconds = 90)
        {
            Error = null;
            double deadline = Time.realtimeSinceStartupAsDouble + waitSeconds;
            bool announced = false;
            string direct = ServerUrl == RelayUrl ? null : ServerUrl;
            int attempt = 0;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (stillCurrent != null && !stillCurrent()) yield break;
                // 直接と予備の経路を交互に試す（予備が無ければ直接だけ）
                string candidate = RelayUrl == null || direct == null ? ServerUrl : attempt++ % 2 == (PreferRelay ? 1 : 0) ? direct : RelayUrl;
                using (var request = UnityWebRequest.Get(candidate + "/health"))
                {
                    request.timeout = Math.Max(1, Math.Min(10, (int)Math.Ceiling(deadline - Time.realtimeSinceStartupAsDouble)));
                    var pending = request.SendWebRequest();
                    while (!pending.isDone)
                    {
                        if (stillCurrent != null && !stillCurrent()) { request.Abort(); yield break; }
                        yield return null;
                    }
                    StatusCode = request.responseCode;
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        RoomHealth health = null;
                        try { health = JsonUtility.FromJson<RoomHealth>(request.downloadHandler.text); } catch { }
                        if (health != null && health.ok && health.version == Version)
                        {
                            ServerUrl = candidate;
                            if (candidate == RelayUrl && direct != null) PreferRelay = true;
                            yield break;
                        }
                        if (health != null && !string.IsNullOrEmpty(health.version) && health.version != Version)
                        { Error = "ルームサーバーのバージョンが合いません。"; yield break; }
                    }
                }
                if (!announced) { announced = true; onWaiting?.Invoke(); }
                // Only the read-only health request is retried; never duplicate room creation.
                yield return new WaitForSecondsRealtime(Mathf.Max(0, Mathf.Min(2, (float)(deadline - Time.realtimeSinceStartupAsDouble))));
            }
            Error = "サーバーを起動できませんでした。少し待ってからもう一度お試しください。";
        }
        public static string NormalizeCode(string value) => (value ?? "").Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
        public static bool ValidCode(string value) => System.Text.RegularExpressions.Regex.IsMatch(value ?? "", "^[A-HJ-NP-Z2-9]{6}$");
        public IEnumerator Create(FighterBuildData build) => Call("/rooms", new RoomRequest { build = build });
        public IEnumerator Join(string code, FighterBuildData build) => Call("/rooms/" + NormalizeCode(code) + "/join", new RoomRequest { build = build });
        public IEnumerator Poll() => Call("/rooms/" + Code, null);
        public IEnumerator SetReady(bool ready) => Call("/rooms/" + Code + "/ready", new RoomRequest { ready = ready });
        public IEnumerator SetBuild(FighterBuildData build) => Call("/rooms/" + Code + "/build", new RoomRequest { build = build });
        public IEnumerator StartBattle() => Call("/rooms/" + Code + "/start", new RoomRequest());
        public IEnumerator ReturnToLobby() => Call("/rooms/" + Code + "/lobby", new RoomRequest());
        public IEnumerator Leave() => Call("/rooms/" + Code + "/leave", new RoomRequest());
        public IEnumerator SendFrame(int matchId, FriendBattleFrame frame) => Call("/rooms/" + Code + "/frame", new RoomRequest { matchId = matchId, frame = frame });
        public IEnumerator SendInput(int matchId, FriendInputFrame input) => Call("/rooms/" + Code + "/input", new RoomRequest { matchId = matchId, input = input });
        /// <summary>直近の通信の往復秒。</summary>
        public float LastRoundTrip { get; private set; } = .2f;
        IEnumerator Call(string path, RoomRequest body)
        {
            while (sending) yield return null;
            sending = true;
            try { yield return Request(path, body); }
            finally { sending = false; }
        }
        IEnumerator Request(string path, RoomRequest body)
        {
            Error = null; StatusCode = 0;
            using (var request = new UnityWebRequest(ServerUrl + path, body == null ? "GET" : "POST"))
            {
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 5;
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (!string.IsNullOrEmpty(Token)) request.SetRequestHeader("Authorization", "Bearer " + Token);
                double sentAt = Time.realtimeSinceStartupAsDouble;
                yield return request.SendWebRequest();
                StatusCode = request.responseCode;
                if (request.result == UnityWebRequest.Result.Success)
                    LastRoundTrip = Mathf.Lerp(LastRoundTrip, (float)(Time.realtimeSinceStartupAsDouble - sentAt), .3f);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    try { Error = JsonUtility.FromJson<RoomError>(request.downloadHandler.text)?.error; } catch { }
                    if (string.IsNullOrEmpty(Error)) Error = "サーバーに接続できません。接続先と通信環境を確認してください。";
                    yield break;
                }
                RoomState state = null;
                try { state = JsonUtility.FromJson<RoomState>(request.downloadHandler.text); }
                catch { Error = "サーバーの応答を読み取れません。"; }
                bool requiresRoom = body?.frame == null && !path.EndsWith("/leave");
                if (requiresRoom && (state == null || string.IsNullOrEmpty(state.code) || state.players?.Length != 2 || state.side < 0 || state.side > 1))
                { Error = "ルームサーバーの応答を読み取れません。接続先を確認してください。"; yield break; }
                if (state != null && !string.IsNullOrEmpty(state.code) && (State == null || state.code != State.code || state.revision >= State.revision))
                {
                    // JsonUtility can materialize an empty nested class for JSON null.
                    if (state.stage != "battle" || state.frame == null || state.frame.sequence < 1 || state.frame.actors?.Length != 2) state.frame = null;
                    Code = state.code;
                    if (!string.IsNullOrEmpty(state.token)) Token = state.token;
                    State = state;
                }
            }
        }
    }
}
