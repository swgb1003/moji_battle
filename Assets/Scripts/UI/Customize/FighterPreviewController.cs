using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// カスタマイズ画面のリアルタイムプレビュー（カスタマイズ仕様 4.2 / 8.2）。
    /// 戦闘と同じ計算（FighterFactory.ResolveWeapon・FighterModifiers）で字形の縮尺・握り点・Collider・重心・構えを描く。
    /// 物理は動かさず、試し振りは「同じ最大角加速度（慣性比込み）で目標角へ向かう」簡易運動で見せる（サイズ・握る位置の重さが見える）。
    /// </summary>
    public sealed class FighterPreviewController : MonoBehaviour
    {
        public Vector2 feet = new Vector2(-3.4f, 0f);
        public bool showGuides = true;

        CombatBalance B => CombatBalance.Default;
        CustomizeBalance CB => CustomizeBalance.Default;

        LineRenderer head, torso, armFront, armBack, legA, legB, ground, gripMarker, comMarker;
        readonly List<LineRenderer> colliderLines = new List<LineRenderer>();
        SpriteRenderer weapon;
        // 二刀流の副武器（反対の手）
        SpriteRenderer offhand;
        GlyphDefinitionRuntime offGlyph;
        WeaponGeometry offGeometry;
        Sprite offSprite;
        float offPsi = float.NaN;
        public bool HasOffhand => Build != null && Build.IsDualWield && offGeometry != null;
        SpriteRenderer printedHead;
        Sprite currentSprite;
        FighterVolumeRig volume;
        SpriteRenderer pedestalTop, pedestalSide;
        public FighterVolumeRig VolumeRig => volume;

        public FighterBuildData Build { get; private set; }
        public int Side { get; private set; }
        public WeaponGeometry Geometry { get; private set; }
        public FighterModifiers Mods { get; private set; }
        public FighterStats Stats { get; private set; }
        public float WeaponMass { get; private set; }
        public float AttackRange { get; private set; }
        public WeightClass EffectiveClass { get; private set; }
        GlyphDefinitionRuntime glyph;
        Texture2D gripTexture;
        Color32[] gripPixels;

        // 試し振り
        enum Demo { Hold, Windup, Swing, Back }
        Demo demo;
        float demoTime, psi, omega;
        public float CurrentPsi => psi;
        /// <summary>握る場所を選んでいる間は試し振りを止める</summary>
        public bool Paused { get; set; }
        Vector3 shownGrip;
        Quaternion shownRot = Quaternion.identity;
        LineRenderer hoverMarker;
        bool gripEditing;
        bool pauseBeforeEditing;
        Vector3 editingPixelOrigin;
        Quaternion editingRotation;
        Vector2? pendingGripPx;
        public bool GripEditing => gripEditing;

        void Awake()
        {
            var ink = RetroUi.Ink;
            head = UiKit.Line(transform, "Head", ink, 0.075f, 4, loop: true);
            if(GameFlow.ChallengeMode || GameFlow.DraftMode)
            {
                printedHead=UiKit.Rect(transform,"PrintedHead",ink,4);printedHead.sprite=UiKit.DiscSprite;
                printedHead.transform.localScale=new Vector3(.39f,.39f,1);
            }
            torso = UiKit.Line(transform, "Torso", ink, 0.075f, 4);
            armFront = UiKit.Line(transform, "ArmFront", ink, 0.075f, 6);
            armBack = UiKit.Line(transform, "ArmBack", ink, 0.07f, 3);
            legA = UiKit.Line(transform, "LegA", ink, 0.075f, 4);
            legB = UiKit.Line(transform, "LegB", ink, 0.075f, 4);
            ground = UiKit.Line(transform, "Ground", ink, 0.06f, 1);
            gripMarker = UiKit.Line(transform, "GripPoint", new Color(1f, 0.55f, 0f, 1f), 0.035f, 9, loop: true);
            comMarker = UiKit.Line(transform, "CenterOfMass", new Color(0f, 0.45f, 1f, 1f), 0.035f, 9);
            hoverMarker = UiKit.Line(transform, "HoverGrip", new Color(1f, 0.55f, 0f, 0.6f), 0.03f, 9, loop: true);
            var go = new GameObject("WeaponSprite");
            go.transform.SetParent(transform, false);
            weapon = go.AddComponent<SpriteRenderer>();
            weapon.sortingOrder = 5;
            var offGo = new GameObject("OffhandSprite");
            offGo.transform.SetParent(transform, false);
            offhand = offGo.AddComponent<SpriteRenderer>();
            offhand.sortingOrder = 3;
            offhand.enabled = false;
            pedestalSide = UiKit.Rect(transform, "DisplayPlinthSide", PaperUi.Edge, 0);
            pedestalTop = UiKit.Rect(transform, "DisplayPlinthTop", new Color(.94f, .9f, .8f, 1), 0);
            pedestalSide.sprite = pedestalTop.sprite = UiKit.DiscSprite;
            volume = new FighterVolumeRig(transform, weapon, RetroUi.Red);
            volume.AddOffhand(transform, offhand);
            volume.SetVisible(true);
            head.enabled = torso.enabled = armFront.enabled = armBack.enabled = legA.enabled = legB.enabled = false;
            if (printedHead != null) printedHead.enabled = false;
        }

        /// <summary>設定を反映（変更の瞬間に呼ぶ）。</summary>
        public void Show(FighterBuildData build, int side)
        {
            bool glyphChanged = glyph == null || Build == null || build.character != Build.character
                || build.fontType != Build.fontType || build.weaponFlip != Build.weaponFlip;
            bool gripChanged = Build == null || build.customGrip != Build.customGrip
                || (build.customGrip ? build.gripPoint != Build.gripPoint : build.gripPosition != Build.gripPosition);
            bool geometryChanged = glyphChanged || Geometry == null || build.weaponSize != Build.weaponSize || gripChanged;
            bool poseChanged = geometryChanged || build.gripType != Build.gripType || side != Side;
            if (gripEditing && Build != null && (glyphChanged || build.weaponSize != Build.weaponSize || side != Side)) EndGripEditing();
            Build = build.Clone();
            Side = side;
            if (glyphChanged)
            {
                if (!GlyphCatalog.TryGet(build.character, build.fontType, B, GlyphCalibration.Default, out glyph, out _)) return;
                glyph = GlyphFlip.Apply(glyph, build.weaponFlip);
                Stats = StatCalculator.Compute(glyph, B);
            }
            if (geometryChanged)
            {
                Geometry = FighterFactory.ResolveWeapon(Build, glyph, Stats, B, CB, out float mass);
                WeaponMass = mass;
            }
            Mods = FighterModifiers.From(Build, glyph, Stats, Geometry, WeaponMass, B, CB);
            AttackRange = (B.shoulderLocal.x + Geometry.length * B.reachFactor + 0.1f) * Mods.reach;
            EffectiveClass = StatCalculator.Classify(Mods.EffectiveWeightScore(Stats.weightScore, CB), B);

            var tex = glyph.texture;
            if (gripTexture != tex)
            {
                gripTexture = tex;
                gripPixels = tex != null && tex.isReadable ? tex.GetPixels32() : null;
            }
            if (currentSprite == null || geometryChanged)
            {
                if (currentSprite != null) Destroy(currentSprite);
                currentSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(Geometry.gripPx.x / tex.width, Geometry.gripPx.y / tex.height), 1f / Geometry.scale, 0, SpriteMeshType.FullRect);
            }
            weapon.sprite = currentSprite;
            weapon.color = RetroUi.Team(side);
            ShowOffhand(build);
            volume.SetTeam(weapon.color);

            var rects = glyph.features.colliderRects;
            while (colliderLines.Count < rects.Length)
                colliderLines.Add(UiKit.Line(transform, "ColliderOutline", new Color(0f, 0.6f, 0.2f, 0.7f), 0.015f, 7, loop: true));
            psi = ReadyPsi;
            omega = 0f;
            demo = Demo.Hold;
            demoTime = 0f;
            // A confirmed grip must show its new stance now, even while the pointer still hovers here.
            // Rebase the hover freeze onto that stance; only an in-progress drag keeps the old pose.
            RenderPose(preserveEditingPose: !poseChanged);
            if (gripEditing && poseChanged) CaptureEditingPose();
        }

        void ShowOffhand(FighterBuildData build)
        {
            offGeometry = null;
            offhand.enabled = false;
            if (!build.IsDualWield) return;
            string ch = build.OffhandGlyph;
            GlyphDefinitionRuntime g = glyph;
            if (ch != build.character)
            {
                if (!GlyphCatalog.TryGet(ch, build.fontType, B, GlyphCalibration.Default, out g, out _)) g = glyph;
                else g = GlyphFlip.Apply(g, build.weaponFlip);
            }
            offGlyph = g;
            offGeometry = FighterFactory.ResolveWeapon(Build, g, StatCalculator.Compute(g, B), B, CB, out _);
            var tex = g.texture;
            if (offSprite != null) Destroy(offSprite);
            offSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(offGeometry.gripPx.x / tex.width, offGeometry.gripPx.y / tex.height), 1f / offGeometry.scale, 0, SpriteMeshType.FullRect);
            offhand.sprite = offSprite;
            offhand.color = RetroUi.Team(Side);
            offhand.enabled = true;
            offPsi = float.NaN;
        }

        float ReadyPsi => float.IsNaN(Mods.readyPsi) ? B.ReadyPsi(Stats.weightScore) : Mods.readyPsi;
        float GroundPsi => -Mathf.Asin(Mathf.Clamp01((B.shoulderLocal.y - 0.15f) / Mathf.Max(0.01f, Geometry.length))) * Mathf.Rad2Deg + 5f;
        float AccelLimitDeg => B.maxAngularAccel * Mathf.Lerp(1f, B.heavyAccelFactor, Stats.weightScore / 100f) * Mods.handlingAccel * Mathf.Rad2Deg;
        public float WindupTime => StatCalculator.Windup(Stats.weightScore, B) / Mathf.Max(0.01f, Mods.attackSpeed);
        public float RecoveryTime => StatCalculator.Recovery(Stats.weightScore, B) / Mathf.Max(0.01f, Mods.attackSpeed);

        void Update()
        {
            if (Geometry == null) return;
            float dt = Time.unscaledDeltaTime;
            if (RetroMotionSettings.Reduced) { psi = ReadyPsi; omega = 0; return; }
            if (Paused)
            {
                // Pause the displayed pose; moving back to ReadyPsi would move the point under the cursor.
                omega = 0f;
                demo = Demo.Hold;
                demoTime = 0f;
                return;
            }
            demoTime += dt;
            float strikeTo = Mathf.Max(-5f, GroundPsi);
            float target;
            float accelScale = 1f;
            switch (demo)
            {
                case Demo.Hold:
                    target = ReadyPsi;
                    if (demoTime > 1.2f) Next(Demo.Windup);
                    break;
                case Demo.Windup:
                    target = 95f;
                    accelScale = 0.5f * Mods.attackTorque;
                    if (demoTime >= WindupTime && Mathf.Abs(psi - target) < 20f || demoTime > WindupTime * 3f) Next(Demo.Swing);
                    break;
                case Demo.Swing:
                    target = strikeTo;
                    accelScale = Mods.attackTorque;
                    if (psi <= target + 8f || demoTime > 2f) Next(Demo.Back);
                    break;
                default:
                    target = ReadyPsi;
                    accelScale = 0.5f;
                    if (demoTime >= RecoveryTime && Mathf.Abs(psi - target) < 10f || demoTime > 3f) Next(Demo.Hold);
                    break;
            }
            // 最大角加速度で目標へ（行き過ぎないよう手前で減速）
            float a = AccelLimitDeg * accelScale;
            float err = target - psi;
            float want = Mathf.Sign(err) * Mathf.Min(B.weaponMaxAngularSpeedDeg, Mathf.Sqrt(2f * a * Mathf.Abs(err)));
            omega = Mathf.MoveTowards(omega, want, a * dt);
            psi += omega * dt;
        }

        void Next(Demo d) { demo = d; demoTime = 0f; }

        void LateUpdate() => RenderPose();

        void RenderPose(bool preserveEditingPose = true)
        {
            if (Geometry == null) return;
            Vector3 P(float x, float y) => new Vector3(feet.x + x, feet.y + y, 0f);
            Vector3 hip = P(0f, 0.86f), neck = P(0f, 1.42f), headC = P(0f, 1.62f), shoulder = P(0f, 1.34f);
            pedestalTop.transform.position = P(0, -.015f);
            pedestalTop.transform.localScale = new Vector3(1.4f, .1f, 1);
            pedestalSide.transform.position = P(0, -.075f);
            pedestalSide.transform.localScale = new Vector3(1.4f, .16f, 1);
            if(printedHead!=null)printedHead.transform.position=headC;
            head.positionCount = 20;
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                head.SetPosition(i, headC + new Vector3(Mathf.Cos(a) * 0.2f, Mathf.Sin(a) * 0.2f, 0f));
            }
            Line(torso, neck, hip);
            Line(legA, hip, P(0.16f, 0.43f), P(0.2f, 0f));
            Line(legB, hip, P(-0.06f, 0.43f), P(-0.12f, 0f));
            Line(ground, P(-12f, 0f), P(12f, 0f));

            Vector3 grip = P(B.shoulderLocal.x, B.shoulderLocal.y);
            float phi = psi - Geometry.alpha0Deg;
            var rot = Quaternion.Euler(0f, 0f, phi);
            if (gripEditing && preserveEditingPose)
            {
                rot = editingRotation;
                grip = editingPixelOrigin + rot * (Vector3)(Geometry.gripPx * Geometry.scale);
            }
            weapon.transform.SetPositionAndRotation(grip, rot);
            volume.Begin(feet, hip, neck, headC, 1, false);
            volume.Leg(hip, P(.2f, 0), P(.16f, .43f), true, 1, true);
            volume.Leg(hip, P(-.12f, 0), P(-.06f, .43f), false, 1, true);
            shownGrip = grip;
            shownRot = rot;
            Vector3 hand = pendingGripPx.HasValue ? PixelToWorld(pendingGripPx.Value) : grip;
            Line(armFront, shoulder, Vector3.Lerp(shoulder, hand, 0.5f) + Vector3.down * 0.12f, hand);
            Vector3 back = hand;
            Vector3 backAxis = rot * (Vector3)Geometry.comLocal;
            if (HasOffhand)
            {
                // 主武器の振りに少し遅れて追い、構えている間は低く交差させる
                bool swinging = demo == Demo.Windup || demo == Demo.Swing;
                float target = psi + (swinging ? 0f : CB.dualOffhandIdleOffset);
                offPsi = float.IsNaN(offPsi) ? target : Mathf.LerpAngle(offPsi, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(.01f, CB.dualOffhandLag)));
                var offRot = Quaternion.Euler(0f, 0f, offPsi - offGeometry.alpha0Deg);
                back = grip + new Vector3(-0.06f, -0.03f, 0f);
                offhand.transform.SetPositionAndRotation(back, offRot);
                backAxis = offRot * (Vector3)offGeometry.comLocal;
            }
            else if (Mods.grip == GripType.TwoHanded)
            {
                float len = Geometry.comLocal.magnitude;
                if (len > 1e-3f) back = hand + rot * (Vector3)(Geometry.comLocal / len * Mathf.Min(len, CB.secondHandOffset));
            }
            Line(armBack, shoulder + new Vector3(-0.05f, -0.02f, 0f), Vector3.Lerp(shoulder, back, 0.5f) + Vector3.down * 0.2f, back);
            Vector3 axis = rot * (Vector3)Geometry.comLocal;
            volume.Arm(shoulder, hand, Vector3.down * .12f, true, true, axis);
            volume.Arm(shoulder + new Vector3(-.05f, -.02f, 0), back, Vector3.down * .2f, false,
                Mods.grip == GripType.TwoHanded || HasOffhand, HasOffhand ? backAxis : axis);
            volume.End();

            var rects = glyph.features.colliderRects;
            for (int i = 0; i < colliderLines.Count; i++)
            {
                var lr = colliderLines[i];
                if (!showGuides || i >= rects.Length) { lr.positionCount = 0; continue; }
                var r = rects[i];
                lr.positionCount = 4;
                lr.SetPosition(0, grip + rot * (Vector3)Geometry.PxToLocal(new Vector2(r.xMin, r.yMin)));
                lr.SetPosition(1, grip + rot * (Vector3)Geometry.PxToLocal(new Vector2(r.xMax, r.yMin)));
                lr.SetPosition(2, grip + rot * (Vector3)Geometry.PxToLocal(new Vector2(r.xMax, r.yMax)));
                lr.SetPosition(3, grip + rot * (Vector3)Geometry.PxToLocal(new Vector2(r.xMin, r.yMax)));
            }
            ColliderDebugView.DrawGripMarker(gripMarker, hand + Vector3.back * 0.1f, pendingGripPx.HasValue ? .12f : .08f);
            if (showGuides)
            {
                ColliderDebugView.DrawComMarker(comMarker, grip + rot * (Vector3)Geometry.comLocal + Vector3.back * 0.1f, 0.09f);
            }
            else
            {
                comMarker.positionCount = 0;
            }
        }

        /// <summary>
        /// ワールド座標の点を、表示中の字形の外接矩形内の正規化座標（x: 左0→右1、y: 下0→上1）へ変換する。
        /// 字形の上（画線から snapPx 画素以内）でなければ false。
        /// </summary>
        public bool TryPickGrip(Vector3 world, out Vector2 normalized, float snapPx = 14f)
        {
            normalized = default;
            if (Geometry == null || glyph == null) return false;
            Vector2 local = Quaternion.Inverse(shownRot) * (world - shownGrip);
            Vector2 px = local / Geometry.scale + Geometry.gripPx;
            var b = glyph.features.inkBounds;
            if (px.x < b.xMin - snapPx || px.x > b.xMax + snapPx || px.y < b.yMin - snapPx || px.y > b.yMax + snapPx) return false;
            if (!NearInk(px, snapPx)) return false;
            normalized = new Vector2((px.x - b.xMin) / Mathf.Max(1f, b.width), (px.y - b.yMin) / Mathf.Max(1f, b.height));
            return true;
        }

        public void BeginGripEditing()
        {
            if (gripEditing || Geometry == null) return;
            pauseBeforeEditing = Paused;
            Paused = true;
            gripEditing = true;
            CaptureEditingPose();
        }

        void CaptureEditingPose()
        {
            editingRotation = shownRot;
            editingPixelOrigin = shownGrip - shownRot * (Vector3)(Geometry.gripPx * Geometry.scale);
        }

        public void EndGripEditing()
        {
            if (!gripEditing) return;
            gripEditing = false;
            pendingGripPx = null;
            Paused = pauseBeforeEditing;
            psi = ReadyPsi; omega = 0f; demo = Demo.Hold; demoTime = 0f;
            ShowHover(null);
            RenderPose();
        }

        /// <summary>Drag positions are clamped to the glyph; gaps between strokes snap to actual ink.</summary>
        public bool ProjectGrip(Vector3 world, out Vector2 normalized)
        {
            normalized = default;
            if (Geometry == null || glyph == null) return false;
            Vector2 px = (Vector2)(Quaternion.Inverse(shownRot) * (world - shownGrip)) / Geometry.scale + Geometry.gripPx;
            var bounds = glyph.features.inkBounds;
            normalized = new Vector2(Mathf.Clamp01((px.x - bounds.xMin) / Mathf.Max(1f, bounds.width)),
                Mathf.Clamp01((px.y - bounds.yMin) / Mathf.Max(1f, bounds.height)));
            return true;
        }

        public void ShowPendingGrip(Vector2? normalized)
        {
            pendingGripPx = normalized.HasValue && glyph != null ? WeaponGripController.GripFromPoint(glyph,
                normalized.Value, CB.gripClampMin * .5f, 1f - (1f - CB.gripClampMax) * .5f, gripPixels) : (Vector2?)null;
            ShowHover(null);
            RenderPose();
        }

        bool NearInk(Vector2 px, float radius)
        {
            var tex = glyph.texture;
            if (tex == null || gripPixels == null) return true;
            int r = Mathf.CeilToInt(radius);
            int cx = Mathf.FloorToInt(px.x), cy = Mathf.FloorToInt(px.y);
            for (int y = cy - r; y <= cy + r; y += 2)
            for (int x = cx - r; x <= cx + r; x += 2)
            {
                if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) continue;
                if ((x - px.x) * (x - px.x) + (y - px.y) * (y - px.y) > radius * radius) continue;
                if (gripPixels[y * tex.width + x].a >= 128) return true;
            }
            return false;
        }

        /// <summary>字形の画素（マスク画素空間）が今表示されているワールド座標（テスト・確認用）。</summary>
        public Vector3 PixelToWorld(Vector2 px) => shownGrip + shownRot * (Vector3)((px - Geometry.gripPx) * Geometry.scale);

        /// <summary>カーソルが字形の上にある時、握れる場所として丸を出す。</summary>
        public void ShowHover(Vector3? world)
        {
            if (world == null) { hoverMarker.positionCount = 0; return; }
            ColliderDebugView.DrawGripMarker(hoverMarker, world.Value + Vector3.back * 0.1f, 0.1f);
        }

        static void Line(LineRenderer lr, params Vector3[] pts)
        {
            lr.positionCount = pts.Length;
            for (int i = 0; i < pts.Length; i++) lr.SetPosition(i, pts[i]);
        }

        /// <summary>プレビュー下の説明（変更の結果がすぐ分かる数値）。</summary>
        public string Describe()
        {
            if (Geometry == null) return "";
            var m = Mods;
            string cls = EffectiveClass == WeightClass.Light ? "軽量" : EffectiveClass == WeightClass.Heavy ? "重量" : "中量";
            return $"武器の重さ {WeaponMass:F2}   長さ(握り→端) {Geometry.length:F2}   射程 {AttackRange:F2}\n" +
                   $"握り→重心 {m.leverArm:F2}   扱いやすさ {m.handlingAccel * 100f:F0}%   溜め {WindupTime:F2}秒   移動 {m.moveSpeed * 100f:F0}%\n" +
                   $"ガード安定 {m.guardStability * 100f:F0}%   与ダメ(質量) {m.damage * 100f:F0}%   戦い方 {cls}級";
        }

        void OnDestroy()
        {
            volume?.Dispose();
            if (currentSprite != null) Destroy(currentSprite);
        }
    }
}
