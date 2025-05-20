using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using JustDanceSnatcher.Interaction;

namespace JustDanceSnatcher.Discord;

internal abstract class DiscordAssetDownloaderBase<TItem, TEmbedData>
	where TEmbedData : class, new()
{
	protected readonly Queue<TItem> ItemQueue = new();
	protected uint FailCounterForCurrentItem = 0;
	protected DiscordClient DiscordBotClient = null!; // Initialized in SetupDiscordBot
	protected string OutputDirectory = string.Empty;

	// Configuration for derived classes
	protected abstract string BotToken { get; }
	protected virtual DiscordIntents BotIntents => DiscordIntents.MessageContents | DiscordIntents.AllUnprivileged;
	protected virtual int MaxRetriesPerItem => 3; // How many times ProcessItem can fail before skipping
	protected virtual int MaxCommandSendRetries => 1; // How many times sending a command can fail (e.g. error embed) before skipping
	protected virtual int ExpectedEmbedCount => 0; // 0 for any > 0; specific number otherwise.

	// Abstract methods for derived class implementation
	protected abstract Task InitializeAsync(); // Populate ItemQueue, OutputDirectory, etc.
	protected abstract string GetDiscordCommandForItem(TItem item); // Gets the Discord command string (e.g., /assets ...)
	protected abstract TEmbedData? ParseEmbedsToData(DiscordMessage message); // Parses Discord message embeds into TEmbedData
	protected abstract Task<bool> ProcessDataItemAsync(TEmbedData embedData, TItem currentItem); // Processes the parsed data (e.g., downloads files)

	public async Task RunAsync()
	{
		await InitializeAsync();

		if (ItemQueue.Count == 0)
		{
			Console.WriteLine("No items to process.");
			return;
		}

		await SetupDiscordBotAsync();

		Console.WriteLine($"Starting to process {ItemQueue.Count} items.");
		await SendNextDiscordCommandAsync();

		await Task.Delay(-1); // Keep the application alive
	}

	private async Task SetupDiscordBotAsync()
	{
		Console.WriteLine("Initializing Discord bot...");
		DiscordBotClient = new DiscordClient(new DiscordConfiguration()
		{
			Token = BotToken,
			TokenType = TokenType.Bot,
			Intents = BotIntents
		});

		DiscordBotClient.MessageCreated += OnDiscordMessageCreatedAsync;
		// Consider other events like Ready, GuildAvailable if needed.

		await DiscordBotClient.ConnectAsync();
		Console.WriteLine("Discord bot connected successfully.");
	}

	protected async Task SendNextDiscordCommandAsync()
	{
		if (ItemQueue.Count == 0)
		{
			Console.WriteLine("All items processed!");
			await ShutdownBotAsync();
			return;
		}

		TItem currentItem = ItemQueue.Peek();
		string command = GetDiscordCommandForItem(currentItem);
		Console.WriteLine($"Sending command for item: {currentItem} -> {command}");
		await SystemInteraction.SendCommandViaClipboard(command);
	}

	protected virtual async Task OnDiscordMessageCreatedAsync(DiscordClient sender, MessageCreateEventArgs e)
	{
		if (e.Author.IsCurrent || !e.Author.IsBot) // Process commands from non-bot users, ignore self and other bots unless overridden
		{
			await HandleUserCommandAsync(e);
			return;
		}

		// It's a response from a bot, presumably to our command
		Console.WriteLine($"Received bot response from {e.Author.Username} in {e.Channel.Name}. Checking embeds...");
		await e.Message.CreateReactionAsync(DiscordEmoji.FromName(sender, ":white_check_mark:"));

		// Wait for embeds if they are not immediately available
		for (int i = 0; i < 10 && e.Message.Embeds.Count == 0; i++)
		{
			await Task.Delay(1000);
		}

		if (e.Message.Embeds.Count == 0)
		{
			Console.WriteLine("No embeds found in bot message.");
			await HandleCommandFailureAsync("No embeds received.");
			return;
		}

		if (ExpectedEmbedCount > 0 && e.Message.Embeds.Count != ExpectedEmbedCount)
		{
			Console.WriteLine($"Expected {ExpectedEmbedCount} embeds, but found {e.Message.Embeds.Count}.");
			await HandleCommandFailureAsync($"Incorrect embed count: {e.Message.Embeds.Count}");
			return;
		}

		if (IsErrorEmbed(e.Message))
		{
			Console.WriteLine("Received an error embed from the bot.");
			await HandleCommandFailureAsync("Error embed received.");
			return;
		}

		if (ItemQueue.Count == 0)
		{
			Console.WriteLine("Received bot message but queue is empty. Ignoring.");
			return;
		}

		TItem currentItem = ItemQueue.Peek();

		TEmbedData? embedData = ParseEmbedsToData(e.Message);
		if (embedData == null)
		{
			Console.WriteLine("Failed to parse embeds into data object.");
			await HandleItemProcessingFailureAsync(currentItem, "Embed parsing failed.");
			return;
		}

		Console.WriteLine($"Successfully parsed embeds for item: {currentItem}. Processing...");
		bool success = await ProcessDataItemAsync(embedData, currentItem);

		if (success)
		{
			Console.WriteLine($"Successfully processed item: {currentItem}.");
			FailCounterForCurrentItem = 0;
			ItemQueue.Dequeue(); // Move to next item
			await SendNextDiscordCommandAsync();
		}
		else
		{
			await HandleItemProcessingFailureAsync(currentItem, "ProcessDataItemAsync returned false.");
		}
	}

	protected virtual bool IsErrorEmbed(DiscordMessage message)
	{
		if (message.Embeds.Any())
		{
			var firstEmbed = message.Embeds[0];
			return firstEmbed.Fields?.Any(f => f.Name.Equals("Error", StringComparison.OrdinalIgnoreCase)) ?? false;
		}

		return false;
	}

	protected async Task HandleCommandFailureAsync(string reason)
	{
		FailCounterForCurrentItem++;
		Console.WriteLine($"Command send/response failed for current item. Reason: {reason}. Attempt {FailCounterForCurrentItem}/{MaxCommandSendRetries}.");

		if (FailCounterForCurrentItem >= MaxCommandSendRetries || ItemQueue.Count == 0)
		{
			Console.WriteLine(ItemQueue.Count > 0
				? $"Max command retries ({MaxCommandSendRetries}) reached for item {ItemQueue.Peek()}. Skipping item."
				: "Queue is empty, cannot retry command.");

			if (ItemQueue.Count > 0)
				ItemQueue.Dequeue();
			FailCounterForCurrentItem = 0; // Reset for next item
			await SendNextDiscordCommandAsync();
		}
		else
		{
			Console.WriteLine("Retrying command for current item.");
			await SendNextDiscordCommandAsync(); // Retry command for the same item
		}
	}

	protected async Task HandleItemProcessingFailureAsync(TItem item, string reason)
	{
		FailCounterForCurrentItem++;
		Console.WriteLine($"Processing failed for item: {item}. Reason: {reason}. Attempt {FailCounterForCurrentItem}/{MaxRetriesPerItem}.");

		if (FailCounterForCurrentItem >= MaxRetriesPerItem)
		{
			Console.WriteLine($"Max processing retries ({MaxRetriesPerItem}) reached for item {item}. Skipping item.");
			ItemQueue.Dequeue();
			FailCounterForCurrentItem = 0; // Reset for next item
			await SendNextDiscordCommandAsync();
		}
		else
		{
			Console.WriteLine("Retrying command for current item due to processing failure.");
			await SendNextDiscordCommandAsync();
		}
	}

	protected virtual async Task HandleUserCommandAsync(MessageCreateEventArgs e)
	{
		string content = e.Message.Content.Trim().ToLowerInvariant();
		switch (content)
		{
			case "skip":
			case "!skip":
				if (ItemQueue.Count > 0)
				{
					var skippedItem = ItemQueue.Dequeue();
					FailCounterForCurrentItem = 0;
					Console.WriteLine($"User skipped item: {skippedItem}");
					await e.Message.RespondAsync($"Skipped: `{skippedItem}`. {(ItemQueue.Count > 0 ? $"Next: `{ItemQueue.Peek()}`" : "Queue is now empty.")}");
					await SendNextDiscordCommandAsync();
				}
				else
					await e.Message.RespondAsync("Queue is empty, nothing to skip.");
				break;
			case "stop":
			case "!stop":
				Console.WriteLine("User requested stop.");
				await e.Message.RespondAsync("Stopping bot as requested...");
				await ShutdownBotAsync();
				break;
			case "info":
			case "!info":
				if (ItemQueue.Count > 0)
					await e.Message.RespondAsync($"Currently on: `{ItemQueue.Peek()}`. Items remaining: {ItemQueue.Count}. Failures for current item: {FailCounterForCurrentItem}.");
				else
					await e.Message.RespondAsync("Queue is empty.");
				break;
		}
	}

	protected async Task ShutdownBotAsync()
	{
		Console.WriteLine("Shutting down Discord bot...");
		if (DiscordBotClient != null)
		{
			await DiscordBotClient.DisconnectAsync();
			DiscordBotClient.Dispose();
		}

		Console.WriteLine("Bot shutdown complete. Exiting application.");
		Environment.Exit(0);
	}
}