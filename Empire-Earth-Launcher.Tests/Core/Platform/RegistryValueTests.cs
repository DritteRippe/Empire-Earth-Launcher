using System;
using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>Registry values carry their type (<see cref="RegistryValue"/>, ADR 0006).</summary>
    [TestFixture]
    public class RegistryValueTests
    {
        [Test]
        public void TypedValues()
        {
            Assert.That(RegistryValue.FromString("Direct3D").StringValue, Is.EqualTo("Direct3D"));
            Assert.That(RegistryValue.FromExpandString("%TEMP%").StringValue, Is.EqualTo("%TEMP%"));
            Assert.That(RegistryValue.FromDWord(1200000).DWordValue, Is.EqualTo(1200000));
            Assert.That(RegistryValue.FromQWord(1L << 40).QWordValue, Is.EqualTo(1L << 40));
            Assert.That(RegistryValue.FromMultiString(new[] { "a", "" }).MultiStringValue, Is.EqualTo(new[] { "a", "" }));
            Assert.That(RegistryValue.FromBinary(new byte[] { 1, 2 }).GetBytes(), Is.EqualTo(new byte[] { 1, 2 }));
            Assert.That(RegistryValue.FromRaw(RegistryValueType.None, new byte[0]).Type, Is.EqualTo(RegistryValueType.None));
            Assert.That(RegistryValue.FromRaw((RegistryValueType)8, new byte[] { 9 }).Type, Is.EqualTo((RegistryValueType)8));
        }

        [Test]
        public void WrongAccessor_Throws()
        {
            Assert.That(() => RegistryValue.FromDWord(1).StringValue, Throws.InvalidOperationException);
            Assert.That(() => RegistryValue.FromString("1").DWordValue, Throws.InvalidOperationException);
            Assert.That(() => RegistryValue.FromString("1").GetBytes(), Throws.InvalidOperationException);
            Assert.That(() => RegistryValue.FromRaw(RegistryValueType.String, new byte[0]), Throws.ArgumentException);
            Assert.That(() => RegistryValue.FromString(null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => RegistryValue.FromMultiString(new[] { "a", null }), Throws.ArgumentException);
        }

        [Test]
        public void Arrays_AreCopied()
        {
            var bytes = new byte[] { 1, 2 };
            RegistryValue value = RegistryValue.FromBinary(bytes);
            bytes[0] = 9;
            value.GetBytes()[1] = 9;

            Assert.That(value.GetBytes(), Is.EqualTo(new byte[] { 1, 2 }));
        }

        [Test]
        public void Equality_ComparesTypeAndData()
        {
            Assert.That(RegistryValue.FromString("32"), Is.EqualTo(RegistryValue.FromString("32")));
            Assert.That(RegistryValue.FromString("32"), Is.Not.EqualTo(RegistryValue.FromDWord(32)));
            Assert.That(RegistryValue.FromString("a"), Is.Not.EqualTo(RegistryValue.FromString("A")), "data is case-sensitive");
            Assert.That(RegistryValue.FromString("a"), Is.Not.EqualTo(RegistryValue.FromExpandString("a")));
            Assert.That(RegistryValue.FromBinary(new byte[] { 1 }), Is.EqualTo(RegistryValue.FromBinary(new byte[] { 1 })));
            Assert.That(RegistryValue.FromMultiString(new[] { "a" }), Is.EqualTo(RegistryValue.FromMultiString(new[] { "a" })));
            Assert.That(RegistryValue.FromDWord(0).GetHashCode(), Is.EqualTo(RegistryValue.FromDWord(0).GetHashCode()));
        }

        [Test]
        public void ToString_ForTheLog()
        {
            Assert.That(RegistryValue.FromDWord(32).ToString(), Is.EqualTo("REG_DWORD 0x00000020 (32)"));
            Assert.That(RegistryValue.FromDWord(-1).ToString(), Is.EqualTo("REG_DWORD 0xffffffff (-1)"));
            Assert.That(RegistryValue.FromString("Direct3D").ToString(), Is.EqualTo("REG_SZ \"Direct3D\""));
            Assert.That(RegistryValue.FromBinary(new byte[] { 0xAB, 1 }).ToString(), Is.EqualTo("REG_BINARY ab 01"));
            Assert.That(RegistryValue.FromRaw((RegistryValueType)8, new byte[] { 2 }).ToString(), Is.EqualTo("REG type 8 02"));
        }
    }
}
