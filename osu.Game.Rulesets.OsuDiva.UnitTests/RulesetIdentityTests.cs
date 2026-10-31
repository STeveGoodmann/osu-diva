using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Rulesets.DivaFlight.UnitTests
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
            // lazer's database row for this ruleset holds exactly this string (RulesetInfo.InstantiationInfo) and its short name as primary key.
            // If this string changes, the stored row no longer resolves, a new row is made with the same short name, and lazer fails with
            // "An issue with ruleset ... occurred". Folders and file names may change freely; the namespace and the assembly name may not.
            var info = new OsuDivaRuleset().RulesetInfo;

            Assert.That(info.InstantiationInfo, Is.EqualTo("osu.Game.Rulesets.DivaFlight.OsuDivaRuleset, osu.Game.Rulesets.DivaFlight"));
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
