using System;
using NUnit.Framework;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.OsuDiva.UnitTests
{
    /// <summary>Ports of test_fan.py, test_combo_fan.py, test_distance_color.py and test_target_color.py (screen 1280x720 as in the Python tests).</summary>
    [TestFixture]
    public class OsuDivaMathTests
    {
        private static readonly ScreenBounds screen = new ScreenBounds(0, 0, 1280, 720);

        [TestCase(200f)]
        [TestCase(640f)]
        [TestCase(1080f)]
        public void FanFliesAtConstantSpeedAndArrivesOnTarget(float x)
        {
            var direction = OsuDivaMath.Direction(x, screen, OsuDivaDirection.Fan, 25);

            var offset = OsuDivaMath.FlightOffset(direction, 500, 500);
            Assert.That(offset.Length, Is.EqualTo(250).Within(1e-3));
            Assert.That(offset.X < 0, Is.EqualTo(x < 640), "notes left of the centre start further left");

            Assert.That(OsuDivaMath.FlightOffset(direction, 0, 500), Is.EqualTo(Vector2.Zero));
            Assert.That(OsuDivaMath.FlightOffset(direction, -30, 500), Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void FanAngleIsMeasuredFromTheScreenCentreAndReachesTheMaximumAtTheEdge()
        {
            var centre = OsuDivaMath.Direction(640, screen, OsuDivaDirection.Fan, 25);
            Assert.That(centre.X, Is.EqualTo(0).Within(1e-6));

            var edge = OsuDivaMath.Direction(1280, screen, OsuDivaDirection.Fan, 25);
            Assert.That(edge.X, Is.EqualTo(Math.Sin(25 * Math.PI / 180)).Within(1e-5));

            var half = OsuDivaMath.Direction(960, screen, OsuDivaDirection.Fan, 25);
            Assert.That(half.X, Is.EqualTo(Math.Sin(12.5 * Math.PI / 180)).Within(1e-5));
        }

        [Test]
        public void ZeroAngleAndVerticalModeFlyStraightDown()
        {
            var zero = OsuDivaMath.FlightOffset(OsuDivaMath.Direction(100, screen, OsuDivaDirection.Fan, 0), 500, 500);
            Assert.That(zero.X, Is.EqualTo(0).Within(1e-4));
            Assert.That(zero.Y, Is.EqualTo(-250).Within(1e-4));

            var vertical = OsuDivaMath.FlightOffset(OsuDivaMath.Direction(100, screen, OsuDivaDirection.Vertical, 45), 500, 500);
            Assert.That(vertical.X, Is.EqualTo(0).Within(1e-4));
        }

        [Test]
        public void SpreadOffsetIsAddedAndClamped()
        {
            var d = OsuDivaMath.Direction(640, screen, OsuDivaDirection.Fan, 25, 30);
            Assert.That(d.X, Is.EqualTo(Math.Sin(30 * Math.PI / 180)).Within(1e-5));

            var clamped = OsuDivaMath.Direction(640, screen, OsuDivaDirection.Fan, 25, 500);
            Assert.That(clamped.X, Is.EqualTo(Math.Sin(75 * Math.PI / 180)).Within(1e-5));
        }

        [Test]
        public void AbsoluteAnglesMapToTheFourSides()
        {
            Assert.That(OsuDivaMath.DirectionFromAngle(0).Y, Is.EqualTo(1).Within(1e-6), "0: from above");
            Assert.That(OsuDivaMath.DirectionFromAngle(90).X, Is.EqualTo(1).Within(1e-6), "90: from the right");
            Assert.That(OsuDivaMath.DirectionFromAngle(180).Y, Is.EqualTo(-1).Within(1e-6), "180: from below");
            Assert.That(OsuDivaMath.DirectionFromAngle(-90).X, Is.EqualTo(-1).Within(1e-6), "-90: from the left");

            foreach (float angle in new[] { -170f, -90f, -30f, 0f, 45f, 90f, 135f, 180f })
                Assert.That(OsuDivaMath.AngleDifference(OsuDivaMath.AngleOf(OsuDivaMath.DirectionFromAngle(angle)), angle), Is.LessThan(1e-3f));

            Assert.That(OsuDivaMath.AngleDifference(170, -170), Is.EqualTo(20).Within(1e-4), "the short way round");
        }

        [Test]
        public void EnteringFromBelowFliesAtConstantSpeedFromBelowTheTarget()
        {
            var offset = OsuDivaMath.FlightOffset(OsuDivaMath.DirectionFromAngle(160), 500, 500);
            Assert.That(offset.Y, Is.GreaterThan(0), "starts below the target");
            Assert.That(offset.Length, Is.EqualTo(250).Within(1e-3));
        }

        [Test]
        public void EntryTimeMatchesTheHandComputedCase()
        {
            // test_spread.py: flight_entry_time(100, 300, 1000, 0, 1, 500, 20, 800, 600) == 360
            double t = OsuDivaMath.EntryTime(100, 300, 1000, new Vector2(0, 1), 500, 20, new ScreenBounds(0, 0, 800, 600));
            Assert.That(t, Is.EqualTo(360).Within(1e-6));
        }

        [Test]
        public void EntryTimeUsesTheBottomEdgeForNotesEnteringFromBelow()
        {
            // straight up from the bottom: distance to leave the bottom edge is (600 + 20) - 300 = 320 units.
            double t = OsuDivaMath.EntryTime(100, 300, 1000, new Vector2(0, -1), 500, 20, new ScreenBounds(0, 0, 800, 600));
            Assert.That(t, Is.EqualTo(1000 - 320 / 500.0 * 1000).Within(1e-6));
        }

        [Test]
        public void ColourProgressRunsFromZeroToOne()
        {
            Assert.That(OsuDivaMath.ColourProgress(0), Is.EqualTo(1).Within(1e-9));
            Assert.That(OsuDivaMath.ColourProgress(100000), Is.EqualTo(0).Within(1e-9));
            Assert.That(OsuDivaMath.ColourProgress(200), Is.GreaterThan(OsuDivaMath.ColourProgress(800)));
        }

        [Test]
        public void ColourChangesFasterNearArrival()
        {
            double near = OsuDivaMath.ColourProgress(0) - OsuDivaMath.ColourProgress(10);
            double far = OsuDivaMath.ColourProgress(500) - OsuDivaMath.ColourProgress(510);
            Assert.That(near, Is.GreaterThan(far));
            Assert.That(OsuDivaMath.ColourProgress(-10), Is.EqualTo(OsuDivaMath.ColourProgress(0)));
        }

        [Test]
        public void ArrivalColourIsNearWhiteAndFarColourIsDark()
        {
            var combo = new Color4(1f, 0.357f, 0.271f, 1f);
            var near = OsuDivaMath.DistanceColour(0, combo);
            var far = OsuDivaMath.DistanceColour(5000, combo);
            Assert.That(near.R, Is.GreaterThan(0.93f));
            Assert.That(near.G, Is.GreaterThan(0.7f));
            Assert.That(far.R, Is.LessThan(0.4f));
        }

        [Test]
        public void ColourFollowsTheTargetHue()
        {
            var red = OsuDivaMath.DistanceColour(300, new Color4(1, 0, 0, 1));
            var blue = OsuDivaMath.DistanceColour(300, new Color4(0, 0, 1, 1));
            Assert.That(red.R, Is.GreaterThan(red.B));
            Assert.That(blue.B, Is.GreaterThan(blue.R));

            var far = OsuDivaMath.DistanceColour(2400, new Color4(1, 0, 0, 1));
            var near = OsuDivaMath.DistanceColour(0, new Color4(1, 0, 0, 1));
            Assert.That(far.R + far.G + far.B, Is.LessThan(near.R + near.G + near.B));
            Assert.That(near.R, Is.GreaterThan(near.G));
        }
    }
}
