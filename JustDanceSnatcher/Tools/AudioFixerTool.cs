using JustDanceSnatcher.Core;
using JustDanceSnatcher.Helpers;
using JustDanceSnatcher.UbisoftStuff;

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace JustDanceSnatcher.Tools;

internal static class AudioFixerTool
{
	public static void FixVolumes()
	{
		Console.WriteLine("--- Custom Audio Volume Fixer (FFmpeg) ---");
		Console.WriteLine("This tool analyzes custom song audio files and suggests volume adjustments");
		Console.WriteLine("to match target loudness levels. Requires FFmpeg in your PATH.\n");

		string inputMapsFolder = Question.AskFolder("Enter the path to your maps folder (containing custom songs): ", true);
		if (!CheckFFmpegExists())
			return;

		string[] mapFolderPaths = Directory.GetDirectories(inputMapsFolder);
		List<(string audioFilePath, float targetGainDb)> filesToFix = [];

		Console.WriteLine("\nAnalyzing audio files...");
		Parallel.ForEach(mapFolderPaths, mapFolderPath =>
		{
			string mapName = Path.GetFileName(mapFolderPath);
			string songInfoPath = Path.Combine(mapFolderPath, "SongInfo.json");
			if (!File.Exists(songInfoPath))
				return;

			JDNextDatabaseEntry? song = JsonSerializer.Deserialize<JDNextDatabaseEntry>(File.ReadAllText(songInfoPath), GlobalConfig.JsonOptions);
			if (song == null || !song.tags.Contains("Custom", StringComparer.OrdinalIgnoreCase))
				return;

			// Process main audio
			ProcessAudioFileForFixing(Path.Combine(mapFolderPath, "Audio_opus"), mapName, -12.2f, filesToFix);
			// Process audio preview
			ProcessAudioFileForFixing(Path.Combine(mapFolderPath, "AudioPreview_opus"), mapName + " (Preview)", -11.1f, filesToFix);
		});

		filesToFix.Sort((a, b) => string.Compare(a.audioFilePath, b.audioFilePath, StringComparison.OrdinalIgnoreCase));

		if (filesToFix.Count != 0)
		{
			Console.WriteLine($"\nFound {filesToFix.Count} audio files potentially needing volume adjustment:");
			foreach (var (filePath, gain) in filesToFix)
			{
				string relativePath = Path.GetRelativePath(inputMapsFolder, filePath);
				Console.WriteLine($"- '{relativePath}' needs {gain:F1} dB gain.");
			}

			Console.WriteLine("\nNote: Positive gain increases volume, negative gain decreases it.");
			if (Question.Ask(["No", "Yes"], 0, "Proceed with applying these fixes? (0 for No, 1 for Yes)") != 1)
			{
				Console.WriteLine("Audio fixing aborted by user.");
				return;
			}

			Console.WriteLine("\nApplying fixes...");
			foreach (var (filePath, gain) in filesToFix)
			{
				Console.WriteLine($"Fixing '{Path.GetFileName(filePath)}' in '{Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(filePath))!)}' by {gain:F1} dB...");
				ApplyVolumeGain(filePath, gain);
			}

			Console.WriteLine("\nAudio fixing process completed.");
		}
		else
		{
			Console.WriteLine("\nNo audio files found requiring volume adjustment based on current criteria.");
		}
	}

	private static bool CheckFFmpegExists()
	{
		try
		{
			ProcessStartInfo psi = new("ffmpeg", "-version")
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			using Process? process = Process.Start(psi);
			process?.WaitForExit(2000); // Wait up to 2 seconds
			if (process != null && process.ExitCode == 0)
			{
				Console.WriteLine("FFmpeg found.");
				return true;
			}
		}
		catch (System.ComponentModel.Win32Exception) { /* FFmpeg not found or not in PATH */ }
		catch (Exception ex)
		{
			Console.WriteLine($"Error checking for FFmpeg: {ex.Message}");
		}

		Console.WriteLine("Error: FFmpeg is not found in your system's PATH. This tool cannot continue.");
		Console.WriteLine("Please install FFmpeg and ensure it's accessible from the command line.");
		return false;
	}

