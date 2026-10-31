
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace mpvnet
{
    /// <summary>
    /// gettext-style translations loaded from Locale/&lt;lang&gt;/LC_MESSAGES/mpvnet.po
    /// </summary>
    public static class I18n
    {
        static readonly Dictionary<string, string> Map = new Dictionary<string, string>();
        static readonly object Sync = new object();

        /// <summary>Resolved language folder name, e.g. zh, en.</summary>
        public static string Language { get; private set; } = "en";

        public static void Init(string preference)
        {
            lock (Sync)
            {
                Map.Clear();
                Language = ResolveLanguage(preference);

                if (Language == "en")
                    return;

                string poPath = Path.Combine(Folder.Startup, "Locale", Language, "LC_MESSAGES", "mpvnet.po");
                if (!File.Exists(poPath))
                    poPath = Path.Combine(Folder.Startup, "Locale", Language, "mpvnet.po");

                if (File.Exists(poPath))
                    LoadPo(poPath);
            }
        }

        public static string ResolveLanguage(string preference)
        {
            if (string.IsNullOrWhiteSpace(preference) || preference == "system")
                return CultureToLang(CultureInfo.CurrentUICulture);

            preference = preference.Trim().ToLowerInvariant();
            if (preference == "chinese" || preference == "chinese-china" || preference == "zh-cn" || preference == "zh_cn")
                return "zh";
            if (preference.StartsWith("zh"))
                return "zh";
            if (preference == "en" || preference == "english")
                return "en";

            return CultureToLang(new CultureInfo(preference.Replace('_', '-')));
        }

        static string CultureToLang(CultureInfo culture)
        {
            if (culture == null || culture.Equals(CultureInfo.InvariantCulture))
                return "en";

            string two = culture.TwoLetterISOLanguageName;
            if (two == "zh")
                return "zh";
            return two;
        }

        public static string _(string msgid)
        {
            if (string.IsNullOrEmpty(msgid))
                return msgid;

            lock (Sync)
            {
                if (Map.TryGetValue(msgid, out string msgstr) && !string.IsNullOrEmpty(msgstr))
                    return msgstr;
            }

            return msgid;
        }

        /// <summary>Translate menu path segments separated by " &gt; " or " | ".</summary>
        public static string TranslateMenuPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            string[] parts = path.Split(new[] { " > ", " | " }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "-")
                    continue;
                parts[i] = _(parts[i]);
            }

            // Rebuild with the first separator style found.
            if (path.Contains(" | "))
                return string.Join(" | ", parts);
            return string.Join(" > ", parts);
        }

        static void LoadPo(string path)
        {
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            string msgid = null;
            string msgstr = null;
            bool inMsgid = false;
            bool inMsgstr = false;

            void Commit()
            {
                if (msgid != null && msgstr != null && msgid != "")
                    Map[msgid] = msgstr;
                msgid = null;
                msgstr = null;
                inMsgid = false;
                inMsgstr = false;
            }

            foreach (string raw in lines)
            {
                string line = raw;

                if (line.StartsWith("#") || line.Trim() == "")
                {
                    if (line.Trim() == "")
                        Commit();
                    continue;
                }

                if (line.StartsWith("msgid "))
                {
                    Commit();
                    msgid = Unquote(line.Substring(6));
                    inMsgid = true;
                    inMsgstr = false;
                    continue;
                }

                if (line.StartsWith("msgstr "))
                {
                    msgstr = Unquote(line.Substring(7));
                    inMsgid = false;
                    inMsgstr = true;
                    continue;
                }

                if (line.StartsWith("\""))
                {
                    string piece = Unquote(line);
                    if (inMsgid)
                        msgid += piece;
                    else if (inMsgstr)
                        msgstr += piece;
                }
            }

            Commit();
        }

        static string Unquote(string s)
        {
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                s = s.Substring(1, s.Length - 2);

            return s
                .Replace("\\n", "\n")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }
    }
}
