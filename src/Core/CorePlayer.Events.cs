
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
        public void MainEventLoop()
        {
            while (true)
                mpv_wait_event(Handle, -1);
        }

        public void EventLoop()
        {
            while (true)
            {
                IntPtr ptr = mpv_wait_event(NamedHandle, -1);
                mpv_event evt = (mpv_event)Marshal.PtrToStructure(ptr, typeof(mpv_event));

                try
                {
                    switch (evt.event_id)
                    {
                        case mpv_event_id.MPV_EVENT_SHUTDOWN:
                            IsQuitNeeded = false;
                            Shutdown?.Invoke();
                            ShutdownAutoResetEvent.Set();
                            return;
                        case mpv_event_id.MPV_EVENT_LOG_MESSAGE:
                            {
                                var data = (mpv_event_log_message)Marshal.PtrToStructure(evt.data, typeof(mpv_event_log_message));

                                if (data.log_level == mpv_log_level.MPV_LOG_LEVEL_INFO)
                                {
                                    string prefix = ConvertFromUtf8(data.prefix);

                                    if (prefix == "bd")
                                        ProcessBluRayLogMessage(ConvertFromUtf8(data.text));
                                }

                                if (LogMessage != null || LogMessageAsync != null)
                                {
                                    string msg = $"[{ConvertFromUtf8(data.prefix)}] {ConvertFromUtf8(data.text)}";
                                    InvokeAsync(LogMessageAsync, data.log_level, msg);
                                    LogMessage?.Invoke(data.log_level, msg);
                                }
                            }
                            break;
                        case mpv_event_id.MPV_EVENT_CLIENT_MESSAGE:
                            {
                                var data = (mpv_event_client_message)Marshal.PtrToStructure(evt.data, typeof(mpv_event_client_message));
                                string[] args = ConvertFromUtf8Strings(data.args, data.num_args);

                                if (UseNewMsgModel && args[0] != "mpv.net")
                                    App.RunTask(() => Commands.Execute(args[0], args.Skip(1).ToArray()));
                                else if (args.Length > 1 && args[0] == "mpv.net")
                                    App.RunTask(() => Commands.Execute(args[1], args.Skip(2).ToArray()));

                                if (args.Length > 1 && args[0] == "osc-idlescreen")
                                {
                                    if (args[1] == "no")
                                        HideLogo();
                                    else if (args[1] == "yes" && PlaylistPos == -1)
                                        ShowLogo();
                                }

                                InvokeAsync(ClientMessageAsync, args);
                                ClientMessage?.Invoke(args);
                            }
                            break;
                        case mpv_event_id.MPV_EVENT_VIDEO_RECONFIG:
                            UpdateVideoSize("dwidth", "dheight");
                            InvokeEvent(VideoReconfig, VideoReconfigAsync);
                            break;
                        case mpv_event_id.MPV_EVENT_END_FILE:
                            {
                                var data = (mpv_event_end_file)Marshal.PtrToStructure(evt.data, typeof(mpv_event_end_file));
                                var reason = (mpv_end_file_reason)data.reason;
                                InvokeAsync(EndFileAsync, reason);
                                EndFile?.Invoke(reason);
                                FileEnded = true;
                            }
                            break;
                        case mpv_event_id.MPV_EVENT_FILE_LOADED:
                            {
                                if (App.AutoPlay && Paused)
                                    SetPropertyBool("pause", false);

                                App.QuickBookmark = 0;

                                HideLogo();

                                Duration = TimeSpan.FromSeconds(GetPropertyDouble("duration"));

                                // Only for later files — resetting on the first load
                                // causes a visible large→autofit resize after reveal.
                                if (App.StartSize == "video" && Core.Shown)
                                    WasInitialSizeSet = false;

                                string path = GetPropertyString("path");

                                if (!VideoTypes.Contains(path.Ext()) || AudioTypes.Contains(path.Ext()))
                                {
                                    UpdateVideoSize("width", "height");
                                    VideoSizeAutoResetEvent.Set();
                                }

                                PreferSubtitleBySlang();
                                App.RunTask(new Action(() => UpdateTracks()));

                                InvokeEvent(FileLoaded, FileLoadedAsync);
                            }
                            break;
                        case mpv_event_id.MPV_EVENT_PROPERTY_CHANGE:
                            {
                                var data = (mpv_event_property)Marshal.PtrToStructure(evt.data, typeof(mpv_event_property));

                                if (data.format == mpv_format.MPV_FORMAT_FLAG)
                                {
                                    lock (BoolPropChangeActions)
                                        foreach (var pair in BoolPropChangeActions)
                                            if (pair.Key == data.name)
                                            {
                                                bool value = Marshal.PtrToStructure<int>(data.data) == 1;

                                                foreach (var action in pair.Value)
                                                    action.Invoke(value);
                                            }
                                }
                                else if (data.format == mpv_format.MPV_FORMAT_STRING)
                                {
                                    lock (StringPropChangeActions)
                                        foreach (var pair in StringPropChangeActions)
                                            if (pair.Key == data.name)
                                            {
                                                string value = ConvertFromUtf8(Marshal.PtrToStructure<IntPtr>(data.data));

                                                foreach (var action in pair.Value)
                                                    action.Invoke(value);
                                            }
                                }
                                else if (data.format == mpv_format.MPV_FORMAT_INT64)
                                {
                                    lock (IntPropChangeActions)
                                        foreach (var pair in IntPropChangeActions)
                                            if (pair.Key == data.name)
                                            {
                                                int value = Marshal.PtrToStructure<int>(data.data);

                                                foreach (var action in pair.Value)
                                                    action.Invoke(value);
                                            }
                                }
                                else if (data.format == mpv_format.MPV_FORMAT_NONE)
                                {
                                    lock (PropChangeActions)
                                        foreach (var pair in PropChangeActions)
                                            if (pair.Key == data.name)
                                                foreach (var action in pair.Value)
                                                    action.Invoke();
                                }
                                else if (data.format == mpv_format.MPV_FORMAT_DOUBLE)
                                {
                                    lock (DoublePropChangeActions)
                                        foreach (var pair in DoublePropChangeActions)
                                            if (pair.Key == data.name)
                                            {
                                                double value = Marshal.PtrToStructure<double>(data.data);

                                                foreach (var action in pair.Value)
                                                    action.Invoke(value);
                                            }
                                }
                            }
                            break;
                        case mpv_event_id.MPV_EVENT_GET_PROPERTY_REPLY:
                            InvokeEvent(GetPropertyReply, GetPropertyReplyAsync);
                            break;
                        case mpv_event_id.MPV_EVENT_SET_PROPERTY_REPLY:
                            InvokeEvent(SetPropertyReply, SetPropertyReplyAsync);
                            break;
                        case mpv_event_id.MPV_EVENT_COMMAND_REPLY:
                            InvokeEvent(CommandReply, CommandReplyAsync);
                            break;
                        case mpv_event_id.MPV_EVENT_START_FILE:
                            InvokeEvent(StartFile, StartFileAsync);
                            App.RunTask(() => LoadFolder());
                            break;
                        case mpv_event_id.MPV_EVENT_AUDIO_RECONFIG:
                            InvokeEvent(AudioReconfig, AudioReconfigAsync);
                            break;
                        case mpv_event_id.MPV_EVENT_SEEK:
                            InvokeEvent(Seek, SeekAsync);
                            break;
                        case mpv_event_id.MPV_EVENT_PLAYBACK_RESTART:
                            InvokeEvent(PlaybackRestart, PlaybackRestartAsync);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    App.ShowException(ex);
                }
            }
        }

        void ProcessBluRayLogMessage(string msg)
        {
            lock (BluRayTitles)
            {
                if (msg.Contains(" 0 duration: "))
                    BluRayTitles.Clear();

                if (msg.Contains(" duration: "))
                {
                    int start = msg.IndexOf(" duration: ") + 11;
                    BluRayTitles.Add(new TimeSpan(
                        msg.Substring(start, 2).ToInt(),
                        msg.Substring(start + 3, 2).ToInt(),
                        msg.Substring(start + 6, 2).ToInt()));
                }
            }
        }

        public void SetBluRayTitle(int id)
        {
            LoadFiles(new[] { @"bd://" + id }, false, false);
        }

        void InvokeEvent(Action action, Action asyncAction)
        {
            InvokeAsync(asyncAction);
            action?.Invoke();
        }

        void InvokeEvent<T>(Action<T> action, Action<T> asyncAction, T t)
        {
            InvokeAsync(asyncAction, t);
            action?.Invoke(t);
        }

        void InvokeAsync(Action action)
        {
            if (action != null)
            {
                foreach (Action a in action.GetInvocationList())
                {
                    var a2 = a;
                    App.RunTask(a2);
                }
            }
        }

        void InvokeAsync<T>(Action<T> action, T t)
        {
            if (action != null)
            {
                foreach (Action<T> a in action.GetInvocationList())
                {
                    var a2 = a;
                    App.RunTask(() => a2.Invoke(t));
                }
            }
        }

        void InvokeAsync<T1, T2>(Action<T1, T2> action, T1 t1, T2 t2)
        {
            if (action != null)
            {
                foreach (Action<T1, T2> a in action.GetInvocationList())
                {
                    var a2 = a;
                    App.RunTask(() => a2.Invoke(t1, t2));
                }
            }
        }
    }
}
