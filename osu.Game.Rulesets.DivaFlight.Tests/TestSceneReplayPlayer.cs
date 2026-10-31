using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.DivaFlight.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.DivaFlight.Tests
{
    /// <summary>
    /// Watching a replay (or playing with Autoplay) goes through ReplayPlayer, which makes DrawableOsuRuleset add its ReplayAnalysisOverlay.
    /// That path needs OsuRulesetConfigManager and used to fail with "dependency is not registered".
    /// </summary>
    [TestFixture]
    public partial class TestSceneReplayPlayer : RateAdjustedBeatmapTestScene
    {
        protected override Ruleset CreateRuleset() => new OsuDivaRuleset();

        private TestReplayPlayer player = null!;

        [Test]
        public void TestAutoplayReplayLoadsAndShowsFlyingNotes()
        {
            AddStep("load replay player with autoplay", () =>
            {
                var ruleset = new OsuDivaRuleset();
                Ruleset.Value = ruleset.RulesetInfo;
                Beatmap.Value = CreateWorkingBeatmap(CreateBeatmap(ruleset.RulesetInfo));
                SelectedMods.Value = new[] { ruleset.GetAutoplayMod()! };

                LoadScreen(player = new TestReplayPlayer(false, false, false));
            });

            AddUntilStep("player loaded", () => player.IsLoaded && player.Alpha == 1);
            AddUntilStep("flight layer exists", () => player.ChildrenOfType<DivaFlightLayer>().Any());
            AddUntilStep("a note is flying", () => player.ChildrenOfType<DivaFlightLayer>().First().ActiveNoteCount > 0);
        }
    }
}
