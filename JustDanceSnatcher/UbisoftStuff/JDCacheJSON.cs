using System.Text.Json.Serialization;

namespace JustDanceSnatcher.UbisoftStuff;

public class JDCacheJSON
{
	[JsonPropertyName("schemaVersion")]
	public uint SchemaVersion { get; set; } = 1;

	[JsonPropertyName("mapsDict")]
	// String is the map ID
	public Dictionary<string, JDSong> MapsDict { get; set; } = [];
}

public class JDSong
{
	[JsonPropertyName("songDatabaseEntry")]
	public SongDatabaseEntry SongDatabaseEntry { get; set; } = new();

	[JsonPropertyName("audioPreviewTrk")]
	public string AudioPreviewTrk { get; set; } = "";

	[JsonPropertyName("assetFilesDict")]
	public AssetFilesDict AssetFilesDict { get; set; } = new();

	[JsonPropertyName("sizes")]
	public Sizes Sizes { get; set; } = new();
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	[JsonPropertyName("hasSongTitleInCover")]
	public bool? HasSongTitleInCover { get; set; } = null;
}

public class SongDatabaseEntry
{
	// Must be a version 4 UUID
	public string MapId { get; set; } = "";
	public string ParentMapId { get; set; } = "";
	public string Title { get; set; } = "";
	public string Artist { get; set; } = "";
	public string Credits { get; set; } = "";
	// Format: "#RRGGBB"
	public string LyricsColor { get; set; } = "";
	// Song length in seconds
	public double MapLength { get; set; }
	public uint OriginalJDVersion { get; set; }
	// Must be between 1 and 4
	public uint CoachCount { get; set; }
	// Must be between 1 and 5
	public uint Difficulty { get; set; }
	public uint SweatDifficulty { get; set; }
	public List<string> Tags { get; set; } = [];
	public List<string> TagIds { get; set; } = [];
	// Seems to always be empty
	public List<uint> SearchTagsLocIds { get; set; } = [];
	// Can be empty, causes all names to be blank
	public List<uint> CoachNamesLocIds { get; set; } = [];
}

public class AssetFilesDict
{
	public Asset Cover { get; set; } = new();
	public Asset CoachesSmall { get; set; } = new();
	public Asset CoachesLarge { get; set; } = new();
	public Asset AudioPreview_opus { get; set; } = new();
	public Asset VideoPreview_MID_vp9_webm { get; set; } = new();
	[JsonPropertyName("songTitleLogo")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public Asset? SongTitleLogo { get; set; } = null;
	public Asset Audio_opus { get; set; } = new();
	public Asset Video_HIGH_vp9_webm { get; set; } = new();
	public Asset MapPackage { get; set; } = new();
}

public enum AssetType
{
	Cover = 1,
	CoachesSmall = 12,
	CoachesLarge = 13,
	AudioPreview_opus = 14,
	VideoPreview_MID_vp9_webm = 18,
	Audio_opus = 23,
	Video_HIGH_vp9_webm = 31,
	MapPackage = 36,
	SongTitleLogo = 37
}

public class Asset
{
	[JsonPropertyName("assetType")]
	public AssetType AssetType { get; set; }
	[JsonPropertyName("name")]
	public string Name { get; set; } = "";
	[JsonPropertyName("hash")]
	public string Hash { get; set; } = "";
	[JsonPropertyName("ready")]
	public bool Ready { get; set; }
	[JsonPropertyName("size")]
	public uint Size { get; set; }
	[JsonPropertyName("category")]
	public uint Category { get; set; }
	[JsonPropertyName("filePath")]
	// If this is set, change the hash to the file's MD5 hash
	public string FilePath { get; set; } = "";
}

// Everything in this class seems to always be 0
public class Sizes
{
	[JsonPropertyName("totalSize")]
	public uint TotalSize { get; set; } = 0;
	[JsonPropertyName("commitSize")]
	public uint CommitSize { get; set; } = 0;
	[JsonPropertyName("baseAssetsSize")]
	public uint BaseAssetsSize { get; set; } = 0;
	[JsonPropertyName("runtimeAssetsSize")]
	public uint RuntimeAssetsSize { get; set; } = 0;
	[JsonPropertyName("runtimeCacheSize")]
	public uint RuntimeCacheSize { get; set; } = 0;
}
