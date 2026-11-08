using UnityEngine;

namespace Diwali
{
    /// <summary>
    /// The phuljhadi. Sparks are simulated in world space, so they hang in the air behind the moving
    /// hand. The burning point creeps down the rod, leaving a dark spent wire, as a real one does.
    /// </summary>
    public sealed class SparklerItem : FestivalHeld
    {
        public ParticleSystem sparks;
        public FlameDriver core;
        [Tooltip("Grey coating, scaled down from the tip as it burns.")]
        public Transform coating;
        public Transform burnPoint;
        public float burnSeconds = 26;

        public bool Lit { get; private set; }

        SoundLoop crackle;
        float burned;
        Vector3 fullCoating, tipStart, tipEnd;

        protected override void Awake()
        {
            base.Awake();
            fullCoating = coating.localScale;
            tipStart = burnPoint.localPosition;
            tipEnd = tipStart - Vector3.up * (fullCoating.y * 2f);
        }

        protected override void Prepare()
        {
            burned = 0;
            Lit = false;
            coating.localScale = fullCoating;
            burnPoint.localPosition = tipStart;
            core.SetInstant(0);
        }

        public void Ignite()
        {
            if (Lit) return;
            Lit = true;
            sparks.Play();
            core.Ignite();
            crackle = Session.sound.StartLoop("sparkler", burnPoint, 1, 0.3f);
            Session.sound.Play("ignite", burnPoint.position, 0.7f);
            Session.rig.Rumble(0.4f, 0.1f, Holder);
            Session.hint.Hide();
        }

        protected override void Update()
        {
            base.Update();
            if (!Lit || !gameObject.activeSelf) return;

            burned += Time.deltaTime / burnSeconds;
            coating.localScale = new Vector3(fullCoating.x, fullCoating.y * (1 - burned), fullCoating.z);
            burnPoint.localPosition = Vector3.Lerp(tipStart, tipEnd, burned);
            if (Holder != null) Session.rig.Rumble(0.04f, 0.03f, Holder);
            if (burned >= 1) Finish();
        }

        void Finish()
        {
            Session.Completed(CrackerId.Phuljhadi);
            Return();
            Session.Say("Phuljhadi finished. Take a fresh one from its tray tile.", 5);
        }

        public override void Return()
        {
            Lit = false;
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            core.Extinguish();
            crackle?.Stop(0.25f);
            base.Return();
        }
    }
}
