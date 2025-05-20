using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using JustDanceSnatcher.Helpers;
using JustDanceSnatcher.UbisoftStuff;

using System.Text;
using System.Text.Json;

namespace JustDanceSnatcher;

internal class JDNextDownloading
{
	// Queue of map IDs
	static readonly Queue<KeyValuePair<string, JDNextDatabaseEntry>> mapIds = [];
	static Dictionary<string, JDNextDatabaseEntry> jDCacheJSON = [];
	static Dictionary<string, string> jdBundleTag = [];
	static string output = string.Empty;
	static uint failCounter = 0;

	static readonly JsonSerializerOptions options = new() 
	{
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	public static async Task Run()
	{
		Console.Clear();
		// Ask for the path to the cache.json file
		string path = Question.AskFile("Enter the path to the jnext database json file: ", true, true);
		output = Question.AskFolder("Enter the path to your maps folder: ", true);

		// If the path starts with http, get content as string
		string json = string.Empty;
		if (path.StartsWith("http"))
		{
			using HttpClient client = new();
			json = await client.GetStringAsync(path);
		}
		else
		{
			json = File.ReadAllText(path);
		}

		jDCacheJSON = JsonSerializer.Deserialize<Dictionary<string, JDNextDatabaseEntry>>(json)!;

		// If we have a pre-existing cache, load it and remove all the mapIDs that are already in the cache
		HandlePreExistingCache();

		// Now we have a list of all the mapIDs that are missing
		Console.WriteLine($"We have to download {jDCacheJSON.Count} songs!");

		// Add all the missing mapIDs to the queue
		foreach (KeyValuePair<string, JDNextDatabaseEntry> entry in jDCacheJSON)
			mapIds.Enqueue(entry);

		// Create a new DiscordClientBuilder
		Console.WriteLine("Starting discord bot!");

		string token = File.ReadAllText("Secret.txt").Trim();

		DiscordClient discord = new DiscordClient(new DiscordConfiguration()
		{
			Token = token,
			TokenType = TokenType.Bot,
			Intents = DiscordIntents.MessageContents | DiscordIntents.AllUnprivileged
		});

		// Add the message created event
		discord.MessageCreated += OnMessageCreated;

		// Connect to Discord
		await discord.ConnectAsync();

		Console.WriteLine($"Downloading {mapIds.Count} map ids");

		// Set the clipboard to the first map ID
		if (mapIds.Count != 0)
			Program.SetClipboard($"/assets server:jdnext codename:{mapIds.Peek().Value.parentMapName}");
		else
			return;

		// Wait for the bot to disconnect
		await Task.Delay(-1);
	}

	static void HandlePreExistingCache()
	{
		// Each folder in the output directory is a map codename
		string[] alreadyDownloaded = Directory.GetDirectories(output).Select(x => Path.GetFileName(x)).ToArray();

		// Remove any where parentMapName is in the names array
		jDCacheJSON = jDCacheJSON.Where(x =>
		{
			if (!alreadyDownloaded.Contains(x.Value.parentMapName))
				return true;

			// Parse its SongInfo.json file
			string json = File.ReadAllText(Path.Combine(output, x.Value.parentMapName, "SongInfo.json"));
			JDNextDatabaseEntry entry = JsonSerializer.Deserialize<JDNextDatabaseEntry>(json, options)!;

			// If it has a custom tag, delete the folder and return true
			if (entry.tags.Contains("Custom"))
			{
				Console.WriteLine($"Redownloading {x.Value.parentMapName} as it's now official");

				// If any of the tags starts with songpack, store it in the jdBundleTag dictionary
				foreach (string tag in entry.tags)
				{
					if (tag.StartsWith("songpack"))
						jdBundleTag[x.Value.parentMapName] = tag;
				}

				Directory.Delete(Path.Combine(output, x.Value.parentMapName), true);
				return true;
			}

			Console.WriteLine($"Removing {x.Value.parentMapName} from the list of songs to download");
			return false;
		}).ToDictionary(x => x.Key, x => x.Value);
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
			if (e.Message.Content.Equals("skip", StringComparison.CurrentCultureIgnoreCase))
			{
				mapIds.Dequeue();
				Program.SetClipboard($"/assets server:jdnext codename:{mapIds.Peek().Value.parentMapName}");
				return;
			}
			else if (e.Message.Content.Equals("stop", StringComparison.CurrentCultureIgnoreCase))
			{
				Console.WriteLine("We stopping bois!");
				await e.Message.RespondAsync("Aight, we stoppin");

				// Disconnect the bot
				await client.DisconnectAsync();
				Environment.Exit(0);
			}
			else if (e.Message.Content.Equals("info", StringComparison.CurrentCultureIgnoreCase))
			{
				// If we have no map IDs left, return
				if (mapIds.Count != 0)
					// Say which song we're currently on and how many are left
					await e.Message.RespondAsync($"Currently on {mapIds.Peek().Value.parentMapName}. {mapIds.Count} songs left.");
				else
					// Say that we're done
					await e.Message.RespondAsync("We're done here!");
				return;
			}

			// If the message content is not "skip" or "stop", ignore it
			return;
		}

		// Add a checkmark reaction to the message
		await e.Message.CreateReactionAsync(DiscordEmoji.FromName(client, ":white_check_mark:"));

		// If there's no embeds, wait for 1 second and recount the embeds up to a max of 10 times
		for (int i = 0; i < 10 && e.Message.Embeds.Count == 0; i++)
			await Task.Delay(1000);

		StringBuilder response = new();

		response.Append($"Message from {e.Author.Username}#{e.Author.Discriminator} in {e.Channel.Name}:\n");
		response.Append($"This message has {e.Message.Embeds.Count} embeds.\n");

		// If the message does not have 3 embeds, send the reply and return
		if (e.Message.Embeds.Count != 3)
		{
			await e.Message.RespondAsync(response.ToString());
			return;
		}

		// Create a new instance of SongData
		JDNextDiscordEmbed songData = new();

		// The first embed contains the image URLs
		foreach (DiscordEmbedField field in e.Message.Embeds[0].Fields)
		{
			string name = field.Name;
			string? value = Program.CleanURL(field.Value);

			if (name == "coachesSmall:")
				songData.ImageURLs.coachesSmall = value;
			else if (name == "coachesLarge:")
				songData.ImageURLs.coachesLarge = value;
			else if (name == "Cover:")
				songData.ImageURLs.Cover = value;
			else if (name == "Cover1024:")
				songData.ImageURLs.Cover1024 = value;
			else if (name == "CoverSmall:")
				songData.ImageURLs.CoverSmall = value;
			else if (name == "Song Title Logo:")
				songData.ImageURLs.SongTitleLogo = value;
		}

		// The second embed contains the preview URLs
		foreach (DiscordEmbedField field in e.Message.Embeds[1].Fields)
		{
			string name = field.Name;
			string? value = Program.CleanURL(field.Value);

			if (name == "audioPreview.opus:")
				songData.PreviewURLs.audioPreview = value;
			else if (name == "HIGH.vp8.webm:")
				songData.PreviewURLs.HIGHvp8 = value;
			else if (name == "HIGH.vp9.webm:")
				songData.PreviewURLs.HIGHvp9 = value;
			else if (name == "LOW.vp8.webm:")
				songData.PreviewURLs.LOWvp8 = value;
			else if (name == "LOW.vp9.webm:")
				songData.PreviewURLs.LOWvp9 = value;
			else if (name == "MID.vp8.webm:")
				songData.PreviewURLs.MIDvp8 = value;
			else if (name == "MID.vp9.webm:")
				songData.PreviewURLs.MIDvp9 = value;
			else if (name == "ULTRA.vp8.webm:")
				songData.PreviewURLs.ULTRAvp8 = value;
			else if (name == "ULTRA.vp9.webm:")
				songData.PreviewURLs.ULTRAvp9 = value;
		}

		// The third embed contains the content URLs
		foreach (DiscordEmbedField field in e.Message.Embeds[2].Fields)
		{
			string name = field.Name;
			string? value = Program.CleanURL(field.Value);

			if (name == "Ultra HD:")
				songData.ContentURLs.UltraHD = value;
			else if (name == "Ultra VP9:")
				songData.ContentURLs.Ultravp9 = value;
			else if (name == "High HD:")
				songData.ContentURLs.HighHD = value;
			else if (name == "High VP9:")
				songData.ContentURLs.Highvp9 = value;
			else if (name == "Mid HD:")
				songData.ContentURLs.MidHD = value;
			else if (name == "Mid VP9:")
				songData.ContentURLs.Midvp9 = value;
			else if (name == "Low HD:")
				songData.ContentURLs.LowHD = value;
			else if (name == "Low VP9:")
				songData.ContentURLs.Lowvp9 = value;
			else if (name == "Audio:")
				songData.ContentURLs.Audio = value;
			else if (name == "mapPackage:")
				songData.ContentURLs.mapPackage = value;
		}

		// Download the map
		bool failed = !await DownloadMap(songData, mapIds.Peek());
		if (failed)
			// If the download failed, increment the fail counter
			failCounter++;

		// Pop the map ID if we didn't fail or the fail counter is over 10
		if (!failed || failCounter > 10)
		{
			mapIds.Dequeue();
			failCounter = 0;
		}

		// If the queue is empty, return
		if (mapIds.Count == 0)
		{
			Console.WriteLine("We done here!");
			return;
		}

		// Copy the command to the clipboard
		Program.SetClipboard($"/assets server:jdnext codename:{mapIds.Peek().Value.parentMapName}");

		// Press ctrl+v to paste the command
		SendKeys.SendWait("^v");

		// Wait for .5 seconds
		await Task.Delay(100);

		// Press enter to send the command
		SendKeys.SendWait("{ENTER}");
	}

