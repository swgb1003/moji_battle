using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    public struct HitContact
    {
        public Fighter attacker, target;
        public bool targetIsWeapon;
        public bool bodyAttack;
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
        /// <summary>二刀流の副武器の当たり（主武器とは別に、1 スイング 1 回）。</summary>
        public readonly AttackLedger OffhandLedger = new AttackLedger();
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

        /// <summary>弾の攻撃ID・技を使う。持ち主が硬直を終えたり、別の技を始めても一発分の判定は変わらない。</summary>
        /// <summary>
        /// 必殺技の必中: 技の一段が体に当たらないまま終わる時に、その段の当たりを胴への一撃として確定させる。
        /// ガード・受け流し・字形のすり抜けに関係なく通常の当たりと同じ処理（ダメージ・吹っ飛び・打ち上げの連携）を行う。
        /// </summary>
        public void ForceSpecialHit(Fighter a, Fighter d, float time)
        {
            var rtA = a.Runtime;
            if (ctx.MatchOver || d.Runtime.state == FighterState.KO) return;
            var b = ctx.Balance;
            var style = rtA.attackStyle;
            var tech = AttackTechniques.Tuning(style, b);
            bool kick = AttackTechniques.IsKick(style);
            float styleDamage = (tech != null ? tech.damage : 1f) * (kick ? b.kickDamage : 1f);
            float vN = Mathf.Max(b.smashHitSpeed, b.minHitRelativeSpeed * 1.5f);
            float damage = DamageMath.BodyDamage(a.Stats.attack, d.Stats.defense, vN, HitQuality.Normal, BodyPart.Torso, b)
                           * a.Mods.damage * styleDamage * ctx.Interventions.DamageScale(a, rtA.currentAttackId, time);
            Vector2 normal = new Vector2(a.TowardOpponent, .15f).normalized;
            var hit = new HitContact { attacker = a, target = d, part = BodyPart.Torso, bodyAttack = kick,
                point = d.ChestWorld - new Vector2(a.TowardOpponent * .15f, 0f), normal = normal, relativeVelocity = normal * vN };
            ApplyBodyHit(a, d, hit, new ContactCandidate { vN = vN, speed = vN, damage = damage }, HitQuality.Normal, time);
        }

        public void ResolveRanged(RangedProjectile p, Fighter d, Vector2 point, BodyPart part, float time)
        {
            if (ctx.MatchOver || Ledger.IsResolved(p.AttackId, d.Id) || d.Runtime.state == FighterState.KO) return;
            Ledger.MarkResolved(p.AttackId, d.Id);
            var a = p.Owner;
            var b = ctx.Balance;
            var rd = d.Runtime;
            bool wave = p.Style == AttackStyle.GroundWave;
            var tuning = AttackTechniques.Tuning(p.Style, b);
            float speed = wave ? 6f : 4f;
            float load = speed * a.WeaponBody.mass * b.guardLoadPerMomentum * .35f;
            bool frontal = d.Facing * p.VelocityX < 0f;
            float guardPsi = float.IsNaN(rd.guardPsiTarget) ? b.guardPsi : rd.guardPsiTarget;
            bool guarded = rd.state == FighterState.Guard && frontal && (!wave || guardPsi <= 50f);
            if (a.Runtime.currentAttackId == p.AttackId) a.Runtime.attackHadContact = true;
            if (guarded)
            {
                rd.guardLoad += load;
                rd.metrics.guards++;
                bool broke = rd.guardLoad >= b.guardBreakThreshold * d.Mods.guardStability;
                if (broke) { rd.guardLoad = 0f; rd.metrics.guardBreaks++; d.EnterStagger(b.guardBreakStagger); }
                ctx.Events.Raise(new GuardEvent { attacker = a.Id, defender = d.Id, attackId = p.AttackId,
                    load = load, vN = speed, point = point, broke = broke, time = time });
                return;
            }
            var before = rd.state;
            float damage = DamageMath.CapHit(DamageMath.BodyDamage(a.Stats.attack, d.Stats.defense,
                speed, HitQuality.Normal, part, b) * a.Mods.damage * tuning.damage * GlyphMass(a, a.WeaponBody.mass, b)
                * (ctx.Interventions?.DamageScale(a, p.AttackId, time) ?? 1f), rd.maxHp, b);
            damage = Mathf.Min(damage, rd.maxHp * (wave ? .22f : .15f) * (ctx.Interventions?.DamageScale(a, p.AttackId, time) ?? 1f));
            if (ctx.Interventions != null) damage = ctx.Interventions.ApplySpecialLimits(a, p.AttackId, d, damage);
            rd.ApplyDamage(damage);
            a.Runtime.metrics.damageDealt += damage;
            a.Runtime.metrics.hitsLanded++;
            a.Runtime.metrics.partHits[(int)part]++;
            if (part == BodyPart.Head) a.Runtime.metrics.criticals++;
            ctx.LastDamageTime = time;
            float impulse = DamageMath.Impulse(speed, a.WeaponBody.mass, d.Body.mass, b) * tuning.impulse;
            bool stagger = !rd.IsDown && !AbsorbHeavyArmor(a, d, before, damage, b, out _);
            if (rd.hp <= 0f) d.Knockdown.EnterKO(time);
            else if (stagger) d.EnterStagger(wave ? .25f : .12f);
            if (!rd.IsDown)
                d.AddVelocity(new Vector2(Mathf.Sign(p.VelocityX) * (wave ? 1.7f : .7f) / d.Mods.knockbackResistance, 0f));
            ctx.Events.Raise(new HitEvent { attacker = a.Id, defender = d.Id, attackId = p.AttackId,
                style = p.Style, part = part, quality = HitQuality.Normal, damage = damage, vN = speed,
                impulse = impulse, point = point, stagger = stagger, knockdown = rd.hp <= 0f,
                critical = part == BodyPart.Head,
                defenderStateBefore = before, time = time });
        }

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
            bool kick = AttackTechniques.IsKick(rtA.attackStyle);
            if (kick) scratch.RemoveAll(contact => !contact.bodyAttack);
            if (scratch.Count == 0) return;
            // 攻撃窓外の接触は物理衝突のみ（ダメージなし）。攻撃窓 = 振り・振り抜き・投げた武器の飛行中
            if (!a.InAttackWindow && !(kick && rtA.state == FighterState.AttackActive)) { Diag[attackerId, 0]++; return; }
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
                if (c.bodyAttack) va = c.relativeVelocity + d.PreBodyVelocity;
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
                if (rtA.attackStyle == AttackStyle.HookPull && c.targetIsWeapon && a.Actions.IsHookContact(c.point))
                    vN = Mathf.Max(vN, b.minHitRelativeSpeed); // A caught stroke can be pulled even when the collision is slow.
                var cand = new ContactCandidate { isWeapon = c.targetIsWeapon, vN = vN, speed = c.targetIsWeapon ? 0f : Mathf.Max(vN, glancing) };
                if (rtA.attackStyle == AttackStyle.HookPull && !c.targetIsWeapon) { candidates.Add(default); continue; }
                // 横持ちはガードの正面角が広い
                if (c.targetIsWeapon) cand.frontal = IsFrontal(d, c.point, Mathf.Min(180f, b.guardHalfAngle * d.Mods.guardRange));
                else
                {
                    qualities[i] = smashHit ? HitQuality.Normal : ClassifyQuality(a, c.point, n, va, b);
                    float styleDamage = tech != null ? tech.damage : 1f;
                    if (kick) styleDamage *= b.kickDamage;
                    // 刺す: 先端の直撃は中央の当たり（大ダメージ）、柄や側面での接触は弱い
                    if (rtA.attackStyle == AttackStyle.Thrust)
                    {
                        bool tip = IsStabTip(a, c.point, b);
                        qualities[i] = tip ? HitQuality.Center : HitQuality.Normal;
                        styleDamage *= tip ? b.stabTipDamage * (1f + b.precisionTipBonus * StatCalculator.Precision(a.Glyph)) : b.stabShaftDamage;
                    }
                    // サイズの威力は質量を通じてだけ（Mods.damage = 質量倍率^指数）
                    cand.damage = DamageMath.BodyDamage(a.Stats.attack, d.Stats.defense, vN, qualities[i], c.part, b) * a.Mods.damage * styleDamage
                                  * (kick ? 1f : GlyphMass(a, a.WeaponBody.mass, b))
                                  * (ctx.Interventions?.DamageScale(a, time) ?? 1f);
                }
                candidates.Add(cand);
            }

            bool anyBody = false, anyWeapon = false, anyFast = false;
            foreach (var c in candidates) { if (c.isWeapon) anyWeapon = true; else anyBody = true; if (c.GateSpeed >= b.minHitRelativeSpeed) anyFast = true; }
            if (anyBody) Diag[attackerId, 2]++;
            if (anyWeapon) Diag[attackerId, 3]++;
            if (!anyFast) Diag[attackerId, 4]++;
            // A balanced weapon lock intercepts the swing before simultaneous body contacts,
            // just as a successful guard does. One collider callback cannot leak damage through it.
            if (!resolved && !kick && rtA.attackStyle != AttackStyle.HookPull)
                for (int i = 0; i < scratch.Count; i++)
                    if (scratch[i].targetIsWeapon && candidates[i].GateSpeed >= b.minHitRelativeSpeed
                        && a.Actions.TryBind(d, scratch[i].point, CombatMomentum(a, scratch[i].point), CombatMomentum(d, scratch[i].point), time))
                    {
                        ctx.Events.Raise(new ClashEvent { attacker = a.Id, defender = d.Id,
                            attackId = rtA.currentAttackId, point = scratch[i].point, time = time });
                        return;
                    }
            var kind = Choose(candidates, resolved, guarding, b.minHitRelativeSpeed, out int idx);
            if (kind == HitOutcomeKind.None) return;
            var hit = scratch[idx];
            var chosen = candidates[idx];
            if (rtA.attackStyle == AttackStyle.HookPull)
            {
                if (hit.targetIsWeapon) a.Actions.TryHook(d, hit.point, time);
                return;
            }
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
            bool special = ctx.Interventions?.IsSpecial(a) == true;
            // 必殺の一文字は AI の受け流しでは止まらない（観戦者のジャストガードだけが受け流せる）
            if (!hit.bodyAttack && !special && d.CanParry(a, time))
            {
                ApplyParry(a, d, hit, vN, time);
                return;
            }
            float load = vN * ImpactMass(a) * b.guardLoadPerMomentum;
            if (rtA.attackStyle == AttackStyle.Bash) load *= b.bashGuardLoad; // 盾当てはガードごと押し込んで崩す
            if (special) load = Mathf.Max(load, b.guardBreakThreshold * d.Mods.guardStability - rtD.guardLoad + 1f); // 必殺はガードを崩す
            rtD.guardLoad += load;
            rtD.metrics.guards++;
            // 持ち方のガード安定性: 崩れにくく、押し込まれにくい
            float imp = DamageMath.Impulse(vN, ImpactMass(a), d.Body.mass, b, ImpulseCap(a)) * b.guardImpulseRatio / d.Mods.guardStability;
            Vector2 dir = KnockDirection(a, d, hit.normal, 0.1f);
            d.Body.AddForce(dir * imp * b.impulseScale * KnockbackForce(a), ForceMode2D.Impulse);
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
            // 先端の突きは重さに関係なくガードの隙間を一部貫く。疎で細長い字形ほど精密。
            if (!broke && rtA.attackStyle == AttackStyle.Thrust && IsStabTip(a, hit.point, b) && vN >= b.minHitRelativeSpeed)
            {
                float pierce = Mathf.Clamp01(b.stabGuardPierce + b.precisionGuardPierceBonus * StatCalculator.Precision(a.Glyph));
                float chip = DamageMath.CapHit(DamageMath.BodyDamage(a.Stats.attack, d.Stats.defense, vN, HitQuality.Normal, BodyPart.Torso, b) * a.Mods.damage * pierce, rtD.maxHp, b);
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

        /// <summary>受け流し: この振りは本体に届かず、攻撃側はよろけ、受けた側が反撃する。</summary>
        void ApplyParry(Fighter a, Fighter d, HitContact hit, float vN, float time)
        {
            var b = ctx.Balance;
            var rtA = a.Runtime;
            rtA.attackHadContact = true;
            // Resolve this attack before changing its state; no body damage or
            // repeated counter can escape the one-swing ledger.
            Ledger.MarkResolved(rtA.currentAttackId, d.Id);
            d.Runtime.metrics.guards++;
            d.ScheduleRiposte(time);
            a.WeaponBody.angularVelocity *= 0.25f;
            Vector2 deflect = new Vector2(a.X >= d.X ? 1f : -1f, 0.25f).normalized;
            a.WeaponBody.AddForceAtPosition(deflect * Mathf.Min(vN, 6f) * a.WeaponBody.mass * 0.08f,
                hit.point, ForceMode2D.Impulse);
            a.EnterStagger(b.parryAttackerStagger);
            ctx.Events.Raise(new GuardEvent { attacker = a.Id, defender = d.Id, attackId = rtA.currentAttackId,
                point = hit.point, vN = vN, time = time, parried = true });
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
            // 二刀流: 同じ一撃で副武器が先に当たっていれば、2 本目の当たりとして威力を抑える
            if (a.Mods.dualWield && OffhandLedger.IsResolved(rtA.currentAttackId, d.Id)) dmg *= DualSecondHit;
            if (rtA.aerialStage == 1 && rtA.attackStyle == AttackStyle.Rising) dmg *= b.aerialSlashDamage;
            if (ctx.Interventions != null) dmg = ctx.Interventions.ApplySpecialLimits(a, rtA.currentAttackId, d, dmg);
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
            float impulse = DamageMath.Impulse(c.vN, ImpactMass(a), d.Body.mass, b, ImpulseCap(a)) / d.Mods.knockbackResistance
                            * (tech != null ? tech.impulse : 1f)
                            * (hit.bodyAttack ? 1f : GlyphMass(a, a.WeaponBody.mass, b)); // 重い字形ほど一撃で大きく揺さぶる
            // 足払いは脚を払う（どこに当たっても転倒の判定は脚扱い）
            bool leg = hit.part == BodyPart.Leg || rtA.attackStyle == AttackStyle.LowSweep;
            rtD.knockdownAccum += impulse * b.knockdownAccumPerImpulse + (leg ? b.legKnockdownBonus : 0f);
            bool knock = impulse >= b.knockdownImpulse || (leg && impulse >= b.legKnockdownImpulse) || rtD.knockdownAccum >= b.knockdownAccumThreshold;
            bool launch = impulse >= b.launchImpulse;
            bool armored = AbsorbHeavyArmor(a, d, stateBefore, dmg, b, out bool armorBroken);
            bool stagger = impulse >= b.staggerImpulse || armorBroken;

            if (rtD.hp <= 0f) d.Knockdown.EnterKO(time);
            else if (knock) d.Knockdown.EnterKnockdown(time);
            // 重量級のスーパーアーマー: 溜め・振りの最中は、ずっと軽い武器の転倒に至らない打撃ではひるまずに振り切る（受けながら一撃）
            else if (stagger && !rtD.IsDown && !armored) d.EnterStagger(b.staggerDuration);
            if (launch) d.MarkLaunched(time);

            Vector2 dir = KnockDirection(a, d, hit.normal, tech != null && tech.upBias >= 0f ? tech.upBias : b.launchUpBias);
            // 大型武器は判定（ひるみ・転倒）を変えずに、飛ぶ距離だけ伸ばす
            Vector2 force = dir * impulse * b.impulseScale * KnockbackForce(a);
            if (hit.bodyAttack && !rtD.IsDown)
            {
                force = Vector2.zero;
                d.AddVelocity(new Vector2(a.TowardOpponent * b.kickPushSpeed / d.Mods.knockbackResistance - d.Body.linearVelocity.x, 0f));
                if (!armored && !rtD.IsDown) d.EnterStagger(armorBroken ? b.staggerDuration : .2f);
            }
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
                stagger = stagger && !armored, armored = armored && !knock && rtD.hp > 0f, launch = launch, knockdown = knock || rtD.hp <= 0f, time = time,
                defenderStateBefore = stateBefore, style = rtA.attackStyle, combo = rtA.attackStyle == AttackStyle.Smash,
            });
        }

        /// <summary>
        /// 字形の重さの威力（カスタマイズなしの選手のみ）。カスタマイズした選手は、選んだサイズの重さが
        /// 与ダメージ倍率（Mods.damage）と慣性に入っているので掛けない（二重にしない）。
        /// </summary>
        static float GlyphMass(Fighter a, float weaponMass, CombatBalance b) =>
            a.Mods.customized ? 1f : DamageMath.MassFactor(weaponMass, b);

        float DualSecondHit => (ctx.Customize != null ? ctx.Customize : CustomizeBalance.Default).dualSecondHitDamage;

        public bool OffhandResolved(int attackId, int defenderId) => OffhandLedger.IsResolved(attackId, defenderId);

        /// <summary>
        /// 二刀流の副武器が相手の体に重なった。速さが足りなければ当たりにしない（true を返さない）。
        /// 正面からのガードは受け止められる。主武器が先に当たっていれば 2 本目として威力を抑える。
        /// 副武器は小さな追い打ちなので、吹き飛ばし・打ち上げ・叩き落としの連携は主武器だけが起こす。
        /// </summary>
        public bool ResolveOffhand(Fighter a, Fighter d, BodyPart part, Vector2 point, Vector2 velocity, float time)
        {
            var rtA = a.Runtime;
            var rtD = d.Runtime;
            var b = ctx.Balance;
            if (ctx.MatchOver || rtD.state == FighterState.KO || OffhandLedger.IsResolved(rtA.currentAttackId, d.Id)) return false;
            var style = rtA.attackStyle;
            if (AttackTechniques.IsKick(style) || AttackTechniques.IsRanged(style) || style == AttackStyle.HookPull || style == AttackStyle.Throw) return false;
            float speed = (velocity - d.PreBodyVelocity).magnitude;
            if (style == AttackStyle.Smash) speed = Mathf.Max(speed, b.smashHitSpeed);
            if (speed < b.minHitRelativeSpeed) return false;
            OffhandLedger.MarkResolved(rtA.currentAttackId, d.Id);
            rtA.attackHadContact = true;
            if (rtD.state == FighterState.Guard && IsFrontal(d, point, Mathf.Min(180f, b.guardHalfAngle * d.Mods.guardRange)))
            {
                ctx.Events.Raise(new GuardEvent { attacker = a.Id, defender = d.Id, attackId = rtA.currentAttackId,
                    load = 0f, vN = speed, point = point, time = time });
                return true;
            }
            var tech = AttackTechniques.Tuning(style, b);
            float dmg = DamageMath.BodyDamage(a.Stats.attack, d.Stats.defense, speed, HitQuality.Normal, part, b)
                        * a.Mods.damage * (tech != null ? tech.damage : 1f) * (ctx.Interventions?.DamageScale(a, time) ?? 1f)
                        * GlyphMass(a, a.Offhand.Mass, b);
            dmg = DamageMath.CapHit(dmg, rtD.maxHp, b);
            if (Ledger.IsResolved(rtA.currentAttackId, d.Id)) dmg *= DualSecondHit;
            if (ctx.Interventions != null) dmg = ctx.Interventions.ApplySpecialLimits(a, rtA.currentAttackId, d, dmg);
            var stateBefore = rtD.state;
            rtD.ApplyDamage(dmg);
            rtA.metrics.damageDealt += dmg;
            rtA.metrics.hitsLanded++;
            rtA.metrics.partHits[(int)part]++;
            if (part == BodyPart.Head) rtA.metrics.criticals++;
            ctx.LastDamageTime = time;
            float impulse = DamageMath.Impulse(speed, a.Offhand.Mass, d.Body.mass, b) / d.Mods.knockbackResistance * .5f;
            bool armored = AbsorbHeavyArmor(a, d, stateBefore, dmg, b, out bool armorBroken);
            bool stagger = (impulse >= b.staggerImpulse || armorBroken) && !armored;
            if (rtD.hp <= 0f) d.Knockdown.EnterKO(time);
            else if (stagger && !rtD.IsDown) d.EnterStagger(b.staggerDuration * .6f);
            ctx.Events.Raise(new HitEvent
            {
                attacker = a.Id, defender = d.Id, attackId = rtA.currentAttackId,
                part = part, quality = HitQuality.Normal, damage = dmg, vN = speed, impulse = impulse,
                point = point, critical = part == BodyPart.Head, stagger = stagger, knockdown = rtD.hp <= 0f, time = time,
                defenderStateBefore = stateBefore, style = style, offhand = true,
            });
            return true;
        }

        /// <summary>
        /// スーパーアーマー: 溜め・振りの最中、ずっと軽い武器の打撃ではひるまない。重量級の字形に加え、
        /// L・XL の武器は自分より軽い武器（サイズごとの割合）に対して踏みとどまる。
        /// </summary>
        static bool AbsorbHeavyArmor(Fighter a, Fighter d, FighterState defenderState, float damage, CombatBalance b, out bool broken)
        {
            broken = false;
            if (!b.heavyArmorDuringAttack || (defenderState != FighterState.AttackWindup && defenderState != FighterState.AttackActive)) return false;
            float ratio = Mathf.Max(d.WeightClass == WeightClass.Heavy ? b.heavyArmorMassRatio : 0f, d.Mods.armorMassRatio);
            if (ratio <= 0f || a.WeaponBody.mass >= d.WeaponBody.mass * ratio) return false;
            d.Runtime.armorPressure += damage;
            broken = d.Runtime.armorPressure >= d.Runtime.maxHp * b.heavyArmorBreakHpFraction;
            return !broken;
        }

        /// <summary>大型武器の振りは吹き飛ばしの上限を引き上げる（蹴りは本体なので対象外）。</summary>
        static float ImpulseCap(Fighter a) => AttackTechniques.IsKick(a.Runtime.attackStyle) ? 1f : a.Mods.impulseCap;
        static float KnockbackForce(Fighter a) => AttackTechniques.IsKick(a.Runtime.attackStyle) ? 1f : a.Mods.knockbackForce;

        static float ImpactMass(Fighter a) => AttackTechniques.IsKick(a.Runtime.attackStyle) ? a.Body.mass * .6f : a.WeaponBody.mass;

        static float CombatMomentum(Fighter f, Vector2 point)
        {
            float speed = f.PreWeaponPointVelocity(point).magnitude;
            if (AttackTechniques.IsYaw(f.Runtime.attackStyle))
                speed = Mathf.Max(speed, Mathf.Min(Mathf.Abs(f.Runtime.sweepYawRate), f.Balance.sweepMaxYawSpeed)
                    * Mathf.Deg2Rad * Mathf.Abs(f.WeaponLocalPoint(point).x) * f.Balance.sweepSpeedScale);
            return f.WeaponBody.mass * speed;
        }

        void ApplyClash(Fighter a, Fighter d, HitContact hit, float vN, float time)
        {
            var rtA = a.Runtime;
            var b = ctx.Balance;
            float mA = a.WeaponBody.mass, mD = d.WeaponBody.mass;
            var lighter = mA <= mD ? a : d;
            var heavier = lighter == a ? d : a;
            // 勢い（質量×接触点の速度）で上回る振りは、受けた武器を振りの方向へ叩き落としてそのまま振り抜く。
            // 構えているだけの武器は勢いがほぼ 0 なので押し退けられる。受け止めたいならガード姿勢（ガード判定）が必要。
            Vector2 va = a.PreWeaponPointVelocity(hit.point);
            float momentumA = CombatMomentum(a, hit.point), momentumD = CombatMomentum(d, hit.point);
            if (a.Actions.TryBind(d, hit.point, momentumA, momentumD, time))
            {
                ctx.Events.Raise(new ClashEvent { attacker = a.Id, defender = d.Id, attackId = rtA.currentAttackId, point = hit.point, time = time });
                return;
            }
            if (rtA.clashedThisAttack) return;
            rtA.clashedThisAttack = true;
            rtA.attackHadContact = true;
            rtA.metrics.clashes++;
            bool overpower = momentumA >= b.clashMinMomentum && momentumA >= momentumD * b.overpowerMomentumRatio;
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
            bool defenderWins = !overpower && momentumD >= b.clashMinMomentum && momentumD >= momentumA * b.overpowerMomentumRatio;
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
