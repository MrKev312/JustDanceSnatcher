using JustDanceSnatcher.Helpers;
using JustDanceSnatcher.UbisoftStuff;

using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace JustDanceSnatcher;

class Temp
{
	public static void CreateContentAuthorization()
	{
		// Ask for ubiart json database
		string ubiartJsonPath = Question.AskFile("Enter the path to the UbiArt JSON database: ", true);
		// Ask for server song path
		string output = Question.AskFolder("Enter the path to your maps folder: ", true);

		// Parse the ubiart json database
		Dictionary<string, UbiArtSong> ubiArtDB = JsonSerializer.Deserialize<Dictionary<string, UbiArtSong>>(File.ReadAllText(ubiartJsonPath))!;

		// Get all folder names in the output folder
		string[] folders = Directory.GetDirectories(output).Select(Path.GetFileName).ToArray()!;

		//string[] missing = ubiArtDB.Keys.Except(folders).ToArray();
		UbiArtSong[] missing = ubiArtDB.Values.Where(song => !folders.Contains(song.mapName)).ToArray();

		Console.WriteLine($"Missing songs ({missing.Length}):");

		// Sort it by song name
		UbiArtSong[] sorted = [.. missing.OrderBy(song => song.title)];

		Dictionary<string, ContentAuthorization> songToFolder = [];

		JsonSerializerOptions options = new() { WriteIndented = true };

		if (File.Exists("contentAuthorization.json"))
			songToFolder = JsonSerializer.Deserialize<Dictionary<string, ContentAuthorization>>(File.ReadAllText("contentAuthorization.json"))!;

		// for each song in the ubiart database
		foreach (UbiArtSong song in sorted)
		{
			// Print the title (mapName)
			Console.WriteLine($"{song.title} ({song.mapName}) ({songToFolder.Count}/{missing.Length - 1})");

			// Now keep reading input until a blank line is entered
			string input = Console.ReadLine()!;

			if (input == "skip")
				continue;

			while (true)
			{
				string line = Console.ReadLine()!;

				if (string.IsNullOrWhiteSpace(line))
					break;

				input += line + Environment.NewLine;
			}

			// Parse the input as a ContentAuthorization object
			ContentAuthorization contentAuthorization = JsonSerializer.Deserialize<ContentAuthorization>(input)!;

			// Add the song to the dictionary
			songToFolder[song.mapName] = contentAuthorization;

			// Write just in case
			File.WriteAllText("contentAuthorization.json", JsonSerializer.Serialize(songToFolder, options));
		}
	}

	static readonly HttpClient client = new();

