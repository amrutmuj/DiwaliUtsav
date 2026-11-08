using System.Collections.Generic;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Diwali.Editor
{
    /// <summary>
    /// Keeps Play-over-Quest-Link working without anyone having to tick a box.
    ///
    /// The Editor always starts XR from the *Standalone* XR profile, even while the active build
    /// target is Android, so that profile has to stay correct for Link. This runs on every domain
    /// reload and again just before Play, and only touches values that are wrong.
    /// </summary>
    [InitializeOnLoad]
    public static class DiwaliLinkSetup
    {
        const string DesktopRunKey = "Diwali.DesktopValidationRun";
        const string StartSceneKey = "Diwali.ValidationStartScene";

        /// <summary>OpenXR features Link needs. The APK's Android profile is configured separately.</summary>
        static readonly string[] RequiredStandaloneFeatures =
        {
            "MetaXRFeature Standalone",
            "OculusTouchControllerProfile Standalone",
            "MetaQuestTouchPlusControllerProfile Standalone",
            "HandTracking Standalone",
            "MetaHandTrackingAim Standalone",
        };

        static DiwaliLinkSetup()
        {
            EditorApplication.delayCall += () => Apply();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode && !SessionState.GetBool(DesktopRunKey, false))
                Apply();
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(DesktopRunKey, false))
            {
                SessionState.SetBool(DesktopRunKey, false);
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(StartSceneKey, ""));
                Apply();
            }
        }

        /// <summary>
        /// Automated validation runs without a headset for one Play session. The change is held in
        /// memory only and is never saved, so an interrupted run cannot break the next Link session.
        /// </summary>
        public static void UseDesktopForNextPlay()
        {
            SessionState.SetString(StartSceneKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            // Automated reviews explicitly open the scene they intend to exercise.
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(DesktopRunKey, true);
            var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (xr) xr.InitManagerOnStart = false;
        }

        public static List<string> Apply()
        {
            var changes = new List<string>();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return changes;

            var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (xr && !xr.InitManagerOnStart)
            {
                xr.InitManagerOnStart = true;
                EditorUtility.SetDirty(xr);
                changes.Add("Turned on 'Initialize XR on Startup' for the Standalone profile");
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/XR/Settings/OpenXRPackageSettings.asset"))
            {
                if (!asset || System.Array.IndexOf(RequiredStandaloneFeatures, asset.name) < 0) continue;
                var feature = new SerializedObject(asset);
                var enabled = feature.FindProperty("m_enabled");
                if (enabled == null || enabled.boolValue) continue;
                enabled.boolValue = true;
                feature.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                changes.Add("Enabled OpenXR feature " + asset.name);
            }

            if (changes.Count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Diwali] Quest Link setup: " + string.Join("; ", changes));
            }
            return changes;
        }
    }
}
