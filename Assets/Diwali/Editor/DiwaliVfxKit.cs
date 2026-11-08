using UnityEditor;
using UnityEngine;

namespace Diwali.Editor
{
    /// <summary>Effect materials and the building blocks shared by every cracker prefab.</summary>
    public static partial class DiwaliSceneBuilder
    {
        static Mesh flameBox;
        static Material flameMaterial, jetMaterial, haloMaterial, poolMaterial, shadowMaterial, ringMaterial, sparkMaterial, smokeMaterial, debrisMaterial;
        static Material blackSmokeMaterial, edgeHaloMaterial;
        static Material anarSparks, potSparks, chakriSparks, fuseSparks, muzzleSparks, cueMaterial, burstMaterial, reticleMaterial;
        static Texture2D noiseTexture;
        const float AnarHeightScale = 1.5f;

        static Material Fx(string name, string shader, System.Action<Material> setup = null)
        {
            var found = Shader.Find(shader);
            if (!found) throw new System.InvalidOperationException("Shader did not compile: " + shader);
            var material = new Material(found) { enableInstancing = true };
            setup?.Invoke(material);
            return Asset(material, name);
        }

        const int TrailSparks = 160;

        /// <summary>Largest ladi puff, before the cracker's own size. Small puffs, in quantity, for the same bank of smoke.</summary>
        const float LadiPuffSize = 0.26f;

        static Material Sparks(string name, Color hot, Color cool, float speedMin, float speedMax, float spread, float lifeMin, float lifeMax,
            float drag, float gravity, float size, float stretch, float radius = 0.012f, float swirl = 0, float boost = 1.7f, float heightScale = 1)
        {
            return Fx(name, "Diwali/Spark Field", m =>
            {
                m.SetColor("_HotColor", hot);
                m.SetColor("_CoolColor", cool);
                m.SetFloat("_SpeedMin", speedMin);
                m.SetFloat("_SpeedMax", speedMax);
                m.SetFloat("_HeightScale", heightScale);
                m.SetFloat("_Spread", spread);
                m.SetFloat("_LifeMin", lifeMin);
                m.SetFloat("_LifeMax", lifeMax);
                m.SetFloat("_Drag", drag);
                m.SetFloat("_Gravity", gravity);
                m.SetFloat("_Size", size);
                m.SetFloat("_Stretch", stretch);
                m.SetFloat("_Radius", radius);
                m.SetFloat("_Swirl", swirl);
                m.SetFloat("_Boost", boost);
            });
        }

