using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Diwali.Editor
{
    /// <summary>Exercises the actual cracker audio, including moving sources and silent completion.</summary>
    [InitializeOnLoad]
    public static class DiwaliAudioReview
    {
        const string Output = "Verification/Audio/CrackerCorrection";
        const string Pending = "Diwali.AudioReview";
        static bool running;
        static bool editorMuted;
        static int step, errors, whistleCount, burstCount;
        static double next, timeout;
        static float poweredVolume, normalVolume;
        static Vector3 previousPosition;
        static FestivalItem item;
        static SoundLoop probe;
        static Transform probeOrigin;

        static DiwaliAudioReview()
        {
            EditorApplication.update += Pump;
            Application.logMessageReceived += Log;
        }

        [MenuItem("Diwali/Validate Firework Audio")]
        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before audio review.");
            FestivalRecordedAudio.Refresh();
            editorMuted = EditorUtility.audioMasterMute;
            EditorUtility.audioMasterMute = false;
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/playmode-validation.txt", "Firework audio review\nUTC: " + DateTime.UtcNow.ToString("O") + "\n");
            File.WriteAllText(Output + "/runtime-errors.txt", "");
            DiwaliLinkSetup.UseDesktopForNextPlay();
            EditorSceneManager.OpenScene(DiwaliSceneBuilder.MRScenePath);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        static void Log(string message, string stack, LogType type)
        {
            if (!running) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || message.StartsWith("[Diwali] Missing sound:"))
            {
                errors++;
                File.AppendAllText(Output + "/runtime-errors.txt", message + "\n" + stack + "\n");
            }
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            File.AppendAllText(Output + "/playmode-validation.txt", "PASS " + message + "\n");
        }

        static void Wait(double seconds) { step++; next = EditorApplication.timeSinceStartup + seconds; }
        static AudioSource Voice(DiwaliSession s, string prefix) => s.sound.GetComponentsInChildren<AudioSource>()
            .FirstOrDefault(source => source.isPlaying && source.clip && source.clip.name.StartsWith(prefix));

        static void Audible(AudioSource source, string label)
        {
            Check(source && source.spatialBlend == 1 && source.volume > 0.005f, label + " plays through a spatial source");
            var samples = new float[2048];
            // Read the final mix: a spatializer can consume a source buffer
            // before AudioSource.GetOutputData exposes it to the editor.
            AudioListener.GetOutputData(samples, 0);
            float rms = Mathf.Sqrt(samples.Sum(value => value * value) / samples.Length);
            File.AppendAllText(Output + "/playmode-validation.txt", $"INFO {label}: listener RMS {rms:F6}, source gain {source.volume:F3}, sample {source.timeSamples}\n");
            Check(rms > 0.00001f, label + " produces non-silent decoded output");
        }

        static FestivalItem Light(DiwaliSession s, CrackerId id)
        {
            var placed = s.placement.PlaceAt(id, new Vector3(0, 0, 1.8f));
            placed.fuseSeconds = 0.05f;
            placed.fuseRange = Vector2.zero;
            placed.Ignite();
            return placed;
        }

        static void Pump()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool(Pending, false) && EditorApplication.isPlaying && DiwaliSession.Instance)
            {
                SessionState.SetBool(Pending, false);
                running = true; step = errors = 0;
                next = EditorApplication.timeSinceStartup + 1;
                timeout = EditorApplication.timeSinceStartup + 65;
            }
            if (!running) return;
            if (!EditorApplication.isPlaying) { running = false; return; }
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                CheckTimeout();
                var s = DiwaliSession.Instance;
                switch (step)
                {
                    case 0:
                        s.rig.enabled = false;
                        if (s.music) { s.music.enabled = false; s.music.Track.Stop(); s.music.Jingle.Stop(); }
                        s.sound.StopAll();
                        Check(s.sound.bank.sounds.All(sound => sound.id != "done" && sound.clips.All(clip => clip && clip.name != "ui_done")), "Neither completion cue nor bell clip is in the sound bank");
                        Check(s.sound.bank.sounds.Select(sound => sound.id).Distinct().Count() == s.sound.bank.sounds.Length, "Every sound bank ID is unique");
                        foreach (var id in FestivalCatalog.Journey) { s.Completed(id); s.Completed(id); }
                        Check(s.JourneyProgress == FestivalCatalog.Journey.Length, "Silent completion still updates every journey step once");
                        Check(s.sound.LastCue("done").count == 0, "Starting and completing crackers never trigger a bell");
                        Wait(0.4); break;
                    case 1:
                        Check(!s.sound.GetComponentsInChildren<AudioSource>().Any(source => source.isPlaying), "Completion leaves the audio voice pool silent");
                        item = Light(s, CrackerId.Anar); ((FountainItem)item).duration = 2.6f;
                        Wait(1.4); break;
                    case 2:
                        var fountain = Voice(s, "anar_loop"); Audible(fountain, "Anar");
                        Check(Vector3.Distance(fountain.transform.position, ((FountainItem)item).jet.transform.position) < 0.02f, "Anar roar stays at the fountain jet");
                        Wait(3.2); break;
                    case 3:
                        Check(!Voice(s, "anar_loop"), "Fountain sound stops after burnout");
                        Object.Destroy(item.gameObject);
                        item = Light(s, CrackerId.Chakri); ((SpinnerItem)item).duration = 3;
                        Wait(1.8); break;
                    case 4:
                        var spinner = Voice(s, "chakri_loop"); Audible(spinner, "Chakri");
                        poweredVolume = spinner.volume;
                        Check(((SpinnerItem)item).AngularSpeed > 1, "Chakri jet sounds while the disc spins");
                        Wait(1.8); break;
                    case 5:
                        var coast = Voice(s, "chakri_loop");
                        Check(coast && coast.volume < poweredVolume && coast.pitch < 1 && ((SpinnerItem)item).AngularSpeed > 0, "Restored chakri sound winds down with the coasting disc");
                        Wait(3.2); break;
                    case 6:
                        Check(!Voice(s, "chakri_loop"), "Chakri sound ends with the coast");
                        Object.Destroy(item.gameObject);
                        item = Light(s, CrackerId.Butterfly); ((ButterflyItem)item).flightSeconds = 3.5f;
                        Wait(1.2); break;
                    case 7:
                        var butterfly = Voice(s, "butterfly_loop"); Audible(butterfly, "Butterfly");
                        Check(!Voice(s, "chakri_loop") && butterfly.dopplerLevel > 0, "Butterfly has its own recording and restrained Doppler motion");
                        previousPosition = butterfly.transform.position;
                        Wait(0.4); break;
                    case 8:
                        var moving = Voice(s, "butterfly_loop");
                        Check(moving && Vector3.Distance(moving.transform.position, ((ButterflyItem)item).body.position) < 0.04f && Vector3.Distance(previousPosition, moving.transform.position) > 0.05f, "Butterfly sound follows its flight on successive frames");
                        Wait(2.4); break;
                    case 9:
                        Check(!Voice(s, "butterfly_loop"), "Butterfly flutter fades out at landing");
                        Object.Destroy(item.gameObject);
                        whistleCount = s.sound.LastCue("whistle").count; burstCount = s.sound.LastCue("burst").count;
                        item = Light(s, CrackerId.Rocket);
                        Wait(0.35); break;
                    case 10:
                        var motor = Voice(s, "rocket_motor_"); Audible(motor, "Rocket motor");
                        Check(!motor.loop && Vector3.Distance(motor.transform.position, ((RocketItem)item).body.position) < 0.35f, "Rocket motor plays once and follows the climbing body");
                        Check(s.sound.LastCue("whistle").count == whistleCount, "Rocket coast whistle waits for motor burnout");
                        Wait(0.85); break;
                    case 11:
                        var whistle = Voice(s, "whistle"); Audible(whistle, "Rocket coast whistle");
                        Check(!Voice(s, "rocket_motor_") && !whistle.loop, "Rocket motor ends before the separate coast whistle");
                        Wait(3.0); break;
                    case 12:
                        Check(s.sound.LastCue("burst").count > burstCount && !Voice(s, "whistle"), "Rocket ends its whistle and triggers the independent sky report");
                        Check(s.sound.LastCue("done").count == 0, "No bell is triggered by actual fountain, chakri, butterfly or rocket playback");
                        s.sound.StopAll();
                        probeOrigin = new GameObject("Audio level probe").transform;
                        probeOrigin.position = s.rig.view.transform.position + Vector3.right;
                        probe = s.sound.StartLoop("sparkler", probeOrigin, 1, 0.01f);
                        Wait(0.4); break;
                    case 13:
                        var sparkler = Voice(s, "sparkler_loop"); Audible(sparkler, "Phuljhadi");
                        normalVolume = sparkler.volume;
                        s.sound.SetGentle(true);
                        Wait(0.2); break;
                    case 14:
                        Check(Mathf.Abs(Voice(s, "sparkler_loop").volume / normalVolume - s.sound.gentleLevel) < 0.01f, "Gentle mode retains the intended gain reduction");
                        probe.Stop(0.05f); s.sound.SetGentle(false);
                        Wait(0.3); break;
                    case 15:
                        Check(!Voice(s, "sparkler_loop"), "Stopped loops release their audio voices");
                        var selectedBomb = s.sound.bank.sounds.First(sound => sound.id == "bomb");
                        Check(selectedBomb.clips.Length == 1 && selectedBomb.clips[0].name == "bomb", "Bomb uses only the selected recorded firecracker, with no retired explosion variants");
                        item = Light(s, CrackerId.Bomb);
                        Wait(0.2); break;
                    case 16:
                        Audible(Voice(s, "bomb"), "Recorded firecracker bomb");
                        Check(s.sound.LastCue("bomb").frame == ((BlastItem)item).DetonationFrame, "Recorded firecracker report starts on the bomb flash frame");
                        Wait(2.5); break;
                    case 17:
                        Check(!Voice(s, "bomb") && s.sound.LastCue("done").count == 0, "Recorded bomb decay ends naturally without a completion bell");
                        var selection = s.sound.bank.sounds.Single(sound => sound.id == "select");
                        Check(selection.clips.Length == 1 && selection.clips[0].name == "ui_select" && selection.clips[0].length < 0.12f && selection.reverbSendDb == -60,
                            "Cracker selection uses one short, dry click");
                        Check(!s.sound.bank.sounds.Any(sound => sound.id == "hover"), "The old hover cue is absent from the active bank");
                        var tile = s.thali.tiles.First(candidate => candidate.id == CrackerId.Anar);
                        int selections = s.sound.LastCue("select").count, lifts = s.sound.LastCue("lift").count;
                        tile.Hover(true); tile.Hover(false);
                        Check(s.sound.LastCue("select").count == selections, "Hovering over a cracker stays silent");
                        var hand = new FestivalPointer { Tracked = true, Pressed = true, NearInteraction = true, PinchPoint = new Vector3(0.2f, 0.9f, 1.4f) };
                        tile.Press(hand);
                        Check(hand.Grasp != null && s.sound.LastCue("select").count == selections + 1 && s.sound.LastCue("lift").count == lifts,
                            "Selecting a cracker plays exactly one click without the old pickup cue");
                        tile.Press(hand);
                        Check(s.sound.LastCue("select").count == selections + 1, "A blocked selection does not play another click");
                        Check(errors == 0, "No runtime audio errors or missing sound cues");
                        File.AppendAllText(Output + "/playmode-validation.txt", "PASS audio review complete\n");
                        running = false;
                        EditorUtility.audioMasterMute = editorMuted;
                        EditorApplication.isPlaying = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(Output + "/playmode-validation.txt", "FAIL " + ex + "\n");
                running = false;
                EditorUtility.audioMasterMute = editorMuted;
                EditorApplication.isPlaying = false;
                Debug.LogException(ex);
            }
        }

        static void CheckTimeout()
        {
            if (EditorApplication.timeSinceStartup > timeout) throw new TimeoutException("Audio review exceeded 65 seconds.");
        }
    }
}
