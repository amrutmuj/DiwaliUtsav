using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using UnityEngine.SpatialTracking;

namespace Diwali.Editor
{
    public static partial class DiwaliSceneBuilder
    {
        static string Root="Assets/Diwali/GeneratedMR";
        static bool mrMaterials=true;
        static Dictionary<string,Material> materials;
        static Font font;
        static Material gold,clay,teal,indigo,ivory,glow,rose;
        static DiwaliSession session;
        static int meshId;

        static Material Asset(Material material,string name)
        {
            string path=Root+"/"+Safe(name)+".mat"; var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(existing) {material.name=name;EditorUtility.CopySerialized(material,existing);EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(material);return existing;}
            material.name=name; AssetDatabase.CreateAsset(material,path);return material;
        }
        static string Safe(string text) {return string.Concat(text.Select(c=>char.IsLetterOrDigit(c)||c=='-'?c:'_'));}
        static Material Mat(string name,Color colour,float metallic=0,float smooth=.25f,float emission=0)
        {
            if(materials.TryGetValue(name,out var material))return material;
            material=new Material(Shader.Find(mrMaterials?"Diwali/MR Lit":"Universal Render Pipeline/Lit")); material.SetColor("_BaseColor",colour); material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smooth);material.enableInstancing=true;
            if(emission>0) {material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",colour*emission);}
            materials[name]=Asset(material,name);return materials[name];
        }
        static Transform Group(string name,Transform parent,Vector3 position)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        static GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 p,Vector3 scale,Material mat)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;
            g.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        static GameObject Art(string name,Transform parent,Vector3 position,float rotation=0,float desiredWidth=0)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Diwali/Art/{name}/{name}.fbx");
            if(!prefab)throw new FileNotFoundException("Missing imported licensed model: "+name);
            var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab); PrefabUtility.UnpackPrefabInstance(g,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localRotation=Quaternion.Euler(0,rotation,0)*g.transform.localRotation;
            foreach(var renderer in g.GetComponentsInChildren<MeshRenderer>()) {
                var converted=new List<Material>();
                foreach(var source in renderer.sharedMaterials) {
                    string key="Source-"+source.name;
                    if(!materials.TryGetValue(key,out var m)) {
                        var colour=source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.HasProperty("_Color")?source.GetColor("_Color"):Color.white;
                        m=Mat(key,colour,source.name.Contains("brass")?.6f:0,.32f);
                        var texture=source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):source.HasProperty("_MainTex")?source.GetTexture("_MainTex"):null;
                        if(texture)m.SetTexture("_BaseMap",texture);
                    }
                    converted.Add(m);
                }
                renderer.sharedMaterials=converted.ToArray();
                if(name=="Diya" && renderer.name.ToLowerInvariant().Contains("flame"))renderer.gameObject.SetActive(false);
            }
            if(desiredWidth>0) {
                var renderers=g.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                float factor=desiredWidth/bounds.size.x;g.transform.localScale*=factor;
                var bottom=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)-g.transform.position;
                foreach(Transform child in g.transform)child.localPosition-=g.transform.InverseTransformVector(bottom*factor);
            }
            return g;
        }
        static void Rod(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material mat)
        {var g=Shape(name,PrimitiveType.Cylinder,parent,(a+b)*.5f,new Vector3(radius,(b-a).magnitude*.5f,radius),mat);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        static void Ring(Transform parent,Vector3 p,float inner,float outer,Material mat,int count=96)
        {
            var vertices=new Vector3[count*2];var triangles=new int[count*6];
            for(int i=0;i<count;i++) {float a=i*Mathf.PI*2/count;vertices[i*2]=new Vector3(Mathf.Cos(a)*inner,0,Mathf.Sin(a)*inner);vertices[i*2+1]=new Vector3(Mathf.Cos(a)*outer,0,Mathf.Sin(a)*outer);int n=(i+1)%count;int k=i*6;triangles[k]=i*2;triangles[k+1]=n*2+1;triangles[k+2]=i*2+1;triangles[k+3]=i*2;triangles[k+4]=n*2;triangles[k+5]=n*2+1;}
            var mesh=new Mesh{name="Rangoli ring"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh=SaveMesh(mesh);
            var g=Group("Rangoli • coloured powder",parent,p).gameObject;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;
        }
        static void BuildRig()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
            var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab);root.name="Visitor • Meta Quest hands and controllers";
            var rig=root.AddComponent<FestivalRig>();session.rig=rig;rig.metaRig=root.GetComponent<OVRCameraRig>();
            rig.metaRig.disableEyeAnchorCameras=true;
            foreach(var c in root.GetComponentsInChildren<Camera>()) {c.enabled=false;c.tag="Untagged";}
            foreach(var listener in root.GetComponentsInChildren<AudioListener>())listener.enabled=false;
            rig.view=Group("Festival view",rig.metaRig.trackingSpace,new Vector3(0,1.65f,0)).gameObject.AddComponent<Camera>();
            var pose=rig.view.gameObject.AddComponent<TrackedPoseDriver>();pose.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice,TrackedPoseDriver.TrackedPose.Center);pose.enabled=false;
            var manager=root.GetComponent<OVRManager>();manager.trackingOriginType=OVRManager.TrackingOrigin.FloorLevel;
            rig.metaRig.enabled=false;manager.enabled=false;
            rig.view.tag="MainCamera";rig.view.nearClipPlane=.06f;rig.view.farClipPlane=100;rig.view.fieldOfView=70;rig.view.backgroundColor=new Color(.015f,.015f,.05f);
            rig.view.transform.localPosition=new Vector3(0,1.65f,0);rig.view.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            if(!rig.view.GetComponent<AudioListener>())rig.view.gameObject.AddComponent<AudioListener>();
            rig.LeftHold=Group("Left hand • lighter socket",rig.metaRig.trackingSpace,new Vector3(-.3f,1.2f,.5f));
            rig.RightHold=Group("Right hand • sparkler socket",rig.metaRig.trackingSpace,new Vector3(.3f,1.2f,.5f));
            rig.rayMaterial=Asset(new Material(Shader.Find("Diwali/Pointer Ray")),"Interaction ray");
            var fc=new GameObject("Comfort fade",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));fc.transform.SetParent(rig.view.transform,false);fc.transform.localPosition=new Vector3(0,0,.1f);fc.transform.localScale=Vector3.one*.001f;
            var canvas=fc.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=rig.view;
            var rect=fc.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(1000,1000);var image=UIRect("Fade",fc.transform,Vector2.zero,new Vector2(1000,1000),Color.black);image.raycastTarget=false;
            rig.fade=fc.GetComponent<CanvasGroup>();rig.fade.alpha=0;rig.fade.blocksRaycasts=false;
        }
        static Canvas CanvasAt(string name,Transform parent,Vector3 p,Vector2 size,float scale,Camera view=null)
        {
            var g=new GameObject(name,typeof(RectTransform),typeof(Canvas));g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=Vector3.one*scale;
            var canvas=g.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=view?view:session?session.rig.view:null;
            g.GetComponent<RectTransform>().sizeDelta=size;return canvas;
        }
        static Image UIRect(string name,Transform parent,Vector2 position,Vector2 size,Color colour)
        {
            var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);var r=g.GetComponent<RectTransform>();r.anchoredPosition=position;r.sizeDelta=size;
            var image=g.GetComponent<Image>();image.color=colour;image.raycastTarget=false;
            if(mrMaterials){image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Diwali/Art/Original/RoundedPanel.png");image.type=Image.Type.Sliced;}
            return image;
        }
        static Text Text(string name,Transform parent,string value,Vector2 p,Vector2 size,int fontSize,Color colour,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var g=new GameObject(name,typeof(RectTransform),typeof(Text));g.transform.SetParent(parent,false);var r=g.GetComponent<RectTransform>();r.anchoredPosition=p;r.sizeDelta=size;
            var t=g.GetComponent<Text>();t.font=font;t.text=value;t.fontSize=fontSize;t.color=colour;t.alignment=alignment;t.raycastTarget=false;t.supportRichText=true;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
        }
        static Mesh SaveMesh(Mesh mesh)
        {
            string path=Root+"/Mesh-"+(meshId++)+".asset";mesh.name=Path.GetFileNameWithoutExtension(path);var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing) {EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        public static void ConfigureQuest()
        {
            // Company, product, package id and icons belong to the release, so nothing here may set them.
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
            PlayerSettings.stereoRenderingPath=StereoRenderingPath.Instancing;PlayerSettings.runInBackground=true;
            var rp=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if(rp) {rp.renderScale=1;rp.msaaSampleCount=4;rp.shadowDistance=24;rp.supportsHDR=false;rp.supportsCameraDepthTexture=false;rp.supportsCameraOpaqueTexture=false;GraphicsSettings.defaultRenderPipeline=rp;QualitySettings.renderPipeline=rp;EditorUtility.SetDirty(rp);}
            foreach(var obj in AssetDatabase.LoadAllAssetsAtPath("Assets/XR/Settings/OpenXRPackageSettings.asset")) {
                if(!obj || !obj.name.EndsWith(" Android"))continue;
                if(obj.name.StartsWith("HandTracking ")||obj.name.StartsWith("MetaHandTrackingAim ")||obj.name.StartsWith("OculusTouchControllerProfile ")||obj.name.StartsWith("MetaQuestTouchPlusControllerProfile ")||obj.name.StartsWith("MetaQuestFeature ")) {
                    var so=new SerializedObject(obj);var enabled=so.FindProperty("m_enabled");if(enabled!=null){enabled.boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(obj);}
                }
            }
            var config=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Oculus/OculusProjectConfig.asset");
            if(config) {var so=new SerializedObject(config);so.FindProperty("handTrackingSupport").enumValueIndex=1;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(config);}
        }
    }
}
