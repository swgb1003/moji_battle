using UnityEngine;

namespace MojiBattle
{
    /// <summary>Presentation offsets only. Grip, rigidbodies and hitboxes remain physical.</summary>
    public struct CombatPose
    {
        public float lean, crouch, twist, step, knee;
        public Vector3 head, chest;

        public static CombatPose Sample(Fighter f, float reaction, BodyPart part, bool guarded)
        {
            var r = f.Runtime;
            var pose = new CombatPose();
            if (r.IsDown) return pose;
            float weight = Mathf.Clamp01(f.Stats.weightScore / 100f);
            float t = 0f;
            if (r.state == FighterState.AttackWindup)
            {
                t = Mathf.SmoothStep(0, 1, r.stateTime / Mathf.Max(.01f, r.windupDuration));
                pose.lean = -Mathf.Lerp(.11f, .23f, weight) * t;
                pose.crouch = Mathf.Lerp(.06f, .15f, weight) * t;
                pose.twist = -.1f * t;
            }
            else if (r.state == FighterState.AttackActive)
            {
                t = Mathf.SmoothStep(0, 1, r.stateTime / Mathf.Max(.01f, r.activeDuration));
                pose.lean = Mathf.Lerp(-.1f, Mathf.Lerp(.2f, .31f, weight), t);
                pose.crouch = .06f * (1 - t);
                pose.twist = Mathf.Lerp(-.1f, .15f, t);
                pose.step = Mathf.Lerp(.06f, .2f, weight) * t;
            }
            else if (r.state == FighterState.AttackRecovery)
            {
                t = 1 - Mathf.SmoothStep(0, 1, r.stateTime / Mathf.Max(.01f, r.recoveryDuration));
                pose.lean = Mathf.Lerp(.2f, .31f, weight) * t;
                pose.twist = .15f * t;
                pose.step = Mathf.Lerp(.06f, .2f, weight) * t;
            }
            else if (r.state == FighterState.Guard) { pose.lean = -.09f; pose.crouch = .07f; }
            else if (r.state == FighterState.Bind)
            {
                pose.lean = .24f;
                pose.crouch = .14f;
                pose.twist = GameSettings.ReducedEffects ? 0 : Mathf.Sin(f.Context.SimTime * 43) * .018f;
            }
            if (r.attackStyle == AttackStyle.Thrust) { pose.twist *= .4f; pose.step *= 1.25f; }
            pose.lean *= f.Facing;
            pose.twist *= f.Facing;
            pose.step *= f.Facing;
            if (reaction > 0)
            {
                if (guarded) { pose.lean -= .2f * reaction * f.Facing; pose.crouch += .13f * reaction; }
                else switch (part)
                {
                    case BodyPart.Head:
                        pose.lean -= .28f * reaction * f.Facing;
                        pose.head = new Vector3(-.18f * f.Facing, .025f, 0) * reaction;
                        break;
                    case BodyPart.Leg:
                        pose.crouch += .22f * reaction; pose.knee = .19f * reaction;
                        pose.lean += .12f * reaction * f.Facing;
                        break;
                    case BodyPart.Arm:
                        pose.twist -= .18f * reaction * f.Facing;
                        pose.lean -= .16f * reaction * f.Facing;
                        break;
                    default:
                        pose.chest = new Vector3(.13f * f.Facing, -.11f, 0) * reaction;
                        pose.lean += .19f * reaction * f.Facing;
                        pose.crouch += .07f * reaction;
                        break;
                }
            }
            return pose;
        }
    }
}
