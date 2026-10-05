using UnityEngine;

namespace MojiBattle
{
    /// <summary>本体の部位 Collider。ダメージ倍率の識別に使う。</summary>
    public sealed class BodyPartHitbox : MonoBehaviour
    {
        public Fighter Owner;
        public BodyPart Part;
    }
}
