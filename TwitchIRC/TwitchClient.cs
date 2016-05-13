using ChatSharp;
using ChatSharp.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchIRC
{
    class TwitchClient
    {
        public event EventHandler<string> CommentReceived;
        public event EventHandler<string> ErrorReceived;

        public bool Cancel { get; set; }

        private readonly string _server = "irc.chat.twitch.tv";
        private readonly string _username = "cheetahk";
        private readonly string _nickname = "cheetahk";
        private readonly string _oathPass = "oauth:n4h74d8yc9woy9hg7oct6nzqsspu69";
        private IrcClient _client;
        private IrcUser _ircUser;
        private string _channel;

        public void Connect(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentNullException("Channel can't be null");

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

            //---Code from this point forward executed only after cancel is set to true---

            // Unsub from event to prevent memory leak
            _client.ChannelMessageRecieved -= _client_ChannelMessageReceived;
            _client.Quit("User cancel");
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
