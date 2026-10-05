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

        BattleInstance battle;
        string error;
        GameObject resultPanel;
        public BattleInstance Battle => battle;
        public int CurrentSeed { get; private set; }

        void Start()
        {
            StartMatch(seed != 0 ? seed : NewSeed());
        }

        static int NewSeed() => (int)(System.DateTime.Now.Ticks & 0x7FFFFFFF) | 1;

        public void StartMatch(int matchSeed)
        {
            battle?.Destroy();
            TimeController.ResetAll();
            CurrentSeed = matchSeed;
            var b = balance != null ? balance : CombatBalance.Default;
            var c = calibration != null ? calibration : GlyphCalibration.Default;
            bool custom = BuildRuntimeStore.Active;
            var config = new MatchConfig
            {
                left = custom ? BuildRuntimeStore.Get(0).ToLoadout() : new FighterLoadout(leftGrapheme, leftFont),
                right = custom ? BuildRuntimeStore.Get(1).ToLoadout() : new FighterLoadout(rightGrapheme, rightFont),
                seed = matchSeed,
                durationSeconds = b.matchDuration,
            };
            battle = BattleBuilder.Build(config, b, c, true, useCountdown ? b.countdownSeconds : 0f, out error);
            if (resultPanel != null) resultPanel.SetActive(false);
            if (battle == null) Debug.LogError("試合を開始できません: " + error);
            else battle.Director.MatchEnded += r =>
            {
                Debug.Log($"[MojiBattle] seed={r.seed} {r.finishReason} winner={r.winner} t={r.elapsedSeconds:F1}s HP {r.hpRemaining[0]:F0}/{r.hpRemaining[1]:F0}");
                ShowResultButtons();
            };
        }

        /// <summary>リザルトの操作: 同じ設定でもう一度（新しい seed）/ カスタマイズへ戻る。</summary>
        void ShowResultButtons()
        {
            if (resultPanel == null)
            {
                var canvas = UguiKit.MakeCanvas("ResultCanvas", 10);
                resultPanel = canvas.gameObject;
                var again = UguiKit.Choice(canvas.transform, "Again", "同じ設定でもう一度", 960f - 470f, 640f, 440f, 96f, 36, RematchSameBuild);
                again.image.color = FighterFactory.Ink;
                again.label.color = Color.white;
                UguiKit.Choice(canvas.transform, "ToCustomize", "カスタマイズへ戻る", 960f + 30f, 640f, 440f, 96f, 36, GameFlow.ToCustomize);
            }
            resultPanel.SetActive(true);
        }

        /// <summary>同じ設定（BuildRuntimeStore の内容をそのまま）で新しい seed の試合。</summary>
        public void RematchSameBuild() => StartMatch(NewSeed());

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) TimeController.SetPaused(!TimeController.Paused);
            if (Input.GetKeyDown(KeyCode.Alpha1)) TimeController.SetSpectatorSpeed(0.5f);
            if (Input.GetKeyDown(KeyCode.Alpha2)) TimeController.SetSpectatorSpeed(1f);
            if (Input.GetKeyDown(KeyCode.Alpha3)) TimeController.SetSpectatorSpeed(2f);
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

        void OnDestroy() => TimeController.ResetAll();
    }
}
