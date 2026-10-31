using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Rulesets.DivaFlight.Localisation;
using osuTK;

namespace osu.Game.Rulesets.DivaFlight
{
    /// <summary>A playable note as the planner sees it.</summary>
    public sealed class PlannerNote
    {
        public Vector2 Target { get; init; }
        public double Time { get; init; }

        /// <summary>Radius of the flying note.</summary>
        public float Radius { get; init; }

        public bool StartsCombo { get; init; }
    }

    /// <summary>A circle that is on screen for a while and that a flying note should not pass through.</summary>
    public sealed class PlannerObstacle
    {
        /// <summary>Index of the note this obstacle belongs to; a note may pass over its own circle and slider.</summary>
        public int Owner { get; init; }

        public Vector2 Center { get; init; }
        public float Radius { get; init; }
        public double From { get; init; }
        public double To { get; init; }

        /// <summary>True for a point along a slider path, false for a circle or slider head. For measuring where the cost comes from.</summary>
        public bool IsSliderBody { get; init; }
    }

    /// <summary>How <see cref="OsuDivaPlanner.Refine"/> looks for lower-cost angles.</summary>
    public enum PlannerSearch
    {
        /// <summary>One note at a time, plus turning whole combos by one angle.</summary>
        [LocalisableDescription(typeof(DivaStrings), nameof(DivaStrings.SearchSingleNotes))]
        Notes,

        /// <summary>Per combo: a centre direction and a fan width; the angle of each note follows from its position across the combo. Nothing else.</summary>
        [LocalisableDescription(typeof(DivaStrings), nameof(DivaStrings.SearchWholeCombos))]
        Combos,

        /// <summary>The combo search first, then small adjustments of single notes (a few degrees, never a jump to another side).</summary>
        [LocalisableDescription(typeof(DivaStrings), nameof(DivaStrings.SearchWholeCombosThenNotes))]
        CombosThenNotes,
    }

    public readonly struct PlannerOptions
    {
        /// <summary>A note should be on screen at least this long (ms) before it lands.</summary>
        public float MinVisibleMs { get; init; }

        /// <summary>
        /// Cost of a note that is on screen for (almost) no time at all; the cost grows with the square of the missing share of <see cref="MinVisibleMs"/>.
        /// Large on purpose: turning a whole combo around costs about 1.4 per note, so the weight has to be well above that to be worth it.
        /// </summary>
        public float ShortWeight { get; init; }

        /// <summary>Cost of a flight that goes right through another visible object for <see cref="ThroughFullMs"/> or more.</summary>
        public float ThroughWeight { get; init; }

        public float ThroughFullMs { get; init; }

        /// <summary>Two notes count as touching when their centres are closer than (r1 + r2) * this.</summary>
        public float OverlapFactor { get; init; }

        /// <summary>Cost of each pair of flight segments that cross while both notes are on screen.</summary>
        public float CrossWeight { get; init; }

        /// <summary>Cost of entering the screen out of hit order within a combo.</summary>
        public float OrderWeight { get; init; }

        /// <summary>Cost of turning a flight all the way around from its starting direction. Keeps untouched flights untouched.</summary>
        public float MoveWeight { get; init; }

        /// <summary>
        /// Two notes of one combo should enter within this many degrees of each other (0..180).
        /// Beyond it the pair is penalised, growing with the square of the excess.
        /// </summary>
        public float MaxAngleDifference { get; init; }

        /// <summary>
        /// Two neighbouring notes of a combo may differ in entry direction by up to <see cref="MaxAngleDifference"/> when they are
        /// at least this far apart (playfield units) AND at least <see cref="AdjacentFullIntervalMs"/> apart in time.
        /// Closer or faster pairs are allowed proportionally less. 0 turns the distance scaling off.
        /// </summary>
        public float AdjacentFullDistance { get; init; }

        /// <summary>See <see cref="AdjacentFullDistance"/>. 0 turns the time scaling off.</summary>
        public float AdjacentFullIntervalMs { get; init; }

        /// <summary>
        /// Strength of the reward for neighbouring notes of a combo whose flight lines move apart towards the screen edge (an outward fan),
        /// and of the penalty for lines that move towards each other. 0 turns it off.
        /// </summary>
        public float DivergenceWeight { get; init; }

        /// <summary>Cost of a pair of notes of one combo entering 180 degrees apart. Shared out over the combo, so long combos are not pulled harder.</summary>
        public float AngleDifferenceWeight { get; init; }

        /// <summary>Reward (a negative cost) for a combo whose average entry direction is exactly horizontal or vertical and whose notes all agree.</summary>
        public float AxisAlignmentWeight { get; init; }

        /// <summary>
        /// False: a note may only turn within 75 degrees either side of the direction mode direction, so it keeps entering from above.
        /// True: any note may enter from any side, also within one combo.
        /// </summary>
        public bool FreeDirection { get; init; }

        public int MaxPasses { get; init; }

        public PlannerSearch Search { get; init; }

        /// <summary>
        /// Stop searching after this many milliseconds and keep what has been found so far (every accepted move lowered the cost,
        /// so an early stop is never worse than the start). Guards against enormous maps. 0 means no limit.
        /// </summary>
        public int TimeBudgetMs { get; init; }

        public PlannerOptions WithSearch(PlannerSearch search) => new PlannerOptions
        {
            MinVisibleMs = MinVisibleMs, ShortWeight = ShortWeight, ThroughWeight = ThroughWeight, ThroughFullMs = ThroughFullMs, OverlapFactor = OverlapFactor,
            CrossWeight = CrossWeight, OrderWeight = OrderWeight, MoveWeight = MoveWeight, MaxAngleDifference = MaxAngleDifference,
            AdjacentFullDistance = AdjacentFullDistance, AdjacentFullIntervalMs = AdjacentFullIntervalMs, DivergenceWeight = DivergenceWeight,
            AngleDifferenceWeight = AngleDifferenceWeight, AxisAlignmentWeight = AxisAlignmentWeight, FreeDirection = FreeDirection, MaxPasses = MaxPasses,
            Search = search, TimeBudgetMs = TimeBudgetMs,
        };

        public static PlannerOptions Default => new PlannerOptions
        {
            MinVisibleMs = 900,
            ShortWeight = 32,
            ThroughWeight = 6,
            ThroughFullMs = 150,
            OverlapFactor = 0.75f,
            CrossWeight = 3,
            OrderWeight = 5,
            MoveWeight = 0.7f,
            MaxAngleDifference = 15,
            AdjacentFullDistance = 150,
            AdjacentFullIntervalMs = 300,
            DivergenceWeight = 1,
            AngleDifferenceWeight = 2,
            AxisAlignmentWeight = 0.3f,
            FreeDirection = true,
            MaxPasses = 3,
            Search = PlannerSearch.CombosThenNotes,
            TimeBudgetMs = 4000,
        };
    }

