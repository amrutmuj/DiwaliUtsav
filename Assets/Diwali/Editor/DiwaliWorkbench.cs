using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace Diwali.Editor
{
    /// <summary>Local, explicit work commands and an end-to-end play-mode validation.</summary>
    [InitializeOnLoad]
    public static class DiwaliWorkbench
    {
        static double nextPoll, deadline;
        static int step;
        static bool testing;
        static int errors;
        static float leftEar,rightEar;
        static int audioWaits;
        static double linkProfileEnds,previewAt;
        static int previewStep;
        static BlastItem previewBomb;
        const string Command="Tools/diwali-command.txt";
        const string Output="Verification";
        static DiwaliWorkbench() {EditorApplication.update+=Update;Application.logMessageReceived+=OnLog;}
        static void OnLog(string message,string stack,LogType type) {if(testing && (type==LogType.Error||type==LogType.Exception||type==LogType.Assert)){errors++;File.AppendAllText(Output+"/runtime-errors.txt",message+"\n"+stack+"\n");}}
        static void Update()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(SessionState.GetBool("DiwaliSmokePending",false)&&EditorApplication.isPlaying&&!testing) {
                SessionState.SetBool("DiwaliSmokePending",false);testing=true;step=-3;errors=0;audioWaits=0;deadline=EditorApplication.timeSinceStartup+2;Directory.CreateDirectory(Output);
            }
            if(SessionState.GetBool("DiwaliLinkProfile",false)&&EditorApplication.isPlaying) {
                DiwaliProfileRun.Pump("xr");
                if(linkProfileEnds==0)linkProfileEnds=EditorApplication.timeSinceStartup+12;
                if(EditorApplication.timeSinceStartup>=linkProfileEnds){SessionState.SetBool("DiwaliLinkProfile",false);DiwaliProfileRun.Finish("Play over Link, XR running, headset not worn");EditorApplication.isPlaying=false;}
                return;
            }
            if(SessionState.GetBool("DiwaliPreview",false)&&EditorApplication.isPlaying) {if(EditorApplication.timeSinceStartup>=previewAt)PreviewStep();return;}
            if(testing) {DiwaliProfileRun.Pump(step.ToString());if(EditorApplication.timeSinceStartup>=deadline)SmokeStep();return;}
            if(EditorApplication.timeSinceStartup<nextPoll)return;nextPoll=EditorApplication.timeSinceStartup+1;
            if(!File.Exists(Command))return;string command=File.ReadAllText(Command).Trim();File.Delete(Command);
            try {
                Directory.CreateDirectory(Output);
                switch(command) {
                    case "generate": DiwaliSceneBuilder.BuildMR(); break;
                    case "generate-mr": DiwaliSceneBuilder.BuildMR(); break;
                    case "snapshot": Snapshot("mr-preview"); break;
                    case "inspect": Inspect(); break;
                    case "inspect-xr": InspectXR(); break;
                    case "smoke": StartSmoke(); break;
                    case "review-rocket": DiwaliRocketReview.Start(); break;
                    case "profile": DiwaliProfileRun.Begin();StartSmoke(); break;
                    case "preview-bomb": DiwaliLinkSetup.UseDesktopForNextPlay();SessionState.SetBool("DiwaliPreview",true);EditorSceneManager.OpenScene(DiwaliSceneBuilder.MRScenePath);EditorApplication.isPlaying=true;break;
                    case "profile-link": DiwaliProfileRun.Begin();SessionState.SetBool("DiwaliLinkProfile",true);linkProfileEnds=0;EditorSceneManager.OpenScene(DiwaliSceneBuilder.MRScenePath);EditorApplication.isPlaying=true; break;
                    case "build": BuildAndroid(); break;
                    case "stop": EditorApplication.isPlaying=false;break;
                    case "refresh": AssetDatabase.Refresh(); break;
                    case "audio": FestivalRecordedAudio.Refresh(); break;
                    case "review-audio": DiwaliAudioReview.Start(); break;
                    case "bench": DiwaliSoundBench.Start(); break;
                    case "bench-tools": DiwaliSoundBench.Start("tools"); break;
                    case "bench-sky": DiwaliSoundBench.Start("sky"); break;
                    case "bench-fuse": DiwaliSoundBench.Start("fuse"); break;
                    case "fx": DiwaliSceneBuilder.ApplyEffectTuning(); break;
                    case "identity": DiwaliSceneBuilder.InstallFestivalIdentity(); break;
                    case "tray": DiwaliSceneBuilder.ApplyThaliLayout(); break;
                    case "verify-splash": DiwaliSplashReview.Start(); break;
                    case "verify-placement": DiwaliPlacementReview.Start(); break;
                    case "verify-pinch": VerifyPinch(); break;
                    case "verify-ray": VerifyRay(); break;
                    case "record-splash": DiwaliCinematicCapture.StartSplashScreenRecording(); break;
                    case "capture-menu": DiwaliCinematicCapture.StartMenuCapture(); break;
                    case "retake-anar": DiwaliCinematicCapture.StartAnarRetake(); break;
                    case "record-bomb":
                    case "retake-bomb": DiwaliCinematicCapture.StartBombRecording(); break;
                    case "record-rocket": DiwaliCinematicCapture.StartRocketCinematic(); break;
                    case "record-chakri": DiwaliCinematicCapture.StartChakriCinematic(); break;
                    case "record-skyshot": DiwaliCinematicCapture.StartSkyShotCinematic(); break;
                    case "record-complete":
                    case "record-cinematic":
                    case "record-all":
                    case "record-ground": DiwaliCinematicCapture.StartCompleteCrackersRecording(); break;
                    case "splash": DiwaliSceneBuilder.UpdateSplashPresentation(); break;
                    case "xr-clean": foreach(var group in new[]{BuildTargetGroup.Standalone,BuildTargetGroup.Android})UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(group);AssetDatabase.SaveAssets();break;
                    case "import-tmp": AssetDatabase.ImportPackage("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage",false); break;
                    case "play": EditorSceneManager.OpenScene(DiwaliSceneBuilder.SplashScenePath);EditorApplication.isPlaying=true;break;
                    default:throw new InvalidOperationException("Unknown Diwali work command.");
                }
                File.WriteAllText(Output+"/last-command.txt",command+" completed "+DateTime.UtcNow.ToString("O"));
            } catch(Exception ex) {File.WriteAllText(Output+"/failure.txt",ex.ToString());Debug.LogException(ex);}
        }
        [MenuItem("Diwali/Validate Curved Ray")]
        public static void VerifyRay()
        {
            var lines = new System.Collections.Generic.List<string>();
            void Check(bool value, string message)
            {
                if (!value) throw new Exception("Ray validation failed: " + message);
                lines.Add("PASS " + message);
            }
            var pointer = new FestivalPointer { Ray = new Ray(Vector3.up, Vector3.forward) };
            pointer.SmoothAim(1/72f);
            Vector3 tremor = Quaternion.Euler(0, 1, 0) * Vector3.forward;
            pointer.Ray = new Ray(Vector3.up, tremor); pointer.SmoothAim(1/72f);
            Check(Vector3.Angle(Vector3.forward, pointer.Ray.direction) < .5f, "Small aim tremors are reduced");
            Check(pointer.Ray.origin == Vector3.up, "The ray stays anchored to the current hand position");
            pointer.Ray = new Ray(Vector3.up, Vector3.right); pointer.SmoothAim(1/72f);
            Check(Vector3.Angle(pointer.Ray.direction, Vector3.right) < 30, "Deliberate turns respond promptly");
            pointer.ResetAim(); pointer.Ray = new Ray(Vector3.up, Vector3.back); pointer.SmoothAim(1/72f);
            Check(Vector3.Angle(pointer.Ray.direction, Vector3.back) < .01f, "Tracking recovery starts at the current pose");
            var rig = UnityEngine.Object.FindFirstObjectByType<FestivalRig>();
            if (!rig) throw new Exception("Open a Diwali scene before ray validation.");
            var lineObject = new GameObject("Ray validation");
            var reticleObject = new GameObject("Reticle validation");
            try
            {
                pointer.Line = lineObject.AddComponent<LineRenderer>();
                pointer.Reticle = reticleObject.AddComponent<MeshRenderer>();
                pointer.Ray = new Ray(new Vector3(0, 1.1f, 0), new Vector3(0, -.4f, 1).normalized);
                Vector3 landing = new Vector3(.3f, 0, 1.8f);
                rig.ShowPlacementRay(pointer, landing);
                Check(pointer.Line.positionCount == 25 && pointer.Line.enabled, "Placement draws a smooth 25-point curve");
                Check(Vector3.Distance(pointer.Line.GetPosition(0), pointer.Ray.origin) < .001f &&
                    Vector3.Distance(pointer.Line.GetPosition(24), landing) < .001f, "The full curve connects the hand to the actual landing preview");
                float length = 0;
                for (int i = 1; i < pointer.Line.positionCount; i++) length += Vector3.Distance(pointer.Line.GetPosition(i-1), pointer.Line.GetPosition(i));
                Check(length >= Vector3.Distance(pointer.Ray.origin, landing), "The curve spans the complete hand-to-target distance");
                Check(Vector3.Distance(pointer.Line.GetPosition(12), Vector3.Lerp(pointer.Line.GetPosition(0), landing, .5f)) > .005f,
                    "The hand-to-target placement ray retains its curve");
                rig.ShowRay(pointer, landing, false);
                Check(pointer.Line.positionCount == 2, "Returning to menu pointing clears the placement curve");
                Check(Mathf.Abs(Vector3.Distance(pointer.Line.GetPosition(0), pointer.Line.GetPosition(1)) - .15f) < .001f,
                    "An idle ray is only 15 centimetres long");
                rig.ShowRay(pointer, landing, false, true);
                Check(Vector3.Distance(pointer.Line.GetPosition(0), pointer.Ray.origin) < .001f &&
                    Vector3.Distance(pointer.Line.GetPosition(1), landing) < .001f,
                    "Selection draws the full ray to the target");
                rig.ShowRay(pointer, landing, true);
                Check(Vector3.Distance(pointer.Line.GetPosition(1), landing) < .001f && pointer.Reticle.enabled,
                    "Hovering a target extends the ray and shows its target feedback");
                var bank = AssetDatabase.LoadAssetAtPath<FestivalSoundBank>("Assets/Diwali/FestivalSoundBank.asset");
                var lift = bank.sounds.Single(sound => sound.id == "lift");
                Check(lift.clips.Length == 1 && lift.clips[0].name == "ui_pickup_soft" && lift.volume <= .45f,
                    "Pickup uses the new quiet single-tap cue");
            }
            finally { UnityEngine.Object.DestroyImmediate(lineObject); UnityEngine.Object.DestroyImmediate(reticleObject); }
            Directory.CreateDirectory("Verification/Ray"); lines.Add("PASSED");
            File.WriteAllLines("Verification/Ray/validation.txt", lines);
        }

        [MenuItem("Diwali/Validate Pinch Response")]
        public static void VerifyPinch()
        {
            var lines = new System.Collections.Generic.List<string>();
            void Check(bool value, string message)
            {
                if (!value) throw new Exception("Pinch validation failed: " + message);
                lines.Add("PASS " + message);
            }
            Check(!FestivalPointer.IsPinching(.05f, false), "An open hand does not select");
            Check(FestivalPointer.IsPinching(.027f, false), "A closing pinch selects before the former 22 mm fallback threshold");
            Check(!FestivalPointer.IsPinching(.03f, false), "Approaching fingers alone do not select");
            Check(FestivalPointer.IsPinching(.03f, true), "Small fingertip movement does not chatter while holding");
            Check(!FestivalPointer.IsPinching(.036f, true), "Opening past 35 mm releases immediately");
            Check(FestivalPointer.IsPinching(.027f, false), "A released hand can select again without a cooldown");
            Check(!FestivalPointer.IsPinching(float.NaN, true), "Invalid joint distances cannot sustain a pinch");
            Check(FestivalPointer.IsGripping(.5f, .5f, .9f, false), "Two closed fingers take a grip");
            Check(!FestivalPointer.IsGripping(.5f, .9f, .9f, false), "One closed finger does not");
            Check(!FestivalPointer.IsGripping(.75f, .75f, .75f, false), "A half-open hand does not start a grip");
            Check(FestivalPointer.IsGripping(.75f, .75f, .75f, true), "A half-open hand keeps a grip, so a held tool does not chatter");
            Check(!FestivalPointer.IsGripping(.9f, .9f, .9f, true), "A relaxed hand lets go without spreading the fingers");
            var left = new FestivalPointer { Pressed = FestivalPointer.IsPinching(.027f, false) };
            var right = new FestivalPointer { Pressed = FestivalPointer.IsPinching(.05f, false) };
            Check(left.PressedNow && !right.PressedNow, "Hands retain independent selection edges");
            left.WasPressed = left.Pressed; left.Pressed = FestivalPointer.IsPinching(.03f, left.WasPressed);
            Check(!left.PressedNow && left.Holding, "Holding does not repeatedly activate the target");
            left.WasPressed = left.Pressed; left.Pressed = FestivalPointer.IsPinching(.036f, left.WasPressed);
            Check(left.ReleasedNow && !left.Holding, "Release remains compatible with placement and tool controls");
            Directory.CreateDirectory("Verification/Pinch");
            lines.Add("PASSED");
            File.WriteAllLines("Verification/Pinch/validation.txt", lines);
        }

        [MenuItem("Diwali/Validate Guided Experience")]
        public static void StartSmoke()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop the current play session before validation.");
            DiwaliLinkSetup.UseDesktopForNextPlay();SetAsideDock();
            EditorSceneManager.OpenScene(DiwaliSceneBuilder.MRScenePath);SessionState.SetBool("DiwaliSmokePending",true);EditorApplication.isPlaying=true;
        }
        static void Assert(bool value,string message) {if(!value)throw new Exception("Validation failed: "+message);File.AppendAllText(Output+"/playmode-validation.txt","PASS "+message+"\n");}
        /// <summary>The run moves and resets the tray, so the player's own place is set aside first and put back at the end.</summary>
        static void SetAsideDock()
        {
            if(SessionState.GetString("DiwaliSavedDock","")=="")SessionState.SetString("DiwaliSavedDock",PlayerPrefs.GetString(ThaliMenu.DockKey,""));
            PlayerPrefs.DeleteKey(ThaliMenu.DockKey);
        }
        static void RestoreDock()
        {
            string dock=SessionState.GetString("DiwaliSavedDock","");SessionState.EraseString("DiwaliSavedDock");
            if(dock!=""){PlayerPrefs.SetString(ThaliMenu.DockKey,dock);PlayerPrefs.Save();}
        }
        static bool SkyHeadsLit(DiwaliSession s) {return s.sky.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.name=="Head"&&r.enabled);}

        /// <summary>A placed rangoli can never be picked up again, while other placed items still can.</summary>
        static void CheckRangoliStays(DiwaliSession s)
        {
            var hand=new FestivalPointer();
            var rangoli=Place(s,CrackerId.Rangoli,new Vector3(0,0,1.6f));var anar=Place(s,CrackerId.Anar,new Vector3(.6f,0,1.6f));
            Vector3 spot=rangoli.transform.position;
            Assert(anar.CanGrab(hand),"A placed cracker can still be picked up");
            Assert(rangoli.State==ItemState.Ready&&!rangoli.CanGrab(hand),"A placed rangoli cannot be picked up");
            rangoli.Press(hand);
            Assert(hand.Grasp==null&&rangoli.transform.position==spot,"Pinching a placed rangoli leaves it where it is");
            Clear(s);
        }

        /// <summary>The edge halo lights solid parts only and fades away, photographed so it can be judged by eye.</summary>
        static void CheckEdgeHalo(DiwaliSession s)
        {
            Assert(!s.rig.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("Grab region")),"No ring is drawn around grabbable objects");
            var items=new[]{Place(s,CrackerId.TwelveShot,new Vector3(-.28f,0,1.1f)),Place(s,CrackerId.Anar,new Vector3(0,0,1.1f)),Place(s,CrackerId.Bomb,new Vector3(.24f,0,1.1f))};
            foreach(var item in items)
            {
                var halo=item.GetComponent<GrabZone>().Halo;halo.Show(1);
                Assert(halo.Lit&&halo.Shells>0,item.name+" lights an edge halo");
            }
            Look(s,new Vector3(0,.55f,.45f),new Vector3(0,.07f,1.1f));Snapshot("02b-edge-halo");ResetView(s);
            Clear(s);
        }

        /// <summary>The pops tile is a heap of small pops, photographed close so it can be judged by eye.</summary>
        static void CheckPopsTile(DiwaliSession s)
        {
            var tile=s.thali.tiles.First(t=>t.id==CrackerId.Popper).transform;
            Assert(tile.GetComponentsInChildren<Transform>().Count(t=>t.name=="Pop")>=5,"The pops tile shows several small pops");
            Look(s,tile.position-s.thali.plate.forward*.17f+s.thali.plate.up*.04f,tile.position);Snapshot("02a-pops-tile");ResetView(s);
        }

        /// <summary>The fireworks have the main panel to themselves; Grand Show and the settings sit in the side panel and still answer a pinch.</summary>
        static void CheckSidePanel(DiwaliSession s)
        {
            var surface=s.thali.plate.Find("Plate surface");
            Func<string,Rect> pane=name=>{var r=(RectTransform)surface.Find(name);return new Rect(r.anchoredPosition-r.sizeDelta/2,r.sizeDelta);};
            Func<Component,Vector3> onSurface=c=>surface.InverseTransformPoint(c.transform.position);
            Rect main=pane("Glass"),side=pane("Side glass");
            var settings=surface.GetComponentsInChildren<ThaliSetting>(true);
            var show=s.thali.tiles.First(t=>t.id==CrackerId.GrandShow);
            Assert(side.xMax<main.xMin,"The side panel stands apart, to the left of the main panel");
            Assert(settings.Length==4&&settings.All(x=>side.Contains(onSurface(x))),"Sun and night, gentle, music and reset are all in the side panel");
            Assert(s.thali.tiles.Length==18&&side.Contains(onSurface(show))&&s.thali.tiles.All(t=>t==show||main.Contains(onSurface(t))),"Grand Show is in the side panel and all seventeen fireworks are in the main panel");

            Physics.SyncTransforms();
            var forward=s.thali.plate.forward;
            foreach(var control in settings.Cast<Component>().Append(show))
            {
                bool hit=Physics.Raycast(control.transform.position-forward*.2f,forward,out var found,.4f,1<<control.gameObject.layer);
                Assert(hit&&found.collider.gameObject==control.gameObject,control.name+" answers a pinch in the side panel");
            }

            var middle=surface.TransformPoint(new Vector3(side.center.x,side.center.y,0));
            Assert(s.thali.Receives(new FestivalPointer{PinchPoint=middle,Ray=new Ray(middle-Vector3.forward*.4f,Vector3.forward)}),"A held item can be put back over the side panel");
        }
        /// <summary>The speed a hand's path releases at: it builds to its peak, and the fingers read open just after it.</summary>
        static float ReleaseFrom(float peak,float buildSeconds,float frame,float stillSeconds=0)
        {
            const int slices=32;var hand=new FestivalPointer();float travelled=0,time=0,end=buildSeconds+.03f;
            while(time<end+stillSeconds)
            {
                hand.PinchPoint=Vector3.forward*travelled;hand.SampleMotion(time);
                for(int i=0;i<slices;i++)
                {
                    float speed=time<buildSeconds?peak*Mathf.Pow(Mathf.Sin(Mathf.PI*.5f*time/buildSeconds),2):time<end?peak*(1-8*(time-buildSeconds)):0;
                    travelled+=speed*frame/slices;time+=frame/slices;
                }
            }
            return hand.ReleaseVelocity.magnitude;
        }
        /// <summary>A throw twice as hard has to fly twice as fast, from the hand's path to the end of the flight.</summary>
        static void CheckThrowScaling()
        {
            float drag=CrackerPhysics.For(CrackerId.Popper).airDrag,worstRelease=0,worstFlight=0,firstRelease=0,firstSpeed=0,firstReach=0;
            foreach(float peak in new[]{2f,4f,8f,16f,24f})
            {
                float release=ReleaseFrom(peak,.15f,1/72f),scale=peak/2;
                Vector3 position=Vector3.up*3,velocity=Vector3.forward*release;
                for(int i=0;i<96;i++)FestivalToss.Fly(ref position,ref velocity,drag,1/240f);
                if(firstRelease==0){firstRelease=release;firstSpeed=velocity.z;firstReach=position.z;}
                worstRelease=Mathf.Max(worstRelease,Mathf.Abs(release/firstRelease/scale-1));
                worstFlight=Mathf.Max(worstFlight,Mathf.Abs(velocity.z/firstSpeed/scale-1),Mathf.Abs(position.z/firstReach/scale-1));
                File.AppendAllText(Output+"/playmode-validation.txt",$"INFO throw: hand peak {peak:F0} m/s, released at {release:F2}, then {velocity.z:F2} m/s and {position.z:F2} m out after 0.4 s\n");
            }
            Assert(worstRelease<.01f,"Twice the hand speed reads as twice the throw");
            Assert(worstFlight<.01f,"A throw twice as fast flies twice as fast and twice as far in the same time");
            float steady=ReleaseFrom(8,.15f,1/72f)/8,whip=ReleaseFrom(8,.09f,1/72f)/8,halfRate=ReleaseFrom(8,.09f,1/36f)/8;
            File.AppendAllText(Output+"/playmode-validation.txt",$"INFO share of true peak read: steady {steady:F3}, short whip {whip:F3}, whip at half frame rate {halfRate:F3}\n");
            Assert(Mathf.Min(steady,whip,halfRate)>.9f&&Mathf.Max(steady,whip,halfRate)<1.02f,"A short hard throw reads within a tenth of its true peak, even at half frame rate");
            Assert(ReleaseFrom(1,.2f,1/72f,.15f)<.05f,"A hand that stops before opening sets the pop down rather than throwing it");
        }
        static FestivalItem Place(DiwaliSession s,CrackerId id,Vector3 position) {return s.placement.PlaceAt(id,position);}
        static void Look(DiwaliSession s,Vector3 from,Vector3 at) {var t=s.rig.view.transform;t.position=from;t.LookAt(at);}
        static void ResetView(DiwaliSession s) {var t=s.rig.view.transform;t.localPosition=new Vector3(0,1.5f,0);t.localRotation=Quaternion.Euler(22,0,0);}
        static FestivalItem anar,pot,chakri,ladi,bigLadi,bomb,caps,rocket,sky,twelve,fifteen,touch;
        static FestivalItem thrown,dropped,quickPlaced;
        static Vector3 quickSpot,popperRing;
        static int flightFrames,flightStalls,landingSounds;
        static float lastLandingSound,settledAt;
        static ButterflyItem butterfly;
        static PopperItem popper;
        static readonly FestivalPointer Probe=new FestivalPointer{Tracked=true,Index=1};
        static readonly FestivalPointer OtherHand=new FestivalPointer{Tracked=true,Index=0,IsHand=true};
        static Vector3 Flat(Vector3 value) {value.y=0;return value;}
        static void Clear(DiwaliSession s) {foreach(var item in s.placement.Items.ToArray()){s.placement.Forget(item);UnityEngine.Object.Destroy(item.gameObject);}}
        /// <summary>Watches a flight on every rendered frame: whether it moves each frame, and when the landing sounds fire.</summary>
        static System.Collections.IEnumerator WatchFlight(DiwaliSession s,FestivalToss toss)
        {
            flightFrames=flightStalls=landingSounds=0;lastLandingSound=settledAt=0;
            Vector3 last=toss.transform.position;int heard=s.sound.LastCue("place").count;
            while(toss&&toss.Flying)
            {
                yield return null;
                if(!toss)yield break;
                var cue=s.sound.LastCue("place");
                if(cue.count!=heard){landingSounds+=cue.count-heard;heard=cue.count;lastLandingSound=Time.time;}
                if(toss.Flying&&toss.transform.position.y>.3f){flightFrames++;if(toss.transform.position==last)flightStalls++;}
                last=toss.transform.position;
            }
            settledAt=Time.time;
        }
        static float Rms(int channel)
        {
            var data=new float[4096];AudioListener.GetOutputData(data,channel);double sum=0;foreach(var v in data)sum+=v*v;return Mathf.Sqrt((float)(sum/data.Length));
        }
        static bool earsWereMuted;
        static double leftPower,rightPower;
        static int earReads;
        /// <summary>One read is 85 ms of crackle. A level is the power averaged over twelve reads that do not overlap.</summary>
        static bool ReadEars(out float left,out float right)
        {
            float l=Rms(0),r=Rms(1);leftPower+=l*l;rightPower+=r*r;earReads++;
            left=Mathf.Sqrt((float)(leftPower/earReads));right=Mathf.Sqrt((float)(rightPower/earReads));
            if(earReads<12){deadline=EditorApplication.timeSinceStartup+.1;return false;}
            leftPower=rightPower=0;earReads=0;return true;
        }
        static float Db(float value) {return 20*Mathf.Log10(Mathf.Max(value,1e-6f));}
        static SoundLoop probeLoop;
        static GameObject probeSource;
        /// <summary>Captures the bomb's plume over time, against a bright background standing in for a lit room.</summary>
        static void PreviewStep()
        {
            var s=DiwaliSession.Instance;if(!s)return;
            int shot=previewStep++;
            if(shot==0){previewAt=EditorApplication.timeSinceStartup+1.5;return;}
            if(shot==1)
            {
                s.Begin();s.placement.previewRoom.SetActive(false);s.rig.view.backgroundColor=new Color(.74f,.71f,.66f);s.thali.Show(false);
                previewBomb=(BlastItem)s.placement.PlaceAt(CrackerId.Bomb,new Vector3(0,0,2));previewBomb.fuseRange=new Vector2(.3f,.3f);previewBomb.Ignite();
                Look(s,new Vector3(0,1.5f,0),new Vector3(0,.9f,2));previewAt=EditorApplication.timeSinceStartup+1;File.WriteAllText(Output+"/bomb-plume.txt","Bomb plume over time\n");return;
            }
            float[] waits={1,1.5f,2.5f,3,0};
            if(shot-2<waits.Length-1){Snapshot("preview-bomb-"+(shot-1));LogPlume(previewBomb);previewAt=EditorApplication.timeSinceStartup+waits[shot-2];return;}
            previewStep=0;SessionState.SetBool("DiwaliPreview",false);EditorApplication.isPlaying=false;
        }
        /// <summary>Where the plume actually is, since a single render or a mean can hide a plume that has stopped moving.</summary>
        static void LogPlume(BlastItem blast)
        {
            var puffs=new ParticleSystem.Particle[200];int count=blast.clouds.GetParticles(puffs);float mean=0,top=0,widest=0;var site=blast.LastDetonationPoint;
            for(int i=0;i<count;i++){float height=puffs[i].position.y-site.y;mean+=height;top=Mathf.Max(top,height);widest=Mathf.Max(widest,Flat(puffs[i].position-site).magnitude);}
            File.AppendAllText(Output+"/bomb-plume.txt",$"{Time.time-blast.DetonationTime:F1} s: {count} puffs, mean height {mean/Mathf.Max(count,1):F2} m, top {top:F2} m, widest {widest:F2} m\n");
        }
        /// <summary>Walks the whole experience without a headset and records what it finds.</summary>
        static void SmokeStep()
        {
            try {
                var s=DiwaliSession.Instance;if(!s)throw new Exception("No Diwali session found.");
                switch(step++) {
                    case -3:
                        File.WriteAllText(Output+"/playmode-validation.txt","Diwali guided play-mode validation (desktop preview)\n");
                        Assert(s.Started,"The experience scene starts directly without the launch screen");
                        Assert(s.rig.view.rect==new Rect(0,0,1,1),"Main camera uses the full undistorted viewport");
                        Assert(s.ambience.passthrough.textureOpacity==1,"Passthrough stays fully opaque");
                        Assert(s.rig.metaRig.GetComponent<OVRManager>().isInsightPassthroughEnabled,"Passthrough is switched on at run time");
                        Assert(File.ReadAllText(DiwaliSceneBuilder.MRScenePath).Contains("propertyPath: isInsightPassthroughEnabled"),"Passthrough is switched on in the saved scene");
                        Assert(!UnityEngine.Object.FindObjectsByType<Meta.XR.MRUtilityKit.EffectMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(),"No room mesh is generated");
                        Assert(s.placement.floor.radius==10,"The floor layer reaches 10 metres");
                        Assert(s.catalog.entries.Length==18&&s.catalog.entries.All(e=>e.held||e.id==CrackerId.GrandShow||e.prefab),"All eighteen catalog entries resolve");
                        Assert(s.sound.bank.sounds.All(b=>b.clips.Length>0&&b.clips.All(c=>c)),"Every sound id has its clips");
                        Snapshot("01-welcome");s.thali.ForgetDock(-42,.5f,.34f);s.Begin();deadline=EditorApplication.timeSinceStartup+1.4;return;
                    case -2:
                        Assert(s.thali.gameObject.activeInHierarchy,"The experience shows the thali immediately");
                        Look(s,new Vector3(0,1.5f,-.15f),s.thali.transform.position);Snapshot("02-thali");ResetView(s);
                        CheckSidePanel(s);
                        CheckPopsTile(s);
                        CheckRangoliStays(s);
                        CheckEdgeHalo(s);
                        Assert(!SkyHeadsLit(s),"No comet glow waits in the sky before the first shell");

                        Probe.Line=s.rig.Right.Line;Probe.Reticle=s.rig.Right.Reticle;
                        OtherHand.Line=s.rig.Pointers[0].Line;OtherHand.Reticle=s.rig.Pointers[0].Reticle;
                        var head=s.rig.view.transform;var eye=head.position;var ahead=Flat(head.forward).normalized;

                        // The tray sits to the side, and can be moved.
                        float trayBearing=Vector3.SignedAngle(ahead,Flat(s.thali.transform.position-eye),Vector3.up);
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO tray bearing {trayBearing:F0} degrees\n");
                        Assert(trayBearing<-25,"The tray waits to the left, out of the central view");
                        Probe.Pressed=true;Probe.WasPressed=true;Probe.PinchPoint=s.thali.transform.position;Probe.Ray=new Ray(eye,(s.thali.transform.position-eye).normalized);
                        s.thali.BeginMove(Probe);Assert(s.thali.Moving&&Probe.Grasp==(IFestivalGrasp)s.thali,"Pinching the bar takes hold of the tray");
                        Probe.PinchPoint=eye+head.right*.5f+ahead*.25f+Vector3.down*.3f;s.thali.Steer(Probe);
                        Probe.Pressed=false;s.thali.Steer(Probe);
                        Assert(!s.thali.Moving&&s.thali.dockYaw>20,"The tray can be moved to the other side and stays there");
                        s.thali.ForgetDock(-42,.5f,.34f);

                        var prediction=FestivalToss.PredictLanding(new Vector3(0,1.3f,0),new Vector3(0,2,4),0);
                        Assert(prediction.z>2.7f&&prediction.y==0,"Throw preview follows release velocity and gravity");
                        CheckThrowScaling();

                        var gripEdge=new FestivalPointer { Gripping=true };
                        Assert(gripEdge.GrippedNow&&gripEdge.Holding,"A full-hand grip activates grabbing without an index pinch");
                        gripEdge.Gripping=false;gripEdge.WasGripping=true;gripEdge.Pressed=true;
                        Assert(!gripEdge.ReleasedNow,"Switching from grip to pinch does not drop an item");

                        Probe.ResetMotion();Probe.Gripping=false;Probe.WasGripping=false;Probe.NearInteraction=true;
                        Probe.Pressed=true;Probe.WasPressed=true;Probe.PinchPoint=new Vector3(-.2f,1,.4f);
                        s.placement.TakeNew(s.catalog.Find(CrackerId.Popper),Probe);dropped=s.placement.Items.Last();((PopperItem)dropped).impactSpeed=99;
                        Assert(dropped.transform.localScale.x<.5f,"A new item is small from its first frame in the hand");
                        Probe.Pressed=false;Probe.Grasp.Steer(Probe);
                        Assert(Probe.Grasp==null&&dropped.Airborne,"A short pinch releases the pop immediately, like a held tool");

                        Probe.ResetMotion();Probe.Gripping=false;Probe.WasGripping=false;
                        Probe.Pressed=true;Probe.WasPressed=true;Probe.PinchPoint=new Vector3(0,1.1f,.5f);Probe.Ray=new Ray(eye,Vector3.forward);
                        s.placement.TakeNew(s.catalog.Find(CrackerId.Popper),Probe);
                        thrown=s.placement.Items.Last();((PopperItem)thrown).impactSpeed=99;
                        Probe.Grasp.Steer(Probe);
                        Assert(Probe.Grasp!=null&&thrown.Carrier==Probe,"Holding a pinch keeps a pop in the hand, ready to throw");
                        Probe.Grasp.Lost(Probe);
                        Assert(Probe.Grasp!=null&&thrown.Carrier==Probe,"Tracking loss does not auto-place a held cracker");
                        Probe.Pressed=true;Probe.WasPressed=false;Probe.Grasp.Steer(Probe);
                        Probe.PinchPoint=new Vector3(0,1.3f,.5f);Probe.Rotation=Quaternion.identity;Probe.SampleMotion(0);
                        Probe.PinchPoint=new Vector3(0,1.5f,.9f);Probe.Rotation=Quaternion.Euler(0,30,0);Probe.SampleMotion(.1f);
                        Probe.Pressed=false;Probe.WasPressed=true;Probe.Grasp.Steer(Probe);
                        Assert(Probe.Grasp==null&&thrown.GetComponent<FestivalToss>().Flying,"Opening the hand releases a physical throw");
                        Assert(thrown.GetComponent<FestivalToss>().Velocity.z>3.9f,"The throw preserves measured hand velocity");
                        Assert(thrown.GetComponent<FestivalToss>().AngularVelocity.y>5,"The throw retains hand angular momentum");
                        Assert(thrown.CanGrab(Probe),"An airborne cracker can be caught again");
                        var flightZone=thrown.GetComponent<GrabZone>();
                        Probe.Gripping=true;Probe.WasGripping=true;Probe.ClosedAt=Time.unscaledTime-1;Probe.PinchPoint=flightZone.WorldCentre;
                        Assert(!s.placement.TryCatch(Probe),"A hand closed long ago does not snatch a passing cracker");
                        Probe.ClosedAt=Time.unscaledTime;Probe.PinchPoint=flightZone.WorldCentre+Vector3.right*(flightZone.radius*2);
                        Assert(!s.placement.TryCatch(Probe),"A cracker out of reach is not caught");
                        Probe.PinchPoint=flightZone.WorldCentre+Vector3.right*(flightZone.radius*1.4f);
                        Assert(s.placement.TryCatch(Probe),"A hand closing just as a cracker arrives catches it, even from outside the grab region");
                        Assert(Probe.Grasp!=null&&!thrown.GetComponent<FestivalToss>().Flying,"Catching cancels free flight and returns the cracker to the hand");
                        Probe.Gripping=false;Probe.Pressed=false;Probe.Grasp.Steer(Probe);
                        s.StartCoroutine(WatchFlight(s,thrown.GetComponent<FestivalToss>()));
                        step=30;deadline=EditorApplication.timeSinceStartup+2.5;return;
                    case 30:
                        var eye2=s.rig.view.transform.position;
                        Assert(thrown.transform.position.z>3&&thrown.State==ItemState.Ready,"A thrown cracker lands beyond two metres and becomes usable");
                        Assert(Mathf.Abs(thrown.transform.position.y-s.placement.FloorHeight)<.01f,"The thrown cracker settles on the tracked floor");
                        Assert(Flat(dropped.transform.position-new Vector3(-.2f,0,.4f)).magnitude<.02f&&dropped.State==ItemState.Ready,"A slow release lands directly below the hand, with no forced distance");
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO flight: {flightFrames} airborne frames, {flightStalls} without movement; {landingSounds} landing sounds, the last {settledAt-lastLandingSound:F2} s before it came to rest\n");
                        Assert(flightFrames>10&&flightStalls==0,"A thrown cracker moves on every rendered frame");
                        Assert(landingSounds>=1&&settledAt-lastLandingSound>.25f,"Landing sounds belong to floor contact, and coming to rest is silent");
                        Clear(s);Probe.ResetMotion();OtherHand.ResetMotion();

                        // A fast throw can outrun hand tracking, and the hand may come back already open.
                        Probe.Gripping=true;Probe.WasGripping=true;Probe.Pressed=false;Probe.WasPressed=false;Probe.Rotation=Quaternion.identity;Probe.PinchPoint=eye2+Vector3.forward*.5f;
                        s.placement.TakeNew(s.catalog.Find(CrackerId.Popper),Probe);var vanished=s.placement.Items.Last();((PopperItem)vanished).impactSpeed=99;
                        Probe.SampleMotion(0);Probe.PinchPoint+=new Vector3(0,.1f,.4f);Probe.SampleMotion(.1f);Probe.Grasp.Steer(Probe);
                        Probe.LoseMotion(Time.unscaledTime);Probe.Grasp.Lost(Probe);
                        Assert(Probe.Grasp!=null,"Losing the hand holds the cracker where it was");
                        Probe.Gripping=false;Probe.WasGripping=false;Probe.SampleMotion(Time.unscaledTime);Probe.Grasp.Steer(Probe);
                        Assert(Probe.Grasp==null&&vanished.Airborne,"A hand that returns open lets go without another pinch");
                        Assert(vanished.GetComponent<FestivalToss>().Velocity.z>3,"A throw survives a brief tracking dropout");
                        Probe.ResetMotion();Probe.Gripping=true;Probe.WasGripping=true;Probe.PinchPoint=eye2+Vector3.forward*.5f;
                        s.placement.TakeNew(s.catalog.Find(CrackerId.Popper),Probe);var stale=s.placement.Items.Last();((PopperItem)stale).impactSpeed=99;
                        Probe.SampleMotion(0);Probe.PinchPoint+=new Vector3(0,.1f,.4f);Probe.SampleMotion(.1f);Probe.Grasp.Steer(Probe);
                        Probe.LoseMotion(Time.unscaledTime-1);Probe.Gripping=false;Probe.WasGripping=false;Probe.SampleMotion(Time.unscaledTime);Probe.Grasp.Steer(Probe);
                        Assert(stale.Airborne&&stale.GetComponent<FestivalToss>().Velocity.magnitude<.1f,"After a long dropout the cracker just drops");
                        Clear(s);Probe.ResetMotion();Probe.Gripping=false;Probe.WasGripping=false;

                        // A ray-selected cracker previews and settles directly on the floor.
                        var head2=s.rig.view.transform;var flatAhead=Flat(head2.forward).normalized;
                        Probe.NearInteraction=false;Probe.Pressed=true;Probe.WasPressed=true;Probe.PinchPoint=eye2+flatAhead*.3f+Vector3.down*.4f;Probe.Ray=new Ray(eye2,(flatAhead*1.7f+Vector3.down*eye2.y).normalized);
                        s.placement.TakeNew(s.catalog.Find(CrackerId.Bomb),Probe);quickPlaced=s.placement.Items.Last();
                        Probe.Pressed=false;Probe.Grasp.Steer(Probe);
                        Assert(Probe.Grasp==null&&!quickPlaced.Airborne&&quickPlaced.State==ItemState.Ready,"A ray selection settles a cracker directly on the floor");
                        quickSpot=s.placement.LastLanding;
                        Assert(Mathf.Abs(Flat(quickSpot-eye2).magnitude-1.7f)<.01f,"A ray selection preserves the aimed floor position");
                        Probe.Pressed=true;Probe.Ray=new Ray(eye2,Vector3.down);
                        Assert(Mathf.Abs(s.placement.RayLandingPoint(Probe.Ray,null,.1f).y-s.placement.FloorHeight)<.001f,"A downward ray stays on the tracked floor");
                        Probe.Ray=new Ray(eye2,flatAhead);
                        Assert(Mathf.Abs(Flat(s.placement.RayLandingPoint(Probe.Ray,null,.1f)-eye2).magnitude-1.5f)<.01f,"A ray without a floor intersection falls back to 1.5 metres");
                        Probe.Pressed=false;
                        foreach(var kind in new[]{CrackerId.Chakri,CrackerId.Ladi,CrackerId.Diya}){var physics=CrackerPhysics.For(kind);File.AppendAllText(Output+"/playmode-validation.txt",$"INFO {kind}: bounce {physics.bounce:F2}, wobble {physics.wobble:F1} rad/s, thud {physics.thud:F2}\n");}
                        Assert(CrackerPhysics.For(CrackerId.Chakri).bounce>CrackerPhysics.For(CrackerId.Diya).bounce&&CrackerPhysics.For(CrackerId.Ladi).bounce==0,"Materials land differently: a disc rattles, clay thuds, a string lies dead");
                        Assert(s.catalog.entries.Where(e=>e.prefab).All(e=>CrackerPhysics.For(e.id).throwable==(e.id==CrackerId.Popper)),"Only pops are thrown");

                        // Either hand, and passing between them.
                        Probe.NearInteraction=true;Probe.Gripping=true;Probe.WasGripping=true;Probe.Pressed=false;Probe.WasPressed=false;Probe.PinchPoint=eye2+Vector3.forward*.4f;Probe.Ray=new Ray(eye2,Vector3.forward);
                        s.placement.TakeNew(s.catalog.Find(CrackerId.Anar),Probe);
                        var carried=UnityEngine.Object.FindObjectsByType<FestivalItem>(FindObjectsSortMode.None).First(i=>i.Carrier==Probe);
                        var region=carried.GetComponent<GrabZone>();
                        OtherHand.PinchPoint=region.WorldCentre+Vector3.up*(region.radius*.8f);OtherHand.Ray=new Ray(eye2,Vector3.forward);OtherHand.Gripping=true;OtherHand.WasGripping=true;OtherHand.Pressed=false;OtherHand.WasPressed=false;
                        Assert(GrabZone.Nearest(OtherHand,1,out _)==region,"The other hand finds the grab region");
                        OtherHand.PinchPoint=region.WorldCentre+Vector3.up*(region.radius*1.3f);
                        Assert(GrabZone.Nearest(OtherHand,1,out _)==null,"Just outside the region, nothing can be grabbed");
                        OtherHand.PinchPoint=region.WorldCentre;carried.Press(OtherHand);
                        Assert(Probe.Grasp==null&&OtherHand.Grasp!=null&&carried.Carrier==OtherHand,"The other hand can take a carried cracker");
                        s.placement.TakeNew(s.catalog.Find(CrackerId.Bomb),Probe);
                        Assert(Probe.Grasp!=null&&OtherHand.Grasp!=null,"Each hand can carry its own item");
                        OtherHand.Grasp.Steer(OtherHand);Probe.Grasp.Steer(Probe);
                        Probe.Pressed=false;OtherHand.Pressed=false;Probe.Gripping=false;OtherHand.Gripping=false;Probe.Grasp.Steer(Probe);OtherHand.Grasp.Steer(OtherHand);
                        Assert(Probe.Grasp==null&&OtherHand.Grasp==null&&s.placement.Items.Count==3,"Letting go with both hands sets both items down");
                        s.sparkler.Take(Probe);s.sparkler.Ignite();OtherHand.Pressed=true;s.sparkler.Press(OtherHand);
                        Assert(s.sparkler.Holder==OtherHand&&Probe.Grasp==null&&s.sparkler.Lit,"A lit phuljhadi passes between hands without going out");
                        s.sparkler.Return();OtherHand.Pressed=false;OtherHand.WasPressed=false;Probe.WasPressed=false;OtherHand.WasGripping=false;Probe.WasGripping=false;
                        step=31;deadline=EditorApplication.timeSinceStartup+1.6;return;
                    case 31:
                        if(quickPlaced.Airborne&&audioWaits++<40){step=31;deadline=EditorApplication.timeSinceStartup+.25;return;}
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO placed bomb after {audioWaits} extra waits: {quickPlaced.State}, airborne {quickPlaced.Airborne}, {Flat(quickPlaced.transform.position-quickSpot).magnitude:F3} m from preview, at {quickPlaced.transform.position}, preview {quickSpot}\n");
                        Assert(quickPlaced.State==ItemState.Ready&&Flat(quickPlaced.transform.position-quickSpot).magnitude<.005f,"The ray-placed cracker remains at its preview position");
                        Clear(s);

                        butterfly=(ButterflyItem)Place(s,CrackerId.Butterfly,new Vector3(-.6f,0,1.8f));butterfly.fuseSeconds=.1f;butterfly.Ignite();
                        popper=(PopperItem)Place(s,CrackerId.Popper,new Vector3(.3f,1,1.6f));popper.Lift();
                        popperRing=FestivalToss.PredictLanding(popper.transform.position,new Vector3(0,1,1),s.placement.FloorHeight,popper.Physics.airDrag);
                        var popToss=popper.gameObject.AddComponent<FestivalToss>();popToss.Launch(popper,s.placement,new Vector3(0,1,1),new Vector3(0,10,0));
                        Assert(!popper.NeedsFlame&&!popper.Popped,"The hand popper waits for an impact without a fuse");

                        // Touching a flame to a fuse.
                        touch=Place(s,CrackerId.Caps,new Vector3(.2f,0,1.9f));
                        Probe.Pressed=true;Probe.Rotation=Quaternion.identity;Probe.PinchPoint=new Vector3(.2f,.4f,1.2f);s.lighter.Take(Probe);s.lighter.Steer(Probe);
                        Assert(s.lighter.FlameOn,"Taking the flip-top lighter lights it");
                        Probe.Pressed=false;Probe.WasPressed=false;s.lighter.Steer(Probe);
                        Assert(!s.lighter.FlameOn&&s.lighter.Dropped&&Probe.Grasp==null,"Opening the hand extinguishes and drops the lighter");
                        Probe.Pressed=true;s.lighter.Take(Probe);s.lighter.Steer(Probe);
                        Assert(s.lighter.FlameOn&&s.lighter.Holder==Probe,"A dropped lighter can be recovered without extinguishing it");
                        Probe.PinchPoint=touch.fuse.position-(s.lighter.tip.position-s.lighter.transform.position);s.lighter.Steer(Probe);
                        step=-1;deadline=EditorApplication.timeSinceStartup+.9;return;
                    case -1:
                        Assert(touch.State==ItemState.Burning,"A flame touching the fuse lights it");
                        Assert(popper&&popper.Popped,"The thrown popper snaps on its first firm floor impact");
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO pop landed {Flat(popper.transform.position-popperRing).magnitude*100:F1} cm from its ring\n");
                        Assert(Flat(popper.transform.position-popperRing).magnitude<.03f,"The landing ring marks where a thrown pop really comes down");
                        Assert(butterfly.body.position.y>butterfly.transform.position.y+.1f,"The butterfly lifts with a short fluttering flight");
                        Look(s,new Vector3(-.6f,.5f,.8f),butterfly.body.position);Snapshot("03a-butterfly");ResetView(s);
                        Probe.Pressed=false;s.lighter.Steer(Probe);Assert(!s.lighter.FlameOn&&s.lighter.Dropped,"Letting go extinguishes the lighter and frees the hand");
                        s.lighter.Return();Assert(Probe.Held==null,"The lighter goes back");
                        audioWaits=0;step=19;deadline=EditorApplication.timeSinceStartup+.25;return;
                    case 19:
                        // A butterfly in flight is as loud as the probe and passes close in front of the head, so measure only once everything has burnt out.
                        if(s.placement.Items.Any(i=>i.State==ItemState.Burning)&&audioWaits++<60){step=19;deadline=EditorApplication.timeSinceStartup+.25;return;}
                        Assert(s.placement.Items.All(i=>i.State!=ItemState.Burning),"Nothing else is burning while the ears are measured, found "+string.Join(", ",s.placement.Items.Where(i=>i.State==ItemState.Burning).Select(i=>i.name)));
                        audioWaits=0;ResetView(s);probeSource=new GameObject("Audio probe");probeSource.transform.position=s.rig.view.transform.position+s.rig.view.transform.right*-2.2f;
                        earsWereMuted=EditorUtility.audioMasterMute;EditorUtility.audioMasterMute=false;File.AppendAllText(Output+"/playmode-validation.txt",$"INFO Game view audio muted before measuring: {earsWereMuted}\n");
                        probeLoop=s.sound.StartLoop("anar",probeSource.transform,1,.05f);step=20;deadline=EditorApplication.timeSinceStartup+3;return;
                    case 20:
                        // The mixer takes a moment to start, so wait for sound rather than a fixed delay.
                        if(Mathf.Max(Rms(0),Rms(1))<.002f&&audioWaits++<24){step=20;deadline=EditorApplication.timeSinceStartup+.5;return;}
                        if(!ReadEars(out leftEar,out rightEar)){step=20;return;}
                        probeSource.transform.position=s.rig.view.transform.position+s.rig.view.transform.right*2.2f;deadline=EditorApplication.timeSinceStartup+1.5;return;
                    case 21:
                        if(!ReadEars(out float otherLeft,out float otherRight)){step=21;return;}
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO source on the left:  L {Db(leftEar):F1} dBFS  R {Db(rightEar):F1} dBFS  lead {Db(leftEar)-Db(rightEar):F1} dB\nINFO source on the right: L {Db(otherLeft):F1} dBFS  R {Db(otherRight):F1} dBFS  lead {Db(otherRight)-Db(otherLeft):F1} dB\n");
                        EditorUtility.audioMasterMute=earsWereMuted;
                        Assert(leftEar>.002f||rightEar>.002f,"Spatial audio reaches the listener at an audible level");
                        Assert(leftEar>rightEar*1.25f&&otherRight>otherLeft*1.25f,"A sound on the left is louder in the left ear, and the reverse");
                        probeLoop.Stop(.05f);UnityEngine.Object.Destroy(probeSource);step=22;deadline=EditorApplication.timeSinceStartup+.2;return;
                    case 22:
                        foreach(var p in new[]{new Vector3(-.3f,0,1.15f),new Vector3(0,0,1f),new Vector3(.3f,0,1.15f),new Vector3(.75f,0,1.5f),new Vector3(1.1f,0,1.55f)})Place(s,CrackerId.Diya,p);
                        foreach(var item in s.placement.Items)if(item is DiyaItem)item.Ignite();
                        Assert(s.placement.Items.Where(i=>i is DiyaItem).All(i=>i.State==ItemState.Burning),"Diyas light from the accessible path");
                        step=23;break;
                    case 23:
                        Assert(s.IsComplete(CrackerId.Diya),"Lighting a diya completes the first journey step");
                        Look(s,new Vector3(.05f,.42f,.42f),new Vector3(0,.08f,1.05f));Snapshot("03-diya-flames");
                        Look(s,new Vector3(.9f,.2f,1.17f),new Vector3(.75f,.08f,1.5f));Snapshot("04-diya-closeup");ResetView(s);
                        s.sparkler.Take(s.rig.Right);s.sparkler.Ignite();step=0;deadline=EditorApplication.timeSinceStartup+1.6;return;
                    case 0:
                        Assert(s.sparkler.Lit,"Phuljhadi lights while held");
                        Look(s,s.sparkler.burnPoint.position+new Vector3(.05f,.12f,-.42f),s.sparkler.burnPoint.position);Snapshot("05-phuljhadi");ResetView(s);s.sparkler.Return();
                        Assert(s.sparkler.Holder==null&&s.rig.Right.Held==null,"Returning a held item frees the hand");
                        anar=Place(s,CrackerId.Anar,new Vector3(-.7f,0,2));pot=Place(s,CrackerId.FlowerPot,new Vector3(.7f,0,2));chakri=Place(s,CrackerId.Chakri,new Vector3(0,0,1.9f));
                        Assert(anar.State==ItemState.Ready&&anar.fuse,"Placed crackers wait with a fuse");
                        anar.Ignite();pot.Ignite();chakri.Ignite();Assert(anar.State==ItemState.Burning,"Ignite starts the fuse");
                        deadline=EditorApplication.timeSinceStartup+6.5;return;
                    case 1:
                        Look(s,new Vector3(0,1.4f,-.4f),new Vector3(0,.7f,2));Snapshot("06-ground-fountains");
                        Look(s,new Vector3(.22f,.7f,1.35f),new Vector3(0,.04f,1.9f));Snapshot("07-chakri");ResetView(s);
                        ladi=Place(s,CrackerId.Ladi,new Vector3(-.6f,0,1.2f));bigLadi=Place(s,CrackerId.BigLadi,new Vector3(.25f,0,1.5f));caps=Place(s,CrackerId.Caps,new Vector3(.95f,0,1.15f));bomb=Place(s,CrackerId.Bomb,new Vector3(1.35f,0,2.5f));
                        var small=(ChainItem)ladi;var big=(ChainItem)bigLadi;
                        float Pitch(ChainItem chain)=>Vector3.Distance(chain.pieces[0].position,chain.pieces[1].position);
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO ladi: small {small.pieces.Length} pairs at {Pitch(small)*1000:F1} mm, big {big.pieces.Length} pairs at {Pitch(big)*1000:F1} mm\n");
                        Assert(big.pieces.Length>=small.pieces.Length*2,"The big ladi has at least twice the crackers");
                        Assert(Pitch(small)<.012f&&Pitch(big)<.016f,"Ladi crackers are packed shoulder to shoulder");
                        Look(s,new Vector3(.1f,.62f,.72f),new Vector3(-.05f,.02f,1.35f));Snapshot("08a-ladi-compact");ResetView(s);
                        ladi.Ignite();bigLadi.Ignite();caps.Ignite();bomb.Ignite();deadline=EditorApplication.timeSinceStartup+3.4;return;
                    case 2:
                        Look(s,new Vector3(.1f,1.05f,.35f),new Vector3(0,.12f,1.4f));Snapshot("08-ladi-pops");ResetView(s);deadline=EditorApplication.timeSinceStartup+5.4;return;
                    case 3:
                        var blast=(BlastItem)bomb;
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO bomb fuse {blast.LastFuseSeconds:F1} s, clouds {blast.clouds.particleCount} puffs\n");
                        Assert(blast.LastFuseSeconds>=5&&blast.LastFuseSeconds<=7,"The bomb's fuse burns for 5 to 7 seconds");
                        var visitor=s.rig.view.transform.position;var bombSite=blast.LastDetonationPoint;
                        Assert(blast.State!=ItemState.Ready&&blast.clouds.particleCount>=20,"The blast emits a dense smoke plume");
                        Assert(blast.flames.All(f=>Vector3.Distance(f.transform.position,bombSite)<.01f),"The brief flame is at the detonation site");
                        var report=s.sound.LastCue("bomb");
                        Assert(report.frame==blast.DetonationFrame&&s.sky.LastFlashFrame==blast.DetonationFrame&&blast.PuffsAtDetonation>=20,"Report, flash and first smoke all start on the detonation frame");
                        Assert(Vector3.Distance(report.position,bombSite)<.001f&&Vector3.Distance(s.sky.LastFlashPoint,bombSite)<.001f,"Report and flash both come from the charge itself");
                        var cloudBuffer=new ParticleSystem.Particle[200];int cloudCount=blast.clouds.GetParticles(cloudBuffer);Vector3 cloudCentre=Vector3.zero;
                        for(int i=0;i<cloudCount;i++) cloudCentre+=cloudBuffer[i].position;
                        cloudCentre/=Mathf.Max(cloudCount,1);
                        float plumeWidth=0,plumeHeight=0;
                        for(int i=0;i<cloudCount;i++){plumeWidth=Mathf.Max(plumeWidth,Flat(cloudBuffer[i].position-bombSite).magnitude);plumeHeight+=cloudBuffer[i].position.y-bombSite.y;}
                        plumeHeight/=Mathf.Max(cloudCount,1);
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO {Time.time-blast.DetonationTime:F1} s after detonation: cloud centre {Flat(cloudCentre-bombSite).magnitude:F2} m from bomb, widest puff {plumeWidth:F2} m out, mean height {plumeHeight:F2} m\n");
                        Assert(Flat(cloudCentre-bombSite).magnitude<.45f,"The expanding cloud stays centred over the bomb");
                        Assert(plumeWidth<1.2f,"The plume stays at the charge rather than running out across the room");
                        Assert(plumeHeight>.4f&&plumeHeight<1.6f,"The plume rises steadily, neither stuck at the charge nor rushing to the ceiling");
                        Assert(!blast.model.activeSelf,"The charge disappears at detonation");
                        Assert(popper==null||popper.Popped,"The thrown popper snaps once on landing");
                        var spinner=(SpinnerItem)chakri;
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO chakri: peak {spinner.PeakAngularSpeed:F0} rad/s, drifted {spinner.LongestDrift*100:F1} cm, coasted {spinner.CoastSeconds:F1} s after burnout\n");
                        Assert(spinner.PeakAngularSpeed>70,"The chakri reaches the increased angular speed");
                        Assert(spinner.LongestDrift>.03f,"The chakri skitters about the floor while it burns");
                        Assert(spinner.CoastSeconds>1,"The chakri keeps turning on its own momentum after burnout");
                        Look(s,visitor,bombSite+Vector3.up*.9f);Snapshot("09-bomb");ResetView(s);
                        rocket=Place(s,CrackerId.Rocket,new Vector3(-.7f,0,1.6f));rocket.Ignite();deadline=EditorApplication.timeSinceStartup+3.6;return;
                    case 4:
                        Look(s,new Vector3(0,1.5f,0),new Vector3(-.7f,10,1.6f));Snapshot("10-rocket-climb");ResetView(s);deadline=EditorApplication.timeSinceStartup+2.5;return;
                    case 5:
                        if(RocketItem.LastFlight.seconds<=0&&audioWaits++<70){step=5;deadline=EditorApplication.timeSinceStartup+.25;return;}
                        var flight=RocketItem.LastFlight;var eye5=s.rig.view.transform.position;
                        float elevation=Mathf.Atan2(flight.burstPoint.y-eye5.y,Flat(flight.burstPoint-eye5).magnitude)*Mathf.Rad2Deg;
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO rocket: {flight.seconds:F2} s, burst height {flight.apexHeight:F1} m, downrange {flight.downrange:F1} m, {flight.speedAtBurnout:F1} m/s at burnout, burst {elevation:F0} degrees above the horizon\n");
                        Assert(!flight.sideSpeedGrewAfterBurnout,"After burnout the rocket is never steered: sideways speed only falls");
                        Assert(flight.apexHeight>17&&flight.apexHeight<19&&flight.rightwardDistance>1&&flight.rightwardDistance<5,"The rocket climbs to about 18 metres with a modest rightward drift");
                        Assert(flight.climbRateAtBurst>8.5f&&flight.downrange/flight.apexHeight<.1f,"The rocket bursts on a steep ascent before any terminal bend");
                        Assert(elevation>80,"The rocket bursts high above its launch site while still climbing");
                        Look(s,eye5,flight.burstPoint);Snapshot("11-rocket-burst");ResetView(s);

                        var paper=UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Where(p=>p.name=="Litter"&&p.transform.parent==null).ToArray();
                        int pieces=paper.Sum(p=>p.particleCount);
                        var buffer=new ParticleSystem.Particle[400];float lowest=0;int settled=0;
                        foreach(var scrap in paper){int count=scrap.GetParticles(buffer);for(int i=0;i<count;i++){if(buffer[i].position.y<.06f)settled++;lowest=Mathf.Min(lowest,buffer[i].position.y);}}
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO litter: {pieces} scraps, {settled} on the floor\n");
                        Assert(paper.Length>=2&&pieces>100,"Both ladis leave paper behind after they finish");
                        Assert(settled>pieces*.7f&&lowest>-.02f,"The paper settles on the floor and does not fall through it");
                        Look(s,new Vector3(-.1f,.9f,.55f),new Vector3(-.1f,0,1.4f));Snapshot("11a-ladi-litter");ResetView(s);

                        twelve=Place(s,CrackerId.TwelveShot,new Vector3(-.5f,0,1.6f));twelve.Ignite();deadline=EditorApplication.timeSinceStartup+6.2;return;
                    case 6:
                        Look(s,new Vector3(0,1.5f,0),new Vector3(0,20,30));Snapshot("12-twelve-shot");ResetView(s);deadline=EditorApplication.timeSinceStartup+7;return;
                    case 7:
                        fifteen=Place(s,CrackerId.FifteenShot,new Vector3(.5f,0,1.6f));sky=Place(s,CrackerId.SkyShot,new Vector3(0,0,1.8f));fifteen.Ignite();sky.Ignite();audioWaits=0;deadline=EditorApplication.timeSinceStartup+7.5;return;
                    case 8:
                        if(audioWaits==0){Look(s,new Vector3(0,1.5f,0),new Vector3(4,19,30));Snapshot("13-fifteen-shot");ResetView(s);}

                        // One shell at a time takes over half a minute a box, so wait for the boxes, not a fixed time.
                        if((((MortarItem)fifteen).State==ItemState.Burning||s.sky.ShellsInFlight>0)&&audioWaits++<320){step=8;deadline=EditorApplication.timeSinceStartup+.25;return;}
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO sky after the boxes: {s.sky.ShellsInFlight} shells in flight, grand show running {s.sky.ShowRunning}, heads lit at [{string.Join("; ",s.sky.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Head"&&r.enabled).Select(r=>r.transform.position.ToString("F1")))}], waited {audioWaits} ticks\n");
                        Assert(s.sky.ShellsInFlight==0&&!SkyHeadsLit(s),"Nothing of a shell stays in the sky after it bursts");
                        Assert(SkyShow.LastShellFlight.seconds>0&&SkyShow.LastShellFlight.downrange<.01f,"Shells fly a straight line and burst exactly at their point");
                        var tray=(MortarItem)twelve;var gaps=tray.BurstPoints.Zip(tray.BurstPoints.Skip(1),(a,b)=>Flat(a-b).magnitude).ToArray();
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO 12 shot: first tube {tray.FirstShotWait:F2} s after the fuse, shortest gap {tray.ShortestGap:F2} s, steps {gaps.Min():F1} to {gaps.Max():F1} m\n");
                        Assert(tray.BurstPoints.Count==12&&tray.FirstShotWait>=.6f,"A box pauses after the fuse, then fires every tube");
                        Assert(tray.BurstPoints.All(point=>point.y>=19.95f&&point.y<=22.05f),"Box shells burst between 20 and 22 metres");
                        Assert(tray.ShortestGap>2&&((MortarItem)fifteen).ShortestGap>2,"Each tube waits until the shell before it has burst");
                        Assert(gaps.All(g=>g>3&&g<4),"Each shot steps to the neighbouring tube in the tray");
                        File.AppendAllText(Output+"/playmode-validation.txt",$"INFO butterfly: reached {butterfly.FurthestReach:F2} m, came within {butterfly.ClosestToVisitor:F2} m of the visitor, returned {butterfly.Returns} times\n");
                        Assert(butterfly.FurthestReach>1&&butterfly.Returns>=2,"The butterfly alternates wide arcs with close returns");
                        Assert(butterfly.ClosestToVisitor>=.5f&&butterfly.ClosestToVisitor<1.3f,"The butterfly comes close without entering personal space");
                        Look(s,new Vector3(0,1.5f,-.2f),new Vector3(0,1.9f,4));Snapshot("14-room-under-sky");ResetView(s);deadline=EditorApplication.timeSinceStartup+9;return;
                    case 9:
                        s.sky.BeginGrandShow();Assert(s.sky.ShowRunning,"Grand show begins");deadline=EditorApplication.timeSinceStartup+15;return;
                    case 10:
                        Look(s,new Vector3(0,1.5f,0),new Vector3(0,18,30));Snapshot("15-grand-show");ResetView(s);
                        var particles=UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
                        File.AppendAllText(Output+"/playmode-validation.txt","INFO particle systems alive: "+particles+"\n");
                        Assert(particles<60,"Particle-system count stays bounded");
                        s.ResetAll();audioWaits=0;deadline=EditorApplication.timeSinceStartup+.6;return;
                    case 11:
                        if(!s.Started&&audioWaits++<40){step=11;deadline=EditorApplication.timeSinceStartup+.25;return;}
                        Assert(s.Started&&!UnityEngine.Object.FindFirstObjectByType<FestivalSplash>(),"Reset restarts the experience without the launch screen");
                        Assert(s.placement.Items.Count==0&&!s.sky.ShowRunning,"Reset clears every placed item and the show");
                        Assert(s.rig.transform.position==Vector3.zero,"The visitor is never moved by the experience");
                        Assert(errors==0,"No runtime errors or exceptions");
                        File.AppendAllText(Output+"/playmode-validation.txt","COMPLETE\n");DiwaliProfileRun.Finish("Build: "+DateTime.Now);RestoreDock();testing=false;EditorApplication.isPlaying=false;return;
                }
                deadline=EditorApplication.timeSinceStartup+1;
            } catch(Exception ex) {File.AppendAllText(Output+"/playmode-validation.txt",ex+"\nFAILED\n");RestoreDock();testing=false;EditorApplication.isPlaying=false;Debug.LogException(ex);}
        }
        [MenuItem("Diwali/Capture Mixed Reality Preview")]
        public static void SnapshotMenu() {Snapshot("mr-preview");}
        internal static void Snapshot(string name)
        {
            if(DiwaliProfileRun.Active)return; // A 1080p capture would swamp the numbers being measured.
            Directory.CreateDirectory(Output);var session=UnityEngine.Object.FindFirstObjectByType<DiwaliSession>();var camera=session?session.rig.view:Camera.main;if(!camera)throw new Exception("No festival camera to capture.");
            var old=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1920,1080,24);
            try {camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);}
            finally {camera.targetTexture=old;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
        static void Inspect()
        {
            var lines=new System.Collections.Generic.List<string>();
            foreach(var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))lines.Add($"CAMERA {c.name} {c.transform.position} enabled {c.enabled}");
            foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.enabled))lines.Add($"MESH {r.name} at {r.transform.position} bounds {r.bounds.center} size {r.bounds.size}");
            File.WriteAllLines(Output+"/scene-inspection.txt",lines);
        }
        static void InspectXR()
        {
            var lines=new System.Collections.Generic.List<string>{"Selected group: "+EditorUserBuildSettings.selectedBuildTargetGroup,"Active target: "+EditorUserBuildSettings.activeBuildTarget};
            foreach(var group in new[]{BuildTargetGroup.Android,BuildTargetGroup.Standalone}) {var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(group);lines.Add(group+" settings: "+(xr?xr.name:"missing")+" MetaQuestFeature: "+(xr&&xr.GetFeature<MetaQuestFeature>()?xr.GetFeature<MetaQuestFeature>().name:"missing"));}
            File.WriteAllLines(Output+"/xr-settings.txt",lines);
        }
        [MenuItem("Diwali/Build Quest APK")]
        public static void BuildAndroid()
        {
            // OpenXR 1.19 validation reads the selected group separately from the active target.
            EditorUserBuildSettings.selectedBuildTargetGroup=BuildTargetGroup.Android;
            DiwaliSceneBuilder.ConfigureQuest();if(File.Exists(DiwaliSceneBuilder.MRScenePath))DiwaliSceneBuilder.ConfigureMR();AssetDatabase.SaveAssets();Directory.CreateDirectory("Builds");
            DiwaliSceneBuilder.ConfigureSceneFlow();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{DiwaliSceneBuilder.SplashScenePath,DiwaliSceneBuilder.MRScenePath},locationPathName="Builds/DiwaliUtsav.apk",target=BuildTarget.Android,options=BuildOptions.None});
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/android-build.txt",$"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Quest Android build failed. See Unity Editor log.");
        }
    }
}
