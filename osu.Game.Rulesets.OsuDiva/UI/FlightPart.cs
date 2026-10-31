using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.DivaFlight.UI
{
    public enum FlightPartKind
    {
        Circle,
        SliderHead,
        Spinner,

        /// <summary>Dots along the slider path; not a flying note.</summary>
        SliderBody,
    }

    /// <summary>
    /// Everything the layer needs to draw one flying note, computed once from the beatmap and the settings.
    /// </summary>
    public sealed class FlightPart
    {
        public FlightPartKind Kind { get; init; }

        public OsuHitObject Source { get; init; } = null!;

        /// <summary>Where the note lands, in playfield units.</summary>
        public Vector2 Target { get; init; }

        /// <summary>Planned hit (arrival) time.</summary>
        public double Time { get; init; }

        /// <summary>The note stays on its target until this time (spinners and slider bodies); equal to <see cref="Time"/> otherwise.</summary>
        public double HoldUntil { get; init; }

        /// <summary>Time at which the note first enters the screen.</summary>
        public double VisibleFrom { get; init; }

        /// <summary>Note diameter in playfield units.</summary>
        public float Diameter { get; init; }

        /// <summary>Absolute entry angle in degrees: 0 enters from above, 90 from the right, 180 from below, -90 from the left.</summary>
        public float EntryAngle { get; init; }

        /// <summary>Flight direction as in the Python prototype: the note sits at (x + dx * d, y - dy * d) at distance d from its target.</summary>
        public Vector2 Direction { get; init; }

        /// <summary>Combo number to print on the note, or 0 for none.</summary>
        public int Number { get; init; }

        public Slider? Slider { get; init; }

        public bool IsFlying => Kind != FlightPartKind.SliderBody;
    }

    public readonly struct FlightSettings
    {
        public float Speed { get; init; }
        public float NoteScale { get; init; }
        public OsuDivaDirection Direction { get; init; }
        public float FanAngle { get; init; }
        public bool ShowNumbers { get; init; }
        public bool ShowSliderBody { get; init; }
        public ScreenBounds Screen { get; init; }

        /// <summary>Run the penalty-based second planning stage (see <see cref="OsuDivaPlanner"/>).</summary>
        public bool Optimise { get; init; }

        /// <summary>A note should be on screen at least this long (ms) before it lands.</summary>
        public float MinVisibleMs { get; init; }

        /// <summary>How strongly the optimiser avoids notes that are on screen for less than <see cref="MinVisibleMs"/>.</summary>
        public float ShortWeight { get; init; }

        /// <summary>Notes of one combo should enter within this many degrees of each other (0..180).</summary>
        public float MaxAngleDifference { get; init; }

        /// <summary>Neighbouring notes of a combo may differ by the full <see cref="MaxAngleDifference"/> only when at least this far apart (playfield units).</summary>
        public float AdjacentFullDistance { get; init; }

        /// <summary>...and at least this far apart in time (ms). Closer or faster pairs are allowed proportionally less.</summary>
        public float AdjacentFullIntervalMs { get; init; }

        /// <summary>Strength of the reward for neighbouring notes whose flight lines fan outwards.</summary>
        public float DivergenceWeight { get; init; }

        /// <summary>Strength of the penalty for notes of one combo entering further apart than <see cref="MaxAngleDifference"/>.</summary>
        public float AngleDifferenceWeight { get; init; }

        /// <summary>Strength of the reward for combos whose average entry direction is horizontal or vertical.</summary>
        public float AxisAlignmentWeight { get; init; }

        /// <summary>Let any note enter from any side, also within one combo, instead of only from its combo's top or bottom sector.</summary>
        public bool FreeDirection { get; init; }

        /// <summary>How the optimiser searches: note by note, per combo, or per combo then note by note.</summary>
        public PlannerSearch Search { get; init; }

        public FlightSettings WithOptimise(bool optimise) => new FlightSettings
        {
            Speed = Speed, NoteScale = NoteScale, Direction = Direction, FanAngle = FanAngle,
            ShowNumbers = ShowNumbers, ShowSliderBody = ShowSliderBody, Screen = Screen, Optimise = optimise, MinVisibleMs = MinVisibleMs, ShortWeight = ShortWeight,
            MaxAngleDifference = MaxAngleDifference, AngleDifferenceWeight = AngleDifferenceWeight, AxisAlignmentWeight = AxisAlignmentWeight,
            AdjacentFullDistance = AdjacentFullDistance, AdjacentFullIntervalMs = AdjacentFullIntervalMs, DivergenceWeight = DivergenceWeight,
            FreeDirection = FreeDirection, Search = Search,
        };

        public FlightPlan Plan => new FlightPlan { Speed = Speed, Direction = Direction, FanAngle = FanAngle, Screen = Screen };

        public static FlightSettings Default => new FlightSettings
        {
            Speed = 400,
            NoteScale = 1,
            Direction = OsuDivaDirection.Fan,
            FanAngle = 25,
            ShowNumbers = false,
            ShowSliderBody = true,
            Screen = ScreenBounds.Default,
            Optimise = true,
            MinVisibleMs = 900,
            ShortWeight = 32,
            MaxAngleDifference = 15,
            AdjacentFullDistance = 150,
            AdjacentFullIntervalMs = 300,
            DivergenceWeight = 1,
            AngleDifferenceWeight = 2,
            AxisAlignmentWeight = 0.3f,
            FreeDirection = true,
            Search = PlannerSearch.CombosThenNotes,
        };
    }

    /// <summary>Everything <see cref="OsuDivaPlanner.Refine"/> needs for one beatmap.</summary>
    public sealed class PlannerInput
    {
        public IReadOnlyList<PlannerNote> Notes { get; init; } = null!;
        public IReadOnlyList<PlannerObstacle> Obstacles { get; init; } = null!;
        public FlightPlan Plan { get; init; }
        public float[] BaseAngles { get; init; } = null!;
        public PlannerOptions Options { get; init; }
    }

    public static class FlightPartBuilder
    {
        /// <summary>
        /// The input of the planner for a beatmap, built the same way <see cref="Build"/> builds it. For measuring and testing the planner on its own.
        /// </summary>
        public static PlannerInput CreatePlannerInput(IEnumerable<OsuHitObject> hitObjects, FlightSettings settings)
        {
            var mains = hitObjects.OrderBy(h => h.StartTime).Where(o => o is HitCircle || o is Slider || o is Spinner).ToList();
            var plan = settings.Plan;
            var angles = mains.Select(o => OsuDivaMath.AngleOf(plan.DirectionOf(targetOf(o).X))).ToArray();
            return createPlannerInput(mains, angles, plan, settings);
        }

        /// <summary>How long before the slider's start time its body guide appears.</summary>
        public const double BODY_LEAD_TIME = 800;

        public static List<FlightPart> Build(IEnumerable<OsuHitObject> hitObjects, FlightSettings settings)
        {
            var objects = hitObjects.OrderBy(h => h.StartTime).ToList();
            var parts = new List<FlightPart>();
            var plan = settings.Plan;

            // Circles, slider heads and spinners are the notes the player plays.
            var mains = objects.Where(o => o is HitCircle || o is Slider || o is Spinner).ToList();

            // A flight is an absolute entry angle. The direction mode (vertical or fan) gives the starting angles,
            // the optimiser may then send any note in from any side (unless the free direction rule is off).
            var angles = mains.Select(o => OsuDivaMath.AngleOf(plan.DirectionOf(targetOf(o).X))).ToArray();

            if (settings.Optimise)
                angles = refine(mains, angles, plan, settings);

            for (int i = 0; i < mains.Count; i++)
            {
                var o = mains[i];
                float diameter = (float)o.Radius * 2 * settings.NoteScale;

                var kind = o is Slider ? FlightPartKind.SliderHead : o is Spinner ? FlightPartKind.Spinner : FlightPartKind.Circle;
                var slider = o as Slider;

                parts.Add(make(plan, kind, o, targetOf(o), o.StartTime, o is Spinner s ? s.EndTime : o.StartTime, diameter, angles[i],
                    kind == FlightPartKind.Spinner ? 0 : numberOf(o, settings), slider));

                if (slider == null)
                    continue;

                // Only the head flies in. Repeats and the tail get no note of their own.
                if (settings.ShowSliderBody)
                {
                    parts.Add(new FlightPart
                    {
                        Kind = FlightPartKind.SliderBody,
                        Source = slider,
                        Slider = slider,
                        Target = slider.StackedPosition,
                        Time = slider.StartTime,
                        HoldUntil = slider.EndTime,
                        VisibleFrom = slider.StartTime - BODY_LEAD_TIME,
                        Diameter = diameter,
                    });
                }
            }

            return parts;
        }

        /// <summary>
        /// Planning: starts from the direction mode angles and lowers the penalties
        /// for notes on screen too briefly, flights through other objects, and crossing flights.
        /// </summary>
        private static float[] refine(IReadOnlyList<OsuHitObject> mains, float[] baseAngles, FlightPlan plan, FlightSettings settings)
        {
            var input = createPlannerInput(mains, baseAngles, plan, settings);
            return OsuDivaPlanner.Refine(input.Notes, input.Obstacles, input.Plan, input.BaseAngles, input.Options);
        }

        private static PlannerInput createPlannerInput(IReadOnlyList<OsuHitObject> mains, float[] baseAngles, FlightPlan plan, FlightSettings settings)
        {
            var notes = new List<PlannerNote>(mains.Count);
            var obstacles = new List<PlannerObstacle>();

            for (int i = 0; i < mains.Count; i++)
            {
                var o = mains[i];

                notes.Add(new PlannerNote
                {
                    Target = targetOf(o),
                    Time = o.StartTime,
                    Radius = (float)o.Radius * settings.NoteScale,
                    StartsCombo = startsCombo(o),
                });

                if (o is Spinner)
                    continue;

                float radius = (float)o.Radius;
                double from = o.StartTime - o.TimePreempt;

                // The circle (or slider head) itself is on screen from its approach start until just after it is hit.
                obstacles.Add(new PlannerObstacle { Owner = i, Center = targetOf(o), Radius = radius, From = from, To = o.StartTime + obstacle_linger });

                // A slider body is on screen until the slider ends: treat points along the path as circles too.
                if (o is Slider slider)
                {
                    int samples = (int)Math.Clamp(slider.Path.Distance / Math.Max(1, radius), 1, 40);

                    for (int k = 1; k <= samples; k++)
                    {
                        obstacles.Add(new PlannerObstacle
                        {
                            Owner = i,
                            Center = slider.StackedPosition + slider.Path.PositionAt((double)k / samples),
                            Radius = radius,
                            From = from,
                            To = slider.EndTime + obstacle_linger,
                            IsSliderBody = true,
                        });
                    }
                }
            }

            var options = PlannerOptions.Default;
            options = new PlannerOptions
            {
                MinVisibleMs = settings.MinVisibleMs,
                ShortWeight = settings.ShortWeight,
                ThroughWeight = options.ThroughWeight,
                ThroughFullMs = options.ThroughFullMs,
                OverlapFactor = options.OverlapFactor,
                CrossWeight = options.CrossWeight,
                OrderWeight = options.OrderWeight,
                MoveWeight = options.MoveWeight,
                MaxAngleDifference = settings.MaxAngleDifference,
                AdjacentFullDistance = settings.AdjacentFullDistance,
                AdjacentFullIntervalMs = settings.AdjacentFullIntervalMs,
                DivergenceWeight = settings.DivergenceWeight,
                AngleDifferenceWeight = settings.AngleDifferenceWeight,
                AxisAlignmentWeight = settings.AxisAlignmentWeight,
                FreeDirection = settings.FreeDirection,
                MaxPasses = options.MaxPasses,
                Search = settings.Search,
                TimeBudgetMs = options.TimeBudgetMs,
            };

            return new PlannerInput { Notes = notes, Obstacles = obstacles, Plan = plan, BaseAngles = baseAngles, Options = options };
        }

        /// <summary>How long a circle stays on screen after its hit time (ms).</summary>
        private const double obstacle_linger = 150;

        private static FlightPart make(FlightPlan plan, FlightPartKind kind, OsuHitObject source, Vector2 target, double time, double holdUntil, float diameter,
                                       float angle, int number, Slider? slider)
        {
            var direction = OsuDivaMath.DirectionFromAngle(angle);

            return new FlightPart
            {
                Kind = kind,
                Source = source,
                Slider = slider,
                Target = target,
                Time = time,
                HoldUntil = Math.Max(time, holdUntil),
                VisibleFrom = OsuDivaMath.EntryTime(target.X, target.Y, time, direction, plan.Speed, diameter, plan.Screen),
                Diameter = diameter,
                EntryAngle = angle,
                Direction = direction,
                Number = number,
            };
        }

        private static Vector2 targetOf(OsuHitObject o) => o is Spinner ? o.Position : o.StackedPosition;

        private static bool startsCombo(OsuHitObject o) => o is IHasComboInformation c && c.IndexInCurrentCombo == 0;

        private static int numberOf(OsuHitObject o, FlightSettings settings)
            => settings.ShowNumbers && o is IHasComboInformation c ? c.IndexInCurrentCombo + 1 : 0;
    }
}
