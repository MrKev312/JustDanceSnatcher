using System.Security.Cryptography;
using System.Text;

// If there's no argument, throw an error
if (args.Length == 0)
{
	Console.WriteLine("Please provide the path to the maps folder as an argument.");
	return;
}

Parallel.ForEach(args, path =>
{
	try
	{
		string trimmedPath = path.Trim('"'); // Trim quotes if present
		MakeUnique(trimmedPath);
	}
	catch (Exception ex)
	{
		Console.WriteLine($"An error occurred while processing '{path}': {ex.Message}");
	}
});

void MakeUnique(string path)
{
	// Check if the path exists
	if (!Directory.Exists(path))
	{
		Console.WriteLine($"The path '{path}' does not exist.");
		return;
	}

	// Check it has a .bundle extension
	if (!Path.GetExtension(path).Equals(".bundle", StringComparison.CurrentCultureIgnoreCase))
	{
		Console.WriteLine($"The file '{path}' is not a .bundle file.");
		return;
	}

	UpdateCabMarkerWithHash(path);

	// Rename the file to the new md5 hash
	string md5Hash = GetMD5Hash(path);
	string newFileName = $"{md5Hash}.bundle";
	string newPath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, newFileName);

	if (File.Exists(newPath))
	{
		Console.WriteLine($"The file '{newPath}' already exists. Skipping renaming.");
		return;
	}

	File.Move(path, newPath);
}

/// <summary>
/// Updates the CAB marker in the file with the MD5 hash of the file.
/// The marker is expected to be at the end of the file, starting with "CAB-".
/// The MD5 hash will be written immediately after the marker.
/// Throws an exception if the marker is not found.
/// </summary>
/// <param name="path">The path to the .bundle file.</param>
/// <exception cref="InvalidOperationException">Thrown if the marker "CAB-" is not found in the file.</exception>
void UpdateCabMarkerWithHash(string path)
{
	// Get the md5 hash of the file
	string md5Hash = GetMD5Hash(path);

	BinaryReader binaryReader = new(File.OpenRead(path));
	const string marker = "CAB-";
	byte[] markerBytes = Encoding.UTF8.GetBytes(marker);
	bool foundMarker = false;
	long startPosition = binaryReader.BaseStream.Length - 0x40; // Start from -0x40 from the end
	long position = startPosition < 0 ? 0 : startPosition; // Ensure the start position is not out of bounds

	binaryReader.BaseStream.Seek(position, SeekOrigin.Begin); // Move to the start position

	// Read through the file from the start position to find the marker
	while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
	{
		byte[] buffer = binaryReader.ReadBytes(markerBytes.Length);
		if (buffer.SequenceEqual(markerBytes))
		{
			foundMarker = true;
			position = binaryReader.BaseStream.Position - markerBytes.Length;
			break;
		}

		// Move back the stream's position to ensure overlapping sequences are checked
		binaryReader.BaseStream.Seek(-markerBytes.Length + 1, SeekOrigin.Current);
	}

	if (!foundMarker)
	{
		throw new InvalidOperationException("Marker 'CAB-' not found in the file.");
	}

	binaryReader.Close();

	// Now that we've found the marker, we can update the offset
	long offset = position + 4;
	BinaryWriter binaryWriter = new(File.OpenWrite(path));
	binaryWriter.BaseStream.Seek(offset, SeekOrigin.Begin);
	binaryWriter.Write(Encoding.UTF8.GetBytes(md5Hash));
	binaryWriter.Close();
}

/// <summary>
/// Computes the MD5 hash of the specified file and returns it as a lowercase hexadecimal string.
/// </summary>
/// <param name="filePath">The path to the file to hash.</param>
/// <returns>The MD5 hash of the file as a lowercase hexadecimal string.</returns>
string GetMD5Hash(string filePath)
{
	using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
	byte[] hashBytes = MD5.HashData(stream);
	return Convert.ToHexStringLower(hashBytes);
}