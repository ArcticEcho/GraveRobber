using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Parser.Html;

namespace GraveRobber.Edit
{
	public class EditModel
	{
		public int TotalDistance { get; set; }
		public double Normalised { get; set; }
		public double AdjustedNormalised { get; set; }
		public double Code { get; set; }
		public double Formatting { get; set; }

		public string TotalDistancePretty => TotalDistance.ToString("N0");
		public string NormalisedPretty => $"{Math.Round(Normalised * 100)}%";
		public string AdjustedNormalisedPretty => $"{Math.Round(AdjustedNormalised * 100)}%";
		public string CodePretty => $"{Math.Round(Code * 100)}%";
		public string FormattingPretty => $"{Math.Round(Formatting * 100)}%";
	}

	public static class EditClassifier
	{
		private static Regex multiWhitespace = new Regex("\\s+",RegexOptions.Compiled | RegexOptions.CultureInvariant);



		public static EditModel Classify(string source, string target)
		{
			var txtDist = CalculatePlainTextDistance(source, target);
			var codeDist = GetCodeDistance(source, target);
			var formattingDist = GetFormattingDistance(source, target);
			var totalDist = txtDist + codeDist + formattingDist;

			var codeDiff = codeDist == 0 
				? 0
				: codeDist * 1.0 / totalDist;

			var formattingDiff = formattingDist == 0
				? 0
				: formattingDist * 1.0 / totalDist;

			var len = Math.Max(source.Length, target.Length);
			var norm = totalDist * 1.0 / len;
			var minLen = ConfigAccessor.GetValue<int>("MinLength");
			var adNorm = totalDist * 1.0 / Math.Max(len, minLen);

			return new EditModel
			{
				TotalDistance = totalDist,
				Normalised = norm,
				AdjustedNormalised = adNorm,
				Code = codeDiff,
				Formatting = formattingDiff
			};
		}



		private static int CalculatePlainTextDistance(string source, string target)
		{
			var sourceTxt = GetPlainText(source);
			var targetTxt = GetPlainText(target);
			var dist = DamerauLevenshteinDistance.Calculate(sourceTxt, targetTxt);

			return dist;
		}

		private static int GetCodeDistance(string source, string target)
		{
			var sourceCode = GetCode(source);
			var targetCode = GetCode(target);
			var dist = DamerauLevenshteinDistance.Calculate(sourceCode, targetCode);

			return dist;
		}

		private static int GetFormattingDistance(string source, string target)
		{
			var sourceTxt = GetFormattedText(source);
			var targetTxt = GetFormattedText(target);

			var dist = DamerauLevenshteinDistance.Calculate(sourceTxt, targetTxt);

			return dist;
		}

		// Excludes code blocks.
		private static string GetFormattedText(string rev)
		{
			var dom = new HtmlParser().Parse(rev);
			var allFormatElements = dom.QuerySelectorAll("pre,code,strong,em,blockquote,img,a");
			var formattedText = new StringBuilder();

			foreach (var element in allFormatElements)
			{
				if (allFormatElements.Any(x => x == element?.ParentElement))
				{
					continue;
				}

				if (element.NodeName == "PRE")
				{
					continue;
				}

				if (element.NodeName == "A")
				{
					formattedText.Append(element.Attributes["href"].Value);
					formattedText.Append(" ");
				}

				if (element.NodeName == "IMG")
				{
					formattedText.Append(element.Attributes["src"].Value);
					formattedText.Append(" ");
				}

				var f = multiWhitespace.Replace(element.TextContent, " ").Trim();
				formattedText.Append(f);
				formattedText.Append(" ");
			}

			return formattedText.ToString().Trim();
		}

		private static string GetCode(string rev)
		{
			var dom = new HtmlParser().Parse(rev);
			var codeElements = dom.QuerySelectorAll("pre code");
			var code = new StringBuilder();

			foreach (var e in codeElements)
			{
				var c = multiWhitespace.Replace(e.TextContent, " ").Trim();
				code.Append(c);
				code.Append(" ");
			}

			return code.ToString().Trim();
		}

		private static string GetPlainText(string rev)
		{
			var dom = new HtmlParser().Parse(rev);
			var txtElements = dom.QuerySelectorAll("p");
			var text = new StringBuilder();

			for (var i = 0; i < txtElements.Length; i++)
			{
				var p = txtElements[i];

				if (p.ParentElement.NodeName != "BODY")
				{
					continue;
				}
				
				while (p.LastElementChild != null)
				{
					p.RemoveChild(p.LastElementChild);
				}

				if (string.IsNullOrEmpty(p.TextContent))
				{
					continue;
				}

				var pTxt = multiWhitespace.Replace(p.TextContent, " ").Trim();
				text.Append(pTxt);
				text.Append(" ");
			}

			return text.ToString().Trim();
		}
	}
}
