using JustDanceSnatcher.Helpers;
using JustDanceSnatcher.UbisoftStuff;
using System.Text.Json;
using DSharpPlus.Entities;

namespace JustDanceSnatcher;

// Note: This class is no longer static. Instantiate it to use.
internal class JDNextDownloader : DiscordAssetDownloaderBase<KeyValuePair<string, JDNextDatabaseEntry>, JDNextDiscordEmbed>
{
	private Dictionary<string, JDNextDatabaseEntry> _jDCacheJson = [];
	private Dictionary<string, string> _jdBundleTags = []; // Stores songpack tags for redownloaded official maps

	// Mappings for EmbedParserHelper
	private static readonly IReadOnlyDictionary<string, string> ImageUrlFieldMap = new Dictionary<string, string>
	{
		{"coachesSmall:", nameof(ImageURLs.coachesSmall)}, {"coachesLarge:", nameof(ImageURLs.coachesLarge)},
		{"Cover:", nameof(ImageURLs.Cover)}, {"Cover1024:", nameof(ImageURLs.Cover1024)},
		{"CoverSmall:", nameof(ImageURLs.CoverSmall)}, {"Song Title Logo:", nameof(ImageURLs.SongTitleLogo)}
	};
	private static readonly IReadOnlyDictionary<string, string> PreviewUrlFieldMap = new Dictionary<string, string>
	{
		{"audioPreview.opus:", nameof(PreviewURLs.audioPreview)}, {"HIGH.vp8.webm:", nameof(PreviewURLs.HIGHvp8)},
		{"HIGH.vp9.webm:", nameof(PreviewURLs.HIGHvp9)}, {"LOW.vp8.webm:", nameof(PreviewURLs.LOWvp8)},
		{"LOW.vp9.webm:", nameof(PreviewURLs.LOWvp9)}, {"MID.vp8.webm:", nameof(PreviewURLs.MIDvp8)},
		{"MID.vp9.webm:", nameof(PreviewURLs.MIDvp9)}, {"ULTRA.vp8.webm:", nameof(PreviewURLs.ULTRAvp8)},
		{"ULTRA.vp9.webm:", nameof(PreviewURLs.ULTRAvp9)}
	};
	private static readonly IReadOnlyDictionary<string, string> ContentUrlFieldMap = new Dictionary<string, string>
	{
		{"Ultra HD:", nameof(ContentURLs.UltraHD)}, {"Ultra VP9:", nameof(ContentURLs.Ultravp9)},
		{"High HD:", nameof(ContentURLs.HighHD)}, {"High VP9:", nameof(ContentURLs.Highvp9)},
		{"Mid HD:", nameof(ContentURLs.MidHD)}, {"Mid VP9:", nameof(ContentURLs.Midvp9)},
		{"Low HD:", nameof(ContentURLs.LowHD)}, {"Low VP9:", nameof(ContentURLs.Lowvp9)},
		{"Audio:", nameof(ContentURLs.Audio)}, {"mapPackage:", nameof(ContentURLs.mapPackage)}
	};

	protected override string BotToken => File.ReadAllText("Secret.txt").Trim();
	protected override int ExpectedEmbedCount => 3; // JDNext bot sends 3 embeds for assets

	protected override async Task InitializeAsync()
	{
		Console.Clear();
		string dbPath = Question.AskFile("Enter the path or URL to the jnext database json file: ", true, true);
		OutputDirectory = Question.AskFolder("Enter the path to your maps folder: ", true);

		string jsonContent;
		if (dbPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			using HttpClient client = new();
			jsonContent = await client.GetStringAsync(dbPath);
		}
		else
		{
			jsonContent = File.ReadAllText(dbPath);
		}

		_jDCacheJson = JsonSerializer.Deserialize<Dictionary<string, JDNextDatabaseEntry>>(jsonContent, Program.jsonOptions) ?? [];

		HandlePreExistingCache();

		Console.WriteLine($"Found {_jDCacheJson.Count} songs to download.");
		foreach (var entry in _jDCacheJson)
		{
			ItemQueue.Enqueue(entry);
		}
	}