    /// <summary>
    /// Optional measurements of a <see cref="OsuDivaPlanner.Refine"/> run: where the time goes and how many moves each pass accepted.
    /// Pass one to <c>Refine</c> to fill it in; without it nothing is measured.
    /// </summary>
    public sealed class PlannerStats
    {
        /// <summary>Time inside the cost of one note at one candidate angle, per term. The Stopwatch ticks add some overhead of their own.</summary>
        public long ShortTicks, ThroughTicks, CrossingTicks, OrderTicks, ComboTicks;

        /// <summary>Time of the whole note-by-note search and of the whole-combo turns, and of the run as a whole.</summary>
        public long NoteSearchTicks, ComboTurnTicks, TotalTicks;

        /// <summary>Number of candidate angles evaluated for single notes, and for whole combo turns.</summary>
        public long NoteEvaluations, ComboTurnEvaluations;

        /// <summary>Moves accepted per pass: single notes and whole combos.</summary>
        public readonly List<int> NoteMovesPerPass = new List<int>();

        public readonly List<int> ComboTurnsPerPass = new List<int>();

        public static double ToMilliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    }

    public readonly struct PlanCost
    {
        public double Short { get; init; }
        public double Through { get; init; }
        public double Crossing { get; init; }
        public double Order { get; init; }
        public double Move { get; init; }

        /// <summary>Penalty for notes of one combo entering from very different directions.</summary>
        public double AngleDifference { get; init; }

        /// <summary>Reward for combos whose average direction is horizontal or vertical; zero or negative.</summary>
        public double AxisAlignment { get; init; }

        /// <summary>Penalty for neighbouring notes of a combo that differ more than their distance and interval allow.</summary>
        public double AdjacentDifference { get; init; }

        /// <summary>Reward (negative) for flight lines that fan outwards, penalty (positive) for lines that converge.</summary>
        public double Divergence { get; init; }

        /// <summary>Number of pairs of flight segments that cross.</summary>
        public int CrossingPairs { get; init; }

        public double Total => Short + Through + Crossing + Order + Move + AngleDifference + AxisAlignment + AdjacentDifference + Divergence;
    }

    /// <summary>
    /// Second stage of the trajectory planning. The direction mode (vertical or fan) gives every note a starting direction.
    /// This stage tries other directions for each note and keeps the one with the lowest cost.
    /// Directions are absolute angles in degrees: 0 enters from above, 90 from the right, 180 (or -180) from below, -90 from the left.
    /// <list type="bullet">
    /// <item>The note is on screen too briefly.</item>
    /// <item>Its flight passes through another object.</item>
    /// <item>Its flight segment (from entering the screen to its target) crosses another note's flight segment.</item>
    /// <item>Notes of one combo enter from directions that differ by more than a limit.</item>
    /// <item>A combo whose average entry direction is horizontal or vertical is rewarded.</item>
    /// </list>
    /// </summary>
    public static class OsuDivaPlanner
    {
        private const float max_offset = OsuDivaMath.MAX_OFFSET_DEGREES;

        [ThreadStatic]
        private static PlannerStats? currentStats;
        private const float free_step = 10;
        private const double epsilon = 1e-6;

        private readonly struct Geometry
        {
            public readonly Vector2 Target;
            public readonly Vector2 Direction; // unit vector from the target towards where the note enters the screen
            public readonly float Length; // distance from the target to the screen edge along Direction
            public readonly double Time;
            public readonly double VisibleFrom;

            public Geometry(Vector2 target, Vector2 direction, float length, double time, double visibleFrom)
            {
                Target = target;
                Direction = direction;
                Length = length;
                Time = time;
                VisibleFrom = visibleFrom;
            }

            public Vector2 Entry => Target + Direction * Length;
        }

        /// <summary>A combo: consecutive notes from one that starts a combo up to the next such note.</summary>
        private sealed class Group
        {
            public readonly List<int> Members = new List<int>();
            public double SumX;
            public double SumY;
        }

        /// <summary>The absolute entry angle of every note for the direction mode, plus an optional extra angle per note.</summary>
        public static float[] AnglesFromOffsets(IReadOnlyList<PlannerNote> notes, FlightPlan plan, IReadOnlyList<float> offsets)
            => notes.Select((n, i) => OsuDivaMath.AngleOf(plan.DirectionOf(n.Target.X, offsets[i]))).ToArray();

        /// <summary>
        /// Searches for lower-cost entry angles. <paramref name="baseAngles"/> are the starting angle of every note (see <see cref="AnglesFromOffsets"/>);
        /// the result is the entry angle of every note after the search.
        /// </summary>
        public static float[] Refine(IReadOnlyList<PlannerNote> notes, IReadOnlyList<PlannerObstacle> obstacles, FlightPlan plan, IReadOnlyList<float> baseAngles, PlannerOptions options,
                                     PlannerStats? stats = null)
        {
            var previousStats = currentStats;
            currentStats = stats;
            long started = stats == null ? 0 : Stopwatch.GetTimestamp();

            try
            {
                return refine(notes, obstacles, plan, baseAngles, options, stats);
            }
            finally
            {
                currentStats = previousStats;

                if (stats != null)
                    stats.TotalTicks += Stopwatch.GetTimestamp() - started;
            }
        }

