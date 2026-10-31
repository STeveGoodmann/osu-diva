using System;
using NUnit.Framework;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.OsuDiva.UnitTests
{
    /// <summary>
    /// Repeats the checks lazer's RealmRulesetStore runs when it loads a ruleset DLL.
    /// </summary>
    [TestFixture]
    public class LazerLoadChecksTests
    {
        [Test]
        public void PassesRealmRulesetStoreConsistencyChecks()
        {
            var type = typeof(OsuDivaRuleset);
            var instance = (Ruleset)Activator.CreateInstance(type)!;

            Assert.That(instance.RulesetInfo, Is.Not.Null);
            Assert.That(instance.RulesetAPIVersionSupported, Is.EqualTo(Ruleset.CURRENT_RULESET_API_VERSION));

            // lazer: resolvedType.Assembly.GetTypes() to find missing implementations eagerly.
            Assert.DoesNotThrow(() => type.Assembly.GetTypes());

            // lazer: testRulesetCompatibility
            Assert.DoesNotThrow(() =>
            {
                instance.CreateAllMods();
                instance.CreateIcon();
                instance.CreateResourceStore();

                var beatmap = new Beatmap();
                var converter = instance.CreateBeatmapConverter(beatmap);
                instance.CreateBeatmapProcessor(converter.Convert());
            });
        }
    }
}