	static void AddMissingTitles()
	{
		//// First we grab all entries where the title is missing and the title cover is present in the database
		//List<JDSong> songs;
		//// For each song, we download the titlecover and add it to the cache
		//Parallel.ForEach(songs, AddMissingTitle);
	}

	static void AddMissingTitle(string songID)
	{
		// The database entry for the song
		JDNextDatabaseEntry entry = jDCacheJSON.Values.AsParallel().Where(x => x.parentMapName == songID).First();

		// The path to the title cover
		string path = Path.Combine(output, songID, "songTitleLogo");

		// Download the title cover and get the MD5 hash
		string md5 = Download.DownloadFileMD5(entry.assets.songTitleLogo!, path);
	}

	static async Task<bool> DownloadMap(JDNextDiscordEmbed songURLs, KeyValuePair<string, JDNextDatabaseEntry> songInfo)
	{
		string mapName = songInfo.Value.parentMapName;
		// If any of the URLs are null, return false (except for songTitleLogo ofc because it's optional)
		if (songURLs.ContentURLs.Audio is null ||
			songURLs.ContentURLs.mapPackage is null ||
			songURLs.ContentURLs.UltraHD is null ||
			songURLs.ContentURLs.Highvp9 is null ||
			songURLs.ContentURLs.Midvp9 is null ||
			songURLs.ContentURLs.Lowvp9 is null ||
			songURLs.ImageURLs.Cover is null ||
			songURLs.ImageURLs.coachesLarge is null ||
			songURLs.ImageURLs.coachesSmall is null ||
			songInfo.Value.assets is null)
		{
			Console.WriteLine($"Missing URLs for {mapName}");
			return false;
		}

		Console.WriteLine($"Downloading the map {mapName}");

		// Output path
		string path = Path.Combine(output, mapName);

		List<Task<string>> tasks =
		[
			// Downloading the preview files
			Download.DownloadFileMD5Async(songInfo.Value.assets.audioPreviewopus, Path.Combine(path, "AudioPreview_opus")),
			Download.DownloadFileMD5Async(songInfo.Value.assets.videoPreview_ULTRAvp9webm, Path.Combine(path, "videoPreview"), "ULTRA"),
			Download.DownloadFileMD5Async(songInfo.Value.assets.videoPreview_HIGHvp9webm, Path.Combine(path, "videoPreview"), "HIGH"),
			Download.DownloadFileMD5Async(songInfo.Value.assets.videoPreview_MIDvp9webm, Path.Combine(path, "videoPreview"), "MID"),
			Download.DownloadFileMD5Async(songInfo.Value.assets.videoPreview_LOWvp9webm, Path.Combine(path, "videoPreview"), "LOW"),

			// Downloading the image files
			Download.DownloadFileMD5Async(songURLs.ImageURLs.Cover!, Path.Combine(path, "Cover")),
			Download.DownloadFileMD5Async(songURLs.ImageURLs.coachesLarge!, Path.Combine(path, "CoachesLarge")),
			Download.DownloadFileMD5Async(songURLs.ImageURLs.coachesSmall!, Path.Combine(path, "CoachesSmall")),

			// Downloading the content files
			Download.DownloadFileMD5Async(songURLs.ContentURLs.Audio!, Path.Combine(path, "Audio_opus")),
			Download.DownloadFileMD5Async(songURLs.ContentURLs.UltraHD!, Path.Combine(path, "video"), "ULTRA"),
			Download.DownloadFileMD5Async(songURLs.ContentURLs.Highvp9!, Path.Combine(path, "video"), "HIGH"),
			Download.DownloadFileMD5Async(songURLs.ContentURLs.Midvp9!, Path.Combine(path, "video"), "MID"),
			Download.DownloadFileMD5Async(songURLs.ContentURLs.Lowvp9!, Path.Combine(path, "video"), "LOW"),
			Download.DownloadFileMD5Async(songURLs.ContentURLs.mapPackage!, Path.Combine(path, "MapPackage"))
		];

		if (songInfo.Value.hasSongTitleInCover)
		{
			tasks.Add(Download.DownloadFileMD5Async(songInfo.Value.assets.songTitleLogo!, Path.Combine(path, "songTitleLogo"))!);
		}

		// Wait for all downloads to complete
		await Task.WhenAll(tasks);

		// Now we add the SongInfo.json file
		songInfo.Value.songID = songInfo.Key;

		// If the song has a songpack tag, add it to the tags
		if (jdBundleTag.ContainsKey(mapName))
			songInfo.Value.tags.Add(jdBundleTag[mapName]);

		string json = JsonSerializer.Serialize(songInfo.Value, options);
		File.WriteAllText(Path.Combine(path, "SongInfo.json"), json);

		// Return true
		return true;
	}
}
