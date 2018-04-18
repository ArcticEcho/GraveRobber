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

		public string TotalDistancePretty => TotalDistance.ToString("N0");
		public string NormalisedPretty => $"{Math.Round(Normalised * 100)}%";
		public string AdjustedNormalisedPretty => $"{Math.Round(AdjustedNormalised * 100)}%";
		public string CodePretty => $"{Math.Round(Code * 100)}%";
	}

	public static class EditClassifier
	{
		private static Regex multiWhitespace = new Regex("\\s+",RegexOptions.Compiled | RegexOptions.CultureInvariant);



		public static EditModel Classify(string source, string target)
		{
			var renderedDist = CalculateRenderedTextDistance(source, target, out var len);
			var plainDist = CalculatePlainTextDistance(source, target);
			var codeDist = GetCodeDistance(source, target);
			var totalDist = plainDist + codeDist;

			var codeDiff = codeDist == 0 
				? 0
				: codeDist * 1.0 / totalDist;

			var norm = renderedDist * 1.0 / len;
			var minLen = ConfigAccessor.GetValue<int>("MinLength");
			var adNorm = renderedDist * 1.0 / Math.Max(len, minLen);

			return new EditModel
			{
				TotalDistance = totalDist,
				Normalised = norm,
				AdjustedNormalised = adNorm,
				Code = codeDiff
			};
		}



		private static int CalculateRenderedTextDistance(string source, string target, out int len)
		{
			var sourceTxt = GetRenderedText(source);
			var targetTxt = GetRenderedText(target);
			var dist = DamerauLevenshteinDistance.Calculate(sourceTxt, targetTxt);

			len = Math.Max(sourceTxt.Length, targetTxt.Length);

			return dist;
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

		private static string GetCode(string rev)
		{
			var dom = new HtmlParser().Parse(rev);
			var codeElements = dom.QuerySelectorAll("pre code");
			var code = new StringBuilder();

			foreach (var e in codeElements)
			{
				var c = multiWhitespace.Replace(e.TextContent, "");
				code.Append(c);
			}

			return code.ToString();
		}

		private static string GetRenderedText(string rev)
		{
			var dom = new HtmlParser().Parse(rev);

			return multiWhitespace.Replace(dom.Body.TextContent, "");
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

				var pTxt = multiWhitespace.Replace(p.TextContent, "");
				text.Append(pTxt);
			}

			return text.ToString();
		}
	}
}
