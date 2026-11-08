using UnityEngine;

namespace Diwali
{
    /// <summary>Hold a pinch or grip to carry; open the hand to drop. Active tools can be picked up again.</summary>
    public abstract class FestivalHeld : MonoBehaviour, IFestivalGrabbable, IFestivalGrasp
    {
        public Transform tip;
        public Vector3 gripRotation = new Vector3(70, 0, 0);
        public FestivalPointer Holder { get; private set; }
        public bool Dropped => motion && motion.Dropped;

        /// <summary>Gone the moment it touches the floor, to be taken from its tray tile again.</summary>
        public virtual bool ClearsOnLanding => false;
        protected virtual string CarryLine => "Hold pinch or grip to carry. Open your hand to put it down; pinch it to pick it up again.";
        protected virtual string DroppedLine => "Hand free. Pick the tool up again, or take it from its tray tile.";
        protected static DiwaliSession Session => DiwaliSession.Instance;
        FestivalToolDrop motion;
        BoxCollider shell;
        bool hovering;

        protected virtual void Awake()
        {
            gameObject.layer = 8;
            shell = GetComponent<BoxCollider>();
            if (!shell) shell = gameObject.AddComponent<BoxCollider>();
            bool sparkler = this is SparklerItem;
            shell.center = new Vector3(0, sparkler ? .19f : .043f, 0);
            shell.size = sparkler ? new Vector3(.045f, .4f, .045f) : new Vector3(.05f, .086f, .04f);
            shell.isTrigger = true;
            motion = GetComponent<FestivalToolDrop>();
            if (!motion) motion = gameObject.AddComponent<FestivalToolDrop>();
        }

        public virtual void Take(FestivalPointer pointer)
        {
            if (pointer == null || (pointer.Grasp != null && !ReferenceEquals(pointer.Grasp, this))) return;
            bool fresh = !gameObject.activeSelf;
            if (Holder != null) Holder.Grasp = null;
            gameObject.SetActive(true);
            motion.Catch();
            shell.enabled = false;
            Holder = pointer;
            pointer.Grasp = this;
            if (fresh) Prepare();
            PositionInHand(pointer);
            FestivalRig.HideRay(pointer);
            bool cracker = this is SparklerItem;
            Session.sound.Play(cracker ? "select" : "lift", pointer.PinchPoint, 1, cracker ? 0 : .03f);
            Session.rig.Rumble(.2f, .04f, pointer);
            Session.Say(CarryLine, 7);
        }

        protected virtual void Prepare() { }
        public bool CanGrab(FestivalPointer pointer) => gameObject.activeInHierarchy && pointer != null && Holder != pointer && pointer.Grasp == null;
        public void Hover(bool on) => hovering = on;
        public void Press(FestivalPointer pointer) { if (CanGrab(pointer)) Take(pointer); }

        protected virtual void Update()
        {
            if (hovering && Holder == null) GetComponent<GrabZone>().Halo.Show(1);
        }

        void PositionInHand(FestivalPointer pointer)
        {
            Quaternion aim = pointer.Rotation * Quaternion.Euler(gripRotation);
            transform.SetPositionAndRotation(pointer.PinchPoint, aim);
        }

        public virtual void Steer(FestivalPointer pointer)
        {
            if (pointer != Holder) return;
            PositionInHand(pointer);
            if (!pointer.Holding) Drop(pointer);
        }

        void Drop(FestivalPointer pointer)
        {
            Holder = null;
            pointer.Grasp = null;
            shell.enabled = true;
            OnDropped();
            motion.Drop(this, shell, pointer.ReleaseVelocity, pointer.ReleaseSpin);
            Session.Say(DroppedLine, 4);
        }

        // Brief tracking loss keeps the last pose. An open hand releases it when tracking returns.
        public void Lost(FestivalPointer pointer) { }

        protected virtual void OnDropped() { }

        public virtual void Return()
        {
            if (Holder != null) Holder.Grasp = null;
            Holder = null;
            motion?.Catch();
            gameObject.SetActive(false);
        }
    }
}
