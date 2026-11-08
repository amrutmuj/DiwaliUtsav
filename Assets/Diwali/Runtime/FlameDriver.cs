using UnityEngine;

namespace Diwali
{
    /// <summary>
    /// One source of firelight: the flame volume, its halo, the pool of light on the surface below
    /// and the point light. All of them share a single flicker signal, because in a real room
    /// the shadows move at exactly the rhythm of the flame.
    /// </summary>
    public sealed class FlameDriver : MonoBehaviour
    {
        static readonly int Intensity = Shader.PropertyToID("_Intensity");
        static readonly int Lean = Shader.PropertyToID("_Lean");
        static readonly int Seed = Shader.PropertyToID("_Seed");

        public Renderer flame;
        public Renderer halo;
        public Renderer pool;
        public Light lamp;
        public float flameLevel = 1;
        public float haloLevel = 0.32f;
        public float poolLevel = 0.3f;
        public float lampLevel = 0.5f;
        [Tooltip("How quickly the flame reaches full size, in seconds.")]
        public float riseTime = 0.45f;
        public float flickerRate = 3.2f;
        [Range(0, 1)] public float flickerDepth = 0.2f;
        [Tooltip("Flames always rise against gravity, whatever is holding them.")]
        public bool keepUpright = true;
        [Tooltip("How far motion through the air bends the flame.")]
        public float leanResponse = 0.5f;

        public float Level { get; private set; }

        MaterialPropertyBlock flameBlock, haloBlock, poolBlock;
        float target, seed;
        Vector3 lastPosition;
        Vector2 lean;

        void Awake()
        {
            seed = Random.value * 100f;
            flameBlock = new MaterialPropertyBlock();
            haloBlock = new MaterialPropertyBlock();
            poolBlock = new MaterialPropertyBlock();
            lastPosition = transform.position;
            Apply(0);
        }

        public void Ignite() => target = 1;

        public void Extinguish() => target = 0;

        public void SetTarget(float value) => target = value;

        public void SetInstant(float value)
        {
            target = value;
            Level = value;
            Apply(1);
        }

        void LateUpdate()
        {
            if (keepUpright) transform.rotation = Quaternion.identity;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Level = Mathf.MoveTowards(Level, target, dt / Mathf.Max(riseTime, 0.01f));
            if (Level <= 0 && target <= 0)
            {
                Show(false);
                return;
            }

            Show(true);
            Vector3 velocity = (transform.position - lastPosition) / dt;
            lastPosition = transform.position;
            Vector2 wanted = Vector2.ClampMagnitude(new Vector2(-velocity.x, -velocity.z) * leanResponse, 0.45f);
            lean = Vector2.Lerp(lean, wanted, 1 - Mathf.Exp(-dt * 9f));

            // Two octaves plus an occasional dip: a steady sine reads as artificial immediately.
            float time = Time.time * flickerRate + seed;
            float wobble = Mathf.PerlinNoise(time, seed) * 0.7f + Mathf.PerlinNoise(time * 2.7f, seed + 9) * 0.3f;
            float gutter = Mathf.Pow(Mathf.PerlinNoise(time * 0.31f, seed + 21), 3f) * 0.5f;
            Apply(Mathf.Clamp01(1 - flickerDepth * (1 - wobble) - flickerDepth * gutter));
        }

        void Apply(float flicker)
        {
            float level = Level * flicker;
            if (flame)
            {
                flameBlock.SetFloat(Intensity, flameLevel * Mathf.Lerp(0.82f, 1f, flicker) * Level);
                flameBlock.SetVector(Lean, lean);
                flameBlock.SetFloat(Seed, seed);
                flame.SetPropertyBlock(flameBlock);
            }
            if (halo)
            {
                haloBlock.SetFloat(Intensity, haloLevel * level);
                halo.SetPropertyBlock(haloBlock);
            }
            if (pool)
            {
                poolBlock.SetFloat(Intensity, poolLevel * level);
                pool.SetPropertyBlock(poolBlock);
            }
            if (lamp) lamp.intensity = lampLevel * level;
        }

        void Show(bool visible)
        {
            if (flame) flame.enabled = visible;
            if (halo) halo.enabled = visible;
            if (pool) pool.enabled = visible;
            if (lamp) lamp.enabled = visible;
        }
    }
}
