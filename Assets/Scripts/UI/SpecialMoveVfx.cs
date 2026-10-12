using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 必殺技の見た目。AI が技名から決めた属性・色・演出の形で、発動の告知・技の間の武器の纏い・当たりの炸裂を描く。
    /// 初期必殺技にも文字ごとの属性・色・演出を使う。戦闘計算は持たない（ゲストも同期された状態で同じものを描く）。
    /// </summary>
    [DefaultExecutionOrder(130)]
    public sealed class SpecialMoveVfx : MonoBehaviour
    {
        const int MaxParticles = 420;
        MatchDirector director;
        CameraRig rig;
        BattleIntervention iv;
        readonly List<Bit> bits = new List<Bit>();
        readonly List<Bolt> bolts = new List<Bolt>();
        readonly SpriteRenderer[] auras = new SpriteRenderer[2];
        readonly Color[] baseWeaponColor = new Color[2];
        readonly float[] emitCarry = new float[2];
        readonly bool[] wasActive = new bool[2];
        readonly float[] ghostCarry = new float[2];
        readonly List<Ghost> ghosts = new List<Ghost>();
        readonly List<Wave> waves = new List<Wave>();
        readonly List<Stamp> stamps = new List<Stamp>();
        readonly SpriteRenderer[] groundGlow = new SpriteRenderer[2];
        SpriteRenderer dimmer, flash;
        float dimAlpha, flashAlpha;
        Color dimColor, flashColor;

        sealed class Ghost { public SpriteRenderer sr; public float age, life; public Color color; }
        sealed class Wave { public LineRenderer lr; public Vector2 at; public float age, life, radius; public Color color; }
        sealed class Stamp { public TextMesh tm; public float age, life, from, to; public Color color; }

        sealed class Bit
        {
            public SpriteRenderer sr;
            public Vector3 vel;
            public float age, life, spin, gravity, drag, size, grow;
            public Color color;
            public Vector3 swirlCenter;
            public float swirl;
        }
        sealed class Bolt { public LineRenderer lr; public float age, life; public Color color; }

        public void Bind(MatchDirector d, CameraRig cameraRig)
        {
            director = d; rig = cameraRig; iv = d.Context.Interventions;
            d.Events.Intervention += OnIntervention;
            d.Events.Hit += OnHit;
            for (int i = 0; i < 2; i++)
            {
                var weapon = d.Fighters[i].WeaponSprite;
                baseWeaponColor[i] = weapon.color;
                // 武器の後ろに少し大きく重ねる「気」。技の間だけ出す
                var aura = new GameObject("SpecialAura" + i).AddComponent<SpriteRenderer>();
                aura.transform.SetParent(transform, false);
                aura.sprite = weapon.sprite;
                aura.sortingOrder = weapon.sortingOrder - 1;
                aura.enabled = false;
                auras[i] = aura;
                // 足元の光の輪（技の間だけ）
                var glow = new GameObject("SpecialGround" + i).AddComponent<SpriteRenderer>();
                glow.transform.SetParent(transform, false);
                glow.sprite = UiKit.DiscSprite; glow.sortingOrder = 2; glow.enabled = false;
                groundGlow[i] = glow;
            }
            // 画面全体: 技の間は背景を暗く沈め、当たりの瞬間に技の色で光らせる（闘う二人より奥 / 手前）
            dimmer = UiKit.Rect(transform, "SpecialDim", Color.clear, 1);
            flash = UiKit.Rect(transform, "SpecialFlash", Color.clear, 95);
        }

        SpecialMovePlan Plan(int side) => iv.Side(side).plan;
        Color Primary(int side) => Plan(side).HasAppearance ? Plan(side).move.PrimaryColor : FighterFactory.TeamColor(side);
        Color Secondary(int side) => Plan(side).HasAppearance ? Plan(side).move.SecondaryColor : FighterFactory.Ink;
        SpecialElement Element(int side) => Plan(side).HasAppearance ? Plan(side).move.Element : SpecialElement.None;
        SpecialEffect Effect(int side) => Plan(side).HasAppearance ? Plan(side).move.Effect : SpecialEffect.Burst;

        void OnIntervention(InterventionEvent e)
        {
            if (e.kind != InterventionKind.Special || !e.success) return;
            var f = director.Fighters[e.fighter];
            Vector2 at = f.ChestWorld;
            if (e.dashed) DashTrail(e.dashFrom, f.Body.position, e.fighter);
            if (e.step == 0)
            {
                // 発動: 気が爆ぜる（カットインで止まっている間も見えるよう、止まった画面に焼き付ける）
                Shape(Effect(e.fighter), at, e.fighter, 1.5f);
                Ring(at, e.fighter, 40, 7f);
                Shockwave(at, Primary(e.fighter), 4.5f, .6f, .22f);
                Shockwave(at, Secondary(e.fighter), 3f, .45f, .12f);
                ElementBurst(Element(e.fighter), at, e.fighter, 40);
                string mark = Plan(e.fighter).HasAppearance ? SpecialMoveRules.ElementLabel(Element(e.fighter)) : f.Loadout.grapheme;
                StampText(mark, f.HeadWorld + Vector2.up * 1.1f, Primary(e.fighter), 2.4f, 1.1f, .9f);
            }
            else
            {
                Shockwave(at, Primary(e.fighter), 2.2f, .3f, .1f);
                ElementBurst(Element(e.fighter), at, e.fighter, 14);
            }
            rig?.Shake(.12f, .2f);
        }

        void OnHit(HitEvent e)
        {
            var s = iv.Side(e.attacker);
            if (!s.OwnsAttack(e.attackId) && !s.chainActive) return;
            int side = e.attacker;
            bool final = s.chainStep >= s.plan.styles.Length || director.Fighters[e.defender].Runtime.hp <= 0f;
            float power = final ? 1.6f : 1f;
            Shape(Effect(side), e.point, side, 1.3f * power);
            ElementBurst(Element(side), e.point, side, (int)(36 * power));
            Shockwave(e.point, Primary(side), 3.5f * power, .4f, .2f);
            Shockwave(e.point, Color.white, 2.2f * power, .28f, .1f);
            Shockwave(e.point, Secondary(side), 5f * power, .55f, .08f);
            for (int i = 0; i < 10; i++)
            {
                // 放射状の斬撃線
                float a = i / 10f * Mathf.PI * 2f + Random.Range(-.15f, .15f);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Line(e.point + dir * .3f, e.point + dir * Random.Range(1.6f, 3f) * power, i % 2 == 0 ? Primary(side) : Color.white, .07f, .22f);
            }
            string mark = Plan(side).HasAppearance ? SpecialMoveRules.ElementLabel(Element(side)) : "斬";
            StampText(mark, e.point + Vector2.up * .2f, Primary(side), 3f * power, 1.3f * power, .55f);
            Flash(Color.Lerp(Primary(side), Color.white, .4f), final ? .85f : .55f);
            TimeController.TriggerHitStop(final ? .45f : .16f);
            rig?.Shake(final ? .45f : .28f, final ? .5f : .3f);
            rig?.ZoomPunch(final ? .22f : .12f, final ? .7f : .4f);
            if (final) StampText("必殺", e.point + new Vector2(0f, 1.8f), Color.white, 2.6f, 1.2f, 1f);
        }

        /// <summary>瞬歩: 元の位置から今の位置まで、残像と速度線を引く。</summary>
        void DashTrail(Vector2 from, Vector2 to, int side)
        {
            var f = director.Fighters[side];
            for (int i = 0; i < 7; i++)
            {
                float k = i / 7f;
                var at = Vector2.Lerp(from, to, k);
                var g = SpawnGhost(f, side, (Vector3)(at - (Vector2)f.Body.position), .45f * (.3f + k));
                if (g != null) g.life = .25f + .35f * k;
            }
            for (int i = 0; i < 6; i++)
            {
                float y = Random.Range(.2f, 1.8f);
                Line(from + Vector2.up * y, to + Vector2.up * y, i % 2 == 0 ? Primary(side) : Color.white, .05f, .3f);
            }
            Shape(SpecialEffect.Burst, from + Vector2.up * .3f, side, .6f);
        }

        Ghost SpawnGhost(Fighter f, int side, Vector3 offset, float alpha)
        {
            if (!TimeController.PresentationEffectsEnabled || ghosts.Count > 60) return null;
            var w = f.WeaponSprite;
            var sr = new GameObject("SpecialGhost").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = w.sprite; sr.flipX = w.flipX; sr.sortingOrder = w.sortingOrder - 2;
            sr.transform.SetPositionAndRotation(w.transform.position + offset + Vector3.forward * .1f, w.transform.rotation);
            sr.transform.localScale = w.transform.lossyScale;
            var c = Primary(side); c.a = alpha;
            sr.color = c;
            var g = new Ghost { sr = sr, life = .28f, color = c };
            ghosts.Add(g);
            return g;
        }

        void Shockwave(Vector2 at, Color color, float radius, float life, float width)
        {
            if (!TimeController.PresentationEffectsEnabled) return;
            var lr = UiKit.Line(transform, "SpecialWave", color, width, 24, true);
            lr.positionCount = 40;
            waves.Add(new Wave { lr = lr, at = at, life = life, radius = radius, color = color });
        }

        void StampText(string text, Vector2 at, Color color, float from, float to, float life)
        {
            if (!TimeController.PresentationEffectsEnabled) return;
            var tm = UiKit.Text(transform, "SpecialStamp", text, color, to, 33);
            UiKit.FitText(tm, to, rig != null ? rig.Cam : Camera.main);
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.transform.position = new Vector3(at.x, at.y, -1.2f);
            stamps.Add(new Stamp { tm = tm, life = life, from = from, to = to, color = color });
        }

        void Flash(Color color, float alpha)
        {
            if (!TimeController.PresentationEffectsEnabled || GameSettings.ReducedEffects) return;
            flashColor = color; flashAlpha = Mathf.Max(flashAlpha, alpha);
        }

        void Popup(string text, Vector2 at, Color color, float height)
        {
            if (!TimeController.PresentationEffectsEnabled) return;
            var tm = UiKit.Text(transform, "SpecialMark", text, color, height, 32);
            UiKit.FitText(tm, height, rig != null ? rig.Cam : Camera.main);
            tm.fontStyle = FontStyle.Bold;
            tm.transform.position = new Vector3(at.x, at.y, -1f);
            Destroy(tm.gameObject, .7f);
        }

        Bit Spawn(Vector2 at, Color color, float size, Vector3 vel, float life, float gravity = 0f, float drag = 0f, bool round = true)
        {
            if (!TimeController.PresentationEffectsEnabled || bits.Count >= MaxParticles) return null;
            var sr = UiKit.Rect(transform, "SpecialBit", color, 22);
            if (round) sr.sprite = UiKit.DiscSprite;
            sr.transform.position = new Vector3(at.x, at.y, -.6f);
            sr.transform.localScale = Vector3.one * size;
            var b = new Bit { sr = sr, vel = vel, life = life, color = color, size = size, gravity = gravity, drag = drag,
                spin = Random.Range(-360f, 360f) };
            bits.Add(b);
            return b;
        }

        void Ring(Vector2 at, int side, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                Spawn(at, i % 2 == 0 ? Primary(side) : Secondary(side), .14f, new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * speed, .45f, 0f, 2f);
            }
        }

        /// <summary>AI が選んだ演出の形。</summary>
        void Shape(SpecialEffect effect, Vector2 at, int side, float scale)
        {
            Color p = Primary(side), s = Secondary(side);
            switch (effect)
            {
                case SpecialEffect.Ring:
                    Ring(at, side, 36, 6f * scale);
                    Ring(at, side, 18, 3f * scale);
                    break;
                case SpecialEffect.Pillar:
                    for (int i = 0; i < 40; i++)
                        Spawn(at + new Vector2(Random.Range(-.35f, .35f) * scale, -1f), i % 3 == 0 ? s : p, Random.Range(.1f, .22f),
                            new Vector3(Random.Range(-.3f, .3f), Random.Range(5f, 10f) * scale), Random.Range(.4f, .7f), -2f, 1f);
                    break;
                case SpecialEffect.Slash:
                    for (int i = 0; i < 4; i++)
                    {
                        float a = Random.Range(0f, 180f) * Mathf.Deg2Rad;
                        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.6f * scale;
                        Line(at - dir, at + dir, i % 2 == 0 ? p : s, .09f, .35f);
                    }
                    for (int i = 0; i < 14; i++) Spawn(at, p, .08f, Random.insideUnitCircle * 7f, .3f, 0f, 3f, false);
                    break;
                case SpecialEffect.Spiral:
                    for (int i = 0; i < 36; i++)
                    {
                        var b = Spawn(at, i % 2 == 0 ? p : s, .13f, Vector3.zero, .8f);
                        if (b == null) break;
                        b.swirlCenter = at; b.swirl = i / 36f * Mathf.PI * 2f; b.grow = 2.2f * scale;
                    }
                    break;
                case SpecialEffect.Rain:
                    for (int i = 0; i < 40; i++)
                        Spawn(at + new Vector2(Random.Range(-1.4f, 1.4f) * scale, Random.Range(1.5f, 3f)), i % 2 == 0 ? p : s,
                            Random.Range(.07f, .14f), new Vector3(0f, -Random.Range(6f, 10f)), Random.Range(.35f, .6f), 6f);
                    break;
                default:
                    for (int i = 0; i < 34; i++)
                        Spawn(at, i % 3 == 0 ? s : p, Random.Range(.1f, .24f), Random.insideUnitCircle.normalized * Random.Range(3f, 9f) * scale,
                            Random.Range(.3f, .6f), 9f, 1.5f);
                    break;
            }
        }

        /// <summary>属性ごとの粒の動き（炎は昇る、氷はゆっくり落ちる、雷は走る…）。</summary>
        void ElementBurst(SpecialElement element, Vector2 at, int side, int count)
        {
            for (int i = 0; i < count; i++) ElementBit(element, at, side, 1f);
        }

        void ElementBit(SpecialElement element, Vector2 at, int side, float energy)
        {
            Color p = Primary(side), s = Secondary(side);
            Color c = Random.value < .65f ? p : s;
            Vector3 r = Random.insideUnitCircle;
            switch (element)
            {
                case SpecialElement.Fire: Spawn(at, c, Random.Range(.1f, .22f), new Vector3(r.x, 2.5f + Random.value * 2f) * energy, .45f, -3f, 1.5f); break;
                case SpecialElement.Thunder:
                    if (Random.value < .25f) Line(at, at + (Vector2)(r.normalized * Random.Range(.6f, 1.4f)), c, .05f, .12f, true);
                    else Spawn(at, c, .06f, r * 6f * energy, .18f, 0f, 4f, false);
                    break;
                case SpecialElement.Ice: Spawn(at, c, Random.Range(.07f, .14f), new Vector3(r.x * 1.5f, -.6f), .9f, 1f, 1f, false); break;
                case SpecialElement.Wind: Spawn(at, c, .08f, new Vector3((Random.value < .5f ? -1 : 1) * Random.Range(4f, 8f), r.y) * energy, .35f, 0f, 2f, false); break;
                case SpecialElement.Water: Spawn(at, c, Random.Range(.08f, .15f), new Vector3(r.x * 3f, Random.Range(1f, 4f)) * energy, .6f, 12f); break;
                case SpecialElement.Earth: Spawn(at, c, Random.Range(.12f, .26f), new Vector3(r.x * 3f, Random.Range(2f, 5f)) * energy, .7f, 16f, 0f, false); break;
                case SpecialElement.Light: Spawn(at + (Vector2)(r * .5f), c, Random.Range(.05f, .16f), Vector3.zero, .4f); break;
                case SpecialElement.Dark: Spawn(at, c, Random.Range(.14f, .3f), r * 1.5f, .7f, -1f, .5f); break;
                case SpecialElement.Flower: Spawn(at, c, Random.Range(.09f, .15f), new Vector3(r.x * 2f + 1f, Random.Range(.5f, 2f)), 1.1f, 1.5f, 1f, false); break;
                case SpecialElement.Poison: Spawn(at, c, Random.Range(.08f, .2f), new Vector3(r.x * .6f, Random.Range(.6f, 1.6f)), .9f, -.5f, .5f); break;
                default: Spawn(at, c, Random.Range(.06f, .14f), r * 3f * energy, .35f, 6f, 1f, false); break;
            }
        }

        void Line(Vector2 a, Vector2 b, Color color, float width, float life, bool jagged = false)
        {
            if (!TimeController.PresentationEffectsEnabled) return;
            var lr = UiKit.Line(transform, "SpecialLine", color, width, 23);
            int n = jagged ? 5 : 2;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)(n - 1));
                if (jagged && i > 0 && i < n - 1) p += Random.insideUnitCircle * .15f;
                lr.SetPosition(i, new Vector3(p.x, p.y, -.6f));
            }
            bolts.Add(new Bolt { lr = lr, life = life, color = color });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                var b = bits[i];
                b.age += dt;
                float t = b.age / b.life;
                if (b.grow > 0f)
                {
                    // 渦: 回りながら広がる
                    float radius = b.grow * t;
                    float a = b.swirl + t * 6f;
                    b.sr.transform.position = b.swirlCenter + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * radius + Vector3.forward * -.6f;
                }
                else
                {
                    b.vel += Vector3.down * b.gravity * dt;
                    b.vel *= Mathf.Max(0f, 1f - b.drag * dt);
                    b.sr.transform.position += b.vel * dt;
                }
                b.sr.transform.Rotate(0f, 0f, b.spin * dt);
                var c = b.color;
                c.a = b.color.a * (1f - Mathf.Clamp01(t));
                b.sr.color = c;
                if (b.age >= b.life) { Destroy(b.sr.gameObject); bits.RemoveAt(i); }
            }
            float u = Time.unscaledDeltaTime;
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var g = ghosts[i];
                g.age += u;
                var c = g.color; c.a = g.color.a * (1f - Mathf.Clamp01(g.age / g.life));
                g.sr.color = c;
                if (g.age >= g.life) { Destroy(g.sr.gameObject); ghosts.RemoveAt(i); }
            }
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i];
                w.age += u;
                float t = Mathf.Clamp01(w.age / w.life);
                float r = w.radius * (1f - Mathf.Pow(1f - t, 3f));
                for (int k = 0; k < w.lr.positionCount; k++)
                {
                    float a = k / (float)w.lr.positionCount * Mathf.PI * 2f;
                    w.lr.SetPosition(k, new Vector3(w.at.x + Mathf.Cos(a) * r, w.at.y + Mathf.Sin(a) * r * .8f, -.7f));
                }
                var c = w.color; c.a = 1f - t;
                w.lr.startColor = w.lr.endColor = c;
                if (w.age >= w.life) { Destroy(w.lr.gameObject); waves.RemoveAt(i); }
            }
            for (int i = stamps.Count - 1; i >= 0; i--)
            {
                var st = stamps[i];
                st.age += u;
                float t = Mathf.Clamp01(st.age / st.life);
                float punch = Mathf.Clamp01(st.age / .08f);
                st.tm.characterSize = Mathf.Lerp(st.from, st.to, 1f - Mathf.Pow(1f - punch, 3f)) * 10f / st.tm.fontSize;
                var c = st.color; c.a = t < .6f ? 1f : 1f - (t - .6f) / .4f;
                st.tm.color = c;
                if (st.age >= st.life) { Destroy(st.tm.gameObject); stamps.RemoveAt(i); }
            }
            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                var b = bolts[i];
                b.age += Time.unscaledDeltaTime;
                var c = b.color; c.a = 1f - Mathf.Clamp01(b.age / b.life);
                b.lr.startColor = b.lr.endColor = c;
                if (b.age >= b.life) { Destroy(b.lr.gameObject); bolts.RemoveAt(i); }
            }
        }

        void LateUpdate()
        {
            if (director == null) return;
            bool anyActive = false;
            Color activeColor = Color.black;
            for (int i = 0; i < 2; i++) if (iv.InSpecial(i) && !director.Ended) { anyActive = true; activeColor = Primary(i); }
            if (rig != null && rig.Cam != null)
            {
                var cam = rig.Cam;
                float h = cam.orthographicSize * 2f;
                var scale = new Vector3(h * cam.aspect + 2f, h + 2f, 1f);
                dimmer.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 5f);
                dimmer.transform.localScale = scale;
                flash.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, -5f);
                flash.transform.localScale = scale;
            }
            float u = Time.unscaledDeltaTime;
            dimAlpha = Mathf.MoveTowards(dimAlpha, anyActive && TimeController.PresentationEffectsEnabled ? .42f : 0f, u * 3f);
            if (anyActive) dimColor = Color.Lerp(Color.black, activeColor, .25f);
            dimmer.color = new Color(dimColor.r, dimColor.g, dimColor.b, dimAlpha);
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, u * 3.5f);
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, GameSettings.ReducedEffects ? 0 : flashAlpha);
            for (int i = 0; i < 2; i++)
            {
                var glow = groundGlow[i];
                bool on = iv.InSpecial(i) && !director.Ended && TimeController.PresentationEffectsEnabled;
                glow.enabled = on;
                if (on)
                {
                    var f = director.Fighters[i];
                    float p = .5f + .5f * Mathf.Sin(Time.unscaledTime * 14f);
                    glow.transform.position = new Vector3(f.X, .05f, .2f);
                    glow.transform.localScale = new Vector3(2.6f + .5f * p, .45f, 1f);
                    var c = Primary(i); c.a = .45f + .25f * p;
                    glow.color = c;
                }
            }
            for (int i = 0; i < 2; i++)
            {
                var f = director.Fighters[i];
                bool active = iv.InSpecial(i) && !director.Ended && !f.Runtime.weaponDetached;
                var weapon = f.WeaponSprite;
                var aura = auras[i];
                if (!active)
                {
                    ghostCarry[i] = 0f;
                    if (wasActive[i]) weapon.color = baseWeaponColor[i];
                    aura.enabled = false; wasActive[i] = false;
                    continue;
                }
                wasActive[i] = true;
                float pulse = .5f + .5f * Mathf.Sin(Time.time * 18f);
                weapon.color = Color.Lerp(baseWeaponColor[i], Primary(i), .55f + .35f * pulse);
                aura.enabled = TimeController.PresentationEffectsEnabled;
                aura.transform.SetPositionAndRotation(weapon.transform.position + Vector3.forward * .05f, weapon.transform.rotation);
                aura.transform.localScale = weapon.transform.localScale * (1.12f + .06f * pulse);
                aura.flipX = weapon.flipX;
                var glow = Secondary(i); glow.a = .45f + .25f * pulse;
                aura.color = glow;
                // 武器の残像（止まっている間は増やさない）
                ghostCarry[i] += Time.deltaTime / .035f;
                while (ghostCarry[i] >= 1f) { ghostCarry[i] -= 1f; SpawnGhost(f, i, Vector3.zero, .5f); }
                // 武器から属性の粒をこぼす（一秒あたりの数は一定）
                emitCarry[i] += Time.deltaTime * 90f;
                var bounds = weapon.bounds;
                while (emitCarry[i] >= 1f)
                {
                    emitCarry[i] -= 1f;
                    var at = new Vector2(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y));
                    ElementBit(Element(i), at, i, .5f);
                }
            }
        }

        void OnDestroy()
        {
            if (director == null) return;
            director.Events.Intervention -= OnIntervention;
            director.Events.Hit -= OnHit;
        }
    }
}
