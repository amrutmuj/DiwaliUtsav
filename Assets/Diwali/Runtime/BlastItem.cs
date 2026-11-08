using System.Collections;
using UnityEngine;

namespace Diwali
{
    /// <summary>One synchronized report, flash and expanding plume, all at the bomb's actual site.</summary>
    public sealed class BlastItem : FestivalItem
    {
        public ParticleSystem clouds;
        public FlameDriver[] flames;
        public float flameSeconds = 0.22f;
        public int cloudPuffs = 130;
        public float cloudSeconds = 1.25f;
        public Vector3 LastDetonationPoint { get; private set; }
        public int DetonationFrame { get; private set; }
        public float DetonationTime { get; private set; }
        public int PuffsAtDetonation { get; private set; }

        protected override IEnumerator Perform()
        {
            Vector3 site = transform.position + Vector3.up * 0.045f;
            LastDetonationPoint = site;
            DetonationFrame = Time.frameCount;
            DetonationTime = Time.time;
            model.SetActive(false);
            var shadow = transform.Find("Contact shadow");
            if (shadow) shadow.gameObject.SetActive(false);

            // These begin in the same frame. The pressure front and smoke expand from the charge.
            Session.sound.Play("bomb", site, 1, 0.015f);
            Session.sky.Pop(site, new Color(1, 0.8f, 0.5f), 1.8f);
            Session.sky.ShowFlash(site, new Color(1, 0.72f, 0.4f), 1.2f, 0.12f, 1.8f);
            Session.sky.Embers(site, 70, 5);
            Session.ambience.Flash(Session.Gentle ? 0.08f : 0.3f);
            Session.rig.Rumble(1, 0.22f);
            foreach (var flame in flames) flame.SetTarget(Session.Gentle ? 0.5f : 1);
            int total = Session.Gentle ? cloudPuffs * 2 / 3 : cloudPuffs;
            int initial = Mathf.Min(40, total);
            for (int i = 0; i < initial; i++) EmitPuff(site, i, true);
            PuffsAtDetonation = clouds.particleCount;
            float elapsed = 0;
            int emitted = initial;
            while (elapsed < cloudSeconds)
            {
                elapsed += Time.deltaTime;
                int wanted = initial + Mathf.FloorToInt((total - initial) * Mathf.Clamp01(elapsed / cloudSeconds));
                while (emitted < wanted) EmitPuff(site, emitted++, false);
                if (elapsed >= flameSeconds) foreach (var flame in flames) flame.Extinguish();
                yield return null;
            }
            foreach (var flame in flames) flame.Extinguish();
            yield return new WaitForSeconds(6);
        }

        void EmitPuff(Vector3 site, int index, bool blast)
        {
            // Even angular coverage with overlapping billows, rather than isolated random patches.
            float angle = index * 2.399963f;
            float radius = 0.025f * Mathf.Sqrt((index % 8) / 7f);
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

            // One puff in three is a heavy billow. The fine wisps around them give the plume its edge at a
            // fraction of the screen a few big puffs would fill, which is what a headset actually pays for.
            bool billow = index % 3 == 0;
            clouds.Emit(new ParticleSystem.EmitParams
            {
                position = site + radial * radius,
                velocity = radial * ((blast ? 0.75f : 0.3f) * Random.Range(0.7f, 1.3f)) + Vector3.up * ((blast ? 0.9f : 0.65f) * Random.Range(0.85f, 1.15f)),
                startSize = billow ? Random.Range(0.36f, 0.46f) : Random.Range(0.17f, 0.27f),
                startLifetime = billow ? Random.Range(5f, 7f) : Random.Range(3.8f, 5.6f),
                applyShapeToPosition = false,
            }, 1);
        }
    }
}
