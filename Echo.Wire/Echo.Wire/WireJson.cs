using System.Text.Json;
using System.Text.Json.Serialization;

namespace Echo.Wire;

public static class WireJson
{
	public static readonly JsonSerializerOptions Options = Create();

	private static JsonSerializerOptions Create()
	{
		return new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Converters = { (JsonConverter)new UInt64StringConverter() }
		};
	}
}
