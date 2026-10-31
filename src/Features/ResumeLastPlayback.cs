
using static mpvnet.Global;

namespace mpvnet
{
    /// <summary>When started with no file, open the most recent media from settings.</summary>
    public static class ResumeLastPlayback
    {
        public static void MaybeStart()
        {
            if (!App.ResumeLast)
                return;

            var timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                timer.Dispose();

                if (!string.IsNullOrEmpty(Core.GetPropertyString("path")))
                    return;
                if (Core.GetPropertyInt("playlist-count") > 0)
                    return;
                if (App.Settings.RecentFiles.Count == 0)
                    return;

                string last = App.GetTitleAndPath(App.Settings.RecentFiles[0]).Path;
                if (string.IsNullOrEmpty(last))
                    return;

                Core.CommandV("loadfile", last, "replace");
                Core.CommandV("show-text", "继续上次播放");
            };
            timer.Start();
        }
    }
}
