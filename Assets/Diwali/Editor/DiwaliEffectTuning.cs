using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Diwali.Editor
{
    public static partial class DiwaliSceneBuilder
    {
        const string PrefabFolder = "Assets/Diwali/Prefabs/";

        // Motor acceleration that puts the burst at 18 metres, still climbing, with the same 0.8-second burn.
        const float RocketThrust = 32.4f;
        const float RocketBurstRadius = 4.5f;
        const float RocketGlowSize = 0.45f;

        /// <summary>Out past the stand, towards the visitor, so the fuse can be seen and touched.</summary>
        static readonly Vector3 RocketFuseReach = new Vector3(0.03f, -0.035f, 0.11f);

        /// <summary>Boxes break 20 to 22 metres above the floor, counted here from the tube tops. The single Sky Shot stays higher and opens wider.</summary>
        static void TuneMortar(MortarItem item, bool single)
        {
            item.burstHeight = single ? new Vector2(23, 26) : new Vector2(19.85f, 21.85f);
            item.burstRadius = single ? 6.5f : 5.5f;
        }

        /// <summary>
        /// Puts the current smoke and ember tuning into the saved prefabs and scene, and touches nothing else.
        /// Use this rather than <c>generate</c> for effect changes: <c>generate</c> rebuilds every prefab and both
        /// scenes from code, and the released assets have moved on from that code.
        /// </summary>
        public static void ApplyEffectTuning()
        {
            using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabFolder + "Bomb.prefab"))
            {
                var blast = scope.prefabContentsRoot.GetComponent<BlastItem>();
                blast.cloudPuffs = 130;
                TuneBlastPlume(blast.clouds);
            }

            foreach (string ladi in new[] { "Small Ladi", "Big Ladi" })
            {
                using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabFolder + ladi + ".prefab"))
                {
                    var chain = scope.prefabContentsRoot.GetComponent<ChainItem>();
                    float size = LadiPuffSize * chain.popSize;
                    var main = chain.smoke.main;
                    main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
                    LimitOnScreen(chain.smoke);
                }
            }

            foreach (string box in new[] { "12 Shot", "15 Shot", "Sky Shot" })
            {
                using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabFolder + box + ".prefab"))
                {
                    var mortar = scope.prefabContentsRoot.GetComponent<MortarItem>();
                    LimitOnScreen(mortar.smoke);
                    TuneMortar(mortar, box == "Sky Shot");
                }
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabFolder + "Rocket.prefab"))
            {
                var rocket = scope.prefabContentsRoot.GetComponent<RocketItem>();
                rocket.thrust = RocketThrust;
                rocket.burstRadius = RocketBurstRadius;
                rocket.burningTail.transform.localScale = Vector3.one * RocketGlowSize;
                ThinTrail(rocket.trail);
                Vector3 tip = rocket.fuseRoot + RocketFuseReach;
                rocket.fuse.localPosition = tip;
                rocket.fuseCord.localPosition = (rocket.fuseRoot + tip) * 0.5f;
                rocket.fuseCord.localRotation = Quaternion.FromToRotation(Vector3.up, RocketFuseReach);
                rocket.fuseCord.localScale = new Vector3(0.0022f, RocketFuseReach.magnitude * 0.5f, 0.0022f);
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabFolder + "Comet.prefab"))
                ThinTrail(scope.prefabContentsRoot.GetComponent<ParticleSystem>());

            if (EditorSceneManager.GetActiveScene().path != MRScenePath) EditorSceneManager.OpenScene(MRScenePath);
            ApplySunAndNight();
            var sky = Object.FindFirstObjectByType<SkyShow>();
            sky.emberPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(PrefabFolder + "Embers.prefab");
            if (!sky.emberPrefab) throw new System.InvalidOperationException("Embers prefab is missing.");
            EditorUtility.SetDirty(sky);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[Diwali] Effect tuning applied to the saved prefabs and scene.");
        }

        /// <summary>Takes out the sky dome and its shader, and makes the moon control a sun and night toggle.</summary>
        static void ApplySunAndNight()
        {
            var dome = Object.FindFirstObjectByType<FestivalAmbience>().transform.Find("Night sky");
            if (dome) Object.DestroyImmediate(dome.gameObject);
            foreach (string asset in new[] { "Assets/Diwali/GeneratedMR/Night_sky.mat", "Assets/Diwali/Shaders/FestivalNightSky.shader" })
                if (AssetDatabase.LoadMainAssetAtPath(asset)) AssetDatabase.DeleteAsset(asset);

            string sunPath = TextureRoot + "/ui_sun.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(sunPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            foreach (var setting in Object.FindObjectsByType<ThaliSetting>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (setting.kind != SettingKind.Night) continue;
                setting.name = "Sun and night";
                setting.sunIcon = AssetDatabase.LoadAssetAtPath<Sprite>(sunPath);
                setting.nightIcon = AssetDatabase.LoadAssetAtPath<Sprite>(TextureRoot + "/ui_evening.png");
                if (!setting.sunIcon || !setting.nightIcon) throw new System.InvalidOperationException("Sun or night glyph is missing.");
                setting.icon.sprite = setting.sunIcon;
                EditorUtility.SetDirty(setting);
                EditorUtility.SetDirty(setting.icon);
            }
        }
    }
}
