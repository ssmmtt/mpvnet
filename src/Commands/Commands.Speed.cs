
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;

using WinForms = System.Windows.Forms;

using static mpvnet.Global;

namespace mpvnet
{
    public partial class Commands
    {
        public static string FormatSpeedOsd(double speed) =>
            I18n._("Speed") + ": " + speed.ToString("0.00", CultureInfo.InvariantCulture);

        static string OsdDurationArg()
        {
            int ms = Core.GetPropertyInt("osd-duration");
            if (ms <= 0)
                ms = 3000;
            return ms.ToString(CultureInfo.InvariantCulture);
        }

        public static void ShowSpeedOsd()
        {
            double speed = Math.Round(Core.GetPropertyDouble("speed") * 100) / 100;
            if (Math.Abs(Core.GetPropertyDouble("speed") - speed) > 0.0001)
                Core.SetPropertyDouble("speed", speed);
            Core.CommandV("show-text", FormatSpeedOsd(speed), OsdDurationArg());
        }

        public static void AddSpeed(string delta)
        {
            if (!double.TryParse(delta, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                return;

            double speed = Core.GetPropertyDouble("speed") + d;
            if (speed < 0.01)
                speed = 0.01;

            speed = Math.Round(speed * 100) / 100;
            Core.SetPropertyDouble("speed", speed);
            Core.CommandV("show-text", FormatSpeedOsd(speed), OsdDurationArg());
        }
    }
}
