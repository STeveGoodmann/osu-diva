using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.DivaFlight.Configuration;
using osu.Game.Rulesets.DivaFlight.Localisation;
using osu.Game.Rulesets.Osu.UI;

namespace osu.Game.Rulesets.DivaFlight.UI
{
    /// <summary>
    /// The osu! ruleset options followed by the flight options.
    /// </summary>
    public partial class DivaFlightSettingsSubsection : OsuSettingsSubsection
    {
        public DivaFlightSettingsSubsection(Ruleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var diva = ((DivaFlightRulesetConfigManager)Config).Diva;

            AddRange(new Drawable[]
            {
                slider(DivaStrings.FlightSpeed, DivaStrings.FlightSpeedHint, diva.GetBindable<float>(DivaFlightSetting.FlightSpeed), 10),
                slider(DivaStrings.NoteSize, null, diva.GetBindable<float>(DivaFlightSetting.NoteScale), 0.05f, percentage: true),
                new SettingsItemV2(new FormEnumDropdown<DivaFlightDirection>
                {
                    Caption = DivaStrings.FlightDirection,
                    HintText = DivaStrings.FlightDirectionHint,
                    Current = diva.GetBindable<DivaFlightDirection>(DivaFlightSetting.FlightDirection),
                }),
                slider(DivaStrings.FanAngle, DivaStrings.FanAngleHint, diva.GetBindable<float>(DivaFlightSetting.FanAngle), 1),
                check(DivaStrings.ShowNumbers, null, diva.GetBindable<bool>(DivaFlightSetting.ShowNumbers)),
                check(DivaStrings.ShowTrail, null, diva.GetBindable<bool>(DivaFlightSetting.ShowTrail)),
                check(DivaStrings.ShowSliderBody, DivaStrings.ShowSliderBodyHint, diva.GetBindable<bool>(DivaFlightSetting.ShowSliderBody)),
                slider(DivaStrings.ApproachCircleVisibility, DivaStrings.ApproachCircleVisibilityHint, diva.GetBindable<float>(DivaFlightSetting.ApproachCircleVisibility), 0.05f, percentage: true),
                check(DivaStrings.OptimiseTrajectories, DivaStrings.OptimiseTrajectoriesHint, diva.GetBindable<bool>(DivaFlightSetting.OptimiseTrajectories)),
                slider(DivaStrings.MinOnScreenTime, DivaStrings.MinOnScreenTimeHint, diva.GetBindable<float>(DivaFlightSetting.MinOnScreenTime), 50),
                slider(DivaStrings.OnScreenTimeStrength, DivaStrings.OnScreenTimeStrengthHint, diva.GetBindable<float>(DivaFlightSetting.OnScreenTimeStrength), 1),
                check(DivaStrings.FreeEntryDirection, DivaStrings.FreeEntryDirectionHint, diva.GetBindable<bool>(DivaFlightSetting.FreeEntryDirection)),
                new SettingsItemV2(new FormEnumDropdown<PlannerSearch>
                {
                    Caption = DivaStrings.OptimiserSearch,
                    HintText = DivaStrings.OptimiserSearchHint,
                    Current = diva.GetBindable<PlannerSearch>(DivaFlightSetting.SearchMethod),
                }),
                slider(DivaStrings.ComboAngleRange, DivaStrings.ComboAngleRangeHint, diva.GetBindable<float>(DivaFlightSetting.MaxComboAngleDifference), 1),
                slider(DivaStrings.FullAngleRangeDistance, DivaStrings.FullAngleRangeDistanceHint, diva.GetBindable<float>(DivaFlightSetting.AdjacentFullDistance), 10),
                slider(DivaStrings.FullAngleRangeInterval, DivaStrings.FullAngleRangeIntervalHint, diva.GetBindable<float>(DivaFlightSetting.AdjacentFullInterval), 10),
                slider(DivaStrings.OutwardFanStrength, DivaStrings.OutwardFanStrengthHint, diva.GetBindable<float>(DivaFlightSetting.OutwardSpreadStrength), 0.1f),
                slider(DivaStrings.ComboAnglePenalty, DivaStrings.ComboAnglePenaltyHint, diva.GetBindable<float>(DivaFlightSetting.ComboAngleDifferencePenalty), 0.5f),
                slider(DivaStrings.AxisAlignmentReward, DivaStrings.AxisAlignmentRewardHint, diva.GetBindable<float>(DivaFlightSetting.AxisAlignmentReward), 0.1f),
                new SettingsItemV2(new FormEnumDropdown<DivaFlightMaterial>
                {
                    Caption = DivaStrings.NoteMaterial,
                    Current = diva.GetBindable<DivaFlightMaterial>(DivaFlightSetting.Material),
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