        private static float[] refine(IReadOnlyList<PlannerNote> notes, IReadOnlyList<PlannerObstacle> obstacles, FlightPlan plan, IReadOnlyList<float> baseAngles, PlannerOptions options,
                                      PlannerStats? stats)
        {
            var angles = baseAngles.ToArray();

            if (notes.Count == 0)
                return angles;

            var sortedObstacles = obstacles.OrderBy(o => o.From).ToArray();
            double maxSpan = sortedObstacles.Length == 0 ? 0 : sortedObstacles.Max(o => o.To - o.From);
            double window = flightWindowMs(notes, plan);

            var geometries = new Geometry[notes.Count];
            for (int i = 0; i < notes.Count; i++)
                geometries[i] = geometry(notes[i], angles[i], plan);

            var groups = comboGroups(notes, angles, out int[] groupOf);

            long deadline = options.TimeBudgetMs > 0 ? Stopwatch.GetTimestamp() + options.TimeBudgetMs * Stopwatch.Frequency / 1000 : long.MaxValue;
            bool outOfTime() => Stopwatch.GetTimestamp() > deadline;

            for (int pass = 0; pass < options.MaxPasses && !outOfTime(); pass++)
            {
                bool changed = false;
                int noteMoves = 0, comboTurns = 0;

                long comboFitStarted = stats == null ? 0 : Stopwatch.GetTimestamp();

                if (options.Search != PlannerSearch.Notes)
                {
                    var fitState = new GroupState(notes, sortedObstacles, maxSpan, geometries, angles, baseAngles, plan, options, window, groupOf);

                    foreach (var group in groups)
                    {
                        if (outOfTime())
                            break;

                        if (fitCombo(group, fitState))
                        {
                            changed = true;
                            comboTurns++;
                        }
                    }
                }

                if (stats != null)
                    stats.ComboTurnTicks += Stopwatch.GetTimestamp() - comboFitStarted;

                long noteSearchStarted = stats == null ? 0 : Stopwatch.GetTimestamp();
                int noteLimit = options.Search == PlannerSearch.Combos ? 0 : notes.Count;

                for (int i = 0; i < noteLimit && !outOfTime(); i++)
                {
                    var (lo, hi) = neighbours(notes, i, window);
                    var group = groups[groupOf[i]];

                    float best = angles[i];
                    var context = new Context(notes, sortedObstacles, maxSpan, geometries, angles, baseAngles, plan, options, window, lo, hi, group);
                    double bestCost = cost(i, angles[i], geometries[i], context).Total;
                    Geometry bestGeometry = geometries[i];

                    foreach (float candidate in candidates(notes[i], baseAngles[i], angles[i], plan, options, fineOnly: options.Search == PlannerSearch.CombosThenNotes).Concat(neighbourCandidates(i, notes, angles, plan, options)))
                    {
                        if (OsuDivaMath.AngleDifference(candidate, angles[i]) < 1e-3f)
                            continue;

                        var g = geometry(notes[i], candidate, plan);
                        double c = cost(i, candidate, g, context).Total;

                        if (c < bestCost - epsilon)
                        {
                            bestCost = c;
                            best = candidate;
                            bestGeometry = g;
                        }
                    }

                    if (OsuDivaMath.AngleDifference(best, angles[i]) > 1e-3f)
                    {
                        var oldUnit = OsuDivaMath.DirectionFromAngle(angles[i]);
                        var newUnit = OsuDivaMath.DirectionFromAngle(best);
                        group.SumX += newUnit.X - oldUnit.X;
                        group.SumY += newUnit.Y - oldUnit.Y;

                        angles[i] = best;
                        geometries[i] = bestGeometry;
                        changed = true;
                        noteMoves++;
                    }
                }

                long comboTurnStarted = 0;

                if (stats != null)
                {
                    comboTurnStarted = Stopwatch.GetTimestamp();
                    stats.NoteSearchTicks += comboTurnStarted - noteSearchStarted;
                }

                if (options.Search != PlannerSearch.Notes)
                    comboTurnStarted = long.MinValue; // nothing more to measure below

                // Whole combos at a time: with the notes of a combo tied to one range, a single note cannot move far on its own
                // (it would pay for every neighbour it leaves behind), so try turning the entire combo by the same angle.
                for (int g = 0; options.Search == PlannerSearch.Notes && g < groups.Count; g++)
                {
                    if (groups[g].Members.Count < 2)
                        continue;

                    var state = new GroupState(notes, sortedObstacles, maxSpan, geometries, angles, baseAngles, plan, options, window, groupOf);

                    if (turnCombo(groups[g], state))
                    {
                        changed = true;
                        comboTurns++;
                    }
                }

                if (stats != null)
                {
                    if (comboTurnStarted != long.MinValue)
                        stats.ComboTurnTicks += Stopwatch.GetTimestamp() - comboTurnStarted;

                    stats.NoteMovesPerPass.Add(noteMoves);
                    stats.ComboTurnsPerPass.Add(comboTurns);
                }

                if (!changed)
                    break;
            }

            return angles;
        }

        private sealed class GroupState
        {
            public readonly IReadOnlyList<PlannerNote> Notes;
            public readonly PlannerObstacle[] Obstacles;
            public readonly double MaxSpan;
            public readonly Geometry[] Geometries;
            public readonly float[] Angles;
            public readonly IReadOnlyList<float> BaseAngles;
            public readonly FlightPlan Plan;
            public readonly PlannerOptions Options;
            public readonly double Window;
            public readonly int[] GroupOf;

            public GroupState(IReadOnlyList<PlannerNote> notes, PlannerObstacle[] obstacles, double maxSpan, Geometry[] geometries, float[] angles,
                              IReadOnlyList<float> baseAngles, FlightPlan plan, PlannerOptions options, double window, int[] groupOf)
            {
                Notes = notes;
                Obstacles = obstacles;
                MaxSpan = maxSpan;
                Geometries = geometries;
                Angles = angles;
                BaseAngles = baseAngles;
                Plan = plan;
                Options = options;
                Window = window;
                GroupOf = groupOf;
            }
        }

