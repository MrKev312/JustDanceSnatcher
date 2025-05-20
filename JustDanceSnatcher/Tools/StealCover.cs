using JustDanceSnatcher.Helpers;

using System.Text.Json;
using System.Text.Json.Nodes;

namespace JustDanceSnatcher.Tools;

internal static class StealCover
{
	public static void Run()
	{
		Console.WriteLine("--- Cover Stealing Utility ---");
		string mapsPath = Question.AskFolder("Enter path to game's maps folder: ", true);
		string coverOutputPath = Question.AskFolder("Enter path for cover output: ", true);

		var mapsToConsider = Directory.GetDirectories(mapsPath)
			.Where(mapDir => !Directory.Exists(Path.Combine(coverOutputPath, Path.GetFileName(mapDir)!)) &&
							 Directory.Exists(Path.Combine(mapDir, "songTitleLogo")))
			.Where(mapDir =>
			{
				string songInfoPath = Path.Combine(mapDir, "SongInfo.json");
				if (!File.Exists(songInfoPath))
					return false;
				try
				{
					JsonObject? mapInfo = JsonSerializer.Deserialize<JsonObject>(File.ReadAllText(songInfoPath));
					if (mapInfo == null)
						return false;
					if (mapInfo.TryGetPropertyValue("originalJDVersion", out JsonNode? v) && v!.GetValue<int>() < 2023)
						return true;
					if (mapInfo.TryGetPropertyValue("tags", out JsonNode? t) && t!.AsArray().Select(n => n?.ToString() ?? "").Contains("Custom", StringComparer.OrdinalIgnoreCase))
						return true;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error reading SongInfo for {Path.GetFileName(mapDir)}: {ex.Message}");
				}

				return false;
			}).ToList();

		Console.WriteLine($"Found {mapsToConsider.Count} maps eligible for cover stealing.");
		if (mapsToConsider.Count == 0)
			return;
		mapsToConsider.ForEach(mapDir => Console.WriteLine($"- {Path.GetFileName(mapDir)}"));

		Console.WriteLine("\nIMPORTANT: Manually extract covers from 'songTitleLogo' folders to PNGs.");
		Console.WriteLine("Expected filenames: '{MapName}_Cover_2x.png' and '{MapName}_Title.png'.");
		string extractedCoversPath = Question.AskFolder("Enter path to extracted PNGs folder: ", true);

		string[] extractedImageFiles = Directory.GetFiles(extractedCoversPath, "*.png");
		if (extractedImageFiles.Length == 0)
		{
			Console.WriteLine("No PNGs in extracted folder.");
			return;
		}

		Console.WriteLine($"Processing {extractedImageFiles.Length} extracted images...");
		foreach (string imageFile in extractedImageFiles)
		{
			string[] parts = Path.GetFileNameWithoutExtension(imageFile).Split('_');
			if (parts.Length < 2)
			{
				Console.WriteLine($"Skipping '{imageFile}': unrecognized format.");
				continue;
			}

			string mapName = parts[0];
			string coverType = parts[1];
			string targetMapCoverFolder = Path.Combine(coverOutputPath, mapName);
			Directory.CreateDirectory(targetMapCoverFolder);
			string targetCoverFile = Path.Combine(targetMapCoverFolder, $"{coverType}.png");
			try
			{
				File.Copy(imageFile, targetCoverFile, true);
				Console.WriteLine($"Copied '{Path.GetFileName(imageFile)}' to '{targetCoverFile}'.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error copying '{imageFile}': {ex.Message}");
			}
		}

		Console.WriteLine("Cover stealing finished.");
	}
}