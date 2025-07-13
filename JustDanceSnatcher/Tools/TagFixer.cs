using JustDanceSnatcher.Helpers;

using System.Text.Json;
using System.Text.Json.Nodes;

namespace JustDanceSnatcher.Tools;
internal class TagFixer
{
	static readonly JsonSerializerOptions jsonSerializerOptions = new()
	{
		WriteIndented = true
	};

	public static void FixTags()
	{
		string inputMapsFolder = Question.AskFolder("Enter the path to your maps folder (containing custom songs): ", true);

		// Loop through all folders in the inputMapsFolder
		foreach (string folder in Directory.GetDirectories(inputMapsFolder))
		{
			// For each, load the songinfo.json file
			string songInfoPath = Path.Combine(folder, "songinfo.json");
			string jsonContent = File.ReadAllText(songInfoPath);
			JsonNode songInfo = JsonNode.Parse(jsonContent)!;

			// get the tags array
			JsonArray tags = (songInfo["tags"] as JsonArray)!;
			string[] tagsArray = [.. tags.Select(tag => tag!.ToString())];

			// If it contains "Custom" and anything starting with "songpack", remove the jdplus tag
			if (tagsArray.Contains("jdplus") &&
				tagsArray.Contains("Custom") && tagsArray.Any(tag => tag.StartsWith("songpack", StringComparison.OrdinalIgnoreCase)))
			{
				// Get the index of "jdplus" tag
				int jdPlusIndex = Array.FindIndex(tagsArray, tag => tag.Equals("jdplus", StringComparison.OrdinalIgnoreCase));
				if (jdPlusIndex < 0)
					continue; // Skip to the next folder if no jdplus tag is found

				// Remove it from the tags array
				tags.RemoveAt(jdPlusIndex);
				Console.WriteLine($"Removed 'jdplus' tag from {Path.GetFileName(folder)}");

				// Save the updated songinfo.json file
				jsonContent = songInfo.ToJsonString(jsonSerializerOptions);
				File.WriteAllText(songInfoPath, jsonContent);
			}
		}
	}
}
