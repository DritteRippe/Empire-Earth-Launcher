using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// The synthetic hashes of the fixtures (ADR 0012 plan review, REV-15): the SHA-256 of the ASCII text
    /// <c>sample-&lt;n&gt;</c>, which is also the content of the files the integrity tests create. No hash of real game data
    /// appears in the tests; <c>FixtureProvenanceTests</c> checks the fixture files and <c>docs/contract-samples</c>.
    /// </summary>
    internal static class SampleHashes
    {
        /// <summary>The highest <c>n</c> of <c>sample-&lt;n&gt;</c> a fixture may use.</summary>
        public const int MaxSample = 9999;

        /// <summary>The text <c>sample-&lt;n&gt;</c>, the content of a synthetic file.</summary>
        public static string Content(int n)
        {
            return "sample-" + n.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The SHA-256 of <see cref="Content"/>, 64 lowercase hex digits.</summary>
        public static string Of(int n)
        {
            return Sha256(Encoding.ASCII.GetBytes(Content(n)));
        }

        /// <summary>The SHA-256 of <paramref name="bytes"/>, 64 lowercase hex digits.</summary>
        public static string Sha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                var text = new StringBuilder();
                foreach (byte value in sha256.ComputeHash(bytes))
                    text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }
    }
}
