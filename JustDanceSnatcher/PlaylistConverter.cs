using JustDanceSnatcher.Helpers;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JustDanceSnatcher;
internal static class PlaylistConverter
{
	static readonly JsonSerializerOptions options = new()
	{
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	static readonly JsonSerializerOptions writer = new()
	{
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		// Write with lowercase property names
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public static void Convert()
	{
		// Ask for the json file
		string jsonPath = Question.AskFile("Select the json file", true);

		// Read the json file
		string json = File.ReadAllText(jsonPath);
		// Deserialize the json file
		PlaylistJson playlistJson = JsonSerializer.Deserialize<PlaylistJson>(json, options)!;

		// Ask for the maps folder
		string mapsFolder = Question.AskFolder("Select the maps folder", true);

		// Loop through each map and add it to a dictionary
		Dictionary<Guid, string> maps = [];

		string[] folders = Directory.GetDirectories(mapsFolder);
		foreach (string file in folders)
		{
			string songInfo = Path.Combine(file, "SongInfo.json");

			if (!File.Exists(songInfo))
			{
				Console.WriteLine($"SongInfo.json doesn't exist in the map folder");
				continue;
			}

			string songInfoContent = File.ReadAllText(songInfo);

			// Open as jsonobject
			JsonObject info = JsonSerializer.Deserialize<JsonObject>(songInfoContent)!;

			// Get the songId string
			string songId = info["songID"]!.ToString()!;
			Guid guid = Guid.Parse(songId);

			// Get the song name
			string songName = info["mapName"]!.ToString()!;

			maps.Add(guid, songName);
		}

		// Load the maps/../../database/config/localizedStrings.json file
		string localizedStringsPath = Path.Combine(mapsFolder, "..", "..", "database", "config", "localizedStrings.json");
		string localizedStringsContent = File.ReadAllText(localizedStringsPath);
		LocalizedStrings localizedStrings = JsonSerializer.Deserialize<LocalizedStrings>(localizedStringsContent, options)!;

		// Create the folder maps/../../database/playlists/
		string playlistFolder = Path.Combine(mapsFolder, "..", "..", "database", "playlists");
		Directory.CreateDirectory(playlistFolder);

		// Loop through each playlist
		foreach (KeyValuePair<Guid, Playlist> playlist in playlistJson.playlists)
		{
			// If it already exists, skip it
			string playlistName = playlist.Value.playlistName;
			string playlistPath = Path.Combine(playlistFolder, $"{playlistName}.json");
			//if (File.Exists(playlistPath))
			//{
			//	Console.WriteLine($"Playlist {playlistName} already exists, skipping");
			//	continue;
			//}

			// If the playlist is dynamic, skip it
			if (playlistJson.dynamicPlaylists.Contains(playlist.Key))
			{
				Console.WriteLine($"Playlist {playlist.Value.playlistName} is dynamic, skipping");
				continue;
			}

			JsonPlaylist jsonPlaylist = new()
			{
				Guid = playlist.Key,
				PlaylistName = playlist.Value.playlistName,
				LocalizedTitle = localizedStrings.localizedStrings.FirstOrDefault(x => x.oasisId == playlist.Value.localizedTitle.ToString())?.displayString ?? "",
				LocalizedDescription = localizedStrings.localizedStrings.FirstOrDefault(x => x.oasisId == playlist.Value.localizedDescription.ToString())?.displayString ?? "",
				CoverUrl = playlist.Value.assets.en.cover,
				CoverDetailsUrl = playlist.Value.assets.en.coverDetails,
				Tags = playlist.Value.tags.Length != 0 ? playlist.Value.tags : null
			};

			// Loop through each item in the playlist
			foreach (Itemlist item in playlist.Value.itemList)
			{
				// If the item is a map, add it to the list
				if (item.type == "map")
				{
					// Get the guid from the id
					Guid guid = Guid.Parse(item.id);
					// If the guid is in the maps dictionary, add it to the playlist
					if (maps.TryGetValue(guid, out string? value))
					{
						jsonPlaylist.ItemList.Add(value);
					}
					else
					{
						Console.WriteLine($"Map {guid} not found in the maps folder");
					}
				}
				else
				{
					Console.WriteLine("Item is not a map, skipping");
					Console.WriteLine($"Item type: {item.type}");
				}
			}

			string finalJson = JsonSerializer.Serialize(jsonPlaylist, writer);

			File.WriteAllText(playlistPath, finalJson);
		}
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

	// The maps
	public List<string> ItemList { get; set; } = [];
}

public class LocalizedStrings
{
	public string spaceId { get; set; }
	public Localizedstring[] localizedStrings { get; set; }
}

public class Localizedstring
{
	public string oasisId { get; set; }
	public string localeCode { get; set; }
	public string displayString { get; set; }
	public string localizedStringId { get; set; }
	public object obj { get; set; }
	public int spaceRevision { get; set; }
}

public class PlaylistJson
{
	public Dictionary<Guid, Playlist> playlists { get; set; }
	public Guid[] dynamicPlaylists { get; set; }
}

public class Playlist
{
	public string playlistName { get; set; }
	public Itemlist[] itemList { get; set; }
	public string listSource { get; set; }
	public int localizedTitle { get; set; }
	public int localizedDescription { get; set; }
	public string defaultLanguage { get; set; }
	public Assets assets { get; set; }
	public string[] tags { get; set; }
	public object[] offersTags { get; set; }
	public bool hidden { get; set; }
}

public class Assets
{
	public En en { get; set; }
}

public class En
{
	public string cover { get; set; }
	public string coverDetails { get; set; }
}

public class Itemlist
{
	public string id { get; set; }
	public string type { get; set; }
}
