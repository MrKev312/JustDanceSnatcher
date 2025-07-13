using JustDanceSnatcher.Discord;
using JustDanceSnatcher.Helpers;
using JustDanceSnatcher.Tools;

Console.Clear();
Console.WriteLine("--- JustDanceSnatcher Utilities ---");
List<string> mainOptions =
[
	"JDNext Download (Discord Bot)",
	"Server Video Upgrader (Discord Bot)",
	"UbiArt Upgrade Video (Discord Bot)",
	"Create ContentAuthorization JSON (UbiArt Tool)",
	"Download from ContentAuthorization JSON (UbiArt Tool)",
	"Fix Custom Audio Volume (FFmpeg Tool)",
	"Convert Playlist (Next to Next+ format)",
	"Steal Covers (Manual Extraction Helper)",
	"Fix jdplus tags"
];

int choice = Question.Ask(mainOptions, 0, "Select an operation:");

switch (choice)
{
	case 0: await new JDNextDownloader().RunAsync(); break;
	case 1: await new ServerVidUpgrader().RunAsync(); break;
	case 2: await new UbiArtVidUpgrader().RunAsync(); break;
	case 3: ContentAuthorizationTool.CreateJson(); break;
	case 4: await ContentAuthorizationTool.DownloadFromContentAuthorizationAsync(); break;
	case 5: AudioFixerTool.FixVolumes(); break;
	case 6: PlaylistConverter.Convert(); break;
	case 7: StealCover.Run(); break;
	case 8: TagFixer.FixTags(); break;
	default: Console.WriteLine("Invalid option selected."); break;
}

Console.WriteLine("\nOperation finished. Press any key to exit.");
Console.ReadKey();
