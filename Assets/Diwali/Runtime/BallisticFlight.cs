using UnityEngine;

namespace Diwali
{
    /// <summary>
    /// Free flight under gravity and air drag, plus an optional push from a motor. There is deliberately
    /// no target anywhere in here: where something ends up is decided by how it left the ground.
    /// </summary>
    public struct BallisticFlight
    {
        public const float Gravity = 9.81f;

        public Vector3 Position;
        public Vector3 Velocity;

        public BallisticFlight(Vector3 position)
        {
            Position = position;
            Velocity = Vector3.zero;
        }

        /// <param name="thrust">Acceleration from the motor, along <paramref name="axis"/>. Zero once it burns out.</param>
        /// <param name="drag">Quadratic drag coefficient, per metre.</param>
        public void Step(float dt, float thrust, Vector3 axis, float drag)
        {
            Vector3 acceleration = Vector3.down * Gravity - Velocity * (drag * Velocity.magnitude) + axis * thrust;
            Velocity += acceleration * dt;
            Position += Velocity * dt;
        }

        /// <summary>The launch velocity that makes an unpowered shell peak at <paramref name="apex"/>.</summary>
        public static Vector3 LaunchFor(Vector3 from, Vector3 apex, out float secondsToApex)
        {
            float rise = Mathf.Max(apex.y - from.y, 1);
            float upward = Mathf.Sqrt(2 * Gravity * rise);
            secondsToApex = upward / Gravity;
            Vector3 across = apex - from;
            across.y = 0;
            return across / secondsToApex + Vector3.up * upward;
        }
    }

    /// <summary>What a rocket actually did, for validation and for tuning.</summary>
    public struct FlightRecord
    {
        public float seconds, apexHeight, downrange, speedAtBurnout;
        public float climbRateAtBurst, rightwardDistance;
        public Vector3 burstPoint;
        public bool sideSpeedGrewAfterBurnout;
    }
}
