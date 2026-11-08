using System;
using System.Collections.Generic;
using UnityEngine;

namespace Diwali
{
    /// <summary>A sound that follows its source, with a controllable fade.</summary>
    public sealed class SoundLoop
    {
        internal AudioSource source;
        internal Transform follow;
        internal float level, target, baseVolume, fadeRate, hush = 1;
        internal bool stopping, following, hushed;

        public bool Alive => source;

        /// <summary>Ignored once stopping, so a routine still driving a swell cannot bring a stopped sound back.</summary>
        public void SetLevel(float value)
        {
            if (!stopping) target = Mathf.Clamp01(value);
        }

        public void SetPitch(float value)
        {
            if (source) source.pitch = value;
        }

        public void Stop(float fadeSeconds = 0.3f)
        {
            target = 0;
            stopping = true;
            fadeRate = 1 / Mathf.Max(fadeSeconds, 0.01f);
        }
    }

    /// <summary>When and where a sound last played, and how many times it has.</summary>
    public struct SoundCue
    {
        public int frame, count;
        public Vector3 position;
    }

    /// <summary>
    /// Every sound has a real position in the room and goes through Meta XR Audio's HRTF.
    /// The room model is sized from the scanned room, so reflections match the walls you can see.
    /// </summary>
    public sealed class FestivalAudio : MonoBehaviour
    {
        const int OneShotVoices = 48;

        // One for everything that can burn at once: a tray of lit diyas must not take the voice of a fuse.
        // Above Unity's real voice count the quietest simply goes virtual, and returns when there is room.
        const int LoopVoices = 28;
        const float HushSeconds = 0.08f;

        public FestivalSoundBank bank;
        public MetaXRAudioRoomAcousticProperties room;
        [Range(0, 1)] public float gentleLevel = 0.5f;
        public float bedLevel = 0.5f;

        public float MasterLevel { get; private set; } = 1;

        readonly Dictionary<string, SoundEntry> sounds = new Dictionary<string, SoundEntry>();
        readonly List<SoundLoop> loops = new List<SoundLoop>();
        readonly Dictionary<AudioSource, MetaXRAudioSource> spatial = new Dictionary<AudioSource, MetaXRAudioSource>();
        readonly Dictionary<AudioSource, AudioLowPassFilter> toneFilters = new Dictionary<AudioSource, AudioLowPassFilter>();
        readonly Dictionary<AudioSource, double> reservedUntil = new Dictionary<AudioSource, double>();
        readonly Dictionary<string, SoundCue> cues = new Dictionary<string, SoundCue>();
        AudioSource[] voices;
        AudioSource[] loopVoices;
        SoundLoop bed;
        Transform bedOrigin;
        int cursor;

        void Awake()
        {
            foreach (var sound in bank.sounds) sounds[sound.id] = sound;
            voices = CreateVoices("Spatial voice", OneShotVoices, false);
            loopVoices = CreateVoices("Spatial loop", LoopVoices, true);
            bedOrigin = new GameObject("Distant celebration").transform;
            bedOrigin.SetParent(transform, false);
            if (room)
            {
                room.lockPositionToListener = true;
                SetRoomSize(new Vector3(4.5f, 2.6f, 4.5f), Vector3.zero, false);
            }
        }

        AudioSource[] CreateVoices(string label, int count, bool looping)
        {
            var created = new AudioSource[count];
            for (int i = 0; i < count; i++)
            {
                var voice = new GameObject($"{label} {i}").AddComponent<AudioSource>();
                voice.transform.SetParent(transform, false);
                voice.playOnAwake = false;
                voice.loop = looping;
                voice.spatialBlend = 1;
                voice.spatialize = true;
                voice.spatializePostEffects = true;
                voice.dopplerLevel = 0;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;
                var tone = voice.gameObject.AddComponent<AudioLowPassFilter>();
                tone.cutoffFrequency = 3200;
                tone.lowpassResonanceQ = 1;
                tone.enabled = false;
                toneFilters[voice] = tone;
                var meta = voice.gameObject.AddComponent<MetaXRAudioSource>();
                meta.EnableSpatialization = true;
                meta.EnableAcoustics = true;
                spatial[voice] = meta;
                created[i] = voice;
            }
            return created;
        }

