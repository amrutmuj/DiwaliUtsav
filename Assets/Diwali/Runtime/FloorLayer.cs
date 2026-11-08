using System.Collections;
using UnityEngine;

namespace Diwali
{
    /// <summary>
    /// The only part of the room this experience uses: one flat floor, out to a fixed distance from
    /// the visitor. Everything is placed on it. Nothing else in the room is modelled, so furniture and
    /// walls carry no meshes, colliders or occlusion, and the real room simply shows through.
    /// </summary>
    public sealed class FloorLayer : MonoBehaviour
    {
        static readonly int RingRadius = Shader.PropertyToID("_RingRadius");
        static readonly int Intensity = Shader.PropertyToID("_Intensity");

        [Tooltip("How far the layer reaches from the visitor, in metres.")]
        public float radius = 10;
        [Tooltip("Soft ring that sweeps outwards once, to show the floor has been found.")]
        public Renderer reveal;
        public float revealSeconds = 1.6f;

        public float Height { get; private set; }

        /// <summary>A transform at floor height, for particles that need to land on the real floor.</summary>
        public Transform Surface { get; private set; }
        public string Source { get; private set; } = "not measured";

        void Awake()
        {
            if (reveal) reveal.enabled = false;
            Surface = new GameObject("Floor surface").transform;
        }

        public void Use(float height, string source)
        {
            Height = height;
            Source = source;
            Surface.position = new Vector3(0, height, 0);
            Debug.Log($"[Diwali] Floor layer at {height:F3} m from {source}, {radius:F0} m radius.");
        }

        public void Reveal(Vector3 visitor)
        {
            if (reveal) StartCoroutine(Sweep(visitor));
        }

        IEnumerator Sweep(Vector3 visitor)
        {
            reveal.transform.position = new Vector3(visitor.x, Height + 0.004f, visitor.z);
            reveal.transform.localScale = new Vector3(radius * 2, radius * 2, 1);
            reveal.enabled = true;

            var block = new MaterialPropertyBlock();
            for (float t = 0; t < 1; t += Time.deltaTime / revealSeconds)
            {
                float eased = 1 - (1 - t) * (1 - t);
                block.SetFloat(RingRadius, Mathf.Lerp(0.02f, 0.98f, eased));
                block.SetFloat(Intensity, 0.55f * (1 - t) * (1 - t));
                reveal.SetPropertyBlock(block);
                yield return null;
            }
            reveal.enabled = false;
        }
    }
}
