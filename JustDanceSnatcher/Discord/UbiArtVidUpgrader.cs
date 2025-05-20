using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using JustDanceSnatcher.Core;
using JustDanceSnatcher.Discord.Helpers;
using JustDanceSnatcher.Discord.Models;
using JustDanceSnatcher.Helpers;
using JustDanceSnatcher.UbisoftStuff;

using System.Text.Json;

namespace JustDanceSnatcher.Discord;

internal class UbiArtVidUpgrader : DiscordAssetDownloaderBase<string, NoHudDiscordEmbed>
{
	private string _bundlesPath = string.Empty;
	private string _cachePath = string.Empty;
	private List<string> _existingMapsInCache = [];
	private string? _mapsFolderNameInBundle;

	private static readonly IReadOnlyDictionary<string, string> NoHudFieldMap = new Dictionary<string, string>
	{
		{"Ultra:", nameof(NoHudDiscordEmbed.Ultra)}, {"Ultra HD:", nameof(NoHudDiscordEmbed.UltraHD)},
		{"High:", nameof(NoHudDiscordEmbed.High)}, {"High HD:", nameof(NoHudDiscordEmbed.HighHD)},
		{"Mid:", nameof(NoHudDiscordEmbed.Mid)}, {"Mid HD:", nameof(NoHudDiscordEmbed.MidHD)},
		{"Low:", nameof(NoHudDiscordEmbed.Low)}, {"Low HD:", nameof(NoHudDiscordEmbed.LowHD)},
		{"Audio:", nameof(NoHudDiscordEmbed.Audio)}
	};

	protected override string BotToken => File.ReadAllText("Secret.txt").Trim();
	protected override int ExpectedEmbedCount => 1;

	protected override async Task InitializeAsync()
	{
		Console.Clear();
		_bundlesPath = Question.AskFolder("Enter the path to your UbiArt game bundles: ", true);
		_cachePath = Question.AskFolder("Enter the path to your existing upgraded maps cache: ", true);
		_existingMapsInCache = Directory.Exists(_cachePath)
			? [.. Directory.GetDirectories(_cachePath).Select(s => Path.GetFileName(s)!)]
			: [];

		PopulateMapsToUpgrade();
		Console.WriteLine($"Found {ItemQueue.Count} UbiArt maps to upgrade videos for.");
		await Task.CompletedTask;
	}

