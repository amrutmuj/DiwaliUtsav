using UnityEngine;
using UnityEngine.UI;

namespace Diwali
{
    /// <summary>The grab bar under the thali. Pinch it with either hand to move the tray wherever you want it.</summary>
    public sealed class ThaliHandle : MonoBehaviour, IFestivalTarget
    {
        public ThaliMenu menu;
        public Image bar;
        public Color idle = new Color(1, 1, 1, 0.34f);
        public Color hover = new Color(1, 0.8f, 0.46f, 0.95f);

        public void Hover(bool on)
        {
            bar.color = on ? hover : idle;
            if (on) menu.SetHover(null, "Pinch and hold to move the tray");
            else menu.ClearHover(null);
        }

        public void Press(FestivalPointer pointer) => menu.BeginMove(pointer);
    }
}
