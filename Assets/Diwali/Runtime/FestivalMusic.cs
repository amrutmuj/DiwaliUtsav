using UnityEngine;

namespace Diwali
{
    /// <summary>Scene settings for one persistent score and transition-cue player.</summary>
    [DefaultExecutionOrder(-35)]
    public sealed class FestivalMusic : MonoBehaviour
    {
        public AudioClip backgroundTrack;
        public AudioClip openingJingle;
        public bool launchScreen;
        [Range(0, 0.25f)] public float splashLevel = 0.12f;
        [Range(0, 0.25f)] public float activityLevel = 0.075f;
        [Range(0, 0.1f)] public float duckedLevel = 0.025f;
        [Range(0, 0.5f)] public float jingleLevel = 0.26f;

        FestivalMusicPlayback playback;
        public bool Muted => playback && playback.Muted;
        public bool Suspended => playback && playback.Suspended;
        public AudioSource Track => playback ? playback.Track : null;
        public AudioSource Jingle => playback ? playback.Jingle : null;

        void Awake() => playback = FestivalMusicPlayback.Connect(this);
        void Start() => OpenSplash(false);
        public void OpenSplash(bool playSignature) { if (playback) playback.Open(playSignature); }
        public void PlaySignature() { if (playback) playback.PlaySignature(); }
        public void Duck(float seconds) { if (playback) playback.Duck(seconds); }
        public void ToggleMute() => SetMuted(!Muted);
        public void SetMuted(bool value) { if (playback) playback.SetMuted(value); }
        public void SetHeadset(bool present) { if (playback) playback.SetHeadset(present); }
        // Scene unloading destroys only these settings. Playback owns its own
        // root object, so neither the score nor the transition tail is cut off.
    }
}
