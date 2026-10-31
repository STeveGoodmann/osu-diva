using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Rulesets.DivaFlight.Configuration;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.DivaFlight.UI
{
    /// <summary>
    /// Draws notes that fly in from outside the screen and land on every circle, slider head, slider repeat, slider tail
    /// and spinner at its hit time. Purely visual: it reads the beatmap and the gameplay clock, and never touches judgement or input.
    /// </summary>
    public partial class DivaFlightLayer : CompositeDrawable
    {
        /// <summary>How long the arrival ring stays after the hit time.</summary>
        public const double ARRIVAL_TIME = 256;

        private readonly IReadOnlyList<OsuHitObject> hitObjects;
        private readonly DivaFlightConfigManager? config;

        private readonly Dictionary<FlightPart, FlightVisual> active = new Dictionary<FlightPart, FlightVisual>();
        private readonly List<FlightPart> expired = new List<FlightPart>();

        private List<FlightPart> parts = new List<FlightPart>();
        private double maxSpan;
        private bool dirty = true;

        private ScreenBounds screen = ScreenBounds.Default;
        private ScreenBounds pendingScreen = ScreenBounds.Default;
        private long pendingScreenSince;

        /// <summary>A resized window must stay the same size this long before the flight lines are recalculated.</summary>
        private const long screen_settle_ms = 300;

        private readonly Bindable<float> speed = new Bindable<float>(400);
        private readonly Bindable<float> noteScale = new Bindable<float>(1);
        private readonly Bindable<DivaFlightDirection> direction = new Bindable<DivaFlightDirection>(DivaFlightDirection.Fan);
        private readonly Bindable<float> fanAngle = new Bindable<float>(25);
        private readonly Bindable<bool> showNumbers = new Bindable<bool>();
        private readonly Bindable<bool> showTrail = new Bindable<bool>(true);
        private readonly Bindable<bool> showSliderBody = new Bindable<bool>(true);
        private readonly Bindable<bool> optimise = new Bindable<bool>(true);
        private readonly Bindable<float> minOnScreen = new Bindable<float>(900);
        private readonly Bindable<float> onScreenStrength = new Bindable<float>(32);
        private readonly Bindable<bool> freeDirection = new Bindable<bool>(true);
        private readonly Bindable<PlannerSearch> searchMethod = new Bindable<PlannerSearch>(PlannerSearch.CombosThenNotes);
        private readonly Bindable<float> maxAngleDifference = new Bindable<float>(15);
        private readonly Bindable<float> adjacentDistance = new Bindable<float>(150);
        private readonly Bindable<float> adjacentInterval = new Bindable<float>(300);
        private readonly Bindable<float> outwardStrength = new Bindable<float>(1);
        private readonly Bindable<float> angleDifferencePenalty = new Bindable<float>(2);
        private readonly Bindable<float> axisReward = new Bindable<float>(0.3f);
        private readonly Bindable<DivaFlightMaterial> material = new Bindable<DivaFlightMaterial>();

        private NoteTextures clear = null!;
        private NoteTextures soft = null!;
        private Texture trail = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        /// <summary>Number of notes currently on screen (flying, holding, or showing their arrival ring).</summary>
        public int ActiveNoteCount => active.Count;

        /// <summary>The screen size the flight lines are currently planned for.</summary>
        public ScreenBounds CurrentScreen => screen;

        /// <summary>Whether the layer is bound to the stored flight settings (false means built-in defaults).</summary>
        public bool UsesStoredSettings => config != null;

        /// <summary>Number of parts computed from the beatmap and the current settings.</summary>
        public int PartCount => parts.Count;

        public DivaFlightLayer(IReadOnlyList<OsuHitObject> hitObjects, DivaFlightConfigManager? config = null)
        {
            RelativeSizeAxes = Axes.Both;

            this.hitObjects = hitObjects;
            this.config = config;
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            clear = NoteTextures.Load(textures, string.Empty);
            soft = NoteTextures.Load(textures, "soft/");
            trail = textures.Get("trail-core")!;

            if (config != null)
            {
                config.BindWith(DivaFlightSetting.FlightSpeed, speed);
                config.BindWith(DivaFlightSetting.NoteScale, noteScale);
                config.BindWith(DivaFlightSetting.FlightDirection, direction);
                config.BindWith(DivaFlightSetting.FanAngle, fanAngle);
                config.BindWith(DivaFlightSetting.ShowNumbers, showNumbers);
                config.BindWith(DivaFlightSetting.ShowTrail, showTrail);
                config.BindWith(DivaFlightSetting.ShowSliderBody, showSliderBody);
                config.BindWith(DivaFlightSetting.OptimiseTrajectories, optimise);
                config.BindWith(DivaFlightSetting.MinOnScreenTime, minOnScreen);
                config.BindWith(DivaFlightSetting.OnScreenTimeStrength, onScreenStrength);
                config.BindWith(DivaFlightSetting.FreeEntryDirection, freeDirection);
                config.BindWith(DivaFlightSetting.SearchMethod, searchMethod);
                config.BindWith(DivaFlightSetting.MaxComboAngleDifference, maxAngleDifference);
                config.BindWith(DivaFlightSetting.AdjacentFullDistance, adjacentDistance);
                config.BindWith(DivaFlightSetting.AdjacentFullInterval, adjacentInterval);
                config.BindWith(DivaFlightSetting.OutwardSpreadStrength, outwardStrength);
                config.BindWith(DivaFlightSetting.ComboAngleDifferencePenalty, angleDifferencePenalty);
                config.BindWith(DivaFlightSetting.AxisAlignmentReward, axisReward);
                config.BindWith(DivaFlightSetting.Material, material);
            }

            watch(speed);
            watch(noteScale);
            watch(direction);
            watch(fanAngle);
            watch(showNumbers);
            watch(showTrail);
            watch(showSliderBody);
            watch(optimise);
            watch(minOnScreen);
            watch(onScreenStrength);
            watch(freeDirection);
            watch(searchMethod);
            watch(maxAngleDifference);
            watch(adjacentDistance);
            watch(adjacentInterval);
            watch(outwardStrength);
            watch(angleDifferencePenalty);
            watch(axisReward);
            watch(material);
        }

        /// <summary>
        /// A setting that is still being dragged must stop changing this long before the flights are planned again,
        /// so a slider drag does not start dozens of plans.
        /// </summary>
        private const long settings_settle_ms = 250;

        private long lastSettingChange;

        // Any setting change rebuilds the parts once the setting has settled.
        private void watch<T>(Bindable<T> bindable) => bindable.ValueChanged += _ =>
        {
            dirty = true;
            lastSettingChange = Environment.TickCount64;
        };

        private int generation;
        private (int generation, Task<List<FlightPart>> task)? building;

        /// <summary>
        /// Starts planning all flights for the current beatmap and settings. The optimiser can take a moment on dense maps,
        /// so it runs on a background thread; no notes are shown until the result is ready.
        /// </summary>
        private void rebuild()
        {
            dirty = false;

            foreach (var visual in active.Values)
                RemoveInternal(visual, true);

            active.Clear();
            parts = new List<FlightPart>();
            maxSpan = 0;

            var settings = new FlightSettings
            {
                Speed = speed.Value,
                NoteScale = noteScale.Value,
                Direction = direction.Value,
                FanAngle = fanAngle.Value,
                ShowNumbers = showNumbers.Value,
                ShowSliderBody = showSliderBody.Value,
                Screen = screen,
                Optimise = optimise.Value,
                MinVisibleMs = minOnScreen.Value,
                ShortWeight = onScreenStrength.Value,
                FreeDirection = freeDirection.Value,
                Search = searchMethod.Value,
                MaxAngleDifference = maxAngleDifference.Value,
                AdjacentFullDistance = adjacentDistance.Value,
                AdjacentFullIntervalMs = adjacentInterval.Value,
                DivergenceWeight = outwardStrength.Value,
                AngleDifferenceWeight = angleDifferencePenalty.Value,
                AxisAlignmentWeight = axisReward.Value,
            };

            var objects = hitObjects;
            int id = ++generation;

            building = (id, Task.Run(() => plan(objects, settings)));
        }

        private static List<FlightPart> plan(IReadOnlyList<OsuHitObject> objects, FlightSettings settings)
        {
            try
            {
                return FlightPartBuilder.Build(objects, settings).OrderBy(p => p.VisibleFrom).ToList();
            }
            catch (Exception e)
            {
                // A planning bug must never take gameplay down: fall back to the plan without the optimiser.
                Logger.Error(e, "DIVA Flight trajectory planning failed; using the unoptimised plan.");
                return FlightPartBuilder.Build(objects, settings.WithOptimise(false)).OrderBy(p => p.VisibleFrom).ToList();
            }
        }

        private void adoptFinishedPlan()
        {
            if (building is not { } current || !current.task.IsCompleted)
                return;

            building = null;

            // A newer rebuild may already have been started (settings changed while planning): drop this result.
            if (current.generation != generation || current.task.IsFaulted)
                return;

            parts = current.task.Result;
            maxSpan = parts.Count == 0 ? 0 : parts.Max(p => p.HoldUntil + ARRIVAL_TIME - p.VisibleFrom);
        }

        /// <summary>True while the flights are being planned.</summary>
        public bool IsPlanning => building != null;

        protected override void Update()
        {
            base.Update();

            trackScreenSize();

            if (dirty && Environment.TickCount64 - lastSettingChange >= settings_settle_ms)
                rebuild();

            adoptFinishedPlan();

            double now = Time.Current;

            expired.Clear();

            foreach (var kvp in active)
            {
                if (now < kvp.Key.VisibleFrom || now > kvp.Key.HoldUntil + ARRIVAL_TIME)
                    expired.Add(kvp.Key);
            }

            foreach (var part in expired)
            {
                RemoveInternal(active[part], true);
                active.Remove(part);
            }

            int first = lowerBound(now - maxSpan);

            for (int i = first; i < parts.Count && parts[i].VisibleFrom <= now; i++)
            {
                var part = parts[i];

                if (now > part.HoldUntil + ARRIVAL_TIME || active.ContainsKey(part))
                    continue;

                var visual = new FlightVisual(part, material.Value == DivaFlightMaterial.Soft ? soft : clear, trail,
                    ((IHasComboInformation)part.Source).GetComboColour(skin), showTrail.Value, speed.Value);

                active[part] = visual;
                AddInternal(visual);
            }

            foreach (var kvp in active)
                kvp.Value.UpdateTime(now);
        }

        /// <summary>
        /// The visible screen in this layer's coordinates (playfield units). Entry times and the fan angle depend on it,
        /// so a changed window size recalculates all flights, once it has stopped changing.
        /// </summary>
        private void trackScreenSize()
        {
            var measured = measureScreen();

            if (measured == null || measured.Value.Equals(screen))
            {
                pendingScreenSince = 0;
                return;
            }

            long now = Environment.TickCount64;

            if (pendingScreenSince == 0 || !measured.Value.Equals(pendingScreen))
            {
                pendingScreen = measured.Value;
                pendingScreenSince = now;
                return;
            }

            if (now - pendingScreenSince >= screen_settle_ms)
            {
                screen = pendingScreen;
                pendingScreenSince = 0;
                dirty = true;
            }
        }

        private ScreenBounds? measureScreen()
        {
            Drawable root = this;

            while (root.Parent != null)
                root = root.Parent;

            var quad = root.ScreenSpaceDrawQuad;
            var topLeft = ToLocalSpace(quad.TopLeft);
            var bottomRight = ToLocalSpace(quad.BottomRight);

            float width = bottomRight.X - topLeft.X;
            float height = bottomRight.Y - topLeft.Y;

            if (float.IsNaN(width) || float.IsNaN(height) || width < 200 || height < 150)
                return null;

            return new ScreenBounds(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        }

        /// <summary>First index whose <see cref="FlightPart.VisibleFrom"/> is at or after <paramref name="time"/>.</summary>
        private int lowerBound(double time)
        {
            int lo = 0, hi = parts.Count;

            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (parts[mid].VisibleFrom < time) lo = mid + 1;
                else hi = mid;
            }

            return lo;
        }
    }
}
