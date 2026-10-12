using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>
    /// KO 後、とどめの一撃が決まる瞬間をスローで再生する（表示だけ。物理・勝敗は動かさない）。
    /// 試合中は直近数秒の姿勢を記録し続け、KO のスロー演出の後・結果の表示の前に再生する。
    /// 再生が終わったら KO 後の実際の姿勢へ戻す。クリック・Enter で飛ばせる。
    /// </summary>
    public sealed class KoReplay : MonoBehaviour
    {
        /// <summary>テストなどで止める。</summary>
        public static bool Enabled = true;
        /// <summary>KO の何秒前から、何秒後まで再生するか（試合内の時間）</summary>
        public const float Before = 1.0f, After = .45f;
        /// <summary>再生の速さ（実時間に対する倍率）</summary>
        public const float Speed = .5f;
        const float Keep = 3f;

        struct Snapshot { public float time; public FriendActorFrame left, right; }

        MatchDirector director;
        readonly Queue<Snapshot> buffer = new Queue<Snapshot>();
        Snapshot[] clip;
        FriendActorFrame liveLeft, liveRight;
        float koTime = -1f, playTime, endTime;
        bool finishShown;
        GameObject overlay;
        Text banner;
        Image[] bars;

        public bool Playing { get; private set; }
        /// <summary>この試合で再生する（KO を記録し、演出を省く設定でない）。</summary>
        public bool WillPlay => Enabled && koTime >= 0f && TimeController.PresentationEffectsEnabled && buffer.Count >= 4;
        /// <summary>KO の後、吹き飛ぶところまで記録できた。</summary>
        public bool ReadyToPlay => director.Context.SimTime >= koTime + After;

        public void Bind(MatchDirector d)
        {
            director = d;
            d.Replay = this;
            d.Events.KO += OnKo;
        }

        void OnDestroy()
        {
            if (director != null && director.Context != null) director.Events.KO -= OnKo;
            if (overlay != null) Destroy(overlay);
        }

        void OnKo(KoEvent e) => koTime = e.time;

        /// <summary>物理ステップの直後に呼ぶ（KO 後のスロー中も記録し、吹き飛ぶところまで残す）。</summary>
        public void Record()
        {
            if (Playing || director.Fighters == null) return;
            float now = director.Context.SimTime;
            buffer.Enqueue(new Snapshot { time = now,
                left = FriendActorFrame.CaptureBody(director.Fighters[0]), right = FriendActorFrame.CaptureBody(director.Fighters[1]) });
            while (buffer.Count > 0 && buffer.Peek().time < now - Keep) buffer.Dequeue();
        }

        /// <summary>再生できれば始めて true。記録が足りない・演出を省く設定なら false（そのまま結果へ）。</summary>
        public bool TryBegin()
        {
            if (!Enabled || koTime < 0f || !TimeController.PresentationEffectsEnabled || buffer.Count < 4) return false;
            var all = buffer.ToArray();
            float start = Mathf.Max(all[0].time, koTime - Before);
            endTime = Mathf.Min(all[all.Length - 1].time, koTime + After);
            var list = new List<Snapshot>();
            foreach (var s in all) if (s.time >= start - .05f && s.time <= endTime + .05f) list.Add(s);
            if (list.Count < 4 || endTime - start < .3f) return false;
            clip = list.ToArray();
            playTime = clip[0].time;
            liveLeft = all[all.Length - 1].left; liveRight = all[all.Length - 1].right;
            finishShown = false;
            Playing = true;
            ShowOverlay(true);
            Apply(playTime);
            return true;
        }

        /// <summary>実時間で進める。終わったら true。</summary>
        public bool Tick(float unscaledDt)
        {
            if (!Playing) return true;
            bool skip = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            playTime += unscaledDt * Speed;
            if (!finishShown && playTime >= koTime)
            {
                finishShown = true;
                GetComponent<CombatVfxDirector>()?.ReplayFinish();
            }
            if (skip || playTime >= endTime) { End(); return true; }
            Apply(playTime);
            float fade = Mathf.Clamp01((playTime - clip[0].time) / .15f) * Mathf.Clamp01((endTime - playTime) / .15f);
            if (banner != null) banner.color = new Color(banner.color.r, banner.color.g, banner.color.b, .4f + .6f * fade);
            return false;
        }

        void Apply(float t)
        {
            int i = 0;
            while (i < clip.Length - 2 && clip[i + 1].time < t) i++;
            var a = clip[i]; var b = clip[i + 1];
            float alpha = b.time > a.time ? Mathf.Clamp01((t - a.time) / (b.time - a.time)) : 1f;
            b.left.Apply(director.Fighters[0], a.left, alpha, false);
            b.right.Apply(director.Fighters[1], a.right, alpha, false);
        }

        void End()
        {
            if (!Playing) return;
            Playing = false;
            // KO 後の実際の姿勢へ戻す（結果の画面はこの姿勢の上に出る）
            liveLeft.Apply(director.Fighters[0], null, 1f, false);
            liveRight.Apply(director.Fighters[1], null, 1f, false);
            foreach (var f in director.Fighters) f.View.RemoteCrouch = -1;
            ShowOverlay(false);
        }

        void ShowOverlay(bool on)
        {
            if (on && overlay == null)
            {
                var canvas = UguiKit.MakeCanvas("KoReplayCanvas", 40);
                overlay = canvas.gameObject;
                bars = new[]
                {
                    UguiKit.Panel(canvas.transform, "ReplayBarTop", 0, 0, 1920, 92, new Color(.08f, .07f, .06f, .92f)),
                    UguiKit.Panel(canvas.transform, "ReplayBarBottom", 0, 988, 1920, 92, new Color(.08f, .07f, .06f, .92f)),
                };
                foreach (var bar in bars) bar.raycastTarget = false;
                banner = UguiKit.Label(canvas.transform, "ReplayBanner", "REPLAY  ／  とどめの一撃", 70, 16, 900, 60, 40,
                    new Color(.95f, .92f, .85f, 1f), TextAnchor.MiddleLeft, false);
                banner.font = RetroUi.Font;
                UguiKit.Label(canvas.transform, "ReplaySkip", "クリック / Enter でスキップ", 1250, 1004, 600, 60, 26,
                    new Color(.8f, .77f, .7f, 1f), TextAnchor.MiddleRight, false).font = RetroUi.Font;
            }
            if (overlay != null) overlay.SetActive(on);
        }
    }
}