        static void BuildEffectMaterials()
        {
            noiseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Diwali/Textures/fx_noise.png");
            var smokeSheet = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Diwali/Textures/fx_smoke.png");

            flameMaterial = Fx("Fire flame", "Diwali/Living Flame", m => m.SetTexture("_NoiseTex", noiseTexture));
            jetMaterial = Fx("Fire jet", "Diwali/Living Flame", m =>
            {
                m.SetTexture("_NoiseTex", noiseTexture);
                m.SetFloat("_Turbulence", 0.95f);
                m.SetFloat("_Speed", 3.6f);
                m.SetFloat("_Body", 0.62f);
                m.SetFloat("_Gain", 4.2f);
                m.SetColor("_Hot", new Color(1, 0.97f, 0.86f));
                m.SetColor("_Mid", new Color(1, 0.74f, 0.26f));
                m.SetColor("_Base", new Color(1, 0.7f, 0.3f));
            });
            haloMaterial = Fx("Fire halo", "Diwali/Glow", m => { m.SetColor("_Tint", new Color(1, 0.52f, 0.14f)); m.SetFloat("_Falloff", 5.5f); });
            reticleMaterial = Fx("Pointer reticle", "Diwali/Glow", m => { m.SetColor("_Tint", new Color(1, 0.88f, 0.66f)); m.SetFloat("_Intensity", 1.1f); m.SetFloat("_Falloff", 7); });
            cueMaterial = Fx("Fuse cue", "Diwali/Glow", m => { m.SetColor("_Tint", new Color(1, 0.62f, 0.2f)); m.SetFloat("_Intensity", 0.2f); m.SetFloat("_Falloff", 6); });
            poolMaterial = Fx("Light on surface", "Diwali/Surface Light", m => { m.SetTexture("_NoiseTex", noiseTexture); m.SetColor("_Tint", new Color(1, 0.46f, 0.13f)); });
            shadowMaterial = Fx("Contact shadow", "Diwali/Surface Light", m => { m.SetFloat("_Shadow", 1); m.SetFloat("_Intensity", 0.62f); });
            ringMaterial = Fx("Surface ring", "Diwali/Surface Light", m =>
            {
                m.SetColor("_Tint", new Color(1, 0.76f, 0.36f));
                m.SetFloat("_Intensity", 0.9f);
                m.SetFloat("_Ring", 0.035f);
                m.SetFloat("_RingRadius", 0.92f);
            });
            sparkMaterial = Fx("Spark streak", "Diwali/SoftSpark");
            smokeMaterial = Fx("Smoke", "Diwali/Smoke", m => m.SetTexture("_MainTex", smokeSheet));
            blackSmokeMaterial = Fx("Black smoke", "Diwali/Smoke", m =>
            {
                m.SetTexture("_MainTex", smokeSheet);
                m.SetColor("_Shade", new Color(0.012f, 0.012f, 0.016f));
                m.SetFloat("_Thickness", 3.2f);
                m.SetFloat("_Blur", 0.6f);
                m.SetFloat("_Cohesion", 0.6f);
            });
            edgeHaloMaterial = Fx("Edge halo", "Diwali/Edge Halo");
            debrisMaterial = Fx("Paper debris", "Diwali/Debris");
            burstMaterial = Fx("Star burst", "Diwali/Star Burst");

            var gold = new Color(1, 0.8f, 0.42f);
            var ember = new Color(1, 0.3f, 0.05f);
            anarSparks = Sparks("Sparks anar", gold, ember, 1.9f, 5.2f, 0.27f, 0.55f, 1.65f, 0.9f, 5.2f, 0.0036f, 0.018f, 0.014f, heightScale: AnarHeightScale);
            potSparks = Sparks("Sparks flower pot", new Color(0.92f, 0.96f, 1), new Color(0.5f, 0.8f, 1), 2.2f, 5.8f, 0.2f, 0.5f, 1.4f, 1, 5.2f, 0.0034f, 0.02f, 0.011f);
            chakriSparks = Sparks("Sparks chakri", gold, ember, 1.6f, 3.6f, 0.5f, 0.28f, 0.72f, 2.2f, 4, 0.003f, 0.013f, 0.06f, 1);
            fuseSparks = Sparks("Sparks fuse", new Color(1, 0.86f, 0.55f), ember, 0.3f, 1, 1.1f, 0.1f, 0.3f, 2.8f, 2.6f, 0.0018f, 0.012f, 0.002f, 0, 2.1f);
            muzzleSparks = Sparks("Sparks muzzle", gold, ember, 3, 7, 0.34f, 0.2f, 0.65f, 2, 5, 0.0034f, 0.018f, 0.03f);
        }

