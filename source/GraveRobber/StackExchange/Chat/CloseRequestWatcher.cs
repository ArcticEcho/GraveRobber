using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using StackExchange.Auth;
using StackExchange.Chat;
using StackExchange.Chat.Events;
using StackExchange.Chat.Events.Message.Extensions;
using StackExchange.Net;
using StackExchange.Net.WebSockets;

namespace GraveRobber.StackExchange.Chat
{
	public class CloseRequestWatcher
	{
		private readonly Regex cvplsPattern;
		private readonly RoomWatcher<DefaultWebSocket> roomWatcher;

		public event Action<Message, int> OnNewRequest;

		public CloseRequestWatcher(IEnumerable<Cookie> authCookies)
		{
			var cvplsPatternStr = ConfigAccessor.GetValue<string>("CloseRequestPattern");

			cvplsPattern = new Regex(cvplsPatternStr, RegexOptions.Compiled | RegexOptions.CultureInvariant);

			var roomUrl = ConfigAccessor.GetValue<string>("StackExchange.Chat.RoomUrl");

			roomWatcher = new RoomWatcher<DefaultWebSocket>(authCookies, roomUrl);

			roomWatcher.AddMessageCreatedEventHandler(HandleNewMessage);

			roomWatcher.WebSocket.OnError += ex =>
			{
				Console.WriteLine(ex);
			};
		}

		private void HandleNewMessage(Message msg)
		{
			var match = cvplsPattern.Match(msg.Text);

			if (!match.Success)
			{
				return;
			}

			if (!int.TryParse(match.Groups[1].Value, out var questionId))
			{
				return;
			}

			OnNewRequest?.Invoke(msg, questionId);
		}
	}
}
