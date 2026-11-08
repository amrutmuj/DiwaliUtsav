using UnityEngine;
using UnityEngine.UI;

namespace Diwali
{
    public sealed class FestivalRemoveButton : MonoBehaviour, IFestivalTarget
    {
        public FestivalRemoval owner;
        public void Hover(bool on) => GetComponent<Image>().color = on
            ? new Color(.65f, .16f, .08f, 1) : new Color(.3f, .075f, .055f, .98f);
        public void Press(FestivalPointer pointer)
        {
            if (pointer != null && pointer.Grasp == null && pointer.PressedNow) owner.Remove();
        }
    }
}
