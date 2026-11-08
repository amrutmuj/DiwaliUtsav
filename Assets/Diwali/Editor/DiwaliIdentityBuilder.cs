using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Diwali.Editor
{
    public static partial class DiwaliSceneBuilder
    {
        const string IdentityRoot = "Assets/Diwali/Identity";

        [MenuItem("Diwali/Apply Diwali Utsav Identity")]
        public static void InstallFestivalIdentity()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play before applying the identity.");
            if (!File.Exists(MRScenePath) && File.Exists(LegacyScenePath))
            {
                var current = EditorSceneManager.GetActiveScene();
                if (current.path == LegacyScenePath && current.isDirty) EditorSceneManager.SaveScene(current);
                string error = AssetDatabase.MoveAsset(LegacyScenePath, MRScenePath);
                if (!string.IsNullOrEmpty(error)) throw new System.IO.IOException(error);
            }
            if (EditorSceneManager.GetActiveScene().path != MRScenePath) EditorSceneManager.OpenScene(MRScenePath);
            session = Object.FindFirstObjectByType<DiwaliSession>();
            if (!session) throw new System.InvalidOperationException("The Diwali scene has no session.");
            uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            mrMaterials = true;
            ImportIdentityMedia();
            // Remove the old embedded entrance without regenerating the user's activities.
            foreach (var card in Object.FindObjectsByType<WelcomeCard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(card.gameObject);
            session.music = BuildFestivalMusic(session.transform);
            session.music.launchScreen = false;
            // Add the quiet music control to the side panel, preserving the tray.
            Transform surface = session.thali.plate.Find("Plate surface");
            if (surface && !surface.Find("Music")) BuildMusicSetting(session.thali, surface);
            ConfigureFestivalIdentity();
            EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            BuildSplashScene();
            ConfigureSceneFlow();
            AssetDatabase.SaveAssets();
            Debug.Log("DIWALI_UTSAV_IDENTITY_READY");
        }

        public static void ConfigureSceneFlow()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SplashScenePath, true),
                new EditorBuildSettingsScene(MRScenePath, true)
            };
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SplashScenePath);
        }

        static void BuildSplashScene()
        {
            var experience = session.gameObject.scene;
            var launch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(launch);
            var rig = Object.Instantiate(session.rig.gameObject).GetComponent<FestivalRig>();
            rig.name = "Visitor • launch input";
            var root = BuildUtsavWelcome(rig);
            var splash = root.GetComponent<FestivalSplash>();
            splash.rig = rig;
            rig.splash = splash;
            var music = new GameObject("Launch music").AddComponent<FestivalMusic>();
            EditorUtility.CopySerialized(session.music, music);
            music.launchScreen = true;
            splash.music = music;
            splash.uiShader = Shader.Find("Diwali/Splash UI");
            splash.textShader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
            rig.view.useOcclusionCulling = false;
            var lamp = new GameObject("Diya ignition light").AddComponent<Light>();
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(-.223f, .04f, -.1f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1, .59f, .19f);
            lamp.range = 1.4f;
            lamp.intensity = .8f;
            lamp.shadows = LightShadows.None;
            lamp.enabled = true;
            splash.diyaLight = lamp;
            EditorSceneManager.SaveScene(launch, SplashScenePath);
            EditorSceneManager.CloseScene(experience, true);
        }

        [MenuItem("Diwali/Update Splash Presentation")]
        public static void UpdateSplashPresentation()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play before updating the splash.");
            var launch = EditorSceneManager.OpenScene(SplashScenePath);
            var splash = Object.FindFirstObjectByType<FestivalSplash>();
            splash.uiShader = Shader.Find("Diwali/Splash UI");
            splash.textShader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
            if (!splash.uiShader || !splash.textShader) throw new System.InvalidOperationException("Splash shaders are missing.");
            splash.art.ignition = 1;
            splash.diyaLight.enabled = true;
            splash.rig.view.useOcclusionCulling = false;
            splash.actionLabel.text = "Begin celebration";
            splash.music.openingJingle = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioRoot}/ui_lift.wav");
            EditorUtility.SetDirty(splash.music);
            var canvas = splash.content.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            EditorSceneManager.MarkSceneDirty(launch);
            EditorSceneManager.SaveScene(launch);
        }

        static void ImportIdentityMedia()
        {
            var glyph=AssetImporter.GetAtPath($"{TextureRoot}/ui_music.png") as TextureImporter;
            if(glyph && glyph.textureType!=TextureImporterType.Sprite)
            {
                glyph.textureType=TextureImporterType.Sprite;
                glyph.mipmapEnabled=false;
                glyph.alphaIsTransparency=true;
                glyph.SaveAndReimport();
            }
            foreach(string path in Directory.GetFiles(IdentityRoot,"*.png"))
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\','/')) as TextureImporter;
                if (!importer) continue;
                importer.textureType = TextureImporterType.Default;
                importer.maxTextureSize = 1024;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = path.Contains("Foreground");
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            foreach (string name in new[] { "utsav_music_120", "utsav_jingle" })
            {
                var importer=AssetImporter.GetAtPath($"{AudioRoot}/{name}.wav") as AudioImporter;
                if (!importer) throw new FileNotFoundException("Run Tools/make_diwali_identity.py and refresh assets first.");
                bool music=name=="utsav_music_120";
                importer.forceToMono=false;
                var settings=importer.defaultSampleSettings;
                settings.loadType=music?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat=music?AudioCompressionFormat.Vorbis:AudioCompressionFormat.PCM;
                settings.quality=.75f;
                settings.preloadAudioData=!music;
                importer.defaultSampleSettings=settings;
                FestivalRecordedAudio.PreserveLevels(importer);
                importer.SaveAndReimport();
            }
        }

        public static void ConfigureFestivalIdentity()
        {
            PlayerSettings.productName=FestivalIdentity.AppName;
            var icon=AssetDatabase.LoadAssetAtPath<Texture2D>($"{IdentityRoot}/DiwaliUtsav-Icon.png");
            var background=AssetDatabase.LoadAssetAtPath<Texture2D>($"{IdentityRoot}/DiwaliUtsav-Background.png");
            var foreground=AssetDatabase.LoadAssetAtPath<Texture2D>($"{IdentityRoot}/DiwaliUtsav-Foreground.png");
            if (!icon) return;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown,new[]{icon},IconKind.Any);
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var slots=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,kind);
                foreach (var slot in slots)
                {
                    if (slot.minLayerCount > 1) slot.SetTextures(new[]{background,foreground});
                    else slot.SetTextures(new[]{icon});
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,kind,slots);
            }
        }

        static FestivalMusic BuildFestivalMusic(Transform parent)
        {
            ImportIdentityMedia();
            var music=parent.GetComponent<FestivalMusic>();
            if (!music) music=parent.gameObject.AddComponent<FestivalMusic>();
            music.backgroundTrack=AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioRoot}/utsav_music_120.wav");
            music.openingJingle=AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioRoot}/ui_lift.wav");
            return music;
        }

        static GameObject BuildUtsavWelcome(FestivalRig rig)
        {
            var root=new GameObject(FestivalIdentity.AppName+" • animated splash");
            var follow=root.AddComponent<WelcomeCard>();
            follow.distance=1.25f;
            follow.rig=rig;
            var splash=root.AddComponent<FestivalSplash>();
            var canvas=CanvasAt("Festival entrance",root.transform,Vector3.zero,new Vector2(880,540),.001f,rig.view);
            splash.content=canvas.gameObject.AddComponent<CanvasGroup>();
            Rounded("Gold edge",canvas.transform,Vector2.zero,new Vector2(884,544),new Color(.53f,.34f,.22f,.85f),32);
            Rounded("Midnight plum",canvas.transform,Vector2.zero,new Vector2(880,540),new Color(.07f,.035f,.115f,.97f),30);
            Label("Brand",canvas.transform,FestivalIdentity.AppName.ToUpperInvariant(),new Vector2(-246,228),new Vector2(320,30),17,Brass,TextAlignmentOptions.Left,FontStyles.Bold).characterSpacing=4;
            Label("Occasion",canvas.transform,"SHUBH DEEPAVALI",new Vector2(248,228),new Vector2(310,28),13,Muted,TextAlignmentOptions.Right).characterSpacing=3;
            UIRect("Top hairline",canvas.transform,new Vector2(0,202),new Vector2(806,1),new Color(1,.76f,.36f,.20f));

            var hero=new GameObject("Living diya",typeof(RectTransform),typeof(CanvasRenderer),typeof(FestivalSplashArt));
            hero.transform.SetParent(canvas.transform,false);
            var rect=hero.GetComponent<RectTransform>();rect.anchoredPosition=new Vector2(-223,39);rect.sizeDelta=new Vector2(334,334);
            splash.art=hero.GetComponent<FestivalSplashArt>();splash.art.raycastTarget=false;
            splash.art.view=rig.view;
            hero.layer=TargetLayer;
            hero.AddComponent<BoxCollider>().size=new Vector3(300,310,18);
            var heroAction=hero.AddComponent<FestivalSplashButton>();heroAction.splash=splash;
            splash.eyebrow=Label("Experience family",canvas.transform,"",new Vector2(174,158),new Vector2(382,36),12,Brass,TextAlignmentOptions.Left,FontStyles.Bold);
            splash.eyebrow.characterSpacing=2;
            splash.title=Label("Experience name",canvas.transform,"Diwali\nUtsav",new Vector2(174,69),new Vector2(382,155),58,Ink,TextAlignmentOptions.Left,FontStyles.Bold);
            splash.title.enableAutoSizing=true;splash.title.fontSizeMin=32;splash.title.fontSizeMax=58;
            splash.description=Label("Invitation",canvas.transform,FestivalIdentity.Tagline,new Vector2(174,-59),new Vector2(382,91),21,Muted,TextAlignmentOptions.Left);
            splash.description.enableAutoSizing=true;splash.description.fontSizeMin=17;splash.description.fontSizeMax=21;

            var enter=SplashEnterButton(splash,canvas.transform,"Begin celebration",new Vector2(120,-161),new Vector2(486,62));
            splash.actionLabel=enter.GetComponentInChildren<TMP_Text>();
            splash.instruction=Label("How to begin",canvas.transform,"",new Vector2(0,-220),new Vector2(814,53),14,Muted);
            return root;
        }

        static GameObject SplashEnterButton(FestivalSplash splash,Transform parent,string title,Vector2 position,Vector2 size)
        {
            var face=Rounded("Enter",parent,position,size,Brass,20);
            face.gameObject.layer=TargetLayer;
            face.gameObject.AddComponent<BoxCollider>().size=new Vector3(size.x,size.y,24);
            var button=face.gameObject.AddComponent<FestivalSplashButton>();
            button.splash=splash;button.background=face;button.normal=Brass;
            button.hover=new Color(1,.88f,.62f);
            var text=Label("Label",face.transform,title,Vector2.zero,size-new Vector2(20,4),23,new Color(.13f,.07f,.08f),TextAlignmentOptions.Center,FontStyles.Bold);
            text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=23;
            return face.gameObject;
        }

        static void BuildMusicSetting(ThaliMenu thali,Transform canvas)
        {
            BuildSetting(thali,canvas,SettingKind.Music);
        }
    }
}
