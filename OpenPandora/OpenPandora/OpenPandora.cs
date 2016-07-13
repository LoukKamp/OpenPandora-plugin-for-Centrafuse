using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using centrafuse.Plugins;
using OpenPandora.JSON;
using PandoraSharp;
using Un4seen.Bass;

namespace OpenPandora
{
    public class OpenPandora : CFPlugin
    {
        private const string PLUGIN_NAME = "OpenPandora";

        private string _username;
        private string Username
        {
            get
            {
                return _username;
            }
        }

        private string _password;
        private string Password
        {
            get
            {
                return _password;
            }
        }

        private string _quality;
        private string Quality
        {
            get
            {
                return _quality;
            }
        }

        public OpenPandora()
        {
            OnSongCompleteDelegate = new SYNCPROC(OnSongComplete);
        }

        public override void CF_pluginInit()
        {
            this.CF3_initPlugin(PLUGIN_NAME, true);

            this.CF_localskinsetup();

            this.CF_params.Media.isAudioPlugin = true;
        }

        private Pandora _client = null;
        private object _clientLock = new object();
        private Pandora Client
        {
            get
            {
                lock (_clientLock)
                {
                    if (_client == null)
                    {
                        _client = new Pandora();
                        _client.ConnectionEvent += _client_ConnectionEvent;
                        _client.LoginStatusEvent += _client_LoginStatusEvent;
                        _client.StationsUpdatingEvent += _client_StationsUpdatingEvent;
                        _client.StationUpdateEvent += _client_StationUpdateEvent;
                    }

                    return _client;
                }
            }
        }

        void _client_StationUpdateEvent(object sender)
        {
            HideInfo();
            PopulateStationList();
        }

        private void PopulateStationList()
        {
            if (StationListBindingSource.DataSource != null)
            {
                var source = StationListBindingSource.DataSource as DataTable;
                StationListBindingSource.DataSource = null;
                source.Dispose();
            }

            var table = new DataTable();
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Playing", typeof(string));
            table.Columns.Add("StationObject", typeof(Station));
            
            Client.Stations.ForEach(s =>
                {
                    var newRow = table.NewRow();
                    newRow["Name"] = s.Name;
                    newRow["Playing"] = "No";
                    newRow["StationObject"] = s;
                    table.Rows.Add(newRow);
                });
            
            StationListBindingSource.DataSource = table;

            advancedlistArray[CF_getAdvancedListID("stationList")].Refresh();
        }

        void _client_StationsUpdatingEvent(object sender)
        {
            ShowInfo("Loading station list...");
        }

        void _client_LoginStatusEvent(object sender, string status)
        {
            ShowInfo(status);
        }

        private bool connecting = false;
        private bool connected = false;
        void _client_ConnectionEvent(object sender, bool state, Util.ErrorCodes code)
        {
            connected = state;
            connecting = false;
            HideInfo();
            if (!state)
                CF_displayMessage("Connection error:\n" + code.ToString());
        }

        private int _zone = 0;
        bool _hasControl = false;
        public override void CF_pluginShow()
        {
            base.CF_pluginShow();
            _hasControl = true;
            if (!connected && !connecting)
            {
                LoadSettings();

                if (string.IsNullOrEmpty(Username))
                {
                    CF_displayMessage("Username not specified");
                    return;
                }

                switch (Quality)
                {
                    case "0":
                        Client.AudioFormat = PAudioFormat.AACPlus;
                        break;
                    case "1":
                        Client.AudioFormat = PAudioFormat.MP3;
                        break;
                    case "2":
                        Client.AudioFormat = PAudioFormat.MP3_HIFI;
                        break;
                    default:
                        Client.AudioFormat = PAudioFormat.MP3;
                        break;

                }

                connecting = true;
                Task.Factory.StartNew(() => Client.Connect(Username, Password));
            }
        }

        public override DialogResult CF_pluginShowSetup()
        {
            Setup setup = new Setup(this.MainForm, this.pluginConfig, this.pluginLang);
            return setup.ShowDialog();
        }

