using System.Text;
using GraveRobber.Edit;
using GraveRobber.StackExchange.Api;
using StackExchange.Chat;

namespace GraveRobber
{
	public static class ReportBuilder
	{
		private const string projectUrl = "https://github.com/SO-Close-Vote-Reviewers/GraveRobber";
		private const string site = "stackoverflow.com";
		private static readonly string msgUrlBase = $"https://chat.{site}/transcript/message/";

		public static string Build(CloseRequest req, QuestionVotes v, EditModel edit, bool editByOp)
		{
			var sb = new StringBuilder();

			var revsLink = $"https://{site}/posts/{v.Id}/revisions";
			var qLink = $"https://{site}/q/{v.Id}";
			var msgLink = $"{msgUrlBase}{req.MessageId}";

			sb.Append($"[ [GraveRobber]({projectUrl}) ] ");
			sb.Append($"[{edit.NormalisedPretty}]({revsLink} ");
			sb.Append($"\"Adjusted: {edit.AdjustedNormalisedPretty}. ");
			sb.Append($"Distance: {edit.DistancePretty}.\") ");
			sb.Append($"changed{(editByOp ? " (by OP)" : "")}, ");
			sb.Append($"affecting code by {edit.CodePretty} ");
			sb.Append($"and formatting by {edit.FormattingPretty}: ");
			sb.Append($"[question]({qLink}) ");
			sb.Append($"(-{v.Down}/+{v.Up}) ");
			sb.Append($"- [req]({msgLink}) ");

			if (!IgnoreList.Ids.Contains(req.AuthorId))
			{
				var author = new User($"chat.{site}", req.AuthorId);

				sb.Append($"@{author.Username.Replace(" ", "").Trim()}");
			}

			return sb.ToString();
		}
	}
}
