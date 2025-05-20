using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using JustDanceSnatcher.Helpers;
using JustDanceSnatcher.UbisoftStuff; // For SongDesc

using System.Text.Json;

using Xabe.FFmpeg.Downloader; // Keep if FFmpegDownloader is essential for this class's setup

namespace JustDanceSnatcher;

// Note: This class is no longer static. Instantiate it to use.
internal class UbiArtVidUpgrader : DiscordAssetDownloaderBase<string, NoHudDiscordEmbed>
{
	private string _bundlesPath = string.Empty; // Equivalent to original `output`
	private string _cachePath = string.Empty;
	private List<string> _existingMapsInCache = [];
	private string? _mapsFolderNameInBundle; // e.g., "maps", "jd2015"

	// Mapping for EmbedParserHelper
	private static readonly IReadOnlyDictionary<string, string> NoHudFieldMap = new Dictionary<string, string>
	{
		{"Ultra:", nameof(NoHudDiscordEmbed.Ultra)}, {"Ultra HD:", nameof(NoHudDiscordEmbed.UltraHD)},
		{"High:", nameof(NoHudDiscordEmbed.High)}, {"High HD:", nameof(NoHudDiscordEmbed.HighHD)},
		{"Mid:", nameof(NoHudDiscordEmbed.Mid)}, {"Mid HD:", nameof(NoHudDiscordEmbed.MidHD)},
		{"Low:", nameof(NoHudDiscordEmbed.Low)}, {"Low HD:", nameof(NoHudDiscordEmbed.LowHD)},
		{"Audio:", nameof(NoHudDiscordEmbed.Audio)}
	};

	protected override string BotToken => File.ReadAllText("Secret.txt").Trim();
	protected override int ExpectedEmbedCount => 1; // /nohud usually returns 1 embed

	protected override async Task InitializeAsync()
	{
		// FFmpeg download (if still needed for this specific class, otherwise remove)
		// Task ffmpegTask = FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);
		// await ffmpegTask; // Ensure FFmpeg is ready before proceeding if it's used by ProcessDataItemAsync

		Console.Clear();
		_bundlesPath = Question.AskFolder("Enter the path to your UbiArt game bundles: ", true);
		_cachePath = Question.AskFolder("Enter the path to your existing upgraded maps cache (e.g., output of previous runs): ", true);
		_existingMapsInCache = Directory.Exists(_cachePath)
			? [.. Directory.GetDirectories(_cachePath).Select(s => Path.GetFileName(s)!)]
			: [];

		PopulateMapsToUpgrade();
		Console.WriteLine($"Found {ItemQueue.Count} UbiArt maps to upgrade videos for.");
	}

