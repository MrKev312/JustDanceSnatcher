using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using JustDanceSnatcher.Helpers;
using JustDanceSnatcher.UbisoftStuff;

using System.Text;
using System.Text.Json;

using Xabe.FFmpeg.Downloader;

namespace JustDanceSnatcher;

internal class UbiArtUpgradeVideo
{
	// Queue of map IDs
	readonly static Queue<string> maps = [];
	static List<string> existingMaps = [];
	static string output = string.Empty;
	static uint failCounter = 0;
	static bool downloading = false;

	public static async Task Run()
	{
		Task ffmpegTask = FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);

		Console.Clear();
		// Ask for the path to the cache.json file
		output = Question.AskFolder("Enter the path to your bundles: ", true);
		string cacheLocation = Question.AskFolder("Enter the path to your cache: ", true);
		existingMaps = Directory.GetDirectories(cacheLocation).Select(x => Path.GetFileName(x)).ToList();

		// Get the maps
		GetMaps();

		// Create a new DiscordClientBuilder
		Console.WriteLine("Starting discord bot!");

		string token = File.ReadAllText("Secret.txt").Trim();

		DiscordClient discord = new(new DiscordConfiguration()
		{
			Token = token,
			TokenType = TokenType.Bot,
			Intents = DiscordIntents.MessageContents | DiscordIntents.AllUnprivileged
		});

		// Add the message created event
		discord.MessageCreated += OnMessageCreated;

		// Connect to Discord
		await discord.ConnectAsync();
		await ffmpegTask;

		Console.WriteLine($"Downloading {maps.Count} map ids");

		// Set the clipboard to the first map ID
		if (maps.Count != 0)
			UpdateClipboard();
		else
			return;

