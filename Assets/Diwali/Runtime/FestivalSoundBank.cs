using System;
using UnityEngine;

namespace Diwali
{
    [Serializable]
    public sealed class SoundEntry
    {
        public string id;
        public AudioClip[] clips;
        [Range(0, 1)] public float volume = 1;
        [Tooltip("Distance at which the sound plays at full level. Loud, large sources get a larger value.")]
        public float minDistance = 0.6f;
        public float maxDistance = 120;
        [Tooltip("Send to the room model in dB. Interface sounds stay dry, blasts ring in the room.")]
        public float reverbSendDb = 0;
        [Tooltip("Playback rate. A smaller charge makes the same crack higher and shorter.")]
        public float pitch = 1;
        [Tooltip("Size of the source in metres. A shell breaking across the sky is not a point; zero is.")]
        public float sourceRadius = 0;
    }

    public sealed class FestivalSoundBank : ScriptableObject
    {
        public SoundEntry[] sounds;
    }
}
