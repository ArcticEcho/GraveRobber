using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GraveRobber
{
	public static class IgnoreList
	{
		private const string filePath = "ignore-list";
		private static object lck;

		public static HashSet<int> Ids { get; private set; }

		static IgnoreList()
		{
			lck = new object();
			Ids = new HashSet<int>();

			if (!File.Exists(filePath))
			{
				return;
			}

			using (var fs = File.Open(filePath, FileMode.Open))
			using (var br = new BinaryReader(fs))
			{
				var idCount = br.ReadInt32();

				for (var i = 0; i < idCount; i++)
				{
					Ids.Add(br.ReadInt32());
				}
			}
		}

		public static void Add(int id)
		{
			if (!Ids.Add(id))
			{
				return;
			}

			WriteIds();
		}

		public static void Remove(int id)
		{
			if (!Ids.Remove(id))
			{
				return;
			}

			WriteIds();
		}

		private static void WriteIds()
		{
			lock (lck)
			{
				var idsCopy = Ids.ToArray();

				using (var fs = File.Open(filePath, FileMode.OpenOrCreate))
				using (var bw = new BinaryWriter(fs))
				{
					bw.Write(idsCopy.Length);

					foreach (var id in idsCopy)
					{
						bw.Write(id);
					}
				}
			}
		}
	}
}
