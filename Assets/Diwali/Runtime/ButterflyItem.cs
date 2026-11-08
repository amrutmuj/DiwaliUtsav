using System.Collections;
using UnityEngine;

namespace Diwali
{
    /// <summary>
    /// A small spinning butterfly. It rises and comes close to the visitor first, then plays: swinging out
    /// on wide arcs and darting back near them, each arc wider than the last, before it drops home.
    /// </summary>
    public sealed class ButterflyItem : FestivalItem
    {
        const float SparkSpin = 16;

        public Transform body;
        public Transform[] wings;
        public SparkField sparks;
        public ParticleSystem smoke;
        [Tooltip("Seconds in the air.")]
        public float flightSeconds = 9;
        [Tooltip("How many times it swings out and comes back.")]
        public int swings = 3;
        [Tooltip("Width of the first and last arcs, in metres.")]
        public Vector2 arcReach = new Vector2(0.8f, 1.6f);
        [Tooltip("It never comes closer to the visitor than this, in metres.")]
        public float personalSpace = 0.6f;

        public float FurthestReach { get; private set; }
        public float ClosestToVisitor { get; private set; } = float.MaxValue;
        public int Returns { get; private set; }

        protected override IEnumerator Perform()
        {
            Vector3 start = body.localPosition;
            Vector3 home = body.position;
            Vector3 toVisitor = Session.rig.view.transform.position - home;
            toVisitor.y = 0;
            float approach = Mathf.Clamp(toVisitor.magnitude - personalSpace, 0, 1.1f);
            toVisitor = toVisitor.sqrMagnitude > 0.001f ? toVisitor.normalized : transform.forward;
            float orbit = Mathf.Atan2(toVisitor.z, toVisitor.x), lastSwing = 0;
            bool leaving = false;

            sparks.Begin();
            smoke.Play();
            var flutter = Session.sound.StartLoop("butterfly", body, 0, 0.15f);
            for (float t = 0; t < flightSeconds; t += Time.deltaTime)
            {
                if (RemovalHidden) { t -= Time.deltaTime; yield return null; continue; }
                float progress = t / flightSeconds;
                float envelope = Mathf.Sin(Mathf.PI * progress);
                float sway = Mathf.Min(1, envelope * 3);
                float lift = Mathf.SmoothStep(0, 1, t / 0.8f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.88f, 1, progress)));

                // Swing is 0 close by and 1 at the far end of an arc. It darts when near and sails when far.
                float wave = Mathf.Sin(Mathf.PI * swings * progress);
                float swing = wave * wave;
                orbit += (1.6f + 3.2f * (1 - swing)) * Time.deltaTime;
                float reach = 0.12f + Mathf.Lerp(arcReach.x, arcReach.y, progress) * swing;
                Vector3 arc = new Vector3(Mathf.Cos(orbit), 0, Mathf.Sin(orbit)) * reach;
                Vector3 height = Vector3.up * (0.75f + 0.55f * swing + 0.07f * Mathf.Sin(t * 7));
                Vector3 next = home + (toVisitor * (approach * (1 - swing)) + arc + height) * lift;

                // An arc can swing towards the visitor on top of the approach, so the limit is enforced here.
                Vector3 head = Session.rig.view.transform.position;
                Vector3 fromHead = next - head;
                fromHead.y = 0;
                if (fromHead.sqrMagnitude < personalSpace * personalSpace) next = new Vector3(head.x, next.y, head.z) + fromHead.normalized * personalSpace;
                body.position = next;

                if (swing > lastSwing) leaving = true;
                else if (leaving && swing < 0.1f) { leaving = false; Returns++; }
                lastSwing = swing;
                Vector3 flat = body.position - home;
                flat.y = 0;
                FurthestReach = Mathf.Max(FurthestReach, flat.magnitude);
                Vector3 toHead = Session.rig.view.transform.position - body.position;
                toHead.y = 0;
                if (lift > 0.9f) ClosestToVisitor = Mathf.Min(ClosestToVisitor, toHead.magnitude);

                body.localRotation = Quaternion.Euler(Mathf.Sin(t * 11) * 14 * sway, t * 920, Mathf.Cos(t * 9) * 12 * sway);
                for (int i = 0; i < wings.Length; i++) wings[i].localRotation = Quaternion.Euler(0, 0, (i == 0 ? -1 : 1) * (12 + Mathf.Sin(t * 38) * 32));

                // The field follows the body but does not turn with it, or every spark would orbit as one rigid disc.
                sparks.transform.position = body.position;
                sparks.Spin = SparkSpin;
                sparks.Tint = Color.Lerp(SkyShow.Emerald, SkyShow.Rose, 0.5f + 0.5f * Mathf.Sin(t * 2));
                sparks.Density = Mathf.Clamp01(envelope * 2);
                flutter.SetLevel(lift * Mathf.Clamp01(envelope * 3) * (0.72f + 0.2f * (1 - swing)));
                flutter.SetPitch(0.96f + 0.1f * envelope + 0.025f * (1 - swing));
                yield return null;
            }
            sparks.Stop();
            smoke.Stop();
            flutter.Stop(0.2f);
            Session.sky.Pop(body.position, SkyShow.Gold, 0.4f);
            Session.sound.Play("cap", body.position, 0.6f);
            body.localPosition = start;
            body.localRotation = Quaternion.identity;
        }
    }
}
