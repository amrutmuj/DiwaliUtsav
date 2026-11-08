using System.Collections;
using UnityEngine;

namespace Diwali
{
    /// <summary>The diya stays alight for the rest of the session and quietly lights the room.</summary>
    public sealed class DiyaItem : FestivalItem
    {
        public FlameDriver flame;
        public override bool Persists => true;
        public bool Lit => State == ItemState.Burning;

        protected override IEnumerator Perform()
        {
            Session.sound.Play("ignite", fuse.position, 0.7f);
            flame.Ignite();
            Session.sound.StartLoop("flame", fuse, 0.9f, 1.2f);
            Session.Completed(id);
            while (true) yield return null;
        }
    }
}