	private void HandlePreExistingCache()
	{
		string[] alreadyDownloadedFolders = Directory.Exists(OutputDirectory)
			? [.. Directory.GetDirectories(OutputDirectory).Select(s => Path.GetFileName(s)!)]
			: [];

		_jDCacheJson = _jDCacheJson.Where(kvp =>
		{
			JDNextDatabaseEntry dbEntry = kvp.Value;
			if (!alreadyDownloadedFolders.Contains(dbEntry.parentMapName))
				return true; // Not downloaded yet

			string songInfoPath = Path.Combine(OutputDirectory, dbEntry.parentMapName, "SongInfo.json");
			if (!File.Exists(songInfoPath))
			{
				Console.WriteLine($"SongInfo.json missing for {dbEntry.parentMapName}, marking for redownload.");
				return true; // Corrupted download, redownload
			}

			try
			{
				string songInfoJson = File.ReadAllText(songInfoPath);
				JDNextDatabaseEntry existingEntry = JsonSerializer.Deserialize<JDNextDatabaseEntry>(songInfoJson, Program.jsonOptions)!;

				if (existingEntry.tags.Contains("Custom")) // If previously marked as custom
				{
					// Check if the new DB entry still has "Custom" or if it became official
					bool isNowOfficial = !dbEntry.tags.Contains("Custom");
					// Or based on some other logic, for now, assume if the new entry is NOT custom, it's "official"
					// This logic might need to be more robust based on actual database flags.

					if (isNowOfficial) // For simplicity, if the new DB entry does NOT have "Custom", assume it's official
					{
						Console.WriteLine($"'{dbEntry.parentMapName}' was custom, now official. Marking for redownload.");
						// Store songpack tags if any, to re-add them later
						foreach (string tag in existingEntry.tags.Where(t => t.StartsWith("songpack")))
						{
							_jdBundleTags[dbEntry.parentMapName] = tag;
						}

						Directory.Delete(Path.Combine(OutputDirectory, dbEntry.parentMapName), true);
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error reading SongInfo.json for {dbEntry.parentMapName}: {ex.Message}. Marking for redownload.");
				Directory.Delete(Path.Combine(OutputDirectory, dbEntry.parentMapName), true); // Clean up potentially corrupt folder
				return true;
			}

			Console.WriteLine($"'{dbEntry.parentMapName}' already downloaded and up-to-date. Skipping.");
			return false; // Already downloaded and not needing update based on "Custom" tag logic
		}).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
	}

	protected override string GetDiscordCommandForItem(KeyValuePair<string, JDNextDatabaseEntry> item)
	{
		return $"/assets server:jdnext codename:{item.Value.parentMapName}";
	}

	protected override JDNextDiscordEmbed? ParseEmbedsToData(DiscordMessage message)
	{
		if (message.Embeds.Count < 3)
		{
			Console.WriteLine($"Error: Expected 3 embeds for JDNext assets, got {message.Embeds.Count}.");
			return null;
		}

		var songData = new JDNextDiscordEmbed();
		EmbedParserHelper.ParseFields(songData.ImageURLs, message.Embeds[0].Fields, ImageUrlFieldMap);
		EmbedParserHelper.ParseFields(songData.PreviewURLs, message.Embeds[1].Fields, PreviewUrlFieldMap);
		EmbedParserHelper.ParseFields(songData.ContentURLs, message.Embeds[2].Fields, ContentUrlFieldMap);
		return songData;
	}

	protected override async Task<bool> ProcessDataItemAsync(JDNextDiscordEmbed songURLs, KeyValuePair<string, JDNextDatabaseEntry> songInfoPair)
	{
		JDNextDatabaseEntry songInfo = songInfoPair.Value;
		string mapName = songInfo.parentMapName;

		// Validate essential URLs
		if (songURLs.ContentURLs.Audio == null || songURLs.ContentURLs.mapPackage == null ||
			songURLs.ContentURLs.UltraHD == null || songURLs.ContentURLs.Highvp9 == null || // Assuming HighVP9 is a key quality
			songURLs.ImageURLs.Cover == null || songURLs.ImageURLs.coachesLarge == null || songURLs.ImageURLs.coachesSmall == null ||
			songInfo.assets == null) // songInfo.assets contains URLs from the database, not discord.
		{
			Console.WriteLine($"Critical URLs missing for '{mapName}'. Download cannot proceed.");
			return false;
		}

		Console.WriteLine($"Downloading assets for '{mapName}'...");
		string mapPath = Path.Combine(OutputDirectory, mapName);
		Directory.CreateDirectory(mapPath); // Ensure base map directory exists

		List<Task<string>> downloadTasks = new();

		// Preview files (URLs from database 'songInfo.assets')
		if (songInfo.assets.audioPreviewopus != null)
			downloadTasks.Add(Download.DownloadFileMD5Async(songInfo.assets.audioPreviewopus, Path.Combine(mapPath, "AudioPreview_opus")));
		if (songInfo.assets.videoPreview_ULTRAvp9webm != null)
			downloadTasks.Add(Download.DownloadFileMD5Async(songInfo.assets.videoPreview_ULTRAvp9webm, Path.Combine(mapPath, "videoPreview"), "ULTRA"));
		if (songInfo.assets.videoPreview_HIGHvp9webm != null)
			downloadTasks.Add(Download.DownloadFileMD5Async(songInfo.assets.videoPreview_HIGHvp9webm, Path.Combine(mapPath, "videoPreview"), "HIGH"));
		// Add MID and LOW previews if they are consistently available and needed
		if (songInfo.assets.videoPreview_MIDvp9webm != null)
			downloadTasks.Add(Download.DownloadFileMD5Async(songInfo.assets.videoPreview_MIDvp9webm, Path.Combine(mapPath, "videoPreview"), "MID"));
		if (songInfo.assets.videoPreview_LOWvp9webm != null)
			downloadTasks.Add(Download.DownloadFileMD5Async(songInfo.assets.videoPreview_LOWvp9webm, Path.Combine(mapPath, "videoPreview"), "LOW"));

		// Image files (URLs from Discord 'songURLs.ImageURLs')
		downloadTasks.Add(Download.DownloadFileMD5Async(songURLs.ImageURLs.Cover, Path.Combine(mapPath, "Cover")));
		downloadTasks.Add(Download.DownloadFileMD5Async(songURLs.ImageURLs.coachesLarge, Path.Combine(mapPath, "CoachesLarge")));
		downloadTasks.Add(Download.DownloadFileMD5Async(songURLs.ImageURLs.coachesSmall, Path.Combine(mapPath, "CoachesSmall")));

		// Song Title Logo (from database 'songInfo.assets' if present)
		if (songInfo.hasSongTitleInCover && songInfo.assets.songTitleLogo != null)
		{
			downloadTasks.Add(Download.DownloadFileMD5Async(songInfo.assets.songTitleLogo, Path.Combine(mapPath, "songTitleLogo")));
		}

		// Content files (URLs from Discord 'songURLs.ContentURLs')
		downloadTasks.Add(Download.DownloadFileMD5Async(songURLs.ContentURLs.Audio, Path.Combine(mapPath, "Audio_opus")));
		downloadTasks.Add(Download.DownloadFileMD5Async(songURLs.ContentURLs.UltraHD, Path.Combine(mapPath, "video"), "ULTRA"));
		downloadTasks.Add(Download.DownloadFileMD5Async(songURLs.ContentURLs.Highvp9, Path.Combine(mapPath, "video"), "HIGH"));
		// Add MID and LOW main videos if available and needed
		if (songURLs.ContentURLs.Midvp9 != null)
			downloadTasks.Add(Download.DownloadFileMD5Async(songURLs.ContentURLs.Midvp9, Path.Combine(mapPath, "video"), "MID"));
		if (songURLs.ContentURLs.Lowvp9 != null)
			downloadTasks.Add(Download.DownloadFileMD5Async(songURLs.ContentURLs.Lowvp9, Path.Combine(mapPath, "video"), "LOW"));

		downloadTasks.Add(Download.DownloadFileMD5Async(songURLs.ContentURLs.mapPackage, Path.Combine(mapPath, "MapPackage")));

		try
		{
			await Task.WhenAll(downloadTasks);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"One or more downloads failed for '{mapName}': {ex.Message}");
			// Consider cleanup of partially downloaded files if necessary
			return false;
		}

		// Create SongInfo.json
		songInfo.songID = songInfoPair.Key; // Ensure songID is set from the database key
		if (_jdBundleTags.TryGetValue(mapName, out string? bundleTag))
		{
			if (!songInfo.tags.Contains(bundleTag))
				songInfo.tags.Add(bundleTag);
		}

		string songInfoJson = JsonSerializer.Serialize(songInfo, Program.jsonOptions);
		File.WriteAllText(Path.Combine(mapPath, "SongInfo.json"), songInfoJson);

		Console.WriteLine($"Successfully downloaded and prepared '{mapName}'.");
		return true;
	}

	// Commented out methods AddMissingTitles and AddMissingTitle can be re-implemented if needed.
	// static void AddMissingTitles() { /* ... */ }
	// static void AddMissingTitle(string songID) { /* ... */ }
}