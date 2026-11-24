using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AntigravityCompanion
{
    /// <summary>
    /// Unified zero-dependency JSON &amp; JSONC codec, typed accessor, and comment-preserving patcher.
    /// Consolidates MiniJson, JsonKeyManipulator, StreamingZipIndexReader, and TranscriptMarkdownExporter lexers.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    public static class UnifiedJson
    {
        private const int MaxParseDepth = 256;

        // ====================================================================
        // 1. JSON / JSONC PARSING
        // ====================================================================

        /// <summary>
        /// Parses JSON or JSONC (supports // line comments, /* */ block comments, and trailing commas).
        /// Returns Dictionary&lt;string, object&gt;, List&lt;object&gt;, string, long/double, bool, or null.
        /// </summary>
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new FormatException("JSON content is empty (offset 0).");

            int pos = 0;
            if (json[0] == '\uFEFF') pos = 1;

            SkipWsAndComments(json, ref pos);
            if (pos >= json.Length)
                throw new FormatException("JSON contains no root value (offset " + pos + ").");

            object value = ParseValue(json, ref pos, 0);
            SkipWsAndComments(json, ref pos);
            if (pos < json.Length)
                throw new FormatException("Unexpected trailing character '" + json[pos] + "' after JSON root (offset " + pos + ").");

            return value;
        }

        public static bool TryParse(string json, out object value)
        {
            value = null;
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                value = Parse(json);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        private static object ParseValue(string s, ref int pos, int depth)
        {
            if (depth > MaxParseDepth)
                throw new FormatException("JSON nesting depth exceeds limit (" + MaxParseDepth + ").");

            SkipWsAndComments(s, ref pos);
            if (pos >= s.Length)
                throw new FormatException("JSON expected value but reached end of input (offset " + pos + ").");

            char c = s[pos];
            if (c == '{') return ParseObject(s, ref pos, depth + 1);
            if (c == '[') return ParseArray(s, ref pos, depth + 1);
            if (c == '"') return ParseString(s, ref pos);
            if (c == 't') return ParseLiteral(s, ref pos, "true", true);
            if (c == 'f') return ParseLiteral(s, ref pos, "false", false);
            if (c == 'n') return ParseLiteral(s, ref pos, "null", null);
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(s, ref pos);

            throw new FormatException("Unexpected character '" + c + "' in JSON (offset " + pos + ").");
        }

        private static Dictionary<string, object> ParseObject(string s, ref int pos, int depth)
        {
            pos++; // skip '{'
            Dictionary<string, object> dict = new Dictionary<string, object>(StringComparer.Ordinal);
            bool needComma = false;

            while (true)
            {
                SkipWsAndComments(s, ref pos);
                if (pos >= s.Length)
                    throw new FormatException("Unterminated JSON object missing '}' (offset " + pos + ").");

                if (s[pos] == '}')
                {
                    pos++;
                    return dict;
                }

                if (needComma)
                {
                    if (s[pos] != ',')
                        throw new FormatException("Expected ',' or '}' in JSON object, found '" + s[pos] + "' (offset " + pos + ").");

                    pos++; // skip ','
                    SkipWsAndComments(s, ref pos);
                    if (pos >= s.Length)
                        throw new FormatException("Unterminated JSON object after ',' (offset " + pos + ").");
                    if (s[pos] == '}')
                    {
                        pos++; // trailing comma allowed in JSONC
                        return dict;
                    }
                }

                if (s[pos] != '"')
                    throw new FormatException("Expected quoted property name in JSON object (offset " + pos + ").");

                string key = ParseString(s, ref pos);
                SkipWsAndComments(s, ref pos);
                if (pos >= s.Length || s[pos] != ':')
                    throw new FormatException("Expected ':' after property name '" + key + "' (offset " + pos + ").");

                pos++; // skip ':'
                object val = ParseValue(s, ref pos, depth);
                dict[key] = val;
                needComma = true;
            }
        }

        private static List<object> ParseArray(string s, ref int pos, int depth)
        {
            pos++; // skip '['
            List<object> list = new List<object>();
            bool needComma = false;

            while (true)
            {
                SkipWsAndComments(s, ref pos);
                if (pos >= s.Length)
                    throw new FormatException("Unterminated JSON array missing ']' (offset " + pos + ").");

                if (s[pos] == ']')
                {
                    pos++;
                    return list;
                }

                if (needComma)
                {
                    if (s[pos] != ',')
                        throw new FormatException("Expected ',' or ']' in JSON array, found '" + s[pos] + "' (offset " + pos + ").");

                    pos++; // skip ','
                    SkipWsAndComments(s, ref pos);
                    if (pos >= s.Length)
                        throw new FormatException("Unterminated JSON array after ',' (offset " + pos + ").");
                    if (s[pos] == ']')
                    {
                        pos++; // trailing comma allowed in JSONC
                        return list;
                    }
                }

                object item = ParseValue(s, ref pos, depth);
                list.Add(item);
                needComma = true;
            }
        }

        private static object ParseLiteral(string s, ref int pos, string expected, object result)
        {
            int start = pos;
            if (pos + expected.Length > s.Length ||
                string.CompareOrdinal(s, pos, expected, 0, expected.Length) != 0)
            {
                throw new FormatException("Expected '" + expected + "' (offset " + start + ").");
            }
            pos += expected.Length;
            return result;
        }

        private static string ParseString(string s, ref int pos)
        {
            int start = pos;
            if (pos >= s.Length || s[pos] != '"')
                throw new FormatException("Expected '\"' to start JSON string (offset " + pos + ").");

            pos++; // skip opening '"'
            StringBuilder sb = new StringBuilder();
            int len = s.Length;

            while (pos < len)
            {
                char c = s[pos++];
                if (c == '"')
                    return sb.ToString();

                if (c == '\\')
                {
                    if (pos >= len)
                        throw new FormatException("Truncated escape sequence in JSON string (offset " + pos + ").");

                    char esc = s[pos++];
                    switch (esc)
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
                            if (pos + 4 > len)
                                throw new FormatException("Truncated \\uXXXX escape in JSON string (offset " + (pos - 2) + ").");

                            int code;
                            if (!TryParseHex4(s, pos, out code))
                                throw new FormatException("Invalid \\uXXXX hex escape in JSON string (offset " + (pos - 2) + ").");
                            pos += 4;

                            if (code >= 0xD800 && code <= 0xDBFF &&
                                pos + 6 <= len && s[pos] == '\\' && s[pos + 1] == 'u')
                            {
                                int low;
                                if (TryParseHex4(s, pos + 2, out low) && low >= 0xDC00 && low <= 0xDFFF)
                                {
                                    int cp = 0x10000 + ((code - 0xD800) << 10) + (low - 0xDC00);
                                    sb.Append(char.ConvertFromUtf32(cp));
                                    pos += 6;
                                    break;
                                }
                            }
                            sb.Append((char)code);
                            break;
                        default:
                            throw new FormatException("Invalid escape sequence '\\" + esc + "' in JSON string (offset " + (pos - 2) + ").");
                    }
                }
                else if (c < 0x20)
                {
                    throw new FormatException("Unescaped control character in JSON string (offset " + (pos - 1) + ").");
                }
                else
                {
                    sb.Append(c);
                }
            }

            throw new FormatException("Unterminated JSON string starting at offset " + start + ".");
        }

        private static object ParseNumber(string s, ref int pos)
        {
            int start = pos;
            int len = s.Length;

            if (s[pos] == '-') pos++;
            if (pos >= len || s[pos] < '0' || s[pos] > '9')
                throw new FormatException("Invalid JSON number literal (offset " + start + ").");

            if (s[pos] == '0')
            {
                pos++;
                if (pos < len && s[pos] >= '0' && s[pos] <= '9')
                    throw new FormatException("Leading zeros are not permitted in JSON numbers (offset " + start + ").");
            }
            else
            {
                while (pos < len && s[pos] >= '0' && s[pos] <= '9') pos++;
            }

            bool isFloat = false;
            if (pos < len && s[pos] == '.')
            {
                isFloat = true;
                pos++;
                if (pos >= len || s[pos] < '0' || s[pos] > '9')
                    throw new FormatException("Missing fractional digits in JSON number (offset " + pos + ").");
                while (pos < len && s[pos] >= '0' && s[pos] <= '9') pos++;
            }

            if (pos < len && (s[pos] == 'e' || s[pos] == 'E'))
            {
                isFloat = true;
                pos++;
                if (pos < len && (s[pos] == '+' || s[pos] == '-')) pos++;
                if (pos >= len || s[pos] < '0' || s[pos] > '9')
                    throw new FormatException("Missing exponent digits in JSON number (offset " + pos + ").");
                while (pos < len && s[pos] >= '0' && s[pos] <= '9') pos++;
            }

            string token = s.Substring(start, pos - start);
            if (!isFloat)
            {
                long lv;
                if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out lv))
                    return lv;
            }

            double dv;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out dv) &&
                !double.IsNaN(dv) && !double.IsInfinity(dv))
            {
                return dv;
            }

            throw new FormatException("Invalid or overflowing JSON number '" + token + "' (offset " + start + ").");
        }

        private static void SkipWsAndComments(string s, ref int pos)
        {
            int len = s.Length;
            while (pos < len)
            {
                char c = s[pos];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    pos++;
                    continue;
                }
                if (c == '/' && pos + 1 < len)
                {
                    char next = s[pos + 1];
                    if (next == '/')
                    {
                        pos += 2;
                        while (pos < len && s[pos] != '\n' && s[pos] != '\r') pos++;
                        continue;
                    }
                    if (next == '*')
                    {
                        int commentStart = pos;
                        pos += 2;
                        bool closed = false;
                        while (pos + 1 < len)
                        {
                            if (s[pos] == '*' && s[pos + 1] == '/')
                            {
                                pos += 2;
                                closed = true;
                                break;
                            }
                            pos++;
                        }
                        if (!closed)
                            throw new FormatException("Unterminated block comment '/*' starting at offset " + commentStart + ".");
                        continue;
                    }
                }
                break;
            }
        }

        private static bool TryParseHex4(string s, int offset, out int code)
        {
            code = 0;
            if (s == null || offset < 0 || offset + 4 > s.Length) return false;
            for (int i = 0; i < 4; i++)
            {
                char c = s[offset + i];
                int v;
                if (c >= '0' && c <= '9') v = c - '0';
                else if (c >= 'a' && c <= 'f') v = 10 + (c - 'a');
                else if (c >= 'A' && c <= 'F') v = 10 + (c - 'A');
                else return false;
                code = (code << 4) | v;
            }
            return true;
        }

        // ====================================================================
        // 2. SERIALIZATION & ESCAPING
        // ====================================================================

        public static string Serialize(object val, bool indented = false)
        {
            StringBuilder sb = new StringBuilder();
            WriteValue(sb, val, indented, 0);
            return sb.ToString();
        }

        public static string EscapeString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            StringBuilder sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value, bool indented, int level)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (value is bool)
            {
                sb.Append(((bool)value) ? "true" : "false");
                return;
            }

            string str = value as string;
            if (str != null)
            {
                sb.Append('"').Append(EscapeString(str)).Append('"');
                return;
            }

            if (value is char)
            {
                sb.Append('"').Append(EscapeString(((char)value).ToString())).Append('"');
                return;
            }

            if (value is sbyte || value is byte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is double)
            {
                double d = (double)value;
                if (double.IsNaN(d) || double.IsInfinity(d)) sb.Append("null");
                else sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is float)
            {
                float f = (float)value;
                if (float.IsNaN(f) || float.IsInfinity(f)) sb.Append("null");
                else sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is decimal)
            {
                sb.Append(((decimal)value).ToString(CultureInfo.InvariantCulture));
                return;
            }

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                WriteDictionary(sb, dict, indented, level);
                return;
            }

            IDictionary<string, object> genericDict = value as IDictionary<string, object>;
            if (genericDict != null)
            {
                Dictionary<string, object> copy = new Dictionary<string, object>(genericDict, StringComparer.Ordinal);
                WriteDictionary(sb, copy, indented, level);
                return;
            }

            IEnumerable seq = value as IEnumerable;
            if (seq != null)
            {
                WriteArray(sb, seq, indented, level);
                return;
            }

            if (value is DateTime)
            {
                sb.Append('"').Append(EscapeString(((DateTime)value).ToString("o", CultureInfo.InvariantCulture))).Append('"');
                return;
            }

            sb.Append('"').Append(EscapeString(Convert.ToString(value, CultureInfo.InvariantCulture))).Append('"');
        }

        private static void WriteDictionary(StringBuilder sb, IDictionary dict, bool indented, int level)
        {
            if (dict.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            string childIndent = indented ? new string(' ', (level + 1) * 2) : string.Empty;
            string closeIndent = indented ? new string(' ', level * 2) : string.Empty;

            sb.Append(indented ? "{\r\n" : "{");
            bool first = true;
            foreach (DictionaryEntry entry in dict)
            {
                if (!first) sb.Append(indented ? ",\r\n" : ",");
                first = false;

                if (indented) sb.Append(childIndent);
                string keyStr = entry.Key != null ? Convert.ToString(entry.Key, CultureInfo.InvariantCulture) : string.Empty;
                sb.Append('"').Append(EscapeString(keyStr)).Append('"');
                sb.Append(indented ? ": " : ":");
                WriteValue(sb, entry.Value, indented, level + 1);
            }
            if (indented) sb.Append("\r\n").Append(closeIndent);
            sb.Append("}");
        }

        private static void WriteArray(StringBuilder sb, IEnumerable seq, bool indented, int level)
        {
            List<object> items = new List<object>();
            foreach (object item in seq) items.Add(item);

            if (items.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            string childIndent = indented ? new string(' ', (level + 1) * 2) : string.Empty;
            string closeIndent = indented ? new string(' ', level * 2) : string.Empty;

            sb.Append(indented ? "[\r\n" : "[");
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(indented ? ",\r\n" : ",");
                if (indented) sb.Append(childIndent);
                WriteValue(sb, items[i], indented, level + 1);
            }
            if (indented) sb.Append("\r\n").Append(closeIndent);
            sb.Append("]");
        }

        // ====================================================================
        // 3. TYPED ACCESSORS
        // ====================================================================

        public static Dictionary<string, object> AsDict(object obj)
        {
            if (obj == null) return null;

            Dictionary<string, object> exact = obj as Dictionary<string, object>;
            if (exact != null) return exact;

            IDictionary<string, object> generic = obj as IDictionary<string, object>;
            if (generic != null)
            {
                Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> kvp in generic)
                {
                    copy[kvp.Key] = kvp.Value;
                }
                return copy;
            }

            IDictionary nonGeneric = obj as IDictionary;
            if (nonGeneric != null)
            {
                Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (DictionaryEntry entry in nonGeneric)
                {
                    if (entry.Key != null)
                    {
                        copy[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = entry.Value;
                    }
                }
                return copy;
            }

            return null;
        }

        public static List<object> AsList(object obj)
        {
            if (obj == null || obj is string || obj is IDictionary) return null;

            List<object> exact = obj as List<object>;
            if (exact != null) return exact;

            IList<object> generic = obj as IList<object>;
            if (generic != null)
            {
                List<object> copy = new List<object>(generic.Count);
                for (int i = 0; i < generic.Count; i++) copy.Add(generic[i]);
                return copy;
            }

            IList nonGeneric = obj as IList;
            if (nonGeneric != null)
            {
                List<object> copy = new List<object>(nonGeneric.Count);
                for (int i = 0; i < nonGeneric.Count; i++) copy.Add(nonGeneric[i]);
                return copy;
            }

            return null;
        }

        public static string GetString(Dictionary<string, object> d, string key, string def = "")
        {
            return GetString((IDictionary<string, object>)d, key, def);
        }

        public static string GetString(IDictionary<string, object> d, string key, string def = "")
        {
            object val;
            if (!TryGetValueFlexible(d, key, out val) || val == null)
                return def;

            string s = val as string;
            if (s != null) return s;

            if (val is bool) return ((bool)val) ? "true" : "false";
            return Convert.ToString(val, CultureInfo.InvariantCulture);
        }

        public static bool GetBool(Dictionary<string, object> d, string key, bool def = false)
        {
            return GetBool((IDictionary<string, object>)d, key, def);
        }

        public static bool GetBool(IDictionary<string, object> d, string key, bool def = false)
        {
            object val;
            if (!TryGetValueFlexible(d, key, out val) || val == null)
                return def;

            if (val is bool) return (bool)val;
            if (val is long) return (long)val != 0L;
            if (val is int) return (int)val != 0;
            if (val is double) return Math.Abs((double)val) > double.Epsilon;

            string s = val as string;
            if (s != null)
            {
                string t = s.Trim();
                if (string.Equals(t, "true", StringComparison.OrdinalIgnoreCase) || t == "1") return true;
                if (string.Equals(t, "false", StringComparison.OrdinalIgnoreCase) || t == "0") return false;
            }

            return def;
        }

        public static int GetInt(Dictionary<string, object> d, string key, int def = 0)
        {
            return GetInt((IDictionary<string, object>)d, key, def);
        }

        public static int GetInt(IDictionary<string, object> d, string key, int def = 0)
        {
            object val;
            if (!TryGetValueFlexible(d, key, out val) || val == null)
                return def;

            if (val is int) return (int)val;
            if (val is long)
            {
                long lv = (long)val;
                if (lv >= int.MinValue && lv <= int.MaxValue) return (int)lv;
                return def;
            }
            if (val is double)
            {
                double dv = (double)val;
                if (dv >= int.MinValue && dv <= int.MaxValue) return (int)dv;
                return def;
            }

            string s = val as string;
            int parsed;
            if (s != null && int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return parsed;

            return def;
        }

        public static long GetLong(Dictionary<string, object> d, string key, long def = 0L)
        {
            return GetLong((IDictionary<string, object>)d, key, def);
        }

        public static long GetLong(IDictionary<string, object> d, string key, long def = 0L)
        {
            object val;
            if (!TryGetValueFlexible(d, key, out val) || val == null)
                return def;

            if (val is long) return (long)val;
            if (val is int) return (int)val;
            if (val is double)
            {
                double dv = (double)val;
                if (!double.IsNaN(dv) && dv >= -9223372036854775808.0 && dv < 9223372036854775808.0)
                    return (long)dv;
                return def;
            }

            string s = val as string;
            long parsed;
            if (s != null && long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return parsed;

            return def;
        }

        private static bool TryGetValueFlexible(IDictionary<string, object> dict, string key, out object value)
        {
            value = null;
            if (dict == null || key == null)
                return false;

            if (dict.TryGetValue(key, out value))
                return true;

            foreach (KeyValuePair<string, object> kvp in dict)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = kvp.Value;
                    return true;
                }
            }
            return false;
        }

        // ====================================================================
        // 4. COMMENT-PRESERVING JSONC PATCHER (P0-5 & P0-1)
        // ====================================================================

        private sealed class JsonObjectEntrySpan
        {
            public string Key;
            public int KeyStart;
            public int KeyEnd;
            public int ColonIndex;
            public int ValStart;
            public int ValEnd;
            public int CommaIndex;
        }

        private sealed class JsonObjectSpan
        {
            public int OpenBraceIndex;
            public int CloseBraceIndex;
            public List<JsonObjectEntrySpan> Entries = new List<JsonObjectEntrySpan>();
        }

        private sealed class JsonArrayElementSpan
        {
            public int ValStart;
            public int ValEnd;
            public int CommaIndex;
        }

        private sealed class JsonArraySpan
        {
            public int OpenBracketIndex;
            public int CloseBracketIndex;
            public List<JsonArrayElementSpan> Elements = new List<JsonArrayElementSpan>();
        }

        private sealed class TextEdit
        {
            public int Start;
            public int Length;
            public string Replacement;

            public TextEdit(int start, int length, string replacement)
            {
                Start = start;
                Length = length;
                Replacement = replacement ?? string.Empty;
            }
        }

        private enum PathStepKind
        {
            ObjectKey,
            ArrayIndex,
            ArrayWildcard
        }

        private sealed class PathStep
        {
            public PathStepKind Kind;
            public string Key;
            public int Index;
        }

        /// <summary>
        /// Updates or inserts a key inside either the root object (parentSectionOrNull == null/empty)
        /// or a named top-level section object, replacing ONLY the target value token span or inserting cleanly
        /// while preserving all existing comments and formatting.
        /// Fails closed (throws FormatException) on corrupted input JSONC or invalid value literal.
        /// </summary>
        public static string PatchObjectKeyPreservingComments(string rawJsonc, string parentSectionOrNull, string key, string rawJsonValueLiteral)
        {
            if (key == null) throw new ArgumentNullException("key");
            string cleanVal = ValidateSingleJsonValueLiteral(rawJsonValueLiteral);

            string source = string.IsNullOrWhiteSpace(rawJsonc) ? "{}" : rawJsonc;
            ValidateJsoncRootDocument(source);

            int rootStart = FindRootStart(source);
            if (rootStart >= source.Length || source[rootStart] != '{')
                throw new FormatException("JSONC root must be an object '{...}'.");

            JsonObjectSpan rootObj = ScanObjectSpan(source, rootStart, 0);
            string nl = DetectNewline(source);
            string indentUnit = DetectIndentUnit(source);
            List<TextEdit> edits = new List<TextEdit>();

            if (string.IsNullOrEmpty(parentSectionOrNull))
            {
                BuildObjectPatchEdits(source, rootObj, key, cleanVal, nl, indentUnit, edits);
            }
            else
            {
                int secIdx = FindObjectEntryIndex(rootObj.Entries, parentSectionOrNull);
                if (secIdx >= 0)
                {
                    JsonObjectEntrySpan secEntry = rootObj.Entries[secIdx];
                    if (source[secEntry.ValStart] != '{')
                        throw new InvalidOperationException("Target section '" + parentSectionOrNull + "' is not a JSON object.");

                    JsonObjectSpan secObj = ScanObjectSpan(source, secEntry.ValStart, 1);
                    BuildObjectPatchEdits(source, secObj, key, cleanVal, nl, indentUnit, edits);
                }
                else
                {
                    string objOuterIndent = GetLineIndentAt(source, rootObj.OpenBraceIndex);
                    string secIndent = DetectEntryIndent(source, rootObj, objOuterIndent, indentUnit);
                    string nestedIndent = secIndent + indentUnit;
                    string reindentedVal = ReindentMultilineValue(cleanVal, nestedIndent, nl);
                    string sectionLiteral = "{" + nl +
                                            nestedIndent + "\"" + EscapeString(key) + "\": " + reindentedVal + nl +
                                            secIndent + "}";
                    BuildObjectInsertEdits(source, rootObj, parentSectionOrNull, sectionLiteral, nl, indentUnit, edits);
                }
            }

            return ApplyEdits(source, edits);
        }

        /// <summary>
        /// Deletes a key inside either the root object (parentSectionOrNull == null/empty)
        /// or a named top-level section object, removing trailing/preceding commas as needed
        /// while preserving all comments and surrounding formatting.
        /// </summary>
        public static string DeleteObjectKeyPreservingComments(string rawJsonc, string parentSectionOrNull, string key)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (string.IsNullOrWhiteSpace(rawJsonc)) return "{}";

            ValidateJsoncRootDocument(rawJsonc);
            int rootStart = FindRootStart(rawJsonc);
            if (rootStart >= rawJsonc.Length || rawJsonc[rootStart] != '{')
                throw new FormatException("JSONC root must be an object '{...}'.");

            JsonObjectSpan rootObj = ScanObjectSpan(rawJsonc, rootStart, 0);
            List<TextEdit> edits = new List<TextEdit>();

            if (string.IsNullOrEmpty(parentSectionOrNull))
            {
                BuildObjectDeleteEdits(rawJsonc, rootObj, key, edits);
            }
            else
            {
                int secIdx = FindObjectEntryIndex(rootObj.Entries, parentSectionOrNull);
                if (secIdx < 0)
                    return rawJsonc;

                JsonObjectEntrySpan secEntry = rootObj.Entries[secIdx];
                if (rawJsonc[secEntry.ValStart] != '{')
                    throw new InvalidOperationException("Target section '" + parentSectionOrNull + "' is not a JSON object.");

                JsonObjectSpan secObj = ScanObjectSpan(rawJsonc, secEntry.ValStart, 1);
                BuildObjectDeleteEdits(rawJsonc, secObj, key, edits);
            }

            return edits.Count == 0 ? rawJsonc : ApplyEdits(rawJsonc, edits);
        }

        /// <summary>
        /// Patches a nested path in raw JSONC while preserving all comments and formatting.
        /// Supports object keys (including keys containing dots), array indices ("0" or "[0]" or "arr[0]"),
        /// and array wildcards ("*" or "[*]" or "arr[*]"), e.g. ["projectResources", "resources[*]", "gitFolder", "folderUri"].
        /// </summary>
        public static string PatchNestedPathPreservingComments(string rawJsonc, string[] pathSegments, string rawJsonValueLiteral)
        {
            if (pathSegments == null || pathSegments.Length == 0)
                throw new ArgumentException("pathSegments must contain at least one segment.", "pathSegments");

            string cleanVal = ValidateSingleJsonValueLiteral(rawJsonValueLiteral);
            List<PathStep> steps = ParsePathSteps(pathSegments);
            if (steps.Count == 0)
                throw new ArgumentException("pathSegments resolved to zero steps.", "pathSegments");

            bool hasWildcard = false;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].Kind == PathStepKind.ArrayWildcard)
                {
                    hasWildcard = true;
                    break;
                }
            }

            string source = string.IsNullOrWhiteSpace(rawJsonc) ? "{}" : rawJsonc;
            ValidateJsoncRootDocument(source);

            int rootStart = FindRootStart(source);
            string nl = DetectNewline(source);
            string indentUnit = DetectIndentUnit(source);

            List<TextEdit> edits = new List<TextEdit>();
            TraverseAndPatch(source, rootStart, steps, 0, cleanVal, nl, indentUnit, false, hasWildcard, edits, 0);
            return edits.Count == 0 ? source : ApplyEdits(source, edits);
        }

        private static void TraverseAndPatch(
            string source,
            int nodeStart,
            List<PathStep> steps,
            int stepIndex,
            string cleanVal,
            string nl,
            string indentUnit,
            bool insideWildcard,
            bool pathHasWildcard,
            List<TextEdit> edits,
            int depth)
        {
            if (depth > MaxParseDepth)
                throw new FormatException("JSON nesting depth exceeds limit.");

            PathStep step = steps[stepIndex];
            bool isLast = (stepIndex == steps.Count - 1);
            char token = source[nodeStart];

            if (token == '{')
            {
                if (step.Kind == PathStepKind.ArrayWildcard)
                    throw new InvalidOperationException("Wildcard '*' cannot be applied to a JSON object.");

                string key = step.Kind == PathStepKind.ObjectKey
                    ? step.Key
                    : step.Index.ToString(CultureInfo.InvariantCulture);

                JsonObjectSpan obj = ScanObjectSpan(source, nodeStart, depth);
                int foundIdx = FindObjectEntryIndex(obj.Entries, key);

                if (isLast)
                {
                    if (foundIdx >= 0)
                    {
                        JsonObjectEntrySpan entry = obj.Entries[foundIdx];
                        string entryIndent = GetLineIndentAt(source, entry.KeyStart);
                        string reindented = ReindentMultilineValue(cleanVal, entryIndent, nl);
                        edits.Add(new TextEdit(entry.ValStart, entry.ValEnd - entry.ValStart, reindented));
                    }
                    else
                    {
                        BuildObjectInsertEdits(source, obj, key, cleanVal, nl, indentUnit, edits);
                    }
                    return;
                }

                if (foundIdx >= 0)
                {
                    TraverseAndPatch(source, obj.Entries[foundIdx].ValStart, steps, stepIndex + 1, cleanVal, nl, indentUnit, insideWildcard, pathHasWildcard, edits, depth + 1);
                    return;
                }

                // Missing intermediate key:
                // - Inside wildcard (e.g. resources[*].gitFolder.folderUri where a resource lacks gitFolder) -> skip element.
                // - When whole path uses wildcard and array/container is absent -> no-op.
                if (insideWildcard || pathHasWildcard)
                    return;

                // Deterministic object-only suffix -> materialize nested object chain and insert cleanly.
                for (int r = stepIndex + 1; r < steps.Count; r++)
                {
                    if (steps[r].Kind != PathStepKind.ObjectKey)
                        throw new InvalidOperationException("Missing intermediate container '" + key + "' before array segment.");
                }

                string outerIndent = GetLineIndentAt(source, obj.OpenBraceIndex);
                string entryBaseIndent = DetectEntryIndent(source, obj, outerIndent, indentUnit);
                string builtNested = BuildNestedObjectLiteral(steps, stepIndex + 1, cleanVal, entryBaseIndent, indentUnit, nl);
                BuildObjectInsertEdits(source, obj, key, builtNested, nl, indentUnit, edits);
            }
            else if (token == '[')
            {
                JsonArraySpan arr = ScanArraySpan(source, nodeStart, depth);

                if (step.Kind == PathStepKind.ArrayWildcard)
                {
                    if (isLast)
                    {
                        for (int i = 0; i < arr.Elements.Count; i++)
                        {
                            JsonArrayElementSpan elem = arr.Elements[i];
                            string elemIndent = GetLineIndentAt(source, elem.ValStart);
                            string reindented = ReindentMultilineValue(cleanVal, elemIndent, nl);
                            edits.Add(new TextEdit(elem.ValStart, elem.ValEnd - elem.ValStart, reindented));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < arr.Elements.Count; i++)
                        {
                            char elemChar = source[arr.Elements[i].ValStart];
                            if (elemChar != '{' && elemChar != '[') continue;
                            TraverseAndPatch(source, arr.Elements[i].ValStart, steps, stepIndex + 1, cleanVal, nl, indentUnit, true, pathHasWildcard, edits, depth + 1);
                        }
                    }
                    return;
                }

                int targetIdx;
                if (step.Kind == PathStepKind.ArrayIndex)
                {
                    targetIdx = step.Index;
                }
                else
                {
                    if (!int.TryParse(step.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out targetIdx) || targetIdx < 0)
                        throw new InvalidOperationException("Cannot index JSON array with key '" + step.Key + "'.");
                }

                if (targetIdx < 0 || targetIdx >= arr.Elements.Count)
                {
                    if (insideWildcard) return;
                    throw new InvalidOperationException("JSON array index " + targetIdx + " out of range (length " + arr.Elements.Count + ").");
                }

                JsonArrayElementSpan targetElem = arr.Elements[targetIdx];
                if (isLast)
                {
                    string elemIndent = GetLineIndentAt(source, targetElem.ValStart);
                    string reindented = ReindentMultilineValue(cleanVal, elemIndent, nl);
                    edits.Add(new TextEdit(targetElem.ValStart, targetElem.ValEnd - targetElem.ValStart, reindented));
                }
                else
                {
                    TraverseAndPatch(source, targetElem.ValStart, steps, stepIndex + 1, cleanVal, nl, indentUnit, insideWildcard, pathHasWildcard, edits, depth + 1);
                }
            }
            else
            {
                if (insideWildcard) return;
                throw new InvalidOperationException("Cannot traverse into primitive JSON value at step '" + (step.Key ?? "*") + "'.");
            }
        }

        private static string BuildNestedObjectLiteral(List<PathStep> steps, int fromIndex, string cleanVal, string baseIndent, string indentUnit, string nl)
        {
            if (fromIndex >= steps.Count)
                return ReindentMultilineValue(cleanVal, baseIndent, nl);

            string innerIndent = baseIndent + indentUnit;
            string innerVal = BuildNestedObjectLiteral(steps, fromIndex + 1, cleanVal, innerIndent, indentUnit, nl);
            return "{" + nl +
                   innerIndent + "\"" + EscapeString(steps[fromIndex].Key) + "\": " + innerVal + nl +
                   baseIndent + "}";
        }

        private static List<PathStep> ParsePathSteps(string[] pathSegments)
        {
            List<PathStep> steps = new List<PathStep>();
            for (int i = 0; i < pathSegments.Length; i++)
            {
                string raw = pathSegments[i];
                if (string.IsNullOrEmpty(raw))
                    throw new ArgumentException("pathSegments[" + i + "] cannot be null or empty.", "pathSegments");

                if (raw == "*")
                {
                    steps.Add(new PathStep { Kind = PathStepKind.ArrayWildcard });
                    continue;
                }

                List<PathStep> trailingBrackets = new List<PathStep>();
                int end = raw.Length;
                while (end >= 3 && raw[end - 1] == ']')
                {
                    int openBracket = raw.LastIndexOf('[', end - 2);
                    if (openBracket < 0) break;

                    string inner = raw.Substring(openBracket + 1, end - openBracket - 2);
                    if (inner == "*")
                    {
                        trailingBrackets.Add(new PathStep { Kind = PathStepKind.ArrayWildcard });
                        end = openBracket;
                        continue;
                    }

                    int idx;
                    if (int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out idx) && idx >= 0)
                    {
                        trailingBrackets.Add(new PathStep { Kind = PathStepKind.ArrayIndex, Index = idx });
                        end = openBracket;
                        continue;
                    }

                    break;
                }

                if (trailingBrackets.Count > 0)
                {
                    if (end > 0)
                    {
                        steps.Add(new PathStep { Kind = PathStepKind.ObjectKey, Key = raw.Substring(0, end) });
                    }
                    for (int t = trailingBrackets.Count - 1; t >= 0; t--)
                    {
                        steps.Add(trailingBrackets[t]);
                    }
                    continue;
                }

                steps.Add(new PathStep { Kind = PathStepKind.ObjectKey, Key = raw });
            }
            return steps;
        }

        private static void BuildObjectPatchEdits(
            string source,
            JsonObjectSpan obj,
            string key,
            string cleanVal,
            string nl,
            string indentUnit,
            List<TextEdit> edits)
        {
            int idx = FindObjectEntryIndex(obj.Entries, key);
            if (idx >= 0)
            {
                JsonObjectEntrySpan entry = obj.Entries[idx];
                string entryIndent = GetLineIndentAt(source, entry.KeyStart);
                string reindented = ReindentMultilineValue(cleanVal, entryIndent, nl);
                edits.Add(new TextEdit(entry.ValStart, entry.ValEnd - entry.ValStart, reindented));
            }
            else
            {
                BuildObjectInsertEdits(source, obj, key, cleanVal, nl, indentUnit, edits);
            }
        }

        private static void BuildObjectInsertEdits(
            string source,
            JsonObjectSpan obj,
            string key,
            string cleanVal,
            string nl,
            string indentUnit,
            List<TextEdit> edits)
        {
            string quotedKey = "\"" + EscapeString(key) + "\"";
            string outerIndent = GetLineIndentAt(source, obj.OpenBraceIndex);
            string entryIndent = DetectEntryIndent(source, obj, outerIndent, indentUnit);
            string reindentedVal = ReindentMultilineValue(cleanVal, entryIndent, nl);
            bool hasNewlineInside = HasNewlineBetween(source, obj.OpenBraceIndex, obj.CloseBraceIndex);

            if (obj.Entries.Count == 0)
            {
                if (!hasNewlineInside)
                {
                    string insertion = nl + entryIndent + quotedKey + ": " + reindentedVal + nl + outerIndent;
                    edits.Add(new TextEdit(obj.CloseBraceIndex, 0, insertion));
                }
                else
                {
                    int closeLineStart;
                    if (IsClosingTokenAtLineStart(source, obj.CloseBraceIndex, out closeLineStart))
                    {
                        string insertion = entryIndent + quotedKey + ": " + reindentedVal + nl;
                        edits.Add(new TextEdit(closeLineStart, 0, insertion));
                    }
                    else
                    {
                        string insertion = nl + entryIndent + quotedKey + ": " + reindentedVal + nl + outerIndent;
                        edits.Add(new TextEdit(obj.CloseBraceIndex, 0, insertion));
                    }
                }
                return;
            }

            JsonObjectEntrySpan last = obj.Entries[obj.Entries.Count - 1];
            if (!hasNewlineInside)
            {
                if (last.CommaIndex < 0)
                {
                    edits.Add(new TextEdit(last.ValEnd, 0, ", " + quotedKey + ": " + reindentedVal));
                }
                else
                {
                    edits.Add(new TextEdit(last.CommaIndex + 1, 0, " " + quotedKey + ": " + reindentedVal));
                }
                return;
            }

            if (last.CommaIndex < 0)
            {
                edits.Add(new TextEdit(last.ValEnd, 0, ","));
            }

            int closeBraceLineStart;
            if (IsClosingTokenAtLineStart(source, obj.CloseBraceIndex, out closeBraceLineStart))
            {
                string insertion = entryIndent + quotedKey + ": " + reindentedVal + nl;
                edits.Add(new TextEdit(closeBraceLineStart, 0, insertion));
            }
            else
            {
                string insertion = nl + entryIndent + quotedKey + ": " + reindentedVal + nl + outerIndent;
                edits.Add(new TextEdit(obj.CloseBraceIndex, 0, insertion));
            }
        }

        private static void BuildObjectDeleteEdits(
            string source,
            JsonObjectSpan obj,
            string key,
            List<TextEdit> edits)
        {
            int idx = FindObjectEntryIndex(obj.Entries, key);
            if (idx < 0) return;

            JsonObjectEntrySpan target = obj.Entries[idx];
            int count = obj.Entries.Count;

            // If deleting the last entry when a previous entry exists, strip the previous entry's comma
            // so the resulting JSON remains strict-JSON valid without a dangling trailing comma.
            if (count > 1 && idx == count - 1)
            {
                JsonObjectEntrySpan prev = obj.Entries[idx - 1];
                if (prev.CommaIndex >= 0)
                {
                    edits.Add(new TextEdit(prev.CommaIndex, 1, string.Empty));
                }
            }

            int tokenEnd = target.CommaIndex >= 0 ? target.CommaIndex + 1 : target.ValEnd;
            bool hasInnerComment = target.CommaIndex >= 0 && HasNonWhitespaceBetween(source, target.ValEnd, target.CommaIndex);

            if (!hasInnerComment)
            {
                int delStart = target.KeyStart;
                int delEnd = tokenEnd;

                int lineStart = FindHorizontalLineStartIfCleanBefore(source, target.KeyStart);
                int lineEndWithNewline = FindHorizontalLineEndWithNewlineIfCleanAfter(source, tokenEnd);

                if (lineStart >= 0 && lineEndWithNewline >= 0)
                {
                    delStart = lineStart;
                    delEnd = lineEndWithNewline;
                }
                else
                {
                    // Single-line object cleanup: trim one adjacent space if bounded cleanly
                    if (delEnd < source.Length && source[delEnd] == ' ' && delStart > 0 && source[delStart - 1] == ' ')
                    {
                        delEnd++;
                    }
                    else if (delStart > 0 && source[delStart - 1] == ' ' &&
                             (delEnd < source.Length && (source[delEnd] == '}' || source[delEnd] == ',')))
                    {
                        delStart--;
                    }
                }

                edits.Add(new TextEdit(delStart, delEnd - delStart, string.Empty));
            }
            else
            {
                // Preserve comments placed between value and trailing comma
                edits.Add(new TextEdit(target.KeyStart, target.ValEnd - target.KeyStart, string.Empty));
                edits.Add(new TextEdit(target.CommaIndex, 1, string.Empty));
            }
        }

        private static int FindObjectEntryIndex(List<JsonObjectEntrySpan> entries, string key)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (string.Equals(entries[i].Key, key, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static string ValidateSingleJsonValueLiteral(string rawJsonValueLiteral)
        {
            if (string.IsNullOrWhiteSpace(rawJsonValueLiteral))
                throw new FormatException("JSON value literal cannot be empty.");

            string trimmed = rawJsonValueLiteral.Trim();
            Parse(trimmed);
            return trimmed;
        }

        private static void ValidateJsoncRootDocument(string rawJsonc)
        {
            Parse(rawJsonc);
        }

        private static int FindRootStart(string s)
        {
            int pos = 0;
            if (s.Length > 0 && s[0] == '\uFEFF') pos = 1;
            SkipWsAndComments(s, ref pos);
            return pos;
        }

        private static JsonObjectSpan ScanObjectSpan(string s, int openBraceIdx, int depth)
        {
            if (depth > MaxParseDepth)
                throw new FormatException("JSON nesting depth exceeds limit.");
            if (openBraceIdx >= s.Length || s[openBraceIdx] != '{')
                throw new FormatException("Expected '{' at offset " + openBraceIdx + ".");

            JsonObjectSpan obj = new JsonObjectSpan();
            obj.OpenBraceIndex = openBraceIdx;
            int pos = openBraceIdx + 1;

            while (true)
            {
                SkipWsAndComments(s, ref pos);
                if (pos >= s.Length)
                    throw new FormatException("Unterminated JSON object starting at offset " + openBraceIdx + ".");

                if (s[pos] == '}')
                {
                    obj.CloseBraceIndex = pos;
                    return obj;
                }

                if (obj.Entries.Count > 0 && obj.Entries[obj.Entries.Count - 1].CommaIndex < 0)
                    throw new FormatException("Missing ',' between JSON object entries at offset " + pos + ".");

                if (s[pos] != '"')
                    throw new FormatException("Expected property name string at offset " + pos + ".");

                int keyStart = pos;
                string key = ParseString(s, ref pos);
                int keyEnd = pos;

                SkipWsAndComments(s, ref pos);
                if (pos >= s.Length || s[pos] != ':')
                    throw new FormatException("Expected ':' after property '" + key + "' at offset " + pos + ".");

                int colonIdx = pos;
                pos++; // skip ':'

                SkipWsAndComments(s, ref pos);
                if (pos >= s.Length)
                    throw new FormatException("Missing property value for '" + key + "' at offset " + pos + ".");

                int valStart = pos;
                SkipSingleValue(s, ref pos, depth + 1);
                int valEnd = pos;

                SkipWsAndComments(s, ref pos);
                int commaIdx = -1;
                if (pos < s.Length && s[pos] == ',')
                {
                    commaIdx = pos;
                    pos++;
                }

                obj.Entries.Add(new JsonObjectEntrySpan
                {
                    Key = key,
                    KeyStart = keyStart,
                    KeyEnd = keyEnd,
                    ColonIndex = colonIdx,
                    ValStart = valStart,
                    ValEnd = valEnd,
                    CommaIndex = commaIdx
                });
            }
        }

        private static JsonArraySpan ScanArraySpan(string s, int openBracketIdx, int depth)
        {
            if (depth > MaxParseDepth)
                throw new FormatException("JSON nesting depth exceeds limit.");
            if (openBracketIdx >= s.Length || s[openBracketIdx] != '[')
                throw new FormatException("Expected '[' at offset " + openBracketIdx + ".");

            JsonArraySpan arr = new JsonArraySpan();
            arr.OpenBracketIndex = openBracketIdx;
            int pos = openBracketIdx + 1;

            while (true)
            {
                SkipWsAndComments(s, ref pos);
                if (pos >= s.Length)
                    throw new FormatException("Unterminated JSON array starting at offset " + openBracketIdx + ".");

                if (s[pos] == ']')
                {
                    arr.CloseBracketIndex = pos;
                    return arr;
                }

                if (arr.Elements.Count > 0 && arr.Elements[arr.Elements.Count - 1].CommaIndex < 0)
                    throw new FormatException("Missing ',' between JSON array elements at offset " + pos + ".");

                int valStart = pos;
                SkipSingleValue(s, ref pos, depth + 1);
                int valEnd = pos;

                SkipWsAndComments(s, ref pos);
                int commaIdx = -1;
                if (pos < s.Length && s[pos] == ',')
                {
                    commaIdx = pos;
                    pos++;
                }

                arr.Elements.Add(new JsonArrayElementSpan
                {
                    ValStart = valStart,
                    ValEnd = valEnd,
                    CommaIndex = commaIdx
                });
            }
        }

        private static void SkipSingleValue(string s, ref int pos, int depth)
        {
            if (pos >= s.Length)
                throw new FormatException("Unexpected end of JSON value at offset " + pos + ".");

            char c = s[pos];
            if (c == '"')
            {
                ParseString(s, ref pos);
                return;
            }
            if (c == '{')
            {
                JsonObjectSpan nestedObj = ScanObjectSpan(s, pos, depth);
                pos = nestedObj.CloseBraceIndex + 1;
                return;
            }
            if (c == '[')
            {
                JsonArraySpan nestedArr = ScanArraySpan(s, pos, depth);
                pos = nestedArr.CloseBracketIndex + 1;
                return;
            }
            if (c == 't')
            {
                ParseLiteral(s, ref pos, "true", true);
                return;
            }
            if (c == 'f')
            {
                ParseLiteral(s, ref pos, "false", false);
                return;
            }
            if (c == 'n')
            {
                ParseLiteral(s, ref pos, "null", null);
                return;
            }
            if (c == '-' || (c >= '0' && c <= '9'))
            {
                ParseNumber(s, ref pos);
                return;
            }

            throw new FormatException("Unexpected character '" + c + "' in JSON value at offset " + pos + ".");
        }

        private static string ApplyEdits(string source, List<TextEdit> edits)
        {
            if (edits == null || edits.Count == 0) return source;

            List<KeyValuePair<int, TextEdit>> indexed = new List<KeyValuePair<int, TextEdit>>(edits.Count);
            for (int i = 0; i < edits.Count; i++)
                indexed.Add(new KeyValuePair<int, TextEdit>(i, edits[i]));

            indexed.Sort(delegate(KeyValuePair<int, TextEdit> a, KeyValuePair<int, TextEdit> b)
            {
                int cmp = a.Value.Start.CompareTo(b.Value.Start);
                if (cmp != 0) return cmp;
                cmp = a.Value.Length.CompareTo(b.Value.Length);
                if (cmp != 0) return cmp;
                return a.Key.CompareTo(b.Key);
            });

            StringBuilder sb = new StringBuilder(source.Length + 64);
            int cursor = 0;
            for (int i = 0; i < indexed.Count; i++)
            {
                TextEdit e = indexed[i].Value;
                if (e.Start < cursor)
                    throw new InvalidOperationException("Overlapping JSONC patch edit detected.");
                if (e.Start > cursor)
                    sb.Append(source, cursor, e.Start - cursor);
                sb.Append(e.Replacement);
                cursor = e.Start + e.Length;
            }
            if (cursor < source.Length)
                sb.Append(source, cursor, source.Length - cursor);

            return sb.ToString();
        }

        private static string DetectNewline(string s)
        {
            int lf = s.IndexOf('\n');
            if (lf > 0 && s[lf - 1] == '\r') return "\r\n";
            if (lf >= 0) return "\n";
            return "\r\n";
        }

        private static string DetectIndentUnit(string s)
        {
            int i = 0;
            while (i < s.Length)
            {
                int nl = s.IndexOf('\n', i);
                if (nl < 0) break;
                int lineStart = nl + 1;
                if (lineStart >= s.Length) break;

                int wsEnd = lineStart;
                while (wsEnd < s.Length && (s[wsEnd] == ' ' || s[wsEnd] == '\t')) wsEnd++;
                if (wsEnd > lineStart && wsEnd < s.Length && s[wsEnd] != '\r' && s[wsEnd] != '\n')
                {
                    if (s[lineStart] == '\t') return "\t";
                    int spaces = wsEnd - lineStart;
                    if (spaces >= 4 && (spaces % 4 == 0)) return "    ";
                    if (spaces >= 2) return "  ";
                }
                i = wsEnd;
            }
            return "  ";
        }

        private static string GetLineIndentAt(string s, int index)
        {
            if (index <= 0 || index > s.Length) return string.Empty;
            int lineStart = index;
            while (lineStart > 0 && s[lineStart - 1] != '\n' && s[lineStart - 1] != '\r')
            {
                lineStart--;
            }
            int wsEnd = lineStart;
            while (wsEnd < index && (s[wsEnd] == ' ' || s[wsEnd] == '\t'))
            {
                wsEnd++;
            }
            return s.Substring(lineStart, wsEnd - lineStart);
        }

        private static string DetectEntryIndent(string s, JsonObjectSpan obj, string outerIndent, string indentUnit)
        {
            if (obj.Entries.Count > 0)
            {
                int firstKeyStart = obj.Entries[0].KeyStart;
                int lineStart = firstKeyStart;
                while (lineStart > 0 && s[lineStart - 1] != '\n' && s[lineStart - 1] != '\r') lineStart--;
                bool onlyWsBefore = true;
                for (int i = lineStart; i < firstKeyStart; i++)
                {
                    if (s[i] != ' ' && s[i] != '\t') { onlyWsBefore = false; break; }
                }
                if (onlyWsBefore)
                    return s.Substring(lineStart, firstKeyStart - lineStart);
            }
            return outerIndent + indentUnit;
        }

        private static bool HasNewlineBetween(string s, int start, int end)
        {
            for (int i = start + 1; i < end && i < s.Length; i++)
            {
                if (s[i] == '\n' || s[i] == '\r') return true;
            }
            return false;
        }

        private static bool IsClosingTokenAtLineStart(string s, int closeIdx, out int lineStart)
        {
            lineStart = closeIdx;
            while (lineStart > 0 && s[lineStart - 1] != '\n' && s[lineStart - 1] != '\r')
            {
                char ch = s[lineStart - 1];
                if (ch != ' ' && ch != '\t')
                    return false;
                lineStart--;
            }
            return true;
        }

        private static bool HasNonWhitespaceBetween(string s, int start, int end)
        {
            for (int i = start; i < end && i < s.Length; i++)
            {
                char ch = s[i];
                if (ch != ' ' && ch != '\t' && ch != '\r' && ch != '\n')
                    return true;
            }
            return false;
        }

        private static int FindHorizontalLineStartIfCleanBefore(string s, int index)
        {
            int pos = index;
            while (pos > 0)
            {
                char c = s[pos - 1];
                if (c == '\n' || c == '\r') return pos;
                if (c != ' ' && c != '\t') return -1;
                pos--;
            }
            return 0;
        }

        private static int FindHorizontalLineEndWithNewlineIfCleanAfter(string s, int index)
        {
            int pos = index;
            while (pos < s.Length)
            {
                char c = s[pos];
                if (c == '\r')
                {
                    if (pos + 1 < s.Length && s[pos + 1] == '\n') return pos + 2;
                    return pos + 1;
                }
                if (c == '\n')
                {
                    return pos + 1;
                }
                if (c != ' ' && c != '\t')
                {
                    return -1;
                }
                pos++;
            }
            return s.Length;
        }

        private static string ReindentMultilineValue(string rawTrimmed, string baseIndent, string nl)
        {
            if (string.IsNullOrEmpty(rawTrimmed) || rawTrimmed.IndexOf('\n') < 0)
                return rawTrimmed ?? "null";

            string normalized = rawTrimmed.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            if (lines.Length <= 1) return rawTrimmed;

            int minCommonIndent = int.MaxValue;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                int indent = 0;
                while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t'))
                    indent++;
                if (indent < minCommonIndent) minCommonIndent = indent;
            }
            if (minCommonIndent == int.MaxValue) minCommonIndent = 0;

            StringBuilder sb = new StringBuilder();
            sb.Append(lines[0].Trim());
            for (int i = 1; i < lines.Length; i++)
            {
                sb.Append(nl);
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                string stripped = line.Length >= minCommonIndent ? line.Substring(minCommonIndent) : line.TrimStart(' ', '\t');
                sb.Append(baseIndent).Append(stripped);
            }
            return sb.ToString();
        }

        // ====================================================================
        // 5. TIMESTAMPED SINGLE-COPY BACKUP & PRUNING
        // ====================================================================

        /// <summary>
        /// Creates at most ONE timestamped backup (&lt;file&gt;.bak.&lt;yyyyMMdd_HHmmss&gt;, or with _fff/_seq on collision)
        /// and prunes older .bak.* files beyond maxKeep. Returns the created backup path, or null if filePath does not exist.
        /// </summary>
        public static string CreateTimestampedBackup(string filePath, int maxKeep = 5)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            string dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            string fileName = Path.GetFileName(filePath);

            DateTime now = DateTime.Now;
            string baseStamp = now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string backupPath = filePath + ".bak." + baseStamp;

            int maxExistingMillis = -1;
            int maxSeqForMaxMillis = -1;
            bool anySameSecondExists = File.Exists(backupPath);

            try
            {
                string sameSecondPrefix = fileName + ".bak." + baseStamp;
                string[] existingSameSecond = Directory.GetFiles(dir, sameSecondPrefix + "*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < existingSameSecond.Length; i++)
                {
                    string name = Path.GetFileName(existingSameSecond[i]);
                    if (name == null || !name.StartsWith(sameSecondPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (name.Length == sameSecondPrefix.Length)
                    {
                        anySameSecondExists = true;
                        continue;
                    }

                    if (name[sameSecondPrefix.Length] == '_')
                    {
                        anySameSecondExists = true;
                        BackupSuffixKey parsedKey = ParseBackupSuffixKey(name.Substring(fileName.Length + 5));
                        if (parsedKey.HasTimestamp)
                        {
                            if (parsedKey.Millis > maxExistingMillis)
                            {
                                maxExistingMillis = parsedKey.Millis;
                                maxSeqForMaxMillis = parsedKey.Seq;
                            }
                            else if (parsedKey.Millis == maxExistingMillis && parsedKey.Seq > maxSeqForMaxMillis)
                            {
                                maxSeqForMaxMillis = parsedKey.Seq;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            if (anySameSecondExists)
            {
                int ms = now.Millisecond;
                if (maxExistingMillis > ms)
                {
                    ms = maxExistingMillis;
                }
                string msStamp = baseStamp + "_" + ms.ToString("D3", CultureInfo.InvariantCulture);
                backupPath = filePath + ".bak." + msStamp;
                if (ms == maxExistingMillis || File.Exists(backupPath))
                {
                    int seq = maxSeqForMaxMillis >= 1 ? maxSeqForMaxMillis + 1 : 1;
                    backupPath = filePath + ".bak." + msStamp + "_" + seq.ToString(CultureInfo.InvariantCulture);
                    while (File.Exists(backupPath))
                    {
                        seq++;
                        backupPath = filePath + ".bak." + msStamp + "_" + seq.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }

            using (FileStream src = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (FileStream dst = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                src.CopyTo(dst);
                dst.Flush();
            }

            PruneTimestampedBackups(dir, fileName, maxKeep);
            return backupPath;
        }

        private static void PruneTimestampedBackups(string dir, string fileName, int maxKeep)
        {
            if (maxKeep < 0) maxKeep = 0;

            string prefix = fileName + ".bak.";
            string[] candidates;
            try
            {
                candidates = Directory.GetFiles(dir, prefix + "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return;
            }

            List<string> matches = new List<string>();
            for (int i = 0; i < candidates.Length; i++)
            {
                string name = Path.GetFileName(candidates[i]);
                if (name != null && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.Length > prefix.Length)
                {
                    matches.Add(candidates[i]);
                }
            }

            if (matches.Count <= maxKeep) return;

            matches.Sort(delegate(string a, string b)
            {
                return CompareBackupPathsDescending(a, b, prefix.Length);
            });

            for (int i = maxKeep; i < matches.Count; i++)
            {
                try
                {
                    File.SetAttributes(matches[i], FileAttributes.Normal);
                    File.Delete(matches[i]);
                }
                catch
                {
                }
            }
        }

        private static int CompareBackupPathsDescending(string pathA, string pathB, int prefixLen)
        {
            string nameA = Path.GetFileName(pathA) ?? string.Empty;
            string nameB = Path.GetFileName(pathB) ?? string.Empty;

            string sufA = nameA.Length > prefixLen ? nameA.Substring(prefixLen) : string.Empty;
            string sufB = nameB.Length > prefixLen ? nameB.Substring(prefixLen) : string.Empty;

            BackupSuffixKey keyA = ParseBackupSuffixKey(sufA);
            BackupSuffixKey keyB = ParseBackupSuffixKey(sufB);

            if (keyA.HasTimestamp && keyB.HasTimestamp)
            {
                int cmpBase = string.CompareOrdinal(keyB.BaseStamp, keyA.BaseStamp);
                if (cmpBase != 0) return cmpBase;

                int cmpMs = keyB.Millis.CompareTo(keyA.Millis);
                if (cmpMs != 0) return cmpMs;

                int cmpSeq = keyB.Seq.CompareTo(keyA.Seq);
                if (cmpSeq != 0) return cmpSeq;

                return string.Compare(nameB, nameA, StringComparison.OrdinalIgnoreCase);
            }

            if (keyA.HasTimestamp != keyB.HasTimestamp)
                return keyA.HasTimestamp ? -1 : 1;

            DateTime dtA = File.GetLastWriteTimeUtc(pathA);
            DateTime dtB = File.GetLastWriteTimeUtc(pathB);
            int cmpTime = dtB.CompareTo(dtA);
            if (cmpTime != 0) return cmpTime;

            return string.Compare(nameB, nameA, StringComparison.OrdinalIgnoreCase);
        }

        private struct BackupSuffixKey
        {
            public bool HasTimestamp;
            public string BaseStamp;
            public int Millis;
            public int Seq;
        }

        private static BackupSuffixKey ParseBackupSuffixKey(string suffix)
        {
            BackupSuffixKey k = new BackupSuffixKey();
            if (string.IsNullOrEmpty(suffix) || suffix.Length < 15) return k;
            if (suffix[8] != '_') return k;

            for (int i = 0; i < 15; i++)
            {
                if (i == 8) continue;
                if (suffix[i] < '0' || suffix[i] > '9') return k;
            }

            k.HasTimestamp = true;
            k.BaseStamp = suffix.Substring(0, 15);
            k.Millis = -1;
            k.Seq = -1;

            if (suffix.Length > 15 && suffix[15] == '_')
            {
                string rest = suffix.Substring(16);
                string[] parts = rest.Split('_');
                int ms;
                if (parts.Length >= 1 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out ms))
                {
                    k.Millis = ms;
                }
                int seq;
                if (parts.Length >= 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out seq))
                {
                    k.Seq = seq;
                }
            }

            return k;
        }
    }
}
