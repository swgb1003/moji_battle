using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    public struct HitContact
    {
        public Fighter attacker, target;
        public bool targetIsWeapon;
        public BodyPart part;
        public Vector2 point, normal, relativeVelocity;
        public int myColliderId, otherColliderId;
    }

    /// <summary>attackId × defenderId で「1スイング1回」を保証する台帳。</summary>
    public sealed class AttackLedger
    {
        readonly HashSet<long> resolved = new HashSet<long>();
        static long Key(int attackId, int defenderId) => ((long)attackId << 8) | (uint)(defenderId & 0xFF);
        public bool IsResolved(int attackId, int defenderId) => resolved.Contains(Key(attackId, defenderId));
        public void MarkResolved(int attackId, int defenderId) => resolved.Add(Key(attackId, defenderId));
        public int Count => resolved.Count;
    }

    public enum HitOutcomeKind { None, Guard, Body, Clash }

    /// <summary>1ステップ分の候補接触（解決の純粋ロジック用）。</summary>
    public struct ContactCandidate
    {
        public bool isWeapon;
        public bool frontal;
        public float vN;
        public float damage;
        /// <summary>当たりの判定に使う速さ（すれ違う速さも含む）。0 なら vN で判定する</summary>
        public float speed;
        public float GateSpeed => speed > 0f ? speed : vN;
    }

    /// <summary>
    /// 接触をその場で処理せず、1物理ステップ分を蓄積してから解決する（8.1）。
    /// 有効な武器ガードを本体ヒットより優先し、同一スイング中の本体ダメージは最も有効な1接触だけ。
    /// </summary>
    public sealed class HitResolver
    {
        readonly MatchContext ctx;
        readonly List<HitContact> queue = new List<HitContact>(128);
        readonly List<HitContact> scratch = new List<HitContact>(64);
        readonly List<ContactCandidate> candidates = new List<ContactCandidate>(64);
        public readonly AttackLedger Ledger = new AttackLedger();
        public int TotalContactsReported { get; private set; }
        /// <summary>診断用: [攻撃側] 攻撃窓外の接触ステップ / 解決済み / 本体接触ステップ / 武器接触ステップ / 速度不足で棄却</summary>
        public readonly int[,] Diag = new int[2, 5];

        public HitResolver(MatchContext ctx) { this.ctx = ctx; }

        public void Report(HitContact c)
        {
            queue.Add(c);
            TotalContactsReported++;
        }

        public void Discard() => queue.Clear();

        public void ResolveStep(float time)
        {
            if (queue.Count == 0) return;
            // Unity のコールバック順序に依存しないよう決定的に並べ替える
            queue.Sort((a, b) =>
            {
                int c = a.attacker.Id.CompareTo(b.attacker.Id);
                if (c != 0) return c;
                c = a.myColliderId.CompareTo(b.myColliderId);
                if (c != 0) return c;
                c = a.otherColliderId.CompareTo(b.otherColliderId);
                if (c != 0) return c;
                c = a.point.x.CompareTo(b.point.x);
                return c != 0 ? c : a.point.y.CompareTo(b.point.y);
            });
            for (int id = 0; id < 2; id++) ResolveFor(id, time);
            queue.Clear();
        }

        /// <summary>
        /// 解決規則（純粋関数）。既に解決済みのスイングなら何もしない。
        /// 有効ガード（防御側がガード中・正面・速度閾値以上の武器接触）> 本体ヒット（最大ダメージ）> 武器同士の弾き。
        /// </summary>
        public static HitOutcomeKind Choose(IReadOnlyList<ContactCandidate> list, bool alreadyResolved, bool defenderGuarding,
            float minSpeed, out int chosen)
        {
            chosen = -1;
            if (alreadyResolved || list.Count == 0) return HitOutcomeKind.None;
            int guard = -1, body = -1, clash = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c.GateSpeed < minSpeed) continue;
                if (c.isWeapon)
                {
                    if (defenderGuarding && c.frontal)
                    {
                        if (guard < 0 || c.vN > list[guard].vN) guard = i;
                    }
                    else if (clash < 0 || c.vN > list[clash].vN) clash = i;
                }
                else if (body < 0 || c.damage > list[body].damage) body = i;
            }
            if (guard >= 0) { chosen = guard; return HitOutcomeKind.Guard; }
            if (body >= 0) { chosen = body; return HitOutcomeKind.Body; }
            if (clash >= 0) { chosen = clash; return HitOutcomeKind.Clash; }
            return HitOutcomeKind.None;
        }

        void ResolveFor(int attackerId, float time)
        {
            scratch.Clear();
            foreach (var c in queue) if (c.attacker.Id == attackerId) scratch.Add(c);
            if (scratch.Count == 0) return;
            var a = scratch[0].attacker;
            var d = a.Opponent;
            var rtA = a.Runtime;
            var b = ctx.Balance;
            // 攻撃窓外の接触は物理衝突のみ（ダメージなし）。攻撃窓 = 振り・振り抜き・投げた武器の飛行中
            if (!a.InAttackWindow) { Diag[attackerId, 0]++; return; }
            bool resolved = Ledger.IsResolved(rtA.currentAttackId, d.Id);
            if (resolved) Diag[attackerId, 1]++;
            bool guarding = d.Runtime.state == FighterState.Guard;

            candidates.Clear();
            var qualities = new HitQuality[scratch.Count];
            for (int i = 0; i < scratch.Count; i++)
            {
                var c = scratch[i];
                Vector2 n = c.normal.sqrMagnitude > 1e-6f ? c.normal.normalized : Vector2.right;
                Vector2 va = a.PreWeaponPointVelocity(c.point);
                Vector2 vd = c.targetIsWeapon ? d.PreWeaponPointVelocity(c.point) : d.PreBodyVelocity;
                float vN = Mathf.Max(Mathf.Abs(Vector2.Dot(va - vd, n)), Mathf.Abs(Vector2.Dot(c.relativeVelocity, n)));
                // 横薙ぎ・足払い・回転斬りは奥行き方向の回転なので、平面上の速度の代わりに「奥行きの角速度 × 握りからの水平距離」を使う
                var tech = AttackTechniques.Tuning(rtA.attackStyle, b);
                if (AttackTechniques.IsYaw(rtA.attackStyle))
                {
                    float r = Mathf.Abs(a.WeaponLocalPoint(c.point).x);
                    float vSweep = Mathf.Min(Mathf.Abs(rtA.sweepYawRate), b.sweepMaxYawSpeed) * Mathf.Deg2Rad * r * b.sweepSpeedScale;
                    if (vSweep > vN) { vN = vSweep; va = n * vSweep; }
                }
                // 叩き落としは当たりの速さを固定する（浮いた相手への判定は武器の回転速度に左右されない）
                bool smashHit = rtA.attackStyle == AttackStyle.Smash && !c.targetIsWeapon;
                if (smashHit) { vN = b.smashHitSpeed; va = n * vN; }
                // 当たりの判定は接触面に垂直な速さだけでなく、かすめる（すれ違う）速さも含める。威力は垂直な速さ（vN）で決まる
                float glancing = Mathf.Max((va - vd).magnitude, c.relativeVelocity.magnitude);
                var cand = new ContactCandidate { isWeapon = c.targetIsWeapon, vN = vN, speed = c.targetIsWeapon ? 0f : Mathf.Max(vN, glancing) };
                // 横持ちはガードの正面角が広い
                if (c.targetIsWeapon) cand.frontal = IsFrontal(d, c.point, Mathf.Min(180f, b.guardHalfAngle * d.Mods.guardRange));
                else
                {
                    qualities[i] = smashHit ? HitQuality.Normal : ClassifyQuality(a, c.point, n, va, b);
                    float styleDamage = tech != null ? tech.damage : 1f;
                    // 刺す: 先端の直撃は中央の当たり（大ダメージ）、柄や側面での接触は弱い
                    if (rtA.attackStyle == AttackStyle.Thrust)
                    {
                        bool tip = IsStabTip(a, c.point, b);
                        qualities[i] = tip ? HitQuality.Center : HitQuality.Normal;
                        styleDamage *= tip ? b.stabTipDamage : b.stabShaftDamage;
                    }
                    // サイズの威力は質量を通じてだけ（Mods.damage = 質量倍率^指数）
                    cand.damage = DamageMath.BodyDamage(a.Stats.attack, d.Stats.defense, vN, qualities[i], c.part, b) * a.Mods.damage * styleDamage;
                }
                candidates.Add(cand);
            }

            bool anyBody = false, anyWeapon = false, anyFast = false;
            foreach (var c in candidates) { if (c.isWeapon) anyWeapon = true; else anyBody = true; if (c.GateSpeed >= b.minHitRelativeSpeed) anyFast = true; }
            if (anyBody) Diag[attackerId, 2]++;
            if (anyWeapon) Diag[attackerId, 3]++;
            if (!anyFast) Diag[attackerId, 4]++;
            var kind = Choose(candidates, resolved, guarding, b.minHitRelativeSpeed, out int idx);
            if (kind == HitOutcomeKind.None) return;
            var hit = scratch[idx];
            var chosen = candidates[idx];
            switch (kind)
            {
                case HitOutcomeKind.Guard: ApplyGuard(a, d, hit, chosen.vN, time); break;
                case HitOutcomeKind.Body: ApplyBodyHit(a, d, hit, chosen, qualities[idx], time); break;
                case HitOutcomeKind.Clash: ApplyClash(a, d, hit, chosen.vN, time); break;
            }
        }

        /// <summary>刺すの先端（握りからの距離が武器の長さの一定割合以上）での接触か。</summary>
        static bool IsStabTip(Fighter a, Vector2 point, CombatBalance b) =>
            a.WeaponLocalPoint(point).magnitude >= a.Weapon.length * b.stabTipFraction;

        static bool IsFrontal(Fighter defender, Vector2 point, float halfAngle)
        {
            Vector2 toPoint = point - defender.ChestWorld;
            if (toPoint.sqrMagnitude < 1e-6f) return true;
            return Vector2.Angle(new Vector2(defender.Facing, 0f), toPoint) <= halfAngle;
        }

        /// <summary>中央寄り直撃 / 通常 / 端のかすり。字形の主な伸び方向の端と、接触方向の浅さで判定。</summary>
        public static HitQuality ClassifyQuality(Fighter a, Vector2 point, Vector2 n, Vector2 pointVelocity, CombatBalance b)
        {
            var g = a.Weapon;
            Vector2 local = a.WeaponLocalPoint(point);
            Rect r = g.boundsLocal;
            float edge = float.MaxValue;
            if (r.width >= 0.3f * g.maxSide) edge = Mathf.Min(edge, Mathf.Min(local.x - r.xMin, r.xMax - local.x));
            if (r.height >= 0.3f * g.maxSide) edge = Mathf.Min(edge, Mathf.Min(local.y - r.yMin, r.yMax - local.y));
            edge = Mathf.Max(0f, edge) / g.maxSide;
            float tip = local.magnitude / Mathf.Max(0.01f, g.length);
            float dirDot = pointVelocity.sqrMagnitude > 1e-4f ? Mathf.Abs(Vector2.Dot(pointVelocity.normalized, n)) : 0f;
            if ((edge < b.grazeEdgeFraction || tip > b.grazeTipFraction) && dirDot < 0.5f) return HitQuality.Graze;
            if ((local - g.comLocal).magnitude / g.maxSide < b.centerFraction && dirDot >= 0.6f) return HitQuality.Center;
            return HitQuality.Normal;
        }

        static Vector2 KnockDirection(Fighter a, Fighter d, Vector2 n, float upBias)
        {
            // 投げた武器は飛んできた側から押す
            float fromX = a.Runtime.throwLive ? a.WeaponBody.worldCenterOfMass.x : a.X;
            float toward = d.X >= fromX ? 1f : -1f;
            Vector2 dir = n;
            if (dir.x * toward < 0f) dir = -dir;
            dir.x = toward * Mathf.Max(Mathf.Abs(dir.x), 0.35f);
            dir.y = Mathf.Max(dir.y, 0f) + upBias;
            return dir.normalized;
        }

        void ApplyGuard(Fighter a, Fighter d, HitContact hit, float vN, float time)
        {
            var b = ctx.Balance;
            var rtA = a.Runtime;
            var rtD = d.Runtime;
            rtA.attackHadContact = true;
            float load = vN * a.WeaponBody.mass * b.guardLoadPerMomentum;
            if (rtA.attackStyle == AttackStyle.Bash) load *= b.bashGuardLoad; // 盾当てはガードごと押し込んで崩す
            rtD.guardLoad += load;
            rtD.metrics.guards++;
            // 持ち方のガード安定性: 崩れにくく、押し込まれにくい
            float imp = DamageMath.Impulse(vN, a.WeaponBody.mass, d.Body.mass, b) * b.guardImpulseRatio / d.Mods.guardStability;
            Vector2 dir = KnockDirection(a, d, hit.normal, 0.1f);
            d.Body.AddForce(dir * imp * b.impulseScale, ForceMode2D.Impulse);
            // 武器同士は反発し、重量差があると軽い側の本体も後退する
            float mA = a.WeaponBody.mass, mD = d.WeaponBody.mass;
            // 投げた武器の反発は持ち主に届かない
            if (mA < mD && !rtA.throwLive) a.Body.AddForce(-dir * imp * b.impulseScale * (mD - mA) / (mA + mD), ForceMode2D.Impulse);
            bool broke = rtD.guardLoad >= b.guardBreakThreshold * d.Mods.guardStability;
            // ガードが持ちこたえたらこのスイングの本体ダメージは無し。崩れた場合は同じスイングがそのまま本体に届き得る。
            if (!broke) Ledger.MarkResolved(rtA.currentAttackId, d.Id);
            if (broke)
            {
                rtD.guardLoad = 0f;
                rtD.metrics.guardBreaks++;
                d.EnterStagger(b.guardBreakStagger);
                a.SetWeaponPassThrough(true); // 崩したガードを押し退けて振り抜く
            }
            ctx.Events.Raise(new GuardEvent
            {
                attacker = a.Id, defender = d.Id, attackId = rtA.currentAttackId,
                load = load, vN = vN, point = hit.point, broke = broke, time = time,
            });
            // 刺すの先端はガードの隙間を一部貫く（相手の武器が自分の 2 倍以上重いと貫けない）
            if (!broke && rtA.attackStyle == AttackStyle.Thrust && IsStabTip(a, hit.point, b) && mD < mA * 2f && vN >= b.minHitRelativeSpeed)
            {
                float chip = DamageMath.BodyDamage(a.Stats.attack, d.Stats.defense, vN, HitQuality.Normal, BodyPart.Torso, b) * a.Mods.damage * b.stabGuardPierce;
                rtD.ApplyDamage(chip);
                rtA.metrics.damageDealt += chip;
                ctx.LastDamageTime = time;
                if (rtD.hp <= 0f) d.Knockdown.EnterKO(time);
                ctx.Events.Raise(new HitEvent
                {
                    attacker = a.Id, defender = d.Id, attackId = rtA.currentAttackId, part = BodyPart.Torso, quality = HitQuality.Graze,
                    damage = chip, vN = vN, point = hit.point, time = time, defenderStateBefore = FighterState.Guard,
                    style = AttackStyle.Thrust, pierce = true, knockdown = rtD.hp <= 0f,
                });
            }
        }

        void ApplyBodyHit(Fighter a, Fighter d, HitContact hit, ContactCandidate c, HitQuality quality, float time)
        {
            var b = ctx.Balance;
            var rtA = a.Runtime;
            var rtD = d.Runtime;
            rtA.attackHadContact = true;
            Ledger.MarkResolved(rtA.currentAttackId, d.Id);

            var stateBefore = rtD.state;
            float dmg = DamageMath.CapHit(c.damage, rtD.maxHp, b);
            rtD.ApplyDamage(dmg);
            rtA.metrics.damageDealt += dmg;
            rtA.metrics.hitsLanded++;
            rtA.metrics.partHits[(int)hit.part]++;
            if (hit.part == BodyPart.Head) rtA.metrics.criticals++;
            if (quality == HitQuality.Graze) rtA.metrics.grazes++;
            if (quality == HitQuality.Center) rtA.metrics.centerHits++;
            ctx.LastDamageTime = time;

            // 両手持ちはノックバック耐性で衝撃を割る
            var tech = AttackTechniques.Tuning(rtA.attackStyle, b);
            float impulse = DamageMath.Impulse(c.vN, a.WeaponBody.mass, d.Body.mass, b) / d.Mods.knockbackResistance
                            * (tech != null ? tech.impulse : 1f);
            // 足払いは脚を払う（どこに当たっても転倒の判定は脚扱い）
            bool leg = hit.part == BodyPart.Leg || rtA.attackStyle == AttackStyle.LowSweep;
            rtD.knockdownAccum += impulse * b.knockdownAccumPerImpulse + (leg ? b.legKnockdownBonus : 0f);
            bool knock = impulse >= b.knockdownImpulse || (leg && impulse >= b.legKnockdownImpulse) || rtD.knockdownAccum >= b.knockdownAccumThreshold;
            bool launch = impulse >= b.launchImpulse;
            bool stagger = impulse >= b.staggerImpulse;

            if (rtD.hp <= 0f) d.Knockdown.EnterKO(time);
            else if (knock) d.Knockdown.EnterKnockdown(time);
            // 重量級のスーパーアーマー: 溜め・振りの最中は、ずっと軽い武器の転倒に至らない打撃ではひるまずに振り切る（受けながら一撃）
            else if (stagger && !rtD.IsDown && !HeavyArmor(a, d, stateBefore, b)) d.EnterStagger(b.staggerDuration);
            if (launch) d.MarkLaunched(time);

            Vector2 dir = KnockDirection(a, d, hit.normal, tech != null && tech.upBias >= 0f ? tech.upBias : b.launchUpBias);
            Vector2 force = dir * impulse * b.impulseScale;
            // 連携: 打ち上げは必ず真上へ浮かせ（叩き落としが届くよう横へは飛ばさない）、叩き落としは地面へ向けて落とす。
            // どちらも吹き飛ばしの力の代わりに速度を直接決める
            bool comboMove = (rtA.attackStyle == AttackStyle.Launch && rtD.state != FighterState.KO) || rtA.attackStyle == AttackStyle.Smash;
            if (!comboMove)
            {
                if (rtD.IsDown) d.Body.AddForceAtPosition(force, hit.point, ForceMode2D.Impulse);
                else d.Body.AddForce(force, ForceMode2D.Impulse);
            }

            float away = d.X >= a.X ? 1f : -1f;
            if (rtA.attackStyle == AttackStyle.Launch && rtD.state != FighterState.KO)
            {
                var v = d.Body.linearVelocity;
                d.AddVelocity(new Vector2(away * b.launchLiftAway, Mathf.Max(v.y, b.launchLiftSpeed)) - v);
                if (!launch) { launch = true; d.MarkLaunched(time); }
                rtA.launchConnectedAt = time;
            }
            else if (rtA.attackStyle == AttackStyle.Smash)
            {
                var v = d.Body.linearVelocity;
                d.AddVelocity(new Vector2(away * b.smashDriveAway, -b.smashDriveSpeed) - v);
                d.MarkLaunched(time); // 地面への激突を吹っ飛びの環境ダメージとして数える
                launch = true;
                rtD.smashedAt = time;
            }

            ctx.Events.Raise(new HitEvent
            {
                attacker = a.Id, defender = d.Id, attackId = rtA.currentAttackId,
                part = hit.part, quality = quality, damage = dmg, vN = c.vN, impulse = impulse,
                point = hit.point, critical = hit.part == BodyPart.Head,
                stagger = stagger, launch = launch, knockdown = knock || rtD.hp <= 0f, time = time,
                defenderStateBefore = stateBefore, style = rtA.attackStyle, combo = rtA.attackStyle == AttackStyle.Smash,
            });
        }

        static bool HeavyArmor(Fighter a, Fighter d, FighterState defenderState, CombatBalance b) =>
            b.heavyArmorDuringAttack && d.WeightClass == WeightClass.Heavy
            && (defenderState == FighterState.AttackWindup || defenderState == FighterState.AttackActive)
            && a.WeaponBody.mass < d.WeaponBody.mass * b.heavyArmorMassRatio;

        void ApplyClash(Fighter a, Fighter d, HitContact hit, float vN, float time)
        {
            var rtA = a.Runtime;
            if (rtA.clashedThisAttack) return;
            rtA.clashedThisAttack = true;
            rtA.attackHadContact = true;
            rtA.metrics.clashes++;
            var b = ctx.Balance;
            float mA = a.WeaponBody.mass, mD = d.WeaponBody.mass;
            var lighter = mA <= mD ? a : d;
            var heavier = lighter == a ? d : a;
            // 勢い（質量×接触点の速度）で上回る振りは、受けた武器を振りの方向へ叩き落としてそのまま振り抜く。
            // 構えているだけの武器は勢いがほぼ 0 なので押し退けられる。受け止めたいならガード姿勢（ガード判定）が必要。
            Vector2 va = a.PreWeaponPointVelocity(hit.point);
            Vector2 vd = d.PreWeaponPointVelocity(hit.point);
            float momentumA = mA * va.magnitude, momentumD = mD * vd.magnitude;
            bool overpower = mA >= mD * b.overpowerMassRatio || momentumA >= momentumD * b.overpowerMomentumRatio;
            if (overpower)
            {
                if (va.sqrMagnitude > 1e-4f)
                {
                    float share = Mathf.Clamp01((momentumA - momentumD) / Mathf.Max(0.01f, momentumA + momentumD));
                    d.WeaponBody.AddForceAtPosition(va.normalized * share * Mathf.Min(vN, 15f) * mD * b.clashWeaponKnock, hit.point, ForceMode2D.Impulse);
                }
                a.SetWeaponPassThrough(true);
            }
            // 勢い負けした側は短くよろける（攻撃中なら中断）。勝った側はその隙に追撃できる
            bool defenderWins = !overpower && momentumD >= momentumA * b.overpowerMomentumRatio && mD * b.overpowerMassRatio > mA;
            var loser = overpower ? d : defenderWins && !rtA.throwLive ? a : null;
            if (loser != null && !loser.Runtime.IsDown && loser.Runtime.state != FighterState.Guard && loser.Runtime.state != FighterState.Stagger)
                loser.EnterStagger(b.clashLoserStagger);
            float push = b.clashPush * Mathf.Abs(mD - mA) / (mA + mD) * Mathf.Min(vN, 12f) * 0.2f;
            if (push > 0f && !(lighter == a && rtA.throwLive))
            {
                float dir = lighter.X >= heavier.X ? 1f : -1f;
                lighter.Body.AddForce(new Vector2(dir * push * b.impulseScale, 0.2f), ForceMode2D.Impulse);
            }
            ctx.Events.Raise(new ClashEvent { attacker = a.Id, defender = d.Id, attackId = rtA.currentAttackId, point = hit.point, time = time });
        }
    }
}
