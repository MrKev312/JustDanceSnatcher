using JustDanceSnatcher.Helpers;
using JustDanceSnatcher.UbisoftStuff; // For UbiArtSong, ContentAuthorization, SkuPackage

using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace JustDanceSnatcher;

internal static class Temp
{
	public static void CreateContentAuthorization()
	{
		string ubiartJsonPath = Question.AskFile("Enter the path to the UbiArt JSON database: ", true);
		string mapsFolder = Question.AskFolder("Enter the path to your maps folder (to check existing): ", true);
		string outputJsonFile = "contentAuthorization.json"; // Define output filename

		Dictionary<string, UbiArtSong> ubiArtDB = JsonSerializer.Deserialize<Dictionary<string, UbiArtSong>>(File.ReadAllText(ubiartJsonPath), Program.jsonOptions)!;
		string[] existingMapNames = Directory.Exists(mapsFolder)
			? [.. Directory.GetDirectories(mapsFolder).Select(s => Path.GetFileName(s)!)]
			: [];

		UbiArtSong[] missingSongs = [.. ubiArtDB.Values
			.Where(song => !existingMapNames.Contains(song.mapName, StringComparer.OrdinalIgnoreCase))
			.OrderBy(song => song.title)];

		Console.WriteLine($"Found {missingSongs.Length} missing songs to create content authorization for.");
		if (missingSongs.Length == 0)
			return;

		Dictionary<string, ContentAuthorization> songToAuthMap = [];
		if (File.Exists(outputJsonFile))
		{
			try
			{
				songToAuthMap = JsonSerializer.Deserialize<Dictionary<string, ContentAuthorization>>(File.ReadAllText(outputJsonFile), Program.jsonOptions) ?? [];
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Warning: Could not load existing '{outputJsonFile}': {ex.Message}. Starting fresh.");
			}
		}

		int processedCount = 0;
		foreach (UbiArtSong song in missingSongs)
		{
			if (songToAuthMap.ContainsKey(song.mapName))
			{
				Console.WriteLine($"Skipping {song.title} ({song.mapName}) as it's already in '{outputJsonFile}'.");
				processedCount++;
				continue;
			}

			Console.WriteLine($"\n({processedCount + 1}/{missingSongs.Length}) For song: {song.title} ({song.mapName})");
			Console.WriteLine("Paste the ContentAuthorization JSON for this song. Enter a blank line when done, or 'skip' to skip this song.");

			StringBuilder jsonInputBuilder = new();
			string? line;
			bool firstLine = true;
			while (!string.IsNullOrWhiteSpace(line = Console.ReadLine()))
			{
				if (firstLine && line.Trim().Equals("skip", StringComparison.OrdinalIgnoreCase))
				{
					jsonInputBuilder.Clear(); // Ensure it's empty
					break;
				}

				jsonInputBuilder.AppendLine(line);
				firstLine = false;
			}

			string jsonInput = jsonInputBuilder.ToString();
			if (string.IsNullOrWhiteSpace(jsonInput))
			{
				Console.WriteLine("Skipped.");
				continue; // Skip if input was 'skip' or empty
			}

			try
			{
				ContentAuthorization? contentAuth = JsonSerializer.Deserialize<ContentAuthorization>(jsonInput, Program.jsonOptions);
				if (contentAuth != null)
				{
					songToAuthMap[song.mapName] = contentAuth;
					File.WriteAllText(outputJsonFile, JsonSerializer.Serialize(songToAuthMap, Program.jsonOptions));
					Console.WriteLine($"Saved authorization for {song.mapName}.");
					processedCount++;
				}
				else
				{
					Console.WriteLine("Failed to parse JSON input. Please try again or check format.");
					// To retry, this loop would need adjustment, or user manually re-runs for this song.
					// For now, it moves to next song.
				}
			}
			catch (JsonException ex)
			{
				Console.WriteLine($"Invalid JSON: {ex.Message}. Authorization for {song.mapName} not saved.");
			}
		}

		Console.WriteLine("ContentAuthorization creation process finished.");
	}

