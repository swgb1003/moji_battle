using UnityEngine;

namespace MojiBattle
{
    /// <summary>アリーナの壁・地面。</summary>
    public sealed class ArenaSurface : MonoBehaviour
    {
        public enum SurfaceKind { Ground, Wall }
        public SurfaceKind Kind;
    }
}
