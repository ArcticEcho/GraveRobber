//using System;
//using System.Linq;
//using MargieBot;

//namespace GraveRobber
//{
//    public class SlackChat : IDisposable
//    {
//        private readonly Bot bot = new Bot();
//        private bool disposed = false;

//        public SlackChat(string apiKey)
//        {
//            if (string.IsNullOrWhiteSpace(apiKey))
//            {
//                throw new ArgumentException("Invalid API key.", nameof(apiKey));
//            }

//            bot.Connect(apiKey).Wait();
//        }

//        ~SlackChat()
//        {
//            Dispose();
//        }

//        public void PostMessage(string channelName, string message)
//        {
//            if (!bot.IsConnected) return;

//            var chn = bot.ConnectedChannels.FirstOrDefault(c => c.Name == channelName);

//            if (chn == null)
//            {
//                throw new ArgumentException($"Unable to find channel with name: {channelName}");
//            }

//            var msg = new BotMessage
//            {
//                ChatHub = chn,
//                Text = message
//            };

//            bot.Say(msg).Wait();
//        }

//        public void Dispose()
//        {
//            if (disposed) return;
//            disposed = true;
//            bot.Disconnect();
//            GC.SuppressFinalize(this);
//        }
//    }
//}
