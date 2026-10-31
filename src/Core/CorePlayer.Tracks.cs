
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
        public void UpdateTracks()
        {
            string path = GetPropertyString("path");

            if (!path.ToLowerEx().StartsWithEx("bd://"))
                lock (BluRayTitles)
                    BluRayTitles.Clear();

            List<MediaTrack> tracks;

            // I/O outside the lock. Skip MediaInfo on network/WebDAV — use mpv track-list instead.
            if (App.MediaInfo && !IsRemotePath(path))
            {
                bool exists = false;

                try { exists = File.Exists(path); }
                catch { exists = false; }

                tracks = exists ? GetMediaInfoTracks(path) : GetTracks();
            }
            else
                tracks = GetTracks();

            lock (MediaTracksLock)
                MediaTracks = tracks;
        }

        public List<Chapter> GetChapters() {
            List<Chapter> chapters = new List<Chapter>();
            int count = GetPropertyInt("chapter-list/count");

            for (int x = 0; x < count; x++)
            {
                string title = GetPropertyString($"chapter-list/{x}/title");
                double time = GetPropertyDouble($"chapter-list/{x}/time");

                if (string.IsNullOrEmpty(title) ||
                    (title.Length == 12 && title.Contains(":") && title.Contains(".")))

                    title = "Chapter " + (x + 1);

                chapters.Add(new Chapter() { Title = title, Time = time });
            }

            return chapters;
        }

        public void UpdateExternalTracks()
        {
            lock (MediaTracksLock)
                UpdateExternalTracksUnlocked();
        }

        // Caller must hold MediaTracksLock.
        public void UpdateExternalTracksUnlocked()
        {
            int trackListTrackCount = GetPropertyInt("track-list/count");
            int editionCount = GetPropertyInt("edition-list/count");
            int count = MediaTracks.Where(i => i.Type != "g").Count();

            if (count != (trackListTrackCount + editionCount))
            {
                MediaTracks = MediaTracks.Where(i => !i.External).ToList();
                MediaTracks.AddRange(GetTracks(false));
            }
        }

        public List<MediaTrack> GetTracks(bool includeInternal = true, bool includeExternal = true)
        {
            List<MediaTrack> tracks = new List<MediaTrack>();

            int trackCount = GetPropertyInt("track-list/count");

            for (int i = 0; i < trackCount; i++)
            {
                bool external = GetPropertyBool($"track-list/{i}/external");

                if ((external && !includeExternal) || (!external && !includeInternal))
                    continue;

                string type = GetPropertyString($"track-list/{i}/type");
                string filename = GetPropertyString($"filename/no-ext");
                string title = GetPropertyString($"track-list/{i}/title").Replace(filename, "");

                title = Regex.Replace(title, @"^[\._\-]", "");

                if (type == "video")
                {
                    string codec = GetPropertyString($"track-list/{i}/codec").ToUpperEx();
                    if (codec == "MPEG2VIDEO")
                        codec = "MPEG2";
                    else if (codec == "DVVIDEO")
                        codec = "DV";
                    MediaTrack track = new MediaTrack();
                    Add(track, codec);
                    Add(track, GetPropertyString($"track-list/{i}/demux-w") + "x" + GetPropertyString($"track-list/{i}/demux-h"));
                    Add(track, GetPropertyString($"track-list/{i}/demux-fps").Replace(".000000", "") + " FPS");
                    Add(track, GetPropertyBool($"track-list/{i}/default") ? "Default" : null);
                    track.Text = "V: " + track.Text.Trim(' ', ',');
                    track.Type = "v";
                    track.ID = GetPropertyInt($"track-list/{i}/id");
                    tracks.Add(track);
                }
                else if (type == "audio")
                {
                    string codec = GetPropertyString($"track-list/{i}/codec").ToUpperEx();
                    if (codec.Contains("PCM"))
                        codec = "PCM";
                    MediaTrack track = new MediaTrack();
                    Add(track, GetLanguage(GetPropertyString($"track-list/{i}/lang")));
                    Add(track, codec);
                    Add(track, GetPropertyInt($"track-list/{i}/audio-channels") + " ch");
                    Add(track, GetPropertyInt($"track-list/{i}/demux-samplerate") / 1000 + " kHz");
                    Add(track, GetPropertyBool($"track-list/{i}/forced") ? I18n._("Forced") : null);
                    Add(track, GetPropertyBool($"track-list/{i}/default") ? I18n._("Default") : null);
                    Add(track, GetPropertyBool($"track-list/{i}/external") ? I18n._("External") : null);
                    Add(track, LocalizeLanguageLabel(title));
                    track.Text = "A: " + track.Text.Trim(' ', ',');
                    track.Type = "a";
                    track.ID = GetPropertyInt($"track-list/{i}/id");
                    track.External = external;
                    tracks.Add(track);
                }
                else if (type == "sub")
                {
                    string codec = GetPropertyString($"track-list/{i}/codec").ToUpperEx();
                    if (codec.Contains("PGS"))
                        codec = "PGS";
                    else if (codec == "SUBRIP")
                        codec = "SRT";
                    else if (codec == "WEBVTT")
                        codec = "VTT";
                    else if (codec == "DVB_SUBTITLE")
                        codec = "DVB";
                    else if (codec == "DVD_SUBTITLE")
                        codec = "VOB";
                    MediaTrack track = new MediaTrack();
                    Add(track, GetLanguage(GetPropertyString($"track-list/{i}/lang")));
                    Add(track, codec);
                    Add(track, GetPropertyBool($"track-list/{i}/forced") ? I18n._("Forced") : null);
                    Add(track, GetPropertyBool($"track-list/{i}/default") ? I18n._("Default") : null);
                    Add(track, GetPropertyBool($"track-list/{i}/external") ? I18n._("External") : null);
                    Add(track, LocalizeLanguageLabel(title));
                    track.Text = "S: " + track.Text.Trim(' ', ',');
                    track.Type = "s";
                    track.ID = GetPropertyInt($"track-list/{i}/id");
                    track.External = external;
                    tracks.Add(track);
                }
            }

            if (includeInternal)
            {
                int editionCount = GetPropertyInt("edition-list/count");

                for (int i = 0; i < editionCount; i++)
                {
                    string title = GetPropertyString($"edition-list/{i}/title");
                    if (string.IsNullOrEmpty(title))
                        title = "Edition " + i;
                    MediaTrack track = new MediaTrack();
                    track.Text = "E: " + title;
                    track.Type = "e";
                    track.ID = i;
                    tracks.Add(track);
                }
            }

            return tracks;
        }

        public List<MediaTrack> GetMediaInfoTracks(string path)
        {
            List<MediaTrack> tracks = new List<MediaTrack>();

            using (MediaInfo mi = new MediaInfo(path))
            {
                MediaTrack track = new MediaTrack();
                Add(track, mi.GetGeneral("Format"));
                Add(track, mi.GetGeneral("FileSize/String"));
                Add(track, mi.GetGeneral("Duration/String"));
                Add(track, mi.GetGeneral("OverallBitRate/String"));
                track.Text = "G: " + track.Text.Trim(' ', ',');
                track.Type = "g";
                tracks.Add(track);

                int videoCount = mi.GetCount(MediaInfoStreamKind.Video);

                for (int i = 0; i < videoCount; i++)
                {
                    string fps = mi.GetVideo(i, "FrameRate");

                    if (float.TryParse(fps, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                        fps = result.ToString(CultureInfo.InvariantCulture);

                    track = new MediaTrack();
                    Add(track, mi.GetVideo(i, "Format"));
                    Add(track, mi.GetVideo(i, "Format_Profile"));
                    Add(track, mi.GetVideo(i, "Width") + "x" + mi.GetVideo(i, "Height"));
                    Add(track, mi.GetVideo(i, "BitRate/String"));
                    Add(track, fps + " FPS");
                    Add(track, (videoCount > 1 && mi.GetVideo(i, "Default") == "Yes") ? "Default" : "");
                    track.Text = "V: " + track.Text.Trim(' ', ',');
                    track.Type = "v";
                    track.ID = i + 1;
                    tracks.Add(track);
                }

                int audioCount = mi.GetCount(MediaInfoStreamKind.Audio);

                for (int i = 0; i < audioCount; i++)
                {
                    string lang = mi.GetAudio(i, "Language/String");
                    string nativeLang = GetNativeLanguage(lang);
                    string title = mi.GetAudio(i, "Title");
                    string format = mi.GetAudio(i, "Format");

                    if (!string.IsNullOrEmpty(title))
                    {
                        if (title.ContainsEx("DTS-HD MA"))
                            format = "DTS-MA";

                        if (title.ContainsEx("DTS-HD MA"))
                            title = title.Replace("DTS-HD MA", "");

                        if (title.ContainsEx("Blu-ray"))
                            title = title.Replace("Blu-ray", "");

                        if (title.ContainsEx("UHD "))
                            title = title.Replace("UHD ", "");

                        if (title.ContainsEx("EAC"))
                            title = title.Replace("EAC", "E-AC");

                        if (title.ContainsEx("AC3"))
                            title = title.Replace("AC3", "AC-3");

                        if (title.ContainsEx(lang))
                            title = title.Replace(lang, "").Trim();

                        if (title.ContainsEx(nativeLang))
                            title = title.Replace(nativeLang, "").Trim();

                        if (title.ContainsEx("Surround"))
                            title = title.Replace("Surround", "");

                        if (title.ContainsEx("Dolby Digital"))
                            title = title.Replace("Dolby Digital", "");

                        if (title.ContainsEx("Stereo"))
                            title = title.Replace("Stereo", "");

                        if (title.StartsWithEx(format + " "))
                            title = title.Replace(format + " ", "");

                        foreach (string i2 in new [] { "2.0", "5.1", "6.1", "7.1" })
                            if (title.ContainsEx(i2))
                                title = title.Replace(i2, "").Trim();

                        if (title.ContainsEx("@ "))
                            title = title.Replace("@ ", "");

                        if (title.ContainsEx(" @"))
                            title = title.Replace(" @", "");

                        if (title.ContainsEx("()"))
                            title = title.Replace("()", "");

                        if (title.ContainsEx("[]"))
                            title = title.Replace("[]", "");

                        if (title.TrimEx() == format)
                            title = null;

                        if (!string.IsNullOrEmpty(title))
                            title = title.Trim(" _-".ToCharArray());
                    }

                    track = new MediaTrack();
                    Add(track, LocalizeLanguageLabel(lang));
                    Add(track, format);
                    Add(track, mi.GetAudio(i, "Format_Profile"));
                    Add(track, mi.GetAudio(i, "BitRate/String"));
                    Add(track, mi.GetAudio(i, "Channel(s)") + " ch");
                    Add(track, mi.GetAudio(i, "SamplingRate/String"));
                    Add(track, mi.GetAudio(i, "Forced") == "Yes" ? I18n._("Forced") : "");
                    Add(track, (audioCount > 1 && mi.GetAudio(i, "Default") == "Yes") ? I18n._("Default") : "");
                    Add(track, LocalizeLanguageLabel(title));

                    if (track.Text.Contains("MPEG Audio, Layer 2"))
                        track.Text = track.Text.Replace("MPEG Audio, Layer 2", "MP2");

                    if (track.Text.Contains("MPEG Audio, Layer 3"))
                        track.Text = track.Text.Replace("MPEG Audio, Layer 2", "MP3");

                    track.Text = "A: " + track.Text.Trim(' ', ',');
                    track.Type = "a";
                    track.ID = i + 1;
                    tracks.Add(track);
                }

                int subCount = mi.GetCount(MediaInfoStreamKind.Text);

                for (int i = 0; i < subCount; i++)
                {
                    string codec = mi.GetText(i, "Format").ToUpperEx();

                    if (codec == "UTF-8")
                        codec = "SRT";
                    else if (codec == "WEBVTT")
                        codec = "VTT";
                    else if (codec == "VOBSUB")
                        codec = "VOB";

                    string lang = mi.GetText(i, "Language/String");
                    string nativeLang = GetNativeLanguage(lang);
                    string title = mi.GetText(i, "Title");
                    bool forced = mi.GetText(i, "Forced") == "Yes";

                    if (!string.IsNullOrEmpty(title))
                    {
                        if (title.ContainsEx("VobSub"))
                            title = title.Replace("VobSub", "VOB");

                        if (title.ContainsEx(codec))
                            title = title.Replace(codec, "");

                        if (title.ContainsEx(lang.ToLowerEx()))
                            title = title.Replace(lang.ToLowerEx(), lang);

                        if (title.ContainsEx(nativeLang.ToLowerEx()))
                            title = title.Replace(nativeLang.ToLowerEx(), nativeLang).Trim();

                        if (title.ContainsEx(lang))
                            title = title.Replace(lang, "");

                        if (title.ContainsEx(nativeLang))
                            title = title.Replace(nativeLang, "").Trim();

                        if (title.ContainsEx("full"))
                            title = title.Replace("full", "").Trim();

                        if (title.ContainsEx("Full"))
                            title = title.Replace("Full", "").Trim();

                        if (title.ContainsEx("Subtitles"))
                            title = title.Replace("Subtitles", "").Trim();

                        if (title.ContainsEx("forced"))
                            title = title.Replace("forced", "Forced").Trim();

                        if (forced && title.ContainsEx("Forced"))
                            title = title.Replace("Forced", "").Trim();

                        if (title.ContainsEx("()"))
                            title = title.Replace("()", "");

                        if (title.ContainsEx("[]"))
                            title = title.Replace("[]", "");

                        if (!string.IsNullOrEmpty(title))
                            title = title.Trim(" _-".ToCharArray());
                    }

                    track = new MediaTrack();
                    Add(track, LocalizeLanguageLabel(lang));
                    Add(track, codec);
                    Add(track, mi.GetText(i, "Format_Profile"));
                    Add(track, forced ? I18n._("Forced") : "");
                    Add(track, (subCount > 1 && mi.GetText(i, "Default") == "Yes") ? I18n._("Default") : "");
                    Add(track, LocalizeLanguageLabel(title));
                    track.Text = "S: " + track.Text.Trim(' ', ',');
                    track.Type = "s";
                    track.ID = i + 1;
                    tracks.Add(track);
                }
            }

            int editionCount = GetPropertyInt("edition-list/count");

            for (int i = 0; i < editionCount; i++)
            {
                string title = GetPropertyString($"edition-list/{i}/title");
                if (string.IsNullOrEmpty(title))
                    title = "Edition " + i;
                MediaTrack track = new MediaTrack();
                track.Text = "E: " + title;
                track.Type = "e";
                track.ID = i;
                tracks.Add(track);
            }

            return tracks;
        }

        void Add(MediaTrack track, object value)
        {
            string str = value.ToStringEx().Trim();

            if (str != "" && !(track.Text != null && track.Text.Contains(str)))
                track.Text += " " + str + ",";
        }
    }
}
