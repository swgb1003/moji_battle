using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    public struct EnvContact
    {
        public Fighter fighter;
        public bool isWall;
        public Vector2 point, normal, relativeVelocity;
    }

    /// <summary>
    /// 壁・地面への追加ダメージ（8.3）。法線相対速度 7 以上、かつ直前 1.5 秒以内に相手の攻撃で吹っ飛んだ場合のみ。
    /// 同一吹っ飛びにつき壁1回・地面1回まで。通常歩行や起き上がりではダメージなし。
    /// </summary>
    public sealed class EnvironmentImpactResolver
    {
        readonly MatchContext ctx;
        readonly List<EnvContact> queue = new List<EnvContact>(16);

        public EnvironmentImpactResolver(MatchContext ctx) { this.ctx = ctx; }

        public void Report(EnvContact c) => queue.Add(c);

        public void Discard() => queue.Clear();

        public void ResolveStep(float time)
        {
            if (queue.Count == 0) return;
            queue.Sort((a, b) =>
            {
                int c = a.fighter.Id.CompareTo(b.fighter.Id);
                return c != 0 ? c : a.isWall.CompareTo(b.isWall);
            });
            var b = ctx.Balance;
            foreach (var c in queue)
            {
                var f = c.fighter;
                var rt = f.Runtime;
                Vector2 n = c.normal.sqrMagnitude > 1e-6f ? c.normal.normalized : Vector2.up;
                float vN = Mathf.Max(Mathf.Abs(Vector2.Dot(c.relativeVelocity, n)), Mathf.Abs(Vector2.Dot(f.PreBodyVelocity, n)));
                bool used = c.isWall ? rt.launchWallUsed : rt.launchGroundUsed;
                bool launchHit = DamageMath.EnvironmentDamageAllowed(vN, time, rt.launchedAt, used, b);
                // 仕様拡張: 相手の武器で持ち上げられて落とされた（叩きつけ）
                // 仕様拡張: 相手の武器で持ち上げられて（足が liftMinHeight 以上に達して）落とされた（叩きつけ）
                bool slam = rt.liftPeakY >= b.liftMinHeight && DamageMath.SlamAllowed(vN, time, rt.liftedAt, rt.liftActive, rt.slamUsed, b);
                if (!launchHit && !slam) continue;
                float dmg = 0f;
                if (launchHit)
                {
                    if (c.isWall) rt.launchWallUsed = true; else rt.launchGroundUsed = true;
                    dmg = DamageMath.EnvironmentDamage(vN, b);
                }
                if (slam)
                {
                    rt.slamUsed = true;
                    float slamDmg = DamageMath.SlamDamage(vN, b);
                    if (slamDmg > dmg) dmg = slamDmg; else slam = false;
                }
                if (dmg <= 0f) continue;
                // 叩き落とされて地面に激突したら一度だけ跳ねて倒れる
                bool smash = !c.isWall && time - rt.smashedAt <= b.smashBounceWindow;
                if (smash)
                {
                    rt.smashedAt = -999f;
                    var v = f.Body.linearVelocity;
                    f.AddVelocity(new Vector2(v.x * 0.5f, b.smashBounceSpeed) - v);
                }
                if (slam) rt.metrics.slamsTaken++;
                rt.ApplyDamage(dmg);
                rt.metrics.envDamageTaken += dmg;
                rt.metrics.envImpacts++;
                f.Opponent.Runtime.metrics.damageDealt += dmg;
                rt.knockdownAccum += vN * b.envAccumPerSpeed;
                ctx.LastDamageTime = time;
                if (rt.hp <= 0f) f.Knockdown.EnterKO(time);
                else if (!rt.IsDown && (smash || rt.knockdownAccum >= b.knockdownAccumThreshold)) f.Knockdown.EnterKnockdown(time);
                ctx.Events.Raise(new EnvImpactEvent
                {
                    fighter = f.Id, isWall = c.isWall, vN = vN, damage = dmg,
                    timeSinceLaunch = slam ? time - rt.liftedAt : time - rt.launchedAt, point = c.point, time = time,
                    slam = slam,
                    smash = smash,
                });
            }
            queue.Clear();
        }
    }
}
