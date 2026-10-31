using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Rulesets.OsuDiva.UnitTests
{
    [TestFixture]
    public class RulesetIdentityTests
    {
        [Test]
        public void ApproachCirclesAreFaintByDefault()
        {
            var config = new Configuration.OsuDivaConfigManager(null, new OsuDivaRuleset().RulesetInfo);
            var visibility = (osu.Framework.Bindables.BindableNumber<float>)config.GetBindable<float>(Configuration.OsuDivaSetting.ApproachCircleVisibility);

            Assert.That(visibility.Default, Is.EqualTo(0.25f), "the default is 25%");
            Assert.That(visibility.MinValue, Is.EqualTo(0f));
            Assert.That(visibility.MaxValue, Is.EqualTo(1f));
            Assert.That(Mods.ApproachCircleOpacityMod.StartScale(0.25f), Is.EqualTo(1.75f).Within(1e-6), "the ring starts at 1 + 3 x 25% circles");
            Assert.That(Mods.ApproachCircleOpacityMod.StartScale(1f), Is.EqualTo(4f).Within(1e-6), "100% is osu!'s own 4");
            Assert.That(Mods.ApproachCircleOpacityMod.StartScale(0f), Is.EqualTo(1f).Within(1e-6));
        }

        [Test]
        public void StoredTypeIdentityDoesNotChange()
        {
            // lazer's database row for this ruleset holds this string (RulesetInfo.InstantiationInfo) and the short name as primary key.
            // The short name must never change. If the type string changes, lazer re-points the stored row only while the short name is
            // unchanged (and the old DLL is removed from the rulesets folder), so a change here is deliberate: update this test with it.
            var info = new OsuDivaRuleset().RulesetInfo;

            Assert.That(info.InstantiationInfo, Is.EqualTo("osu.Game.Rulesets.OsuDiva.OsuDivaRuleset, osu.Game.Rulesets.OsuDiva"));
            Assert.That(info.ShortName, Is.EqualTo("osudiva"));
        }

        [Test]
        public void IsSeparateFromOsuRulesetButSharesItsRules()
        {
            var diva = new OsuDivaRuleset();
            var osu = new OsuRuleset();

            Assert.That(diva.ShortName, Is.EqualTo("osudiva"));
            Assert.That(diva.RulesetInfo.OnlineID, Is.EqualTo(-1), "must not claim osu!'s legacy ID 0");
            Assert.That(diva.RulesetAPIVersionSupported, Is.EqualTo(Ruleset.CURRENT_RULESET_API_VERSION));

            Assert.That(diva.CreateAllMods().Select(m => m.Acronym), Is.EqualTo(osu.CreateAllMods().Select(m => m.Acronym)));
            Assert.That(diva.GetDefaultKeyBindings().Select(k => k.KeyCombination.ToString()),
                Is.EqualTo(osu.GetDefaultKeyBindings().Select(k => k.KeyCombination.ToString())));
        }
    }
}
