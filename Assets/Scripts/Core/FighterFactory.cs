using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>棒人間・文字武器・部位 Collider・陣営表示を組み立てる。</summary>
    public static class FighterFactory
    {
        public static readonly Color LeftColor = new Color32(0xD8, 0x3B, 0x39, 0xFF);
        public static readonly Color RightColor = new Color32(0x26, 0x73, 0xC9, 0xFF);
        public static readonly Color Ink = new Color32(0x10, 0x10, 0x12, 0xFF);
        public static readonly Color Paper = new Color32(0xF4, 0xF0, 0xE8, 0xFF);

        static PhysicsMaterial2D bodyMaterial, weaponMaterial;

        public static Color TeamColor(int id) => id == 0 ? LeftColor : RightColor;

        public static Fighter Create(int id, FighterLoadout loadout, GlyphDefinitionRuntime glyph, MatchContext ctx,
            MatchRandom rng, Transform parent)
        {
            var b = ctx.Balance;
            if (bodyMaterial == null) bodyMaterial = new PhysicsMaterial2D("FighterBody") { friction = 0.4f, bounciness = 0f };
            if (weaponMaterial == null) weaponMaterial = new PhysicsMaterial2D("Weapon") { friction = 0.3f, bounciness = 0.05f };

            var stats = StatCalculator.Compute(glyph, b);
            var baseStats = stats;
            var build = loadout.build;
            var cb = ctx.Customize != null ? ctx.Customize : CustomizeBalance.Default;
            // カスタマイズ: 字形の最大辺（サイズ）・握り点（握る位置）・武器質量（サイズ）を実際の構成へ反映する
            var geo = ResolveWeapon(build, glyph, baseStats, b, cb, out stats.weaponMass);
            int facing = id == 0 ? 1 : -1;
            float startX = id == 0 ? -b.startX : b.startX;

            var container = new GameObject(id == 0 ? "Fighter_Left" : "Fighter_Right").transform;
            container.SetParent(parent, false);

            // ---- 本体 ----
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(container, false);
            bodyGo.transform.position = new Vector3(startX, 0.02f, 0f);
            var body = bodyGo.AddComponent<Rigidbody2D>();
            body.mass = stats.bodyMass;
            body.useAutoMass = false;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.None; // 専用物理シーンは手動 Simulate のため描画側で補間する
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.5f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var fighter = bodyGo.AddComponent<Fighter>();
            fighter.Id = id;
            fighter.Loadout = loadout;
            fighter.Glyph = glyph;
            fighter.Stats = stats;
            fighter.WeightClass = StatCalculator.Classify(stats.weightScore, b);
            fighter.Context = ctx;
            fighter.Body = body;
            fighter.Runtime = new FighterRuntime { hp = stats.maxHp, maxHp = stats.maxHp };

            var bodyColliders = new List<Collider2D>
            {
                AddPart(bodyGo, BodyPart.Head, fighter, go => { var c = go.AddComponent<CircleCollider2D>(); c.radius = 0.2f; c.offset = new Vector2(0f, 1.62f); return c; }),
                AddPart(bodyGo, BodyPart.Torso, fighter, go => { var c = go.AddComponent<CapsuleCollider2D>(); c.size = new Vector2(0.34f, 0.66f); c.offset = new Vector2(0f, 1.1f); return c; }),
                AddPart(bodyGo, BodyPart.Arm, fighter, go => { var c = go.AddComponent<BoxCollider2D>(); c.size = new Vector2(0.44f, 0.14f); c.offset = new Vector2(0f, 1.28f); return c; }),
                AddPart(bodyGo, BodyPart.Leg, fighter, go => { var c = go.AddComponent<CapsuleCollider2D>(); c.size = new Vector2(0.32f, 0.82f); c.offset = new Vector2(0f, 0.41f); return c; }),
            };
            fighter.BodyColliders = bodyColliders.ToArray();
            bodyGo.AddComponent<BodyContactReporter>().Owner = fighter;

            // ---- 武器 ----
            if (build != null)
            {
                fighter.Mods = FighterModifiers.From(build, glyph, baseStats, geo, stats.weaponMass, b, cb);
                // 重量クラス（AI の連撃数・間隔・構え方）は実際に持つ武器の扱いにくさで決め直す。
                // 溜め時間などの字形固有の値は字形の重量のまま（慣性の差は物理で出る）
                fighter.WeightClass = StatCalculator.Classify(fighter.Mods.EffectiveWeightScore(stats.weightScore, cb), b);
                fighter.Style = BattleStyleStrategy.Create(fighter, build.battleStyle, cb);
            }
            fighter.Weapon = geo;
            var weaponGo = new GameObject("Weapon_" + glyph.grapheme);
            weaponGo.transform.SetParent(container, false);
            var wb = weaponGo.AddComponent<Rigidbody2D>();
            wb.useAutoMass = false;
            wb.mass = stats.weaponMass;
            wb.interpolation = RigidbodyInterpolation2D.None;
            wb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            wb.angularDamping = b.weaponAngularDamping;
            wb.linearDamping = 0f;
            wb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            fighter.WeaponBody = wb;

            var geoRoot = new GameObject("Geometry").transform;
            geoRoot.SetParent(weaponGo.transform, false);
            fighter.WeaponGeometryRoot = geoRoot;
            var weaponColliders = new List<Collider2D>();
            var colliderBase = new List<Vector4>();
            var colliderHost = geoRoot.gameObject;
            foreach (var r in glyph.features.colliderRects)
            {
                var box = colliderHost.AddComponent<BoxCollider2D>();
                Vector2 center = geo.PxToLocal(r.center);
                box.offset = new Vector2(center.x * facing, center.y);
                colliderBase.Add(new Vector4(center.x, center.y, r.width * geo.scale, r.height * geo.scale));
                box.size = new Vector2(r.width * geo.scale, r.height * geo.scale);
                box.sharedMaterial = weaponMaterial;
                weaponColliders.Add(box);
            }
            fighter.WeaponColliders = weaponColliders.ToArray();
            fighter.WeaponColliderBase = colliderBase.ToArray();

            // 表示用 Sprite は Rigidbody の子にせず、StickmanView が補間した姿勢へ毎フレーム置く
            var spriteGo = new GameObject("WeaponSprite_" + glyph.grapheme);
            spriteGo.transform.SetParent(container, false);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            var tex = glyph.texture;
            tex.filterMode = FilterMode.Bilinear;
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(geo.gripPx.x / tex.width, geo.gripPx.y / tex.height), 1f / geo.scale, 0, SpriteMeshType.FullRect);
            sr.color = TeamColor(id);
            sr.flipX = facing < 0;
            fighter.WeaponSprite = sr;
            sr.sortingOrder = 5;

            weaponGo.AddComponent<WeaponHitReporter>().Owner = fighter;

            // 自分の本体・手とは衝突させない
            foreach (var wc in weaponColliders)
            foreach (var bc in bodyColliders)
                Physics2D.IgnoreCollision(wc, bc, true);

            // サブシステム
            fighter.Brain = new FighterBrain(fighter, rng);
            fighter.Motor = new FighterMotor2D(fighter);
            fighter.WeaponMotor = new WeaponMotor2D(fighter);
            fighter.Knockdown = new KnockdownController(fighter);
            fighter.WalkSpeed = StatCalculator.WalkSpeed(stats.speed, b) * fighter.Mods.moveSpeed;
            fighter.AttackRange = (b.shoulderLocal.x + geo.length * b.reachFactor + 0.1f) * fighter.Mods.reach;
            if (fighter.Style != null) fighter.SetTendency(fighter.Style.Apply(b.TendencyFor(fighter.WeightClass), fighter.Mods.guardBiasBonus));

            // 初期向き・姿勢（ヒンジ接続前に配置）
            fighter.InitFacing(facing);
            wb.centerOfMass = new Vector2(geo.comLocal.x * facing, geo.comLocal.y);
            Vector2 shoulder = fighter.ShoulderLocal(facing);
            float phi = fighter.ReadyPsi - geo.alpha0Deg;
            Vector2 weaponPos = (Vector2)bodyGo.transform.position + shoulder;
            weaponGo.transform.SetPositionAndRotation(weaponPos, Quaternion.Euler(0f, 0f, phi * facing));
            wb.position = weaponPos;
            wb.rotation = phi * facing;

            var hinge = weaponGo.AddComponent<HingeJoint2D>();
            hinge.connectedBody = body;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = Vector2.zero;
            hinge.connectedAnchor = shoulder;
            hinge.enableCollision = false;
            hinge.useLimits = false;
            hinge.useMotor = false;
            fighter.Hinge = hinge;

            fighter.CapturePose();
            fighter.CapturePose();
            fighter.View = container.gameObject.AddComponent<StickmanView>();
            fighter.View.Init(fighter);
            return fighter;
        }

        static Collider2D AddPart(GameObject bodyGo, BodyPart part, Fighter owner, System.Func<GameObject, Collider2D> make)
        {
            var go = new GameObject("Hitbox_" + part);
            go.transform.SetParent(bodyGo.transform, false);
            var col = make(go);
            col.sharedMaterial = bodyMaterial;
            var hb = go.AddComponent<BodyPartHitbox>();
            hb.Owner = owner;
            hb.Part = part;
            return col;
        }

        /// <summary>
        /// 字形の外接矩形の最大辺を weaponMaxSide に収める等方スケール。Sprite と Collider は同じ変換を使う。
        /// </summary>
        /// <summary>
        /// 武器の構成（縮尺・握り点・質量）。戦闘とカスタマイズ画面のプレビューで同じ計算を使う。build が null なら字形の既定。
        /// </summary>
        public static WeaponGeometry ResolveWeapon(FighterBuildData build, GlyphDefinitionRuntime glyph, FighterStats baseStats,
            CombatBalance b, CustomizeBalance cb, out float weaponMass)
        {
            if (build == null)
            {
                weaponMass = baseStats.weaponMass;
                return BuildGeometry(glyph, b);
            }
            weaponMass = WeaponSizeController.WeaponMass(baseStats.weaponMass, build.weaponSize, cb);
            float maxSide = WeaponSizeController.MaxSide(build.weaponSize, b, cb);
            var gripPx = build.customGrip
                ? WeaponGripController.GripFromPoint(glyph, build.gripPoint, cb.gripClampMin * 0.5f, 1f - (1f - cb.gripClampMax) * 0.5f)
                : WeaponGripController.GripPixel(glyph, cb.ClampGrip(build.gripPosition));
            return BuildGeometry(glyph, maxSide, gripPx, true);
        }

        public static WeaponGeometry BuildGeometry(GlyphDefinitionRuntime glyph, CombatBalance b) =>
            BuildGeometry(glyph, b.weaponMaxSide, glyph.features.gripPoint);

        /// <summary>
        /// 最大辺（サイズ）と握り点（握る位置）を指定して作る。表示・Collider・射程・重心はすべてここから決まる。
        /// 姿勢角 ψ の基準は「握り→重心」。カスタマイズ（longAxis）では握る位置で重心が握りに重なり向きが定まらないため、
        /// 「字形の長い方の軸に沿って、握りから遠い側の端へ向かう方向」を基準にする（中央持ちでも字形は傾かずに構える）。
        /// </summary>
        public static WeaponGeometry BuildGeometry(GlyphDefinitionRuntime glyph, float maxSide, Vector2 gripPx, bool longAxis = false)
        {
            var f = glyph.features;
            float maxSidePx = Mathf.Max(f.inkBounds.width, f.inkBounds.height);
            var g = new WeaponGeometry
            {
                scale = maxSide / maxSidePx,
                gripPx = gripPx,
                colliderCount = f.colliderRects.Length,
            };
            g.maxSide = maxSidePx * g.scale;
            g.comLocal = g.PxToLocal(f.centerOfMass);
            var bMin = g.PxToLocal(new Vector2(f.inkBounds.xMin, f.inkBounds.yMin));
            var bMax = g.PxToLocal(new Vector2(f.inkBounds.xMax, f.inkBounds.yMax));
            g.boundsLocal = Rect.MinMaxRect(bMin.x, bMin.y, bMax.x, bMax.y);
            float len = 0f;
            foreach (var r in f.colliderRects)
            {
                foreach (var corner in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) })
                    len = Mathf.Max(len, g.PxToLocal(corner).magnitude);
            }
            g.length = len;
            Vector2 axis = g.comLocal;
            if (longAxis)
            {
                var bl = g.boundsLocal;
                axis = f.inkBounds.width >= f.inkBounds.height
                    ? new Vector2(bl.xMax >= -bl.xMin ? 1f : -1f, 0f)
                    : new Vector2(0f, bl.yMax >= -bl.yMin ? 1f : -1f);
            }
            g.alpha0Deg = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
            return g;
        }
    }
}
