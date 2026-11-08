using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Diwali.Editor
{
    [InitializeOnLoad]
    public static class DiwaliRocketReview
    {
        const string Pending = "Diwali.RocketReview";
        static DiwaliRocketReview() => EditorApplication.update += Pump;

        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before rocket review.");
            DiwaliLinkSetup.UseDesktopForNextPlay();
            EditorSceneManager.OpenScene(DiwaliSceneBuilder.MRScenePath);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        static void Pump()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || !DiwaliSession.Instance) return;
            SessionState.SetBool(Pending, false);
            DiwaliSession.Instance.StartCoroutine(Review());
        }

        static IEnumerator Review()
        {
            yield return new WaitForSeconds(1);
            var s = DiwaliSession.Instance;
            s.Begin();
            s.rig.enabled = false;
            var rocket = (RocketItem)s.placement.PlaceAt(CrackerId.Rocket, new Vector3(0, 0, 2));
            rocket.fuseSeconds = 0.15f;
            Vector3 start = rocket.body.position, last = start;
            Vector3 right = Vector3.ProjectOnPlane(s.rig.view.transform.right, Vector3.up).normalized;
            bool descended = false, reversed = false;
            var trace = new System.Text.StringBuilder("seconds,height,rightward\n");
            rocket.Ignite();
            float elapsed = 0;
            while (rocket && rocket.body.gameObject.activeSelf && elapsed < 7)
            {
                elapsed += Time.deltaTime;
                Vector3 point = rocket.body.position;
                descended |= point.y < last.y - 0.0001f;
                reversed |= Vector3.Dot(point - last, right) < -0.0001f;
                trace.AppendLine(FormattableString.Invariant($"{elapsed:F4},{point.y-start.y:F4},{Vector3.Dot(point-start,right):F4}"));
                last = point;
                yield return null;
            }
            var flight = RocketItem.LastFlight;
            bool passed = !descended && !reversed && flight.apexHeight > 17 && flight.apexHeight < 19 && flight.rightwardDistance > 1 &&
                flight.rightwardDistance < 5 && flight.climbRateAtBurst > 8.5f && !flight.sideSpeedGrewAfterBurnout;
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/rocket-ascent.csv", trace.ToString());
            File.WriteAllText("Verification/rocket-review.txt", FormattableString.Invariant(
                $"{(passed ? "PASS" : "FAIL")} upward rocket review\nHeight: {flight.apexHeight:F2} m\nRightward: {flight.rightwardDistance:F2} m\nClimb at burst: {flight.climbRateAtBurst:F2} m/s\nDescended: {descended}\nReversed sideways: {reversed}\nSteered after burnout: {flight.sideSpeedGrewAfterBurnout}\n"));
            Debug.Log("[Diwali] Rocket review " + (passed ? "passed" : "FAILED"));
            EditorApplication.isPlaying = false;
        }
    }
}
