using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // 1. ZERO-DEPENDENCY STRICT JSON CODEC (C# 5.0)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 零外部依赖的 RFC 8259 严格 JSON 解析器、序列化器与强类型字典读取辅助类
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new FormatException("JSON 内容为空 (offset 0)。");

            int pos = 0;
            if (json[0] == '\uFEFF') pos = 1;

            SkipWhitespace(json, ref pos);
            if (pos >= json.Length)
                throw new FormatException("JSON 未包含任何根节点值 (offset " + pos + ")。");

            object value = ParseValue(json, ref pos);
            SkipWhitespace(json, ref pos);
            if (pos < json.Length)
                throw new FormatException("JSON 根节点结束后存在多余字符 '" + json[pos] + "' (offset " + pos + ")。");

            return value;
        }

        public static string Serialize(object value)
        {
            return Serialize(value, false);
        }

        public static string Serialize(object value, bool pretty)
        {
            StringBuilder sb = new StringBuilder();
            WriteValue(sb, value, pretty, 0);
            return sb.ToString();
        }

        public static string GetString(IDictionary<string, object> dict, string key)
        {
            return GetString(dict, key, null);
        }

        public static string GetString(IDictionary<string, object> dict, string key, string defaultValue)
        {
            object val;
            if (!TryGetValueFlexible(dict, key, out val) || val == null)
                return defaultValue;

            string s = val as string;
            if (s != null) return s;

            return Convert.ToString(val, CultureInfo.InvariantCulture);
        }

        public static bool GetBool(IDictionary<string, object> dict, string key)
        {
            return GetBool(dict, key, false);
        }

        public static bool GetBool(IDictionary<string, object> dict, string key, bool defaultValue)
        {
            object val;
            if (!TryGetValueFlexible(dict, key, out val) || val == null)
                return defaultValue;

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

            return defaultValue;
        }

        public static int GetInt(IDictionary<string, object> dict, string key)
        {
            return GetInt(dict, key, 0);
        }

        public static int GetInt(IDictionary<string, object> dict, string key, int defaultValue)
        {
            object val;
            if (!TryGetValueFlexible(dict, key, out val) || val == null)
                return defaultValue;

            if (val is int) return (int)val;
            if (val is long)
            {
                long lv = (long)val;
                if (lv >= int.MinValue && lv <= int.MaxValue) return (int)lv;
                return defaultValue;
            }
            if (val is double)
            {
                double dv = (double)val;
                if (dv >= int.MinValue && dv <= int.MaxValue) return (int)dv;
                return defaultValue;
            }

            string s = val as string;
            int parsed;
            if (s != null && int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return parsed;

            return defaultValue;
        }

        public static long GetLong(IDictionary<string, object> dict, string key)
        {
            return GetLong(dict, key, 0L);
        }

        public static long GetLong(IDictionary<string, object> dict, string key, long defaultValue)
        {
            object val;
            if (!TryGetValueFlexible(dict, key, out val) || val == null)
                return defaultValue;

            if (val is long) return (long)val;
            if (val is int) return (int)val;
            if (val is double)
            {
                double dv = (double)val;
                if (dv >= long.MinValue && dv <= long.MaxValue) return (long)dv;
                return defaultValue;
            }

            string s = val as string;
            long parsed;
            if (s != null && long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return parsed;

            return defaultValue;
        }

        public static Dictionary<string, object> GetDict(IDictionary<string, object> dict, string key)
        {
            object val;
            if (!TryGetValueFlexible(dict, key, out val) || val == null)
                return null;

            Dictionary<string, object> exact = val as Dictionary<string, object>;
            if (exact != null) return exact;

            IDictionary<string, object> generic = val as IDictionary<string, object>;
            if (generic != null)
            {
                Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> kvp in generic)
                {
                    copy[kvp.Key] = kvp.Value;
                }
                return copy;
            }

            return null;
        }

        public static List<object> GetList(IDictionary<string, object> dict, string key)
        {
            object val;
            if (!TryGetValueFlexible(dict, key, out val) || val == null)
                return null;

            List<object> exact = val as List<object>;
            if (exact != null) return exact;

            IList<object> generic = val as IList<object>;
            if (generic != null)
            {
                List<object> copy = new List<object>(generic.Count);
                for (int i = 0; i < generic.Count; i++) copy.Add(generic[i]);
                return copy;
            }

            return null;
        }

        private static bool TryGetValueFlexible(IDictionary<string, object> dict, string key, out object value)
        {
            value = null;
            if (dict == null || string.IsNullOrEmpty(key))
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

        private static void SkipWhitespace(string s, ref int pos)
        {
            int len = s.Length;
            while (pos < len)
            {
                char c = s[pos];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    pos++;
                else
                    break;
            }
        }

        private static object ParseValue(string s, ref int pos)
        {
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length)
                throw new FormatException("JSON 期望值但提前结束 (offset " + pos + ")。");

            char c = s[pos];
            if (c == '{') return ParseObject(s, ref pos);
            if (c == '[') return ParseArray(s, ref pos);
            if (c == '"') return ParseString(s, ref pos);
            if (c == 't') return ParseLiteral(s, ref pos, "true", true);
            if (c == 'f') return ParseLiteral(s, ref pos, "false", false);
            if (c == 'n') return ParseLiteral(s, ref pos, "null", null);
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(s, ref pos);

            throw new FormatException("JSON 非法字符 '" + c + "' (offset " + pos + ")。");
        }

        private static object ParseLiteral(string s, ref int pos, string expected, object result)
        {
            int start = pos;
            if (pos + expected.Length > s.Length ||
                string.CompareOrdinal(s, pos, expected, 0, expected.Length) != 0)
            {
                throw new FormatException("JSON 期望 '" + expected + "' (offset " + start + ")。");
            }
            pos += expected.Length;
            return result;
        }

        private static Dictionary<string, object> ParseObject(string s, ref int pos)
        {
            pos++; // skip '{'
            Dictionary<string, object> dict = new Dictionary<string, object>(StringComparer.Ordinal);
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == '}')
            {
                pos++;
                return dict;
            }

            while (true)
            {
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length || s[pos] != '"')
                    throw new FormatException("JSON 对象期望双引号属性名 (offset " + pos + ")。");

                string key = ParseString(s, ref pos);
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length || s[pos] != ':')
                    throw new FormatException("JSON 属性名后缺少 ':' (offset " + pos + ")。");

                pos++; // skip ':'
                object val = ParseValue(s, ref pos);
                dict[key] = val;

                SkipWhitespace(s, ref pos);
                if (pos >= s.Length)
                    throw new FormatException("JSON 对象缺少闭合 '}' (offset " + pos + ")。");

                char next = s[pos++];
                if (next == '}') return dict;
                if (next != ',')
                    throw new FormatException("JSON 对象期望 ',' 或 '}' 但遇到 '" + next + "' (offset " + (pos - 1) + ")。");
            }
        }

        private static List<object> ParseArray(string s, ref int pos)
        {
            pos++; // skip '['
            List<object> list = new List<object>();
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == ']')
            {
                pos++;
                return list;
            }

            while (true)
            {
                object item = ParseValue(s, ref pos);
                list.Add(item);

                SkipWhitespace(s, ref pos);
                if (pos >= s.Length)
                    throw new FormatException("JSON 数组缺少闭合 ']' (offset " + pos + ")。");

                char next = s[pos++];
                if (next == ']') return list;
                if (next != ',')
                    throw new FormatException("JSON 数组期望 ',' 或 ']' 但遇到 '" + next + "' (offset " + (pos - 1) + ")。");
            }
        }

        private static string ParseString(string s, ref int pos)
        {
            int start = pos;
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
                        throw new FormatException("JSON 字符串转义截断 (offset " + pos + ")。");

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
                                throw new FormatException("JSON \\uXXXX 转义截断 (offset " + (pos - 2) + ")。");

                            int code;
                            if (!TryParseHex4(s, pos, out code))
                                throw new FormatException("JSON \\uXXXX 非法十六进制转义 (offset " + (pos - 2) + ")。");
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
                            throw new FormatException("JSON 非法转义序列 '\\" + esc + "' (offset " + (pos - 2) + ")。");
                    }
                }
                else if (c < 0x20)
                {
                    throw new FormatException("JSON 字符串中存在未转义控制字符 (offset " + (pos - 1) + ")。");
                }
                else
                {
                    sb.Append(c);
                }
            }

            throw new FormatException("JSON 字符串未闭合，起始于 offset " + start + "。");
        }

        private static object ParseNumber(string s, ref int pos)
        {
            int start = pos;
            int len = s.Length;

            if (s[pos] == '-') pos++;
            if (pos >= len || s[pos] < '0' || s[pos] > '9')
                throw new FormatException("JSON 非法数字字面量 (offset " + start + ")。");

            if (s[pos] == '0')
            {
                pos++;
                if (pos < len && s[pos] >= '0' && s[pos] <= '9')
                    throw new FormatException("JSON 数字不允许前导零 (offset " + start + ")。");
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
                    throw new FormatException("JSON 小数点后缺少数字 (offset " + pos + ")。");
                while (pos < len && s[pos] >= '0' && s[pos] <= '9') pos++;
            }

            if (pos < len && (s[pos] == 'e' || s[pos] == 'E'))
            {
                isFloat = true;
                pos++;
                if (pos < len && (s[pos] == '+' || s[pos] == '-')) pos++;
                if (pos >= len || s[pos] < '0' || s[pos] > '9')
                    throw new FormatException("JSON 指数部分缺少数字 (offset " + pos + ")。");
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

            throw new FormatException("JSON 数字格式错误或数值溢出: " + token + " (offset " + start + ")。");
        }

        internal static bool TryParseHex4(string s, int offset, out int code)
        {
            code = 0;
            if (s == null || offset < 0 || offset + 4 > s.Length) return false;
            for (int i = 0; i < 4; i++)
            {
                int v = HexDigitValue(s[offset + i]);
                if (v < 0) return false;
                code = (code << 4) | v;
            }
            return true;
        }

        internal static int HexDigitValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return 10 + (c - 'a');
            if (c >= 'A' && c <= 'F') return 10 + (c - 'A');
            return -1;
        }

        private static void WriteValue(StringBuilder sb, object value, bool pretty, int indent)
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
                sb.Append('"').Append(JsonKeyManipulator.EscapeJsonString(str)).Append('"');
                return;
            }

            if (value is char)
            {
                sb.Append('"').Append(JsonKeyManipulator.EscapeJsonString(((char)value).ToString())).Append('"');
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
                WriteDictionary(sb, dict, pretty, indent);
                return;
            }

            IEnumerable seq = value as IEnumerable;
            if (seq != null)
            {
                WriteArray(sb, seq, pretty, indent);
                return;
            }

            if (value is DateTime)
            {
                sb.Append('"').Append(JsonKeyManipulator.EscapeJsonString(((DateTime)value).ToString("o", CultureInfo.InvariantCulture))).Append('"');
                return;
            }

            sb.Append('"').Append(JsonKeyManipulator.EscapeJsonString(Convert.ToString(value, CultureInfo.InvariantCulture))).Append('"');
        }

        private static void WriteDictionary(StringBuilder sb, IDictionary dict, bool pretty, int indent)
        {
            if (dict.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            string childIndent = pretty ? new string(' ', (indent + 1) * 2) : string.Empty;
            string closeIndent = pretty ? new string(' ', indent * 2) : string.Empty;

            sb.Append(pretty ? "{\r\n" : "{");
            bool first = true;
            foreach (DictionaryEntry entry in dict)
            {
                if (!first) sb.Append(pretty ? ",\r\n" : ",");
                first = false;

                if (pretty) sb.Append(childIndent);
                string keyStr = entry.Key != null ? Convert.ToString(entry.Key, CultureInfo.InvariantCulture) : string.Empty;
                sb.Append('"').Append(JsonKeyManipulator.EscapeJsonString(keyStr)).Append('"');
                sb.Append(pretty ? ": " : ":");
                WriteValue(sb, entry.Value, pretty, indent + 1);
            }
            if (pretty) sb.Append("\r\n").Append(closeIndent);
            sb.Append("}");
        }

        private static void WriteArray(StringBuilder sb, IEnumerable seq, bool pretty, int indent)
        {
            List<object> items = new List<object>();
            foreach (object item in seq) items.Add(item);

            if (items.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            string childIndent = pretty ? new string(' ', (indent + 1) * 2) : string.Empty;
            string closeIndent = pretty ? new string(' ', indent * 2) : string.Empty;

            sb.Append(pretty ? "[\r\n" : "[");
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(pretty ? ",\r\n" : ",");
                if (pretty) sb.Append(childIndent);
                WriteValue(sb, items[i], pretty, indent + 1);
            }
            if (pretty) sb.Append("\r\n").Append(closeIndent);
            sb.Append("]");
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // 2. PROTOBUF TEXT FORMAT (.pbtxt) ANNOTATION TITLE CODEC
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Antigravity annotations/&lt;cid&gt;.pbtxt 顶层 title 字段无损编解码器
    /// 支持 C/Protobuf 连续八进制 UTF-8 字节序列与嵌套块深度隔离，保留其余字段字节原样不变
    /// </summary>
    public static class PbtxtAnnotationCodec
    {
        private struct TopLevelTitleSpan
        {
            public bool Found;
            public int ValueStart;
            public int ValueEnd;
            public string DecodedTitle;
        }

        public static string ExtractTitle(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            TopLevelTitleSpan span = FindTopLevelTitleSpan(content, true);
            return span.Found ? (span.DecodedTitle ?? string.Empty) : string.Empty;
        }

        public static string UpsertTitle(string existingContent, string newTitle)
        {
            string escaped = EscapePbtxtString(newTitle ?? string.Empty);
            string formattedLiteral = "\"" + escaped + "\"";

            if (string.IsNullOrWhiteSpace(existingContent))
                return "title:" + formattedLiteral;

            TopLevelTitleSpan span = FindTopLevelTitleSpan(existingContent, false);
            if (span.Found)
            {
                StringBuilder sb = new StringBuilder(existingContent.Length + formattedLiteral.Length);
                sb.Append(existingContent, 0, span.ValueStart);
                sb.Append(formattedLiteral);
                sb.Append(existingContent, span.ValueEnd, existingContent.Length - span.ValueEnd);
                return sb.ToString();
            }

            string trimmedStart = existingContent.TrimStart();
            if (trimmedStart.Length == 0)
                return "title:" + formattedLiteral;

            return "title:" + formattedLiteral + " " + trimmedStart;
        }

        private static string EscapePbtxtString(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            StringBuilder sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\x").Append(((int)c).ToString("x2"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        private static TopLevelTitleSpan FindTopLevelTitleSpan(string content, bool decodeValue)
        {
            TopLevelTitleSpan res = new TopLevelTitleSpan();
            int len = content.Length;
            int pos = 0;
            if (len > 0 && content[0] == '\uFEFF') pos = 1;

            int braceDepth = 0;
            while (pos < len)
            {
                SkipWsAndComments(content, ref pos, len);
                if (pos >= len) break;

                char c = content[pos];
                if (c == '{')
                {
                    braceDepth++;
                    pos++;
                    continue;
                }
                if (c == '}')
                {
                    if (braceDepth > 0) braceDepth--;
                    pos++;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    SkipOrDecodeQuotedLiteral(content, ref pos, len, null, null);
                    continue;
                }

                if (braceDepth == 0 && IsIdentStart(c))
                {
                    int identStart = pos;
                    while (pos < len && IsIdentPart(content[pos])) pos++;
                    string ident = content.Substring(identStart, pos - identStart);

                    if (string.Equals(ident, "title", StringComparison.Ordinal))
                    {
                        int probe = pos;
                        SkipWsAndComments(content, ref probe, len);
                        if (probe < len && content[probe] == ':')
                        {
                            probe++;
                            SkipWsAndComments(content, ref probe, len);
                            if (probe < len && (content[probe] == '"' || content[probe] == '\''))
                            {
                                int valStart = probe;
                                StringBuilder decodedSb = decodeValue ? new StringBuilder() : null;
                                List<byte> pendingBytes = decodeValue ? new List<byte>() : null;

                                while (probe < len && (content[probe] == '"' || content[probe] == '\''))
                                {
                                    SkipOrDecodeQuotedLiteral(content, ref probe, len, decodedSb, pendingBytes);
                                    int nextQuoteProbe = probe;
                                    SkipWsAndComments(content, ref nextQuoteProbe, len);
                                    if (nextQuoteProbe < len && (content[nextQuoteProbe] == '"' || content[nextQuoteProbe] == '\''))
                                    {
                                        probe = nextQuoteProbe;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }

                                if (decodeValue) FlushPendingUtf8Bytes(pendingBytes, decodedSb);

                                res.Found = true;
                                res.ValueStart = valStart;
                                res.ValueEnd = probe;
                                res.DecodedTitle = decodeValue ? decodedSb.ToString() : null;
                                return res;
                            }
                        }
                    }
                    continue;
                }

                pos++;
            }

            return res;
        }

        private static void SkipWsAndComments(string s, ref int pos, int len)
        {
            while (pos < len)
            {
                char c = s[pos];
                if (char.IsWhiteSpace(c))
                {
                    pos++;
                    continue;
                }
                if (c == '#')
                {
                    pos++;
                    while (pos < len && s[pos] != '\n' && s[pos] != '\r') pos++;
                    continue;
                }
                break;
            }
        }

        private static bool IsIdentStart(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';
        }

        private static bool IsIdentPart(char c)
        {
            return IsIdentStart(c) || (c >= '0' && c <= '9');
        }

        private static void FlushPendingUtf8Bytes(List<byte> pendingBytes, StringBuilder sb)
        {
            if (pendingBytes == null || pendingBytes.Count == 0) return;
            sb.Append(Encoding.UTF8.GetString(pendingBytes.ToArray()));
            pendingBytes.Clear();
        }

        private static void SkipOrDecodeQuotedLiteral(
            string s,
            ref int pos,
            int len,
            StringBuilder sb,
            List<byte> pendingBytes)
        {
            if (pos >= len) return;
            char quote = s[pos];
            if (quote != '"' && quote != '\'') return;
            pos++; // skip opening quote

            bool decode = sb != null;
            while (pos < len)
            {
                char c = s[pos++];
                if (c == quote)
                {
                    if (decode) FlushPendingUtf8Bytes(pendingBytes, sb);
                    return;
                }

                if (c == '\\')
                {
                    if (pos >= len) return;
                    char esc = s[pos++];

                    if (esc >= '0' && esc <= '7')
                    {
                        int octVal = esc - '0';
                        for (int k = 0; k < 2 && pos < len; k++)
                        {
                            char next = s[pos];
                            if (next < '0' || next > '7') break;
                            octVal = (octVal << 3) | (next - '0');
                            pos++;
                        }
                        if (decode) pendingBytes.Add((byte)(octVal & 0xFF));
                        continue;
                    }

                    if (esc == 'x' || esc == 'X')
                    {
                        int hexVal = 0;
                        int hexDigits = 0;
                        while (hexDigits < 2 && pos < len)
                        {
                            int d = MiniJson.HexDigitValue(s[pos]);
                            if (d < 0) break;
                            hexVal = (hexVal << 4) | d;
                            pos++;
                            hexDigits++;
                        }
                        if (decode)
                        {
                            if (hexDigits > 0)
                            {
                                pendingBytes.Add((byte)hexVal);
                            }
                            else
                            {
                                FlushPendingUtf8Bytes(pendingBytes, sb);
                                sb.Append(esc);
                            }
                        }
                        continue;
                    }

                    if (decode) FlushPendingUtf8Bytes(pendingBytes, sb);

                    switch (esc)
                    {
                        case '\\': if (decode) sb.Append('\\'); break;
                        case '"': if (decode) sb.Append('"'); break;
                        case '\'': if (decode) sb.Append('\''); break;
                        case 'n': if (decode) sb.Append('\n'); break;
                        case 'r': if (decode) sb.Append('\r'); break;
                        case 't': if (decode) sb.Append('\t'); break;
                        case 'a': if (decode) sb.Append('\a'); break;
                        case 'b': if (decode) sb.Append('\b'); break;
                        case 'f': if (decode) sb.Append('\f'); break;
                        case 'v': if (decode) sb.Append('\v'); break;
                        case '?': if (decode) sb.Append('?'); break;
                        case 'u':
                            if (pos + 4 <= len)
                            {
                                int code;
                                if (MiniJson.TryParseHex4(s, pos, out code))
                                {
                                    pos += 4;
                                    if (decode)
                                    {
                                        if (code >= 0xD800 && code <= 0xDBFF &&
                                            pos + 6 <= len && s[pos] == '\\' && s[pos + 1] == 'u')
                                        {
                                            int low;
                                            if (MiniJson.TryParseHex4(s, pos + 2, out low) &&
                                                low >= 0xDC00 && low <= 0xDFFF)
                                            {
                                                int cp = 0x10000 + ((code - 0xD800) << 10) + (low - 0xDC00);
                                                sb.Append(char.ConvertFromUtf32(cp));
                                                pos += 6;
                                                break;
                                            }
                                        }
                                        sb.Append((char)code);
                                    }
                                    break;
                                }
                            }
                            if (decode) sb.Append('u');
                            break;
                        case 'U':
                            if (pos + 8 <= len)
                            {
                                int hiWord;
                                int loWord;
                                if (MiniJson.TryParseHex4(s, pos, out hiWord) &&
                                    MiniJson.TryParseHex4(s, pos + 4, out loWord))
                                {
                                    int cp = (hiWord << 16) | loWord;
                                    pos += 8;
                                    if (decode)
                                    {
                                        if (cp >= 0 && cp <= 0x10FFFF && !(cp >= 0xD800 && cp <= 0xDFFF))
                                            sb.Append(char.ConvertFromUtf32(cp));
                                        else
                                            sb.Append((char)(cp & 0xFFFF));
                                    }
                                    break;
                                }
                            }
                            if (decode) sb.Append('U');
                            break;
                        default:
                            if (decode) sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    if (decode)
                    {
                        FlushPendingUtf8Bytes(pendingBytes, sb);
                        sb.Append(c);
                    }
                }
            }

            if (decode) FlushPendingUtf8Bytes(pendingBytes, sb);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // 3. RENAME RULES CONFIG & AUTO TITLE SYNTHESIZER
    // ══════════════════════════════════════════════════════════════════════

    public class ModelMatcherRule
    {
        public string Model;
        public List<string> Keywords;

        public ModelMatcherRule()
        {
            Model = string.Empty;
            Keywords = new List<string>();
        }
    }

    public class MultiRepoRule
    {
        public string ProjectName;
        public string DefaultModel;
        public List<string> BranchRegexes;
        public List<ModelMatcherRule> ModelMatchers;

        public MultiRepoRule()
        {
            ProjectName = string.Empty;
            DefaultModel = string.Empty;
            BranchRegexes = new List<string>();
            ModelMatchers = new List<ModelMatcherRule>();
        }
    }

    public class RenameRulesConfig
    {
        public int Version;
        public bool KeepExistingBracketedPrefix;
        public Dictionary<string, string> ProjectAliases;
        public List<MultiRepoRule> MultiRepoRules;

        public RenameRulesConfig()
        {
            Version = 1;
            KeepExistingBracketedPrefix = false;
            ProjectAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            MultiRepoRules = new List<MultiRepoRule>();
        }

        public static RenameRulesConfig CreateDefault()
        {
            string dummyErr;
            RenameRulesConfig cfg;
            if (AutoTitleSynthesizer.TryParseRulesFromJson(AutoTitleSynthesizer.GetDefaultRulesJsonText(), out cfg, out dummyErr) && cfg != null)
                return cfg;
            return new RenameRulesConfig();
        }
    }

    public class SessionRenameCandidate
    {
        public string ConversationId;
        public string ProjectName;
        public string ProjectId;
        public string FolderUri;
        public string OldTitle;
        public string OldAutoTitle;
        public string OldCustomTitle;
        public bool HasCustomTitle;
        public string FirstPromptSnippet;
        public string GitBranchHint;
        public string SuggestedPrefix;
        public string CleanedPrompt;
        public string SuggestedTitle;
        public string NewTitle;
        public bool Selected;
        public bool IsLowValue;
        public bool IsCli;
        public DateTime UpdatedTime;
        public int StepCount;
        public ConversationSummaryInfo Summary;
    }

    public class RuleSandboxItem
    {
        public string ConversationId;
        public string ProjectName;
        public string OldTitle;
        public string SuggestedPrefix;
        public string SuggestedTitle;
        public bool IsLowValue;
    }

    /// <summary>
    /// 规则驱动的会话标题标准化合成器与 Prompt 清洗引擎
    /// </summary>
    public static class AutoTitleSynthesizer
    {
        private const string RulesFileName = "rename_rules.json";
        private const int MaxStandardPrefixCloseIndex = 36;
        private const int MaxCleanedBodyChars = 34;

        private static readonly Regex AdditionalMetadataBlockRegex =
            new Regex("<ADDITIONAL_METADATA>[\\s\\S]*?</ADDITIONAL_METADATA>", RegexOptions.IgnoreCase);
        private static readonly Regex AdditionalMetadataUnclosedRegex =
            new Regex("<ADDITIONAL_METADATA>[\\s\\S]*$", RegexOptions.IgnoreCase);
        private static readonly Regex UserSettingsBlockRegex =
            new Regex("<USER_SETTINGS_CHANGE>[\\s\\S]*?</USER_SETTINGS_CHANGE>", RegexOptions.IgnoreCase);
        private static readonly Regex UserRequestTagRegex =
            new Regex("</?USER_REQUEST\\b[^>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex ConversationMentionRegex =
            new Regex("@\\[conversation:\"[^\"]*\"\\]", RegexOptions.IgnoreCase);
        private static readonly Regex GenericAtMentionRegex =
            new Regex("@\\[[^\\]]*\\]");
        private static readonly Regex SlashCommandRegex =
            new Regex("(?:^|(?<=[\\s(\\[\"'<>]))/(?:plan|grill-me|boost|dual-agent-team|turn-audit|project-handoff|teamwork-preview|run-workflow|compact)(?=$|[\\s,，。；;:：)\\]}>\"'!！?？\\u4e00-\\u9fa5])", RegexOptions.IgnoreCase);
        private static readonly Regex QuotedWinPathPrefixRegex =
            new Regex("([\"'])[A-Za-z]:(?:\\\\\\\\|\\\\|/)[^\"'\\r\\n]*(?:\\\\\\\\|\\\\|/)([^\"'\\\\/\\r\\n]*)\\1");
        private static readonly Regex UnquotedWinPathPrefixRegex =
            new Regex("[A-Za-z]:(?:\\\\\\\\|\\\\|/)[^\"'<>|\\r\\n\\s]*(?:\\\\\\\\|\\\\|/)");
        private static readonly Regex FileUriPrefixRegex =
            new Regex("file:///(?:[A-Za-z](?::|%3[Aa])/)?(?:[^\\s\"'`<>|\\r\\n]*/)?", RegexOptions.IgnoreCase);
        private static readonly Regex CodeFenceRegex =
            new Regex("```[A-Za-z0-9_+-]*");
        private static readonly Regex LinePrefixMarkdownRegex =
            new Regex("^[ \\t]{0,3}(?:#{1,6}[ \\t]+|>[ \\t]*|[-*+•●▪▫][ \\t]+|\\d+[.)、][ \\t]+)", RegexOptions.Multiline);
        private static readonly Regex CollapseWhitespaceRegex =
            new Regex("\\s+");

        private static readonly HashSet<string> GenericTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "begin", "hi", "hello", "hey", "test", "testing", "ping", "ok",
            "问题", "新建会话", "新会话", "未命名会话", "untitled", "new chat",
            "new session", "new conversation", "chat", "help", "继续", "continue",
            "check", "debug", "fix", "init", "start", "请看", "帮忙",
            "你好", "您好", "？", "?", "request for clarification", "在吗", "测试"
        };

        private static readonly HashSet<string> GenericBranchWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "当前", "主", "公版", "这个", "该", "这", "本", "那个", "哪", "哪个",
            "什么", "其他", "其它", "对应", "所在", "目标", "目前", "默认", "各自",
            "所有", "不同", "原", "新", "旧", "把", "将", "从", "对", "在", "的",
            "切换", "新建", "合并", "HEAD",
            "is", "are", "was", "were", "be", "has", "have", "had", "do", "does",
            "to", "of", "in", "on", "at", "by", "for", "with", "from", "and", "or",
            "the", "a", "an", "this", "that", "it", "not", "only", "name", "list"
        };

        private static readonly string[] GenericBranchChineseSuffixes = new string[]
        {
            "当前", "这个", "那个", "哪个", "公版", "该", "主"
        };

        public static string GetDefaultRulesJsonText()
        {
            return
                "{\r\n" +
                "  \"version\": 1,\r\n" +
                "  \"keepExistingBracketedPrefix\": false,\r\n" +
                "  \"projectAliases\": {\r\n" +
                "    \"EvoKernel\": \"Evo\",\r\n" +
                "    \"Antigravity_Companion\": \"Companion\",\r\n" +
                "    \"BMS_Protocol_Simulator\": \"BMS\",\r\n" +
                "    \"3KU_BOOT\": \"3KU_BOOT\",\r\n" +
                "    \"dldz_offgridinverter_framework2nd_boot\": \"3KU_BOOT\",\r\n" +
                "    \"INV3K_5K_VMH\": \"3KU\",\r\n" +
                "    \"InvertProject\": \"6KU\",\r\n" +
                "    \"6-10k-ups-hv-single\": \"UPS 6-10K\",\r\n" +
                "    \"JLinkScope\": \"JLinkScope\",\r\n" +
                "    \"Google\": \"Google\"\r\n" +
                "  },\r\n" +
                "  \"multiRepoRules\": [\r\n" +
                "    {\r\n" +
                "      \"projectName\": \"Inverter\",\r\n" +
                "      \"defaultModel\": \"Inverter\",\r\n" +
                "      \"branchRegexes\": [\r\n" +
                "        \"(?:^|[\\\\s，,。；;、：:])((?:Dev|Master|Customization|Release|Hotfix|Feature)_[A-Za-z0-9_\\\\u4e00-\\\\u9fa5-]+)\",\r\n" +
                "        \"(?:分支|branch)[\\\\s:：]*([A-Za-z0-9_\\\\u4e00-\\\\u9fa5-]+)\",\r\n" +
                "        \"([A-Za-z0-9_\\\\u4e00-\\\\u9fa5-]+)分支\"\r\n" +
                "      ],\r\n" +
                "      \"modelMatchers\": [\r\n" +
                "        { \"model\": \"3/6KU\", \"keywords\": [\"3/6KU\", \"3KU与5KU\", \"3KU和6KU\", \"3KU与6KU\"] },\r\n" +
                "        { \"model\": \"3KUbak\", \"keywords\": [\"3KUbak\", \"3KU备用\", \"vmh_3ku_bak\", \"3ku_bak\"] },\r\n" +
                "        { \"model\": \"UPS 1-3K\", \"keywords\": [\"UPS 1-3K\", \"UPS1-3K\", \"u.psdr.l\", \"CT1K\", \"CT3K\", \"1-3K\"] },\r\n" +
                "        { \"model\": \"UPS 6-10K\", \"keywords\": [\"UPS 6-10K\", \"UPS6-10K\", \"6-10k-ups\", \"6-10K\"] },\r\n" +
                "        { \"model\": \"6KU\", \"keywords\": [\"6KU\", \"vmhct6ku\", \"InvertProject\"] },\r\n" +
                "        { \"model\": \"3KU\", \"keywords\": [\"3KU\", \"vmh_ct3k\", \"INV3K_5K_VMH\"] }\r\n" +
                "      ]\r\n" +
                "    }\r\n" +
                "  ]\r\n" +
                "}";
        }

        public static RenameRulesConfig LoadRules(string repositoryPath, out string error)
        {
            error = null;
            string effectiveRepo = string.IsNullOrWhiteSpace(repositoryPath)
                ? CapabilityRepositoryManager.ResolveDefaultRepositoryPath()
                : repositoryPath.Trim();

            string rulesPath = Path.Combine(effectiveRepo, RulesFileName);
            if (!File.Exists(rulesPath))
            {
                return RenameRulesConfig.CreateDefault();
            }

            try
            {
                string jsonText;
                using (FileStream fs = new FileStream(rulesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                {
                    jsonText = sr.ReadToEnd();
                }

                RenameRulesConfig parsed;
                string parseErr;
                if (TryParseRulesFromJson(jsonText, out parsed, out parseErr) && parsed != null)
                {
                    return parsed;
                }

                error = "解析 rename_rules.json 失败，已回退内置默认规则: " + (parseErr ?? "未知错误");
            }
            catch (Exception ex)
            {
                error = "读取 rename_rules.json 失败，已回退内置默认规则: " + ex.Message;
            }

            return RenameRulesConfig.CreateDefault();
        }

        public static bool ValidateAndSaveRules(string repositoryPath, string jsonText, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                error = "规则 JSON 内容不能为空。";
                return false;
            }

            RenameRulesConfig parsed;
            if (!TryParseRulesFromJson(jsonText, out parsed, out error))
            {
                return false;
            }

            string effectiveRepo = string.IsNullOrWhiteSpace(repositoryPath)
                ? CapabilityRepositoryManager.ResolveDefaultRepositoryPath()
                : repositoryPath.Trim();

            try
            {
                string rulesPath = Path.Combine(effectiveRepo, RulesFileName);
                LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(rulesPath, jsonText, false, rulesPath + ".bak");
                return true;
            }
            catch (Exception ex)
            {
                error = "写入 rename_rules.json 失败: " + ex.Message;
                return false;
            }
        }

        internal static bool TryParseRulesFromJson(string jsonText, out RenameRulesConfig config, out string error)
        {
            config = null;
            error = null;

            object rootObj;
            try
            {
                rootObj = MiniJson.Parse(jsonText);
            }
            catch (Exception ex)
            {
                error = "JSON 语法错误: " + ex.Message;
                return false;
            }

            Dictionary<string, object> rootDict = rootObj as Dictionary<string, object>;
            if (rootDict == null)
            {
                error = "rename_rules.json 根节点必须是 JSON 对象。";
                return false;
            }

            RenameRulesConfig result = new RenameRulesConfig();
            result.Version = MiniJson.GetInt(rootDict, "version", 1);
            result.KeepExistingBracketedPrefix = MiniJson.GetBool(rootDict, "keepExistingBracketedPrefix", false);

            object aliasesObj;
            if (rootDict.TryGetValue("projectAliases", out aliasesObj) && aliasesObj != null)
            {
                Dictionary<string, object> aliasDict = aliasesObj as Dictionary<string, object>;
                if (aliasDict == null)
                {
                    error = "\"projectAliases\" 必须是 JSON 对象。";
                    return false;
                }
                foreach (KeyValuePair<string, object> kvp in aliasDict)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value == null) continue;
                    string valStr = Convert.ToString(kvp.Value, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(valStr))
                    {
                        result.ProjectAliases[kvp.Key.Trim()] = valStr.Trim();
                    }
                }
            }

            object multiObj;
            if (rootDict.TryGetValue("multiRepoRules", out multiObj) && multiObj != null)
            {
                List<object> multiList = multiObj as List<object>;
                if (multiList == null)
                {
                    error = "\"multiRepoRules\" 必须是 JSON 数组。";
                    return false;
                }

                for (int i = 0; i < multiList.Count; i++)
                {
                    Dictionary<string, object> ruleDict = multiList[i] as Dictionary<string, object>;
                    if (ruleDict == null)
                    {
                        error = "\"multiRepoRules[" + i + "]\" 必须是 JSON 对象。";
                        return false;
                    }

                    MultiRepoRule rule = new MultiRepoRule();
                    rule.ProjectName = (MiniJson.GetString(ruleDict, "projectName", string.Empty) ?? string.Empty).Trim();
                    rule.DefaultModel = (MiniJson.GetString(ruleDict, "defaultModel", string.Empty) ?? string.Empty).Trim();

                    if (string.IsNullOrEmpty(rule.ProjectName))
                    {
                        error = "\"multiRepoRules[" + i + "].projectName\" 不能为空。";
                        return false;
                    }

                    object regexesObj;
                    if (ruleDict.TryGetValue("branchRegexes", out regexesObj) && regexesObj != null)
                    {
                        List<object> regexList = regexesObj as List<object>;
                        if (regexList == null)
                        {
                            error = "\"multiRepoRules[" + i + "].branchRegexes\" 必须是字符串数组。";
                            return false;
                        }

                        for (int r = 0; r < regexList.Count; r++)
                        {
                            string pat = regexList[r] as string;
                            if (string.IsNullOrEmpty(pat))
                            {
                                error = "\"multiRepoRules[" + i + "].branchRegexes[" + r + "]\" 不能为空字符串。";
                                return false;
                            }
                            try
                            {
                                new Regex(pat);
                            }
                            catch (ArgumentException ex)
                            {
                                error = "分支正则表达式编译失败 (multiRepoRules[" + i + "].branchRegexes[" + r + "]): \"" + pat + "\" -> " + ex.Message;
                                return false;
                            }
                            rule.BranchRegexes.Add(pat);
                        }
                    }

                    object matchersObj;
                    if (ruleDict.TryGetValue("modelMatchers", out matchersObj) && matchersObj != null)
                    {
                        List<object> matcherList = matchersObj as List<object>;
                        if (matcherList == null)
                        {
                            error = "\"multiRepoRules[" + i + "].modelMatchers\" 必须是对象数组。";
                            return false;
                        }

                        for (int m = 0; m < matcherList.Count; m++)
                        {
                            Dictionary<string, object> mDict = matcherList[m] as Dictionary<string, object>;
                            if (mDict == null)
                            {
                                error = "\"multiRepoRules[" + i + "].modelMatchers[" + m + "]\" 必须是 JSON 对象。";
                                return false;
                            }

                            ModelMatcherRule matcher = new ModelMatcherRule();
                            matcher.Model = (MiniJson.GetString(mDict, "model", string.Empty) ?? string.Empty).Trim();
                            if (string.IsNullOrEmpty(matcher.Model))
                            {
                                error = "\"multiRepoRules[" + i + "].modelMatchers[" + m + "].model\" 不能为空。";
                                return false;
                            }

                            object kwsObj;
                            if (mDict.TryGetValue("keywords", out kwsObj) && kwsObj != null)
                            {
                                List<object> kwList = kwsObj as List<object>;
                                if (kwList == null)
                                {
                                    error = "\"multiRepoRules[" + i + "].modelMatchers[" + m + "].keywords\" 必须是字符串数组。";
                                    return false;
                                }
                                for (int k = 0; k < kwList.Count; k++)
                                {
                                    string kw = kwList[k] as string;
                                    if (!string.IsNullOrEmpty(kw))
                                    {
                                        matcher.Keywords.Add(kw);
                                    }
                                }
                            }
                            rule.ModelMatchers.Add(matcher);
                        }
                    }

                    result.MultiRepoRules.Add(rule);
                }
            }

            config = result;
            return true;
        }

        public static bool HasStandardBracketedPrefix(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return false;
            string trimmed = title.Trim();
            if (trimmed.Length < 3 || trimmed[0] != '【') return false;
            int closeIdx = trimmed.IndexOf('】');
            return closeIdx > 1 && closeIdx < MaxStandardPrefixCloseIndex;
        }

        public static string StripLeadingBracketPrefix(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;
            string cur = title.Trim();
            while (cur.Length >= 2 && cur[0] == '【')
            {
                int closeIdx = cur.IndexOf('】');
                if (closeIdx >= 1 && closeIdx < MaxStandardPrefixCloseIndex)
                {
                    cur = cur.Substring(closeIdx + 1).TrimStart();
                }
                else
                {
                    break;
                }
            }
            return cur.Trim();
        }

        public static string CleanFirstPrompt(string rawPrompt)
        {
            if (string.IsNullOrWhiteSpace(rawPrompt)) return string.Empty;

            string s = rawPrompt;
            s = AdditionalMetadataBlockRegex.Replace(s, " ");
            s = AdditionalMetadataUnclosedRegex.Replace(s, " ");
            s = UserSettingsBlockRegex.Replace(s, " ");
            s = UserRequestTagRegex.Replace(s, " ");
            s = ConversationMentionRegex.Replace(s, " ");
            s = GenericAtMentionRegex.Replace(s, " ");
            s = SlashCommandRegex.Replace(s, " ");

            s = QuotedWinPathPrefixRegex.Replace(s, "$2");
            s = UnquotedWinPathPrefixRegex.Replace(s, string.Empty);
            s = FileUriPrefixRegex.Replace(s, string.Empty);

            s = CodeFenceRegex.Replace(s, " ");
            s = s.Replace("`", string.Empty);

            // 剥离嵌套行首 Markdown 标记（如 "> - **标题**"）
            for (int pass = 0; pass < 3; pass++)
            {
                string next = LinePrefixMarkdownRegex.Replace(s, string.Empty);
                if (string.Equals(next, s, StringComparison.Ordinal)) break;
                s = next;
            }

            s = s.Replace("**", string.Empty).Replace("__", string.Empty);
            s = CollapseWhitespaceRegex.Replace(s, " ").Trim();
            s = s.TrimStart(':', '：', '-', ',', '，', ';', '；');
            return s.Trim();
        }

        public static void SynthesizeTitle(
            ConversationSummaryInfo session,
            RenameRulesConfig rules,
            out string suggestedPrefix,
            out string cleanedPrompt,
            out string suggestedTitle)
        {
            string snippet = TryReadStringMember(session, "FirstPromptSnippet");
            string branchHint = TryReadStringMember(session, "GitBranchHint");
            if (string.IsNullOrWhiteSpace(branchHint))
            {
                branchHint = ExtractGitBranchHintFromSession(session);
            }

            SynthesizeTitle(session, snippet, branchHint, rules, out suggestedPrefix, out cleanedPrompt, out suggestedTitle);
        }

        public static void SynthesizeTitle(
            ConversationSummaryInfo session,
            string firstPromptSnippet,
            string gitBranchHint,
            RenameRulesConfig rules,
            out string suggestedPrefix,
            out string cleanedPrompt,
            out string suggestedTitle)
        {
            RenameRulesConfig effectiveRules = rules ?? RenameRulesConfig.CreateDefault();
            if (session == null)
            {
                suggestedPrefix = "【通用】";
                cleanedPrompt = CleanFirstPrompt(firstPromptSnippet);
                string fallbackBody = !string.IsNullOrEmpty(cleanedPrompt)
                    ? TruncateGracefully(cleanedPrompt, MaxCleanedBodyChars)
                    : "未命名会话";
                suggestedTitle = suggestedPrefix + fallbackBody;
                return;
            }

            string rawOldTitle = ResolveRawOldTitle(session);
            string projectName = session.ProjectName != null ? session.ProjectName.Trim() : string.Empty;
            List<KeyValuePair<string, string>> repoBranchHints = ExtractGitBranchCandidatesFromSession(session);
            string effectiveBranchHint = !string.IsNullOrWhiteSpace(gitBranchHint)
                ? gitBranchHint
                : TryReadStringMember(session, "GitBranchHint");
            if (string.IsNullOrWhiteSpace(effectiveBranchHint))
            {
                effectiveBranchHint = ExtractGitBranchHintFromSession(session);
            }

            SynthesizeCore(
                projectName,
                rawOldTitle,
                firstPromptSnippet,
                effectiveBranchHint,
                repoBranchHints,
                session.FolderUri,
                effectiveRules,
                out suggestedPrefix,
                out cleanedPrompt,
                out suggestedTitle);
        }

        public static void SynthesizeCandidate(SessionRenameCandidate candidate, RenameRulesConfig rules)
        {
            if (candidate == null) return;

            string snippet = candidate.FirstPromptSnippet;
            if (string.IsNullOrWhiteSpace(snippet) && candidate.Summary != null)
                snippet = TryReadStringMember(candidate.Summary, "FirstPromptSnippet");

            string branchHint = candidate.GitBranchHint;
            List<KeyValuePair<string, string>> repoBranchHints = null;
            if (candidate.Summary != null)
            {
                repoBranchHints = ExtractGitBranchCandidatesFromSession(candidate.Summary);
                if (string.IsNullOrWhiteSpace(branchHint))
                {
                    branchHint = TryReadStringMember(candidate.Summary, "GitBranchHint");
                    if (string.IsNullOrWhiteSpace(branchHint))
                        branchHint = ExtractGitBranchHintFromSession(candidate.Summary);
                }
            }

            string rawOld = !string.IsNullOrWhiteSpace(candidate.OldTitle)
                ? candidate.OldTitle.Trim()
                : (candidate.Summary != null ? ResolveRawOldTitle(candidate.Summary) : string.Empty);

            string projName = !string.IsNullOrWhiteSpace(candidate.ProjectName)
                ? candidate.ProjectName.Trim()
                : (candidate.Summary != null && candidate.Summary.ProjectName != null ? candidate.Summary.ProjectName.Trim() : string.Empty);
            string folderUri = !string.IsNullOrWhiteSpace(candidate.FolderUri)
                ? candidate.FolderUri
                : (candidate.Summary != null ? candidate.Summary.FolderUri : null);

            string prefix;
            string cleaned;
            string title;
            SynthesizeCore(
                projName,
                rawOld,
                snippet,
                branchHint,
                repoBranchHints,
                folderUri,
                rules ?? RenameRulesConfig.CreateDefault(),
                out prefix,
                out cleaned,
                out title);

            candidate.SuggestedPrefix = prefix;
            candidate.CleanedPrompt = cleaned;
            candidate.SuggestedTitle = title;
            candidate.IsLowValue = IsLowValueSession(candidate.Summary, cleaned);
            candidate.IsCli = candidate.Summary != null && candidate.Summary.IsCliSession;
            if (string.IsNullOrWhiteSpace(candidate.NewTitle))
            {
                candidate.NewTitle = title;
            }
        }

        private static void SynthesizeCore(
            string projectName,
            string rawOldTitle,
            string firstPromptSnippet,
            string gitBranchHint,
            List<KeyValuePair<string, string>> repoBranchHints,
            string sessionFolderUri,
            RenameRulesConfig rules,
            out string suggestedPrefix,
            out string cleanedPrompt,
            out string suggestedTitle)
        {
            cleanedPrompt = CleanFirstPrompt(firstPromptSnippet);
            string safeOldTitle = rawOldTitle ?? string.Empty;

            if (rules.KeepExistingBracketedPrefix && HasStandardBracketedPrefix(safeOldTitle))
            {
                string trimmedOld = safeOldTitle.Trim();
                int closeIdx = trimmedOld.IndexOf('】');
                suggestedPrefix = trimmedOld.Substring(0, closeIdx + 1);
                string bodyKeep = DeriveSummaryBody(safeOldTitle, cleanedPrompt);
                suggestedTitle = suggestedPrefix + bodyKeep;
                return;
            }

            MultiRepoRule matchedMulti = FindMultiRepoRule(projectName, rules);
            string projectAlias = ResolveProjectAlias(projectName, rules);
            List<string> absorbedTags;
            string titleAfterFold = ExtractAndStripEmbeddedBracketTags(
                safeOldTitle,
                matchedMulti,
                projectName,
                projectAlias,
                out absorbedTags);

            suggestedPrefix = BuildSuggestedPrefix(
                projectName,
                projectAlias,
                matchedMulti,
                safeOldTitle,
                firstPromptSnippet,
                cleanedPrompt,
                gitBranchHint,
                repoBranchHints,
                sessionFolderUri,
                absorbedTags,
                rules);

            string body = DeriveSummaryBody(titleAfterFold, cleanedPrompt);
            suggestedTitle = suggestedPrefix + body;
        }

        private static MultiRepoRule FindMultiRepoRule(string projectName, RenameRulesConfig rules)
        {
            if (string.IsNullOrEmpty(projectName) || rules == null || rules.MultiRepoRules == null)
                return null;
            for (int i = 0; i < rules.MultiRepoRules.Count; i++)
            {
                MultiRepoRule r = rules.MultiRepoRules[i];
                if (r != null && string.Equals(r.ProjectName, projectName, StringComparison.OrdinalIgnoreCase))
                    return r;
            }
            return null;
        }

        private static string ResolveProjectAlias(string projectName, RenameRulesConfig rules)
        {
            string projTrim = (projectName ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(projTrim) && rules != null && rules.ProjectAliases != null)
            {
                string alias;
                if (rules.ProjectAliases.TryGetValue(projTrim, out alias) && !string.IsNullOrWhiteSpace(alias))
                    return alias.Trim();
            }
            return null;
        }

        private static string ExtractAndStripEmbeddedBracketTags(
            string rawOldTitle,
            MultiRepoRule matchedMulti,
            string projectName,
            string projectAlias,
            out List<string> absorbedTags)
        {
            absorbedTags = new List<string>();
            if (string.IsNullOrWhiteSpace(rawOldTitle)) return string.Empty;

            // 先剥离行首已有标准前缀，仅对正文内部/末尾【...】判断是否属于分支/工程/机型元标签
            string strippedLeading = StripLeadingBracketPrefix(rawOldTitle);
            if (string.IsNullOrEmpty(strippedLeading) || strippedLeading.IndexOf('【') < 0)
                return rawOldTitle;

            StringBuilder sb = new StringBuilder(strippedLeading.Length);
            int idx = 0;
            bool strippedAny = false;
            while (idx < strippedLeading.Length)
            {
                int open = strippedLeading.IndexOf('【', idx);
                if (open < 0)
                {
                    sb.Append(strippedLeading.Substring(idx));
                    break;
                }
                int close = strippedLeading.IndexOf('】', open + 1);
                if (close < 0 || close - open > MaxStandardPrefixCloseIndex)
                {
                    sb.Append(strippedLeading.Substring(idx));
                    break;
                }

                string innerRaw = strippedLeading.Substring(open + 1, close - open - 1).Trim();
                string normalizedInner = innerRaw;
                if (normalizedInner.StartsWith("[", StringComparison.Ordinal) &&
                    normalizedInner.EndsWith("]", StringComparison.Ordinal) &&
                    normalizedInner.Length > 2)
                {
                    normalizedInner = normalizedInner.Substring(1, normalizedInner.Length - 2).Trim();
                }

                if (ShouldAbsorbEmbeddedBracketTag(normalizedInner, matchedMulti, projectName, projectAlias))
                {
                    sb.Append(strippedLeading.Substring(idx, open - idx));
                    sb.Append(' ');
                    if (!string.IsNullOrEmpty(normalizedInner) &&
                        !absorbedTags.Exists(delegate(string ex) { return string.Equals(ex, normalizedInner, StringComparison.OrdinalIgnoreCase); }))
                    {
                        absorbedTags.Add(normalizedInner);
                    }
                    strippedAny = true;
                    idx = close + 1;
                }
                else
                {
                    sb.Append(strippedLeading.Substring(idx, close + 1 - idx));
                    idx = close + 1;
                }
            }

            if (!strippedAny) return rawOldTitle;
            return CollapseWhitespaceRegex.Replace(sb.ToString(), " ").Trim();
        }

        private static bool ShouldAbsorbEmbeddedBracketTag(
            string tag,
            MultiRepoRule matchedMulti,
            string projectName,
            string projectAlias)
        {
            if (string.IsNullOrWhiteSpace(tag) || tag.Length > MaxStandardPrefixCloseIndex - 2) return false;
            string t = tag.Trim();

            // 1. 匹配工程名或别名
            if (!string.IsNullOrEmpty(projectName) && string.Equals(t, projectName, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(projectAlias) && string.Equals(t, projectAlias, StringComparison.OrdinalIgnoreCase))
                return true;

            // 2. 包含 ASCII 字母/数字/下划线且非长句（典型分支/客户代号，如 Dev_TianKeNaNeng2 / Dev_LiShi / Servotech / YiNeng）
            if (ContainsAsciiOrDigit(t) && t.Length <= 32)
            {
                bool hasSpaceSentence = t.IndexOf(' ') >= 0 && t.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length >= 3;
                if (!hasSpaceSentence) return true;
            }

            // 3. 匹配多仓规则机型名或分支正则
            if (matchedMulti != null)
            {
                if (matchedMulti.ModelMatchers != null)
                {
                    for (int i = 0; i < matchedMulti.ModelMatchers.Count; i++)
                    {
                        ModelMatcherRule mm = matchedMulti.ModelMatchers[i];
                        if (mm != null && string.Equals(t, mm.Model, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
                string br = ExtractBranchName(null, matchedMulti.BranchRegexes, t);
                if (!string.IsNullOrEmpty(br)) return true;
            }

            return false;
        }

        private static string BuildSuggestedPrefix(
            string projectName,
            string projectAlias,
            MultiRepoRule matchedMulti,
            string rawOldTitle,
            string rawPromptSnippet,
            string cleanedPrompt,
            string gitBranchHint,
            List<KeyValuePair<string, string>> repoBranchHints,
            string sessionFolderUri,
            List<string> absorbedTags,
            RenameRulesConfig rules)
        {
            if (matchedMulti != null)
            {
                string modelSearchText = ((rawPromptSnippet ?? string.Empty) + " " + (rawOldTitle ?? string.Empty)).Trim();
                ModelMatcherRule matchedRule = null;
                string matchedModel = null;

                if (matchedMulti.ModelMatchers != null)
                {
                    for (int i = 0; i < matchedMulti.ModelMatchers.Count; i++)
                    {
                        ModelMatcherRule matcher = matchedMulti.ModelMatchers[i];
                        if (matcher == null || string.IsNullOrEmpty(matcher.Model) || matcher.Keywords == null) continue;

                        for (int k = 0; k < matcher.Keywords.Count; k++)
                        {
                            string kw = matcher.Keywords[k];
                            if (!string.IsNullOrEmpty(kw) &&
                                modelSearchText.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                matchedRule = matcher;
                                matchedModel = matcher.Model.Trim();
                                break;
                            }
                        }
                        if (matchedModel != null) break;
                    }
                }

                string preferredHint = SelectBestBranchCandidate(repoBranchHints, matchedRule, sessionFolderUri);
                if (string.IsNullOrWhiteSpace(preferredHint))
                    preferredHint = gitBranchHint;

                string branchSearchText = ((cleanedPrompt ?? string.Empty) + " " + (rawOldTitle ?? string.Empty)).Trim();
                string branch = ExtractBranchName(preferredHint, matchedMulti.BranchRegexes, branchSearchText);

                string effectiveModel = !string.IsNullOrEmpty(matchedModel)
                    ? matchedModel
                    : (!string.IsNullOrEmpty(matchedMulti.DefaultModel) ? matchedMulti.DefaultModel.Trim() : projectName);

                if (string.IsNullOrEmpty(effectiveModel))
                    effectiveModel = "通用";

                if (string.IsNullOrEmpty(branch) && absorbedTags != null)
                {
                    for (int t = 0; t < absorbedTags.Count; t++)
                    {
                        string candTag = absorbedTags[t];
                        if (!string.IsNullOrWhiteSpace(candTag) &&
                            !string.Equals(candTag, effectiveModel, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(candTag, projectName, StringComparison.OrdinalIgnoreCase) &&
                            !IsGenericBranchWord(candTag))
                        {
                            branch = candTag;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(branch))
                {
                    return FormatModelBranchPrefix(effectiveModel, branch);
                }

                return "【" + effectiveModel + "】";
            }

            string projTrim = (projectName ?? string.Empty).Trim();
            string baseLabel;
            if (!string.IsNullOrEmpty(projectAlias))
            {
                baseLabel = projectAlias;
            }
            else if (string.IsNullOrEmpty(projTrim) ||
                     string.Equals(projTrim, "Outside of Project", StringComparison.OrdinalIgnoreCase))
            {
                baseLabel = "通用";
            }
            else
            {
                baseLabel = projTrim;
            }

            if (absorbedTags != null)
            {
                for (int t = 0; t < absorbedTags.Count; t++)
                {
                    string candTag = absorbedTags[t];
                    if (!string.IsNullOrWhiteSpace(candTag) &&
                        !string.Equals(candTag, baseLabel, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(candTag, projTrim, StringComparison.OrdinalIgnoreCase) &&
                        !IsGenericBranchWord(candTag))
                    {
                        return FormatModelBranchPrefix(baseLabel, candTag);
                    }
                }
            }

            return "【" + baseLabel + "】";
        }

        private static string FormatModelBranchPrefix(string model, string branch)
        {
            int maxInterior = MaxStandardPrefixCloseIndex - 2; // 34 chars inside 【...】
            int maxBranchLen = maxInterior - model.Length - 1;
            string b = branch;
            if (maxBranchLen >= 4 && b.Length > maxBranchLen)
            {
                b = b.Substring(0, maxBranchLen);
            }
            if (model.Length + 1 + b.Length <= maxInterior)
            {
                return "【" + model + "·" + b + "】";
            }
            return "【" + model + "】";
        }

        private static string SelectBestBranchCandidate(
            List<KeyValuePair<string, string>> candidates,
            ModelMatcherRule matchedRule,
            string sessionFolderUri)
        {
            if (candidates == null || candidates.Count == 0) return null;

            // 1. 优先匹配命中当前机型关键词的仓库路径
            if (matchedRule != null && matchedRule.Keywords != null && matchedRule.Keywords.Count > 0)
            {
                string fallbackMatchedRepoBranch = null;
                for (int i = 0; i < candidates.Count; i++)
                {
                    string rp = candidates[i].Key ?? string.Empty;
                    string br = candidates[i].Value ?? string.Empty;
                    if (string.IsNullOrEmpty(rp) || string.IsNullOrEmpty(br)) continue;
                    bool hitRepo = false;
                    for (int k = 0; k < matchedRule.Keywords.Count; k++)
                    {
                        string kw = matchedRule.Keywords[k];
                        if (!string.IsNullOrEmpty(kw) && rp.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            hitRepo = true;
                            break;
                        }
                    }
                    if (!hitRepo) continue;
                    if (!string.Equals(br, "main", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(br, "master", StringComparison.OrdinalIgnoreCase))
                        return br;
                    if (fallbackMatchedRepoBranch == null) fallbackMatchedRepoBranch = br;
                }
                if (!string.IsNullOrEmpty(fallbackMatchedRepoBranch))
                    return fallbackMatchedRepoBranch;
            }

            // 2. 匹配 session.FolderUri
            if (!string.IsNullOrEmpty(sessionFolderUri))
            {
                string normSess = DirectStreamArchiver.NormalizeUri(sessionFolderUri);
                for (int i = 0; i < candidates.Count; i++)
                {
                    string rp = candidates[i].Key ?? string.Empty;
                    string br = candidates[i].Value ?? string.Empty;
                    if (string.IsNullOrEmpty(rp) || string.IsNullOrEmpty(br)) continue;
                    if (string.Equals(DirectStreamArchiver.NormalizeUri(rp), normSess, StringComparison.OrdinalIgnoreCase))
                        return br;
                }
            }

            // 3. 若多仓中有非 main/master 唯一/首条分支
            for (int i = 0; i < candidates.Count; i++)
            {
                string br = candidates[i].Value;
                if (!string.IsNullOrEmpty(br) &&
                    !string.Equals(br, "main", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(br, "master", StringComparison.OrdinalIgnoreCase))
                {
                    return br;
                }
            }

            return candidates[0].Value;
        }

        private static string ExtractBranchName(string gitBranchHint, List<string> branchRegexes, string searchText)
        {
            if (!string.IsNullOrWhiteSpace(gitBranchHint))
            {
                string trimmedHint = gitBranchHint.Trim();
                if (trimmedHint.EndsWith("分支", StringComparison.Ordinal) && trimmedHint.Length > 2)
                    trimmedHint = trimmedHint.Substring(0, trimmedHint.Length - 2).Trim();
                if (!IsGenericBranchWord(trimmedHint))
                    return trimmedHint;
            }

            if (branchRegexes == null || branchRegexes.Count == 0 || string.IsNullOrWhiteSpace(searchText))
                return null;

            for (int i = 0; i < branchRegexes.Count; i++)
            {
                string pat = branchRegexes[i];
                if (string.IsNullOrEmpty(pat)) continue;

                try
                {
                    MatchCollection matches = Regex.Matches(searchText, pat, RegexOptions.IgnoreCase);
                    for (int m = 0; m < matches.Count; m++)
                    {
                        Match match = matches[m];
                        if (!match.Success) continue;
                        string rawCand = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                        if (string.IsNullOrWhiteSpace(rawCand)) continue;
                        string cand = rawCand.Trim();
                        if (IsGluedChineseBranchKeyword(match, cand)) continue;
                        if (cand.EndsWith("分支", StringComparison.Ordinal) && cand.Length > 2)
                            cand = cand.Substring(0, cand.Length - 2).Trim();
                        if (!IsGenericBranchWord(cand))
                            return cand;
                    }
                }
                catch (ArgumentException)
                {
                    // 单条非法正则不阻断后续规则
                }
            }

            return null;
        }

        private static bool IsGluedChineseBranchKeyword(Match match, string cand)
        {
            if (match == null || match.Groups.Count <= 1 || string.IsNullOrEmpty(cand)) return false;
            string full = match.Value;
            int prefixLen = 0;
            if (full.StartsWith("分支", StringComparison.Ordinal)) prefixLen = 2;
            else if (full.StartsWith("branch", StringComparison.OrdinalIgnoreCase)) prefixLen = 6;
            if (prefixLen == 0) return false;

            int gap = match.Groups[1].Index - (match.Index + prefixLen);
            if (gap > 0) return false;

            return !ContainsAsciiOrDigit(cand);
        }

        private static bool ContainsAsciiOrDigit(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')
                    return true;
            }
            return false;
        }

        private static bool IsGenericBranchWord(string cand)
        {
            if (string.IsNullOrWhiteSpace(cand)) return true;
            string t = cand.Trim();
            if (GenericBranchWords.Contains(t)) return true;

            if (!ContainsAsciiOrDigit(t))
            {
                if (t.Length <= 1) return true;
                for (int i = 0; i < GenericBranchChineseSuffixes.Length; i++)
                {
                    if (t.EndsWith(GenericBranchChineseSuffixes[i], StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private static string DeriveSummaryBody(string rawOldTitle, string cleanedPrompt)
        {
            string strippedOld = StripLeadingBracketPrefix(rawOldTitle);
            if (IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(rawOldTitle) ||
                strippedOld.StartsWith("未命名会话 (", StringComparison.Ordinal))
            {
                strippedOld = string.Empty;
            }

            bool isOldDescriptive = strippedOld.Length >= 5 && !IsGenericTitle(strippedOld);
            if (isOldDescriptive)
            {
                return strippedOld;
            }

            if (!string.IsNullOrEmpty(cleanedPrompt))
            {
                return TruncateGracefully(cleanedPrompt, MaxCleanedBodyChars);
            }

            if (!string.IsNullOrEmpty(strippedOld) && !IsGenericTitle(strippedOld))
            {
                return strippedOld;
            }

            if (!string.IsNullOrEmpty(strippedOld))
            {
                return strippedOld;
            }

            return "未命名会话";
        }

        private static bool IsGenericTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return true;
            string normalized = title.Trim().TrimEnd('.', '。', '!', '！', '?', '？', '…', ' ');
            if (string.IsNullOrEmpty(normalized)) return true;
            return GenericTitles.Contains(normalized);
        }

        private static string TruncateGracefully(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string s = text.Trim();
            if (s.Length <= maxChars) return s;

            int cutLimit = Math.Max(1, maxChars - 1); // reserve 1 char for '…'
            int bestBreak = -1;
            int minBreak = Math.Max(4, cutLimit - 10);

            for (int i = cutLimit; i >= minBreak; i--)
            {
                char ch = s[i];
                if (ch == ' ' || ch == ',' || ch == '，' || ch == '.' || ch == '。' ||
                    ch == ';' || ch == '；' || ch == '、' || ch == ':' || ch == '：')
                {
                    bestBreak = i;
                    break;
                }
            }

            int cutLen = bestBreak >= minBreak ? bestBreak : cutLimit;
            if (cutLen > 0 && cutLen < s.Length && char.IsHighSurrogate(s[cutLen - 1]) && char.IsLowSurrogate(s[cutLen]))
            {
                cutLen--;
            }

            string head = s.Substring(0, cutLen).TrimEnd(' ', ',', '，', '.', '。', ';', '；', '、', ':', '：');
            if (string.IsNullOrEmpty(head))
                head = s.Substring(0, cutLimit);

            return head + "…";
        }

        private static string ResolveRawOldTitle(ConversationSummaryInfo session)
        {
            if (session == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(session.DisplayTitle) &&
                !IncrementalProtoShardMerger.IsSyntheticPlaceholderTitle(session.DisplayTitle))
            {
                return session.DisplayTitle.Trim();
            }
            if (!string.IsNullOrWhiteSpace(session.CustomTitle))
                return session.CustomTitle.Trim();
            if (!string.IsNullOrWhiteSpace(session.AutoSummaryTitle))
                return session.AutoSummaryTitle.Trim();
            return session.DisplayTitle != null ? session.DisplayTitle.Trim() : string.Empty;
        }

        private static string TryReadStringMember(object obj, string memberName)
        {
            if (obj == null || string.IsNullOrEmpty(memberName)) return null;
            try
            {
                Type t = obj.GetType();
                FieldInfo fi = t.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
                if (fi != null && fi.FieldType == typeof(string))
                {
                    return fi.GetValue(obj) as string;
                }
                PropertyInfo pi = t.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
                if (pi != null && pi.PropertyType == typeof(string) && pi.CanRead)
                {
                    return pi.GetValue(obj, null) as string;
                }
            }
            catch
            {
            }
            return null;
        }

        public static bool IsLowValueSession(ConversationSummaryInfo session, string cleanedPrompt)
        {
            if (session == null) return true;
            if (session.StepCount <= 0) return true;

            string rawOld = ResolveRawOldTitle(session);
            string strippedOld = StripLeadingBracketPrefix(rawOld);
            bool oldLow = string.IsNullOrWhiteSpace(strippedOld) ||
                          strippedOld.StartsWith("未命名会话 (", StringComparison.Ordinal) ||
                          IsGenericTitle(strippedOld);
            if (!oldLow) return false;

            string p = cleanedPrompt != null ? cleanedPrompt.Trim() : string.Empty;
            if (string.IsNullOrEmpty(p) || p.Length < 4) return true;
            return IsGenericTitle(p);
        }

        public static string SerializeRulesToJson(RenameRulesConfig config)
        {
            RenameRulesConfig cfg = config ?? new RenameRulesConfig();
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["version"] = cfg.Version > 0 ? cfg.Version : 1;
            root["keepExistingBracketedPrefix"] = cfg.KeepExistingBracketedPrefix;

            Dictionary<string, object> aliases = new Dictionary<string, object>();
            if (cfg.ProjectAliases != null)
            {
                List<string> keys = new List<string>(cfg.ProjectAliases.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < keys.Count; i++)
                {
                    aliases[keys[i]] = cfg.ProjectAliases[keys[i]] ?? string.Empty;
                }
            }
            root["projectAliases"] = aliases;

            List<object> multiList = new List<object>();
            if (cfg.MultiRepoRules != null)
            {
                for (int i = 0; i < cfg.MultiRepoRules.Count; i++)
                {
                    MultiRepoRule mr = cfg.MultiRepoRules[i];
                    if (mr == null) continue;
                    Dictionary<string, object> mDict = new Dictionary<string, object>();
                    mDict["projectName"] = mr.ProjectName ?? string.Empty;
                    mDict["defaultModel"] = mr.DefaultModel ?? string.Empty;

                    List<object> regList = new List<object>();
                    if (mr.BranchRegexes != null)
                    {
                        for (int r = 0; r < mr.BranchRegexes.Count; r++)
                        {
                            if (!string.IsNullOrEmpty(mr.BranchRegexes[r]))
                                regList.Add(mr.BranchRegexes[r]);
                        }
                    }
                    mDict["branchRegexes"] = regList;

                    List<object> mmList = new List<object>();
                    if (mr.ModelMatchers != null)
                    {
                        for (int m = 0; m < mr.ModelMatchers.Count; m++)
                        {
                            ModelMatcherRule mm = mr.ModelMatchers[m];
                            if (mm == null) continue;
                            Dictionary<string, object> mmDict = new Dictionary<string, object>();
                            mmDict["model"] = mm.Model ?? string.Empty;
                            List<object> kws = new List<object>();
                            if (mm.Keywords != null)
                            {
                                for (int k = 0; k < mm.Keywords.Count; k++)
                                {
                                    if (!string.IsNullOrEmpty(mm.Keywords[k]))
                                        kws.Add(mm.Keywords[k]);
                                }
                            }
                            mmDict["keywords"] = kws;
                            mmList.Add(mmDict);
                        }
                    }
                    mDict["modelMatchers"] = mmList;
                    multiList.Add(mDict);
                }
            }
            root["multiRepoRules"] = multiList;

            return MiniJson.Serialize(root, true);
        }

        public static List<RuleSandboxItem> PreviewRulesOnSessions(
            RenameRulesConfig config,
            List<ConversationSummaryInfo> sessions,
            int maxSamples)
        {
            List<RuleSandboxItem> list = new List<RuleSandboxItem>();
            if (sessions == null || sessions.Count == 0) return list;

            RenameRulesConfig effective = config ?? RenameRulesConfig.CreateDefault();
            int cap = maxSamples > 0 ? maxSamples : 30;
            for (int i = 0; i < sessions.Count && list.Count < cap; i++)
            {
                ConversationSummaryInfo s = sessions[i];
                if (s == null || s.IsSubagent || SmartProjectFilter.IsJunkOrEphemeral(s.ProjectName)) continue;

                string prefix, cleaned, title;
                SynthesizeTitle(s, effective, out prefix, out cleaned, out title);
                list.Add(new RuleSandboxItem
                {
                    ConversationId = s.ConversationId ?? string.Empty,
                    ProjectName = s.ProjectName ?? string.Empty,
                    OldTitle = ResolveRawOldTitle(s),
                    SuggestedPrefix = prefix ?? string.Empty,
                    SuggestedTitle = title ?? string.Empty,
                    IsLowValue = IsLowValueSession(s, cleaned)
                });
            }
            return list;
        }

        public static List<KeyValuePair<string, string>> ExtractGitBranchCandidatesFromSession(ConversationSummaryInfo session)
        {
            List<KeyValuePair<string, string>> results = new List<KeyValuePair<string, string>>();
            if (session == null) return results;

            if (session.RepoBranchHints != null && session.RepoBranchHints.Count > 0)
            {
                for (int i = 0; i < session.RepoBranchHints.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(session.RepoBranchHints[i].Value))
                        results.Add(session.RepoBranchHints[i]);
                }
                if (results.Count > 0) return results;
            }

            try
            {
                ProtoNode entry = session.RawProtoEntry;
                if (entry == null && session.RawBytes != null && session.RawBytes.Length > 0)
                {
                    List<ProtoNode> decoded = ProtoCodec.Decode(session.RawBytes);
                    if (decoded != null && decoded.Count > 0) entry = decoded[0];
                }
                if (entry == null) return results;

                ProtoCodec.DecodeSummaryEntryTree(entry);
                ProtoNode meta = entry.GetChild(2);
                if (meta == null || !meta.HasChildren) return results;

                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < meta.Children.Count; i++)
                {
                    ProtoNode f9 = meta.Children[i];
                    if (f9.FieldNumber != 9 || f9.WireType != 2) continue;
                    if (!f9.HasChildren && f9.RawBytes != null && f9.RawBytes.Length > 0)
                    {
                        List<ProtoNode> sub9;
                        if (ProtoCodec.TryDecodeStrict(f9.RawBytes, out sub9)) f9.Children = sub9;
                    }
                    if (!f9.HasChildren) continue;

                    string repoPath = string.Empty;
                    string directBranch = null;
                    string legacyNestedBranch = null;

                    for (int j = 0; j < f9.Children.Count; j++)
                    {
                        ProtoNode ch = f9.Children[j];
                        if (ch.FieldNumber == 4 && ch.WireType == 2 && directBranch == null)
                        {
                            string b4;
                            if (ch.TryGetStrictUtf8String(out b4) && !string.IsNullOrWhiteSpace(b4))
                                directBranch = b4.Trim();
                        }
                        else if (ch.FieldNumber == 3 && ch.WireType == 2)
                        {
                            List<ProtoNode> sub3 = ch.HasChildren ? ch.Children : null;
                            if (sub3 == null && ch.RawBytes != null && ch.RawBytes.Length > 0)
                            {
                                ProtoCodec.TryDecodeStrict(ch.RawBytes, out sub3);
                            }
                            if (sub3 != null)
                            {
                                for (int k = 0; k < sub3.Count; k++)
                                {
                                    ProtoNode inner = sub3[k];
                                    if (inner.FieldNumber == 1 && inner.WireType == 2 && string.IsNullOrEmpty(repoPath))
                                    {
                                        string rp;
                                        if (inner.TryGetStrictUtf8String(out rp) && !string.IsNullOrWhiteSpace(rp))
                                            repoPath = rp.Trim();
                                    }
                                    else if (inner.FieldNumber == 4 && inner.WireType == 2 && legacyNestedBranch == null)
                                    {
                                        string b34;
                                        if (inner.TryGetStrictUtf8String(out b34) && !string.IsNullOrWhiteSpace(b34))
                                            legacyNestedBranch = b34.Trim();
                                    }
                                }
                            }
                        }
                    }

                    string eff = !string.IsNullOrEmpty(directBranch) ? directBranch : legacyNestedBranch;
                    if (!string.IsNullOrEmpty(eff))
                    {
                        string key = (repoPath ?? string.Empty) + "|" + eff;
                        if (seen.Add(key))
                            results.Add(new KeyValuePair<string, string>(repoPath ?? string.Empty, eff));
                    }
                }
            }
            catch
            {
            }
            return results;
        }

        private static string ExtractGitBranchHintFromSession(ConversationSummaryInfo session)
        {
            List<KeyValuePair<string, string>> candidates = ExtractGitBranchCandidatesFromSession(session);
            if (candidates.Count == 0) return null;

            for (int i = 0; i < candidates.Count; i++)
            {
                string b = candidates[i].Value;
                if (!string.IsNullOrEmpty(b) &&
                    !string.Equals(b, "main", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(b, "master", StringComparison.OrdinalIgnoreCase))
                {
                    return b;
                }
            }
            return candidates[0].Value;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // 4. THREE-LAYER RENAME SNAPSHOT & TRANSACTIONAL ROLLBACK MANAGER
    // ══════════════════════════════════════════════════════════════════════

    public class RenameSnapshotItem
    {
        public string ConversationId;
        public string ProjectName;
        public string OldTitle;
        public string OldPreview;
        public string NewTitle;
        public bool HadPbtxt;
        public string OldPbtxtRaw;
        public string OldRawSummaryBase64;
        public bool HadPbF15;
        public string OldPbAutoTitle;
        public string OldPbCustomTitle;

        public byte[] GetOldRawSummaryBytes()
        {
            if (OldRawSummaryBase64 == null) return null;
            if (OldRawSummaryBase64.Length == 0) return new byte[0];
            try
            {
                return Convert.FromBase64String(OldRawSummaryBase64);
            }
            catch
            {
                return null;
            }
        }

        public void SetOldRawSummaryBytes(byte[] raw)
        {
            if (raw == null)
                OldRawSummaryBase64 = null;
            else if (raw.Length == 0)
                OldRawSummaryBase64 = string.Empty;
            else
                OldRawSummaryBase64 = Convert.ToBase64String(raw);
        }
    }

    public class RenameSnapshotInfo
    {
        public string SnapshotDir;
        public string SnapshotName;
        public string CreatedAt;
        public DateTime CreatedTime;
        public int ItemCount;
        public bool HasPbBackup;
        public List<RenameSnapshotItem> Items;

        public RenameSnapshotInfo()
        {
            Items = new List<RenameSnapshotItem>();
        }
    }

    public class RenameRollbackResult
    {
        public bool Success;
        public int RestoredPbtxtCount;
        public int DeletedPbtxtCount;
        public int SkippedOrphanPbtxtCount;
        public int RestoredDbCount;
        public int RestoredPbCount;
        public bool PbSkippedDueToLock;
        public List<string> Warnings;
        public string ErrorMessage;

        public RenameRollbackResult()
        {
            Warnings = new List<string>();
        }
    }

    /// <summary>
    /// 批量重命名三层快照备份与精准按 CID 回滚管理器 (Layer 1 .pbtxt / Layer 2 SQLite / Layer 3 .pb)
    /// </summary>
    public static class RenameBackupManager
    {
        private const string BackupRootFolderName = "companion_rename_backups";
        private const string ManifestFileName = "rename_manifest.json";
        private const string PbFileName = "agyhub_summaries_proto.pb";
        private const string PbBackupFileName = "agyhub_summaries_proto.pb.bak";
        private const string SummaryDbFileName = "conversation_summaries.db";
        private const int MaxSnapshotsToKeep = 15;

        public static string GetBackupRoot(string geminiPath)
        {
            if (string.IsNullOrEmpty(geminiPath)) return string.Empty;
            return Path.Combine(geminiPath, "antigravity", BackupRootFolderName);
        }

        public static string CreateSnapshot(string geminiPath, List<RenameSnapshotItem> items)
        {
            if (string.IsNullOrEmpty(geminiPath))
                throw new ArgumentException("geminiPath 不能为空。", "geminiPath");

            List<RenameSnapshotItem> itemList = items ?? new List<RenameSnapshotItem>();
            string backupRoot = GetBackupRoot(geminiPath);
            if (!Directory.Exists(backupRoot))
            {
                Directory.CreateDirectory(backupRoot);
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            string snapDir = null;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                string rand4 = Guid.NewGuid().ToString("N").Substring(0, 4);
                string dirName = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2}items", stamp, rand4, itemList.Count);
                string candidate = Path.Combine(backupRoot, dirName);
                if (!Directory.Exists(candidate))
                {
                    snapDir = candidate;
                    break;
                }
            }
            if (snapDir == null)
            {
                snapDir = Path.Combine(backupRoot, stamp + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + "_" + itemList.Count + "items");
            }

            Directory.CreateDirectory(snapDir);

            Dictionary<string, object> manifest = new Dictionary<string, object>(StringComparer.Ordinal);
            manifest["created_at"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);

            List<object> itemObjs = new List<object>(itemList.Count);
            for (int i = 0; i < itemList.Count; i++)
            {
                RenameSnapshotItem item = itemList[i];
                if (item == null) continue;

                Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.Ordinal);
                d["conversation_id"] = item.ConversationId ?? string.Empty;
                d["project_name"] = item.ProjectName ?? string.Empty;
                d["old_title"] = item.OldTitle ?? string.Empty;
                d["old_preview"] = item.OldPreview ?? string.Empty;
                d["new_title"] = item.NewTitle ?? string.Empty;
                d["had_pbtxt"] = item.HadPbtxt;
                d["old_pbtxt_raw"] = item.OldPbtxtRaw;
                d["old_raw_summary_base64"] = item.OldRawSummaryBase64;
                d["had_pb_f15"] = item.HadPbF15;
                d["old_pb_auto_title"] = item.OldPbAutoTitle ?? string.Empty;
                d["old_pb_custom_title"] = item.OldPbCustomTitle ?? string.Empty;
                itemObjs.Add(d);
            }
            manifest["items"] = itemObjs;

            string manifestJson = MiniJson.Serialize(manifest, true);
            string manifestPath = Path.Combine(snapDir, ManifestFileName);
            LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(manifestPath, manifestJson, false, null);

            string livePbPath = Path.Combine(geminiPath, "antigravity", PbFileName);
            if (File.Exists(livePbPath))
            {
                try
                {
                    byte[] pbBytes = ReadAllBytesShared(livePbPath);
                    string pbBakPath = Path.Combine(snapDir, PbBackupFileName);
                    LocalWorkspaceManager.WriteBytesAtomicWithBackup(pbBakPath, pbBytes, null);
                }
                catch
                {
                    // 热占用情况下不阻断 manifest 落盘
                }
            }

            PruneOldSnapshots(backupRoot, MaxSnapshotsToKeep);
            return snapDir;
        }

        public static List<RenameSnapshotInfo> ListSnapshots(string geminiPath)
        {
            List<RenameSnapshotInfo> list = new List<RenameSnapshotInfo>();
            string backupRoot = GetBackupRoot(geminiPath);
            if (string.IsNullOrEmpty(backupRoot) || !Directory.Exists(backupRoot))
                return list;

            string[] dirs = Directory.GetDirectories(backupRoot);
            for (int i = 0; i < dirs.Length; i++)
            {
                string dir = dirs[i];
                string manifestPath = Path.Combine(dir, ManifestFileName);
                if (!File.Exists(manifestPath)) continue;

                try
                {
                    RenameSnapshotInfo info = ParseSnapshotDirectory(dir, manifestPath);
                    if (info != null) list.Add(info);
                }
                catch
                {
                    // 忽略损坏的单个快照目录
                }
            }

            list.Sort(delegate(RenameSnapshotInfo a, RenameSnapshotInfo b)
            {
                int cmp = DateTime.Compare(b.CreatedTime, a.CreatedTime);
                if (cmp != 0) return cmp;
                return string.Compare(b.SnapshotName, a.SnapshotName, StringComparison.OrdinalIgnoreCase);
            });

            return list;
        }

        public static bool RollbackSnapshot(
            string geminiPath,
            string snapshotDir,
            bool allowHotWrite,
            out RenameRollbackResult result)
        {
            result = new RenameRollbackResult();
            if (string.IsNullOrEmpty(geminiPath) || string.IsNullOrEmpty(snapshotDir))
            {
                result.ErrorMessage = "geminiPath 或 snapshotDir 不能为空。";
                return false;
            }

            string manifestPath = Path.Combine(snapshotDir, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                result.ErrorMessage = "快照清单文件不存在: " + manifestPath;
                return false;
            }

            RenameSnapshotInfo snap;
            try
            {
                snap = ParseSnapshotDirectory(snapshotDir, manifestPath);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "解析快照清单失败: " + ex.Message;
                return false;
            }

            if (snap == null || snap.Items == null || snap.Items.Count == 0)
            {
                result.Success = true;
                return true;
            }

            string antigravityDir = Path.Combine(geminiPath, "antigravity");
            string annotationsDir = Path.Combine(antigravityDir, "annotations");
            string pbPath = Path.Combine(antigravityDir, PbFileName);
            string dbPath = Path.Combine(antigravityDir, SummaryDbFileName);

            // 收集实时 .pb 与 conversation_summaries.db 中的活跃会话 ID，避免为已删除会话重建孤儿 .pbtxt
            bool pbExists = File.Exists(pbPath);
            bool pbReadable = false;
            HashSet<string> livePbCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (pbExists)
            {
                try
                {
                    byte[] pbBytes = ReadAllBytesShared(pbPath);
                    if (pbBytes != null && pbBytes.Length > 0)
                    {
                        List<ProtoNode> rootNodes = ProtoCodec.Decode(pbBytes);
                        for (int i = 0; i < rootNodes.Count; i++)
                        {
                            ProtoNode entry = rootNodes[i];
                            if (entry.FieldNumber == 1 && entry.WireType == 2)
                            {
                                string cid = ExtractEntryCidWithoutMutating(entry);
                                if (SessionMetadataExtractor.IsLikelyConversationId(cid))
                                {
                                    livePbCids.Add(cid.Trim());
                                }
                            }
                        }
                    }
                    pbReadable = true;
                }
                catch (Exception ex)
                {
                    result.Warnings.Add("读取实时 .pb 索引检查活跃会话失败: " + ex.Message);
                }
            }

            bool dbExists = File.Exists(dbPath);
            bool dbReadable = false;
            HashSet<string> liveDbCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (dbExists)
            {
                try
                {
                    liveDbCids = SummarySqliteBridge.ReadAllConversationIds(dbPath);
                    dbReadable = true;
                }
                catch (Exception ex)
                {
                    result.Warnings.Add("读取 conversation_summaries.db 活跃会话失败: " + ex.Message);
                }
            }

            // 去重保留快照中的目标条目
            Dictionary<string, RenameSnapshotItem> targetByCid = new Dictionary<string, RenameSnapshotItem>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < snap.Items.Count; i++)
            {
                RenameSnapshotItem item = snap.Items[i];
                if (item == null || !SessionMetadataExtractor.IsLikelyConversationId(item.ConversationId)) continue;
                targetByCid[item.ConversationId.Trim()] = item;
            }

            // Layer 1: 还原或删除 annotations/<cid>.pbtxt（跳过在 .pb 与 .db 均已消失的会话）
            foreach (KeyValuePair<string, RenameSnapshotItem> kvp in targetByCid)
            {
                string cid = kvp.Key;
                RenameSnapshotItem item = kvp.Value;

                bool existsInLivePb = livePbCids.Contains(cid);
                bool existsInLiveDb = liveDbCids.Contains(cid);
                bool isLiveSession = existsInLivePb || existsInLiveDb;

                // 若两个存储均不可读但磁盘上有 .pb/.db 文件存在且报错，保守仅对已存在文件执行不新建孤儿
                if (!isLiveSession && (pbReadable || !pbExists) && (dbReadable || !dbExists))
                {
                    result.SkippedOrphanPbtxtCount++;
                    continue;
                }
                if (!isLiveSession)
                {
                    result.SkippedOrphanPbtxtCount++;
                    continue;
                }

                string pbtxtPath = Path.Combine(annotationsDir, cid + ".pbtxt");
                if (!IsPathContainedUnder(annotationsDir, pbtxtPath))
                {
                    result.Warnings.Add("跳过越界 .pbtxt 路径: " + cid);
                    continue;
                }

                try
                {
                    if (item.HadPbtxt)
                    {
                        LocalWorkspaceManager.WriteUtf8TextAtomicWithBackup(pbtxtPath, item.OldPbtxtRaw ?? string.Empty, false, null);
                        result.RestoredPbtxtCount++;
                    }
                    else
                    {
                        if (File.Exists(pbtxtPath))
                        {
                            try { File.SetAttributes(pbtxtPath, FileAttributes.Normal); } catch { }
                            File.Delete(pbtxtPath);
                            result.DeletedPbtxtCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = "还原 .pbtxt 失败 (" + cid + "): " + ex.Message;
                    return false;
                }
            }

            // Layer 2: 通过 SummarySqliteBridge 事务级还原 conversation_summaries 与 conversations_fts
            if (dbExists && new FileInfo(dbPath).Length > 0)
            {
                Dictionary<string, SummaryDbRollbackEntry> restoreMap = new Dictionary<string, SummaryDbRollbackEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, RenameSnapshotItem> kvp in targetByCid)
                {
                    string cid = kvp.Key;
                    if (dbReadable && !liveDbCids.Contains(cid))
                        continue;

                    RenameSnapshotItem item = kvp.Value;
                    SummaryDbRollbackEntry rb = new SummaryDbRollbackEntry();
                    rb.ConversationId = cid;
                    rb.OldTitle = item.OldTitle ?? string.Empty;
                    rb.OldPreview = item.OldPreview ?? string.Empty;
                    rb.OldRawSummary = item.GetOldRawSummaryBytes();
                    restoreMap[cid] = rb;
                }

                if (restoreMap.Count > 0)
                {
                    List<string> dbWarnings;
                    if (!SummarySqliteBridge.RestoreSummaryRowsTransaction(dbPath, restoreMap, out dbWarnings))
                    {
                        if (dbWarnings != null) result.Warnings.AddRange(dbWarnings);
                        result.ErrorMessage = "SQLite 回滚失败: " + (SummarySqliteBridge.LastError ?? "未知错误");
                        return false;
                    }

                    int missCount = 0;
                    if (dbWarnings != null)
                    {
                        for (int w = 0; w < dbWarnings.Count; w++)
                        {
                            result.Warnings.Add(dbWarnings[w]);
                            if (dbWarnings[w] != null && dbWarnings[w].StartsWith("回滚未命中", StringComparison.Ordinal))
                                missCount++;
                        }
                    }
                    result.RestoredDbCount = Math.Max(0, restoreMap.Count - missCount);
                }
            }

            // Layer 3: 仅针对快照内目标 CID 精确修补实时 agyhub_summaries_proto.pb（完整保留其余及新增会话）
            if (pbExists)
            {
                try
                {
                    byte[] livePbBytes = ReadAllBytesShared(pbPath);
                    if (livePbBytes != null && livePbBytes.Length > 0)
                    {
                        List<ProtoNode> rootNodes = ProtoCodec.Decode(livePbBytes);
                        bool modifiedAny = false;

                        for (int i = 0; i < rootNodes.Count; i++)
                        {
                            ProtoNode entry = rootNodes[i];
                            if (entry.FieldNumber != 1 || entry.WireType != 2) continue;

                            string cid = ExtractEntryCidWithoutMutating(entry);
                            if (string.IsNullOrEmpty(cid)) continue;

                            RenameSnapshotItem snapItem;
                            if (!targetByCid.TryGetValue(cid, out snapItem)) continue;

                            ProtoCodec.DecodeSummaryEntryTree(entry);
                            ProtoNode meta = entry.GetChild(2);
                            if (meta == null) continue;
                            if (meta.Children == null) meta.Children = new List<ProtoNode>();

                            RestoreMetaTitlesInAst(meta, snapItem);
                            entry.IsModified = true;
                            modifiedAny = true;
                            result.RestoredPbCount++;
                        }

                        if (modifiedAny)
                        {
                            byte[] encoded = ProtoCodec.Encode(rootNodes);
                            LocalWorkspaceManager.WriteBytesAtomicWithBackup(pbPath, encoded, pbPath + ".bak");
                        }
                    }
                }
                catch (IOException ioEx)
                {
                    if (allowHotWrite)
                    {
                        result.PbSkippedDueToLock = true;
                        result.Warnings.Add("热写入模式跳过 .pb 回滚（文件被占用，L1/L2 已还原）: " + ioEx.Message);
                    }
                    else
                    {
                        result.ErrorMessage = "回滚 agyhub_summaries_proto.pb 失败: " + ioEx.Message;
                        result.Warnings.Add(result.ErrorMessage);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = "回滚 agyhub_summaries_proto.pb 失败: " + ex.Message;
                    result.Warnings.Add(result.ErrorMessage);
                    return false;
                }
            }

            result.Success = true;
            return true;
        }

        private static void RestoreMetaTitlesInAst(ProtoNode meta, RenameSnapshotItem snapItem)
        {
            if (meta == null || snapItem == null) return;
            if (meta.Children == null) meta.Children = new List<ProtoNode>();

            // 1. 恢复或删除 meta Field 1 (AutoSummaryTitle)
            if (!string.IsNullOrEmpty(snapItem.OldPbAutoTitle))
            {
                ProtoNode f1 = null;
                for (int i = 0; i < meta.Children.Count; i++)
                {
                    if (meta.Children[i].FieldNumber == 1)
                    {
                        if (f1 == null && meta.Children[i].WireType == 2)
                        {
                            f1 = meta.Children[i];
                        }
                        else
                        {
                            meta.Children.RemoveAt(i);
                            i--;
                        }
                    }
                }
                if (f1 != null)
                {
                    f1.SetString(snapItem.OldPbAutoTitle);
                }
                else
                {
                    ProtoNode addedF1 = new ProtoNode(1, 2);
                    addedF1.SetString(snapItem.OldPbAutoTitle);
                    meta.Children.Insert(0, addedF1);
                }
            }
            else
            {
                for (int i = meta.Children.Count - 1; i >= 0; i--)
                {
                    if (meta.Children[i].FieldNumber == 1)
                        meta.Children.RemoveAt(i);
                }
            }

            // 2. 恢复 Field 15 -> Field 1 或删除 Field 15 节点
            if (snapItem.HadPbF15)
            {
                string customTitle = snapItem.OldPbCustomTitle ?? string.Empty;
                ProtoNode f15 = null;
                for (int i = 0; i < meta.Children.Count; i++)
                {
                    if (meta.Children[i].FieldNumber == 15)
                    {
                        if (f15 == null && meta.Children[i].WireType == 2)
                        {
                            f15 = meta.Children[i];
                        }
                        else
                        {
                            meta.Children.RemoveAt(i);
                            i--;
                        }
                    }
                }

                if (f15 == null)
                {
                    f15 = new ProtoNode(15, 2);
                    ProtoNode ct1 = new ProtoNode(1, 2);
                    ct1.SetString(customTitle);
                    f15.Children.Add(ct1);
                    f15.IsModified = true;
                    meta.Children.Add(f15);
                }
                else
                {
                    if (!f15.HasChildren && f15.RawBytes != null && f15.RawBytes.Length > 0)
                    {
                        List<ProtoNode> sub15;
                        if (f15.RawBytes.Length >= 2 && (f15.RawBytes[0] == 0x0A || f15.RawBytes[0] == 0x3A) &&
                            ProtoCodec.TryDecodeStrict(f15.RawBytes, out sub15))
                        {
                            f15.Children = sub15;
                        }
                    }

                    if (f15.Children == null) f15.Children = new List<ProtoNode>();

                    if (f15.HasChildren)
                    {
                        ProtoNode ct1 = f15.GetChild(1);
                        if (ct1 != null && ct1.WireType == 2)
                        {
                            ct1.SetString(customTitle);
                        }
                        else
                        {
                            ProtoNode newCt1 = new ProtoNode(1, 2);
                            newCt1.SetString(customTitle);
                            f15.Children.Insert(0, newCt1);
                        }
                    }
                    else
                    {
                        f15.RawBytes = null;
                        ProtoNode newCt1 = new ProtoNode(1, 2);
                        newCt1.SetString(customTitle);
                        f15.Children.Add(newCt1);
                    }
                    f15.IsModified = true;
                }
            }
            else
            {
                for (int i = meta.Children.Count - 1; i >= 0; i--)
                {
                    if (meta.Children[i].FieldNumber == 15)
                        meta.Children.RemoveAt(i);
                }
            }

            meta.IsModified = true;
        }

        private static string ExtractEntryCidWithoutMutating(ProtoNode entryNode)
        {
            if (entryNode == null) return null;
            if (!entryNode.HasChildren && entryNode.WireType == 2 && entryNode.RawBytes != null && entryNode.RawBytes.Length > 0)
            {
                List<ProtoNode> sub;
                if (ProtoCodec.TryDecodeStrict(entryNode.RawBytes, out sub))
                {
                    for (int i = 0; i < sub.Count; i++)
                    {
                        if (sub[i].FieldNumber == 1 && sub[i].WireType == 2)
                        {
                            string s = sub[i].GetString();
                            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
                        }
                    }
                }
                return null;
            }

            ProtoNode f1 = entryNode.GetChild(1);
            if (f1 != null && f1.WireType == 2)
            {
                string s = f1.GetString();
                return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            }
            return null;
        }

        private static RenameSnapshotInfo ParseSnapshotDirectory(string dir, string manifestPath)
        {
            string jsonText;
            using (FileStream fs = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
            {
                jsonText = sr.ReadToEnd();
            }

            Dictionary<string, object> root = MiniJson.Parse(jsonText) as Dictionary<string, object>;
            if (root == null) return null;

            RenameSnapshotInfo info = new RenameSnapshotInfo();
            info.SnapshotDir = dir;
            info.SnapshotName = Path.GetFileName(dir);
            info.CreatedAt = MiniJson.GetString(root, "created_at", string.Empty);
            info.HasPbBackup = File.Exists(Path.Combine(dir, PbBackupFileName));

            DateTime parsedTime;
            if (!string.IsNullOrEmpty(info.CreatedAt) &&
                DateTime.TryParse(info.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsedTime))
            {
                info.CreatedTime = parsedTime.ToLocalTime();
            }
            else if (!TryParseDirTimestamp(info.SnapshotName, out parsedTime))
            {
                info.CreatedTime = Directory.GetCreationTime(dir);
            }
            else
            {
                info.CreatedTime = parsedTime;
            }

            List<object> itemObjs = MiniJson.GetList(root, "items");
            if (itemObjs != null)
            {
                for (int i = 0; i < itemObjs.Count; i++)
                {
                    Dictionary<string, object> d = itemObjs[i] as Dictionary<string, object>;
                    if (d == null) continue;

                    RenameSnapshotItem item = new RenameSnapshotItem();
                    item.ConversationId = MiniJson.GetString(d, "conversation_id", string.Empty);
                    item.ProjectName = MiniJson.GetString(d, "project_name", string.Empty);
                    item.OldTitle = MiniJson.GetString(d, "old_title", string.Empty);
                    item.OldPreview = MiniJson.GetString(d, "old_preview", string.Empty);
                    item.NewTitle = MiniJson.GetString(d, "new_title", string.Empty);
                    item.HadPbtxt = MiniJson.GetBool(d, "had_pbtxt", false);
                    item.OldPbtxtRaw = MiniJson.GetString(d, "old_pbtxt_raw", null);
                    item.OldRawSummaryBase64 = MiniJson.GetString(d, "old_raw_summary_base64", null);
                    item.HadPbF15 = MiniJson.GetBool(d, "had_pb_f15", false);
                    item.OldPbAutoTitle = MiniJson.GetString(d, "old_pb_auto_title", string.Empty);
                    item.OldPbCustomTitle = MiniJson.GetString(d, "old_pb_custom_title", string.Empty);
                    info.Items.Add(item);
                }
            }

            info.ItemCount = info.Items.Count;
            return info;
        }

        private static bool TryParseDirTimestamp(string dirName, out DateTime dt)
        {
            dt = DateTime.MinValue;
            if (string.IsNullOrEmpty(dirName) || dirName.Length < 19) return false;
            return DateTime.TryParseExact(
                dirName.Substring(0, 19),
                "yyyyMMdd_HHmmss_fff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out dt);
        }

        private static void PruneOldSnapshots(string backupRoot, int maxKeep)
        {
            if (string.IsNullOrEmpty(backupRoot) || !Directory.Exists(backupRoot) || maxKeep < 1)
                return;

            string[] dirs = Directory.GetDirectories(backupRoot);
            if (dirs.Length <= maxKeep) return;

            List<RenameSnapshotInfo> snaps = new List<RenameSnapshotInfo>(dirs.Length);
            for (int i = 0; i < dirs.Length; i++)
            {
                string dir = dirs[i];
                string name = Path.GetFileName(dir);
                if (!File.Exists(Path.Combine(dir, ManifestFileName)) &&
                    !Regex.IsMatch(name ?? string.Empty, "^\\d{8}_\\d{6}_\\d{3}_"))
                {
                    continue;
                }

                RenameSnapshotInfo entry = new RenameSnapshotInfo();
                entry.SnapshotDir = dir;
                entry.SnapshotName = name;
                DateTime ts;
                if (TryParseDirTimestamp(name, out ts))
                {
                    entry.CreatedTime = ts;
                }
                else
                {
                    entry.CreatedTime = Directory.GetCreationTime(dir);
                }
                snaps.Add(entry);
            }

            if (snaps.Count <= maxKeep) return;

            snaps.Sort(delegate(RenameSnapshotInfo a, RenameSnapshotInfo b)
            {
                int cmp = DateTime.Compare(b.CreatedTime, a.CreatedTime);
                if (cmp != 0) return cmp;
                return string.Compare(b.SnapshotName, a.SnapshotName, StringComparison.OrdinalIgnoreCase);
            });

            for (int i = maxKeep; i < snaps.Count; i++)
            {
                string oldDir = snaps[i].SnapshotDir;
                if (IsPathContainedUnder(backupRoot, oldDir))
                {
                    try { Directory.Delete(oldDir, true); } catch { }
                }
            }
        }

        private static byte[] ReadAllBytesShared(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (MemoryStream ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static bool IsPathContainedUnder(string rootDir, string targetPath)
        {
            try
            {
                string fullRoot = Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string fullTarget = Path.GetFullPath(targetPath);
                return fullTarget.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // 5. AI POLISH PROMPT & CLIPBOARD JSON EXCHANGE
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// AI 辅助润色会话标题的 Prompt 导出与剪贴板 JSON 校验回填器
    /// </summary>
    public static class AiPolishPromptExchange
    {
        private static readonly Regex MarkdownFenceRegex =
            new Regex("```(?:[A-Za-z0-9_-]+)?\\s*([\\s\\S]*?)```", RegexOptions.IgnoreCase);

        public static string BuildPolishPrompt(IList<SessionRenameCandidate> rows)
        {
            string rowsJson = ExportRowsToJson(rows);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("请根据下列会话的所属工程、原标题与首条用户提问摘要，为每条会话润色生成简洁、准确的中文/技术标题。");
            sb.AppendLine();
            sb.AppendLine("严格要求：");
            sb.AppendLine("1. 必须保留各条目的 `suggested_prefix`（如 `【3KU·Dev_xxx】` 或 `【Companion】`）作为新标题前缀，后接精炼中文摘要（建议总长控制在 15~36 字内，严禁包含换行符）。");
            sb.AppendLine("2. 原封不动保留 `id` 字段（完整 UUID），严禁修改、省略或合并条目。");
            sb.AppendLine("3. 仅返回严格合法的 JSON（可使用 ```json ... ``` 代码块包裹），格式为对象 `{\"items\":[{\"id\":\"...\",\"new_title\":\"...\"}]}` 或数组 `[{\"id\":\"...\",\"new_title\":\"...\"}]`。");
            sb.AppendLine();
            sb.AppendLine("待处理会话列表 JSON：");
            sb.AppendLine("```json");
            sb.AppendLine(rowsJson);
            sb.AppendLine("```");
            return sb.ToString();
        }

        public static string ExportRowsToJson(IList<SessionRenameCandidate> rows)
        {
            List<object> list = new List<object>();
            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    SessionRenameCandidate r = rows[i];
                    if (r == null || string.IsNullOrWhiteSpace(r.ConversationId)) continue;

                    Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.Ordinal);
                    d["id"] = r.ConversationId.Trim();
                    d["project"] = r.ProjectName ?? string.Empty;
                    d["old_title"] = r.OldTitle ?? string.Empty;
                    d["suggested_prefix"] = r.SuggestedPrefix ?? string.Empty;
                    d["suggested_title"] = !string.IsNullOrWhiteSpace(r.NewTitle)
                        ? r.NewTitle.Trim()
                        : (r.SuggestedTitle ?? string.Empty);
                    d["cleaned_prompt"] = r.CleanedPrompt ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(r.GitBranchHint))
                    {
                        d["git_branch"] = r.GitBranchHint.Trim();
                    }
                    list.Add(d);
                }
            }
            return MiniJson.Serialize(list, true);
        }

        public static bool ParseAiClipboardJson(
            string rawText,
            IList<SessionRenameCandidate> rows,
            out Dictionary<string, string> cidToNewTitle,
            out string error)
        {
            cidToNewTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            error = null;

            if (string.IsNullOrWhiteSpace(rawText))
            {
                error = "输入文本为空。";
                return false;
            }

            if (rows == null || rows.Count == 0)
            {
                error = "当前候选会话列表为空，无法匹配回填。";
                return false;
            }

            string jsonBody = StripOptionalMarkdownFence(rawText.Trim());
            if (string.IsNullOrWhiteSpace(jsonBody))
            {
                error = "剥离 Markdown 代码块后 JSON 内容为空。";
                return false;
            }

            object parsed;
            try
            {
                parsed = MiniJson.Parse(jsonBody);
            }
            catch (Exception ex)
            {
                error = "剪贴板 JSON 解析失败: " + ex.Message;
                return false;
            }

            List<object> itemList = parsed as List<object>;
            if (itemList == null)
            {
                Dictionary<string, object> rootDict = parsed as Dictionary<string, object>;
                if (rootDict != null)
                {
                    itemList = MiniJson.GetList(rootDict, "items");
                }
            }

            if (itemList == null)
            {
                error = "JSON 根节点必须是数组 `[...]` 或包含 `\"items\"` 数组的对象。";
                return false;
            }

            if (itemList.Count == 0)
            {
                error = "JSON 未包含任何条目。";
                return false;
            }

            List<string> candidateCids = new List<string>(rows.Count);
            HashSet<string> seenCandidateCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows.Count; i++)
            {
                SessionRenameCandidate r = rows[i];
                if (r == null || string.IsNullOrWhiteSpace(r.ConversationId)) continue;
                string trimmedCid = r.ConversationId.Trim();
                if (seenCandidateCids.Add(trimmedCid))
                {
                    candidateCids.Add(trimmedCid);
                }
            }

            Dictionary<string, string> resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < itemList.Count; i++)
            {
                Dictionary<string, object> itemDict = itemList[i] as Dictionary<string, object>;
                if (itemDict == null)
                {
                    error = "第 " + (i + 1) + " 项不是合法的 JSON 对象。";
                    return false;
                }

                string rawId = MiniJson.GetString(itemDict, "id", null);
                if (string.IsNullOrWhiteSpace(rawId))
                {
                    rawId = MiniJson.GetString(itemDict, "conversation_id", null);
                }
                if (string.IsNullOrWhiteSpace(rawId))
                {
                    error = "第 " + (i + 1) + " 项缺少非空的 \"id\" 字段。";
                    return false;
                }
                string idToken = rawId.Trim();

                string rawTitle = MiniJson.GetString(itemDict, "new_title", null);
                string cleanTitle = NormalizeSingleLineTitle(rawTitle);
                if (string.IsNullOrWhiteSpace(cleanTitle))
                {
                    error = "第 " + (i + 1) + " 项 (id=" + idToken + ") 的 \"new_title\" 不能为空。";
                    return false;
                }

                string matchedCid;
                string matchErr;
                if (!TryResolveCandidateCid(idToken, candidateCids, out matchedCid, out matchErr))
                {
                    error = "第 " + (i + 1) + " 项匹配失败: " + matchErr;
                    return false;
                }

                resolved[matchedCid] = cleanTitle;
            }

            cidToNewTitle = resolved;
            return true;
        }

        private static string StripOptionalMarkdownFence(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed)) return string.Empty;
            Match m = MarkdownFenceRegex.Match(trimmed);
            if (m.Success)
            {
                return m.Groups[1].Value.Trim();
            }
            return trimmed;
        }

        private static string NormalizeSingleLineTitle(string rawTitle)
        {
            if (string.IsNullOrWhiteSpace(rawTitle)) return string.Empty;
            return Regex.Replace(rawTitle, "[\\r\\n]+", " ").Trim();
        }

        private static bool TryResolveCandidateCid(
            string idToken,
            List<string> candidateCids,
            out string matchedCid,
            out string error)
        {
            matchedCid = null;
            error = null;

            // 1. 优先精确匹配完整 UUID / CID
            for (int i = 0; i < candidateCids.Count; i++)
            {
                if (string.Equals(candidateCids[i], idToken, StringComparison.OrdinalIgnoreCase))
                {
                    matchedCid = candidateCids[i];
                    return true;
                }
            }

            // 2. 校验 8+ 位十六进制前缀（允许包含 UUID 连字符 '-'）
            int hexCount = 0;
            for (int i = 0; i < idToken.Length; i++)
            {
                char c = idToken[i];
                if (MiniJson.HexDigitValue(c) >= 0)
                {
                    hexCount++;
                }
                else if (c != '-')
                {
                    error = "ID \"" + idToken + "\" 未精确命中任何会话且包含非十六进制字符。";
                    return false;
                }
            }

            if (hexCount < 8)
            {
                error = "短 ID \"" + idToken + "\" 少于 8 位十六进制字符，已拒绝模糊匹配。";
                return false;
            }

            List<string> prefixMatches = new List<string>();
            for (int i = 0; i < candidateCids.Count; i++)
            {
                if (candidateCids[i].StartsWith(idToken, StringComparison.OrdinalIgnoreCase))
                {
                    prefixMatches.Add(candidateCids[i]);
                }
            }

            if (prefixMatches.Count == 0)
            {
                error = "未找到匹配 ID 前缀 \"" + idToken + "\" 的会话。";
                return false;
            }

            if (prefixMatches.Count > 1)
            {
                error = "ID 前缀 \"" + idToken + "\" 存在歧义，同时命中了 " + prefixMatches.Count + " 个会话，请使用更长前缀或完整 UUID。";
                return false;
            }

            matchedCid = prefixMatches[0];
            return true;
        }
    }
}
