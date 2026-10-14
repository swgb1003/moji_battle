using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>自操作で出せる技（操作説明・入力表示に使う）。</summary>
    public enum PlayerCommand
    {
        None, Overhead, Rising, Thrust, LowSweep, Sweep, Bash, Launch, DashSlash, Whirl, Spin, HookPull,
        Ranged, Leap, Throw, FrontKick, Feint, HopOver, WallJump, Duck, Backstep, Guard, Parry,
        Riposte, DuckCounter, PickupSlash, RecoverKick, RecoverRoll, AerialChase, Smash, BindCounter
    }

    /// <summary>
    /// 自操作: キーボードの入力を、AI と同じ行動（攻撃・回避・ガード・移動の意図）へ置き換える。力やトルクは加えない。
    /// 方向は向いている側が「前」。↑・↓ は押した直後に技のボタンが続けば方向として扱い、続かなければ跳ぶ・しゃがむ。
    /// 動けない間（溜め・硬直・回避中）に押した技は少しの間だけ覚えておき、動けるようになった瞬間に出す。
    /// </summary>
    public sealed class PlayerController
    {
        /// <summary>動けない間に押した技は、動けるようになってからこの秒数まで覚えておく（押してから最長 MaxBufferAge 秒）</summary>
        public const float BufferSeconds = .25f, MaxBufferAge = 1f;
        /// <summary>自操作の攻撃の後の待ち時間の倍率（入力に素早く応える）</summary>
        public const float CooldownScale = .5f;
        /// <summary>↑・↓ の後に技のボタンを押せば方向として扱う猶予</summary>
        public const float ModifierGrace = .07f;
        /// <summary>受け流しの受付: 当たるまでの時間がこれ以内で C を押すと受け流す</summary>
        public const float ParryLead = .3f;
        /// <summary>武器を拾ってから拾い斬りを出せる時間</summary>
        public const float PickupSlashWindow = .6f;

        struct Buffered { public PlayerPress press; public float time; }

        readonly Fighter self;
        public readonly PlayerInput Input = new PlayerInput();
        readonly List<Buffered> buffer = new List<Buffered>();
        float jumpAt = -1f, duckAt = -1f, parryPressAt = -1f, pickedUpAt = -1f;
        float lastSlashAt = -1f, lastKickAt = -1f, lastBusyAt = -1f;

        /// <summary>直前に出した技（入力表示用）</summary>
        public PlayerCommand LastCommand { get; private set; }
        public float LastCommandAt { get; private set; } = -1f;
        /// <summary>出せなかった理由（入力表示用。空なら無し）</summary>
        public string LastRejection { get; private set; } = "";
        public float LastRejectionAt { get; private set; } = -1f;

        public PlayerController(Fighter self) { this.self = self; }

        /// <summary>入力表示: 押した操作と出た技。</summary>
        public static string Label(PlayerCommand c)
        {
            switch (c)
            {
                case PlayerCommand.Overhead: return "Z　振り下ろし";
                case PlayerCommand.Rising: return "↑＋Z　斬り上げ";
                case PlayerCommand.Thrust: return "前＋Z　突き";
                case PlayerCommand.LowSweep: return "↓＋Z　足払い";
                case PlayerCommand.Sweep: return "後＋Z　横薙ぎ";
                case PlayerCommand.Bash: return "X　盾当て";
                case PlayerCommand.Launch: return "↑＋X　打ち上げ";
                case PlayerCommand.DashSlash: return "前＋X　踏込斬り";
                case PlayerCommand.Whirl: return "前＋X　全身ぶん回し";
                case PlayerCommand.Spin: return "↓＋X　回転斬り";
                case PlayerCommand.HookPull: return "後＋X　引き崩し";
                case PlayerCommand.Ranged: return "A　飛び道具";
                case PlayerCommand.Leap: return "前＋A　跳び斬り";
                case PlayerCommand.Throw: return "↓＋A　投げ";
                case PlayerCommand.FrontKick: return "V　前蹴り";
                case PlayerCommand.Feint: return "後＋V　フェイント";
                case PlayerCommand.HopOver: return "↑　跳び越え";
                case PlayerCommand.WallJump: return "↑　壁蹴り";
                case PlayerCommand.Duck: return "↓　しゃがみ";
                case PlayerCommand.Backstep: return "後後　バックステップ";
                case PlayerCommand.Guard: return "C　ガード";
                case PlayerCommand.Parry: return "C　受け流し";
                case PlayerCommand.Riposte: return "Z　受け流し反撃";
                case PlayerCommand.DuckCounter: return "Z　斬り返し";
                case PlayerCommand.PickupSlash: return "Z　拾い斬り";
                case PlayerCommand.RecoverKick: return "V　起き上がり蹴り";
                case PlayerCommand.RecoverRoll: return "後　後転復帰";
                case PlayerCommand.AerialChase: return "Z　空中追撃";
                case PlayerCommand.Smash: return "Z　叩き落とし";
                case PlayerCommand.BindCounter: return "Z　崩し反撃";
                default: return "";
            }
        }

        FighterRuntime Rt => self.Runtime;
        CombatBalance B => self.Balance;

        /// <summary>試合が動いていない間（カウントダウン・決着後）は入力を捨てる。</summary>
        public void Drain()
        {
            while (Input.TryTake(out _)) { }
            buffer.Clear();
            jumpAt = duckAt = parryPressAt = -1f;
        }

        public void Tick(float time)
        {
            while (Input.TryTake(out var press)) Receive(press, time);
            if (!Rt.CanBeControlled || time < Rt.attackReadyAt) lastBusyAt = time;
            buffer.RemoveAll(b => time - b.time > MaxBufferAge || time - Mathf.Max(b.time, lastBusyAt) > BufferSeconds);
            if (!Rt.CanBeControlled) { self.Brain.Steer(MoveIntent.Hold); return; }

            // 跳ぶ・しゃがむは出せるまで少し待つ（着地の瞬間・回避の待ち時間）
            if (jumpAt >= 0f && time >= jumpAt)
            {
                if (Jump(time)) { jumpAt = -1f; return; }
                if (time - jumpAt > BufferSeconds) jumpAt = -1f;
            }
            if (duckAt >= 0f && time >= duckAt)
            {
                if (Duck(time)) { duckAt = -1f; return; }
                if (time - duckAt > BufferSeconds) duckAt = -1f;
            }

            // 受け流しの反撃・拾い斬りなどの続きが待っている間は、通常の技として使わない
            if (Rt.riposteAt < 0f)
                for (int i = 0; i < buffer.Count; i++)
                {
                    var cmd = Resolve(buffer[i].press);
                    if (!TryExecute(cmd, time)) continue;
                    buffer.RemoveAt(i);
                    return;
                }

            if (Input.GuardHeld && !Rt.weaponDetached)
            {
                if (Rt.state != FighterState.Guard) { self.SetState(FighterState.Guard); Used(PlayerCommand.Guard, time); }
                AimGuard();
                if (parryPressAt >= 0f && time - parryPressAt <= .1f)
                {
                    parryPressAt = -1f;
                    if (self.Brain.ThreatIncoming(out float toHit) && toHit <= ParryLead && time >= Rt.parryReadyAt)
                    {
                        self.ArmParry(time, Mathf.Max(0f, toHit - .05f));
                        Used(PlayerCommand.Parry, time);
                    }
                }
                self.Brain.Steer(MoveIntent.Hold);
                return;
            }
            parryPressAt = -1f;
            if (Rt.state == FighterState.Guard) self.SetState(FighterState.Approach);

            int x = Input.ScreenX;
            self.Brain.Steer(x == 0 ? MoveIntent.Hold : x == self.TowardOpponent ? MoveIntent.Advance : MoveIntent.Retreat);
        }

        void Receive(PlayerPress press, float time)
        {
            switch (press.button)
            {
                case PlayerButton.Jump: jumpAt = time + ModifierGrace; return;
                case PlayerButton.Duck: duckAt = time + ModifierGrace; return;
                case PlayerButton.Guard: parryPressAt = time; return;
                case PlayerButton.Special:
                case PlayerButton.Cheer: return; // 必殺・声援は観戦と同じ関与の操作（InterventionHud）
            }
            // ↑・↓ の直後の技のボタンは方向つきの技（跳ばない・しゃがまない）
            if (jumpAt >= 0f && press.y == 0) press.y = 1;
            if (duckAt >= 0f && press.y == 0) press.y = -1;
            if (press.button != PlayerButton.Backstep) jumpAt = duckAt = -1f;
            if (press.button == PlayerButton.Slash || press.button == PlayerButton.Tech) lastSlashAt = time;
            if (press.button == PlayerButton.Kick) lastKickAt = time;
            buffer.Add(new Buffered { press = press, time = time });
        }

        /// <summary>踏み込み斬りを使える大きさ（S・M）。L・XL は代わりに全身ぶん回し。</summary>
        bool DashSized => self.Mods.customized ? self.Mods.size <= WeaponSize.M : self.WeightClass != WeightClass.Heavy;

        /// <summary>押した方向（向いている側が前）とボタンから技を決める。上下を左右より優先する。</summary>
        public PlayerCommand Resolve(PlayerPress p)
        {
            int forward = p.screenX * self.Facing;
            switch (p.button)
            {
                case PlayerButton.Slash:
                    return p.y > 0 ? PlayerCommand.Rising : p.y < 0 ? PlayerCommand.LowSweep
                        : forward > 0 ? PlayerCommand.Thrust : forward < 0 ? PlayerCommand.Sweep : PlayerCommand.Overhead;
                case PlayerButton.Tech:
                    return p.y > 0 ? PlayerCommand.Launch : p.y < 0 ? PlayerCommand.Spin
                        : forward > 0 ? (DashSized ? PlayerCommand.DashSlash : PlayerCommand.Whirl)
                        : forward < 0 ? PlayerCommand.HookPull : PlayerCommand.Bash;
                case PlayerButton.Ranged:
                    return p.y < 0 ? PlayerCommand.Throw : forward > 0 ? PlayerCommand.Leap : PlayerCommand.Ranged;
                case PlayerButton.Kick:
                    return forward < 0 ? PlayerCommand.Feint : PlayerCommand.FrontKick;
                case PlayerButton.Backstep:
                    // 後ろへの素早い 2 回押し（前への 2 回押しは何もしない）
                    return forward < 0 ? PlayerCommand.Backstep : PlayerCommand.None;
                default: return PlayerCommand.None;
            }
        }

        /// <summary>今出せれば出して true。待てば出せる（クールダウン中など）なら false で覚えておく。出せない技は捨てる（true）。</summary>
        bool TryExecute(PlayerCommand cmd, float time)
        {
            bool armed = !Rt.weaponDetached;
            bool attackReady = self.IsGrounded && time >= Rt.attackReadyAt;
            switch (cmd)
            {
                case PlayerCommand.Backstep:
                    if (!self.IsGrounded || time < Rt.evadeReadyAt) return false;
                    self.StartEvade(EvadeKind.Backstep, time);
                    return Used(cmd, time);
                case PlayerCommand.FrontKick:
                    if (!B.expandedActions) return Reject("前蹴りは使えない", time);
                    if (!attackReady || time < Rt.kickReadyAt) return false;
                    return Attack(AttackStyle.FrontKick, cmd, time);
                case PlayerCommand.None: return true;
            }
            if (!armed) return Reject("武器を拾うまで蹴りだけ", time);
            if (!attackReady) return false;
            // 武器を拾った直後の斬る・技は拾い斬り
            if ((cmd <= PlayerCommand.Sweep && cmd >= PlayerCommand.Overhead) && time - pickedUpAt <= PickupSlashWindow
                && time >= Rt.pickupSlashReadyAt && B.expandedActions)
            {
                pickedUpAt = -1f;
                return Attack(AttackStyle.PickupSlash, PlayerCommand.PickupSlash, time);
            }
            switch (cmd)
            {
                case PlayerCommand.Overhead: return Attack(AttackStyle.Overhead, cmd, time);
                case PlayerCommand.Rising: return Attack(AttackStyle.Rising, cmd, time);
                case PlayerCommand.Thrust: return Attack(AttackStyle.Thrust, cmd, time);
                case PlayerCommand.LowSweep: return Attack(AttackStyle.LowSweep, cmd, time);
                case PlayerCommand.Sweep: return Attack(AttackStyle.Sweep, cmd, time);
                case PlayerCommand.Bash: return Attack(AttackStyle.Bash, cmd, time);
                case PlayerCommand.Launch: return Attack(AttackStyle.Launch, cmd, time);
                case PlayerCommand.Spin: return Attack(AttackStyle.Spin, cmd, time);
                case PlayerCommand.DashSlash:
                    if (time < Rt.dashReadyAt) return false;
                    return Attack(AttackStyle.DashSlash, cmd, time);
                case PlayerCommand.Whirl:
                    if (time < Rt.whirlReadyAt) return false;
                    return Attack(AttackStyle.Whirl, cmd, time);
                case PlayerCommand.HookPull:
                    if (!B.expandedActions || self.Actions == null || !self.Actions.HasHook) return Reject("引き崩しは、くぼみのある文字だけ", time);
                    if (time < Rt.hookReadyAt) return false;
                    return Attack(AttackStyle.HookPull, cmd, time);
                case PlayerCommand.Ranged:
                    if (!B.rangedAttacks || !AttackTechniques.CanGroundWave(self) && !AttackTechniques.CanStrokeShot(self)) return Reject("飛び道具は使えない", time);
                    if (time < Rt.rangedReadyAt) return false;
                    return Attack(AttackTechniques.CanGroundWave(self) ? AttackStyle.GroundWave : AttackStyle.StrokeShot, cmd, time);
                case PlayerCommand.Leap:
                    if (self.WeightClass == WeightClass.Heavy) return Reject("重い文字は跳び斬りできない", time);
                    return Attack(AttackStyle.Leap, cmd, time);
                case PlayerCommand.Throw:
                    if (time < Rt.throwReadyAt) return false;
                    return Attack(AttackStyle.Throw, cmd, time);
                case PlayerCommand.Feint:
                    if (!B.expandedActions) return Reject("フェイントは使えない", time);
                    if (time < Rt.feintReadyAt) return false;
                    self.Actions.StartFeint(time);
                    return Rt.state == FighterState.Feint ? Used(cmd, time) : false;
            }
            return true;
        }

        bool Attack(AttackStyle style, PlayerCommand cmd, float time)
        {
            if (Rt.state == FighterState.Guard) self.SetState(FighterState.Approach);
            int id = Rt.currentAttackId;
            Rt.comboRemaining = 0;
            self.StartAttack(time, style);
            Rt.comboRemaining = 0;
            // 開始の条件（クールダウン・地面）を満たさず始まらなかった
            if (Rt.currentAttackId == id) return false;
            return Used(cmd, time);
        }

        bool Jump(float time)
        {
            if (!self.IsGrounded || time < Rt.evadeReadyAt) return false;
            bool wall = B.expandedActions && self.WeightClass != WeightClass.Heavy && !Rt.weaponDetached
                && self.BackSpace <= B.wallJumpDistance && self.DistanceToOpponent <= 2.3f && time >= Rt.wallJumpReadyAt;
            if (wall) Rt.wallJumpReadyAt = time + B.wallJumpCooldown;
            self.StartEvade(wall ? EvadeKind.WallJump : EvadeKind.HopOver, time);
            return Rt.state == FighterState.Evade && Used(wall ? PlayerCommand.WallJump : PlayerCommand.HopOver, time);
        }

        bool Duck(float time)
        {
            if (!B.expandedActions || !self.IsGrounded || Rt.weaponDetached || time < Rt.evadeReadyAt || time < Rt.duckReadyAt) return false;
            Rt.duckReadyAt = time + B.duckCooldown * .5f;
            // しゃがみ中に斬るを押せば、立ち上がりながら斬り上げる
            Rt.duckCounterPending = true;
            self.StartEvade(EvadeKind.Duck, time);
            return Rt.state == FighterState.Evade && Used(PlayerCommand.Duck, time);
        }

        /// <summary>ガードの高さ: 相手の振りに合わせる（↓ を押していれば下段）。奥を回り込む振りにも構え直す。</summary>
        void AimGuard()
        {
            var o = self.Opponent.Runtime;
            bool swinging = o.state == FighterState.AttackWindup || o.state == FighterState.AttackActive || o.state == FighterState.Feint;
            Rt.guardPsiTarget = Input.Down ? self.GuardPsiLow : swinging ? self.Brain.GuardPsiFor(o.attackStyle) : float.NaN;
            Rt.guardAgainstSweep = swinging && AttackTechniques.IsYaw(o.attackStyle);
        }

        bool Used(PlayerCommand cmd, float time)
        {
            LastCommand = cmd; LastCommandAt = time;
            return true;
        }

        bool Reject(string reason, float time)
        {
            LastRejection = reason; LastRejectionAt = time;
            return true;
        }

        // ---- 状況で出る続きの技（受け流しの反撃・空中追撃・叩き落とし・斬り返し・起き上がり・鍔迫り合い）----

        /// <summary>since 以降に斬る（または技）を押したか。押していれば覚えていた入力を使い切る。</summary>
        public bool TakeFollowUp(float since, PlayerCommand used)
        {
            if (lastSlashAt < 0f || lastSlashAt < since) return false;
            lastSlashAt = -1f;
            buffer.RemoveAll(b => b.press.button == PlayerButton.Slash || b.press.button == PlayerButton.Tech);
            Used(used, self.Context.SimTime);
            return true;
        }

        public void OnPickedUp(float time) => pickedUpAt = time;

        /// <summary>起き上がり: 倒れている間に蹴り・斬るを押していれば起き上がり蹴り、後ろを押していれば後転して離れる。</summary>
        public void OnRecovered(float time, float downSince)
        {
            if (Rt.weaponDetached && lastKickAt < downSince) return;
            if ((lastKickAt >= downSince || lastSlashAt >= downSince) && self.DistanceToOpponent <= B.kickRange * 1.4f
                && time >= Rt.kickReadyAt && !self.Opponent.Runtime.IsDown)
            {
                lastKickAt = lastSlashAt = -1f;
                buffer.Clear();
                self.StartAttack(time, AttackStyle.RecoverKick);
                Rt.comboRemaining = 0;
                Used(PlayerCommand.RecoverKick, time);
                return;
            }
            if (Input.ScreenX != 0 && Input.ScreenX != self.TowardOpponent && self.BackSpace > .8f && self.IsGrounded)
            {
                self.StartEvade(EvadeKind.RecoverRoll, time);
                Used(PlayerCommand.RecoverRoll, time);
            }
        }
    }
}
