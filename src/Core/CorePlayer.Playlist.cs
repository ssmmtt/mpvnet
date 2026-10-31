
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

using static libmpv;
using static mpvnet.Global;
using System.Text.RegularExpressions;

namespace mpvnet
{
    public partial class CorePlayer
    {
        public void ProcessCommandLine(bool preInit)
        {
            bool shuffle = false;
            var args = Environment.GetCommandLineArgs().Skip(1);

            string[] preInitProperties = { "input-terminal", "terminal", "input-file", "config",
                "config-dir", "input-conf", "load-scripts", "scripts", "player-operation-mode",
                "idle", "log-file", "msg-color", "dump-stats", "msg-level", "really-quiet" };

            foreach (string i in args)
            {
                string arg = i;

                if (arg.StartsWith("-") && arg.Length > 1)
                {
                    if (!preInit)
                    {
                        if (arg == "--profile=help")
                        {
                            Console.WriteLine(mpvHelp.GetProfiles());
                            continue;
                        }
                        else if (arg == "--vd=help" || arg == "--ad=help")
                        {
                            Console.WriteLine(mpvHelp.GetDecoders());
                            continue;
                        }
                        else if (arg == "--audio-device=help")
                        {
                            Console.WriteLine(GetPropertyOsdString("audio-device-list"));
                            continue;
                        }
                        else if (arg == "--version")
                        {
                            Console.WriteLine(App.Version);
                            continue;
                        }
                        else if (arg == "--input-keylist")
                        {
                            Console.WriteLine(GetPropertyString("input-key-list").Replace(",", BR));
                            continue;
                        }
                        else if (arg.StartsWith("--command="))
                        {
                            Command(arg.Substring(10));
                            continue;
                        }
                    }

                    if (!arg.StartsWith("--"))
                        arg = "-" + arg;

                    if (!arg.Contains("="))
                    {
                        if (arg.Contains("--no-"))
                        {
                            arg = arg.Replace("--no-", "--");
                            arg += "=no";
                        }
                        else
                            arg += "=yes";
                    }

                    string left = arg.Substring(2, arg.IndexOf("=") - 2);
                    string right = arg.Substring(left.Length + 3);

                    switch (left)
                    {
                        case "script":        left = "scripts";        break;
                        case "audio-file":    left = "audio-files";    break;
                        case "sub-file":      left = "sub-files";      break;
                        case "external-file": left = "external-files"; break;
                    }

                    if (preInit && preInitProperties.Contains(left))
                    {
                        ProcessProperty(left, right);

                        if (!App.ProcessProperty(left, right))
                            SetPropertyString(left, right);
                    }
                    else if (!preInit && !preInitProperties.Contains(left))
                    {
                        ProcessProperty(left, right);

                        if (!App.ProcessProperty(left, right))
                        {
                            SetPropertyString(left, right);

                            if (left == "shuffle" && right == "yes")
                                shuffle = true;
                        }
                    }
                }
            }

            if (!preInit)
            {
                List<string> files = new List<string>();

                foreach (string i in args)
                    if (!i.StartsWith("--") && (i == "-" || i.Contains("://") ||
                        i.Contains(":\\") || i.StartsWith("\\\\") || File.Exists(i)))

                        files.Add(i);

                LoadFiles(files.ToArray(), !App.Queue, Control.ModifierKeys.HasFlag(Keys.Control) || App.Queue);

                if (shuffle)
                {
                    Command("playlist-shuffle");
                    SetPropertyInt("playlist-pos", 0);
                }

                if (files.Count == 0 || files[0].Contains("://"))
                {
                    VideoSizeChanged?.Invoke(VideoSize);
                    VideoSizeAutoResetEvent.Set();
                }
            }
        }

        public DateTime LastLoad;

        public void LoadFiles(string[] files, bool loadFolder, bool append)
        {
            if (files is null || files.Length == 0)
                return;

            if ((DateTime.Now - LastLoad).TotalMilliseconds < 1000)
                append = true;

            LastLoad = DateTime.Now;

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];

                if (string.IsNullOrEmpty(file))
                    continue;

                if (file.Contains("|"))
                    file = file.Substring(0, file.IndexOf("|"));

                file = ConvertFilePath(file);

                string ext = file.Ext();

                switch (ext)
                {
                    case "avs": LoadAviSynth(); break;
                    case "lnk": file = GetShortcutTarget(file); break;
                }

