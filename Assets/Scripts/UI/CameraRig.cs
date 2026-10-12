using UnityEngine;

namespace MojiBattle
{
    /// <summary>両者と文字武器を近くから追い、離れたり飛んだりしたときは画角を広げる。</summary>
    [DefaultExecutionOrder(50)]
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public Bounds FocusBounds { get; private set; }
        MatchDirector director;
        float xVel, yVel, sizeVel;
        float shakeAmp, shakeTime;
        float zoomAmount, zoomTime, zoomDuration;
        Vector3 basePos;

        public const float GroundMargin = 1.35f;

        public void Bind(MatchDirector d)
        {
            director = d;
            Cam = GetComponent<Camera>();
            xVel = yVel = sizeVel = shakeAmp = shakeTime = zoomAmount = zoomTime = zoomDuration = 0;
            Cam.orthographicSize = 4f;
            basePos = new Vector3(0f, Cam.orthographicSize * (1 - 2 * BattleHudPresenter.GroundViewport), -10f);
            transform.position = basePos;
        }

        public void Shake(float amplitude, float duration)
        {
            if (!TimeController.PresentationEffectsEnabled || GameSettings.ReducedEffects) return;
            shakeAmp = Mathf.Max(shakeAmp, amplitude);
            shakeTime = Mathf.Max(shakeTime, duration);
        }

        /// <summary>一瞬だけ寄る（画面の大きさを amount の割合だけ縮め、duration 秒で戻す）。</summary>
        public void ZoomPunch(float amount, float duration)
        {
            if (!TimeController.PresentationEffectsEnabled || GameSettings.ReducedEffects) return;
            zoomAmount = amount;
            zoomTime = zoomDuration = Mathf.Max(0.01f, duration);
        }

        void LateUpdate()
        {
            if (director == null || director.Fighters[0] == null) return;
            var bounds = FighterBounds(director.Fighters[0]);
            bounds.Encapsulate(FighterBounds(director.Fighters[1]));
            FocusBounds = bounds;
            float aspect = Mathf.Max(.1f, Cam.aspect);
            float fitSize = Mathf.Max((bounds.size.x + 1.6f) / (2f * aspect), (bounds.size.y + .9f) * .5f);
            float targetSize = Mathf.Max(2.6f, fitSize);
            float dt = Time.unscaledDeltaTime;
            if (GameSettings.ReducedEffects) shakeAmp = shakeTime = zoomAmount = zoomTime = 0;
            float size;
            if (zoomTime > 0f)
            {
                // 寄りは追従の遅れなしで効かせ、時間とともに元の大きさへ戻す
                zoomTime -= dt;
                float k = Mathf.Clamp01(zoomTime / zoomDuration);
                size = Mathf.Max(fitSize, targetSize * (1f - zoomAmount * k));
                sizeVel = 0f;
            }
            else size = Mathf.SmoothDamp(Cam.orthographicSize, targetSize, ref sizeVel,
                targetSize > Cam.orthographicSize ? .12f : .4f, Mathf.Infinity, dt);
            // React immediately only when the current view would cut off a fighter or weapon.
            size = Mathf.Max(size, fitSize);
            Cam.orthographicSize = size;
            float halfW = size * aspect;
            float targetX = Mathf.Clamp(bounds.center.x, -director.Context.ArenaHalfWidth, director.Context.ArenaHalfWidth);
            float x = Mathf.SmoothDamp(basePos.x, targetX, ref xVel, .18f, Mathf.Infinity, dt);
            x = Mathf.Clamp(x, bounds.max.x - halfW + .4f, bounds.min.x + halfW - .4f);
            float targetY = Mathf.Max(size * (1 - 2 * BattleHudPresenter.GroundViewport), bounds.center.y + .15f);
            float y = Mathf.SmoothDamp(basePos.y, targetY, ref yVel, .18f, Mathf.Infinity, dt);
            y = Mathf.Clamp(y, bounds.max.y - size + .35f, bounds.min.y + size - .15f);
            basePos = new Vector3(x, y, -10f);
            Vector3 offset = Vector3.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                offset = (Vector3)(Random.insideUnitCircle * shakeAmp);
                if (shakeTime <= 0f) shakeAmp = 0f;
            }
            transform.position = basePos + offset;
        }

        static Bounds FighterBounds(Fighter f)
        {
            var bounds = new Bounds(f.Body.transform.position, Vector3.zero);
            foreach (var collider in f.BodyColliders) bounds.Encapsulate(collider.bounds);
            // Frame the actual ink bounds rather than the transparent padding in the glyph sprite.
            var ink = f.Weapon.boundsLocal;
            var sprite = f.WeaponSprite;
            foreach (var point in new[] { ink.min, new Vector2(ink.xMin, ink.yMax), ink.max, new Vector2(ink.xMax, ink.yMin) })
            {
                var local = new Vector3(sprite.flipX ? -point.x : point.x, point.y, 0);
                bounds.Encapsulate(sprite.transform.TransformPoint(local));
            }
            return bounds;
        }
    }
}
