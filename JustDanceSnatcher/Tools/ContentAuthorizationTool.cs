using JustDanceSnatcher.Core;
using JustDanceSnatcher.Helpers;
using JustDanceSnatcher.UbisoftStuff;

using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace JustDanceSnatcher.Tools;

internal static class ContentAuthorizationTool
{
	public static void CreateJson()
	{
		Console.WriteLine("--- Content Authorization JSON Creator ---");
		Console.WriteLine("This tool helps you create a 'contentAuthorization.json' file by ");
		Console.WriteLine("matching missing songs from your UbiArt database with pasted JSON data.\n");

		string ubiartJsonPath = Question.AskFile("Enter path to UbiArt JSON database: ", true);
		string mapsFolder = Question.AskFolder("Enter path to your maps folder (to check existing): ", true);
		string outputJsonFile = "contentAuthorization.json";

		var ubiArtDB = JsonSerializer.Deserialize<Dictionary<string, UbiArtSong>>(File.ReadAllText(ubiartJsonPath), GlobalConfig.JsonOptions)!;
		string[] existingMapNames = Directory.Exists(mapsFolder)
			? [.. Directory.GetDirectories(mapsFolder).Select(s => Path.GetFileName(s)!)] : [];
		UbiArtSong[] missingSongs = [.. ubiArtDB.Values
			.Where(s => !existingMapNames.Contains(s.mapName, StringComparer.OrdinalIgnoreCase))
			.OrderBy(s => s.title)];

		Console.WriteLine($"Found {missingSongs.Length} missing songs to create content authorization for.");
		if (missingSongs.Length == 0)
			return;

		var songToAuthMap = new Dictionary<string, ContentAuthorizationEntry>();
		if (File.Exists(outputJsonFile))
		{
			try
			{
				songToAuthMap = JsonSerializer.Deserialize<Dictionary<string, ContentAuthorizationEntry>>(File.ReadAllText(outputJsonFile), GlobalConfig.JsonOptions) ?? [];
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Warning: Could not load existing '{outputJsonFile}': {ex.Message}.");
			}
		}

		int initialAuthMapCount = songToAuthMap.Count;
		int newEntriesAdded = 0;

		for (int i = 0; i < missingSongs.Length; i++)
		{
			UbiArtSong song = missingSongs[i];
			if (songToAuthMap.ContainsKey(song.mapName))
			{
				// This song was already in the file, so don't count it as a "newly processed" one for the progress indicator.
				Console.WriteLine($"Skipping {song.title} ({song.mapName}), already in '{outputJsonFile}'.");
				continue;
			}

			Console.WriteLine($"\n({newEntriesAdded + 1}/{missingSongs.Length - (initialAuthMapCount - songToAuthMap.Count(kvp => !missingSongs.Any(ms => ms.mapName == kvp.Key)))}) For song: {song.title} ({song.mapName})");
			Console.WriteLine("Paste the ContentAuthorization JSON for this song. Enter a blank line when done, or 'skip' to skip this song.");

			StringBuilder sb = new();
			string? line;
			bool first = true;
			while (!string.IsNullOrWhiteSpace(line = Console.ReadLine()))
			{
				if (first && line.Trim().Equals("skip", StringComparison.OrdinalIgnoreCase))
				{
					sb.Clear();
					break;
				}

				sb.AppendLine(line);
				first = false;
			}

			string jsonInput = sb.ToString();
			if (string.IsNullOrWhiteSpace(jsonInput))
			{
				Console.WriteLine("Skipped.");
				continue;
			}

			try
			{
				ContentAuthorizationEntry? ca = JsonSerializer.Deserialize<ContentAuthorizationEntry>(jsonInput, GlobalConfig.JsonOptions);
				if (ca != null)
				{
					songToAuthMap[song.mapName] = ca;
					File.WriteAllText(outputJsonFile, JsonSerializer.Serialize(songToAuthMap, GlobalConfig.JsonOptions));
					Console.WriteLine($"Saved authorization for {song.mapName}.");
					newEntriesAdded++;
				}
				else
					Console.WriteLine("Failed to parse JSON input.");
			}
			catch (JsonException ex)
			{
				Console.WriteLine($"Invalid JSON for {song.mapName}: {ex.Message}. Not saved.");
			}
		}

		Console.WriteLine("\nContentAuthorization JSON creation process finished.");
	}

