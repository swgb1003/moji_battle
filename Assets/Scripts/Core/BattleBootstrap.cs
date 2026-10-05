using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// Battle シーンの起点（P0/P1 戦闘検証版）。文字入力・字体選択 UI は P4 で接続する。
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
            var config = new MatchConfig
            {
                left = new FighterLoadout(leftGrapheme, leftFont),
                right = new FighterLoadout(rightGrapheme, rightFont),
                seed = matchSeed,
                durationSeconds = b.matchDuration,
            };
            battle = BattleBuilder.Build(config, b, c, true, useCountdown ? b.countdownSeconds : 0f, out error);
            if (battle == null) Debug.LogError("試合を開始できません: " + error);
            else battle.Director.MatchEnded += r =>
                Debug.Log($"[MojiBattle] seed={r.seed} {r.finishReason} winner={r.winner} t={r.elapsedSeconds:F1}s HP {r.hpRemaining[0]:F0}/{r.hpRemaining[1]:F0}");
        }

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
            if (Input.GetKeyDown(KeyCode.Z)) CycleGlyph(ref leftGrapheme, leftFont, -1);
            if (Input.GetKeyDown(KeyCode.X)) CycleGlyph(ref leftGrapheme, leftFont, +1);
            if (Input.GetKeyDown(KeyCode.N)) CycleGlyph(ref rightGrapheme, rightFont, -1);
            if (Input.GetKeyDown(KeyCode.M)) CycleGlyph(ref rightGrapheme, rightFont, +1);
        }

        void CycleGlyph(ref string grapheme, FontStyleId font, int step)
        {
            var list = GlyphCatalog.AvailableGraphemes(font);
            if (list.Count == 0) return;
            const string order = "一口山火鬱AIOX";
            list.Sort((a, b) => order.IndexOf(a, System.StringComparison.Ordinal).CompareTo(order.IndexOf(b, System.StringComparison.Ordinal)));
            int i = list.IndexOf(grapheme);
            grapheme = list[((i < 0 ? 0 : i + step) % list.Count + list.Count) % list.Count];
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
