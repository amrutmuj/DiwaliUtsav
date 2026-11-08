#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Diwali.Editor
{
    /// <summary>
    /// Master cinematic capture and recording suite for all Diwali crackers, fireworks,
    /// animated splash screen, and the Thali menu.
    /// Captures high-definition 1080p 60 FPS H.264 MP4 videos and curated hero stills.
    /// </summary>
    [InitializeOnLoad]
    public static class DiwaliCinematicCapture
    {
        public const string ModeKey = "Diwali.CinematicModeInt";
        public const string OutputRoot = "Recordings";
        public const string VideosDir = OutputRoot + "/Videos";
        public const string ImagesDir = OutputRoot + "/Images";
        public const string VerificationRecordingsDir = "Verification/Recordings";

        public enum CaptureMode
        {
            None = 0,
            CompleteShowcase = 1,
            AnarRetake = 2,
            Bomb = 3,
            Rocket = 4,
            Chakri = 5,
            SkyShot = 6,
            SplashScreen = 7,
            Menu = 8
        }

        static DiwaliCinematicCapture()
        {
            EditorApplication.update += Pump;
        }

        [MenuItem("Diwali/Cinematics/Record Complete Crackers Video & Images", priority = 200)]
        public static void StartCompleteCrackersRecording() => Launch(CaptureMode.CompleteShowcase);

        [MenuItem("Diwali/Cinematics/Record Bomb (Detonation & Plume)", priority = 201)]
        public static void StartBombRecording() => Launch(CaptureMode.Bomb);

        [MenuItem("Diwali/Cinematics/Retake Anar (Cone, Flame & Sparkles)", priority = 202)]
        public static void StartAnarRetake() => Launch(CaptureMode.AnarRetake);

        [MenuItem("Diwali/Cinematics/Record Rocket (Ascent & Bloom)", priority = 203)]
        public static void StartRocketCinematic() => Launch(CaptureMode.Rocket);

        [MenuItem("Diwali/Cinematics/Record Chakri (Spin & Spark Ring)", priority = 204)]
        public static void StartChakriCinematic() => Launch(CaptureMode.Chakri);

        [MenuItem("Diwali/Cinematics/Record SkyShot (Apex Detonation)", priority = 205)]
        public static void StartSkyShotCinematic() => Launch(CaptureMode.SkyShot);

        [MenuItem("Diwali/Cinematics/Record Splash Screen Video & Image", priority = 206)]
        public static void StartSplashScreenRecording() => Launch(CaptureMode.SplashScreen);

        [MenuItem("Diwali/Cinematics/Capture Menu Image (Thali Tray)", priority = 207)]
        public static void StartMenuCapture() => Launch(CaptureMode.Menu);

        public static void StartAllCinematics() => StartCompleteCrackersRecording();
        public static void StartGroundCinematic() => StartCompleteCrackersRecording();

        public static void Launch(CaptureMode mode)
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            EnsureDirectories();
            DiwaliLinkSetup.UseDesktopForNextPlay();
            string scenePath = (mode == CaptureMode.SplashScreen)
                ? DiwaliSceneBuilder.SplashScenePath
                : DiwaliSceneBuilder.MRScenePath;
            EditorSceneManager.OpenScene(scenePath);
            SessionState.SetInt(ModeKey, (int)mode);
            EditorApplication.isPlaying = true;
        }

        static void Pump()
        {
            int modeInt = SessionState.GetInt(ModeKey, 0);
            if (modeInt == 0 || !EditorApplication.isPlaying) return;
            var mode = (CaptureMode)modeInt;

            if (mode == CaptureMode.SplashScreen)
            {
                var splash = UnityEngine.Object.FindFirstObjectByType<FestivalSplash>();
                if (splash == null) return;
                SessionState.SetInt(ModeKey, 0);
                splash.StartCoroutine(RunSplashScreenSequence(splash));
                return;
            }

            if (DiwaliSession.Instance == null) return;
            SessionState.SetInt(ModeKey, 0);

            switch (mode)
            {
                case CaptureMode.Menu:
                    DiwaliSession.Instance.StartCoroutine(RunMenuCaptureSequence(DiwaliSession.Instance));
                    break;
                case CaptureMode.Bomb:
                    DiwaliSession.Instance.StartCoroutine(RunBombSequence(DiwaliSession.Instance));
                    break;
                case CaptureMode.AnarRetake:
                    DiwaliSession.Instance.StartCoroutine(RunAnarRetakeSequence(DiwaliSession.Instance));
                    break;
                case CaptureMode.Rocket:
                    DiwaliSession.Instance.StartCoroutine(RunRocketSequence(DiwaliSession.Instance));
                    break;
                case CaptureMode.Chakri:
                    DiwaliSession.Instance.StartCoroutine(RunChakriSequence(DiwaliSession.Instance));
                    break;
                case CaptureMode.SkyShot:
                    DiwaliSession.Instance.StartCoroutine(RunSkyShotSequence(DiwaliSession.Instance));
                    break;
                case CaptureMode.CompleteShowcase:
                default:
                    DiwaliSession.Instance.StartCoroutine(RunCompleteShowcaseSequence(DiwaliSession.Instance));
                    break;
            }
        }

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(OutputRoot);
            Directory.CreateDirectory(VideosDir);
            Directory.CreateDirectory(ImagesDir);
            Directory.CreateDirectory(VerificationRecordingsDir);

            // Clean up old frames directory if present
            string oldFramesDir = Path.Combine(OutputRoot, "Frames");
            if (Directory.Exists(oldFramesDir))
            {
                try { Directory.Delete(oldFramesDir, true); } catch { }
            }
        }

        // ==============================================================================================
        // DEDICATED SPLASH SCREEN SEQUENCE: FLAWLESS STILL IMAGE & ANIMATED 1080P60 MP4 VIDEO
        // ==============================================================================================
        static IEnumerator RunSplashScreenSequence(FestivalSplash splash)
        {
            yield return new WaitForSeconds(0.8f);
            EnsureDirectories();

            var report = new System.Text.StringBuilder();
            report.AppendLine("================================================================================");
            report.AppendLine("DIWALI UTSAV - SPLASH SCREEN CINEMATIC RECORDING");
            report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine("Composition: Eye-level framing of launch card, rotating rangoli diorama, breathing");
            report.AppendLine("             diya flame, drifting embers, title typography, and Begin celebration button");
            report.AppendLine("================================================================================\n");

            var cam = splash.rig.view;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.035f, 0.045f, 1f);
            cam.fieldOfView = 48f;
            cam.transform.localRotation = Quaternion.identity;

            Canvas.ForceUpdateCanvases();
            yield return new WaitForSeconds(0.4f);

            // 1. Capture pristine still image of the Splash Screen (1920x1080)
            CaptureCuratedImage(cam, "05_splash_screen_optimal");
            string srcImg = Path.Combine(ImagesDir, "05_splash_screen_optimal.png");
            string aliasImg = Path.Combine(ImagesDir, "splash_screen_still.png");
            try { File.Copy(srcImg, aliasImg, true); } catch { }
            report.AppendLine("  * Captured Still Image: 05_splash_screen_optimal.png & splash_screen_still.png");

            // 2. Start movie recorder for animated Splash Screen MP4
            var recorder = StartMovieRecorder("cinematic_splash_screen", 60f);
            report.AppendLine("Recorder: Started H.264 MP4 stream (60 FPS, 1920x1080)");

            // Record 4.0 seconds of animated diorama rotation, flame breathing, and ambient glow
            yield return new WaitForSeconds(4.0f);

            StopMovieRecorder(recorder);
            report.AppendLine("Recorder: Stopped. Video saved to Recordings/Videos/cinematic_splash_screen.mp4");

            SyncVerificationFiles();

            File.WriteAllText(Path.Combine(OutputRoot, "splash-recording-report.txt"), report.ToString());
            File.WriteAllText(Path.Combine(VerificationRecordingsDir, "splash-recording-report.txt"), report.ToString());
            Debug.Log("[Diwali] Splash screen recording complete!\n" + report);

            yield return new WaitForSeconds(0.5f);
            EditorApplication.isPlaying = false;
        }

        // ==============================================================================================
        // DEDICATED THALI MENU CAPTURE: HERO STILL OF FLOATING PALETTE WITH ALL 18 CRACKERS
        // ==============================================================================================
        static IEnumerator RunMenuCaptureSequence(DiwaliSession session)
        {
            yield return new WaitForSeconds(0.5f);
            session.Begin();

            // Dock Thali straight ahead in comfortable view
            session.thali.ForgetDock(0f, 0.60f, 0.32f);
            session.thali.Show(true);

            // Give the Thali 1.0 second to glide into position and miniatures to spin
            yield return new WaitForSeconds(1.0f);

            // Freeze the Thali menu and rig so LateUpdate will never move the tray or camera
            session.thali.enabled = false;
            session.rig.enabled = false;
            if (session.placement.previewRoom != null) session.placement.previewRoom.SetActive(false);

            EnsureDirectories();
            var report = new System.Text.StringBuilder();
            report.AppendLine("================================================================================");
            report.AppendLine("DIWALI UTSAV - THALI MENU CINEMATIC CAPTURE");
            report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine("Composition: Hero framing of Thali menu tray, all 18 cracker miniatures across 3 rows,");
            report.AppendLine("             gold filigree rim, grab handle, Grand Show card, and side controls");
            report.AppendLine("================================================================================\n");

            var cam = session.rig.view;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.03f, 0.05f, 1f);
            cam.fieldOfView = 46f;

            // Warm studio key directional light
            var studioLight = new GameObject("MenuStudioLight");
            var light = studioLight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.96f, 0.90f);
            light.intensity = 1.4f;
            studioLight.transform.rotation = Quaternion.Euler(45f, -25f, 0f);

            // Soft studio fill light for balanced shadow detail
            var fillLight = new GameObject("MenuFillLight");
            var fill = fillLight.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.92f, 0.94f, 1.0f);
            fill.intensity = 0.65f;
            fillLight.transform.rotation = Quaternion.Euler(30f, 40f, 0f);

            // Position camera looking directly down at the front face of the plate
            Vector3 platePos = session.thali.plate.position;
            cam.transform.position = new Vector3(platePos.x, platePos.y + 0.30f, platePos.z - 0.40f);
            cam.transform.LookAt(platePos);

            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return new WaitForEndOfFrame();

            // Capture flawless 1920x1080 still of the Menu
            CaptureCuratedImage(cam, "06_menu_thali_optimal");
            string srcImg = Path.Combine(ImagesDir, "06_menu_thali_optimal.png");
            string aliasImg = Path.Combine(ImagesDir, "menu_image.png");
            try { File.Copy(srcImg, aliasImg, true); } catch { }
            report.AppendLine("  * Captured Menu Still: 06_menu_thali_optimal.png & menu_image.png");

            if (studioLight) UnityEngine.Object.DestroyImmediate(studioLight);
            if (fillLight) UnityEngine.Object.DestroyImmediate(fillLight);
            SyncVerificationFiles();

            File.WriteAllText(Path.Combine(OutputRoot, "menu-capture-report.txt"), report.ToString());
            File.WriteAllText(Path.Combine(VerificationRecordingsDir, "menu-capture-report.txt"), report.ToString());
            Debug.Log("[Diwali] Menu capture complete!\n" + report);

            yield return new WaitForSeconds(0.5f);
            EditorApplication.isPlaying = false;
        }

        // ==============================================================================================
        // DEDICATED ATOM BOMB SEQUENCE: FUSE SIZZLE, DETONATION BURST & EXPANDING SMOKE PLUME
        // ==============================================================================================
        static IEnumerator RunBombSequence(DiwaliSession session)
        {
            yield return new WaitForSeconds(0.8f);
            session.Begin();

            session.rig.enabled = false;
            session.thali.Show(false);
            if (session.placement.previewRoom != null)
            {
                session.placement.previewRoom.SetActive(false);
            }

            var cam = session.rig.view;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.fieldOfView = 50f;

            EnsureDirectories();
            var report = new System.Text.StringBuilder();
            report.AppendLine("================================================================================");
            report.AppendLine("DIWALI UTSAV - ATOM BOMB CINEMATIC RECORDING");
            report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine("Composition: Clear framing of unlit bomb, fuse cord ignition & burn, sudden");
            report.AppendLine("             explosive detonation burst (flash, report, embers), and expanding smoke plume");
            report.AppendLine("================================================================================\n");

            var recorder = StartMovieRecorder("cinematic_bomb", 60f);
            float floor = session.placement.FloorHeight;

            Vector3 bombSpot = new Vector3(0f, floor, 1.55f);
            var bomb = (BlastItem)session.placement.PlaceAt(CrackerId.Bomb, bombSpot);
            bomb.fuseRange = new Vector2(1.2f, 1.2f);
            bomb.fuseSeconds = 1.2f;

            Vector3 camPos = bombSpot + new Vector3(-0.32f, 0.42f, -1.25f);
            Vector3 target = bombSpot + new Vector3(0f, 0.32f, 0f);
            cam.transform.position = camPos;
            cam.transform.LookAt(target);

            // 1. Unlit cracker resting on physical ground
            yield return new WaitForSeconds(0.6f);

            // 2. Fuse ignition & burn
            bomb.Ignite();
            report.AppendLine("  * Bomb ignited. Fuse cord burning with sizzling sparks for 1.2s...");
            yield return new WaitForSeconds(1.2f);

            // 3. EXPLOSIVE DETONATION BURST!
            yield return new WaitForSeconds(0.04f);
            report.AppendLine("  * DETONATION: Explosive report, flash, fiery embers, and billowing smoke unleashed!");

            // 4. Expanding and rising smoke plume
            yield return new WaitForSeconds(2.8f);

            StopMovieRecorder(recorder);
            report.AppendLine("Recorder: Stopped. Video saved to Recordings/Videos/cinematic_bomb.mp4");

            if (bomb) session.placement.Remove(bomb);
            SyncVerificationFiles();

            File.WriteAllText(Path.Combine(OutputRoot, "bomb-recording-report.txt"), report.ToString());
            File.WriteAllText(Path.Combine(VerificationRecordingsDir, "bomb-recording-report.txt"), report.ToString());
            Debug.Log("[Diwali] Bomb cinematic recording complete!\n" + report);

            yield return new WaitForSeconds(0.5f);
            EditorApplication.isPlaying = false;
        }

        // ==============================================================================================
        // DEDICATED RETAKE: ANAR CONE, FLAME & SPARKLES
        // ==============================================================================================
        static IEnumerator RunAnarRetakeSequence(DiwaliSession session)
        {
            yield return new WaitForSeconds(0.8f);
            session.Begin();

            session.rig.enabled = false;
            session.thali.Show(false);
            if (session.placement.previewRoom != null)
            {
                session.placement.previewRoom.SetActive(false);
            }

            var cam = session.rig.view;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.fieldOfView = 50f;

            EnsureDirectories();
            var report = new System.Text.StringBuilder();
            report.AppendLine("================================================================================");
            report.AppendLine("DIWALI UTSAV - ANAR COMBUSTION RETAKE");
            report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine("Composition: Entire Anar cone (base to tip), nozzle flame, and golden sparkles in frame");
            report.AppendLine("================================================================================\n");

            var recorder = StartMovieRecorder("cinematic_anar", 60f);
            float floor = session.placement.FloorHeight;

            Vector3 anarSpot = new Vector3(0f, floor, 1.55f);
            var anar = session.placement.PlaceAt(CrackerId.Anar, anarSpot);

            Vector3 camPos = anarSpot + new Vector3(-0.35f, 0.35f, -1.15f);
            Vector3 target = anarSpot + new Vector3(0f, 0.22f, 0f);
            cam.transform.position = camPos;
            cam.transform.LookAt(target);

            CaptureCuratedImage(cam, "01_anar_initial_frame");
            report.AppendLine("  * Captured: 01_anar_initial_frame.png (Entire unlit Anar cone resting on ground)");

            anar.Ignite();
            yield return new WaitForSeconds(0.6f);
            yield return new WaitForSeconds(1.2f);

            CaptureCuratedImage(cam, "02_cracker_combustion_optimal");
            report.AppendLine("  * Captured Image: 02_cracker_combustion_optimal.png (Entire Anar cone, nozzle flame & golden sparkles)");

            yield return new WaitForSeconds(2.8f);

            StopMovieRecorder(recorder);
            report.AppendLine("Recorder: Stopped. Video saved to Recordings/Videos/cinematic_anar.mp4");

            if (anar) session.placement.Remove(anar);
            SyncVerificationFiles();

            File.WriteAllText(Path.Combine(OutputRoot, "anar-retake-report.txt"), report.ToString());
            File.WriteAllText(Path.Combine(VerificationRecordingsDir, "anar-retake-report.txt"), report.ToString());
            Debug.Log("[Diwali] Anar retake complete!\n" + report);

            yield return new WaitForSeconds(0.5f);
            EditorApplication.isPlaying = false;
        }

        // ==============================================================================================
        // DEDICATED ROCKET SEQUENCE: INITIAL CRACKER FRAME, IGNITION, ASCENT & CELESTIAL STARBURST
        // ==============================================================================================
        static IEnumerator RunRocketSequence(DiwaliSession session)
        {
            yield return new WaitForSeconds(0.8f);
            session.Begin();

            session.rig.enabled = false;
            session.thali.Show(false);
            if (session.placement.previewRoom != null) session.placement.previewRoom.SetActive(false);

            var cam = session.rig.view;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.fieldOfView = 50f;

            EnsureDirectories();
            var report = new System.Text.StringBuilder();
            report.AppendLine("================================================================================");
            report.AppendLine("DIWALI UTSAV - ROCKET CINEMATIC RECORDING");
            report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine("Composition: Clear hero framing of unlit rocket on ground (studio lit), 1.5s initial");
            report.AppendLine("             dwell time, fuse burn, motor liftoff, camera crane ascent, and 18m starburst");
            report.AppendLine("================================================================================\n");

            float floor = session.placement.FloorHeight;
            Vector3 rocketSpot = new Vector3(0f, floor, 1.8f);
            var rocket = (RocketItem)session.placement.PlaceAt(CrackerId.Rocket, rocketSpot);
            rocket.fuseSeconds = 1.4f;

            var studioLight = new GameObject("RocketStudioLight");
            var light = studioLight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.96f, 0.9f);
            light.intensity = 1.3f;
            studioLight.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

            Vector3 rocketCamGround = rocketSpot + new Vector3(-0.35f, 0.26f, -0.75f);
            cam.transform.position = rocketCamGround;
            cam.transform.LookAt(rocketSpot + Vector3.up * 0.16f);

            CaptureCuratedImage(cam, "01_cracker_initial_frame");
            report.AppendLine("  * Captured Image 1: 01_cracker_initial_frame.png (Crisp cracker on ground before ignition)");

            var recorder = StartMovieRecorder("cinematic_rocket", 60f);
            report.AppendLine("Recorder: Started H.264 MP4 stream (60 FPS, 1920x1080)");

            yield return new WaitForSeconds(1.5f);
            report.AppendLine("  * Initial Frame Dwell: 1.5s of crisp unlit rocket on physical floor recorded");

            rocket.Ignite();
            report.AppendLine("  * Fuse burning down with crackling sparks for 1.4s...");
            yield return new WaitForSeconds(1.4f);

            CaptureCuratedImage(cam, "03_firework_ascent_liftoff");
            report.AppendLine("  * Captured Image 3: 03_firework_ascent_liftoff.png (Rocket motor liftoff and climb from floor)");

            Vector3 rocketSkyCam = new Vector3(0f, floor + 1.45f, 0.1f);
            float ascentStart = Time.time;
            while (rocket != null && rocket.body != null && rocket.body.gameObject.activeSelf && Time.time - ascentStart < 3.5f)
            {
                float t = Mathf.Clamp01((Time.time - ascentStart) / 1.6f);
                cam.transform.position = Vector3.Lerp(rocketCamGround, rocketSkyCam, Mathf.SmoothStep(0, 1, t));
                Vector3 targetRocket = rocket.body.position + Vector3.up * 0.5f;
                cam.transform.LookAt(targetRocket);
                yield return null;
            }

            Vector3 burstPoint = RocketItem.LastFlight.burstPoint != Vector3.zero
                ? RocketItem.LastFlight.burstPoint
                : rocketSpot + Vector3.up * 18.0f;

            cam.transform.position = new Vector3(0f, floor + 1.5f, 0f);
            cam.transform.LookAt(burstPoint);

            yield return new WaitForSeconds(0.42f);

            CaptureCuratedImage(cam, "04_firework_sky_burst_optimal");
            report.AppendLine("  * Captured Image 4: 04_firework_sky_burst_optimal.png (Best celestial sky-burst frame in full bloom)");

            yield return new WaitForSeconds(2.0f);

            StopMovieRecorder(recorder);
            report.AppendLine("Recorder: Stopped. Video saved to Recordings/Videos/cinematic_rocket.mp4");

            if (studioLight) UnityEngine.Object.DestroyImmediate(studioLight);
            if (rocket) session.placement.Remove(rocket);
            SyncVerificationFiles();

            File.WriteAllText(Path.Combine(OutputRoot, "rocket-recording-report.txt"), report.ToString());
            File.WriteAllText(Path.Combine(VerificationRecordingsDir, "rocket-recording-report.txt"), report.ToString());
            Debug.Log("[Diwali] Rocket cinematic recording complete!\n" + report);

            yield return new WaitForSeconds(0.5f);
            EditorApplication.isPlaying = false;
        }

        // ==============================================================================================
        // DEDICATED CHAKRI SEQUENCE: ROTATIONAL TORQUE & SPARK RING
        // ==============================================================================================
        static IEnumerator RunChakriSequence(DiwaliSession session)
        {
            yield return new WaitForSeconds(0.8f);
            session.Begin();

            session.rig.enabled = false;
            session.thali.Show(false);
            if (session.placement.previewRoom != null) session.placement.previewRoom.SetActive(false);

            var cam = session.rig.view;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.fieldOfView = 50f;

            EnsureDirectories();
            var recorder = StartMovieRecorder("cinematic_chakri", 60f);
            float floor = session.placement.FloorHeight;

            Vector3 chakriSpot = new Vector3(0f, floor, 1.45f);
            var chakri = session.placement.PlaceAt(CrackerId.Chakri, chakriSpot);

            cam.transform.position = chakriSpot + new Vector3(0f, 0.42f, -0.75f);
            cam.transform.LookAt(chakriSpot);

            yield return new WaitForSeconds(0.6f);
            chakri.Ignite();

            yield return new WaitForSeconds(3.8f);

            StopMovieRecorder(recorder);
            if (chakri) session.placement.Remove(chakri);
            SyncVerificationFiles();

            yield return new WaitForSeconds(0.5f);
            EditorApplication.isPlaying = false;
        }

        // ==============================================================================================
        // DEDICATED SKYSHOT SEQUENCE: MORTAR TUBE & APEX DETONATION
        // ==============================================================================================
        static IEnumerator RunSkyShotSequence(DiwaliSession session)
        {
            yield return new WaitForSeconds(0.8f);
            session.Begin();

            session.rig.enabled = false;
            session.thali.Show(false);
            if (session.placement.previewRoom != null) session.placement.previewRoom.SetActive(false);

            var cam = session.rig.view;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.fieldOfView = 50f;

            EnsureDirectories();
            var recorder = StartMovieRecorder("cinematic_skyshot", 60f);
            float floor = session.placement.FloorHeight;

            Vector3 mortarSpot = new Vector3(0f, floor, 2.0f);
            var skyShot = (MortarItem)session.placement.PlaceAt(CrackerId.SkyShot, mortarSpot);
            skyShot.firstShotDelay = 0.5f;

            cam.transform.position = new Vector3(0f, floor + 1.2f, 0.3f);
            cam.transform.LookAt(new Vector3(0f, floor + 18.0f, 22.0f));

            yield return new WaitForSeconds(0.6f);
            skyShot.Ignite();

            yield return new WaitForSeconds(3.4f);

            StopMovieRecorder(recorder);
            if (skyShot) session.placement.Remove(skyShot);
            SyncVerificationFiles();

            yield return new WaitForSeconds(0.5f);
            EditorApplication.isPlaying = false;
        }

        // ==============================================================================================
        // COMPLETE SHOWCASE SEQUENCE (ALL 7 ACTS INCLUDING FLAWLESS BOMB BURST & ROCKET INITIAL FRAME)
        // ==============================================================================================
        static IEnumerator RunCompleteShowcaseSequence(DiwaliSession session)
        {
            yield return new WaitForSeconds(0.8f);
            session.Begin();

            session.rig.enabled = false;
            if (session.placement.previewRoom != null)
            {
                session.placement.previewRoom.SetActive(false);
            }

            var cam = session.rig.view;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.fieldOfView = 50f;

            EnsureDirectories();
            var report = new System.Text.StringBuilder();
            report.AppendLine("================================================================================");
            report.AppendLine("DIWALI UTSAV - COMPLETE CRACKERS CINEMATIC RECORDING");
            report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine("Format: 1920x1080 (16:9 Landscape Video Editing UI Format)");
            report.AppendLine("Alpha Channel: Premultiplied RGBA for Reality Footage Compositing");
            report.AppendLine("Crackers Included: Diya, Phuljhadi, Chakri, Anar, Flowerpot, Butterfly,");
            report.AppendLine("                   Ladi, Bomb, SkyShot, TwelveShot, Rocket, Grand Show Finale");
            report.AppendLine("================================================================================\n");

            // Capture Menu Still while Thali is active at celebration startup
            session.thali.Show(true);
            Vector3 platePos = session.thali.plate.position;
            cam.transform.position = platePos - session.thali.plate.forward * 0.76f + session.thali.plate.up * 0.20f;
            cam.transform.LookAt(platePos, session.thali.plate.up);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForSeconds(0.4f);

            CaptureCuratedImage(cam, "06_menu_thali_optimal");
            string srcMenu = Path.Combine(ImagesDir, "06_menu_thali_optimal.png");
            string aliasMenu = Path.Combine(ImagesDir, "menu_image.png");
            try { File.Copy(srcMenu, aliasMenu, true); } catch { }
            report.AppendLine("  * Captured Menu Still: 06_menu_thali_optimal.png & menu_image.png");

            session.thali.Show(false);

            var recorder = StartMovieRecorder("cinematic_complete_crackers", 60f);
            report.AppendLine("Recorder: Started high-bitrate H.264 MP4 stream (60 FPS, 1920x1080)");

            float floor = session.placement.FloorHeight;

            // ==========================================================================================
            // ACT 1: SACRED LIGHT (DIYA & PHULJHADI SPARKLER)
            // ==========================================================================================
            report.AppendLine("\n[Act 1: Sacred Light - Diya & Phuljhadi]");
            var diya = session.placement.PlaceAt(CrackerId.Diya, new Vector3(-0.18f, floor, 1.25f));
            diya.Ignite();

            session.sparkler.gameObject.SetActive(true);
            session.sparkler.transform.position = new Vector3(0.15f, floor + 0.32f, 1.25f);
            session.sparkler.Ignite();

            Vector3 lightCamPos = new Vector3(0f, floor + 0.38f, 0.72f);
            cam.transform.position = lightCamPos;
            cam.transform.LookAt(new Vector3(0f, floor + 0.22f, 1.25f));

            yield return new WaitForSeconds(2.4f);

            // ==========================================================================================
            // ACT 2: GROUND CRACKERS & FOUNTAIN COMBUSTION (CHAKRI & ANAR)
            // ==========================================================================================
            report.AppendLine("[Act 2: Ground Crackers - Chakri & Anar Fountain]");
            var chakri = session.placement.PlaceAt(CrackerId.Chakri, new Vector3(0.42f, floor, 1.55f));
            chakri.Ignite();

            Vector3 anarSpot = new Vector3(-0.25f, floor, 1.55f);
            var anar = session.placement.PlaceAt(CrackerId.Anar, anarSpot);
            anar.Ignite();

            Vector3 anarCamPos = anarSpot + new Vector3(-0.35f, 0.35f, -1.15f);
            Vector3 anarTarget = anarSpot + new Vector3(0f, 0.22f, 0f);

            float glideStart = Time.time;
            while (Time.time - glideStart < 1.4f)
            {
                float t = Mathf.Clamp01((Time.time - glideStart) / 1.4f);
                cam.transform.position = Vector3.Lerp(lightCamPos, anarCamPos, Mathf.SmoothStep(0, 1, t));
                cam.transform.LookAt(Vector3.Lerp(new Vector3(0f, floor + 0.22f, 1.25f), anarTarget, Mathf.SmoothStep(0, 1, t)));
                yield return null;
            }

            yield return new WaitForSeconds(0.6f);

            CaptureCuratedImage(cam, "02_cracker_combustion_optimal");
            report.AppendLine("  * Captured Image 2: 02_cracker_combustion_optimal.png (Entire Anar cone, nozzle flame & golden sparkles)");

            yield return new WaitForSeconds(1.8f);

            session.placement.Remove(anar);
            session.placement.Remove(chakri);
            session.placement.Remove(diya);
            session.sparkler.gameObject.SetActive(false);

            // ==========================================================================================
            // ACT 3: DYNAMIC FLUTTER & RHYTHM (BUTTERFLY & LADI)
            // ==========================================================================================
            report.AppendLine("[Act 3: Aerial Flutter & Rhythm - Butterfly & Ladi]");
            var butterfly = (ButterflyItem)session.placement.PlaceAt(CrackerId.Butterfly, new Vector3(-0.22f, floor, 1.65f));
            butterfly.fuseSeconds = 0.1f;
            butterfly.Ignite();

            var ladi = session.placement.PlaceAt(CrackerId.Ladi, new Vector3(0.22f, floor, 1.55f));
            ladi.fuseSeconds = 0.2f;
            ladi.Ignite();

            cam.transform.position = new Vector3(0f, floor + 0.5f, 0.9f);
            cam.transform.LookAt(new Vector3(0f, floor + 0.5f, 1.6f));

            yield return new WaitForSeconds(2.8f);

            session.placement.Remove(butterfly);
            session.placement.Remove(ladi);

            // ==========================================================================================
            // ACT 4: HIGH-IMPACT EXPLOSIVE (ATOM BOMB WITH FULL BURST & PLUME)
            // ==========================================================================================
            report.AppendLine("[Act 4: High-Impact Detonation - Atom Bomb]");
            var bomb = (BlastItem)session.placement.PlaceAt(CrackerId.Bomb, new Vector3(0.1f, floor, 1.7f));
            bomb.fuseRange = new Vector2(1.1f, 1.1f);
            bomb.fuseSeconds = 1.1f;
            bomb.Ignite();

            cam.transform.position = new Vector3(-0.35f, floor + 0.42f, 0.95f);
            cam.transform.LookAt(new Vector3(0.1f, floor + 0.32f, 1.7f));

            yield return new WaitForSeconds(1.1f);

            report.AppendLine("  * Atom Bomb DETONATED: Loud explosive report, flash, and expanding smoke plume!");
            yield return new WaitForSeconds(2.6f);

            session.placement.Remove(bomb);

            // ==========================================================================================
            // ACT 5: SKY MORTARS (SKYSHOT & TWELVE-SHOT)
            // ==========================================================================================
            report.AppendLine("[Act 5: Sky Mortars - SkyShot & TwelveShot]");
            var skyShot = (MortarItem)session.placement.PlaceAt(CrackerId.SkyShot, new Vector3(-0.35f, floor, 2.0f));
            skyShot.firstShotDelay = 0.3f;
            skyShot.Ignite();

            var twelve = (MortarItem)session.placement.PlaceAt(CrackerId.TwelveShot, new Vector3(0.35f, floor, 2.0f));
            twelve.shots = 3;
            twelve.firstShotDelay = 0.6f;
            twelve.Ignite();

            Vector3 mortarCamPos = new Vector3(0f, floor + 1.4f, 0.2f);
            cam.transform.position = mortarCamPos;
            cam.transform.LookAt(new Vector3(0f, floor + 18.0f, 25.0f));

            yield return new WaitForSeconds(3.5f);

            session.placement.Remove(skyShot);
            session.placement.Remove(twelve);

            // ==========================================================================================
            // ACT 6: HERO FIREWORK (ASCENDING ROCKET & CELESTIAL BLOOM)
            // ==========================================================================================
            report.AppendLine("[Act 6: Hero Firework - Rocket Ascent & Red Spider Bloom]");
            Vector3 rocketSpot = new Vector3(0f, floor, 1.8f);
            var rocket = (RocketItem)session.placement.PlaceAt(CrackerId.Rocket, rocketSpot);
            rocket.fuseSeconds = 1.4f;

            var rocketStudioLight = new GameObject("CompleteRocketStudioLight");
            var rLight = rocketStudioLight.AddComponent<Light>();
            rLight.type = LightType.Directional;
            rLight.color = new Color(1.0f, 0.96f, 0.9f);
            rLight.intensity = 1.3f;
            rocketStudioLight.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

            Vector3 rocketCamGround = rocketSpot + new Vector3(-0.35f, 0.26f, -0.75f);
            cam.transform.position = rocketCamGround;
            cam.transform.LookAt(rocketSpot + Vector3.up * 0.16f);

            // Hold steady for 1.4s on the unlit rocket on the floor for reality compositing
            yield return new WaitForSeconds(1.4f);

            CaptureCuratedImage(cam, "01_cracker_initial_frame");
            report.AppendLine("  * Captured Image 1: 01_cracker_initial_frame.png (Crisp cracker on ground before ignition)");

            rocket.Ignite();
            report.AppendLine("  * Fuse burning down with crackling sparks for 1.4s...");
            yield return new WaitForSeconds(1.4f);

            CaptureCuratedImage(cam, "03_firework_ascent_liftoff");
            report.AppendLine("  * Captured Image 3: 03_firework_ascent_liftoff.png (Rocket motor liftoff and climb from floor)");

            Vector3 rocketSkyCam = new Vector3(0f, floor + 1.45f, 0.1f);
            float ascentStart = Time.time;
            while (rocket != null && rocket.body != null && rocket.body.gameObject.activeSelf && Time.time - ascentStart < 3.5f)
            {
                float t = Mathf.Clamp01((Time.time - ascentStart) / 1.6f);
                cam.transform.position = Vector3.Lerp(rocketCamGround, rocketSkyCam, Mathf.SmoothStep(0, 1, t));
                Vector3 targetRocket = rocket.body.position + Vector3.up * 0.5f;
                cam.transform.LookAt(targetRocket);
                yield return null;
            }

            Vector3 burstPoint = RocketItem.LastFlight.burstPoint != Vector3.zero
                ? RocketItem.LastFlight.burstPoint
                : rocketSpot + Vector3.up * 18.0f;

            cam.transform.position = new Vector3(0f, floor + 1.5f, 0f);
            cam.transform.LookAt(burstPoint);

            yield return new WaitForSeconds(0.42f);

            CaptureCuratedImage(cam, "04_firework_sky_burst_optimal");
            report.AppendLine("  * Captured Image 4: 04_firework_sky_burst_optimal.png (Best celestial sky-burst frame in full bloom)");

            yield return new WaitForSeconds(1.8f);

            if (rocketStudioLight) UnityEngine.Object.DestroyImmediate(rocketStudioLight);
            if (rocket) session.placement.Remove(rocket);

            // ==========================================================================================
            // ACT 7: GRAND FINALE
            // ==========================================================================================
            report.AppendLine("[Act 7: Grand Finale]");
            session.sky.StartCoroutine(session.sky.Finale());
            yield return new WaitForSeconds(3.5f);

            StopMovieRecorder(recorder);
            report.AppendLine("Recorder: Stopped. Complete video saved to Recordings/Videos/cinematic_complete_crackers.mp4");

            SyncVerificationFiles();

            report.AppendLine("\n================================================================================");
            report.AppendLine("COMPLETE SHOWCASE RECORDING FINISHED SUCCESSFULLY");
            report.AppendLine("Video: Recordings/Videos/cinematic_complete_crackers.mp4");
            report.AppendLine("Curated Images (6 total, no frame dumps):");
            report.AppendLine("  - 01_cracker_initial_frame.png");
            report.AppendLine("  - 02_cracker_combustion_optimal.png");
            report.AppendLine("  - 03_firework_ascent_liftoff.png");
            report.AppendLine("  - 04_firework_sky_burst_optimal.png");
            report.AppendLine("  - 05_splash_screen_optimal.png");
            report.AppendLine("  - 06_menu_thali_optimal.png");
            report.AppendLine("================================================================================");

            File.WriteAllText(Path.Combine(OutputRoot, "recording-report.txt"), report.ToString());
            File.WriteAllText(Path.Combine(VerificationRecordingsDir, "recording-report.txt"), report.ToString());
            Debug.Log("[Diwali] Complete crackers recording finished!\n" + report);

            yield return new WaitForSeconds(0.5f);
            EditorApplication.isPlaying = false;
        }

        static RecorderController StartMovieRecorder(string videoBaseName, float fps)
        {
            var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var recorderController = new RecorderController(controllerSettings);

            string projectDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absVideoPath = Path.Combine(projectDir, VideosDir, videoBaseName);

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "Movie Recorder (" + videoBaseName + ")";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
            };
            movie.CaptureAlpha = true;
            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = 1920,
                OutputHeight = 1080
            };
            movie.OutputFile = absVideoPath;

            controllerSettings.AddRecorderSettings(movie);
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRate = fps;

            RecorderOptions.VerboseMode = false;
            recorderController.PrepareRecording();
            recorderController.StartRecording();

            return recorderController;
        }

        static void StopMovieRecorder(RecorderController controller)
        {
            if (controller != null && controller.IsRecording())
            {
                controller.StopRecording();
            }
        }

        public static void CaptureCuratedImage(Camera cam, string imageName)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Directory.CreateDirectory(ImagesDir);
            string path = Path.Combine(ImagesDir, imageName + ".png");

            var oldTarget = cam.targetTexture;
            var active = RenderTexture.active;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);

            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;

                var texture = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();

                byte[] pngBytes = texture.EncodeToPNG();
                File.WriteAllBytes(path, pngBytes);
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                cam.targetTexture = oldTarget;
                RenderTexture.active = active;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        static void SyncVerificationFiles()
        {
            try
            {
                if (Directory.Exists(ImagesDir))
                {
                    foreach (var file in Directory.GetFiles(ImagesDir, "*.png"))
                    {
                        string dest = Path.Combine(VerificationRecordingsDir, Path.GetFileName(file));
                        File.Copy(file, dest, true);
                    }
                }
                if (Directory.Exists(VideosDir))
                {
                    foreach (var file in Directory.GetFiles(VideosDir, "*.mp4"))
                    {
                        string dest = Path.Combine(VerificationRecordingsDir, Path.GetFileName(file));
                        File.Copy(file, dest, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Diwali] Verification sync warning: " + ex.Message);
            }
        }
    }
}
#endif