	public static async Task DownloadFromContentAuthorizationAsync()
	{
		Console.WriteLine("--- Download from ContentAuthorization Tool ---");
		Console.WriteLine("This tool downloads IPKs and associated assets based on a 'contentAuthorization.json' file.\n");

		string contentAuthPath = Question.AskFile("Enter path to contentAuthorization.json: ", true);
		string dbPath = Question.AskFile("Enter path to UbiArt JSON database: ", true);
		string skuPath = Question.AskFile("Enter path to SkuPackage JSON: ", true);
		string outputRoot = Question.AskFolder("Enter output folder for all downloads: ", false);

		var contentAuthDict = JsonSerializer.Deserialize<Dictionary<string, ContentAuthorizationEntry>>(File.ReadAllText(contentAuthPath), GlobalConfig.JsonOptions)!;
		var ubiArtDB = JsonSerializer.Deserialize<Dictionary<string, UbiArtSong>>(File.ReadAllText(dbPath), GlobalConfig.JsonOptions)!;
		var skuPackageDB = JsonSerializer.Deserialize<Dictionary<string, SkuPackageEntry>>(File.ReadAllText(skuPath), GlobalConfig.JsonOptions)!;
		string tempZipFolder = Path.Combine(outputRoot, "temp_zips_contentauth"); // More specific temp folder name
		Directory.CreateDirectory(tempZipFolder);

		Console.WriteLine("\n--- Stage 1: Downloading IPK archives ---");
		foreach (var pair in contentAuthDict)
		{
			string mapName = pair.Key;
			string ipkOut = Path.Combine(outputRoot, $"{mapName}.ipk");
			if (File.Exists(ipkOut))
			{
				Console.WriteLine($"IPK for '{mapName}' already exists. Skipping.");
				continue;
			}

			if (!ubiArtDB.TryGetValue(mapName, out UbiArtSong? song) || song?.packages?.mapContent == null ||
				!skuPackageDB.TryGetValue(song.packages.mapContent, out SkuPackageEntry? package) || package == null)
			{
				Console.WriteLine($"Warning: SkuPackage info missing for '{mapName}'. Cannot download IPK.");
				continue;
			}

			string zipUrl = package.url;
			string tempZip = Path.Combine(tempZipFolder, $"{mapName}.zip");
			try
			{
				Console.WriteLine($"Downloading ZIP for '{mapName}' from '{zipUrl}'...");
				await Download.DownloadFileAsync(zipUrl, tempZipFolder, $"{mapName}.zip");
				ExtractIpkFile(tempZip, ipkOut);
				File.Delete(tempZip);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error processing IPK for '{mapName}': {ex.Message}");
			}
		}

		if (Directory.Exists(tempZipFolder) && !Directory.EnumerateFileSystemEntries(tempZipFolder).Any())
		{
			Directory.Delete(tempZipFolder);
		}
		else if (Directory.Exists(tempZipFolder))
		{
			Console.WriteLine($"Note: Temporary zip files may remain in '{tempZipFolder}'.");
		}

		Console.WriteLine("\nIPK archives downloaded (if any were pending).");
		Console.WriteLine("IMPORTANT: Please extract all IPK files into their respective folders within your output directory.");
		Console.WriteLine("   (e.g., '{outputRootFolder}/{MapName}/world/maps/{MapName}/...')");
		Console.WriteLine("Press any key to continue to download associated assets (textures, media)...");
		Console.ReadKey();

		Console.WriteLine("\n--- Stage 2: Downloading associated assets ---");
		List<Task> assetTasks = [];
		foreach (var pair in contentAuthDict)
		{
			assetTasks.Add(Task.Run(async () =>
			{
				string mapName = pair.Key;
				ContentAuthorizationEntry auth = pair.Value;
				if (!ubiArtDB.TryGetValue(mapName, out UbiArtSong? song) || song?.assets == null)
					return;

				string mapSpecificOutputBase = Path.Combine(outputRoot, mapName); // Assets go into outputRoot/MapName/
				string menuArtTexturesPath = Path.Combine(mapSpecificOutputBase, "world", "maps", mapName, "menuart", "textures");
				string mediaPath = Path.Combine(mapSpecificOutputBase, "world", "maps", mapName, "media");
				Directory.CreateDirectory(menuArtTexturesPath); // Ensure target dirs exist
				Directory.CreateDirectory(mediaPath);

				var tasks = new List<Task> {
					TryDownloadAssetAsync(song.assets.map_bkgImageUrl, menuArtTexturesPath, $"{mapName}_map_bkg.tga.ckd", mapName),
					TryDownloadAssetAsync(song.assets.coach1ImageUrl, menuArtTexturesPath, $"{mapName}_coach1.tga.ckd", mapName),
					TryDownloadAssetAsync(song.assets.expandCoachImageUrl, menuArtTexturesPath, $"{mapName}_AlbumCoach.tga.ckd", mapName)
				};
				if (!string.IsNullOrEmpty(song.assets.coach2ImageUrl))
					tasks.Add(TryDownloadAssetAsync(song.assets.coach2ImageUrl, menuArtTexturesPath, $"{mapName}_coach2.tga.ckd", mapName));
				if (!string.IsNullOrEmpty(song.assets.coach3ImageUrl))
					tasks.Add(TryDownloadAssetAsync(song.assets.coach3ImageUrl, menuArtTexturesPath, $"{mapName}_coach3.tga.ckd", mapName));
				if (!string.IsNullOrEmpty(song.assets.coach4ImageUrl))
					tasks.Add(TryDownloadAssetAsync(song.assets.coach4ImageUrl, menuArtTexturesPath, $"{mapName}_coach4.tga.ckd", mapName));

				string? oggUrl = auth.urls.FirstOrDefault(u => u.Key.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)).Value;
				if (oggUrl != null)
					tasks.Add(TryDownloadAssetAsync(oggUrl, mediaPath, $"{mapName}.ogg", mapName));
				else
					Console.WriteLine($"Warning: OGG audio URL not found in content auth for '{mapName}'.");

				string? webmUrl = auth.urls.FirstOrDefault(u => u.Key.EndsWith("ULTRA.hd.webm", StringComparison.OrdinalIgnoreCase)).Value;
				if (webmUrl != null)
					tasks.Add(TryDownloadAssetAsync(webmUrl, mediaPath, $"{mapName}_ULTRA.hd.webm", mapName));
				else
					Console.WriteLine($"Warning: ULTRA.hd.webm URL not found in content auth for '{mapName}'.");

				await Task.WhenAll(tasks);
				Console.WriteLine($"Asset download attempts finished for '{mapName}'.");
			}));
		}

