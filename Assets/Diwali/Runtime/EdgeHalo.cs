using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Diwali
{
    /// <summary>
    /// A soft line of light around an object's outline, which says "this one" without drawing anything over
    /// the room. The object's solid parts are copied once, the first time it is pointed at: a mask that marks
    /// its shape, and a few slightly larger layers that show only outside that shape.
    /// </summary>
    public sealed class EdgeHalo : MonoBehaviour
    {
        const float FadeSeconds = 0.16f;
        const int Layers = 5;
        const float WidthPerMetre = 0.006f;
        /// <summary>Parts much thinner than the main body, such as a fuse or twine, would only add clutter.</summary>
        const float ThinnestPart = 0.2f;

        static readonly int Intensity = Shader.PropertyToID("_Intensity");
        static readonly int Spread = Shader.PropertyToID("_Spread");
        static readonly int Inflate = Shader.PropertyToID("_Inflate");
        static Material maskMaterial;

        readonly List<Renderer> masks = new List<Renderer>();
        readonly List<Renderer>[] layers = new List<Renderer>[Layers];
        MaterialPropertyBlock[] blocks;
        Vector3 localMiddle, localHalf;
        float level, held;

        public int Shells => masks.Count;
        public bool Lit => level > 0;

        public static EdgeHalo On(Transform model)
        {
            var halo = model.GetComponent<EdgeHalo>();
            return halo ? halo : model.gameObject.AddComponent<EdgeHalo>();
        }

        /// <summary>Lights the halo for this frame, so call it every frame to hold it. 1 is full strength.</summary>
        public void Show(float strength)
        {
            if (blocks == null) Build();
            held = Mathf.Max(held, strength);
            level = Mathf.Max(level, strength);
            enabled = true;
            Apply();
        }

        static Material MaskFor(Material halo)
        {
            if (maskMaterial) return maskMaterial;
            maskMaterial = new Material(halo) { name = "Edge halo mask", renderQueue = halo.renderQueue - 1 };
            maskMaterial.SetFloat("_ColorMask", 0);
            maskMaterial.SetFloat("_StencilComp", (float)CompareFunction.Always);
            maskMaterial.SetFloat("_StencilPass", (float)StencilOp.Replace);
            maskMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
            return maskMaterial;
        }

        static float Thickness(MeshFilter filter)
        {
            Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, filter.transform.lossyScale);
            return Mathf.Min(Mathf.Abs(size.x), Mathf.Min(Mathf.Abs(size.y), Mathf.Abs(size.z)));
        }

        void Build()
        {
            var halo = DiwaliSession.Instance.rig.edgeHaloMaterial;
            var mask = MaskFor(halo);
            blocks = new MaterialPropertyBlock[Layers];
            for (int i = 0; i < Layers; i++)
            {
                layers[i] = new List<Renderer>();
                blocks[i] = new MaterialPropertyBlock();
                blocks[i].SetFloat(Spread, (i + 1) / (float)Layers);
            }

            var solid = new List<MeshFilter>();
            float thickest = 0;
            foreach (var part in GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = part.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh || !part.sharedMaterial) continue;
                if (part.sharedMaterial.renderQueue >= (int)RenderQueue.Transparent) continue;
                solid.Add(filter);
                thickest = Mathf.Max(thickest, Thickness(filter));
            }

            var bounds = new Bounds();
            bool first = true;
            foreach (var filter in solid)
            {
                if (Thickness(filter) < thickest * ThinnestPart) continue;
                masks.Add(Copy(filter, mask));
                for (int i = 0; i < Layers; i++) layers[i].Add(Copy(filter, halo));

                var box = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 sign = new Vector3((corner & 1) * 2 - 1, (corner & 2) - 1, (corner & 4) / 2 - 1);
                    Vector3 point = transform.InverseTransformPoint(filter.transform.TransformPoint(box.center + Vector3.Scale(box.extents, sign)));
                    if (first) bounds = new Bounds(point, Vector3.zero);
                    else bounds.Encapsulate(point);
                    first = false;
                }
            }
            localMiddle = bounds.center;
            localHalf = bounds.extents;
        }

        static Renderer Copy(MeshFilter part, Material material)
        {
            var shell = new GameObject("Edge halo", typeof(MeshFilter), typeof(MeshRenderer));
            shell.layer = part.gameObject.layer;
            shell.transform.SetParent(part.transform, false);
            shell.GetComponent<MeshFilter>().sharedMesh = part.sharedMesh;

            var view = shell.GetComponent<MeshRenderer>();
            var materials = new Material[part.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            view.sharedMaterials = materials;
            view.shadowCastingMode = ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.enabled = false;
            return view;
        }

        void LateUpdate()
        {
            level = Mathf.Max(held, Mathf.MoveTowards(level, 0, Time.deltaTime / FadeSeconds));
            held = 0;
            if (blocks != null) Apply();
            if (level <= 0) enabled = false;
        }

        /// <summary>Enlarges the object by the same width on every side, steady in the eye at any distance.</summary>
        Matrix4x4 Enlargement()
        {
            Vector3 head = DiwaliSession.Instance.rig.view.transform.position;
            float width = WidthPerMetre * Vector3.Distance(head, transform.TransformPoint(localMiddle));
            Vector3 size = transform.lossyScale;
            var scale = new Vector3(Grow(width, size.x, localHalf.x), Grow(width, size.y, localHalf.y), Grow(width, size.z, localHalf.z));
            var local = Matrix4x4.Translate(localMiddle) * Matrix4x4.Scale(scale) * Matrix4x4.Translate(-localMiddle);
            return transform.localToWorldMatrix * local * transform.worldToLocalMatrix;
        }

        static float Grow(float width, float scale, float half) => 1 + width / Mathf.Max(Mathf.Abs(scale) * half, 0.001f);

        void Apply()
        {
            bool lit = level > 0;
            foreach (var mask in masks) if (mask) mask.enabled = lit;
            if (!lit)
            {
                foreach (var layer in layers) foreach (var shell in layer) if (shell) shell.enabled = false;
                return;
            }

            Matrix4x4 enlargement = Enlargement();
            for (int i = 0; i < Layers; i++)
            {
                blocks[i].SetFloat(Intensity, level);
                blocks[i].SetMatrix(Inflate, enlargement);
                foreach (var shell in layers[i])
                {
                    if (!shell) continue;
                    shell.enabled = true;
                    shell.SetPropertyBlock(blocks[i]);
                }
            }
        }
    }
}
