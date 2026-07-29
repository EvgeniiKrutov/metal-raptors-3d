using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MetalRaptors
{
    public static class MusicJson
    {
        public static object Parse(string text)
        {
            int pos = 0;
            return ParseValue(text, ref pos);
        }

        static object ParseValue(string s, ref int pos)
        {
            SkipWhitespace(s, ref pos);
            char c = s[pos];
            switch (c)
            {
                case '{': return ParseObject(s, ref pos);
                case '[': return ParseArray(s, ref pos);
                case '"': return ParseString(s, ref pos);
                case 't': pos += 4; return true;
                case 'f': pos += 5; return false;
                case 'n': pos += 4; return null;
                default: return ParseNumber(s, ref pos);
            }
        }

        static Dictionary<string, object> ParseObject(string s, ref int pos)
        {
            var result = new Dictionary<string, object>();
            pos++;
            SkipWhitespace(s, ref pos);
            if (s[pos] == '}')
            {
                pos++;
                return result;
            }
            while (true)
            {
                SkipWhitespace(s, ref pos);
                string key = ParseString(s, ref pos);
                SkipWhitespace(s, ref pos);
                pos++;
                result[key] = ParseValue(s, ref pos);
                SkipWhitespace(s, ref pos);
                if (s[pos++] == '}') return result;
            }
        }

        static List<object> ParseArray(string s, ref int pos)
        {
            var result = new List<object>();
            pos++;
            SkipWhitespace(s, ref pos);
            if (s[pos] == ']')
            {
                pos++;
                return result;
            }
            while (true)
            {
                result.Add(ParseValue(s, ref pos));
                SkipWhitespace(s, ref pos);
                if (s[pos++] == ']') return result;
            }
        }

        static string ParseString(string s, ref int pos)
        {
            var sb = new StringBuilder();
            pos++;
            while (s[pos] != '"')
            {
                char c = s[pos++];
                if (c == '\\')
                {
                    char e = s[pos++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)int.Parse(s.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            pos += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            pos++;
            return sb.ToString();
        }

        static object ParseNumber(string s, ref int pos)
        {
            int start = pos;
            while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '-' || s[pos] == '+' ||
                                      s[pos] == '.' || s[pos] == 'e' || s[pos] == 'E'))
                pos++;
            return double.Parse(s.Substring(start, pos - start), CultureInfo.InvariantCulture);
        }

        static void SkipWhitespace(string s, ref int pos)
        {
            while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
        }
    }
}