	private void PopulateMapsToUpgrade()
	{
		string[] bundleFolders = Directory.GetDirectories(_bundlesPath);
		string? platform = DetectPlatformFromBundles(bundleFolders);
		if (platform == null)
		{
			Console.WriteLine("Could not determine platform from bundle structure. Cannot find maps.");
			return;
		}

		// Filter out patch bundles (as in original logic)
		bundleFolders = [.. bundleFolders.Where(f => !Path.GetFileName(f).Contains($"patch_{platform}", StringComparison.OrdinalIgnoreCase))];

		List<string> mapsToProcessList = [];

		foreach (string bundleFolder in bundleFolders)
		{
			if (_mapsFolderNameInBundle == null) // Detect maps folder name once
			{
				string worldPath = Path.Combine(bundleFolder, "cache", "itf_cooked", platform, "world");
				if (Directory.Exists(Path.Combine(worldPath, "maps")))
					_mapsFolderNameInBundle = "maps";
				else if (Directory.Exists(Path.Combine(worldPath, "jd2015")))
					_mapsFolderNameInBundle = "jd2015";
				else if (Directory.Exists(Path.Combine(worldPath, "jd5")))
					_mapsFolderNameInBundle = "jd5";
				// Add other potential map folder names if necessary

				if (_mapsFolderNameInBundle == null)
				{
					Console.WriteLine($"Could not find a known maps folder (e.g., 'maps', 'jd2015') in '{worldPath}'. Skipping bundle.");
					continue;
				}
			}

			string mapDirectoryRootPath = Path.Combine(bundleFolder, "cache", "itf_cooked", platform, "world", _mapsFolderNameInBundle);
			if (!Directory.Exists(mapDirectoryRootPath))
				continue;

			string[] mapSpecificFolders = Directory.GetDirectories(mapDirectoryRootPath);
			foreach (string mapFolder in mapSpecificFolders)
			{
				string songDescPath = Path.Combine(mapFolder, "songdesc.tpl.ckd");
				if (!File.Exists(songDescPath))
					continue;

				try
				{
					// Original JSON options from Program.cs should be fine for SongDesc
					SongDesc songDesc = JsonSerializer.Deserialize<SongDesc>(File.ReadAllText(songDescPath).Trim('\0'), Program.jsonOptions)!;
					if (songDesc.COMPONENTS == null || songDesc.COMPONENTS.Length == 0)
						continue;

					string mapName = songDesc.COMPONENTS[0].MapName;

					if (string.IsNullOrWhiteSpace(mapName))
						continue;
					if (mapsToProcessList.Contains(mapName) || ItemQueue.Contains(mapName))
						continue; // Already added
					if (_existingMapsInCache.Contains(mapName, StringComparer.OrdinalIgnoreCase))
					{
						// Console.WriteLine($"'{mapName}' already exists in cache. Skipping.");
						continue;
					}

					// Check if video path exists (as per original GetVideoPath logic)
					if (GetVideoPathInBundles(mapName) == null)
					{
						// Console.WriteLine($"Original video for '{mapName}' not found in bundles. Skipping upgrade candidate.");
						continue;
					}

					mapsToProcessList.Add(mapName);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error processing songdesc for map in '{mapFolder}': {ex.Message}");
				}
			}
		}

		mapsToProcessList.Sort(); // Sort before enqueuing
		foreach (string map in mapsToProcessList)
			ItemQueue.Enqueue(map);
	}

	private static string? DetectPlatformFromBundles(string[] bundleFolders)
	{
		foreach (string folder in bundleFolders)
		{
			string pathToPlatformDir = Path.Combine(folder, "cache", "itf_cooked");
			if (!Directory.Exists(pathToPlatformDir))
				continue;

			string[] platformDirs = Directory.GetDirectories(pathToPlatformDir);
			if (platformDirs.Length > 0)
				return Path.GetFileName(platformDirs[0]);
		}

		return null;
	}

	// Finds the path to the *original* video in the bundle structure.
	private string? GetVideoPathInBundles(string mapName)
	{
		if (_mapsFolderNameInBundle == null)
			return null; // Cannot determine path if maps folder name unknown

		// Iterate through bundle folders to find the video for the given mapName
		// This assumes platform is detected and _mapsFolderNameInBundle is set.
		string[] bundleFolders = Directory.GetDirectories(_bundlesPath);
		string? platform = DetectPlatformFromBundles(bundleFolders); // Re-detect or store it
		if (platform == null)
			return null;

		bundleFolders = [.. bundleFolders.Where(f => !Path.GetFileName(f).Contains($"patch_{platform}", StringComparison.OrdinalIgnoreCase))];

		foreach (string bundleFolder in bundleFolders)
		{
			// Path to specific map's videoscoach folder structure. UbiArt paths can be complex.
			// Example: bundle_nx/cache/itf_cooked/nx/world/maps/MapName/videoscoach/
			string videoCoachPath = Path.Combine(bundleFolder, "cache", "itf_cooked", platform, "world", _mapsFolderNameInBundle, mapName, "videoscoach");
			if (Directory.Exists(videoCoachPath))
			{
				string[] videoFiles = Directory.GetFiles(videoCoachPath, "*.webm"); // Or other relevant extensions
				if (videoFiles.Length > 0)
					return videoFiles[0]; // Return path to the first found video
			}
		}

		return null;
	}

