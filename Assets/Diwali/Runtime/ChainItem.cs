using System.Collections;
using UnityEngine;

namespace Diwali
{
    /// <summary>
    /// Ladi and ring caps. A real ladi does not tick along evenly: it rattles off a run of reports,
    /// catches its breath, then rattles again, jumping about as it goes. It leaves a drifting bank of
    /// smoke and a scatter of red paper on the floor that stays for a while after the noise has stopped.
    /// </summary>
    public sealed class ChainItem : FestivalItem
    {
        public Transform[] pieces;
        public ParticleSystem smoke;
        [Tooltip("Paper left behind. It outlives the cracker, so it is released when the run ends.")]
        public ParticleSystem litter;
        [Tooltip("Reports per run, as a minimum and maximum.")]
        public Vector2Int runLength = new Vector2Int(3, 8);
        [Tooltip("Seconds between reports inside a run.")]
        public Vector2 gap = new Vector2(0.025f, 0.048f);
        [Tooltip("Seconds of quiet between runs.")]
        public Vector2 pause = new Vector2(0.035f, 0.12f);
        public string report = "pop";
        public float volume = 1;
        public float popSize = 1;
        public int smokePerReport = 1;
        public int litterPerReport = 3;
        [Tooltip("How far each report kicks the crackers next to it, in metres.")]
        public float kick = 0.004f;
        public Color flashColour = new Color(1, 0.72f, 0.4f);

        protected override void Awake()
        {
            base.Awake();
            if (!litter) return;
            var collision = litter.collision;
            collision.SetPlane(0, Session.placement.floor.Surface);
        }

        protected override IEnumerator Perform()
        {
            int next = 0;
            while (next < pieces.Length)
            {
                int run = Random.Range(runLength.x, runLength.y + 1);
                for (int i = 0; i < run && next < pieces.Length; i++, next++)
                {
                    while (RemovalHidden) yield return null;
                    Report(next);
                    yield return new WaitForSeconds(Random.Range(gap.x, gap.y));
                }
                yield return new WaitForSeconds(Random.Range(pause.x, pause.y));
            }

            if (litter)
            {
                litter.transform.SetParent(null, true);
                litter.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Session.placement.KeepLitter(litter);
            }
            yield return new WaitForSeconds(0.8f);
        }

        void Report(int index)
        {
            Transform piece = pieces[index];
            Vector3 position = piece.position + Vector3.up * 0.01f;
            piece.gameObject.SetActive(false);

            Session.sky.Pop(position, flashColour, popSize);
            if (id != CrackerId.Caps) Session.sky.Pop(position + transform.forward * 0.018f, flashColour, popSize * 0.8f);
            Session.sound.Play(report, position, volume, 0.025f);
            Session.rig.Rumble(0.18f, 0.03f);
            Session.ambience.Flash(Session.Gentle ? 0 : 0.02f);

            var burst = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = false };
            if (smoke) smoke.Emit(burst, smokePerReport);
            if (litter) litter.Emit(burst, litterPerReport);

            // Each report kicks the ones still waiting, which is what makes a ladi jump about.
            for (int i = index + 1; i < Mathf.Min(index + 4, pieces.Length); i++)
            {
                Vector2 shove = Random.insideUnitCircle * kick;
                pieces[i].localPosition += new Vector3(shove.x, 0, shove.y);
                pieces[i].Rotate(0, Random.Range(-9f, 9f), 0, Space.World);
            }
        }
    }
}
