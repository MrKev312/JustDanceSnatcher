using JustDanceSnatcher.Core;
using JustDanceSnatcher.Helpers;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JustDanceSnatcher.Tools;

internal static class PlaylistConverter
{
	private static readonly JsonSerializerOptions writerOptions = new()
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public static void Convert()
	{
		string jsonPath = Question.AskFile("Select the playlist JSON file to convert: ", true);
		string mapsFolder = Question.AskFolder("Select the maps folder (containing SongInfo.json files): ", true);

		string json = File.ReadAllText(jsonPath);
		PlaylistJson? playlistJsonSource = JsonSerializer.Deserialize<PlaylistJson>(json, GlobalConfig.JsonOptions);

		if (playlistJsonSource == null)
		{
			Console.WriteLine("Failed to deserialize source playlist JSON.");
			return;
		}

		Dictionary<Guid, string> songIdToMapNameMap = LoadSongIdToMapNameMapping(mapsFolder);

		string localizedStringsPath = Path.Combine(mapsFolder, "..", "..", "database", "config", "localizedStrings.json");
		LocalizedStrings? localizedStrings = null;
		if (File.Exists(localizedStringsPath))
		{
			localizedStrings = JsonSerializer.Deserialize<LocalizedStrings>(File.ReadAllText(localizedStringsPath), GlobalConfig.JsonOptions);
		}
		else
		{
			Console.WriteLine($"Warning: Localized strings file not found at '{localizedStringsPath}'.");
		}

		string outputPlaylistFolder = Path.Combine(mapsFolder, "..", "..", "database", "playlists");
		Directory.CreateDirectory(outputPlaylistFolder);

		foreach (KeyValuePair<Guid, Playlist> playlistEntry in playlistJsonSource.playlists)
		{
			if (playlistJsonSource.dynamicPlaylists.Contains(playlistEntry.Key))
			{
				Console.WriteLine($"Playlist '{playlistEntry.Value.playlistName}' is dynamic. Skipping.");
				continue;
			}

			Console.WriteLine($"Processing playlist: {playlistEntry.Value.playlistName}");

			JsonPlaylist newPlaylist = new()
			{
				Guid = playlistEntry.Key,
				PlaylistName = playlistEntry.Value.playlistName,
				CoverUrl = playlistEntry.Value.assets?.en?.cover ?? string.Empty,
				CoverDetailsUrl = playlistEntry.Value.assets?.en?.coverDetails ?? string.Empty,
				Tags = playlistEntry.Value.tags?.Length > 0 ? playlistEntry.Value.tags : null,
				LocalizedTitle = localizedStrings?.localizedStrings
					.FirstOrDefault(x => x.oasisId == playlistEntry.Value.localizedTitle.ToString())?.displayString ?? playlistEntry.Value.localizedTitle.ToString(),
				LocalizedDescription = localizedStrings?.localizedStrings
					.FirstOrDefault(x => x.oasisId == playlistEntry.Value.localizedDescription.ToString())?.displayString ?? playlistEntry.Value.localizedDescription.ToString()
			};

			foreach (Itemlist item in playlistEntry.Value.itemList)
			{
				if (item.type == "map" && Guid.TryParse(item.id, out Guid mapGuid))
				{
					if (songIdToMapNameMap.TryGetValue(mapGuid, out string? mapName))
						newPlaylist.ItemList.Add(mapName);
					else
						Console.WriteLine($"Warning: Map ID '{mapGuid}' not found for playlist '{newPlaylist.PlaylistName}'.");
				}
				else
					Console.WriteLine($"Skipping non-map item: Type='{item.type}', ID='{item.id}' in playlist '{newPlaylist.PlaylistName}'.");
			}

			string outputJsonPath = Path.Combine(outputPlaylistFolder, $"{SanitizeFileName(newPlaylist.PlaylistName)}.json");
			File.WriteAllText(outputJsonPath, JsonSerializer.Serialize(newPlaylist, writerOptions));
			Console.WriteLine($"Converted playlist '{newPlaylist.PlaylistName}' saved to '{outputJsonPath}'.");
		}
	}

	private static Dictionary<Guid, string> LoadSongIdToMapNameMapping(string mapsFolder)
	{
		Dictionary<Guid, string> map = [];
		foreach (string mapFolder in Directory.GetDirectories(mapsFolder))
		{
			string songInfoPath = Path.Combine(mapFolder, "SongInfo.json");
			if (!File.Exists(songInfoPath))
				continue;
			try
			{
				JsonObject? info = JsonSerializer.Deserialize<JsonObject>(File.ReadAllText(songInfoPath));
				if (info != null && info.TryGetPropertyValue("songID", out JsonNode? songIdNode) &&
					info.TryGetPropertyValue("mapName", out JsonNode? mapNameNode) &&
					Guid.TryParse(songIdNode!.GetValue<string>(), out Guid songGuid))
				{
					map[songGuid] = mapNameNode!.GetValue<string>();
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error reading SongInfo from '{songInfoPath}': {ex.Message}");
			}
		}

		return map;
	}
	private static string SanitizeFileName(string name) => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
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
	public object? obj { get; set; }
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
	public Assets? assets { get; set; }
	public string[] tags { get; set; } = [];
	public object[] offersTags { get; set; } = [];
	public bool hidden { get; set; }
}

public class Assets
{
	public En? en { get; set; }
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