using System;
using System.ComponentModel;
using System.Reflection;

namespace Empire_Earth_Mod_Lib
{
    /// <summary>
    /// Display names of enum values, taken from their <see cref="DescriptionAttribute"/>.
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// The <see cref="DescriptionAttribute"/> text of <paramref name="value"/>, or its name if it has none
        /// (also for values that are not defined in the enum).
        /// </summary>
        public static string GetDescription(this Enum value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            FieldInfo field = value.GetType().GetField(value.ToString());
            var attribute = field == null
                ? null
                : (DescriptionAttribute)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
            return attribute == null ? value.ToString() : attribute.Description;
        }

        /// <summary>
        /// Finds the value whose <see cref="GetDescription"/> is <paramref name="description"/> (ordinal
        /// comparison), the inverse of <see cref="GetDescription"/>.
        /// </summary>
        /// <returns>false if no value has this description.</returns>
        public static bool TryParseDescription<TEnum>(string description, out TEnum value) where TEnum : struct, Enum
        {
            foreach (TEnum candidate in Enum.GetValues(typeof(TEnum)))
            {
                if (string.Equals(candidate.GetDescription(), description, StringComparison.Ordinal))
                {
                    value = candidate;
                    return true;
                }
            }
            value = default;
            return false;
        }

        /// <summary>Like <see cref="TryParseDescription{TEnum}"/>, but throws for an unknown description.</summary>
        /// <exception cref="ArgumentException">No value has this description.</exception>
        public static TEnum ParseDescription<TEnum>(string description) where TEnum : struct, Enum
        {
            TEnum value;
            if (!TryParseDescription(description, out value))
                throw new ArgumentException("\"" + description + "\" is not a " + typeof(TEnum).Name + ".", nameof(description));
            return value;
        }
    }
}
