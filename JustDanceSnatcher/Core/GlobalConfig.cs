using System.Text.Encodings.Web;
using System.Text.Json;

namespace JustDanceSnatcher.Core;

public static class GlobalConfig
{
	public static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};
}