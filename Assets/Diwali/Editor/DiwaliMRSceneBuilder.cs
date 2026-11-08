using System.Collections.Generic;
using System.IO;
using Meta.XR.MRUtilityKit;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Diwali.Editor
{
    /// <summary>Regenerates the mixed reality scene, the cracker prefabs and the catalog from source.</summary>
    public static partial class DiwaliSceneBuilder
    {
        public const string MRScenePath = "Assets/Scenes/DiwaliExperience.unity";
        public const string SplashScenePath = "Assets/Scenes/SplashScreen.unity";
        const string LegacyScenePath = "Assets/Scenes/DiwaliMixedReality.unity";
        const string PrefabRoot = "Assets/Diwali/Prefabs";
        const string TextureRoot = "Assets/Diwali/Textures";
        const string AudioRoot = "Assets/Diwali/Audio";
        const int TargetLayer = 8;

        // Sized for a pinch ray from across the room. A hand inside the grab region still picks the item up.
        const float FuseTargetRadius = 0.05f;

        static readonly Color Ink = new Color(0.97f, 0.95f, 0.9f);
        static readonly Color Muted = new Color(0.97f, 0.95f, 0.9f, 0.62f);
        static readonly Color Brass = new Color(1, 0.76f, 0.36f);

        static TMP_FontAsset uiFont;

        [MenuItem("Diwali/Build Mixed Reality Scene")]
        public static void BuildMR()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop play mode before generating the scene.");

            Root = "Assets/Diwali/GeneratedMR";
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(PrefabRoot);
            mrMaterials = true;
            materials = new Dictionary<string, Material>();
            meshId = 0;
            flameBox = null;
            foreach (string stale in Directory.GetFiles(Root, "Mesh-*.asset")) AssetDatabase.DeleteAsset(stale.Replace('\\', '/'));
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (!uiFont) throw new FileNotFoundException("TextMesh Pro essentials are missing. Run the import-tmp command.");

            ImportMedia();
            var rounded = AssetImporter.GetAtPath("Assets/Diwali/Art/Original/RoundedPanel.png") as TextureImporter;
            if (rounded && rounded.textureType != TextureImporterType.Sprite)
            {
                rounded.textureType = TextureImporterType.Sprite;
                rounded.spriteBorder = new Vector4(36, 36, 36, 36);
                rounded.SaveAndReimport();
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            gold = Mat("Antique gold", new Color(0.75f, 0.39f, 0.095f), 0.75f, 0.72f);
            clay = Mat("Terracotta", new Color(0.42f, 0.12f, 0.045f));
            teal = Mat("Peacock teal", new Color(0.025f, 0.27f, 0.26f), 0.3f, 0.5f);
            indigo = Mat("Midnight velvet", new Color(0.024f, 0.014f, 0.064f), 0.1f, 0.4f);
            ivory = Mat("Warm ivory", new Color(0.85f, 0.69f, 0.43f));
            rose = Mat("Festival fuchsia", new Color(0.62f, 0.018f, 0.12f), 0.1f, 0.45f);
            glow = Mat("Gold radiance", new Color(1, 0.46f, 0.055f), 0, 0.4f, 3);
            BuildEffectMaterials();

            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.48f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.34f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.13f, 0.12f);

            var fill = Group("Virtual object fill light", null, Vector3.zero).gameObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(1, 0.86f, 0.72f);
            fill.intensity = 0.55f;
            fill.transform.rotation = Quaternion.Euler(52, -25, 0);
            fill.shadows = LightShadows.None;

            session = new GameObject("Diwali • session").AddComponent<DiwaliSession>();
            session.gameObject.AddComponent<FestivalPerf>();
            BuildRig();
            session.rig.reticleMaterial = reticleMaterial;
            session.rig.edgeHaloMaterial = edgeHaloMaterial;
            session.rig.view.clearFlags = CameraClearFlags.SolidColor;
            session.rig.view.backgroundColor = Color.clear;
            session.rig.view.cullingMask = ~0;
            session.rig.view.nearClipPlane = 0.03f;
            session.rig.view.farClipPlane = 220;

            var catalog = BuildCatalog();
            session.catalog = catalog;
            session.sound = BuildSound();
            BuildAmbience();
            BuildSky();
            BuildRoomUnderstanding();
            BuildHeldItems();
            BuildPreviewRoom();
            session.thali = BuildThali(catalog);
            session.music = BuildFestivalMusic(session.transform);
            session.hint = BuildHint();

            ConfigureQuest();
            ConfigureMR();
            EditorSceneManager.SaveScene(scene, MRScenePath);
            BuildSplashScene();
            ConfigureSceneFlow();
            AssetDatabase.SaveAssets();
            Debug.Log("DIWALI_MR_READY: " + MRScenePath);
        }

        // ---------- media import ----------

        static void ImportMedia()
        {
            foreach (string path in Directory.GetFiles(TextureRoot, "*.png"))
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
                if (!importer) continue;
                string file = Path.GetFileNameWithoutExtension(path);
                importer.maxTextureSize = 1024;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.anisoLevel = 2;
                if (file == "fx_noise")
                {
                    importer.sRGBTexture = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                }
                else if (file == "fx_smoke")
                {
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.alphaIsTransparency = true;
                }
                else if (file.StartsWith("ui_"))
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.alphaIsTransparency = true;
                    if (file == "ui_round")
                    {
                        var sprite = new TextureImporterSettings();
                        importer.ReadTextureSettings(sprite);
                        sprite.spriteMode = (int)SpriteImportMode.Single;
                        sprite.spriteBorder = new Vector4(63, 63, 63, 63);
                        sprite.spriteMeshType = SpriteMeshType.FullRect;
                        importer.SetTextureSettings(sprite);
                    }
                }
                importer.SaveAndReimport();
            }

            var recordedAudio = FestivalRecordedAudio.ClipNames();
            foreach (string path in Directory.GetFiles(AudioRoot, "*.wav"))
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as AudioImporter;
                if (!importer) continue;
                string clipName = Path.GetFileNameWithoutExtension(path);
                bool music = clipName == "utsav_music_120";
                bool signature = clipName == "utsav_jingle";
                bool bed = clipName == "night_bed";
                var settings = importer.defaultSampleSettings;
                settings.loadType = music ? AudioClipLoadType.Streaming : bed ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = recordedAudio.Contains(clipName) && !bed && clipName != "fuse_loop"
                    ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis;
                settings.quality = 0.72f;
                settings.preloadAudioData = !music;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = !music && !signature;
                if (recordedAudio.Contains(clipName)) FestivalRecordedAudio.PreserveLevels(importer);
                importer.SaveAndReimport();
            }
        }

        static Material Wrap(string name, float smoothness = 0.34f)
        {
            var material = Mat("Wrap " + name, Color.white, 0, smoothness);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureRoot}/wrap_{name}.png"));
            return material;
        }

        static GameObject SavePrefab(GameObject root, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------- item construction ----------

        static T NewItem<T>(string name, CrackerId id, out Transform model) where T : FestivalItem
        {
            var item = new GameObject(name).AddComponent<T>();
            item.id = id;
            model = Group("Model", item.transform, Vector3.zero);
            item.model = model.gameObject;
            return item;
        }

        static void AddFuse(FestivalItem item, Vector3 root, Vector3 tip, float cueSize = 0.07f)
        {
            Rod("Fuse cord", item.model.transform, root, tip, 0.0022f, Mat("Fuse paper", new Color(0.32f, 0.27f, 0.22f)));
            item.fuseCord = item.model.transform.Find("Fuse cord");
            item.fuseRoot = root;
            item.fuse = Group("Fuse point", item.transform, tip);
            item.fuseSparks = SparkEmitter("Fuse sparks", item.fuse, Vector3.zero, fuseSparks, 110, 0.4f);
            var target = item.fuse.gameObject.AddComponent<FestivalFuseTarget>();
            target.item = item;
            item.fuse.gameObject.AddComponent<SphereCollider>().radius = FuseTargetRadius;
            item.fuseCue = Billboard("Fuse cue", item.fuse, cueMaterial, cueSize).GetComponent<Renderer>();
        }

        static GameObject Finish(FestivalItem item, string prefabName, Vector3 colliderCentre, Vector3 colliderSize, float shadowSize)
        {
            foreach (var transform in item.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 0;
            item.gameObject.layer = TargetLayer;
            if (item.fuse) item.fuse.gameObject.layer = TargetLayer;
            var collider = item.gameObject.AddComponent<BoxCollider>();
            collider.center = colliderCentre;
            collider.size = colliderSize;

            var zone = item.gameObject.AddComponent<GrabZone>();
            zone.centre = colliderCentre;
            zone.radius = Mathf.Clamp(Mathf.Max(colliderSize.x, Mathf.Max(colliderSize.y, colliderSize.z)) * 0.5f + 0.02f, 0.09f, 0.18f);

            if (shadowSize > 0) Decal("Contact shadow", item.transform, shadowMaterial, shadowSize);
            return SavePrefab(item.gameObject, prefabName);
        }

        static GameObject BuildDiya()
        {
            var item = NewItem<DiyaItem>("Diya", CrackerId.Diya, out var model);
            var art = Art("Diya", model, Vector3.zero, 0, 0.16f);
            Vector3 wickPoint = WickTip(art) + Vector3.up * 0.002f;

            item.fuse = Group("Wick", item.transform, wickPoint);
            item.fuseSeconds = 0;
            var target = item.fuse.gameObject.AddComponent<FestivalFuseTarget>();
            target.item = item;
            item.fuse.gameObject.AddComponent<SphereCollider>().radius = FuseTargetRadius;
            item.fuseCue = Billboard("Wick cue", item.fuse, cueMaterial, 0.05f).GetComponent<Renderer>();
            item.flame = Fire("Flame", item.transform, wickPoint, new Vector3(0.034f, 0.078f, 0.034f), flameMaterial, 0.3f, 0.8f, wickPoint.y, 1.5f, 0.2f);
            item.flame.haloLevel = 0.24f;
            item.flame.poolLevel = 0.2f;
            return Finish(item, "Diya", new Vector3(0, 0.05f, 0), new Vector3(0.18f, 0.12f, 0.18f), 0.2f);
        }

        /// <summary>The end of the wick that rests on the lip of the bowl: its point furthest from the bowl's centre.</summary>
        static Vector3 WickTip(GameObject art)
        {
            var wick = System.Array.Find(art.GetComponentsInChildren<MeshFilter>(), part => part.name.ToLowerInvariant().Contains("wick"));
            if (!wick) return new Vector3(0, 0.06f, 0);

            Vector3 best = wick.transform.position;
            float furthest = -1;
            foreach (var vertex in wick.sharedMesh.vertices)
            {
                Vector3 point = wick.transform.TransformPoint(vertex);
                float reach = new Vector2(point.x, point.z).magnitude;
                if (reach <= furthest) continue;
                furthest = reach;
                best = point;
            }
            return best;
        }

        static GameObject BuildRangoli()
        {
            var item = NewItem<DecorItem>("Rangoli", CrackerId.Rangoli, out var model);
            Ring(model, new Vector3(0, 0.002f, 0), 0.4f, 0.425f, gold);
            Ring(model, new Vector3(0, 0.003f, 0), 0.43f, 0.445f, rose);
            Ring(model, new Vector3(0, 0.003f, 0), 0.13f, 0.165f, gold);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6;
                var petal = Shape("Petal", PrimitiveType.Sphere, model, new Vector3(Mathf.Cos(angle) * 0.29f, 0.003f, Mathf.Sin(angle) * 0.29f), new Vector3(0.2f, 0.004f, 0.075f), i % 2 == 0 ? rose : teal);
                petal.transform.localRotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
            }
            return Finish(item, "Rangoli", new Vector3(0, 0.01f, 0), new Vector3(0.9f, 0.03f, 0.9f), 0);
        }

        static GameObject BuildFountain(string name, CrackerId id, string wrap, Vector2[] profile, float top, Material sparks, int count, float seconds, Color second)
        {
            var item = NewItem<FountainItem>(name, id, out var model);
            Part("Paper shell", model, Lathe(profile), Wrap(wrap));
            AddFuse(item, new Vector3(0, top, 0), new Vector3(0.006f, top + 0.032f, 0));

            item.sparks = SparkEmitter("Fountain", item.transform, new Vector3(0, top, 0), sparks, count, 1.8f);
            item.duration = seconds;
            item.secondColour = second;
            float flameHeight = 0.15f * (id == CrackerId.Anar ? AnarHeightScale : 1);
            item.jet = Fire("Nozzle fire", item.transform, new Vector3(0, top - 0.004f, 0), new Vector3(0.046f, flameHeight, 0.046f), jetMaterial, 0.8f, 3.2f, top, 3.2f, 2.4f);
            item.jet.haloLevel = 0.5f;
            item.jet.poolLevel = 0.62f;
            item.jet.riseTime = 0.5f;
            item.jet.flickerRate = 9;
            item.jet.flickerDepth = 0.32f;
            item.smoke = Smoke("Smoke", item.transform, new Vector3(0, top + 0.05f, 0), 6, 32, 0.28f, 0.42f);
            return Finish(item, name, new Vector3(0, top / 2, 0), new Vector3(0.16f, top + 0.05f, 0.16f), 0.21f);
        }

        static GameObject BuildChakri()
        {
            var item = NewItem<SpinnerItem>("Chakri", CrackerId.Chakri, out var model);
            var disc = Group("Spinning disc", model, new Vector3(0, 0.004f, 0));
            var profile = new[] { new Vector2(0, 0.019f), new Vector2(0.056f, 0.019f), new Vector2(0.063f, 0.011f), new Vector2(0.06f, 0), new Vector2(0, 0) };
            Part("Coil", disc, Lathe(profile, 48, true), Wrap("chakri"));
            item.disc = disc;
            item.fuseSeconds = 1.8f;
            AddFuse(item, new Vector3(0.06f, 0.014f, 0), new Vector3(0.088f, 0.018f, 0), 0.06f);

            item.sparks = SparkEmitter("Rim sparks", item.transform, new Vector3(0, 0.014f, 0), chakriSparks, 1100, 0.8f);
            item.rim = Fire("Rim glow", item.transform, new Vector3(0, 0.02f, 0), Vector3.one * 0.05f, null, 0.62f, 2.2f, 0.02f, 2.6f, 1.6f);
            item.rim.haloLevel = 0.42f;
            item.rim.poolLevel = 0.62f;
            item.rim.flickerRate = 13;
            item.rim.flickerDepth = 0.35f;
            item.rim.riseTime = 0.5f;
            item.smoke = Smoke("Smoke", item.transform, new Vector3(0, 0.05f, 0), 5, 24, 0.22f, 0.26f, 2.4f, 0.14f);
            return Finish(item, "Chakri", new Vector3(0, 0.02f, 0), new Vector3(0.17f, 0.06f, 0.17f), 0.2f);
        }

        /// <summary>
        /// A real ladi is a tight plait, not a loose string: crackers packed shoulder to shoulder in a
        /// herringbone along a central cord. Each step of the plait is one pair, and one report.
        /// </summary>
        static GameObject BuildLadi(string name, CrackerId id, int steps, float crackerRadius, float crackerLength, float popSize, float safety, int smokePerReport, int litterPerReport, int smokeMaximum, int litterMaximum)
        {
            const float spread = 62;
            var item = NewItem<ChainItem>(name, id, out var model);
            var paper = Wrap("ladi", 0.25f);
            var pair = CrackerPair(crackerRadius, crackerLength, spread);

            // Spaced so neighbours just touch, measured across their slanted bodies.
            float pitch = crackerRadius * 2.05f / Mathf.Sin(spread * Mathf.Deg2Rad);
            float length = pitch * (steps - 1);
            Rod("Cord", model, new Vector3(-length / 2 - 0.01f, crackerRadius * 1.5f, 0), new Vector3(length / 2 + 0.01f, crackerRadius * 1.5f, 0), crackerRadius * 0.9f, Mat("Twine", new Color(0.78f, 0.64f, 0.38f)));

            var pieces = new Transform[steps];
            for (int i = 0; i < steps; i++)
            {
                var step = Part("Pair " + (i + 1), model, pair, paper, new Vector3(length / 2 - i * pitch, crackerRadius * (i % 2 == 0 ? 1 : 1.45f), 0));
                step.transform.localRotation = Quaternion.Euler(0, (i % 3 - 1) * 3.5f, 0);
                pieces[i] = step.transform;
            }
            item.pieces = pieces;

            item.fuseSeconds = 1.8f;
            item.safetyRadius = safety;
            AddFuse(item, new Vector3(length / 2 + 0.008f, crackerRadius, 0), new Vector3(length / 2 + 0.065f, crackerRadius + 0.012f, 0));

            item.popSize = popSize;
            item.smokePerReport = smokePerReport;
            item.litterPerReport = litterPerReport;
            item.smoke = CohesiveSmoke("Smoke", item.transform, Vector3.zero, 0, smokeMaximum * 2, LadiPuffSize * popSize, 0.3f, 4.6f, 0.38f);
            item.smokePerReport = smokePerReport * 3;
            item.litter = Litter(item.transform, litterMaximum);
            item.flashColour = new Color(1, 0.72f, 0.4f);

            float width = crackerLength * 2 * Mathf.Sin(spread * Mathf.Deg2Rad);
            var shadow = Decal("Contact shadow", item.transform, shadowMaterial, 1);
            shadow.transform.localScale = new Vector3(length + 0.09f, width + 0.07f, 1);
            return Finish(item, name, new Vector3(0, 0.02f, 0), new Vector3(length + 0.08f, 0.07f, width + 0.04f), 0);
        }

        /// <summary>Two crackers splayed from the cord, as one mesh, so a long plait stays cheap to draw.</summary>
        static Mesh CrackerPair(float radius, float length, float spread)
        {
            float half = length / 2;
            var shell = Lathe(new[] { new Vector2(radius * 0.9f, -half), new Vector2(radius, -half + 0.002f), new Vector2(radius, half - 0.002f), new Vector2(radius * 0.6f, half), new Vector2(0, half) }, 10);

            float angle = spread * Mathf.Deg2Rad;
            var left = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var right = new Vector3(Mathf.Cos(angle), 0, -Mathf.Sin(angle));
            var parts = new[]
            {
                new CombineInstance { mesh = shell, transform = Matrix4x4.TRS(left * half, Quaternion.FromToRotation(Vector3.up, left), Vector3.one) },
                new CombineInstance { mesh = shell, transform = Matrix4x4.TRS(right * half, Quaternion.FromToRotation(Vector3.up, right), Vector3.one) },
            };

            var mesh = new Mesh();
            mesh.CombineMeshes(parts, true, true);
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        static GameObject BuildCaps()
        {
            var item = NewItem<ChainItem>("Caps", CrackerId.Caps, out var model);
            var paper = Wrap("caps");
            Part("Ring", model, Lathe(RingProfile(0.05f, 0.009f, 0.009f), 40), paper);
            Part("Inner ring", model, Lathe(RingProfile(0.031f, 0.007f, 0.007f), 32), paper);

            const int count = 10;
            var pieces = new Transform[count];
            var dome = Mat("Cap powder", new Color(0.9f, 0.86f, 0.8f));
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                var cap = Shape("Cap " + (i + 1), PrimitiveType.Sphere, model, new Vector3(Mathf.Cos(angle) * 0.05f, 0.017f, Mathf.Sin(angle) * 0.05f), new Vector3(0.013f, 0.006f, 0.013f), dome);
                pieces[i] = cap.transform;
            }
            item.pieces = pieces;
            item.fuseSeconds = 1.2f;
            item.runLength = new Vector2Int(1, 3);
            item.gap = new Vector2(0.05f, 0.14f);
            item.pause = new Vector2(0.04f, 0.2f);
            item.kick = 0;
            item.report = "cap";
            item.volume = 0.85f;
            item.popSize = 0.55f;
            item.flashColour = new Color(1, 0.9f, 0.76f);
            AddFuse(item, new Vector3(0.06f, 0.012f, 0), new Vector3(0.082f, 0.018f, 0), 0.05f);
            item.smoke = Smoke("Smoke", item.transform, Vector3.zero, 0, 14, 0.1f, 0.14f, 1.8f, 0.17f);
            return Finish(item, "Caps", new Vector3(0, 0.02f, 0), new Vector3(0.15f, 0.05f, 0.15f), 0.15f);
        }

        static GameObject BuildBomb()
        {
            var item = NewItem<BlastItem>("Bomb", CrackerId.Bomb, out var model);
            const float radius = 0.04f;
            Part("Paper ball", model, Lathe(SphereProfile(radius, radius), 28), Wrap("bomb"));
            var twine = Mat("Twine", new Color(0.72f, 0.6f, 0.4f));
            var binding = Lathe(RingProfile(radius + 0.0006f, 0.0013f, 0), 28);
            for (int i = 0; i < 3; i++)
            {
                var band = Part("Twine " + i, model, binding, twine, new Vector3(0, radius, 0));
                band.transform.localRotation = Quaternion.Euler(90, i * 60, 0);
            }
            item.fuseRange = new Vector2(5, 7);
            item.safetyRadius = 1.3f;
            item.restSeconds = 11;
            AddFuse(item, new Vector3(0, radius * 2, 0), new Vector3(0.022f, radius * 2 + 0.085f, 0));
            item.clouds = BlackClouds(item.transform);
            item.cloudPuffs = 130;
            item.flames = new[] { BlastFlame(item.transform, 0.045f) };
            item.flameSeconds = 0.22f;
            return Finish(item, "Bomb", new Vector3(0, 0.05f, 0), new Vector3(0.11f, 0.13f, 0.11f), 0.13f);
        }

        static GameObject BuildRocket()
        {
            var item = NewItem<RocketItem>("Rocket", CrackerId.Rocket, out var model);
            item.thrust = RocketThrust;
            item.burstRadius = RocketBurstRadius;
            item.rightLeanDegrees = 2.5f;
            var wood = Mat("Launch stand wood", new Color(0.36f, 0.16f, 0.1f), 0, 0.4f);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2 / 3 + 0.4f;
                Rod("Stand leg", model, new Vector3(Mathf.Cos(angle) * 0.13f, 0, Mathf.Sin(angle) * 0.13f), new Vector3(0, 0.26f, 0), 0.009f, wood);
            }
            // The guide rod stands vertical, and the rocket leaves straight up along it.
            Vector3 rodFoot = new Vector3(0, 0.03f, 0.06f);
            Rod("Guide rod", model, rodFoot, rodFoot + Vector3.up * 0.44f, 0.0065f, wood);

            var body = Group("Rocket body", model, rodFoot + Vector3.up * 0.24f + Vector3.right * 0.011f);
            item.body = body;
            Part("Paper tube", body, Lathe(new[] { new Vector2(0.018f, 0), new Vector2(0.018f, 0.16f) }, 24), Wrap("rocket"));
            Part("Nose", body, Lathe(new[] { new Vector2(0.0205f, 0.158f), new Vector2(0.0205f, 0.163f), new Vector2(0, 0.215f) }, 24), Mat("Rocket nose", new Color(0.72f, 0.06f, 0.08f), 0, 0.5f));
            Rod("Stick", body, new Vector3(-0.011f, -0.17f, 0), new Vector3(-0.011f, 0.02f, 0), 0.005f, Mat("Bamboo", new Color(0.72f, 0.56f, 0.32f)));

            item.fuseSeconds = 2.4f;
            item.safetyRadius = 0.9f;
            Vector3 tail = body.localPosition;
            AddFuse(item, tail, tail + RocketFuseReach);

            item.trail = RocketTrail(body, Vector3.zero);
            item.burningTail = Billboard("Burning tail", body, Fx("Rocket glow", "Diwali/Glow", m =>
            {
                m.SetColor("_Tint", new Color(1, 0.74f, 0.42f));
                m.SetFloat("_Intensity", 1.1f);
                m.SetFloat("_Falloff", 7);
            }), RocketGlowSize).GetComponent<Renderer>();
            item.burningTail.enabled = false;
            item.exhaust = Fire("Exhaust", body, Vector3.zero, new Vector3(0.05f, 0.2f, 0.05f), jetMaterial, 0.8f, 0, 0, 3.2f, 2);
            item.exhaust.transform.localRotation = Quaternion.Euler(180, 0, 0);
            item.exhaust.keepUpright = false;
            item.exhaust.haloLevel = 0.6f;
            item.exhaust.riseTime = 0.12f;
            item.exhaust.flickerRate = 16;
            return Finish(item, "Rocket", new Vector3(0, 0.24f, 0), new Vector3(0.24f, 0.5f, 0.24f), 0.3f);
        }

        static GameObject BuildMortar(string name, CrackerId id, System.Action<Transform> shell, float height, float width, int shots, int finale, float interval)
        {
            var item = NewItem<MortarItem>(name, id, out var model);
            shell(model);
            item.muzzle = Group("Muzzle", item.transform, new Vector3(0, height, 0));
            item.shots = shots;
            item.finaleShots = finale;
            item.interval = interval;
            TuneMortar(item, id == CrackerId.SkyShot);
            item.fuseSeconds = 3;
            item.safetyRadius = 1.1f;
            AddFuse(item, new Vector3(width / 2, height * 0.22f, 0), new Vector3(width / 2 + 0.036f, height * 0.26f, 0));
            item.muzzleSparks = SparkEmitter("Muzzle sparks", item.muzzle, Vector3.zero, muzzleSparks, 380, 0.7f);
            item.smoke = Smoke("Smoke", item.transform, new Vector3(0, height + 0.02f, 0), 7, 40, 0.34f, 0.6f, 3.2f, 0.24f);
            return Finish(item, name, new Vector3(0, height / 2, 0), new Vector3(width + 0.05f, height + 0.04f, width + 0.05f), width * 1.45f);
        }

        // ---------- catalog ----------

        static FestivalCatalog BuildCatalog()
        {
            var tube = new[] { new Vector2(0.042f, 0), new Vector2(0.042f, 0.17f) };
            var lip = new[] { new Vector2(0.042f, 0.17f), new Vector2(0.032f, 0.172f), new Vector2(0.03f, 0.158f), new Vector2(0, 0.158f) };
            var skyShot = BuildMortar("Sky Shot", CrackerId.SkyShot, model =>
            {
                Part("Tube", model, Lathe(tube), Wrap("skyshot"));
                Part("Tube lip", model, Lathe(lip), Mat("Dark card", new Color(0.05f, 0.04f, 0.05f)));
            }, 0.17f, 0.084f, 1, 0, 1);

            var twelve = BuildMortar("12 Shot", CrackerId.TwelveShot, model => Part("Carton", model, Carton(0.15f, 0.12f, 0.15f), Wrap("12shot")), 0.12f, 0.15f, 12, 0, 0.6f);
            var fifteen = BuildMortar("15 Shot", CrackerId.FifteenShot, model => Part("Carton", model, Carton(0.17f, 0.14f, 0.17f), Wrap("15shot")), 0.14f, 0.17f, 15, 3, 0.56f);

            var anarProfile = new[] { new Vector2(0.06f, 0), new Vector2(0.018f, 0.125f), new Vector2(0.012f, 0.13f), new Vector2(0, 0.13f) };
            var potProfile = new[] { new Vector2(0.046f, 0), new Vector2(0.024f, 0.15f), new Vector2(0.03f, 0.152f), new Vector2(0.028f, 0.16f), new Vector2(0, 0.16f) };

            var catalog = ScriptableObject.CreateInstance<FestivalCatalog>();
            catalog.entries = new[]
            {
                Entry(CrackerId.Lighter, "Lighter", "Take to light. Hold to carry; release to drop", CrackerTier.Light, null, 0, held: true),
                Entry(CrackerId.Diya, "Diya", "A small lamp to light and leave burning", CrackerTier.Light, BuildDiya(), 0.1f),
                Entry(CrackerId.Phuljhadi, "Phuljhadi", "Hold it and draw with sparks", CrackerTier.Light, null, 0, held: true),
                Entry(CrackerId.Rangoli, "Rangoli", "Colour for your floor", CrackerTier.Light, BuildRangoli(), 0.46f),
                Entry(CrackerId.Anar, "Anar", "A tall fountain of gold", CrackerTier.Ground, BuildFountain("Anar", CrackerId.Anar, "anar", anarProfile, 0.13f, anarSparks, 1500, 11, default), 0.08f),
                Entry(CrackerId.FlowerPot, "Flower Pot", "A fountain that turns from silver to green", CrackerTier.Ground, BuildFountain("Flower Pot", CrackerId.FlowerPot, "flowerpot", potProfile, 0.16f, potSparks, 1400, 10, SkyShow.Emerald), 0.07f),
                Entry(CrackerId.Chakri, "Chakri", "Spins and skitters across the floor", CrackerTier.Ground, BuildChakri(), 0.09f),
                Entry(CrackerId.Ladi, "Small Ladi", "A tight string that rattles and jumps", CrackerTier.Ground, BuildLadi("Small Ladi", CrackerId.Ladi, 26, 0.0042f, 0.032f, 0.85f, 0.9f, 1, 3, 54, 110), 0.17f),
                Entry(CrackerId.BigLadi, "Big Ladi", "A long string, loud and full of smoke", CrackerTier.Ground, BuildLadi("Big Ladi", CrackerId.BigLadi, 56, 0.0058f, 0.046f, 1.15f, 1.25f, 2, 5, 96, 300), 0.42f),
                Entry(CrackerId.Bomb, "Bomb", "A sharp report and a billowing plume at the charge", CrackerTier.Ground, BuildBomb(), 0.07f),
                Entry(CrackerId.Caps, "Ring Caps", "A ring of tiny snaps", CrackerTier.Ground, BuildCaps(), 0.08f),
                Entry(CrackerId.Rocket, "Rocket", "Climbs and bursts overhead", CrackerTier.Sky, BuildRocket(), 0.14f),
                Entry(CrackerId.SkyShot, "Sky Shot", "One big shell in the open sky", CrackerTier.Sky, skyShot, 0.08f),
                Entry(CrackerId.TwelveShot, "12 Shot", "Twelve golden shells rising straight up", CrackerTier.Sky, twelve, 0.11f),
                Entry(CrackerId.FifteenShot, "15 Shot", "Fifteen shells in every colour", CrackerTier.Sky, fifteen, 0.12f),
                Entry(CrackerId.Butterfly, "Butterfly", "Spins, flutters and lifts with coloured sparks", CrackerTier.Ground, BuildButterfly(), 0.1f),
                Entry(CrackerId.Popper, "Hand Popper", "Grab and throw it; it snaps on impact", CrackerTier.Light, BuildPopper(), 0.04f),
                Entry(CrackerId.GrandShow, "Grand Show", "A full show across your sky", CrackerTier.Sky, null, 0),
            };

            string path = "Assets/Diwali/FestivalCatalog.asset";
            var existing = AssetDatabase.LoadAssetAtPath<FestivalCatalog>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(catalog, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(catalog);
                return existing;
            }
            AssetDatabase.CreateAsset(catalog, path);
            return catalog;
        }

        static CrackerEntry Entry(CrackerId id, string title, string description, CrackerTier tier, GameObject prefab, float radius, bool held = false)
        {
            return new CrackerEntry { id = id, title = title, description = description, tier = tier, prefab = prefab, footprintRadius = radius, held = held };
        }

        // ---------- scene systems ----------

        static FestivalAudio BuildSound()
        {
            AudioClip[] Clips(params string[] names)
            {
                var clips = new AudioClip[names.Length];
                for (int i = 0; i < names.Length; i++) clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioRoot}/{names[i]}.wav");
                return clips;
            }
            SoundEntry Sound(string id, float volume, float near, float send, params string[] names) =>
                new SoundEntry { id = id, clips = Clips(names), volume = volume, minDistance = near, maxDistance = 200, reverbSendDb = send };

            var bank = ScriptableObject.CreateInstance<FestivalSoundBank>();
            bank.sounds = new[]
            {
                Sound("strike", 0.9f, 0.35f, -6, "lighter_strike_0", "lighter_strike_1"),
                Sound("lighter", 0.45f, 0.3f, -14, "lighter_loop"),
                Sound("ignite", 0.9f, 0.5f, -6, "ignite_soft_0", "ignite_soft_1"),
                Sound("flame", 0.5f, 0.3f, -12, "flame_loop"),
                Sound("fuse", 0.8f, 0.5f, -6, "fuse_loop"),
                Sound("sparkler", 0.85f, 0.45f, -6, "sparkler_loop"),
                Sound("anar", 1, 0.9f, -3, "anar_loop"),
                Sound("chakri", 1, 0.8f, -3, "chakri_loop"),
                Sound("butterfly", 0.9f, 0.65f, -6, "butterfly_loop", "butterfly_loop_1"),
                Sound("pop", 1, 1.2f, 0, "pop_0", "pop_1", "pop_2", "pop_3"),
                Sound("cap", 1, 1, -3, "cap_1", "cap_2"),
                Sound("snap", 0.9f, 1.2f, -3, "pop_0", "pop_1", "pop_2"),
                Sound("bomb", 1, 2.4f, -9, "bomb"),
                Sound("mortar", 1, 1.4f, 0, "mortar_0", "mortar_1"),
                Sound("launch", 0.9f, 12, -6, "mortar_0", "mortar_1"),
                Sound("rocket_motor", 1, 1.4f, -3, "rocket_motor_0", "rocket_motor_1"),
                Sound("whistle", 0.85f, 3, -3, "whistle"),
                Sound("burst", 1, 34, -9, "burst_0", "burst_1", "burst_2"),
                Sound("crackle", 0.9f, 28, -12, "crackle"),
                Sound("thunder", 1, 40, -9, "rolling_thunder"),
                Sound("select", 0.6f, 0.6f, -60, "ui_select"),
                Sound("lift", 0.45f, 0.6f, -60, "ui_pickup_soft"),
                Sound("place", 1, 0.7f, -9, "ui_place"),
                Sound("night", 0.9f, 7, -60, "night_bed"),
            };

            FestivalRecordedAudio.ApplyVoicing(bank.sounds);
            string path = "Assets/Diwali/FestivalSoundBank.asset";
            var existing = AssetDatabase.LoadAssetAtPath<FestivalSoundBank>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(bank, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(bank);
                bank = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(bank, path);
            }

            var sound = new GameObject("Spatial sound").AddComponent<FestivalAudio>();
            sound.bank = bank;
            sound.room = new GameObject("Room acoustics").AddComponent<MetaXRAudioRoomAcousticProperties>();
            sound.room.transform.SetParent(sound.transform, false);
            sound.room.ceilingMaterial = MetaXRAudioRoomAcousticProperties.MaterialPreset.GypsumBoard;
            sound.room.floorMaterial = MetaXRAudioRoomAcousticProperties.MaterialPreset.Carpet;
            sound.room.clutterFactor = 0.45f;
            return sound;
        }

        static void BuildAmbience()
        {
            // Without recording this, the override is silently dropped from the saved scene and the
            // prefab default (off) wins: everything runs, and the room is simply black.
            var manager = session.rig.metaRig.GetComponent<OVRManager>();
            manager.isInsightPassthroughEnabled = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);

            var passthrough = Group("Full colour passthrough", session.rig.transform, Vector3.zero).gameObject.AddComponent<OVRPassthroughLayer>();
            passthrough.textureOpacity = 1;
            passthrough.enabled = false;

            var ambience = new GameObject("Festival ambience").AddComponent<FestivalAmbience>();
            ambience.passthrough = passthrough;
            session.ambience = ambience;
        }

        static void BuildSky()
        {
            var sky = new GameObject("Sky show").AddComponent<SkyShow>();
            sky.burstMaterial = burstMaterial;
            sky.glowMaterial = Fx("Burst flash", "Diwali/Glow", m => m.SetFloat("_Falloff", 4.5f));
            sky.cometPrefab = BuildCometPrefab();
            sky.emberPrefab = BuildEmberPrefab();
            session.sky = sky;
        }

        static void BuildRoomUnderstanding()
        {
            var placement = new GameObject("Placement").AddComponent<FestivalPlacement>();
            session.placement = placement;

            var kit = new GameObject("MRUK • room understanding").AddComponent<MRUK>();
            kit.SceneSettings = new MRUK.MRUKSettings { DataSource = MRUK.SceneDataSource.Device, LoadSceneOnStartup = false };
            kit.EnableWorldLock = false;
            kit.enabled = false;
            placement.roomKit = kit;

            var floor = placement.gameObject.AddComponent<FloorLayer>();
            floor.radius = 10;
            var sweep = Fx("Floor reveal", "Diwali/Surface Light", m =>
            {
                m.SetColor("_Tint", new Color(1, 0.78f, 0.45f));
                m.SetFloat("_Ring", 0.012f);
            });
            floor.reveal = Decal("Floor reveal ring", placement.transform, sweep, 1).GetComponent<Renderer>();
            placement.floor = floor;

            placement.dropRing = Decal("Drop ring", placement.transform, ringMaterial, 0.3f).GetComponent<Renderer>();
            var safety = Fx("Safety ring", "Diwali/Surface Light", m =>
            {
                m.SetColor("_Tint", new Color(1, 0.5f, 0.2f));
                m.SetFloat("_Intensity", 0.55f);
                m.SetFloat("_Ring", 0.012f);
                m.SetFloat("_RingRadius", 0.94f);
            });
            placement.safetyRing = Decal("Stand back ring", placement.transform, safety, 1).GetComponent<Renderer>();
        }

        static void BuildHeldItems()
        {
            // Lighter
            var lighter = new GameObject("Lighter").AddComponent<LighterItem>();
            var lighterModel = Group("Model", lighter.transform, Vector3.zero);
            Shape("Brass case", PrimitiveType.Cube, lighterModel, new Vector3(0, 0.03f, 0), new Vector3(0.026f, 0.066f, 0.014f), gold);
            Shape("Chimney", PrimitiveType.Cube, lighterModel, new Vector3(0, 0.071f, 0), new Vector3(0.022f, 0.016f, 0.012f), Mat("Steel", new Color(0.55f, 0.56f, 0.6f), 0.8f, 0.6f));
            lighter.tip = Group("Tip", lighter.transform, new Vector3(0, 0.081f, 0));
            lighter.flame = Fire("Flame", lighter.tip, Vector3.zero, new Vector3(0.017f, 0.05f, 0.017f), flameMaterial, 0.2f, 0, 0, 1.1f, 0.34f);
            lighter.flame.riseTime = 0.08f;
            lighter.flame.flickerRate = 5;
            lighter.flame.flickerDepth = 0.12f;
            var lighterGrip = lighter.gameObject.AddComponent<GrabZone>();
            lighterGrip.centre = new Vector3(0, 0.035f, 0);
            lighterGrip.radius = 0.075f;
            session.lighter = lighter;

            // Phuljhadi
            var sparkler = new GameObject("Phuljhadi").AddComponent<SparklerItem>();
            var sparklerModel = Group("Model", sparkler.transform, Vector3.zero);
            Rod("Wire", sparklerModel, Vector3.zero, new Vector3(0, 0.38f, 0), 0.0028f, Mat("Burnt wire", new Color(0.17f, 0.15f, 0.14f), 0.6f, 0.35f));

            sparkler.coating = Group("Coating pivot", sparklerModel, new Vector3(0, 0.16f, 0));
            sparkler.coating.localScale = new Vector3(1, 0.11f, 1);
            Shape("Coating", PrimitiveType.Cylinder, sparkler.coating, new Vector3(0, 1, 0), new Vector3(0.0072f, 1, 0.0072f), Wrap("sparkler", 0.2f));

            sparkler.burnPoint = Group("Burning point", sparkler.transform, new Vector3(0, 0.38f, 0));
            sparkler.tip = sparkler.burnPoint;
            sparkler.sparks = SparklerSparks(sparkler.burnPoint);
            sparkler.core = Fire("White heat", sparkler.burnPoint, Vector3.zero, Vector3.one * 0.02f, null, 0.34f, 0, 0, 2.2f, 1.1f);
            sparkler.core.haloLevel = 0.62f;
            sparkler.core.flickerRate = 14;
            sparkler.core.flickerDepth = 0.4f;
            sparkler.core.riseTime = 0.2f;
            sparkler.core.halo.sharedMaterial = Fx("Sparkler halo", "Diwali/Glow", m => { m.SetColor("_Tint", new Color(1, 0.8f, 0.52f)); m.SetFloat("_Falloff", 6.5f); });
            var sparklerGrip = sparkler.gameObject.AddComponent<GrabZone>();
            sparklerGrip.centre = new Vector3(0, 0.12f, 0);
            sparklerGrip.radius = 0.1f;
            session.sparkler = sparkler;
        }

        static void BuildPreviewRoom()
        {
            var root = new GameObject("Desktop preview room • hidden on a headset");
            var floorMaterial = Mat("Preview floor", new Color(0.3f, 0.27f, 0.25f), 0, 0.3f);
            var wallMaterial = Mat("Preview wall", new Color(0.36f, 0.34f, 0.33f), 0, 0.15f);

            Shape("Room floor", PrimitiveType.Cube, root.transform, new Vector3(0, -0.05f, 1), new Vector3(7, 0.1f, 7), floorMaterial);
            Shape("Back wall", PrimitiveType.Cube, root.transform, new Vector3(0, 1.4f, 4.5f), new Vector3(7, 2.8f, 0.1f), wallMaterial);
            Shape("Left wall", PrimitiveType.Cube, root.transform, new Vector3(-3.5f, 1.4f, 1), new Vector3(0.1f, 2.8f, 7), wallMaterial);
            Shape("Right wall", PrimitiveType.Cube, root.transform, new Vector3(3.5f, 1.4f, 1), new Vector3(0.1f, 2.8f, 7), wallMaterial);
            session.placement.previewRoom = root;
        }

        // ---------- interface ----------

        /// <summary>A rounded rectangle, disc or pill with an exact corner radius.</summary>
        static Image Rounded(string name, Transform parent, Vector2 position, Vector2 size, Color colour, float radius)
        {
            var image = UIRect(name, parent, position, size, colour);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TextureRoot}/ui_round.png");
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 63f / radius;
            return image;
        }

        static TextMeshProUGUI Label(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize, Color colour, TextAlignmentOptions alignment = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = uiFont;
            text.text = value;
            text.fontSize = fontSize;
            text.color = colour;
            text.alignment = alignment;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        static Transform Miniature(Transform source, Transform tile, float fit)
        {
            var pivot = Group("Miniature", tile, new Vector3(0, 0, -0.024f));
            var copy = Object.Instantiate(source.gameObject, pivot);
            copy.name = "Model";
            FitInside(copy.transform, pivot, fit);
            return pivot;
        }

        static void FitInside(Transform model, Transform pivot, float fit)
        {
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;
            var parts = model.GetComponentsInChildren<Renderer>();
            var bounds = parts[0].bounds;
            foreach (var part in parts) bounds.Encapsulate(part.bounds);
            float scale = fit / Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            model.localScale = Vector3.one * scale;
            model.localPosition = -(bounds.center - pivot.position) * scale;
        }

        /// <summary>A real phuljhadi is too thin to read at this size, so its token is drawn thicker.</summary>
        static Transform SparklerToken(Transform tile)
        {
            var pivot = Group("Miniature", tile, new Vector3(0, 0, -0.024f));
            var model = Group("Model", pivot, Vector3.zero);
            Rod("Stem", model, new Vector3(0, -0.026f, 0), new Vector3(0, 0.028f, 0), 0.0017f, Mat("Sparkler stem", new Color(0.62f, 0.5f, 0.32f), 0.4f, 0.4f));
            Rod("Coating", model, new Vector3(0, -0.004f, 0), new Vector3(0, 0.024f, 0), 0.0042f, Wrap("sparkler", 0.2f));
            Shape("Spark", PrimitiveType.Sphere, model, new Vector3(0, 0.027f, 0), Vector3.one * 0.0052f, glow);
            return pivot;
        }

        /// <summary>One cream pouch alone reads as a garlic bulb at this size, so the tile shows a small coloured heap of pops.</summary>
        static Transform PopsToken(Transform tile)
        {
            var pivot = Group("Miniature", tile, new Vector3(0, 0, -0.024f));
            var model = Group("Model", pivot, new Vector3(0, -0.012f, 0));
            var pouch = Lathe(SphereProfile(0.0076f, 0.0076f, 10), 12);
            var twist = Lathe(new[] { new Vector2(0.0038f, 0.0113f), new Vector2(0.0013f, 0.0168f), new Vector2(0.0034f, 0.0244f), new Vector2(0, 0.0252f) }, 8);
            var tissue = new[]
            {
                Mat("Pop tissue rose", new Color(0.95f, 0.33f, 0.52f), 0, 0.1f),
                Mat("Pop tissue saffron", new Color(0.98f, 0.66f, 0.14f), 0, 0.1f),
                Mat("Pop tissue green", new Color(0.33f, 0.76f, 0.42f), 0, 0.1f),
                Mat("Pop tissue blue", new Color(0.28f, 0.58f, 0.94f), 0, 0.1f),
                Mat("Popper tissue", new Color(0.93f, 0.87f, 0.72f), 0, 0.1f),
            };

            // Five around the edge, leaning out so each twist shows, and one resting on top.
            for (int i = 0; i < 6; i++)
            {
                bool top = i == tissue.Length;
                float angle = i * Mathf.PI * 2 / tissue.Length;
                var outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var pop = Group("Pop", model, top ? new Vector3(0, 0.008f, 0) : outward * 0.0155f);
                if (!top) pop.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.up + outward * 0.45f);
                var paper = tissue[i % tissue.Length];
                Part("Pouch", pop, pouch, paper);
                Part("Twist", pop, twist, paper);
            }
            return pivot;
        }

        static Transform ShowToken(Transform tile)
        {
            var pivot = Group("Miniature", tile, new Vector3(0, 0, -0.024f));
            var model = Group("Model", pivot, Vector3.zero);
            Shape("Core", PrimitiveType.Sphere, model, Vector3.zero, Vector3.one * 0.013f, glow);
            for (int i = 0; i < 12; i++)
            {
                Vector3 direction = Quaternion.Euler(i * 137.5f, i * 57.3f, 0) * Vector3.up;
                Rod("Ray", model, direction * 0.006f, direction * 0.023f, 0.0016f, i % 3 == 0 ? rose : i % 3 == 1 ? gold : teal);
            }
            return pivot;
        }

        // The tray is two panes on one plate: the fireworks, and a narrow side panel on the left for
        // Grand Show and the settings. The pair is centred on the plate, so it docks and turns about its middle.
        const float MainWidth = 446, MainHeight = 320, SideWidth = 92, SideHeight = 304, PaneGap = 16;
        const float TrayWidth = SideWidth + PaneGap + MainWidth;
        const float MainX = (SideWidth + PaneGap) / 2, SideX = -(MainWidth + PaneGap) / 2;
        static readonly Color TrayGlass = new Color(0.045f, 0.04f, 0.06f, 0.62f);
        static readonly Color TrayRule = new Color(1, 0.8f, 0.5f, 0.22f);

        /// <summary>The main panel, from what fills the sky down to what you hold. A shorter row sits centred.</summary>
        static readonly CrackerId[][] TrayGrid =
        {
            new[] { CrackerId.Rocket, CrackerId.SkyShot, CrackerId.TwelveShot, CrackerId.FifteenShot, CrackerId.Anar, CrackerId.FlowerPot },
            new[] { CrackerId.Ladi, CrackerId.BigLadi, CrackerId.Chakri, CrackerId.Bomb, CrackerId.Butterfly },
            new[] { CrackerId.Lighter, CrackerId.Diya, CrackerId.Phuljhadi, CrackerId.Rangoli, CrackerId.Popper, CrackerId.Caps },
        };

        static Vector3 TileSlot(CrackerId id)
        {
            if (id == CrackerId.GrandShow) return new Vector3(SideX, 114, 0) / 1000;
            for (int row = 0; row < TrayGrid.Length; row++)
            {
                int column = System.Array.IndexOf(TrayGrid[row], id);
                if (column >= 0) return new Vector3(MainX + (column - (TrayGrid[row].Length - 1) / 2f) * 69, 89 - row * 78, 0) / 1000;
            }
            throw new System.ArgumentException("No place on the tray for " + id);
        }

        /// <summary>The side panel, top to bottom. Reset stands apart at the foot so it is not pressed by mistake.</summary>
        static Vector2 SettingSlot(SettingKind kind)
        {
            return new Vector2(SideX, kind == SettingKind.Music ? 46 : kind == SettingKind.Gentle ? -16 : kind == SettingKind.Night ? -78 : -131);
        }

        static void PlaceRect(Transform surface, string name, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)surface.Find(name);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Every position on the tray, so the generator and an existing scene always agree.</summary>
        static void ArrangeThali(ThaliMenu thali)
        {
            var surface = (RectTransform)thali.plate.Find("Plate surface");
            surface.sizeDelta = new Vector2(TrayWidth, MainHeight);

            if (!surface.Find("Side glass"))
            {
                int above = surface.Find("Glass").GetSiblingIndex() + 1;
                Rounded("Side glass", surface, Vector2.zero, Vector2.one, TrayGlass, 22).transform.SetSiblingIndex(above);
                UIRect("Show rule", surface, Vector2.zero, Vector2.one, TrayRule).transform.SetSiblingIndex(above + 1);
                UIRect("Reset rule", surface, Vector2.zero, Vector2.one, TrayRule).transform.SetSiblingIndex(above + 2);
            }

            PlaceRect(surface, "Glass", new Vector2(MainX, 0), new Vector2(MainWidth, MainHeight));
            PlaceRect(surface, "Hairline", new Vector2(MainX, -116), new Vector2(404, 1));
            PlaceRect(surface, "Guidance", new Vector2(MainX, -140), new Vector2(420, 40));
            for (int i = 0; i < FestivalCatalog.Journey.Length; i++)
                if (surface.Find("Step " + (i + 1))) Object.DestroyImmediate(surface.Find("Step " + (i + 1)).gameObject);
            if (surface.Find("Tool rest")) Object.DestroyImmediate(surface.Find("Tool rest").gameObject);

            PlaceRect(surface, "Side glass", new Vector2(SideX, 0), new Vector2(SideWidth, SideHeight));
            PlaceRect(surface, "Show rule", new Vector2(SideX, 76), new Vector2(60, 1));
            PlaceRect(surface, "Reset rule", new Vector2(SideX, -110), new Vector2(60, 1));
            foreach (var setting in surface.GetComponentsInChildren<ThaliSetting>(true)) ((RectTransform)setting.transform).anchoredPosition = SettingSlot(setting.kind);
            foreach (var tile in thali.tiles) tile.transform.localPosition = TileSlot(tile.id);
            thali.dockYaw = ThaliMenu.DefaultYaw;
            thali.reach = ThaliMenu.DefaultReach;
            thali.drop = ThaliMenu.DefaultDrop;

            // The bar moves the whole tray, so it sits under the pair rather than under one pane.
            PlaceRect(surface, "Move bar", new Vector2(0, -MainHeight / 2 - 16), new Vector2(124, 9));

            thali.receiveHalfSize = new Vector2(TrayWidth / 2 + 7, MainHeight / 2 + 2) / 1000;
            thali.followAngle = 72;
            EnsureShowSprite(thali);
        }

        static void EnsureShowSprite(ThaliMenu thali)
        {
            const string path = "Assets/Diwali/Textures/ui_grand_show.png";
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new FileNotFoundException("Grand Show sprite is missing", path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var tile = System.Array.Find(thali.tiles, value => value.id == CrackerId.GrandShow);
            var surface = thali.plate.Find("Plate surface");
            if (!tile.showImage)
            {
                if (tile.miniature) Object.DestroyImmediate(tile.miniature.gameObject);
                tile.miniature = Group("Sprite anchor", tile.transform, Vector3.zero);
                tile.showImage = UIRect("Grand Show sprite", surface, new Vector2(SideX, 121), new Vector2(48, 48), Color.white);
                tile.showImage.preserveAspect = true;
                tile.showImage.raycastTarget = false;
                tile.showLabel = Label("Grand Show label", surface, "Grand Show", new Vector2(SideX, 86), new Vector2(80, 18), 12, Ink);
            }
            tile.showImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            tile.showImage.rectTransform.anchoredPosition = new Vector2(SideX, 123);
            tile.showImage.rectTransform.sizeDelta = new Vector2(56, 56);
            tile.showLabel.rectTransform.anchoredPosition = new Vector2(SideX, 85);
            EditorUtility.SetDirty(tile);
        }

        /// <summary>Applies the tray layout to the saved scene and touches nothing else, unlike a full generate.</summary>
        [MenuItem("Diwali/Apply Tray Layout")]
        public static void ApplyThaliLayout()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play before applying the tray layout.");
            if (EditorSceneManager.GetActiveScene().path != MRScenePath) EditorSceneManager.OpenScene(MRScenePath);
            var found = Object.FindFirstObjectByType<DiwaliSession>();
            if (!found) throw new System.InvalidOperationException("The Diwali scene has no session.");
            mrMaterials = true;
            uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            ArrangeThali(found.thali);
            found.music.openingJingle = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioRoot}/ui_lift.wav");
            EditorUtility.SetDirty(found.music);
            found.catalog.Find(CrackerId.Lighter).description = "Take to light. Hold to carry; release to drop";
            EditorUtility.SetDirty(found.catalog);
            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(found.thali);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("DIWALI_TRAY_READY");
        }

        static ThaliMenu BuildThali(FestivalCatalog catalog)
        {
            var thali = new GameObject("Thali").AddComponent<ThaliMenu>();
            var plate = Group("Plate", thali.transform, Vector3.zero);
            thali.plate = plate;

            var canvas = CanvasAt("Plate surface", plate, Vector3.zero, new Vector2(TrayWidth, MainHeight), 0.001f);
            Rounded("Glass", canvas.transform, Vector2.zero, Vector2.one, TrayGlass, 22);
            UIRect("Hairline", canvas.transform, Vector2.zero, Vector2.one, TrayRule);

            thali.caption = Label("Guidance", canvas.transform, "", Vector2.zero, Vector2.one, 15, Ink);

            var tiles = new List<ThaliTile>();
            foreach (var row in TrayGrid)
                foreach (var id in row) tiles.Add(BuildTile(thali, plate, catalog, id));
            tiles.Add(BuildTile(thali, plate, catalog, CrackerId.GrandShow));
            thali.tiles = tiles.ToArray();

            BuildSetting(thali, canvas.transform, SettingKind.Music);
            BuildSetting(thali, canvas.transform, SettingKind.Gentle);
            BuildSetting(thali, canvas.transform, SettingKind.Night);
            BuildSetting(thali, canvas.transform, SettingKind.Reset, 26);

            // The grab bar: the one place that moves the tray, with either hand.
            var bar = Rounded("Move bar", canvas.transform, Vector2.zero, Vector2.one, new Color(1, 1, 1, 0.34f), 4.5f);
            bar.gameObject.layer = TargetLayer;
            bar.gameObject.AddComponent<BoxCollider>().size = new Vector3(230, 46, 46);
            var handle = bar.gameObject.AddComponent<ThaliHandle>();
            handle.menu = thali;
            handle.bar = bar;

            // A little of its own light, so the miniatures read in a dim room and through the dusk grade.
            var lamp = Group("Tray light", plate, new Vector3(0, 0.04f, -0.26f)).gameObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1, 0.93f, 0.82f);
            lamp.range = 0.8f;
            lamp.intensity = 0.16f;
            lamp.shadows = LightShadows.None;

            ArrangeThali(thali);
            return thali;
        }

        static ThaliTile BuildTile(ThaliMenu thali, Transform plate, FestivalCatalog catalog, CrackerId id)
        {
            var go = new GameObject(id.ToString());
            go.transform.SetParent(plate, false);
            go.transform.localPosition = TileSlot(id);
            go.layer = TargetLayer;

            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.064f, 0.064f, 0.07f);
            collider.center = new Vector3(0, 0, -0.024f);

            var tile = go.AddComponent<ThaliTile>();
            tile.id = id;
            tile.menu = thali;

            if (id == CrackerId.GrandShow) tile.miniature = Group("Sprite anchor", go.transform, Vector3.zero);
            else if (id == CrackerId.Phuljhadi) tile.miniature = SparklerToken(go.transform);
            else if (id == CrackerId.Popper) tile.miniature = PopsToken(go.transform);
            else if (id == CrackerId.Lighter) tile.miniature = Miniature(session.lighter.transform.Find("Model"), go.transform, 0.05f);
            else if (id == CrackerId.Rocket) tile.miniature = Miniature(catalog.Find(id).prefab.transform.Find("Model/Rocket body"), go.transform, 0.066f);
            else tile.miniature = Miniature(catalog.Find(id).prefab.transform.Find("Model"), go.transform, 0.05f);

            return tile;
        }

        static void BuildSetting(ThaliMenu thali, Transform canvas, SettingKind kind, float size = 46)
        {
            var disc = Rounded(kind.ToString(), canvas, SettingSlot(kind), new Vector2(size, size), new Color(1, 1, 1, 0.06f), size / 2);
            disc.gameObject.layer = TargetLayer;
            var collider = disc.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(size + 16, size + 16, 44);

            var setting = disc.gameObject.AddComponent<ThaliSetting>();
            setting.kind = kind;
            setting.menu = thali;
            setting.disc = disc;

            string glyph = kind == SettingKind.Night ? "ui_sun" : kind == SettingKind.Gentle ? "ui_gentle" : kind == SettingKind.Music ? "ui_music" : "ui_reset";
            var icon = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(disc.transform, false);
            icon.GetComponent<RectTransform>().sizeDelta = new Vector2(size * 0.56f, size * 0.56f);
            var image = icon.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TextureRoot}/{glyph}.png");
            image.raycastTarget = false;
            setting.icon = image;
            if (kind == SettingKind.Night)
            {
                setting.sunIcon = image.sprite;
                setting.nightIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{TextureRoot}/ui_evening.png");
            }
        }

        static FestivalHint BuildHint()
        {
            var root = new GameObject("Object hint");
            var hint = root.AddComponent<FestivalHint>();
            var canvas = CanvasAt("Hint", root.transform, Vector3.zero, new Vector2(460, 90), 0.00062f);
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(460, 90);
            hint.label = Label("Text", canvas.transform, "", Vector2.zero, new Vector2(460, 90), 34, Ink);
            hint.label.fontStyle = FontStyles.Bold;
            return hint;
        }

        public static void ConfigureMR()
        {
            var config = AssetDatabase.LoadAssetAtPath<Object>("Assets/Oculus/OculusProjectConfig.asset");
            var serialized = new SerializedObject(config);
            foreach (var name in new[] { "anchorSupport", "sceneSupport", "_insightPassthroughSupport" })
            {
                var property = serialized.FindProperty(name);
                if (property != null) property.intValue = 1;
            }
            var enabled = serialized.FindProperty("insightPassthroughEnabled");
            if (enabled != null) enabled.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/XR/Settings/OpenXRPackageSettings.asset"))
            {
                if (!asset || asset.name != "MetaXRFeature Android") continue;
                var feature = new SerializedObject(asset);
                feature.FindProperty("m_enabled").boolValue = true;
                feature.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }

            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            var pipeline = new SerializedObject(pipelineAsset);
            pipeline.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            pipeline.ApplyModifiedPropertiesWithoutUndo();
            pipelineAsset.maxAdditionalLightsCount = 4;
            pipelineAsset.supportsHDR = true;
            EditorUtility.SetDirty(pipelineAsset);
            if (session && session.rig) session.rig.view.allowHDR = false;
        }
    }
}