	protected override string GetDiscordCommandForItem(string mapName)
	{
		return $"/nohud codename:{mapName}";
	}

	protected override NoHudDiscordEmbed? ParseEmbedsToData(DiscordMessage message)
	{
		if (message.Embeds.Count == 0)
			return null;
		var embedData = new NoHudDiscordEmbed();
		EmbedParserHelper.ParseFields(embedData, message.Embeds[0].Fields, NoHudFieldMap);
		return embedData;
	}

	protected override async Task<bool> ProcessDataItemAsync(NoHudDiscordEmbed songURLs, string mapName)
	{
		if (string.IsNullOrWhiteSpace(songURLs.UltraHD) || songURLs.UltraHD.Equals("undefined", StringComparison.OrdinalIgnoreCase))
		{
			Console.WriteLine($"UltraHD URL missing or undefined for '{mapName}'. Cannot download.");
			return false;
		}

		Console.WriteLine($"Downloading upgraded UltraHD video for '{mapName}'...");

		// The destination for the new video should be the *cache* path, not the bundle path.
		string destinationMapFolder = Path.Combine(_cachePath, mapName, "videoscoach"); // Using _cachePath as output for new videos
		Directory.CreateDirectory(destinationMapFolder);

		// Find the original video path to determine its original filename (if needed for replacement)
		// Or, if the new video should always be MD5-named or specific-named (e.g. mapName_ULTRA.webm)
		string? originalVideoPath = GetVideoPathInBundles(mapName);
		if (originalVideoPath == null)
		{
			Console.WriteLine($"Could not find original video path for {mapName} to replace. Saving with new name.");
			// Fallback: save as mapName_ULTRAHD.webm or MD5 hash. For now, using MD5.
		}

		try
		{
			// Download the new UltraHD video. It will be named by its MD5 hash.
			string downloadedFileName = await Download.DownloadFileMD5Async(songURLs.UltraHD, destinationMapFolder);

			// If we need to replace an existing file with a specific name:
			if (originalVideoPath != null)
			{
				string originalFileNameWithExt = Path.GetFileName(originalVideoPath);
				string finalNewVideoPath = Path.Combine(destinationMapFolder, originalFileNameWithExt); // Target name is same as original
				string currentNewVideoPath = Path.Combine(destinationMapFolder, downloadedFileName);

				if (File.Exists(finalNewVideoPath))
					File.Delete(finalNewVideoPath); // Delete old if exists at target location with original name
				File.Move(currentNewVideoPath, finalNewVideoPath);
				Console.WriteLine($"Replaced video for '{mapName}' at '{finalNewVideoPath}'.");
			}
			else
			{
				Console.WriteLine($"Downloaded new video for '{mapName}' as '{downloadedFileName}' in cache.");
			}

			return true;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to download or move video for '{mapName}': {ex.Message}");
			return false;
		}
	}

	// Override user command handling if "redo" or "retry" specific to this class are needed.
	// The base class handles "skip", "stop", "info".
	protected override async Task HandleUserCommandAsync(MessageCreateEventArgs e)
	{
		// Call base handler first for common commands
		await base.HandleUserCommandAsync(e);

		// Add specific commands for UbiArtVidUpgrader if any. Example:
		string content = e.Message.Content.Trim().ToLowerInvariant();
		if (content is "redo" or "!redo" or "retry" or "!retry")
		{
			if (ItemQueue.Count > 0)
			{
				Console.WriteLine($"User requested redo/retry for current item: {ItemQueue.Peek()}");
				await e.Message.RespondAsync($"Retrying command for `{ItemQueue.Peek()}`.");
				FailCounterForCurrentItem = 0; // Reset fail counter for this item
				await SendNextDiscordCommandAsync(); // Resend current command
			}
			else
			{
				await e.Message.RespondAsync("Queue is empty, nothing to redo.");
			}
		}
	}
}