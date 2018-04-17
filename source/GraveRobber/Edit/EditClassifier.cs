using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Parser.Html;

namespace GraveRobber.Edit
{
	public class EditModel
	{
		public int Distance { get; set; }
		public int Length { get; set; }
		public double Normalised { get; set; }
		public double AdjustedNormalised { get; set; }
		public double Code { get; set; }
		public double Formatting { get; set; }

		public string DistancePretty => Distance.ToString("N0");
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
			var txtDiff = CalculateTextDiff(source, target);
			var codeDist = GetCodeDistance(source, target);
			var formattingDist = GetFormattingDistance(source, target);
			var codeDiff = codeDist == 0 
				? 0
				: codeDist * 1.0 / (txtDiff.Distance + codeDist);
			var formattingDiff = formattingDist == 0
				? 0
				: formattingDist * 1.0 / (txtDiff.Distance + formattingDist);

			return new EditModel
			{
				Distance = txtDiff.Distance,
				Length = txtDiff.Length,
				Normalised = txtDiff.Normalised,
				AdjustedNormalised = txtDiff.AdjustedNormalised,
				Code = codeDiff,
				Formatting = formattingDiff
			};
		}



		private static DldResult CalculateTextDiff(string source, string target)
		{
			var sourceTxt = GetRenderedText(source);
			var targetTxt = GetRenderedText(target);

			return DamerauLevenshteinDistance.Calculate(sourceTxt, targetTxt);
		}

		private static int GetCodeDistance(string source, string target)
		{
			var sourceCode = GetCode(source);
			var targetCode = GetCode(target);
			var diff = DamerauLevenshteinDistance.Calculate(sourceCode, targetCode);

			return diff.Distance;
		}

		private static int GetFormattingDistance(string source, string target)
		{
			var sourceTxt = GetFormattedText(source);
			var targetTxt = GetFormattedText(target);

			var diff = DamerauLevenshteinDistance.Calculate(sourceTxt, targetTxt);

			return diff.Distance;
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

				formattedText.Append(element.TextContent);
				formattedText.Append(" ");
			}

			return formattedText.ToString();
		}

		private static string GetCode(string rev)
		{
			var dom = new HtmlParser().Parse(rev);
			var codeElements = dom.QuerySelectorAll("pre code");
			var code = new StringBuilder();

			foreach (var e in codeElements)
			{
				var c = multiWhitespace.Replace(e.TextContent, " ");
				code.Append(c);
				code.Append(" ");
			}

			return code.ToString().Trim();
		}

		private static string GetRenderedText(string rev)
		{
			var dom = new HtmlParser().Parse(rev);

			return multiWhitespace.Replace(dom.Body.TextContent, " ");
		}
	}
}