	public static void DownloadFromContentAuthorization()
	{
		// Ask for the path to the contentAuthorization.json file
		string contentAuthorizationPath = Question.AskFile("Enter the path to the contentAuthorization JSON file: ", true);
		// Ask for database path
		string databasePath = Question.AskFile("Enter the path to the UbiArt JSON database: ", true);
		// Ask for SkuPackage path
		string skuPackagePath = Question.AskFile("Enter the path to the SkuPackage JSON file: ", true);

		// Ask for the output folder
		string output = Question.AskFolder("Enter the path to the output folder: ", true);

		// Parse the contentAuthorization.json file
		Dictionary<string, ContentAuthorization> contentAuthorization = JsonSerializer.Deserialize<Dictionary<string, ContentAuthorization>>(File.ReadAllText(contentAuthorizationPath))!;

		// Parse the ubiart json database
		Dictionary<string, UbiArtSong> ubiArtDB = JsonSerializer.Deserialize<Dictionary<string, UbiArtSong>>(File.ReadAllText(databasePath))!;

		// Parse the SkuPackage json file
		Dictionary<string, SkuPackage> skuPackage = JsonSerializer.Deserialize<Dictionary<string, SkuPackage>>(File.ReadAllText(skuPackagePath))!;

		// Loop through each song in the contentAuthorization.json file
		foreach (KeyValuePair<string, ContentAuthorization> pair in contentAuthorization)
		{
			if (File.Exists(Path.Combine(output, $"{pair.Key}.ipk")))
			{
				Console.WriteLine($"Skipping {pair.Key}");
				continue;
			}

			// Get the song and the SkuPackage
			UbiArtSong song = ubiArtDB[pair.Key];
			string mapContent = song.packages.mapContent;
			SkuPackage package = skuPackage[mapContent];
			string url = package.url;

			// Download it in output/temp/{key}.zip
			string temp = Path.Combine(output, "temp", $"{pair.Key}.zip");
			Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
			DownloadFile(url, temp);

			// Extract the file ending in .ipk to output/{key}.ipk
			string ipk = Path.Combine(output, $"{pair.Key}.ipk");
			ExtractIpkFile(temp, ipk);
		}

		// Delete the temp folder
		if (Directory.Exists(Path.Combine(output, "temp")))
			Directory.Delete(Path.Combine(output, "temp"), recursive: true);

		Console.WriteLine("Now simply extract all ipks and press any key to continue");
		Console.ReadKey();

		// Download the needed files for conversion
		//foreach (KeyValuePair<string, ContentAuthorization> pair in contentAuthorization)
		ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = 8 };
		Parallel.ForEach(contentAuthorization, parallelOptions, pair =>
		{
			// Get the song and the authorization
			UbiArtSong song = ubiArtDB[pair.Key];
			ContentAuthorization authorization = pair.Value;

			string basePaths = Path.Combine(output, pair.Key, "world", "maps", pair.Key, "menuart", "textures");
			Directory.CreateDirectory(basePaths);
			DownloadFile(song.assets.map_bkgImageUrl, Path.Combine(basePaths, $"{pair.Key}_map_bkg.tga.ckd"));
			DownloadFile(song.assets.coach1ImageUrl, Path.Combine(basePaths, $"{pair.Key}_coach1.tga.ckd"));
			if (song.assets.coach2ImageUrl != null)
				DownloadFile(song.assets.coach2ImageUrl, Path.Combine(basePaths, $"{pair.Key}_coach2.tga.ckd"));
			if (song.assets.coach3ImageUrl != null)
				DownloadFile(song.assets.coach3ImageUrl, Path.Combine(basePaths, $"{pair.Key}_coach3.tga.ckd"));
			if (song.assets.coach4ImageUrl != null)
				DownloadFile(song.assets.coach4ImageUrl, Path.Combine(basePaths, $"{pair.Key}_coach4.tga.ckd"));
			DownloadFile(song.assets.expandCoachImageUrl, Path.Combine(basePaths, $"{pair.Key}_AlbumCoach.tga.ckd"));

			// Download the content authorization files
			basePaths = Path.Combine(output, pair.Key, "world", "maps", pair.Key, "media");
			// Get the first one ending in .ogg's URL
			string oggUrl = authorization.urls.First(pair => pair.Key.EndsWith(".ogg")).Value;
			DownloadFile(oggUrl, Path.Combine(basePaths, $"{pair.Key}.ogg"));
			// Get the first one ending in ULTRA.hd.webm's URL
			string webmUrl = authorization.urls.First(pair => pair.Key.EndsWith("ULTRA.hd.webm")).Value;
			DownloadFile(webmUrl, Path.Combine(basePaths, $"{pair.Key}_ULTRA.hd.webm"));

			Console.WriteLine($"Downloaded {pair.Key}");
		});
	}

	static void DownloadFile(string url, string output)
	{
		// Create the directory if it doesn't exist
		Directory.CreateDirectory(Path.GetDirectoryName(output)!);

		// If the file exists, skip it
		if (File.Exists(output))
			return;

		// Download the file
		using Stream stream = client.GetStreamAsync(url).Result;
		using FileStream file = File.Create(output);
		stream.CopyTo(file);
	}

	// Function to extract the .ipk file from the .zip
	static void ExtractIpkFile(string zipFilePath, string ipkDestination)
	{
		// Open the zip file
		using ZipArchive archive = ZipFile.OpenRead(zipFilePath);

		// Find the .ipk file in the zip archive
		foreach (ZipArchiveEntry entry in archive.Entries)
		{
			if (!entry.FullName.EndsWith(".ipk", StringComparison.OrdinalIgnoreCase))
				continue;

			// Extract the .ipk file to the destination
			entry.ExtractToFile(ipkDestination, overwrite: true);
			Console.WriteLine($"Extracted {entry.FullName} to {ipkDestination}");
			return;
		}

		throw new Exception("No .ipk file found in the zip archive");
	}

	internal static void FixAudio()
	{
		// Ask for an input maps folder
		string input = Question.AskFolder("Enter the path to the input maps folder: ", true);

		// Get all folder names in the input folder
		string[] folders = Directory.GetDirectories(input).Select(Path.GetFileName).ToArray()!;

		List<(string name, float volume)> toFix = [];

		// for each folder in the input folder
		//foreach (string folder in folders)
		Parallel.ForEach(folders, folder =>
		{
			// Check the SongInfo.json file
			string songInfo = Path.Combine(input, folder, "SongInfo.json");
			if (!File.Exists(songInfo))
				return;

			// Parse the SongInfo.json file
			JDNextDatabaseEntry song = JsonSerializer.Deserialize<JDNextDatabaseEntry>(File.ReadAllText(songInfo))!;

			// If it doesn't have a "Custom" tag, skip it
			if (!song.tags.Contains("Custom"))
				return;

			// Get the Audio_opus subfolder and grab the only file in it
			string audioOpus = Path.Combine(input, folder, "Audio_opus");
			string audioFile = Directory.GetFiles(audioOpus).First();
			float? loudness = GetLoudness(audioFile);

			if (loudness == null)
			{
				Console.WriteLine($"Couldn't extract loudness for {folder}");
				return;
			}

			if (loudness.Value < -16f)
			{
				float diff = -12.2f - loudness.Value;
				// Truncate the decimal part to one digit
				diff = (float)Math.Truncate(diff * 10) / 10;
				Console.WriteLine($"{folder} is too quiet by {diff}");
				toFix.Add((audioFile, diff));
			}

			// Do the same but for the audio preview
			string audioPreview = Path.Combine(input, folder, "AudioPreview_opus");
			string audioPreviewFile = Directory.GetFiles(audioPreview).First();
			float? loudnessPreview = GetLoudness(audioPreviewFile);

			if (loudnessPreview == null)
			{
				Console.WriteLine($"Couldn't extract loudness for {folder}");
				return;
			}

			if (loudnessPreview.Value < -16f)
			{
				float diff = -11.1f - loudnessPreview.Value;
				// Truncate the decimal part to one digit
				diff = (float)Math.Truncate(diff * 10) / 10;
				Console.WriteLine($"{folder} is too quiet by {diff}");
				toFix.Add((audioPreviewFile, diff));
			}
		});

		// Sort the list by name
		toFix.Sort((a, b) => string.Compare(a.name, b.name));

		foreach ((string name, float volume) in toFix)
		{
			Console.WriteLine($"Fixing {name} by {volume}");
			IncreaseVolume(name, volume);
		}
	}

	private static float? GetLoudness(string audioFile)
	{
		string arguments = $"-i \"{audioFile}\" -af ebur128 -f null -";

		// Configure ProcessStartInfo
		ProcessStartInfo startInfo = new()
		{
			FileName = "ffmpeg",
			Arguments = arguments,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};

		Process process = Process.Start(startInfo)!;

		string output = process.StandardError.ReadToEnd();
		process.WaitForExit();
		process.Close();
		process.Dispose();

		float? loudness = ExtractLoudness(output);
		return loudness;
	}

	static float? ExtractLoudness(string output)
	{
		// Find the line with the loudness
		IEnumerable<string> lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.TakeLast(8);

		string? line = lines.FirstOrDefault(line => line.Contains("I:"));

		// If the line is null, return null
		if (line == null)
			return null;

		// Split the line by spaces
		string[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		// Find the part with the loudness
		string loudness = parts[1];

		// Parse the loudness as a float
		return float.Parse(loudness);
	}

	static void IncreaseVolume(string input, float volume)
	{
		// Increase volume by 4 dB using ffmpeg and overwrite the file
		string arguments = $"-i \"{input}\" -af \"volume={volume}dB\" -c:a libopus -f opus -y \"{input}.mod\"";
		ProcessStartInfo startInfo = new()
		{
			FileName = "ffmpeg",
			Arguments = arguments,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};

		Process process = Process.Start(startInfo)!;
		process.WaitForExit();
		process.Close();
		process.Dispose();
		// Rename the new file to the original file
		File.Move($"{input}.mod", input, true);
	}

	public class SkuPackage
	{
		public string md5 { get; set; }
		public int storageType { get; set; }
		public string url { get; set; }
		public int version { get; set; }
	}

	public class ContentAuthorization
	{
		public string __class { get; set; }
		public int duration { get; set; }
		public int changelist { get; set; }
		public Dictionary<string, string> urls { get; set; }
	}
}
