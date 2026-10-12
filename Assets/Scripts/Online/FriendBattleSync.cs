using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>Host owns all physics and results. Guest interpolates authoritative poses and replays deduplicated presentation events.</summary>
    [DefaultExecutionOrder(-90)]
    public sealed class FriendBattleSync : MonoBehaviour
    {
        MatchDirector director;
        FriendRoomSession session;
        int matchId, frameSequence, lastEvent, lastInput, inputSequence;
        FriendBattleFrame previous, current;
        float receivedAt;
        readonly Queue<FriendEventFrame> history = new Queue<FriendEventFrame>();
        public void Bind(MatchDirector match)
        {
            director = match; session = FriendRoomSession.Instance; matchId = session.Room.matchId;
            if (session.IsHost)
            {
                var e = director.Events;
                e.Hit += v => Record("hit", v); e.Guard += v => Record("guard", v); e.Clash += v => Record("clash", v);
                e.Motion += v => Record("motion", v); e.RangedLaunch += v => Record("ranged", v);
                e.EnvImpact += v => Record("env", v); e.SurfaceContact += v => Record("surface", v);
                e.StateChanged += v => Record("state", v); e.Recovered += v => Record("recover", v);
                e.Evaded += v => Record("evade", v); e.KO += v => Record("ko", v);
                e.Intervention += v => Record("intervene", v);
                StartCoroutine(Publish());
            }
            else
            {
                BindPlayback(match);
                // ゲストの関与の操作はホストへ送り、ホストの試合で判定する
                var iv = director.Context.Interventions;
                if (iv != null) iv.Forward = (side, kind) => StartCoroutine(SendInput(kind));
            }
        }
        public void BindPlayback(MatchDirector match)
        {
            director = match; director.RemotePlayback = true;
            foreach (var f in director.Fighters)
            {
                f.Body.simulated = false; f.WeaponBody.simulated = false; f.Hinge.enabled = false;
                f.Body.interpolation = f.WeaponBody.interpolation = RigidbodyInterpolation2D.None;
            }
        }
        IEnumerator SendInput(InterventionKind kind)
        {
            if (!session.Connected || session.Room.matchId != matchId) yield break;
            yield return session.Client.SendInput(matchId, new FriendInputFrame { sequence = ++inputSequence, kind = FriendInputFrame.Name(kind) });
        }
        /// <summary>ホスト: 部屋の情報に届いたゲストの操作を、まだ反映していないものだけ試合へ入れる。</summary>
        void ApplyGuestInputs()
        {
            var iv = director.Context.Interventions;
            var inputs = session.Room.inputs;
            if (iv == null || inputs == null) return;
            int guest = 1 - session.Room.side;
            foreach (var input in inputs)
            {
                if (input == null || input.sequence <= lastInput) continue;
                lastInput = input.sequence;
                if (FriendInputFrame.TryParse(input.kind, out var kind)) iv.Request(guest, kind, true);
            }
        }
        void Record<T>(string kind, T value)
        {
            history.Enqueue(new FriendEventFrame { sequence = ++lastEvent, kind = kind, data = JsonUtility.ToJson(value) });
            while (history.Count > 128) history.Dequeue();
        }
        IEnumerator Publish()
        {
            while (session.Connected && session.Room.stage == "battle" && session.Room.matchId == matchId)
            {
                var frame = FriendBattleFrame.Capture(director, ++frameSequence, history.ToArray());
                yield return session.Client.SendFrame(matchId, frame);
                yield return new WaitForSecondsRealtime(.08f);
            }
        }
        void Update()
        {
            if (session == null) { RenderPlayback(); return; }
            if (!session.Connected || session.Room.matchId != matchId) return;
            if (session.IsHost) { ApplyGuestInputs(); return; }
            var frame = session.Room.frame;
            ReceiveFrame(frame);
            RenderPlayback();
        }
        public void ReceiveFrame(FriendBattleFrame frame)
        {
            if (frame == null || frame.sequence < 1 || frame.actors?.Length != 2) return;
            if (frame != null && (current == null || frame.sequence > current.sequence))
            {
                previous = current; current = frame; receivedAt = Time.unscaledTime;
                var iv = director.Context.Interventions;
                if (iv != null) iv.RemoteFrameAt = receivedAt;
                director.GetComponent<AchievementMatchRecorder>()?.ApplyRemote(frame.achievements, frame.clock > 0 || frame.ended);
                director.ApplyRemoteClock(frame);
                foreach (var e in frame.events ?? System.Array.Empty<FriendEventFrame>())
                {
                    if (e.sequence <= lastEvent) continue;
                    lastEvent = e.sequence; Replay(e);
                }
                director.Context.Ranged.ApplyRemote(frame.shots);
            }
        }
        void RenderPlayback()
        {
            if (current == null) return;
            float alpha = Mathf.Clamp01((Time.unscaledTime - receivedAt) / .12f);
            for (int i = 0; i < 2; i++) current.actors[i].Apply(director.Fighters[i], previous?.actors[i], alpha);
        }
        void Replay(FriendEventFrame e)
        {
            var ev = director.Events;
            switch (e.kind)
            {
                case "hit": ev.Raise(JsonUtility.FromJson<HitEvent>(e.data)); break;
                case "guard": ev.Raise(JsonUtility.FromJson<GuardEvent>(e.data)); break;
                case "clash": ev.Raise(JsonUtility.FromJson<ClashEvent>(e.data)); break;
                case "motion": ev.Raise(JsonUtility.FromJson<FighterMotionEvent>(e.data)); break;
                case "ranged": ev.Raise(JsonUtility.FromJson<RangedLaunchEvent>(e.data)); break;
                case "env": ev.Raise(JsonUtility.FromJson<EnvImpactEvent>(e.data)); break;
                case "surface": ev.Raise(JsonUtility.FromJson<SurfaceContactEvent>(e.data)); break;
                case "state": ev.Raise(JsonUtility.FromJson<StateChangeEvent>(e.data)); break;
                case "recover": ev.Raise(JsonUtility.FromJson<RecoverEvent>(e.data)); break;
                case "evade": ev.Raise(JsonUtility.FromJson<EvadeEvent>(e.data)); break;
                case "ko": ev.Raise(JsonUtility.FromJson<KoEvent>(e.data)); break;
                case "intervene": ev.Raise(JsonUtility.FromJson<InterventionEvent>(e.data)); break;
            }
        }
    }
}
