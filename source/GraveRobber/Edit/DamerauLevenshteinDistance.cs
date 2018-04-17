using System;

namespace GraveRobber.Edit
{
	public static class DamerauLevenshteinDistance
	{
		// Adapted from https://stackoverflow.com/a/9454016.

		public static int Calculate(string source, string target)
		{
			var length1 = source.Length;
			var length2 = target.Length;

			// Ensure arrays [i] / length1 use shorter length
			if (length1 > length2)
			{
				Swap(ref target, ref source);
				Swap(ref length1, ref length2);
			}

			var maxi = length1;
			var maxj = length2;

			var dCurrent = new int[maxi + 1];
			var dMinus1 = new int[maxi + 1];
			var dMinus2 = new int[maxi + 1];
			int[] dSwap;

			for (var i = 0; i <= maxi; i++)
			{
				dCurrent[i] = i;
			}

			int jm1 = 0, im1 = 0, im2 = -1;

			for (var j = 1; j <= maxj; j++)
			{
				// Rotate
				dSwap = dMinus2;
				dMinus2 = dMinus1;
				dMinus1 = dCurrent;
				dCurrent = dSwap;

				// Initialize
				var minDistance = int.MaxValue;
				dCurrent[0] = j;
				im1 = 0;
				im2 = -1;

				for (var i = 1; i <= maxi; i++)
				{
					var cost = source[im1] == target[jm1] ? 0 : 1;

					var del = dCurrent[im1] + 1;
					var ins = dMinus1[i] + 1;
					var sub = dMinus1[im1] + cost;

					// Fastest execution for min value of 3 integers
					var min = (del > ins) ? (ins > sub ? sub : ins) : (del > sub ? sub : del);

					if (i > 1 && j > 1 && source[im2] == target[jm1] && source[im1] == target[j - 2])
					{
						min = Math.Min(min, dMinus2[im2] + cost);
					}

					dCurrent[i] = min;

					if (min < minDistance)
					{
						minDistance = min;
					}

					im1++;
					im2++;
				}

				jm1++;
			}

			return dCurrent[maxi];
		}

		private static void Swap<T>(ref T arg1, ref T arg2)
		{
			var temp = arg1;
			arg1 = arg2;
			arg2 = temp;
		}
	}
}
