using UnityEngine;

namespace MojiBattle
{
    /// <summary>本体と壁・地面の衝突を EnvironmentImpactResolver へ報告する。</summary>
    public sealed class BodyContactReporter : MonoBehaviour
    {
        public Fighter Owner;

        void OnCollisionEnter2D(Collision2D c)
        {
            if (Owner == null || Owner.Context == null) return;
            var surface = c.collider.GetComponent<ArenaSurface>();
            if (surface == null) return;
            int n = c.contactCount;
            if (n == 0) return;
            var cp = c.GetContact(0);
            Owner.Context.Events.Raise(new SurfaceContactEvent
            {
                fighter = Owner.Id,
                isWall = surface.Kind == ArenaSurface.SurfaceKind.Wall,
                down = Owner.Runtime.IsDown,
                speed = Mathf.Max(Mathf.Abs(Vector2.Dot(c.relativeVelocity, cp.normal)),
                    Mathf.Abs(Vector2.Dot(Owner.PreBodyVelocity, cp.normal))),
                time = Owner.Context.SimTime,
                point = cp.point,
            });
            Owner.Context.Env.Report(new EnvContact
            {
                fighter = Owner,
                isWall = surface.Kind == ArenaSurface.SurfaceKind.Wall,
                point = cp.point,
                normal = cp.normal,
                relativeVelocity = c.relativeVelocity,
            });
        }
    }
}
