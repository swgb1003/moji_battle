using UnityEngine;

namespace MojiBattle
{
    public sealed class RetroIdle : MonoBehaviour
    {
        Quaternion home;
        void Awake() { home = transform.localRotation; }
        void Update()
        {
            transform.localRotation = home * Quaternion.Euler(0, 0,
                RetroMotionSettings.Reduced ? 0 : Mathf.Sin(Time.unscaledTime * 1.6f) * 1.6f);
        }
    }
}
