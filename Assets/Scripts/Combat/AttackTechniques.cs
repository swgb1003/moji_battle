using System;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>技ごとの調整値（選びやすさ・時間・威力・衝撃）。</summary>
    [Serializable]
    public class TechniqueTuning
    {
        [Tooltip("選ぶ重み（状況・持ち方・スタイルでさらに増減）")] public float weight = 0.1f;
        [Tooltip("溜め / 振り / 硬直の時間倍率")] public float windup = 1f, active = 1f, recovery = 1f;
        [Tooltip("与ダメージ倍率")] public float damage = 1f;
        [Tooltip("衝撃（吹っ飛び・よろけ・転倒の判定と押す力）の倍率")] public float impulse = 1f;
        [Tooltip("吹っ飛ばす向きの上向き成分（既定は launchUpBias）。負なら既定")] public float upBias = -1f;
    }

    /// <summary>
    /// 攻撃の技（振り下ろし・斬り上げ・刺す・横振り・足払い・盾タックル・打ち上げ・回転斬り）の分類と選び方。
    /// 基本の 3 つ（振り下ろし / 斬り上げ / 刺す）を相手の構えで選んだ後、状況に合う技へ置き換えることがある。
    /// 置き換えの合計確率は上限付き（技が増えても基本の振りが主体のまま）。
    /// </summary>
    public static class AttackTechniques
    {
        /// <summary>字形を縦軸まわりに回す（奥行き方向に振る）技。正面の盾を回り込み、反応して構えたガードでしか止まらない。</summary>
        public static bool IsYaw(AttackStyle s) => s == AttackStyle.Sweep || s == AttackStyle.LowSweep || s == AttackStyle.Spin;

        public static string Label(AttackStyle s)
        {
            switch (s)
            {
                case AttackStyle.Thrust: return "刺す";
                case AttackStyle.Sweep: return "横薙ぎ";
                case AttackStyle.LowSweep: return "足払い";
                case AttackStyle.Bash: return "盾当て";
                case AttackStyle.Launch: return "打ち上げ";
                case AttackStyle.Spin: return "回転斬り";
                case AttackStyle.Rising: return "斬り上げ";
                case AttackStyle.Throw: return "投げ";
                default: return "振り下ろし";
            }
        }

        public static TechniqueTuning Tuning(AttackStyle s, CombatBalance b)
        {
            switch (s)
            {
                case AttackStyle.Thrust: return b.stab;
                case AttackStyle.Sweep: return b.sweep;
                case AttackStyle.LowSweep: return b.lowSweep;
                case AttackStyle.Bash: return b.bash;
                case AttackStyle.Launch: return b.launch;
                case AttackStyle.Spin: return b.spin;
                case AttackStyle.Throw: return b.throwAttack;
                default: return null;
            }
        }

        /// <summary>
        /// 状況に合う技を 1 つ選ぶ（null なら基本の振りのまま）。乱数は 1 回だけ使う。
        /// </summary>
        public static AttackStyle? ChooseSpecial(Fighter f, float roll)
        {
            var b = f.Balance;
            var o = f.Opponent;
            var ort = o.Runtime;
            float d = f.DistanceToOpponent;
            bool oppGuarding = ort.state == FighterState.Guard;
            bool oppBehind = f.TowardOpponent != f.Facing;
            var mods = f.Mods;

            // 横薙ぎ: 構えて待つ相手（先に構えた盾）・大きな字形の相手に
            float wSweep = b.sweep.weight;
            if (oppGuarding && !ort.guardAgainstSweep) wSweep = Mathf.Max(wSweep, b.sweepChanceVsGuard);
            if (o.Weapon.maxSide > b.weaponMaxSide * 1.1f) wSweep += b.sweepChanceBigWeaponBonus;

            // 足払い: 上段に構える相手・跳び越えの苦手な重量級に。屈んで払う間合い（射程の半分以上）が要る
            float wLow = d >= f.AttackRange * 0.5f ? b.lowSweep.weight : 0f;
            if (wLow > 0f && oppGuarding && !float.IsNaN(ort.guardPsiTarget) && ort.guardPsiTarget > 60f) wLow += 0.15f;
            if (wLow > 0f && o.WeightClass == WeightClass.Heavy) wLow += 0.08f;

            // 盾当て: 壁を背にした相手・構えて待つ相手に。大きな字形ほど使いやすい。間合いが近い時だけ
            float wBash = d <= f.AttackRange * 0.9f ? b.bash.weight : 0f;
            if (wBash > 0f)
            {
                if (o.BackSpace < b.bashWallDistance) wBash += 0.2f;
                if (oppGuarding) wBash += 0.12f;
                wBash *= Mathf.Clamp(f.Weapon.maxSide / b.weaponMaxSide, 0.5f, 1.6f);
            }

            // 打ち上げ: 近い相手に。重い・両手持ちほど使いやすい（浮かせて叩きつけ・追撃へ）
            float wLaunch = d <= f.AttackRange ? b.launch.weight : 0f;
            if (wLaunch > 0f && f.WeightClass != WeightClass.Light) wLaunch += 0.04f;

            // 回転斬り: 背後を取られた・密着された時に
            float wSpin = b.spin.weight;
            if (oppBehind) wSpin += 0.35f;
            if (d < f.AttackRange * 0.5f) wSpin += 0.1f;

            // カスタマイズの持ち方・戦い方との相性
            if (mods.customized)
            {
                if (mods.grip == GripType.Horizontal) wBash *= 2f;
                if (mods.grip == GripType.TwoHanded) wLaunch *= 2f;
                if (mods.grip == GripType.Reverse) { wSpin *= 1.5f; wLow *= 1.3f; }
                if (mods.style == BattleStyle.Defensive) wBash *= 1.5f;
                if (mods.style == BattleStyle.Aggressive) wSpin *= 1.3f;
                if (mods.style == BattleStyle.HitAndAway) wLow *= 1.3f;
            }

            float total = wSweep + wLow + wBash + wLaunch + wSpin;
            if (total <= 0f) return null;
            // 置き換える確率の上限（基本の振りを主体に保つ）。足し合わせた重みがそのまま確率、超えたら比で割り振る
            float scale = total > b.specialAttackCap ? b.specialAttackCap / total : 1f;
            float acc = 0f;
            if (roll < (acc += wSweep * scale)) return AttackStyle.Sweep;
            if (roll < (acc += wLow * scale)) return AttackStyle.LowSweep;
            if (roll < (acc += wBash * scale)) return AttackStyle.Bash;
            if (roll < (acc += wLaunch * scale)) return AttackStyle.Launch;
            if (roll < (acc += wSpin * scale)) return AttackStyle.Spin;
            return null;
        }
    }
}
