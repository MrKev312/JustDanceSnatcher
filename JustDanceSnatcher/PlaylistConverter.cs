using JustDanceSnatcher.Helpers;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JustDanceSnatcher;

internal static class PlaylistConverter
{
	// This 'writer' uses CamelCase, different from Program.jsonOptions. So it's kept separate.
	private static readonly JsonSerializerOptions writerOptions = new()
	{
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public static void Convert()
	{
		string jsonPath = Question.AskFile("Select the playlist JSON file to convert: ", true);
		string mapsFolder = Question.AskFolder("Select the maps folder (containing SongInfo.json files): ", true);

		string json = File.ReadAllText(jsonPath);
		PlaylistJson? playlistJsonSource = JsonSerializer.Deserialize<PlaylistJson>(json, Program.jsonOptions);

		if (playlistJsonSource == null)
		{
			Console.WriteLine("Failed to deserialize the source playlist JSON.");
			return;
		}

		Dictionary<Guid, string> songIdToMapNameMap = LoadSongIdToMapNameMapping(mapsFolder);

		string localizedStringsPath = Path.Combine(mapsFolder, "..", "..", "database", "config", "localizedStrings.json");
		LocalizedStrings? localizedStrings = null;
		if (File.Exists(localizedStringsPath))
		{
			string localizedStringsContent = File.ReadAllText(localizedStringsPath);
			localizedStrings = JsonSerializer.Deserialize<LocalizedStrings>(localizedStringsContent, Program.jsonOptions);
		}
		else
		{
			Console.WriteLine($"Warning: Localized strings file not found at '{localizedStringsPath}'. Titles and descriptions might be Oasis IDs.");
		}

		string outputPlaylistFolder = Path.Combine(mapsFolder, "..", "..", "database", "playlists");
		Directory.CreateDirectory(outputPlaylistFolder);

		foreach (KeyValuePair<Guid, Playlist> playlistEntry in playlistJsonSource.playlists)
		{
			Guid playlistGuid = playlistEntry.Key;
			Playlist sourcePlaylist = playlistEntry.Value;

			if (playlistJsonSource.dynamicPlaylists.Contains(playlistGuid))
			{
				Console.WriteLine($"Playlist '{sourcePlaylist.playlistName}' ({playlistGuid}) is dynamic. Skipping.");
				continue;
			}

			Console.WriteLine($"Processing playlist: {sourcePlaylist.playlistName}");

			JsonPlaylist newPlaylist = new()
			{
				Guid = playlistGuid,
				PlaylistName = sourcePlaylist.playlistName,
				CoverUrl = sourcePlaylist.assets?.en?.cover ?? string.Empty,
				CoverDetailsUrl = sourcePlaylist.assets?.en?.coverDetails ?? string.Empty,
				Tags = sourcePlaylist.tags?.Length > 0 ? sourcePlaylist.tags : null
			};

			if (localizedStrings != null)
			{
				newPlaylist.LocalizedTitle = localizedStrings.localizedStrings
					.FirstOrDefault(x => x.oasisId == sourcePlaylist.localizedTitle.ToString())?.displayString ?? sourcePlaylist.localizedTitle.ToString();
				newPlaylist.LocalizedDescription = localizedStrings.localizedStrings
					.FirstOrDefault(x => x.oasisId == sourcePlaylist.localizedDescription.ToString())?.displayString ?? sourcePlaylist.localizedDescription.ToString();
			}
			else
			{
				newPlaylist.LocalizedTitle = sourcePlaylist.localizedTitle.ToString(); // Fallback to ID
				newPlaylist.LocalizedDescription = sourcePlaylist.localizedDescription.ToString(); // Fallback to ID
			}


			foreach (Itemlist item in sourcePlaylist.itemList)
			{
				if (item.type == "map" && Guid.TryParse(item.id, out Guid mapGuid))
				{
					if (songIdToMapNameMap.TryGetValue(mapGuid, out string? mapName))
					{
						newPlaylist.ItemList.Add(mapName);
					}
					else
					{
						Console.WriteLine($"Warning: Map with ID '{mapGuid}' not found in local maps folder for playlist '{sourcePlaylist.playlistName}'.");
					}
				}
				else
				{
					Console.WriteLine($"Skipping non-map item or item with invalid ID: Type='{item.type}', ID='{item.id}' in playlist '{sourcePlaylist.playlistName}'.");
				}
			}

			string outputJsonPath = Path.Combine(outputPlaylistFolder, $"{SanitizeFileName(newPlaylist.PlaylistName)}.json");
			string finalJson = JsonSerializer.Serialize(newPlaylist, writerOptions);
			File.WriteAllText(outputJsonPath, finalJson);
			Console.WriteLine($"Converted playlist '{newPlaylist.PlaylistName}' saved to '{outputJsonPath}'.");
		}
	}

	private static Dictionary<Guid, string> LoadSongIdToMapNameMapping(string mapsFolder)
	{
		Dictionary<Guid, string> map = [];
		string[] individualMapFolders = Directory.GetDirectories(mapsFolder);

		foreach (string mapFolder in individualMapFolders)
		{
			string songInfoPath = Path.Combine(mapFolder, "SongInfo.json");
			if (!File.Exists(songInfoPath))
			{
				Console.WriteLine($"Warning: SongInfo.json not found in '{mapFolder}'. Skipping for playlist conversion mapping.");
				continue;
			}

			try
			{
				string songInfoContent = File.ReadAllText(songInfoPath);
				JsonObject? info = JsonSerializer.Deserialize<JsonObject>(songInfoContent);

				if (info != null && info.TryGetPropertyValue("songID", out JsonNode? songIdNode) &&
					info.TryGetPropertyValue("mapName", out JsonNode? mapNameNode))
				{
					if (Guid.TryParse(songIdNode!.GetValue<string>(), out Guid songGuid))
					{
						map[songGuid] = mapNameNode!.GetValue<string>();
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error reading SongInfo from '{songInfoPath}': {ex.Message}");
			}
		}

		return map;
	}
	private static string SanitizeFileName(string name)
	{
		return string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
	}
}

public class JsonPlaylist
{
	public Guid Guid { get; set; } = Guid.Empty;
	public string PlaylistName { get; set; } = "";
	public string LocalizedTitle { get; set; } = "";
	public string LocalizedDescription { get; set; } = "";
	public string CoverUrl { get; set; } = "";
	public string CoverDetailsUrl { get; set; } = "";
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string[]? Tags { get; set; }
	public List<string> ItemList { get; set; } = [];
}

public class LocalizedStrings
{
	public string spaceId { get; set; } = "";
	public Localizedstring[] localizedStrings { get; set; } = [];
}

public class Localizedstring
{
	public string oasisId { get; set; } = "";
	public string localeCode { get; set; } = "";
	public string displayString { get; set; } = "";
	public string localizedStringId { get; set; } = "";
	public object? obj { get; set; } // Can be null
	public int spaceRevision { get; set; }
}

public class PlaylistJson
{
	public Dictionary<Guid, Playlist> playlists { get; set; } = [];
	public Guid[] dynamicPlaylists { get; set; } = [];
}

public class Playlist
{
	public string playlistName { get; set; } = "";
	public Itemlist[] itemList { get; set; } = [];
	public string listSource { get; set; } = "";
	public int localizedTitle { get; set; }
	public int localizedDescription { get; set; }
	public string defaultLanguage { get; set; } = "";
	public Assets? assets { get; set; } // Can be null
	public string[] tags { get; set; } = [];
	public object[] offersTags { get; set; } = [];
	public bool hidden { get; set; }
}

public class Assets
{
	public En? en { get; set; } // Can be null
}

public class En
{
	public string cover { get; set; } = "";
	public string coverDetails { get; set; } = "";
}

public class Itemlist
{
	public string id { get; set; } = "";
	public string type { get; set; } = "";
}