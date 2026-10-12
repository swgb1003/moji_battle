using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MojiBattle
{
    /// <summary>
    /// 技名をルームサーバーへ送り、AI が設計した必殺技を受け取る。
    /// 無料枠のサーバーは休止していることがあるので、先に /health で起動を待ってから 1 回だけ生成を頼む（生成は課金されるため再送しない）。
    /// </summary>
    public sealed class SpecialMoveClient
    {
        [Serializable] sealed class GenerateRequest { public string version = FriendRoomClient.Version, character, name; }
        [Serializable] sealed class GenerateResponse { public SpecialMoveData special; public string error; }

        public string ServerUrl { get; }
        public SpecialMoveData Result { get; private set; }
        public string Error { get; private set; }

        public SpecialMoveClient(string url = null) { ServerUrl = (url ?? FriendRoomSession.ResolvedServerUrl).TrimEnd('/'); }

        public IEnumerator Generate(string character, string name, Func<bool> stillCurrent = null, Action onWaking = null)
        {
            Result = null; Error = null;
            var rooms = new FriendRoomClient(ServerUrl);
            yield return rooms.WaitUntilReady(stillCurrent, onWaking);
            if (stillCurrent != null && !stillCurrent()) yield break;
            if (!string.IsNullOrEmpty(rooms.Error)) { Error = rooms.Error.Replace("ルームサーバー", "サーバー"); yield break; }
            var body = JsonUtility.ToJson(new GenerateRequest { character = character, name = name.Trim() });
            using (var request = new UnityWebRequest(rooms.ServerUrl + "/specials", "POST"))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 75;
                yield return request.SendWebRequest();
                GenerateResponse response = null;
                try { response = JsonUtility.FromJson<GenerateResponse>(request.downloadHandler.text); } catch { }
                if (request.result != UnityWebRequest.Result.Success || response?.special == null || !response.special.IsSet)
                {
                    Error = !string.IsNullOrEmpty(response?.error) ? response.error
                        : request.result == UnityWebRequest.Result.Success ? "サーバーの応答を読み取れません。"
                        : "サーバーに接続できません。通信環境を確認してください。";
                    yield break;
                }
                Result = SpecialMoveRules.Sanitize(response.special, character);
                if (Result == null) Error = "サーバーの応答を読み取れません。";
            }
        }
    }
}
