using System.Collections;
using UnityEngine;

namespace Diwali
{
    public enum ItemState { Placing, Ready, Burning, Spent }

    /// <summary>
    /// Anything set down in the real room. It waits with a softly glowing fuse, burns the fuse when a
    /// flame touches it, performs, then cools and clears itself away. Crackers are consumable, like the
    /// real ones: taking another from the thali is how you do it again.
    /// </summary>
    public abstract class FestivalItem : MonoBehaviour, IFestivalGrabbable
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Intensity = Shader.PropertyToID("_Intensity");

        public CrackerId id;
        [Tooltip("Where a flame has to touch.")]
        public Transform fuse;
        public float fuseSeconds = 2.4f;
        [Tooltip("When set, the fuse lasts a random time in this range instead.")]
        public Vector2 fuseRange;
        [Tooltip("The cord, which shortens as it burns.")]
        public Transform fuseCord;
        public Vector3 fuseRoot;
        public SparkField fuseSparks;
        [Tooltip("Soft pulse that says: light me here.")]
        public Renderer fuseCue;
        public GameObject model;
        [Tooltip("How long the burnt-out shell stays before clearing.")]
        public float restSeconds = 5;
        [Tooltip("Keep people this far back while the fuse burns.")]
        public float safetyRadius = 0.8f;

        public ItemState State { get; private set; } = ItemState.Placing;
        public FestivalPointer Carrier { get; set; }
        public bool RemovalHidden { get; internal set; }
        public bool HasBeenPlaced { get; private set; }
        public float LastFuseSeconds { get; private set; }
        public string AnchorId { get; set; }
        public virtual bool NeedsFlame => true;
        public virtual bool Persists => false;
        /// <summary>Once set down it stays exactly there, and no hand can pick it up again.</summary>
        public virtual bool FixedOnceSet => false;
        public CrackerPhysics Physics => CrackerPhysics.For(id);
        public bool Airborne => TryGetComponent(out FestivalToss toss) && toss.Flying;

        protected static DiwaliSession Session => DiwaliSession.Instance;

        MaterialPropertyBlock cueBlock;
        float cuePhase;
        bool hovered;

        protected virtual void Awake()
        {
            cueBlock = new MaterialPropertyBlock();
            cuePhase = Random.value * 6;
            if (fuseCue) fuseCue.enabled = false;
        }

        /// <summary>The item has settled on a real surface and can be lit.</summary>
        public void Settle()
        {
            HasBeenPlaced = true;
            if (State == ItemState.Placing) State = ItemState.Ready;
            if (fuseCue) fuseCue.enabled = NeedsFlame;
        }

        /// <summary>Called when picked back up, so the fuse cue hides while it is moving.</summary>
        public void Lift()
        {
            if (State == ItemState.Ready) State = ItemState.Placing;
            if (fuseCue) fuseCue.enabled = false;
        }

        protected virtual void Update()
        {
            if (State != ItemState.Ready || !fuseCue || !fuseCue.enabled) return;
            float resting = 0.16f + 0.12f * Mathf.Sin(Time.time * 2.6f + cuePhase);
            cueBlock.SetFloat(Intensity, hovered ? 0.7f : resting);
            fuseCue.SetPropertyBlock(cueBlock);
        }

        public void Ignite()
        {
            if (RemovalHidden || State != ItemState.Ready || !NeedsFlame) return;
            Activate();
        }

        protected void Activate()
        {
            if (State == ItemState.Burning || State == ItemState.Spent) return;
            State = ItemState.Burning;
            if (fuseCue) fuseCue.enabled = false;
            Session.hint.Hide();
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (NeedsFlame) yield return BurnFuse();
            yield return Perform();
            State = ItemState.Spent;
            Session.Completed(id);
            yield return Retire();
        }

        IEnumerator BurnFuse()
        {
            LastFuseSeconds = fuseRange.y > 0 ? Random.Range(fuseRange.x, fuseRange.y) : fuseSeconds;
            if (LastFuseSeconds <= 0) yield break;

            Session.placement.ShowSafety(transform.position, safetyRadius, LastFuseSeconds);
            var sizzle = Session.sound.StartLoop("fuse", fuse, 1, 0.08f);
            if (fuseSparks) fuseSparks.Begin();

            float elapsed = 0;
            Vector3 start = fuse.localPosition;
            Vector3 cordStart = fuseCord ? fuseCord.localPosition : default;
            Vector3 cordSize = fuseCord ? fuseCord.localScale : default;
            while (elapsed < LastFuseSeconds)
            {
                // Behind Remove the fuse waits, so nothing goes off from an object nobody can see.
                if (RemovalHidden) { yield return null; continue; }
                elapsed += Time.deltaTime;
                float burnt = Mathf.Clamp01(elapsed / LastFuseSeconds);
                fuse.localPosition = Vector3.Lerp(start, fuseRoot, burnt);
                sizzle.SetPitch(Mathf.Lerp(1, 1.3f, burnt * burnt));
                if (fuseCord)
                {
                    fuseCord.localPosition = Vector3.Lerp(cordStart, fuseRoot, burnt);
                    fuseCord.localScale = new Vector3(cordSize.x, cordSize.y * (1 - burnt), cordSize.z);
                }
                yield return null;
            }

            sizzle.Stop(0.05f);
            if (fuseSparks) fuseSparks.Stop();
        }

        protected abstract IEnumerator Perform();

        /// <summary>Impact crackers consume themselves on a sufficiently firm landing.</summary>
        public virtual bool FloorImpact(float speed) => false;

        IEnumerator Retire()
        {
            if (model)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColor, new Color(0.14f, 0.12f, 0.11f));
                foreach (var part in model.GetComponentsInChildren<Renderer>()) part.SetPropertyBlock(block);
            }

            yield return new WaitForSeconds(restSeconds);

            // A shell behind Remove waits too, or it would clear itself before it could be brought back.
            while (RemovalHidden) yield return null;

            Vector3 size = transform.localScale;
            for (float t = 0; t < 1; t += Time.deltaTime / 0.5f)
            {
                transform.localScale = size * (1 - t * t);
                yield return null;
            }

            Session.placement.Forget(this);
            Destroy(gameObject);
        }

        public void Hover(bool on) => hovered = on;

        public bool CanGrab(FestivalPointer pointer)
        {
            if (RemovalHidden || pointer == null || pointer.Grasp != null || State == ItemState.Burning || State == ItemState.Spent) return false;
            return Carrier != null ? Carrier != pointer : (State == ItemState.Ready && !FixedOnceSet) || (State == ItemState.Placing && Airborne);
        }

        public void Press(FestivalPointer pointer)
        {
            if (CanGrab(pointer)) Session.placement.Grab(this, pointer);
        }
    }
}