        /// <summary>
        /// Fits a combo with two numbers: a centre direction and a fan width. The angle of each note is the centre direction plus the width times
        /// the note's position across the combo (seen along the flight, from the left edge of the combo, -0.5, to the right edge, +0.5),
        /// so a note to the right leans right and a note to the left leans left: an outward fan. The width is limited so that no neighbouring pair differs
        /// by more than it is allowed to, and never exceeds the combo range. Keeps the best fit if it is cheaper than the combo as it is.
        /// </summary>
        private static bool fitCombo(Group group, GroupState st)
        {
            var members = group.Members;
            int n = members.Count;
            var original = members.Select(i => st.Angles[i]).ToArray();

            var centroid = Vector2.Zero;
            foreach (int i in members)
                centroid += st.Notes[i].Target;
            centroid /= n;

            var trial = new float[n];
            var lateral = new float[n];

            double best = comboCost(group, st);
            float[]? bestAngles = null;
            float bestPhi = 0, bestFraction = 0;

            void attempt(float phi, float fraction)
            {
                fan(st, members, centroid, phi, fraction, trial, lateral);
                applyAngles(group, st, trial);

                if (currentStats != null)
                    currentStats.ComboTurnEvaluations++;

                double c = comboCost(group, st);

                if (c < best - epsilon)
                {
                    best = c;
                    bestAngles = (float[])trial.Clone();
                    bestPhi = phi;
                    bestFraction = fraction;
                }
            }

            float[] fractions = n == 1 ? new[] { 0f } : new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
            float reach = st.Options.FreeDirection ? 180 : max_offset + 25;

            for (float phi = -180 + free_step; phi <= 180; phi += free_step)
            {
                if (OsuDivaMath.AngleDifference(phi, 0) > reach)
                    continue;

                foreach (float fraction in fractions)
                    attempt(phi, fraction);
            }

            if (bestAngles != null)
            {
                // fine steps around the best centre direction, with every width
                float centre = bestPhi;

                foreach (float fine in new[] { -5f, -2f, -1f, 1f, 2f, 5f })
                    foreach (float fraction in fractions)
                        attempt(wrap(centre + fine), fraction);
            }

            if (bestAngles == null)
            {
                applyAngles(group, st, original);
                return false;
            }

            applyAngles(group, st, bestAngles);
            return true;
        }

        /// <summary>The angle of every note of a combo for a centre direction and a fraction (0..1) of the widest fan the combo may have in that direction.</summary>
        private static void fan(GroupState st, List<int> members, Vector2 centroid, float phi, float fraction, float[] output, float[] lateral)
        {
            int n = members.Count;
            double radians = phi * Math.PI / 180;
            var across = new Vector2((float)Math.Cos(radians), (float)Math.Sin(radians)); // to the right of a note entering at phi

            float maxAbs = 0;
            for (int k = 0; k < n; k++)
            {
                lateral[k] = Vector2.Dot(st.Notes[members[k]].Target - centroid, across);
                maxAbs = Math.Max(maxAbs, Math.Abs(lateral[k]));
            }

            bool spread = maxAbs > 1e-3f;
            float widest = Math.Clamp(st.Options.MaxAngleDifference, 0, 180);

            for (int k = 0; spread && k + 1 < n; k++)
            {
                float step = Math.Abs(lateral[k] - lateral[k + 1]) / (2 * maxAbs);

                if (step > 1e-6f)
                    widest = Math.Min(widest, AllowedDifference(st.Notes[members[k]], st.Notes[members[k + 1]], st.Options) / step);
            }

            float width = widest * fraction;

            for (int k = 0; k < n; k++)
            {
                float angle = wrap(phi + (spread ? width * lateral[k] / (2 * maxAbs) : 0));

                if (!st.Options.FreeDirection)
                {
                    float mode = OsuDivaMath.AngleOf(st.Plan.DirectionOf(st.Notes[members[k]].Target.X));

                    if (OsuDivaMath.AngleDifference(angle, mode) > max_offset)
                        angle = wrap(mode + Math.Sign(wrap(angle - mode)) * max_offset);
                }

                output[k] = angle;
            }
        }

        private static void applyAngles(Group group, GroupState st, float[] angles)
        {
            group.SumX = 0;
            group.SumY = 0;

            for (int k = 0; k < group.Members.Count; k++)
            {
                int i = group.Members[k];
                st.Angles[i] = angles[k];
                st.Geometries[i] = geometry(st.Notes[i], angles[k], st.Plan);

                var unit = OsuDivaMath.DirectionFromAngle(angles[k]);
                group.SumX += unit.X;
                group.SumY += unit.Y;
            }
        }

        /// <summary>Turns the whole combo by the same angle if that lowers the cost of the combo as a whole. Returns whether it did.</summary>
        private static bool turnCombo(Group group, GroupState st)
        {
            var members = group.Members;
            var original = members.Select(i => st.Angles[i]).ToArray();

            double best = comboCost(group, st);
            float bestDelta = 0;

            for (float delta = -180 + free_step; delta <= 180; delta += free_step)
            {
                if (Math.Abs(delta) < 1e-3f)
                    continue;

                if (!st.Options.FreeDirection && Math.Abs(delta) > max_offset)
                    continue;

                applyTurn(group, st, original, delta);
                double c = comboCost(group, st);

                if (currentStats != null)
                    currentStats.ComboTurnEvaluations++;

                if (c < best - epsilon)
                {
                    best = c;
                    bestDelta = delta;
                }
            }

            applyTurn(group, st, original, bestDelta);
            return Math.Abs(bestDelta) > 1e-3f;
        }

        private static void applyTurn(Group group, GroupState st, float[] original, float delta)
        {
            group.SumX = 0;
            group.SumY = 0;

            for (int k = 0; k < group.Members.Count; k++)
            {
                int i = group.Members[k];
                st.Angles[i] = wrap(original[k] + delta);
                st.Geometries[i] = geometry(st.Notes[i], st.Angles[i], st.Plan);

                var unit = OsuDivaMath.DirectionFromAngle(st.Angles[i]);
                group.SumX += unit.X;
                group.SumY += unit.Y;
            }
        }

