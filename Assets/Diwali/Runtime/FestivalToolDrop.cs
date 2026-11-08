using UnityEngine;

namespace Diwali
{
    /// <summary>Gravity and floor contact for recoverable tools, using the same flight step as crackers.</summary>
    public sealed class FestivalToolDrop : MonoBehaviour
    {
        public bool Dropped { get; private set; }
        public bool Moving { get; private set; }
        public Vector3 Velocity { get; private set; }
        FestivalHeld tool;
        BoxCollider shell;
        Vector3 spin;
        bool landed;
        Quaternion restingRotation;

        public void Drop(FestivalHeld value, BoxCollider collider, Vector3 velocity, Vector3 angularVelocity)
        {
            tool = value; shell = collider;
            Velocity = Vector3.ClampMagnitude(velocity, 30);
            spin = Vector3.ClampMagnitude(angularVelocity, 40);
            Vector3 along = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            if (along.sqrMagnitude < .001f) along = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (along.sqrMagnitude < .001f) along = Vector3.forward;
            // Both tools lie on their side once they reach the surface.
            restingRotation = Quaternion.LookRotation(Vector3.up, along.normalized);
            Dropped = Moving = true; landed = false;
        }
        public void Catch() { Dropped = Moving = false; Velocity = spin = Vector3.zero; }

        void Update()
        {
            if (!Moving || !tool) return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Min(Time.deltaTime, .1f) * 240));
            float dt = Mathf.Min(Time.deltaTime, .1f) / steps;
            for (int i = 0; i < steps && Moving; i++)
            {
                Vector3 position = transform.position, velocity = Velocity;
                FestivalToss.Fly(ref position, ref velocity, .03f, dt);
                transform.position = position; Velocity = velocity;
                if (spin.sqrMagnitude > .001f) transform.rotation = Quaternion.AngleAxis(spin.magnitude * Mathf.Rad2Deg * dt, spin.normalized) * transform.rotation;
                Vector3 half = Vector3.Scale(shell.size * .5f, transform.lossyScale);
                float extent = Mathf.Abs(transform.right.y) * half.x + Mathf.Abs(transform.up.y) * half.y + Mathf.Abs(transform.forward.y) * half.z;
                float bottom = transform.TransformPoint(shell.center).y - extent;
                float floor = DiwaliSession.Instance.placement.FloorHeight + .002f;
                if (bottom > floor) continue;
                transform.position += Vector3.up * (floor - bottom);
                float impact = Mathf.Max(0, -Velocity.y);
                if (!landed || impact > .8f) DiwaliSession.Instance.sound.Play("place", transform.position, Mathf.Clamp(impact * .12f, .15f, .6f), .03f);
                landed = true;
                if (tool.ClearsOnLanding)
                {
                    tool.Return();
                    return;
                }
                transform.rotation = Quaternion.Slerp(transform.rotation, restingRotation, 1 - Mathf.Exp(-18 * dt));
                SeatOnFloor(floor);
                float friction = Mathf.Exp(-8 * dt);
                Velocity = new Vector3(Velocity.x * friction, impact > .7f ? impact * .12f : 0, Velocity.z * friction);
                spin *= friction;
                if (Velocity.sqrMagnitude < .02f && spin.sqrMagnitude < .1f && Quaternion.Angle(transform.rotation, restingRotation) < 1)
                {
                    transform.rotation = restingRotation;
                    SeatOnFloor(floor);
                    Moving = false; Velocity = spin = Vector3.zero;
                }
            }
        }

        void SeatOnFloor(float floor)
        {
            Vector3 half = Vector3.Scale(shell.size * .5f, transform.lossyScale);
            float extent = Mathf.Abs(transform.right.y) * half.x + Mathf.Abs(transform.up.y) * half.y + Mathf.Abs(transform.forward.y) * half.z;
            transform.position += Vector3.up * (floor - (transform.TransformPoint(shell.center).y - extent));
        }
    }
}