        private void LoadSettings()
        {
            _username = this.pluginConfig.ReadField("/APPCONFIG/EMAIL");
            
            string encryptedPassword = this.pluginConfig.ReadField("/APPCONFIG/PASSWORD");

            if (!string.IsNullOrEmpty(encryptedPassword))
            {
                try
                {
                    _password = EncryptionHelper.DecryptString(encryptedPassword, Setup.PASSWORD);
                }
                catch (Exception ex)
                {
                    CF_displayMessage(ex.Message);
                }
            }

            _quality = this.pluginConfig.ReadField("/APPCONFIG/BITRATE");
        }

        BindingSource StationListBindingSource;
        public override void CF_localskinsetup()
        {
            this.CF3_initSection("Main");
            var list = advancedlistArray[CF_getAdvancedListID("stationList")];

            StationListBindingSource = new BindingSource();

            list.DoubleClickListTiming = true;
            list.DoubleClick += list_DoubleClick;
            list.LinkedItemClick += list_LinkedItemClick;
            list.DataBinding = StationListBindingSource;
            list.TemplateID = "default";
        }

        void list_LinkedItemClick(object sender, CFControlsExtender.Listview.LinkedItemArgs e)
        {
            switch(e.LinkId)
            {
                case "playStation":
                    var table = StationListBindingSource.DataSource as DataTable;
                    if (e.ItemId < table.Rows.Count)
                    {
                        var station = table.Rows[e.ItemId]["StationObject"] as Station;
                        SwitchToStation(station);
                    }
                    break;
            }
        }

        void list_DoubleClick(object sender, CFControlsExtender.Listview.ItemArgs e)
        {
            var table = StationListBindingSource.DataSource as DataTable;
            if (table != null && e.ItemId < table.Rows.Count)
            {
                var station = table.Rows[e.ItemId]["StationObject"] as Station;
                SwitchToStation(station);
            }
        }

        private Station CurrentStation = null;
        private void SwitchToStation(Station station)
        {
            if (PlayerStreamID != -1)
            {
                CF_controlAudioStream(PlayerStreamID, CF_AudioAction.Stop);
                CF_controlAudioStream(PlayerStreamID, CF_AudioAction.Free);
                PlayerStreamID = -1;
            }

            CurrentStation = station;
            currentSongIx = 0;
            currentStationPlaylist = null;
            ShowInfo("Loading Station...");
            Task.Factory.StartNew(() =>
                {
                    currentStationPlaylist = CurrentStation.GetPlaylist();
                    this.BeginInvoke(new MethodInvoker(() =>
                        {
                            HideInfo();
                            PlaySong(CurrentSong);
                        }));
                });
        }

        private Image CurrentImage = null;
        private int PlayerStreamID = -1;
        private void PlaySong(PandoraSharp.Song song)
        {
            if (PlayerStreamID != -1)
            {
                CF_controlAudioStream(PlayerStreamID, CF_AudioAction.Stop);
                CF_controlAudioStream(PlayerStreamID, CF_AudioAction.Free);
                PlayerStreamID = -1;
            }

            var url = song.AudioUrl;
            CF_clearPictureImage("albumArt");
            Task.Factory.StartNew(() =>
                {
                    using (WebClient client = new WebClient())
                    {
                        try
                        {
                            var imageData = client.DownloadData(song.AlbumArtUrl);
                            this.BeginInvoke(new MethodInvoker(() =>
                                {
                                    var currentSong = CurrentSong;
                                    if (currentSong != null && currentSong.AudioUrl.Equals(url))
                                    {
                                        if (CurrentImage != null)
                                            CurrentImage.Dispose();

                                        using (MemoryStream ms = new MemoryStream(imageData))
                                        {
                                            CurrentImage = Image.FromStream(ms);
                                        }

                                        CF_setPictureImage("albumArt", CurrentImage);
                                    }
                                }));
                        }
                        catch { }
                    }
                });

            PlayerStreamID = PlayStream(url);

            if (CurrentSong.Loved)
                CF_setButtonOn("thumbsUp");
            else
                CF_setButtonOff("thumbsUp");

            var table = StationListBindingSource.DataSource as DataTable;
            if(table != null)
            {
                table.Rows.Cast<DataRow>().ToList().ForEach(row => row["Playing"] = "No");

                List<Station> stationsToUpdate = new List<Station>();
                stationsToUpdate.Add(CurrentStation);
                
                if (!song.Station.ID.Equals(CurrentStation.ID)) //in case of quickmix, stations are different
                    stationsToUpdate.Add(song.Station);

                var rowsToUpdate = stationsToUpdate.Select(s => table.Rows.Cast<DataRow>().SingleOrDefault(r => (r["StationObject"] as Station).ID.Equals(s.ID))).ToList();

                rowsToUpdate.ForEach(row =>
                    {
                        if (row != null)
                        {
                            row["Playing"] = "playIcon";
                        }
                    });
            }

            advancedlistArray[CF_getAdvancedListID("stationList")].Refresh();

            isPaused = false;
        }

