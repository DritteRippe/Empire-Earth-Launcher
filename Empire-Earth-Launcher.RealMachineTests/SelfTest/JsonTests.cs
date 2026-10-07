using System;
using System.Collections.Generic;
using Empire_Earth_Launcher.RealMachineTests.Json;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.SelfTest
{
    /// <summary>The strict JSON reader and the writer of the harness.</summary>
    [TestFixture]
    [Category(RealMachineCategories.SelfTest)]
    public class JsonTests
    {
        [Test]
        public void Reader_ReadsObjectsArraysStringsIntegersAndLiterals()
        {
            var value = (Dictionary<string, object>)JsonReader.Parse(
                " {\r\n \"a\": [1, -20, 0, true, false, null], \"b\": { }, \"c\": \"x\\\"\\\\\\/\\b\\f\\n\\r\\t\\u00c4\", \"d\": [] } ");

            Assert.That(value["a"], Is.EqualTo(new object[] { 1L, -20L, 0L, true, false, null }));
            Assert.That(value["b"], Is.Empty);
            Assert.That(value["c"], Is.EqualTo("x\"\\/\b\f\n\r\t\u00c4"));
            Assert.That(value["d"], Is.Empty);
        }

        [TestCase("{\"a\": 1, \"a\": 2}", "appears twice")]
        [TestCase("{\"a\": 1.5}", "only integers")]
        [TestCase("{\"a\": 1e3}", "only integers")]
        [TestCase("{\"a\": 01}", "only integers")]
        [TestCase("[1, 2,]", "unexpected character ']'")]
        [TestCase("{\"a\": 1,}", "a member name in double quotes expected")]
        [TestCase("{'a': 1}", "a member name in double quotes expected")]
        [TestCase("{\"a\": 1} x", "text after the value")]
        [TestCase("{\"a\": \"line\nbreak\"}", "must be escaped")]
        [TestCase("{\"a\": \"\\x\"}", "unknown escape")]
        [TestCase("{\"a\": tru}", "unexpected character 't'")]
        [TestCase("{\"a\": 99999999999999999999}", "too large")]
        [TestCase("[", "a value expected, the text ends")]
        [TestCase("\"open", "does not end")]
        [TestCase("", "a value expected")]
        public void Reader_RefusesWhatIsNotStrictJson(string text, string message)
        {
            var error = Assert.Throws<FormatException>(() => JsonReader.Parse(text));

            Assert.That(error.Message, Does.StartWith("JSON: ").And.Contain(message).And.Contain("(line "));
        }

        [Test]
        public void Reader_NamesLineAndColumn()
        {
            var error = Assert.Throws<FormatException>(() => JsonReader.Parse("{\n  \"a\": 1,\n  \"b\": x\n}"));

            Assert.That(error.Message, Does.EndWith("(line 3, column 8)."));
        }

        [Test]
        public void Reader_RefusesTooDeepNesting()
        {
            Assert.Throws<FormatException>(() => JsonReader.Parse(new string('[', 100) + new string(']', 100)));
        }

        [Test]
        public void Writer_EscapesQuotesBackslashesAndEverythingOutsidePrintableAscii()
        {
            Assert.That(JsonWriter.Quote("C:\\Spiele\\\u00c4gypten \"x\"\t\u0001"), Is.EqualTo("\"C:\\\\Spiele\\\\\\u00c4gypten \\\"x\\\"\\t\\u0001\""));
            Assert.That(JsonWriter.Quote(null), Is.EqualTo("null"));
            Assert.That(JsonReader.Parse(JsonWriter.Quote("C:\\Spiele\\\u00c4gypten")), Is.EqualTo("C:\\Spiele\\\u00c4gypten"));
            Assert.That(JsonWriter.Member("code", JsonWriter.Quote("a")), Is.EqualTo("\"code\": \"a\""));
        }
    }
}
