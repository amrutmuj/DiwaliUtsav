using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Diwali.Editor
{
    /// <summary>
    /// Numbers rather than opinions: what each sound reaches the ears at, whether the mix clips, what
    /// starting a voice costs the main thread, how much smoke a headset has to fill, and what first use of
    /// each cracker costs. Send <c>bench</c> through the bridge; results land in Verification/Audio/Bench.
    /// These are desktop timings, so compare runs with each other, never with Quest frame times.
    /// </summary>
    [InitializeOnLoad]
    public static class DiwaliSoundBench
    {
        const string Pending = "Diwali.SoundBench";
        const string Section = "Diwali.SoundBench.Section";
        const string Output = "Verification/Audio/Bench";
        const int Window = 8192;

        // A Quest eye sees roughly this much of the room, as tangents of the half angles.
        const float TanHalfWidth = 1.28f, TanHalfHeight = 1.15f;

        static IEnumerator routine;
        static double resume;
        static readonly StringBuilder Report = new StringBuilder();
        static readonly float[] Left = new float[Window], Right = new float[Window];

        static DiwaliSoundBench() => EditorApplication.update += Pump;

        /// <param name="section">"tools" or "sky" runs only that part, which takes seconds rather than minutes.</param>
        public static void Start(string section = "")
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before the sound bench.");
            SessionState.SetString(Section, section);
            Directory.CreateDirectory(Output);
            DiwaliLinkSetup.UseDesktopForNextPlay();
            EditorSceneManager.OpenScene(DiwaliSceneBuilder.MRScenePath);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        static void Pump()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool(Pending, false) && EditorApplication.isPlaying && DiwaliSession.Instance && DiwaliSession.Instance.Started)
            {
                SessionState.SetBool(Pending, false);
                Report.Clear();
                routine = Run(DiwaliSession.Instance);
                resume = EditorApplication.timeSinceStartup + 1;
            }
            if (routine == null) return;
            if (!EditorApplication.isPlaying) { routine = null; return; }
            if (EditorApplication.timeSinceStartup < resume) return;
            try
            {
                if (routine.MoveNext())
                {
                    resume = EditorApplication.timeSinceStartup + (routine.Current is float seconds ? seconds : 0);
                    return;
                }
                File.WriteAllText(Output + "/report.txt", Report.ToString());
                Debug.Log("[Diwali] Sound bench finished.");
            }
            catch (Exception exception)
            {
                File.WriteAllText(Output + "/report.txt", Report + "\nFAILED " + exception);
                Debug.LogException(exception);
            }
            routine = null;
            EditorApplication.isPlaying = false;
        }

        static void Line(string text) => Report.AppendLine(text);

        static IEnumerator Run(DiwaliSession s)
        {
            Line("Diwali sound bench  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            s.rig.enabled = false;
            if (s.music) { s.music.enabled = false; s.music.Track.Stop(); s.music.Jingle.Stop(); }
            s.sound.StopAll();
            yield return 0.6f;

            string section = SessionState.GetString(Section, "");
            if (section == "" || section == "fuse") foreach (object step in Fuses(s)) yield return step;
            if (section == "fuse") yield break;
            if (section != "sky") foreach (object step in ToolRecovery(s)) yield return step;
            if (section != "tools") foreach (object step in Sky(s)) yield return step;
            if (section != "") yield break;
            foreach (object step in Mechanics(s)) yield return step;
            foreach (object step in VoiceStalls(s)) yield return step;
            foreach (object step in EarLevels(s)) yield return step;
            foreach (object step in Mix(s)) yield return step;
            foreach (object step in Spread(s)) yield return step;
            foreach (object step in Smoke(s)) yield return step;
            FirstUse(s);
        }

        // --- reaching a fuse, through the rig's real targeting ---------------------------------------------

        static IEnumerable Fuses(DiwaliSession s)
        {
            Line("\n== What a pinch reaches when aimed at each fuse ==");
            Line("cracker       from 1.6 m away     hand at the fuse");
            var find = typeof(FestivalRig).GetMethod("FindTarget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var headset = typeof(FestivalRig).GetProperty("IsVR");
            var hand = new FestivalPointer { Tracked = true, IsHand = true, Index = 1 };
            Vector3 eye = s.rig.view.transform.position;
            string Reached()
            {
                Physics.SyncTransforms();
                object[] args = { hand, Vector3.zero, false };
                var target = find.Invoke(s.rig, args);
                return target is FestivalFuseTarget ? "fuse" : target is FestivalItem ? "body" : target == null ? "nothing" : target.GetType().Name;
            }

            foreach (var id in new[] { CrackerId.Rocket, CrackerId.Anar, CrackerId.FlowerPot, CrackerId.Chakri, CrackerId.Ladi, CrackerId.BigLadi, CrackerId.Bomb, CrackerId.Caps, CrackerId.SkyShot, CrackerId.TwelveShot, CrackerId.FifteenShot, CrackerId.Butterfly, CrackerId.Diya })
            {
                var item = s.placement.PlaceAt(id, new Vector3(0, s.placement.FloorHeight, 1.6f));
                yield return 0.05f;
                hand.PinchPoint = eye + Vector3.down * 0.4f;
                hand.Ray = new Ray(hand.PinchPoint, (item.fuse.position - hand.PinchPoint).normalized);
                string far = Reached();
                headset.GetSetMethod(true).Invoke(s.rig, new object[] { true });
                hand.PinchPoint = item.fuse.position + Vector3.up * 0.03f;
                hand.Ray = new Ray(hand.PinchPoint, Vector3.down);
                string near = Reached();
                headset.GetSetMethod(true).Invoke(s.rig, new object[] { false });
                Line($"{id,-13} {far,-19} {near}");
                if (id == CrackerId.Rocket)
                {
                    Check(far == "fuse", "A pinch aimed at the rocket's fuse from where you stand reaches the fuse");
                    Check(near == "fuse", "A hand at the rocket's fuse reaches the fuse, not the pick-up");
                }
                s.placement.Remove(item);
                yield return 0.05f;
            }
        }

        // --- getting a dropped tool back, through the rig's real input path -----------------------------

        static IEnumerable ToolRecovery(DiwaliSession s)
        {
            Line("\n== Dropped tools ==");
            var interact = typeof(FestivalRig).GetMethod("Interact", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var hand = s.rig.Right;
            hand.Tracked = hand.IsHand = true;
            hand.Rotation = Quaternion.identity;
            hand.PinchPoint = new Vector3(0.25f, 1.1f, 0.3f);
            hand.Ray = new Ray(hand.PinchPoint, Vector3.forward);
            void Frame(bool pinch)
            {
                hand.WasPressed = hand.Pressed;
                hand.Pressed = pinch;
                hand.SampleMotion(Time.unscaledTime);
                Physics.SyncTransforms();
                interact.Invoke(s.rig, new object[] { hand });
            }

            hand.Pressed = true;
            s.lighter.Take(hand);
            Frame(true);
            Frame(false);
            Check(s.lighter.gameObject.activeSelf && s.lighter.Dropped, "A released lighter falls");
            yield return 1.2f;
            Check(!s.lighter.gameObject.activeSelf && !s.lighter.FlameOn && hand.Grasp == null, "It is gone the moment it reaches the floor");
            hand.Pressed = true;
            hand.WasPressed = false;
            s.Take(CrackerId.Lighter, hand);
            Check(s.lighter.Holder == hand && s.lighter.FlameOn, "Its tray tile gives it straight back, lit");
            Frame(true);
            Frame(false);
            yield return 1.2f;
            Check(Object.FindObjectsByType<LighterItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "There is only ever one lighter");

            hand.Pressed = true;
            s.sparkler.Take(hand);
            Frame(true);
            Frame(false);
            yield return 1.5f;
            Check(s.sparkler.gameObject.activeSelf && s.sparkler.Dropped && s.sparkler.Holder == null, "A phuljhadi stays where it falls");
            hand.Ray = new Ray(hand.PinchPoint, (s.sparkler.GetComponent<GrabZone>().WorldCentre - hand.PinchPoint).normalized);
            Frame(false);
            Frame(true);
            Check(s.sparkler.Holder == hand, "A ray pinch takes it at once");
            for (int i = 0; i < 12; i++) { Frame(true); yield return 0.02f; }
            Check(s.sparkler.Holder == hand, "It stays in the hand while the pinch is held");
            Frame(false);
            Frame(false);
            yield return 0.1f;
            Check(s.sparkler.Holder == null && s.sparkler.Dropped && hand.Grasp == null, "Opening the hand lets it go");
            s.sparkler.Return();
            hand.Tracked = false;
        }

        // --- what goes up, and how big it opens ----------------------------------------------------------

        static IEnumerable Sky(DiwaliSession s)
        {
            Line("\n== Heights and bursts, seen from where the visitor stands ==");
            Directory.CreateDirectory("Verification/Sky");
            var eye = s.rig.view.transform;
            Vector3 head = eye.position;
            Quaternion look = eye.rotation;
            float floor = s.placement.FloorHeight;
            var mesh = s.sky.GetComponentsInChildren<MeshFilter>(true).First(view => view.name == "Sky burst 0").sharedMesh;
            Line($"stars per burst {mesh.vertexCount / SparkMeshes.VerticesPerStar}, vertices {mesh.vertexCount}");

            var rocket = (RocketItem)s.placement.PlaceAt(CrackerId.Rocket, new Vector3(0, floor, 1.6f));
            Light(rocket, 0.02f);
            for (float t = 0; t < 5 && rocket.body.gameObject.activeSelf; t += 0.02f) yield return 0.02f;
            var flight = RocketItem.LastFlight;
            float angle = Mathf.Atan2(s.sky.LastBurstRadius, Vector3.Distance(head, flight.burstPoint)) * Mathf.Rad2Deg;
            Line($"rocket: {flight.apexHeight:F1} m in {flight.seconds:F2} s, burst radius {s.sky.LastBurstRadius:F1} m, {angle:F0} degrees either side of its centre");
            Check(flight.apexHeight > 17 && flight.apexHeight < 19, "The rocket bursts at about 18 metres");
            Check(Mathf.Abs(s.sky.LastBurstRadius - rocket.burstRadius) < 0.01f, "Its burst opens to the radius set for it");
            eye.LookAt(flight.burstPoint);
            yield return 0.45f;
            DiwaliWorkbench.Snapshot("Sky/rocket-opening");
            yield return 0.7f;
            DiwaliWorkbench.Snapshot("Sky/rocket-falling");

            foreach (var id in new[] { CrackerId.TwelveShot, CrackerId.FifteenShot })
            {
                var box = (MortarItem)s.placement.PlaceAt(id, new Vector3(0, floor, 1.6f));
                box.shots = 3;
                Light(box, 0.02f);
                for (float t = 0; t < 8 && box.BurstPoints.Count < 1; t += 0.05f) yield return 0.05f;
                yield return 2.5f;
                Vector3 point = box.BurstPoints[0];
                eye.LookAt(point);
                DiwaliWorkbench.Snapshot($"Sky/{id}-opening");
                for (float t = 0; t < 10 && box.BurstPoints.Count < 3; t += 0.05f) yield return 0.05f;
                float low = box.BurstPoints.Min(burst => burst.y - floor), high = box.BurstPoints.Max(burst => burst.y - floor);
                Line($"{id}: bursts between {low:F1} and {high:F1} m, radius {s.sky.LastBurstRadius:F1} m, {Mathf.Atan2(s.sky.LastBurstRadius, Vector3.Distance(head, point)) * Mathf.Rad2Deg:F0} degrees either side");
                Check(low >= 19.95f && high <= 22.05f, id + " shells burst between 20 and 22 metres");
                yield return 3;
                s.sound.StopFor(box.transform);
                s.placement.Remove(box);
            }

            if (rocket) s.placement.Remove(rocket);

            // Each reference, where it can be set beside its photograph.
            var sights = new[]
            {
                ("1-violet", BurstStyle.VioletPeony()), ("2-gold", BurstStyle.GoldChrysanthemum()),
                ("2-blue", BurstStyle.Arc(SkyShow.ShellBlue, SkyShow.ShellBlue)), ("2-pink", BurstStyle.Arc(SkyShow.ShellPink, SkyShow.ShellOrange)),
                ("3-red", BurstStyle.RedSpider()), ("4-bead", BurstStyle.RedBead(0.22f)),
            };
            Vector3 sight = head + Vector3.forward * 30 + Vector3.up * 18;
            foreach (var (name, style) in sights)
            {
                eye.position = head;
                eye.LookAt(sight);
                s.sky.BurstInSky(sight, style.Sized(10));
                yield return 0.75f;
                DiwaliWorkbench.Snapshot("Sky/reference-" + name);
                yield return 2.6f;
            }

            eye.rotation = Quaternion.LookRotation(new Vector3(0, 0.42f, 1));
            s.sky.StartCoroutine(s.sky.Finale());
            yield return 3.0f;
            DiwaliWorkbench.Snapshot("Sky/reference-4-finale");
            yield return 3;
            eye.SetPositionAndRotation(head, look);
            yield return 0.5f;
        }

        // --- mechanisms that must simply work ---------------------------------------------------------

        static void Check(bool passed, string label)
        {
            Line((passed ? "PASS " : "FAIL ") + label);
            if (!passed) Debug.LogError("[Diwali] Sound bench: " + label);
        }

        static AudioSource Voice(DiwaliSession s, string clip) => s.sound.GetComponentsInChildren<AudioSource>()
            .FirstOrDefault(source => source.isPlaying && source.clip && source.clip.name == clip);

        static void Pinch(FestivalPointer pointer) { pointer.Pressed = true; pointer.WasPressed = false; }

        /// <summary>Holds a pinch for the full two seconds and lets go, as a hand would, so Remove is on offer.</summary>
        static void Offer(FestivalRemoval removal, FestivalPointer pointer, IFestivalTarget target)
        {
            float now = Time.unscaledTime;
            Pinch(pointer);
            removal.Handle(pointer, target, now);
            pointer.WasPressed = true;
            removal.Handle(pointer, target, now + FestivalRemoval.HoldSeconds + 0.01f);
            pointer.Pressed = false;
            removal.Cancel(pointer);
        }

        static void Light(FestivalItem item, float fuse)
        {
            item.fuseSeconds = fuse;
            item.fuseRange = Vector2.zero;
            item.Ignite();
        }

        static IEnumerable Mechanics(DiwaliSession s)
        {
            Line("\n== Mechanisms ==");
            var near = new GameObject("Bench probe").transform;
            near.position = s.rig.view.transform.position + Vector3.forward;

            var stopped = s.sound.StartLoop("sparkler", near, 1, 0.01f);
            yield return 0.1f;
            stopped.Stop(0.05f);
            stopped.SetLevel(1);
            yield return 0.3f;
            Check(!stopped.Alive, "A routine still setting its level cannot revive a stopped loop");

            var doomed = new GameObject("Bench doomed").transform;
            var orphan = s.sound.StartLoop("sparkler", doomed, 1, 0.01f);
            yield return 0.1f;
            Object.Destroy(doomed.gameObject);
            yield return 0.4f;
            Check(!orphan.Alive, "A sound whose object was destroyed stops by itself");

            s.sound.Play("thunder", near.position, 1, 0);
            yield return 0.05f;
            var roll = Voice(s, "rolling_thunder");
            float full = roll ? roll.volume : 0;
            s.sound.SetGentle(true);
            Check(roll && Mathf.Abs(roll.volume - full * s.sound.gentleLevel) < 0.01f, "Gentle lowers a sound that is already playing");
            s.sound.SetGentle(false);
            Check(roll && Mathf.Abs(roll.volume - full) < 0.01f, "Leaving Gentle restores it");
            s.sound.StopAll();
            yield return 0.3f;

            var removal = s.rig.GetComponent<FestivalRemoval>();
            if (!removal) removal = s.rig.gameObject.AddComponent<FestivalRemoval>();
            var pointer = new FestivalPointer { Tracked = true };
            float floor = s.placement.FloorHeight;

            var diya = s.placement.PlaceAt(CrackerId.Diya, new Vector3(0, floor, 1.4f));
            Light(diya, 0.02f);
            yield return 1.6f;
            var flame = Voice(s, "flame_loop");
            float burning = flame ? flame.volume : 0;
            Check(flame && burning > 0.05f, "A lit diya has its flame sound");
            Offer(removal, pointer, diya);
            yield return 0.4f;
            Check(flame && flame.volume < 0.01f && diya.RemovalHidden, "The two-second hold hides a lit diya and hushes its flame");
            Pinch(pointer);
            Check(removal.HandleTapAway(pointer, null), "A tap elsewhere withdraws the offer");
            yield return 0.4f;
            Check(diya && !diya.RemovalHidden && diya.GetComponentsInChildren<MeshRenderer>().Any(part => !part.forceRenderingOff), "The diya comes back");
            Check(flame && flame.isPlaying && flame.volume > burning * 0.8f, "Its flame sound comes back with it");

            Offer(removal, pointer, diya);
            removal.Remove();
            yield return 0.2f;
            Check(!diya && !s.placement.Items.Contains(diya), "Pressing Remove deletes the object at once");
            Pinch(pointer);
            Check(!removal.HandleTapAway(pointer, null), "A tap elsewhere afterwards changes nothing");
            yield return 0.4f;
            Check(!diya && !Voice(s, "flame_loop"), "It stays gone, and so does its sound");

            var bomb = (BlastItem)s.placement.PlaceAt(CrackerId.Bomb, new Vector3(0.5f, floor, 1.4f));
            Light(bomb, 1);
            yield return 0.2f;
            Offer(removal, pointer, bomb);
            yield return 1.6f;
            Check(bomb && bomb.DetonationFrame == 0, "A lit bomb hidden by the offer does not go off from nowhere");
            Pinch(pointer);
            removal.HandleTapAway(pointer, null);
            yield return 1.3f;
            Check(bomb && bomb.DetonationFrame > 0, "Brought back, its fuse carries on and it goes off");

            var pop = (PopperItem)s.placement.PlaceAt(CrackerId.Popper, new Vector3(-0.5f, floor, 1.4f));
            pop.FloorImpact(4);
            yield return 0.4f;
            Offer(removal, pointer, pop);
            yield return 2.6f;
            Check(pop && pop.RemovalHidden, "A spent shell behind the offer waits instead of clearing itself");
            Pinch(pointer);
            removal.HandleTapAway(pointer, null);

            var toggle = Object.FindObjectsByType<ThaliSetting>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(setting => setting.kind == SettingKind.Night);
            Check(!s.ambience.Night && toggle.icon.sprite == toggle.sunIcon, "The room starts on Sun, undimmed, and the button shows the sun");
            toggle.Press(null);
            Check(s.ambience.Night && toggle.icon.sprite == toggle.nightIcon, "One pinch turns it to Night, and the button shows the moon");
            toggle.Press(null);
            Check(!s.ambience.Night && toggle.icon.sprite == toggle.sunIcon, "Another pinch turns it back to Sun");
            Check(!Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(view => view.name == "Night sky"), "No night sky dome is left in the scene");

            foreach (var item in s.placement.Items.ToArray()) if (item) { s.sound.StopFor(item.transform); s.placement.Remove(item); }
            Object.Destroy(near.gameObject);
            yield return 1;
        }

        // --- what starting a voice costs -------------------------------------------------------------

        static List<(string name, ProfilerRecorder recorder)> Recorders()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var found = new List<(string, ProfilerRecorder)>();
            foreach (var handle in handles)
            {
                string name = ProfilerRecorderHandle.GetDescription(handle).Name;
                if (name == "Flush DSP connections" || name == "AudioManager.Update")
                    found.Add((name, new ProfilerRecorder(handle, 4096, ProfilerRecorderOptions.Default)));
            }
            return found;
        }

        static IEnumerable VoiceStalls(DiwaliSession s)
        {
            Line("\n== Main-thread cost of starting a voice (desktop ms) ==");

            // One sound first, so that the audio markers exist before they are looked up.
            s.sound.Play("pop", Vector3.forward * 1.5f, 0.001f, 0);
            yield return 0.2f;

            foreach (object step in Stall("project voices", (position, clip) => s.sound.Play("pop", position, 0.001f, 0), s)) yield return step;

            // Alternating forces the filter on and off on the same voices, which is when a stall would show.
            foreach (object step in Stall("project voices, filter toggling", (position, clip) => s.sound.Play(clip % 2 == 0 ? "crackle" : "pop", position, 0.001f, 0), s)) yield return step;

            var root = new GameObject("Bench voices").transform;
            var plain = new List<AudioSource>();
            for (int i = 0; i < 48; i++)
            {
                var voice = new GameObject("Bench voice " + i).AddComponent<AudioSource>();
                voice.transform.SetParent(root);
                voice.playOnAwake = false;
                voice.spatialBlend = 1;
                voice.spatialize = true;
                voice.gameObject.AddComponent<MetaXRAudioSource>();
                plain.Add(voice);
            }
            yield return 0.2f;
            int next = 0;
            var clips = s.sound.bank.sounds.First(sound => sound.id == "pop").clips;
            foreach (object step in Stall("spatial voices with no filter component", (position, clip) =>
            {
                var voice = plain[next++ % plain.Count];
                voice.Stop();
                voice.clip = clips[clip % clips.Length];
                voice.volume = 0.001f;
                voice.transform.position = position;
                voice.Play();
            }, s)) yield return step;
            Object.Destroy(root.gameObject);
            yield return 0.3f;
        }

        static IEnumerable Stall(string label, Action<Vector3, int> play, DiwaliSession s)
        {
            var recorders = Recorders();
            foreach (var entry in recorders) entry.recorder.Start();
            var times = new List<double>();
            var clock = new Stopwatch();
            for (int i = 0; i < 80; i++)
            {
                clock.Restart();
                play(new Vector3(UnityEngine.Random.Range(-1f, 1f), 0.1f, 1.5f), i);
                clock.Stop();
                times.Add(clock.Elapsed.TotalMilliseconds);
                yield return 0.04f;
            }
            yield return 0.3f;
            times.Sort();
            Line($"{label}: 80 starts, call mean {times.Average():F3}, p95 {times[75]:F3}, worst {times[79]:F3}");
            foreach (var entry in recorders)
            {
                var recorder = entry.recorder;
                recorder.Stop();
                var values = new List<double>();
                for (int i = 0; i < recorder.Count; i++) values.Add(recorder.GetSample(i).Value / 1e6);
                int busy = values.Count(value => value > 0.05);
                Line($"   {entry.name}: {values.Count} frames, over 0.05 ms in {busy}, sum {values.Sum():F1}, worst frame {(values.Count > 0 ? values.Max() : 0):F2}");
                recorder.Dispose();
            }
        }

        // --- what reaches the ears -------------------------------------------------------------------

        static void ReadMix()
        {
            AudioListener.GetOutputData(Left, 0);
            AudioListener.GetOutputData(Right, 1);
        }

        static float Peak(float[] samples) => samples.Max(Mathf.Abs);

        /// <summary>Loudest 50 ms in the window, which is close to how loud a short bang actually seems.</summary>
        static float Loudest(float[] samples)
        {
            int span = 2400;
            double sum = 0, best = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * samples[i];
                if (i >= span) sum -= samples[i - span] * samples[i - span];
                if (i >= span - 1) best = Math.Max(best, sum / span);
            }
            return (float)Math.Sqrt(best);
        }

        static float Db(float value) => 20 * Mathf.Log10(Mathf.Max(value, 1e-6f));

        static IEnumerable EarLevels(DiwaliSession s)
        {
            Line("\n== Level at the ears, one sound at a time, dead ahead (dBFS) ==");
            Line("sound      distance   peak   loudest 50 ms");
            string[] ids = { "cap", "snap", "pop", "mortar", "place", "strike", "ignite", "select" };
            float[] distances = { 1.5f, 4 };
            Vector3 head = s.rig.view.transform.position;
            Vector3 ahead = Vector3.ProjectOnPlane(s.rig.view.transform.forward, Vector3.up).normalized;
            foreach (string id in ids)
            {
                foreach (float distance in distances)
                {
                    float peak = 0, loud = 0;
                    var entry = s.sound.bank.sounds.First(sound => sound.id == id);
                    for (int take = 0; take < entry.clips.Length * 2; take++)
                    {
                        s.sound.Play(id, head + ahead * distance, 1, 0);
                        yield return 0.16f;
                        ReadMix();
                        peak = Mathf.Max(peak, Mathf.Max(Peak(Left), Peak(Right)));
                        loud = Mathf.Max(loud, Mathf.Max(Loudest(Left), Loudest(Right)));
                        yield return 0.2f;
                    }
                    Line($"{id,-10} {distance,4:F1} m  {Db(peak),6:F1}  {Db(loud),6:F1}");
                }
            }

            // The Hand Popper as it is really heard: one report where a throw lands.
            foreach (float distance in distances)
            {
                float loud = 0, peak = 0;
                for (int take = 0; take < 6; take++)
                {
                    var pop = s.placement.PlaceAt(CrackerId.Popper, new Vector3(head.x + ahead.x * distance, s.placement.FloorHeight, head.z + ahead.z * distance));
                    pop.FloorImpact(4);
                    yield return 0.16f;
                    ReadMix();
                    peak = Mathf.Max(peak, Mathf.Max(Peak(Left), Peak(Right)));
                    loud = Mathf.Max(loud, Mathf.Max(Loudest(Left), Loudest(Right)));
                    yield return 0.25f;
                }
                Line($"Hand Popper {distance,3:F1} m  {Db(peak),6:F1}  {Db(loud),6:F1}");
            }
            foreach (var item in s.placement.Items.ToArray()) if (item) s.placement.Remove(item);
            yield return 0.5f;
        }

        static IEnumerable Mix(DiwaliSession s)
        {
            Line("\n== Whole-mix peak while real crackers perform, 1.5 m ahead ==");
            CrackerId[][] runs =
            {
                new[] { CrackerId.BigLadi },
                new[] { CrackerId.Bomb },
                new[] { CrackerId.BigLadi, CrackerId.Bomb, CrackerId.Anar },
            };
            foreach (var run in runs)
            {
                var crackers = new List<FestivalItem>();
                for (int i = 0; i < run.Length; i++)
                {
                    var item = s.placement.PlaceAt(run[i], new Vector3(-0.5f + i * 0.5f, s.placement.FloorHeight, 1.5f));
                    item.fuseSeconds = 0.02f;
                    item.fuseRange = Vector2.zero;
                    item.Ignite();
                    crackers.Add(item);
                }
                float peak = 0;
                int over = 0;
                for (float t = 0; t < 4.5f; t += 0.04f)
                {
                    ReadMix();
                    foreach (float sample in Left) { float a = Mathf.Abs(sample); peak = Mathf.Max(peak, a); if (a >= 1) over++; }
                    foreach (float sample in Right) { float a = Mathf.Abs(sample); peak = Mathf.Max(peak, a); if (a >= 1) over++; }
                    yield return 0.04f;
                }
                Line($"{string.Join(" + ", run),-24} peak {peak:F3} ({Db(peak):F1} dBFS), samples at or over full scale {over}");
                foreach (var item in crackers) if (item) { s.sound.StopFor(item.transform); s.placement.Remove(item); }
                yield return 1.2f;
            }
        }

        /// <summary>How far left and right differ for a distant sound off to one side. A wide source reads as less of a point.</summary>
        static IEnumerable Spread(DiwaliSession s)
        {
            Line("\n== Source size: left ear lead for the same clip 45 m to the left, as a point and at its set size (dB) ==");
            Vector3 head = s.rig.view.transform.position;
            Vector3 left = -Vector3.ProjectOnPlane(s.rig.view.transform.right, Vector3.up).normalized;
            foreach (string id in new[] { "burst", "thunder" })
            {
                var entry = s.sound.bank.sounds.First(sound => sound.id == id);
                float size = entry.sourceRadius;
                var clips = entry.clips;
                entry.clips = new[] { clips[0] };
                foreach (float radius in new[] { 0, size })
                {
                    entry.sourceRadius = radius;
                    double l = 0, r = 0;
                    for (int take = 0; take < 6; take++)
                    {
                        s.sound.Play(id, head + left * 45, 1, 0);
                        yield return 0.16f;
                        ReadMix();
                        foreach (float sample in Left) l += sample * sample;
                        foreach (float sample in Right) r += sample * sample;
                        s.sound.StopAll();
                        yield return 0.3f;
                    }
                    Line($"{id,-10} radius {radius,4:F0} m   lead {10 * Math.Log10(Math.Max(l, 1e-12) / Math.Max(r, 1e-12)),5:F1}");
                }
                entry.sourceRadius = size;
                entry.clips = clips;
            }
        }

        // --- how much the headset has to fill -----------------------------------------------------------

        static float Coverage(Vector3 eye, Vector3 forward, Vector3 position, float size)
        {
            float depth = Vector3.Dot(position - eye, forward);
            if (depth < 0.05f) return 0;
            float width = Mathf.Min(1, size / (2 * depth * TanHalfWidth));
            float height = Mathf.Min(1, size / (2 * depth * TanHalfHeight));
            return width * height;
        }

        static IEnumerable Smoke(DiwaliSession s)
        {
            Line("\n== Smoke fill: screens of blended smoke per eye, 1.5 m away, looking at it ==");
            Line("cracker     puffs  peak screens  mean screens  (each screen is one full-eye layer of blending)");
            CrackerId[] crackers = { CrackerId.Bomb, CrackerId.BigLadi, CrackerId.Ladi, CrackerId.Anar, CrackerId.Chakri, CrackerId.TwelveShot, CrackerId.Popper };
            var buffer = new ParticleSystem.Particle[2048];
            foreach (var id in crackers)
            {
                var item = s.placement.PlaceAt(id, new Vector3(0, s.placement.FloorHeight, 1.5f));
                item.fuseSeconds = 0.02f;
                item.fuseRange = Vector2.zero;
                if (id == CrackerId.Popper) item.FloorImpact(4);
                else item.Ignite();
                var systems = item.GetComponentsInChildren<ParticleSystem>(true)
                    .Where(system => system.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name == "Diwali/Smoke").ToArray();

                var eye = new Vector3(0, 1.5f, 0);
                var forward = (new Vector3(0, 0.6f, 1.5f) - eye).normalized;
                float peak = 0, total = 0;
                int samples = 0, most = 0;
                for (float t = 0; t < 9 && item; t += 0.1f)
                {
                    float screens = 0;
                    int alive = 0;
                    foreach (var system in systems)
                    {
                        if (!system) continue;
                        int count = system.GetParticles(buffer);
                        alive += count;
                        bool world = system.main.simulationSpace == ParticleSystemSimulationSpace.World;
                        for (int i = 0; i < count; i++)
                        {
                            Vector3 position = world ? buffer[i].position : system.transform.TransformPoint(buffer[i].position);
                            screens += Coverage(eye, forward, position, buffer[i].GetCurrentSize(system));
                        }
                    }
                    peak = Mathf.Max(peak, screens);
                    most = Mathf.Max(most, alive);
                    if (screens > 0.01f) { total += screens; samples++; }
                    yield return 0.1f;
                }
                Line($"{id,-11} {most,5}  {peak,12:F1}  {(samples > 0 ? total / samples : 0),12:F1}");
                if (item) { s.sound.StopFor(item.transform); s.placement.Remove(item); }
                yield return 0.4f;
            }
        }

        // --- first use ----------------------------------------------------------------------------------

        static void FirstUse(DiwaliSession s)
        {
            Line("\n== One-frame cost the first time something is used (desktop ms) ==");
            Line("item         place first  place again  halo build  halo shells  renderers");
            var clock = new Stopwatch();
            foreach (var entry in s.catalog.entries)
            {
                if (!entry.prefab || entry.id == CrackerId.GrandShow || !entry.prefab.GetComponent<FestivalItem>()) continue;
                clock.Restart();
                var first = s.placement.PlaceAt(entry.id, new Vector3(0, s.placement.FloorHeight, 2));
                clock.Stop();
                double placeFirst = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                var second = s.placement.PlaceAt(entry.id, new Vector3(0.4f, s.placement.FloorHeight, 2));
                clock.Stop();
                double placeAgain = clock.Elapsed.TotalMilliseconds;

                var zone = first.GetComponent<GrabZone>();
                clock.Restart();
                if (zone) zone.Halo.Show(1);
                clock.Stop();
                int shells = zone ? zone.Halo.Shells : 0;
                int renderers = first.GetComponentsInChildren<Renderer>(true).Length;
                Line($"{entry.id,-12} {placeFirst,11:F2}  {placeAgain,11:F2}  {clock.Elapsed.TotalMilliseconds,10:F2}  {shells,11}  {renderers,9}");
                s.placement.Remove(first);
                s.placement.Remove(second);
            }
        }
    }
}
