using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Configuration;
using BuildingType = TRPG.Domain.Models.BuildingType;

namespace TRPG.Application.WorldGeneration.Generators;

public record DungeonPremiseRequest(
    string DungeonName,
    BuildingType DungeonType,
    string? NearestSettlement,
    IReadOnlyCollection<string> RoomNames,
    string? ExpeditionContext = null
);

public class DungeonPremiseGenerator([FromKeyedServices(LlmRoleKeys.Gameplay)] IChatClient client)
{
    private const string SystemPrompt = """
        You invent the history of one abandoned place in a fantasy world, in a single sentence.

        Say what it was, and what happened to it. Be concrete and specific. Something went wrong
        here, or someone left in a hurry, or someone else has been at it since.

        One sentence. No preamble, no quotation marks, no commentary.
        """;

    public async Task<string> Generate(
        DungeonPremiseRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var nearby =
            request.NearestSettlement == null
                ? ""
                : $"\nThe nearest settlement is {request.NearestSettlement}.";

        var response = await client.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(
                    ChatRole.User,
                    $"""
                    Name: {request.DungeonName}
                    Kind: {request.DungeonType}{nearby}
                    Rooms inside: {string.Join(", ", request.RoomNames)}
                    Established circumstances (must remain true): {request.ExpeditionContext}
                    Describe the place before this expedition arrived, not the expedition or its outcome.
                    Do not invent actionable mechanisms, keys, exits, or rewards.
                    """
                ),
            ],
            cancellationToken: cancellationToken
        );

        return response.Text.Trim();
    }
}
