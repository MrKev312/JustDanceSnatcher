using DSharpPlus.Entities;

using JustDanceSnatcher.Utils;

using System.Reflection;

namespace JustDanceSnatcher.Discord.Helpers;

internal static class EmbedParserHelper
{
	public static void ParseFields<T>(T targetObject, IEnumerable<DiscordEmbedField> fields, IReadOnlyDictionary<string, string> fieldToPropertyMap) where T : class
	{
		ArgumentNullException.ThrowIfNull(targetObject);
		if (fields == null)
			return;

		Type targetType = typeof(T);

		foreach (var field in fields)
		{
			if (fieldToPropertyMap.TryGetValue(field.Name, out var propertyName))
			{
				PropertyInfo? prop = targetType.GetProperty(propertyName);
				if (prop != null && prop.CanWrite)
				{
					string? cleanedValue = StringUtils.CleanDiscordEmbedURL(field.Value);
					try
					{
						if (prop.PropertyType == typeof(string))
						{
							prop.SetValue(targetObject, cleanedValue);
						}
						else
						{
							Console.WriteLine($"Warning: Property {propertyName} is not a string. Auto-parsing not implemented for type {prop.PropertyType}. Value: {cleanedValue}");
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Error setting property '{propertyName}' from field '{field.Name}': {ex.Message}");
					}
				}
			}
		}
	}
}