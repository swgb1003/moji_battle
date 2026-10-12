using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed class FriendRoomScreen : MonoBehaviour
    {
        FriendRoomSession session;
        GameObject entry, lobby, startShadow, readyShadow;
        InputField codeInput, serverInput;
        GameObject serverPanel;
        readonly Text[] codeCells = new Text[6], glyphs = new Text[2], summaries = new Text[2], readyText = new Text[2], headings = new Text[2];
        Text entryMessage, lobbyMessage, roomCode, bothStatus, settingsError;
        readonly Image[] readySquares = new Image[2];
        UguiKit.ChoiceButton create, join, start, ready, customize, copy;
        Button exit;
        int shownRevision = -1;
        string shownMessage;
        void Start()
        {
            GameFlow.EnterFriendMode();
            if (Camera.main != null) { Camera.main.backgroundColor = RetroUi.Paper; Camera.main.cullingMask = 0; }
            session = FriendRoomSession.Ensure();
            BuildEntry(); BuildLobby(); session.Changed += Refresh; Refresh();
        }
        void BuildEntry()
        {
            var root = RetroUi.Screen("FriendEntryCanvas", out var canvas); entry = canvas.gameObject;
            RetroUi.Header(root, "03 / FRIEND BATTLE", () => session.Leave(true));
            RetroUi.Heading(root, "友人と対戦", 82);
            RetroUi.Label(root, "FriendSubtitle", "ルームコードで、友人と文字バトル。", 300, 255, 1320, 60, 34, null, TextAnchor.MiddleCenter);
            var left = Panel(root, "CreateRoomPanel", 96, 334, 846, 654, RetroUi.Red, "部屋をつくる");
            var right = Panel(root, "JoinRoomPanel", 978, 334, 846, 654, RetroUi.Blue, "コードで参加");
            RetroUi.Label(left, "InviteHeading", "友人を招待する", 32, 100, 782, 100, 64, null, TextAnchor.MiddleCenter);
            // Printed ticket, built from the same ink rules as the existing menus.
            RetroUi.Group(left, "RoomTicket", 184, 234, 478, 142).gameObject.AddComponent<FriendRoomTicket>();
            for (int i = 0; i < 3; i++) RetroUi.Line(left, new Vector2(108, 262 + i * 52), new Vector2(155, 288 + i * 25), RetroUi.Ink, 5);
            for (int i = 0; i < 3; i++) RetroUi.Line(left, new Vector2(693, 288 + i * 25), new Vector2(740, 262 + i * 52), RetroUi.Ink, 5);
            RetroUi.Label(left, "InviteHelp", "コードを発行して\n友人に教えよう。", 44, 404, 758, 92, 35, null, TextAnchor.MiddleCenter);
            create = RetroUi.Button(left, "CreateRoom", "部屋をつくる →", 78, 533, 690, 90, 42, () => session.Connect(true), RetroUi.Red);
            RetroUi.Label(right, "JoinHeading", "友人の部屋に入る", 24, 100, 798, 100, 64, null, TextAnchor.MiddleCenter);
            RetroUi.Label(right, "CodeHint", "6桁のルームコード", 40, 212, 766, 48, 33, null, TextAnchor.MiddleCenter);
            for (int i = 0; i < 6; i++)
            {
                RetroUi.Frame(right, 48 + i * 126, 283, 118, 118, RetroUi.Ink, 2);
                codeCells[i] = RetroUi.Label(right, "CodeCell" + i, "", 48 + i * 126, 281, 118, 118, 76, null, TextAnchor.MiddleCenter);
            }
            codeInput = UguiKit.InputBox(right, "RoomCodeInput", 48, 283, 748, 118, 76, _ => { });
            // The six printed cells supply all visible code text, including the empty state.
            ((Text)codeInput.placeholder).text = "";
            codeInput.characterLimit = 0; codeInput.textComponent.color = Color.clear;
            codeInput.GetComponent<Image>().color = Color.clear; codeInput.GetComponent<Outline>().enabled = false;
            codeInput.caretWidth = 2; codeInput.customCaretColor = true; codeInput.caretColor = RetroUi.Red;
            codeInput.selectionColor = new Color(RetroUi.Blue.r, RetroUi.Blue.g, RetroUi.Blue.b, .15f);
            codeInput.onValueChanged.AddListener(UpdateCode);
            WebCharacterInput.Attach(codeInput,
                () => entry.activeSelf && (serverPanel == null || !serverPanel.activeSelf), () => 0,
                "ルームコード", "", 0, .64f, usePrintedCode: true, onSubmit: Join);
            RetroUi.Button(right, "PasteRoomCode", "貼り付け", 594, 425, 202, 56, 29, () => UpdateCode(GUIUtility.systemCopyBuffer), shadow: false);
            join = RetroUi.Button(right, "JoinRoom", "参加する →", 78, 533, 690, 90, 42, Join, RetroUi.Blue);
            RetroUi.Block(root, "FooterRule", 96, 996, 1728, 2, RetroUi.Ink);
            entryMessage = RetroUi.Label(root, "FriendEntryMessage", "武器と持ち方を決めて、対戦を始めよう。", 240, 1000, 1310, 34, 24, null, TextAnchor.MiddleCenter);
            if (!FriendRoomSession.HasBundledServer)
            {
                RetroUi.Button(root, "RoomServerSettings", "接続設定", 1640, 1000, 178, 34, 21, () => serverPanel.SetActive(true), shadow: false);
                BuildServerSettings(root);
            }
            RetroUi.Enter(left.gameObject, .05f); RetroUi.Enter(right.gameObject, .12f);
        }
        static RectTransform Panel(Transform root, string name, float x, float y, float w, float h, Color color, string title)
        {
            var panel = RetroUi.Group(root, name, x, y, w, h); RetroUi.Frame(panel, 0, 0, w, h, RetroUi.Ink, 2);
            RetroUi.Block(panel, "TitleBand", 10, 10, w - 20, 80, color);
            RetroUi.Label(panel, "Title", title, 42, 10, w - 84, 80, 42, RetroUi.Paper);
            return panel;
        }
        void BuildLobby()
        {
            var root = RetroUi.Screen("FriendLobbyCanvas", out var canvas); lobby = canvas.gameObject;
            // Header uses the same geometry as entry, with the approved exit label.
            RetroUi.Header(root, "04 / ROOM LOBBY", () => session.Leave());
            exit = root.Find("Back").GetComponent<Button>();
            foreach (var t in root.GetComponentsInChildren<Text>()) if (t.transform.parent.name == "Back") t.text = "← 退出";
            RetroUi.Heading(root, "対戦ルーム", 82);
            RetroUi.Frame(root, 416, 264, 1088, 118, RetroUi.Ink, 2);
            RetroUi.Label(root, "RoomCodeCaption", "ROOM CODE", 448, 292, 232, 62, 28);
            roomCode = RetroUi.Label(root, "RoomCode", "", 693, 269, 580, 106, 86, null, TextAnchor.MiddleCenter);
            copy = RetroUi.Button(root, "CopyRoomCode", "コピー", 1300, 294, 182, 62, 29, CopyCode, shadow: false);
            RetroUi.Label(root, "ShareHelp", "このコードを友人に伝えてください", 390, 390, 1140, 44, 27, null, TextAnchor.MiddleCenter);
            for (int side = 0; side < 2; side++)
            {
                var panel = Panel(root, "RoomPlayer" + side, side == 0 ? 96 : 1048, 458, 776, 398, RetroUi.Team(side), "");
                headings[side] = panel.Find("Title").GetComponent<Text>();
                RetroUi.Block(panel, "RolePaper", 540, 20, 213, 59, RetroUi.Paper);
                RetroUi.Label(panel, "Role", side == 0 ? "ホスト" : "ゲスト", 540, 20, 213, 59, 29, null, TextAnchor.MiddleCenter);
                glyphs[side] = RetroUi.Label(panel, "RoomGlyph" + side, "", 36, 93, 355, 294, 232, RetroUi.Team(side), TextAnchor.MiddleCenter);
                summaries[side] = RetroUi.Label(panel, "RoomBuild" + side, "", 420, 117, 336, 124, 33);
                RetroUi.Block(panel, "SummaryRule", 420, 275, 308, 1, RetroUi.Rule);
                readySquares[side] = RetroUi.Block(panel, "ReadySquare" + side, 427, 305, 47, 47, RetroUi.Rule);
                readyText[side] = RetroUi.Label(panel, "RoomReady" + side, "", 510, 298, 240, 64, 35);
            }
            RetroUi.Label(root, "RoomVs", "VS", 890, 585, 140, 100, 66, null, TextAnchor.MiddleCenter);
            RetroUi.Block(root, "VsRule", 922, 576, 76, 2, RetroUi.Ink); RetroUi.Block(root, "VsRule", 922, 693, 76, 2, RetroUi.Ink);
            RetroUi.Block(root, "LobbyFooterRule", 96, 870, 1728, 2, RetroUi.Ink);
            customize = RetroUi.Button(root, "RoomCustomize", "装備をカスタマイズ", 150, 897, 470, 84, 32, () => session.Customize());
            bothStatus = RetroUi.Label(root, "BothReadyStatus", "", 655, 890, 510, 95, 31, null, TextAnchor.MiddleCenter);
            start = RetroUi.Button(root, "RoomStart", "対戦開始 →", 1240, 897, 560, 84, 42, () => session.BeginBattle(), RetroUi.Red);
            ready = RetroUi.Button(root, "RoomSetReady", "準備完了にする", 1240, 897, 560, 84, 37, () => session.ToggleReady(), RetroUi.Blue);
            startShadow = root.Find("RoomStartShadow").gameObject; readyShadow = root.Find("RoomSetReadyShadow").gameObject;
            lobbyMessage = RetroUi.Label(root, "LobbyMessage", "", 110, 1000, 1480, 46, 25, RetroUi.Red);
            lobby.SetActive(false);
        }
        void BuildServerSettings(Transform root)
        {
            var shade = UguiKit.Fill(root, "ServerSettingsShade").gameObject.AddComponent<Image>(); shade.color = new Color(0, 0, 0, .5f);
            serverPanel = shade.gameObject;
            var panel = RetroUi.Group(shade.transform, "ServerSettings", 460, 320, 1000, 430);
            RetroUi.Block(panel, "Paper", 0, 0, 1000, 430, RetroUi.Paper); RetroUi.Frame(panel, 0, 0, 1000, 430, RetroUi.Ink, 3);
            RetroUi.Label(panel, "SettingsTitle", "ルームサーバーの接続先", 40, 32, 920, 80, 42);
            serverInput = UguiKit.InputBox(panel, "RoomServerUrl", 40, 138, 920, 72, 28, _ => { });
            serverInput.text = session.ServerUrl;
            RetroUi.Label(panel, "ServerHelp", "友人と同じ接続先を設定してください。", 40, 232, 920, 54, 27, RetroUi.Muted);
            settingsError = RetroUi.Label(panel, "ServerSettingsError", "", 40, 280, 920, 36, 23, RetroUi.Red);
            RetroUi.Button(panel, "SaveRoomServer", "保存", 586, 325, 374, 65, 31, SaveServer, RetroUi.Red);
            RetroUi.Button(panel, "CloseRoomServer", "閉じる", 40, 325, 374, 65, 31, () => serverPanel.SetActive(false));
            serverPanel.SetActive(false);
        }
        void SaveServer()
        {
            if (session.SetServer(serverInput.text)) { settingsError.text = ""; serverPanel.SetActive(false); entryMessage.text = "接続先を保存しました。"; }
            else settingsError.text = "http:// または https:// で始まる接続先を入力してください。";
        }
        void UpdateCode(string value)
        {
            string code = FriendRoomClient.NormalizeCode(value); if (code.Length > 6) code = code.Substring(0, 6);
            codeInput.SetTextWithoutNotify(code);
            for (int i = 0; i < 6; i++) { codeCells[i].text = i < code.Length ? code[i].ToString() : ""; codeCells[i].color = RetroUi.Ink; }
            if (join != null) join.button.interactable = FriendRoomClient.ValidCode(code) && !session.Busy;
        }
        void Join() { if (!session.Busy && FriendRoomClient.ValidCode(codeInput.text)) session.Connect(false, codeInput.text); }
        void CopyCode() { GUIUtility.systemCopyBuffer = session.Room.code; copy.label.text = "コピー済み"; Invoke(nameof(RestoreCopy), 1.5f); }
        void RestoreCopy() { copy.label.text = "コピー"; }
        void Refresh()
        {
            bool inRoom = session.Connected;
            entry.SetActive(!inRoom); lobby.SetActive(inRoom);
            create.button.interactable = !session.Busy; codeInput.interactable = !session.Busy;
            join.button.interactable = !session.Busy && FriendRoomClient.ValidCode(codeInput.text);
            entryMessage.text = session.Message ?? "武器と持ち方を決めて、対戦を始めよう。";
            entryMessage.color = session.Message != null ? RetroUi.Red : RetroUi.Ink;
            if (!inRoom) return;
            var room = session.Room;
            roomCode.text = string.Join(" ", room.code.ToCharArray());
            bool localReady = room.players[room.side].ready;
            if (shownRevision != room.revision || shownMessage != session.Message)
            {
                for (int i = 0; i < 2; i++)
                {
                    var p = room.players[i]; bool local = room.side == i;
                    headings[i].text = "P" + (i + 1) + (local ? " / YOU" : " / FRIEND");
                    glyphs[i].text = p.joined ? p.build.character : "?";
                    if (p.joined) glyphs[i].font = GlyphFontLibrary.Default.Get(p.build.fontType).font;
                    summaries[i].text = p.joined ? GlyphCatalog.FontDisplayName(p.build.fontType) + " / " + p.build.weaponSize + "\n" + CustomizeLabels.Grip(p.build.gripType) + " / " + CustomizeLabels.StyleShort(p.build.battleStyle) : "友人の参加を\n待っています";
                    readyText[i].text = !p.joined ? "未参加" : p.ready ? "準備完了" : "準備中";
                    readySquares[i].color = p.ready ? RetroUi.Team(i) : RetroUi.Wash;
                }
                shownRevision = room.revision; shownMessage = session.Message;
            }
            bool hostStart = session.IsHost && localReady;
            start.button.gameObject.SetActive(hostStart); ready.button.gameObject.SetActive(!hostStart);
            startShadow.SetActive(hostStart); readyShadow.SetActive(!hostStart);
            start.button.interactable = room.BothReady && !session.Busy;
            ready.label.text = localReady ? "準備完了を解除" : "準備完了にする";
            ready.button.interactable = !session.Busy;
            // Host can undo ready even while showing the approved start button.
            bothStatus.text = room.BothReady ? "2人とも準備完了" : !room.players[1].joined ? "友人の参加を待っています" : localReady ? "友人の準備を待っています" : "装備を決めて準備完了に";
            customize.button.interactable = !session.Busy;
            lobbyMessage.text = session.Message ?? (session.IsHost && localReady ? "装備を変更すると、準備完了が解除されます。" : "");
            lobbyMessage.color = session.Message != null ? RetroUi.Red : RetroUi.Muted;
            exit.interactable = !session.Busy;
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) && entry.activeSelf && (serverPanel == null || !serverPanel.activeSelf) && EventSystem.current.currentSelectedGameObject == codeInput.gameObject) Join();
            if (Input.GetKeyDown(KeyCode.Escape)) { if (serverPanel != null && serverPanel.activeSelf) serverPanel.SetActive(false); else if (session.Connected) session.Leave(); else session.Leave(true); }
        }
        void OnDestroy() { if (session != null) session.Changed -= Refresh; }
    }
}