	private static void ProcessAudioFileForFixing(string audioFolder, string logName, float targetIntegratedLoudness, List<(string, float)> filesToFix)
	{
		if (!Directory.Exists(audioFolder))
			return;
		string? audioFile = Directory.GetFiles(audioFolder).FirstOrDefault(); // Assumes one audio file per folder
		if (audioFile == null)
			return;

		float? currentLoudness = GetIntegratedLoudness(audioFile);
		if (currentLoudness == null)
		{
			Console.WriteLine($"Could not determine loudness for '{logName}'. Skipping.");
			return;
		}

		// Only consider fixing if current loudness is below a threshold (e.g., -16 LUFS)
		if (currentLoudness.Value < -16.0f)
		{
			float gainNeeded = targetIntegratedLoudness - currentLoudness.Value;
			gainNeeded = (float)Math.Round(gainNeeded, 1); // Round to one decimal place

			if (Math.Abs(gainNeeded) > 0.05) // Only add if a meaningful gain is actually needed
			{
				// Console.WriteLine($"'{logName}' current loudness: {currentLoudness.Value:F1} LUFS. Target: {targetIntegratedLoudness} LUFS. Calculated gain: {gainNeeded:F1} dB.");
				lock (filesToFix)
				{
					filesToFix.Add((audioFile, gainNeeded));
				}
			}
		}
	}

	private static float? GetIntegratedLoudness(string audioFilePath)
	{
		string arguments = $"-nostats -i \"{audioFilePath}\" -af ebur128=framelog=verbose -f null -";
		ProcessStartInfo startInfo = new("ffmpeg", arguments)
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};

		try
		{
			using Process process = Process.Start(startInfo)!;
			string ffmpegOutput = process.StandardError.ReadToEnd(); // EBU R128 summary is on stderr
			process.WaitForExit();

			string[] lines = ffmpegOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
			string? loudnessLine = lines.LastOrDefault(line => line.Contains("Integrated loudness:") && line.Contains("LUFS"));

			if (loudnessLine != null)
			{
				int iMarker = loudnessLine.IndexOf("I:") + 2;
				int lufsMarker = loudnessLine.IndexOf("LUFS", iMarker);
				if (iMarker > 1 && lufsMarker > iMarker)
				{
					string loudnessValueStr = loudnessLine[iMarker..lufsMarker].Trim();
					if (float.TryParse(loudnessValueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float loudnessValue))
					{
						return loudnessValue;
					}
				}
			}

			Console.WriteLine($"Could not parse integrated loudness from FFmpeg output for '{Path.GetFileName(audioFilePath)}'. Review FFmpeg output if issues persist.");
			return null;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error running FFmpeg for loudness check on '{Path.GetFileName(audioFilePath)}': {ex.Message}");
			return null;
		}
	}

	private static void ApplyVolumeGain(string audioFilePath, float gainDb)
	{
		string tempOutputFile = $"{audioFilePath}.tmp.opus";
		// Ensure gainDb is formatted with a period for decimal separator for FFmpeg
		string gainArg = gainDb.ToString("F1", CultureInfo.InvariantCulture);
		string arguments = $"-i \"{audioFilePath}\" -af \"volume={gainArg}dB\" -c:a libopus -y \"{tempOutputFile}\"";
		ProcessStartInfo startInfo = new("ffmpeg", arguments)
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};

		try
		{
			using Process process = Process.Start(startInfo)!;
			process.WaitForExit();
			if (process.ExitCode != 0)
			{
				string errorOutput = process.StandardError.ReadToEnd();
				throw new Exception($"FFmpeg process exited with code {process.ExitCode} for {Path.GetFileName(audioFilePath)}. Error: {errorOutput}");
			}

			File.Delete(audioFilePath);
			File.Move(tempOutputFile, audioFilePath);
			Console.WriteLine($"Volume successfully adjusted for '{Path.GetFileName(audioFilePath)}'.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error increasing volume for '{Path.GetFileName(audioFilePath)}': {ex.Message}");
			if (File.Exists(tempOutputFile))
			{
				try
				{
					File.Delete(tempOutputFile); } catch { /* Best effort */ }
			}
		}
	}
}