		// Wait for the bot to disconnect
		await Task.Delay(-1);
	}

	private static void UpdateClipboard() => Program.SetClipboard($"/nohud codename:{maps.Peek()}");

	static string? GetVideoPath(string songID)
	{
		// Get the path to the video
		string path = Path.Combine("world", mapsName!, songID, "videoscoach");
		List<string> paths = [];

		foreach (string folder in Directory.GetDirectories(output))
		{
			string videoPath = Path.Combine(folder, path);
			if (!Directory.Exists(videoPath))
				continue;

			string[] videoFiles = Directory.GetFiles(videoPath, "*.webm");
			paths.AddRange(videoFiles);
		}

		// Return the path
		return paths.Count == 0 ? null : paths[0];
	}

	static string? mapsName = null;
	static void GetMaps()
	{
		// Get the name of each folder in the output directory
		string[] folders = Directory.GetDirectories(output);
		string? platform = null;

		// Get the platform
		foreach (string folder in folders)
		{
			string pathToPlatform = Path.Combine(folder, "cache", "itf_cooked");
			if (!Directory.Exists(pathToPlatform))
				continue;

			string[] dirs = Directory.GetDirectories(pathToPlatform);
			if (dirs.Length == 0)
				continue;

			platform = Path.GetFileName(dirs[0]);
			break;
		}

		if (platform is null)
		{
			Console.WriteLine("Couldn't find the platform folder");
			return;
		}

		// remove "bundle_{platform}" and "patch_{platform}" from the list
		folders = folders.Where(x => !x.Contains($"patch_{platform}", StringComparison.OrdinalIgnoreCase)).ToArray();

		List<string> mapsList = [];

		foreach (string bundleFolder in folders)
		{
			if (mapsName is null)
			{
				string mapNameFolder = Path.Combine(bundleFolder, "cache", "itf_cooked", platform, "world");
				if (Directory.Exists(Path.Combine(mapNameFolder, "maps")))
					mapsName = "maps";
				else if (Directory.Exists(Path.Combine(mapNameFolder, "jd2015")))
					mapsName = "jd2015";
				else if (Directory.Exists(Path.Combine(mapNameFolder, "jd5")))
					mapsName = "jd5";

				if (mapsName is null)
				{
					Console.WriteLine("Couldn't find the maps folder");
					return;
				}
			}

			// For each folder
			string mapDirectoryPath = Path.Combine(bundleFolder, "cache", "itf_cooked", platform, "world", mapsName);
			if (!Directory.Exists(mapDirectoryPath))
				continue;
			string[] mapsFolders = Directory.GetDirectories(mapDirectoryPath);

			foreach (string map in mapsFolders)
			{
				// If the songdesc doesn't exist, skip
				string songDescPath = Path.Combine(map, "songdesc.tpl.ckd");
				if (!File.Exists(songDescPath))
					continue;

				SongDesc songDesc = JsonSerializer.Deserialize<SongDesc>(File.ReadAllText(songDescPath).Trim('\0'), Program.jsonOptions)!;

				string mapName = songDesc.COMPONENTS[0].MapName;

				// If it's already in the queue, skip
				if (maps.Contains(mapName))
				{
					Console.WriteLine($"{mapName} is already in the queue");
					continue;
				}

				// If the mapname is already in the cache, skip
				if (existingMaps.Contains(mapName, StringComparer.OrdinalIgnoreCase))
				{
					Console.WriteLine($"{mapName} is already in the cache");
					continue;
				}

				string? videoName = GetVideoPath(mapName);
				if (videoName is null)
				{
					Console.WriteLine($"Couldn't find the video for {mapName}");
					continue;
				}

				// Add the songDesc name to the queue
				mapsList.Add(mapName);
			}
		}

		// Sort the list
		mapsList.Sort();

		// Add the list to the queue
		foreach (string map in mapsList)
			maps.Enqueue(map);

		// Now we have a list of all the songs we need to download
		Console.WriteLine($"We have to download {maps.Count} songs!");
	}

	public static async Task OnMessageCreated(DiscordClient client, MessageCreateEventArgs e)
	{
		ArgumentNullException.ThrowIfNull(client);

		ArgumentNullException.ThrowIfNull(e);

		// If the message is from ourselves, ignore it
		if (e.Author.IsCurrent)
			return;

		// If the author is not a bot, ignore it
		if (!e.Author.IsBot)
		{
			// If the message content is "skip", pop the first map ID and copy the command to the clipboard
			string message = e.Message.Content.ToLower();
			switch (message)
			{
				case "skip":
				case "next":
					maps.Dequeue();
					UpdateClipboard();
					await e.Message.RespondAsync($"Skipping to {maps.Peek()}");
					return;
				case "stop":
				case "exit":
				case "quit":
					Console.WriteLine("We stopping bois!");
					await e.Message.RespondAsync("Aight, we stoppin");

					// Disconnect the bot
					await client.DisconnectAsync();
					Environment.Exit(0);
					break;
				case "redo":
				case "retry":
					// Set clipboard again just in case
					UpdateClipboard();
					// Press ctrl+v to paste the command
					SendKeys.SendWait("^v");
					// Wait for .5 seconds
					await Task.Delay(100);
					// Press enter to send the command
					SendKeys.SendWait("{ENTER}");
					break;
				case "status":
				case "info":
					// If we have no map IDs left, return
					if (maps.Count != 0)
						// Say which song we're currently on and how many are left
						await e.Message.RespondAsync($"Currently on {maps.Peek()}. {maps.Count} songs left. We're {(downloading ? "" : "not")} downloading!");
					else
						// Say that we're done
						await e.Message.RespondAsync("We're done here!");
					return;
			}

			// If the message content is not "skip" or "stop", ignore it
			return;
		}

		if (downloading)
		{
			await e.Message.RespondAsync("We're currently downloading a video, please wait!");
			await e.Message.CreateReactionAsync(DiscordEmoji.FromName(client, ":x:"));
			return;
		}

		// Add a checkmark reaction to the message
		await e.Message.CreateReactionAsync(DiscordEmoji.FromName(client, ":white_check_mark:"));

		// If there's no embeds, wait for 1 second and recount the embeds up to a max of 10 times
		for (int i = 0; i < 10 && (e.Message.Embeds.Count == 0 || e.Message.Embeds[0].Fields == null); i++)
			await Task.Delay(1000);

		StringBuilder response = new();

		response.Append($"This message has {e.Message.Embeds.Count} embeds.\n");

		// If the message does not have 1 embeds, send the reply and return
		if (e.Message.Embeds.Count != 1)
		{
			await e.Message.RespondAsync(response.ToString());
			return;
		}

		// If fields is null, reply and return
		if (e.Message.Embeds[0].Fields == null)
		{
			response.Append($"And has no fields.\n");
			await e.Message.RespondAsync(response.ToString());
			return;
		}

		// If the first field's name is "Error", add a skull reaction and return
		if (e.Message.Embeds[0].Fields[0].Name == "Error")
		{
			await e.Message.CreateReactionAsync(DiscordEmoji.FromName(client, ":skull:"));
		}

		// Create a new instance of SongData
		NoHudDiscordEmbed songData = new();

		// The first embed contains the image URLs
		foreach (DiscordEmbedField field in e.Message.Embeds[0].Fields)
		{
			string name = field.Name;
			string? value = Program.CleanURL(field.Value);

			if (name == "Ultra:")
				songData.Ultra = value;
			else if (name == "Ultra HD:")
				songData.UltraHD = value;
			else if (name == "High:")
				songData.High = value;
			else if (name == "High HD:")
				songData.HighHD = value;
			else if (name == "Mid:")
				songData.Mid = value;
			else if (name == "Mid HD:")
				songData.MidHD = value;
			else if (name == "Low:")
				songData.Low = value;
			else if (name == "Low HD:")
				songData.LowHD = value;
			else if (name == "Audio:")
				songData.Audio = value;
		}

		// Download the map
		bool failed = !await DownloadMap(songData, maps.Peek());

		// Pop the map ID if we didn't fail or the fail counter is over 3
		if (failed)
		{
			failCounter++;

			if (failCounter >= 1)
			{
				failCounter = 0;
				maps.Dequeue();
			}
		}
		else
		{
			failCounter = 0;
			maps.Dequeue();
		}

		// If the queue is empty, return
		if (maps.Count == 0)
		{
			Console.WriteLine("We done here!");
			return;
		}

		// Copy the command and send it
		UpdateClipboard();
		await PasteAndSend();
	}

	private static async Task PasteAndSend()
	{
		// Press ctrl+v to paste the command
		SendKeys.SendWait("^v");

		// Wait for .5 seconds
		await Task.Delay(100);

		// Press enter to send the command
		SendKeys.SendWait("{ENTER}");
	}

	static async Task<bool> DownloadMap(NoHudDiscordEmbed songURLs, string mapName)
	{
		// If any of the URLs are null, return false (except for songTitleLogo ofc because it's optional)
		if (songURLs.UltraHD is null or "undefined")
		{
			Console.WriteLine($"Missing URL for {mapName}");
			return false;
		}

		Console.WriteLine($"Downloading the video for {mapName}");

		string? videoPath = GetVideoPath(mapName);
		if (videoPath is null)
		{
			Console.WriteLine($"Couldn't find the video for {mapName}");
			return false;
		}

		string downloadPath = Path.GetDirectoryName(videoPath)!;

		// Set downloading to true
		downloading = true;

		// Downloading the cache0 files
		string md5;
		try
		{
			md5 = await Download.DownloadFileMD5Async(songURLs.UltraHD, downloadPath);
		}
		catch
		{
			Console.WriteLine($"Failed to download {mapName}");
			downloading = false;
			return false;
		}

		// Delete the old video
		File.Delete(videoPath);

		// Move the new video to the correct location
		File.Move(Path.Combine(downloadPath, md5), videoPath);

		// Set downloading to false
		downloading = false;

		// Return true
		return true;
	}
}
