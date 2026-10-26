using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VK
{
    /// <summary>
    /// Localisation by phrase: the game is written in Vietnamese and every displayed string passes through
    /// <see cref="Tr"/>, which swaps each known Vietnamese phrase (Resources/Loc/strings.txt: vi TAB en TAB zh)
    /// for the target language while leaving numbers, rich-text tags and punctuation in place.
    /// Phrases are matched longest-first on word boundaries, case-insensitively for all-caps titles.
    /// </summary>
    public static class Loc
    {
        public const int VI = 0, EN = 1, ZH = 2;
        public static readonly string[] LangName = { "Tiếng Việt", "English", "中文" };
        public static int Lang { get; private set; }

        static Dictionary<char, List<KeyValuePair<string, string>>> table;   // first char -> (vi, target) longest first
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        static readonly StringBuilder sb = new StringBuilder(256);

        public static void SetLang(int lang)
        {
            Lang = Mathf.Clamp(lang, 0, 2);
            cache.Clear();
            table = null;
            Res.ResetFont();
        }

        /// <summary>Initial language for a new save: the OS language if it is Vietnamese or Chinese, otherwise English.</summary>
        public static int SystemDefault()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Vietnamese: return VI;
                case SystemLanguage.Chinese: case SystemLanguage.ChineseSimplified: case SystemLanguage.ChineseTraditional: return ZH;
                default: return EN;
            }
        }

        static void Load()
        {
            table = new Dictionary<char, List<KeyValuePair<string, string>>>();
            var ta = Resources.Load<TextAsset>("Loc/strings");
            if (ta == null) return;
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                var f = line.Split('\t');
                if (f.Length < 3 || f[0].Length == 0) continue;
                string target = f[Lang].Replace("\\n", "\n");
                if (target.Length == 0) continue;
                Add(f[0], target);
                string up = f[0].ToUpperInvariant();
                if (up != f[0]) Add(up, Lang == ZH ? target : target.ToUpperInvariant());
            }
            foreach (var l in table.Values) l.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
        }

        static void Add(string key, string val)
        {
            if (!table.TryGetValue(key[0], out var l)) table[key[0]] = l = new List<KeyValuePair<string, string>>();
            l.Add(new KeyValuePair<string, string>(key, val));
        }

        static bool IsWordChar(char c) => char.IsLetterOrDigit(c);

        public static string Tr(string s)
        {
            if (Lang == VI || string.IsNullOrEmpty(s)) return s;
            if (cache.TryGetValue(s, out var hit)) return hit;
            if (table == null) Load();
            sb.Clear();
            int i = 0, n = s.Length;
            while (i < n)
            {
                char c = s[i];
                if (c == '<')
                {   // copy rich-text tags untouched
                    int close = s.IndexOf('>', i);
                    if (close > i) { sb.Append(s, i, close - i + 1); i = close + 1; continue; }
                }
                bool matched = false;
                if ((i == 0 || !IsWordChar(s[i - 1])) && table.TryGetValue(c, out var list))
                {
                    foreach (var kv in list)
                    {
                        int len = kv.Key.Length;
                        if (i + len > n || string.CompareOrdinal(s, i, kv.Key, 0, len) != 0) continue;
                        if (i + len < n && IsWordChar(s[i + len]) && IsWordChar(kv.Key[len - 1])) continue;
                        sb.Append(kv.Value);
                        i += len;
                        matched = true;
                        break;
                    }
                }
                if (!matched) { sb.Append(c); i++; }
            }
            string r = sb.ToString();
            if (cache.Count > 4000) cache.Clear();
            cache[s] = r;
            return r;
        }
    }

    /// <summary>uGUI Text that localises whatever is assigned to it.</summary>
    public class LocText : UnityEngine.UI.Text
    {
        public override string text
        {
            get => base.text;
            set => base.text = Loc.Tr(value);
        }
    }
}
