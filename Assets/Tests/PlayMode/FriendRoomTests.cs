using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MojiBattle.Tests
{
    public class FriendRoomTests
    {
        FighterBuildData left, right;
        string serverUrl, developmentServer;
        int reduced;
        static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
        static IEnumerator Until(Func<bool> condition, string reason, float timeout = 12)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeout;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(condition(), reason);
        }
        static IEnumerator Ready() { yield return null; yield return null; yield return new WaitForSecondsRealtime(.85f); }
        [SetUp]
        public void Setup()
        {
            left = BuildRuntimeStore.Get(0).Clone(); right = BuildRuntimeStore.Get(1).Clone();
            serverUrl = PlayerPrefs.GetString("FriendRoom.ServerUrl", ""); reduced = PlayerPrefs.GetInt("MenuReducedMotion", 0);
            developmentServer = FriendRoomSession.DevelopmentServerOverride;
            FriendRoomSession.DevelopmentServerOverride = "http://127.0.0.1:8787";
            PlayerPrefs.SetString("FriendRoom.ServerUrl", "http://127.0.0.1:8787"); PlayerPrefs.SetInt("MenuReducedMotion", 0);
            var build = FighterBuildData.Default("力"); build.fontType = FontStyleId.Mincho; BuildRuntimeStore.Set(0, build);
            TimeController.ResetAll();
        }
        [TearDown]
        public void Cleanup()
        {
            GameFlow.ToTitle(); TimeController.ResetAll();
            BuildRuntimeStore.Set(0, left); BuildRuntimeStore.Set(1, right); BuildRuntimeStore.Active = false;
            if (serverUrl.Length == 0) PlayerPrefs.DeleteKey("FriendRoom.ServerUrl"); else PlayerPrefs.SetString("FriendRoom.ServerUrl", serverUrl);
            PlayerPrefs.SetInt("MenuReducedMotion", reduced);
            FriendRoomSession.DevelopmentServerOverride = developmentServer;
        }
        [UnityTest]
        public IEnumerator RoomEntry_EmptyCode_RendersTicketWithoutCharacterPlaceholder()
        {
            GameFlow.ToFriendRoom(); yield return Ready();
            Canvas.ForceUpdateCanvases();
            var ticket = GameObject.Find("RoomTicket").GetComponent<FriendRoomTicket>();
            var renderer = ticket.GetComponent<CanvasRenderer>();
            Assert.IsNotNull(renderer, "The ticket requires a renderer to appear on screen.");
            var mesh = renderer.GetMesh();
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 0, "The ticket must submit visible ink geometry.");
            Assert.Greater(mesh.bounds.size.x, 400);
            Assert.Greater(mesh.bounds.size.y, 100);
            var input = GameObject.Find("RoomCodeInput").GetComponent<InputField>();
            var browserInput = input.GetComponent<WebCharacterInput>();
            Assert.IsNotNull(browserInput, "Web code entry needs a native browser input.");
            Assert.AreEqual("", ((Text)input.placeholder).text);
            Assert.IsFalse(Button("JoinRoom").interactable);
            MenuUiTests.Capture("friend_room_entry_empty_fixed", 1230, 690);
            MenuUiTests.Capture("friend_room_entry_empty_fixed_880x450", 880, 450);
            foreach (var value in new[] { "m", "M7", "M7k", "M7K2", "M7K2q" }) browserInput.ReceiveText(value);
            Assert.IsFalse(Button("JoinRoom").interactable, "Incomplete hand entry cannot join.");
            browserInput.ReceiveText("m7-k2q9");
            Assert.AreEqual("M7K2Q9", input.text);
            Assert.AreEqual("M", GameObject.Find("CodeCell0").GetComponent<Text>().text);
            Assert.AreEqual("9", GameObject.Find("CodeCell5").GetComponent<Text>().text);
            Assert.IsTrue(Button("JoinRoom").interactable);
            browserInput.ReceiveText("M7K2Q"); // Backspace from a complete code.
            Assert.AreEqual("", GameObject.Find("CodeCell5").GetComponent<Text>().text);
            Assert.IsFalse(Button("JoinRoom").interactable);
            input.interactable = false;
            browserInput.ReceiveText("ABCDEF");
            Assert.AreEqual("M7K2Q", input.text, "Busy fields must not accept late browser edits.");
            input.interactable = true;
            input.text = "";
            Assert.AreEqual("", GameObject.Find("CodeCell0").GetComponent<Text>().text);
            Assert.AreEqual("", ((Text)input.placeholder).text);
            Assert.IsFalse(Button("JoinRoom").interactable);
            if (FriendRoomSession.HasBundledServer) Assert.IsNull(GameObject.Find("RoomServerSettings"));
            else
            {
                Button("RoomServerSettings").onClick.Invoke();
                Assert.IsNotNull(GameObject.Find("ServerSettings"));
            }
        }
        [UnityTest]
        public IEnumerator RoomUi_TwoClients_CustomizeReadyStartAndReturn_PreserveOfflineBuilds()
        {
            GameFlow.ToModeSelect(); yield return Ready();
            Button("FriendModeStart").onClick.Invoke(); yield return Ready();
            Assert.IsTrue(GameFlow.FriendMode);
            var input = GameObject.Find("RoomCodeInput").GetComponent<InputField>();
            input.text = "m7-k2q9";
            Assert.AreEqual("M7K2Q9", input.text); Assert.IsTrue(Button("JoinRoom").interactable);
            MenuUiTests.Capture("friend_room_entry", 1230, 690); MenuUiTests.Capture("friend_room_entry_880x450", 880, 450);
            Button("CreateRoom").onClick.Invoke();
            var session = FriendRoomSession.Instance;
            yield return Until(() => session.Connected && !session.Busy, session.Message ?? "Create room failed.");
            yield return Ready();
            Assert.IsTrue(session.IsHost); Assert.IsFalse(Button("RoomStart").gameObject.activeSelf);
            MenuUiTests.Capture("friend_room_waiting", 1230, 690);
            var guest = new FriendRoomClient(session.ServerUrl);
            var build = FighterBuildData.Default("史"); build.fontType = FontStyleId.Mincho; build.gripType = GripType.TwoHanded; build.battleStyle = BattleStyle.Counter;
            yield return guest.Join(session.Room.code, build); Assert.IsNull(guest.Error);
            yield return Until(() => session.Room.players[1].joined, "Guest join must appear on host lobby.");
            session.SelectStage(BattleStageId.EraserFactory);
            yield return Until(()=>!session.Busy && session.Room.arena==(int)BattleStageId.EraserFactory,"Host stage selection must be shared.");
            yield return guest.Poll();Assert.AreEqual((int)BattleStageId.EraserFactory,guest.State.arena);
            Button("CopyRoomCode").onClick.Invoke(); Assert.AreEqual(session.Room.code, GUIUtility.systemCopyBuffer);
            Button("RoomSetReady").onClick.Invoke();
            yield return Until(() => !session.Busy && session.Room.players[0].ready, "Host ready failed.");
            Assert.IsFalse(Button("RoomStart").interactable);
            yield return guest.SetReady(true); Assert.IsNull(guest.Error);
            yield return Until(() => session.Room.BothReady, "Both players ready failed.");
            yield return Ready(); MenuUiTests.Capture("friend_room_lobby", 1230, 690); MenuUiTests.Capture("friend_room_lobby_880x450", 880, 450);
            Assert.IsTrue(Button("RoomStart").interactable);
            var offline = BuildRuntimeStore.Get(0).Clone();
            Button("RoomCustomize").onClick.Invoke();
            yield return Until(() => SceneManager.GetActiveScene().name == GameFlow.CustomizeScene, "Customize transition failed."); yield return Ready();
            var customize = Object.FindAnyObjectByType<CustomizeScreen>();
            Assert.IsFalse(Button("Side1").interactable);
            customize.Customizer.SetCharacter("木"); customize.Customizer.SetGrip(GripType.Horizontal);
            customize.Customizer.SetGripPoint(new Vector2(.3f, .7f));
            Button("Battle").onClick.Invoke();
            yield return Until(() => SceneManager.GetActiveScene().name == GameFlow.FriendRoomScene && !session.Busy, "Return from customize failed."); yield return Ready();
            Assert.IsFalse(session.Room.players[0].ready); Assert.IsTrue(session.Room.players[1].ready);
            yield return guest.Poll(); Assert.AreEqual("木", guest.State.players[0].build.character);
            Assert.IsTrue(guest.State.players[0].build.customGrip);
            Assert.AreEqual("史", guest.State.players[1].build.character);
            Assert.AreEqual(offline.character, BuildRuntimeStore.Get(0).character);
            Assert.AreEqual(offline.gripType, BuildRuntimeStore.Get(0).gripType);
            Button("RoomSetReady").onClick.Invoke(); yield return Until(() => !session.Busy && session.Room.BothReady, "Ready after customize failed.");
            Button("RoomStart").onClick.Invoke();
            yield return Until(() => SceneManager.GetActiveScene().name == GameFlow.BattleScene, "Online battle failed."); yield return Ready();
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.IsNotNull(boot.Battle); Assert.AreEqual("木", boot.Battle.Left.Loadout.grapheme);
            Assert.AreEqual(BattleStageId.EraserFactory,boot.Battle.Director.Config.stage);
            Assert.AreEqual("史", boot.Battle.Right.Loadout.grapheme);
            Assert.IsFalse(Button("BattlePause").interactable); Assert.IsFalse(Button("BattleAgain").interactable);
            yield return new WaitForSecondsRealtime(4);
            Button("BattleSettings").onClick.Invoke(); yield return null; yield return null;
            Assert.IsTrue(SettingsScreen.IsOpen); Assert.IsFalse(TimeController.Paused, "Online settings must leave the host simulation running.");
            float beforeSettings = boot.Battle.Director.Clock;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.Greater(boot.Battle.Director.Clock, beforeSettings);
            Assert.AreEqual("オンライン対戦は進行中です", GameObject.Find("SettingsSaveHint").GetComponent<Text>().text);
            Button("SettingsDone").onClick.Invoke(); yield return null; yield return null;
            Assert.IsFalse(SettingsScreen.IsOpen); Assert.IsFalse(TimeController.Paused);
            yield return guest.Poll(); Assert.IsNull(guest.Error); Assert.AreEqual("battle", guest.State.stage);
            Assert.AreEqual(boot.CurrentSeed, guest.State.seed);
            Assert.IsNotNull(guest.State.frame); Assert.Greater(guest.State.frame.sequence, 5); Assert.Greater(guest.State.frame.clock, 0);
            Assert.AreEqual(2, guest.State.frame.actors.Length);
            MenuUiTests.Capture("friend_battle_host", 1230, 690);
            session.BackToLobby(); yield return Until(() => SceneManager.GetActiveScene().name == GameFlow.FriendRoomScene && !session.Busy, "Return to lobby failed."); yield return Ready();
            Assert.IsFalse(session.Room.BothReady); Assert.IsNull(session.Room.frame);
            yield return guest.Leave(); yield return Until(() => !session.Room.players[1].joined, "Guest exit failed.");
            session.Leave(); yield return Until(() => !session.Connected && !session.Busy, "Host exit failed.");
            Assert.IsTrue(Button("CreateRoom").interactable);
        }
        [UnityTest]
        public IEnumerator AuthoritativeFrames_ReplayPoseHpProjectilesEventsAndFinalResultWithoutGuestPhysics()
        {
            var config = new MatchConfig { left = FighterBuildData.Default("力").ToLoadout(), right = FighterBuildData.Default("史").ToLoadout(), seed = 812, durationSeconds = 60 };
            var host = BattleBuilder.Build(config, CombatBalance.Default, GlyphCalibration.Default, false, 0, out var error);
            Assert.IsNull(error); host.Director.enabled = false; host.Director.SilentSimulation = true;
            var guest = BattleBuilder.Build(config, CombatBalance.Default, GlyphCalibration.Default, false, 0, out error);
            Assert.IsNull(error); guest.Director.enabled = false;
            try
            {
                var sync = guest.Root.AddComponent<FriendBattleSync>(); sync.BindPlayback(guest.Director);
                host.Context.Interventions = new BattleIntervention(host.Director, InterventionController.Local, InterventionController.Remote);
                guest.Context.Interventions = new BattleIntervention(guest.Director, InterventionController.Remote, InterventionController.Local);
                for (int i = 0; i < 200; i++) host.Director.SimulateStep();
                int hits = 0, specials = 0; guest.Director.Events.Hit += _ => hits++;
                guest.Director.Events.Intervention += e => { if (e.kind == InterventionKind.Special && e.style == AttackStyle.Whirl) specials++; };
                var hostSide = host.Context.Interventions.Side(1);
                hostSide.gauge = .65f; hostSide.cheerUntil = host.Context.SimTime + 2; hostSide.cheerReadyAt = host.Context.SimTime + 9;
                host.Context.Interventions.Side(0).chainActive = true;
                var hit = new HitEvent { attacker = 0, defender = 1, damage = 5, point = Vector2.one, style = AttackStyle.Thrust };
                var special = new InterventionEvent { fighter = 0, kind = InterventionKind.Special, success = true, style = AttackStyle.Whirl };
                var frame = FriendBattleFrame.Capture(host.Director, 10, new[] { new FriendEventFrame { sequence = 1, kind = "hit", data = JsonUtility.ToJson(hit) },
                    new FriendEventFrame { sequence = 2, kind = "intervene", data = JsonUtility.ToJson(special) } });
                frame.shots = new[] { new FriendShotFrame { owner = 0, id = 55, style = AttackStyle.StrokeShot, position = Vector2.one, size = Vector2.one, velocity = 9 } };
                // Real wire roundtrip must retain nested actor and event data.
                frame = JsonUtility.FromJson<FriendBattleFrame>(JsonUtility.ToJson(frame));
                sync.ReceiveFrame(frame); yield return new WaitForSecondsRealtime(.2f);
                Assert.AreEqual(host.Director.Clock, guest.Director.Clock);
                Assert.AreEqual(host.Left.Runtime.hp, guest.Left.Runtime.hp); Assert.AreEqual(host.Right.Runtime.hp, guest.Right.Runtime.hp);
                Assert.Less(Vector2.Distance(host.Left.Body.position, guest.Left.Body.position), .0001f);
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(host.Right.WeaponBody.rotation, guest.Right.WeaponBody.rotation)), .001f);
                Assert.AreEqual(1, hits); sync.ReceiveFrame(frame); Assert.AreEqual(1, hits);
                // The guest's own gauge, cheer and cooldown follow the host's simulation.
                var guestSide = guest.Context.Interventions.Side(1);
                Assert.AreEqual(.65f, guestSide.gauge, 1e-4f);
                Assert.AreEqual(2f, guestSide.cheerUntil - guest.Context.SimTime, .01f);
                Assert.AreEqual(9f, guestSide.cheerReadyAt - guest.Context.SimTime, .01f);
                Assert.IsTrue(guest.Context.Interventions.InSpecial(0), "必殺技の最中はゲストも演出する");
                Assert.AreEqual(1, specials);
                Assert.AreEqual(1, guest.Context.Ranged.Projectiles.Count);
                Assert.IsFalse(guest.Left.Body.simulated); Assert.IsFalse(guest.Right.WeaponBody.simulated);
                int steps = guest.Director.StepCount; guest.Director.SimulateStep(); Assert.AreEqual(steps, guest.Director.StepCount);
                var ko = JsonUtility.FromJson<FriendBattleFrame>(JsonUtility.ToJson(frame)); ko.sequence = 11; ko.ko = true;
                sync.ReceiveFrame(ko); guest.Director.enabled = true; yield return new WaitForSecondsRealtime(.2f);
                Assert.IsFalse(guest.Director.Ended, "Guest must wait for the host's final result during KO presentation.");
                for (int i = 0; i < 10000 && !host.Director.Ended; i++) host.Director.SimulateStep();
                Assert.IsTrue(host.Director.Ended);
                int ends = 0; guest.Director.MatchEnded += _ => ends++;
                var final = FriendBattleFrame.Capture(host.Director, 20, Array.Empty<FriendEventFrame>());
                final = JsonUtility.FromJson<FriendBattleFrame>(JsonUtility.ToJson(final));
                sync.ReceiveFrame(final); yield return new WaitForSecondsRealtime(.2f);
                Assert.IsTrue(guest.Director.Ended); Assert.AreEqual(host.Director.Result.winner, guest.Director.Result.winner);
                Assert.AreEqual(host.Director.Result.hpRemaining, guest.Director.Result.hpRemaining);
                sync.ReceiveFrame(frame); sync.ReceiveFrame(final); Assert.AreEqual(1, ends);
            }
            finally { host.Destroy(); guest.Destroy(); }
        }
        [UnityTest]
        public IEnumerator InvalidCodeAndMissingRoom_AreRecoverableAndKeepConnectControlsUsable()
        {
            GameFlow.ToFriendRoom(); yield return Ready();
            var input = GameObject.Find("RoomCodeInput").GetComponent<InputField>(); input.text = "000000";
            Assert.IsFalse(Button("JoinRoom").interactable);
            var session = FriendRoomSession.Instance; session.Connect(false, "ABC");
            Assert.IsFalse(session.Busy); Assert.IsFalse(session.Connected);
            input.text = "ZZZZZZ"; Button("JoinRoom").onClick.Invoke();
            yield return Until(() => !session.Busy, "Join failure should finish.");
            Assert.IsFalse(session.Connected); Assert.IsNotEmpty(session.Message);
            Assert.IsTrue(Button("JoinRoom").interactable); Assert.IsTrue(Button("CreateRoom").interactable);
        }
        [UnityTest]
        public IEnumerator GuestUi_ReceivesAuthoritativeBattleAndResult_AndHandlesHostExit()
        {
            var hostClient = new FriendRoomClient("http://127.0.0.1:8787");
            var hostBuild = FighterBuildData.Default("史"); hostBuild.fontType = FontStyleId.Mincho;
            yield return hostClient.Create(hostBuild); Assert.IsNull(hostClient.Error);
            GameFlow.ToFriendRoom(); yield return Ready();
            GameObject.Find("RoomCodeInput").GetComponent<InputField>().text = hostClient.Code;
            Button("JoinRoom").onClick.Invoke();
            var session = FriendRoomSession.Instance;
            yield return Until(() => session.Connected && !session.Busy, "Guest UI join failed.");
            Assert.IsFalse(session.IsHost); Assert.AreEqual(1, session.Room.side);
            Button("RoomSetReady").onClick.Invoke(); yield return Until(() => !session.Busy && session.Room.players[1].ready, "Guest ready failed.");
            yield return hostClient.SetReady(true);
            yield return Until(() => session.Room.BothReady, "Guest must see both ready.");
            yield return Ready(); MenuUiTests.Capture("friend_room_guest", 1230, 690);
            Assert.IsFalse(Button("RoomStart").gameObject.activeSelf);
            yield return hostClient.StartBattle(); Assert.IsNull(hostClient.Error);
            yield return Until(() => SceneManager.GetActiveScene().name == GameFlow.BattleScene, "Guest must enter the shared battle automatically.");
            yield return Ready();
            var guestBoot = Object.FindAnyObjectByType<BattleBootstrap>(); var guest = guestBoot.Battle;
            Assert.IsTrue(guest.Director.RemotePlayback); Assert.IsFalse(guest.Left.Body.simulated);
            Assert.IsFalse(guest.Director.Ended, "Waiting for the first frame must not manufacture a result.");
            Assert.AreEqual(hostClient.State.seed, guestBoot.CurrentSeed);
            var host = BattleBuilder.Build(guest.Director.Config, CombatBalance.Default, GlyphCalibration.Default, false, 0, out var error);
            Assert.IsNull(error); host.Director.enabled = false; host.Director.SilentSimulation = true;
            foreach (var renderer in host.Root.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            try
            {
                for (int i = 0; i < 250; i++) host.Director.SimulateStep();
                var frame = FriendBattleFrame.Capture(host.Director, 1, Array.Empty<FriendEventFrame>());
                yield return hostClient.SendFrame(hostClient.State.matchId, frame); Assert.IsNull(hostClient.Error);
                yield return Until(() => Mathf.Abs(guest.Director.Clock - frame.clock) < .001f, "Guest must receive host clock over HTTP.");
                yield return new WaitForSecondsRealtime(.2f);
                Assert.AreEqual(frame.actors[0].hp, guest.Left.Runtime.hp); Assert.AreEqual(frame.actors[1].hp, guest.Right.Runtime.hp);
                Assert.Less(Vector2.Distance(frame.actors[0].body, guest.Left.Body.position), .001f);
                // The guest's spectator actions are sent to the host through the room, never simulated locally.
                var hud = guest.Root.GetComponent<InterventionHud>();
                Assert.AreEqual(1, hud.LocalSide);
                Button("CheerButton").onClick.Invoke();
                Assert.AreEqual(-1f, guest.Context.Interventions.Side(1).cheerUntil, .001f, "The guest waits for the host's result.");
                for (int i = 0; i < 40 && (hostClient.State.inputs == null || hostClient.State.inputs.Length == 0); i++)
                { yield return new WaitForSecondsRealtime(.1f); yield return hostClient.Poll(); }
                Assert.AreEqual(1, hostClient.State.inputs.Length); Assert.AreEqual("cheer", hostClient.State.inputs[0].kind);
                MenuUiTests.Capture("friend_battle_guest", 1230, 690);
                for (int i = 0; i < 10000 && !host.Director.Ended; i++) host.Director.SimulateStep();
                Assert.IsTrue(host.Director.Ended);
                var final = FriendBattleFrame.Capture(host.Director, 2, Array.Empty<FriendEventFrame>());
                yield return hostClient.SendFrame(hostClient.State.matchId, final); Assert.IsNull(hostClient.Error);
                yield return Until(() => guest.Director.Ended, "Guest must display the host's final result."); yield return Ready();
                Assert.AreEqual(host.Director.Result.winner, guest.Director.Result.winner);
                Assert.AreEqual(host.Director.Result.hpRemaining, guest.Director.Result.hpRemaining);
                MenuUiTests.Capture("friend_battle_guest_result", 1230, 690);
                Button("Again").onClick.Invoke();
                yield return Until(() => SceneManager.GetActiveScene().name == GameFlow.FriendRoomScene && !session.Busy, "Guest return to lobby failed."); yield return Ready();
                yield return hostClient.Poll(); Assert.AreEqual("lobby", hostClient.State.stage); Assert.IsFalse(hostClient.State.BothReady);
                yield return hostClient.Leave();
                yield return Until(() => session.Lost, "Host exit must invalidate the room for the guest.");
                Assert.IsTrue(Button("CreateRoom").gameObject.activeInHierarchy); Assert.IsNotEmpty(session.Message);
            }
            finally { host.Destroy(); }
        }
        [UnityTest]
        public IEnumerator LeavingDuringConnect_DiscardsLateResponseAndDoesNotRestoreRoom()
        {
            GameFlow.ToFriendRoom(); yield return Ready();
            var session = FriendRoomSession.Instance; session.Connect(true);
            Assert.IsTrue(session.Busy); GameFlow.ToModeSelect();
            yield return new WaitForSecondsRealtime(1);
            Assert.IsFalse(session.Connected); Assert.IsFalse(session.Busy); Assert.IsNull(session.Room);
            Assert.IsFalse(GameFlow.FriendMode);
        }
    }
}
