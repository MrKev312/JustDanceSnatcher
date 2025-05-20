using DSharpPlus.Entities;

using System.Reflection;

namespace JustDanceSnatcher.Helpers;

internal static class EmbedParserHelper
{
	/// <summary>
	/// Parses Discord embed fields and populates properties of a target object.
	/// </summary>
	/// <typeparam name="T">The type of the target object.</typeparam>
	/// <param name="targetObject">The object whose properties will be set.</param>
	/// <param name="fields">The collection of DiscordEmbedFields to parse.</param>
	/// <param name="fieldToPropertyMap">A dictionary mapping Discord field names (e.g., "coachesSmall:") to C# property names (e.g., "coachesSmall").</param>
	public static void ParseFields<T>(T targetObject, IEnumerable<DiscordEmbedField> fields, IReadOnlyDictionary<string, string> fieldToPropertyMap) where T : class
	{
		ArgumentNullException.ThrowIfNull(targetObject);
		if (fields == null)
			return; // No fields to parse

		Type targetType = typeof(T);

		foreach (var field in fields)
		{
			if (fieldToPropertyMap.TryGetValue(field.Name, out var propertyName))
			{
				PropertyInfo? prop = targetType.GetProperty(propertyName);
				if (prop != null && prop.CanWrite)
				{
					// Assuming Program.CleanURL is accessible and suitable for all field values.
					string? cleanedValue = Program.CleanURL(field.Value);
					try
					{
						// Basic type conversion could be added here if needed, e.g., for int, bool.
						// For now, it assumes properties are string or nullable string.
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
				else
				{
					// Console.WriteLine($"Warning: Property '{propertyName}' not found or not writable on type '{targetType.Name}' for field '{field.Name}'.");
				}
			}
		}
	}
}