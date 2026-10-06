using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>診断用: 振り（有効時間）で武器が相手の体に重なったのに本体ダメージが入らなかったケースを分類する。</summary>
    public class HitAudit
    {
        class Swing
        {
            public string label;
            public AttackStyle style;
            public bool overlapped, damaged, guarded, clashed, ignoredPair, slow, anyFast;
            public float maxSpeed;
            public string firstOverlapState = "";
            public bool winOverlap, recOverlap; public float winSpeed, recSpeed;
        }

        [UnityTest, Explicit("診断用")]
        [Timeout(3600000)]
        public IEnumerator AuditMissedHits()
        {
            SimHarness.Begin(false);
            const string nine = "一口山火鬱AIOX";
            var all = new List<Swing>();
            int pairIndex = 0;
            for (int i = 0; i < nine.Length; i++)
            for (int j = i + 1; j < nine.Length; j++)
            {
                pairIndex++;
                if (pairIndex % 3 != 0) continue;
                var battle = SimHarness.Build(5000 + pairIndex * 31, left: nine[i].ToString(), right: nine[j].ToString());
                var fs = new[] { battle.Left, battle.Right };
                var swings = new Dictionary<int, Swing>();
                Swing Get(int id, Fighter a)
                {
                    if (!swings.TryGetValue(id, out var s)) swings[id] = s = new Swing { label = $"{battle.Left.Loadout.grapheme}v{battle.Right.Loadout.grapheme} {a.Loadout.grapheme}", style = a.Runtime.attackStyle };
                    return s;
                }
                battle.Context.Events.Hit += e => { if (!e.pierce) Get(e.attackId, fs[e.attacker]).damaged = true; };
                battle.Context.Events.Guard += e => Get(e.attackId, fs[e.attacker]).guarded = true;
                battle.Context.Events.Clash += e => Get(e.attackId, fs[e.attacker]).clashed = true;
                TimeController.SetSpectatorSpeed(10f);
                int last = -1, frames = 0;
                while (!battle.Director.Ended && frames++ < 200000)
                {
                    yield return null;
                    if (battle.Director.StepCount == last) continue;
                    last = battle.Director.StepCount;
                    foreach (var a in fs)
                    {
                        var rt = a.Runtime;
                        bool live = a.InAttackWindow;
                        bool win = rt.state == FighterState.AttackWindup, rec = rt.state == FighterState.AttackRecovery && !live;
                        if (!live && !win && !rec) continue;
                        var s = Get(rt.currentAttackId, a);
                        var d = a.Opponent;
                        foreach (var w in a.WeaponColliders)
                        foreach (var bc in d.BodyColliders)
                        {
                            if (!w.bounds.Intersects(bc.bounds)) continue;
                            var dist = Physics2D.Distance(w, bc);
                            if (!dist.isValid || dist.distance > 0.03f) continue;
                            float sp0 = (a.WeaponBody.GetPointVelocity(dist.pointA) - d.Body.linearVelocity).magnitude;
                            if (win) { s.winOverlap = true; s.winSpeed = Mathf.Max(s.winSpeed, sp0); continue; }
                            if (rec) { s.recOverlap = true; s.recSpeed = Mathf.Max(s.recSpeed, sp0); continue; }
                            if (!s.overlapped) s.firstOverlapState = $"d={d.Runtime.state} t={battle.Context.SimTime:F2}";
                            s.overlapped = true;
                            if (Physics2D.GetIgnoreCollision(w, bc)) s.ignoredPair = true;
                            float sp = (a.WeaponBody.GetPointVelocity(dist.pointA) - d.Body.linearVelocity).magnitude;
                            s.maxSpeed = Mathf.Max(s.maxSpeed, sp);
                            if (sp >= battle.Context.Balance.minHitRelativeSpeed) s.anyFast = true; else s.slow = true;
                        }
                    }
                }
                all.AddRange(swings.Values);
                battle.Destroy();
                yield return null;
            }
            var over = all.Where(s => s.overlapped).ToList();
            var missed = over.Where(s => !s.damaged && !s.guarded).ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"swings={all.Count} overlapped={over.Count} damaged={over.Count(s => s.damaged)} guarded={over.Count(s => s.guarded && !s.damaged)} missed={missed.Count}");
            sb.AppendLine($"missed: ignoredPair={missed.Count(s => s.ignoredPair)} clashedFirst={missed.Count(s => s.clashed)} onlySlow={missed.Count(s => !s.anyFast)} fastButNoHit={missed.Count(s => s.anyFast && !s.ignoredPair && !s.clashed)}");
            var winOnly = all.Where(x => x.winOverlap && !x.damaged && !x.guarded).ToList();
            var recOnly = all.Where(x => x.recOverlap && !x.damaged && !x.guarded && !x.overlapped).ToList();
            sb.AppendLine($"windup overlap without damage={winOnly.Count} (fast>=3: {winOnly.Count(x => x.winSpeed >= 3f)}) recovery-only overlap={recOnly.Count} (fast>=3: {recOnly.Count(x => x.recSpeed >= 3f)})");
            foreach (var g in recOnly.Where(x => x.recSpeed >= 3f).GroupBy(x => x.style)) sb.AppendLine($"  recovery fast {g.Key}: {g.Count()}");
            foreach (var g in winOnly.Where(x => x.winSpeed >= 3f).GroupBy(x => x.style)) sb.AppendLine($"  windup fast {g.Key}: {g.Count()}");
            foreach (var g in missed.GroupBy(s => s.style)) sb.AppendLine($"  style {g.Key}: {g.Count()} (ignored {g.Count(s => s.ignoredPair)}, slowOnly {g.Count(s => !s.anyFast)}, clash {g.Count(s => s.clashed)})");
            foreach (var s in missed.Take(25)) sb.AppendLine($"  {s.label} {s.style} ign={s.ignoredPair} clash={s.clashed} maxV={s.maxSpeed:F1} {s.firstOverlapState}");
            Debug.Log("[HITAUDIT]\n" + sb);
            SimHarness.End();
        }
    }
}
