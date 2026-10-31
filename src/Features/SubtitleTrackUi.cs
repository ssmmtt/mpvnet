
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace mpvnet
{
    /// <summary>Subtitle prefer/sort/watch-later helpers and language labels for UI.</summary>
    public static class SubtitleTrackUi
    {
        /// <summary>Split conf subtitle-language priority list: "a, b,c" → ["a","b","c"].</summary>
        public static string[] ParsePreferenceList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new string[0];

            string s = raw.Trim().Trim('\'', '"');
            var list = new List<string>();
            foreach (string part in s.Split(','))
            {
                string t = part.Trim().Trim('\'', '"');
                if (t.Length > 0)
                    list.Add(t);
            }
            return list.ToArray();
        }

        /// <summary>
        /// Walk conf subtitle-language priority in order; first key that matches any
        /// sub track wins (lang or title). 0 = no match.
        /// </summary>
        public static int FindSidByPreferenceOrder(
            string[] preferenceKeys,
            IList<Tuple<int, string, string>> subTracks)
        {
            if (preferenceKeys == null || preferenceKeys.Length == 0 || subTracks == null)
                return 0;

            foreach (string key in preferenceKeys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;

                for (int i = 0; i < subTracks.Count; i++)
                {
                    var t = subTracks[i];
                    string hay = ((t.Item2 ?? "") + " " + (t.Item3 ?? "")).Trim();
                    if (hay.Length == 0)
                        continue;
                    if (TrackTextMatchesKeys(hay, new[] { key }))
                        return t.Item1;
                }
            }

            return 0;
        }

        public static bool WatchLaterHasSid(string configFolder, string path)
        {
            string dir = configFolder + "watch_later";
            if (!Directory.Exists(dir))
                return false;

            foreach (string candidate in WatchLaterPathCandidates(path))
            {
                string file = Path.Combine(dir, StringHelp.GetMD5Hash(candidate));
                if (!File.Exists(file))
                    continue;

                try
                {
                    foreach (string line in File.ReadAllLines(file))
                    {
                        string t = line.Trim();
                        if (t.StartsWith("sid=", StringComparison.OrdinalIgnoreCase) ||
                            t.StartsWith("sid =", StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
                catch { }
            }

            return false;
        }

        public static List<string> WatchLaterPathCandidates(string path)
        {
            var list = new List<string> { path };
            if (path.IndexOf('/') >= 0)
                list.Add(path.Replace('/', '\\'));
            if (path.IndexOf('\\') >= 0)
                list.Add(path.Replace('\\', '/'));
            try
            {
                string full = Path.GetFullPath(path);
                if (!string.Equals(full, path, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(full);
                    list.Add(full.Replace('\\', '/'));
                }
            }
            catch { }
            return list;
        }

        /// <summary>
        /// Tracks matching conf subtitle-language priority first (that list's order),
        /// then the rest. Falls back to UI-language keys if preferenceKeys empty.
        /// </summary>
        public static List<MediaTrack> OrderSubtitleTracksForUi(
            IEnumerable<MediaTrack> tracks, string[] preferenceKeys = null)
        {
            string[] keys = (preferenceKeys != null && preferenceKeys.Length > 0)
                ? preferenceKeys
                : UiLanguageMatchKeys();

            var ordered = new List<MediaTrack>();
            var used = new HashSet<MediaTrack>();

            foreach (string key in keys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;
                foreach (MediaTrack t in tracks)
                {
                    if (used.Contains(t))
                        continue;
                    if (!TrackTextMatchesKeys(t.Text, new[] { key }))
                        continue;
                    ordered.Add(t);
                    used.Add(t);
                }
            }

            foreach (MediaTrack t in tracks)
            {
                if (!used.Contains(t))
                    ordered.Add(t);
            }

            return ordered;
        }

        public static string[] UiLanguageMatchKeys()
        {
            switch (I18n.Language)
            {
                case "zh":
                    return new[] {
                        "中文", "简体", "繁体", "繁體", "chinese",
                        "zh-hans", "zh-hant", "zh-cn", "zh-tw", "zh-hk",
                        "chs", "cht", "zho", "chi", "zh", "sc", "tc"
                    };
                case "ja":
                    return new[] { "日语", "日本語", "japanese", "jpn", "ja" };
                case "ko":
                    return new[] { "韩语", "한국어", "korean", "kor", "ko" };
                case "de":
                    return new[] { "德语", "deutsch", "german", "deu", "ger", "de" };
                case "fr":
                    return new[] { "法语", "français", "french", "fra", "fre", "fr" };
                case "es":
                    return new[] { "西班牙语", "español", "spanish", "spa", "es" };
                case "ru":
                    return new[] { "俄语", "русский", "russian", "rus", "ru" };
                default:
                    return new[] { "英语", "english", "eng", "en" };
            }
        }

        public static bool TrackTextMatchesKeys(string text, string[] keys)
        {
            if (string.IsNullOrEmpty(text) || keys == null || keys.Length == 0)
                return false;

            string h = text.ToLowerInvariant();

            foreach (string key in keys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;

                string k = key.ToLowerInvariant();
                bool codeLike = k.Length <= 3 && IsAsciiLetterToken(k);

                if (codeLike)
                {
                    int i = 0;
                    while ((i = h.IndexOf(k, i, StringComparison.Ordinal)) >= 0)
                    {
                        bool leftOk = i == 0 || !char.IsLetterOrDigit(h[i - 1]);
                        bool rightOk = i + k.Length >= h.Length || !char.IsLetterOrDigit(h[i + k.Length]);
                        if (leftOk && rightOk)
                            return true;
                        i += k.Length;
                    }
                }
                else if (h.IndexOf(k, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsAsciiLetterToken(string s)
        {
            foreach (char c in s)
            {
                if (c > 127 || !char.IsLetter(c))
                    return false;
            }
            return s.Length > 0;
        }

        public static string LocalizeLanguageLabel(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            string s = raw.Trim();
            string lower = s.ToLowerInvariant();

            if ((lower.Contains("traditional") && lower.Contains("chinese")) ||
                lower == "cht" || lower == "zh-hant" || lower == "zh-tw" || lower == "zh-hk" || lower == "zh_tw" || lower == "zh_hk")
                return I18n._("Chinese (Traditional)");
            if ((lower.Contains("simplified") && lower.Contains("chinese")) ||
                lower == "chs" || lower == "sc" || lower == "zh-hans" || lower == "zh-cn" || lower == "zh_cn")
                return I18n._("Chinese (Simplified)");

            CultureInfo match = ResolveCulture(s);
            if (match == null)
                return s;

            if (IsChineseCulture(match))
                return I18n._("Chinese");

            if (I18n.Language == "en")
                return match.EnglishName;

            CultureInfo old = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                return match.DisplayName;
            }
            catch
            {
                return match.EnglishName;
            }
            finally
            {
                CultureInfo.CurrentUICulture = old;
            }
        }

        static bool IsChineseCulture(CultureInfo ci)
        {
            string two = ci.TwoLetterISOLanguageName;
            return two == "zh" || ci.ThreeLetterISOLanguageName == "zho" || ci.ThreeLetterISOLanguageName == "chi";
        }

        static CultureInfo ResolveCulture(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            try
            {
                if (id.Length <= 3 || id.Contains("-") || id.Contains("_"))
                {
                    string name = id.Replace('_', '-');
                    try { return CultureInfo.GetCultureInfo(name); }
                    catch { }
                }
            }
            catch { }

            string mapped = MapBibliographic(id.ToLowerInvariant());
            foreach (CultureInfo ci in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
            {
                if (ci.ThreeLetterISOLanguageName == id ||
                    ci.ThreeLetterISOLanguageName == mapped ||
                    ci.TwoLetterISOLanguageName.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                    ci.EnglishName.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                    ci.NativeName.Equals(id, StringComparison.OrdinalIgnoreCase))
                    return ci;
            }

            foreach (CultureInfo ci in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
            {
                if (ci.EnglishName.StartsWith(id + " (", StringComparison.OrdinalIgnoreCase) ||
                    id.StartsWith(ci.EnglishName + " (", StringComparison.OrdinalIgnoreCase))
                    return ci;
            }

            return null;
        }

        static string MapBibliographic(string id2)
        {
            switch (id2)
            {
                case "bng": return "ben";
                case "ces": return "cze";
                case "deu": return "ger";
                case "ell": return "gre";
                case "eus": return "baq";
                case "fra": return "fre";
                case "hye": return "arm";
                case "isl": return "ice";
                case "kat": return "geo";
                case "mya": return "bur";
                case "nld": return "dut";
                case "sqi": return "alb";
                case "zho": return "chi";
                default: return id2;
            }
        }
    }
}
