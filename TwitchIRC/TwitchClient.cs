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
        public TwitchClient(string channel)
        {
            _channel = channel;
        }
        public event EventHandler<string> CommentReceived;

        public bool Cancel { get; set; }

        private readonly string server = "irc.chat.twitch.tv";
        private readonly string username = "cheetahk";
        private readonly string nickname = "cheetahk";
        private readonly string oathPass = "oauth:n4h74d8yc9woy9hg7oct6nzqsspu69";
        private IrcClient _client;
        private IrcUser _ircUser;
        private string _channel;

        public void Connect(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentNullException(nameof(channel));

            _ircUser = new IrcUser(nickname, username, oathPass);
            _client = new IrcClient(server, _ircUser);

            // Channels need to have a hashtag prepended to it in IRC clients
            _client.ConnectionComplete += _client_ConnectionComplete;
            _client.ChannelMessageRecieved += _client_ChannelMessageReceived;

            _client.ConnectAsync();

            // Continue fetching comments until Cancel is set to true
            while (!Cancel) ;

            // Any code here is execute after Cancel is set to true
            // Unsub from event to prevent memory leak
            _client.ChannelMessageRecieved -= _client_ChannelMessageReceived;
        }

        private void _client_ChannelMessageReceived(object sender, PrivateMessageEventArgs e)
        {
            CommentReceived?.Invoke(this, e.PrivateMessage.Message);
        }

        private void _client_ConnectionComplete(object sender, EventArgs e)
        {
            _client.JoinChannel($"#{_channel}");
            _client.ConnectionComplete -= _client_ConnectionComplete;
        }
    }

}
