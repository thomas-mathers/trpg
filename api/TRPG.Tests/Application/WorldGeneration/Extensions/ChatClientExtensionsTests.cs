using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.WorldGeneration.Extensions;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Extensions;

public class ChatClientExtensionsTests
{
    private sealed class TestSchema
    {
        public string Name { get; set; } = "";
    }

    [Fact]
    public async Task GetValidatedJson_ParsesResult_WhenModelWrapsJsonInMarkdownFence()
    {
        // Arrange
        var client = new FakeChatClient
        {
            ChatResponseText = "```json\n{\"name\":\"Aldwyn\"}\n```",
        };

        // Act
        var result = await client.GetValidatedJson<TestSchema>(
            NullLogger.Instance,
            "system prompt",
            "user prompt",
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("Aldwyn", result.Name);
    }

    [Fact]
    public async Task GetValidatedJson_ParsesResult_WhenModelSurroundsJsonWithProse()
    {
        // Arrange
        var client = new FakeChatClient
        {
            ChatResponseText =
                "Here is the JSON you asked for: {\"name\":\"Aldwyn\"} Let me know if you need anything else!",
        };

        // Act
        var result = await client.GetValidatedJson<TestSchema>(
            NullLogger.Instance,
            "system prompt",
            "user prompt",
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("Aldwyn", result.Name);
    }
}
