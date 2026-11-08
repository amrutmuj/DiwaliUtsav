using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Diwali.Editor
{
    /// <summary>Checks launch, real pointer selection, scene loading and repeated experience resets.</summary>
    [InitializeOnLoad]
    public static class DiwaliSplashReview
    {
        const string Output = "Verification/Splash";
        const string PendingKey = "DiwaliUtsav.SplashReview";
        static bool running, savedMute;
        static int step, errors, oldSessionId, experienceLoads, launchLoads;
        static double next, deadline;
        static float initialMotion;
        static AudioSource launchTrack, transitionCue;
        static int trackBeforeEntry;
        static double cueStarted;
        static float mixPeak;
        static int clippedSamples;
        static FestivalPointer toolLeft, toolRight;
        static float coatingBefore;

        static DiwaliSplashReview()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += Log;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        [MenuItem("Diwali/Validate Splash Scene Flow")]
        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before reviewing the splash.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/playmode-validation.txt", "Diwali launch and experience scene validation\n");
            File.WriteAllText(Output + "/runtime-errors.txt", "");
            DiwaliLinkSetup.UseDesktopForNextPlay();
            EditorSceneManager.OpenScene(DiwaliSceneBuilder.SplashScenePath);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!running) return;
            if (scene.name == "SplashScreen") launchLoads++;
            if (scene.name == DiwaliSession.ExperienceSceneName) experienceLoads++;
        }

        static void Log(string text, string stack, LogType type)
        {
            if (running && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            {
                errors++;
                File.AppendAllText(Output + "/runtime-errors.txt", text + "\n" + stack + "\n");
            }
        }

        static void Check(bool passed, string message)
        {
            if (!passed) throw new Exception(message);
            File.AppendAllText(Output + "/playmode-validation.txt", "PASS " + message + "\n");
        }

        static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool(PendingKey, false) && EditorApplication.isPlaying)
            {
                SessionState.SetBool(PendingKey, false);
                running = true; step = errors = experienceLoads = 0; launchLoads = 1;
                mixPeak = 0; clippedSamples = 0;
                next = EditorApplication.timeSinceStartup + 2;
                deadline = next + 30;
                savedMute = PlayerPrefs.GetInt("DiwaliUtsav.MusicMuted", 0) != 0;
            }
            if (!running) return;
            if (!EditorApplication.isPlaying) { Finish(); return; }
            ReadMix();
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Scene flow review timed out at step " + step);
                Step();
            }
            catch (Exception ex)
            {
                File.AppendAllText(Output + "/playmode-validation.txt", "FAILED " + ex + "\n");
                Finish(); Debug.LogException(ex);
            }
        }

        static void Step()
        {
            var splash = Object.FindFirstObjectByType<FestivalSplash>();
            var s = DiwaliSession.Instance;
            switch (step)
            {
                case 0:
                    splash.music.SetMuted(false);
                    Check(SceneManager.GetActiveScene().name == "SplashScreen", "Application launch opens the separate SplashScreen scene");
                    Check(!s && !Object.FindFirstObjectByType<FestivalPlacement>(), "The celebration and its activities are not instantiated behind the launch screen");
                    Check(splash && splash.rig && splash.music && splash.diyaLight, "The launch screen has its own rig, music and ignition light");
                    Check(splash.diyaLight.enabled && splash.art.ignition == 1, "The launch diya is already lit before any input");
                    Check(!splash.rig.view.useOcclusionCulling && splash.art.material.shader.name == "Diwali/Splash UI" &&
                        splash.title.fontSharedMaterial.shader.name.EndsWith("Overlay"), "Splash artwork and text use depth-independent rendering");
                    var scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray();
                    Check(scenes.SequenceEqual(new[] { DiwaliSceneBuilder.SplashScenePath, DiwaliSceneBuilder.MRScenePath }), "Build scene order is SplashScreen then DiwaliExperience");
                    Check(splash.music.backgroundTrack && Mathf.Abs(splash.music.backgroundTrack.length - 120) < .01f, "The two-minute music remains available without delaying entry");
                    splash.rig.view.transform.localRotation = Quaternion.identity;
                    Canvas.ForceUpdateCanvases();
                    Check(!splash.title.isTextOverflowing && !splash.description.isTextOverflowing, "Launch text fits its bounds");
                    initialMotion = splash.art.MotionTime;
                    Capture(splash.rig.view, "01-launch");
                    step++; next = EditorApplication.timeSinceStartup + 1.1; return;
                case 1:
                    Check(splash.art.MotionTime > initialMotion + .5f, "The launch diorama animates while waiting for input");
                    Check(splash.diyaLight.enabled && splash.art.ignition == 1, "The diya remains lit while waiting without any button press");
                    var card = splash.GetComponent<WelcomeCard>();
                    Quaternion cardRotation = card.transform.rotation;
                    Vector3 headPosition = splash.rig.view.transform.position;
                    splash.rig.view.transform.position += Vector3.right * .015f;
                    typeof(WelcomeCard).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(card, null);
                    Check(Quaternion.Angle(cardRotation, card.transform.rotation) < .01f, "Small head movements leave the splash orientation stable in the room");
                    splash.rig.view.transform.position = headPosition;
                    var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    blocker.name = "Splash depth regression occluder";
                    blocker.transform.position = Vector3.Lerp(headPosition, card.transform.position, .75f);
                    blocker.transform.localScale = new Vector3(1.1f, .7f, .02f);
                    Capture(splash.rig.view, "01a-depth-overlap");
                    Object.DestroyImmediate(blocker);
                    var button = splash.GetComponentsInChildren<FestivalSplashButton>().First(b => b.background);
                    var pointer = new FestivalPointer { Tracked = true, Grasp = new OccupiedHand() };
                    button.Press(pointer);
                    Check(!splash.Transitioning && splash.diyaLight.enabled, "An occupied hand cannot accidentally enter or extinguish the diya");
                    pointer.Grasp = null;
                    Physics.SyncTransforms();
                    pointer.Ray = new Ray(splash.rig.view.transform.position, (button.transform.position - splash.rig.view.transform.position).normalized);
                    object[] args = { pointer, Vector3.zero, false };
                    var target = typeof(FestivalRig).GetMethod("FindTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(splash.rig, args);
                    Check(ReferenceEquals(target, button), "The shared pointer ray reaches the launch button");
                    launchTrack = splash.music.Track; transitionCue = splash.music.Jingle;
                    trackBeforeEntry = launchTrack.timeSamples;
                    cueStarted = AudioSettings.dspTime;
                    button.Press(pointer);
                    Check(splash.Transitioning && splash.diyaLight.enabled && splash.art.ignition == 1, "The diya stays illuminated while entry starts");
                    Check(transitionCue.isPlaying && transitionCue.clip.name == "ui_lift" && transitionCue.clip.length < .3f,
                        "Begin plays one short soft confirmation instead of the musical jingle");
                    button.Press(pointer);
                    Check(splash.Transitioning, "Repeated presses are ignored during entry");
                    step++; next = EditorApplication.timeSinceStartup + .2; return;
                case 2:
                    if (splash)
                    {
                        Check(splash.art.ignition == 1 && splash.diyaLight.enabled, "Entry preserves the continuously burning diya");
                        Capture(splash.rig.view, "02-diya-lit");
                    }
                    step++; return;
                case 3:
                    if (!s || !s.Started) return;
                    CheckExperience(s);
                    Check(experienceLoads == 1, "One button activation instantiates exactly one DiwaliExperience scene");
                    Check(s.music && !s.music.launchScreen && s.music.Track == launchTrack && s.music.Jingle == transitionCue,
                        "The same music and transition sources survive the scene change");
                    Check(s.music.Track.volume < .001f && !s.music.Track.isPlaying,
                        "The splash score fades out on Begin and stays silent in the experience");
                    Vector3 trayOffset = Vector3.ProjectOnPlane(s.thali.transform.position - s.rig.view.transform.position, Vector3.up);
                    Check(Mathf.Abs(Mathf.DeltaAngle(Vector3.SignedAngle(Vector3.ProjectOnPlane(s.rig.view.transform.forward, Vector3.up), trayOffset, Vector3.up), s.thali.dockYaw)) < 1 &&
                        Mathf.Abs(trayOffset.magnitude - s.thali.reach) < .02f,
                        "The menu starts at its saved angular offset and reach");
                    Vector3 trayBeforeGlance = s.thali.transform.position;
                    Quaternion headBeforeGlance = s.rig.view.transform.rotation;
                    s.rig.view.transform.rotation = Quaternion.AngleAxis(-65, Vector3.up) * headBeforeGlance;
                    typeof(ThaliMenu).GetMethod("Follow", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.thali, null);
                    Check(Vector3.Distance(trayBeforeGlance, s.thali.transform.position) < .005f, "Looking left at the menu does not move it into the interaction area");
                    s.rig.view.transform.rotation = headBeforeGlance;
                    Check(Object.FindObjectsByType<FestivalMusicPlayback>(FindObjectsSortMode.None).Length == 1,
                        "There is exactly one persistent music player");
                    if (AudioSettings.dspTime - cueStarted < transitionCue.clip.length - 0.1)
                        Check(transitionCue.isPlaying && transitionCue.timeSamples > 0, "The transition cue is still audible in the experience scene");
                    Capture(s.rig.view, "03-experience");
                    step = 6; return;
                case 6:
                    if (transitionCue.isPlaying) return;
                    Check(AudioSettings.dspTime - cueStarted >= transitionCue.clip.length - .1,
                        "The transition cue plays its full natural duration without being cut short");
                    Check(!s.thali.plate.Find("Plate surface/Tool rest") && !s.thali.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith("Step ")),
                        "The Tool rest panel and every menu progress dot are removed");
                    s.rig.enabled = false;
                    CheckRemoval(s);
                    toolLeft = new FestivalPointer { Tracked = true, IsHand = true, Pressed = true, Index = 0, PinchPoint = new Vector3(-.25f, 1.2f, .8f), Ray = new Ray(new Vector3(-.25f, 1.2f, .8f), Vector3.forward) };
                    toolRight = new FestivalPointer { Tracked = true, IsHand = true, Pressed = true, Index = 1, PinchPoint = new Vector3(.25f, 1.2f, .8f), Ray = new Ray(new Vector3(.25f, 1.2f, .8f), Vector3.forward) };
                    s.lighter.Take(toolLeft); s.lighter.Steer(toolLeft);
                    s.sparkler.Take(toolRight); s.sparkler.Steer(toolRight); s.sparkler.Ignite();
                    foreach (var rotation in new[] { Quaternion.Euler(35, 60, 95), Quaternion.Euler(-70, 120, -110) })
                    {
                        toolLeft.Rotation = toolRight.Rotation = rotation;
                        s.lighter.Steer(toolLeft); s.sparkler.Steer(toolRight);
                        Check(Quaternion.Angle(s.lighter.transform.rotation, rotation * Quaternion.Euler(s.lighter.gripRotation)) < .05f &&
                            Quaternion.Angle(s.sparkler.transform.rotation, rotation * Quaternion.Euler(s.sparkler.gripRotation)) < .05f,
                            "Lighter and phuljhadi follow wrist pitch, yaw and roll with their grip alignment");
                    }
                    Check(s.lighter.FlameOn && s.sparkler.Lit, "Taking the lighter lights it, and both tools remain usable while held");
                    Check(s.Guidance(out _).Contains("Open a hand"), "Guidance explains opening the hand and picking the tool up again");
                    step = 7; next = EditorApplication.timeSinceStartup + .7; return;
                case 7:
                    Canvas.ForceUpdateCanvases();
                    var showTile = s.thali.tiles.Single(tile => tile.id == CrackerId.GrandShow);
                    Check(showTile.showImage && showTile.showImage.sprite && showTile.showLabel.text == "Grand Show", "Grand Show uses the generated image sprite and a readable label");
                    var view = s.rig.view.transform; var savedPosition = view.position; var savedRotation = view.rotation;
                    view.position = s.thali.plate.position - s.thali.plate.forward * .8f;
                    view.LookAt(s.thali.plate.position, s.thali.plate.up);
                    Capture(s.rig.view, "05-revised-menu");
                    view.SetPositionAndRotation(savedPosition, savedRotation);
                    coatingBefore = s.sparkler.coating.localScale.y;
                    toolLeft.Pressed = toolRight.Pressed = false;
                    s.lighter.Steer(toolLeft);
                    s.sparkler.Steer(toolRight);
                    Check(toolLeft.Grasp == null && toolRight.Grasp == null && s.lighter.Dropped && s.sparkler.Dropped, "Opening both hands drops both tools and immediately frees the hands");
                    Check(!s.lighter.FlameOn && s.lighter.flame.Level == 0 && s.sparkler.Lit, "Releasing the lighter immediately extinguishes it while phuljhadi keeps burning");
                    step = 9; next = EditorApplication.timeSinceStartup + .9; return;
                case 9:
                    Check(!s.lighter.gameObject.activeSelf && !s.lighter.Dropped && !s.lighter.FlameOn, "A released lighter is gone the moment it reaches the floor");
                    Check(s.sparkler.Lit && s.sparkler.coating.localScale.y < coatingBefore, "Phuljhadi continues burning while it is on the floor");
                    Check(Mathf.Abs(s.sparkler.transform.up.y) < .02f && !s.sparkler.GetComponent<FestivalToolDrop>().Moving,
                        "Dropped phuljhadi settles flat on the ground rather than retaining the hand angle");
                    toolLeft.Pressed = toolRight.Pressed = true;
                    toolRight.PinchPoint = s.sparkler.GetComponent<GrabZone>().WorldCentre;
                    int strikes = s.sound.LastCue("strike").count;
                    Check(s.sparkler.CanGrab(toolRight), "A dropped phuljhadi stays grabbable");
                    s.sparkler.Press(toolRight);
                    s.Take(CrackerId.Lighter, toolLeft);
                    Check(s.lighter.Holder == toolLeft && s.sparkler.Holder == toolRight && s.lighter.FlameOn && s.sparkler.Lit,
                        "The tray tile gives the lighter back, and a phuljhadi picked up from the floor keeps its burn");
                    Check(s.sound.LastCue("strike").count == strikes + 1, "Taking the lighter from the tray lights it again");
                    s.sparkler.burnSeconds = .25f;
                    step = 10; next = EditorApplication.timeSinceStartup + .4; return;
                case 10:
                    Check(Object.FindObjectsByType<LighterItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                        "There is only ever one lighter");
                    Check(!s.sparkler.gameObject.activeSelf && toolRight.Grasp == null, "A spent phuljhadi clears itself and frees its hand");
                    s.sparkler.burnSeconds = 26;
                    s.Take(CrackerId.Phuljhadi, toolRight);
                    Check(s.sparkler.Holder == toolRight && !s.sparkler.Lit && s.sparkler.coating.localScale.y > coatingBefore,
                        "The phuljhadi tile immediately supplies a fresh replacement after burnout");
                    toolLeft.Pressed = false; s.lighter.Steer(toolLeft);
                    toolLeft.Pressed = true; s.Take(CrackerId.Anar, toolLeft);
                    Check(toolLeft.Grasp != null && toolLeft.Held == null, "A hand freed by dropping a tool can take another cracker");
                    Check(mixPeak < 1 && clippedSamples == 0, "Tool pickup, drop and recovery remain below digital clipping in the measured mix");
                    File.AppendAllText(Output + "/playmode-validation.txt", $"INFO sampled tool stereo mix peak {mixPeak:F6}; clipped samples {clippedSamples}\n");
                    var showButton = s.thali.tiles.Single(tile => tile.id == CrackerId.GrandShow);
                    showButton.Press(null);
                    Check(s.sky.ShowRunning && !showButton.ActionAvailable, "Grand Show becomes unavailable immediately when its experience starts");
                    showButton.Press(null);
                    Time.timeScale = 6;
                    step = 11; next = EditorApplication.timeSinceStartup + .2; deadline = EditorApplication.timeSinceStartup + 35; return;
                case 11:
                    var activeShow = s.thali.tiles.Single(tile => tile.id == CrackerId.GrandShow);
                    Check(activeShow.showLabel.text == "Playing…" && activeShow.showImage.color.a < .5f, "The Grand Show sprite stays visible and clearly shows the running state");
                    step = 12; return;
                case 12:
                    if (s.sky.ShowRunning) return;
                    Time.timeScale = 1;
                    var completedShow = s.thali.tiles.Single(tile => tile.id == CrackerId.GrandShow);
                    Check(completedShow.ActionAvailable, "Grand Show becomes selectable again only after the full experience completes");
                    completedShow.Press(null);
                    Check(s.sky.ShowRunning, "The completed Grand Show can be started again");
                    s.sky.StopEverything(); s.sound.StopAll();
                    step = 8; next = EditorApplication.timeSinceStartup + .3; return;
                case 8:
                    s.placement.PlaceAt(CrackerId.Diya, new Vector3(0, s.placement.FloorHeight, 1.5f));
                    s.Completed(CrackerId.Diya);
                    s.sky.BeginGrandShow();
                    s.music.SetMuted(true);
                    oldSessionId = s.GetInstanceID();
                    s.thali.GetComponentsInChildren<ThaliSetting>().Single(x => x.kind == SettingKind.Reset).Press(null);
                    s.ResetAll();
                    step = 4; deadline = EditorApplication.timeSinceStartup + 30; return;
                case 4:
                    if (!s || !s.Started || s.GetInstanceID() == oldSessionId) return;
                    CheckExperience(s);
                    Check(s.placement.Items.Count == 0 && s.JourneyProgress == 0 && !s.sky.ShowRunning, "Reset clears placed items, progress and fireworks");
                    Check(experienceLoads == 2 && launchLoads == 1, "Reset reloads only DiwaliExperience and ignores duplicate reset requests");
                    Check(s.music.Muted, "The remembered music preference survives a scene reset");
                    s.music.SetMuted(false);
                    Check(!s.music.Track.isPlaying, "Unmuting in the experience does not restart splash music");
                    s.music.SetMuted(true);
                    Check(s.music.Track == launchTrack && Object.FindObjectsByType<FestivalMusicPlayback>(FindObjectsSortMode.None).Length == 1,
                        "Reset preserves one audio owner without duplicating the score");
                    Capture(s.rig.view, "04-reset-experience");
                    oldSessionId = s.GetInstanceID();
                    s.ResetAll();
                    step++; deadline = EditorApplication.timeSinceStartup + 30; return;
                case 5:
                    if (!s || !s.Started || s.GetInstanceID() == oldSessionId) return;
                    CheckExperience(s);
                    Check(experienceLoads == 3 && launchLoads == 1, "Repeated resets never return to SplashScreen");
                    Check(errors == 0, "Launch, entry and repeated resets complete without runtime errors");
                    File.AppendAllText(Output + "/playmode-validation.txt", "PASSED\n");
                    Finish(); return;
            }
        }

        static void ReadMix()
        {
            if (step == 11 || step == 12) return;
            var samples = new float[2048];
            for (int channel = 0; channel < 2; channel++)
            {
                AudioListener.GetOutputData(samples, channel);
                foreach (float sample in samples)
                {
                    float magnitude = Mathf.Abs(sample);
                    mixPeak = Mathf.Max(mixPeak, magnitude);
                    if (magnitude >= 1) clippedSamples++;
                }
            }
        }

        sealed class OccupiedHand : IFestivalGrasp
        {
            public void Steer(FestivalPointer pointer) { }
            public void Lost(FestivalPointer pointer) { }
        }

        static void CheckRemoval(DiwaliSession s)
        {
            var removal = s.rig.GetComponent<FestivalRemoval>();
            if (!removal) removal = s.rig.gameObject.AddComponent<FestivalRemoval>();
            var item = s.placement.PlaceAt(CrackerId.Rangoli, new Vector3(0, s.placement.FloorHeight, 1.5f));
            var other = s.placement.PlaceAt(CrackerId.Anar, new Vector3(.6f, s.placement.FloorHeight, 1.5f));
            var fuse = other.GetComponentInChildren<FestivalFuseTarget>();
            var pointer = new FestivalPointer { Tracked = true };
            float now = Time.unscaledTime;
            void Hold(IFestivalTarget target, float at, float duration = 2.01f)
            {
                pointer.Pressed = true; pointer.WasPressed = false;
                removal.Handle(pointer, target, at);
                pointer.WasPressed = true;
                removal.Handle(pointer, target, at + duration);
            }
            void Release(IFestivalTarget target, float at)
            {
                pointer.Pressed = false; pointer.WasPressed = true;
                removal.Handle(pointer, target, at);
                pointer.WasPressed = false;
            }
            void Tap(IFestivalTarget target, float at)
            {
                Hold(target, at, .035f);
                Release(target, at + .04f);
            }
            Tap(item, now); Tap(item, now + .1f); Tap(item, now + .2f);
            Check(!removal.Selected && item && item.gameObject.activeSelf, "Rapid taps no longer open Remove");
            Hold(item, now + 1, 1.99f);
            Check(!removal.Selected, "A hold shorter than two seconds does not show Remove");
            removal.Handle(pointer, item, now + 3.01f);
            Check(removal.Selected == item && removal.Button.gameObject.activeSelf && item.gameObject.activeSelf,
                "A continuous two-second pinch reveals Remove without deleting the object");
            Check(item.RemovalHidden && item.GetComponentsInChildren<Renderer>(true).All(r => r.forceRenderingOff) &&
                item.GetComponentsInChildren<Collider>(true).All(c => !c.enabled),
                "Remove hides the selected object's complete visuals and collision targets immediately");
            Check(!other.RemovalHidden && other.GetComponentsInChildren<Renderer>(true).Any(r => !r.forceRenderingOff),
                "Showing Remove leaves other objects visible");
            Vector3 savedEye = s.rig.view.transform.position;
            Quaternion savedLook = s.rig.view.transform.rotation;
            s.rig.view.transform.position = removal.Button.transform.position + new Vector3(.25f, .18f, -.6f);
            s.rig.view.transform.LookAt(removal.Button.transform.position);
            Capture(s.rig.view, "06-remove-button");
            s.rig.view.transform.SetPositionAndRotation(savedEye, savedLook);
            removal.Button.Press(pointer);
            Check(item.gameObject.activeSelf, "The revealing hold cannot also confirm removal");
            Release(item, now + 3.02f);
            pointer.Ray = new Ray(s.rig.view.transform.position, (removal.Button.transform.position - s.rig.view.transform.position).normalized);
            Physics.SyncTransforms();
            object[] args = { pointer, Vector3.zero, false };
            var hit = typeof(FestivalRig).GetMethod("FindTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.rig, args);
            Check(ReferenceEquals(hit, removal.Button), "The real pointer ray can target the world-space Remove button");
            pointer.Pressed = true; pointer.WasPressed = false;
            ((IFestivalTarget)hit).Press(pointer);
            Check(!item.gameObject.activeSelf && !s.placement.Items.Contains(item) &&
                other.gameObject.activeSelf && !removal.Button.gameObject.activeSelf,
                "Confirming Remove deletes only the selected object, at once");
            Check(!removal.HandleTapAway(pointer, null) && !item.gameObject.activeSelf && !s.placement.Items.Contains(item),
                "A tap elsewhere after confirming does not bring the object back");
            var kept = s.placement.PlaceAt(CrackerId.Rangoli, new Vector3(-.6f, s.placement.FloorHeight, 1.5f));
            Hold(kept, now + 3.5f);
            Check(removal.Selected == kept && kept.RemovalHidden, "Holding offers Remove on another object");
            removal.Cancel(pointer);
            pointer.Pressed = true; pointer.WasPressed = false;
            Check(removal.HandleTapAway(pointer, null) && !kept.RemovalHidden && s.placement.Items.Contains(kept) && !removal.Button.gameObject.activeSelf,
                "A tap elsewhere before confirming cancels, and the object comes back");
            pointer.Pressed = false; pointer.WasPressed = true;
            Hold(fuse, now + 4, 1);
            pointer.Tracked = false; removal.Handle(pointer, fuse, now + 5.1f);
            pointer.Tracked = true; removal.Handle(pointer, fuse, now + 7);
            Check(!removal.Selected, "Losing tracking cancels the hold and requires a fresh pinch");
            removal.Cancel(pointer);
            Hold(fuse, now + 8, 1);
            removal.Handle(pointer, s.thali.tiles[0], now + 9.1f);
            removal.Handle(pointer, fuse, now + 11);
            Check(!removal.Selected, "Aiming away cancels the hold and requires a fresh pinch");
            removal.Cancel(pointer);
            other.fuseSeconds = 100; other.fuseRange = Vector2.zero;
            var rendererStates = other.GetComponentsInChildren<Renderer>(true).ToDictionary(r => r, r => r.forceRenderingOff);
            var colliderStates = other.GetComponentsInChildren<Collider>(true).ToDictionary(c => c, c => c.enabled);
            var testLight = new GameObject("Removal light check").AddComponent<Light>();
            testLight.transform.SetParent(other.transform, false);
            Hold(fuse, now + 12);
            Check(other.RemovalHidden && !testLight.enabled && !other.CanGrab(new FestivalPointer()),
                "Hidden fuse objects cannot be grabbed and their lights are hidden");
            var flamePointer = new FestivalPointer { Tracked = true, Pressed = true, PinchPoint = new Vector3(0, 1, 1) };
            s.lighter.Take(flamePointer);
            flamePointer.PinchPoint = other.fuse.position - (s.lighter.tip.position - s.lighter.transform.position);
            s.lighter.Steer(flamePointer);
            typeof(DiwaliSession).GetMethod("IgniteByTouch", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, null);
            other.Ignite();
            Check(other.State == ItemState.Ready, "Flame contact and direct fuse ignition cannot light an object hidden behind Remove");
            s.lighter.Return();
            testLight.enabled = true;
            typeof(FestivalRemoval).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(removal, null);
            Check(!testLight.enabled, "Late animation updates cannot restore hidden object lights");
            removal.Dismiss(); removal.Cancel(pointer);
            Check(!other.RemovalHidden && testLight.enabled && rendererStates.All(pair => pair.Key.forceRenderingOff == pair.Value) &&
                colliderStates.All(pair => pair.Key.enabled == pair.Value),
                "Dismissing Remove restores each renderer, collider and light to its previous state");
            Object.Destroy(testLight.gameObject);
            Tap(fuse, now + 15);
            Check(other.State == ItemState.Burning, "A single quick pinch still resolves to the original fuse action");
            removal.Cancel(pointer);
            Hold(fuse, now + 13);
            Check(removal.Selected == other, "A burning object can also offer Remove");
            s.sound.StopFor(other.transform); s.placement.Remove(other); removal.Dismiss();
            Check(!removal.Button.gameObject.activeSelf, "Dismissal hides the removal control");
            pointer.Pressed = true; pointer.WasPressed = false;
            s.lighter.Take(pointer);
            Check(!removal.Handle(pointer, s.lighter, now + 16), "Held tools keep their normal carrying behavior");
            pointer.Pressed = false; s.lighter.Steer(pointer);
            Hold(s.lighter, now + 17);
            Check(!removal.Selected, "A hold on a dropped tool never offers Remove, because a pinch picks it up");
            s.lighter.Return();
            removal.Cancel(pointer);
            Hold(kept, now + 19);
            Release(kept, now + 21.1f);
            typeof(FestivalRemoval).GetField("hideAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(removal, Time.unscaledTime - 1);
            typeof(FestivalRemoval).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(removal, null);
            Check(!removal.Selected && kept.gameObject.activeSelf && !kept.RemovalHidden, "An unused Remove button expires without deleting its object");
            Check(kept.GetComponentsInChildren<Renderer>(true).All(r => !r.forceRenderingOff), "Expiration restores the object's visibility");
            s.placement.Remove(kept);
        }

        static void CheckExperience(DiwaliSession s)
        {
            Check(SceneManager.GetActiveScene().name == DiwaliSession.ExperienceSceneName && s.Started && s.thali.gameObject.activeInHierarchy,
                "DiwaliExperience starts directly with its tray ready");
            Check(!SceneManager.GetSceneByName("SplashScreen").isLoaded && !Object.FindFirstObjectByType<FestivalSplash>(),
                "The launch scene and its input targets are unloaded");
            Check(Object.FindObjectsByType<FestivalRig>(FindObjectsSortMode.None).Length == 1 &&
                Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.enabled) == 1,
                "Scene transitions leave one rig and one active audio listener");
        }

        static void Finish()
        {
            Time.timeScale = 1;
            PlayerPrefs.SetInt("DiwaliUtsav.MusicMuted", savedMute ? 1 : 0);
            PlayerPrefs.Save();
            running = false;
            EditorApplication.isPlaying = false;
        }

        static void Capture(Camera camera, string name)
        {
            var old = camera.targetTexture; var active = RenderTexture.active; float oldFov = camera.fieldOfView;
            var rt = new RenderTexture(1600, 1000, 24);
            try
            {
                camera.fieldOfView = 48; camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            finally { camera.fieldOfView = oldFov; camera.targetTexture = old; RenderTexture.active = active; rt.Release(); Object.DestroyImmediate(rt); }
        }
    }
}

