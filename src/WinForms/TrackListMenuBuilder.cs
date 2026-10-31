
using System.Collections.Generic;

using WpfControls = System.Windows.Controls;

using static mpvnet.Global;

namespace mpvnet
{
    public static class TrackListMenuBuilder
    {
        public static void Fill(WpfControls.MenuItem menu, List<MediaTrack> tracks, string prop, string currentId, string noneLabel)
        {
            if (menu == null)
                return;

            for (int i = menu.Items.Count - 1; i >= 0; i--)
            {
                object it = menu.Items[i];
                if ((it is WpfControls.MenuItem mi && Equals(mi.Tag, "dyn-track")) ||
                    (it is WpfControls.Separator sep && Equals(sep.Tag, "dyn-track")))
                    menu.Items.RemoveAt(i);
            }

            int at = 0;
            foreach (MediaTrack track in tracks)
            {
                MediaTrack t = track;
                var mi = new WpfControls.MenuItem
                {
                    Header = t.Text.Replace("_", "__"),
                    Tag = "dyn-track",
                    IsChecked = currentId == t.ID.ToString()
                };
                mi.Click += (sender, args) => Core.CommandV("set", prop, t.ID.ToString());
                menu.Items.Insert(at++, mi);
            }

            if (noneLabel != null)
            {
                var none = new WpfControls.MenuItem
                {
                    Header = I18n._(noneLabel),
                    Tag = "dyn-track",
                    IsChecked = currentId == "no"
                };
                none.Click += (sender, args) => Core.CommandV("set", prop, "no");
                menu.Items.Insert(at++, none);
            }

            if (at > 0)
                menu.Items.Insert(at, new WpfControls.Separator { Tag = "dyn-track" });
        }
    }
}
