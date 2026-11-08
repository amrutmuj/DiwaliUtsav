using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Diwali
{
    /// <summary>
    /// Draws every effect once, as a speck, while the scene opens. On Quest the first draw of each shader
    /// and mesh layout builds a GPU pipeline, and a first use also builds its spark mesh. Left alone, those
    /// stalls all land on the first blast, the worst moment for them. Here nobody can see them.
    /// </summary>
    public static class FestivalWarmup
    {
        const float Speck = 0.0004f;
        const float Reach = 1.5f;
        const int DrawnFrames = 2;

        public static IEnumerator Run(DiwaliSession session)
        {
            Transform view = session.rig.view.transform;
            Vector3 spot = view.position + view.forward * Reach;
            var specks = new List<GameObject>();
            bool haloWarmed = false;
            foreach (var entry in session.catalog.entries)
            {
                if (!entry.prefab || !entry.prefab.GetComponent<FestivalItem>()) continue;
                var speck = Object.Instantiate(entry.prefab, spot, Quaternion.identity);
                speck.transform.localScale = Vector3.one * Speck;
                Light(speck);
                if (!haloWarmed && speck.GetComponent<GrabZone>() is GrabZone zone && zone.transform.Find("Model"))
                {
                    zone.Halo.Show(1);
                    haloWarmed = true;
                }
                specks.Add(speck);
            }
            session.sky.Warm(spot);

            for (int i = 0; i < DrawnFrames; i++) yield return null;
            foreach (var speck in specks) if (speck) Object.Destroy(speck);
        }

        /// <summary>Gets each effect drawing, with no light or sound of its own.</summary>
        static void Light(GameObject speck)
        {
            foreach (var flame in speck.GetComponentsInChildren<FlameDriver>(true)) flame.SetInstant(0.001f);
            foreach (var field in speck.GetComponentsInChildren<SparkField>(true)) field.Begin(0.02f);
            foreach (var system in speck.GetComponentsInChildren<ParticleSystem>(true)) system.Emit(1);
            foreach (var lamp in speck.GetComponentsInChildren<Light>(true)) lamp.intensity = 0;
        }
    }
}
