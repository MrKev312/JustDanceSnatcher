using JustDanceSnatcher.Helpers;

using System.Text.Json;

namespace JustDanceSnatcher;

internal class Program
{
	// Default JSON serialization options
	// This prevents weird \uXXXX characters from appearing in the JSON when possible
	public static readonly JsonSerializerOptions jsonOptions = new() 
	{ 
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	static async Task Main()
	{
		Console.Clear();
		Console.WriteLine("--- JustDanceSnatcher Utilities ---");
		List<string> mainOptions =
		[
			"JDNext Download (Discord Bot)",
			"Server Video Upgrader (Discord Bot)",
			"UbiArt Upgrade Video (Discord Bot)",
			"Create ContentAuthorization JSON (Interactive)",
			"Download from ContentAuthorization JSON",
			"Fix Custom Audio Volume (FFmpeg)",
			"Convert Playlist (Next to Next+ format)",
			"Steal Covers (Manual Extraction Helper)"
		];

		int choice = Question.Ask(mainOptions, 0, "Select an operation:");

		switch (choice)
		{
			case 0:
				await new JDNextDownloader().RunAsync();
				break;
			case 1:
				await new ServerVidUpgrader().RunAsync();
				break;
			case 2:
				await new UbiArtVidUpgrader().RunAsync();
				break;
			case 3:
				Temp.CreateContentAuthorization();
				break;
			case 4:
				await Temp.DownloadFromContentAuthorizationAsync();
				break;
			case 5:
				Temp.FixAudio();
				break;
			case 6:
				PlaylistConverter.Convert();
				break;
			case 7:
				StealCover.Run();
				break;
			default:
				Console.WriteLine("Invalid option selected.");
				break;
		}
		Console.WriteLine("\nOperation finished. Press any key to exit.");
		Console.ReadKey();
	}

	public static void SetClipboard(string text)
	{
		// Create a new thread
		Thread thread = new(() => Clipboard.SetText(text));
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		thread.Join();
	}

	/// <summary>
	/// Cleans a URL extracted from a Discord embed field, removing [Link](...) markdown.
	/// </summary>
	/// <param name="urlFieldContent">The raw string value from the embed field.</param>
	/// <returns>The cleaned URL, or null if not a valid link format or "undefined".</returns>
	public static string? CleanURL(string urlFieldContent)
	{
		if (string.IsNullOrWhiteSpace(urlFieldContent)) return null;

		// Format: [Link](ActualURL)
		if (urlFieldContent.StartsWith("[Link](") && urlFieldContent.EndsWith(")"))
		{
			string extractedUrl = urlFieldContent[7..^1]; // Extract "ActualURL"
			return extractedUrl.Equals("undefined", StringComparison.OrdinalIgnoreCase) ? null : extractedUrl;
		}

		// Format might also be just the URL if the bot doesn't use markdown links consistently
		// or if the field isn't meant to be a clickable link in the embed.
		// For now, strictly check the [Link](...) format. If other formats are possible, add checks.
		// Or, if it's sometimes just a raw URL:
		// if (urlFieldContent.StartsWith("http://") || urlFieldContent.StartsWith("https://")) return urlFieldContent;

		// If not the expected markdown link format, and not a raw URL (if that check is added),
		// assume it's not a URL we want to clean, or it's an invalid/unintended value.
		// Original code returned null if not starting with "[Link]".
		return null;
	}

	/// <summary>
	/// Sets clipboard text and simulates Ctrl+V, Enter.
	/// Highly dependent on the active window being the Discord client.
	/// </summary>
	public static async Task SendCommandToDiscord(string command)
	{
		SetClipboard(command);
		try
		{
			// Brief pause to ensure clipboard is set and target window can process.
			await Task.Delay(200);
			SendKeys.SendWait("^v");
			await Task.Delay(100); // Pause between Ctrl+V and Enter
			SendKeys.SendWait("{ENTER}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"ERROR: Failed to send keys for command ('{command}'). Ensure Discord is the active window. Details: {ex.Message}");
			Console.WriteLine("You may need to paste and send the command manually.");
			// Potentially pause here and ask user to confirm manual send.
		}
	}
}
