
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace mpvnet
{
    /// <summary>Subtitle prefer/sort/watch-later helpers and language labels for UI.</summary>
    public static class SubtitleTrackUi
    {
        public static readonly string[] SimplifiedSubPrefer = {
            "chs", "sc", "zh-hans", "zh-cn", "简体", "简中", "简日", "简英", "simplified"
        };
        public static readonly string[] TraditionalSubAvoid = {
            "cht", "tc", "zh-hant", "zh-tw", "zh-hk", "繁体", "繁體", "繁中", "繁日", "繁英", "traditional"
        };

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
        /// UI-language subtitle tracks first; relative order kept within each group.
        /// For zh UI, simplified Chinese is ahead of other Chinese tracks.
        /// </summary>
        public static List<MediaTrack> OrderSubtitleTracksForUi(IEnumerable<MediaTrack> tracks)
        {
            string[] keys = UiLanguageMatchKeys();
            var match = new List<MediaTrack>();
            var rest = new List<MediaTrack>();

            foreach (MediaTrack t in tracks)
            {
                if (TrackTextMatchesKeys(t.Text, keys))
                    match.Add(t);
                else
                    rest.Add(t);
            }

            if (I18n.Language == "zh")
                match = PreferSimplifiedSubtitleOrder(match);

            match.AddRange(rest);
            return match;
        }

        public static List<MediaTrack> PreferSimplifiedSubtitleOrder(List<MediaTrack> chineseTracks)
        {
            var simp = new List<MediaTrack>();
            var other = new List<MediaTrack>();

            foreach (MediaTrack t in chineseTracks)
            {
                string h = (t.Text ?? "").ToLowerInvariant();
                bool trad = false;
                foreach (string a in TraditionalSubAvoid)
                {
                    if (h.IndexOf(a.ToLowerInvariant(), StringComparison.Ordinal) >= 0)
                    {
                        trad = true;
                        break;
                    }
                }

                bool simplified = false;
                if (!trad)
                {
                    foreach (string a in SimplifiedSubPrefer)
                    {
                        if (h.IndexOf(a.ToLowerInvariant(), StringComparison.Ordinal) >= 0)
                        {
                            simplified = true;
                            break;
                        }
                    }
                    if (!simplified && (h.Contains("简体") || h.Contains("简中")))
                        simplified = true;
                }

                if (simplified)
                    simp.Add(t);
                else
                    other.Add(t);
            }

            simp.AddRange(other);
            return simp;
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
