using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.DivaFlight.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.DivaFlight.UnitTests
{
    [TestFixture]
    public class FlightPartTests
    {
        private static T applied<T>(T o) where T : OsuHitObject
        {
            o.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return o;
        }

        private static List<OsuHitObject> sampleMap()
        {
            var circle = applied(new HitCircle { StartTime = 1000, Position = new Vector2(256, 192), NewCombo = true });

            var slider = applied(new Slider
            {
                StartTime = 2000,
                Position = new Vector2(100, 100),
                RepeatCount = 1,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(200, 0) }, 200),
            });

            var spinner = applied(new Spinner { StartTime = 5000, Duration = 2000, Position = new Vector2(256, 192) });

            return new List<OsuHitObject> { circle, slider, spinner };
        }

        [Test]
        public void BuildsOnePartPerPlayableThing()
        {
            var parts = FlightPartBuilder.Build(sampleMap(), FlightSettings.Default);

            Assert.That(parts.Count(p => p.Kind == FlightPartKind.Circle), Is.EqualTo(1));
            Assert.That(parts.Count(p => p.Kind == FlightPartKind.SliderHead), Is.EqualTo(1));
            Assert.That(parts.Count(p => p.Kind == FlightPartKind.SliderBody), Is.EqualTo(1));
            Assert.That(parts.Count(p => p.Kind == FlightPartKind.Spinner), Is.EqualTo(1));
        }

        [Test]
        public void SliderOnlyFliesItsHeadInAndHasAGuideBody()
        {
            var slider = (Slider)sampleMap()[1];
            Assert.That(slider.NestedHitObjects.OfType<SliderRepeat>().Count(), Is.EqualTo(1), "the sample slider does have a repeat");

            var parts = FlightPartBuilder.Build(new OsuHitObject[] { slider }, FlightSettings.Default);

            Assert.That(parts.Select(p => p.Kind), Is.EquivalentTo(new[]
            {
                FlightPartKind.SliderHead, FlightPartKind.SliderBody,
            }), "no note for the repeat (turn) or the tail");
            Assert.That(parts.Where(p => p.IsFlying).Select(p => p.Time), Is.All.EqualTo(slider.StartTime));
        }

        [Test]
        public void SpinnerHoldsUntilItEnds()
        {
            var spinner = FlightPartBuilder.Build(sampleMap(), FlightSettings.Default).Single(p => p.Kind == FlightPartKind.Spinner);
            Assert.That(spinner.Time, Is.EqualTo(5000));
            Assert.That(spinner.HoldUntil, Is.EqualTo(7000));
            Assert.That(spinner.Target, Is.EqualTo(new Vector2(256, 192)));
            Assert.That(spinner.Number, Is.EqualTo(0));
        }

        [Test]
        public void EveryNoteIsVisibleBeforeItArrives()
        {
            foreach (var part in FlightPartBuilder.Build(sampleMap(), FlightSettings.Default).Where(p => p.IsFlying))
                Assert.That(part.VisibleFrom, Is.LessThan(part.Time), part.Kind.ToString());
        }

        [Test]
        public void NumbersOnlyWhenEnabledAndCountWithinTheCombo()
        {
            var objects = new List<OsuHitObject>
            {
                applied(new HitCircle { StartTime = 1000, Position = new Vector2(50, 50), NewCombo = true }),
                applied(new HitCircle { StartTime = 1200, Position = new Vector2(300, 50) }),
            };

            // IndexInCurrentCombo is assigned by the beatmap processor; set it as the processor would.
            objects[0].IndexInCurrentCombo = 0;
            objects[1].IndexInCurrentCombo = 1;

            Assert.That(FlightPartBuilder.Build(objects, FlightSettings.Default).Select(p => p.Number), Is.All.EqualTo(0));

            var parts = FlightPartBuilder.Build(objects, withChanges(s => s.ShowNumbers = true));
            Assert.That(parts.Select(p => p.Number), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void NoSliderBodyWhenDisabled()
        {
            var parts = FlightPartBuilder.Build(sampleMap(), withChanges(s => s.ShowSliderBody = false));
            Assert.That(parts.Any(p => p.Kind == FlightPartKind.SliderBody), Is.False);
        }

        [Test]
        public void NoteScaleScalesNoteSizeFromTwentyToTwoHundredPercent()
        {
            float normal = FlightPartBuilder.Build(sampleMap(), FlightSettings.Default).First(p => p.Kind == FlightPartKind.Circle).Diameter;
            float small = FlightPartBuilder.Build(sampleMap(), withChanges(s => s.NoteScale = 0.2f)).First(p => p.Kind == FlightPartKind.Circle).Diameter;
            float large = FlightPartBuilder.Build(sampleMap(), withChanges(s => s.NoteScale = 2f)).First(p => p.Kind == FlightPartKind.Circle).Diameter;

            Assert.That(small, Is.EqualTo(normal * 0.2f).Within(1e-3));
            Assert.That(large, Is.EqualTo(normal * 2f).Within(1e-3));
        }

        [Test]
        public void ScreenSizeChangesTheFanAndTheEntryTime()
        {
            var circle = new List<OsuHitObject> { applied(new HitCircle { StartTime = 5000, Position = new Vector2(500, 100), NewCombo = true }) };
            circle[0].IndexInCurrentCombo = 0;

            var narrow = FlightPartBuilder.Build(circle, withChanges(s => s.Screen = new ScreenBounds(-64, -48, 576, 432))).Single();
            var wide = FlightPartBuilder.Build(circle, withChanges(s => s.Screen = new ScreenBounds(-342, -48, 854, 432))).Single();

            // the same playfield position is closer to the edge of a narrow screen, so its fan angle is larger.
            Assert.That(narrow.Direction.X, Is.GreaterThan(wide.Direction.X));
            Assert.That(narrow.VisibleFrom, Is.Not.EqualTo(wide.VisibleFrom));
        }

        private static FlightSettings withChanges(System.Action<Box> change)
        {
            var d = FlightSettings.Default;
            var box = new Box
            {
                Speed = d.Speed, NoteScale = d.NoteScale, Direction = d.Direction, FanAngle = d.FanAngle,
                ShowNumbers = d.ShowNumbers, ShowSliderBody = d.ShowSliderBody, Screen = d.Screen,
                Optimise = d.Optimise, MinVisibleMs = d.MinVisibleMs,
            };
            change(box);

            return new FlightSettings
            {
                Speed = box.Speed, NoteScale = box.NoteScale, Direction = box.Direction, FanAngle = box.FanAngle,
                ShowNumbers = box.ShowNumbers, ShowSliderBody = box.ShowSliderBody, Screen = box.Screen,
                Optimise = box.Optimise, MinVisibleMs = box.MinVisibleMs,
            };
        }

        /// <summary>FlightSettings is an init-only struct; this mutable copy lets tests change one field.</summary>
        private class Box
        {
            public float Speed, NoteScale, FanAngle;
            public DivaFlightDirection Direction;
            public bool ShowNumbers, ShowSliderBody, Optimise;
            public float MinVisibleMs;
            public ScreenBounds Screen;
        }
    }
}
