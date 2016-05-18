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
    class Producer
    {
        public event EventHandler<string> CommentReceived;
        public event EventHandler<string> ErrorReceived;
        
        readonly string _server = "irc.chat.twitch.tv";
        
        /*
         * Register on http://www.twitch.tv
         * Username and nickname are your username that you registered with
         */
        readonly string _username = "your_username";
        readonly string _nickname = "your_nickname";

        /*
         * After you have registered on twitch, obtain an OAuth password
         * from https://twitchapps.com/tmi/
         * and enter it below in the form of 'oath:n928jd892jd8h...'
         */
        readonly string _oathPass = "oauth:your_oath_pass";

        IrcClient _client;
        IrcUser _ircUser;

        /*
         * Twitch channels can be found on http://www.twitch.tv
         * Open any stream on twitch and the channel can be found in the URL
         * For example in https://www.twitch.tv/lirik the channel is 'lirik'
         * Make sure the channel is live, otherwise there are probably no people commenting on it
         */
        string _channel;

        public void Connect(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentNullException("Channel can't be null");

            // Channel should start with a hashtag (user might or might not have already added one)
            _channel = $"#{channel.Replace("#", "")}";

            _ircUser = new IrcUser(_nickname, _username, _oathPass);
            _client = new IrcClient(_server, _ircUser);

            _client.NoticeRecieved += _client_NoticeRecieved;
            _client.ConnectionComplete += _client_ConnectionComplete;
            _client.ChannelMessageRecieved += _client_ChannelMessageReceived;

            _client.ConnectAsync();
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
