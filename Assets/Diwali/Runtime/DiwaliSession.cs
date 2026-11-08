using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Diwali
{
    /// <summary>
    /// The celebration itself: what has been done, what to suggest next, and how a flame touching a
    /// fuse lights it. Nothing is locked. The journey only ever suggests, in one short line.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class DiwaliSession : MonoBehaviour
    {
        const float TouchDistance = 0.085f;
        public const string ExperienceSceneName = "DiwaliExperience";

        public static DiwaliSession Instance { get; private set; }

        public FestivalCatalog catalog;
        public FestivalRig rig;
        public FestivalAudio sound;
        public FestivalAmbience ambience;
        public SkyShow sky;
        public FestivalPlacement placement;
        public ThaliMenu thali;
        public FestivalHint hint;
        public LighterItem lighter;
        public SparklerItem sparkler;
        public FestivalMusic music;

        public bool Started { get; private set; }
        public bool Gentle { get; private set; }
        public int JourneyProgress => completed.Count;

        readonly HashSet<CrackerId> completed = new HashSet<CrackerId>();
        readonly HashSet<CrackerId> hinted = new HashSet<CrackerId>();
        string overrideLine;
        float overrideUntil;
        bool restarting;

        void Awake()
        {
            Instance = this;
            FestivalPerf.Note("session Awake");
            if (!Application.isEditor) Application.targetFrameRate = 72;
            lighter.gameObject.SetActive(false);
            sparkler.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Opening the experience directly (including Reset) starts play without a launch card.
        IEnumerator Start()
        {
            // The rig establishes headset state and the tracked floor before play begins.
            yield return null;
            StartCoroutine(FestivalWarmup.Run(this));
            Begin();
        }

        public void HeadsetChanged(bool present)
        {
            if (music) music.SetHeadset(present);
            ambience.SetHeadset(present);
            placement.HeadsetChanged(present);
            if (Started) placement.ReadFloor();
        }

        public void Begin()
        {
            if (Started) return;
            Started = true;
            thali.Show(true);
            ambience.Begin();
            placement.ReadFloor();
            Vector3 head = rig.view.transform.position;
            sound.StartNightBed(head + rig.view.transform.forward * 6 + Vector3.up * 2);
        }

        /// <summary>A pinch on a thali tile. Tools go to the hand; ray-selected crackers preview on the floor.</summary>
        public void Take(CrackerId id, FestivalPointer pointer)
        {
            if (!Started || restarting || (pointer != null && pointer.Grasp != null)) return;
            var entry = catalog.Find(id);
            if (id == CrackerId.GrandShow)
            {
                sky.BeginGrandShow();
                Say("Look up.", 4);
                return;
            }
            if (id == CrackerId.Lighter) TakeLighter(pointer);
            else if (id == CrackerId.Phuljhadi) TakeSparkler(pointer);
            else if (placement.HasRoomForMore) placement.TakeNew(entry, pointer);
            else Say("Plenty out already. Light a few first.");
        }

        void TakeLighter(FestivalPointer pointer)
        {
            if (pointer != null) lighter.Take(pointer);
        }

        void TakeSparkler(FestivalPointer pointer)
        {
            sparkler.Take(pointer);
            if (hinted.Add(CrackerId.Phuljhadi)) Say("Touch its tip to a flame.", 7);
        }

        public void ItemPlaced(FestivalItem item)
        {
            if (!item.NeedsFlame || !hinted.Add(item.id)) return;
            string line = lighter.Holder != null ? "Touch the lit lighter here" : "Touch a flame here, or pinch it from where you stand";
            hint.Show(item.fuse ? item.fuse : item.transform, line);
        }

        public void Completed(CrackerId id)
        {
            if (id == CrackerId.BigLadi) id = CrackerId.Ladi;
            if (Array.IndexOf(FestivalCatalog.Journey, id) >= 0 && completed.Add(id))
            {
                if (completed.Count == FestivalCatalog.Journey.Length) Say("All eight are done. The Grand Show is ready.", 12);
            }
        }

        public void CelebrationEnded() => Say("Shubh Deepavali. May your year be full of light.", 14);

        public void Say(string line, float seconds = 5)
        {
            overrideLine = line;
            overrideUntil = Time.time + seconds;
        }

        public void SetGentle(bool gentle)
        {
            Gentle = gentle;
            sound.SetGentle(gentle);
        }

        public void ResetAll()
        {
            if (restarting) return;
            restarting = true;
            sky.StopEverything();
            sound.StopAll();
            ambience.Stop();
            if (lighter.gameObject.activeSelf) lighter.Return();
            if (sparkler.gameObject.activeSelf) sparkler.Return();
            placement.ClearAll();
            hint.Hide();
            completed.Clear();
            hinted.Clear();
            overrideLine = null;
            Started = false;
            thali.Show(false);
            SceneManager.LoadSceneAsync(ExperienceSceneName, LoadSceneMode.Single);
        }

        void Update()
        {
            if (Started) IgniteByTouch();
        }

        /// <summary>Lighting is physical: any flame that reaches a fuse lights it, whatever is holding it.</summary>
        void IgniteByTouch()
        {
            bool lighterOn = lighter.gameObject.activeInHierarchy && lighter.FlameOn;
            bool sparklerLit = sparkler.gameObject.activeInHierarchy && sparkler.Lit;
            bool sparklerWaiting = sparkler.gameObject.activeInHierarchy && !sparkler.Lit;

            if (sparklerWaiting && lighterOn && Near(lighter.tip, sparkler.tip)) sparkler.Ignite();

            foreach (var item in placement.Items)
            {
                if (!item || item.RemovalHidden || !item.fuse) continue;
                if (item.State == ItemState.Ready && item.NeedsFlame)
                {
                    if ((lighterOn && Near(lighter.tip, item.fuse)) || (sparklerLit && Near(sparkler.burnPoint, item.fuse))) item.Ignite();
                }
                else if (sparklerWaiting && item is DiyaItem diya && diya.Lit && Near(sparkler.tip, diya.fuse))
                {
                    sparkler.Ignite();
                }
            }
        }

        static bool Near(Transform a, Transform b) => Vector3.Distance(a.position, b.position) < TouchDistance;

        /// <summary>The single line of guidance on the thali, and the tile it points to.</summary>
        public string Guidance(out CrackerId? suggested)
        {
            suggested = null;
            if (lighter.Holder != null && sparkler.Holder != null)
                return "Open a hand to put its tool down. Pinch the tool to pick it up again.";
            if (overrideLine != null)
            {
                if (Time.time < overrideUntil) return overrideLine;
                overrideLine = null;
            }

            CrackerId? next = null;
            foreach (var id in FestivalCatalog.Journey)
            {
                if (completed.Contains(id)) continue;
                next = id;
                break;
            }

            if (next == null)
            {
                suggested = CrackerId.GrandShow;
                return sky.ShowRunning ? "" : "Pinch the Grand Show when you are ready.";
            }

            if (next == CrackerId.Phuljhadi)
            {
                if (sparkler.Holder != null) return sparkler.Lit ? "Hold to draw. Open your hand to put it down; pinch to pick it up again." : "Touch its tip to a lit diya or the lighter.";
                suggested = CrackerId.Phuljhadi;
                return "Take a phuljhadi.";
            }

            var waiting = placement.Items.Find(item => item && item.State != ItemState.Spent && (item.id == next || (item.id == CrackerId.BigLadi && next == CrackerId.Ladi)));
            if (waiting == null)
            {
                suggested = next;
                return "Pinch the " + catalog.Find(next.Value).title.ToLowerInvariant() + " and set it down.";
            }
            if (waiting.State == ItemState.Burning) return "";
            if (lighter.Holder != null) return "Touch the lit lighter to the " + (next == CrackerId.Diya ? "wick." : "fuse.");
            suggested = CrackerId.Lighter;
            return "Take the lighter, or pinch the " + (next == CrackerId.Diya ? "wick" : "fuse") + " to light it from here.";
        }

        public bool IsComplete(CrackerId id) => completed.Contains(id);
    }
}
