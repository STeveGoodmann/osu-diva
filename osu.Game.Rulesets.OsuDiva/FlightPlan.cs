using osuTK;

namespace osu.Game.Rulesets.DivaFlight
{
    /// <summary>
    /// How notes fly: everything that decides a flight line besides the note itself.
    /// </summary>
    public readonly struct FlightPlan
    {
        public float Speed { get; init; }
        public OsuDivaDirection Direction { get; init; }
        public float FanAngle { get; init; }
        public ScreenBounds Screen { get; init; }

        /// <summary>The direction mode flight direction for a note at <paramref name="x"/>, plus an optional extra angle in degrees.</summary>
        public Vector2 DirectionOf(float x, float offset = 0)
            => OsuDivaMath.Direction(x, Screen, Direction, FanAngle, offset);
    }
}
