using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Play
{
    /// <summary>
    /// <see cref="LauncherArguments"/>: <c>--product=EE|NeoEE</c> of the suite shortcuts (contract 1.4, revision 4); every other
    /// value or argument is ignored and logged, the launcher never fails on its command line.
    /// </summary>
    [TestFixture]
    public class LauncherArgumentsTests
    {
        private RecordingLogger logger;

        [SetUp]
        public void SetUp()
        {
            logger = new RecordingLogger();
        }

        [TestCase("--product=EE", "EE")]
        [TestCase("--product=NeoEE", "NeoEE")]
        [TestCase("--PRODUCT=NeoEE", "NeoEE")]
        public void AValidProduct_IsTheSessionProduct(string argument, string product)
        {
            LauncherArguments arguments = LauncherArguments.Parse(new[] { argument }, logger);

            Assert.That(arguments.SessionProduct, Is.SameAs(Product.FromId(product)));
            Assert.That(arguments.Ignored, Is.Empty);
            Assert.That(logger.MessagesOf(LogLevel.Warning), Is.Empty);
            Assert.That(logger.MessagesOf(LogLevel.Info).Single(), Does.Contain("--product=" + product));
        }

        [TestCase("--product=")]
        [TestCase("--product=Neo")]
        [TestCase("--product=ee")]
        [TestCase("--product=neoee")]
        [TestCase("--product=EE,NeoEE")]
        [TestCase("--product= EE")]
        [TestCase("--product=AoC")]
        public void AnInvalidValue_IsIgnored_AndLogged(string argument)
        {
            LauncherArguments arguments = LauncherArguments.Parse(new[] { argument }, logger);

            Assert.That(arguments.SessionProduct, Is.Null);
            Assert.That(arguments.Ignored, Is.EqualTo(new[] { argument }));
            Assert.That(logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain("\"" + argument + "\" is ignored"));
            Assert.That(logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain("EE or NeoEE"));
        }

        [TestCase("--product")]
        [TestCase("EE")]
        [TestCase("-product=EE")]
        [TestCase("/product=EE")]
        [TestCase("--other=1")]
        [TestCase("")]
        public void AnotherArgument_IsIgnored_AndLogged(string argument)
        {
            LauncherArguments arguments = LauncherArguments.Parse(new[] { argument }, logger);

            Assert.That(arguments.SessionProduct, Is.Null);
            Assert.That(arguments.Ignored, Is.EqualTo(new[] { argument }));
            Assert.That(logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain("is not known"));
        }

        [Test]
        public void NoArguments_AreNone()
        {
            Assert.That(LauncherArguments.Parse(new string[0], logger).SessionProduct, Is.Null);
            Assert.That(LauncherArguments.Parse(null, logger), Is.SameAs(LauncherArguments.None));
            Assert.That(logger.Entries, Is.Empty, "nothing to say");
        }

        [Test]
        public void TheLastValidValue_Counts_AndAnInvalidOneBetweenDoesNotDropIt()
        {
            LauncherArguments arguments = LauncherArguments.Parse(new[] { "--product=EE", "--product=x", "--product=NeoEE", "--product=y" }, logger);

            Assert.That(arguments.SessionProduct, Is.SameAs(Product.NeoEE));
            Assert.That(arguments.Ignored, Is.EqualTo(new[] { "--product=x", "--product=y" }));
        }

        [Test]
        public void ANullEntry_IsIgnored()
        {
            LauncherArguments arguments = LauncherArguments.Parse(new string[] { null, "--product=EE" }, logger);

            Assert.That(arguments.SessionProduct, Is.SameAs(Product.EE));
            Assert.That(arguments.Ignored, Has.Count.EqualTo(1));
        }

        [Test]
        public void Parse_ChecksTheLogger()
        {
            Assert.That(() => LauncherArguments.Parse(new string[0], null), Throws.ArgumentNullException);
        }

        [TestCase("EE", "EE")]
        [TestCase("NeoEE", "NeoEE")]
        [TestCase("Ee", null)]
        [TestCase(null, null)]
        public void ParseProduct_IsExact(string value, string expected)
        {
            Assert.That(LauncherArguments.ParseProduct(value)?.Id, Is.EqualTo(expected));
        }
    }
}
