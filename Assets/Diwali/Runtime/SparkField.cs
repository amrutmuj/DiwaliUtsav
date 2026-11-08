using UnityEngine;

namespace Diwali
{
    /// <summary>
    /// A continuous spray of sparks drawn entirely on the GPU. The mesh only stores one quad per
    /// spark; the Diwali/Spark Field shader solves each flight, so density costs no CPU time at all.
    /// Sparks are emitted relative to this transform, so use Particle Systems for anything hand held.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public sealed class SparkField : MonoBehaviour
    {
        static readonly int Clock = Shader.PropertyToID("_Clock");
        static readonly int EmitEnd = Shader.PropertyToID("_EmitEnd");
        static readonly int DensityId = Shader.PropertyToID("_Density");
        static readonly int FloorY = Shader.PropertyToID("_FloorY");
        static readonly int SpeedScaleId = Shader.PropertyToID("_SpeedScale");
        static readonly int Omega = Shader.PropertyToID("_Omega");
        static readonly int HotColor = Shader.PropertyToID("_HotColor");

        public int sparkCount = 900;
        [Tooltip("Longest spark life in seconds, so the field is hidden only after the last one lands.")]
        public float longestLife = 1.6f;

        public bool Running { get; private set; }

        /// <summary>Share of sparks alive, 0 to 1. Rising and falling this swells the fountain.</summary>
        public float Density { get; set; } = 1;

        /// <summary>Multiplies launch speed, which sets how high the fountain reaches.</summary>
        public float SpeedScale { get; set; } = 1;

        /// <summary>Rotation of the emitter in radians per second. Used by the chakri.</summary>
        public float Spin { get; set; }

        /// <summary>Overrides the material's hot colour when alpha is above zero.</summary>
        public Color Tint { get; set; }

        MeshRenderer view;
        MaterialPropertyBlock block;
        float clock, emitEnd;
        float floorHeight = float.NegativeInfinity;

        void Awake()
        {
            view = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
            GetComponent<MeshFilter>().sharedMesh = SparkMeshes.Field(sparkCount);
            view.enabled = false;
        }

        public void Begin(float seconds = float.PositiveInfinity)
        {
            clock = 0;
            emitEnd = seconds;
            Running = true;
            view.enabled = true;
            floorHeight = FloorBelow();
            Push();
        }

        public void Stop()
        {
            if (Running) emitEnd = Mathf.Min(emitEnd, clock);
        }

        public void Halt()
        {
            Running = false;
            if (view) view.enabled = false;
        }

        float FloorBelow()
        {
            var session = DiwaliSession.Instance;
            return session && session.placement ? session.placement.FloorHeight : 0;
        }

        void Update()
        {
            if (!Running) return;
            clock += Time.deltaTime;
            if (clock > emitEnd + longestLife)
            {
                Halt();
                return;
            }
            Push();
        }

        void Push()
        {
            block.SetFloat(Clock, clock);
            block.SetFloat(EmitEnd, float.IsPositiveInfinity(emitEnd) ? 1e6f : emitEnd);
            block.SetFloat(DensityId, Density);
            block.SetFloat(SpeedScaleId, SpeedScale);
            block.SetFloat(FloorY, floorHeight);
            if (Spin != 0) block.SetFloat(Omega, Spin);
            if (Tint.a > 0) block.SetColor(HotColor, Tint);
            view.SetPropertyBlock(block);
        }
    }

    /// <summary>Shared geometry for GPU-driven effects. Built once, on first use.</summary>
    public static class SparkMeshes
    {
        static readonly System.Collections.Generic.Dictionary<int, Mesh> Fields = new System.Collections.Generic.Dictionary<int, Mesh>();
        static readonly System.Collections.Generic.Dictionary<int, Mesh> Bursts = new System.Collections.Generic.Dictionary<int, Mesh>();

        /// <summary>Points along each star's trail. The star burst shader has to agree.</summary>
        public const int TrailSegments = 6;
        public const int VerticesPerStar = (TrailSegments + 1) * 2 + 4;
        const float WorldBounds = 500;

        public static Mesh Field(int count)
        {
            if (Fields.TryGetValue(count, out var cached) && cached) return cached;

            var positions = new Vector3[count * 4];
            var corners = new Vector2[count * 4];
            var seeds = new Vector2[count * 4];
            var triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                int v = i * 4;
                float id = (i + 0.5f) / count;
                corners[v] = new Vector2(-1, -1);
                corners[v + 1] = new Vector2(1, -1);
                corners[v + 2] = new Vector2(-1, 1);
                corners[v + 3] = new Vector2(1, 1);
                for (int c = 0; c < 4; c++) seeds[v + c] = new Vector2(id, 0);
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }

            var mesh = Create("Spark field", positions, corners, seeds, triangles);
            Fields[count] = mesh;
            return mesh;
        }

        public static Mesh Burst(int stars)
        {
            if (Bursts.TryGetValue(stars, out var cached) && cached) return cached;

            int rings = TrailSegments + 1;
            var positions = new Vector3[stars * VerticesPerStar];
            var strips = new Vector2[positions.Length];
            var seeds = new Vector2[positions.Length];
            var triangles = new int[stars * (TrailSegments + 1) * 6];
            var random = new System.Random(1108);
            int vertex = 0, index = 0;
            for (int star = 0; star < stars; star++)
            {
                float id = (star + 0.5f) / stars;
                float randomValue = (float)random.NextDouble();
                int first = vertex;
                for (int segment = 0; segment < rings; segment++)
                {
                    strips[vertex] = new Vector2(-1, segment);
                    seeds[vertex++] = new Vector2(id, randomValue);
                    strips[vertex] = new Vector2(1, segment);
                    seeds[vertex++] = new Vector2(id, randomValue);
                }
                for (int segment = 0; segment < TrailSegments; segment++)
                {
                    int a = first + segment * 2;
                    triangles[index++] = a;
                    triangles[index++] = a + 2;
                    triangles[index++] = a + 1;
                    triangles[index++] = a + 1;
                    triangles[index++] = a + 2;
                    triangles[index++] = a + 3;
                }

                // A round head on its own quad. The shader reads the corner from these offset values.
                int head = vertex;
                for (int corner = 0; corner < 4; corner++)
                {
                    strips[vertex] = new Vector2(corner % 2 * 2 - 1, 100 + corner / 2 * 2 - 1);
                    seeds[vertex++] = new Vector2(id, randomValue);
                }
                triangles[index++] = head;
                triangles[index++] = head + 2;
                triangles[index++] = head + 1;
                triangles[index++] = head + 1;
                triangles[index++] = head + 2;
                triangles[index++] = head + 3;
            }

            var mesh = Create("Star burst", positions, strips, seeds, triangles);
            Bursts[stars] = mesh;
            return mesh;
        }

        static Mesh Create(string name, Vector3[] positions, Vector2[] uv0, Vector2[] uv1, int[] triangles)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
            mesh.vertices = positions;
            mesh.uv = uv0;
            mesh.uv2 = uv1;
            mesh.triangles = triangles;
            // The shader moves every vertex, so culling must not rely on the stored positions.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * WorldBounds);
            return mesh;
        }
    }
}
