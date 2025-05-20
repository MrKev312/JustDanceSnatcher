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
		// Ask for the path to the cache.json file
		int option = Question.Ask([
			"JDNext download",
			"Server vid upgrader",
			"UbiArt upgrade video",
			"CreateContentAuthorization",
			"Download from contentAuthorization",
			"Fix Audio Volume",
			"Convert playlist from next to next+",
			"Steal covers >:)"
			]);

		switch (option)
		{
			case 0:
				await JDNextDownloading.Run();
				break;
			case 1:
				await ServerVidUpgrader.Run();
				break;
			case 2:
				await UbiArtUpgradeVideo.Run();
				break;
			case 3:
				Temp.CreateContentAuthorization();
				break;
			case 4:
				Temp.DownloadFromContentAuthorization();
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

		}
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
	/// Cleans a URL from a Discord embed field
	/// </summary>
	/// <param name="url">The URL to clean</param>
	/// <returns></returns>
	public static string? CleanURL(string url)
	{
		// If the URL doesn't start with [Link] return null
		if (!url.StartsWith("[Link]"))
			return null;

		// Remove the [Link]( ... ) prefix and suffix
		url = url[7..^1];

		if (url == "undefined")
			return null;

		return url;
	}
}
