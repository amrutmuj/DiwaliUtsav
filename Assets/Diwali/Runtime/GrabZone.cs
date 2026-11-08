using System.Collections.Generic;
using UnityEngine;

namespace Diwali
{
    /// <summary>Something a hand can take hold of.</summary>
    public interface IFestivalGrabbable : IFestivalTarget
    {
        bool CanGrab(FestivalPointer pointer);
    }

    /// <summary>
    /// The part of an object a hand can actually take hold of. Reaching inside this sphere is what
    /// counts, and the object's edges light as the hand approaches, so you can see what a pinch will
    /// take rather than guess. It is a plain distance test, so it also works on objects in the other hand.
    /// </summary>
    public sealed class GrabZone : MonoBehaviour
    {
        static readonly List<GrabZone> Zones = new List<GrabZone>();

        [Tooltip("Centre of the region, relative to this object.")]
        public Vector3 centre;
        public float radius = 0.1f;

        public IFestivalGrabbable Owner { get; private set; }
        public Vector3 WorldCentre => transform.TransformPoint(centre);
        public EdgeHalo Halo => halo ? halo : halo = EdgeHalo.On(transform.Find("Model"));

        EdgeHalo halo;

        void Awake() => Owner = GetComponent<IFestivalGrabbable>();
        void OnEnable() => Zones.Add(this);
        void OnDisable() => Zones.Remove(this);

        /// <summary>
        /// The closest zone this pointer could grab, within reach times its radius.
        /// <paramref name="depth"/> is the distance in radii, so 1 or less means inside the region.
        /// </summary>
        /// <param name="airborneOnly">Only consider crackers in flight, for catching.</param>
        public static GrabZone Nearest(FestivalPointer pointer, float reach, out float depth, bool airborneOnly = false)
        {
            GrabZone best = null;
            depth = float.MaxValue;
            foreach (var zone in Zones)
            {
                if (zone.Owner == null || !zone.Owner.CanGrab(pointer)) continue;
                if (airborneOnly && !(zone.Owner is FestivalItem { Airborne: true })) continue;
                float distance = Vector3.Distance(pointer.PinchPoint, zone.WorldCentre) / zone.radius;
                if (distance > reach || distance >= depth) continue;
                depth = distance;
                best = zone;
            }
            return best;
        }
    }
}
