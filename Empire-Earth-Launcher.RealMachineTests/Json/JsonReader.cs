using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Empire_Earth_Launcher.RealMachineTests.Json
{
    /// <summary>
    /// A strict reader of the JSON the harness reads (RFC 8259 without fractions and exponents, which no file of the harness
    /// needs): an object becomes a <see cref="Dictionary{TKey,TValue}"/> with ordinal keys (a duplicate key is an error), an
    /// array a <see cref="List{T}"/>, a string a <see cref="string"/>, an integer a <see cref="long"/>, <c>true</c> and
    /// <c>false</c> a <see cref="bool"/>, <c>null</c> null.
    /// </summary>
    /// <remarks>
    /// Own code instead of a serializer of the framework: the expectation files are written by a CI job, and a misspelt member
    /// must be an error, not a silently ignored check (<see cref="Expectations.ExpectationFile"/> rejects unknown members).
    /// </remarks>
    internal static class JsonReader
    {
        /// <summary>The value of <paramref name="text"/>.</summary>
        /// <exception cref="FormatException">The text is not JSON of the subset, with line and column.</exception>
        public static object Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            return new Parser(text).ParseDocument();
        }

        private sealed class Parser
        {
            private const int MaxDepth = 64;

            private readonly string text;
            private int position;
            private int depth;

            public Parser(string text)
            {
                this.text = text;
            }

            public object ParseDocument()
            {
                SkipWhiteSpace();
                object value = ParseValue();
                SkipWhiteSpace();
                if (position != text.Length)
                    throw Error("text after the value");
                return value;
            }

            private object ParseValue()
            {
                if (position >= text.Length)
                    throw Error("a value expected, the text ends");
                char c = text[position];
                switch (c)
                {
                    case '{':
                        return Nested(ParseObject);
                    case '[':
                        return Nested(ParseArray);
                    case '"':
                        return ParseString();
                    case 't':
                        ExpectWord("true");
                        return true;
                    case 'f':
                        ExpectWord("false");
                        return false;
                    case 'n':
                        ExpectWord("null");
                        return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                            return ParseInteger();
                        throw Error("unexpected character '" + c + "'");
                }
            }

            private object Nested(Func<object> parse)
            {
                if (++depth > MaxDepth)
                    throw Error("nested deeper than " + MaxDepth.ToString(CultureInfo.InvariantCulture) + " levels");
                object value = parse();
                depth--;
                return value;
            }

            private object ParseObject()
            {
                position++;
                var members = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhiteSpace();
                if (Current == '}')
                {
                    position++;
                    return members;
                }
                while (true)
                {
                    SkipWhiteSpace();
                    if (Current != '"')
                        throw Error("a member name in double quotes expected");
                    int start = position;
                    string name = ParseString();
                    if (members.ContainsKey(name))
                        throw Error("the member \"" + name + "\" appears twice", start);
                    SkipWhiteSpace();
                    if (Current != ':')
                        throw Error("':' expected after the member name");
                    position++;
                    SkipWhiteSpace();
                    members.Add(name, ParseValue());
                    SkipWhiteSpace();
                    char separator = Current;
                    if (separator == '}')
                    {
                        position++;
                        return members;
                    }
                    if (separator != ',')
                        throw Error("',' or '}' expected");
                    position++;
                }
            }

            private object ParseArray()
            {
                position++;
                var items = new List<object>();
                SkipWhiteSpace();
                if (Current == ']')
                {
                    position++;
                    return items;
                }
                while (true)
                {
                    SkipWhiteSpace();
                    items.Add(ParseValue());
                    SkipWhiteSpace();
                    char separator = Current;
                    if (separator == ']')
                    {
                        position++;
                        return items;
                    }
                    if (separator != ',')
                        throw Error("',' or ']' expected");
                    position++;
                }
            }

            private string ParseString()
            {
                position++;
                var value = new StringBuilder();
                while (true)
                {
                    if (position >= text.Length)
                        throw Error("the string does not end");
                    char c = text[position++];
                    if (c == '"')
                        return value.ToString();
                    if (c < ' ')
                        throw Error("a control character in a string must be escaped", position - 1);
                    if (c != '\\')
                    {
                        value.Append(c);
                        continue;
                    }
                    if (position >= text.Length)
                        throw Error("the string does not end");
                    char escape = text[position++];
                    switch (escape)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            value.Append(escape);
                            break;
                        case 'b':
                            value.Append('\b');
                            break;
                        case 'f':
                            value.Append('\f');
                            break;
                        case 'n':
                            value.Append('\n');
                            break;
                        case 'r':
                            value.Append('\r');
                            break;
                        case 't':
                            value.Append('\t');
                            break;
                        case 'u':
                            if (position + 4 > text.Length ||
                                !int.TryParse(text.Substring(position, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture,
                                    out int code))
                                throw Error("\\u needs four hex digits");
                            value.Append((char)code);
                            position += 4;
                            break;
                        default:
                            throw Error("unknown escape \\" + escape, position - 2);
                    }
                }
            }

            private long ParseInteger()
            {
                int start = position;
                if (Current == '-')
                    position++;
                if (Current == '0')
                    position++;
                else if (Current >= '1' && Current <= '9')
                {
                    while (Current >= '0' && Current <= '9')
                        position++;
                }
                else
                {
                    throw Error("a digit expected");
                }
                if (Current == '.' || Current == 'e' || Current == 'E' || (Current >= '0' && Current <= '9'))
                    throw Error("only integers without leading zeros are allowed", start);
                if (!long.TryParse(text.Substring(start, position - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                        out long value))
                    throw Error("the integer is too large", start);
                return value;
            }

            private void ExpectWord(string word)
            {
                if (string.CompareOrdinal(text, position, word, 0, word.Length) != 0)
                    throw Error("unexpected character '" + text[position] + "'");
                position += word.Length;
            }

            /// <summary>The character at the position; '\0' at the end of the text.</summary>
            private char Current
            {
                get { return position < text.Length ? text[position] : '\0'; }
            }

            private void SkipWhiteSpace()
            {
                while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' ||
                                                  text[position] == '\n'))
                    position++;
            }

            private FormatException Error(string message, int at = -1)
            {
                int where = Math.Min(at < 0 ? position : at, text.Length);
                int line = 1;
                int column = 1;
                for (int i = 0; i < where; i++)
                {
                    if (text[i] == '\n')
                    {
                        line++;
                        column = 1;
                    }
                    else
                    {
                        column++;
                    }
                }
                return new FormatException("JSON: " + message + " (line " + line.ToString(CultureInfo.InvariantCulture) + ", column " +
                                           column.ToString(CultureInfo.InvariantCulture) + ").");
            }
        }
    }
}
