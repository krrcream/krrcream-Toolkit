using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace krrTools.Bindable
{
    /// <summary>
    /// JSON converter for Bindable&lt;T&gt; that serializes/deserializes the Value property.
    /// </summary>
    /// <typeparam name="T">The type of the bindable value.</typeparam>
    public class BindableJsonConverter<T> : JsonConverter<Bindable<T>>
    {
        public override Bindable<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Deserialize the value directly
            T? value = JsonSerializer.Deserialize<T>(ref reader, options);
            return new Bindable<T>(value ?? default!);
        }

        public override void Write(Utf8JsonWriter writer, Bindable<T> value, JsonSerializerOptions options)
        {
            // Serialize only the Value property
            JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}