        /// <summary>Gentle mode lowers everything together, including what is already sounding, so balance and direction stay intact.</summary>
        public void SetGentle(bool gentle)
        {
            float level = gentle ? gentleLevel : 1;
            float change = level / MasterLevel;
            foreach (var voice in voices) if (voice.isPlaying) voice.volume *= change;
            MasterLevel = level;
        }

        void Configure(AudioSource voice, SoundEntry sound)
        {
            voice.minDistance = sound.minDistance;
            voice.maxDistance = sound.maxDistance;
            spatial[voice].ReverbSendDb = sound.reverbSendDb;
            // The glitter tail needs a softer top end. Reset this on every pooled voice reuse,
            // so a later report, tool or interface cue keeps its original tone.
            toneFilters[voice].enabled = sound.id == "crackle";
        }

        /// <param name="pitch">Multiplies the sound's own pitch, for a material or size that differs from the recording.</param>
        public void Play(string id, Vector3 position, float volume = 1, float pitchVariation = 0.05f, float delay = 0, float pitch = 1)
        {
            if (!sounds.TryGetValue(id, out var sound) || sound.clips.Length == 0)
            {
                Debug.LogWarning("[Diwali] Missing sound: " + id);
                return;
            }

            var voice = NextVoice();
            voice.Stop();
            Configure(voice, sound);
            voice.transform.position = position;
            voice.clip = sound.clips[UnityEngine.Random.Range(0, sound.clips.Length)];
            voice.volume = sound.volume * volume * MasterLevel;
            voice.pitch = sound.pitch * pitch * (1 + UnityEngine.Random.Range(-pitchVariation, pitchVariation));
            voice.priority = id == "bomb" ? 24 : id == "burst" ? 48 : id == "pop" || id == "snap" ? 80 : 160;
            reservedUntil[voice] = AudioSettings.dspTime + delay + voice.clip.length / voice.pitch;
            if (id != "lift" && id != "place" && id != "select" && id != "hover" && DiwaliSession.Instance && DiwaliSession.Instance.music)
                DiwaliSession.Instance.music.Duck(delay + voice.clip.length / voice.pitch + 0.8f);
            if (delay > 0.005f) voice.PlayDelayed(delay);
            else voice.Play();
            ApplySize(voice, sound);
            cues[id] = new SoundCue { frame = Time.frameCount, count = LastCue(id).count + 1, position = position };
        }

        /// <summary>The spatializer instance only exists once a voice is playing, so this follows Play.</summary>
        static void ApplySize(AudioSource voice, SoundEntry sound) =>
            voice.SetSpatializerFloat((int)MetaXRAudioSource.NativeParameterIndex.P_RADIUS, sound.sourceRadius);

        public SoundCue LastCue(string id) => cues.TryGetValue(id, out var cue) ? cue : default;

