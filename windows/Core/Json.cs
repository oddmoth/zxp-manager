using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ZXPManager
{
    /// <summary>
    /// Minimal JSON reader (no external dependencies).
    /// Objects → Dictionary&lt;string, object&gt;, arrays → List&lt;object&gt;,
    /// strings → string, numbers → double, true/false → bool, null → null.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            var parser = new Parser(text ?? "");
            parser.SkipWhitespace();
            var value = parser.ReadValue();
            parser.SkipWhitespace();
            if (!parser.AtEnd) throw new FormatException("Unexpected data after JSON value");
            return value;
        }

        private class Parser
        {
            private readonly string s;
            private int i;

            public Parser(string text) { s = text; }

            public bool AtEnd => i >= s.Length;

            public void SkipWhitespace()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                if (i == 0 && s.Length > 0 && s[0] == '﻿') { i++; SkipWhitespace(); } // BOM
            }

            private char Peek() => i < s.Length ? s[i] : throw new FormatException("Unexpected end of JSON");

            private void Expect(char c)
            {
                if (Peek() != c) throw new FormatException("Expected '" + c + "' at position " + i);
                i++;
            }

            public object ReadValue()
            {
                SkipWhitespace();
                char c = Peek();
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': ReadWord("true"); return true;
                    case 'f': ReadWord("false"); return false;
                    case 'n': ReadWord("null"); return null;
                    default: return ReadNumber();
                }
            }

            private void ReadWord(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                    throw new FormatException("Invalid literal at position " + i);
                i += word.Length;
            }

            private Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>();
                Expect('{');
                SkipWhitespace();
                if (Peek() == '}') { i++; return result; }
                while (true)
                {
                    SkipWhitespace();
                    var key = ReadString();
                    SkipWhitespace();
                    Expect(':');
                    result[key] = ReadValue();
                    SkipWhitespace();
                    if (Peek() == ',') { i++; continue; }
                    Expect('}');
                    return result;
                }
            }

            private List<object> ReadArray()
            {
                var result = new List<object>();
                Expect('[');
                SkipWhitespace();
                if (Peek() == ']') { i++; return result; }
                while (true)
                {
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (Peek() == ',') { i++; continue; }
                    Expect(']');
                    return result;
                }
            }

            private string ReadString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    char c = Peek();
                    i++;
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }

                    char e = Peek();
                    i++;
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException("Invalid \\u escape");
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: throw new FormatException("Invalid escape at position " + i);
                    }
                }
            }

            private double ReadNumber()
            {
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                if (start == i) throw new FormatException("Unexpected character at position " + i);
                return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }
}