	// Made async to use awaitable DownloadFileAsync
	public static async Task DownloadFromContentAuthorizationAsync()
	{
		string contentAuthPath = Question.AskFile("Enter the path to the contentAuthorization JSON file: ", true);
		string databasePath = Question.AskFile("Enter the path to the UbiArt JSON database: ", true);
		string skuPackagePath = Question.AskFile("Enter the path to the SkuPackage JSON file: ", true);
		string outputRootFolder = Question.AskFolder("Enter the path to the output folder for downloads: ", false); // Create if not exists

		Dictionary<string, ContentAuthorization> contentAuthDict = JsonSerializer.Deserialize<Dictionary<string, ContentAuthorization>>(File.ReadAllText(contentAuthPath), Program.jsonOptions)!;
		Dictionary<string, UbiArtSong> ubiArtDB = JsonSerializer.Deserialize<Dictionary<string, UbiArtSong>>(File.ReadAllText(databasePath), Program.jsonOptions)!;
		Dictionary<string, SkuPackage> skuPackageDB = JsonSerializer.Deserialize<Dictionary<string, SkuPackage>>(File.ReadAllText(skuPackagePath), Program.jsonOptions)!;

		string tempZipFolder = Path.Combine(outputRootFolder, "temp_zips");
		Directory.CreateDirectory(tempZipFolder);

		Console.WriteLine("--- Downloading IPK archives ---");
		foreach (KeyValuePair<string, ContentAuthorization> pair in contentAuthDict)
		{
			string mapName = pair.Key;
			string ipkOutputPath = Path.Combine(outputRootFolder, $"{mapName}.ipk");

			if (File.Exists(ipkOutputPath))
			{
				Console.WriteLine($"IPK for '{mapName}' already exists. Skipping download.");
				continue;
			}

			if (!ubiArtDB.TryGetValue(mapName, out UbiArtSong? song) || song == null)
			{
				Console.WriteLine($"Warning: Song '{mapName}' not found in UbiArt DB. Skipping.");
				continue;
			}

			if (string.IsNullOrEmpty(song.packages?.mapContent) || !skuPackageDB.TryGetValue(song.packages.mapContent, out SkuPackage? package) || package == null)
			{
				Console.WriteLine($"Warning: SkuPackage info missing for '{mapName}'. Cannot download IPK. MapContent: {song.packages?.mapContent}");
				continue;
			}

			string zipUrl = package.url;
			string tempZipPath = Path.Combine(tempZipFolder, $"{mapName}.zip");

			try
			{
				Console.WriteLine($"Downloading ZIP for '{mapName}' from '{zipUrl}'...");
				await Download.DownloadFileAsync(zipUrl, tempZipFolder, $"{mapName}.zip"); // Downloads to temp_zips/MapName.zip
				ExtractIpkFile(tempZipPath, ipkOutputPath);
				File.Delete(tempZipPath); // Clean up zip after extraction
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error processing IPK for '{mapName}': {ex.Message}");
			}
		}

		if (Directory.Exists(tempZipFolder) && !Directory.EnumerateFileSystemEntries(tempZipFolder).Any())
		{
			Directory.Delete(tempZipFolder); // Clean up temp_zips folder if empty
		}
		else if (Directory.Exists(tempZipFolder))
		{
			Console.WriteLine($"Temporary zip files may remain in '{tempZipFolder}'.");
		}


		Console.WriteLine("\nIPK archives downloaded. Please extract all IPKs into their respective folders (e.g., outputFolder/MapName/...).");
		Console.WriteLine("Press any key to continue to download associated assets (textures, media)...");
		Console.ReadKey();

		Console.WriteLine("\n--- Downloading associated assets ---");
		List<Task> assetDownloadTasks = [];
		ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = 8 };

