using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Rulesets.DivaFlight.Configuration;
using osu.Game.Rulesets.DivaFlight.UI;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.DivaFlight.Tests
{
    [TestFixture]
    public partial class TestSceneOsuPlayer : PlayerTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new OsuDivaRuleset();

        private DivaFlightLayer layer => this.ChildrenOfType<DivaFlightLayer>().First();

        private DrawableDivaFlightRuleset drawableRuleset => this.ChildrenOfType<DrawableDivaFlightRuleset>().First();

        /// <summary>
        /// The approach circle visibility lives in the stored config, which all tests of a run share, and it only applies to circles that appear
        /// after it was set. So it is written before the player is loaded: a test with a number as its argument gets that number,
        /// every other test gets the default, and all circles of the beatmap are treated the same from the first one on.
        /// </summary>
        public override void SetUpSteps()
        {
            AddStep("store the approach circle visibility this test needs", () =>
            {
                float wanted = TestContext.CurrentContext.Test.Arguments.Length > 0 && TestContext.CurrentContext.Test.Arguments[0] is float f ? f : 0.25f;
                var config = (DivaFlightRulesetConfigManager)Dependencies.Get<IRulesetConfigCache>().GetConfigFor(new OsuDivaRuleset())!;
                config.Diva.SetValue(DivaFlightSetting.ApproachCircleVisibility, wanted);
            });

            base.SetUpSteps();
        }

        private static double progressOf(DrawableHitCircle c) => (c.Time.Current - (c.HitObject.StartTime - c.HitObject.TimePreempt)) / c.HitObject.TimePreempt;

        /// <summary>Circles (and slider heads) part way through their approach.</summary>
        private System.Collections.Generic.List<DrawableHitCircle> circlesBetween(double from, double to) => this.ChildrenOfType<DrawableHitCircle>().Where(c =>
            c.IsAlive && progressOf(c) > from && progressOf(c) < to).ToList();

        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void TestApproachCircleStartsSmallerAndShrinksToTheCircle(float visibility)
        {
            AddAssert("the setting is what the test stored", () => System.Math.Abs(drawableRuleset.ApproachCircleVisibility.Value - visibility) < 1e-6f);
            AddUntilStep("a circle is part way through its approach", () => circlesBetween(0.2, 0.9).Any());

            // 1 + 3 * visibility times the circle at the start of the approach (osu!'s own 4 at 100%), shrinking evenly to 1.
            AddAssert("the ring is exactly as big as that, for every such circle", () => circlesBetween(0.2, 0.9).All(c =>
                System.Math.Abs(c.ApproachCircle.Scale.X - (1 + 3 * visibility * (1 - (float)progressOf(c)))) < 0.04f));

            AddAssert("and no more visible than visibility * osu!'s 90%", () => circlesBetween(0.2, 0.9).All(c => c.ApproachCircle.Alpha <= 0.9f * visibility + 0.01f));
            AddUntilStep("while it is visible", () => circlesBetween(0.2, 0.9).Any(c => c.ApproachCircle.Alpha > 0.3f * 0.9f * visibility));
        }

        [TestCase(0.25f)]
        public void TestApproachCircleEndsAtTheSizeOfTheCircle(float visibility)
        {
            AddUntilStep("a circle is about to be hit", () => circlesBetween(0.93, 0.99).Any());
            AddAssert("its ring is (almost) the size of the circle", () => circlesBetween(0.93, 0.99).All(c => c.ApproachCircle.Scale.X < 1.1f && c.ApproachCircle.Scale.X >= 0.98f));
        }

        [TestCase(0f)]
        public void TestApproachCirclesCanBeHidden(float visibility)
        {
            AddUntilStep("a circle is part way through its approach", () => circlesBetween(0.2, 0.9).Any());
            AddAssert("none of them shows a ring", () => circlesBetween(0.2, 0.9).All(c => c.ApproachCircle.Alpha < 0.01f));
        }

        [Test]
        public void TestFlyingNotesAreDrawn()
        {
            AddUntilStep("flight layer exists", () => this.ChildrenOfType<DivaFlightLayer>().Any());
            AddAssert("layer is bound to the stored settings", () => layer.UsesStoredSettings);
            AddUntilStep("layer finished planning the flights", () => !layer.IsPlanning && layer.PartCount > 0);
            AddUntilStep("a note is flying", () => layer.ActiveNoteCount > 0);
        }

        [Test]
        public void TestLayerMeasuresTheRealScreen()
        {
            AddUntilStep("flight layer exists", () => this.ChildrenOfType<DivaFlightLayer>().Any());
            AddUntilStep("screen size measured", () => !layer.CurrentScreen.Equals(ScreenBounds.Default));
            AddAssert("measured screen contains the 512x384 playfield", () =>
                layer.CurrentScreen.Left < 0 && layer.CurrentScreen.Top < 0 && layer.CurrentScreen.Right > 512 && layer.CurrentScreen.Bottom > 384);
        }

        /// <summary>
        /// osu!'s own gameplay classes (ReplayAnalysisOverlay, OsuPlayfield, ...) ask for the config by its osu! type.
        /// Regression test for "ReplayAnalysisOverlay has a dependency on OsuRulesetConfigManager, but the dependency is not registered".
        /// </summary>
        [Test]
        public void TestOsuGameplayCodeCanResolveItsConfig()
        {
            AddUntilStep("gameplay is running", () => this.ChildrenOfType<DrawableDivaFlightRuleset>().Any());
            AddStep("add a probe that needs OsuRulesetConfigManager", () => this.ChildrenOfType<PlayfieldAdjustmentContainer>().First().Add(new ConfigProbe()));
            AddAssert("the probe got it", () => this.ChildrenOfType<ConfigProbe>().First().Config != null);
        }

        private partial class ConfigProbe : Drawable
        {
            [Resolved]
            public OsuRulesetConfigManager Config { get; private set; } = null!;
        }
    }
}
