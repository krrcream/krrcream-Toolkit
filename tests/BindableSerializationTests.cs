using System.Text.Json;
using krrTools.Bindable;
using Xunit;

namespace krrTools.Tests
{
    public class BindableSerializationTests
    {
        [Fact]
        public void BindableString_SerializesCorrectly()
        {
            // Arrange
            var bindable = new Bindable<string>("test path");

            // Act
            var options = new JsonSerializerOptions();
            options.Converters.Add(new BindableJsonConverter<string>());
            string json = JsonSerializer.Serialize(bindable, options);

            // Assert
            Assert.Contains("\"Value\":\"test path\"", json);
            Assert.Contains("\"Disabled\":false", json);
        }

        [Fact]
        public void BindableString_DeserializesCorrectly()
        {
            // Arrange
            string json = "{\"Value\":\"test path\",\"Disabled\":false}";

            // Act
            var options = new JsonSerializerOptions();
            options.Converters.Add(new BindableJsonConverter<string>());
            var bindable = JsonSerializer.Deserialize<Bindable<string>>(json, options);

            // Assert
            Assert.NotNull(bindable);
            Assert.Equal("test path", bindable.Value);
            Assert.False(bindable.Disabled);
        }

        [Fact]
        public void BindableString_BackwardCompatibility()
        {
            // Arrange
            string json = "\"test path\"";

            // Act
            var options = new JsonSerializerOptions();
            options.Converters.Add(new BindableJsonConverter<string>());
            var bindable = JsonSerializer.Deserialize<Bindable<string>>(json, options);

            // Assert
            Assert.NotNull(bindable);
            Assert.Equal("test path", bindable.Value);
        }
    }
}
