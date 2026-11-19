// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Pure C# — no UnityEngine dependency (portable to other engines).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AccessCalibrate.Core
{
    /// <summary>
    /// A tiny JSON reader/writer, just enough for <see cref="AccessibilityProfile"/>.
    /// Parse returns: Dictionary&lt;string, object&gt;, List&lt;object&gt;, string, double, bool or null.
    /// </summary>
    internal static class MiniJson
    {
        public static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? string.Empty)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        public static object Parse(string json)
        {
            if (json == null) throw new FormatException("JSON is null.");
            var p = new Parser(json);
            var value = p.ReadValue();
            p.SkipWhitespace();
            if (!p.AtEnd) throw p.Error("Unexpected text after JSON value");
            return value;
        }

        sealed class Parser
        {
            readonly string _s;
            int _i;

            public Parser(string s) { _s = s; }
            public bool AtEnd => _i >= _s.Length;

            public FormatException Error(string msg) => new FormatException($"{msg} at position {_i}.");

            public void SkipWhitespace()
            {
                while (!AtEnd && char.IsWhiteSpace(_s[_i])) _i++;
            }

            char Peek() { SkipWhitespace(); if (AtEnd) throw Error("Unexpected end of JSON"); return _s[_i]; }

            void Expect(char c)
            {
                if (Peek() != c) throw Error($"Expected '{c}'");
                _i++;
            }

            public object ReadValue()
            {
                char c = Peek();
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': ReadLiteral("true"); return true;
                    case 'f': ReadLiteral("false"); return false;
                    case 'n': ReadLiteral("null"); return null;
                    default:
                        if (c == '-' || char.IsDigit(c)) return ReadNumber();
                        throw Error($"Unexpected character '{c}'");
                }
            }

            Dictionary<string, object> ReadObject()
            {
                var obj = new Dictionary<string, object>();
                Expect('{');
                if (Peek() == '}') { _i++; return obj; }
                while (true)
                {
                    if (Peek() != '"') throw Error("Expected property name");
                    string key = ReadString();
                    Expect(':');
                    obj[key] = ReadValue();
                    char c = Peek();
                    _i++;
                    if (c == '}') return obj;
                    if (c != ',') throw Error("Expected ',' or '}'");
                }
            }

            List<object> ReadArray()
            {
                var list = new List<object>();
                Expect('[');
                if (Peek() == ']') { _i++; return list; }
                while (true)
                {
                    list.Add(ReadValue());
                    char c = Peek();
                    _i++;
                    if (c == ']') return list;
                    if (c != ',') throw Error("Expected ',' or ']'");
                }
            }

            string ReadString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("Unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("Unterminated escape");
                    char e = _s[_i++];
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
                            if (_i + 4 > _s.Length) throw Error("Bad unicode escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw Error($"Bad escape '\\{e}'");
                    }
                }
            }

            double ReadNumber()
            {
                int start = _i;
                while (!AtEnd && "+-0123456789.eE".IndexOf(_s[_i]) >= 0) _i++;
                string token = _s.Substring(start, _i - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    throw Error($"Bad number '{token}'");
                return d;
            }

            void ReadLiteral(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error($"Expected '{word}'");
                _i += word.Length;
            }
        }
    }
}
