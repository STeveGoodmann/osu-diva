using System;
using osu.Framework.Localisation;
using osu.Game.Rulesets.DivaFlight.Localisation;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.DivaFlight
{
    public enum DivaFlightDirection
    {
        /// <summary>Straight down (plus any per-note spread angle).</summary>
        [LocalisableDescription(typeof(DivaStrings), nameof(DivaStrings.DirectionVertical))]
        Vertical,

        /// <summary>Angle grows with the note's horizontal distance from the screen centre.</summary>
        [LocalisableDescription(typeof(DivaStrings), nameof(DivaStrings.DirectionFan))]
        Fan,
    }

    /// <summary>
    /// The visible screen in playfield units (the 512x384 osu! area is centred in it).
    /// Used to decide where notes enter and how wide the fan is.
    /// </summary>
    public readonly struct ScreenBounds : IEquatable<ScreenBounds>
    {
        public readonly float Left;
        public readonly float Top;
        public readonly float Right;
        public readonly float Bottom;

        public ScreenBounds(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public float Width => Right - Left;
        public float Height => Bottom - Top;
        public float CenterX => (Left + Right) / 2;

        /// <summary>
        /// A 16:9 screen with the playfield at 80% of the screen height, centred. Used until the real size is known.
        /// </summary>
        public static ScreenBounds Default => new ScreenBounds(-171, -48, 683, 432);

        public bool Equals(ScreenBounds other) => Math.Abs(Left - other.Left) < 0.5f && Math.Abs(Top - other.Top) < 0.5f
                                                  && Math.Abs(Right - other.Right) < 0.5f && Math.Abs(Bottom - other.Bottom) < 0.5f;

        public override bool Equals(object? obj) => obj is ScreenBounds other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
    }

    /// <summary>
    /// Flight path and colour maths, ported from native_core.py
    /// (diva_direction, diva_position, flight_entry_time, diva_distance_color).
    /// All lengths are osu! playfield units (512x384, y grows downward).
    /// </summary>
    public static class DivaFlightMath
    {
        /// <summary>
        /// Screen pixels per playfield unit that the Python distance-colour curve was tuned for (about 1080p).
        /// </summary>
        public const float COLOUR_DISTANCE_SCALE = 2.25f;

        public const float MAX_OFFSET_DEGREES = 75;

        /// <summary>
        /// Unit vector (dx, dy) exactly as in the Python prototype: a note at distance d from its target sits at
        /// (x + dx * d, y - dy * d), so dy is positive for notes entering from above.
        /// The fan angle grows linearly with the horizontal offset from the screen centre, up to <paramref name="maxAngleDegrees"/>,
        /// plus an optional extra <paramref name="offsetDegrees"/>.
        /// </summary>
        public static Vector2 Direction(float x, ScreenBounds screen, DivaFlightDirection mode, float maxAngleDegrees, float offsetDegrees = 0)
        {
            float angleDegrees = 0;

            if (mode == DivaFlightDirection.Fan && screen.Width > 0)
                angleDegrees = maxAngleDegrees * Math.Clamp((x - screen.CenterX) / (screen.Width / 2), -1f, 1f);

            double angle = Math.Clamp(angleDegrees + offsetDegrees, -MAX_OFFSET_DEGREES, MAX_OFFSET_DEGREES) * Math.PI / 180;
            return new Vector2((float)Math.Sin(angle), (float)Math.Cos(angle));
        }

        /// <summary>
        /// The same (dx, dy) vector for an absolute entry angle in degrees: 0 enters from above, 90 from the right, 180 from below, -90 from the left.
        /// </summary>
        public static Vector2 DirectionFromAngle(float degrees)
        {
            double angle = degrees * Math.PI / 180;
            return new Vector2((float)Math.Sin(angle), (float)Math.Cos(angle));
        }

        /// <summary>The absolute entry angle in degrees (-180..180] of a (dx, dy) vector, the inverse of <see cref="DirectionFromAngle"/>.</summary>
        public static float AngleOf(Vector2 direction) => (float)(Math.Atan2(direction.X, direction.Y) * 180 / Math.PI);

        /// <summary>Smallest absolute difference between two angles, 0..180 degrees.</summary>
        public static float AngleDifference(float a, float b)
        {
            float d = Math.Abs(a - b) % 360;
            return d > 180 ? 360 - d : d;
        }

        /// <summary>
        /// Offset of the flying note from its target for a given <see cref="Direction"/> result, in playfield units. Zero at and after the hit time.
        /// </summary>
        public static Vector2 FlightOffset(Vector2 direction, double remainingMs, float speed)
        {
            if (remainingMs <= 0)
                return Vector2.Zero;

            float distance = (float)(speed * remainingMs / 1000);
            return new Vector2(direction.X * distance, -direction.Y * distance);
        }

        /// <summary>
        /// Time at which a note first enters the screen, given its direction. Mirrors flight_entry_time.
        /// </summary>
        public static double EntryTime(float x, float y, double hitTime, Vector2 direction, float speed, float size, ScreenBounds screen)
        {
            float best = float.PositiveInfinity;

            if (direction.X > 0)
                best = Math.Min(best, (screen.Right + size - x) / direction.X);
            else if (direction.X < 0)
                best = Math.Min(best, (screen.Left - size - x) / direction.X);

            if (direction.Y > 0)
                best = Math.Min(best, (y - screen.Top + size) / direction.Y);
            else if (direction.Y < 0)
                best = Math.Min(best, (y - screen.Bottom - size) / direction.Y);

            if (float.IsInfinity(best))
                best = 0;

            return hitTime - best * 1000.0 / speed;
        }

        public static double ColourProgress(double distance)
        {
            distance = Math.Clamp(distance / 2, 0, 1200);
            return 1 - Math.Log(1 + distance / 80) / Math.Log(1 + 1200.0 / 80);
        }

        /// <summary>
        /// Colour of the note while it flies: dim and desaturated far away, the combo colour on approach, near white on arrival.
        /// <paramref name="distance"/> is in screen pixels.
        /// </summary>
        public static Color4 DistanceColour(double distance, Color4 target)
        {
            double progress = ColourProgress(distance);
            rgbToHsv(target, out double hue, out double saturation, out double value);

            (double at, Color4 colour)[] stops =
            {
                (0, hsvToRgb(hue, saturation * .25, value * .32)),
                (.65, hsvToRgb(hue, saturation, value)),
                (1, hsvToRgb(hue, saturation * .22, Math.Max(.94, value))),
            };

            for (int i = 0; i < stops.Length - 1; i++)
            {
                var (a, start) = stops[i];
                var (b, end) = stops[i + 1];

                if (progress <= b)
                {
                    float f = (float)((progress - a) / (b - a));
                    return new Color4(
                        start.R + (end.R - start.R) * f,
                        start.G + (end.G - start.G) * f,
                        start.B + (end.B - start.B) * f,
                        1f);
                }
            }

            return stops[^1].colour;
        }

        private static void rgbToHsv(Color4 c, out double h, out double s, out double v)
        {
            double max = Math.Max(c.R, Math.Max(c.G, c.B));
            double min = Math.Min(c.R, Math.Min(c.G, c.B));
            double d = max - min;
            v = max;
            s = max <= 0 ? 0 : d / max;

            if (d <= 0)
                h = 0;
            else if (max == c.R)
                h = (((c.G - c.B) / d) % 6 + 6) % 6 / 6;
            else if (max == c.G)
                h = ((c.B - c.R) / d + 2) / 6;
            else
                h = ((c.R - c.G) / d + 4) / 6;
        }

        private static Color4 hsvToRgb(double h, double s, double v)
        {
            double h6 = (h - Math.Floor(h)) * 6;
            int i = (int)Math.Floor(h6);
            double f = h6 - i;
            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);

            (double r, double g, double b) = (i % 6) switch
            {
                0 => (v, t, p),
                1 => (q, v, p),
                2 => (p, v, t),
                3 => (p, q, v),
                4 => (t, p, v),
                _ => (v, p, q),
            };

            return new Color4((float)r, (float)g, (float)b, 1f);
        }
    }
}
