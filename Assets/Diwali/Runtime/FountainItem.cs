using System.Collections;
using UnityEngine;

namespace Diwali
{
    /// <summary>Anar and flower pot: a roaring fountain that swells, holds and dies away.</summary>
    public sealed class FountainItem : FestivalItem
    {
        public SparkField sparks;
        public FlameDriver jet;
        public ParticleSystem smoke;
        public float duration = 11;
        [Tooltip("A flower pot turns from one colour to another halfway through.")]
        public Color secondColour;

        protected override IEnumerator Perform()
        {
            sparks.Begin();
            jet.Ignite();
            smoke.Play();
            var roar = Session.sound.StartLoop("anar", jet.transform, 0, 0.5f);
            Color first = sparks.GetComponent<Renderer>().sharedMaterial.GetColor("_HotColor");

            float elapsed = 0;
            while (elapsed < duration)
            {
                if (RemovalHidden) { yield return null; continue; }
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float swell = Mathf.SmoothStep(0, 1, elapsed / 1.3f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.84f, 1, progress)));
                float surge = 0.9f + 0.2f * Mathf.PerlinNoise(Time.time * 1.6f, 3);
                sparks.Density = Mathf.Lerp(0.2f, 1, swell);
                sparks.SpeedScale = Mathf.Lerp(0.45f, 1, swell) * surge;
                if (secondColour.a > 0) sparks.Tint = Color.Lerp(first, secondColour, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.4f, 0.6f, progress)));
                jet.SetTarget(swell);
                roar.SetLevel(swell);
                yield return null;
            }

            sparks.Stop();
            jet.Extinguish();
            smoke.Stop();
            roar.Stop(0.5f);
            yield return new WaitForSeconds(1.4f);
        }
    }
}
