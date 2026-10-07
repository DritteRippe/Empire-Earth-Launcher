using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using Krypton.Toolkit;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// Every wrapping text of the launcher is a <see cref="LauncherWrapLabel"/>, never a <c>KryptonWrapLabel</c>, which keeps
    /// a font the palette disposed and then shows a red X (bug report of 2026-10-03), and no text is measured with the font of
    /// a label, which may be such a font. The field names keep <c>...KryptonWrapLabel</c> (ADR 0014); the rules look at types.
    /// </summary>
    [TestFixture]
    public class WrapLabelRulesTests
    {
        private static readonly Regex KryptonWrapLabelType = new Regex(@"\bKryptonWrapLabel\b", RegexOptions.CultureInvariant);

        /// <summary><c>MeasureString(..., x.Font ...)</c>: measuring with the font of another control.</summary>
        private static readonly Regex MeasureWithAControlFont = new Regex(@"MeasureString\([^;]*\.Font\b", RegexOptions.CultureInvariant);

        [Test]
        [Category(TestCategories.SourceTree)]
        public void TheSources_UseNoKryptonWrapLabel()
        {
            var offenders = ProductionSources.CodeLines()
                .Where(line => KryptonWrapLabelType.IsMatch(line.Text))
                .Select(line => line.ToString())
                .ToList();

            Assert.That(offenders, Is.Empty, "use LauncherWrapLabel");
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void NoTextIsMeasuredWithTheFontOfAControl()
        {
            var offenders = ProductionSources.CodeLines()
                .Where(line => MeasureWithAControlFont.IsMatch(line.Text))
                .Select(line => line.ToString())
                .ToList();

            Assert.That(offenders, Is.Empty, "measure with LauncherWrapLabel.TextHeight, which uses a font no palette disposes");
        }

        [Test]
        public void TheLauncherAssembly_HasNoKryptonWrapLabelField()
        {
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Type[] types = typeof(LauncherWrapLabel).Assembly.GetTypes();

            var offenders = types.SelectMany(type => type.GetFields(Fields)
                                     .Where(field => typeof(KryptonWrapLabel).IsAssignableFrom(field.FieldType))
                                     .Select(field => type.Name + "." + field.Name))
                                 .ToList();
            int wrapLabels = types.Sum(type => type.GetFields(Fields).Count(field => field.FieldType == typeof(LauncherWrapLabel)));

            Assert.That(offenders, Is.Empty);
            Assert.That(wrapLabels, Is.GreaterThanOrEqualTo(40), "the wrapping labels of the pages and the repair advice");
        }
    }
}
