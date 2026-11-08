using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SpatialTracking;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
#if UNITY_EDITOR
using UnityEngine.XR.OpenXR;
#endif
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace Diwali
{
    /// <summary>Anything that can be pointed at and pinched: one route for hands, controllers and the mouse.</summary>
    public interface IFestivalTarget
    {
        void Hover(bool on);
        void Press(FestivalPointer pointer);
    }

    /// <summary>Whatever a hand has hold of. While it lasts, that hand's pinch belongs to it.</summary>
    public interface IFestivalGrasp
    {
        void Steer(FestivalPointer pointer);
        void Lost(FestivalPointer pointer);
    }

    public sealed class FestivalPointer
    {
        public int Index;
        public bool Tracked, IsHand, Pressed, WasPressed;
        public bool Gripping, WasGripping;
        public bool NearInteraction;
        public Ray Ray;
        public Vector3 PinchPoint;
        public Quaternion Rotation = Quaternion.identity;
        public IFestivalTarget Focus;
        public IFestivalGrasp Grasp;
        public LineRenderer Line;
        public Renderer Reticle;
        public readonly Vector3[] Curve = new Vector3[25];
        Vector3 aimDirection;
        bool aimReady;

        public void ResetAim() => aimReady = false;

        public void SmoothAim(float dt)
        {
            Vector3 wanted = Ray.direction;
            if (!aimReady) { aimDirection = wanted; aimReady = true; }
            else
            {
                // Suppress small tracking tremors, but follow deliberate turns promptly.
                float rate = Mathf.Lerp(22, 90, Mathf.Clamp01(Vector3.Angle(aimDirection, wanted) / 18));
                aimDirection = Vector3.Slerp(aimDirection, wanted, 1 - Mathf.Exp(-rate * dt)).normalized;
            }
            Ray = new Ray(Ray.origin, aimDirection);
        }

        public FestivalHeld Held => Grasp as FestivalHeld;

        public bool PressedNow => Pressed && !WasPressed;
        public bool GrippedNow => Gripping && !WasGripping;
        public bool Holding => Pressed || Gripping;
        public bool ReleasedNow => !Holding && (WasPressed || WasGripping);
        /// <summary>Recognise a closing pinch immediately; a wider release threshold prevents chatter.</summary>
        public static bool IsPinching(float fingertipDistance, bool wasPressed) =>
            fingertipDistance >= 0 && fingertipDistance <= (wasPressed ? .035f : .028f);

        /// <summary>
        /// A grip takes two of the three outer fingers closing, each measured from 1 (straight) to about 0.4
        /// (a fist). It lets go well before the hand is flat, because a hand at rest is never quite open.
        /// </summary>
        public static bool IsGripping(float middle, float ring, float little, bool wasGripping)
        {
            float closed = wasGripping ? 0.85f : 0.7f;
            return (middle < closed ? 1 : 0) + (ring < closed ? 1 : 0) + (little < closed ? 1 : 0) >= 2;
        }

        public Vector3 Velocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }

        /// <summary>When this hand last closed, for catching something that is already on its way.</summary>
        public float ClosedAt = float.NegativeInfinity;

        /// <summary>The throw to use on letting go, even if the hand vanished partway through it.</summary>
        public Vector3 ReleaseVelocity => ThrowRemembered ? lostVelocity : FastestRecentVelocity();
        public Vector3 ReleaseSpin => ThrowRemembered ? lostSpin : AngularVelocity;

        /// <summary>Whether the hold point sits on the palm rather than between the fingertips.</summary>
        public bool OnPalm;

        const float ThrowMemorySeconds = 0.3f;
        const float HistorySeconds = 0.14f;
        const float SpinSeconds = 0.1f;
        const float LineSeconds = 0.05f;
        const float ReleaseLookbackSeconds = 0.08f;
        const float SmallestTimeSpread = 1e-6f;
        readonly Vector3[] positions = new Vector3[48];
        readonly Quaternion[] rotations = new Quaternion[48];
        readonly float[] times = new float[48];
        Vector3 lostVelocity, lostSpin;
        float lostAt = float.NegativeInfinity;
        int samples, nextSample;

        bool ThrowRemembered => samples < 2 && Time.unscaledTime - lostAt < ThrowMemorySeconds;

        public void ResetMotion() { samples = nextSample = 0; Velocity = AngularVelocity = Vector3.zero; }

        /// <summary>A fast throw often outruns hand tracking, so the last measured motion is kept for a moment.</summary>
        public void LoseMotion(float now)
        {
            if (samples > 0)
            {
                lostVelocity = FastestRecentVelocity();
                lostSpin = AngularVelocity;
                lostAt = now;
            }
            ResetMotion();
        }

        /// <summary>The hold point moved to another part of the hand, which is not the hand moving.</summary>
        public void ShiftMotion(Vector3 offset)
        {
            for (int i = 0; i < positions.Length; i++) positions[i] += offset;
        }

        public void SampleMotion(float now)
        {
            positions[nextSample] = PinchPoint;
            rotations[nextSample] = Rotation;
            times[nextSample] = now;
            nextSample = (nextSample + 1) % positions.Length;
            samples = Mathf.Min(samples + 1, positions.Length);
            while (samples > 2 && now - times[Slot(samples - 1)] > HistorySeconds) samples--;
            if (samples < 2) return;
            if (TryFitLine(0, out Vector3 velocity)) Velocity = velocity;

            int oldest = 1;
            while (oldest + 1 < samples && now - times[Slot(oldest + 1)] <= SpinSeconds) oldest++;
            float dt = now - times[Slot(oldest)];
            if (dt < 0.001f) return;
            Quaternion delta = Rotation * Quaternion.Inverse(rotations[Slot(oldest)]);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180) angle -= 360;
            AngularVelocity = Mathf.Abs(angle) > 0.001f ? axis.normalized * (angle * Mathf.Deg2Rad / dt) : Vector3.zero;
        }

        /// <summary>Where the sample taken <paramref name="back"/> samples ago is stored.</summary>
        int Slot(int back) => (nextSample - 1 - back + 2 * positions.Length) % positions.Length;

        /// <summary>
        /// The fastest stretch of the last moments, rather than their average. A throw is still gaining
        /// speed until just before the fingers open, and a hard throw is the shortest, so an average
        /// under-reads exactly the throws that matter most. Each stretch is a straight line through the
        /// hand's path, which makes the result scale with the hand: twice as fast reads as twice as fast.
        /// </summary>
        Vector3 FastestRecentVelocity()
        {
            Vector3 fastest = Velocity;
            if (samples < 2) return fastest;
            float newest = times[Slot(0)];
            for (int back = 0; back < samples - 1 && newest - times[Slot(back)] <= ReleaseLookbackSeconds; back++)
                if (TryFitLine(back, out Vector3 velocity) && velocity.sqrMagnitude > fastest.sqrMagnitude) fastest = velocity;
            return fastest;
        }

        /// <summary>Least-squares speed along the hand's path, over the short stretch ending <paramref name="back"/> samples ago.</summary>
        bool TryFitLine(int back, out Vector3 velocity)
        {
            int end = Slot(back);
            Vector3 sumPosition = Vector3.zero, sumProduct = Vector3.zero;
            float sumTime = 0, sumSquares = 0;
            int count = 0;
            for (int k = back; k < samples; k++)
            {
                int slot = Slot(k);
                float time = times[slot] - times[end];
                if (count >= 2 && -time > LineSeconds) break;
                Vector3 position = positions[slot] - positions[end];
                sumTime += time;
                sumSquares += time * time;
                sumPosition += position;
                sumProduct += position * time;
                count++;
            }

            float spread = count * sumSquares - sumTime * sumTime;
            velocity = spread > SmallestTimeSpread ? (count * sumProduct - sumTime * sumPosition) / spread : Vector3.zero;
            return spread > SmallestTimeSpread;
        }
    }

    /// <summary>
    /// The visitor. Reads hands and controllers on a headset, or the mouse without one, and follows
    /// the headset as it comes and goes, so Play works with or without Quest Link connected.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class FestivalRig : MonoBehaviour
    {
        const int TargetLayerMask = 1 << 8;
        const float RayLength = 9;
        const float NearReach = 0.035f;
        /// <summary>How close a pinch has to be to a fuse for it to mean the fuse rather than the cracker.</summary>
        const float FuseReach = 0.06f;
        const float GrabHaloShowsAt = 2.4f;
        const float ReconnectSeconds = 8;

        static readonly InputFeatureUsage<Vector3> PointerPosition = new InputFeatureUsage<Vector3>("PointerPosition");
        static readonly InputFeatureUsage<Quaternion> PointerRotation = new InputFeatureUsage<Quaternion>("PointerRotation");

        public Camera view;
        public OVRCameraRig metaRig;
        public Transform LeftHold, RightHold;
        public Material rayMaterial;
        public Material reticleMaterial;
        public Material edgeHaloMaterial;
        public CanvasGroup fade;
        public FestivalSplash splash;
        [Tooltip("Camera height when there is no headset.")]
        public float desktopEyeHeight = 1.5f;

        public bool IsVR { get; private set; }
        public FestivalPointer Right => Pointers[1];
        public readonly FestivalPointer[] Pointers = { new FestivalPointer { Index = 0 }, new FestivalPointer { Index = 1 } };

        readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
        readonly RaycastHit[] rayHits = new RaycastHit[8];
        readonly Collider[] nearHits = new Collider[8];
        XRHandSubsystem hands;
        TrackedPoseDriver poseDriver;
        OVRManager metaManager;
        float yaw, pitch = 22;
        bool hadHeadset;
        float nextReconnect;

        DiwaliSession Session => DiwaliSession.Instance;
        bool SplashVisible => splash && splash.gameObject.activeInHierarchy;

        void Awake()
        {
            poseDriver = view.GetComponent<TrackedPoseDriver>();
            metaManager = metaRig.GetComponent<OVRManager>();

            // This is a mixed reality app, so passthrough is never left to a saved setting.
            if (metaManager) metaManager.isInsightPassthroughEnabled = true;

            foreach (var pointer in Pointers) BuildPointerVisuals(pointer);
        }

        void BuildPointerVisuals(FestivalPointer pointer)
        {
            var go = new GameObject("Pointer ray " + pointer.Index);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = rayMaterial;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = 0.0035f;
            line.endWidth = 0.0012f;
            line.numCapVertices = 2;
            line.enabled = false;
            pointer.Line = line;

            var reticle = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(reticle.GetComponent<Collider>());
            reticle.name = "Pointer reticle " + pointer.Index;
            reticle.transform.SetParent(transform, false);
            reticle.transform.localScale = Vector3.one * 0.02f;
            pointer.Reticle = reticle.GetComponent<Renderer>();
            pointer.Reticle.sharedMaterial = reticleMaterial;
            pointer.Reticle.enabled = false;
        }

#if UNITY_EDITOR
        // OpenXR ends Play mode when the runtime drops the session, for example when a headset
        // sleeps. Stay in Play on the desktop preview instead and reconnect when it wakes.
        void OnEnable() => OpenXRRuntime.wantsToQuit += StayInPlayMode;
        void OnDisable() => OpenXRRuntime.wantsToQuit -= StayInPlayMode;

        static bool StayInPlayMode()
        {
            Debug.Log("[Diwali] Link session ended. Staying in Play on the desktop preview until the headset returns.");
            return false;
        }

        void WatchForLink()
        {
            var settings = XRGeneralSettings.Instance;
            if (IsVR || !hadHeadset || !settings || !settings.InitManagerOnStart || settings.Manager.activeLoader) return;
            if (Time.unscaledTime < nextReconnect) return;
            nextReconnect = Time.unscaledTime + ReconnectSeconds;
            StartCoroutine(Reconnect(settings.Manager));
        }

        static IEnumerator Reconnect(XRManagerSettings manager)
        {
            yield return manager.InitializeLoader();
            if (manager.activeLoader) manager.StartSubsystems();
        }
#endif

        void Start()
        {
            ApplyHeadset(XRSettings.isDeviceActive);
            if (fade) fade.alpha = 0;
        }

        void ApplyHeadset(bool active)
        {
            foreach (var pointer in Pointers) pointer.ResetMotion();
            IsVR = active;
            hadHeadset |= active;
            metaRig.enabled = active;

            // OVRManager only advances passthrough start-up in its own Update, so it keeps running
            // whenever XR is up, even while the headset is off the head.
            if (metaManager) metaManager.enabled = active || OVRPlugin.initialized;
            if (poseDriver) poseDriver.enabled = active;

            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = active ? Color.clear : new Color(0.05f, 0.05f, 0.07f);
            if (!active)
            {
                view.transform.localPosition = new Vector3(0, desktopEyeHeight, 0);
                view.transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
            }

            if (Session) Session.HeadsetChanged(active);
            else if (splash)
            {
                if (splash.music) splash.music.SetHeadset(active);
                foreach (var layer in GetComponentsInChildren<OVRPassthroughLayer>(true)) layer.enabled = active;
            }
            Debug.Log(active ? "[Diwali] Headset active: mixed reality." : "[Diwali] No headset: desktop preview.");
            if (active) StartCoroutine(CheckPassthrough());
            if (active) TuneForQuest();
        }

        /// <summary>
        /// A blast arrives with no warning, so the clocks must already be up, and the edges of the view may be
        /// shaded coarsely while the GPU is busy. Passthrough is composited separately, so the room stays sharp.
        /// </summary>
        static void TuneForQuest()
        {
            if (Application.platform != RuntimePlatform.Android) return;
            OVRManager.suggestedCpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            OVRManager.suggestedGpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.High;
            OVRManager.useDynamicFoveatedRendering = true;
        }

        /// <summary>A missing room is the one failure nobody can work around, so say so rather than show black.</summary>
        IEnumerator CheckPassthrough()
        {
            for (float waited = 0; waited < 6; waited += 0.25f)
            {
                if (OVRManager.IsInsightPassthroughInitialized())
                {
                    Debug.Log("[Diwali] Passthrough is running.");
                    yield break;
                }
                if (OVRManager.HasInsightPassthroughInitFailed()) break;
                yield return new WaitForSeconds(0.25f);
            }

            Debug.LogError("[Diwali] Passthrough did not start. Over Link, check Passthrough over Meta Quest Link in the Link app.");
            if (Session) Session.Say("Your room could not be shown. Check passthrough is allowed.", 20);
            else if (splash) splash.instruction.text = "Your room could not be shown. Check passthrough is allowed.";
        }

        void Update()
        {
#if UNITY_EDITOR
            WatchForLink();
#endif
            bool active = XRSettings.isDeviceActive;
            if (active != IsVR) ApplyHeadset(active);

            foreach (var pointer in Pointers)
            {
                pointer.WasPressed = pointer.Pressed;
                pointer.WasGripping = pointer.Gripping;
                pointer.Pressed = false;
                pointer.Gripping = false;
            }

            if (IsVR)
            {
                FindHands();
                ReadHeadsetPointer(Pointers[0], XRNode.LeftHand, MetaAimHand.left, LeftHold);
                ReadHeadsetPointer(Pointers[1], XRNode.RightHand, MetaAimHand.right, RightHold);
            }
            else
            {
                Pointers[0].Tracked = false;
                ReadMouse(Pointers[1]);
            }

            float now = Time.unscaledTime;
            foreach (var pointer in Pointers)
            {
                if (IsVR && pointer.Tracked) pointer.SmoothAim(Time.unscaledDeltaTime);
                else pointer.ResetAim();
                if (pointer.Tracked) pointer.SampleMotion(now);
                else pointer.LoseMotion(now);
                if (pointer.PressedNow || pointer.GrippedNow) pointer.ClosedAt = now;
                Interact(pointer);
            }
        }

        void FindHands()
        {
            if (hands != null && hands.running) return;
            SubsystemManager.GetSubsystems(handSubsystems);
            hands = handSubsystems.Find(subsystem => subsystem.running);
        }

        void ReadHeadsetPointer(FestivalPointer pointer, XRNode node, MetaAimHand aimHand, Transform socket)
        {
            Transform space = metaRig.trackingSpace;
            pointer.Tracked = false;

            if (aimHand != null && aimHand.isTracked.isPressed && (aimHand.aimFlags.ReadValue() & (int)MetaAimFlags.Valid) != 0)
            {
                Vector3 position = space.TransformPoint(aimHand.devicePosition.ReadValue());
                Quaternion rotation = space.rotation * aimHand.deviceRotation.ReadValue();
                pointer.Tracked = true;
                pointer.IsHand = true;
                pointer.Pressed = aimHand.indexPressed.isPressed;
                pointer.Ray = new Ray(position, rotation * Vector3.forward);
                pointer.Rotation = rotation;
                pointer.PinchPoint = PinchPoint(pointer, node, space, position + rotation * Vector3.forward * 0.08f);
            }
            else if (ReadClosedHand(pointer, node, space)) { }
            else
            {
                var device = InputDevices.GetDeviceAtXRNode(node);
                if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked)
                {
                    device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 local);
                    device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion localRotation);
                    device.TryGetFeatureValue(CommonUsages.triggerButton, out pointer.Pressed);
                    device.TryGetFeatureValue(CommonUsages.gripButton, out pointer.Gripping);
                    Vector3 position = space.TransformPoint(local);
                    Quaternion rotation = space.rotation * localRotation;
                    Vector3 aimFrom = position;
                    Quaternion aim = rotation;
                    if (device.TryGetFeatureValue(PointerPosition, out Vector3 pointerLocal)) aimFrom = space.TransformPoint(pointerLocal);
                    if (device.TryGetFeatureValue(PointerRotation, out Quaternion pointerAim)) aim = space.rotation * pointerAim;

                    pointer.Tracked = true;
                    pointer.IsHand = false;
                    pointer.Ray = new Ray(aimFrom, aim * Vector3.forward);
                    pointer.Rotation = aim;
                    pointer.PinchPoint = aimFrom + aim * new Vector3(0, 0, 0.06f);
                }
            }

            if (pointer.IsHand && pointer.Tracked) ReadHandGrip(pointer, node, space);
            if (pointer.Tracked) socket.SetPositionAndRotation(pointer.PinchPoint, pointer.Rotation);
        }

        bool ReadClosedHand(FestivalPointer pointer, XRNode node, Transform space)
        {
            if (hands == null) return false;
            XRHand hand = node == XRNode.LeftHand ? hands.leftHand : hands.rightHand;
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm)) return false;
            // A fist can invalidate Meta's pointing pose. Joint tracking still supplies a grab pose.
            pointer.Tracked = pointer.IsHand = true;
            pointer.Rotation = space.rotation * palm.rotation;
            pointer.PinchPoint = PinchPoint(pointer, node, space, space.TransformPoint(palm.position));
            pointer.Ray = new Ray(pointer.PinchPoint, pointer.Rotation * Vector3.forward);
            return true;
        }

        void ReadHandGrip(FestivalPointer pointer, XRNode node, Transform space)
        {
            if (hands == null) return;
            XRHand hand = node == XRNode.LeftHand ? hands.leftHand : hands.rightHand;
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm)) return;
            pointer.Gripping = FestivalPointer.IsGripping(Straightness(hand, XRHandJointID.MiddleProximal),
                Straightness(hand, XRHandJointID.RingProximal), Straightness(hand, XRHandJointID.LittleProximal), pointer.WasGripping);
            // Object orientation follows the tracked hand, independently of the pointing ray.
            pointer.Rotation = space.rotation * palm.rotation;
            // Keep one stable grip point through opening the fingers, avoiding a false throw impulse.
            bool onPalm = pointer.Gripping || pointer.WasGripping;
            Vector3 palmPoint = space.TransformPoint(palm.position);
            if (onPalm != pointer.OnPalm)
            {
                pointer.ShiftMotion(onPalm ? palmPoint - pointer.PinchPoint : pointer.PinchPoint - palmPoint);
                pointer.OnPalm = onPalm;
            }
            if (onPalm)
            {
                pointer.PinchPoint = palmPoint;
            }
        }

        /// <summary>
        /// Knuckle to fingertip, over the finger's length. Being a ratio, it reads the same on any size of hand,
        /// where a fixed distance to the palm counted the small fingers of a relaxed hand as still closed.
        /// </summary>
        static float Straightness(XRHand hand, XRHandJointID knuckle)
        {
            Vector3 first = default, last = default;
            float length = 0;
            for (int joint = 0; joint < 4; joint++)
            {
                if (!hand.GetJoint(knuckle + joint).TryGetPose(out Pose pose)) return 1;
                if (joint == 0) first = pose.position;
                else length += Vector3.Distance(last, pose.position);
                last = pose.position;
            }
            return length > 0.001f ? Vector3.Distance(first, last) / length : 1;
        }

        /// <summary>The point between thumb and index finger: where a held thing actually sits.</summary>
        Vector3 PinchPoint(FestivalPointer pointer, XRNode node, Transform space, Vector3 fallback)
        {
            if (hands == null) return fallback;
            XRHand hand = node == XRNode.LeftHand ? hands.leftHand : hands.rightHand;
            if (!hand.isTracked) return fallback;
            if (!hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose index)) return fallback;
            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumb)) return fallback;
            // Use the same joint sample for selection and positioning, without waiting for the
            // provider's completed-pinch flag. That flag remains the fallback when joints are unavailable.
            pointer.Pressed = FestivalPointer.IsPinching(Vector3.Distance(index.position, thumb.position), pointer.WasPressed);
            return space.TransformPoint((index.position + thumb.position) * 0.5f);
        }

        void ReadMouse(FestivalPointer pointer)
        {
            var mouse = Mouse.current;
            var keys = Keyboard.current;
            if (mouse == null) return;

            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * 0.12f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.12f, -80, 80);
                view.transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
            }
            if (keys != null && keys.escapeKey.wasPressedThisFrame)
            {
                yaw = 0;
                pitch = 22;
                view.transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
            }

            Ray ray = view.ScreenPointToRay(mouse.position.ReadValue());
            pointer.Tracked = true;
            pointer.IsHand = false;
            pointer.Pressed = mouse.leftButton.isPressed;
            pointer.Gripping = keys != null && keys.gKey.isPressed;
            pointer.Ray = ray;
            pointer.Rotation = Quaternion.LookRotation(ray.direction);
            pointer.PinchPoint = ray.GetPoint(0.5f);

            // A mouse has no reach, so a held tool jumps to whatever it is pointed at.
            if (pointer.Held != null && Physics.Raycast(ray, out RaycastHit hit, RayLength, TargetLayerMask, QueryTriggerInteraction.Collide))
            {
                var item = hit.collider.GetComponentInParent<FestivalItem>();
                pointer.PinchPoint = item && item.fuse ? item.fuse.position : hit.point - ray.direction * 0.02f;
            }
        }

        void Interact(FestivalPointer pointer)
        {
            var removal = GetComponent<FestivalRemoval>();
            if (!removal) removal = gameObject.AddComponent<FestivalRemoval>();
            if (!pointer.Tracked)
            {
                removal.Cancel(pointer);
                SetFocus(pointer, null);
                HideRay(pointer);
                pointer.Grasp?.Lost(pointer);
                return;
            }

            if (pointer.Grasp != null)
            {
                removal.Cancel(pointer);
                SetFocus(pointer, null);
                HideRay(pointer);
                pointer.Grasp.Steer(pointer);
                return;
            }

            if (IsVR && Session && !SplashVisible && Session.placement.TryCatch(pointer)) return;

            var target = FindTarget(pointer, out Vector3 point, out bool hit);
            SetFocus(pointer, target);
            ShowGrabHalo(pointer);
            if (pointer.IsHand && Vector3.Distance(pointer.PinchPoint, point) < 0.08f) HideRay(pointer);
            else ShowRay(pointer, point, hit && !(target is FestivalItem) && !(target is FestivalFuseTarget), target != null);

            if (removal.HandleTapAway(pointer, target))
            {
                removal.Cancel(pointer);
                return;
            }

            // A ray pinch on a placed cracker or a dropped tool takes it on the down edge. The removal
            // gesture applies to fuse targets and fixed objects, where it cannot block a grab.
            bool startsGrab = pointer.PressedNow && target is IFestivalGrabbable grabbable && grabbable.CanGrab(pointer);
            if (!startsGrab && removal.Handle(pointer, target, Time.unscaledTime)) return;

            if ((pointer.PressedNow || pointer.GrippedNow) && target != null &&
                (!pointer.GrippedNow || target is IFestivalGrabbable || target is ThaliTile || target is ThaliHandle)) target.Press(pointer);
        }

        IFestivalTarget FindTarget(FestivalPointer pointer, out Vector3 point, out bool hit)
        {
            pointer.NearInteraction = false;
            // A hand already inside a grab region wins over wherever its ray happens to point.
            if (IsVR && Session && !SplashVisible)
            {
                var zone = GrabZone.Nearest(pointer, 1, out _);
                if (zone)
                {
                    pointer.NearInteraction = true;
                    point = pointer.PinchPoint;
                    hit = true;
                    return FuseAt(zone.Owner, pointer) ?? zone.Owner;
                }
            }

            if (pointer.IsHand)
            {
                int count = Physics.OverlapSphereNonAlloc(pointer.PinchPoint, NearReach, nearHits, TargetLayerMask, QueryTriggerInteraction.Collide);
                for (int i = 0; i < count; i++)
                {
                    var near = nearHits[i].GetComponentInParent<IFestivalTarget>();
                    if (near == null) continue;
                    if (SplashVisible && !(near is FestivalSplashButton)) continue;
                    pointer.NearInteraction = true;
                    point = pointer.PinchPoint;
                    hit = true;
                    return near;
                }
            }

            int hits = Physics.RaycastNonAlloc(pointer.Ray, rayHits, RayLength, TargetLayerMask, QueryTriggerInteraction.Collide);
            float nearest = float.MaxValue;
            IFestivalTarget found = null;
            point = pointer.Ray.GetPoint(1.6f);
            hit = false;
            for (int i = 0; i < hits; i++)
            {
                if (rayHits[i].distance >= nearest) continue;
                var candidate = rayHits[i].collider.GetComponentInParent<IFestivalTarget>();
                if (candidate == null) continue;
                if (SplashVisible && !(candidate is FestivalSplashButton)) continue;
                nearest = rayHits[i].distance;
                found = candidate;
                point = rayHits[i].point;
                hit = true;
            }

            // A fuse wins over the cracker it belongs to, even from inside its box. The rocket's stand
            // and the diya's bowl both surround theirs, which left nothing but the pick-up to reach.
            if (found is FestivalItem cracker && Lightable(cracker))
            {
                for (int i = 0; i < hits; i++)
                {
                    if (rayHits[i].collider.transform != cracker.fuse) continue;
                    point = rayHits[i].point;
                    return rayHits[i].collider.GetComponent<FestivalFuseTarget>();
                }
            }
            return found;
        }

        static bool Lightable(FestivalItem item) => item.NeedsFlame && item.State == ItemState.Ready && item.fuse;

        /// <summary>A pinch at a fuse lights it. A closed hand still picks the cracker up.</summary>
        static IFestivalTarget FuseAt(IFestivalGrabbable owner, FestivalPointer pointer)
        {
            if (pointer.Gripping || !(owner is FestivalItem item) || !Lightable(item)) return null;
            if (Vector3.Distance(pointer.PinchPoint, item.fuse.position) > FuseReach) return null;
            return item.fuse.GetComponent<FestivalFuseTarget>();
        }

        /// <summary>
        /// Lights the edges of whatever a pinch would take. The halo fades in as a hand approaches and
        /// strengthens once the hand is inside the grab region, so nobody has to guess where pinching will work.
        /// </summary>
        void ShowGrabHalo(FestivalPointer pointer)
        {
            // At a fuse the pinch lights it, so nothing here should say it will pick the cracker up.
            if (pointer.Focus is FestivalFuseTarget) return;
            GrabZone zone = null;
            float depth = float.MaxValue;
            if (IsVR) zone = GrabZone.Nearest(pointer, GrabHaloShowsAt, out depth);
            if (!zone && pointer.Focus is FestivalItem item && item.CanGrab(pointer))
            {
                zone = item.GetComponent<GrabZone>();
                depth = 1;
            }

            if (!zone) return;
            float approach = Mathf.InverseLerp(GrabHaloShowsAt, 1, depth);
            zone.Halo.Show(depth <= 1 ? 1 : Mathf.Lerp(0.12f, 0.5f, approach));
        }

        void SetFocus(FestivalPointer pointer, IFestivalTarget target)
        {
            if (ReferenceEquals(pointer.Focus, target)) return;
            if (pointer.Focus is Object oldObject && oldObject) pointer.Focus.Hover(false);
            pointer.Focus = target;
            target?.Hover(true);
        }

        public void ShowRay(FestivalPointer pointer, Vector3 end, bool hit, bool interacting = false)
        {
            pointer.Line.enabled = IsVR;
            pointer.Line.positionCount = 2;
            pointer.Line.SetPosition(0, pointer.Ray.origin);
            pointer.Line.SetPosition(1, hit || interacting ? end : pointer.Ray.origin + pointer.Ray.direction * .15f);

            pointer.Reticle.enabled = hit;
            if (!hit) return;
            var reticle = pointer.Reticle.transform;
            reticle.position = end;
            reticle.localScale = Vector3.one * (pointer.Pressed ? 0.016f : 0.024f);
        }

        /// <summary>A continuous curve from the hand to the actual landing preview.</summary>
        public void ShowPlacementRay(FestivalPointer pointer, Vector3 end)
        {
            Vector3 start = pointer.Ray.origin;
            float span = Vector3.Distance(start, end);
            Vector3 outgoing = start + pointer.Ray.direction * Mathf.Min(span * .45f, .65f);
            Vector3 incoming = end + Vector3.up * Mathf.Min(span * .3f, .5f);
            for (int i = 0; i < pointer.Curve.Length; i++)
            {
                float t = i / (float)(pointer.Curve.Length - 1), u = 1 - t;
                pointer.Curve[i] = u*u*u*start + 3*u*u*t*outgoing + 3*u*t*t*incoming + t*t*t*end;
            }
            pointer.Line.positionCount = pointer.Curve.Length;
            pointer.Line.SetPositions(pointer.Curve);
            pointer.Line.enabled = true;
            pointer.Reticle.enabled = false;
        }

        public static void HideRay(FestivalPointer pointer)
        {
            if (pointer.Line) pointer.Line.enabled = false;
            if (pointer.Reticle) pointer.Reticle.enabled = false;
        }

        /// <summary>Haptic pulse on a controller. Hand tracking simply has no equivalent, and that is fine.</summary>
        public void Rumble(float amplitude, float seconds, FestivalPointer pointer = null)
        {
            if (!IsVR) return;
            StartCoroutine(Pulse(amplitude * (Session && Session.Gentle ? 0.6f : 1), seconds, pointer));
        }

        IEnumerator Pulse(float amplitude, float seconds, FestivalPointer pointer)
        {
            var controllers = pointer == null ? OVRInput.Controller.Touch : pointer.Index == 0 ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            OVRInput.SetControllerVibration(0.4f, amplitude, controllers);
            yield return new WaitForSeconds(seconds);
            OVRInput.SetControllerVibration(0, 0, controllers);
        }
    }
}