        AudioSource NextVoice()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                var candidate = voices[(cursor + i) % voices.Length];
                if (candidate.isPlaying || (reservedUntil.TryGetValue(candidate, out double until) && until > AudioSettings.dspTime)) continue;
                cursor += i + 1;
                return candidate;
            }
            var replace = voices[0];
            foreach (var voice in voices)
                if (voice.priority > replace.priority || (voice.priority == replace.priority && voice.volume < replace.volume)) replace = voice;
            return replace;
        }

        /// <param name="fromStart">For a sound with a shape of its own. Steady loops start at a random point.</param>
        public SoundLoop StartLoop(string id, Transform follow, float level = 1, float fadeSeconds = 0.25f, bool fromStart = false)
            => StartFollowing(id, follow, level, fadeSeconds, fromStart, true);

        /// <summary>Plays a recording once while it follows a moving rocket or shell.</summary>
        public SoundLoop PlayFollowing(string id, Transform follow, float level = 1, float fadeSeconds = 0.01f)
            => StartFollowing(id, follow, level, fadeSeconds, true, false);

        SoundLoop StartFollowing(string id, Transform follow, float level, float fadeSeconds, bool fromStart, bool looping)
        {
            if (!sounds.TryGetValue(id, out var sound) || sound.clips.Length == 0)
            {
                Debug.LogWarning("[Diwali] Missing sound: " + id);
                return new SoundLoop();
            }

            var voice = Array.Find(loopVoices, candidate => !loops.Exists(loop => loop.source == candidate));
            if (!voice)
            {
                var quietest = loops[0];
                foreach (var loop in loops) if (loop.level < quietest.level) quietest = loop;
                voice = quietest.source;
                quietest.source = null;
                loops.Remove(quietest);
            }

            Configure(voice, sound);
            voice.clip = sound.clips[UnityEngine.Random.Range(0, sound.clips.Length)];
            voice.loop = looping;
            voice.dopplerLevel = id == "rocket_motor" || id == "whistle" || id == "butterfly" ? 0.45f : 0;
            voice.pitch = sound.pitch;
            voice.volume = 0;

            // Anything that keeps sounding outranks a short report, or a long ladi would cut out a fountain.
            voice.priority = id == "flame" ? 200 : id == "night" ? 150 : 64;
            voice.time = fromStart ? 0 : UnityEngine.Random.Range(0, voice.clip.length * 0.8f);
            voice.transform.position = follow ? follow.position : voice.transform.position;
            voice.Play();
            ApplySize(voice, sound);
            cues[id] = new SoundCue { frame = Time.frameCount, count = LastCue(id).count + 1, position = voice.transform.position };

            var handle = new SoundLoop
            {
                source = voice, follow = follow, following = follow, target = level, baseVolume = sound.volume,
                fadeRate = 1 / Mathf.Max(fadeSeconds, 0.01f),
            };
            loops.Add(handle);
            return handle;
        }

        void Update()
        {
            bool activitySound = false;
            for (int i = loops.Count - 1; i >= 0; i--)
            {
                var loop = loops[i];
                if (!loop.source || (!loop.source.loop && !loop.source.isPlaying))
                {
                    loop.source = null;
                    loops.RemoveAt(i);
                    continue;
                }

                // Whatever this sound belonged to has gone, and nothing is left to stop it.
                if (loop.following && !loop.follow && !loop.stopping) loop.Stop(0.1f);
                loop.level = Mathf.MoveTowards(loop.level, loop.target, Time.deltaTime * loop.fadeRate);
                loop.hush = Mathf.MoveTowards(loop.hush, loop.hushed ? 0 : 1, Time.deltaTime / HushSeconds);
                float heard = loop.level * loop.hush;
                loop.source.volume = heard * loop.baseVolume * MasterLevel;
                if (loop != bed && heard > 0.02f) activitySound = true;
                if (loop.follow) loop.source.transform.position = loop.follow.position;
                if (loop.stopping && loop.level <= 0)
                {
                    loop.source.Stop();
                    loop.source = null;
                    loops.RemoveAt(i);
                }
            }
            if (activitySound && DiwaliSession.Instance && DiwaliSession.Instance.music) DiwaliSession.Instance.music.Duck(0.8f);
        }

        /// <summary>Matches the acoustic model to the real room, so a blast rings in the space you are standing in.</summary>
        public void SetRoomSize(Vector3 size, Vector3 centre, bool measured)
        {
            if (!room) return;
            room.lockPositionToListener = !measured;
            room.transform.position = centre;
            room.width = Mathf.Clamp(size.x, 1.5f, 20);
            room.height = Mathf.Clamp(size.y, 2, 6);
            room.depth = Mathf.Clamp(size.z, 1.5f, 20);
        }

        /// <summary>The far-off neighbourhood. In a scanned room it comes in through your actual window.</summary>
        public void StartNightBed(Vector3 from)
        {
            bedOrigin.position = from;
            if (bed == null || !bed.Alive) bed = StartLoop("night", bedOrigin, bedLevel, 3);
        }

        public void StopAll()
        {
            foreach (var voice in voices) voice.Stop();
            reservedUntil.Clear();
            foreach (var loop in loops) loop.Stop(0.2f);
            bed = null;
        }

        public void StopFor(Transform root)
        {
            foreach (var loop in loops)
                if (Belongs(loop, root)) loop.Stop(.05f);
        }

        /// <summary>Silences an object's sounds without ending them, so they can resume if it comes back.</summary>
        public void Hush(Transform root, bool on)
        {
            foreach (var loop in loops)
                if (Belongs(loop, root)) loop.hushed = on;
        }

        static bool Belongs(SoundLoop loop, Transform root) => loop.follow && (loop.follow == root || loop.follow.IsChildOf(root));
    }
}