		// Changed to regular foreach with Task list for better error propagation with await Task.WhenAll
		foreach (var pair in contentAuthDict)
		{
			assetDownloadTasks.Add(Task.Run(async () => // Wrap in Task.Run for parallelism with async calls
			{
				string mapName = pair.Key;
				ContentAuthorization auth = pair.Value;

				if (!ubiArtDB.TryGetValue(mapName, out UbiArtSong? song) || song == null)
					return; // Already warned above

				string mapOutputBase = Path.Combine(outputRootFolder, mapName); // Assets go into outputFolder/MapName/
				string menuArtTexturesPath = Path.Combine(mapOutputBase, "world", "maps", mapName, "menuart", "textures");
				string mediaPath = Path.Combine(mapOutputBase, "world", "maps", mapName, "media");

				Directory.CreateDirectory(menuArtTexturesPath);
				Directory.CreateDirectory(mediaPath);

				List<Task> currentMapTasks = [];

				// Texture assets
				if (song.assets != null)
				{
					currentMapTasks.Add(TryDownloadAsync(song.assets.map_bkgImageUrl, menuArtTexturesPath, $"{mapName}_map_bkg.tga.ckd"));
					currentMapTasks.Add(TryDownloadAsync(song.assets.coach1ImageUrl, menuArtTexturesPath, $"{mapName}_coach1.tga.ckd"));
					if (!string.IsNullOrEmpty(song.assets.coach2ImageUrl))
						currentMapTasks.Add(TryDownloadAsync(song.assets.coach2ImageUrl, menuArtTexturesPath, $"{mapName}_coach2.tga.ckd"));
					if (!string.IsNullOrEmpty(song.assets.coach3ImageUrl))
						currentMapTasks.Add(TryDownloadAsync(song.assets.coach3ImageUrl, menuArtTexturesPath, $"{mapName}_coach3.tga.ckd"));
					if (!string.IsNullOrEmpty(song.assets.coach4ImageUrl))
						currentMapTasks.Add(TryDownloadAsync(song.assets.coach4ImageUrl, menuArtTexturesPath, $"{mapName}_coach4.tga.ckd"));
					currentMapTasks.Add(TryDownloadAsync(song.assets.expandCoachImageUrl, menuArtTexturesPath, $"{mapName}_AlbumCoach.tga.ckd"));
				}

				// Media assets from content authorization
				string? oggUrl = auth.urls.FirstOrDefault(u => u.Key.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)).Value;
				if (oggUrl != null)
					currentMapTasks.Add(TryDownloadAsync(oggUrl, mediaPath, $"{mapName}.ogg"));
				else
					Console.WriteLine($"Warning: OGG audio URL not found for '{mapName}'.");

				string? webmUrl = auth.urls.FirstOrDefault(u => u.Key.EndsWith("ULTRA.hd.webm", StringComparison.OrdinalIgnoreCase)).Value;
				if (webmUrl != null)
					currentMapTasks.Add(TryDownloadAsync(webmUrl, mediaPath, $"{mapName}_ULTRA.hd.webm"));
				else
					Console.WriteLine($"Warning: ULTRA.hd.webm URL not found for '{mapName}'.");

				await Task.WhenAll(currentMapTasks);
				Console.WriteLine($"Finished asset downloads for '{mapName}'.");
			}));
		}

		await Task.WhenAll(assetDownloadTasks);
		Console.WriteLine("All asset downloads attempted.");
	}

	private static async Task TryDownloadAsync(string? url, string folder, string filename)
	{
		if (string.IsNullOrEmpty(url))
			return;
		try
		{
			await Download.DownloadFileAsync(url, folder, filename);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to download '{filename}' from '{url}': {ex.Message}");
		}
	}

	private static void ExtractIpkFile(string zipFilePath, string ipkDestinationPath)
	{
		using ZipArchive archive = ZipFile.OpenRead(zipFilePath);
		ZipArchiveEntry? ipkEntry = archive.Entries.FirstOrDefault(entry => entry.FullName.EndsWith(".ipk", StringComparison.OrdinalIgnoreCase))
			?? throw new FileNotFoundException($"No .ipk file found in ZIP archive '{zipFilePath}'.");
		Console.WriteLine($"Extracting '{ipkEntry.FullName}' to '{ipkDestinationPath}'...");
		ipkEntry.ExtractToFile(ipkDestinationPath, overwrite: true);
		Console.WriteLine($"Extracted successfully.");
	}

	public static void FixAudio()
	{
		string inputMapsFolder = Question.AskFolder("Enter the path to the input maps folder (for audio fixing): ", true);
		string[] mapFolderPaths = Directory.GetDirectories(inputMapsFolder);

		List<(string audioFilePath, float targetGainDb)> filesToFix = [];

		Parallel.ForEach(mapFolderPaths, mapFolderPath =>
		{
			string mapName = Path.GetFileName(mapFolderPath);
			string songInfoPath = Path.Combine(mapFolderPath, "SongInfo.json");
			if (!File.Exists(songInfoPath))
				return;

			JDNextDatabaseEntry? song = JsonSerializer.Deserialize<JDNextDatabaseEntry>(File.ReadAllText(songInfoPath), Program.jsonOptions);
			if (song == null || !song.tags.Contains("Custom", StringComparer.OrdinalIgnoreCase))
				return;

			ProcessAudioFileForFixing(Path.Combine(mapFolderPath, "Audio_opus"), mapName, -12.2f, filesToFix);
			ProcessAudioFileForFixing(Path.Combine(mapFolderPath, "AudioPreview_opus"), mapName + " (Preview)", -11.1f, filesToFix);
		});

		filesToFix.Sort((a, b) => string.Compare(a.audioFilePath, b.audioFilePath, StringComparison.OrdinalIgnoreCase));

		if (filesToFix.Count > 0)
		{
			Console.WriteLine($"\nFound {filesToFix.Count} audio files needing volume adjustment:");
			foreach (var (filePath, gain) in filesToFix)
			{
				Console.WriteLine($"- '{Path.GetFileName(Path.GetDirectoryName(filePath))}/{Path.GetFileName(filePath)}' needs {gain:F1} dB gain.");
			}

			Console.Write("Proceed with fixing? (y/n): ");
			if (Console.ReadLine()?.Trim().ToLowerInvariant() != "y")
			{
				Console.WriteLine("Audio fixing aborted by user.");
				return;
			}

			foreach (var (filePath, gain) in filesToFix)
			{
				Console.WriteLine($"Fixing '{filePath}' by {gain:F1} dB...");
				IncreaseVolume(filePath, gain);
			}

			Console.WriteLine("Audio fixing process completed.");
		}
		else
		{
			Console.WriteLine("No audio files found requiring volume adjustment based on current criteria.");
		}
	}

	private static void ProcessAudioFileForFixing(string audioFolder, string logName, float targetIntegratedLoudness, List<(string, float)> filesToFix)
	{
		if (!Directory.Exists(audioFolder))
			return;
		string? audioFile = Directory.GetFiles(audioFolder).FirstOrDefault();
		if (audioFile == null)
			return;

		float? currentLoudness = GetIntegratedLoudness(audioFile);
		if (currentLoudness == null)
		{
			Console.WriteLine($"Could not determine loudness for '{logName}'. Skipping.");
			return;
		}

		// Original criteria: if loudness < -16 LUFS
		if (currentLoudness.Value < -16.0f)
		{
			float gainNeeded = targetIntegratedLoudness - currentLoudness.Value;
			// Round to one decimal place
			gainNeeded = (float)Math.Round(gainNeeded, 1);

			if (Math.Abs(gainNeeded) > 0.05) // Only add if meaningful gain is needed
			{
				Console.WriteLine($"'{logName}' current loudness: {currentLoudness.Value:F1} LUFS. Target: {targetIntegratedLoudness} LUFS. Calculated gain: {gainNeeded:F1} dB.");
				lock (filesToFix)
				{
					filesToFix.Add((audioFile, gainNeeded));
				}
			}
		}
	}

	private static float? GetIntegratedLoudness(string audioFilePath)
	{
		// EBU R128 integrated loudness (I value)
		string arguments = $"-nostats -i \"{audioFilePath}\" -af ebur128=framelog=verbose -f null -";
		ProcessStartInfo startInfo = new()
		{
			FileName = "ffmpeg",
			Arguments = arguments,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};

		try
		{
			using Process process = Process.Start(startInfo)!;
			string ffmpegOutput = process.StandardError.ReadToEnd(); // EBU R128 summary is on stderr
			process.WaitForExit();

			// Example relevant line: Integrated loudness: I: -23.0 LUFS
			string[] lines = ffmpegOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
			string? loudnessLine = lines.LastOrDefault(line => line.Contains("Integrated loudness:") && line.Contains("LUFS"));

			if (loudnessLine != null)
			{
				int iMarker = loudnessLine.IndexOf("I:") + 2; // Start after "I: "
				int lufsMarker = loudnessLine.IndexOf("LUFS", iMarker);
				if (iMarker > 1 && lufsMarker > iMarker)
				{
					string loudnessValueStr = loudnessLine.Substring(iMarker, lufsMarker - iMarker).Trim();
					if (float.TryParse(loudnessValueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float loudnessValue))
					{
						return loudnessValue;
					}
				}
			}

			Console.WriteLine($"Could not parse integrated loudness from FFmpeg output for '{Path.GetFileName(audioFilePath)}'.");
			// Console.WriteLine($"FFMPEG Output: {ffmpegOutput}"); // For debugging
			return null;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error running FFmpeg for loudness check on '{Path.GetFileName(audioFilePath)}': {ex.Message}");
			return null;
		}
	}

	private static void IncreaseVolume(string audioFilePath, float gainDb)
	{
		string tempOutputFile = $"{audioFilePath}.tmp.opus"; // FFmpeg prefers different input/output files
		string arguments = $"-i \"{audioFilePath}\" -af \"volume={gainDb:F1}dB\" -c:a libopus -y \"{tempOutputFile}\"";
		ProcessStartInfo startInfo = new()
		{
			FileName = "ffmpeg",
			Arguments = arguments,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};

		try
		{
			using Process process = Process.Start(startInfo)!;
			// string output = process.StandardError.ReadToEnd(); // For debugging if needed
			process.WaitForExit();
			if (process.ExitCode != 0)
			{
				// Console.WriteLine($"FFmpeg error for '{Path.GetFileName(audioFilePath)}':\n{output}");
				throw new Exception($"FFmpeg process exited with code {process.ExitCode} for {Path.GetFileName(audioFilePath)}.");
			}

			File.Delete(audioFilePath);
			File.Move(tempOutputFile, audioFilePath);
			Console.WriteLine($"Volume adjusted for '{Path.GetFileName(audioFilePath)}'.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error increasing volume for '{Path.GetFileName(audioFilePath)}': {ex.Message}");
			if (File.Exists(tempOutputFile))
				try
				{
					File.Delete(tempOutputFile); 
				} catch { }
		}
	}

	// SkuPackage and ContentAuthorization classes remain unchanged.
	public class SkuPackage
	{
		public string md5 { get; set; } = "";
		public int storageType { get; set; }
		public string url { get; set; } = "";
		public int version { get; set; }
	}

	public class ContentAuthorization
	{
		public string __class { get; set; } = "";
		public int duration { get; set; }
		public int changelist { get; set; }
		public Dictionary<string, string> urls { get; set; } = [];
	}
}