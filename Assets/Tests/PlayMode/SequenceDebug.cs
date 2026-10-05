using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>調整用: 任意の対戦を一定間隔で撮影する（SEQ_LEFT / SEQ_RIGHT / SEQ_SEED / SEQ_START / SEQ_EVERY / SEQ_COUNT）。</summary>
    [Category("Debug")]
    [Explicit("調整用の診断。-testFilter で明示実行する")]
    public class SequenceDebug
    {
        static string Env(string k, string d) => System.Environment.GetEnvironmentVariable(k) ?? d;

        [UnityTest]
        public IEnumerator CaptureSequence()
        {
            SimHarness.Begin(false);
            string l = Env("SEQ_LEFT", "口"), r = Env("SEQ_RIGHT", "火");
            int seed = int.Parse(Env("SEQ_SEED", "5310"));
            float start = float.Parse(Env("SEQ_START", "3"));
            int every = int.Parse(Env("SEQ_EVERY", "10")), count = int.Parse(Env("SEQ_COUNT", "24"));
            var battle = SimHarness.Build(seed, presentation: true, left: l, right: r);
            var cam = battle.CameraRig.Cam;
            var rt = new RenderTexture(960, 540, 24);
            var dir = Path.Combine(SimHarness.ReportDir, "Seq");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            TimeController.SetSpectatorSpeed(1f);
            int shots = 0, frame = 0;
            while (!battle.Director.Ended && shots < count && frame < 20000)
            {
                yield return null;
                frame++;
                if (battle.Director.Clock < start || frame % every != 0) continue;
                cam.targetTexture = rt;
                cam.aspect = 16f / 9f;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                var L = battle.Left; var R = battle.Right;
                File.WriteAllBytes(Path.Combine(dir, $"s{shots++:000}_t{battle.Director.Clock:F1}_L{L.Runtime.state}_R{R.Runtime.state}.png"), tex.EncodeToPNG());
                Object.Destroy(tex);
            }
            battle.Destroy();
            SimHarness.End();
        }
    }
}