        static SparkField SparkEmitter(string name, Transform parent, Vector3 position, Material material, int count, float longestLife)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(SparkField));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var field = go.GetComponent<SparkField>();
            field.sparkCount = count;
            field.longestLife = longestLife;
            return field;
        }

        static GameObject Billboard(string name, Transform parent, Material material, float size, Vector3 position = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A quad lying flat on the surface below the item.</summary>
        static GameObject Decal(string name, Transform parent, Material material, float size, float height = 0.0015f)
        {
            var go = Billboard(name, parent, material, size, new Vector3(0, height, 0));
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            return go;
        }

        /// <summary>A complete fire source: volume, halo, light on the surface and a point light on one flicker.</summary>
        static FlameDriver Fire(string name, Transform parent, Vector3 position, Vector3 volume, Material material,
            float haloSize, float poolSize, float surfaceBelow, float lampRange, float lampLevel)
        {
            var root = Group(name, parent, position);
            var driver = root.gameObject.AddComponent<FlameDriver>();

            if (material)
            {
                var flame = Part("Flame volume", root, FlameBox(), material);
                flame.transform.localScale = volume;
                flame.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                driver.flame = flame.GetComponent<MeshRenderer>();
            }

            if (haloSize > 0)
            {
                var halo = Billboard("Halo", root, haloMaterial, haloSize, new Vector3(0, volume.y * 0.42f, 0));
                driver.halo = halo.GetComponent<MeshRenderer>();
            }

            if (poolSize > 0)
            {
                var pool = Decal("Light on the surface", root, poolMaterial, poolSize, 0.002f - surfaceBelow);
                driver.pool = pool.GetComponent<MeshRenderer>();
            }

            if (lampRange > 0)
            {
                var lamp = Group("Firelight", root, new Vector3(0, volume.y * 0.5f, 0)).gameObject.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.color = new Color(1, 0.5f, 0.17f);
                lamp.range = lampRange;
                lamp.shadows = LightShadows.None;
                lamp.enabled = false;
                driver.lamp = lamp;
                driver.lampLevel = lampLevel;
            }
            return driver;
        }

        static ParticleSystem NewSystem(string name, Transform parent, Vector3 position, int maximum, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = maximum;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = system.emission;
            emission.rateOverTime = 0;
            var shape = system.shape;
            shape.enabled = false;
            var view = go.GetComponent<ParticleSystemRenderer>();
            view.sharedMaterial = material;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return system;
        }

        static Gradient Fade(params (float time, float alpha)[] keys)
        {
            var alphas = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) alphas[i] = new GradientAlphaKey(keys[i].alpha, keys[i].time);
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, alphas);
            return gradient;
        }

        /// <summary>Cooling metal: white, yellow, orange, deep red, then gone.</summary>
        static Gradient Cooling()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1, 0.96f, 0.85f), 0),
                    new GradientColorKey(new Color(1, 0.8f, 0.36f), 0.3f),
                    new GradientColorKey(new Color(1, 0.42f, 0.08f), 0.72f),
                    new GradientColorKey(new Color(0.75f, 0.12f, 0.02f), 1),
                },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            return gradient;
        }

        static ParticleSystem Smoke(string name, Transform parent, Vector3 position, float rate, int maximum, float size, float rise, float life = 3.2f, float opacity = 0.2f)
        {
            var system = NewSystem(name, parent, position, maximum, smokeMaterial);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.65f, life);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startSpeed = new ParticleSystem.MinMaxCurve(rise * 0.6f, rise);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new Color(0.78f, 0.75f, 0.72f, opacity);
            main.gravityModifier = -0.012f;

            var emission = system.emission;
            emission.rateOverTime = rate;
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14;
            shape.radius = 0.02f;
            shape.rotation = new Vector3(-90, 0, 0);

            var colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade((0, 0), (0.14f, 1), (0.6f, 0.6f), (1, 0));
            var scale = system.sizeOverLifetime;
            scale.enabled = true;
            scale.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.45f, 1, 1.5f));
            var turn = system.rotationOverLifetime;
            turn.enabled = true;
            turn.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            var drift = system.noise;
            drift.enabled = true;
            drift.strength = 0.09f;
            drift.frequency = 0.35f;
            drift.quality = ParticleSystemNoiseQuality.Low;

            var sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 15);
            LimitOnScreen(system);
            return system;
        }

        /// <summary>
        /// Blended smoke is what a Quest pays for by the pixel, and a puff near the eye costs the most while
        /// showing the least. This holds any one puff to a share of the view, however close the visitor stands.
        /// </summary>
        static void LimitOnScreen(ParticleSystem system) => system.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.3f;

        /// <summary>The bomb's smoke: near-black billows, with no fire in them.</summary>
        static ParticleSystem DarkSmoke(string name, Transform parent, Vector3 position, int maximum)
        {
            var system = NewSystem(name, parent, position, maximum, blackSmokeMaterial);
            var main = system.main;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new Color(1, 1, 1, 0.94f);

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.08f, 0.074f, 0.074f), 0),
                    new GradientColorKey(new Color(0.05f, 0.046f, 0.05f), 1),
                },
                new[] { new GradientAlphaKey(0.85f, 0), new GradientAlphaKey(0.9f, 0.08f), new GradientAlphaKey(0.62f, 0.45f), new GradientAlphaKey(0, 1) });
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = gradient;

            var turn = system.rotationOverLifetime;
            turn.enabled = true;
            turn.z = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);
            var boil = system.noise;
            boil.enabled = true;
            boil.strength = 0.17f;
            boil.frequency = 0.45f;
            boil.quality = ParticleSystemNoiseQuality.Low;

            var sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 15);

            system.GetComponent<ParticleSystemRenderer>().sortMode = ParticleSystemSortMode.Distance;
            LimitOnScreen(system);
            return system;
        }

        /// <summary>
        /// Overlapping dark billows emitted at the charge, expanding and rising after detonation.
        /// </summary>
        static ParticleSystem BlackClouds(Transform parent)
        {
            var system = DarkSmoke("Blast plume", parent, Vector3.zero, 160);
            var main = system.main;
            main.startSpeed = 0;
            TuneBlastPlume(system);

            // Each puff has its own buoyancy, so the plume draws out into a column rather than sitting as a ball.
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.02f, -0.05f);

            // Without drag a puff keeps its blast speed for its whole life, and the plume leaves the charge
            // for the ceiling. It has to scale with speed: on its own this module is a fixed deceleration,
            // which would cancel the buoyancy outright and leave a ball that never rises.
            var slow = system.limitVelocityOverLifetime;
            slow.enabled = true;
            slow.limit = 30;
            slow.drag = 3;
            slow.multiplyDragByParticleSize = false;
            slow.multiplyDragByParticleVelocity = true;

            var noise = system.noise;
            noise.strength = 0.04f;
            return system;
        }

        /// <summary>
        /// The plume's budget, in many small puffs rather than a few big ones: its billows are detail the eye
        /// reads as smoke, and they fill far less of the screen. Each puff's size and life come from BlastItem.
        /// </summary>
        static void TuneBlastPlume(ParticleSystem system)
        {
            var main = system.main;
            main.maxParticles = 160;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.8f, 7);
            main.startSize = new ParticleSystem.MinMaxCurve(0.17f, 0.46f);
            var scale = system.sizeOverLifetime;
            scale.enabled = true;
            scale.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.7f, 1, 2.4f));
            var sheet = system.textureSheetAnimation;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 15);
            LimitOnScreen(system);
        }

        /// <summary>A brief flame at the charge, hidden until detonation. Its light still lands on the floor below.</summary>
        static FlameDriver BlastFlame(Transform parent, float chargeHeight)
        {
            const float height = 0.5f;
            var flame = Fire("Blast flame", parent, Vector3.up * chargeHeight, new Vector3(height * 0.42f, height, height * 0.42f), jetMaterial, 1.1f, 1.5f, chargeHeight, 3.5f, 1.6f);
            flame.haloLevel = 0.3f;
            flame.poolLevel = 0.42f;
            flame.riseTime = 0.035f;
            flame.flickerRate = 7;
            flame.flickerDepth = 0.34f;
            flame.leanResponse = 0;
            return flame;
        }

        /// <summary>Red paper that lands on the real floor and stays there for a while after the noise stops.</summary>
        static ParticleSystem Litter(Transform parent, int maximum)
        {
            var system = NewSystem("Litter", parent, Vector3.zero, maximum, debrisMaterial);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(16, 26);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.017f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.gravityModifier = 0.55f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.74f, 0.09f, 0.08f), new Color(0.3f, 0.2f, 0.16f));
            main.stopAction = ParticleSystemStopAction.Destroy;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.015f;
            shape.rotation = new Vector3(-90, 0, 0);

            var colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade((0, 1), (0.85f, 1), (1, 0));
            var slow = system.limitVelocityOverLifetime;
            slow.enabled = true;
            slow.drag = 1.8f;
            slow.limit = 30;

            var land = system.collision;
            land.enabled = true;
            land.type = ParticleSystemCollisionType.Planes;
            land.mode = ParticleSystemCollisionMode.Collision3D;
            land.dampen = 0.7f;
            land.bounce = 0.12f;
            land.lifetimeLoss = 0;
            land.radiusScale = 0.2f;

            // Paper lies flat, which is what makes it read as mess on the floor rather than sparks.
            system.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            return system;
        }

        /// <summary>A real sparkler's look comes from forking: every spark splits again as it dies.</summary>
        static ParticleSystem SparklerSparks(Transform parent)
        {
            var system = NewSystem("Sparks", parent, Vector3.zero, 420, sparkMaterial);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.09f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.0024f, 0.0052f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 3.2f);
            main.startColor = Color.white;
            main.gravityModifier = 0.32f;
            var emission = system.emission;
            emission.rateOverTime = 240;
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.003f;
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = Cooling();
            var view = system.GetComponent<ParticleSystemRenderer>();
            view.renderMode = ParticleSystemRenderMode.Stretch;
            view.velocityScale = 0.03f;
            view.lengthScale = 1.2f;
            view.maxParticleSize = 0.2f;

            var fork = NewSystem("Forks", system.transform, Vector3.zero, 900, sparkMaterial);
            var forkMain = fork.main;
            forkMain.loop = false;
            forkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
            forkMain.startSize = new ParticleSystem.MinMaxCurve(0.0014f, 0.0028f);
            forkMain.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.5f);
            forkMain.gravityModifier = 0.22f;
            var forkEmission = fork.emission;
            forkEmission.SetBursts(new[] { new ParticleSystem.Burst(0, 2, 4) });
            var forkShape = fork.shape;
            forkShape.enabled = true;
            forkShape.shapeType = ParticleSystemShapeType.Sphere;
            forkShape.radius = 0.0015f;
            var forkColour = fork.colorOverLifetime;
            forkColour.enabled = true;
            forkColour.color = Cooling();
            var forkView = fork.GetComponent<ParticleSystemRenderer>();
            forkView.renderMode = ParticleSystemRenderMode.Stretch;
            forkView.velocityScale = 0.022f;
            forkView.lengthScale = 1;

            var children = system.subEmitters;
            children.enabled = true;
            children.AddSubEmitter(fork, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing, 0.55f);
            return system;
        }

        static ParticleSystem RocketTrail(Transform parent, Vector3 position)
        {
            var system = NewSystem("Rocket trail", parent, position, TrailSparks, sparkMaterial);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.055f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
            main.gravityModifier = 0.25f;
            ThinTrail(system);
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 11;
            shape.radius = 0.008f;
            shape.rotation = new Vector3(90, 0, 0);
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = Cooling();
            var view = system.GetComponent<ParticleSystemRenderer>();
            view.renderMode = ParticleSystemRenderMode.Stretch;
            view.velocityScale = 0.02f;
            view.lengthScale = 1.4f;
            return system;
        }

        /// <summary>A thin line of sparks: a climb of about twenty metres no longer needs a dense column of them.</summary>
        static void ThinTrail(ParticleSystem system)
        {
            var main = system.main;
            main.maxParticles = TrailSparks;
            var emission = system.emission;
            emission.rateOverTime = 12;
            emission.rateOverDistance = 8;
        }

        /// <summary>Hot flecks shared by every ground report, held as a prefab so the sky show can own one copy.</summary>
        static ParticleSystem BuildEmberPrefab()
        {
            var system = NewSystem("Embers", null, Vector3.zero, 500, sparkMaterial);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.0032f, 0.0085f);
            main.startSpeed = 0;
            main.gravityModifier = 0.62f;
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = Cooling();
            var slow = system.limitVelocityOverLifetime;
            slow.enabled = true;
            slow.limit = 30;
            slow.drag = 1.6f;
            slow.multiplyDragByParticleSize = false;
            slow.multiplyDragByParticleVelocity = true;
            var land = system.collision;
            land.enabled = true;
            land.type = ParticleSystemCollisionType.Planes;
            land.mode = ParticleSystemCollisionMode.Collision3D;
            land.dampen = 0.45f;
            land.bounce = 0.32f;
            land.lifetimeLoss = 0.25f;
            land.radiusScale = 0.2f;
            var view = system.GetComponent<ParticleSystemRenderer>();
            view.renderMode = ParticleSystemRenderMode.Stretch;
            view.velocityScale = 0.022f;
            view.lengthScale = 1;
            view.maxParticleSize = 0.1f;
            return SavePrefab(system.gameObject, "Embers").GetComponent<ParticleSystem>();
        }

        /// <summary>The shell's rising tail, held as a prefab so the sky show can pool copies of it.</summary>
        static ParticleSystem BuildCometPrefab()
        {
            var system = NewSystem("Comet", null, Vector3.zero, TrailSparks, sparkMaterial);
            var main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.62f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.7f);
            main.gravityModifier = 0.32f;
            ThinTrail(system);

            // Taking on part of the shell's speed turns each spark into a streak along its path.
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
            var inherit = system.inheritVelocity;
            inherit.enabled = true;
            inherit.mode = ParticleSystemInheritVelocityMode.Initial;
            inherit.curveMultiplier = 0.16f;
            var view = system.GetComponent<ParticleSystemRenderer>();
            view.renderMode = ParticleSystemRenderMode.Stretch;
            view.velocityScale = 0.05f;
            view.lengthScale = 1.1f;
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade((0, 1), (0.5f, 0.75f), (1, 0));
            var scale = system.sizeOverLifetime;
            scale.enabled = true;
            scale.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0.2f));

            var head = Billboard("Head", system.transform, haloMaterial, 0.5f);
            head.GetComponent<MeshRenderer>().sharedMaterial = Fx("Comet head", "Diwali/Glow", m =>
            {
                m.SetColor("_Tint", new Color(1, 0.82f, 0.52f));
                m.SetFloat("_Intensity", 1.2f);
                m.SetFloat("_Falloff", 7);
            });
            return SavePrefab(system.gameObject, "Comet").GetComponent<ParticleSystem>();
        }
    }
}
