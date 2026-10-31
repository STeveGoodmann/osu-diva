using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Rulesets.DivaFlight.Localisation;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Osu.Configuration;

namespace osu.Game.Rulesets.DivaFlight.Configuration
{
    public enum OsuDivaSetting
    {
        FlightSpeed,
        NoteScale,
        // Named FlightDirection rather than Direction on purpose: an old stored ComboFan under the old name would no longer parse.
        FlightDirection,
        FanAngle,
        ShowNumbers,
        ShowTrail,
        ShowSliderBody,
        // Named ApproachCircleVisibility rather than reusing ShowApproachCircles (a bool): an old stored True/False would no longer parse as a number.
        ApproachCircleVisibility,
        OptimiseTrajectories,
        MinOnScreenTime,
        OnScreenTimeStrength,
        FreeEntryDirection,
        SearchMethod,
        MaxComboAngleDifference,
        AdjacentFullDistance,
        AdjacentFullInterval,
        OutwardSpreadStrength,
        ComboAngleDifferencePenalty,
        AxisAlignmentReward,
        Material,
    }

    public enum OsuDivaMaterial
    {
        [LocalisableDescription(typeof(DivaStrings), nameof(DivaStrings.MaterialClear))]
        Clear,

        [LocalisableDescription(typeof(DivaStrings), nameof(DivaStrings.MaterialSoft))]
        Soft,
    }

    /// <summary>
    /// Settings of the flight layer. Stored under their own variant so they never mix with the osu! ruleset's settings.
    /// </summary>
    public class OsuDivaConfigManager : RulesetConfigManager<OsuDivaSetting>
    {
        public const int VARIANT = 1;

        public OsuDivaConfigManager(SettingsStore? settings, RulesetInfo ruleset)
            : base(settings, ruleset, VARIANT)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            // Flight speed in playfield units (osu! pixels) per second. 400 equals the prototype's 900 px/s on a 1080p screen.
            SetDefault(OsuDivaSetting.FlightSpeed, 400f, 100f, 1200f, 10f);
            SetDefault(OsuDivaSetting.NoteScale, 0.55f, 0.2f, 2f, 0.05f);
            SetDefault(OsuDivaSetting.FlightDirection, OsuDivaDirection.Fan);
            SetDefault(OsuDivaSetting.FanAngle, 25f, 0f, 45f, 1f);
            SetDefault(OsuDivaSetting.ShowNumbers, false);
            SetDefault(OsuDivaSetting.ShowTrail, true);
            SetDefault(OsuDivaSetting.ShowSliderBody, true);
            SetDefault(OsuDivaSetting.ApproachCircleVisibility, 0.25f, 0f, 1f, 0.05f);
            SetDefault(OsuDivaSetting.OptimiseTrajectories, true);
            SetDefault(OsuDivaSetting.MinOnScreenTime, 900f, 200f, 1500f, 50f);
            SetDefault(OsuDivaSetting.OnScreenTimeStrength, 50f, 0f, 64f, 1f);
            SetDefault(OsuDivaSetting.FreeEntryDirection, true);
            SetDefault(OsuDivaSetting.SearchMethod, PlannerSearch.CombosThenNotes);
            SetDefault(OsuDivaSetting.MaxComboAngleDifference, 15f, 0f, 180f, 1f);
            SetDefault(OsuDivaSetting.AdjacentFullDistance, 150f, 0f, 400f, 10f);
            SetDefault(OsuDivaSetting.AdjacentFullInterval, 300f, 0f, 1000f, 10f);
            SetDefault(OsuDivaSetting.OutwardSpreadStrength, 1f, 0f, 5f, 0.1f);
            SetDefault(OsuDivaSetting.ComboAngleDifferencePenalty, 2f, 0f, 10f, 0.5f);
            SetDefault(OsuDivaSetting.AxisAlignmentReward, 0.3f, 0f, 3f, 0.1f);
            SetDefault(OsuDivaSetting.Material, OsuDivaMaterial.Clear);
        }
    }

    /// <summary>
    /// The config manager registered for this ruleset. It is an <see cref="OsuRulesetConfigManager"/>
    /// because the reused osu! gameplay code casts its config to that type, and it carries the flight settings alongside.
    /// </summary>
    public class OsuDivaRulesetConfigManager : OsuRulesetConfigManager
    {
        public OsuDivaConfigManager Diva { get; }

        public OsuDivaRulesetConfigManager(SettingsStore? settings, RulesetInfo ruleset)
            : base(settings, ruleset)
        {
            Diva = new OsuDivaConfigManager(settings, ruleset);
        }

        protected override void Dispose(bool isDisposing)
        {
            Diva.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
