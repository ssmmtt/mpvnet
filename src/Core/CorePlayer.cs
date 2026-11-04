
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
        public static string[] VideoTypes { get; set; } = "mkv mp4 avi mov flv mpg webm wmv ts vob 264 265 asf avc avs dav h264 h265 hevc m2t m2ts m2v m4v mpeg mpv mts vpy y4m".Split(' ');
        public static string[] AudioTypes { get; set; } = "mp3 flac m4a mka mp2 ogg opus aac ac3 dts dtshd dtshr dtsma eac3 mpa mpc thd w64 wav".Split(' ');
        public static string[] ImageTypes { get; set; } = { "jpg", "bmp", "png", "gif", "webp" };
        public static string[] SubtitleTypes { get; } = { "srt", "ass", "idx", "sub", "sup", "ttxt", "txt", "ssa", "smi", "mks" };

        public event Action<mpv_log_level, string> LogMessageAsync; // log-message        MPV_EVENT_LOG_MESSAGE
        public event Action<mpv_end_file_reason> EndFileAsync;      // end-file           MPV_EVENT_END_FILE
        public event Action<string[]> ClientMessageAsync;           // client-message     MPV_EVENT_CLIENT_MESSAGE
        public event Action GetPropertyReplyAsync;                  // get-property-reply MPV_EVENT_GET_PROPERTY_REPLY
        public event Action SetPropertyReplyAsync;                  // set-property-reply MPV_EVENT_SET_PROPERTY_REPLY
        public event Action CommandReplyAsync;                      // command-reply      MPV_EVENT_COMMAND_REPLY
        public event Action StartFileAsync;                         // start-file         MPV_EVENT_START_FILE
        public event Action FileLoadedAsync;                        // file-loaded        MPV_EVENT_FILE_LOADED
        public event Action VideoReconfigAsync;                     // video-reconfig     MPV_EVENT_VIDEO_RECONFIG
        public event Action AudioReconfigAsync;                     // audio-reconfig     MPV_EVENT_AUDIO_RECONFIG
        public event Action SeekAsync;                              // seek               MPV_EVENT_SEEK
        public event Action PlaybackRestartAsync;                   // playback-restart   MPV_EVENT_PLAYBACK_RESTART

        public event Action<mpv_log_level, string>LogMessage; // log-message        MPV_EVENT_LOG_MESSAGE
        public event Action<mpv_end_file_reason> EndFile;     // end-file           MPV_EVENT_END_FILE
        public event Action<string[]> ClientMessage;          // client-message     MPV_EVENT_CLIENT_MESSAGE
        public event Action Shutdown;                         // shutdown           MPV_EVENT_SHUTDOWN
        public event Action GetPropertyReply;                 // get-property-reply MPV_EVENT_GET_PROPERTY_REPLY
        public event Action SetPropertyReply;                 // set-property-reply MPV_EVENT_SET_PROPERTY_REPLY
        public event Action CommandReply;                     // command-reply      MPV_EVENT_COMMAND_REPLY
        public event Action StartFile;                        // start-file         MPV_EVENT_START_FILE
        public event Action FileLoaded;                       // file-loaded        MPV_EVENT_FILE_LOADED
        public event Action VideoReconfig;                    // video-reconfig     MPV_EVENT_VIDEO_RECONFIG
        public event Action AudioReconfig;                    // audio-reconfig     MPV_EVENT_AUDIO_RECONFIG
        public event Action Seek;                             // seek               MPV_EVENT_SEEK
        public event Action PlaybackRestart;                  // playback-restart   MPV_EVENT_PLAYBACK_RESTART

        public event Action Initialized;
        public event Action InitializedAsync;
        public event Action Pause;
        public event Action ShowMenu;
        public event Action<double> WindowScaleMpv;
        public event Action<float> ScaleWindow;
        public event Action<float> WindowScaleNET;
        public event Action<int> PlaylistPosChanged;
        public event Action<int> PlaylistPosChangedAsync;
        public event Action<Size> VideoSizeChanged;
        public event Action<Size> VideoSizeChangedAsync;
        public event Action<string> MoveWindow;

        public Dictionary<string, List<Action>>               PropChangeActions { get; set; } = new Dictionary<string, List<Action>>();
        public Dictionary<string, List<Action<int>>>       IntPropChangeActions { get; set; } = new Dictionary<string, List<Action<int>>>();
        public Dictionary<string, List<Action<bool>>>     BoolPropChangeActions { get; set; } = new Dictionary<string, List<Action<bool>>>();
        public Dictionary<string, List<Action<double>>> DoublePropChangeActions { get; set; } = new Dictionary<string, List<Action<double>>>();
        public Dictionary<string, List<Action<string>>> StringPropChangeActions { get; set; } = new Dictionary<string, List<Action<string>>>();

        public AutoResetEvent ShutdownAutoResetEvent { get; } = new AutoResetEvent(false);
        public AutoResetEvent VideoSizeAutoResetEvent { get; } = new AutoResetEvent(false);
        public IntPtr Handle { get; set; }
        public IntPtr NamedHandle { get; set; }
        public List<MediaTrack> MediaTracks { get; set; } = new List<MediaTrack>();
        public List<TimeSpan> BluRayTitles { get; } = new List<TimeSpan>();
        public object MediaTracksLock { get; } = new object();
        public Size VideoSize { get; set; }
        public TimeSpan Duration;

        public string ConfPath { get => ConfigFolder + "mpv.conf"; }
        public string GPUAPI { get; set; } = "auto";
        public string InputConfPath => ConfigFolder + "input.conf";
        public string Path { get; set; } = "";
        public string VO { get; set; } = "gpu-next,gpu";

        public string VID { get; set; } = "";
        public string AID { get; set; } = "";
        public string SID { get; set; } = "";

        public bool Border { get; set; } = true;
        public bool FileEnded { get; set; }
        public bool Fullscreen { get; set; }
        public bool IsQuitNeeded { set; get; } = true;
        public bool KeepaspectWindow { get; set; }
        public bool Paused { get; set; }
        public bool Shown { get; set; }
        public bool SnapWindow { get; set; }
        public bool TaskbarProgress { get; set; } = true;
        public bool WasInitialSizeSet;
        public bool WindowMaximized { get; set; }
        public bool WindowMinimized { get; set; }

        public int Edition { get; set; }
        public int PlaylistPos { get; set; } = -1;
        public int Screen { get; set; } = -1;
        public int VideoRotate { get; set; }

        public float Autofit { get; set; } = 0.6f;
        public float AutofitSmaller { get; set; } = 0.3f;
        public float AutofitLarger { get; set; } = 0.8f;

        public void Init(IntPtr handle)
        {
            ApplyShowMenuFix();

            Handle = mpv_create();

            var events = Enum.GetValues(typeof(mpv_event_id)).Cast<mpv_event_id>();

            foreach (mpv_event_id i in events)
                mpv_request_event(Handle, i, 0);

            mpv_request_log_messages(Handle, "no");

            App.RunTask(() => MainEventLoop());

            if (Handle == IntPtr.Zero)
                throw new Exception("error mpv_create");

            if (App.IsTerminalAttached)
            {
                SetPropertyString("terminal", "yes");
                SetPropertyString("input-terminal", "yes");
            }

            SetPropertyInt("osd-duration", 2000);
            SetPropertyLong("wid", handle.ToInt64());

            SetPropertyBool("input-default-bindings", true);
            SetPropertyBool("input-builtin-bindings", false);

            // Per-file resume: position + audio/subtitle. Volume/mute are global (remember-volume).
            SetPropertyString("save-position-on-quit", "yes");
            SetPropertyString("watch-later-options", "start,aid,sid");
            SetPropertyString("screenshot-directory", "~~desktop/");
            SetPropertyString("osd-playing-msg", "${media-title}");
            SetPropertyString("osc", "yes");
            SetPropertyString("force-window", "yes");
            SetPropertyString("config-dir", ConfigFolder);
            SetPropertyString("config", "yes");

            ProcessCommandLine(true);

            Environment.SetEnvironmentVariable("MPVNET_VERSION", Application.ProductVersion);

            mpv_error err = mpv_initialize(Handle);

            if (err < 0)
                throw new Exception("mpv_initialize error" + BR2 + GetError(err) + BR);

            string idle = GetPropertyString("idle");
            App.Exit = idle == "no" || idle == "once";

            NamedHandle = mpv_create_client(Handle, "mpvnet");

            if (NamedHandle == IntPtr.Zero)
                throw new Exception("mpv_create_client error");

            mpv_request_log_messages(NamedHandle, "terminal-default");

            App.RunTask(() => EventLoop());

            // otherwise shutdown is raised before media files are loaded,
            // this means Lua scripts that use idle might not work correctly
            SetPropertyString("idle", "yes");

            ObservePropertyDouble("window-scale", value => WindowScaleMpv(value));
          
            ObservePropertyString("path", value => Path = value);

            ObservePropertyBool("pause", value => {
                Paused = value;
                Pause();
            });

            ObservePropertyInt("video-rotate", value => {
                VideoRotate = value;
                UpdateVideoSize("dwidth", "dheight");
            });

            ObservePropertyInt("playlist-pos", value => {
                PlaylistPos = value;
                InvokeEvent(PlaylistPosChanged, PlaylistPosChangedAsync, value);

                if (MainForm.Instance != null && MainForm.Instance.IsClosing)
                    return;

                if (value == -1 && Core.Shown)
                    ShowLogo();
                
                if (value != -1)
                    HideLogo();

                if (FileEnded && value == -1)
                {
                    if (GetPropertyString("keep-open") == "no" && App.Exit)
                        Core.CommandV("quit");
                }
            });

            if (!GetPropertyBool("osd-scale-by-window"))
                App.StartThreshold = 0;

            Initialized?.Invoke();
            InvokeAsync(InitializedAsync);
        }

        public void Destroy()
        {
            mpv_destroy(Handle);
            mpv_destroy(NamedHandle);
        }

        void ApplyShowMenuFix()
        {
            if (App.Settings.ShowMenuFixApplied)
                return;

            if (File.Exists(InputConfPath))
            {
                string content = File.ReadAllText(InputConfPath);

                if (!content.Contains("script-message mpv.net show-menu") &&
                    !content.Contains("script-message-to mpvnet show-menu"))

                    File.WriteAllText(InputConfPath, BR + content.Trim() + BR +
                        "MBTN_Right script-message-to mpvnet show-menu" + BR);
            }

            App.Settings.ShowMenuFixApplied = true;
        }

        void ApplyInputDefaultBindingsFix()
        {
            if (App.Settings.InputDefaultBindingsFixApplied)
                return;

            if (File.Exists(ConfPath))
            {
                string content = File.ReadAllText(ConfPath);

                if (content.Contains("input-default-bindings = no"))
                    File.WriteAllText(ConfPath, content.Replace("input-default-bindings = no", ""));

                if (content.Contains("input-default-bindings=no"))
                    File.WriteAllText(ConfPath, content.Replace("input-default-bindings=no", ""));
            }

            App.Settings.InputDefaultBindingsFixApplied = true;
        }

        public void ProcessProperty(string name, string value)
        {
            switch (name)
            {
                case "autofit":
                    {
                        if (int.TryParse(value.Trim('%'), out int result))
                            Autofit = result / 100f;
                    }
                    break;
                case "autofit-smaller":
                    {
                        if (int.TryParse(value.Trim('%'), out int result))
                            AutofitSmaller = result / 100f;
                    }
                    break;
                case "autofit-larger":
                    {
                        if (int.TryParse(value.Trim('%'), out int result))
                            AutofitLarger = result / 100f;
                    }
                    break;
                case "border": Border = value == "yes"; break;
                case "fs":
                case "fullscreen": Fullscreen = value == "yes"; break;
                case "gpu-api": GPUAPI = value; break;
                case "keepaspect-window": KeepaspectWindow = value == "yes"; break;
                case "screen": Screen = Convert.ToInt32(value); break;
                case "snap-window": SnapWindow = value == "yes"; break;
                case "taskbar-progress": TaskbarProgress = value == "yes"; break;
                case "vo": VO = value; break;
                case "window-maximized": WindowMaximized = value == "yes"; break;
                case "window-minimized": WindowMinimized = value == "yes"; break;
            }

            if (AutofitLarger > 1)
                AutofitLarger = 1;
        }

        bool? _UseNewMsgModel;

        public bool UseNewMsgModel {
            get {
                if (!_UseNewMsgModel.HasValue)
                    _UseNewMsgModel = InputConfContent.Contains("script-message-to mpvnet");
                return _UseNewMsgModel.Value;
            }
        }

        string _InputConfContent;

        public string InputConfContent {
            get {
                if (_InputConfContent == null)
                    _InputConfContent = File.ReadAllText(Core.InputConfPath);
                return _InputConfContent;
            }
        }

        string _ConfigFolder;

        public string ConfigFolder {
            get {
                if (_ConfigFolder == null)
                {
                    _ConfigFolder = Folder.Startup + "portable_config";

                    if (!Directory.Exists(_ConfigFolder))
                        _ConfigFolder = Folder.AppData + "mpv.net";

                    if (!Directory.Exists(_ConfigFolder))
                    {
                        try {
                            using (Process proc = new Process())
                            {
                                proc.StartInfo.UseShellExecute = false;
                                proc.StartInfo.CreateNoWindow = true;
                                proc.StartInfo.FileName = "powershell.exe";
                                proc.StartInfo.Arguments = $@"-Command New-Item -Path '{_ConfigFolder}' -ItemType Directory";
                                proc.Start();
                                proc.WaitForExit();
                            }
                        } catch (Exception) {}

                        if (!Directory.Exists(_ConfigFolder))
                            Directory.CreateDirectory(_ConfigFolder);
                    }

                    _ConfigFolder = _ConfigFolder.AddSep();

                    if (!File.Exists(_ConfigFolder + "input.conf"))
                    {
                        File.WriteAllText(_ConfigFolder + "input.conf", Properties.Resources.input_conf);

                        if (!File.Exists(_ConfigFolder + "mpv.conf"))
                            File.WriteAllText(_ConfigFolder + "mpv.conf", Properties.Resources.mpv_conf);

                        string scriptOptsPath = _ConfigFolder + "script-opts" + System.IO.Path.DirectorySeparatorChar;

                        if (!Directory.Exists(scriptOptsPath))
                        {
                            Directory.CreateDirectory(scriptOptsPath);
                            File.WriteAllText(scriptOptsPath + "console.conf", BR + "scale=1.5" + BR);
                            string content = BR + "deadzonesize=1.0" + BR + "idlescreen=no" + BR;
                            File.WriteAllText(scriptOptsPath + "osc.conf", content);
                        }
                    }
                }

                return _ConfigFolder;
            }
        }

        Dictionary<string, string> _Conf;

        public Dictionary<string, string> Conf {
            get {
                if (_Conf == null)
                {
                    ApplyInputDefaultBindingsFix();

                    _Conf = new Dictionary<string, string>();

                    if (File.Exists(ConfPath))
                        foreach (var i in File.ReadAllLines(ConfPath))
                            if (i.Contains("=") && !i.TrimStart().StartsWith("#"))
                            {
                                string key = i.Substring(0, i.IndexOf("=")).Trim();
                                string value = i.Substring(i.IndexOf("=") + 1).Trim();

                                if (key.StartsWith("-"))
                                    key = key.TrimStart('-');

                                if (value.Contains("#") && !value.StartsWith("#") &&
                                    !value.StartsWith("'#") && !value.StartsWith("\"#"))

                                    value = value.Substring(0, value.IndexOf("#")).Trim();

                                _Conf[key] = value;
                            }

                    foreach (var i in _Conf)
                        ProcessProperty(i.Key, i.Value);
                }

                return _Conf;
            }
        }

        public void LoadScripts()
        {
            if (Directory.Exists(ConfigFolder + "scripts-ps"))
                foreach (string file in Directory.GetFiles(ConfigFolder + "scripts-ps", "*.ps1"))
                    App.RunTask(() => InvokePowerShellScript(file));
        }

        public void InvokePowerShellScript(string file)
        {
            PowerShell ps = new PowerShell();
            ps.Variables.Add(new KeyValuePair<string, object>("core", Core));
            ps.Variables.Add(new KeyValuePair<string, object>("window", MainForm.Instance));
            ps.Scripts.Add("Using namespace mpvnet; [Reflection.Assembly]::LoadWithPartialName('mpvnet')" + BR);

            string eventCode = @"
                $eventJob = Register-ObjectEvent -InputObject $mp -EventName Event -Action {
                    foreach ($pair in $mp.EventHandlers)
                    {
                        if ($pair.Key -eq $args[0])
                        {
                            if ($args.Length -gt 1)
                            {
                                $args2 = $args[1]
                            }

                            Invoke-Command -ScriptBlock $pair.Value -ArgumentList $args2
                        }
                    }
                }

                $mp.RedirectStreams($eventJob)
            ";

            string propertyChangedCode = @"
                $propertyChangedJob = Register-ObjectEvent -InputObject $mp -EventName PropertyChanged -Action {
                    foreach ($pair in $mp.PropChangedHandlers)
                    {
                        if ($pair.Key -eq $args[0])
                        {
                            if ($args.Length -gt 1)
                            {
                                $args2 = $args[1]
                            }

                            Invoke-Command -ScriptBlock $pair.Value -ArgumentList $args2
                        }
                    }
                }

                $mp.RedirectStreams($propertyChangedJob)
            ";

            ps.Scripts.Add(eventCode);
            ps.Scripts.Add(propertyChangedCode);
            ps.Scripts.Add(File.ReadAllText(file));
            ps.Module = System.IO.Path.GetFileName(file);
            ps.Print = true;

            lock (PowerShell.References)
                PowerShell.References.Add(ps);

            ps.Invoke();
        }

        void UpdateVideoSize(string w, string h)
        {
            Size size = new Size(GetPropertyInt(w), GetPropertyInt(h));

            if (size.Width == 0 || size.Height == 0)
                return;

            if (VideoRotate == 90 || VideoRotate == 270)
                size = new Size(size.Height, size.Width);

            if (VideoSize != size)
            {
                VideoSize = size;
                InvokeEvent(VideoSizeChanged, VideoSizeChangedAsync, size);
                VideoSizeAutoResetEvent.Set();
            }
        }

        public void Command(string command)
        {
            mpv_error err = mpv_command_string(Handle, command);
            if (err < 0)
                HandleError(err, "error executing command: " + command);
        }

        public void CommandV(params string[] args)
        {
            int count = args.Length + 1;
            IntPtr[] pointers = new IntPtr[count];
            IntPtr rootPtr = Marshal.AllocHGlobal(IntPtr.Size * count);

            for (int index = 0; index < args.Length; index++)
            {
                var bytes = GetUtf8Bytes(args[index]);
                IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
                pointers[index] = ptr;
            }

            Marshal.Copy(pointers, 0, rootPtr, count);
            mpv_error err = mpv_command(Handle, rootPtr);

            foreach (IntPtr ptr in pointers)
                Marshal.FreeHGlobal(ptr);

            Marshal.FreeHGlobal(rootPtr);
            if (err < 0)
                HandleError(err, "error executing command: " + string.Join("\n", args));
        }

        public string Expand(string value)
        {
            if (value == null)
                return "";

            if (!value.Contains("${"))
                return value;

            string[] args = { "expand-text", value };
            int count = args.Length + 1;
            IntPtr[] pointers = new IntPtr[count];
            IntPtr rootPtr = Marshal.AllocHGlobal(IntPtr.Size * count);

            for (int index = 0; index < args.Length; index++)
            {
                var bytes = GetUtf8Bytes(args[index]);
                IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
                pointers[index] = ptr;
            }

            Marshal.Copy(pointers, 0, rootPtr, count);
            IntPtr resultNodePtr = Marshal.AllocHGlobal(16);
            mpv_error err = mpv_command_ret(Handle, rootPtr, resultNodePtr);

            foreach (IntPtr ptr in pointers)
                Marshal.FreeHGlobal(ptr);

            Marshal.FreeHGlobal(rootPtr);

            if (err < 0)
            {
                HandleError(err, "error executing command: " + string.Join("\n", args));
                Marshal.FreeHGlobal(resultNodePtr);
                return "property expansion error";
            }

            mpv_node resultNode = Marshal.PtrToStructure<mpv_node>(resultNodePtr);
            string ret = ConvertFromUtf8(resultNode.str);
            mpv_free_node_contents(resultNodePtr);
            Marshal.FreeHGlobal(resultNodePtr);
            return ret;
        }

        public bool GetPropertyBool(string name)
        {
            mpv_error err = mpv_get_property(Handle, GetUtf8Bytes(name),
                mpv_format.MPV_FORMAT_FLAG, out IntPtr lpBuffer);
            if (err < 0)
                HandleError(err, "error getting property: " + name);
            return lpBuffer.ToInt32() != 0;
        }

        public void SetPropertyBool(string name, bool value)
        {
            long val = value ? 1 : 0;
            mpv_error err = mpv_set_property(Handle, GetUtf8Bytes(name), mpv_format.MPV_FORMAT_FLAG, ref val);
            if (err < 0)
                HandleError(err, $"error setting property: {name} = {value}");
        }

        public int GetPropertyInt(string name)
        {
            mpv_error err = mpv_get_property(Handle, GetUtf8Bytes(name),
                mpv_format.MPV_FORMAT_INT64, out IntPtr lpBuffer);
            if (err < 0 && App.DebugMode)
                HandleError(err, "error getting property: " + name);
            return lpBuffer.ToInt32();
        }

        public void SetPropertyInt(string name, int value)
        {
            long val = value;
            mpv_error err = mpv_set_property(Handle, GetUtf8Bytes(name), mpv_format.MPV_FORMAT_INT64, ref val);
            if (err < 0)
                HandleError(err, $"error setting property: {name} = {value}");
        }

        public void SetPropertyLong(string name, long value)
        {
            mpv_error err = mpv_set_property(Handle, GetUtf8Bytes(name), mpv_format.MPV_FORMAT_INT64, ref value);
            if (err < 0)
                HandleError(err, $"error setting property: {name} = {value}");
        }

        public long GetPropertyLong(string name)
        {
            mpv_error err = mpv_get_property(Handle, GetUtf8Bytes(name),
                mpv_format.MPV_FORMAT_INT64, out IntPtr lpBuffer);
            if (err < 0)
                HandleError(err, "error getting property: " + name);
            return lpBuffer.ToInt64();
        }

        public double GetPropertyDouble(string name, bool handleError = true)
        {
            mpv_error err = mpv_get_property(Handle, GetUtf8Bytes(name),
                mpv_format.MPV_FORMAT_DOUBLE, out double value);
            if (err < 0 && handleError && App.DebugMode)
                HandleError(err, "error getting property: " + name);
            return value;
        }

        public void SetPropertyDouble(string name, double value)
        {
            double val = value;
            mpv_error err = mpv_set_property(Handle, GetUtf8Bytes(name), mpv_format.MPV_FORMAT_DOUBLE, ref val);
            if (err < 0)
                HandleError(err, $"error setting property: {name} = {value}");
        }

        public string GetPropertyString(string name)
        {
            mpv_error err = mpv_get_property(Handle, GetUtf8Bytes(name),
                mpv_format.MPV_FORMAT_STRING, out IntPtr lpBuffer);

            if (err == 0)
            {
                string ret = ConvertFromUtf8(lpBuffer);
                mpv_free(lpBuffer);
                return ret;
            }

            if (err < 0 && App.DebugMode)
                HandleError(err, "error getting property: " + name);

            return "";
        }

        public void SetPropertyString(string name, string value)
        {
            byte[] bytes = GetUtf8Bytes(value);
            mpv_error err = mpv_set_property(Handle, GetUtf8Bytes(name), mpv_format.MPV_FORMAT_STRING, ref bytes);
            if (err < 0)
                HandleError(err, $"error setting property: {name} = {value}");
        }

        public string GetPropertyOsdString(string name)
        {
            mpv_error err = mpv_get_property(Handle, GetUtf8Bytes(name),
                mpv_format.MPV_FORMAT_OSD_STRING, out IntPtr lpBuffer);

            if (err == 0)
            {
                string ret = ConvertFromUtf8(lpBuffer);
                mpv_free(lpBuffer);
                return ret;
            }

            if (err < 0)
                HandleError(err, "error getting property: " + name);

            return "";
        }

        public void ObservePropertyInt(string name, Action<int> action)
        {
            lock (IntPropChangeActions)
            {
                if (!IntPropChangeActions.ContainsKey(name))
                {
                    mpv_error err = mpv_observe_property(NamedHandle, 0, name, mpv_format.MPV_FORMAT_INT64);

                    if (err < 0)
                        HandleError(err, "error observing property: " + name);
                    else
                        IntPropChangeActions[name] = new List<Action<int>>();
                }

                if (IntPropChangeActions.ContainsKey(name))
                    IntPropChangeActions[name].Add(action);
            }
        }

        public void ObservePropertyDouble(string name, Action<double> action)
        {
            lock (DoublePropChangeActions)
            {
                if (!DoublePropChangeActions.ContainsKey(name))
                {
                    mpv_error err = mpv_observe_property(NamedHandle, 0, name, mpv_format.MPV_FORMAT_DOUBLE);

                    if (err < 0)
                        HandleError(err, "error observing property: " + name);
                    else
                        DoublePropChangeActions[name] = new List<Action<double>>();
                }

                if (DoublePropChangeActions.ContainsKey(name))
                    DoublePropChangeActions[name].Add(action);
            }
        }

        public void ObservePropertyBool(string name, Action<bool> action)
        {
            lock (BoolPropChangeActions)
            {
                if (!BoolPropChangeActions.ContainsKey(name))
                {
                    mpv_error err = mpv_observe_property(NamedHandle, 0, name, mpv_format.MPV_FORMAT_FLAG);

                    if (err < 0)
                        HandleError(err, "error observing property: " + name);
                    else
                        BoolPropChangeActions[name] = new List<Action<bool>>();
                }

                if (BoolPropChangeActions.ContainsKey(name))
                    BoolPropChangeActions[name].Add(action);
            }
        }

        public void ObservePropertyString(string name, Action<string> action)
        {
            lock (StringPropChangeActions)
            {
                if (!StringPropChangeActions.ContainsKey(name))
                {
                    mpv_error err = mpv_observe_property(NamedHandle, 0, name, mpv_format.MPV_FORMAT_STRING);

                    if (err < 0)
                        HandleError(err, "error observing property: " + name);
                    else
                        StringPropChangeActions[name] = new List<Action<string>>();
                }

                if (StringPropChangeActions.ContainsKey(name))
                    StringPropChangeActions[name].Add(action);
            }
        }

        public void ObserveProperty(string name, Action action)
        {
            lock (PropChangeActions)
            {
                if (!PropChangeActions.ContainsKey(name))
                {
                    mpv_error err = mpv_observe_property(NamedHandle, 0, name, mpv_format.MPV_FORMAT_NONE);

                    if (err < 0)
                        HandleError(err, "error observing property: " + name);
                    else
                        PropChangeActions[name] = new List<Action>();
                }

                if (PropChangeActions.ContainsKey(name))
                    PropChangeActions[name].Add(action);
            }
        }

        public void HandleError(mpv_error err, string msg)
        {
            Terminal.WriteError(msg);
            Terminal.WriteError(GetError(err));
        }

        public void ShowLogo()
        {
            if (!App.ShowLogo || MainForm.Instance == null || MainForm.Instance.IsClosing ||
                !Core.Shown || Core.Handle == IntPtr.Zero)
                return;

            bool december = DateTime.Now.Month == 12;
            Rectangle cr = MainForm.Instance.ClientRectangle;
            int len = Convert.ToInt32(cr.Height / (december ? 4.5 : 5));

            if (len < 16 || cr.Height < 16)
                return;

            using (Bitmap bmp = new Bitmap(len, len))
            {
                using (Graphics gx = Graphics.FromImage(bmp))
                {
                    gx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    gx.Clear(Color.Black);
                    Rectangle rect = new Rectangle(0, 0, len, len);
                    Bitmap bmp2 = (december && App.ShowSantaLogo) ? Properties.Resources.mpvnet_santa : Properties.Resources.mpvnet;
                    gx.DrawImage(bmp2, rect);
                    BitmapData bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                    int x = Convert.ToInt32((cr.Width - len) / (december ? 1.95 : 2));
                    int y = Convert.ToInt32((cr.Height - len) / 2.0 * (december ? 0.85 : 0.9));
                    CommandV("overlay-add", "0", $"{x}", $"{y}", "&" + bd.Scan0.ToInt64().ToString(), "0", "bgra", bd.Width.ToString(), bd.Height.ToString(), bd.Stride.ToString());
                    bmp.UnlockBits(bd);
                }
            }
        }

        void HideLogo() => Command("overlay-remove 0");

        // Auto-prefer only once per path per process (manual choice wins until quit saves watch-later).
        readonly HashSet<string> AutoSubPreferTried =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // mpv.conf 字幕语言优先级（slang）：按列表顺序依次匹配 lang/title。
        void PreferSubtitleBySlang()
        {
            try
            {
                string path = GetPropertyString("path");
                if (string.IsNullOrEmpty(path))
                    return;

                if (!AutoSubPreferTried.Add(path))
                    return;

                if (SubtitleTrackUi.WatchLaterHasSid(ConfigFolder, path))
                    return;

                string raw;
                if (!Conf.TryGetValue("slang", out raw) || string.IsNullOrWhiteSpace(raw))
                    return;

                string[] prefs = SubtitleTrackUi.ParsePreferenceList(raw);
                if (prefs.Length == 0)
                    return;

                int count = GetPropertyInt("track-list/count");
                var subs = new List<Tuple<int, string, string>>();
                for (int i = 0; i < count; i++)
                {
                    if (GetPropertyString($"track-list/{i}/type") != "sub")
                        continue;

                    int id = GetPropertyInt($"track-list/{i}/id");
                    string lang = GetPropertyString($"track-list/{i}/lang") ?? "";
                    string title = GetPropertyString($"track-list/{i}/title") ?? "";
                    subs.Add(Tuple.Create(id, lang, title));
                }

                int bestId = SubtitleTrackUi.FindSidByPreferenceOrder(prefs, subs);
                if (bestId > 0)
                    SetPropertyInt("sid", bestId);
            }
            catch { }
        }

        public bool IsImage => ImageTypes.Contains(Path.Ext());
        
        public bool IsAudio => AudioTypes.Contains(Path.Ext());

        string GetLanguage(string id) => SubtitleTrackUi.LocalizeLanguageLabel(id);

        string GetNativeLanguage(string name) => SubtitleTrackUi.LocalizeLanguageLabel(name);

        string LocalizeLanguageLabel(string raw) => SubtitleTrackUi.LocalizeLanguageLabel(raw);

        public static string GetShortcutTarget(string path)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            dynamic sh = Activator.CreateInstance(t);
            return sh.CreateShortcut(path).TargetPath;
        }

        public void RaiseScaleWindow(float value) => ScaleWindow(value);

        public void RaiseMoveWindow(string value) => MoveWindow(value);
        
        public void RaiseWindowScaleNET(float value) => WindowScaleNET(value);
        
        public void RaiseShowMenu() => ShowMenu();

        // ponytail: MediaInfo/File.Exists on WebDAV/mapped drives can stall for minutes.
        // Never hold MediaTracksLock during that I/O — UpdateMenu runs on the UI thread and
        // waits on the same lock, which freezes the context menu and Close.
        public static bool IsRemotePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            if (path.Contains("://") || path.Contains(@"\\.\pipe\"))
                return true;

            if (path.StartsWith(@"\\"))
                return true;

            try
            {
                string root = System.IO.Path.GetPathRoot(path);

                if (string.IsNullOrEmpty(root) || root.Length < 2 || root[1] != ':')
                    return false;

                // GetDriveType uses the local mount table; does not contact the server.
                DriveType type = new DriveInfo(root).DriveType;
                return type == DriveType.Network || type == DriveType.NoRootDirectory;
            }
            catch
            {
                return true;
            }
        }

        private string[] _ProfileNames;

        public string[] ProfileNames
        {
            get
            {
                if (_ProfileNames == null)
                {
                    string[] ignore = { "builtin-pseudo-gui", "encoding", "libmpv", "pseudo-gui", "default" };
                    string profileList = Core.GetPropertyString("profile-list");
                    var json = profileList.FromJson<List<Dictionary<string, object>>>();
                    _ProfileNames = json.Select(i => i["name"].ToString())
                                        .Where(i => !ignore.Contains(i)).ToArray();
                }

                return _ProfileNames;
            }
        }
    }
}
