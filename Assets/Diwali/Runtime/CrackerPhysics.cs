namespace Diwali
{
    /// <summary>
    /// How one kind of cracker behaves in the air and on the floor, from what it is made of.
    /// Every item leaves the hand through the same flight code; only these numbers differ.
    /// </summary>
    public readonly struct CrackerPhysics
    {
        /// <summary>Leaves the hand with its real velocity. Everything else is set down on the aimed ring.</summary>
        public readonly bool throwable;
        /// <summary>Multiplies the time in the air when set down. Light things float, heavy things drop.</summary>
        public readonly float hang;
        /// <summary>Share of impact speed returned as a bounce.</summary>
        public readonly float bounce;
        /// <summary>Rocking on landing, in radians per second.</summary>
        public readonly float wobble;
        /// <summary>How quickly rocking and tumbling die away, per second.</summary>
        public readonly float spinLoss;
        /// <summary>Sliding friction on the floor, per second.</summary>
        public readonly float friction;
        /// <summary>
        /// Share of its speed the air takes each second in free flight. Drag that grew with speed would hold
        /// every hard throw near one top speed, so this stays linear: twice the throw is twice the speed.
        /// </summary>
        public readonly float airDrag;
        /// <summary>Loudness of the landing.</summary>
        public readonly float thud;
        /// <summary>Pitch of the landing. Light paper taps, heavy clay and card knock low.</summary>
        public readonly float knock;

        CrackerPhysics(float hang, float bounce, float wobble, float spinLoss, float friction, float airDrag, float thud, float knock, bool throwable = false)
        {
            this.hang = hang;
            this.bounce = bounce;
            this.wobble = wobble;
            this.spinLoss = spinLoss;
            this.friction = friction;
            this.airDrag = airDrag;
            this.thud = thud;
            this.knock = knock;
            this.throwable = throwable;
        }

        static readonly CrackerPhysics PaperPouch = new CrackerPhysics(1, 0.15f, 2, 7, 9, 0.3f, 0.3f, 1.35f, true);
        static readonly CrackerPhysics FiredClay = new CrackerPhysics(1, 0.05f, 1.5f, 14, 12, 0.01f, 1, 0.82f);
        static readonly CrackerPhysics PackedCone = new CrackerPhysics(1, 0.12f, 2.5f, 10, 10, 0.02f, 0.85f, 0.92f);
        static readonly CrackerPhysics CoiledDisc = new CrackerPhysics(1, 0.3f, 6, 5, 5, 0.03f, 0.5f, 1.12f);
        static readonly CrackerPhysics LimpString = new CrackerPhysics(1.1f, 0, 0, 16, 16, 0.08f, 0.35f, 1.2f);
        static readonly CrackerPhysics TwineBall = new CrackerPhysics(0.85f, 0.18f, 3, 8, 7, 0.01f, 1, 0.86f);
        static readonly CrackerPhysics PaperRing = new CrackerPhysics(1.3f, 0.1f, 1, 12, 12, 0.15f, 0.25f, 1.25f);
        static readonly CrackerPhysics PowderSheet = new CrackerPhysics(1.6f, 0, 0, 16, 16, 0.3f, 0.2f, 1.3f);
        static readonly CrackerPhysics LightCard = new CrackerPhysics(1.2f, 0.25f, 4, 7, 8, 0.1f, 0.3f, 1.15f);
        static readonly CrackerPhysics WoodStand = new CrackerPhysics(1, 0.08f, 2, 11, 12, 0.03f, 0.7f, 1);
        static readonly CrackerPhysics HeavyCarton = new CrackerPhysics(0.9f, 0.06f, 1, 14, 13, 0.02f, 1, 0.78f);

        public static CrackerPhysics For(CrackerId id)
        {
            switch (id)
            {
                case CrackerId.Popper: return PaperPouch;
                case CrackerId.Diya: return FiredClay;
                case CrackerId.Anar:
                case CrackerId.FlowerPot: return PackedCone;
                case CrackerId.Chakri: return CoiledDisc;
                case CrackerId.Ladi:
                case CrackerId.BigLadi: return LimpString;
                case CrackerId.Bomb: return TwineBall;
                case CrackerId.Caps: return PaperRing;
                case CrackerId.Rangoli: return PowderSheet;
                case CrackerId.Butterfly: return LightCard;
                case CrackerId.Rocket: return WoodStand;
                default: return HeavyCarton;
            }
        }
    }
}
