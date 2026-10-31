using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.OsuDiva.Configuration;
using osu.Game.Rulesets.OsuDiva.Localisation;
using osu.Game.Rulesets.Osu.UI;

namespace osu.Game.Rulesets.OsuDiva.UI
{
    /// <summary>
    /// The osu! ruleset options followed by the flight options.
    /// </summary>
    public partial class OsuDivaSettingsSubsection : OsuSettingsSubsection
    {
        public OsuDivaSettingsSubsection(Ruleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var diva = ((OsuDivaRulesetConfigManager)Config).Diva;

            AddRange(new Drawable[]
            {
                slider(DivaStrings.FlightSpeed, DivaStrings.FlightSpeedHint, diva.GetBindable<float>(OsuDivaSetting.FlightSpeed), 10),
                slider(DivaStrings.NoteSize, null, diva.GetBindable<float>(OsuDivaSetting.NoteScale), 0.05f, percentage: true),
                new SettingsItemV2(new FormEnumDropdown<OsuDivaDirection>
                {
                    Caption = DivaStrings.FlightDirection,
                    HintText = DivaStrings.FlightDirectionHint,
                    Current = diva.GetBindable<OsuDivaDirection>(OsuDivaSetting.FlightDirection),
                }),
                slider(DivaStrings.FanAngle, DivaStrings.FanAngleHint, diva.GetBindable<float>(OsuDivaSetting.FanAngle), 1),
                check(DivaStrings.ShowNumbers, null, diva.GetBindable<bool>(OsuDivaSetting.ShowNumbers)),
                check(DivaStrings.ShowTrail, null, diva.GetBindable<bool>(OsuDivaSetting.ShowTrail)),
                check(DivaStrings.ShowSliderBody, DivaStrings.ShowSliderBodyHint, diva.GetBindable<bool>(OsuDivaSetting.ShowSliderBody)),
                slider(DivaStrings.ApproachCircleVisibility, DivaStrings.ApproachCircleVisibilityHint, diva.GetBindable<float>(OsuDivaSetting.ApproachCircleVisibility), 0.05f, percentage: true),
                check(DivaStrings.OptimiseTrajectories, DivaStrings.OptimiseTrajectoriesHint, diva.GetBindable<bool>(OsuDivaSetting.OptimiseTrajectories)),
                slider(DivaStrings.MinOnScreenTime, DivaStrings.MinOnScreenTimeHint, diva.GetBindable<float>(OsuDivaSetting.MinOnScreenTime), 50),
                slider(DivaStrings.OnScreenTimeStrength, DivaStrings.OnScreenTimeStrengthHint, diva.GetBindable<float>(OsuDivaSetting.OnScreenTimeStrength), 1),
                check(DivaStrings.FreeEntryDirection, DivaStrings.FreeEntryDirectionHint, diva.GetBindable<bool>(OsuDivaSetting.FreeEntryDirection)),
                new SettingsItemV2(new FormEnumDropdown<PlannerSearch>
                {
                    Caption = DivaStrings.OptimiserSearch,
                    HintText = DivaStrings.OptimiserSearchHint,
                    Current = diva.GetBindable<PlannerSearch>(OsuDivaSetting.SearchMethod),
                }),
                slider(DivaStrings.ComboAngleRange, DivaStrings.ComboAngleRangeHint, diva.GetBindable<float>(OsuDivaSetting.MaxComboAngleDifference), 1),
                slider(DivaStrings.FullAngleRangeDistance, DivaStrings.FullAngleRangeDistanceHint, diva.GetBindable<float>(OsuDivaSetting.AdjacentFullDistance), 10),
                slider(DivaStrings.FullAngleRangeInterval, DivaStrings.FullAngleRangeIntervalHint, diva.GetBindable<float>(OsuDivaSetting.AdjacentFullInterval), 10),
                slider(DivaStrings.OutwardFanStrength, DivaStrings.OutwardFanStrengthHint, diva.GetBindable<float>(OsuDivaSetting.OutwardSpreadStrength), 0.1f),
                slider(DivaStrings.ComboAnglePenalty, DivaStrings.ComboAnglePenaltyHint, diva.GetBindable<float>(OsuDivaSetting.ComboAngleDifferencePenalty), 0.5f),
                slider(DivaStrings.AxisAlignmentReward, DivaStrings.AxisAlignmentRewardHint, diva.GetBindable<float>(OsuDivaSetting.AxisAlignmentReward), 0.1f),
                new SettingsItemV2(new FormEnumDropdown<OsuDivaMaterial>
                {
                    Caption = DivaStrings.NoteMaterial,
                    Current = diva.GetBindable<OsuDivaMaterial>(OsuDivaSetting.Material),
                }),
            });
        }

        private static SettingsItemV2 slider(LocalisableString caption, LocalisableString? hint, Bindable<float> current, float step, bool percentage = false)
            => new SettingsItemV2(new FormSliderBar<float>
            {
                Caption = caption,
                HintText = hint ?? default,
                Current = (BindableNumber<float>)current,
                KeyboardStep = step,
                DisplayAsPercentage = percentage,
            });

        private static SettingsItemV2 check(LocalisableString caption, LocalisableString? hint, Bindable<bool> current)
            => new SettingsItemV2(new FormCheckBox
            {
                Caption = caption,
                HintText = hint ?? default,
                Current = current,
            });
    }
}
