using System.Security.Cryptography;
using System.Net.Http.Headers;

namespace JustDanceSnatcher.Helpers;

public static class Download
{
	private static readonly Lazy<HttpClient> _lazyClient = new(() =>
	{
		HttpClient client = new();
		client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
		// Consider adding a timeout: client.Timeout = TimeSpan.FromMinutes(5);
		return client;
	});

	private static HttpClient HttpClient => _lazyClient.Value;

	/// <summary>
	/// Downloads a file from a URL. If filename is null, the file is named by its MD5 hash.
	/// </summary>
	/// <returns>The final filename (hash.ext or provided filename.ext) within the folderPath.</returns>
	public static async Task<string> DownloadFileMD5Async(string url, string folderPath, string? filename = null, bool errorIfFileExists = false, int maxRetries = 3)
	{
		Directory.CreateDirectory(folderPath);
		Uri uri = new(url);
		string urlFileName = uri.Segments.LastOrDefault() ?? "unknown_file";
		string descriptiveName = uri.Segments.Length > 1 ? uri.Segments[^2].Trim('/') : urlFileName;

		string finalTargetFilePath = Path.Combine(folderPath, urlFileName);
		string downloadTempPath;

		if (filename == null)
		{
			downloadTempPath = Path.Combine(folderPath, Path.GetRandomFileName()); // Unique temp file
		}
		else
		{
			finalTargetFilePath = Path.Combine(folderPath, $"{filename}{Path.GetExtension(urlFileName)}");
			if (File.Exists(finalTargetFilePath))
			{
				if (errorIfFileExists)
					throw new IOException($"File '{finalTargetFilePath}' already exists and errorIfFileExists is true.");

				Console.WriteLine($"File '{finalTargetFilePath}' already exists, skipping download.");
				return Path.GetFileName(finalTargetFilePath);
			}

			downloadTempPath = finalTargetFilePath; // Download directly to target
		}

		for (int attempt = 0; attempt < maxRetries; attempt++)
		{
			try
			{
				using HttpResponseMessage response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
				response.EnsureSuccessStatusCode();

				long? contentLength = response.Content.Headers.ContentLength;
				Console.WriteLine(contentLength.HasValue
					? $"Downloading {descriptiveName} ({contentLength.Value / 1024 / 1024} MB) (Attempt {attempt + 1})"
					: $"Downloading {descriptiveName} (Attempt {attempt + 1})");

				using Stream downloadStream = await response.Content.ReadAsStreamAsync();
				using FileStream fileStream = File.Create(downloadTempPath);
				await downloadStream.CopyToAsync(fileStream);
				await fileStream.FlushAsync();
				// fileStream is disposed here, releasing the file

				if (filename == null) // MD5 logic
				{
					string hash = GetFileMD5(downloadTempPath);
					finalTargetFilePath = Path.Combine(folderPath, hash + Path.GetExtension(urlFileName));

					if (File.Exists(finalTargetFilePath))
					{
						File.Delete(downloadTempPath); // Clean up temp file
						if (errorIfFileExists)
							throw new IOException($"Hashed file '{finalTargetFilePath}' already exists and errorIfFileExists is true.");

						Console.WriteLine($"Hashed file '{finalTargetFilePath}' (from {urlFileName}) already exists.");
						return Path.GetFileName(finalTargetFilePath);
					}

					File.Move(downloadTempPath, finalTargetFilePath, true);
					return Path.GetFileName(finalTargetFilePath);
				}

				// Filename was provided, downloadTempPath is finalTargetFilePath
				return Path.GetFileName(finalTargetFilePath);
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error downloading '{url}' to '{downloadTempPath}': {e.Message}");
				if (File.Exists(downloadTempPath) && filename == null) // Clean up temp if it's a dedicated temp file
				{
					try
					{
						File.Delete(downloadTempPath);
					}
					catch { /* best effort */ }
				}

				if (attempt == maxRetries - 1)
					throw; // Rethrow on last attempt

				Console.WriteLine($"Retrying in {attempt + 1}s... ({attempt + 1}/{maxRetries})");
				await Task.Delay(TimeSpan.FromSeconds(attempt + 1));
			}
		}
		// Should be unreachable if maxRetries > 0
		throw new Exception($"Failed to download file '{url}' after {maxRetries} retries.");
	}

	public static async Task DownloadFileAsync(string url, string folderPath, string? fileName = null, bool errorIfFileExists = false, int maxRetries = 3)
	{
		Uri uri = new(url);
		fileName ??= uri.Segments.LastOrDefault() ?? "unknown_file";
		string filePath = Path.Combine(folderPath, fileName);

		Directory.CreateDirectory(folderPath);

		if (File.Exists(filePath))
		{
			if (errorIfFileExists)
				throw new IOException($"File '{filePath}' already exists and errorIfFileExists is true.");

			Console.WriteLine($"File '{filePath}' already exists, skipping download.");
			return;
		}

		for (int attempt = 0; attempt < maxRetries; attempt++)
		{
			try
			{
				using HttpResponseMessage response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
				response.EnsureSuccessStatusCode();

				Console.WriteLine($"Downloading to '{filePath}' (Attempt {attempt + 1})");

				using Stream downloadStream = await response.Content.ReadAsStreamAsync();
				using FileStream fileStream = File.Create(filePath);
				await downloadStream.CopyToAsync(fileStream);
				await fileStream.FlushAsync();
				return; // Success
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error downloading file to '{filePath}': {e.Message}");
				if (File.Exists(filePath))
				{
					try
					{
						File.Delete(filePath); } catch { /* best effort */ }
				} // Clean up partial file

				if (attempt == maxRetries - 1)
					throw;

				Console.WriteLine($"Retrying in {attempt + 1}s... ({attempt + 1}/{maxRetries})");
				await Task.Delay(TimeSpan.FromSeconds(attempt + 1));
			}
		}

		throw new Exception($"Failed to download file to '{filePath}' after {maxRetries} retries.");
	}

	public static string GetFileMD5(string filePath)
	{
		using FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
		byte[] hash = MD5.HashData(fileStream);
		return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
	}
}
