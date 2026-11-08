using UnityEngine;

namespace Diwali
{
    /// <summary>
    /// The mood of the real room. Passthrough stays on at all times. Sun leaves the room exactly as it is, and
    /// Night dims it a little. Nothing else darkens the room, because it is often night outside already.
    /// A nearby blast still flashes it. The compositor does the grading, so it costs nothing on the GPU.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class FestivalAmbience : MonoBehaviour
    {
        public OVRPassthroughLayer passthrough;
        [Range(-1, 0)] public float nightBrightness = -0.2f;
        [Range(-1, 0)] public float nightSaturation = -0.12f;

        /// <summary>False is Sun, the room as it really is. True is Night, the room dimmed.</summary>
        public bool Night { get; set; }

        float flash, brightness, saturation, appliedBrightness = 1, appliedSaturation = 1;
        bool active;

        /// <summary>Turns passthrough on when a headset is present. It never turns off during a session.</summary>
        public void SetHeadset(bool present)
        {
            if (!passthrough) return;
            passthrough.enabled = present;
            passthrough.textureOpacity = 1;
            appliedBrightness = appliedSaturation = 1;
        }

        public void Begin() => active = true;

        public void Stop() => active = false;

        /// <summary>The room reacts to a nearby flash. Amount is 0 to 1; a single soft pulse, never a strobe.</summary>
        public void Flash(float amount) => flash = Mathf.Max(flash, amount);

        void Update()
        {
            float dt = Time.deltaTime;
            flash = Mathf.MoveTowards(flash, 0, dt * 3.4f);

            float share = active && Night ? 1 : 0;
            brightness = Mathf.MoveTowards(brightness, nightBrightness * share, dt * 0.22f);
            saturation = Mathf.MoveTowards(saturation, nightSaturation * share, dt * 0.22f);
            ApplyGrade(brightness + flash * 0.5f, saturation);
        }

        void ApplyGrade(float newBrightness, float newSaturation)
        {
            if (!passthrough || !passthrough.enabled) return;
            if (Mathf.Abs(newBrightness - appliedBrightness) < 0.002f && Mathf.Abs(newSaturation - appliedSaturation) < 0.002f) return;
            appliedBrightness = newBrightness;
            appliedSaturation = newSaturation;
            passthrough.SetBrightnessContrastSaturation(newBrightness, 0, newSaturation);
        }
    }
}
