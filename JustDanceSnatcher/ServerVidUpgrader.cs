using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using JustDanceSnatcher.Helpers;
using JustDanceSnatcher.UbisoftStuff;

using System.IO;
using System.Text.Json;

namespace JustDanceSnatcher;

internal enum ServerUpgradeStep
{
	RequestPreview, // For /assets server:jdu
	RequestMain // For /nohud
}

// Note: This class is no longer static. Instantiate it to use.
internal class ServerVidUpgrader : DiscordAssetDownloaderBase<JDNextDatabaseEntry, JDNextDiscordEmbed>
{
	private ServerUpgradeStep _currentStep = ServerUpgradeStep.RequestPreview;
	private JDNextDiscordEmbed _accumulatedSongData = new(); // To store data across steps

	// Mappings for EmbedParserHelper
	private static readonly IReadOnlyDictionary<string, string> PreviewUrlFieldMap = new Dictionary<string, string>
	{
		{"Audio:", nameof(PreviewURLs.audioPreview)}, // Note: JDU /assets uses "Audio:" for audioPreview
        {"HIGH (vp9)", nameof(PreviewURLs.HIGHvp9)},
		{"LOW (vp9)", nameof(PreviewURLs.LOWvp9)},
		{"MID (vp9)", nameof(PreviewURLs.MIDvp9)},
		{"ULTRA (vp9)", nameof(PreviewURLs.ULTRAvp9)}
	};

	private static readonly IReadOnlyDictionary<string, string> MainContentUrlFieldMap = new Dictionary<string, string>
	{
		{"Ultra HD:", nameof(ContentURLs.UltraHD)},
		{"High HD:", nameof(ContentURLs.HighHD)},
		{"Mid HD:", nameof(ContentURLs.MidHD)},
		{"Low HD:", nameof(ContentURLs.LowHD)}
	};

	protected override string BotToken => File.ReadAllText("Secret.txt").Trim();
	// ExpectedEmbedCount varies by step, so override IsErrorEmbed or ParseEmbedsToData to handle
	protected override int ExpectedEmbedCount => 0; // Set to 0 to allow custom handling in ParseEmbedsToData

