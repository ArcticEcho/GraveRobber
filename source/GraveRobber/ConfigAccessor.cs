using System.IO;
using Newtonsoft.Json.Linq;

namespace GraveRobber
{
	public static class ConfigAccessor
	{
		public static string ConfigFile { get; set; } = "config.json";

		public static T GetValue<T>(string path)
		{
			var token = GetToken(path);

			if (token == null)
			{
				return default(T);
			}

			return token.Value<T>();
		}



		private static JToken GetToken(string path)
		{
			var json = File.ReadAllText(ConfigFile);
			var obj = JObject.Parse(json);

			return obj.SelectToken(path);
		}
	}
}