		await Task.WhenAll(assetTasks);
		Console.WriteLine("\nAll associated asset downloads attempted.");
	}

	private static async Task TryDownloadAssetAsync(string? url, string folder, string filename, string mapNameForLog)
	{
		if (string.IsNullOrEmpty(url))
			return;
		try
		{
			await Download.DownloadFileAsync(url, folder, filename);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to download '{filename}' for map '{mapNameForLog}': {ex.Message}");
		}
	}

	private static void ExtractIpkFile(string zipFilePath, string ipkDestinationPath)
	{
		using ZipArchive archive = ZipFile.OpenRead(zipFilePath);
		ZipArchiveEntry? ipkEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".ipk", StringComparison.OrdinalIgnoreCase))
			?? throw new FileNotFoundException($"No .ipk file found in ZIP archive '{zipFilePath}'.");
		Console.WriteLine($"Extracting '{ipkEntry.FullName}' from '{Path.GetFileName(zipFilePath)}' to '{Path.GetFileName(ipkDestinationPath)}'...");
		ipkEntry.ExtractToFile(ipkDestinationPath, overwrite: true);
		Console.WriteLine($"Successfully extracted '{Path.GetFileName(ipkDestinationPath)}'.");
	}

	// Nested classes for JSON deserialization specific to this tool
	public class SkuPackageEntry
	{
		public string md5 { get; set; } = "";
		public int storageType { get; set; }
		public string url { get; set; } = "";
		public int version { get; set; }
	}

	public class ContentAuthorizationEntry
	{
		public string __class { get; set; } = "";
		public int duration { get; set; }
		public int changelist { get; set; }
		public Dictionary<string, string> urls { get; set; } = [];
	}
}