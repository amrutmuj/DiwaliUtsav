using UnityEngine;

namespace Diwali
{
    /// <summary>One audio owner across scene loads; never preserves a scene rig or session.</summary>
    public sealed class FestivalMusicPlayback : MonoBehaviour
    {
        const string MuteKey = "DiwaliUtsav.MusicMuted";
        static FestivalMusicPlayback instance;
        public AudioSource Track { get; private set; }
        public AudioSource Jingle { get; private set; }
        public bool Muted { get; private set; }
        public bool Suspended => applicationPaused || headsetAway;
        bool applicationPaused, headsetAway, wasSuspended, opened, hadHeadset, launchScreen;
        bool leavingSplash;
        float duckUntil, splashLevel, activityLevel, duckedLevel, jingleLevel;

        public static FestivalMusicPlayback Connect(FestivalMusic settings)
        {
            if (!instance)
            {
                instance = new GameObject("Festival music • continuous playback").AddComponent<FestivalMusicPlayback>();
                DontDestroyOnLoad(instance.gameObject);
                instance.Muted = PlayerPrefs.GetInt(MuteKey, 0) != 0;
                instance.Track = instance.MakeSource("A Little Light • 120 second score", true, 240);
                instance.Jingle = instance.MakeSource("Diwali Utsav • transition cue", false, 180);
            }
            instance.Configure(settings);
            return instance;
        }

        AudioSource MakeSource(string label, bool loop, int priority)
        {
            var source = new GameObject(label).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.playOnAwake = false; source.loop = loop;
            source.spatialBlend = 0; source.spatialize = false;
            source.dopplerLevel = 0; source.priority = priority; source.volume = 0;
            return source;
        }

        void Configure(FestivalMusic settings)
        {
            launchScreen = settings.launchScreen;
            if (launchScreen) leavingSplash = false;
            splashLevel = settings.splashLevel; activityLevel = settings.activityLevel;
            duckedLevel = settings.duckedLevel; jingleLevel = settings.jingleLevel;
            // Rebinding a scene must not restart the same clip or stop a cue.
            if (Track.clip != settings.backgroundTrack) { Track.Stop(); Track.clip = settings.backgroundTrack; opened = false; }
            if (Jingle.clip != settings.openingJingle) { Jingle.Stop(); Jingle.clip = settings.openingJingle; }
        }

        public void Open(bool playSignature)
        {
            if (!opened)
            {
                opened = true;
                if (launchScreen && !leavingSplash && Track.clip && !Muted && !Suspended) Track.Play();
            }
            if (playSignature) PlaySignature();
        }

        public void PlaySignature()
        {
            leavingSplash = true;
            if (Muted || Suspended || !Jingle.clip) return;
            Jingle.Stop();
            Jingle.volume = jingleLevel * GentleScale;
            Jingle.Play();
            Duck(Jingle.clip.length + 0.2f);
        }

        float GentleScale => DiwaliSession.Instance && DiwaliSession.Instance.Gentle ? DiwaliSession.Instance.sound.gentleLevel : 1;
        public void Duck(float seconds) => duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + seconds);
        public void SetMuted(bool value)
        {
            Muted = value;
            PlayerPrefs.SetInt(MuteKey, value ? 1 : 0); PlayerPrefs.Save();
            if (value) { Track.Pause(); Jingle.Stop(); Track.volume = 0; }
            else if (opened && !Suspended && launchScreen && !leavingSplash)
            {
                Track.UnPause();
                if (!Track.isPlaying && Track.clip) Track.Play();
            }
        }

        public void SetHeadset(bool present)
        {
            hadHeadset |= present;
            headsetAway = hadHeadset && !present;
            ApplySuspension();
        }
        void OnApplicationPause(bool paused) { applicationPaused = paused; ApplySuspension(); }
        void ApplySuspension()
        {
            if (!Track || wasSuspended == Suspended) return;
            wasSuspended = Suspended;
            if (Suspended) { Track.Pause(); Jingle.Pause(); }
            else if (!Muted && opened)
            {
                Jingle.UnPause();
                if (launchScreen && !leavingSplash)
                {
                    Track.UnPause();
                    if (!Track.isPlaying && Track.clip) Track.Play();
                }
            }
        }

        void Update()
        {
            if (!Track || Suspended) return;
            var session = DiwaliSession.Instance;
            bool action = session && session.sky.ShowRunning;
            float target = Muted || !launchScreen || leavingSplash ? 0 : (action || Time.unscaledTime < duckUntil || Jingle.isPlaying)
                ? duckedLevel : launchScreen ? splashLevel : activityLevel;
            target *= GentleScale;
            Track.volume = Mathf.MoveTowards(Track.volume, target, Time.unscaledDeltaTime * (target < Track.volume ? 0.5f : 0.035f));
            if ((!launchScreen || leavingSplash) && Track.volume <= 0 && Track.isPlaying) Track.Stop();
            if (Jingle.isPlaying) Jingle.volume = jingleLevel * GentleScale;
        }

        void OnDestroy() { if (instance == this) instance = null; }
    }
}
