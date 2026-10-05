using System.Collections;
using System.IO;
using UnityEngine;

namespace MojiBattle.Tests
{
    /// <summary>PlayMode テスト用: captureDeltaTime で 1フレーム=固定ステップ×速度 として高速に試合を回す。</summary>
    public static class SimHarness
    {
        public static string ReportDir
        {
            get
            {
                var d = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports"));
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static void Begin(bool effects)
        {
            Time.captureDeltaTime = TimeController.BaseFixedDelta;
            Time.maximumDeltaTime = 0.5f;
            TimeController.ResetAll();
            TimeController.PresentationEffectsEnabled = effects;
        }

        public static void End()
        {
            Time.captureDeltaTime = 0f;
            TimeController.ResetAll();
            TimeController.PresentationEffectsEnabled = true;
        }

        public static BattleInstance Build(int seed, bool presentation = false, float countdown = 0f, float duration = 60f,
            string left = "一", string right = "鬱")
        {
            var b = CombatBalance.Default;
            var config = new MatchConfig
            {
                left = new FighterLoadout(left, FontStyleId.Gothic),
                right = new FighterLoadout(right, FontStyleId.Gothic),
                seed = seed,
                durationSeconds = duration,
            };
            var battle = BattleBuilder.Build(config, b, GlyphCalibration.Default, presentation, countdown, out var error);
            if (battle == null) throw new System.Exception(error);
            return battle;
        }

        public static IEnumerator RunToEnd(BattleInstance battle, float speed, int maxFrames = 200000)
        {
            TimeController.SetSpectatorSpeed(speed);
            int frames = 0;
            while (!battle.Director.Ended && frames++ < maxFrames) yield return null;
        }
    }
}