        /// <summary>
        /// Everything that depends on the angles of the notes of one combo, counted once:
        /// each note's own terms, crossings (a crossing inside the combo once, one with another note once), and the combo terms.
        /// </summary>
        private static double comboCost(Group group, GroupState st)
        {
            var members = group.Members;
            var o = st.Options;
            double total = 0;

            foreach (int i in members)
            {
                var g = st.Geometries[i];

                total += shortPenalty(g, o);
                total += throughPenalty(i, g, st.Notes[i].Radius, st.Obstacles, st.MaxSpan, st.Plan, o, st.Window);
                total += movePenalty(st.Angles[i], st.BaseAngles[i], o);
                total += orderPenalty(i, g, st.Notes, st.Geometries, o, includeNext: false);

                var (lo, hi) = neighbours(st.Notes, i, st.Window);

                for (int j = lo; j <= hi; j++)
                {
                    if (j == i || (st.GroupOf[j] == st.GroupOf[i] && j < i))
                        continue;

                    if (crosses(g, st.Geometries[j]))
                        total += o.CrossWeight;
                }
            }

            for (int a = 0; a < members.Count; a++)
            {
                for (int b = a + 1; b < members.Count; b++)
                    total += pairPenalty(st.Angles[members[a]], st.Angles[members[b]], o) / Math.Max(1, members.Count - 1);
            }

            for (int k = 0; k + 1 < members.Count; k++)
            {
                var (excess, spread) = adjacentTerms(st.Angles[members[k]], st.Angles[members[k + 1]], st.Notes[members[k]], st.Notes[members[k + 1]], members.Count, o);
                total += excess + spread;
            }

            total += axisTerm(group.SumX, group.SumY, members.Count, o);
            return total;
        }

        /// <summary>The total cost of a plan, split by term. Every term is counted once.</summary>
        public static PlanCost Evaluate(IReadOnlyList<PlannerNote> notes, IReadOnlyList<PlannerObstacle> obstacles, FlightPlan plan, IReadOnlyList<float> angles, IReadOnlyList<float> baseAngles, PlannerOptions options)
        {
            var sortedObstacles = obstacles.OrderBy(o => o.From).ToArray();
            double maxSpan = sortedObstacles.Length == 0 ? 0 : sortedObstacles.Max(o => o.To - o.From);
            double window = flightWindowMs(notes, plan);

            var angleArray = angles.ToArray();
            var geometries = new Geometry[notes.Count];
            for (int i = 0; i < notes.Count; i++)
                geometries[i] = geometry(notes[i], angleArray[i], plan);

            var groups = comboGroups(notes, angleArray, out _);

            double shortCost = 0, through = 0, order = 0, move = 0;
            int pairs = 0;

            for (int i = 0; i < notes.Count; i++)
            {
                var (lo, hi) = neighbours(notes, i, window);

                shortCost += shortPenalty(geometries[i], options);
                through += throughPenalty(i, geometries[i], notes[i].Radius, sortedObstacles, maxSpan, plan, options, window);
                order += orderPenalty(i, geometries[i], notes, geometries, options, includeNext: false);
                move += movePenalty(angleArray[i], baseAngles[i], options);

                for (int j = Math.Max(lo, i + 1); j <= hi; j++)
                {
                    if (crosses(geometries[i], geometries[j]))
                        pairs++;
                }
            }

            double difference = 0, axis = 0, adjacent = 0, divergence = 0;

            foreach (var group in groups)
            {
                for (int k = 0; k + 1 < group.Members.Count; k++)
                {
                    var (excess, spread) = adjacentTerms(angleArray[group.Members[k]], angleArray[group.Members[k + 1]], notes[group.Members[k]], notes[group.Members[k + 1]], group.Members.Count, options);
                    adjacent += excess;
                    divergence += spread;
                }

                for (int a = 0; a < group.Members.Count; a++)
                {
                    for (int b = a + 1; b < group.Members.Count; b++)
                        difference += pairPenalty(angleArray[group.Members[a]], angleArray[group.Members[b]], options) / Math.Max(1, group.Members.Count - 1);
                }

                axis += axisTerm(group.SumX, group.SumY, group.Members.Count, options);
            }

            return new PlanCost
            {
                Short = shortCost,
                Through = through,
                Crossing = pairs * options.CrossWeight,
                Order = order,
                Move = move,
                AngleDifference = difference,
                AxisAlignment = axis,
                AdjacentDifference = adjacent,
                Divergence = divergence,
                CrossingPairs = pairs,
            };
        }

        /// <summary>
        /// The cost of every note on its own: its time on screen, its flight through other objects, its entry order and how far it moved,
        /// and the number of crossings it is part of (a crossing counts for both notes). The combo terms are not split by note.
        /// For measuring where the cost of a plan comes from.
        /// </summary>
        public static PlanCost[] EvaluatePerNote(IReadOnlyList<PlannerNote> notes, IReadOnlyList<PlannerObstacle> obstacles, FlightPlan plan, IReadOnlyList<float> angles, IReadOnlyList<float> baseAngles,
                                                 PlannerOptions options)
        {
            var sortedObstacles = obstacles.OrderBy(o => o.From).ToArray();
            double maxSpan = sortedObstacles.Length == 0 ? 0 : sortedObstacles.Max(o => o.To - o.From);
            double window = flightWindowMs(notes, plan);

            var angleArray = angles.ToArray();
            var geometries = new Geometry[notes.Count];
            for (int i = 0; i < notes.Count; i++)
                geometries[i] = geometry(notes[i], angleArray[i], plan);

            var result = new PlanCost[notes.Count];

            for (int i = 0; i < notes.Count; i++)
            {
                var (lo, hi) = neighbours(notes, i, window);
                int pairs = 0;

                for (int j = lo; j <= hi; j++)
                {
                    if (j != i && crosses(geometries[i], geometries[j]))
                        pairs++;
                }

                result[i] = new PlanCost
                {
                    Short = shortPenalty(geometries[i], options),
                    Through = throughPenalty(i, geometries[i], notes[i].Radius, sortedObstacles, maxSpan, plan, options, window),
                    Order = orderPenalty(i, geometries[i], notes, geometries, options, includeNext: false),
                    Move = movePenalty(angleArray[i], baseAngles[i], options),
                    CrossingPairs = pairs,
                    Crossing = pairs * options.CrossWeight,
                };
            }

            return result;
        }

