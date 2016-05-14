using ChatSharp;
using ChatSharp.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchIRC
{
    class TwitchClient
    {
        public TwitchClient(uint timeToRun)
        {
            _timeToRun = timeToRun;
        }

        public event EventHandler<string> CommentReceived;
        public event EventHandler<string> ErrorReceived;

        public bool Cancel { get; set; }
        private uint _timeToRun = 0;

        private readonly string _server = "irc.chat.twitch.tv";
        
        /*
         * Register on http://www.twitch.tv
         * Username and nickname are your username that you registered with
         */
        private readonly string _username = "cheetahk";
        private readonly string _nickname = "cheetahk";

        /*
         * After you have registered on twitch, obtain an OAuth password
         * from https://twitchapps.com/tmi/
         * and enter it below in the form of 'oath:n928jd892jd8h...'
         */
        private readonly string _oathPass = "oauth:n4h74d8yc9woy9hg7oct6nzqsspu69";

        private IrcClient _client;
        private IrcUser _ircUser;

        /*
         * Twitch channels can be found on http://www.twitch.tv
         * Open any stream on twitch and the channel can be found in the URL
         * For example in https://www.twitch.tv/lirik the channel is 'lirik'
         * Make sure the channel is live, otherwise there are probably no people commenting on it
         */
        private string _channel;

        public void Connect(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentNullException("Channel can't be null");

            Timer timer = new Timer(StopRunning);
            timer.Change(DateTime.Now.Millisecond + _timeToRun, Timeout.Infinite);

            // Channel should start with a hashtag, user might have already passed a hashtag
            // Ensure only one hashtag is prepended
            _channel = $"#{channel.Replace("#", "")}";

            _ircUser = new IrcUser(_nickname, _username, _oathPass);
            _client = new IrcClient(_server, _ircUser);

            _client.NoticeRecieved += _client_NoticeRecieved;
            _client.ConnectionComplete += _client_ConnectionComplete;
            _client.ChannelMessageRecieved += _client_ChannelMessageReceived;

            _client.ConnectAsync();
            
            while (!Cancel)
                ; // Do nothing
        }

        public void StopRunning(object p)
        {
            // Unsub from event to prevent memory leak
            _client.ChannelMessageRecieved -= _client_ChannelMessageReceived;
            _client.Quit("User cancel");

            Cancel = true;
        }

        private void _client_NoticeRecieved(object sender, IrcNoticeEventArgs e)
        {
            ErrorReceived?.Invoke(this, e.Notice);
            _client.NoticeRecieved -= _client_NoticeRecieved;
        }

        private void _client_ChannelMessageReceived(object sender, PrivateMessageEventArgs e)
        {
            CommentReceived?.Invoke(this, e.PrivateMessage.Message);
        }

        private void _client_ConnectionComplete(object sender, EventArgs e)
        {
            _client.JoinChannel(_channel);
            _client.ConnectionComplete -= _client_ConnectionComplete;
        }
    }

}
