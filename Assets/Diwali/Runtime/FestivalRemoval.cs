using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Diwali
{
    /// <summary>
    /// A continuous two-second ray pinch hides an object and offers Remove beside it. Pinching Remove deletes
    /// it for good. A tap anywhere else, or letting the offer run out, brings the object back as it was.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class FestivalRemoval : MonoBehaviour
    {
        public const float HoldSeconds = 2;
        const float OfferSeconds = 6;
        sealed class Gesture
        {
            public FestivalItem item;
            public IFestivalTarget action;
            public bool active, shown;
            public float down;
        }
        readonly Dictionary<FestivalPointer, Gesture> gestures = new Dictionary<FestivalPointer, Gesture>();
        FestivalItem selected;
        FestivalRemoveButton button;
        float hideAt;
        Vector3 buttonOffset;
        readonly Dictionary<Renderer, bool> renderers = new Dictionary<Renderer, bool>();
        readonly Dictionary<Collider, bool> colliders = new Dictionary<Collider, bool>();
        readonly Dictionary<Light, bool> lights = new Dictionary<Light, bool>();
        public FestivalItem Selected => selected;
        public FestivalRemoveButton Button => button;

        static FestivalItem ObjectOf(IFestivalTarget target) => target is FestivalFuseTarget fuse ? fuse.item : target as FestivalItem;
        static bool Available(FestivalItem item) => item && item.gameObject.activeInHierarchy &&
            item.Carrier == null && item.HasBeenPlaced && !item.Airborne;

        public void Cancel(FestivalPointer pointer) => gestures.Remove(pointer);

        /// <summary>A tap anywhere but the Remove button withdraws the offer, and the object comes back.</summary>
        public bool HandleTapAway(FestivalPointer pointer, IFestivalTarget target)
        {
            if (pointer == null || !pointer.Tracked || !(pointer.PressedNow || pointer.GrippedNow) || ReferenceEquals(target, button) || !selected) return false;
            Dismiss();
            return true;
        }

        // Reserve a ray pinch until it becomes a long hold or a short released action.
        public bool Handle(FestivalPointer pointer, IFestivalTarget target, float now)
        {
            FestivalItem item = ObjectOf(target);
            if (!pointer.Tracked || pointer.Grasp != null || pointer.NearInteraction || pointer.Gripping || !Available(item))
            {
                Cancel(pointer);
                return false;
            }
            if (!gestures.TryGetValue(pointer, out var gesture) || gesture.item != item)
            {
                gesture = new Gesture { item = item, action = target };
                gestures[pointer] = gesture;
            }
            if (pointer.PressedNow)
            {
                gesture.down = now;
                gesture.active = true;
                gesture.shown = false;
                gesture.action = target;
            }
            if (!gesture.active) return false;
            if (pointer.Pressed)
            {
                if (!gesture.shown && now - gesture.down >= HoldSeconds)
                {
                    Show(item, now);
                    gesture.shown = true;
                }
                return true;
            }
            Cancel(pointer);
            if (!gesture.shown)
            {
                var action = gesture.action;
                // Fuse actions use the pinch edge; replay that edge only for the resolved single action.
                bool previous = pointer.WasPressed;
                bool pressed = pointer.Pressed;
                pointer.Pressed = true;
                pointer.WasPressed = false;
                action.Press(pointer);
                pointer.Pressed = pressed;
                pointer.WasPressed = previous;
                return true;
            }
            return true;
        }

        void Show(FestivalItem item, float now)
        {
            Dismiss();
            if (!button)
            {
                var root = new GameObject("Remove object", typeof(RectTransform), typeof(Canvas), typeof(Image), typeof(BoxCollider), typeof(FestivalRemoveButton));
                root.layer = 8;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 110;
                var rect = root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(160, 52);
                rect.localScale = Vector3.one * .001f;
                root.GetComponent<BoxCollider>().size = new Vector3(160, 52, 18);
                root.GetComponent<BoxCollider>().isTrigger = true;
                var image = root.GetComponent<Image>();
                image.color = new Color(.3f, .075f, .055f, .98f);
                image.raycastTarget = false;
                var text = new GameObject("Remove label", typeof(RectTransform), typeof(TextMeshProUGUI));
                text.transform.SetParent(root.transform, false);
                var textRect = text.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                var label = text.GetComponent<TextMeshProUGUI>();
                label.font = DiwaliSession.Instance.hint.label.font;
                label.fontSize = 32; label.text = "Remove";
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                button = root.GetComponent<FestivalRemoveButton>();
                button.owner = this;
            }
            selected = item; hideAt = now + OfferSeconds;
            var bounds = new Bounds(item.transform.position, Vector3.zero);
            foreach (var collider in item.GetComponentsInChildren<Collider>()) bounds.Encapsulate(collider.bounds);
            buttonOffset = new Vector3(bounds.center.x, bounds.max.y + .09f, bounds.center.z) - item.transform.position;
            SetHidden(item, true);
            DiwaliSession.Instance.hint.HideInstant();
            HideParts(item);
            button.gameObject.SetActive(true);
            PositionButton();
        }

        void PositionButton()
        {
            Vector3 position = selected.transform.position + buttonOffset;
            var head = DiwaliSession.Instance.rig.view.transform;
            button.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - head.position));
        }
        void LateUpdate()
        {
            if (!button || !button.gameObject.activeSelf) return;
            if (!Available(selected) || Time.unscaledTime >= hideAt) { Dismiss(); return; }
            HideParts(selected);
            PositionButton();
        }
        public void Dismiss()
        {
            if (selected) SetHidden(selected, false);
            RestorePartStates();
            selected = null;
            if (button) button.gameObject.SetActive(false);
        }
        static void SetHidden(FestivalItem item, bool value)
        {
            item.RemovalHidden = value;
            var session = DiwaliSession.Instance;
            if (session && session.sound) session.sound.Hush(item.transform, value);
        }
        void HideParts(FestivalItem item)
        {
            // forceRenderingOff preserves each renderer's own enabled state while animations continue.
            foreach (var part in item.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderers.ContainsKey(part)) renderers.Add(part, part.forceRenderingOff);
                part.forceRenderingOff = true;
            }
            foreach (var part in item.GetComponentsInChildren<Collider>(true))
            {
                if (!colliders.ContainsKey(part)) colliders.Add(part, part.enabled);
                part.enabled = false;
            }
            foreach (var part in item.GetComponentsInChildren<Light>(true))
            {
                if (!lights.ContainsKey(part)) lights.Add(part, part.enabled);
                part.enabled = false;
            }
        }
        void RestorePartStates()
        {
            foreach (var part in renderers) if (part.Key) part.Key.forceRenderingOff = part.Value;
            foreach (var part in colliders) if (part.Key) part.Key.enabled = part.Value;
            foreach (var part in lights) if (part.Key) part.Key.enabled = part.Value;
            renderers.Clear(); colliders.Clear(); lights.Clear();
        }

        /// <summary>Deletes the offered object for good. There is no undo: nothing after this brings it back.</summary>
        public void Remove()
        {
            var item = selected;
            if (Available(item))
            {
                var session = DiwaliSession.Instance;
                session.sound.StopFor(item.transform);
                session.hint.Hide();
                selected = null;
                renderers.Clear(); colliders.Clear(); lights.Clear();
                button.gameObject.SetActive(false);
                session.placement.Remove(item);
            }
            else Dismiss();
            gestures.Clear();
        }
        void OnDisable() => Dismiss();
        void OnDestroy() { Dismiss(); if (button) Destroy(button.gameObject); }
    }
}