        private readonly struct Context
        {
            public readonly IReadOnlyList<PlannerNote> Notes;
            public readonly PlannerObstacle[] Obstacles;
            public readonly double MaxSpan;
            public readonly Geometry[] Geometries;
            public readonly float[] Angles;
            public readonly IReadOnlyList<float> BaseAngles;
            public readonly FlightPlan Plan;
            public readonly PlannerOptions Options;
            public readonly double Window;
            public readonly int Lo;
            public readonly int Hi;
            public readonly Group Group;

            public Context(IReadOnlyList<PlannerNote> notes, PlannerObstacle[] obstacles, double maxSpan, Geometry[] geometries, float[] angles, IReadOnlyList<float> baseAngles,
                           FlightPlan plan, PlannerOptions options, double window, int lo, int hi, Group group)
            {
                Notes = notes;
                Obstacles = obstacles;
                MaxSpan = maxSpan;
                Geometries = geometries;
                Angles = angles;
                BaseAngles = baseAngles;
                Plan = plan;
                Options = options;
                Window = window;
                Lo = lo;
                Hi = hi;
                Group = group;
            }
        }

        /// <summary>Everything that changes when note <paramref name="i"/> flies in at <paramref name="angle"/> instead.</summary>
        private static PlanCost cost(int i, float angle, Geometry g, Context c)
        {
            var stats = currentStats;
            long t = stats == null ? 0 : Stopwatch.GetTimestamp();

            if (stats != null)
                stats.NoteEvaluations++;

            int pairs = 0;

            for (int j = c.Lo; j <= c.Hi; j++)
            {
                if (j != i && crosses(g, c.Geometries[j]))
                    pairs++;
            }

            if (stats != null)
            {
                long now = Stopwatch.GetTimestamp();
                stats.CrossingTicks += now - t;
                t = now;
            }

            // combo terms: the pairs this note forms with the rest of its combo, and the combo's average direction with this note's new direction.
            var members = c.Group.Members;
            double difference = 0;

            foreach (int j in members)
            {
                if (j != i)
                    difference += pairPenalty(angle, c.Angles[j], c.Options) / Math.Max(1, members.Count - 1);
            }

            // the pairs this note forms with its neighbours in the combo (hit order): how far apart they may fly in, and whether they fan outwards.
            double adjacent = 0, divergence = 0;

            if (!c.Notes[i].StartsCombo && i > 0)
            {
                var (excess, spread) = adjacentTerms(c.Angles[i - 1], angle, c.Notes[i - 1], c.Notes[i], members.Count, c.Options);
                adjacent += excess;
                divergence += spread;
            }

            if (i + 1 < c.Notes.Count && !c.Notes[i + 1].StartsCombo)
            {
                var (excess, spread) = adjacentTerms(angle, c.Angles[i + 1], c.Notes[i], c.Notes[i + 1], members.Count, c.Options);
                adjacent += excess;
                divergence += spread;
            }

            var oldUnit = OsuDivaMath.DirectionFromAngle(c.Angles[i]);
            var newUnit = OsuDivaMath.DirectionFromAngle(angle);
            double axis = axisTerm(c.Group.SumX + newUnit.X - oldUnit.X, c.Group.SumY + newUnit.Y - oldUnit.Y, members.Count, c.Options);

            if (stats != null)
            {
                long now = Stopwatch.GetTimestamp();
                stats.ComboTicks += now - t;
                t = now;
            }

            double shortCost = shortPenalty(g, c.Options);

            if (stats != null)
            {
                long now = Stopwatch.GetTimestamp();
                stats.ShortTicks += now - t;
                t = now;
            }

            double through = throughPenalty(i, g, c.Notes[i].Radius, c.Obstacles, c.MaxSpan, c.Plan, c.Options, c.Window);

            if (stats != null)
            {
                long now = Stopwatch.GetTimestamp();
                stats.ThroughTicks += now - t;
                t = now;
            }

            double order = orderPenalty(i, g, c.Notes, c.Geometries, c.Options, includeNext: true);

            if (stats != null)
                stats.OrderTicks += Stopwatch.GetTimestamp() - t;

            return new PlanCost
            {
                Short = shortCost,
                Through = through,
                Crossing = pairs * c.Options.CrossWeight,
                Order = order,
                Move = movePenalty(angle, c.BaseAngles[i], c.Options),
                AngleDifference = difference,
                AxisAlignment = axis,
                AdjacentDifference = adjacent,
                Divergence = divergence,
                CrossingPairs = pairs,
            };
        }

        private static List<Group> comboGroups(IReadOnlyList<PlannerNote> notes, float[] angles, out int[] groupOf)
        {
            var groups = new List<Group>();
            groupOf = new int[notes.Count];

            for (int i = 0; i < notes.Count; i++)
            {
                if (groups.Count == 0 || notes[i].StartsCombo)
                    groups.Add(new Group());

                var group = groups[^1];
                group.Members.Add(i);

                var unit = OsuDivaMath.DirectionFromAngle(angles[i]);
                group.SumX += unit.X;
                group.SumY += unit.Y;
                groupOf[i] = groups.Count - 1;
            }

            return groups;
        }

        /// <summary>
        /// How much two neighbouring notes of a combo may differ in entry direction, in degrees: the combo limit when they are far apart in space
        /// and time, less when they are closer or faster (never less than 1 degree, or the limit itself when that is smaller).
        /// </summary>
        public static float AllowedDifference(PlannerNote first, PlannerNote second, PlannerOptions options)
        {
            float limit = Math.Clamp(options.MaxAngleDifference, 0, 180);
            float distance = Vector2.Distance(first.Target, second.Target);
            double interval = Math.Abs(second.Time - first.Time);

            float byDistance = options.AdjacentFullDistance <= 0 ? 1 : Math.Min(1, distance / options.AdjacentFullDistance);
            float byTime = options.AdjacentFullIntervalMs <= 0 ? 1 : (float)Math.Min(1, interval / options.AdjacentFullIntervalMs);

            return Math.Max(Math.Min(1, limit), limit * byDistance * byTime);
        }

