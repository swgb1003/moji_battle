using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>
    /// 自操作の操作説明（全画面）。対戦前画面の「?」・戦闘中の「操作説明」ボタン・H キーで開き、閉じる・Esc・H で閉じる。
    /// 戦闘中に開いている間は試合を止める。初めて自操作で戦う時は試合の前に一度だけ自動で開く。
    /// </summary>
    public sealed class ControlsGuide : MonoBehaviour
    {
        public const string SeenKey = "MojiBattle.ControlsGuideSeen.v1";
        public static ControlsGuide Instance { get; private set; }
        public static bool IsOpen => Instance != null;
        static int closedFrame = -1;
        /// <summary>開いている間と、閉じたフレームは戦闘の操作キーを受け付けない（Esc で閉じた時に戻らない）。</summary>
        public static bool BlocksInput => IsOpen || closedFrame == Time.frameCount;
        bool pausedByGuide;

        /// <summary>行: 見出しは「# 」で始める。それ以外は「キー|説明」。</summary>
        public static readonly string[] Left =
        {
            "# 移動・回避",
            "← →|移動（相手のいる側が「前」）",
            "後ろを素早く 2 回|バックステップ",
            "↑|跳び越え（壁際で相手が近いと壁蹴り）",
            "↓|しゃがみ回避",
            "# 斬る  Z（方向＋Z）",
            "Z|振り下ろし",
            "↑＋Z|斬り上げ",
            "前＋Z|突き",
            "↓＋Z|足払い",
            "後＋Z|横薙ぎ",
            "# 技  X（方向＋X）",
            "X|盾当て",
            "↑＋X|打ち上げ",
            "前＋X|踏込斬り（S・M）／全身ぶん回し（L・XL）",
            "↓＋X|回転斬り",
            "後＋X|引き崩し（くぼみのある文字）",
        };
        public static readonly string[] Right =
        {
            "# 蹴り・遠距離  V・A",
            "V|前蹴り（武器が無くても出せる）",
            "後＋V|フェイント",
            "A|飛び道具（S・M 一画飛ばし／L・XL 衝撃波）",
            "前＋A|跳び斬り（重い文字は不可）",
            "↓＋A|武器を投げる",
            "# 守り",
            "C 長押し|ガード（相手の振りに自動で構える・↓ で下段）",
            "当たる直前に C|受け流し → すぐ Z で反撃",
            "# 必殺・声援",
            "S|必殺技（ゲージ満タンで）",
            "D|声援（相手の必殺中は踏ん張り）",
            "# つなぎ技（その場で Z）",
            "打ち上げが当たったら Z|空中追撃・叩き落とし",
            "しゃがみ中に Z|斬り返し",
            "武器を拾った直後に Z|拾い斬り",
            "倒れている間に V／Z|起き上がり蹴り（後ろで後転）",
            "鍔迫り合い中に Z|崩し反撃",
        };

        public static void Open()
        {
            if (Instance != null) return;
            var root = RetroUi.Screen("ControlsGuideCanvas", out var canvas, 60);
            Instance = canvas.gameObject.AddComponent<ControlsGuide>();
            Instance.Build(root);
            PlayerPrefs.SetInt(SeenKey, 1); PlayerPrefs.Save();
        }

        public static void Close() { if (Instance != null) Destroy(Instance.gameObject); }

        /// <summary>初めての自操作の試合の前に一度だけ開く。</summary>
        public static void OpenOnce() { if (PlayerPrefs.GetInt(SeenKey, 0) == 0) Open(); }

        void Build(RectTransform root)
        {
            RetroUi.Label(root, "ControlsGuideTitle", "操作説明", 80, 60, 600, 80, 56);
            RetroUi.Label(root, "ControlsGuideLead", "方向キーを押しながらボタンで技が変わる。動けない間に押した技は、動けるようになった瞬間に出る。",
                80, 140, 1500, 44, 25, RetroUi.Muted);
            RetroUi.Button(root, "ControlsGuideClose", "閉じる ×", 1600, 66, 220, 64, 30, Close, RetroUi.Ink);
            Column(root, Left, 80, 200);
            Column(root, Right, 990, 200);
            RetroUi.Label(root, "ControlsGuideFoot", "Space：一時停止　　Esc：戻る　　H：この説明を開く・閉じる", 80, 990, 1500, 40, 24, RetroUi.Muted);
            if (FindFirstObjectByType<BattleBootstrap>() != null && !TimeController.Paused)
            { TimeController.SetPaused(true); pausedByGuide = true; }
        }

        static void Column(Transform root, string[] rows, float x, float y)
        {
            foreach (var row in rows)
            {
                if (row.StartsWith("# "))
                {
                    y += 8;
                    RetroUi.Block(root, "GuideHeadingBand", x, y, 850, 40, RetroUi.Ink);
                    RetroUi.Label(root, "GuideHeading", row.Substring(2), x + 14, y, 820, 40, 24, RetroUi.Paper);
                    y += 44;
                    continue;
                }
                var parts = row.Split('|');
                RetroUi.Label(root, "GuideKey", parts[0], x + 6, y, 290, 38, 23, RetroUi.Red);
                RetroUi.Label(root, "GuideText", parts[1], x + 300, y, 550, 38, 22);
                y += 38;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.H)) Close();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            closedFrame = Time.frameCount;
            if (pausedByGuide) TimeController.SetPaused(false);
        }
    }
}
