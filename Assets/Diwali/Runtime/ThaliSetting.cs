using UnityEngine;
using UnityEngine.UI;

namespace Diwali
{
    public enum SettingKind { Night, Gentle, Reset, Begin, Music }

    /// <summary>A small round control in the thali's side panel, or the single button on the welcome card.</summary>
    public sealed class ThaliSetting : MonoBehaviour, IFestivalTarget
    {
        public SettingKind kind;
        public ThaliMenu menu;
        public Image icon;
        public Image disc;
        public Color onColour = new Color(1, 0.76f, 0.34f);
        public Color offColour = new Color(1, 1, 1, 0.42f);
        public Color discColour = new Color(1, 1, 1, 0.06f);
        public Color discHover = new Color(1, 1, 1, 0.2f);
        [Tooltip("The sun and night toggle shows the setting it is on.")]
        public Sprite sunIcon, nightIcon;

        static DiwaliSession Session => DiwaliSession.Instance;

        void OnEnable() => Refresh();

        string Describe()
        {
            switch (kind)
            {
                case SettingKind.Night: return Session.ambience.Night ? "Night  ·  the room dims a little" : "Sun  ·  the room stays as it is";
                case SettingKind.Gentle: return Session.Gentle ? "Gentle mode is on  ·  quieter, softer flashes" : "Gentle mode is off  ·  full sound and light";
                case SettingKind.Reset: return "Clear everything and start again";
                case SettingKind.Music: return Session.music && Session.music.Muted ? "Background music is off" : "Background music is on  ·  kept low beneath activities";
                default: return "Begin";
            }
        }

        public void Hover(bool on)
        {
            if (disc) disc.color = on ? discHover : discColour;
            if (!menu) return;
            if (on) menu.SetHover(null, Describe());
            else menu.ClearHover(null);
        }

        public void Press(FestivalPointer pointer)
        {
            Session.sound.Play("lift", transform.position, 1, 0);
            switch (kind)
            {
                case SettingKind.Night: Session.ambience.Night = !Session.ambience.Night; break;
                case SettingKind.Gentle: Session.SetGentle(!Session.Gentle); break;
                case SettingKind.Reset: Session.ResetAll(); return;
                case SettingKind.Begin: Session.Begin(); return;
                case SettingKind.Music: if (Session.music) Session.music.ToggleMute(); break;
            }
            Refresh();
            if (menu) menu.SetHover(null, Describe());
        }

        void Refresh()
        {
            if (!icon || !Session) return;
            if (kind == SettingKind.Night)
            {
                icon.sprite = Session.ambience.Night ? nightIcon : sunIcon;
                icon.color = onColour;
                return;
            }
            bool on = kind == SettingKind.Music ? Session.music && !Session.music.Muted : kind == SettingKind.Gentle && Session.Gentle;
            icon.color = kind == SettingKind.Reset ? offColour : on ? onColour : offColour;
        }
    }
}
