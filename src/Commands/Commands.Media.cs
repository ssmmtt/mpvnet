
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
        public static void ShowMediaInfo(string[] args) => App.InvokeOnMainThread(() =>
        {
            if (args == null || args.Length == 0)
            {
                (string Name, string Value)[] pairs = {
                    ("Show text box",    "script-message-to mpvnet show-media-info default"),
                    ("Show text editor", "script-message-to mpvnet show-media-info editor"),
                    ("Show on screen",   "script-message-to mpvnet show-media-info osd"),
                    ("Show full",        "script-message-to mpvnet show-media-info editor full"),
                    ("Show raw",         "script-message-to mpvnet show-media-info editor full raw") };

                var list = pairs.Select(i => new CommandPaletteItem(i.Name, () => Core.Command(i.Value)));
                CommandPalette.Instance.SetItems(list);
                MainForm.Instance.ShowCommandPalette();
                CommandPalette.Instance.SelectFirst();
                return;
            }

            string path = Core.GetPropertyString("path");
            string text = "";

            bool full = args.Contains("full");
            bool raw = args.Contains("raw");
            bool editor = args.Contains("editor");
            bool osd = args.Contains("osd");

            if (App.MediaInfo && !osd && !CorePlayer.IsRemotePath(path) && File.Exists(path))
                using (MediaInfo mediaInfo = new MediaInfo(path))
                    text = Regex.Replace(mediaInfo.GetSummary(full, raw), "Unique ID.+", "");
            else
            {
                Core.UpdateExternalTracks();
                text = "N: " + Core.GetPropertyString("filename") + BR;
                lock (Core.MediaTracksLock)
                    foreach (MediaTrack track in Core.MediaTracks)
                        text += track.Text + BR;
            }

            text = text.TrimEx();

            if (editor)
                ShowTextWithEditor("media-info", text);
            else if (osd)
                ShowText(text.Replace("\r", ""), 5000, 16);
            else
            {
                MsgBoxEx.MessageBoxEx.MsgFontFamily = new FontFamily("Consolas");
                Msg.ShowInfo(text);
                MsgBoxEx.MessageBoxEx.MsgFontFamily = new FontFamily("Segoe UI");
            }
        });

        public static void ShowCommandPalette() => App.InvokeOnMainThread(() =>
        {
            CommandPalette.Instance.SetItems(CommandPalette.GetItems());
            MainForm.Instance.ShowCommandPalette();
            CommandPalette.Instance.SelectFirst();
        });

        public static void ShowAudioTracks() => App.InvokeOnMainThread(() =>
        {
            Core.UpdateExternalTracks();

            lock (Core.MediaTracksLock)
            {
                MediaTrack[] tracks = Core.MediaTracks.Where(track => track.Type == "a").ToArray();
       
                if (tracks.Length < 1)
                {
                    Core.CommandV("show-text", "No audio tracks");
                    return;
                }

                List<CommandPaletteItem> items = new List<CommandPaletteItem>();

                foreach (MediaTrack i in tracks)
                {
                    MediaTrack track = i;

                    CommandPaletteItem item = new CommandPaletteItem()
                    {
                        Text = track.Text,
                        Action = () => {
                            Core.CommandV("set", "aid", track.ID.ToString());
                            Core.CommandV("show-text", track.ID + "/" + tracks.Length + ": " +
                                tracks[track.ID - 1].Text.Substring(3), "5000");
                        }
                    };

                    items.Add(item);
                }

                CommandPalette.Instance.SetItems(items);
                MainForm.Instance.ShowCommandPalette();
                CommandPalette.Instance.SelectFirst();
            }

        });

        public static void ShowSubtitleTracks() => App.InvokeOnMainThread(() =>
        {
            Core.UpdateExternalTracks();

            lock (Core.MediaTracksLock)
            {
                string[] subPrefs = SubtitleTrackUi.ParsePreferenceList(
                    Core.Conf.TryGetValue("slang", out string slangRaw) ? slangRaw : null);
                MediaTrack[] tracks = SubtitleTrackUi.OrderSubtitleTracksForUi(
                    Core.MediaTracks.Where(track => track.Type == "s"), subPrefs).ToArray();

                if (tracks.Length < 1)
                {
                    Core.CommandV("show-text", "No subtitle tracks");
                    return;
                }

                List<CommandPaletteItem> items = new List<CommandPaletteItem>();

                foreach (MediaTrack i in tracks)
                {
                    MediaTrack track = i;

                    CommandPaletteItem item = new CommandPaletteItem()
                    {
                        Text = track.Text,
                        Action = () => {
                            Core.CommandV("set", "sid", track.ID.ToString());
                            Core.CommandV("show-text", track.Text, "5000");
                        }
                    };

                    items.Add(item);
                }

                CommandPalette.Instance.SetItems(items);
                MainForm.Instance.ShowCommandPalette();
                CommandPalette.Instance.SelectFirst();
            }
        });

    }
}
