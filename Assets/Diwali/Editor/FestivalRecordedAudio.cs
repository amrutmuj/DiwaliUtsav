using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Diwali.Editor
{
    /// <summary>Preserves recorded transients and the gain balance authored into the WAVs.</summary>
    public static class FestivalRecordedAudio
    {
        const string ManifestPath = "ThirdPartySources/Audio/edits.json";
        [Serializable] sealed class Manifest { public Edit[] edits; }
        [Serializable] sealed class Edit { public string output; }

        public static HashSet<string> ClipNames()
        {
            if (!File.Exists(ManifestPath)) throw new FileNotFoundException("Recorded audio edit manifest is missing.", ManifestPath);
            var names = new HashSet<string>(JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)).edits
                .Select(edit => Path.GetFileNameWithoutExtension(edit.output)));
            names.Add("ui_select");
            return names;
        }

        public static void PreserveLevels(AudioImporter importer)
        {
            var serialized = new SerializedObject(importer);
            var normalize = serialized.FindProperty("m_Normalize");
            if (normalize == null) throw new InvalidOperationException("Cannot preserve recorded audio levels: normalize setting was not found.");
            normalize.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Everything about a sound that is not its recording, shared so that the audio refresh and the
        /// scene generator cannot disagree. A snap is a ladi report from a smaller charge: higher and shorter.
        /// The big sources are given a size, because a shell breaking across the sky is not a point.
        /// </summary>
        public static void ApplyVoicing(IEnumerable<SoundEntry> sounds)
        {
            foreach (var sound in sounds)
            {
                sound.pitch = sound.id == "snap" ? 1.45f : 1;
                sound.sourceRadius = sound.id == "night" ? 5 : sound.id == "burst" ? 6 : sound.id == "crackle" ? 8 : sound.id == "thunder" ? 12 : 0;
            }
        }

        [MenuItem("Diwali/Refresh Recorded Audio")]
        public static void Refresh()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before refreshing recorded audio.");
            AssetDatabase.Refresh();
            var names = ClipNames();
            foreach (string name in names)
            {
                string path = $"Assets/Diwali/Audio/{name}.wav";
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (!importer) throw new FileNotFoundException("Missing recorded clip", path);
                bool bed = name == "night_bed";
                var settings = importer.defaultSampleSettings;
                settings.loadType = bed ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = bed || name == "fuse_loop" ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                // Keep authored compression quality; PCM reports need no quality adjustment.
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                PreserveLevels(importer);
                importer.SaveAndReimport();
            }

            var bank = AssetDatabase.LoadAssetAtPath<FestivalSoundBank>("Assets/Diwali/FestivalSoundBank.asset");
            if (!bank) throw new InvalidOperationException("Festival sound bank is missing.");
            var bomb = Array.Find(bank.sounds, sound => sound.id == "bomb");
            bomb.clips = new[] { "bomb" }
                .Select(name => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Diwali/Audio/{name}.wav")).ToArray();
            bomb.reverbSendDb = -9;
            // Update the shared bank in place so both scenes gain the new
            // recordings without regenerating their objects or layout.
            var entries = bank.sounds.Where(sound => sound.id != "done" && sound.id != "whoosh" && sound.id != "hover").ToList();
            void Set(string id, float volume, float near, float send, params string[] clips)
            {
                var entry = entries.Find(sound => sound.id == id);
                if (entry == null) { entry = new SoundEntry { id = id }; entries.Add(entry); }
                entry.clips = clips.Select(name => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Diwali/Audio/{name}.wav")).ToArray();
                entry.volume = volume; entry.minDistance = near; entry.maxDistance = 200; entry.reverbSendDb = send;
            }
            Set("anar", 1, 0.9f, -3, "anar_loop");
            Set("chakri", 1, 0.8f, -3, "chakri_loop");
            Set("strike", 0.9f, 0.35f, -6, "lighter_strike_0", "lighter_strike_1");
            Set("lighter", 0.45f, 0.3f, -14, "lighter_loop");
            Set("ignite", 0.9f, 0.5f, -6, "ignite_soft_0", "ignite_soft_1");
            Set("flame", 0.5f, 0.3f, -12, "flame_loop");
            Set("select", 0.6f, 0.6f, -60, "ui_select");
            Set("lift", 0.45f, 0.6f, -60, "ui_pickup_soft");
            Set("place", 1, 0.7f, -9, "ui_place");
            Set("butterfly", 0.9f, 0.65f, -6, "butterfly_loop", "butterfly_loop_1");
            Set("rocket_motor", 1, 1.4f, -3, "rocket_motor_0", "rocket_motor_1");
            Set("cap", 1, 1, -3, "cap_1", "cap_2");
            Set("snap", 0.9f, 1.2f, -3, "pop_0", "pop_1", "pop_2");
            Set("launch", 0.9f, 12, -6, "mortar_0", "mortar_1");
            ApplyVoicing(entries);
            bank.sounds = entries.ToArray();
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();

            var lines = new List<string> { "Recorded audio import validation", "UTC: " + DateTime.UtcNow.ToString("O") };
            foreach (var sound in bank.sounds)
            {
                if (sound.clips == null || sound.clips.Length == 0 || sound.clips.Any(clip => !clip))
                    throw new InvalidOperationException("Missing clip in sound bank: " + sound.id);
            }
            foreach (string name in names.OrderBy(name => name))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Diwali/Audio/{name}.wav");
                if (!clip || clip.channels != 1 || clip.frequency != 48000 || clip.length <= 0)
                    throw new InvalidOperationException("Invalid recorded clip: " + name);
                if (name == "night_bed")
                {
                    lines.Add($"PASS {name}: mono {clip.frequency} Hz, {clip.length:F3}s, compressed ambience (WAV waveform checked before import)");
                    continue;
                }
                clip.LoadAudioData();
                var samples = new float[clip.samples];
                if (!clip.GetData(samples, 0) || !samples.Any(value => Mathf.Abs(value) > 0.0001f))
                    throw new InvalidOperationException("Silent or unreadable clip: " + name);
                float peak = samples.Max(value => Mathf.Abs(value));
                if (peak >= 0.99f) throw new InvalidOperationException("Recorded clip has lost headroom: " + name);
                lines.Add($"PASS {name}: mono {clip.frequency} Hz, {clip.length:F3}s, peak {peak:F4}");
            }
            lines.Add($"PASS {names.Count} sourced clips imported; all sound bank references resolve; bomb uses one recorded firecracker report.");
            Directory.CreateDirectory("Verification/Audio");
            File.WriteAllLines("Verification/Audio/unity-import-validation.txt", lines);
            Debug.Log("[Diwali] Recorded audio refreshed and validated.");
        }
    }
}
