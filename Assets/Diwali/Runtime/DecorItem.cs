using System.Collections;
using UnityEngine;

namespace Diwali
{
    /// <summary>Quiet decoration on the floor. Nothing to light, and once set down it stays exactly there.</summary>
    public sealed class DecorItem : FestivalItem
    {
        public override bool NeedsFlame => false;
        public override bool Persists => true;
        public override bool FixedOnceSet => true;
        protected override IEnumerator Perform() { yield break; }
    }
}
