namespace JustDanceSnatcher.UbisoftStuff;

public class UbiArtSong
{
	public string artist { get; set; }
	public AssetsUbiArt assets { get; set; }
	public string audioPreviewData { get; set; }
	public int coachCount { get; set; }
	public string credits { get; set; }
	public int difficulty { get; set; }
	public object[] jdmAttributes { get; set; }
	public string lyricsColor { get; set; }
	public string customTypeName { get; set; }
	public int lyricsType { get; set; }
	public int mainCoach { get; set; }
	public float mapLength { get; set; }
	public string mapName { get; set; }
	public string mapPreviewMpd { get; set; }
	public int mode { get; set; }
	public int originalJDVersion { get; set; }
	public Packages packages { get; set; }
	public string parentMapName { get; set; }
	public int serverChangelist { get; set; }
	public string[] skuIds { get; set; }
	public Songcolors songColors { get; set; }
	public int status { get; set; }
	public int sweatDifficulty { get; set; }
	public string[] tags { get; set; }
	public string title { get; set; }
	public Urls urls { get; set; }
}

public class AssetsUbiArt
{
	public string banner_bkgImageUrl { get; set; }
	public string coach1ImageUrl { get; set; }
	public string? coach2ImageUrl { get; set; }
	public string? coach3ImageUrl { get; set; }
	public string? coach4ImageUrl { get; set; }
	public string cover_smallImageUrl { get; set; }
	public string expandCoachImageUrl { get; set; }
	public string map_bkgImageUrl { get; set; }
}

public class Packages
{
	public string mapContent { get; set; }
}

public class Songcolors
{
	public string songColor_1A { get; set; }
	public string songColor_1B { get; set; }
	public string songColor_2A { get; set; }
	public string songColor_2B { get; set; }
}

public class Urls
{
	public string jmcsjdcontents212212_AudioPreviewogg { get; set; }
	public string jmcsjdcontents212212_MapPreviewNoSoundCrop_LOWvp8webm { get; set; }
}
