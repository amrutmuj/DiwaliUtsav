using System;
using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Diwali
{
    [Serializable]
    public sealed class AnchoredItem
    {
        public CrackerId id;
        public string uuid;
    }

    [Serializable]
    public sealed class AnchorManifest
    {
        public List<AnchoredItem> items = new List<AnchoredItem>();
    }

    /// <summary>
    /// Ray placement previews the actual object on the floor; nearby hands can carry and release it. Pops can be thrown. There is no separate
    /// scan, select, place or save step. MRUK is asked once for the floor height and then released,
    /// so nothing in the room is meshed or occluded. Diyas remember where you left them.
    /// </summary>
    public sealed class FestivalPlacement : MonoBehaviour
    {
        const string AnchorKey = "DiwaliMR.Anchors.v3";
        const int MaximumItems = 22;
        const float MiddleReach = 1.5f;
        public const float InitialPlacementReach = 2f;
        public const float MaximumPlacementReach = 5f;
        const float ItemGap = 0.05f;
        const float GrowSeconds = 0.10f;
        const float MiniatureSize = 0.3f;
        const float CatchSeconds = 0.25f;
        const float CatchReach = 1.6f;

        /// <summary>One thing being carried by one hand. Each hand has its own, so both can be full.</summary>
        sealed class Carry : IFestivalGrasp
        {
            public FestivalPlacement owner;
            public FestivalItem item;
            public FestivalPointer holder;
            public bool remote;
            public float grown, radius;
            public Vector3 landing, grip;
            public Vector3 landingVelocity;
            public Renderer ring;

            public void Steer(FestivalPointer pointer) => owner.SteerCarry(this, pointer);

            /// <summary>If tracking drops, a pop waits for the hand and anything else is set down rather than lost.</summary>
            public void Lost(FestivalPointer pointer)
            {
                if (remote || !item.Physics.throwable) owner.Finish(this);
            }
        }

        public MRUK roomKit;
        public FloorLayer floor;
        public Renderer dropRing;
        public Renderer safetyRing;
        [Tooltip("The desktop stand-in. Hidden whenever a headset is active.")]
        public GameObject previewRoom;

        public readonly List<FestivalItem> Items = new List<FestivalItem>();
        public float FloorHeight => floor.Height;
        public Vector3 LastLanding { get; private set; }

        readonly Dictionary<FestivalItem, OVRSpatialAnchor> anchors = new Dictionary<FestivalItem, OVRSpatialAnchor>();
        readonly List<Carry> carries = new List<Carry>();
        readonly List<ParticleSystem> litter = new List<ParticleSystem>();
        AnchorManifest manifest = new AnchorManifest();
        bool roomRequested, roomRead;

        DiwaliSession Session => DiwaliSession.Instance;

        void Awake()
        {
            if (dropRing) dropRing.enabled = false;
            if (safetyRing) safetyRing.enabled = false;
        }

        public void HeadsetChanged(bool present)
        {
            if (previewRoom) previewRoom.SetActive(!present);
            if (roomKit) roomKit.enabled = present && !roomRead;

            // The tracking origin sits on the real floor, so placement works from the first frame.
            // The room scan, when there is one, only refines this height.
            if (!roomRead) floor.Use(present ? Session.rig.metaRig.trackingSpace.position.y : 0, present ? "headset tracking" : "desktop preview");
        }

        /// <summary>Asks for the floor once. If it cannot be read, the height from tracking stands.</summary>
        public void ReadFloor()
        {
            if (roomRequested) return;
            roomRequested = true;
            if (!Session.rig.IsVR)
            {
                FloorReady();
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => LoadFloor();
                callbacks.PermissionDenied += _ => FloorReady();
                Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission, callbacks);
                return;
            }
#endif
            LoadFloor();
        }

        async void LoadFloor()
        {
            FestivalPerf.Note("reading floor");
            try
            {
                await roomKit.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound: false);
                var room = roomKit.GetCurrentRoom();
                if (room) UseRoom(room);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Diwali] No room data, using the tracked floor: " + exception.Message);
            }

            // Only the floor was wanted. Letting go of the scene means no room geometry stays in memory.
            roomKit.ClearScene();
            roomKit.enabled = false;
            roomRead = true;
            FestivalPerf.Note("floor read");
            FloorReady();
            RestoreAnchors();
        }

        void FloorReady() => floor.Reveal(Session.rig.view.transform.position);

        void UseRoom(MRUKRoom room)
        {
            if (room.FloorAnchors.Count > 0) floor.Use(room.FloorAnchors[0].transform.position.y, "room scan");

            Bounds bounds = room.GetRoomBounds();
            Session.sound.SetRoomSize(bounds.size, bounds.center, true);

            // Distant celebration should arrive from outside: through a window if the room has one.
            foreach (var anchor in room.Anchors)
            {
                if (!anchor.HasAnyLabel(MRUKAnchor.SceneLabels.WINDOW_FRAME)) continue;
                Session.sound.StartNightBed(anchor.transform.position);
                return;
            }
            Session.sound.StartNightBed(bounds.center + Vector3.forward * bounds.extents.z);
        }

        public bool HasRoomForMore => Items.Count < MaximumItems;

        /// <summary>Ray selection starts on the floor; a nearby hand can take the miniature physically.</summary>
        public void TakeNew(CrackerEntry entry, FestivalPointer pointer)
        {
            if (pointer == null || pointer.Grasp != null || !HasRoomForMore) return;
            bool remote = !pointer.NearInteraction && !CrackerPhysics.For(entry.id).throwable;
            Vector3 position = remote ? RayLandingPoint(pointer.Ray, null, entry.footprintRadius) : pointer.PinchPoint;
            var item = Instantiate(entry.prefab, position, FacingVisitor(position)).GetComponent<FestivalItem>();
            Begin(item, pointer, true, remote);
        }

        /// <summary>Takes a placed item, or takes one the other hand is carrying.</summary>
        public void Grab(FestivalItem item, FestivalPointer pointer) => Grab(item, pointer, false);

        void Grab(FestivalItem item, FestivalPointer pointer, bool catching)
        {
            if (!item || !item.CanGrab(pointer)) return;

            var zone = item.GetComponent<GrabZone>();
            bool near = catching || (zone && Vector3.Distance(pointer.PinchPoint, zone.WorldCentre) <= zone.radius);
            var carried = carries.Find(candidate => candidate.item == item);
            if (carried == null)
            {
                Begin(item, pointer, false, !near && !item.Physics.throwable);
                return;
            }

            carried.holder.Grasp = null;
            carried.holder = pointer;
            carried.remote = !near && !item.Physics.throwable;
            item.Carrier = pointer;
            pointer.Grasp = carried;
            if (item.Physics.throwable) FestivalRig.HideRay(pointer);
            Session.sound.Play("select", pointer.PinchPoint, 1, 0);
            Session.rig.Rumble(0.2f, 0.04f, pointer);
        }

        void Begin(FestivalItem item, FestivalPointer pointer, bool fresh, bool remote)
        {
            bool throwable = item.Physics.throwable;
            var carry = new Carry
            {
                owner = this, item = item, holder = pointer, remote = remote,
                grown = fresh && !remote ? 0 : 1,
                grip = item.GetComponent<GrabZone>() is GrabZone zone ? zone.centre : Vector3.zero,
                radius = Session.catalog.Find(item.id).footprintRadius,
            };
            item.transform.SetParent(null, true);

            // Only a miniature taken physically grows from the hand. Ray previews start at full size on the floor.
            if (fresh) item.transform.localScale = Vector3.one * (remote ? 1 : MiniatureSize);
            if (throwable)
            {
                item.transform.SetPositionAndRotation(pointer.PinchPoint - pointer.Rotation * (carry.grip * item.transform.localScale.x), pointer.Rotation);
                FestivalRig.HideRay(pointer);
            }
            if (item.GetComponent<FestivalToss>() is FestivalToss toss) toss.Catch();
            if (fresh) Items.Add(item);
            carries.Add(carry);
            if (!throwable && dropRing)
            {
                carry.ring = carries.Exists(other => other != carry && other.ring == dropRing)
                    ? Instantiate(dropRing, transform) : dropRing;
            }
            pointer.Grasp = carry;
            item.Carrier = pointer;
            item.Lift();
            SetCollidable(item, false);
            carry.landing = remote ? item.transform.position : new Vector3(pointer.PinchPoint.x, floor.Height, pointer.PinchPoint.z);
            if (!throwable)
            {
                Vector3 visitor = Session.rig.view.transform.position;
                Vector3 offset = Vector3.ClampMagnitude(Flat(carry.landing - visitor), PlacementReach(item));
                carry.landing = new Vector3(visitor.x + offset.x, floor.Height, visitor.z + offset.z);
            }
            LastLanding = carry.landing;
            ShowRing(carry);
            Session.sound.Play("select", pointer.PinchPoint, 1, 0);
            Session.rig.Rumble(0.15f, 0.03f, pointer);
        }

        public static float PlacementReach(FestivalItem item) =>
            item && item.HasBeenPlaced ? MaximumPlacementReach : InitialPlacementReach;

        void SteerCarry(Carry carry, FestivalPointer pointer)
        {
            var item = carry.item;
            if (!item) return;
            if (carry.remote) SteerRemote(carry, pointer);
            else if (item.Physics.throwable) SteerThrow(carry, pointer);
            else SteerPlacement(carry, pointer);
        }

        void SteerRemote(Carry carry, FestivalPointer pointer)
        {
            Vector3 target = RayLandingPoint(pointer.Ray, carry.item, carry.radius);
            carry.landing = Vector3.SmoothDamp(carry.landing, target, ref carry.landingVelocity, .055f, Mathf.Infinity, Time.unscaledDeltaTime);
            Vector3 visitor = Session.rig.view.transform.position;
            Vector3 offset = Vector3.ClampMagnitude(Flat(carry.landing - visitor), PlacementReach(carry.item));
            carry.landing = new Vector3(visitor.x + offset.x, floor.Height, visitor.z + offset.z);
            carry.landing.y = floor.Height;
            LastLanding = carry.landing;
            carry.item.transform.SetPositionAndRotation(carry.landing, FacingVisitor(carry.landing));
            carry.item.transform.localScale = Vector3.one;
            ShowRing(carry);
            Session.rig.ShowPlacementRay(pointer, carry.landing);
            if (!pointer.Holding) Finish(carry);
        }

        /// <summary>The object itself previews ray placement on the tracked floor, within its usable radius.</summary>
        public Vector3 RayLandingPoint(Ray aim, FestivalItem item, float radius)
        {
            Vector3 visitor = Session.rig.view.transform.position;
            Vector3 landing;
            float distance = aim.direction.y < -0.02f ? (floor.Height - aim.origin.y) / aim.direction.y : -1;
            if (distance > 0) landing = aim.GetPoint(distance);
            else
            {
                Vector3 forward = Flat(aim.direction);
                if (forward.sqrMagnitude < .01f) forward = Flat(Session.rig.view.transform.forward);
                if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
                landing = visitor + forward.normalized * MiddleReach;
            }
            float reach = Mathf.Min(PlacementReach(item), Mathf.Max(.35f, floor.radius - radius));
            Vector3 offset = Vector3.ClampMagnitude(Flat(landing - visitor), reach);
            landing = visitor + offset;
            landing.y = floor.Height;
            landing = AwayFromOthers(landing, item, radius, visitor);
            offset = Vector3.ClampMagnitude(Flat(landing - visitor), reach);
            return new Vector3(visitor.x + offset.x, floor.Height, visitor.z + offset.z);
        }

        void SteerPlacement(Carry carry, FestivalPointer pointer)
        {
            var item = carry.item;
            float dt = Time.deltaTime;
            carry.grown = Mathf.MoveTowards(carry.grown, 1, dt / GrowSeconds);
            float size = Mathf.Lerp(MiniatureSize, 1, Mathf.SmoothStep(0, 1, carry.grown));
            item.transform.localScale = Vector3.one * size;

            Quaternion upright = FacingVisitor(item.transform.position);
            Vector3 inHand = pointer.PinchPoint - upright * (carry.grip * size);
            float follow = 1 - Mathf.Exp(-dt * 24);
            item.transform.SetPositionAndRotation(Vector3.Lerp(item.transform.position, inHand, follow), Quaternion.Slerp(item.transform.rotation, upright, follow));

            Vector3 belowHand = pointer.PinchPoint;
            belowHand.y = floor.Height;
            carry.landing = AwayFromOthers(belowHand, item, carry.radius, Session.rig.view.transform.position);
            Vector3 visitor = Session.rig.view.transform.position;
            Vector3 offset = Vector3.ClampMagnitude(Flat(carry.landing - visitor), PlacementReach(item));
            carry.landing = new Vector3(visitor.x + offset.x, floor.Height, visitor.z + offset.z);
            LastLanding = carry.landing;
            ShowRing(carry);

            Session.rig.ShowPlacementRay(pointer, carry.landing);
            if (!pointer.Holding) Finish(carry);
        }

        void SteerThrow(Carry carry, FestivalPointer pointer)
        {
            var item = carry.item;
            carry.grown = Mathf.MoveTowards(carry.grown, 1, Time.deltaTime / GrowSeconds);
            float size = Mathf.Lerp(MiniatureSize, 1, Mathf.SmoothStep(0, 1, carry.grown));
            item.transform.localScale = Vector3.one * size;
            item.transform.SetPositionAndRotation(pointer.PinchPoint - pointer.Rotation * (carry.grip * size), pointer.Rotation);
            FestivalRig.HideRay(pointer);

            // Like the lighter: hold to carry, open to release. Velocity and spin drive the throw.
            if (!pointer.Holding) Finish(carry);
        }
        /// <summary>Slides a landing point sideways if something is already standing there.</summary>
        Vector3 AwayFromOthers(Vector3 landing, FestivalItem self, float radius, Vector3 visitor)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                bool clear = true;
                foreach (var other in Items)
                {
                    if (!other || other == self || other.id == CrackerId.Rangoli) continue;
                    float needed = radius + Session.catalog.Find(other.id).footprintRadius + ItemGap;
                    Vector3 apart = Flat(landing - other.transform.position);
                    if (apart.magnitude >= needed) continue;

                    clear = false;
                    Vector3 sideways = Vector3.Cross(Vector3.up, Flat(landing - visitor).normalized);
                    if (sideways.sqrMagnitude < .01f) sideways = Vector3.right;
                    float side = Vector3.Dot(apart, sideways) >= 0 ? 1 : -1;
                    landing += sideways * (side * (needed - apart.magnitude + 0.02f));
                }
                if (clear) break;
            }
            return landing;
        }

        /// <summary>
        /// A thrown cracker crosses a hand in a few frames, far too briefly for a pinch to land inside its
        /// grab region. So a hand that has only just closed takes anything airborne within a wider reach.
        /// </summary>
        public bool TryCatch(FestivalPointer pointer)
        {
            if (pointer.Grasp != null || !pointer.Holding || Time.unscaledTime - pointer.ClosedAt > CatchSeconds) return false;
            var zone = GrabZone.Nearest(pointer, CatchReach, out _, true);
            if (!zone) return false;
            Grab((FestivalItem)zone.Owner, pointer, true);
            return true;
        }

        void Finish(Carry carry)
        {
            HideRing(carry);
            var item = carry.item;
            carry.holder.Grasp = null;
            carries.Remove(carry);
            item.Carrier = null;
            SetCollidable(item, true);
            if (carry.remote)
            {
                item.transform.SetPositionAndRotation(carry.landing, FacingVisitor(carry.landing));
                TossSettled(item);
                Session.sound.Play("place", carry.landing, .7f, .03f);
                return;
            }
            var toss = item.GetComponent<FestivalToss>();
            if (!toss) toss = item.gameObject.AddComponent<FestivalToss>();

            if (item.Physics.throwable)
            {
                toss.Launch(item, this, carry.holder.ReleaseVelocity, carry.holder.ReleaseSpin);
                return;
            }

            float seconds = Mathf.Clamp(Vector3.Distance(item.transform.position, carry.landing) * 0.22f, 0.16f, 0.42f) * item.Physics.hang;
            toss.SetDown(item, this, carry.landing, FacingVisitor(carry.landing), seconds);
        }

        public void TossSettled(FestivalItem item)
        {
            if (!item) return;
            item.Settle();
            LastLanding = item.transform.position;
            Session.ItemPlaced(item);
            if (item.Persists && Session.rig.IsVR) SaveAnchor(item);
        }

        Quaternion FacingVisitor(Vector3 position)
        {
            Vector3 toVisitor = Session.rig.view.transform.position - position;
            toVisitor.y = 0;
            return toVisitor.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toVisitor) : Quaternion.identity;
        }

        /// <summary>Places an item directly. Used by validation and by anchors restored from a previous session.</summary>
        public FestivalItem PlaceAt(CrackerId id, Vector3 position)
        {
            var item = Instantiate(Session.catalog.Find(id).prefab).GetComponent<FestivalItem>();
            item.transform.SetPositionAndRotation(position, FacingVisitor(position));
            Items.Add(item);
            item.Settle();
            return item;
        }

        /// <summary>Use a localized anchor once; its ongoing corrections must not move the visible decoration.</summary>
        public FestivalItem PlaceFromAnchor(CrackerId id, Transform localizedAnchor)
        {
            var item = PlaceAt(id, localizedAnchor.position);
            item.transform.rotation = localizedAnchor.rotation;
            return item;
        }

        public void ShowSafety(Vector3 position, float radius, float seconds)
        {
            // Safety effects do not interfere with the yellow placement preview.
            if (safetyRing) safetyRing.enabled = false;
        }

        static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0, value.z);

        void ShowRing(Carry carry)
        {
            if (!carry.ring) return;
            carry.ring.transform.position = carry.landing + Vector3.up * .008f;
            float diameter = Mathf.Max(.26f, carry.radius * 2 + .08f);
            carry.ring.transform.localScale = new Vector3(diameter, diameter, 1);
            carry.ring.enabled = true;
        }

        void HideRing(Carry carry)
        {
            if (!carry.ring) return;
            carry.ring.enabled = false;
            if (carry.ring != dropRing) Destroy(carry.ring.gameObject);
        }

        static void SetCollidable(FestivalItem item, bool on)
        {
            foreach (var part in item.GetComponentsInChildren<Collider>(true)) part.enabled = on;
        }

        public void Forget(FestivalItem item) => Items.Remove(item);

        public async void Remove(FestivalItem item)
        {
            if (!item || item.Carrier != null) return;
            Items.Remove(item);
            anchors.TryGetValue(item, out var anchor);
            anchors.Remove(item);
            if (anchor)
            {
                manifest.items.RemoveAll(record => record.uuid == anchor.Uuid.ToString());
                WriteManifest();
            }
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
            if (!anchor) return;
            try { await anchor.EraseAnchorAsync(); }
            catch (Exception exception) { Debug.LogWarning("[Diwali] Could not erase anchor: " + exception.Message); }
            if (anchor) Destroy(anchor.gameObject);
        }

        /// <summary>Paper left on the floor outlives the cracker that made it, until a reset sweeps it up.</summary>
        public void KeepLitter(ParticleSystem paper)
        {
            litter.RemoveAll(system => !system);
            litter.Add(paper);
        }

        public void ClearAll()
        {
            foreach (var carry in carries)
            {
                HideRing(carry);
                carry.holder.Grasp = null;
            }
            carries.Clear();
            if (dropRing) dropRing.enabled = false;
            if (safetyRing) safetyRing.enabled = false;
            foreach (var paper in litter) if (paper) Destroy(paper.gameObject);
            litter.Clear();
            foreach (var item in Items) if (item) Destroy(item.gameObject);
            Items.Clear();
            EraseAnchors();
        }

        async void SaveAnchor(FestivalItem item)
        {
            GameObject holder = null;
            bool saved = false;
            try
            {
                holder = new GameObject("Anchor " + item.id);
                holder.transform.SetPositionAndRotation(item.transform.position, item.transform.rotation);
                var anchor = holder.AddComponent<OVRSpatialAnchor>();
                if (!await anchor.WhenCreatedAsync() || !await anchor.WhenLocalizedAsync()) return;
                var result = await anchor.SaveAnchorAsync();
                if (!result.Success) return;
                if (!item || !Items.Contains(item))
                {
                    await anchor.EraseAnchorAsync();
                    return;
                }

                if (anchors.TryGetValue(item, out var previous) && previous)
                {
                    manifest.items.RemoveAll(record => record.uuid == previous.Uuid.ToString());
                    Destroy(previous.gameObject);
                }
                anchors[item] = anchor;
                // The anchor persists the placement for a future session. Keep this session's
                // visible object in world space, just like the other settled crackers.
                manifest.items.Add(new AnchoredItem { id = item.id, uuid = anchor.Uuid.ToString() });
                WriteManifest();
                saved = true;
            }
            catch (Exception exception)
            {
                // A missing anchor only means the diya does not come back next time. Nothing to tell the visitor.
                Debug.LogWarning("[Diwali] Could not save anchor: " + exception.Message);
            }
            finally
            {
                if (!saved && holder) Destroy(holder);
            }
        }

        async void RestoreAnchors()
        {
            try { manifest = JsonUtility.FromJson<AnchorManifest>(PlayerPrefs.GetString(AnchorKey, "")) ?? new AnchorManifest(); }
            catch (ArgumentException) { manifest = new AnchorManifest(); }
            manifest.items ??= new List<AnchoredItem>();

            foreach (var record in manifest.items.ToArray())
            {
                if (!Guid.TryParse(record.uuid, out var uuid) || uuid == Guid.Empty) continue;
                try
                {
                    var buffer = new List<OVRSpatialAnchor.UnboundAnchor>();
                    var result = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(new[] { uuid }, buffer);
                    if (!result.Success || buffer.Count == 0) continue;
                    var unbound = buffer[0];
                    if (!await unbound.LocalizeAsync(10)) continue;

                    var holder = new GameObject("Restored anchor " + record.id);
                    var anchor = holder.AddComponent<OVRSpatialAnchor>();
                    unbound.BindTo(anchor);
                    if (!await anchor.WhenLocalizedAsync())
                    {
                        Destroy(holder);
                        continue;
                    }

                    var item = PlaceFromAnchor(record.id, holder.transform);
                    anchors[item] = anchor;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Diwali] Could not restore anchor: " + exception.Message);
                }
            }
        }

        async void EraseAnchors()
        {
            var erasing = new List<OVRSpatialAnchor>(anchors.Values);
            anchors.Clear();
            manifest.items.Clear();
            WriteManifest();
            foreach (var anchor in erasing)
            {
                if (!anchor) continue;
                try { await anchor.EraseAnchorAsync(); }
                catch (Exception exception) { Debug.LogWarning("[Diwali] Could not erase anchor: " + exception.Message); }
                if (anchor) Destroy(anchor.gameObject);
            }
        }

        void WriteManifest()
        {
            PlayerPrefs.SetString(AnchorKey, JsonUtility.ToJson(manifest));
            PlayerPrefs.Save();
        }
    }
}
