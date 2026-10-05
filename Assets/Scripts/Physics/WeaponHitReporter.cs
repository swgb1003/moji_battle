using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 武器の接触を HitResolver へ報告するだけ。HP は変更しない（10.4）。
    /// 武器ルート（Rigidbody2D）に付け、子の全 Collider の接触を受ける。
    /// </summary>
    public sealed class WeaponHitReporter : MonoBehaviour
    {
        public Fighter Owner;

        void OnCollisionEnter2D(Collision2D c) => Report(c);
        void OnCollisionStay2D(Collision2D c) => Report(c);

        void Report(Collision2D c)
        {
            if (Owner == null || Owner.Context == null) return;
            var other = c.collider;
            var part = other.GetComponent<BodyPartHitbox>();
            WeaponHitReporter otherWeapon = null;
            if (part == null && other.attachedRigidbody != null) otherWeapon = other.attachedRigidbody.GetComponent<WeaponHitReporter>();
            if (part == null && otherWeapon == null) return;
            var target = part != null ? part.Owner : otherWeapon.Owner;
            if (target == null || target == Owner) return;
            int n = c.contactCount;
            for (int i = 0; i < n; i++)
            {
                var cp = c.GetContact(i);
                Owner.Context.Hits.Report(new HitContact
                {
                    attacker = Owner,
                    target = target,
                    targetIsWeapon = otherWeapon != null,
                    part = part != null ? part.Part : BodyPart.Torso,
                    point = cp.point,
                    normal = cp.normal,
                    relativeVelocity = cp.relativeVelocity,
                    myColliderId = c.otherCollider.GetInstanceID(),
                    otherColliderId = other.GetInstanceID(),
                });
            }
        }
    }
}
