using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.OsuDiva.Configuration;
using osu.Game.Rulesets.OsuDiva.UI;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Replays.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.OsuDiva
{
    /// <summary>
    /// osu!standard gameplay, unchanged, plus Project DIVA style flying notes drawn on top as a reading aid.
    /// Everything except the flight layer and the identity (name, short name, icon) is delegated to <see cref="OsuRuleset"/>.
    /// The ruleset is not an <see cref="OsuRuleset"/> subclass on purpose: that would inherit its legacy ID 0.
    /// </summary>
    public class OsuDivaRuleset : Ruleset
    {
        private readonly OsuRuleset osu = new OsuRuleset();

        // The name shown in lazer. lazer refreshes the stored name from this on every start. The short name below is the Realm primary key of the
        // stored ruleset row and the key of the stored settings, scores and key bindings. It must never be changed without also renaming this
        // class: lazer finds the stored row by class name and would then try to rewrite the primary key, which fails.
        public override string Description => "osu! DIVA";

        public override string ShortName => "osudiva";

        public override string PlayingVerb => osu.PlayingVerb;

        public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null) =>
            new DrawableOsuDivaRuleset(this, beatmap, mods);

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => osu.CreateBeatmapConverter(beatmap);

        public override IBeatmapProcessor CreateBeatmapProcessor(IBeatmap beatmap) => osu.CreateBeatmapProcessor(beatmap);

        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new OsuDifficultyCalculator(RulesetInfo, beatmap);

        public override PerformanceCalculator? CreatePerformanceCalculator() => osu.CreatePerformanceCalculator();

        public override ScoreProcessor CreateScoreProcessor() => osu.CreateScoreProcessor();

        public override HealthProcessor CreateHealthProcessor(double drainStartTime) => osu.CreateHealthProcessor(drainStartTime);

        public override ScoreMultiplierCalculator CreateScoreMultiplierCalculator(ScoreMultiplierContext context) => osu.CreateScoreMultiplierCalculator(context);

        public override IEnumerable<Mod> GetModsFor(ModType type) => osu.GetModsFor(type);

        public override IEnumerable<Mod> ConvertFromLegacyMods(LegacyMods mods) => osu.ConvertFromLegacyMods(mods);

        public override LegacyMods ConvertToLegacyMods(Mod[] mods) => osu.ConvertToLegacyMods(mods);

        public override IEnumerable<KeyBinding> GetDefaultKeyBindings(int variant = 0) => osu.GetDefaultKeyBindings(variant);

        public override ISkin? CreateSkinTransformer(ISkin skin, IBeatmap beatmap) => osu.CreateSkinTransformer(skin, beatmap);

        public override IRulesetConfigManager CreateConfig(SettingsStore? settings) => new OsuDivaRulesetConfigManager(settings, RulesetInfo);

        public override RulesetSettingsSubsection CreateSettings() => new OsuDivaSettingsSubsection(this);

        public override HitObjectComposer CreateHitObjectComposer() => new OsuHitObjectComposer(this);

        public override IBeatmapVerifier CreateBeatmapVerifier() => osu.CreateBeatmapVerifier();

        public override IConvertibleReplayFrame CreateConvertibleReplayFrame() => osu.CreateConvertibleReplayFrame();

        public override IEnumerable<HitResult> GetValidHitResults() => osu.GetValidHitResults();

        public override LocalisableString GetDisplayNameForHitResult(HitResult result) => osu.GetDisplayNameForHitResult(result);

        public override StatisticItem[] CreateStatisticsForScore(ScoreInfo score, IBeatmap playableBeatmap) => osu.CreateStatisticsForScore(score, playableBeatmap);

        public override IEnumerable<Drawable> CreateEditorSetupSections() => osu.CreateEditorSetupSections();

        public override BeatmapDifficulty GetAdjustedDisplayDifficulty(IBeatmapInfo difficulty, IReadOnlyCollection<Mod> mods) =>
            osu.GetAdjustedDisplayDifficulty(difficulty, mods);

        public override IEnumerable<RulesetBeatmapAttribute> GetBeatmapAttributesForDisplay(IBeatmapInfo beatmapInfo, IReadOnlyCollection<Mod> mods) =>
            osu.GetBeatmapAttributesForDisplay(beatmapInfo, mods);

        public override bool EditorShowScrollSpeed => osu.EditorShowScrollSpeed;

        public override Drawable CreateIcon() => new OsuDivaRulesetIcon(this);

        /// <summary>
        /// Our textures first, then whatever resources the osu! ruleset ships (its skin lookups still expect them).
        /// </summary>
        public override IResourceStore<byte[]> CreateResourceStore()
        {
            var store = new ResourceStore<byte[]>(base.CreateResourceStore());
            store.AddStore(osu.CreateResourceStore());
            return store;
        }
    }
}
