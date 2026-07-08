using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Echo.Wire;

public sealed class UInt64StringConverter : JsonConverter<ulong>
{
	public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return reader.TokenType switch
		{
			JsonTokenType.String => ulong.Parse(reader.GetString(), CultureInfo.InvariantCulture), 
			JsonTokenType.Number => reader.GetUInt64(), 
			_ => throw new JsonException($"Cannot read ulong from {reader.TokenType}"), 
		};
	}

	public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
	}
}
