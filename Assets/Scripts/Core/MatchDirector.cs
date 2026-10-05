using System;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 物理ステップの管理・時計・KO/時間切れ・勝敗（4, 10.4）。
    /// 各 FixedUpdate で「前ステップの接触解決 → KO判定 → 時計 → 左右の Tick → ステップ直前状態の記録」を固定順で行う。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class MatchDirector : MonoBehaviour
    {
        public MatchConfig Config;
        public MatchContext Context;
        public readonly Fighter[] Fighters = new Fighter[2];
        public MatchTelemetry Telemetry;

        public float Clock { get; private set; }
        public float CountdownRemaining { get; private set; }
        public bool KoInProgress { get; private set; }
        public bool Ended { get; private set; }
        public MatchResult Result { get; private set; }
        public int StepCount { get; private set; }
        public event Action<MatchResult> MatchEnded;

        float koRemaining;
        float stackedTime, clinchTime;
        float unstackRestoreAt = -1f;
        Fighter unstackTop;
        bool unstackBoth;
        readonly float[] dropRestoreAt = { -1f, -1f };
        readonly float[] perchedTime = new float[2];
        readonly float[] stretchTime = new float[2];
        readonly System.Collections.Generic.List<(Collider2D a, Collider2D b)> separatedPairs = new System.Collections.Generic.List<(Collider2D, Collider2D)>();
        readonly ContactPoint2D[] footContacts = new ContactPoint2D[24];
        int pendingWinner;
        string pendingReason;

        public CombatEvents Events => Context.Events;
        public float TimeRemaining => Mathf.Max(0f, Config.durationSeconds - Clock);
        public bool Fighting => CountdownRemaining <= 0f && !Ended && !KoInProgress;
        public bool Overtime { get; private set; }
        public float OvertimeElapsed => Overtime ? Clock - Config.durationSeconds : 0f;
        float otHpLeft, otHpRight;

        PhysicsScene2D physicsScene;

        public void Init(MatchConfig config, MatchContext ctx, Fighter left, Fighter right, float countdown, PhysicsScene2D physics)
        {
            physicsScene = physics;
            Config = config;
            Context = ctx;
            Fighters[0] = left;
            Fighters[1] = right;
            CountdownRemaining = countdown;
            Telemetry = new MatchTelemetry(this);
            ctx.RestoreCollision = (x, y) => SetPair(x, y, false);
            foreach (var f in Fighters) f.CachePreStep();
        }

        void FixedUpdate()
        {
            if (Context == null) return;
            float dt = Time.fixedDeltaTime;
            var ctx = Context;
            StepCount++;
            double t0 = Time.realtimeSinceStartupAsDouble;

            if (!Ended && !KoInProgress)
            {
                ctx.Hits.ResolveStep(ctx.SimTime);
                ctx.Env.ResolveStep(ctx.SimTime);
                CheckKo();
            }
            else
            {
                // KO 演出中・終了後は勝敗を動かさない（物理衝突は継続）
                ctx.Hits.Discard();
                ctx.Env.Discard();
            }

            bool countingDown = CountdownRemaining > 0f;
            if (countingDown) CountdownRemaining -= dt;
            ctx.AiEnabled = !countingDown && !ctx.MatchOver;

            if (Fighting)
            {
                Clock += dt;
                ctx.MatchClock = Clock;
                if (Overtime)
                {
                    // 延長戦: 最初にダメージが入った時点で決着（HP 割合で判定）
                    bool damaged = Fighters[0].Runtime.hp < otHpLeft || Fighters[1].Runtime.hp < otHpRight;
                    if (damaged || OvertimeElapsed >= Context.Balance.suddenDeathMaxSeconds - 1e-5f) TimeUp();
                }
                else if (Clock >= Config.durationSeconds - 1e-5f) TimeUp();
            }

            ctx.SimTime += dt;
            if (StepCount % 5 == 0) ResolveDeepPenetration();
            ResolveStandingOnWeapon();
            ResolveStacking(dt);
            ResolveHingeStretch(dt);
            Fighters[0].Tick(ctx.SimTime, dt);
            Fighters[1].Tick(ctx.SimTime, dt);
            Fighters[0].CachePreStep();
            Fighters[1].CachePreStep();
            // 専用物理ワールドを 1 ステップ進める（接触コールバックはこの中で報告キューへ入る）
            if (physicsScene.IsValid()) physicsScene.Simulate(dt);
            Fighters[0].CapturePose();
            Fighters[1].CapturePose();
            if (!Ended)
            {
                Telemetry.Step(dt);
                Telemetry.RecordStepCost((float)((Time.realtimeSinceStartupAsDouble - t0) * 1000.0));
            }
        }

        void Update()
        {
            TimeController.Tick(Time.unscaledDeltaTime);
            if (KoInProgress && !Ended && !TimeController.Paused)
            {
                koRemaining -= Time.unscaledDeltaTime;
                if (koRemaining <= 0f)
                {
                    TimeController.SetKoSlow(false, 1f);
                    Finish(pendingWinner, pendingReason);
                }
            }
        }

        /// <summary>
        /// 片方が相手の上に乗ったまま動けなくなるのを防ぐ安定化処理。0.8 秒以上重なっていたら、上の方を空いている側へ滑り落とす。
        /// </summary>
        void ResolveStacking(float dt)
        {
            var a = Fighters[0];
            var b = Fighters[1];
            if (unstackTop != null && Context.SimTime >= unstackRestoreAt)
            {
                SetBodyVsWeapon(unstackTop, unstackTop.Opponent, false);
                if (unstackBoth) SetBodyVsWeapon(unstackTop.Opponent, unstackTop, false);
                SetBodyVsBody(unstackTop, unstackTop.Opponent, false);
                unstackTop = null;
                unstackBoth = false;
            }
            if (a.Runtime.IsDown || b.Runtime.IsDown) { stackedTime = 0f; clinchTime = 0f; return; }
            bool stacked = Mathf.Abs(a.X - b.X) < 0.8f && Mathf.Abs(a.Body.position.y - b.Body.position.y) > 0.35f;
            stackedTime = stacked ? stackedTime + dt : 0f;
            // 武器が絡んで密着したまま離れられない状態（クリンチ）も解く
            bool clinch = Mathf.Abs(a.X - b.X) < 1.0f;
            clinchTime = clinch ? clinchTime + dt : 0f;
            if (clinchTime >= 1.5f && unstackTop == null)
            {
                float side = a.X <= b.X ? -1f : 1f;
                float arenaHalf = Context.ArenaHalfWidth;
                if (Mathf.Abs(a.X + side) > arenaHalf - 0.4f || Mathf.Abs(b.X - side) > arenaHalf - 0.4f) side = -side; // 壁際なら入れ替えるように離す
                SetBodyVsWeapon(a, b, true);
                SetBodyVsWeapon(b, a, true);
                a.AddVelocity(new Vector2(side * 3.5f, 1.5f) - a.Body.linearVelocity);
                b.AddVelocity(new Vector2(-side * 3.5f, 1.5f) - b.Body.linearVelocity);
                unstackTop = a; unstackBoth = true;
                a.CancelLift(); b.CancelLift();
                Telemetry?.Note("RESOLVE clinch");
                unstackRestoreAt = Context.SimTime + 0.5f;
                clinchTime = 0f;
                return;
            }
            if (stackedTime < 0.8f) return;
            var top = a.Body.position.y > b.Body.position.y ? a : b;
            var bottom = top == a ? b : a;
            float half = Context.ArenaHalfWidth;
            float dir = top.X >= bottom.X ? 1f : -1f;
            if (Mathf.Abs(bottom.X + dir * 1.2f) > half - 0.5f) dir = -dir;
            top.AddVelocity(new Vector2(dir * 4f, 1f) - top.Body.linearVelocity);
            // 下の相手の武器に乗っている場合があるので、少しの間だけ素通りさせて落とす
            if (unstackTop == null)
            {
                SetBodyVsWeapon(top, bottom, true);
                SetBodyVsBody(top, bottom, true); // 頭の上に乗っている場合も落とす
                top.Runtime.weaponLimpUntil = Context.SimTime + 0.6f;
                top.CancelLift();
                Telemetry?.Note($"RESOLVE stacked top={top.Id}");
                unstackTop = top;
                unstackRestoreAt = Context.SimTime + 0.6f;
            }
            stackedTime = 0f;
        }

        /// <summary>
        /// 相手の武器の上に足で乗ってしまった場合（槍を踏みつけて相手がガードも攻撃もできなくなる等）、
        /// 0.5 秒だけその体と相手の武器の衝突を外して下へ落とす。
        /// </summary>
        void ResolveStandingOnWeapon()
        {
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                var opp = f.Opponent;
                // 浮いたまま静止している時間は、落とす処理の最中も数え続ける
                bool hanging = !f.Runtime.IsDown && f.Body.position.y > 0.5f && Mathf.Abs(f.Body.linearVelocity.y) < 0.4f;
                if (dropRestoreAt[i] >= 0f)
                {
                    perchedTime[i] = hanging ? perchedTime[i] + Time.fixedDeltaTime : 0f;
                    if (perchedTime[i] >= 0.8f) { f.Runtime.weaponLimpUntil = Context.SimTime + 0.6f; perchedTime[i] = 0f; Telemetry?.Note($"RESOLVE limp {i}"); }
                    if (Context.SimTime < dropRestoreAt[i]) continue;
                    SetBodyVsWeapon(f, opp, false);
                    SetBodyVsBody(f, opp, false);
                    dropRestoreAt[i] = -1f;
                }
                if (f.Runtime.IsDown || f.Body.position.y < 0.25f) { perchedTime[i] = 0f; continue; }
                // 高い所で上下にほぼ動かない（相手の字形や体、または自分の字形で地面を突いて浮いている）状態が続いたら落とす
                bool perched = f.Body.position.y > 0.5f && Mathf.Abs(f.Body.linearVelocity.y) < 0.4f;
                perchedTime[i] = perched ? perchedTime[i] + Time.fixedDeltaTime : 0f;
                if (perchedTime[i] >= 0.8f)
                {
                    SetBodyVsWeapon(f, opp, true);
                    SetBodyVsBody(f, opp, true);
                    dropRestoreAt[i] = Context.SimTime + 0.6f;
                    f.Runtime.weaponLimpUntil = Context.SimTime + 0.6f;
                    f.CancelLift();
                    perchedTime[i] = 0f;
                    Telemetry?.Note($"RESOLVE perched {i} y={f.Body.position.y:F2}");
                    continue;
                }
                int n = f.Body.GetContacts(footContacts);
                for (int k = 0; k < n; k++)
                {
                    var c = footContacts[k];
                    var other = c.collider != null && c.collider.attachedRigidbody == f.Body ? c.otherCollider : c.collider;
                    if (other == null || other.attachedRigidbody != opp.WeaponBody) continue;
                    if (c.point.y > f.Body.position.y + 0.6f) continue; // 足元（脚の高さ）での接触だけ
                    SetBodyVsWeapon(f, opp, true);
                    SetBodyVsBody(f, opp, true);
                    dropRestoreAt[i] = Context.SimTime + 0.5f;
                    f.Runtime.weaponLimpUntil = Context.SimTime + 0.5f; // 自分の字形で体を支えていても落ちるように
                    f.CancelLift();
                    Telemetry?.Note($"RESOLVE standing-on-weapon {i}");
                    break;
                }
            }
        }

        /// <summary>
        /// カスタマイズした武器（小さすぎて相手の字形の穴に挟まる・長すぎて押さえ込まれる）が引っ掛かり、
        /// 握り（ヒンジ）が大きくずれたままになったら、相手との衝突を離れるまで外し、武器の保持トルクを一瞬抜いて外す。
        /// カスタマイズ無しの試合は P1/P2 の検証どおりのまま（対象外）。
        /// </summary>
        void ResolveHingeStretch(float dt)
        {
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                if (!f.Mods.customized || f.Runtime.IsDown) { stretchTime[i] = 0f; continue; }
                float stretch = (f.WeaponBody.position - f.Body.GetRelativePoint(f.ShoulderLocal(f.Facing))).magnitude;
                stretchTime[i] = stretch > StretchLimit ? stretchTime[i] + dt : 0f;
                if (stretchTime[i] < StretchSeconds) continue;
                stretchTime[i] = 0f;
                var opp = f.Opponent;
                foreach (var w in f.WeaponColliders)
                {
                    foreach (var o in opp.WeaponColliders) Separate(w, o);
                    foreach (var o in opp.BodyColliders) Separate(w, o);
                }
                f.Runtime.weaponLimpUntil = Context.SimTime + 0.3f;
                Telemetry?.Note($"RESOLVE hinge-stretch {i} {stretch:F2}");
            }
        }

        const float StretchLimit = 0.25f, StretchSeconds = 0.3f;

        /// <summary>離れるまで衝突を外す（ResolveDeepPenetration が 0.05 以上離れたら戻す）。</summary>
        void Separate(Collider2D a, Collider2D b)
        {
            if (Physics2D.GetIgnoreCollision(a, b)) return;
            Physics2D.IgnoreCollision(a, b, true);
            if (!separatedPairs.Contains((a, b))) separatedPairs.Add((a, b));
        }

        /// <summary>
        /// 武器が相手の体・武器に深くめり込んで抜けなくなる（突き刺さったまま固定される）のを防ぐ。
        /// めり込み 0.12 以上の組は離れるまで衝突を外し、0.05 以上離れたら戻す。
        /// </summary>
        void ResolveDeepPenetration()
        {
            for (int i = separatedPairs.Count - 1; i >= 0; i--)
            {
                var (a, b) = separatedPairs[i];
                if (a == null || b == null) { separatedPairs.RemoveAt(i); continue; }
                var d = Physics2D.Distance(a, b);
                if (!d.isValid || d.distance > 0.05f)
                {
                    Physics2D.IgnoreCollision(a, b, false);
                    separatedPairs.RemoveAt(i);
                }
            }
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                var opp = f.Opponent;
                var wb = WeaponBounds(f);
                CheckPenetration(f.WeaponColliders, opp.BodyColliders, wb);
                if (i == 0) CheckPenetration(f.WeaponColliders, opp.WeaponColliders, wb);
            }
        }

        static Bounds WeaponBounds(Fighter f)
        {
            var b = f.WeaponColliders[0].bounds;
            foreach (var c in f.WeaponColliders) b.Encapsulate(c.bounds);
            return b;
        }

        void CheckPenetration(Collider2D[] weapon, Collider2D[] others, Bounds weaponBounds)
        {
            foreach (var o in others)
            {
                if (!o.bounds.Intersects(weaponBounds)) continue;
                foreach (var w in weapon)
                {
                    if (!w.bounds.Intersects(o.bounds) || Physics2D.GetIgnoreCollision(w, o)) continue;
                    var d = Physics2D.Distance(w, o);
                    if (d.isValid && d.distance < -0.12f)
                    {
                        Physics2D.IgnoreCollision(w, o, true);
                        separatedPairs.Add((w, o));
                    }
                }
            }
        }

        void SetBodyVsBody(Fighter a, Fighter b, bool ignore)
        {
            foreach (var x in a.BodyColliders)
            foreach (var y in b.BodyColliders)
                SetPair(x, y, ignore);
        }

        /// <summary>
        /// 衝突の無効化・復帰。復帰時にまだ重なっている組は戻さず、離れてから戻す（めり込んだまま衝突が復活して弾け飛ぶのを防ぐ）。
        /// </summary>
        void SetPair(Collider2D x, Collider2D y, bool ignore)
        {
            if (ignore) { Physics2D.IgnoreCollision(x, y, true); return; }
            var d = Physics2D.Distance(x, y);
            if (d.isValid && d.distance < 0.02f)
            {
                if (!separatedPairs.Contains((x, y))) separatedPairs.Add((x, y));
                return;
            }
            Physics2D.IgnoreCollision(x, y, false);
        }

        void SetBodyVsWeapon(Fighter bodyOwner, Fighter weaponOwner, bool ignore)
        {
            foreach (var bc in bodyOwner.BodyColliders)
            foreach (var wc in weaponOwner.WeaponColliders)
                SetPair(bc, wc, ignore);
        }

        void CheckKo()
        {
            int outcome = MatchRules.DecideKo(Fighters[0].Runtime.hp, Fighters[1].Runtime.hp);
            if (outcome == MatchRules.NoOutcome) return;
            KoInProgress = true;
            Context.MatchOver = true;
            pendingWinner = outcome;
            pendingReason = outcome == MatchRules.Draw ? "DOUBLE_KO" : "KO";
            Vector2 point = Vector2.zero;
            foreach (var f in Fighters)
            {
                if (f.Runtime.hp > 0f) continue;
                if (f.Runtime.state != FighterState.KO) f.Knockdown.EnterKO(Context.SimTime);
                point = f.ChestWorld;
            }
            Events.Raise(new KoEvent { winner = outcome, point = point, time = Context.SimTime });
            var b = Context.Balance;
            if (TimeController.PresentationEffectsEnabled)
            {
                koRemaining = b.koPresentationSeconds;
                TimeController.SetKoSlow(true, b.koTimeScale);
            }
            else Finish(pendingWinner, pendingReason);
        }

        void TimeUp()
        {
            var l = Fighters[0].Runtime;
            var r = Fighters[1].Runtime;
            int w = MatchRules.DecideTimeUp(l.hp, l.maxHp, r.hp, r.maxHp);
            if (w == MatchRules.Draw && !Overtime && Context.Balance.suddenDeath)
            {
                Overtime = true;
                Context.Overtime = true;
                otHpLeft = l.hp;
                otHpRight = r.hp;
                Telemetry?.Note("OVERTIME");
                return;
            }
            Context.MatchOver = true;
            if (Overtime) Finish(w, w == MatchRules.Draw ? "SUDDEN_DEATH_DRAW" : "SUDDEN_DEATH");
            else Finish(w, w == MatchRules.Draw ? "TIME_UP_DRAW" : "TIME_UP");
        }

        void Finish(int winner, string reason)
        {
            if (Ended) return;
            Ended = true;
            Context.MatchOver = true;
            Result = new MatchResult
            {
                winner = winner,
                elapsedSeconds = Clock,
                finishReason = reason,
                seed = Config.seed,
                metrics = new[] { Fighters[0].Runtime.metrics, Fighters[1].Runtime.metrics },
                hpRemaining = new[] { Mathf.Max(0f, Fighters[0].Runtime.hp), Mathf.Max(0f, Fighters[1].Runtime.hp) },
                maxHp = new[] { Fighters[0].Runtime.maxHp, Fighters[1].Runtime.maxHp },
            };
            Telemetry.Finish(Result);
            Events.Raise(Result);
            MatchEnded?.Invoke(Result);
        }

        void OnDestroy()
        {
            if (KoInProgress && !Ended) TimeController.SetKoSlow(false, 1f);
        }
    }
}