        private int PlayStream(string url)
        {
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_PLAYLIST, 1);
            var streamNum = Bass.BASS_StreamCreateURL(url, 0, BASSFlag.BASS_STREAM_AUTOFREE | BASSFlag.BASS_STREAM_STATUS, null, IntPtr.Zero);
            Bass.BASS_ChannelSetSync(streamNum, BASSSync.BASS_SYNC_END, 0, OnSongCompleteDelegate, IntPtr.Zero);
            Bass.BASS_ChannelPlay(streamNum, false);

            return streamNum;
        }

        private SYNCPROC OnSongCompleteDelegate;
        private void OnSongComplete(int a, int b, int c, IntPtr d)
        {
            this.BeginInvoke(new MethodInvoker(() => PlayNextSong()));
        }

        bool gettingNext = false;
        private void PlayNextSong()
        {
            if (gettingNext)
                return;

            if (PlayerStreamID != -1)
            {
                CF_controlAudioStream(PlayerStreamID, CF_AudioAction.Stop);
                CF_controlAudioStream(PlayerStreamID, CF_AudioAction.Free);
                PlayerStreamID = -1;
            }

            currentSongIx++;

            while(currentSongIx < currentStationPlaylist.Count && (currentStationPlaylist.ElementAt(currentSongIx).Banned || !currentStationPlaylist.ElementAt(currentSongIx).IsStillValid))
            {
                currentSongIx++;
            }
            
            if (currentSongIx < currentStationPlaylist.Count)
            {
                PlaySong(CurrentSong);
            }
            else
            {
                ShowInfo("Please Wait...");
                gettingNext = true;
                Task.Factory.StartNew(() =>
                    {
                        currentStationPlaylist = CurrentStation.GetPlaylist();
                        this.BeginInvoke(new MethodInvoker(() =>
                            {
                                currentSongIx = 0;
                                gettingNext = false;
                                HideInfo();
                                PlaySong(CurrentSong);
                            }));
                    });
            }
        }

        private int currentSongIx = 0;
        private List<PandoraSharp.Song> currentStationPlaylist;        
        private PandoraSharp.Song CurrentSong
        {
            get
            {
                if (CurrentStation != null && currentStationPlaylist != null && currentStationPlaylist.Count > currentSongIx)
                {
                    return currentStationPlaylist.ElementAt(currentSongIx);
                }
                else
                    return null;
            }
        }


        private void ShowInfo(string info)
        {
            this.Invoke(new MethodInvoker(() =>
                {
                    HideInfo();
                    this.CF_systemCommand(CF_Actions.SHOWINFO, info);
                }));
        }

        private void HideInfo()
        {
            this.Invoke(new MethodInvoker(() =>
            {
                CF_systemCommand(CF_Actions.HIDEINFO);
            }));
        }