        /// <summary>
        /// The two terms for a pair of neighbouring notes (first hit first):
        /// a penalty when their entry directions differ more than <see cref="AllowedDifference"/>,
        /// and an outward-fan term: negative (a reward) when their flight lines move apart on the way from the targets to the screen edge,
        /// positive (a penalty) when they move towards each other. The fan term saturates at the smaller of the allowed difference and
        /// this pair's share of the combo range (range divided by the number of neighbouring pairs).
        /// </summary>
        private static (double excess, double divergence) adjacentTerms(float firstAngle, float secondAngle, PlannerNote first, PlannerNote second, int comboSize, PlannerOptions options)
        {
            float allowed = AllowedDifference(first, second, options);
            float difference = OsuDivaMath.AngleDifference(firstAngle, secondAngle);

            double over = Math.Max(0, difference - allowed) / Math.Max(allowed, 5);
            double excess = options.AngleDifferenceWeight * Math.Min(9, over * over);

            double divergence = 0;
            var between = second.Target - first.Target;
            float length = between.Length;

            if (options.DivergenceWeight > 0 && length > 1e-3f)
            {
                var a = OsuDivaMath.DirectionFromAngle(firstAngle);
                var b = OsuDivaMath.DirectionFromAngle(secondAngle);

                // from a target towards where its note enters the screen: (dx, -dy)
                var apart = new Vector2(b.X - a.X, -(b.Y - a.Y));

                // positive: the distance between the two lines grows with the distance from the targets.
                float spreading = Vector2.Dot(between / length, apart);
                // An outward fan down a whole combo adds up: n notes with steps of s span (n - 1) * s. To keep the combo within its range the reward
                // is full at the pair's share of the range, so the fan of a long combo is narrower per pair than the fan of a pair.
                float share = options.MaxAngleDifference / Math.Max(1, comboSize - 1);
                double allowedRadians = Math.Max(0.5f, Math.Min(allowed, share)) * Math.PI / 180;

                divergence = -options.DivergenceWeight * Math.Clamp(spreading / allowedRadians, -1, 1);
            }

            return (excess, divergence);
        }

        /// <summary>Penalty of one pair of notes of a combo: zero up to the limit, then growing with the square of the excess.</summary>
        private static double pairPenalty(float a, float b, PlannerOptions options)
        {
            float limit = Math.Clamp(options.MaxAngleDifference, 0, 179.9f);
            float difference = OsuDivaMath.AngleDifference(a, b);

            if (difference <= limit)
                return 0;

            // relative to the limit itself (at least 10 degrees): twice the limit away costs the weight, three times costs four times, capped at nine times
            double excess = (difference - limit) / Math.Max(limit, 10);
            return options.AngleDifferenceWeight * Math.Min(9, excess * excess);
        }

        /// <summary>
        /// Reward for a combo: the length of the average of its notes' entry directions (1 when all agree, 0 when they cancel out)
        /// times how close that average points to horizontal or vertical (1 on an axis, 0 on a diagonal).
        /// </summary>
        private static double axisTerm(double sumX, double sumY, int count, PlannerOptions options)
        {
            if (count == 0 || options.AxisAlignmentWeight == 0)
                return 0;

            double meanX = sumX / count;
            double meanY = sumY / count;
            double length = Math.Sqrt(meanX * meanX + meanY * meanY);

            if (length < 1e-6)
                return 0;

            float angle = OsuDivaMath.AngleOf(new Vector2((float)meanX, (float)meanY));
            float fromAxis = Math.Min(Math.Min(OsuDivaMath.AngleDifference(angle, 0), OsuDivaMath.AngleDifference(angle, 90)),
                Math.Min(OsuDivaMath.AngleDifference(angle, 180), OsuDivaMath.AngleDifference(angle, -90)));

            return -options.AxisAlignmentWeight * Math.Min(1, length) * (1 - fromAxis / 45);
        }

        private static double movePenalty(float angle, float baseAngle, PlannerOptions options)
            => options.MoveWeight * OsuDivaMath.AngleDifference(angle, baseAngle) / 90;

        private static IEnumerable<float> candidates(PlannerNote note, float baseAngle, float current, FlightPlan plan, PlannerOptions options, bool fineOnly = false)
        {
            if (fineOnly)
            {
                // small adjustments of a note that already flies with its combo: a few degrees either way, never a jump to another side.
                foreach (float fine in new[] { -5f, -2f, -1f, 1f, 2f, 5f })
                {
                    float candidate = wrap(current + fine);

                    if (options.FreeDirection || OsuDivaMath.AngleDifference(candidate, OsuDivaMath.AngleOf(plan.DirectionOf(note.Target.X))) <= max_offset)
                        yield return candidate;
                }

                yield break;
            }

            yield return baseAngle;

            if (options.FreeDirection)
            {
                // every side, in coarse steps, and a few fine steps around where the note is now.
                for (float a = -180 + free_step; a <= 180; a += free_step)
                    yield return a;

                foreach (float fine in new[] { -5f, -2f, -1f, 1f, 2f, 5f })
                    yield return wrap(current + fine);
            }
            else
            {
                // only turn within 75 degrees of the direction mode direction.
                for (float offset = -max_offset; offset <= max_offset + 1e-3f; offset += 5)
                    yield return OsuDivaMath.AngleOf(plan.DirectionOf(note.Target.X, offset));
            }

            yield return current;
        }

        /// <summary>
        /// Angles relative to the neighbours in the combo: the neighbour's angle plus or minus the allowed difference (and half of it).
        /// The best angle of a note depends on its neighbours, and one-note-at-a-time search cannot find it on a fixed grid.
        /// </summary>
        private static IEnumerable<float> neighbourCandidates(int i, IReadOnlyList<PlannerNote> notes, float[] angles, FlightPlan plan, PlannerOptions options)
        {
            var neighbours = new List<int>(2);

            if (!notes[i].StartsCombo && i > 0)
                neighbours.Add(i - 1);

            if (i + 1 < notes.Count && !notes[i + 1].StartsCombo)
                neighbours.Add(i + 1);

            float modeAngle = OsuDivaMath.AngleOf(plan.DirectionOf(notes[i].Target.X));

            foreach (int j in neighbours)
            {
                float allowed = AllowedDifference(notes[Math.Min(i, j)], notes[Math.Max(i, j)], options);

                foreach (float delta in new[] { allowed, -allowed, allowed / 2, -allowed / 2 })
                {
                    float candidate = wrap(angles[j] + delta);

                    // without the free direction rule a note stays within 75 degrees of the direction mode's own direction.
                    if (!options.FreeDirection && OsuDivaMath.AngleDifference(candidate, modeAngle) > max_offset)
                        continue;

                    yield return candidate;
                }
            }
        }

        private static float wrap(float degrees)
        {
            while (degrees > 180) degrees -= 360;
            while (degrees <= -180) degrees += 360;
            return degrees;
        }

