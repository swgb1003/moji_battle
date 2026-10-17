using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed class TournamentScreen : MonoBehaviour
    {
        TournamentSession session;
        Canvas canvas;
        RectTransform menuRoot;
        GameObject graph;
        Text status, nextOpponent;
        UguiKit.ChoiceButton next;
        BattleInstance computerBattle;
        string error;
        public bool Busy { get; private set; }
        public TournamentSession Session => session;
        static readonly float[] Columns = { 82, 554, 1026, 1498 };
        void Start()
        {
            session = TournamentRuntimeStore.Current;
            if (session == null) { GameFlow.ToModeSelect(); return; }
            TimeController.ResetAll();
            if (Camera.main != null) Camera.main.backgroundColor = RetroUi.Paper;
            menuRoot = RetroUi.Screen("TournamentCanvas", out canvas);
            RetroUi.Header(menuRoot, "03 / TOURNAMENT", GameFlow.ToModeSelect);
            menuRoot.Find("Back").name = "ExitTournament";
            RetroUi.Label(menuRoot, "Title", TournamentSession.CupName, 76, 148, 1360, 126, 70);
            RetroUi.Block(menuRoot, "StatusPlate", 1462, 168, 376,  84, RetroUi.Red);
            status = RetroUi.Label(menuRoot, "TournamentStatus", "", 1470, 168, 360, 84, 36, RetroUi.Paper, TextAnchor.MiddleCenter);
            PlayModePicker.Build(menuRoot, 1240, 268);
            RetroUi.Label(menuRoot, "Rules", "Sサイズ限定・3連勝で優勝 / 大会中は装備固定 / 毎試合HP回復", 76, 277, 1768, 42, 24, RetroUi.Muted);
            string[] headings = { "1回戦", "準決勝", "決勝", "優勝" };
            for (int i = 0; i < 4; i++)
            {
                RetroUi.Block(menuRoot, "RoundBand", Columns[i], 336, 326, 45, RetroUi.Wash);
                RetroUi.Label(menuRoot, "RoundHeading" + i, headings[i], Columns[i], 336, 326, 45, 28, null, TextAnchor.MiddleCenter);
            }
            RetroUi.Block(menuRoot, "FooterRule", 76, 916, 1768, 2, RetroUi.Ink);
            RetroUi.Block(menuRoot, "NextPlate", 82, 950, 250, 76, RetroUi.Ink);
            RetroUi.Label(menuRoot, "NextHeading", "次の対戦", 82, 950, 250, 76, 32, RetroUi.Paper, TextAnchor.MiddleCenter);
            nextOpponent = RetroUi.Label(menuRoot, "NextOpponent", "", 355, 931, 840, 100, 48, null, TextAnchor.MiddleCenter);
            nextOpponent.supportRichText = true;
            next = RetroUi.Button(menuRoot, "NextTournamentMatch", "", 1260, 950, 568, 70, 37, Next, RetroUi.Red);
            Refresh(); StartCoroutine(ResolveComputerMatches());
        }
        public void Next()
        {
            if (Busy) return;
            if (!string.IsNullOrEmpty(error)) { error = null; StartCoroutine(ResolveComputerMatches()); return; }
            if (session.Finished) GameFlow.ToTournamentCustomize();
            else { ScreenAudioDirector.PlayBattleStart(); GameFlow.ToTournamentBattle(); }
        }
        IEnumerator ResolveComputerMatches()
        {
            Busy = true; Refresh();
            while (session.NextComputerMatch != null)
            {
                var match = session.NextComputerMatch;
                var config = session.BeginMatch(match.Id, CombatBalance.Default.matchDuration);
                computerBattle = BattleBuilder.Build(config, CombatBalance.Default, GlyphCalibration.Default, false, 0, out error);
                if (computerBattle == null) { session.CancelStart(match.Id); Debug.LogError("COMの試合を開始できません: " + error); break; }
                ComputerMatchSimulation.Configure(computerBattle);
                while (!computerBattle.Director.Ended)
                {
                    if (SettingsScreen.IsOpen || TimeController.Paused) { yield return null; continue; }
                    ComputerMatchSimulation.Advance(computerBattle.Director);
                    yield return null;
                }
                session.RecordResult(match.Id, computerBattle.Director.Result);
                computerBattle.Destroy(); computerBattle = null; Refresh(); yield return null;
            }
            Busy = false; Refresh();
        }
        public void Refresh()
        {
            if (session == null || canvas == null) return;
            var upcoming = session.NextPlayerMatch;
            status.text = session.Champion ? "優勝！" : session.Eliminated ? $"敗退 / {session.Wins}勝"
                : TournamentSession.RoundName(Mathf.Min(session.Wins, 2)) + $" / {session.Wins}勝";
            next.button.interactable = !Busy && (session.Finished || upcoming != null || error != null);
            next.label.text = Busy ? "結果を集計中…" : error != null ? "集計を再試行 →"
                : session.Finished ? "もう一度挑戦する →" : upcoming != null
                    ? TournamentSession.RoundName(upcoming.Round) + (upcoming.Draws > 0 ? "を再試合 →" : "へ進む →") : "対戦相手を決定中…";
            if (upcoming != null)
            {
                var opponent = session.Entrants[upcoming.LeftId == 0 ? upcoming.RightId : upcoming.LeftId];
                nextOpponent.text = $"<color=#{ColorUtility.ToHtmlStringRGB(RetroUi.Red)}>{session.Entrants[0].Build.character}</color>   VS   <color=#{ColorUtility.ToHtmlStringRGB(RetroUi.Blue)}>{opponent.Build.character}</color>";
                var stage=StageCatalog.Get(StageCatalog.ForSeed(upcoming.Seed,upcoming.Round==2));
                nextOpponent.text+=$"\n<size=22>会場：{stage.Name}</size>";
            }
            else nextOpponent.text = error != null ? "結果を再集計してください"
                : session.ChampionId >= 0 ? "優勝  " + session.Entrants[session.ChampionId].Build.character
                : "対戦相手を決定中";
            DrawBracket(upcoming);
        }
        void DrawBracket(TournamentMatch upcoming)
        {
            if (graph != null) { graph.SetActive(false); Destroy(graph); }
            graph = RetroUi.Group(menuRoot, "Bracket", 0, 0, 1920, 1080).gameObject;
            var root = graph.transform;
            for (int round = 0; round < 3; round++)
            {
                int count = 4 >> round, offset = round == 0 ? 0 : round == 1 ? 4 : 6;
                for (int i = 0; i < count; i++)
                {
                    var match = session.Matches[offset + i];
                    float a = Center(round, i * 2), b = Center(round, i * 2 + 1), mid = (a + b) * .5f;
                    float branch = Columns[round] + 400, end = Columns[round] + 326;
                    Color WinnerColor(int id) => match.Complete && match.WinnerId == id ? id == 0 ? RetroUi.Red : RetroUi.Blue : RetroUi.Rule;
                    RetroUi.Line(root, new Vector2(end, a), new Vector2(branch, a), WinnerColor(match.LeftId));
                    RetroUi.Line(root, new Vector2(end, b), new Vector2(branch, b), WinnerColor(match.RightId));
                    RetroUi.Line(root, new Vector2(branch, a), new Vector2(branch, b), match.Complete ? WinnerColor(match.WinnerId) : RetroUi.Rule);
                    RetroUi.Line(root, new Vector2(branch, mid), new Vector2(Columns[round + 1], mid), match.Complete ? WinnerColor(match.WinnerId) : RetroUi.Rule);
                }
            }
            for (int stage = 0; stage < 4; stage++)
            {
                for (int i = 0; i < (8 >> stage); i++)
                {
                    int entrant = stage == 0 ? i : session.Matches[stage == 1 ? i : stage == 2 ? 4 + i : 6].WinnerId;
                    bool player = entrant == 0;
                    bool opponent = upcoming != null && entrant >= 0 && entrant != 0 && (upcoming.LeftId == entrant || upcoming.RightId == entrant);
                    Color accent = player ? RetroUi.Red : opponent ? RetroUi.Blue : RetroUi.Ink;
                    float y = Center(stage, i) - 27;
                    var tile = RetroUi.Group(root, $"Slot_{stage}_{i}", Columns[stage], y, 326, 54);
                    RetroUi.Block(tile, "CardPaper", 0, 0, 326, 54, RetroUi.Paper);
                    if (entrant >= 0)
                    {
                        RetroUi.Block(tile, "NumberPlate", 0, 0, 44, 54, player ? accent : RetroUi.Wash);
                        RetroUi.Label(tile, "Number", (entrant + 1).ToString(), 0, 0, 44, 54, 23, player ? RetroUi.Paper : RetroUi.Ink, TextAnchor.MiddleCenter);
                        RetroUi.Label(tile, "Glyph", session.Entrants[entrant].Build.character, 54, -2, 88, 58,  40, accent, align: TextAnchor.MiddleCenter);
                        RetroUi.Label(tile, "Name", session.Entrants[entrant].Name, 152, 1, 162, 26, 18, accent, TextAnchor.MiddleRight);
                        RetroUi.Label(tile, "Build", Note(stage, i, entrant), 148, 28, 166, 23, 15, RetroUi.Muted, TextAnchor.MiddleRight);
                    }
                    else RetroUi.Label(tile, "Waiting", stage == 3 ? "?" : "—", 0, 0, 326, 54, stage == 3 ? 46 : 28, RetroUi.Rule, TextAnchor.MiddleCenter);
                    // Keep all four edges visible at smaller window sizes and above card contents.
                    RetroUi.Frame(tile, 0, 0, 326, 54, entrant < 0 ? RetroUi.Rule : accent, player || opponent ? 3 : 2.5f);
                    if (stage == 3)
                    {
                        RetroUi.Cup(root, Columns[3] + 106, y - 144, .95f, entrant == 0 ? RetroUi.Red : RetroUi.Ink);
                        RetroUi.Label(root, "ChampionCaption", entrant < 0 ? "3連勝で、ここへ。" : "CHAMPION", Columns[3], y + 86, 326, 50, 24, RetroUi.Muted, TextAnchor.MiddleCenter);
                    }
                }
            }
            RetroUi.Enter(graph, .04f, 8);
            next.image.transform.SetAsLastSibling();
        }
        string Note(int stage, int slot, int entrant)
        {
            var build = session.Entrants[entrant].Build;
            string outcome = "";
            if (stage < 3)
            {
                var match = session.Matches[stage == 0 ? slot / 2 : stage == 1 ? 4 + slot / 2 : 6];
                if (match.Complete) outcome = match.WinnerId == entrant ? "勝 / " : "敗 / ";
            }
            return outcome + CustomizeLabels.Grip(build) + " / " + CustomizeLabels.StyleShort(build.battleStyle);
        }
        static float Center(int stage, int slot) => 424 + 62 * ((1 << stage) * slot + ((1 << stage) - 1) * .5f);
        void Update() { if (Input.GetKeyDown(KeyCode.Escape)) GameFlow.ToModeSelect(); }
        void OnDestroy() { if (computerBattle != null) { computerBattle.Destroy(); computerBattle = null; } }
    }
}
