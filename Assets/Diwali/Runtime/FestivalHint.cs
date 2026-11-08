using TMPro;
using UnityEngine;

namespace Diwali
{
    /// <summary>
    /// A short label beside the thing it describes, shown the first time only. Guidance lives on the
    /// object rather than on a panel, so nobody has to read somewhere else and then look back.
    /// </summary>
    public sealed class FestivalHint : MonoBehaviour
    {
        public TMP_Text label;
        public float height = 0.14f;
        public float seconds = 9;

        Transform target;
        float hideAt, alpha;

        void Awake() => label.alpha = 0;

        public void Show(Transform near, string line)
        {
            target = near;
            label.text = line;
            hideAt = Time.time + seconds;
        }

        public void Hide() => hideAt = 0;
        public void HideInstant() { hideAt = alpha = 0; label.alpha = 0; }

        void LateUpdate()
        {
            bool visible = target && Time.time < hideAt;
            alpha = Mathf.MoveTowards(alpha, visible ? 1 : 0, Time.deltaTime * 4);
            label.alpha = alpha;
            if (alpha <= 0 || !target) return;

            Transform head = DiwaliSession.Instance.rig.view.transform;
            transform.position = target.position + Vector3.up * height;
            transform.rotation = Quaternion.LookRotation(transform.position - head.position, Vector3.up);
        }
    }
}
