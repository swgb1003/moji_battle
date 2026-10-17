using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace MojiBattle.Tests
{
    // Records the game screen to PRVideo/footage/<name>.mp4 at 2560x1440.
    // Many menus and effects run on unscaled (real) time, so every frame is paced to exactly 1/fps of
    // real time as well as game time; the footage then plays back at the speed the game really runs.
    [DefaultExecutionOrder(32000)]
    public sealed class PRVideoRecorder : MonoBehaviour
    {
        /// <summary>Output size of the next recording (battles 2560x1440; menus 1920x1080, which keeps them real-time).</summary>
        public static int Width = 1920, Height = 1080;
        /// <summary>Output pixels per UI reference pixel (the UI is laid out for 1920x1080).</summary>
        public static float UiScale => Width / 1920f;
        public static void UseBattleResolution() { Width = 2560; Height = 1440; }
        public static void UseMenuResolution() { Width = 1920; Height = 1080; }
        static PRVideoRecorder current;
        Process ffmpeg; Stream pipe; Thread writer; RenderTexture target; Camera camera;
        readonly BlockingCollection<byte[]> queue = new BlockingCollection<byte[]>(6);
        // Reused frame buffers (more than the queue can hold, so a buffer is never refilled while being written).
        readonly byte[][] pool = new byte[10][]; int poolNext;
        readonly List<string> events = new List<string>();
        readonly Queue<AsyncGPUReadbackRequest> requests = new Queue<AsyncGPUReadbackRequest>();
        readonly List<Canvas> converted = new List<Canvas>();
        readonly HashSet<RenderTexture> sharp = new HashSet<RenderTexture>();
        readonly Stopwatch clock = new Stopwatch();
        string clipName; int frame, overruns; long nextTick; double busy;
        public static int Frame => current != null ? current.frame : 0;
        public static float Seconds => Frame / (float)Fps;
        public static int Fps { get; private set; } = 30;
        public static bool Recording => current != null;
        /// <summary>Battle camera zoom (orthographic size multiplier; 1 = the game's own framing).</summary>
        public static float BattleZoom = 1f;

        public static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../PRVideo"));

        public static void Begin(string name, int fps = 30)
        {
            End();
            Fps = fps; Time.captureFramerate = fps; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            var go = new GameObject("PRVideoRecorder"); DontDestroyOnLoad(go);
            current = go.AddComponent<PRVideoRecorder>(); current.Open(name);
        }
        public static void Mark(string label) { if (current != null) current.events.Add(current.frame + "\t" + label); }
        public static void End()
        {
            if (current == null) return;
            current.Close(); Destroy(current.gameObject); current = null; Time.captureFramerate = 0;
        }

        /// <summary>Position of a world point in the 1920x1080 UI layout of the battle screen.</summary>
        public static Vector2 BattlePixel(Camera arena, Vector2 world)
        {
            var vp = arena.WorldToViewportPoint(world);
            return new Vector2(BattleHudPresenter.ArenaX + vp.x * BattleHudPresenter.ArenaWidth,
                BattleHudPresenter.ArenaY + (1 - vp.y) * BattleHudPresenter.ArenaHeight);
        }

        void Open(string name)
        {
            clipName = name;
            string dir = Path.Combine(Root, "footage"); Directory.CreateDirectory(dir);
            var info = new ProcessStartInfo(Path.Combine(Root, "tools/ffmpeg.exe"),
                $"-y -loglevel error -f rawvideo -pix_fmt rgb24 -s {Width}x{Height} -r {Fps} -i - -vf vflip " +
                $"-c:v libx264 -preset veryfast -crf 12 -pix_fmt yuv420p \"{Path.Combine(dir, name + ".mp4")}\"")
            { UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true };
            ffmpeg = Process.Start(info); pipe = ffmpeg.StandardInput.BaseStream;
            writer = new Thread(() => { foreach (var bytes in queue.GetConsumingEnumerable()) pipe.Write(bytes, 0, bytes.Length); }) { IsBackground = true };
            writer.Start();
            target = new RenderTexture(Width, Height, 24) { antiAliasing = 2 }; target.Create();
            camera = new GameObject("PRVideoCamera").AddComponent<Camera>(); camera.enabled = false;
            camera.transform.SetParent(transform, false);
            Debug.Log($"[MojiBattle] PRVideo {name}: screen {Screen.width}x{Screen.height}");
            clock.Start(); nextTick = 0;
        }

        void Close()
        {
            Drain(true);
            queue.CompleteAdding(); writer.Join(); pipe.Flush(); pipe.Close(); ffmpeg.WaitForExit(); ffmpeg.Dispose();
            foreach (var c in converted) if (c != null) Restore(c);
            RestoreZoom();
            File.WriteAllLines(Path.Combine(Root, "footage", clipName + ".events.tsv"), events);
            Debug.Log($"[MojiBattle] PRVideo {clipName}: {frame} frames ({frame / (float)Fps:0.00}s) busy {busy / Mathf.Max(1, frame):0.0}ms/frame overruns {overruns}");
            target.Release(); Destroy(target);
        }

        void Convert(Canvas c)
        {
            var scaler = c.GetComponent<CanvasScaler>(); if (scaler != null) scaler.enabled = false;
            c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1; c.scaleFactor = UiScale;
            converted.Add(c);
        }
        static void Restore(Canvas c)
        {
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null;
            var scaler = c.GetComponent<CanvasScaler>(); if (scaler != null) scaler.enabled = true;
        }

        /// <summary>
        /// Offscreen views (battle arena, fighter previews) are sized from the window, which is tiny in batch mode.
        /// Give each one a texture at the output resolution of the image that shows it.
        /// </summary>
        void SharpenOffscreenViews()
        {
            var cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var raw in FindObjectsByType<RawImage>(FindObjectsSortMode.None))
            {
                if (!(raw.texture is RenderTexture rt) || sharp.Contains(rt)) continue;
                Camera cam = null;
                foreach (var c in cameras) if (c.targetTexture == rt) { cam = c; break; }
                if (cam == null) continue;
                var r = ((RectTransform)raw.transform).rect;
                int w = Mathf.RoundToInt(r.width * UiScale), h = Mathf.RoundToInt(r.height * UiScale);
                if (w < 32 || h < 32 || (rt.width >= w && rt.height >= h)) { sharp.Add(rt); continue; }
                var hi = new RenderTexture(w, h, 24, rt.format) { antiAliasing = 2, name = rt.name + "_PR" }; hi.Create();
                sharp.Add(hi); float aspect = cam.aspect; cam.targetTexture = hi; cam.aspect = aspect; raw.texture = hi;
            }
        }

        // ---------------------------------------------------------------- battle camera zoom
        CameraRig rig; float rigSize; Vector3 rigPos; bool zoomed; float zx, zxVel;
        void Update()
        {
            // Give the rig back its own framing so its smoothing continues as if nothing changed.
            if (zoomed && rig != null) { rig.Cam.orthographicSize = rigSize; rig.transform.position = rigPos; }
            zoomed = false;
        }
        void RestoreZoom() { if (zoomed && rig != null) { rig.Cam.orthographicSize = rigSize; rig.transform.position = rigPos; } zoomed = false; }
        void ApplyZoom()
        {
            if (BattleZoom >= .999f) return;
            if (rig == null) { rig = FindAnyObjectByType<CameraRig>(); if (rig == null) return; zx = rig.transform.position.x; }
            var cam = rig.Cam; var b = rig.FocusBounds;
            rigSize = cam.orthographicSize; rigPos = rig.transform.position;
            float aspect = Mathf.Max(.1f, cam.aspect);
            float need = Mathf.Max((b.size.x + 1.1f) / (2f * aspect), (b.size.y + .7f) * .5f);
            float size = Mathf.Max(rigSize * BattleZoom, need, 1.7f);
            zx = Mathf.SmoothDamp(zx, b.center.x, ref zxVel, .12f, Mathf.Infinity, Time.unscaledDeltaTime);
            float halfW = size * aspect;
            float x = Mathf.Clamp(zx, b.max.x - halfW + .3f, b.min.x + halfW - .3f);
            var stage = FindAnyObjectByType<BattleBootstrap>()?.Battle?.Director?.Config.stage ?? BattleStageId.Dojo;
            float floor = stage != BattleStageId.Dojo ? .12f : BattleHudPresenter.GroundViewport;
            float y = Mathf.Max(size * (1 - 2 * floor), b.center.y + .15f);
            y = Mathf.Clamp(y, b.max.y - size + .3f, b.min.y + size - .15f);
            // Shake is added in the edit, so the framing here stays steady.
            cam.orthographicSize = size; rig.transform.position = new Vector3(x, y, rigPos.z);
            zoomed = true;
        }

        void LateUpdate()
        {
            var main = Camera.main;
            if (main == null) { Pace(); return; }
            long start = clock.ElapsedTicks;
            ApplyZoom();
            // Scanning the scene is costly; new canvases and views only appear on screen changes.
            if (frame % 6 == 0)
            {
                SharpenOffscreenViews();
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) Convert(c);
            }
            camera.CopyFrom(main); camera.enabled = false; camera.cullingMask = ~(1 << CustomizeScreen.PreviewLayer);
            camera.farClipPlane = 2; camera.targetTexture = target; camera.aspect = (float)Width / Height;
            camera.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            if (main.targetTexture != null) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            requests.Enqueue(AsyncGPUReadback.Request(target, 0, TextureFormat.RGB24));
            Drain(false);
            frame++;
            busy += (clock.ElapsedTicks - start) * 1000.0 / Stopwatch.Frequency;
            Pace();
        }

        // Frames go to ffmpeg strictly in capture order.
        void Drain(bool all)
        {
            while (requests.Count > 0 && (all || requests.Peek().done))
            {
                var request = requests.Dequeue();
                if (all) request.WaitForCompletion();
                if (request.hasError) { Debug.LogError("[MojiBattle] PRVideo readback failed"); continue; }
                var data = request.GetData<byte>();
                var buffer = pool[poolNext] ??= new byte[data.Length]; poolNext = (poolNext + 1) % pool.Length;
                data.CopyTo(buffer); queue.Add(buffer);
            }
        }

        void Pace()
        {
            long period = Stopwatch.Frequency / Fps;
            nextTick += period;
            if (clock.ElapsedTicks > nextTick + period) { overruns++; Mark($"late\t{(clock.ElapsedTicks - nextTick) * 1000.0 / Stopwatch.Frequency:0}ms"); nextTick = clock.ElapsedTicks; return; }
            while (clock.ElapsedTicks < nextTick) { }
        }
    }
}
