using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace krrTools.Bindable
{
    /// <summary>
    /// JSON converter for Bindable&lt;T&gt; that serializes/deserializes the Value and Disabled properties.
    /// </summary>
    /// <typeparam name="T">The type of the bindable value.</typeparam>
    public class BindableJsonConverter<T> : JsonConverter<Bindable<T>>
    {
        public override Bindable<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Deserialize as JsonElement to handle object
            var element = JsonSerializer.Deserialize<JsonElement>(ref reader, options);

            if (element.ValueKind == JsonValueKind.Null) return new Bindable<T>();

            if (element.ValueKind == JsonValueKind.Object)
            {
                // Try to get Value
                T value = default!;

                if (element.TryGetProperty("Value", out JsonElement valueProp)) value = JsonSerializer.Deserialize<T>(valueProp.GetRawText(), options) ?? default!;

                var bindable = new Bindable<T>(value);

                // Try to get Disabled
                if (element.TryGetProperty("Disabled", out JsonElement disabledProp) && disabledProp.ValueKind == JsonValueKind.True) bindable.Disabled = true;

                return bindable;
            }
            else
            {
                // Backward compatibility: if it's not object, assume it's the value
                T value = JsonSerializer.Deserialize<T>(element.GetRawText(), options) ?? default!;
                return new Bindable<T>(value);
            }
        }

        public override void Write(Utf8JsonWriter writer, Bindable<T> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Value");
            JsonSerializer.Serialize(writer, value.Value, options);
            writer.WritePropertyName("Disabled");
            JsonSerializer.Serialize(writer, value.Disabled, options);
            writer.WriteEndObject();
        }
    }
}
