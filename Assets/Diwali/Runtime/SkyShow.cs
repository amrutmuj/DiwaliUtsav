using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Diwali
{
    public enum BurstPattern { Peony = 0, Chrysanthemum = 1, Willow = 2, Ring = 3, Palm = 4, Bead = 5, Spider = 6, Fan = 7 }

    [Serializable]
    public struct BurstStyle
    {
        public BurstPattern pattern;
        public Color first, second, third;
        public float speed, life, drag, gravity, width, glitter;
        [Tooltip("Length of each star's trail, in seconds of its flight.")]
        public float trail;
        [Tooltip("How far the trail turns to the third colour.")]
        public float tailShare;
        [Tooltip("Sparkle along the trail.")]
        public float tailGlitter;
        [Tooltip("Share of stars burning the second colour from the start, rather than all turning to it with age.")]
        public float mix;
        [Tooltip("Share of the shell's stars used. Zero means all of them.")]
        public float share;
        [Tooltip("Share of slow, fine stars at the core.")]
        public float pistil;
        [Tooltip("Size of the bead at the head of each star.")]
        public float head;
        [Tooltip("Half angle of a fan, in radians.")]
        public float fanHalf;
        [Tooltip("Strength of the glow left in the smoke around the burst.")]
        public float haze;

        public static BurstStyle Peony(Color first, Color second) => new BurstStyle
        { pattern = BurstPattern.Peony, first = first, second = second, speed = 24, life = 2.2f, drag = 2.1f, gravity = 2.4f, width = 0.13f, trail = 0.2f, glitter = 0.1f };

        public static BurstStyle Chrysanthemum(Color first, Color second) => new BurstStyle
        { pattern = BurstPattern.Chrysanthemum, first = first, second = second, speed = 28, life = 2.7f, drag = 2.0f, gravity = 2.8f, width = 0.11f, trail = 0.56f, glitter = 0.35f };

        public static BurstStyle Willow(Color gold) => new BurstStyle
        { pattern = BurstPattern.Willow, first = gold, second = gold * 0.75f, speed = 19, life = 4.4f, drag = 1.7f, gravity = 3.8f, width = 0.1f, trail = 1.04f, glitter = 0.7f };

        public static BurstStyle Ring(Color colour) => new BurstStyle
        { pattern = BurstPattern.Ring, first = colour, second = colour, speed = 22, life = 2.0f, drag = 2.2f, gravity = 1.8f, width = 0.16f, trail = 0.2f, glitter = 0 };

        public static BurstStyle Palm(Color first, Color second) => new BurstStyle
        { pattern = BurstPattern.Palm, first = first, second = second, speed = 26, life = 3.0f, drag = 1.8f, gravity = 3.4f, width = 0.17f, trail = 0.72f, glitter = 0.4f };

        /// <summary>First reference: tight violet heads on gold tails, in two layers around a pale core.</summary>
        public static BurstStyle VioletPeony() => new BurstStyle
        {
            pattern = BurstPattern.Chrysanthemum, first = SkyShow.ShellViolet, second = SkyShow.ShellViolet, third = SkyShow.ShellGold,
            speed = 24, drag = 2.1f, life = 2.4f, gravity = 2.4f, width = 0.26f, trail = 0.42f, tailShare = 1, tailGlitter = 0.7f, pistil = 0.14f, glitter = 0.1f, head = 0.1f,
        };

        /// <summary>Second reference: a pale gold chrysanthemum on long lines from the centre.</summary>
        public static BurstStyle GoldChrysanthemum() => new BurstStyle
        {
            pattern = BurstPattern.Chrysanthemum, first = SkyShow.ShellPale, second = SkyShow.ShellGold, third = SkyShow.ShellGold,
            speed = 28, drag = 2, life = 2.5f, gravity = 2.8f, width = 0.16f, trail = 0.85f, tailShare = 1, tailGlitter = 0.25f, pistil = 0.1f, glitter = 0.35f,
        };

        /// <summary>Second reference: long stars that droop into arcs.</summary>
        public static BurstStyle Arc(Color first, Color second) => new BurstStyle
        {
            pattern = BurstPattern.Willow, first = first, second = second, third = second,
            speed = 25, drag = 1.8f, life = 2.8f, gravity = 7, width = 0.18f, trail = 1.1f, tailShare = 0.6f, share = 0.7f, glitter = 0.15f,
        };

        /// <summary>Third reference: a few long red-orange lines with a red glow left in the smoke.</summary>
        public static BurstStyle RedSpider() => new BurstStyle
        {
            pattern = BurstPattern.Spider, first = SkyShow.ShellOrange, second = SkyShow.ShellRed, third = SkyShow.ShellRed,
            speed = 34, drag = 2.6f, life = 1.9f, gravity = 1.4f, width = 0.11f, trail = 0.8f, tailShare = 1, share = 0.42f, haze = 0.3f,
        };

        /// <summary>Fourth reference: a ball of separate red points, with white and mint among them.</summary>
        public static BurstStyle RedBead(float mix) => new BurstStyle
        {
            pattern = BurstPattern.Bead, first = SkyShow.ShellFestivalRed, second = SkyShow.ShellWhite, third = SkyShow.ShellMint,
            speed = 22, drag = 2.2f, life = 2.4f, gravity = 2, width = 0.1f, trail = 0.2f, mix = mix, glitter = 0.2f, head = 2,
        };

        /// <summary>Fourth reference: thin rays from a low point, each ending in a bright bead.</summary>
        public static BurstStyle Fan(Color bead) => new BurstStyle
        {
            pattern = BurstPattern.Fan, first = bead, second = bead, third = SkyShow.ShellWhite * 0.5f,
            speed = 26, drag = 3, life = 2.2f, gravity = 0.8f, width = 0.05f, trail = 1.2f, tailShare = 1, share = 0.06f, head = 6.5f, fanHalf = 0.62f,
        };

        /// <summary>Fourth reference: a low arch of silver glitter.</summary>
        public static BurstStyle Arch() => new BurstStyle
        {
            pattern = BurstPattern.Fan, first = SkyShow.ShellWhite, second = SkyShow.ShellWhite, third = SkyShow.ShellWhite,
            speed = 14, drag = 3, life = 2.8f, gravity = 0.5f, width = 0.05f, trail = 0.5f, tailShare = 1, tailGlitter = 1, glitter = 0.6f, head = 2.6f, fanHalf = 1.2f,
        };

        /// <summary>How far the stars reach, in metres. Drag stops a star at its speed over drag.</summary>
        public float Radius => speed / drag;

        /// <summary>The same shell with a charge to suit its height: it opens to this radius, in thinner stars that burn out sooner.</summary>
        public BurstStyle Sized(float radius)
        {
            float share = radius / Radius;
            BurstStyle sized = this;
            sized.speed = radius * drag;
            sized.width = width * share;
            sized.life = life * (0.7f + 0.3f * share);
            return sized;
        }
    }

    /// <summary>
    /// Everything that happens above the room: rising shells, star bursts and the grand show.
    /// Burst and comet objects are pooled, so repeating a show never allocates or grows.
    /// </summary>
    public sealed class SkyShow : MonoBehaviour
    {
        static readonly int Age = Shader.PropertyToID("_Age");
        static readonly int Life = Shader.PropertyToID("_Life");
        static readonly int Speed = Shader.PropertyToID("_Speed");
        static readonly int Drag = Shader.PropertyToID("_Drag");
        static readonly int Gravity = Shader.PropertyToID("_Gravity");
        static readonly int Width = Shader.PropertyToID("_Width");
        static readonly int Trail = Shader.PropertyToID("_Trail");
        static readonly int Pattern = Shader.PropertyToID("_Pattern");
        static readonly int Glitter = Shader.PropertyToID("_Glitter");
        static readonly int Seed = Shader.PropertyToID("_Seed");
        static readonly int ColorA = Shader.PropertyToID("_ColorA");
        static readonly int ColorB = Shader.PropertyToID("_ColorB");
        static readonly int Boost = Shader.PropertyToID("_Boost");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        static readonly int StarCount = Shader.PropertyToID("_Stars");
        static readonly int ColorC = Shader.PropertyToID("_ColorC");
        static readonly int StarShare = Shader.PropertyToID("_Share");
        static readonly int Mix = Shader.PropertyToID("_Mix");
        static readonly int TailShare = Shader.PropertyToID("_TailShare");
        static readonly int TailGlitter = Shader.PropertyToID("_TailGlitter");
        static readonly int Pistil = Shader.PropertyToID("_Pistil");
        static readonly int HeadBead = Shader.PropertyToID("_Head");
        static readonly int FanHalf = Shader.PropertyToID("_FanHalf");
        static readonly int FanAxis = Shader.PropertyToID("_FanAxis");

        const float SpeedOfSound = 343;

        /// <summary>Enough to read as separate stars with their own trails, rather than one solid ball.</summary>
        const int StarsPerShell = 260;
        const float FlashReach = 1.2f;
        const float ShowBurstRadius = 10;
        const float FrontDistance = 34;
        const float FrontBurstRadius = 12.5f;
        const float FrontHalfWidth = 26;
        const float FrontLowHalfWidth = 24;
        const int FrontShells = 7;
        const int FrontLowCount = 11;
        const int LowStars = 140;

        public Material burstMaterial;
        public Material glowMaterial;
        public ParticleSystem cometPrefab;
        [Tooltip("Hot flecks from anything that goes off at ground level, all in one system and one draw.")]
        public ParticleSystem emberPrefab;
        public int skyBursts = 9;
        [Tooltip("A big ladi fires two pops every 35 ms, each living about half a second, so this has to outlast a run.")]
        public int pops = 28;
        public int popStars = 72;

        public static readonly Color Gold = new Color(1f, 0.74f, 0.3f);
        public static readonly Color Rose = new Color(1f, 0.2f, 0.45f);
        public static readonly Color Peacock = new Color(0.16f, 0.72f, 1f);
        public static readonly Color Violet = new Color(0.68f, 0.32f, 1f);
        public static readonly Color Emerald = new Color(0.22f, 1f, 0.5f);
        public static readonly Color Marigold = new Color(1f, 0.46f, 0.1f);
        public static readonly Color Pearl = new Color(1f, 0.93f, 0.82f);

        // Sampled from the reference photographs, so these are what those shells really burned.
        public static readonly Color ShellViolet = new Color(0.55f, 0.38f, 1f);
        public static readonly Color ShellGold = new Color(1f, 0.78f, 0.43f);
        public static readonly Color ShellPale = new Color(0.97f, 0.94f, 0.86f);
        public static readonly Color ShellBlue = new Color(0.27f, 0.47f, 1f);
        public static readonly Color ShellPink = new Color(0.99f, 0.31f, 0.55f);
        public static readonly Color ShellOrange = new Color(1f, 0.51f, 0.22f);
        public static readonly Color ShellRed = new Color(0.85f, 0.2f, 0.09f);
        public static readonly Color ShellCrimson = new Color(0.76f, 0.09f, 0.16f);
        public static readonly Color ShellFestivalRed = new Color(1f, 0.22f, 0.17f);
        public static readonly Color ShellWhite = new Color(0.99f, 0.9f, 0.87f);
        public static readonly Color ShellMint = new Color(0.78f, 1f, 0.84f);

        sealed class Burst
        {
            public Transform root;
            public MeshRenderer view;
            public MaterialPropertyBlock block;
            public float age, life;
            public int stars;
            public bool active;
        }

        sealed class Flash
        {
            public Transform root;
            public MeshRenderer view;
            public MaterialPropertyBlock block;
            public float age, life, peak;
            public Color colour;
            public bool active;
        }

        sealed class Comet
        {
            public ParticleSystem trail;
            public Renderer head;
            public bool busy;
        }

        readonly List<Burst> skyPool = new List<Burst>();
        readonly List<Burst> popPool = new List<Burst>();
        readonly List<Burst> lowPool = new List<Burst>();
        readonly List<Flash> flashPool = new List<Flash>();
        readonly List<Comet> comets = new List<Comet>();
        ParticleSystem embers;
        int skyCursor, popCursor, lowCursor, flashCursor;

        public bool ShowRunning { get; private set; }
        public int ShellsInFlight => comets.FindAll(comet => comet.busy).Count;
        public Vector3 LastFlashPoint { get; private set; }
        public float LastBurstRadius { get; private set; }
        public int LastFlashFrame { get; private set; }

        DiwaliSession Session => DiwaliSession.Instance;

        void Awake()
        {
            // Sized for a tray firing every third of a second, whatever an older scene has saved.
            for (int i = 0; i < Mathf.Max(skyBursts, 16); i++) skyPool.Add(CreateBurst("Sky burst " + i, StarsPerShell));
            for (int i = 0; i < pops; i++) popPool.Add(CreateBurst("Ground pop " + i, popStars));
            for (int i = 0; i < FrontLowCount * 2; i++) lowPool.Add(CreateBurst("Low fan " + i, LowStars));
            for (int i = 0; i < 16; i++) flashPool.Add(CreateFlash(i));
            for (int i = 0; i < 20; i++)
            {
                var trail = Instantiate(cometPrefab, transform);
                trail.name = "Comet " + i;
                var head = trail.GetComponentInChildren<MeshRenderer>();
                head.enabled = false;
                comets.Add(new Comet { trail = trail, head = head });
            }
            if (emberPrefab)
            {
                embers = Instantiate(emberPrefab, transform);
                embers.name = "Embers";
            }
        }

        void Start()
        {
            if (!embers) return;
            var landing = embers.collision;
            landing.SetPlane(0, Session.placement.floor.Surface);
        }

        Burst CreateBurst(string label, int stars)
        {
            var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = SparkMeshes.Burst(stars);
            var view = go.GetComponent<MeshRenderer>();
            view.sharedMaterial = burstMaterial;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.enabled = false;
            return new Burst { root = go.transform, view = view, block = new MaterialPropertyBlock(), stars = stars };
        }

        Flash CreateFlash(int index)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = "Flash " + index;
            go.transform.SetParent(transform, false);
            var view = go.GetComponent<MeshRenderer>();
            view.sharedMaterial = glowMaterial;
            view.enabled = false;
            return new Flash { root = go.transform, view = view, block = new MaterialPropertyBlock() };
        }

        /// <summary>A shell breaking high above the room.</summary>
        public void BurstInSky(Vector3 position, BurstStyle style)
        {
            var burst = skyPool[skyCursor++ % skyPool.Count];
            Arm(burst, position, style, 1.9f);
            LastBurstRadius = style.Radius;
            ShowFlash(position, style.first, style.Radius * FlashReach, 0.38f, 1.1f);
            if (style.haze > 0) ShowFlash(position, ShellCrimson, style.Radius * 2.6f, 1.5f, style.haze);

            Session.ambience.Flash(Session.Gentle ? 0.05f : 0.14f);

            // Light reaches you first; the sound follows at the speed of sound.
            float distance = Vector3.Distance(position, Session.rig.view.transform.position);
            float delay = distance / SpeedOfSound;
            Session.sound.Play("burst", position, 1, 0.03f, delay);
            if (style.glitter > 0.3f) Session.sound.Play("crackle", position, 0.32f, 0.025f, delay + style.life * 0.45f);
        }

        /// <summary>A small report at ground level, with stars that stay inside the real room.</summary>
        public void Pop(Vector3 position, Color colour, float size = 1)
        {
            var burst = popPool[popCursor++ % popPool.Count];
            var style = new BurstStyle
            {
                pattern = BurstPattern.Peony, first = Pearl, second = colour, speed = 2.6f * size, life = 0.34f,
                drag = 5.5f, gravity = 3, width = 0.0026f, trail = 0.048f, glitter = 0,
            };
            Arm(burst, position, style, 2.1f);
            ShowFlash(position, Color.Lerp(colour, Color.white, 0.5f), 0.34f * size, 0.09f, 0.95f);
            Embers(position, Mathf.CeilToInt(5 * size), 2.2f * size);
        }

        /// <summary>Throws hot flecks out of a report. They arc, skip on the real floor and go dark as they cool.</summary>
        public void Embers(Vector3 position, int count, float speed)
        {
            if (!embers) return;
            if (Session.Gentle) count = (count + 1) / 2;
            var fleck = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = UnityEngine.Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.8f + 0.2f;
                fleck.velocity = direction * (speed * UnityEngine.Random.Range(0.35f, 1));
                embers.Emit(fleck, 1);
            }
        }

        /// <summary>Draws the pooled effects once with nothing to see, for <see cref="FestivalWarmup"/>.</summary>
        public void Warm(Vector3 spot)
        {
            var style = new BurstStyle { first = Color.black, second = Color.black, speed = 0.001f, life = 0.02f, drag = 1, width = 0.0001f };
            Arm(popPool[0], spot, style, 0);
            ShowFlash(spot, Color.black, 0.001f, 0.02f, 0);
            var speck = new ParticleSystem.EmitParams { position = spot, startSize = 0.0001f, startLifetime = 0.02f, applyShapeToPosition = false };
            comets[0].trail.Emit(speck, 1);
            if (embers) embers.Emit(speck, 1);
        }

        void Arm(Burst burst, Vector3 position, BurstStyle style, float boost)
        {
            burst.root.position = position;
            burst.age = 0;
            burst.life = style.life + style.trail + 0.2f;
            burst.active = true;
            burst.view.enabled = true;

            var block = burst.block;
            block.SetFloat(Age, 0);
            block.SetFloat(Life, style.life);
            block.SetFloat(Speed, style.speed);
            block.SetFloat(Drag, style.drag);
            block.SetFloat(Gravity, style.gravity);
            block.SetFloat(Width, style.width);
            block.SetFloat(Trail, style.trail / SparkMeshes.TrailSegments);
            block.SetFloat(Pattern, (float)style.pattern);
            block.SetFloat(Glitter, style.glitter);
            block.SetFloat(Seed, UnityEngine.Random.value * 40);
            block.SetFloat(StarCount, burst.stars);
            block.SetColor(ColorC, style.third);
            block.SetFloat(StarShare, style.share > 0 ? style.share : 1);
            block.SetFloat(Mix, style.mix);
            block.SetFloat(TailShare, style.tailShare);
            block.SetFloat(TailGlitter, style.tailGlitter);
            block.SetFloat(Pistil, style.pistil);
            block.SetFloat(HeadBead, style.head);
            block.SetFloat(FanHalf, style.fanHalf);
            block.SetVector(FanAxis, Vector3.Cross(Vector3.up, Session.rig.view.transform.position - position).normalized);
            block.SetColor(ColorA, style.first);
            block.SetColor(ColorB, style.second);
            block.SetFloat(Boost, boost * (Session.Gentle ? 0.8f : 1));
            burst.view.SetPropertyBlock(block);
        }

        public void ShowFlash(Vector3 position, Color colour, float radius, float seconds, float peak)
        {
            var flash = flashPool[flashCursor++ % flashPool.Count];
            LastFlashPoint = position;
            LastFlashFrame = Time.frameCount;
            flash.root.position = position;
            flash.root.localScale = Vector3.one * radius;
            flash.colour = colour;
            flash.age = 0;
            flash.life = seconds;
            flash.peak = peak * (Session.Gentle ? 0.55f : 1);
            flash.active = true;
            flash.view.enabled = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Advance(skyPool, dt);
            Advance(popPool, dt);
            Advance(lowPool, dt);
            foreach (var flash in flashPool)
            {
                if (!flash.active) continue;
                flash.age += dt;
                float remaining = 1 - flash.age / flash.life;
                if (remaining <= 0)
                {
                    flash.active = false;
                    flash.view.enabled = false;
                    continue;
                }
                flash.block.SetColor(TintId, flash.colour);
                flash.block.SetFloat(IntensityId, flash.peak * remaining * remaining);
                flash.view.SetPropertyBlock(flash.block);
            }
        }

        static void Advance(List<Burst> pool, float dt)
        {
            foreach (var burst in pool)
            {
                if (!burst.active) continue;
                burst.age += dt;
                if (burst.age > burst.life)
                {
                    burst.active = false;
                    burst.view.enabled = false;
                    continue;
                }
                burst.block.SetFloat(Age, burst.age);
                burst.view.SetPropertyBlock(burst.block);
            }
        }

        /// <summary>Picks a comfortable place in the sky: up and ahead, so nobody has to crane their neck.</summary>
        public Vector3 SkyPoint(float yawDegrees, float heightJitter = 0)
        {
            var head = Session.rig.view.transform;
            Vector3 forward = head.forward;
            forward.y = 0;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            forward = Quaternion.AngleAxis(yawDegrees, Vector3.up) * forward;
            float reach = UnityEngine.Random.Range(30f, 38f);
            float height = UnityEngine.Random.Range(21f, 28f) + heightJitter;
            return head.position + forward * reach + Vector3.up * height;
        }

        /// <summary>Sends one shell straight up from <paramref name="from"/> and breaks it at the height of <paramref name="to"/>.</summary>
        public Coroutine Shell(Vector3 from, Vector3 to, BurstStyle style, bool whistle = false)
        {
            // Shells rise above their launch point. Never bend an ascent toward a distant target.
            to = new Vector3(from.x, Mathf.Max(to.y, from.y + 20), from.z);
            return StartCoroutine(ShellRoutine(from, to, style, whistle));
        }

        /// <summary>Sends one shell along a straight line from its tube to its own place in the sky.</summary>
        public Coroutine ShellTo(Vector3 from, Vector3 to, BurstStyle style, bool whistle = false) => StartCoroutine(ShellRoutine(from, to, style, whistle));

        public static FlightRecord LastShellFlight { get; private set; }

        IEnumerator ShellRoutine(Vector3 from, Vector3 to, BurstStyle style, bool whistle)
        {
            var comet = comets.Find(c => !c.busy);
            if (comet == null)
            {
                BurstInSky(to, style);
                yield break;
            }

            comet.busy = true;
            var trail = comet.trail;
            var main = trail.main;
            main.startColor = Color.Lerp(style.first, ShellGold, 0.8f);
            trail.transform.position = from;
            trail.Clear();
            trail.Play();
            comet.head.enabled = true;
            var ascent = whistle ? Session.sound.PlayFollowing("whistle", trail.transform, 0.7f) : null;

            // A shell leaves at full speed and slows as it climbs, breaking as it stalls. The speed
            // changes but the line does not, so a tube's slant never reads as a bend in the trail.
            Vector3 line = to - from;
            float length = line.magnitude;
            float seconds = Mathf.Sqrt(2 * length / BallisticFlight.Gravity);
            Vector3 direction = line / length;
            float speed = BallisticFlight.Gravity * seconds;
            float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed = Mathf.Min(seconds, elapsed + Time.deltaTime);
                trail.transform.position = from + direction * (speed * elapsed - 0.5f * BallisticFlight.Gravity * elapsed * elapsed);
                yield return null;
            }

            ascent?.Stop(0.05f);
            LastShellFlight = new FlightRecord
            {
                seconds = elapsed, apexHeight = to.y - from.y, burstPoint = trail.transform.position,
                downrange = Vector3.Distance(trail.transform.position, to),
            };

            // The burst takes over at this instant, so nothing of the shell stays in the sky.
            Retire(comet);
            BurstInSky(to, style);
        }

        static void Retire(Comet comet)
        {
            comet.head.enabled = false;
            comet.trail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            comet.busy = false;
        }

        public void BeginGrandShow()
        {
            if (!ShowRunning) StartCoroutine(GrandShow());
        }

        IEnumerator GrandShow()
        {
            ShowRunning = true;
            yield return new WaitForSeconds(2.6f);

            // Opening: one of each shell, with room to breathe.
            yield return Salvo(new[] { BurstStyle.VioletPeony() }, 1, 0, 2.2f);
            yield return Salvo(new[] { BurstStyle.GoldChrysanthemum() }, 1, 30, 2.0f);
            yield return Salvo(new[] { BurstStyle.RedSpider() }, 1, -30, 2.2f);

            // Building: pairs, then fans of colour.
            for (int i = 0; i < 4; i++)
            {
                var pair = new[] { BurstStyle.Arc(ShellBlue, ShellBlue), BurstStyle.Arc(ShellPink, ShellOrange) };
                yield return Salvo(pair, 2, i % 2 == 0 ? -22 : 22, 1.7f);
            }

            yield return Salvo(new[] { BurstStyle.Ring(ShellPink), BurstStyle.Ring(ShellBlue), BurstStyle.Ring(ShellGold) }, 3, 0, 1.9f);
            yield return Salvo(new[] { BurstStyle.Palm(ShellGold, ShellOrange), BurstStyle.Palm(ShellPink, ShellGold) }, 2, 0, 2.5f);

            // The willow pause: a slow gold curtain before the finale.
            yield return Salvo(new[] { BurstStyle.Willow(ShellGold) }, 3, 0, 4.2f);

            // A tight, rising barrage.
            for (int i = 0; i < 14; i++)
            {
                var style = i % 3 == 0 ? BurstStyle.GoldChrysanthemum() : i % 3 == 1 ? BurstStyle.VioletPeony() : BurstStyle.RedSpider();
                yield return Salvo(new[] { style }, 2, UnityEngine.Random.Range(-38f, 38f), 0.42f);
            }

            yield return Finale();
            Session.sound.Play("thunder", SkyPoint(0), 1, 0.02f, 0.2f);
            yield return Salvo(new[] { BurstStyle.Willow(ShellGold) }, 5, 0, 5.5f);

            ShowRunning = false;
            Session.CelebrationEnded();
        }

        /// <summary>
        /// The closing front: a bank of red shells side by side, above a line of fans, above a line of silver
        /// arches. The shells go first, so that all three rows are open together.
        /// </summary>
        public IEnumerator Finale()
        {
            Transform head = Session.rig.view.transform;
            Vector3 forward = head.forward;
            forward.y = 0;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 ground = new Vector3(head.position.x, Session.placement.FloorHeight, head.position.z) + forward * FrontDistance;
            Vector3 Front(float across, float height) => ground + right * across + Vector3.up * height;
            float Across(int index, int count, float halfWidth) => Mathf.Lerp(-halfWidth, halfWidth, index / (float)(count - 1));
            float delay = FrontDistance / SpeedOfSound;

            for (int i = 0; i < FrontShells; i++)
            {
                Vector3 target = Front(Across(i, FrontShells, FrontHalfWidth), 26 + (i % 2) * 2.5f);
                if (i % 2 == 0) Session.sound.Play("launch", target + Vector3.down * 19, 1, 0.03f, delay);
                Shell(target + Vector3.down * 19, target, BurstStyle.RedBead(i == 0 ? 0.45f : 0.14f).Sized(FrontBurstRadius));
                yield return new WaitForSeconds(0.06f);
            }
            yield return new WaitForSeconds(0.9f);

            Session.sound.Play("crackle", Front(0, 3), 1, 0.02f, delay);
            for (int i = 0; i < FrontLowCount; i++) Low(Front(Across(i, FrontLowCount, FrontLowHalfWidth), 2.6f), BurstStyle.Arch().Sized(2.9f));
            yield return new WaitForSeconds(0.3f);

            Color[] beads = { ShellFestivalRed, ShellWhite, ShellGold, ShellPink };
            for (int i = 0; i < FrontLowCount; i++) Low(Front(Across(i, FrontLowCount, FrontLowHalfWidth), 4.5f), BurstStyle.Fan(beads[i % beads.Length]).Sized(7));
            yield return new WaitForSeconds(3.2f);
        }

        /// <summary>A low effect with no shell, flash or report of its own.</summary>
        void Low(Vector3 position, BurstStyle style) => Arm(lowPool[lowCursor++ % lowPool.Count], position, style, 1.9f);

        IEnumerator Salvo(BurstStyle[] styles, int count, float centreYaw, float pause)
        {
            for (int i = 0; i < count; i++)
            {
                float spread = count > 1 ? Mathf.Lerp(-34, 34, i / (float)(count - 1)) : 0;
                Vector3 target = SkyPoint(centreYaw + spread);
                Vector3 start = target + new Vector3(UnityEngine.Random.Range(-3f, 3f), -19, UnityEngine.Random.Range(-3f, 3f));

                // A launch out across the sky, carried by the same delay as the bursts.
                float distance = Vector3.Distance(start, Session.rig.view.transform.position);
                Session.sound.Play("launch", start, 1, 0.03f, distance / SpeedOfSound);

                // Further off than a box's shells, so these can open wider and still look the same size.
                Shell(start, target, styles[i % styles.Length].Sized(ShowBurstRadius * UnityEngine.Random.Range(0.9f, 1.1f)));
            }
            yield return new WaitForSeconds(pause);
        }

        public void StopEverything()
        {
            StopAllCoroutines();
            ShowRunning = false;
            foreach (var comet in comets) Retire(comet);
            foreach (var burst in skyPool) { burst.active = false; burst.view.enabled = false; }
            foreach (var burst in popPool) { burst.active = false; burst.view.enabled = false; }
            foreach (var burst in lowPool) { burst.active = false; burst.view.enabled = false; }
            foreach (var flash in flashPool) { flash.active = false; flash.view.enabled = false; }
            if (embers) embers.Clear();
        }
    }
}
