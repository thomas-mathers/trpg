using System.Text.Json;
using TRPG.Data;
using TRPG.Domain;

namespace TRPG.Tests.Data;

public class GameInstantJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new GameInstantJsonConverter() },
    };

    [Fact]
    public void Deserialize_RestoresTheSerializedInstant_IncludingSubsecondPrecision()
    {
        // Arrange
        var instant = new GameInstant(
            new DateTime(1024, 3, 5, 14, 7, 9, 123, DateTimeKind.Unspecified)
        );
        var json = JsonSerializer.Serialize(instant, Options);

        // Act
        var restored = JsonSerializer.Deserialize<GameInstant>(json, Options);

        // Assert
        Assert.Equal(instant, restored);
    }

    [Fact]
    public void Deserialize_RestoresInstantsUsedAsDictionaryValues()
    {
        // Arrange
        var expiries = new Dictionary<string, GameInstant>
        {
            ["Stunned"] = new(new DateTime(975, 1, 1, 8, 0, 12, DateTimeKind.Unspecified)),
        };
        var json = JsonSerializer.Serialize(expiries, Options);

        // Act
        var restored = JsonSerializer.Deserialize<Dictionary<string, GameInstant>>(json, Options);

        // Assert
        Assert.Equal(expiries, restored);
    }
}
