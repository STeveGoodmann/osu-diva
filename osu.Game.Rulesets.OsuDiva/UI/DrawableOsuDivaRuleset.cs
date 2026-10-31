using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.OsuDiva.Configuration;
using osu.Game.Rulesets.OsuDiva.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.UI;

namespace osu.Game.Rulesets.OsuDiva.UI
{
    /// <summary>
    /// The unmodified osu! gameplay with the flight layer added above the playfield.
    /// </summary>
    public partial class DrawableOsuDivaRuleset : DrawableOsuRuleset
    {
        private readonly ApproachCircleOpacityMod approachCircles = new ApproachCircleOpacityMod();

        /// <summary>How visible the osu! approach circles are, 0 to 1. Follows the stored setting; 25% by default.</summary>
        public Bindable<float> ApproachCircleVisibility => approachCircles.Visibility;

        public DrawableOsuDivaRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, withApproachCircleMod(mods, out var mod))
        {
            approachCircles = mod;
        }

        // The mod has to be in the list handed to the base class: lazer applies the mods of that list to every new hit object drawable.
        private static IReadOnlyList<Mod> withApproachCircleMod(IReadOnlyList<Mod>? mods, out ApproachCircleOpacityMod mod)
        {
            mod = new ApproachCircleOpacityMod();
            return (mods ?? System.Array.Empty<Mod>()).Append(mod).ToList();
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            // lazer registers the ruleset config under its runtime type (OsuDivaRulesetConfigManager), but the reused
            // osu! gameplay classes ask for OsuRulesetConfigManager. Register it under that type too.
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<OsuRulesetConfigManager>(Config);
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var diva = (base.Config as OsuDivaRulesetConfigManager)?.Diva;

            diva?.BindWith(OsuDivaSetting.ApproachCircleVisibility, approachCircles.Visibility);

            // Same coordinate space as the playfield: osu! pixels (512x384), scaled by the adjustment container.
            PlayfieldAdjustmentContainer.Add(new OsuDivaLayer(Beatmap.HitObjects, diva));
        }
    }
}
