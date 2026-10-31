using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Game.Rulesets.DivaFlight.Localisation;

namespace osu.Game.Rulesets.DivaFlight.UnitTests
{
    [TestFixture]
    public class LocalisationTests
    {
        private sealed class FakeStore : ILocalisationStore
        {
            public CultureInfo EffectiveCulture { get; }

            public FakeStore(string culture) => EffectiveCulture = new CultureInfo(culture);

            public string Get(string lookup) => lookup;
            public System.Threading.Tasks.Task<string> GetAsync(string lookup, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult(lookup);
            public System.IO.Stream GetStream(string name) => throw new NotSupportedException();
            public IEnumerable<string> GetAvailableResources() => Enumerable.Empty<string>();
            public void Dispose() { }
        }

        private static string evaluate(LocalisableString text, string? culture)
        {
            var parameters = new LocalisationParameters(culture == null ? null : new FakeStore(culture), false);

            // the text is kept inside the struct; get at it the way the framework does when it shows the text
            var data = typeof(LocalisableString).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                                .Select(f => f.GetValue(text)).OfType<ILocalisableStringData>().FirstOrDefault();

            return data != null ? data.GetLocalised(parameters) : text.ToString();
        }

        private static bool hasChinese(string s) => s.Any(c => c >= 0x4E00 && c <= 0x9FFF);

        private static IEnumerable<(string name, LocalisableString text)> allStrings() => typeof(DivaStrings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(LocalisableString))
            .Select(p => (p.Name, (LocalisableString)p.GetValue(null)!));

        [Test]
        public void EveryTextExistsInBothLanguagesAndTheyDiffer()
        {
            var all = allStrings().ToList();
            Assert.That(all.Count, Is.GreaterThan(40), "the list of texts was found");

            foreach (var (name, text) in all)
            {
                string english = evaluate(text, "en-US");
                string chinese = evaluate(text, "zh-CN");

                Assert.That(english, Is.Not.Empty, name);
                Assert.That(hasChinese(english), Is.False, $"{name}: the English text has no Chinese in it");
                Assert.That(hasChinese(chinese), Is.True, $"{name}: the Chinese text is Chinese");
                Assert.That(chinese, Is.Not.EqualTo(english), name);
            }
        }

        [TestCase("zh", true)]
        [TestCase("zh-CN", true)]
        [TestCase("zh-TW", true)]
        [TestCase("en-US", false)]
        [TestCase("en", false)]
        [TestCase("ja-JP", false)]
        [TestCase("ko-KR", false)]
        [TestCase(null, false)]
        public void TheLanguageFollowsLazersLanguage(string? culture, bool chinese)
        {
            Assert.That(hasChinese(evaluate(DivaStrings.FlightSpeed, culture)), Is.EqualTo(chinese));
        }

        [Test]
        public void EnglishIsWhatYouGetWhenNothingIsKnown()
        {
            Assert.That(DivaStrings.FlightSpeed.ToString(), Is.EqualTo("Flight speed"));
            Assert.That(evaluate(DivaStrings.FlightSpeed, "zh-CN"), Is.EqualTo("飞行速度"));
        }

        [Test]
        public void TheDropDownOptionsHaveNamesInBothLanguages()
        {
            var options = new List<Enum>();
            options.AddRange(Enum.GetValues<OsuDivaDirection>().Cast<Enum>());
            options.AddRange(Enum.GetValues<Configuration.OsuDivaMaterial>().Cast<Enum>());
            options.AddRange(Enum.GetValues<PlannerSearch>().Cast<Enum>());
            Assert.That(options.Count, Is.EqualTo(7));

            foreach (var option in options)
            {
                var text = option.GetLocalisableDescription();
                string english = evaluate(text, "en-US");
                string chinese = evaluate(text, "zh-CN");

                Assert.That(english, Is.Not.Empty, option.ToString());
                Assert.That(hasChinese(chinese), Is.True, $"{option}: Chinese name");
                Assert.That(hasChinese(english), Is.False, $"{option}: English name");
            }
        }

        [Test]
        public void TheRulesetIsCalledOsuDivaButKeepsItsStoredIdentity()
        {
            var ruleset = new OsuDivaRuleset();

            Assert.That(ruleset.Description, Is.EqualTo("osu! DIVA"));
            Assert.That(ruleset.RulesetInfo.Name, Is.EqualTo("osu! DIVA"));
            Assert.That(ruleset.ShortName, Is.EqualTo("osudiva"), "the short name keys the stored settings, scores and key bindings");
        }
    }
}
