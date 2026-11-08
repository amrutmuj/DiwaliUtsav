using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Diwali.Editor
{
    /// <summary>
    /// Runs the Unity Profiler over the scripted walk-through and writes what it found to
    /// Verification/profile-report.txt: the worst hitches, what caused each one, and where time goes.
    /// </summary>
    public static class DiwaliProfileRun
    {
        const string ActiveKey = "Diwali.Profiling";
        const string Report = "Verification/profile-report.txt";
        const float HitchMs = 22;
        const int SampleEvery = 8;

        sealed class Hitch
        {
            public int frame;
            public float ms;
            public string step;
            public string detail;
        }

        static readonly List<float> Frames = new List<float>();
        static readonly List<Hitch> Hitches = new List<Hitch>();
        static readonly Dictionary<string, double> SelfTime = new Dictionary<string, double>();
        static readonly List<int> Children = new List<int>();
        static int nextFrame = -1;
        static double gcBytes;
        static int sampled;

        public static bool Active => SessionState.GetBool(ActiveKey, false);

        public static void Begin()
        {
            SessionState.SetBool(ActiveKey, true);
            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.profileEditor = false;
            ProfilerDriver.enabled = true;
        }

        /// <summary>Reads any frames recorded since the last call. Runs between Play frames.</summary>
        public static void Pump(string step)
        {
            if (!Active) return;
            int last = ProfilerDriver.lastFrameIndex;
            if (nextFrame < 0) nextFrame = ProfilerDriver.firstFrameIndex;
            nextFrame = Mathf.Max(nextFrame, ProfilerDriver.firstFrameIndex);
            for (; nextFrame >= 0 && nextFrame <= last; nextFrame++) ReadFrame(nextFrame, step);
        }

        static void ReadFrame(int index, string step)
        {
            using (var view = ProfilerDriver.GetHierarchyFrameDataView(index, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false))
            {
                if (view == null || !view.valid) return;

                int loop = FindChild(view, view.GetRootItemID(), "PlayerLoop");
                if (loop < 0) return;
                float cpu = view.GetItemColumnDataAsFloat(loop, HierarchyFrameDataView.columnTotalTime);
                Frames.Add(cpu);

                bool hitch = cpu >= HitchMs;
                if (!hitch && Frames.Count % SampleEvery != 0) return;

                var items = new List<(string name, float self, float total)>();
                Walk(view, loop, 0, items);
                if (!hitch)
                {
                    sampled++;
                    foreach (var item in items)
                    {
                        SelfTime.TryGetValue(item.name, out double value);
                        SelfTime[item.name] = value + item.self;
                    }
                    gcBytes += view.GetItemColumnDataAsFloat(loop, HierarchyFrameDataView.columnGcMemory);
                    return;
                }

                var top = items.OrderByDescending(item => item.self).Take(7).Where(item => item.self > 0.25f);
                Hitches.Add(new Hitch
                {
                    frame = index,
                    ms = cpu,
                    step = step,
                    detail = string.Join("\n", top.Select(item => $"        {item.self,7:F1} ms  {item.name}")),
                });
            }
        }

        static int FindChild(HierarchyFrameDataView view, int parent, string name)
        {
            view.GetItemChildren(parent, Children);
            foreach (int child in Children)
                if (view.GetItemName(child) == name) return child;
            return -1;
        }

        static void Walk(HierarchyFrameDataView view, int id, int depth, List<(string, float, float)> items)
        {
            if (depth > 12) return;
            var children = new List<int>();
            view.GetItemChildren(id, children);
            foreach (int child in children)
            {
                items.Add((view.GetItemName(child), view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnSelfTime), view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnTotalTime)));
                Walk(view, child, depth + 1, items);
            }
        }

        public static void Finish(string note)
        {
            if (!Active) return;
            SessionState.SetBool(ActiveKey, false);
            Pump("end");
            ProfilerDriver.enabled = false;

            var sorted = Frames.OrderBy(value => value).ToList();
            float At(float share) => sorted.Count == 0 ? 0 : sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * share))];

            var text = new StringBuilder();
            text.AppendLine("Diwali profile: Unity Profiler over the scripted walk-through");
            text.AppendLine(note);
            text.AppendLine("Editor CPU timings on the desktop. Use them to find what is slow, not as Quest frame times.");
            text.AppendLine();
            text.AppendLine($"Frames {Frames.Count}   mean {Frames.DefaultIfEmpty().Average():F2} ms   p50 {At(0.5f):F2}   p95 {At(0.95f):F2}   p99 {At(0.99f):F2}   worst {At(1):F1} ms");
            text.AppendLine($"Hitches over {HitchMs} ms: {Hitches.Count}");
            text.AppendLine($"Managed allocation per sampled frame: {(sampled > 0 ? gcBytes / sampled : 0):F0} bytes");
            text.AppendLine();
            text.AppendLine("Worst hitches (step, CPU ms, then the largest self times in that frame)");
            foreach (var hitch in Hitches.OrderByDescending(item => item.ms).Take(14))
            {
                text.AppendLine($"  step {hitch.step,-5} frame {hitch.frame,-6} {hitch.ms,7:F1} ms");
                text.AppendLine(hitch.detail);
            }
            text.AppendLine();
            text.AppendLine("Where ordinary frames spend their time (self ms per sampled frame)");
            foreach (var entry in SelfTime.OrderByDescending(pair => pair.Value).Take(18))
                text.AppendLine($"  {entry.Value / Mathf.Max(sampled, 1),7:F3} ms  {entry.Key}");

            File.WriteAllText(Report, text.ToString());
            Frames.Clear();
            Hitches.Clear();
            SelfTime.Clear();
            nextFrame = -1;
            gcBytes = 0;
            sampled = 0;
        }
    }
}
