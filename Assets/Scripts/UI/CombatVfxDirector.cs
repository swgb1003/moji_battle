using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 戦闘結果イベントを購読して演出する（P1 最小版）: 擬音・ダメージ表示、墨飛沫＋陣営色の破片、ガードの白い火花、
    /// 重い一撃のヒットストップと画面揺れ、KO の白フラッシュ・速度線・K.O.。戦闘計算は持たない。
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class CombatVfxDirector : MonoBehaviour
    {
        const int MaxPopups = 6;
        MatchDirector director;
        CameraRig rig;
        readonly List<Popup> popups = new List<Popup>();
        readonly List<Particle> particles = new List<Particle>();
        readonly List<LineRenderer> speedLines = new List<LineRenderer>();
        SpriteRenderer flash;
        float flashAlpha, speedLineAlpha;
        Vector3 koPoint;

        sealed class Popup { public TextMesh tm; public float age, life; public Vector3 vel; }
        sealed class Particle { public SpriteRenderer sr; public Vector3 vel; public float age, life, spin; public Color color; }

        public void Bind(MatchDirector d, CameraRig cameraRig)
        {
            director = d;
            rig = cameraRig;
            var ev = d.Events;
            ev.Hit += OnHit;
            ev.Guard += OnGuard;
            ev.Clash += e => SpawnPopup("弾く", e.point, FighterFactory.Ink, 0.35f);
            ev.EnvImpact += OnEnv;
            ev.Evaded += e => { if (e.kind == EvadeKind.HopOver) SpawnPopup("跳！", d.Fighters[e.fighter].HeadWorld, FighterFactory.Ink, 0.34f); };
            ev.StateChanged += e =>
            {
                if (e.to == FighterState.Knockdown) SpawnPopup("DOWN", d.Fighters[e.fighter].HeadWorld + Vector2.up * 0.4f, FighterFactory.Ink, 0.4f);
            };
            ev.KO += OnKo;
            flash = UiKit.Rect(transform, "KoFlash", new Color(1f, 1f, 1f, 0f), 90);
            for (int i = 0; i < 16; i++)
            {
                var lr = UiKit.Line(transform, "SpeedLine", FighterFactory.Ink, 0.05f, 40);
                speedLines.Add(lr);
            }
        }

        void OnHit(HitEvent e)
        {
            var team = FighterFactory.TeamColor(e.attacker);
            int n = Mathf.Clamp(Mathf.RoundToInt(e.damage * 0.8f), 4, 22);
            Burst(e.point, n, FighterFactory.Ink, 4f + e.vN * 0.4f, 0.09f);
            Burst(e.point, n / 2, team, 5f + e.vN * 0.4f, 0.07f);
            string label = e.critical ? "CRITICAL!" : e.quality == HitQuality.Graze ? "かすり" : "";
            Color c = e.critical ? new Color32(0xF3, 0xC6, 0x5A, 0xFF) : FighterFactory.Ink;
            SpawnPopup($"{(label.Length > 0 ? label + "\n" : "")}{e.damage:F0}", e.point + Vector2.up * 0.3f, c, e.critical ? 0.5f : 0.36f);
            if (e.damage >= director.Context.Balance.heavyHitDamage)
            {
                TimeController.TriggerHitStop(director.Context.Balance.hitStopSeconds);
                rig?.Shake(0.18f, 0.2f);
                SpawnPopup("ドゴォ", e.point + new Vector2(0f, 1.0f), FighterFactory.Ink, 0.7f);
            }
        }

        void OnGuard(GuardEvent e)
        {
            Burst(e.point, 10, Color.white, 6f, 0.08f, outline: true);
            SpawnPopup(e.broke ? "GUARD BREAK" : "GUARD", e.point + Vector2.up * 0.35f, FighterFactory.Ink, 0.4f);
            if (e.broke) rig?.Shake(0.1f, 0.15f);
        }

        void OnEnv(EnvImpactEvent e)
        {
            Burst(e.point, 12, FighterFactory.Ink, 5f, 0.1f);
            string label = e.slam ? "叩きつけ！" : e.isWall ? "ドンッ！" : "ズシャ";
            SpawnPopup($"{label}\n{e.damage:F0}", e.point + Vector2.up * 0.5f, FighterFactory.Ink, e.slam ? 0.6f : 0.5f);
            rig?.Shake(0.15f, 0.18f);
        }

        void OnKo(KoEvent e)
        {
            koPoint = e.point;
            flashAlpha = 0.85f;
            speedLineAlpha = 1f;
            Burst(e.point, 30, FighterFactory.Ink, 8f, 0.12f);
            rig?.Shake(0.3f, 0.35f);
        }

        void SpawnPopup(string text, Vector2 at, Color color, float height)
        {
            if (popups.Count >= MaxPopups)
            {
                Destroy(popups[0].tm.gameObject);
                popups.RemoveAt(0);
            }
            var tm = UiKit.Text(transform, "Popup", text, color, height, 30);
            UiKit.FitText(tm, height, rig != null ? rig.Cam : Camera.main);
            tm.fontStyle = FontStyle.Bold;
            tm.transform.position = new Vector3(at.x, at.y, -1f);
            popups.Add(new Popup { tm = tm, life = 0.8f, vel = new Vector3(0f, 0.8f, 0f) });
        }

        void Burst(Vector2 at, int count, Color color, float speed, float size, bool outline = false)
        {
            if (!TimeController.PresentationEffectsEnabled) return;
            for (int i = 0; i < count; i++)
            {
                var sr = UiKit.Rect(transform, "Particle", color, outline ? 21 : 20);
                float a = Random.Range(0f, Mathf.PI * 2f);
                float v = speed * Random.Range(0.3f, 1f);
                sr.transform.position = new Vector3(at.x, at.y, -0.5f);
                sr.transform.localScale = new Vector3(size * Random.Range(0.6f, 1.6f), size * Random.Range(0.4f, 1f), 1f);
                particles.Add(new Particle
                {
                    sr = sr, vel = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * v,
                    life = Random.Range(0.25f, 0.5f), spin = Random.Range(-720f, 720f), color = color,
                });
                if (outline)
                {
                    var back = UiKit.Rect(sr.transform, "Outline", FighterFactory.Ink, 19);
                    back.transform.localScale = new Vector3(1.6f, 1.8f, 1f);
                }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                p.age += Time.unscaledDeltaTime;
                p.tm.transform.position += p.vel * Time.unscaledDeltaTime;
                var c = p.tm.color;
                c.a = 1f - Mathf.Clamp01((p.age - p.life * 0.6f) / (p.life * 0.4f));
                p.tm.color = c;
                if (p.age >= p.life) { Destroy(p.tm.gameObject); popups.RemoveAt(i); }
            }
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.age += dt;
                p.vel += Vector3.down * 9f * dt;
                p.sr.transform.position += p.vel * dt;
                p.sr.transform.Rotate(0f, 0f, p.spin * dt);
                var c = p.color;
                c.a = 1f - Mathf.Clamp01(p.age / p.life);
                p.sr.color = c;
                if (p.age >= p.life) { Destroy(p.sr.gameObject); particles.RemoveAt(i); }
            }
        }

        void LateUpdate()
        {
            if (rig == null || rig.Cam == null) return;
            var cam = rig.Cam;
            float h = cam.orthographicSize * 2f;
            flash.transform.position = cam.transform.position + new Vector3(0f, 0f, 9f);
            flash.transform.localScale = new Vector3(h * cam.aspect + 1f, h + 1f, 1f);
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 2.5f);
            flash.color = new Color(1f, 1f, 1f, flashAlpha);

            speedLineAlpha = Mathf.MoveTowards(speedLineAlpha, 0f, Time.unscaledDeltaTime * 0.9f);
            for (int i = 0; i < speedLines.Count; i++)
            {
                var lr = speedLines[i];
                if (speedLineAlpha <= 0f) { lr.positionCount = 0; continue; }
                float a = i / (float)speedLines.Count * Mathf.PI * 2f + 0.2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                lr.positionCount = 2;
                lr.SetPosition(0, koPoint + dir * 1.4f);
                lr.SetPosition(1, koPoint + dir * 4.5f);
                var c = FighterFactory.Ink;
                c.a = speedLineAlpha * 0.8f;
                lr.startColor = lr.endColor = c;
            }
        }
    }
}