namespace Diwali.Editor
{
    /// <summary>Exercises ray placement, close grabs, transfers and burning-state guards in the real scene.</summary>
    [InitializeOnLoad]
    public static class DiwaliPlacementReview
    {
        const string Output = "Verification/Placement";
        const string Pending = "Diwali.PlacementReview";
        static bool running;
        static int step, errors;
        static double next, deadline;
        static FestivalPointer right, left;
        static FestivalItem item;
        static Vector3 closeLanding;

        static DiwaliPlacementReview()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += Log;
        }

        [MenuItem("Diwali/Validate Floor Placement")]
        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before placement validation.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/playmode-validation.txt", "Floor placement and close-grab validation\n");
            File.WriteAllText(Output + "/runtime-errors.txt", "");
            DiwaliLinkSetup.UseDesktopForNextPlay();
            EditorSceneManager.OpenScene(DiwaliSceneBuilder.MRScenePath);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        static void Log(string message, string stack, LogType type)
        {
            if (!running || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            errors++;
            File.AppendAllText(Output + "/runtime-errors.txt", message + "\n" + stack + "\n");
        }

        static void Check(bool passed, string message)
        {
            if (!passed) throw new Exception(message);
            File.AppendAllText(Output + "/playmode-validation.txt", "PASS " + message + "\n");
        }

        static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool(Pending, false) && EditorApplication.isPlaying)
            {
                SessionState.EraseBool(Pending);
                running = true; step = errors = 0;
                next = EditorApplication.timeSinceStartup + 2;
                deadline = next + 25;
            }
            if (!running) return;
            if (!EditorApplication.isPlaying) { running = false; return; }
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Placement review timed out at " + step);
                if ((step == 2 || step == 40 || step == 41) && right?.Grasp != null) right.Grasp.Steer(right);
                if (EditorApplication.timeSinceStartup < next) return;
                Step();
            }
            catch (Exception exception)
            {
                File.AppendAllText(Output + "/playmode-validation.txt", "FAILED " + exception + "\n");
                Finish(); Debug.LogException(exception);
            }
        }

        static FestivalPointer Pointer(DiwaliSession s, int index) => new FestivalPointer
        {
            Index = index, Tracked = true, Pressed = true,
            PinchPoint = new Vector3(index == 0 ? -.25f : .25f, 1.1f, .4f),
            Line = s.rig.Pointers[index].Line, Reticle = s.rig.Pointers[index].Reticle
        };

        static IFestivalTarget Find(FestivalRig rig, FestivalPointer pointer)
        {
            Physics.SyncTransforms();
            return (IFestivalTarget)typeof(FestivalRig).GetMethod("FindTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(rig, new object[] { pointer, Vector3.zero, false });
        }

        static void Aim(DiwaliSession s, FestivalPointer pointer, Vector3 spot)
        {
            Vector3 origin = s.rig.view.transform.position;
            pointer.Ray = new Ray(origin, (spot - origin).normalized);
        }

        static void Release(FestivalPointer pointer)
        {
            pointer.Pressed = pointer.Gripping = false;
            pointer.Grasp.Steer(pointer);
        }

        static void CheckDecorStability(DiwaliSession session)
        {
            var anchor = new GameObject("Simulated localized decoration anchor");
            Vector3 headPosition = session.rig.view.transform.position;
            Quaternion headRotation = session.rig.view.transform.rotation;
            try
            {
                foreach (var id in new[] { CrackerId.Diya, CrackerId.Rangoli })
                {
                    Vector3 placed = new Vector3(.3f, session.placement.FloorHeight, 1.5f);
                    Quaternion orientation = Quaternion.Euler(0, 37, 0);
                    anchor.transform.SetPositionAndRotation(placed, orientation);
                    var decoration = session.placement.PlaceFromAnchor(id, anchor.transform);
                    Check(Vector3.Distance(decoration.transform.position, placed) < .00001f && Quaternion.Angle(decoration.transform.rotation, orientation) < .01f,
                        id + " restores the localized anchor's position and orientation");
                    bool stable = true;
                    for (int frame = 0; frame < 120; frame++)
                    {
                        float shake = Mathf.Sin(frame * .43f);
                        anchor.transform.SetPositionAndRotation(placed + new Vector3(.005f, .002f, .004f) * shake,
                            Quaternion.AngleAxis(shake * .5f, Vector3.up) * orientation);
                        session.rig.view.transform.SetPositionAndRotation(headPosition + Vector3.right * (shake * .03f),
                            Quaternion.AngleAxis(shake * 25, Vector3.up) * headRotation);
                        stable &= Vector3.Distance(decoration.transform.position, placed) < .00001f && Quaternion.Angle(decoration.transform.rotation, orientation) < .01f;
                    }
                    Check(stable && !decoration.transform.parent,
                        id + " stays fixed while simulated anchor corrections and head shaking change");
                    Check(id == CrackerId.Rangoli ? !decoration.CanGrab(new FestivalPointer()) : decoration.CanGrab(new FestivalPointer()),
                        id + " retains its existing pickup behavior");
                    session.placement.Forget(decoration);
                    Object.DestroyImmediate(decoration.gameObject);
                }
            }
            finally
            {
                session.rig.view.transform.SetPositionAndRotation(headPosition, headRotation);
                Object.DestroyImmediate(anchor);
            }
        }

        static void Step()
        {
            var s = DiwaliSession.Instance;
            switch (step)
            {
                case 0:
                    Check(s && s.Started, "The actual experience scene starts with its tray available");
                    s.rig.enabled = false;
                    CheckDecorStability(s);
                    s.thali.dockYaw = ThaliMenu.DefaultYaw; s.thali.reach = ThaliMenu.DefaultReach; s.thali.drop = ThaliMenu.DefaultDrop;
                    s.thali.Show(true);

                    // The tray turns to face the visitor over several frames, and starts from wherever the
                    // visitor last left it, so settle it here or this check depends on a saved tray position.
                    for (int settle = 0; settle < 600; settle++)
                        typeof(ThaliMenu).GetMethod("Follow", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.thali, null);
                    right = Pointer(s, 1); left = Pointer(s, 0);
                    var tile = s.thali.tiles.Single(t => t.id == CrackerId.Anar);
                    Aim(s, right, tile.transform.position);
                    var target = Find(s.rig, right);
                    Check(ReferenceEquals(target, tile) && !right.NearInteraction,
                        "A distance ray reaches the actual Anar tray tile (found " + (target as Component)?.name + ", near " + right.NearInteraction + ", head " + s.rig.view.transform.position + ", tile " + tile.transform.position + ")");
                    tile.Press(right);
                    item = s.placement.Items.Last();
                    Check(!item.HasBeenPlaced && FestivalPlacement.PlacementReach(item) == 2,
                        "A fresh cracker retains its two-metre limit until it has settled");
                    Check(right.Grasp != null && Mathf.Abs(item.transform.position.y - s.placement.FloorHeight) < .001f &&
                        item.transform.localScale == Vector3.one && Vector3.Distance(item.transform.position, right.PinchPoint) > .4f,
                        "Ray selection creates the full-size cracker directly on the floor from its first frame");
                    Aim(s, right, new Vector3(50, s.placement.FloorHeight, 50));
                    right.Grasp.Steer(right);
                    Check(Vector3.ProjectOnPlane(item.transform.position - s.rig.view.transform.position, Vector3.up).magnitude <= 2.001f,
                        "A distant floor ray is clamped to the two-metre placement limit");
                    Aim(s, right, new Vector3(.35f, s.placement.FloorHeight, 3));
                    right.Grasp.Steer(right);
                    Check(Vector3.Distance(right.Line.GetPosition(right.Line.positionCount - 1), item.transform.position) < .001f,
                        "The curved ray ends on the actual smoothed preview");
                    step = 40; next = EditorApplication.timeSinceStartup + .4; return;
                case 40:
                    Check(Mathf.Abs(Vector3.ProjectOnPlane(item.transform.position - s.rig.view.transform.position, Vector3.up).magnitude - 2) < .01f,
                        "A ray beyond two metres previews the actual cracker at the range boundary");
                    Check(s.placement.dropRing.enabled && !s.placement.safetyRing.enabled && !right.Reticle.enabled &&
                        Vector3.Distance(s.placement.dropRing.transform.position, item.transform.position) < .02f,
                        "A yellow placement circle marks the actual floor preview");
                    Capture(s.rig.view, "00-yellow-placement-circle", item.transform.position);
                    Release(right);
                    Check(right.Grasp == null && item.Carrier == null && item.State == ItemState.Ready && !item.Airborne,
                        "Releasing a distance selection settles it unlit at the preview position");
                    Check(item.HasBeenPlaced && FestivalPlacement.PlacementReach(item) == 5,
                        "After first placement, repositioning unlocks a five-metre radius");
                    Capture(s.rig.view, "01-ray-on-floor", item.transform.position);
                    step = 1; next = EditorApplication.timeSinceStartup + .15; return;
                case 1:
                    var shell = item.GetComponent<BoxCollider>();
                    Vector3 centre = item.transform.TransformPoint(shell.center);
                    right.Pressed = true;
                    right.Ray = new Ray(centre + Vector3.right, Vector3.left);
                    var body = Find(s.rig, right);
                    Check(ReferenceEquals(body, item), "A distance ray selects the placed cracker body separately from its fuse");
                    body.Press(right);
                    Aim(s, right, new Vector3(-.6f, s.placement.FloorHeight, 6));
                    right.Grasp.Steer(right);
                    step = 41; next = EditorApplication.timeSinceStartup + .4; return;
                case 41:
                    Vector3 shownPosition = item.transform.position;
                    Release(right);
                    Check(Vector3.Distance(shownPosition, item.transform.position) < .005f,
                        "Releasing settles at the shown preview without snapping to the raw aim");
                    Check(item.State == ItemState.Ready && Mathf.Abs(Vector3.ProjectOnPlane(item.transform.position - s.rig.view.transform.position, Vector3.up).magnitude - 5) < .02f,
                        "A placed cracker can be repositioned out to five metres, while farther aims are clamped");
                    right.Pressed = true;
                    right.PinchPoint = item.GetComponent<GrabZone>().WorldCentre;
                    item.Press(right);
                    right.PinchPoint += new Vector3(.5f, .8f, -.4f);
                    step = 2; next = EditorApplication.timeSinceStartup + .6; return;
                case 2:
                    Check(item.transform.position.y > s.placement.FloorHeight + .5f && item.Carrier == right,
                        "Approaching and grabbing the body physically lifts it with the hand");
                    left.PinchPoint = item.GetComponent<GrabZone>().WorldCentre;
                    item.Press(left);
                    Check(right.Grasp == null && left.Grasp != null && item.Carrier == left, "A close grab transfers an unlit cracker between hands");
                    right.Pressed = true;
                    Aim(s, right, new Vector3(.8f, s.placement.FloorHeight, 2.4f));
                    s.Take(CrackerId.Bomb, right);
                    Check(right.Grasp != null && left.Grasp != null, "One hand can use distance placement while the other carries an object");
                    Release(right);
                    Release(left);
                    closeLanding = s.placement.LastLanding;
                    Check(item.Airborne && left.Grasp == null, "Opening a nearby hand releases the cracker under gravity");
                    step++; next = EditorApplication.timeSinceStartup + 2; return;
                case 3:
                    if (item.Airborne) return;
                    Check(item.State == ItemState.Ready && Vector3.Distance(item.transform.position, closeLanding) < .2f,
                        "A physically repositioned cracker settles below the releasing hand and remains unlit");
                    item.fuseSeconds = 5;
                    item.Ignite();
                    right.Pressed = true;
                    Check(item.State == ItemState.Burning && !item.CanGrab(right), "A burning cracker cannot be grabbed");
                    Vector3 burningPosition = item.transform.position;
                    s.placement.Grab(item, right);
                    Check(right.Grasp == null && item.transform.position == burningPosition, "Direct placement calls also reject moving a burning cracker");
                    s.placement.ShowSafety(item.transform.position, 10, 5);
                    Check(!s.placement.dropRing.enabled && !s.placement.safetyRing.enabled, "Lighting a fuse does not bring back the yellow ground circle");
                    Aim(s, right, new Vector3(1.3f, s.placement.FloorHeight, 3));
                    s.Take(CrackerId.Popper, right);
                    var pop = s.placement.Items.Last();
                    right.WasPressed = false;
                    foreach (var rotation in new[] { Quaternion.Euler(40, 70, 100), Quaternion.Euler(-65, 120, -95) })
                    {
                        right.Rotation = rotation;
                        right.Grasp.Steer(right);
                        Check(Quaternion.Angle(pop.transform.rotation, rotation) < .05f && !right.Line.enabled && !right.Reticle.enabled,
                            "A held pop follows the hand quaternion exactly and shows no ray or reticle");
                    }
                    Release(right);
                    Check(right.Grasp == null && pop.Airborne,
                        "Even the first short pinch releases a pop immediately, without a second pinch");
                    Check(pop.Airborne && right.Grasp == null && !s.placement.dropRing.enabled,
                        "Ray-selected pops go into the hand and release through throwing, never floor placement");
                    pop.gameObject.SetActive(false);
                    s.placement.Forget(pop);
                    Object.Destroy(pop.gameObject);
                    right.Pressed = true;
                    s.Take(CrackerId.Bomb, right);
                    var lost = s.placement.Items.Last();
                    right.Grasp.Steer(right); right.Grasp.Lost(right);
                    Check(right.Grasp == null && lost.State == ItemState.Ready && !lost.Airborne,
                        "Tracking loss settles a remote preview on the floor and frees its pointer");
                    right.NearInteraction = true; right.Gripping = true;
                    s.Take(CrackerId.Popper, right);
                    var thrown = s.placement.Items.Last();
                    right.Grasp.Steer(right); Release(right);
                    Check(thrown.Airborne, "A physically selected hand popper can still be thrown");
                    var catchZone = thrown.GetComponent<GrabZone>();
                    right.Gripping = true; right.ClosedAt = Time.unscaledTime;
                    right.PinchPoint = catchZone.WorldCentre + Vector3.right * (catchZone.radius * 1.4f);
                    Check(s.placement.TryCatch(right) && !thrown.Airborne && right.Grasp != null,
                        "The forgiving catch reach still takes a flying popper physically instead of switching to ray placement");
                    right.Grasp.Steer(right); Release(right);
                    Check(errors == 0, "The placement sequence completes without runtime errors");
                    File.AppendAllText(Output + "/playmode-validation.txt", "PASSED\n");
                    Finish(); return;
            }
        }

        static void Finish()
        {
            running = false;
            if (DiwaliSession.Instance) DiwaliSession.Instance.placement.ClearAll();
            EditorApplication.isPlaying = false;
        }

        static void Capture(Camera camera, string name, Vector3 itemPosition)
        {
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            Vector3 eye = camera.transform.position;
            Quaternion orientation = camera.transform.rotation;
            var target = new RenderTexture(1600, 1000, 24);
            var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            try
            {
                camera.transform.position = itemPosition + new Vector3(.8f, .6f, -1.2f);
                camera.transform.LookAt(itemPosition + Vector3.up * .08f);
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                camera.transform.SetPositionAndRotation(eye, orientation);
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}


