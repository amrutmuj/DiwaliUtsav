using UnityEngine;

namespace Diwali
{
    /// <summary>
    /// Flight and landing for every cracker, using its <see cref="CrackerPhysics"/>. Pops fly freely from
    /// the hand. Everything else is lobbed to its aimed spot, then bounces and rocks there as its material would.
    /// </summary>
    public sealed class FestivalToss : MonoBehaviour
    {
        const float StepsPerSecond = 240;
        const float PreviewStepsPerSecond = 120;
        const float LongestPreviewSeconds = 6;

        /// <summary>Beyond any throw made in a room, so this only ever stops a tracking glitch.</summary>
        const float FastestThrow = 30;

        public bool Flying { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }
        FestivalItem item;
        FestivalPlacement placement;
        BoxCollider shell;
        float quietTime;
        Quaternion upright;
        bool landed, aimed;
        Vector3 target;
        CrackerPhysics physics;

        /// <summary>Lobs an item so it comes down on <paramref name="landing"/>, whatever its bounce does next.</summary>
        public void SetDown(FestivalItem value, FestivalPlacement owner, Vector3 landing, Quaternion rest, float seconds)
        {
            Vector3 from = transform.position;
            Vector3 lob = (landing - from) / seconds + Vector3.up * (0.5f * BallisticFlight.Gravity * seconds);
            Launch(value, owner, lob, Vector3.zero);
            upright = rest;
            target = landing;
            aimed = true;
        }

        public void Launch(FestivalItem value, FestivalPlacement owner, Vector3 velocity, Vector3 angularVelocity)
        {
            physics = value.Physics;
            aimed = false;
            item = value;
            placement = owner;
            shell = GetComponent<BoxCollider>();
            Velocity = Vector3.ClampMagnitude(velocity, FastestThrow);
            AngularVelocity = Vector3.ClampMagnitude(angularVelocity, 40);
            upright = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            quietTime = 0;
            landed = false;
            Flying = true;
        }

        public void Catch() { Flying = false; Velocity = AngularVelocity = Vector3.zero; }

        /// <summary>One step of free flight. The landing preview and the real flight share it, so they cannot disagree.</summary>
        public static void Fly(ref Vector3 position, ref Vector3 velocity, float drag, float dt)
        {
            velocity += Vector3.down * (BallisticFlight.Gravity * dt);
            velocity *= Mathf.Exp(-drag * dt);
            position += velocity * dt;
        }

        public static Vector3 PredictLanding(Vector3 from, Vector3 velocity, float floor, float drag = 0)
        {
            Vector3 point = from;
            Vector3 moving = Vector3.ClampMagnitude(velocity, FastestThrow);
            for (float flown = 0; flown < LongestPreviewSeconds; flown += 1 / PreviewStepsPerSecond)
            {
                Vector3 before = point;
                Fly(ref point, ref moving, drag, 1 / PreviewStepsPerSecond);
                if (point.y > floor || moving.y >= 0) continue;
                point = Vector3.Lerp(before, point, Mathf.InverseLerp(before.y, point.y, floor));
                break;
            }
            point.y = floor;
            return point;
        }

        void Update()
        {
            if (!Flying || !item) return;
            // Runs on the rendered frame, because the 50 Hz physics tick judders against a 72 Hz display.
            // Equal substeps keep fast throws and floor contacts stable even on a slow frame.
            float elapsed = Mathf.Min(Time.deltaTime, 0.1f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(elapsed * StepsPerSecond));
            float dt = elapsed / steps;
            transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.one, elapsed / 0.2f);
            for (int i = 0; i < steps && Flying; i++) Step(dt);
        }

        void Step(float dt)
        {
            // A lob is solved without drag, so that it arrives exactly on its ring.
            Vector3 position = transform.position, velocity = Velocity;
            Fly(ref position, ref velocity, aimed ? 0 : physics.airDrag, dt);
            transform.position = position;
            Velocity = velocity;
            if (aimed && !landed) transform.rotation = Quaternion.Slerp(transform.rotation, upright, 1 - Mathf.Exp(-12 * dt));
            float spin = AngularVelocity.magnitude;
            if (spin > 0.001f) transform.rotation = Quaternion.AngleAxis(spin * Mathf.Rad2Deg * dt, AngularVelocity / spin) * transform.rotation;

            float bottom = Bottom();
            float floor = placement.FloorHeight + 0.002f;
            if (bottom > floor) { quietTime = 0; return; }
            transform.position += Vector3.up * (floor - bottom);
            float impact = Mathf.Max(0, -Velocity.y);
            if (item.FloorImpact(impact)) { Flying = false; return; }

            // The sound belongs to the contact, so a firm bounce knocks again and coming to rest is silent.
            if (!landed || impact > 0.8f) DiwaliSession.Instance.sound.Play("place", transform.position, Mathf.Clamp(impact * 0.2f, 0.25f, 1) * physics.thud, 0.08f, 0, physics.knock);
            if (aimed && !landed)
            {
                transform.position = new Vector3(target.x, transform.position.y, target.z);
                Velocity = new Vector3(0, Velocity.y, 0);
                Vector2 tilt = Random.insideUnitCircle.normalized;
                AngularVelocity = new Vector3(tilt.x, 0, tilt.y) * physics.wobble;
            }
            landed = true;

            float slide = Mathf.Exp(-physics.friction * dt);
            Velocity = new Vector3(Velocity.x * slide, impact > 0.7f ? impact * physics.bounce : 0, Velocity.z * slide);
            AngularVelocity *= Mathf.Exp(-physics.spinLoss * dt);
            if (Velocity.sqrMagnitude < 0.08f && AngularVelocity.sqrMagnitude < 0.4f)
            {
                quietTime += dt;
                transform.rotation = Quaternion.Slerp(transform.rotation, upright, 1 - Mathf.Exp(-18 * dt));
                if (quietTime > (aimed ? 0.12f : 0.35f))
                {
                    Flying = false;
                    transform.rotation = upright;
                    transform.position = new Vector3(transform.position.x, floor, transform.position.z);
                    placement.TossSettled(item);
                }
            }
        }

        /// <summary>
        /// The lowest point of the rotated box, so a tumbling cracker never sinks through the floor. Boxes
        /// reach a little below the model's base, which only counts once the item has tipped over. Upright,
        /// this is the base itself, so the item does not drop the last few centimetres as it settles.
        /// </summary>
        float Bottom()
        {
            if (!shell) return transform.position.y;
            Vector3 scale = transform.lossyScale;
            Vector3 half = Vector3.Scale(shell.size * 0.5f, scale);
            float extent = Mathf.Abs(transform.right.y) * half.x + Mathf.Abs(transform.up.y) * half.y + Mathf.Abs(transform.forward.y) * half.z;
            float belowBase = Mathf.Max(0, half.y - shell.center.y * scale.y);
            return transform.TransformPoint(shell.center).y - extent + belowBase * Mathf.Max(0, transform.up.y);
        }
    }
}
