using UnityEngine;

namespace Diwali
{
    /// <summary>A deliberate pinch on the fuse lights it; grabbing the body always picks it up.</summary>
    public sealed class FestivalFuseTarget : MonoBehaviour, IFestivalTarget
    {
        public FestivalItem item;
        public void Hover(bool on) => item.Hover(on);
        public void Press(FestivalPointer pointer)
        {
            if (pointer.PressedNow && item.Carrier == null) item.Ignite();
        }
    }
}
