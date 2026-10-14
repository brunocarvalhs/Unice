using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Unice
{
    /// <summary>
    /// Every key in Localization.Strings has pt, en and es, none empty, and the same {0}, {1}… placeholders in all three
    /// (a missing one would drop a name from the sentence; an extra one throws in string.Format).
    /// The table is private, so it is read by reflection — no production change just for the test.
    /// </summary>
    [TestFixture]
    public class LocalizationTests
    {
        static readonly Regex Placeholder = new Regex(@"\{(\d+)(?:[,:][^}]*)?\}");

        static Dictionary<string, string[]> Strings()
        {
            var field = typeof(Localization).GetField("Strings", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Localization.Strings not found (renamed?)");
            return (Dictionary<string, string[]>)field.GetValue(null);
        }

        static string Placeholders(string text) =>
            string.Join(",", Placeholder.Matches(text).Cast<Match>().Select(match => int.Parse(match.Groups[1].Value)).Distinct().OrderBy(index => index));

        [Test]
        public void Strings_EveryKey_HasNonEmptyPtEnEs()
        {
            var broken = Strings()
                .Where(entry => entry.Value == null || entry.Value.Length != 3 || entry.Value.Any(string.IsNullOrWhiteSpace))
                .Select(entry => entry.Key).ToList();

            Assert.That(broken, Is.Empty, "keys missing a pt/en/es translation");
        }

        [Test]
        public void Strings_EveryKey_HasSamePlaceholdersInAllLanguages()
        {
            var broken = Strings()
                .Where(entry => entry.Value != null && entry.Value.Length == 3 && entry.Value.All(text => text != null))
                .Where(entry => entry.Value.Select(Placeholders).Distinct().Count() > 1)
                .Select(entry => entry.Key + " [" + string.Join(" | ", entry.Value.Select(Placeholders)) + "]").ToList();

            Assert.That(broken, Is.Empty, "keys whose {n} placeholders differ between pt/en/es");
        }
    }
}
