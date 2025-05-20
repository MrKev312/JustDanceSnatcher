namespace JustDanceSnatcher.Helpers;

internal static class Question
{
	public static int Ask(List<string> options, int startIndex = 0, string? question = null)
	{
		if (options.Count == 0)
			return -1;

		if (question != null)
			Console.WriteLine(question);

		for (int i = 0; i < options.Count; i++)
			Console.WriteLine($"{i + startIndex})  {options[i]}");

		return AskNumber($"Select an option [{startIndex}-{options.Count + startIndex - 1}]: ", startIndex, options.Count + startIndex - 1);
	}

	public static string AskFolder(string question, bool mustExist = false)
	{
		string? folderPath;
		while (true)
		{
			Console.Write(question);
			folderPath = Console.ReadLine()?.Trim();

			if (string.IsNullOrWhiteSpace(folderPath))
			{
				Console.WriteLine("The path is empty. Please try again.");
				continue;
			}

			if (folderPath.StartsWith('"') && folderPath.EndsWith('"'))
				folderPath = folderPath[1..^1];

			if (mustExist && !Directory.Exists(folderPath))
			{
				Console.WriteLine("The specified folder does not exist. Please try again.");
				continue;
			}

			return folderPath;
		}
	}

	public static string AskFile(string question, bool mustExist = false, bool canBeUrl = false)
	{
		string? filePath;
		while (true)
		{
			Console.Write(question);
			filePath = Console.ReadLine()?.Trim();

			if (string.IsNullOrWhiteSpace(filePath))
			{
				Console.WriteLine("The path is empty. Please try again.");
				continue;
			}

			if (filePath.StartsWith('"') && filePath.EndsWith('"'))
				filePath = filePath[1..^1];

			bool pathValid = (!mustExist || File.Exists(filePath)) || (canBeUrl && (filePath.StartsWith("http://") || filePath.StartsWith("https://")));
			if (!pathValid)
			{
				Console.WriteLine("The specified file path is not valid or does not exist. Please try again.");
				continue;
			}

			return filePath;
		}
	}

	public static int AskNumber(string question, int min = int.MinValue, int max = int.MaxValue)
	{
		while (true)
		{
			Console.Write(question);
			string? numberStr = Console.ReadLine()?.Trim();

			if (string.IsNullOrWhiteSpace(numberStr))
			{
				Console.WriteLine("The input is empty. Please try again.");
				continue;
			}

			if (numberStr.StartsWith('"') && numberStr.EndsWith('"'))
				numberStr = numberStr[1..^1];

			if (!int.TryParse(numberStr, out int readNumber))
			{
				Console.WriteLine("The input is not a valid number. Please try again.");
				continue;
			}

			if (readNumber < min)
			{
				Console.WriteLine($"The number must be greater than or equal to {min}. Please try again.");
				continue;
			}

			if (readNumber > max)
			{
				Console.WriteLine($"The number must be less than or equal to {max}. Please try again.");
				continue;
			}

			return readNumber;
		}
	}

	public static string AskForUrl(string assetName, bool canBeEmpty = false)
	{
		string? url;
		while (true)
		{
			string prompt = canBeEmpty ? $"{assetName} (URL, can be empty): " : $"{assetName} (URL): ";
			Console.Write(prompt);
			url = Console.ReadLine(); // Keep null if empty, don't trim yet

			if (string.IsNullOrWhiteSpace(url))
			{
				if (canBeEmpty)
					return string.Empty;
				Console.WriteLine("URL cannot be empty for this asset. Please provide a URL.");
				continue;
			}

			url = url.Trim(); // Trim non-empty URLs

			if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine("Invalid URL format. Must start with http:// or https://. Please try again.");
				continue;
			}

			break; // URL is valid (or confirmed by user if above check is active)
		}

		return url;
	}
}