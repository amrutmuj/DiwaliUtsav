using System.Collections;
using UnityEngine;

namespace Diwali
{
    /// <summary>Chakri: whirls up to speed on the floor, shifting colour as the composition burns through.</summary>
    public sealed class SpinnerItem : FestivalItem
    {
        public Transform disc;
        public SparkField sparks;
        public FlameDriver rim;
        public ParticleSystem smoke;
        public float duration = 9;
        public float mass = 0.025f;
        public float discRadius = 0.06f;
        public float jetTorque = 0.008f;
        public float rotationalDrag = 1.6f;
        [Tooltip("Push from the uneven burn, in metres per second squared. This is what moves the disc about the floor.")]
        public float skitter = 0.8f;
        public float AngularSpeed { get; private set; }
        public float PeakAngularSpeed { get; private set; }
        public float LongestDrift { get; private set; }
        public float CoastSeconds { get; private set; }

        static readonly Color[] Phases = { SkyShow.Gold, SkyShow.Emerald, SkyShow.Rose, SkyShow.Gold };

        protected override IEnumerator Perform()
        {
            sparks.Begin();
            rim.Ignite();
            smoke.Play();
            var whirr = Session.sound.StartLoop("chakri", disc, 0, 0.4f);
            Vector3 centre = transform.position;

            float elapsed = 0, angle = 0, wanderSeed = Random.value * 50;
            bool burning = true;
            Vector3 driftVelocity = Vector3.zero;
            AngularSpeed = PeakAngularSpeed = LongestDrift = CoastSeconds = 0;
            float inertia = Mathf.Max(0.000001f, mass * discRadius * discRadius * 0.5f);
            float fullSpeed = jetTorque / (inertia * rotationalDrag);

            while (burning || AngularSpeed > 0.5f)
            {
                if (RemovalHidden) { yield return null; continue; }
                elapsed += Time.deltaTime;
                float dt = Mathf.Min(Time.deltaTime, 0.05f);
                float progress = Mathf.Clamp01(elapsed / duration);
                float power = Mathf.SmoothStep(0, 1, elapsed / 1.6f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.82f, 1, progress)));

                if (burning && elapsed >= duration)
                {
                    // The composition is spent, but the disc keeps its momentum and runs down on drag alone.
                    burning = false;
                    sparks.Stop();
                    rim.Extinguish();
                    smoke.Stop();
                }
                if (!burning) CoastSeconds += Time.deltaTime;

                AngularSpeed = Mathf.Max(0, (AngularSpeed + jetTorque * power / inertia * dt) / (1 + rotationalDrag * dt));
                float spin = AngularSpeed;
                PeakAngularSpeed = Mathf.Max(PeakAngularSpeed, spin);

                angle += spin * Mathf.Rad2Deg * Time.deltaTime;
                disc.localRotation = Quaternion.Euler(0, angle, 0);

                // The jet turns far too fast to carry the disc anywhere. A slowly shifting imbalance in
                // the burn is what makes a real chakri skitter, and friction stops it once the burn ends.
                Vector3 offset = transform.position - centre;
                LongestDrift = Mathf.Max(LongestDrift, offset.magnitude);
                if (burning)
                {
                    float heading = Mathf.PerlinNoise(wanderSeed, elapsed * 0.6f) * Mathf.PI * 6;
                    Vector3 push = new Vector3(Mathf.Cos(heading), 0, Mathf.Sin(heading)) * (skitter * power);
                    driftVelocity += (push - offset * 6) * dt;
                }
                driftVelocity *= Mathf.Exp((burning ? -2.5f : -6) * dt);
                transform.position += driftVelocity * dt;

                sparks.Spin = spin;
                sparks.Density = Mathf.Lerp(0.15f, 1, power);
                sparks.SpeedScale = Mathf.Lerp(0.6f, 1.1f, power);
                float phase = progress * (Phases.Length - 1);
                int index = Mathf.Min((int)phase, Phases.Length - 2);
                sparks.Tint = Color.Lerp(Phases[index], Phases[index + 1], Mathf.SmoothStep(0, 1, phase - index));

                if (burning) rim.SetTarget(power);

                // The whirr follows the disc itself, so it winds down with the momentum, not with the burn.
                float speed = Mathf.Clamp01(spin / fullSpeed);
                whirr.SetLevel(speed);
                whirr.SetPitch(Mathf.Lerp(0.7f, 1.12f, speed));
                yield return null;
            }

            AngularSpeed = 0;
            whirr.Stop(0.4f);
            yield return new WaitForSeconds(1.2f);
        }
    }
}