	private void PopulateMapsToUpgrade()
	{
		string[] bundleFolders = Directory.GetDirectories(_bundlesPath);
		string? platform = DetectPlatformFromBundles(bundleFolders);
		if (platform == null)
		{
			Console.WriteLine("Could not determine platform from bundle structure.");
			return;
		}

		bundleFolders = [.. bundleFolders.Where(f => !Path.GetFileName(f).Contains($"patch_{platform}", StringComparison.OrdinalIgnoreCase))];
		List<string> mapsToProcessList = [];

		foreach (string bundleFolder in bundleFolders)
		{
			if (_mapsFolderNameInBundle == null)
			{
				string worldPath = Path.Combine(bundleFolder, "cache", "itf_cooked", platform, "world");
				if (Directory.Exists(Path.Combine(worldPath, "maps")))
					_mapsFolderNameInBundle = "maps";
				else if (Directory.Exists(Path.Combine(worldPath, "jd2015")))
					_mapsFolderNameInBundle = "jd2015";
				else if (Directory.Exists(Path.Combine(worldPath, "jd5")))
					_mapsFolderNameInBundle = "jd5";
				if (_mapsFolderNameInBundle == null)
				{
					Console.WriteLine($"Could not find maps folder in '{worldPath}'. Skipping bundle.");
					continue;
				}
			}

			string mapDirectoryRootPath = Path.Combine(bundleFolder, "cache", "itf_cooked", platform, "world", _mapsFolderNameInBundle);
			if (!Directory.Exists(mapDirectoryRootPath))
				continue;

			foreach (string mapFolder in Directory.GetDirectories(mapDirectoryRootPath))
			{
				string songDescPath = Path.Combine(mapFolder, "songdesc.tpl.ckd");
				if (!File.Exists(songDescPath))
					continue;

				try
				{
					SongDesc songDesc = JsonSerializer.Deserialize<SongDesc>(File.ReadAllText(songDescPath).Trim('\0'), GlobalConfig.JsonOptions)!;
					if (songDesc.COMPONENTS == null || songDesc.COMPONENTS.Length == 0)
						continue;
					string mapName = songDesc.COMPONENTS[0].MapName;

					if (string.IsNullOrWhiteSpace(mapName) || mapsToProcessList.Contains(mapName) || ItemQueue.Contains(mapName) ||
						_existingMapsInCache.Contains(mapName, StringComparer.OrdinalIgnoreCase) || GetVideoPathInBundles(mapName) == null)
					{
						continue;
					}

					mapsToProcessList.Add(mapName);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error processing songdesc for '{mapFolder}': {ex.Message}");
				}
			}
		}

		mapsToProcessList.Sort();
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

	private string? GetVideoPathInBundles(string mapName)
	{
		if (_mapsFolderNameInBundle == null)
			return null;
		string[] bundleFolders = Directory.GetDirectories(_bundlesPath);
		string? platform = DetectPlatformFromBundles(bundleFolders);
		if (platform == null)
			return null;
		bundleFolders = [.. bundleFolders.Where(f => !Path.GetFileName(f).Contains($"patch_{platform}", StringComparison.OrdinalIgnoreCase))];

		foreach (string bundleFolder in bundleFolders)
		{
			string videoCoachPath = Path.Combine(bundleFolder, "cache", "itf_cooked", platform, "world", _mapsFolderNameInBundle, mapName, "videoscoach");
			if (Directory.Exists(videoCoachPath))
			{
				string[] videoFiles = Directory.GetFiles(videoCoachPath, "*.webm");
				if (videoFiles.Length > 0)
					return videoFiles[0];
			}
		}

		return null;
	}

	protected override string GetDiscordCommandForItem(string mapName) => $"/nohud codename:{mapName}";

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
			Console.WriteLine($"UltraHD URL missing for '{mapName}'.");
			return false;
		}

		Console.WriteLine($"Downloading upgraded UltraHD video for '{mapName}'...");
		string destinationMapFolder = Path.Combine(_cachePath, mapName, "videoscoach");
		Directory.CreateDirectory(destinationMapFolder);
		string? originalVideoPath = GetVideoPathInBundles(mapName);

		try
		{
			string downloadedFileName = await Download.DownloadFileMD5Async(songURLs.UltraHD, destinationMapFolder);
			if (originalVideoPath != null)
			{
				string originalFileNameWithExt = Path.GetFileName(originalVideoPath);
				string finalNewVideoPath = Path.Combine(destinationMapFolder, originalFileNameWithExt);
				string currentNewVideoPath = Path.Combine(destinationMapFolder, downloadedFileName);
				if (File.Exists(finalNewVideoPath))
					File.Delete(finalNewVideoPath);
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
			Console.WriteLine($"Failed to download/move video for '{mapName}': {ex.Message}");
			return false;
		}
	}

	protected override async Task HandleUserCommandAsync(MessageCreateEventArgs e)
	{
		await base.HandleUserCommandAsync(e);
		string content = e.Message.Content.Trim().ToLowerInvariant();
		if (content is "redo" or "!redo" or "retry" or "!retry")
		{
			if (ItemQueue.Count > 0)
			{
				Console.WriteLine($"User requested redo/retry for current item: {ItemQueue.Peek()}");
				await e.Message.RespondAsync($"Retrying command for `{ItemQueue.Peek()}`.");
				FailCounterForCurrentItem = 0;
				await SendNextDiscordCommandAsync();
			}
			else
			{
				await e.Message.RespondAsync("Queue is empty, nothing to redo.");
			}
		}
	}
}