using System.Collections;
using UnityEngine;

namespace Diwali
{
    /// <summary>
    /// A steep upward launch with a small, fixed rightward lean and a burst while still climbing.
    /// </summary>
    public sealed class RocketItem : FestivalItem
    {
        const float SimulationStep = 1 / 240f;
        const float MaximumFlightSeconds = 6;
        const float BurstBelowClimbRate = 9;

        public Transform body;
        public ParticleSystem trail;
        public FlameDriver exhaust;
        [Tooltip("Keeps the rocket visible as a point of light once it is far away.")]
        public Renderer burningTail;
        [Tooltip("Acceleration from the motor, in metres per second squared.")]
        public float thrust = 32.4f;
        [Tooltip("Small rightward lean, measured from vertical at launch. It never steers towards a target.")]
        [Range(0, 5)] public float rightLeanDegrees = 2.5f;
        public float burnSeconds = 0.8f;
        [Tooltip("Quadratic air drag, per metre.")]
        public float airDrag = 0.006f;
        [Tooltip("How far the burst opens, in metres. A hand-sized rocket carries a small charge.")]
        public float burstRadius = 4.5f;

        public static FlightRecord LastFlight { get; private set; }

        protected override IEnumerator Perform()
        {
            Session.rig.Rumble(0.6f, 0.2f);

            // Capture right once at launch. Turning the head later cannot steer the rocket.
            Vector3 right = Vector3.ProjectOnPlane(Session.rig.view.transform.right, Vector3.up).normalized;
            Vector3 launchAxis = (Vector3.up + right * Mathf.Tan(Mathf.Clamp(rightLeanDegrees, 0, 5) * Mathf.Deg2Rad)).normalized;
            body.rotation = Quaternion.FromToRotation(Vector3.up, launchAxis);
            var flight = new BallisticFlight(body.position);
            var record = new FlightRecord();
            float groundHeight = transform.position.y;
            Vector3 start = flight.Position;

            var motor = Session.sound.PlayFollowing("rocket_motor", body);
            SoundLoop whistle = null;
            trail.Play();
            exhaust.Ignite();
            burningTail.enabled = true;

            float elapsed = 0, leftover = 0, sideSpeed = 0;
            bool burning = true;
            bool burstReady = false;

            while (elapsed < MaximumFlightSeconds)
            {
                leftover += Time.deltaTime;
                while (leftover >= SimulationStep && !burstReady)
                {
                    leftover -= SimulationStep;
                    elapsed += SimulationStep;

                    // One fixed motor axis; no homing, changing target, or terminal turn.
                    Vector3 axis = launchAxis;
                    flight.Step(SimulationStep, burning ? thrust : 0, axis, airDrag);

                    float side = new Vector2(flight.Velocity.x, flight.Velocity.z).magnitude;
                    if (!burning && side > sideSpeed + 0.001f) record.sideSpeedGrewAfterBurnout = true;
                    sideSpeed = side;

                    if (burning && elapsed >= burnSeconds)
                    {
                        burning = false;
                        record.speedAtBurnout = flight.Velocity.magnitude;
                        exhaust.Extinguish();
                        motor.Stop(0.06f);
                        whistle = Session.sound.PlayFollowing("whistle", body, 0.55f, 0.08f);
                    }
                    burstReady = !burning && flight.Velocity.y <= BurstBelowClimbRate;
                }

                body.position = flight.Position;
                if (flight.Velocity.sqrMagnitude > 0.01f) body.rotation = Quaternion.FromToRotation(Vector3.up, flight.Velocity.normalized);

                if (burstReady) break;
                yield return null;
            }

            record.seconds = elapsed;
            record.burstPoint = flight.Position;
            record.apexHeight = flight.Position.y - groundHeight;
            record.downrange = new Vector2(flight.Position.x - start.x, flight.Position.z - start.z).magnitude;
            record.climbRateAtBurst = flight.Velocity.y;
            record.rightwardDistance = Vector3.Dot(flight.Position - start, right);
            LastFlight = record;

            motor.Stop(0.05f);
            whistle?.Stop(0.08f);
            trail.Stop();
            body.gameObject.SetActive(false);
            Session.sky.BurstInSky(flight.Position, BurstStyle.RedSpider().Sized(burstRadius));
            yield return new WaitForSeconds(3);
        }
    }
}
