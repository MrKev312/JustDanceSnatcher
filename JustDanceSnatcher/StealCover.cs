using JustDanceSnatcher.Helpers;

using System.Text.Json;
using System.Text.Json.Nodes;

namespace JustDanceSnatcher;

internal static class StealCover
{
	public static void Run()
	{
		Console.WriteLine("--- Cover Stealing Utility ---");
		string mapsPath = Question.AskFolder("Enter the path to your game's maps folder: ", true);
		string coverOutputPath = Question.AskFolder("Enter the path to your cover output folder (where new covers will be saved): ", true);

		var mapsToConsider = Directory.GetDirectories(mapsPath)
			.Where(mapDir => !Directory.Exists(Path.Combine(coverOutputPath, Path.GetFileName(mapDir)!))) // Not already processed
			.Where(mapDir => Directory.Exists(Path.Combine(mapDir, "songTitleLogo"))) // Must have songTitleLogo folder
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

					if (mapInfo.TryGetPropertyValue("originalJDVersion", out JsonNode? versionNode) &&
						versionNode!.GetValue<int>() < 2023) // Older than JD2023
					{
						return true;
					}

					if (mapInfo.TryGetPropertyValue("tags", out JsonNode? tagsNode))
					{
						var tags = tagsNode!.AsArray().Select(n => n?.ToString() ?? "");
						if (tags.Contains("Custom", StringComparer.OrdinalIgnoreCase)) // Is custom
						{
							return true;
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error reading SongInfo for {Path.GetFileName(mapDir)}: {ex.Message}");
					return false;
				}

				return false;
			})
			.ToList(); // ToList to count and iterate

		Console.WriteLine($"Found {mapsToConsider.Count} maps with 'songTitleLogo' eligible for cover stealing (JD+ or Custom, not yet in output).");
		if (mapsToConsider.Count == 0)
			return;

		Console.WriteLine("Eligible maps:");
		foreach (string mapDir in mapsToConsider)
		{
			Console.WriteLine($"- {Path.GetFileName(mapDir)}");
		}

		Console.WriteLine("\nIMPORTANT: You need to manually extract the covers from these maps' 'songTitleLogo' folders.");
		Console.WriteLine("Typically, these are texture files that need conversion to PNG (e.g., using a UbiArt texture tool).");
		Console.WriteLine("The expected extracted filenames are '{MapName}_Cover_2x.png' and '{MapName}_Title.png'.");
		string extractedCoversPath = Question.AskFolder("Enter the path to the folder where you've put these extracted PNGs: ", true);

		string[] extractedImageFiles = Directory.GetFiles(extractedCoversPath, "*.png");
		if (extractedImageFiles.Length == 0)
		{
			Console.WriteLine("No PNG files found in the extracted covers folder.");
			return;
		}

		Console.WriteLine($"Processing {extractedImageFiles.Length} extracted image files...");
		foreach (string imageFile in extractedImageFiles)
		{
			string fileNameWithoutExt = Path.GetFileNameWithoutExtension(imageFile);
			string[] parts = fileNameWithoutExt.Split('_'); // Expects MapName_Cover_2x or MapName_Title

			if (parts.Length < 2)
			{
				Console.WriteLine($"Skipping '{imageFile}': Filename format not recognized (expected MapName_Type_Optional.png).");
				continue;
			}

			string mapName = parts[0];
			// Determine cover type (e.g., "Cover", "Title")
			// So for "MapName_Cover_2x.png", this becomes "Cover.png" in the output.
			// And for "MapName_Title.png", this becomes "Title.png" in the output.
			string coverType = parts[1];

			string targetMapCoverFolder = Path.Combine(coverOutputPath, mapName);
			Directory.CreateDirectory(targetMapCoverFolder);

			// Output filename based on the "type" part (e.g., Cover.png, Title.png)
			string targetCoverFile = Path.Combine(targetMapCoverFolder, $"{coverType}.png");

			try
			{
				File.Copy(imageFile, targetCoverFile, true); // Overwrite if exists
				Console.WriteLine($"Copied '{Path.GetFileName(imageFile)}' to '{targetCoverFile}'.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error copying '{imageFile}' to '{targetCoverFile}': {ex.Message}");
			}
		}

		Console.WriteLine("Cover stealing process finished.");
	}
}