        private static Geometry geometry(PlannerNote note, float angle, FlightPlan plan)
        {
            var d = OsuDivaMath.DirectionFromAngle(angle);
            double visibleFrom = OsuDivaMath.EntryTime(note.Target.X, note.Target.Y, note.Time, d, plan.Speed, note.Radius * 2, plan.Screen);
            float length = (float)(plan.Speed * (note.Time - visibleFrom) / 1000);
            return new Geometry(note.Target, new Vector2(d.X, -d.Y), Math.Max(0, length), note.Time, visibleFrom);
        }

        /// <summary>The longest a note can be on screen, plus some margin: notes further apart in time cannot affect each other.</summary>
        private static double flightWindowMs(IReadOnlyList<PlannerNote> notes, FlightPlan plan)
        {
            float diameter = notes.Count == 0 ? 0 : notes.Max(n => n.Radius) * 2;
            double diagonal = Math.Sqrt(plan.Screen.Width * plan.Screen.Width + plan.Screen.Height * plan.Screen.Height) + 2 * diameter;
            return diagonal / Math.Max(1, plan.Speed) * 1000 + 200;
        }

        private static (int lo, int hi) neighbours(IReadOnlyList<PlannerNote> notes, int i, double window)
        {
            int lo = i;
            while (lo > 0 && notes[i].Time - notes[lo - 1].Time <= window) lo--;

            int hi = i;
            while (hi < notes.Count - 1 && notes[hi + 1].Time - notes[i].Time <= window) hi++;

            return (lo, hi);
        }

        /// <summary>Penalty 1: on screen for too short a time.</summary>
        private static double shortPenalty(Geometry g, PlannerOptions options)
        {
            double visible = g.Time - g.VisibleFrom;

            if (visible >= options.MinVisibleMs || options.MinVisibleMs <= 0)
                return 0;

            double missing = (options.MinVisibleMs - Math.Max(0, visible)) / options.MinVisibleMs;
            return options.ShortWeight * missing * missing;
        }

        /// <summary>
        /// Penalty 2: the flight line passes through an object that is on screen at that moment.
        /// Solves |target + direction * s - centre| &lt; R along the flight (s = distance from the target) and
        /// keeps only the part where the object is visible; the penalty grows with the time spent inside.
        /// </summary>
        private static double throughPenalty(int owner, Geometry g, float radius, PlannerObstacle[] obstacles, double maxSpan, FlightPlan plan, PlannerOptions options, double window)
        {
            if (obstacles.Length == 0 || g.Length <= 0)
                return 0;

            double total = 0;
            int start = lowerBound(obstacles, g.VisibleFrom - maxSpan - 1);

            for (int k = start; k < obstacles.Length && obstacles[k].From <= g.Time; k++)
            {
                var o = obstacles[k];

                if (o.Owner == owner || o.To < g.VisibleFrom)
                    continue;

                float reach = (radius + o.Radius) * options.OverlapFactor;
                var a = g.Target - o.Center;
                float b = Vector2.Dot(a, g.Direction);
                float c = a.LengthSquared - reach * reach;
                float discriminant = b * b - c;

                if (discriminant <= 0)
                    continue;

                float root = (float)Math.Sqrt(discriminant);

                // s measured from the target; time t(s) = g.Time - s / speed, so the object is visible for s in [sFrom, sTo].
                double sFrom = plan.Speed * (g.Time - o.To) / 1000;
                double sTo = plan.Speed * (g.Time - o.From) / 1000;

                double lo = Math.Max(Math.Max(-b - root, 0), sFrom);
                double hi = Math.Min(Math.Min(-b + root, g.Length), sTo);

                if (hi <= lo)
                    continue;

                double ms = (hi - lo) * 1000 / plan.Speed;
                total += options.ThroughWeight * Math.Min(1, ms / options.ThroughFullMs);
            }

            return total;
        }

        private static int lowerBound(PlannerObstacle[] obstacles, double from)
        {
            int lo = 0, hi = obstacles.Length;

            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (obstacles[mid].From < from) lo = mid + 1;
                else hi = mid;
            }

            return lo;
        }

        /// <summary>
        /// Penalty 3: the segments from entering the screen to the target cross.
        /// Only pairs that are on screen at the same time count: otherwise nobody could see the lines cross.
        /// </summary>
        private static bool crosses(Geometry a, Geometry b)
        {
            if (a.VisibleFrom >= b.Time || b.VisibleFrom >= a.Time)
                return false;

            return segmentsCross(a.Target, a.Entry, b.Target, b.Entry);
        }

        /// <summary>Proper crossing of two segments. Touching at an end point (for example flights that converge on the same spot) does not count.</summary>
        private static bool segmentsCross(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2)
        {
            var r = p2 - p;
            var s = q2 - q;
            float denominator = r.X * s.Y - r.Y * s.X;

            if (Math.Abs(denominator) < 1e-6f)
                return false;

            var qp = q - p;
            float t = (qp.X * s.Y - qp.Y * s.X) / denominator;
            float u = (qp.X * r.Y - qp.Y * r.X) / denominator;

            const float margin = 0.02f;
            return t > margin && t < 1 - margin && u > margin && u < 1 - margin;
        }

        /// <summary>
        /// Within a combo the notes should enter the screen in hit order.
        /// The pair (i - 1, i) is always looked at; the pair (i, i + 1) only when asked, so a total over all notes counts every pair once
        /// while the cost of changing one note still covers both of its pairs.
        /// </summary>
        private static double orderPenalty(int i, Geometry g, IReadOnlyList<PlannerNote> notes, Geometry[] geometries, PlannerOptions options, bool includeNext)
        {
            double penalty = 0;

            if (!notes[i].StartsCombo && i > 0 && g.VisibleFrom < geometries[i - 1].VisibleFrom)
                penalty += options.OrderWeight * Math.Min(1, (geometries[i - 1].VisibleFrom - g.VisibleFrom) / 100);

            if (includeNext && i + 1 < notes.Count && !notes[i + 1].StartsCombo && g.VisibleFrom > geometries[i + 1].VisibleFrom)
                penalty += options.OrderWeight * Math.Min(1, (g.VisibleFrom - geometries[i + 1].VisibleFrom) / 100);

            return penalty;
        }
    }
}
