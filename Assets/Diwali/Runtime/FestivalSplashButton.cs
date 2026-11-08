using UnityEngine;
using UnityEngine.UI;

namespace Diwali
{
    public sealed class FestivalSplashButton : MonoBehaviour, IFestivalTarget
    {
        public FestivalSplash splash;
        public Image background;
        public Color normal = new Color(1, .73f, .35f);
        public Color hover = new Color(1, .88f, .64f);

        public void Hover(bool on)
        {
            if (background) background.color = on ? hover : normal;
            if (splash && splash.art) splash.art.hovered = on;
        }

        public void Press(FestivalPointer pointer)
        {
            if (!splash || splash.Transitioning) return;
            splash.Continue(pointer);
        }
    }
}