        public override bool CF_pluginCMLCommand(string command, string[] strparams, CF_ButtonState state, int zone)
        {
            if (!_hasControl)
                return false;

            _zone = zone;
            switch (command)
            {
                case "OpenPandora.ScrollUp":
                    if (state >= CF_ButtonState.Click)
                    {
                        var list = advancedlistArray[CF_getAdvancedListID("stationList")];
                        list.PageUp();
                    }
                    return true;
                case "OpenPandora.ScrollDown":
                    if (state >= CF_ButtonState.Click)
                    {
                        var list = advancedlistArray[CF_getAdvancedListID("stationList")];
                        list.PageDown();
                    }
                    return true;
                case "OpenPandora.ThumbsUp":
                    if (state >= CF_ButtonState.Click)
                    {
                        OnThumbsUp();
                    }
                    return true;
                case "OpenPandora.ThumbsDown":
                    if (state >= CF_ButtonState.Click)
                    {
                        OnThumbsDown();
                    }
                    return true;
                case "OpenPandora.Tired":
                    if (state >= CF_ButtonState.Click)
                    {
                        OnTired();
                    }
                    return true;
                case "OpenPandora.ToSpotify":
                    if (state >= CF_ButtonState.Click)
                    {
                        OnToSpotify();
                    }
                    return true;
                case "OpenPandora.AddStation":
                    if (state >= CF_ButtonState.Click)
                    {
                        OnAddStation();
                    }
                    return true;
                case "Centrafuse.Main.Rewind":
                    if (state >= CF_ButtonState.Click)
                    {
                        
                    }
                    return true;
                case "Centrafuse.Main.FastForward":
                    if (state >= CF_ButtonState.Click)
                    {
                        PlayNextSong();
                    }
                    return true;
                case "Centrafuse.Main.PlayPause":
                    if (state >= CF_ButtonState.Click)
                    {
                        PlayPause();
                    }
                    return true;
            }

            return false;
        }

        bool isPaused = true;
        private void PlayPause()
        {
            if (CurrentSong != null)
            {
                if (isPaused)
                {
                    Play();
                }
                else
                {
                    Pause();
                }
            }
        }

        private void Play()
        {
            isPaused = false;
            CF_setPlayPauseButton(false, _zone);
            CF_controlAudioStream(PlayerStreamID, CF_AudioAction.Play);
        }

        private void Pause()
        {
            isPaused = true;
            CF_setPlayPauseButton(true, _zone);
            CF_controlAudioStream(PlayerStreamID, CF_AudioAction.Pause);
        }

        private void OnAddStation()
        {

        }

        private const string SPOTIFY_PLUGIN_NAME = "Spotify";
        private void OnToSpotify()
        {
            return;

            if (CurrentSong != null)
            {
                var pingResponse = CF_getPluginData(SPOTIFY_PLUGIN_NAME, "Ping", string.Empty);

                if (!pingResponse.Equals(true.ToString()))
                {
                    CF_displayMessage("Spotify is not installed!");
                    return;
                }

                SearchRequest request = new SearchRequest()
                {
                    Artist = CurrentSong.Artist,
                    Title = CurrentSong.SongTitle,
                    Album = CurrentSong.Album
                };

                var serializer = new JavaScriptSerializer();
                var serializedRequest = serializer.Serialize(request);

                var response = CF_getPluginData(SPOTIFY_PLUGIN_NAME, "Search", serializedRequest);

                if (!response.Equals(true.ToString()))
                {
                    CF_displayMessage("Spotify plugin refused the search request. Perhaps it's out of date?");
                }
                else
                {
                    ShowInfo("Searching...");
                }
            }
        }

        private void OnTired()
        {
            var song = CurrentSong;
            if (song != null)
            {

                var result = CF_systemDisplayDialog(CF_Dialogs.YesNo, "This song won't be played for a while. Are you sure?");
                if (result != System.Windows.Forms.DialogResult.OK && result != System.Windows.Forms.DialogResult.Yes)
                {
                    return;
                }

                song.SetTired();
                PlayNextSong();
            }
        }

        private void OnThumbsDown()
        {
            var song = CurrentSong;
            if (song != null)
            {

                var result = CF_systemDisplayDialog(CF_Dialogs.YesNo, "Are you sure you want to ban this song?");
                if (result != System.Windows.Forms.DialogResult.OK && result != System.Windows.Forms.DialogResult.Yes)
                {
                    return;
                }

                song.Rate(SongRating.ban);
                PlayNextSong();
            }
        }

