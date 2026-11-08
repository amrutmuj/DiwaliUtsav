using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Diwali
{
    /// <summary>
    /// The whole interface. A small tray of real miniatures that waits at your side, like a thali
    /// held out to you, leaving the view ahead clear. Pinch a miniature to take the real thing. Grand Show
    /// and the settings sit in a narrow side panel beside the fireworks. Pinch the bar beneath them to
    /// move the tray anywhere; it remembers where you left it, relative to your body.
    /// </summary>
    public sealed class ThaliMenu : MonoBehaviour, IFestivalGrasp
    {
        public const string DockKey = "DiwaliMR.ThaliDock.v2";
        public const float DefaultYaw = -65;
        public const float DefaultReach = .65f;
        public const float DefaultDrop = .32f;

        [Serializable]
        struct Dock
        {
            public float yaw, reach, drop;
        }

        public Transform plate;
        public TMP_Text caption;
        public ThaliTile[] tiles;
        [Tooltip("Where the tray sits, in degrees from straight ahead. Negative is to the left.")]
        public float dockYaw = DefaultYaw;
        [Tooltip("Distance out from the body, and below eye level.")]
        public float reach = DefaultReach;
        public float drop = DefaultDrop;
        [Tooltip("The tray waits until you turn further than this before coming round to follow. Wide enough to look straight at the side panel.")]
        public float followAngle = 72;
        public float followDistance = 0.45f;
        public float tilt = 42;
        [Tooltip("How far from the middle of the plate a held object counts as brought back, across both panels.")]
        public Vector2 receiveHalfSize = new Vector2(0.284f, 0.162f);

        public bool Moving => mover != null;

        Vector3 targetPosition, velocity, facing = Vector3.forward, grabOffset;
        float grabDistance;
        bool grabbedNear, settled;
        FestivalPointer mover;
        string hoverLine;
        ThaliTile hovered;

        DiwaliSession Session => DiwaliSession.Instance;

        void Awake()
        {
            string saved = PlayerPrefs.GetString(DockKey, "");
            if (string.IsNullOrEmpty(saved)) return;
            var dock = JsonUtility.FromJson<Dock>(saved);
            dockYaw = dock.yaw;
            reach = dock.reach;
            drop = dock.drop;
        }

        void Start()
        {
            Show(Session && Session.Started);
        }

        public void Show(bool visible)
        {
            gameObject.SetActive(visible);
            settled = false;
        }

        public void SetHover(ThaliTile tile, string line)
        {
            hovered = tile;
            hoverLine = line;
        }

        public void ClearHover(ThaliTile tile)
        {
            if (hovered == tile) hoverLine = null;
        }

        /// <summary>True when a held object has been brought back to the tray.</summary>
        public bool Receives(FestivalPointer pointer)
        {
            if (!gameObject.activeInHierarchy) return false;
            Vector3 local = plate.InverseTransformPoint(pointer.IsHand ? pointer.PinchPoint : pointer.Ray.GetPoint(.4f));
            return Mathf.Abs(local.x) < receiveHalfSize.x && Mathf.Abs(local.y) < receiveHalfSize.y && Mathf.Abs(local.z) < .09f;
        }

        public void BeginMove(FestivalPointer pointer)
        {
            if (pointer.Grasp != null) return;
            mover = pointer;
            pointer.Grasp = this;
            grabDistance = Vector3.Distance(pointer.Ray.origin, transform.position);
            grabbedNear = Vector3.Distance(pointer.PinchPoint, transform.position) < 0.32f;
            grabOffset = transform.position - HoldPoint(pointer);
            Session.sound.Play("lift", transform.position, 1, 0.03f);
            Session.rig.Rumble(0.15f, 0.03f, pointer);
        }

        Vector3 HoldPoint(FestivalPointer pointer) => grabbedNear ? pointer.PinchPoint : pointer.Ray.GetPoint(grabDistance);

        public void Steer(FestivalPointer pointer)
        {
            transform.position = HoldPoint(pointer) + grabOffset;
            if (pointer.ReleasedNow) EndMove(pointer);
        }

        public void Lost(FestivalPointer pointer) => EndMove(pointer);

        void EndMove(FestivalPointer pointer)
        {
            pointer.Grasp = null;
            mover = null;

            // Remember the place relative to the body, so it stays at your side as you turn and walk.
            Transform head = Session.rig.view.transform;
            facing = FlatForward(head, facing);
            Vector3 offset = transform.position - head.position;
            drop = Mathf.Clamp(-offset.y, -0.25f, 0.8f);
            offset.y = 0;
            dockYaw = Vector3.SignedAngle(facing, offset, Vector3.up);
            reach = Mathf.Clamp(offset.magnitude, 0.3f, 0.95f);
            targetPosition = DockPosition(head);

            PlayerPrefs.SetString(DockKey, JsonUtility.ToJson(new Dock { yaw = dockYaw, reach = reach, drop = drop }));
            PlayerPrefs.Save();
            Session.sound.Play("place", transform.position, 0.8f, 0.03f);
        }

        /// <summary>Puts the tray back at its default place on the left.</summary>
        public void ForgetDock(float yaw, float newReach, float newDrop)
        {
            PlayerPrefs.DeleteKey(DockKey);
            dockYaw = yaw;
            reach = newReach;
            drop = newDrop;
            settled = false;
        }

        static Vector3 FlatForward(Transform head, Vector3 fallback)
        {
            Vector3 forward = head.forward;
            forward.y = 0;
            return forward.sqrMagnitude < 0.02f ? fallback : forward.normalized;
        }

        Vector3 DockPosition(Transform head)
        {
            return head.position + Quaternion.AngleAxis(dockYaw, Vector3.up) * facing * reach + Vector3.down * drop;
        }

        void LateUpdate()
        {
            Follow();
            RefreshGuidance();
        }

        void Follow()
        {
            Transform head = Session.rig.view.transform;

            if (!Moving)
            {
                Vector3 forward = FlatForward(head, facing);
                bool turned = Vector3.Angle(facing, forward) > followAngle;
                bool walked = Vector3.Distance(DockPosition(head), targetPosition) > followDistance;
                bool carrying = Session.rig.Pointers[0].Grasp != null || Session.rig.Pointers[1].Grasp != null;

                if (!settled || ((turned || walked) && !carrying))
                {
                    facing = forward;
                    targetPosition = DockPosition(head);
                }

                if (!settled)
                {
                    transform.position = targetPosition;
                    settled = true;
                }
                else
                {
                    transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, 0.32f);
                }
            }

            // It always turns to face you, wherever it has been put.
            Vector3 toTray = transform.position - head.position;
            toTray.y = 0;
            if (toTray.sqrMagnitude < 0.0004f) return;
            Quaternion turn = Quaternion.LookRotation(toTray, Vector3.up) * Quaternion.Euler(tilt, 0, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, turn, 1 - Mathf.Exp(-Time.deltaTime * 8));
        }

        void RefreshGuidance()
        {
            string line = Session.Guidance(out var suggested);
            caption.text = hoverLine ?? line;

            foreach (var tile in tiles) tile.SetSuggested(suggested.HasValue && tile.id == suggested.Value);

        }
    }
}
