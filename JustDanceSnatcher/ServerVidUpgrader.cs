using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using JustDanceSnatcher.Helpers;
using JustDanceSnatcher.UbisoftStuff;

using System.Text;
using System.Text.Json;

namespace JustDanceSnatcher;

enum State
{
	Preview,
	Main
}

internal class ServerVidUpgrader
{
	// Queue of map IDs
	static readonly Queue<JDNextDatabaseEntry> mapIds = [];
	static string input = string.Empty;
	static uint failCounter = 0;

	static State state = State.Preview;
	static JDNextDiscordEmbed currentSongData = new();

	static DiscordClient? discord;

	static readonly JsonSerializerOptions options = new() 
	{
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	public static async Task Run()
	{
		Console.Clear();
		// Ask for the path to the cache.json file
		input = Question.AskFolder("Enter the path to your maps folder: ", true);

		// Add every map ID to the queue
		string[] folders = Directory.GetDirectories(input);
		for (int i = 0; i < folders.Length; i++)
		{
			string folder = folders[i];
			// In the folder, load the SongInfo.json file
			string json = File.ReadAllText(Path.Combine(folder, "SongInfo.json"));
			JDNextDatabaseEntry entry = JsonSerializer.Deserialize<JDNextDatabaseEntry>(json, options)!;

			// If the folder name doesn't match the parent map name, rename the folder
			if (Path.GetFileName(folder) != entry.parentMapName)
			{
				Directory.Move(folder, Path.Combine(input, entry.parentMapName));
				folder = Path.Combine(input, entry.parentMapName);
			}

			// If there's one video file in the video folder, add the map ID to the queue
			if (Directory.GetFiles(Path.Combine(folder, "video")).Length <= 1 || Directory.GetFiles(Path.Combine(folder, "videoPreview")).Length <= 1)
			{
				Console.WriteLine($"Adding {entry.parentMapName} to the queue");
				mapIds.Enqueue(entry);
			}

			// Deprecated since we're now using hashes as filenames
			//// Only if there's an UNKNOWN.webm file in the video folder
			//if (File.Exists(Path.Combine(folder, "video", "UNKNOWN.webm")))
			//{
			//	Console.WriteLine($"Adding {entry.parentMapName} to the queue");
			//	mapIds.Enqueue(entry);
			//}
		}

		// Create a new DiscordClientBuilder
		Console.WriteLine("Starting discord bot!");

		string token = File.ReadAllText("Secret.txt").Trim();

		discord = new(new DiscordConfiguration()
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
			SetClipboard();
		else
			return;

		// Wait for the bot to disconnect
		await Task.Delay(-1);
	}

	static void SetClipboard()
	{
		if (mapIds.Count == 0)
			throw new InvalidOperationException("No map IDs left!");

		string codename = mapIds.Peek().parentMapName;

		string command;

		if (state == State.Preview)
			//Program.SetClipboard($"/assets server:jdu codename:{codename}");
			command = $"/assets server:jdu codename:{codename}";
		else
			//Program.SetClipboard($"/nohud codename:{codename}");
			command = $"/nohud codename:{codename}";

		// Set the clipboard to the command
		Program.SetClipboard(command);
	}

	static async Task OnMessageCreated(DiscordClient client, MessageCreateEventArgs e)
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

				// Respond with the new map ID
				await e.Message.RespondAsync($"Skipping to {mapIds.Peek().parentMapName}");

				state = State.Preview;
				SetClipboard();
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
					await e.Message.RespondAsync($"Currently on {mapIds.Peek().parentMapName}. {mapIds.Count} songs left.");
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

		//if (e.Message.Embeds[0].Fields[0].Name == "Error")
		if (e.Message.Embeds.Count == 0 || (e.Message.Embeds[0].Fields?.Count == 1 && e.Message.Embeds[0].Fields[0].Name == "Error"))
		{
			// Increase the fail counter
			failCounter++;

			// If the fail counter is over 10, pop the map ID
			if (failCounter >= 1)
			{
				mapIds.Dequeue();
				failCounter = 0;

				// If the queue is empty, return
				if (mapIds.Count == 0)
				{
					Console.WriteLine("We done here!");
					return;
				}
			}

			// Copy the command to the clipboard
			state = State.Preview;
			SetClipboard();

			// Press ctrl+v to paste the command
			SendKeys.SendWait("^v");

			// Wait for .5 seconds
			await Task.Delay(100);

			// Press enter to send the command
			SendKeys.SendWait("{ENTER}");

			return;
		}

		// If the message does not have 3 embeds, send the reply and return
		if ((e.Message.Embeds.Count != 1 && state == State.Main) || (e.Message.Embeds.Count != 3 && state == State.Preview))
		{
			await e.Message.RespondAsync(response.ToString());
			return;
		}

		if (state == State.Main)
		{
			// If there's no embeds, wait for 1 second and recount the embeds up to a max of 10 times
			for (int i = 0; i < 100 && e.Message.Embeds[0].Fields == null; i++)
				await Task.Delay(100);

			if (e.Message.Embeds[0].Fields == null)
			{
				SetClipboard();

				// Press ctrl+v to paste the command
				SendKeys.SendWait("^v");

				// Wait for .5 seconds
				await Task.Delay(100);

				// Press enter to send the command
				SendKeys.SendWait("{ENTER}");

				return;
			}

			// The first embed contains the song info
			foreach (DiscordEmbedField field in e.Message.Embeds[0].Fields)
			{
				string name = field.Name;
				string? value = Program.CleanURL(field.Value);
				if (name == "Ultra HD:")
					currentSongData.ContentURLs.UltraHD = value;
				else if (name == "High HD:")
					currentSongData.ContentURLs.HighHD = value;
				else if (name == "Mid HD:")
					currentSongData.ContentURLs.MidHD = value;
				else if (name == "Low HD:")
					currentSongData.ContentURLs.LowHD = value;
			}
		}
		else 
		{
			// The second embed contains the preview URLs
			foreach (DiscordEmbedField field in e.Message.Embeds[1].Fields)
			{
				string name = field.Name;
				string? value = Program.CleanURL(field.Value);

				if (name == "Audio:")
					currentSongData.PreviewURLs.audioPreview = value;
				else if (name == "HIGH (vp9)")
					currentSongData.PreviewURLs.HIGHvp9 = value;
				else if (name == "LOW (vp9)")
					currentSongData.PreviewURLs.LOWvp9 = value;
				else if (name == "MID (vp9)")
					currentSongData.PreviewURLs.MIDvp9 = value;
				else if (name == "ULTRA (vp9)")
					currentSongData.PreviewURLs.ULTRAvp9 = value;
			}

			state = State.Main;
			SetClipboard();

			// Press ctrl+v to paste the command
			SendKeys.SendWait("^v");

			// Wait for .5 seconds
			await Task.Delay(100);

			// Press enter to send the command
			SendKeys.SendWait("{ENTER}");

			return;
		}

		// Download the map
		bool failed = !await DownloadMap(currentSongData, mapIds.Peek());
		if (failed)
			// If the download failed, increment the fail counter
			failCounter++;

		// Pop the map ID if we didn't fail or the fail counter is over 10
		if (!failed || failCounter >= 1)
		{
			mapIds.Dequeue();
			failCounter = 0;
		}

		// If the queue is empty, return
		if (mapIds.Count == 0 && state == State.Main)
		{
			Console.WriteLine("We done here!");
			return;
		}

		// Copy the command to the clipboard
		state = State.Preview;
		SetClipboard();

		// Press ctrl+v to paste the command
		SendKeys.SendWait("^v");

		// Wait for .5 seconds
		await Task.Delay(100);

		// Press enter to send the command
		SendKeys.SendWait("{ENTER}");
	}

	static async Task<bool> DownloadMap(JDNextDiscordEmbed songURLs, JDNextDatabaseEntry songInfo)
	{
		string mapName = songInfo.parentMapName;

		// If any of the URLs are null, return false (except for songTitleLogo ofc because it's optional)
		if (songURLs.PreviewURLs.audioPreview is null ||
			songURLs.ContentURLs.UltraHD is null ||
			songURLs.ContentURLs.HighHD is null ||
			songURLs.ContentURLs.MidHD is null ||
			songURLs.ContentURLs.LowHD is null ||
			songURLs.PreviewURLs.HIGHvp9 is null ||
			songURLs.PreviewURLs.LOWvp9 is null ||
			songURLs.PreviewURLs.MIDvp9 is null ||
			songURLs.PreviewURLs.ULTRAvp9 is null)
		{
			Console.WriteLine($"Missing URLs for {mapName}");
			return false;
		}

		Console.WriteLine($"Downloading the map {mapName}");

		// Output path
		string path = Path.Combine(input, mapName);

		// Delete the UNKNOWN.webm video files
		if (File.Exists(Path.Combine(path, "video", "UNKNOWN.webm")))
		{
			File.Delete(Path.Combine(path, "video", "UNKNOWN.webm"));
			File.Delete(Path.Combine(path, "videoPreview", "UNKNOWN.webm"));
			File.Delete(Path.Combine(path, "videoPreview", "LOW.webm"));
		}

		List<Task<string>> tasks = [];

		// TODO: somehow check if the file already exists and skip the download if it does
		// Gotta figure out how to get the hash before downloading the file
		tasks.Add(Download.DownloadFileMD5Async(songURLs.PreviewURLs.LOWvp9, Path.Combine(path, "videoPreview")));
		tasks.Add(Download.DownloadFileMD5Async(songURLs.PreviewURLs.MIDvp9, Path.Combine(path, "videoPreview")));
		tasks.Add(Download.DownloadFileMD5Async(songURLs.PreviewURLs.HIGHvp9, Path.Combine(path, "videoPreview")));
		tasks.Add(Download.DownloadFileMD5Async(songURLs.PreviewURLs.ULTRAvp9, Path.Combine(path, "videoPreview")));
		if (!Directory.Exists(Path.Combine(path, "AudioPreview_opus")) || Directory.GetFiles(Path.Combine(path, "AudioPreview_opus")).Length == 0)
			tasks.Add(Download.DownloadFileMD5Async(songURLs.PreviewURLs.audioPreview, Path.Combine(path, "AudioPreview_opus")));

		tasks.Add(Download.DownloadFileMD5Async(songURLs.ContentURLs.UltraHD, Path.Combine(path, "video")));
		tasks.Add(Download.DownloadFileMD5Async(songURLs.ContentURLs.HighHD, Path.Combine(path, "video")));
		tasks.Add(Download.DownloadFileMD5Async(songURLs.ContentURLs.MidHD, Path.Combine(path, "video")));
		tasks.Add(Download.DownloadFileMD5Async(songURLs.ContentURLs.LowHD, Path.Combine(path, "video")));

		// Wait for all downloads to complete
		await Task.WhenAll(tasks);

		// Return true
		return true;
	}
}
