using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// Type of a registry value; the numbers are the Windows <c>REG_*</c> constants. Other numbers (e.g. 8,
    /// <c>REG_RESOURCE_LIST</c>) are valid values of this enum and stand for types the launcher does not
    /// interpret; their data is kept as bytes.
    /// </summary>
    public enum RegistryValueType
    {
        /// <summary><c>REG_NONE</c>: bytes without a type.</summary>
        None = 0,
        /// <summary><c>REG_SZ</c>.</summary>
        String = 1,
        /// <summary><c>REG_EXPAND_SZ</c>: a string with unexpanded environment variables.</summary>
        ExpandString = 2,
        /// <summary><c>REG_BINARY</c>.</summary>
        Binary = 3,
        /// <summary><c>REG_DWORD</c>: 32 bits.</summary>
        DWord = 4,
        /// <summary><c>REG_MULTI_SZ</c>.</summary>
        MultiString = 7,
        /// <summary><c>REG_QWORD</c>: 64 bits.</summary>
        QWord = 11
    }

    /// <summary>
    /// The type and data of a registry value (ADR 0006: values carry their kind). Immutable; arrays are copied in
    /// and out.
    /// </summary>
    public sealed class RegistryValue : IEquatable<RegistryValue>
    {
        private readonly object data;

        private RegistryValue(RegistryValueType type, object data)
        {
            Type = type;
            this.data = data;
        }

        public RegistryValueType Type { get; }

        /// <summary>True for <see cref="RegistryValueType.String"/> and <see cref="RegistryValueType.ExpandString"/>.</summary>
        public bool IsString
        {
            get { return Type == RegistryValueType.String || Type == RegistryValueType.ExpandString; }
        }

        /// <summary>The text of a string value.</summary>
        /// <exception cref="InvalidOperationException">The value is not a string.</exception>
        public string StringValue
        {
            get
            {
                if (!IsString)
                    throw new InvalidOperationException("The registry value is " + Type + ", not a string.");
                return (string)data;
            }
        }

        /// <summary>The number of a <see cref="RegistryValueType.DWord"/> value.</summary>
        public int DWordValue
        {
            get
            {
                if (Type != RegistryValueType.DWord)
                    throw new InvalidOperationException("The registry value is " + Type + ", not a DWORD.");
                return (int)data;
            }
        }

        /// <summary>The number of a <see cref="RegistryValueType.QWord"/> value.</summary>
        public long QWordValue
        {
            get
            {
                if (Type != RegistryValueType.QWord)
                    throw new InvalidOperationException("The registry value is " + Type + ", not a QWORD.");
                return (long)data;
            }
        }

        /// <summary>The strings of a <see cref="RegistryValueType.MultiString"/> value.</summary>
        public IReadOnlyList<string> MultiStringValue
        {
            get
            {
                if (Type != RegistryValueType.MultiString)
                    throw new InvalidOperationException("The registry value is " + Type + ", not a multi-string.");
                return ((string[])data).ToList();
            }
        }

        /// <summary>
        /// A copy of the bytes of a <see cref="RegistryValueType.Binary"/>, <see cref="RegistryValueType.None"/>
        /// or uninterpreted value.
        /// </summary>
        public byte[] GetBytes()
        {
            if (!(data is byte[] bytes))
                throw new InvalidOperationException("The registry value is " + Type + ", not stored as bytes.");
            return (byte[])bytes.Clone();
        }

        public static RegistryValue FromString(string value)
        {
            return new RegistryValue(RegistryValueType.String, value ?? throw new ArgumentNullException(nameof(value)));
        }

        public static RegistryValue FromExpandString(string value)
        {
            return new RegistryValue(RegistryValueType.ExpandString, value ?? throw new ArgumentNullException(nameof(value)));
        }

        public static RegistryValue FromDWord(int value)
        {
            return new RegistryValue(RegistryValueType.DWord, value);
        }

        public static RegistryValue FromQWord(long value)
        {
            return new RegistryValue(RegistryValueType.QWord, value);
        }

        public static RegistryValue FromMultiString(IEnumerable<string> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            string[] copy = values.ToArray();
            if (copy.Any(value => value == null))
                throw new ArgumentException("A multi-string cannot contain null.", nameof(values));
            return new RegistryValue(RegistryValueType.MultiString, copy);
        }

        public static RegistryValue FromBinary(byte[] value)
        {
            return new RegistryValue(RegistryValueType.Binary, (byte[])(value ?? throw new ArgumentNullException(nameof(value))).Clone());
        }

        /// <summary>
        /// A value of <see cref="RegistryValueType.None"/> or of a type the launcher does not interpret, as bytes.
        /// </summary>
        /// <exception cref="ArgumentException">The type has its own factory method.</exception>
        public static RegistryValue FromRaw(RegistryValueType type, byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            switch (type)
            {
                case RegistryValueType.String:
                case RegistryValueType.ExpandString:
                case RegistryValueType.DWord:
                case RegistryValueType.QWord:
                case RegistryValueType.MultiString:
                case RegistryValueType.Binary:
                    throw new ArgumentException("Use the factory method of " + type + ".", nameof(type));
            }
            if (type < 0)
                throw new ArgumentOutOfRangeException(nameof(type), type, "Registry types are not negative.");
            return new RegistryValue(type, value.Clone());
        }

        public bool Equals(RegistryValue other)
        {
            if (other == null || other.Type != Type)
                return false;
            switch (data)
            {
                case string text:
                    return string.Equals(text, (string)other.data, StringComparison.Ordinal);
                case string[] texts:
                    return texts.SequenceEqual((string[])other.data, StringComparer.Ordinal);
                case byte[] bytes:
                    return bytes.SequenceEqual((byte[])other.data);
                default:
                    return data.Equals(other.data);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RegistryValue);
        }

        public override int GetHashCode()
        {
            switch (data)
            {
                case string text:
                    return ((int)Type * 397) ^ StringComparer.Ordinal.GetHashCode(text);
                case string[] texts:
                    return ((int)Type * 397) ^ texts.Length;
                case byte[] bytes:
                    return ((int)Type * 397) ^ bytes.Length;
                default:
                    return ((int)Type * 397) ^ data.GetHashCode();
            }
        }

        /// <summary>Type and data for the log, e.g. <c>REG_DWORD 0x00000020 (32)</c>.</summary>
        public override string ToString()
        {
            switch (Type)
            {
                case RegistryValueType.String:
                    return "REG_SZ \"" + data + "\"";
                case RegistryValueType.ExpandString:
                    return "REG_EXPAND_SZ \"" + data + "\"";
                case RegistryValueType.DWord:
                    return string.Format(CultureInfo.InvariantCulture, "REG_DWORD 0x{0:x8} ({1})", (uint)(int)data, (int)data);
                case RegistryValueType.QWord:
                    return string.Format(CultureInfo.InvariantCulture, "REG_QWORD 0x{0:x16} ({1})", (ulong)(long)data, (long)data);
                case RegistryValueType.MultiString:
                    return "REG_MULTI_SZ [" + string.Join(", ", ((string[])data).Select(s => "\"" + s + "\"")) + "]";
                default:
                    string name = Type == RegistryValueType.Binary ? "REG_BINARY"
                        : Type == RegistryValueType.None ? "REG_NONE"
                        : "REG type " + ((int)Type).ToString(CultureInfo.InvariantCulture);
                    return name + " " + BitConverter.ToString((byte[])data).Replace("-", " ").ToLowerInvariant();
            }
        }
    }
}
