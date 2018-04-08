using System;

namespace GraveRobber.StackExchange.Api
{
	public class Revision
	{
		public int QuestionId { get; set; }

		public int AuthorId { get; set; }

		public DateTime CreatedAt { get; set; }

		public string Body { get; set; }
	}
}
