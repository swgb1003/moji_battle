using UnityEngine;

namespace MojiBattle
{
    /// <summary>両者を収める範囲で緩やかに追従・ズーム。強い衝突時だけ短く揺らす（演出専用の乱数を使用）。</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        MatchDirector director;
        float xVel, sizeVel;
        float shakeAmp, shakeTime;
        Vector3 basePos;

        public const float GroundMargin = 1.35f;

        public void Bind(MatchDirector d)
        {
            director = d;
            Cam = GetComponent<Camera>();
            Cam.orthographicSize = 5.2f;
            basePos = new Vector3(0f, Cam.orthographicSize - GroundMargin, -10f);
            transform.position = basePos;
        }

        public void Shake(float amplitude, float duration)
        {
            if (!TimeController.PresentationEffectsEnabled) return;
            shakeAmp = Mathf.Max(shakeAmp, amplitude);
            shakeTime = Mathf.Max(shakeTime, duration);
        }

        void LateUpdate()
        {
            if (director == null || director.Fighters[0] == null) return;
            var a = director.Fighters[0].Body.transform.position;
            var b = director.Fighters[1].Body.transform.position;
            float mid = (a.x + b.x) * 0.5f;
            float dist = Mathf.Abs(a.x - b.x);
            float aspect = Cam.aspect;
            float targetSize = Mathf.Clamp(dist * 0.36f + 3.0f, 4.2f, 5.6f);
            // 高く飛んだ時も見えるように
            float top = Mathf.Max(a.y, b.y) + 2.6f;
            targetSize = Mathf.Max(targetSize, Mathf.Min(6.5f, (top + GroundMargin) * 0.5f / 0.87f));
            float dt = Time.unscaledDeltaTime;
            float size = Mathf.SmoothDamp(Cam.orthographicSize, targetSize, ref sizeVel, 0.35f, Mathf.Infinity, dt);
            Cam.orthographicSize = size;
            float halfW = size * aspect;
            float limit = Mathf.Max(0f, 9.8f - halfW); // 壁の外を見せすぎない
            float targetX = Mathf.Clamp(mid, -limit, limit);
            float x = Mathf.SmoothDamp(basePos.x, targetX, ref xVel, 0.3f, Mathf.Infinity, dt);
            basePos = new Vector3(x, size - GroundMargin, -10f);
            Vector3 offset = Vector3.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                offset = (Vector3)(Random.insideUnitCircle * shakeAmp);
                if (shakeTime <= 0f) shakeAmp = 0f;
            }
            transform.position = basePos + offset;
        }
    }
}