        private void OnThumbsUp()
        {
            var song = CurrentSong;
            if (song != null)
            {
                if (song.Loved)
                {
                    song.Rate(SongRating.none);
                    CF_setButtonOff("thumbsUp");
                }
                else
                {
                    song.Rate(SongRating.love);
                    CF_setButtonOn("thumbsUp");
                }
            }
        }

        public override string CF_pluginCMLData(CF_CMLTextItems textItem)
        {
            switch (textItem)
            {
                case CF_CMLTextItems.MainTitle:
                    return CurrentSong == null ? String.Empty : CurrentSong.SongTitle;
                case CF_CMLTextItems.MediaArtist:
                    return CurrentSong == null ? String.Empty : CurrentSong.Artist;
                case CF_CMLTextItems.MediaTitle:
                    return CurrentSong == null ? String.Empty : CurrentSong.SongTitle;
                case CF_CMLTextItems.MediaAlbum:
                    return CurrentSong == null ? String.Empty : CurrentSong.Album;
                case CF_CMLTextItems.MediaSource:
                case CF_CMLTextItems.MediaStation:
                    return "Spotify";
                case CF_CMLTextItems.MediaDuration:
                    return GetCurrentTrackDuration();
                case CF_CMLTextItems.MediaPosition:
                    return GetCurrentTrackPosition();
                case CF_CMLTextItems.MediaSliderPosition:
                    return GetCurrentTrackScrubberPosition();

                default:
                    return base.CF_pluginCMLData(textItem);
            }
        }

        private string GetCurrentTrackScrubberPosition()
        {
            int pos = 0;
            if (PlayerStreamID != -1)
            {
                long position = Bass.BASS_ChannelGetPosition(PlayerStreamID, BASSMode.BASS_POS_BYTES);
                // the time length 
                long duration = Bass.BASS_ChannelGetLength(PlayerStreamID, BASSMode.BASS_POS_BYTES);

                var positionPercentage = Math.Floor((double)position / (double)duration * 100);
                pos = (int)positionPercentage;
            }

            return pos.ToString();
        }

        private string GetCurrentTrackPosition()
        {
            TimeSpan span = TimeSpan.Zero;
            if (PlayerStreamID != -1)
            {
                long len = Bass.BASS_ChannelGetPosition(PlayerStreamID, BASSMode.BASS_POS_BYTES);
                // the time length 
                double time = Bass.BASS_ChannelBytes2Seconds(PlayerStreamID, len);
                span = TimeSpan.FromSeconds(time);
            }

            return string.Format(timeFormat, span.Minutes, span.Seconds);
        }

        private const string timeFormat = "{0}:{1:00}";
        private string GetCurrentTrackDuration()
        {
            TimeSpan span = TimeSpan.Zero;
            if(PlayerStreamID != -1)
            {
                long len = Bass.BASS_ChannelGetLength(PlayerStreamID, BASSMode.BASS_POS_BYTES);
                // the time length 
                double time = Bass.BASS_ChannelBytes2Seconds(PlayerStreamID, len);
                span = TimeSpan.FromSeconds(time);
            }

            return string.Format(timeFormat, span.Minutes, span.Seconds);
        }

        public override void CF_pluginPause()
        {
            _hasControl = false;

            if (CurrentSong != null && !isPaused)
                Pause();
        }

        public override void CF_pluginResume()
        {
            _hasControl = true;

            if (CurrentSong != null && isPaused)
                Play();
        }

        public override string CF_pluginData(string command, string param1)
        {
            switch (command)
            {
                case "SearchResponse":
                    HideInfo();

                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    var response = serializer.Deserialize<SearchResponse>(param1);
                    if (response == null || !response.Success)
                    {
                        CF_displayMessage("Error occurred" + Environment.NewLine + response != null ? response.Error : string.Empty);
                    }
                    else
                    {
                        if (response.PerfectMatchBookmark)
                        {
                            CF_displayMessage("Your song was added to your Starred playlist!");
                        }
                        else
                        {

                        }
                    }

                    return bool.TrueString;
            }

            return bool.FalseString;
        }
    }
}
