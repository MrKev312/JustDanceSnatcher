using System.Security.Cryptography;

namespace JustDanceSnatcher.Helpers;

public static class Download
{
	static readonly Lazy<HttpClient> client = new(() =>
	{
		HttpClient client = new();
		client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
		return client;
	});

	/// <summary>
	/// Downloads a file from a URL to a folder and renames it to the MD5 hash of the file if no filename is provided
	/// </summary>
	/// <returns>The name of the file</returns>
	public static string DownloadFileMD5(string url, string folderPath, string? filename = null, bool errorIfFileExists = false)
	{
		// Create the destination folder if it doesn't exist
		Directory.CreateDirectory(folderPath);

		// Get the extension of the file
		Uri uri = new(url);
		string urlFileName = uri.Segments.Last();

		// Second to last segment is the folder name
		string thingDownloading = uri.Segments[^2].Trim('/');

		// Download the file as "temp"
		string tempFilePath;
		if (filename == null)
			tempFilePath = Path.Combine(folderPath, "temp");
		else
			tempFilePath = Path.Combine(folderPath, $"{filename}{Path.GetExtension(urlFileName)}");

		try
		{
			client.Value.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
			using HttpResponseMessage response = client.Value.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).Result;
			response.EnsureSuccessStatusCode();
			long? contentLength = response.Content.Headers.ContentLength;
			if (contentLength.HasValue)
				Console.WriteLine($"Downloading {thingDownloading} ({contentLength.Value / 1024 / 1024} MB)");
			using Stream download = response.Content.ReadAsStream();
			using FileStream fileStream = File.Create(tempFilePath);
			// Copy with 10 MB buffer
			download.CopyTo(fileStream, 10 * 1024 * 1024);
		}
		catch (Exception e)
		{
			throw new Exception($"Failed to download the file: {e.Message}");
		}

		if (filename != null)
			return tempFilePath;

		// Get the MD5 hash of the file
		string hashString = GetFileMD5(tempFilePath) + Path.GetExtension(urlFileName);

		// Rename the file to the MD5 hash
		string filePath = Path.Combine(folderPath, hashString);

		if (File.Exists(filePath))
		{
			if (errorIfFileExists)
				throw new Exception($"The file {urlFileName} already exists.");

			Console.WriteLine($"The file {urlFileName} already exists, skipping.");
			return hashString;
		}

		File.Move(tempFilePath, filePath, true);

		return hashString;
	}

	private static string DownloadFileMD5Retry(string url, string folderPath, string? filename, bool errorIfFileExists = false)
	{
		for (int i = 0; i < 10; i++)
		{
			try
			{
				return DownloadFileMD5(url, folderPath, filename, errorIfFileExists);
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error downloading file: {e.Message}");
				Console.WriteLine($"Retrying... ({i + 1}/10)");
			}
		}

		throw new Exception("Failed to download the file.");
	}

	public static Task<string> DownloadFileMD5Async(string url, string folderPath, string? filename = null, bool errorIfFileExists = false)
	{
		return Task.Run(() => DownloadFileMD5Retry(url, folderPath, filename, errorIfFileExists));
	}

	public static string GetFileMD5(string filePath)
	{
		// Get a stream from the file
		using FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
		byte[] hash = MD5.HashData(fileStream);

		// Convert the byte array to a hex string
		string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

		return hashString;
	}

	public static void DownloadFile(string url, string folderPath, string? fileName = null, bool errorIfFileExists = false)
	{
		Uri uri = new(url);

		fileName ??= uri.Segments.Last();

		string filePath = Path.Combine(folderPath, fileName);

		if (File.Exists(filePath))
		{
			if (errorIfFileExists)
				throw new Exception($"The file {fileName} already exists.");

			Console.WriteLine($"The file {fileName} already exists, skipping.");
			return;
		}

		Directory.CreateDirectory(folderPath);

		using HttpClient client = new();
		using Stream stream = client.GetStreamAsync(url).Result;
		using FileStream fileStream = File.Create(filePath);
		stream.CopyTo(fileStream);
	}
}