	protected override async Task InitializeAsync()
	{
		Console.Clear();
		OutputDirectory = Question.AskFolder("Enter the path to your maps folder (this is where videos will be upgraded): ", true);

		string[] mapFolders = Directory.GetDirectories(OutputDirectory);
		foreach (string folder in mapFolders)
		{
			string songInfoPath = Path.Combine(folder, "SongInfo.json");
			if (!File.Exists(songInfoPath))
				continue;

			try
			{
				string json = File.ReadAllText(songInfoPath);
				JDNextDatabaseEntry entry = JsonSerializer.Deserialize<JDNextDatabaseEntry>(json, Program.jsonOptions)!;

				// Rename folder if it doesn't match parentMapName (as in original logic)
				string expectedFolderName = entry.parentMapName;
				string currentFolderName = Path.GetFileName(folder);
				string correctedFolderPath = folder;

				if (!string.Equals(currentFolderName, expectedFolderName, StringComparison.OrdinalIgnoreCase))
				{
					string targetPath = Path.Combine(Path.GetDirectoryName(folder)!, expectedFolderName);
					if (!Directory.Exists(targetPath)) // Avoid conflict if target already exists somehow
					{
						Directory.Move(folder, targetPath);
						correctedFolderPath = targetPath;
						Console.WriteLine($"Renamed folder '{currentFolderName}' to '{expectedFolderName}'.");
					}
					else
					{
						Console.WriteLine($"Warning: Could not rename '{currentFolderName}' to '{expectedFolderName}' as target already exists. Using original path.");
					}
				}

				bool needsUpgrade = false;
				string videoPath = Path.Combine(correctedFolderPath, "video");
				string videoPreviewPath = Path.Combine(correctedFolderPath, "videoPreview");

				if (Directory.Exists(videoPath) && Directory.GetFiles(videoPath).Length <= 1)
					needsUpgrade = true;
				if (Directory.Exists(videoPreviewPath) && Directory.GetFiles(videoPreviewPath).Length <= 1)
					needsUpgrade = true;

				// Original logic also checked for UNKNOWN.webm, but stated deprecated
				// if (File.Exists(Path.Combine(videoPath, "UNKNOWN.webm"))) needsUpgrade = true;

				if (needsUpgrade)
				{
					Console.WriteLine($"Adding '{entry.parentMapName}' to the upgrade queue.");
					ItemQueue.Enqueue(entry);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error processing folder '{folder}': {ex.Message}. Skipping.");
			}
		}

		await Task.CompletedTask; // No async operations in this Initialize
	}

	protected override string GetDiscordCommandForItem(JDNextDatabaseEntry item)
	{
		return _currentStep == ServerUpgradeStep.RequestPreview
			? $"/assets server:jdu codename:{item.parentMapName}"
			: $"/nohud codename:{item.parentMapName}";
	}

	// This downloader has a two-step process per item.
	// We override the message handling part of the base to manage state.
	// The base OnDiscordMessageCreatedAsync is not used directly. We call its helpers.
	// This means the base method's HandleUserCommandAsync will also need to be explicitly called.
	protected async Task OnDiscordMessageCreatedAsync(DiscordClient sender, MessageCreateEventArgs e)
	{
		if (e.Author.IsCurrent || !e.Author.IsBot)
		{
			await HandleUserCommandAsync(e); // Use base class's user command handler
			return;
		}

		Console.WriteLine($"Received bot response for ServerVidUpgrader from {e.Author.Username}. Step: {_currentStep}");
		await e.Message.CreateReactionAsync(DiscordEmoji.FromName(sender, ":white_check_mark:"));

		for (int i = 0; i < 10 && e.Message.Embeds.Count == 0; i++)
			await Task.Delay(1000);

		if (e.Message.Embeds.Count == 0 || IsErrorEmbed(e.Message))
		{
			Console.WriteLine(e.Message.Embeds.Count == 0 ? "No embeds in response." : "Error embed received.");
			// Use base class's command failure logic. It will retry command or skip item.
			await base.HandleCommandFailureAsync(e.Message.Embeds.Count == 0 ? "No embeds" : "Error embed");
			return;
		}

		if (ItemQueue.Count == 0)
		{
			Console.WriteLine("Queue empty, ignoring bot message.");
			return;
		}

		JDNextDatabaseEntry currentItem = ItemQueue.Peek();

		if (_currentStep == ServerUpgradeStep.RequestPreview)
		{
			if (e.Message.Embeds.Count < 3)
			{ // Expect 3 embeds from /assets jdu
				Console.WriteLine($"Expected 3 embeds for preview, got {e.Message.Embeds.Count}.");
				await base.HandleCommandFailureAsync("Incorrect embed count for preview");
				return;
			}
			// Parse preview URLs from Embeds[1] (0: images, 1: previews, 2: content links)
			EmbedParserHelper.ParseFields(_accumulatedSongData.PreviewURLs, e.Message.Embeds[1].Fields, PreviewUrlFieldMap);

			_currentStep = ServerUpgradeStep.RequestMain;
			await SendNextDiscordCommandAsync(); // Sends /nohud command for the same item
		}
		else // ServerUpgradeStep.RequestMain
		{
			if (e.Message.Embeds.Count < 1)
			{ // Expect 1 embed from /nohud
				Console.WriteLine($"Expected 1 embed for main, got {e.Message.Embeds.Count}.");
				await base.HandleCommandFailureAsync("Incorrect embed count for main");
				return;
			}
			// Parse main content URLs (HD versions)
			EmbedParserHelper.ParseFields(_accumulatedSongData.ContentURLs, e.Message.Embeds[0].Fields, MainContentUrlFieldMap);

			Console.WriteLine($"Successfully parsed all data for item: {currentItem.parentMapName}. Processing...");
			bool success = await ProcessDataItemAsync(_accumulatedSongData, currentItem);

			_currentStep = ServerUpgradeStep.RequestPreview; // Reset state for the next item
			_accumulatedSongData = new JDNextDiscordEmbed(); // Clear accumulated data

			if (success)
			{
				Console.WriteLine($"Successfully processed item: {currentItem.parentMapName}.");
				FailCounterForCurrentItem = 0;
				ItemQueue.Dequeue();
				await SendNextDiscordCommandAsync(); // Move to next item
			}
			else
			{
				// Use base class's item processing failure logic
				await base.HandleItemProcessingFailureAsync(currentItem, "ProcessDataItemAsync returned false.");
			}
		}
	}

	// We've overridden OnDiscordMessageCreatedAsync, so ParseEmbedsToData is not directly called by base.
	// Logic is in the overridden OnDiscordMessageCreatedAsync.
	protected override JDNextDiscordEmbed? ParseEmbedsToData(DiscordMessage message)
	{
		// This won't be called by the base flow due to the override.
		// If it were, it would need to be state-aware (_currentStep).
		return null;
	}

	protected override async Task<bool> ProcessDataItemAsync(JDNextDiscordEmbed songURLs, JDNextDatabaseEntry songInfo)
	{
		string mapName = songInfo.parentMapName;
		Console.WriteLine($"Upgrading videos for '{mapName}'...");

		// Validate essential URLs from accumulated data
		if (songURLs.PreviewURLs.audioPreview == null || // from /assets jdu
			songURLs.PreviewURLs.ULTRAvp9 == null || songURLs.PreviewURLs.HIGHvp9 == null ||
			songURLs.PreviewURLs.MIDvp9 == null || songURLs.PreviewURLs.LOWvp9 == null ||
			songURLs.ContentURLs.UltraHD == null || songURLs.ContentURLs.HighHD == null || // from /nohud
			songURLs.ContentURLs.MidHD == null || songURLs.ContentURLs.LowHD == null)
		{
			Console.WriteLine($"Essential URLs missing for '{mapName}' after two-step fetch. Cannot upgrade.");
			return false;
		}

		string mapPath = Path.Combine(OutputDirectory, mapName);
		string videoPath = Path.Combine(mapPath, "video");
		string videoPreviewPath = Path.Combine(mapPath, "videoPreview");
		string audioPreviewOpusPath = Path.Combine(mapPath, "AudioPreview_opus");

		Directory.CreateDirectory(videoPath);
		Directory.CreateDirectory(videoPreviewPath);
		Directory.CreateDirectory(audioPreviewOpusPath);

		// Clean up old "UNKNOWN.webm" files if they exist (as per original logic)
		string unknownVideo = Path.Combine(videoPath, "UNKNOWN.webm");
		string unknownPreview = Path.Combine(videoPreviewPath, "UNKNOWN.webm");
		string lowPreviewLegacy = Path.Combine(videoPreviewPath, "LOW.webm"); // Also in original logic
		if (File.Exists(unknownVideo))
			File.Delete(unknownVideo);
		if (File.Exists(unknownPreview))
			File.Delete(unknownPreview);
		if (File.Exists(lowPreviewLegacy))
			File.Delete(lowPreviewLegacy);

		List<Task> downloadTasks =
		[
			// Preview videos (vp9 versions)
			Download.DownloadFileMD5Async(songURLs.PreviewURLs.LOWvp9, videoPreviewPath),
			Download.DownloadFileMD5Async(songURLs.PreviewURLs.MIDvp9, videoPreviewPath),
			Download.DownloadFileMD5Async(songURLs.PreviewURLs.HIGHvp9, videoPreviewPath),
			Download.DownloadFileMD5Async(songURLs.PreviewURLs.ULTRAvp9, videoPreviewPath),

			// Main videos (HD versions)
			Download.DownloadFileMD5Async(songURLs.ContentURLs.UltraHD, videoPath),
			Download.DownloadFileMD5Async(songURLs.ContentURLs.HighHD, videoPath),
			Download.DownloadFileMD5Async(songURLs.ContentURLs.MidHD, videoPath),
			Download.DownloadFileMD5Async(songURLs.ContentURLs.LowHD, videoPath),
		];

		// Audio preview (opus)
		// Check if AudioPreview_opus folder is empty or doesn't exist (original logic)
		if (!Directory.Exists(audioPreviewOpusPath) || Directory.GetFiles(audioPreviewOpusPath).Length == 0)
		{
			downloadTasks.Add(Download.DownloadFileMD5Async(songURLs.PreviewURLs.audioPreview, audioPreviewOpusPath));
		}

		try
		{
			await Task.WhenAll(downloadTasks);
			Console.WriteLine($"Successfully upgraded videos for '{mapName}'.");
			return true;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"One or more downloads failed during video upgrade for '{mapName}': {ex.Message}");
			return false;
		}
	}
}