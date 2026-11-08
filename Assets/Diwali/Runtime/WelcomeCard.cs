using UnityEngine;

namespace Diwali
{
    /// <summary>Keeps the launch card at eye level, in front of wherever you are looking.</summary>
    public sealed class WelcomeCard : MonoBehaviour
    {
        public float distance = 1.1f;
        public float followAngle = 30;
        public FestivalRig rig;

        Vector3 target, velocity, facing;
        Quaternion targetRotation;
        bool placed;

        void OnEnable() => placed = false;

        void LateUpdate()
        {
            if (!rig || !rig.view) return;
            Transform head = rig.view.transform;
            Vector3 forward = head.forward;
            forward.y = 0;
            if (forward.sqrMagnitude < 0.02f) return;
            forward.Normalize();

            if (!placed || Vector3.Angle(facing, forward) > followAngle ||
                Vector3.Distance(head.position + facing * distance, target) > .45f)
            {
                facing = forward;
                target = head.position + forward * distance;
                targetRotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            if (!placed)
            {
                transform.SetPositionAndRotation(target, targetRotation);
                velocity = Vector3.zero;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, 0.35f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 10));
            }
            placed = true;
        }
    }
}
