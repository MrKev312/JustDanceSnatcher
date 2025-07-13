using System.Text.Json.Serialization;

namespace JustDanceSnatcher.UbisoftStuff;

public class JDNextDatabaseEntry
{
	public string songID { get; set; } = "";

	public string artist { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public Assetsmetadata? assetsMetadata { get; set; }
	public object coachCount { get; set; }
	public object[] coachNamesLocIds { get; set; }
	public object credits { get; set; }
	public object danceVersionLocId { get; set; }
	public object difficulty { get; set; }
	public object doubleScoringType { get; set; }
	public bool hasCameraScoring { get; set; }
	public bool hasSongTitleInCover { get; set; }
	public object lyricsColor { get; set; }
	public object mapLength { get; set; }
	public object mapName { get; set; }
	public object originalJDVersion { get; set; }
	public string parentMapName { get; set; }
	public object sweatDifficulty { get; set; }
	public object[] tagIds { get; set; }
	public List<string> tags { get; set; }
	public object title { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public Assets? assets { get; set; }

	public override string ToString()
	{
		return mapName?.ToString() ?? "Unknown Map";
	}
}

public class Assetsmetadata
{
	public string audioPreviewTrk { get; set; }
	public string videoPreviewMpd { get; set; }
}

public class Assets
{
	[JsonPropertyName("audioPreview.opus")]
	public string audioPreviewopus { get; set; }
	[JsonPropertyName("videoPreview_HIGH.vp8.webm")]
	public string videoPreview_HIGHvp8webm { get; set; }
	[JsonPropertyName("videoPreview_HIGH.vp9.webm")]
	public string videoPreview_HIGHvp9webm { get; set; }
	[JsonPropertyName("videoPreview_LOW.vp8.webm")]
	public string videoPreview_LOWvp8webm { get; set; }
	[JsonPropertyName("videoPreview_LOW.vp9.webm")]
	public string videoPreview_LOWvp9webm { get; set; }
	[JsonPropertyName("videoPreview_MID.vp8.webm")]
	public string videoPreview_MIDvp8webm { get; set; }
	[JsonPropertyName("videoPreview_MID.vp9.webm")]
	public string videoPreview_MIDvp9webm { get; set; }
	[JsonPropertyName("videoPreview_ULTRA.vp8.webm")]
	public string videoPreview_ULTRAvp8webm { get; set; }
	[JsonPropertyName("videoPreview_ULTRA.vp9.webm")]
	public string videoPreview_ULTRAvp9webm { get; set; }
	public string coachesLarge { get; set; }
	public string coachesSmall { get; set; }
	public string cover { get; set; }
	public string cover1024 { get; set; }
	public string coverSmall { get; set; }
	// Ignore when null
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? songTitleLogo { get; set; } = null;
}
