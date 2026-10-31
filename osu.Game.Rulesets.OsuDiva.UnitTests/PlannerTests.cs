using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.OsuDiva.UI;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.OsuDiva.UnitTests
{
    /// <summary>The penalties and rewards of the trajectory planner.</summary>
    [TestFixture]
    public class PlannerTests
    {
        private static readonly FlightPlan plan = new FlightPlan
        {
            Speed = 400,
            Direction = OsuDivaDirection.Vertical,
            FanAngle = 25,
            Screen = ScreenBounds.Default,
        };

        private static readonly PlannerObstacle[] none = new PlannerObstacle[0];

        /// <summary>The default options with the combo rules switched off, so a test sees only the rule it is about.</summary>
        private static PlannerOptions options(Func<PlannerOptions, PlannerOptions>? change = null)
        {
            var o = PlannerOptions.Default;
            var plain = new PlannerOptions
            {
                MinVisibleMs = o.MinVisibleMs, ShortWeight = o.ShortWeight, ThroughWeight = o.ThroughWeight, ThroughFullMs = o.ThroughFullMs,
                OverlapFactor = o.OverlapFactor, CrossWeight = o.CrossWeight, OrderWeight = o.OrderWeight, MoveWeight = o.MoveWeight,
                MaxAngleDifference = 60, AdjacentFullDistance = 0, AdjacentFullIntervalMs = 0, DivergenceWeight = 0,
                AngleDifferenceWeight = 0, AxisAlignmentWeight = 0, FreeDirection = o.FreeDirection, MaxPasses = o.MaxPasses,
            };
            return change?.Invoke(plain) ?? plain;
        }

        private static PlannerOptions with(PlannerOptions o, float? minVisibleMs = null, float? moveWeight = null, float? maxAngleDifference = null,
                                           float? angleDifferenceWeight = null, float? axisWeight = null, bool? free = null, float? adjacentDistance = null,
                                           float? adjacentInterval = null, float? divergence = null)
            => new PlannerOptions
            {
                MinVisibleMs = minVisibleMs ?? o.MinVisibleMs, ShortWeight = o.ShortWeight, ThroughWeight = o.ThroughWeight, ThroughFullMs = o.ThroughFullMs,
                OverlapFactor = o.OverlapFactor, CrossWeight = o.CrossWeight, OrderWeight = o.OrderWeight, MoveWeight = moveWeight ?? o.MoveWeight,
                MaxAngleDifference = maxAngleDifference ?? o.MaxAngleDifference, AdjacentFullDistance = adjacentDistance ?? o.AdjacentFullDistance,
                AdjacentFullIntervalMs = adjacentInterval ?? o.AdjacentFullIntervalMs, DivergenceWeight = divergence ?? o.DivergenceWeight, AngleDifferenceWeight = angleDifferenceWeight ?? o.AngleDifferenceWeight,
                AxisAlignmentWeight = axisWeight ?? o.AxisAlignmentWeight, FreeDirection = free ?? o.FreeDirection, MaxPasses = o.MaxPasses,
            };

        private static PlannerNote note(float x, float y, double time, bool startsCombo = true)
            => new PlannerNote { Target = new Vector2(x, y), Time = time, Radius = 30, StartsCombo = startsCombo };

        private static PlannerObstacle circle(int owner, float x, float y, double hitTime, double preempt = 800)
            => new PlannerObstacle { Owner = owner, Center = new Vector2(x, y), Radius = 30, From = hitTime - preempt, To = hitTime + 150 };

        private static float[] angles(PlannerNote[] notes, params float[] offsets) => OsuDivaPlanner.AnglesFromOffsets(notes, plan, offsets);

        [Test]
        public void ShortTimeOnScreenIsPenalisedAndRepaired()
        {
            // a note at the very top of the playfield flying straight down is on screen for well under 600 ms.
            var notes = new[] { note(256, 10, 3000) };
            var start = angles(notes, 0);
            var opt = options();

            var before = OsuDivaPlanner.Evaluate(notes, none, plan, start, start, opt);
            Assert.That(before.Short, Is.GreaterThan(0));

            var refined = OsuDivaPlanner.Refine(notes, none, plan, start, opt);
            var after = OsuDivaPlanner.Evaluate(notes, none, plan, refined, start, opt);

            Assert.That(OsuDivaMath.AngleDifference(refined[0], start[0]), Is.GreaterThan(0), "a slanted flight crosses more of the screen");
            Assert.That(after.Short, Is.LessThan(before.Short));
            Assert.That(after.Total, Is.LessThan(before.Total));
        }

        [Test]
        public void NoShortPenaltyWhenTheFlightIsLongEnough()
        {
            var start = angles(new[] { note(256, 350, 3000) }, 0);
            var cost = OsuDivaPlanner.Evaluate(new[] { note(256, 350, 3000) }, none, plan, start, start, options());
            Assert.That(cost.Short, Is.EqualTo(0));
        }

        [Test]
        public void FlyingThroughAnotherVisibleObjectIsPenalisedAndAvoided()
        {
            // note 1 sits at (256, 120) from 1200 ms until just after 2000 ms.
            // note 2 lands directly below it at 2100 ms and, flying straight down, passes through note 1 at about 1775 ms.
            var notes = new[] { note(256, 120, 2000), note(256, 250, 2100) };
            var obstacles = new[] { circle(0, 256, 120, 2000), circle(1, 256, 250, 2100) };
            var start = angles(notes, 0, 0);
            var opt = options();

            var before = OsuDivaPlanner.Evaluate(notes, obstacles, plan, start, start, opt);
            Assert.That(before.Through, Is.GreaterThan(0));

            var refined = OsuDivaPlanner.Refine(notes, obstacles, plan, start, opt);
            var after = OsuDivaPlanner.Evaluate(notes, obstacles, plan, refined, start, opt);

            Assert.That(OsuDivaMath.AngleDifference(refined[1], start[1]), Is.GreaterThan(0));
            Assert.That(after.Through, Is.EqualTo(0).Within(1e-9));
            Assert.That(after.Total, Is.LessThan(before.Total));
        }

        [Test]
        public void ObjectsThatAreNotOnScreenAtThatMomentAreNotObstacles()
        {
            // the same geometry, but note 1 is long gone (hit at 500 ms) when note 2 flies through its place.
            var notes = new[] { note(256, 120, 500), note(256, 250, 2100) };
            var obstacles = new[] { circle(0, 256, 120, 500), circle(1, 256, 250, 2100) };
            var start = angles(notes, 0, 0);

            Assert.That(OsuDivaPlanner.Evaluate(notes, obstacles, plan, start, start, options()).Through, Is.EqualTo(0));
        }

        [Test]
        public void OwnCircleIsNotAnObstacle()
        {
            var notes = new[] { note(256, 250, 2100) };
            var start = angles(notes, 0);
            Assert.That(OsuDivaPlanner.Evaluate(notes, new[] { circle(0, 256, 250, 2100) }, plan, start, start, options()).Through, Is.EqualTo(0));
        }

        [Test]
        public void CrossingFlightsArePenalisedAndUncrossed()
        {
            // two notes side by side, hit almost together. Angled towards each other they cross above the targets.
            var notes = new[] { note(100, 300, 2000), note(400, 300, 2050, startsCombo: false) };
            var start = angles(notes, 40, -40);
            var opt = options();

            var before = OsuDivaPlanner.Evaluate(notes, none, plan, start, start, opt);
            Assert.That(before.CrossingPairs, Is.EqualTo(1));
            Assert.That(before.Crossing, Is.EqualTo(opt.CrossWeight));

            var refined = OsuDivaPlanner.Refine(notes, none, plan, start, opt);
            var after = OsuDivaPlanner.Evaluate(notes, none, plan, refined, start, opt);

            Assert.That(after.CrossingPairs, Is.EqualTo(0));
            Assert.That(after.Total, Is.LessThan(before.Total));
        }

        [Test]
        public void ParallelFlightsDoNotCross()
        {
            var notes = new[] { note(100, 300, 2000), note(400, 300, 2050, startsCombo: false) };
            var start = angles(notes, 0, 0);
            Assert.That(OsuDivaPlanner.Evaluate(notes, none, plan, start, start, options()).CrossingPairs, Is.EqualTo(0));
        }

        [Test]
        public void FlightsThatAreNeverOnScreenTogetherDoNotCount()
        {
            // the same crossing angles, but the second note arrives 10 seconds later: they are never visible together.
            var notes = new[] { note(100, 300, 2000), note(400, 300, 12000, startsCombo: false) };
            var start = angles(notes, 40, -40);
            Assert.That(OsuDivaPlanner.Evaluate(notes, none, plan, start, start, options()).CrossingPairs, Is.EqualTo(0));
        }

        [Test]
        public void ACleanPlanIsLeftAlone()
        {
            var notes = new[] { note(100, 300, 2000), note(400, 300, 4000, startsCombo: false) };
            var start = angles(notes, 0, 12);
            Assert.That(OsuDivaPlanner.Refine(notes, none, plan, start, options()), Is.EqualTo(start));
        }

        [Test]
        public void RefinementNeverRaisesTheTotalCost()
        {
            var rng = new Random(7);
            var notes = Enumerable.Range(0, 60).Select(i => note(rng.Next(20, 490), rng.Next(10, 370), 1000 + i * 90, startsCombo: i % 4 == 0)).ToArray();
            var obstacles = notes.Select((n, i) => circle(i, n.Target.X, n.Target.Y, n.Time)).ToArray();
            var start = angles(notes, new float[notes.Length]);

            foreach (var opt in new[] { options(), PlannerOptions.Default })
            {
                var before = OsuDivaPlanner.Evaluate(notes, obstacles, plan, start, start, opt);
                var refined = OsuDivaPlanner.Refine(notes, obstacles, plan, start, opt);
                var after = OsuDivaPlanner.Evaluate(notes, obstacles, plan, refined, start, opt);

                Assert.That(after.Total, Is.LessThanOrEqualTo(before.Total + 1e-6));
                Assert.That(after.Total, Is.LessThan(before.Total), "a dense random map has something to fix");
            }
        }

        #region Combo rules: angle difference, axis alignment, free direction

        [Test]
        public void ComboNotesEnteringFurtherApartThanTheLimitArePenalised()
        {
            var notes = new[] { note(100, 300, 1000), note(200, 300, 1300, false), note(300, 300, 1600, false) };
            var opt = options(o => with(o, angleDifferenceWeight: 2, maxAngleDifference: 60));
            var zero = angles(notes, 0, 0, 0);

            // all within the limit: nothing to pay.
            Assert.That(OsuDivaPlanner.Evaluate(notes, none, plan, new[] { 0f, 30f, 50f }, zero, opt).AngleDifference, Is.EqualTo(0));

            // one note 120 degrees away from the other two: excess (120 - 60) / 60 = 1, squared 1, times weight 2 = 2 per pair,
            // shared out over the combo (2 other notes each) = 1 per pair, two such pairs = 2.
            var cost = OsuDivaPlanner.Evaluate(notes, none, plan, new[] { 0f, 0f, 120f }, zero, opt);
            Assert.That(cost.AngleDifference, Is.EqualTo(2).Within(1e-6));
        }

        [Test]
        public void TheLimitAndTheWeightAreParameters()
        {
            var notes = new[] { note(100, 300, 1000), note(300, 300, 1300, false) };
            var zero = angles(notes, 0, 0);
            var apart = new[] { 0f, 100f };

            Assert.That(OsuDivaPlanner.Evaluate(notes, none, plan, apart, zero, options(o => with(o, angleDifferenceWeight: 2, maxAngleDifference: 120))).AngleDifference, Is.EqualTo(0));
            Assert.That(OsuDivaPlanner.Evaluate(notes, none, plan, apart, zero, options(o => with(o, angleDifferenceWeight: 0, maxAngleDifference: 10))).AngleDifference, Is.EqualTo(0));

            double weak = OsuDivaPlanner.Evaluate(notes, none, plan, apart, zero, options(o => with(o, angleDifferenceWeight: 1, maxAngleDifference: 10))).AngleDifference;
            double strong = OsuDivaPlanner.Evaluate(notes, none, plan, apart, zero, options(o => with(o, angleDifferenceWeight: 3, maxAngleDifference: 10))).AngleDifference;
            Assert.That(strong, Is.EqualTo(weak * 3).Within(1e-9));
        }

        [Test]
        public void AngleDifferenceIsMeasuredTheShortWayRound()
        {
            var notes = new[] { note(100, 300, 1000), note(300, 300, 1300, false) };
            var zero = angles(notes, 0, 0);
            // 170 and -170 degrees are 20 apart, not 340.
            Assert.That(OsuDivaPlanner.Evaluate(notes, none, plan, new[] { 170f, -170f }, zero, options(o => with(o, angleDifferenceWeight: 2, maxAngleDifference: 60))).AngleDifference, Is.EqualTo(0));
        }

        [Test]
        public void TheOptimiserBringsAComboBackWithinTheLimit()
        {
            // two notes of a combo, far apart in space and time so nothing else interferes, starting 150 degrees apart.
            var notes = new[] { note(60, 300, 1000), note(450, 300, 2400, false) };
            var start = new[] { 0f, 150f };
            var opt = options(o => with(o, angleDifferenceWeight: 6, maxAngleDifference: 60, moveWeight: 0.3f));

            var before = OsuDivaPlanner.Evaluate(notes, none, plan, start, start, opt);
            var refined = OsuDivaPlanner.Refine(notes, none, plan, start, opt);
            var after = OsuDivaPlanner.Evaluate(notes, none, plan, refined, start, opt);

            Assert.That(before.AngleDifference, Is.GreaterThan(0));

            // the penalty is soft (it grows with the square of the excess), so the pair may stay a few degrees past the limit
            // when turning further costs more than it saves: but it must come most of the way back from 150 degrees.
            Assert.That(OsuDivaMath.AngleDifference(refined[0], refined[1]), Is.LessThan(70));
            Assert.That(after.AngleDifference, Is.LessThan(before.AngleDifference));
            Assert.That(after.Total, Is.LessThan(before.Total));
        }

        [Test]
        public void ACombosAverageDirectionOnAnAxisIsRewarded()
        {
            var notes = new[] { note(100, 300, 1000), note(300, 300, 1300, false) };
            var zero = angles(notes, 0, 0);
            var opt = options(o => with(o, axisWeight: 1));

            double Axis(params float[] a) => OsuDivaPlanner.Evaluate(notes, none, plan, a, zero, opt).AxisAlignment;

            Assert.That(Axis(0, 0), Is.EqualTo(-1).Within(1e-6), "both straight down: full reward");
            Assert.That(Axis(90, 90), Is.EqualTo(-1).Within(1e-6), "both from the right: horizontal, full reward");
            Assert.That(Axis(180, 180), Is.EqualTo(-1).Within(1e-6));
            Assert.That(Axis(-90, -90), Is.EqualTo(-1).Within(1e-6));
            Assert.That(Axis(45, 45), Is.EqualTo(0).Within(1e-6), "diagonal: no reward");
            Assert.That(Axis(22.5f, 22.5f), Is.EqualTo(-0.5).Within(1e-6), "half way between an axis and a diagonal");
            Assert.That(Axis(0, 180), Is.EqualTo(0).Within(1e-6), "opposite directions cancel out: the combo has no common direction");
            Assert.That(Axis(0, 60), Is.GreaterThan(Axis(0, 0)), "scattered directions earn less than agreeing ones");
            Assert.That(Axis(0, 60), Is.LessThan(0), "but the average of 0 and 60 degrees is 30 degrees, which still earns something");
        }

        [Test]
        public void TheOptimiserTurnsAComboTowardsAnAxisWhenTheRewardIsStrongEnough()
        {
            var notes = new[] { note(100, 300, 1000), note(300, 300, 1300, false) };
            var start = angles(notes, 20, 20);

            var weak = OsuDivaPlanner.Refine(notes, none, plan, start, options(o => with(o, axisWeight: 0.05f, moveWeight: 0.7f)));
            var strong = OsuDivaPlanner.Refine(notes, none, plan, start, options(o => with(o, axisWeight: 3, moveWeight: 0.7f)));

            Assert.That(weak, Is.EqualTo(start), "a weak reward does not beat the cost of turning");

            // the reward is about the combo's AVERAGE direction, so the search may also turn the notes to either side of an axis.
            var strongOpt = options(o => with(o, axisWeight: 3, moveWeight: 0.7f));
            var before = OsuDivaPlanner.Evaluate(notes, none, plan, start, start, strongOpt);
            var after = OsuDivaPlanner.Evaluate(notes, none, plan, strong, start, strongOpt);

            Assert.That(after.AxisAlignment, Is.LessThan(before.AxisAlignment), "a bigger reward");
            Assert.That(after.Total, Is.LessThan(before.Total));

            var mean = strong.Select(OsuDivaMath.DirectionFromAngle).Aggregate(Vector2.Zero, (sum, v) => sum + v) / strong.Length;
            float meanAngle = OsuDivaMath.AngleOf(mean);
            float fromAxis = new[] { 0f, 90f, 180f, -90f }.Min(axis => OsuDivaMath.AngleDifference(meanAngle, axis));
            Assert.That(fromAxis, Is.LessThan(1), "the average direction of the combo is horizontal or vertical");
        }

        [Test]
        public void FreeDirectionLetsNotesEnterFromAnySide()
        {
            // a note in the top left corner is on screen for well under a second flying down; the longest way in is from below.
            var notes = new[] { note(30, 5, 3000) };
            var start = angles(notes, 0);
            var demanding = options(o => with(o, minVisibleMs: 1400, moveWeight: 0.1f));

            var free = OsuDivaPlanner.Refine(notes, none, plan, start, with(demanding, free: true));
            var sector = OsuDivaPlanner.Refine(notes, none, plan, start, with(demanding, free: false));

            Assert.That(Math.Abs(sector[0]), Is.LessThanOrEqualTo(75 + 1e-3f), "within the top sector the angle is limited to 75 degrees either way");
            Assert.That(Math.Abs(free[0]), Is.GreaterThan(75), "free: enters from the side or from below");

            double TimeOnScreen(float angle)
            {
                var d = OsuDivaMath.DirectionFromAngle(angle);
                return 3000 - OsuDivaMath.EntryTime(30, 5, 3000, d, 400, 60, ScreenBounds.Default);
            }

            Assert.That(TimeOnScreen(free[0]), Is.GreaterThan(TimeOnScreen(sector[0])));
        }

        [Test]
        public void NotesOfOneComboAreNotTiedToOneSector()
        {
            // two notes of one combo that both start from the top: with free direction, one may end up entering from below.
            var notes = new[] { note(30, 5, 3000), note(480, 5, 3300, startsCombo: false) };
            var start = angles(notes, 0, 0);
            var opt = with(options(), minVisibleMs: 1400, moveWeight: 0.1f, free: true);

            var refined = OsuDivaPlanner.Refine(notes, none, plan, start, opt);

            // the top sector only reaches angles within 75 degrees of straight down; the optimiser sends at least one note in across the screen instead.
            Assert.That(refined.Any(a => Math.Abs(a) > 75), Is.True);
        }

        [Test]
        public void ComboRulesWorkOnTheCombosTheBuilderMakes()
        {
            var objects = new List<OsuHitObject>();

            for (int i = 0; i < 8; i++)
            {
                var c = new HitCircle { StartTime = 1000 + i * 250, Position = new Vector2(60 + i * 55, 200) };
                c.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                c.IndexInCurrentCombo = i;
                objects.Add(c);
            }

            var parts = FlightPartBuilder.Build(objects, FlightSettings.Default);

            var entry = parts.Select(p => p.EntryAngle).ToArray();
            double spread = 0;

            for (int a = 0; a < entry.Length; a++)
                for (int b = a + 1; b < entry.Length; b++)
                    spread = Math.Max(spread, OsuDivaMath.AngleDifference(entry[a], entry[b]));

            Assert.That(parts.All(p => Math.Abs(OsuDivaMath.AngleOf(p.Direction) - p.EntryAngle) < 1e-3), "the part's direction is its entry angle");
            Assert.That(spread, Is.LessThanOrEqualTo(180));
        }

        #endregion

        #region Combo range, neighbour distance and interval, outward fan

        private static readonly PlannerOptions fanOptions = with(options(), maxAngleDifference: 15, angleDifferenceWeight: 2, adjacentDistance: 150, adjacentInterval: 300, divergence: 1);

        [Test]
        public void TheAllowedAngleDifferenceShrinksForNearbyAndFastNeighbours()
        {
            float Allowed(float distance, double interval, PlannerOptions o)
                => OsuDivaPlanner.AllowedDifference(note(100, 300, 1000), note(100 + distance, 300, 1000 + interval), o);

            var o = PlannerOptions.Default; // 15 degrees, full at 150 units and 300 ms

            Assert.That(Allowed(300, 1000, o), Is.EqualTo(15).Within(1e-4), "far apart and slow: the whole range");
            Assert.That(Allowed(150, 300, o), Is.EqualTo(15).Within(1e-4), "exactly at the reference");
            Assert.That(Allowed(75, 1000, o), Is.EqualTo(7.5).Within(1e-4), "half the distance: half the range");
            Assert.That(Allowed(300, 150, o), Is.EqualTo(7.5).Within(1e-4), "half the interval: half the range");
            Assert.That(Allowed(75, 150, o), Is.EqualTo(3.75).Within(1e-4), "both: a quarter");
            Assert.That(Allowed(10, 20, o), Is.EqualTo(1).Within(1e-4), "a stream of nearby notes is not forced to exactly the same angle");
            Assert.That(Allowed(0, 0, with(o, maxAngleDifference: 0)), Is.EqualTo(0), "a range of 0 means 0");

            Assert.That(Allowed(10, 20, with(o, adjacentDistance: 0, adjacentInterval: 0)), Is.EqualTo(15).Within(1e-4), "scaling off: always the full range");
            Assert.That(Allowed(75, 1000, with(o, adjacentDistance: 0)), Is.EqualTo(15).Within(1e-4), "distance scaling off");
            Assert.That(Allowed(300, 150, with(o, adjacentInterval: 0)), Is.EqualTo(15).Within(1e-4), "interval scaling off");
        }

        [Test]
        public void NeighboursDifferingMoreThanTheirDistanceAndIntervalAllowArePenalised()
        {
            var zero = new[] { 0f, 0f };
            var twelveApart = new[] { 0f, 12f };

            double Excess(PlannerNote a, PlannerNote b) => OsuDivaPlanner.Evaluate(new[] { a, b }, none, plan, twelveApart, zero, fanOptions).AdjacentDifference;

            Assert.That(Excess(note(100, 300, 1000), note(400, 300, 2000, false)), Is.EqualTo(0), "12 degrees is within the 15 allowed for a far, slow pair");

            double near = Excess(note(100, 300, 1000), note(130, 300, 1080, false));
            double nearer = Excess(note(100, 300, 1000), note(110, 300, 1030, false));

            Assert.That(near, Is.GreaterThan(0), "the same 12 degrees is too much for notes 30 units and 80 ms apart");
            Assert.That(nearer, Is.GreaterThan(0));
            Assert.That(Excess(note(100, 300, 1000), note(250, 300, 1200, false)), Is.GreaterThan(0), "150 units apart but only 200 ms: 10 degrees allowed, 12 flown");
        }

        [Test]
        public void FlightLinesThatMoveApartAreRewardedAndLinesThatConvergeArePenalised()
        {
            // two notes side by side: the left one at x=100, the right one at x=300, an easy 1 s apart.
            var notes = new[] { note(100, 300, 1000), note(300, 300, 2000, false) };
            var zero = new[] { 0f, 0f };

            double Fan(float left, float right) => OsuDivaPlanner.Evaluate(notes, none, plan, new[] { left, right }, zero, fanOptions).Divergence;

            Assert.That(Fan(0, 0), Is.EqualTo(0).Within(1e-9), "parallel lines neither fan out nor converge");
            Assert.That(Fan(-5, 5), Is.LessThan(0), "the left note leans left, the right one right: the lines move apart: reward");
            Assert.That(Fan(5, -5), Is.GreaterThan(0), "leaning towards each other: penalty");
            Assert.That(Fan(5, -5), Is.EqualTo(-Fan(-5, 5)).Within(1e-9), "the same size, the other sign");
            Assert.That(Fan(-10, 10), Is.EqualTo(-1).Within(1e-9), "saturates at the strength once they differ by the allowed 15 degrees or more");
            Assert.That(Fan(-30, 30), Is.EqualTo(-1).Within(1e-9));
            Assert.That(Fan(-3, 3), Is.GreaterThan(Fan(-5, 5)), "less fan, less reward");

            // the same lines side by side but the left one is the later note: the pair is judged in hit order, so the sign follows the order of the notes.
            var reversed = new[] { note(300, 300, 1000), note(100, 300, 2000, false) };
            double Reversed(float first, float second) => OsuDivaPlanner.Evaluate(reversed, none, plan, new[] { first, second }, zero, fanOptions).Divergence;
            Assert.That(Reversed(5, -5), Is.LessThan(0), "now the first note is the right one: leaning right/left is the outward fan");
        }

        [Test]
        public void TheOptimiserFansNeighboursOutwardsWithinTheAllowedRange()
        {
            var notes = new[] { note(100, 300, 1000), note(300, 300, 2000, false) };
            var start = new[] { 0f, 0f };

            var refined = OsuDivaPlanner.Refine(notes, none, plan, start, fanOptions);

            // how the 15 degrees are shared between the two notes does not matter (leaning left 15 and right 0 costs the same as 7.5 each way)
            Assert.That(refined[0], Is.LessThan(refined[1]), "the left note leans further left than the right one: the lines move apart");
            Assert.That(OsuDivaMath.AngleDifference(refined[0], refined[1]), Is.LessThanOrEqualTo(15 + 3), "and they stay about within the 15 degree range");
            Assert.That(OsuDivaPlanner.Evaluate(notes, none, plan, refined, start, fanOptions).Divergence, Is.LessThan(-0.9), "nearly the full reward");
            Assert.That(OsuDivaPlanner.Evaluate(notes, none, plan, refined, start, fanOptions).Total, Is.LessThan(OsuDivaPlanner.Evaluate(notes, none, plan, start, start, fanOptions).Total));
        }

        [Test]
        public void NotesThatAreCloseInSpaceAndTimeFlyInAlmostParallel()
        {
            var notes = new[] { note(100, 300, 1000), note(130, 300, 1080, false) };
            var start = new[] { 0f, 0f };

            var refined = OsuDivaPlanner.Refine(notes, none, plan, start, fanOptions);

            Assert.That(OsuDivaMath.AngleDifference(refined[0], refined[1]), Is.LessThanOrEqualTo(3), "30 units and 80 ms apart: nearly the same direction");
        }

        [Test]
        public void AWholeComboIsPulledIntoAboutFifteenDegreesAndKeepsFanningOutwards()
        {
            // five notes in a row, a second apart, entering at the fan angles of a 1080p screen: 50 degrees from the first to the last.
            var notes = new[]
            {
                note(50, 300, 1000), note(150, 300, 2000, false), note(250, 300, 3000, false), note(350, 300, 4000, false), note(450, 300, 5000, false),
            };
            var start = new[] { -25f, -12f, 0f, 12f, 25f };
            var opt = with(PlannerOptions.Default, free: false);

            var refined = OsuDivaPlanner.Refine(notes, none, plan, start, opt);

            double Range(float[] a) => a.Max() - a.Min();

            Assert.That(Range(start), Is.EqualTo(50));
            Assert.That(Range(refined), Is.LessThan(25), "from 50 degrees to about the 15 degree range");
            for (int i = 0; i + 1 < refined.Length; i++)
                Assert.That(refined[i], Is.LessThanOrEqualTo(refined[i + 1] + 1e-3), "still an outward fan: left to right");
        }

        [Test]
        public void ACornerComboTurnsAsAWholeToGetMoreTimeOnScreen()
        {
            // four notes in the top left corner, 400 ms apart. Flown straight down each is on screen for about 300 ms, well under the 600 minimum.
            // A single note cannot leave on its own without breaking the 15 degree range, so the combo has to turn together.
            var notes = new[] { note(30, 15, 2000), note(60, 15, 2400, false), note(90, 15, 2800, false), note(120, 15, 3200, false) };
            var start = new float[notes.Length];
            var opt = PlannerOptions.Default;

            var before = OsuDivaPlanner.Evaluate(notes, none, plan, start, start, opt);
            var refined = OsuDivaPlanner.Refine(notes, none, plan, start, opt);
            var after = OsuDivaPlanner.Evaluate(notes, none, plan, refined, start, opt);

            double range = 0;
            for (int a = 0; a < refined.Length; a++)
                for (int b = a + 1; b < refined.Length; b++)
                    range = Math.Max(range, OsuDivaMath.AngleDifference(refined[a], refined[b]));

            Assert.That(before.Short, Is.GreaterThan(1), "starts well short of the minimum time on screen");
            Assert.That(after.Short, Is.LessThan(before.Short / 4), "after the combo has turned, nearly enough time");
            Assert.That(range, Is.LessThanOrEqualTo(25), "and it still flies in as one group");
            Assert.That(after.Total, Is.LessThan(before.Total));
        }

        #endregion

        private static (PlannerNote[] notes, PlannerObstacle[] obstacles) denseLongMap()
        {
            // 3000 notes, one every 100 ms (a 5 minute map at 10 notes per second), each with 8 slider-sized obstacles: far beyond any real map.
            var rng = new Random(11);
            var notes = Enumerable.Range(0, 3000).Select(i => note(rng.Next(20, 490), rng.Next(10, 370), 1000 + i * 100, startsCombo: i % 5 == 0)).ToArray();
            var obstacles = notes.SelectMany((n, i) => Enumerable.Range(0, 8).Select(k => new PlannerObstacle
            {
                Owner = i, Center = n.Target + new Vector2(k * 20, 0), Radius = 30, From = n.Time - 600, To = n.Time + 400,
            })).ToArray();
            return (notes, obstacles);
        }

        [Test]
        public void TheTimeBudgetStopsTheSearchOnAnEnormousMapWithoutMakingThingsWorse()
        {
            var (notes, obstacles) = denseLongMap();
            var start = angles(notes, new float[notes.Length]);
            var opt = PlannerOptions.Default; // 4 s budget

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var refined = OsuDivaPlanner.Refine(notes, obstacles, plan, start, opt);
            watch.Stop();

            var before = OsuDivaPlanner.Evaluate(notes, obstacles, plan, start, start, opt);
            var after = OsuDivaPlanner.Evaluate(notes, obstacles, plan, refined, start, opt);

            TestContext.WriteLine($"planning 3000 notes / {obstacles.Length} obstacles took {watch.ElapsedMilliseconds} ms, cost {before.Total:0} -> {after.Total:0}");
            Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(opt.TimeBudgetMs / 1000.0 + 3), "stops soon after the budget");
            Assert.That(after.Total, Is.LessThanOrEqualTo(before.Total + 1e-6), "an early stop is never worse than the start");
        }

        [Test]
        public void ABudgetOfOneMillisecondStillReturnsAValidPlan()
        {
            var (notes, obstacles) = denseLongMap();
            var start = angles(notes, new float[notes.Length]);
            var opt = new PlannerOptions
            {
                MinVisibleMs = 600, ShortWeight = 4, ThroughWeight = 6, ThroughFullMs = 150, OverlapFactor = 0.75f, CrossWeight = 3, OrderWeight = 5, MoveWeight = 0.7f,
                MaxAngleDifference = 15, AdjacentFullDistance = 150, AdjacentFullIntervalMs = 300, DivergenceWeight = 1, AngleDifferenceWeight = 2, AxisAlignmentWeight = 0.3f,
                FreeDirection = true, MaxPasses = 3, Search = PlannerSearch.CombosThenNotes, TimeBudgetMs = 1,
            };

            var refined = OsuDivaPlanner.Refine(notes, obstacles, plan, start, opt);

            Assert.That(refined.Length, Is.EqualTo(notes.Length));
            Assert.That(refined.All(a => a >= -180 && a <= 180));
        }

        [TestCase(PlannerSearch.Notes)]
        [TestCase(PlannerSearch.Combos)]
        [TestCase(PlannerSearch.CombosThenNotes)]
        public void EverySearchMethodLowersTheCostOfADenseRandomMap(PlannerSearch search)
        {
            var rng = new Random(7);
            var notes = Enumerable.Range(0, 80).Select(i => note(rng.Next(20, 490), rng.Next(10, 370), 1000 + i * 120, startsCombo: i % 5 == 0)).ToArray();
            var obstacles = notes.Select((n, i) => circle(i, n.Target.X, n.Target.Y, n.Time)).ToArray();
            var start = angles(notes, new float[notes.Length]);
            var opt = PlannerOptions.Default.WithSearch(search);

            var before = OsuDivaPlanner.Evaluate(notes, obstacles, plan, start, start, opt);
            var refined = OsuDivaPlanner.Refine(notes, obstacles, plan, start, opt);
            var after = OsuDivaPlanner.Evaluate(notes, obstacles, plan, refined, start, opt);

            Assert.That(after.Total, Is.LessThan(before.Total));
        }

        [Test]
        public void TheComboSearchKeepsEveryComboWithinItsRangeAndFansItOutwards()
        {
            // a combo of six notes spread over the playfield, a second apart: the shape the combo search is built for.
            var notes = new[]
            {
                note(40, 300, 1000), note(120, 300, 2000, false), note(200, 300, 3000, false), note(280, 300, 4000, false), note(360, 300, 5000, false), note(440, 300, 6000, false),
            };
            var start = new float[notes.Length];
            var opt = PlannerOptions.Default.WithSearch(PlannerSearch.Combos);

            var refined = OsuDivaPlanner.Refine(notes, none, plan, start, opt);

            double range = refined.Max() - refined.Min();
            Assert.That(range, Is.LessThanOrEqualTo(15 + 0.5), "inside the combo range");
            for (int i = 0; i + 1 < refined.Length; i++)
                Assert.That(refined[i], Is.LessThanOrEqualTo(refined[i + 1] + 1e-3), "left to right: an outward fan");
        }

        [Test]
        public void TheFanOfACloseFastComboIsNarrowerThanTheFanOfASparseOne()
        {
            PlannerNote[] Row(float spacing, double interval) => Enumerable.Range(0, 5).Select(i => note(100 + i * spacing, 300, 1000 + i * interval, startsCombo: i == 0)).ToArray();
            var opt = PlannerOptions.Default.WithSearch(PlannerSearch.Combos);

            double Range(PlannerNote[] notes) => OsuDivaPlanner.Refine(notes, none, plan, new float[notes.Length], opt) is var a ? a.Max() - a.Min() : 0;

            double sparse = Range(Row(100, 1000));
            double stream = Range(Row(30, 100));

            Assert.That(stream, Is.LessThan(sparse), "30 units and 100 ms apart: a narrower fan than 100 units and a second apart");
            Assert.That(stream, Is.LessThan(8));
        }

        [Test]
        public void AComboNextToAnEdgeEntersFromTheFarSideByDefault()
        {
            // Reported on real maps: a combo near the top edge was flown in from the top, so its notes were on screen for only 300-400 ms.
            var notes = new[] { note(120, 30, 2000), note(190, 45, 2400, false), note(260, 35, 2800, false), note(330, 50, 3200, false) };
            var start = angles(notes, new float[notes.Length]);

            double Shortest(float[] a) => notes.Select((n, i) =>
                n.Time - OsuDivaMath.EntryTime(n.Target.X, n.Target.Y, n.Time, OsuDivaMath.DirectionFromAngle(a[i]), plan.Speed, n.Radius * 2, plan.Screen)).Min();

            var refined = OsuDivaPlanner.Refine(notes, none, plan, start, PlannerOptions.Default);

            Assert.That(Shortest(start), Is.LessThan(500), "flown straight down from the top edge");
            Assert.That(Shortest(refined), Is.GreaterThanOrEqualTo(800), "after planning every note of the combo has time to be seen (about 1.0-1.4 s here)");
            Assert.That(refined.Select(a => OsuDivaMath.AngleDifference(a, 0)).Min(), Is.GreaterThan(45), "no note comes in from the nearby top edge any more (it crosses the screen from the side)");
            Assert.That(refined.Max() - refined.Min(), Is.LessThanOrEqualTo(20), "and still as one group");
        }

        [Test]
        public void TheOnScreenTimePenaltyCanBeTurnedOff()
        {
            var notes = new[] { note(120, 30, 2000), note(190, 45, 2400, false), note(260, 35, 2800, false), note(330, 50, 3200, false) };
            var start = angles(notes, new float[notes.Length]);
            var off = with(PlannerOptions.Default, free: true);
            off = new PlannerOptions
            {
                MinVisibleMs = off.MinVisibleMs, ShortWeight = 0, ThroughWeight = off.ThroughWeight, ThroughFullMs = off.ThroughFullMs, OverlapFactor = off.OverlapFactor,
                CrossWeight = off.CrossWeight, OrderWeight = off.OrderWeight, MoveWeight = off.MoveWeight, MaxAngleDifference = off.MaxAngleDifference,
                AdjacentFullDistance = off.AdjacentFullDistance, AdjacentFullIntervalMs = off.AdjacentFullIntervalMs, DivergenceWeight = off.DivergenceWeight,
                AngleDifferenceWeight = off.AngleDifferenceWeight, AxisAlignmentWeight = off.AxisAlignmentWeight, FreeDirection = true, MaxPasses = off.MaxPasses,
                Search = off.Search, TimeBudgetMs = off.TimeBudgetMs,
            };

            var refined = OsuDivaPlanner.Refine(notes, none, plan, start, off);

            Assert.That(refined.Select(a => OsuDivaMath.AngleDifference(a, 0)).Max(), Is.LessThan(45), "with the penalty off nothing pulls the combo away from the nearby top edge");
        }

        [Test]
        public void PerNoteCostsAddUpToTheTotals()
        {
            var rng = new Random(3);
            var notes = Enumerable.Range(0, 60).Select(i => note(rng.Next(20, 490), rng.Next(10, 370), 1000 + i * 90, startsCombo: i % 4 == 0)).ToArray();
            var obstacles = notes.Select((n, i) => circle(i, n.Target.X, n.Target.Y, n.Time)).ToArray();
            var start = angles(notes, new float[notes.Length]);
            var opt = PlannerOptions.Default;
            var refined = OsuDivaPlanner.Refine(notes, obstacles, plan, start, opt);

            var total = OsuDivaPlanner.Evaluate(notes, obstacles, plan, refined, start, opt);
            var each = OsuDivaPlanner.EvaluatePerNote(notes, obstacles, plan, refined, start, opt);

            Assert.That(each.Length, Is.EqualTo(notes.Length));
            Assert.That(each.Sum(c => c.Short), Is.EqualTo(total.Short).Within(1e-6));
            Assert.That(each.Sum(c => c.Through), Is.EqualTo(total.Through).Within(1e-6));
            Assert.That(each.Sum(c => c.Order), Is.EqualTo(total.Order).Within(1e-6));
            Assert.That(each.Sum(c => c.Move), Is.EqualTo(total.Move).Within(1e-6));
            Assert.That(each.Sum(c => c.CrossingPairs), Is.EqualTo(total.CrossingPairs * 2), "a crossing counts for both of its notes");
        }

        [Test]
        public void BuilderRunsTheOptimiserOnlyWhenEnabled()
        {
            var circle = new HitCircle { StartTime = 3000, Position = new Vector2(256, 10) };
            circle.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            circle.IndexInCurrentCombo = 0;

            var on = FlightPartBuilder.Build(new OsuHitObject[] { circle }, FlightSettings.Default).Single();
            var off = FlightPartBuilder.Build(new OsuHitObject[] { circle }, FlightSettings.Default.WithOptimise(false)).Single();

            Assert.That(off.EntryAngle, Is.EqualTo(OsuDivaMath.AngleOf(plan.DirectionOf(256))).Within(1e-3), "without the optimiser: the direction mode's angle");
            Assert.That(OsuDivaMath.AngleDifference(on.EntryAngle, off.EntryAngle), Is.GreaterThan(0));
            Assert.That(on.Time - on.VisibleFrom, Is.GreaterThan(off.Time - off.VisibleFrom), "longer on screen after optimising");
        }
    }
}
