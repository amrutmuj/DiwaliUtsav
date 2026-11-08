using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Diwali
{
    /// <summary>One miniature on the thali. It rises to meet your pointer and gives the real thing when pinched.</summary>
    public sealed class ThaliTile : MonoBehaviour, IFestivalTarget
    {
        public CrackerId id;
        public ThaliMenu menu;
        public Transform miniature;
        public float idleSpin = 12;
        public Image showImage;
        public TMP_Text showLabel;
        public bool ActionAvailable => id != CrackerId.GrandShow || !DiwaliSession.Instance.sky.ShowRunning;

        CrackerEntry Entry => DiwaliSession.Instance.catalog.Find(id);

        EdgeHalo halo;
        Vector3 restPosition, restScale;
        float lift, spin;
        float pressedUntil;
        Vector3 showRestPosition;
        bool hovering, suggested;

        void Awake()
        {
            if (id != CrackerId.GrandShow) halo = EdgeHalo.On(miniature);
            restPosition = miniature.localPosition;
            restScale = miniature.localScale;
            spin = Random.value * 360;
            if (showImage) showRestPosition = showImage.rectTransform.localPosition;
        }

        public void SetSuggested(bool value) => suggested = value;

        public void Hover(bool on)
        {
            hovering = on;
            if (on)
            {
                menu.SetHover(this, ActionAvailable ? Entry.title + "  ·  " + Entry.description : "Grand Show is playing. Available again when it finishes.");
            }
            else
            {
                menu.ClearHover(this);
            }
        }

        public void Press(FestivalPointer pointer)
        {
            if (!ActionAvailable) return;
            if (id == CrackerId.GrandShow) pressedUntil = Time.unscaledTime + .12f;
            DiwaliSession.Instance.Take(id, pointer);
        }

        void Update()
        {
            if (id == CrackerId.GrandShow && showImage)
            {
                showImage.color = new Color(1, 1, 1, ActionAvailable ? hovering ? 1 : .9f : .35f);
                showLabel.text = ActionAvailable ? "Grand Show" : "Playing…";
                float size = Time.unscaledTime < pressedUntil ? .94f : ActionAvailable && hovering ? 1.08f : 1;
                showImage.rectTransform.localScale = Vector3.Lerp(showImage.rectTransform.localScale, Vector3.one * size, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 20));
                showImage.rectTransform.localPosition = showRestPosition + Vector3.back * (ActionAvailable && hovering ? 6 : 0);
                return;
            }
            float wanted = hovering ? 1 : suggested ? 0.45f + 0.2f * Mathf.Sin(Time.time * 3) : 0;
            lift = Mathf.Lerp(lift, wanted, 1 - Mathf.Exp(-Time.deltaTime * 14));

            spin += (idleSpin + lift * 60) * Time.deltaTime;
            miniature.localPosition = restPosition + Vector3.back * (0.014f * lift);
            miniature.localScale = restScale * (1 + 0.24f * lift);
            // The wheel's broad face should be visible, rather than turning edge-on in the tray.
            miniature.localRotation = Quaternion.Euler(id == CrackerId.Chakri ? -78 : -46, 0, 0) * Quaternion.Euler(0, spin, 0);

            // Full strength on the tile being pointed at, and a slow breath on the one suggested next.
            if (hovering) halo.Show(1);
            else if (suggested) halo.Show(0.45f + 0.2f * Mathf.Sin(Time.time * 3));
        }
    }
}
