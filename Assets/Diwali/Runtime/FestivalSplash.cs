using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Diwali
{
    /// <summary>The launch-only entrance, owned by the SplashScreen scene.</summary>
    public sealed class FestivalSplash : MonoBehaviour
    {
        public TMP_Text eyebrow, title, description, actionLabel, instruction;
        public FestivalSplashArt art;
        public CanvasGroup content;
        public FestivalRig rig;
        public FestivalMusic music;
        public Light diyaLight;
        public Shader uiShader, textShader;
        public bool Transitioning { get; private set; }
        readonly List<Material> presentationMaterials = new List<Material>();

        void Awake()
        {
            // The launch card draws in canvas order, independently of room/scene depth.
            if (rig && rig.view) rig.view.useOcclusionCulling = false;
            var canvas = content.GetComponent<Canvas>();
            if (canvas) { canvas.overrideSorting = true; canvas.sortingOrder = 100; }
            if (!uiShader) uiShader = Shader.Find("Diwali/Splash UI");
            if (!textShader) textShader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
            var ui = uiShader ? new Material(uiShader) : null;
            if (ui) presentationMaterials.Add(ui);
            foreach (var graphic in content.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is TMP_Text text && textShader)
                {
                    var material = new Material(text.fontSharedMaterial) { shader = textShader };
                    presentationMaterials.Add(material);
                    text.fontSharedMaterial = material;
                }
                else if (ui) graphic.material = ui;
            }
        }

        void OnDestroy()
        {
            foreach (var material in presentationMaterials) if (material) Destroy(material);
        }

        void Start()
        {
            ShowHome();
            if (music) music.OpenSplash(true);

            // Reports from here too, so a Link session shows whether the frame rate fell here or on entry.
            gameObject.AddComponent<FestivalPerf>();
        }

        void OnEnable()
        {
            if (content) content.alpha = 1;
            if (art) art.ignition = 1;
            if (diyaLight) diyaLight.enabled = true;
            Transitioning = false;
        }

        public void ShowHome()
        {
            StopAllCoroutines();
            Transitioning = false;
            gameObject.SetActive(true);
            content.alpha = 1;
            art.ignition = 1;
            if (diyaLight) diyaLight.enabled = true;
            eyebrow.text = "THE FESTIVAL OF LIGHT";
            title.text = "Diwali\nUtsav";
            description.text = FestivalIdentity.Tagline + "\nYour room is the stage.";
            actionLabel.text = "Begin celebration";
            instruction.text = "Point and pinch, press trigger, or click to begin.\nAll fire and fireworks are virtual.";
        }

        public void Continue(FestivalPointer pointer)
        {
            if (Transitioning || (pointer != null && pointer.Grasp != null)) return;
            StartCoroutine(Enter());
        }

        IEnumerator Enter()
        {
            Transitioning = true;
            if (diyaLight) diyaLight.enabled = true;
            // The diya stays lit; the score never imposes a two-minute wait.
            if (music) music.PlaySignature();
            float time = 0;
            while (time < 0.55f)
            {
                time += Time.unscaledDeltaTime;
                art.ignition = 1;
                content.alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.24f, 0.55f, time));
                yield return null;
            }
            // Single mode removes the launch scene, including its camera and input targets.
            FestivalPerf.Note("entering experience");
            yield return SceneManager.LoadSceneAsync(DiwaliSession.ExperienceSceneName, LoadSceneMode.Single);
        }
    }
}
