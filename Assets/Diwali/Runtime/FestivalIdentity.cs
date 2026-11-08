namespace Diwali
{
    /// <summary>One identity shared by the launcher, splash screens and activity introductions.</summary>
    public static class FestivalIdentity
    {
        public const string AppName = "Diwali Utsav";
        public const string Tagline = "A little light. A whole celebration.";

        public static string ExperienceName(CrackerId id)
        {
            switch (id)
            {
                case CrackerId.Lighter: return "The First Flame";
                case CrackerId.Diya: return "Diya Glow";
                case CrackerId.Phuljhadi: return "Phuljhadi Trails";
                case CrackerId.Rangoli: return "Rangoli Colours";
                case CrackerId.Anar: return "Anar Garden";
                case CrackerId.FlowerPot: return "Flower Pot Lights";
                case CrackerId.Chakri: return "Chakri Dance";
                case CrackerId.Ladi: return "Little Ladi";
                case CrackerId.BigLadi: return "Ladi Celebration";
                case CrackerId.Bomb: return "Diwali Thunder";
                case CrackerId.Caps: return "Little Pops";
                case CrackerId.Rocket: return "Rocket Rise";
                case CrackerId.SkyShot: return "Sky Blossom";
                case CrackerId.TwelveShot: return "Twelve Wishes";
                case CrackerId.FifteenShot: return "Fifteen Stars";
                case CrackerId.GrandShow: return "The Grand Utsav";
                case CrackerId.Butterfly: return "Butterfly Flight";
                case CrackerId.Popper: return "Paper Pop";
                default: return AppName;
            }
        }
    }
}