                if (ext == "iso")
                    LoadISO(file);
                else if(SubtitleTypes.Contains(ext))
                    CommandV("sub-add", file);
                else if (!IsMediaExtension(ext) && !file.Contains("://") && Directory.Exists(file) &&
                    File.Exists(System.IO.Path.Combine(file, "BDMV\\index.bdmv")))
                {
                    Command("stop");
                    Thread.Sleep(500);
                    SetPropertyString("bluray-device", file);
                    CommandV("loadfile", @"bd://");
                }
                else
                {
                    if (i == 0 && !append)
                        CommandV("loadfile", file);
                    else
                        CommandV("loadfile", file, "append");
                }
            }

            if (string.IsNullOrEmpty(GetPropertyString("path")))
                SetPropertyInt("playlist-pos", 0);
        }

        public string ConvertFilePath(string path)
        {
            if ((path.Contains(":/") && !path.Contains("://")) || (path.Contains(":\\") && path.Contains("/")))
                path = path.Replace("/", "\\");

            if (!path.Contains(":") && !path.StartsWith("\\\\") && File.Exists(path))
                path = System.IO.Path.GetFullPath(path);

            return path;
        }

        public void LoadISO(string path)
        {
            long gb = new FileInfo(path).Length / 1024 / 1024 / 1024;

            if (gb < 10)
            {
                System.Windows.MessageBoxResult result =
                    Msg.ShowQuestion("Click Yes for Blu-ray and No for DVD.",
                    System.Windows.MessageBoxButton.YesNoCancel);

                switch (result)
                {
                    case System.Windows.MessageBoxResult.Yes:
                        Command("stop");
                        Thread.Sleep(500);
                        SetPropertyString("bluray-device", path);
                        LoadFiles(new[] { @"bd://" }, false, false);
                        break;
                    case System.Windows.MessageBoxResult.No:
                        Command("stop");
                        Thread.Sleep(500);
                        SetPropertyString("dvd-device", path);
                        LoadFiles(new[] { @"dvd://" }, false, false);
                        break;
                }
            }
            else
            {
                Command("stop");
                Thread.Sleep(500);
                SetPropertyString("bluray-device", path);
                LoadFiles(new[] { @"bd://" }, false, false);
            }
        }

        public void LoadDiskFolder(string path)
        {
            Core.Command("stop");
            Thread.Sleep(500);

            if (Directory.Exists(path + "\\BDMV"))
            {
                Core.SetPropertyString("bluray-device", path);
                Core.LoadFiles(new[] { @"bd://" }, false, false);
            }
            else
            {
                Core.SetPropertyString("dvd-device", path);
                Core.LoadFiles(new[] { @"dvd://" }, false, false);
            }
        }

        static object LoadFolderLockObject = new object();

        public void LoadFolder()
        {
            if (!App.AutoLoadFolder || Control.ModifierKeys.HasFlag(Keys.Shift))
                return;

            Thread.Sleep(1000);

            lock (LoadFolderLockObject)
            {
                string path = GetPropertyString("path");

                if (!File.Exists(path) || GetPropertyInt("playlist-count") != 1)
                    return;

                string dir = Environment.CurrentDirectory;

                if (path.Contains(":/") && !path.Contains("://"))
                    path = path.Replace("/", "\\");

                if (path.Contains("\\"))
                    dir = System.IO.Path.GetDirectoryName(path);

                List<string> files = GetMediaFiles(Directory.GetFiles(dir)).ToList();
                files.Sort(new StringLogicalComparer());
                int index = files.IndexOf(path);
                files.Remove(path);

                foreach (string i in files)
                    CommandV("loadfile", i, "append");

                if (index > 0)
                    CommandV("playlist-move", "0", (index + 1).ToString());
            }
        }

        IEnumerable<string> GetMediaFiles(IEnumerable<string> files) => files.Where(i => IsMediaExtension(i.Ext()));

        bool IsMediaExtension(string ext)
        {
            return VideoTypes.Contains(ext) || AudioTypes.Contains(ext) || ImageTypes.Contains(ext);
        }

        bool WasAviSynthLoaded;

        void LoadAviSynth()
        {
            if (!WasAviSynthLoaded)
            {
                string dll = Environment.GetEnvironmentVariable("AviSynthDLL");

                if (File.Exists(dll))
                    Native.LoadLibrary(dll);
                else
                    Native.LoadLibrary("AviSynth.dll");

                WasAviSynthLoaded = true;
            }
        }

    }
}
