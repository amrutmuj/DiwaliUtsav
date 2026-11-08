namespace Diwali
{
    /// <summary>The lighter lights only while held; releasing it extinguishes its flame.</summary>
    public sealed class LighterItem : FestivalHeld
    {
        public FlameDriver flame;
        public bool FlameOn { get; private set; }
        public override bool ClearsOnLanding => true;
        protected override string CarryLine => "Hold pinch or grip to carry. If it falls, take another from the tray.";
        protected override string DroppedLine => "Hand free. Take the lighter from the tray when you need it.";
        SoundLoop hiss;

        public override void Take(FestivalPointer pointer)
        {
            base.Take(pointer);
            if (pointer == null || Holder != pointer || FlameOn) return;
            FlameOn = true;
            flame.Ignite();
            Session.sound.Play("strike", tip.position, 1, .06f);
            hiss = Session.sound.StartLoop("lighter", tip, 1, .15f);
            Session.rig.Rumble(.3f, .05f, pointer);
        }

        public override void Return()
        {
            Extinguish();
            base.Return();
        }

        protected override void OnDropped() => Extinguish();

        void Extinguish()
        {
            FlameOn = false;
            flame.SetInstant(0);
            hiss?.Stop(.12f);
            hiss = null;
        }
    }
}
