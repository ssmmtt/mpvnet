
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using static mpvnet.Global;

namespace mpvnet
{
    /// <summary>Persist playback speed to/from mpv.conf (not hold-boost).</summary>
    public static class SpeedPersistence
    {
        public static double? Read(string confPath)
        {
            if (!File.Exists(confPath))
                return null;

            foreach (string line in File.ReadAllLines(confPath))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("#") || !trimmed.StartsWith("speed"))
                    continue;

                int eq = trimmed.IndexOf('=');
                if (eq < 0)
                    continue;

                if (double.TryParse(trimmed.Substring(eq + 1).Trim().Trim('\'', '"'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double speed))
                    return Clamp(speed);
            }

            return null;
        }

        public static void Write(string confPath, double speed)
        {
            speed = Clamp(speed);
            string speedLine = "speed = " + speed.ToString("0.######", CultureInfo.InvariantCulture);
            var lines = new List<string>();
            bool found = false;

            if (File.Exists(confPath))
            {
                foreach (string line in File.ReadAllLines(confPath))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("speed") && trimmed.Contains("=") && !trimmed.StartsWith("#"))
                    {
                        if (!found)
                        {
                            lines.Add(speedLine);
                            found = true;
                        }
                        continue;
                    }
                    lines.Add(line);
                }
            }

            if (!found)
            {
                if (lines.Count > 0 && lines[lines.Count - 1] != "")
                    lines.Add("");
                lines.Add(speedLine);
            }

            File.WriteAllText(confPath, string.Join(BR, lines) + BR);
        }

        public static double Clamp(double speed)
        {
            if (speed < 0.05) return 0.05;
            if (speed > 5.0) return 5.0;
            return speed;
        }
    }
}
