using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Diwali
{
    /// <summary>
    /// Sky shot and the multi-shot boxes. A box is a tray of tubes fired strictly one at a time: a tube fires,
    /// its shell climbs and bursts, and only then does the next tube fire. It steps along the row and back along
    /// the following row, so bursts walk across a grid in the sky.
    /// </summary>
    public sealed class MortarItem : FestivalItem
    {
        const float MuzzleSeconds = 0.3f;

        public Transform muzzle;
        public SparkField muzzleSparks;
        public ParticleSystem smoke;
        public int shots = 12;
        public float interval = 0.62f;
        public int finaleShots = 0;
        [Tooltip("Lowest and highest burst, in metres above the tube. The spread keeps a run of shells from all opening at one point.")]
        public Vector2 burstHeight = new Vector2(19.85f, 21.85f);
        [Tooltip("How far each burst opens, in metres.")]
        public float burstRadius = 5.5f;
        [Tooltip("Pause between the fuse reaching the box and the first tube firing.")]
        public float firstShotDelay = 0.7f;
        [Tooltip("Pause between one shell bursting and the next tube firing.")]
        public float burstPause = 0.5f;
        [Tooltip("The same pause for the closing shots, which quicken.")]
        public float finalePause = 0.2f;
        [Tooltip("Distance between neighbouring bursts in the sky, in metres.")]
        public float skySpacing = 3.5f;
        [Tooltip("Distance between neighbouring tubes in the box, in metres.")]
        public float tubeSpacing = 0.035f;

        public readonly List<Vector3> BurstPoints = new List<Vector3>();
        public float FirstShotWait { get; private set; }
        public float ShortestGap { get; private set; } = float.MaxValue;

        protected override IEnumerator Perform()
        {
            float lit = Time.time;
            yield return new WaitForSeconds(firstShotDelay);
            FirstShotWait = Time.time - lit;

            smoke.Play();

            float last = Time.time;
            for (int i = 0; i < shots; i++)
            {
                while (RemovalHidden) yield return null;
                if (i > 0) ShortestGap = Mathf.Min(ShortestGap, Time.time - last);
                last = Time.time;
                if (muzzleSparks) muzzleSparks.Begin(MuzzleSeconds);

                // The next tube waits for this shell to burst.
                yield return Fire(StyleFor(i), i);
                bool finale = i >= shots - finaleShots;
                yield return new WaitForSeconds((finale ? finalePause : burstPause) * Random.Range(0.94f, 1.06f));
            }

            smoke.Stop();
            yield return new WaitForSeconds(3.5f);
        }

        Coroutine Fire(BurstStyle style, int index)
        {
            Vector2 cell = TrayCell(index);
            Vector3 across = transform.right, along = transform.forward;
            Vector3 from = muzzle.position + (across * cell.x + along * cell.y) * tubeSpacing;
            Vector3 burst = muzzle.position + (across * cell.x + along * cell.y) * skySpacing + Vector3.up * Random.Range(burstHeight.x, burstHeight.y);
            BurstPoints.Add(burst);
            Session.sound.Play("mortar", from, 1, 0.03f);
            Session.sky.ShowFlash(from, new Color(1, 0.7f, 0.35f), 0.7f, 0.1f, 0.75f);
            Session.ambience.Flash(Session.Gentle ? 0.02f : 0.07f);
            Session.rig.Rumble(0.35f, 0.07f);
            return Session.sky.ShellTo(from, burst, style.Sized(burstRadius), index % 5 == 2);
        }

        /// <summary>The tube's place in the tray, centred on the box. Rows run alternately, so each step is to a neighbour.</summary>
        Vector2 TrayCell(int index)
        {
            int columns = Mathf.CeilToInt(Mathf.Sqrt(shots));
            int rows = Mathf.CeilToInt(shots / (float)columns);
            int row = index / columns, column = index % columns;
            if (row % 2 == 1) column = columns - 1 - column;
            return new Vector2(column - (columns - 1) * 0.5f, row - (rows - 1) * 0.5f);
        }

        BurstStyle StyleFor(int index)
        {
            if (id == CrackerId.SkyShot) return BurstStyle.VioletPeony();
            if (id == CrackerId.TwelveShot)
            {
                switch (index % 3)
                {
                    case 0: return BurstStyle.GoldChrysanthemum();
                    case 1: return BurstStyle.Palm(SkyShow.ShellGold, SkyShow.ShellOrange);
                    default: return BurstStyle.Peony(SkyShow.ShellPale, SkyShow.ShellGold);
                }
            }
            switch (index % 5)
            {
                case 0: return BurstStyle.VioletPeony();
                case 1: return BurstStyle.Arc(SkyShow.ShellBlue, SkyShow.ShellBlue);
                case 2: return BurstStyle.RedSpider();
                case 3: return BurstStyle.Arc(SkyShow.ShellPink, SkyShow.ShellOrange);
                default: return BurstStyle.RedBead(0.22f);
            }
        }
    }
}
