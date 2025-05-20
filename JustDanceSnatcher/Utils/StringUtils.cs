namespace JustDanceSnatcher.Utils;

public static class StringUtils
{
	/// <summary>
	/// Cleans a URL extracted from a Discord embed field, removing [Link](...) markdown.
	/// </summary>
	/// <param name="urlFieldContent">The raw string value from the embed field.</param>
	/// <returns>The cleaned URL, or null if not a valid link format or "undefined".</returns>
	public static string? CleanDiscordEmbedURL(string urlFieldContent)
	{
		if (string.IsNullOrWhiteSpace(urlFieldContent))
			return null;

		if (urlFieldContent.StartsWith("[Link](") && urlFieldContent.EndsWith(')'))
		{
			string extractedUrl = urlFieldContent[7..^1];
			return extractedUrl.Equals("undefined", StringComparison.OrdinalIgnoreCase) ? null : extractedUrl;
		}

		return null;
	}
}