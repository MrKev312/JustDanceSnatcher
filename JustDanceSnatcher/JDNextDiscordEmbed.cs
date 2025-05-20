namespace JustDanceSnatcher;

internal class JDNextDiscordEmbed
{
	public ImageURLs ImageURLs { get; set; } = new();
	public PreviewURLs PreviewURLs { get; set; } = new();
	public ContentURLs ContentURLs { get; set; } = new();
}

internal class ImageURLs
{
	public string? coachesSmall { get; set; }
	public string? coachesLarge { get; set; }
	public string? Cover { get; set; }
	public string? Cover1024 { get; set; }
	public string? CoverSmall { get; set; }
	public string? SongTitleLogo { get; set; }
}

internal class PreviewURLs
{
	public string? audioPreview { get; set; }
	public string? HIGHvp8 { get; set; }
	public string? HIGHvp9 { get; set; }
	public string? LOWvp8 { get; set; }
	public string? LOWvp9 { get; set; }
	public string? MIDvp8 { get; set; }
	public string? MIDvp9 { get; set; }
	public string? ULTRAvp8 { get; set; }
	public string? ULTRAvp9 { get; set; }
}

internal class ContentURLs
{
	public string? UltraHD { get; set; }
	public string? Ultravp9 { get; set; }
	public string? HighHD { get; set; }
	public string? Highvp9 { get; set; }
	public string? MidHD { get; set; }
	public string? Midvp9 { get; set; }
	public string? LowHD { get; set; }
	public string? Lowvp9 { get; set; }
	public string? Audio { get; set; }
	public string? mapPackage { get; set; }
}