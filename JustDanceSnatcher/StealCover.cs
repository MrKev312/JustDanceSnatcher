using JustDanceSnatcher.Helpers;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JustDanceSnatcher;
internal class StealCover
{
	public static void Run()
	{
		// Ask for the path to the maps
		string mapsPath = Question.AskFolder("Enter the path to your maps folder: ", true);
		string coverPath = Question.AskFolder("Enter the path to your cover folder: ", true);

		IEnumerable<string> maps = Directory.GetDirectories(mapsPath)
			//.Except(Directory.GetDirectories(coverPath))
			.Where(x => !Directory.Exists(Path.Combine(coverPath, Path.GetFileName(x)!)))
			// Only if in that folder is a songTitleLogo folder
			.Where(x => Directory.Exists(Path.Combine(x, "songTitleLogo")))
			// Only if we're a JD+ or Custom map
			.Where(x =>
			{
				JsonObject mapInfo = JsonSerializer.Deserialize<JsonObject>(File.ReadAllText(Path.Combine(x, "SongInfo.json")))!;

				// Get the originalJDVersion property
				if (mapInfo.TryGetPropertyValue("originalJDVersion", out JsonNode? originalJustDanceVersionNode))
				{
					// Get the value of the property
					int originalJustDanceVersion = originalJustDanceVersionNode!.GetValue<int>();
					// Check if the value is less than 2023
					if (originalJustDanceVersion < 2023)
						return true;
				}

				// Or Custom is in the tags array
				if (mapInfo.TryGetPropertyValue("tags", out JsonNode? tagsNode))
				{
					// Get the value of the property
					IEnumerable<string> tags = tagsNode!.AsArray().Select(x => x!.ToString());
					// Check if the value is less than 2023
					if (tags.Contains("Custom"))
						return true;
				}

				return false;
			});

		Console.WriteLine($"Found {maps.Count()} maps to steal covers from.");

		foreach (string map in maps)
		{
			// Get the name of the map folder
			string mapName = Path.GetFileName(map)!;

			Console.WriteLine(mapName);
		}

		Console.WriteLine("Wait until after you've extracted them all to a folder.");
		string extractedPath = Question.AskFolder("Enter the path to your extracted covers folder: ", true);

		// They're now in {songName}_Cover_2x.png and {songName}_Title.png
		string[] images = Directory.GetFiles(extractedPath);
		foreach (string image in images)
		{
			// Get the name
			string[] parts = Path.GetFileNameWithoutExtension(image)!.Split('_');
			// Get the name of the map folder
			string mapName = parts[0];
			string coverType = parts[1];

			string coverFolder = Path.Combine(coverPath, mapName);
			Directory.CreateDirectory(coverFolder);
			string coverFile = Path.Combine(coverFolder, $"{coverType}.png");
			File.Copy(image, coverFile, true);
		}
	}
}
