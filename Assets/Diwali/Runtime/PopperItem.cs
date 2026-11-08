using System.Collections;
using UnityEngine;

namespace Diwali
{
    /// <summary>A paper popper that snaps on impact after being thrown, with no fuse. The harder the throw, the sharper the crack.</summary>
    public sealed class PopperItem : FestivalItem
    {
        public ParticleSystem smoke;
        const float HardThrow = 7;

        public float impactSpeed = 1.1f;
        public override bool NeedsFlame => false;
        public bool Popped { get; private set; }
        float strike;

        public override bool FloorImpact(float speed)
        {
            if (Popped || speed < impactSpeed) return Popped;
            Popped = true;
            strike = Mathf.InverseLerp(impactSpeed, HardThrow, speed);
            Activate();
            return true;
        }

        protected override IEnumerator Perform()
        {
            model.SetActive(false);
            var shadow = transform.Find("Contact shadow");
            if (shadow) shadow.gameObject.SetActive(false);
            Vector3 site = transform.position + Vector3.up * 0.015f;
            Session.sound.Play("snap", site, Mathf.Lerp(0.72f, 1, strike), 0.06f);
            Session.sky.Pop(site, SkyShow.Pearl, Mathf.Lerp(0.3f, 0.42f, strike));
            smoke.Emit(new ParticleSystem.EmitParams { position = site, applyShapeToPosition = false }, 10);
            Session.rig.Rumble(0.15f, 0.025f);
            yield return new WaitForSeconds(0.25f);
        }
    }
}
