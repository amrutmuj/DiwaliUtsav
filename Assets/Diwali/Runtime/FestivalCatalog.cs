using System;
using UnityEngine;

namespace Diwali
{
    public enum CrackerId
    {
        Lighter, Diya, Phuljhadi, Rangoli,
        Anar, FlowerPot, Chakri, Ladi, Bomb, Caps,
        Rocket, SkyShot, TwelveShot, FifteenShot, GrandShow,
        BigLadi,
        Butterfly, Popper,
    }

    /// <summary>The rows of the thali, from what you hold to what fills the sky.</summary>
    public enum CrackerTier { Light, Ground, Sky }

    [Serializable]
    public sealed class CrackerEntry
    {
        public CrackerId id;
        public string title;
        [Tooltip("One short line shown while the item is pointed at.")]
        public string description;
        public CrackerTier tier;
        [Tooltip("Taken into the hand instead of being set down on a surface.")]
        public bool held;
        public float footprintRadius = 0.1f;
        public GameObject prefab;
    }

    public sealed class FestivalCatalog : ScriptableObject
    {
        /// <summary>The suggested path through the celebration. Everything stays available at all times.</summary>
        public static readonly CrackerId[] Journey =
        {
            CrackerId.Diya, CrackerId.Phuljhadi, CrackerId.Anar, CrackerId.Chakri,
            CrackerId.Ladi, CrackerId.Rocket, CrackerId.TwelveShot, CrackerId.FifteenShot,
        };

        public CrackerEntry[] entries;

        public CrackerEntry Find(CrackerId id) => Array.Find(entries, entry => entry.id == id);
    }
}
