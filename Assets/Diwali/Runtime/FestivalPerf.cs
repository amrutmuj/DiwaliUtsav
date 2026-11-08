using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Diwali
{
    /// <summary>
    /// A small always-on flight recorder. It notes how long startup took and, while running, reports
    /// frame rate and hitches once every few seconds. Lines start with [Perf], so they can be read
    /// from the Unity Console over Link or from adb logcat on the headset, with no special build.
    /// </summary>
    public sealed class FestivalPerf : MonoBehaviour
    {
        const float ReportSeconds = 5;
        const float HitchSeconds = 1 / 45f;
        const int SettleFrames = 3;

        static readonly Stopwatch Clock = new Stopwatch();

        float windowStart, worst;
        int frames, hitches, settling;
        long lastAllocated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void StartClock() => Clock.Restart();

        /// <summary>Marks a startup milestone against the time the player began loading.</summary>
        public static void Note(string milestone) => Debug.Log($"[Perf] {Clock.Elapsed.TotalSeconds:F2}s {milestone}");

        void Start()
        {
            windowStart = Time.unscaledTime;
            lastAllocated = System.GC.GetTotalMemory(false);
            Note("first Start");
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (settling < SettleFrames)
            {
                // The first frame carries the whole scene load: how long the headset was given nothing new.
                worst = Mathf.Max(worst, dt);
                if (++settling == SettleFrames)
                {
                    windowStart = Time.unscaledTime;
                    Note($"first frames done, longest {worst * 1000:F0} ms");
                    worst = 0;
                }
                return;
            }

            frames++;
            worst = Mathf.Max(worst, dt);
            if (dt > HitchSeconds) hitches++;

            float elapsed = Time.unscaledTime - windowStart;
            if (elapsed >= ReportSeconds) Report(elapsed);
        }

        // A scene rarely ends on a report, and the last moments of the splash are the ones that matter.
        void OnDestroy()
        {
            if (frames > 0) Report(Time.unscaledTime - windowStart);
        }

        void Report(float elapsed)
        {
            long allocated = System.GC.GetTotalMemory(false);
            Debug.Log($"[Perf] {frames / elapsed:F1} fps, worst {worst * 1000:F1} ms, {hitches} hitches, managed {allocated / 1048576f:F1} MB ({(allocated - lastAllocated) / 1024f:+0;-0} KB)");
            windowStart = Time.unscaledTime;
            frames = hitches = 0;
            worst = 0;
            lastAllocated = allocated;
        }
    }
}
