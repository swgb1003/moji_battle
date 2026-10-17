using System.Collections;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// Battle シーンの起点。カスタマイズ画面から来た時（BuildRuntimeStore.Active）はその設定で戦い、
    /// Battle シーン単体の再生は P1/P2 の検証どおり（カスタマイズ無し）で戦う。
    /// リザルト: 同じ設定でもう一度 / カスタマイズへ戻る（カスタマイズ仕様 3・21）。
    /// 観戦操作: Space=一時停止 / 1,2,3=0.5×,1×,2× / R=新しいseedで再戦 / T=同じseedで再戦 / C=Collider表示 / D=AI表示
    /// 文字の切り替え（P2 確認用）: Z/X=左の文字 前/次, N/M=右の文字 前/次（ベイク済みの文字を順に回して再戦）
    /// </summary>
    public sealed class BattleBootstrap : MonoBehaviour
    {
        public CombatBalance balance;
        public GlyphCalibration calibration;
        public string leftGrapheme = "一";
        public string rightGrapheme = "鬱";
        public FontStyleId leftFont = FontStyleId.Gothic;
        public FontStyleId rightFont = FontStyleId.Gothic;
        [Tooltip("0 ならランダム")]
        public int seed;
        public bool useCountdown = true;
        [Tooltip("観戦者の関与（必殺・声援・踏ん張り）。無効なら完全オートで観戦だけ")]
        public bool interventions = true;

        BattleInstance battle;
        string error;
        GameObject resultPanel;
        TournamentSession tournament;
        int tournamentMatchId = -1;
        int worldCupMatchId=-1;
        CombatBalance worldCupBalance;
        public bool IsTournament => tournament != null;
        public BattleInstance Battle => battle;
        public int CurrentSeed { get; private set; }

        void Start()
        {
            if(GameFlow.WorldCupMode)
            {
                var s=WorldCupRuntimeStore.Current;var m=s?.Match(s.SelectedMatch);
                if(m==null || !m.Ready){GameFlow.BackFromWorldCupBattle();return;}
                worldCupMatchId=m.Id;
                BuildMatch(s.BeginMatch(m.Id,(balance!=null?balance:CombatBalance.Default).matchDuration));
                if(battle==null)s.CancelStart(m.Id);
                else battle.Root.GetComponent<BattleHudPresenter>().SetWorldCupInfo(s,m);
                if(battle!=null && m.Group>=0)gameObject.AddComponent<WorldCupLeagueBackground>();
                return;
            }
            if (GameFlow.FriendMode && FriendRoomSession.Instance?.Connected == true)
            {
                var room = FriendRoomSession.Instance.Room;
                BuildMatch(new MatchConfig { left = room.players[0].build.ToLoadout(), right = room.players[1].build.ToLoadout(),
                    seed = room.seed, stage=(BattleStageId)Mathf.Clamp(room.arena,0,5), durationSeconds = (balance != null ? balance : CombatBalance.Default).matchDuration });
                if (battle != null)
                {
                    // 自分の陣営はこの端末、相手の陣営はオンラインの友人が操作する
                    AttachInterventions(room.side == 0 ? InterventionController.Local : InterventionController.Remote,
                        room.side == 1 ? InterventionController.Local : InterventionController.Remote);
                    battle.Root.AddComponent<FriendBattleSync>().Bind(battle.Director);
                    battle.Root.GetComponent<BattleHudPresenter>().SetFriendInfo(room.side);
                    battle.Root.AddComponent<AchievementMatchRecorder>().Bind(battle.Director, room.side, true);
                }
                return;
            }
            if (GameFlow.TournamentMode && TournamentRuntimeStore.Current != null)
            {
                tournament = TournamentRuntimeStore.Current;
                var match = tournament.NextPlayerMatch;
                if (match == null) { GameFlow.ToTournamentBoard(); return; }
                tournamentMatchId = match.Id;
                var config = tournament.BeginMatch(match.Id, (balance != null ? balance : CombatBalance.Default).matchDuration);
                BuildMatch(config);
                if (battle == null) tournament.CancelStart(match.Id);
                else battle.Root.GetComponent<BattleHudPresenter>().SetTournamentInfo(tournament, match);
                return;
            }
            StartMatch(seed != 0 ? seed : NewSeed());
        }

        static int NewSeed() => (int)(System.DateTime.Now.Ticks & 0x7FFFFFFF) | 1;

        public void StartMatch(int matchSeed)
        {
            if(GameFlow.WorldCupMode)return;
            if(GameFlow.RunMode)
            {
                if(RunRuntimeStore.CanBattle)BuildMatch(RunRuntimeStore.BeginMatch((balance!=null?balance:CombatBalance.Default).matchDuration));
                return;
            }
            if(GameFlow.DraftMode)
            {
                if(DraftRuntimeStore.CanBattle) BuildMatch(DraftRuntimeStore.BeginMatch(matchSeed,(balance!=null?balance:CombatBalance.Default).matchDuration));
                return;
            }
            if (IsTournament || GameFlow.FriendMode) return; // Online matches start only after both players ready in their room.
            var b = balance != null ? balance : CombatBalance.Default;
            bool custom = BuildRuntimeStore.Active;
            if(GameFlow.ChallengeMode)
            {
                BuildMatch(ChallengeRuntimeStore.BeginMatch(matchSeed,b.matchDuration));
                return;
            }
            var config = new MatchConfig
            {
                left = custom ? BuildRuntimeStore.Get(0).ToLoadout() : new FighterLoadout(leftGrapheme, leftFont),
                right = custom ? BuildRuntimeStore.Get(1).ToLoadout() : new FighterLoadout(rightGrapheme, rightFont),
                seed = matchSeed,
                stage = GameFlow.PracticeMode?BattleStageId.Dojo:StageSession.Selected,
                durationSeconds = b.matchDuration,
            };
            BuildMatch(config);
        }

        void BuildMatch(MatchConfig config)
        {
            battle?.Destroy();
            TimeController.ResetAll();
            if (!GameFlow.FriendMode) TimeController.SetSpectatorSpeed(GameSettings.InitialSpeed);
            CurrentSeed = config.seed;
            var b = balance != null ? balance : CombatBalance.Default;
            var c = calibration != null ? calibration : GlyphCalibration.Default;
            if(GameFlow.WorldCupMode && WorldCupRuntimeStore.Current.Match(worldCupMatchId).Group>=0)
            {
                worldCupBalance=Instantiate(b);worldCupBalance.suddenDeath=false;b=worldCupBalance;
            }
            bool tournamentFinal = IsTournament && tournament.Matches[tournamentMatchId].Round == 2
                || GameFlow.WorldCupMode && WorldCupRuntimeStore.Current.Match(worldCupMatchId)?.Group<0 && WorldCupRuntimeStore.Current.Match(worldCupMatchId).Round==4 && !WorldCupRuntimeStore.Current.Match(worldCupMatchId).Bronze;
            battle = BattleBuilder.Build(config, b, c, true, useCountdown ? b.countdownSeconds : 0f, out error, tournamentFinal);
            if(battle!=null && GameFlow.RunMode)
            { RunRuntimeStore.ApplyCombat(battle.Left,true);RunRuntimeStore.ApplyCombat(battle.Right,false); }
            // 自操作（練習は常に自操作）: 必殺・声援の操作表示を合わせるため、関与を付ける前に側を決める
            if (battle != null && (GameFlow.PracticeMode || PlayControl.SelfControl)) battle.Context.PlayerSide = PlayerSide();
            if (resultPanel != null) resultPanel.SetActive(false);
            if (battle == null) Debug.LogError("試合を開始できません: " + error);
            // 友人対戦は部屋の情報が揃ってから Start で付ける。それ以外は左（P1）を観戦者、右を COM が操作する
            else if (!GameFlow.FriendMode)
            {
                var m=GameFlow.WorldCupMode?WorldCupRuntimeStore.Current.Match(worldCupMatchId):null;
                if(m!=null)AttachInterventions(m.LeftId==0?InterventionController.Local:InterventionController.Com,m.RightId==0?InterventionController.Local:InterventionController.Com);
                else if(IsTournament && battle.Context.PlayerSide==1)AttachInterventions(InterventionController.Com,InterventionController.Local);
                else AttachInterventions(InterventionController.Local,InterventionController.Com);
            }
            if (battle != null && battle.Context.PlayerSide >= 0) SetupSelfControl();
            if(battle!=null && GameFlow.ChallengeMode)battle.Root.GetComponent<BattleHudPresenter>().SetChallengeInfo();
            if(battle!=null && GameFlow.DraftMode)battle.Root.GetComponent<BattleHudPresenter>().SetDraftInfo();
            if(battle!=null && GameFlow.RunMode)
            { RunRuntimeStore.ApplyInterventions(battle.Context.Interventions);battle.Root.GetComponent<BattleHudPresenter>().SetRunInfo(); }
            if (battle != null && !GameFlow.FriendMode)
            {
                int side = GameFlow.WorldCupMode ? WorldCupRuntimeStore.Current.Match(worldCupMatchId).LeftId==0?0:1 : IsTournament && tournament.Matches[tournamentMatchId].LeftId != 0 ? 1 : 0;
                if(!GameFlow.WorldCupMode || WorldCupRuntimeStore.Current.Match(worldCupMatchId).IncludesPlayer)battle.Root.AddComponent<AchievementMatchRecorder>().Bind(battle.Director, side, false, tournament,
                    IsTournament ? tournament.Matches[tournamentMatchId].Round : -1);
            }
            if (battle != null) battle.Director.MatchEnded += r =>
            {
                Debug.Log($"[MojiBattle] seed={r.seed} {r.finishReason} winner={r.winner} t={r.elapsedSeconds:F1}s HP {r.hpRemaining[0]:F0}/{r.hpRemaining[1]:F0}");
                if(GameFlow.WorldCupMode)
                {
                    if(WorldCupRuntimeStore.Record(worldCupMatchId,r,true,SelfHard))StartCoroutine(ReturnToWorldCup());
                }
                else if (IsTournament)
                {
                    if (tournament.RecordResult(tournamentMatchId, r)) StartCoroutine(ReturnToTournament());
                }
                else if(GameFlow.RunMode)
                {
                    if(!RunRuntimeStore.Record(r))return;
                    if(resultPanel!=null){resultPanel.SetActive(false);Destroy(resultPanel);}
                    resultPanel=new GameObject("RunResult");resultPanel.transform.SetParent(transform,false);
                    resultPanel.AddComponent<RunResultScreen>().Build();
                }
                else if(GameFlow.DraftMode)
                {
                    if(!DraftRuntimeStore.Record(r))return;
                    if(resultPanel!=null){resultPanel.SetActive(false);Destroy(resultPanel);}
                    resultPanel=new GameObject("DraftResult");resultPanel.transform.SetParent(transform,false);
                    resultPanel.AddComponent<DraftResultScreen>().Build();
                }
                else if(GameFlow.ChallengeMode)
                {
                    var recorder=battle.Root.GetComponent<AchievementMatchRecorder>();
                    var outcome=ChallengeRuntimeStore.Record(r,recorder?.Stats[0]);
                    if(resultPanel!=null){resultPanel.SetActive(false);Destroy(resultPanel);}
                    resultPanel=new GameObject("ChallengeResult"); resultPanel.transform.SetParent(transform,false);
                    resultPanel.AddComponent<ChallengeResultScreen>().Build(outcome);
                }
                else ShowResultButtons();
            };
        }

        /// <summary>この端末の選手の側（-1 は自分の試合ではない）。ワールド文字カップ・大会は対戦表の位置で決まる。</summary>
        int PlayerSide()
        {
            if (GameFlow.FriendMode) return -1;
            if (GameFlow.WorldCupMode)
            {
                var m = WorldCupRuntimeStore.Current.Match(worldCupMatchId);
                return m.LeftId == 0 ? 0 : m.RightId == 0 ? 1 : -1;
            }
            if (IsTournament) return tournament.Matches[tournamentMatchId].LeftId != 0 ? 1 : 0;
            return 0;
        }

        /// <summary>この試合を「moji Fighter」以上の COM と自操作で戦った（「真・mojiFighter」の解放条件）。</summary>
        bool SelfHard => battle != null && battle.Context.PlayerSide >= 0 && PlayControl.Difficulty >= ComDifficulty.MojiFighter;

        /// <summary>自操作: 自分の選手を入力で動かし、相手の COM に強さを当てる。練習は HP が減らず、相手はサンドバッグから。</summary>
        void SetupSelfControl()
        {
            var ctx = battle.Context;
            var me = battle.Director.Fighters[ctx.PlayerSide];
            var player = me.Brain.AttachPlayer();
            battle.Root.AddComponent<PlayerInputReader>().Bind(player.Input);
            TimeController.SetSpectatorSpeed(1f);
            if (GameFlow.PracticeMode)
            {
                ctx.Practice = true;
                foreach (var f in battle.Director.Fighters) f.Runtime.invulnerable = true;
                if (ctx.Interventions != null) ctx.Interventions.Practice = true;
                PracticeSession.Difficulty = ComDifficulty.Sandbag;
            }
            PlayControl.ApplyDifficulty(me.Opponent, GameFlow.PracticeMode ? PracticeSession.Difficulty : PlayControl.Difficulty, ctx.Interventions);
            battle.Root.GetComponent<BattleHudPresenter>().SetSelfControl(player);
            ControlsGuide.OpenOnce();
        }

        /// <summary>練習: 右下の切り替えで相手の強さを変える（サンドバッグなら動かない）。</summary>
        public void SetPracticeDifficulty(ComDifficulty d)
        {
            if (battle == null || !battle.Context.Practice || !PlayControl.Available(d)) return;
            PracticeSession.Difficulty = d;
            PlayControl.ApplyDifficulty(battle.Director.Fighters[1 - battle.Context.PlayerSide], d, battle.Context.Interventions);
        }

        void AttachInterventions(InterventionController left, InterventionController right)
        {
            if (!interventions) return;
            battle.Context.Interventions = new BattleIntervention(battle.Director, left, right);
            battle.Root.AddComponent<InterventionHud>().Bind(battle.Director, battle.CameraRig, battle.Root.GetComponent<BattleHudPresenter>());
        }

        IEnumerator ReturnToTournament()
        {
            // Let the existing victory presentation and ending gong finish before changing scenes.
            var gong = Resources.Load<AudioClip>("Audio/Combat/gong_end");
            yield return new WaitForSecondsRealtime(gong != null ? gong.length + .1f : 1.5f);
            GameFlow.ToTournamentBoard();
        }
        IEnumerator ReturnToWorldCup()
        { yield return new WaitForSecondsRealtime(1.8f);GameFlow.ToWorldCupScreen(); }

        /// <summary>リザルトの操作: 同じ設定でもう一度（新しい seed）/ カスタマイズへ戻る。</summary>
        void ShowResultButtons()
        {
            if(!GameFlow.FriendMode && !GameFlow.PracticeMode)
            {
                if(resultPanel!=null){resultPanel.SetActive(false);Destroy(resultPanel);}
                resultPanel=StageResultScreen.Build(battle.Director,RematchSameBuild);return;
            }
            if (resultPanel == null)
            {
                var canvas = UguiKit.MakeCanvas("ResultCanvas", 10);
                resultPanel = canvas.gameObject;
                var actions = UguiKit.Box(canvas.transform, "ResultActions", 0, 0, 1920, 1080);
                actions.anchorMin = actions.anchorMax = actions.pivot = new Vector2(.5f, .5f);
                actions.anchoredPosition = Vector2.zero;
                canvas.GetComponent<UnityEngine.UI.CanvasScaler>().screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
                RetroUi.Button(actions, "Again", GameFlow.FriendMode ? "ロビーへ戻る →" : "同じ設定でもう一度 →", 490, 684, 440, 76, 28,
                    GameFlow.FriendMode ? (System.Action)(() => FriendRoomSession.Instance.BackToLobby()) : RematchSameBuild, RetroUi.Red);
                RetroUi.Button(actions, "ToCustomize", GameFlow.FriendMode ? "部屋を退出する" : "カスタマイズへ戻る", 990, 684, 440, 76, 28,
                    GameFlow.FriendMode ? (System.Action)(() => FriendRoomSession.Instance.Leave(true)) : GameFlow.ToCustomize);
                RetroUi.Enter(resultPanel, 0, 0);
            }
            resultPanel.SetActive(true);
        }

        /// <summary>同じ設定（BuildRuntimeStore の内容をそのまま）で新しい seed の試合。</summary>
        public void RematchSameBuild() { if(!GameFlow.WorldCupMode && !GameFlow.DraftMode && !GameFlow.RunMode) StartMatch(NewSeed()); }

        bool SelfControlled => battle != null && battle.Context.PlayerSide >= 0;

        void Update()
        {
            if (SettingsScreen.BlocksInput || ControlsGuide.BlocksInput) return;
            if (Input.GetKeyDown(KeyCode.H) && SelfControlled) { ControlsGuide.Open(); return; }
            // 自操作: 矢印・Z・X・C・V・A・S・D は操作に使うので、観戦の速度・確認用のキーは使わない
            if (SelfControlled)
            {
                if (Input.GetKeyDown(KeyCode.Space)) TimeController.SetPaused(!TimeController.Paused);
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.B)) battle.Root.GetComponent<BattleHudPresenter>().GoBack();
                return;
            }
            if (GameFlow.FriendMode)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.B)) FriendRoomSession.Instance.BackToLobby();
                if (Input.GetKeyDown(KeyCode.D)) StickmanView.ShowDebugLabels = !StickmanView.ShowDebugLabels;
                return;
            }
            if (Input.GetKeyDown(KeyCode.Space)) TimeController.SetPaused(!TimeController.Paused);
            if (Input.GetKeyDown(KeyCode.Alpha1)) TimeController.SetSpectatorSpeed(0.5f);
            if (Input.GetKeyDown(KeyCode.Alpha2)) TimeController.SetSpectatorSpeed(1f);
            if (Input.GetKeyDown(KeyCode.Alpha3)) TimeController.SetSpectatorSpeed(2f);
            if(GameFlow.WorldCupMode)
            {
                if(Input.GetKeyDown(KeyCode.Alpha4))TimeController.SetSpectatorSpeed(4f);
                if(Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.B))GameFlow.BackFromWorldCupBattle();
                return;
            }
            if(GameFlow.RunMode)
            {
                if(Input.GetKeyDown(KeyCode.Alpha4))TimeController.SetSpectatorSpeed(4f);
                if(Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.B))GameFlow.BackFromRunBattle();
                return;
            }
            if(GameFlow.DraftMode)
            {
                if(Input.GetKeyDown(KeyCode.Alpha4))TimeController.SetSpectatorSpeed(4f);
                if(Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.B))
                { if(DraftRuntimeStore.BackToBuild())GameFlow.ToDraftBuild();else GameFlow.ToDraftPicks(); }
                return;
            }
            if(GameFlow.ChallengeMode)
            {
                if(Input.GetKeyDown(KeyCode.Alpha4))TimeController.SetSpectatorSpeed(4f);
                if(Input.GetKeyDown(KeyCode.R))RematchSameBuild();
                if(Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.B))GameFlow.ToChallengePrepare();
                return;
            }
            if (IsTournament)
            {
                if (Input.GetKeyDown(KeyCode.C)) ColliderDebugView.Visible = !ColliderDebugView.Visible;
                if (Input.GetKeyDown(KeyCode.D)) StickmanView.ShowDebugLabels = !StickmanView.ShowDebugLabels;
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.B))
                {
                    if (battle != null && battle.Director.Ended) GameFlow.ToTournamentBoard();
                    else GameFlow.ToModeSelect();
                }
                return;
            }
            if (Input.GetKeyDown(KeyCode.R)) StartMatch(NewSeed());
            if (Input.GetKeyDown(KeyCode.T)) StartMatch(CurrentSeed);
            if (Input.GetKeyDown(KeyCode.C)) ColliderDebugView.Visible = !ColliderDebugView.Visible;
            if (Input.GetKeyDown(KeyCode.D)) StickmanView.ShowDebugLabels = !StickmanView.ShowDebugLabels;
            if (Input.GetKeyDown(KeyCode.Z)) CycleGlyph(0, -1);
            if (Input.GetKeyDown(KeyCode.X)) CycleGlyph(0, +1);
            if (Input.GetKeyDown(KeyCode.N)) CycleGlyph(1, -1);
            if (Input.GetKeyDown(KeyCode.M)) CycleGlyph(1, +1);
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.B)) GameFlow.ToCustomize();
        }

        /// <summary>文字の切り替え（確認用）。カスタマイズの試合では、その側の設定の文字だけを替える（他の設定は維持）。</summary>
        void CycleGlyph(int side, int step)
        {
            var list = FighterCustomizer.AvailableCharacters(side == 0 ? leftFont : rightFont);
            if (list.Count == 0) return;
            string Next(string current)
            {
                int i = list.IndexOf(current);
                return list[((i < 0 ? 0 : i + step) % list.Count + list.Count) % list.Count];
            }
            if (BuildRuntimeStore.Active)
            {
                var build = BuildRuntimeStore.Get(side).Clone();
                build.character = Next(build.character);
                BuildRuntimeStore.Set(side, build);
            }
            else if (side == 0) leftGrapheme = Next(leftGrapheme);
            else rightGrapheme = Next(rightGrapheme);
            StartMatch(NewSeed());
        }

        void OnGUI()
        {
            if (battle == null && error != null)
                GUI.Label(new Rect(20, 20, 900, 40), "試合を開始できません: " + error);
        }

        void OnDestroy(){TimeController.ResetAll();if(worldCupBalance!=null)Destroy(worldCupBalance);}
    }
}
