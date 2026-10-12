using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 二刀流の副武器（反対の手の字形）。物理には加えず（Rigidbody・Collider を持たない）、毎ステップ主武器の姿勢から置き直す。
    /// 主武器の振りに少し遅れて追うことで、左右交互の二段目になる。当たりは字形の矩形と相手の体の重なりだけを見る
    /// （武器同士の弾き・鍔迫り合い・ガードの受けは主武器が担う）。物理の形状を毎ステップ動かすと COM 試合の計算が重くなるため。
    /// </summary>
    public sealed class OffhandWeapon
    {
        readonly Fighter self;
        public readonly GlyphDefinitionRuntime Glyph;
        public readonly WeaponGeometry Geometry;
        public readonly float Mass;
        public readonly SpriteRenderer Sprite;
        /// <summary>字形の当たりの矩形（右向き・握り原点のローカル座標）: 中心 xy・幅 z・高さ w</summary>
        readonly Vector4[] rects;
        readonly List<Vector2> samples = new List<Vector2>(64);
        float psi = float.NaN, offset;

        public Vector2 Position { get; private set; }
        public float Rotation { get; private set; }
        public Vector2 PrevPos, CurrPos;
        public float PrevRot, CurrRot;
        Vector2 lastPos;
        float lastRot;

        /// <summary>手の位置（主武器の握りから少し後ろ）。描画の後ろ手もここを握る。</summary>
        public Vector2 Grip => Position;
        public int RectCount => rects.Length;

        public OffhandWeapon(Fighter self, GlyphDefinitionRuntime glyph, WeaponGeometry geo, float mass, Transform container, Color team)
        {
            this.self = self; Glyph = glyph; Geometry = geo; Mass = mass;
            var list = new List<Vector4>();
            foreach (var r in glyph.features.colliderRects)
            {
                Vector2 c = geo.PxToLocal(r.center);
                list.Add(new Vector4(c.x, c.y, r.width * geo.scale, r.height * geo.scale));
            }
            rects = list.ToArray();

            var spriteGo = new GameObject("OffhandSprite_" + glyph.grapheme);
            spriteGo.transform.SetParent(container, false);
            Sprite = spriteGo.AddComponent<SpriteRenderer>();
            var tex = glyph.texture;
            tex.filterMode = FilterMode.Bilinear;
            Sprite.sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(geo.gripPx.x / tex.width, geo.gripPx.y / tex.height), 1f / geo.scale, 0, SpriteMeshType.FullRect);
            Sprite.color = team;
            Sprite.sortingOrder = 3;
            Place(0f, true);
            CapturePose(); CapturePose();
        }

        bool Attacking
        {
            get
            {
                var s = self.Runtime.state;
                return s == FighterState.AttackWindup || s == FighterState.AttackActive || s == FighterState.AttackRecovery;
            }
        }

        /// <summary>物理ステップの後に呼ぶ。主武器の姿勢から置き直し、相手の体に重なっていれば当たりを報告する。</summary>
        public void Step(float dt)
        {
            Place(dt, false);
            DetectHits(dt);
        }

        /// <summary>友人対戦のゲスト側: 受け取った姿勢に合わせて置くだけ（当たりはホストが判定する）。</summary>
        public void Follow(float dt)
        {
            Place(dt, false);
            CapturePose(); CapturePose();
        }

        void Place(float dt, bool snap)
        {
            var cb = self.Context.Customize != null ? self.Context.Customize : CustomizeBalance.Default;
            var rt = self.Runtime;
            int facing = self.Facing;
            // 主武器を投げた後・転倒中は、主武器に追わず構えのまま
            float mainPsi = rt.weaponDetached || rt.IsDown ? self.ReadyPsi : self.WeaponMotor.MeasurePsi();
            float targetOffset = Attacking && !rt.weaponDetached ? 0f : cb.dualOffhandIdleOffset;
            if (snap || float.IsNaN(psi)) { psi = mainPsi; offset = targetOffset; }
            else
            {
                float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, cb.dualOffhandLag));
                psi += Mathf.DeltaAngle(psi, mainPsi) * k;
                offset = Mathf.MoveTowards(offset, targetOffset, 300f * dt);
            }
            float phi = psi + offset - Geometry.alpha0Deg;
            Vector2 hand = self.Body.GetRelativePoint(self.HandLocal(facing) + new Vector2(-0.06f * facing, -0.03f));
            float rot = self.Body.rotation + phi * facing;
            lastPos = snap ? hand : Position;
            lastRot = snap ? rot : Rotation;
            Position = hand;
            Rotation = rot;
            Sprite.flipX = facing < 0;
        }

        /// <summary>武器のローカル点（右向き基準）をワールドへ。横振りの奥行きは幅を縮めて表す。</summary>
        Vector2 ToWorld(Vector2 local)
        {
            local.x *= self.Facing * Mathf.Cos(self.Runtime.sweepYaw * Mathf.Deg2Rad);
            return Position + (Vector2)(Quaternion.Euler(0f, 0f, Rotation) * local);
        }

        /// <summary>武器上の点の、直前のステップからの速度。</summary>
        public Vector2 PointVelocity(Vector2 world, float dt)
        {
            if (dt <= 0f) return Vector2.zero;
            Vector2 local = Quaternion.Euler(0, 0, -Rotation) * (world - Position);
            Vector2 before = lastPos + (Vector2)(Quaternion.Euler(0, 0, lastRot) * local);
            return (world - before) / dt;
        }

        /// <summary>
        /// 副武器の当たりの時間。主武器の振り（有効時間・振り抜き）に、副武器が遅れて追う分を足す。
        /// </summary>
        bool InWindow
        {
            get
            {
                if (self.InAttackWindow) return true;
                var rt = self.Runtime;
                var cb = self.Context.Customize != null ? self.Context.Customize : CustomizeBalance.Default;
                return rt.state == FighterState.AttackRecovery && !rt.weaponDetached
                       && !AttackTechniques.IsKick(rt.attackStyle) && !AttackTechniques.IsRanged(rt.attackStyle)
                       && rt.stateTime <= self.Balance.followThroughHitSeconds + cb.dualOffhandLag * 3f;
            }
        }

        /// <summary>字形の矩形の角・辺の中点・中心を、ワールド座標で集める（相手の体との重なりの判定に使う）。</summary>
        void Sample()
        {
            samples.Clear();
            foreach (var r in rects)
            {
                float hx = r.z * .5f, hy = r.w * .5f;
                for (int ix = -1; ix <= 1; ix++)
                for (int iy = -1; iy <= 1; iy++)
                    samples.Add(ToWorld(new Vector2(r.x + ix * hx, r.y + iy * hy)));
            }
        }

        void DetectHits(float dt)
        {
            if (!InWindow || self.Runtime.throwLive) return;
            var opp = self.Opponent;
            if (opp == null || self.Context.Hits.OffhandResolved(self.Runtime.currentAttackId, opp.Id)) return;
            // 大まかな距離で外れていれば調べない（握りから字形の最遠点まで）
            if ((opp.Body.position - Position).magnitude > Geometry.length + 2f) return;
            Sample();
            foreach (var bc in opp.BodyColliders)
            {
                var bounds = bc.bounds;
                foreach (var p in samples)
                {
                    if (!bounds.Contains(new Vector3(p.x, p.y, bounds.center.z)) || !bc.OverlapPoint(p)) continue;
                    var hb = bc.GetComponent<BodyPartHitbox>();
                    if (self.Context.Hits.ResolveOffhand(self, opp, hb != null ? hb.Part : BodyPart.Torso,
                            p, PointVelocity(p, dt), self.Context.SimTime)) return;
                }
            }
        }

        public void CapturePose()
        {
            PrevPos = CurrPos; PrevRot = CurrRot;
            CurrPos = Position; CurrRot = Rotation;
            if ((CurrPos - PrevPos).sqrMagnitude > 4f) { PrevPos = CurrPos; PrevRot = CurrRot; }
        }
    }
}
